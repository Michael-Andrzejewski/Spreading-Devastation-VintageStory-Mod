using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SpreadingDevastation.Models;
using SpreadingDevastation.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SpreadingDevastation.Commands
{
    /// <summary>
    /// Handles all chat commands for the Spreading Devastation mod.
    /// Single Responsibility: Command parsing and response formatting.
    /// </summary>
    public class CommandHandler
    {
        private readonly ICoreServerAPI _api;
        private readonly SpreadingDevastationConfig _config;
        private readonly SourceManager _sourceManager;
        private readonly HauntingService _hauntingService;
        private readonly List<RegrowingBlock> _regrowingBlocks;
        private readonly Action _saveConfig;
        
        // Pause state is managed externally by the mod system
        private Func<bool> _getIsPaused;
        private Action<bool> _setIsPaused;

        public CommandHandler(
            ICoreServerAPI api,
            SpreadingDevastationConfig config,
            SourceManager sourceManager,
            HauntingService hauntingService,
            List<RegrowingBlock> regrowingBlocks,
            Action saveConfig,
            Func<bool> getIsPaused,
            Action<bool> setIsPaused)
        {
            _api = api;
            _config = config;
            _sourceManager = sourceManager;
            _hauntingService = hauntingService;
            _regrowingBlocks = regrowingBlocks;
            _saveConfig = saveConfig;
            _getIsPaused = getIsPaused;
            _setIsPaused = setIsPaused;
        }

        /// <summary>
        /// Registers all commands.
        /// </summary>
        public void RegisterCommands()
        {
            RegisterDevastateCommand("devastate");
            RegisterDevastateCommand("dv");
            
            _api.ChatCommands.Create("devastationspeed")
                .WithDescription("Set devastation spread speed multiplier")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(_api.ChatCommands.Parsers.OptionalWord("multiplier"))
                .HandleWith(HandleSpeedCommand);
            
            _api.ChatCommands.Create("devastationconfig")
                .WithDescription("Reload configuration from file")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => {
                    // This is handled by the mod system
                    return TextCommandResult.Success("Use the mod system to reload config");
                });
        }

        private void RegisterDevastateCommand(string commandName)
        {
            _api.ChatCommands.Create(commandName)
                .WithDescription("Manage devastation sources")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("add")
                    .WithDescription("Add a devastation source at the block you're looking at")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalWordRange("range", "range"),
                              _api.ChatCommands.Parsers.OptionalInt("rangeValue"),
                              _api.ChatCommands.Parsers.OptionalWordRange("amount", "amount"),
                              _api.ChatCommands.Parsers.OptionalInt("amountValue"))
                    .HandleWith(args => HandleAddCommand(args, false))
                .EndSubCommand()
                .BeginSubCommand("heal")
                    .WithDescription("Add a healing source at the block you're looking at")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalWordRange("range", "range"),
                              _api.ChatCommands.Parsers.OptionalInt("rangeValue"),
                              _api.ChatCommands.Parsers.OptionalWordRange("amount", "amount"),
                              _api.ChatCommands.Parsers.OptionalInt("amountValue"))
                    .HandleWith(args => HandleAddCommand(args, true))
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove devastation sources")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalWord("target"))
                    .HandleWith(HandleRemoveCommand)
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("List devastation sources")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalWord("countOrSummary"))
                    .HandleWith(HandleListCommand)
                .EndSubCommand()
                .BeginSubCommand("maxsources")
                    .WithDescription("Set maximum number of sources")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalInt("count"))
                    .HandleWith(HandleMaxSourcesCommand)
                .EndSubCommand()
                .BeginSubCommand("maxattempts")
                    .WithDescription("Set max failed spawn attempts before saturation")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalInt("count"))
                    .HandleWith(HandleMaxAttemptsCommand)
                .EndSubCommand()
                .BeginSubCommand("aircontact")
                    .WithDescription("Toggle surface spreading (require air contact for new sources)")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalWord("onoff"))
                    .HandleWith(args => HandleToggleCommand(args,
                        () => _config.RequireSourceAirContact,
                        v => _config.RequireSourceAirContact = v,
                        "Surface spreading",
                        "new metastasis sources must be adjacent to air",
                        "metastasis sources can spawn anywhere"))
                .EndSubCommand()
                .BeginSubCommand("markers")
                    .WithDescription("Toggle magenta source markers for debugging")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalWord("onoff"))
                    .HandleWith(args => HandleToggleCommand(args,
                        () => _config.ShowSourceMarkers,
                        v => _config.ShowSourceMarkers = v,
                        "Source markers",
                        null, null))
                .EndSubCommand()
                .BeginSubCommand("miny")
                    .WithDescription("Set minimum Y level for new sources")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalInt("level"))
                    .HandleWith(HandleMinYCommand)
                .EndSubCommand()
                .BeginSubCommand("haunting")
                    .WithDescription("Toggle player haunting (devastation creeps toward players)")
                    .WithArgs(_api.ChatCommands.Parsers.OptionalWord("onoff"))
                    .HandleWith(HandleHauntingCommand)
                .EndSubCommand()
                .BeginSubCommand("stop")
                    .WithDescription("Pause all devastation spreading")
                    .HandleWith(args => {
                        _setIsPaused(true);
                        return TextCommandResult.Success("Devastation spreading STOPPED. Use '/devastate start' to resume.");
                    })
                .EndSubCommand()
                .BeginSubCommand("start")
                    .WithDescription("Resume devastation spreading")
                    .HandleWith(args => {
                        _setIsPaused(false);
                        return TextCommandResult.Success("Devastation spreading STARTED.");
                    })
                .EndSubCommand()
                .BeginSubCommand("status")
                    .WithDescription("Show current devastation status")
                    .HandleWith(HandleStatusCommand)
                .EndSubCommand();
        }

        #region Command Handlers

        private TextCommandResult HandleSpeedCommand(TextCommandCallingArgs args)
        {
            string rawArg = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrWhiteSpace(rawArg))
            {
                return SendChatLines(args, new[]
                {
                    $"Current devastation speed: {_config.SpeedMultiplier:F2}x",
                    "Usage: /devastationspeed <multiplier> (e.g., 0.5 for half speed, 5 for 5x speed)"
                });
            }

            if (!double.TryParse(rawArg, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                return TextCommandResult.Error("Invalid number. Usage: /devastationspeed <multiplier> (e.g., 0.5, 1, 5)");
            }

            _config.SpeedMultiplier = Math.Clamp(parsedSpeed, 0.01, 100.0);
            _saveConfig();
            return TextCommandResult.Success($"Devastation speed set to {_config.SpeedMultiplier:F2}x");
        }

        private TextCommandResult HandleAddCommand(TextCommandCallingArgs args, bool isHealing)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error($"Look at a block to mark it as a {(isHealing ? "healing" : "devastation")} source");
            }

            BlockPos pos = blockSel.Position.Copy();

            if (_sourceManager.ExistsAtPosition(pos))
            {
                return TextCommandResult.Error("This block is already a source (use remove to change it)");
            }

            var (range, amount) = ParseRangeAndAmount(args);

            _sourceManager.EnsureCapacity(1);
            _sourceManager.CreateSource(pos, range, amount, isHealing, isProtected: true);

            string action = isHealing ? "healing" : "devastation";
            return TextCommandResult.Success($"Added {action} source at {pos} (range: {range}, amount: {amount} blocks per tick)");
        }

        private TextCommandResult HandleRemoveCommand(TextCommandCallingArgs args)
        {
            string removeArg = args.Parsers[0].GetValue() as string;

            if (removeArg == "all")
            {
                int count = _sourceManager.RemoveAll();
                return TextCommandResult.Success($"Removed all {count} devastation sources");
            }
            else if (removeArg == "saturated")
            {
                int removed = _sourceManager.RemoveSaturated();
                return TextCommandResult.Success($"Removed {removed} saturated devastation sources");
            }
            else if (removeArg == "metastasis")
            {
                int removed = _sourceManager.RemoveMetastasis();
                return TextCommandResult.Success($"Removed {removed} metastasis sources (kept original sources)");
            }
            else
            {
                IServerPlayer player = args.Caller.Player as IServerPlayer;
                if (player == null) return TextCommandResult.Error("This command must be run by a player");

                BlockSelection blockSel = player.CurrentBlockSelection;
                if (blockSel == null)
                {
                    return TextCommandResult.Error("Look at a block to remove it as a devastation source, or use 'remove all/saturated/metastasis'");
                }

                int removed = _sourceManager.RemoveAtPosition(blockSel.Position);
                if (removed > 0)
                {
                    return TextCommandResult.Success($"Removed devastation source at {blockSel.Position}");
                }
                else
                {
                    return TextCommandResult.Error("No devastation source found at this location");
                }
            }
        }

        private TextCommandResult HandleListCommand(TextCommandCallingArgs args)
        {
            var sources = _sourceManager.GetAllSources();
            if (sources.Count == 0)
            {
                return TextCommandResult.Success("No manual devastation sources set");
            }

            string arg = args.Parsers[0].GetValue() as string;

            if (arg == "summary")
            {
                return ShowListSummary(args);
            }

            int limit = 10;
            if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out int parsedLimit))
            {
                limit = Math.Clamp(parsedLimit, 1, 100);
            }

            var stats = _sourceManager.GetStatistics();
            var lines = new List<string>
            {
                $"Devastation sources ({stats.Total}/{stats.MaxSources} cap, {stats.Total - stats.Metastasis} original, {stats.Metastasis} metastasis, {stats.Saturated} saturated):"
            };

            var sortedSources = sources
                .OrderBy(s => s.IsSaturated ? 1 : 0)
                .ThenBy(s => s.GenerationLevel)
                .ThenByDescending(s => s.BlocksDevastatedTotal)
                .Take(limit)
                .ToList();

            foreach (var source in sortedSources)
            {
                string type = source.IsHealing ? "HEAL" : "DEV";
                string genInfo = source.IsMetastasis ? $"G{source.GenerationLevel}" : "Orig";
                string statusLabel = GetSourceStatusLabel(source);
                string idInfo = !string.IsNullOrEmpty(source.SourceId) ? $"#{source.SourceId}" : "";

                lines.Add($"  [{type}] [{genInfo}] {statusLabel}{idInfo} {source.Pos} R:{source.CurrentRadius:F0}/{source.Range} Tot:{source.BlocksDevastatedTotal}");
            }

            if (sources.Count > limit)
            {
                lines.Add($"  ... and {sources.Count - limit} more. Use '/devastate list {limit + 10}' or '/devastate list summary'");
            }

            return SendChatLines(args, lines);
        }

        private TextCommandResult ShowListSummary(TextCommandCallingArgs args)
        {
            var stats = _sourceManager.GetStatistics();

            var lines = new List<string>
            {
                $"=== Devastation Summary ({stats.Total}/{stats.MaxSources} cap) ===",
                $"  Protected (manual): {stats.Protected} (never auto-removed)",
                $"  Metastasis children: {stats.Metastasis}",
                $"  Healing sources: {stats.Healing}",
                $"  Growing: {stats.Growing}",
                $"  Seeding (ready to spawn): {stats.Seeding}",
                $"  Saturated (done): {stats.Saturated}"
            };

            var byGeneration = _sourceManager.GetSourcesByGeneration().ToList();
            if (byGeneration.Count > 0)
            {
                lines.Add("  By Generation:");
                foreach (var gen in byGeneration)
                {
                    int growing = gen.Count(s => !s.IsSaturated && (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range));
                    int seeding = gen.Count(s => s.IsReadyToSeed);
                    int sat = gen.Count(s => s.IsSaturated);
                    long totalBlocks = gen.Sum(s => (long)s.BlocksDevastatedTotal);
                    string genLabel = gen.Key == 0 ? "Origin" : $"Gen {gen.Key}";
                    lines.Add($"    {genLabel}: {gen.Count()} ({growing} growing, {seeding} seeding, {sat} saturated) - {totalBlocks:N0} blocks");
                }
            }

            lines.Add($"  Total blocks devastated: {stats.TotalBlocksDevastated:N0}");

            if (stats.Total >= stats.MaxSources)
            {
                lines.Add("  ⚠ At source cap - oldest sources will be removed for new metastasis");
            }

            return SendChatLines(args, lines);
        }

        private TextCommandResult HandleMaxSourcesCommand(TextCommandCallingArgs args)
        {
            int? maxArg = args.Parsers[0].GetValue() as int?;

            if (!maxArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current max sources cap: {_config.MaxSources}",
                    $"Active sources: {_sourceManager.Count}/{_config.MaxSources}",
                    "Usage: /devastate maxsources <number> (e.g., 20, 50, 100)"
                });
            }

            _config.MaxSources = Math.Clamp(maxArg.Value, 1, 1000);
            _saveConfig();

            string warning = _sourceManager.Count >= _config.MaxSources
                ? $"\nWarning: Already at or above cap ({_sourceManager.Count} sources). No new metastasis will spawn."
                : "";

            return TextCommandResult.Success($"Max sources cap set to {_config.MaxSources}{warning}");
        }

        private TextCommandResult HandleMaxAttemptsCommand(TextCommandCallingArgs args)
        {
            int? attemptsArg = args.Parsers[0].GetValue() as int?;

            if (!attemptsArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current max failed spawn attempts: {_config.MaxFailedSpawnAttempts}",
                    "Usage: /devastate maxattempts <number> (e.g., 5, 10, 20)"
                });
            }

            _config.MaxFailedSpawnAttempts = Math.Clamp(attemptsArg.Value, 1, 100);
            _saveConfig();

            return TextCommandResult.Success($"Max failed spawn attempts set to {_config.MaxFailedSpawnAttempts}");
        }

        private TextCommandResult HandleToggleCommand(
            TextCommandCallingArgs args,
            Func<bool> getValue,
            Action<bool> setValue,
            string settingName,
            string enabledDescription,
            string disabledDescription)
        {
            string onOff = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrEmpty(onOff))
            {
                string status = getValue() ? "ON" : "OFF";
                return SendChatLines(args, new[]
                {
                    $"{settingName}: {status}",
                    $"Usage: /devastate {settingName.ToLower().Replace(" ", "")} [on|off]"
                });
            }

            bool? newValue = ParseToggleValue(onOff);
            if (!newValue.HasValue)
            {
                return TextCommandResult.Error("Invalid value. Use: on, off, 1, 0, true, or false");
            }

            setValue(newValue.Value);
            _saveConfig();

            string message = $"{settingName} {(newValue.Value ? "ENABLED" : "DISABLED")}";
            if (newValue.Value && enabledDescription != null)
            {
                message += $" - {enabledDescription}";
            }
            else if (!newValue.Value && disabledDescription != null)
            {
                message += $" - {disabledDescription}";
            }

            return TextCommandResult.Success(message);
        }

        private TextCommandResult HandleMinYCommand(TextCommandCallingArgs args)
        {
            int? levelArg = args.Parsers[0].GetValue() as int?;

            if (!levelArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current minimum Y level: {_config.MinYLevel}",
                    "Usage: /devastate miny <level> (e.g., 0, -64, 50)"
                });
            }

            _config.MinYLevel = levelArg.Value;
            _saveConfig();

            return TextCommandResult.Success($"Minimum Y level for new sources set to {_config.MinYLevel}");
        }

        private TextCommandResult HandleHauntingCommand(TextCommandCallingArgs args)
        {
            string onOff = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrEmpty(onOff))
            {
                var status = _hauntingService.GetStatus();
                string statusStr = status.Enabled ? "ON" : "OFF";
                string burstStatus = status.BurstRemaining > 0
                    ? $"RELOCATING ({status.BurstRemaining} moves remaining)"
                    : $"waiting ({status.SecondsUntilNextBurst:F1}s until next)";

                string playerInfo = status.NearestPlayerDistance.HasValue
                    ? $"Nearest player: {status.NearestPlayerDistance:F0} blocks (min: {_config.HauntingMinDistance})"
                    : "No players online";

                return SendChatLines(args, new[]
                {
                    $"=== Player Haunting: {statusStr} ===",
                    "Mode: Source relocation (moves existing sources toward players)",
                    $"Interval: {_config.HauntingIntervalSeconds}s (effective: {_config.HauntingIntervalSeconds / _config.SpeedMultiplier:F1}s)",
                    $"Burst size: {_config.HauntingBurstCount} relocations",
                    $"Leap fraction: {_config.HauntingLeapFraction * 100:F0}% of distance",
                    $"Max leap: {_config.HauntingMaxLeapDistance} blocks",
                    $"Angular variance: ±{_config.HauntingAngleVariance}°",
                    $"Movable sources: {status.MovableSourceCount} (excludes protected/saturated/healing)",
                    $"Status: {burstStatus}",
                    playerInfo,
                    "Usage: /dv haunting [on|off|force]"
                });
            }

            if (onOff.Equals("force", StringComparison.OrdinalIgnoreCase))
            {
                _hauntingService.ForceBurst();
                return TextCommandResult.Success($"Forced haunting burst! Moving {_config.HauntingBurstCount} sources toward nearest player.");
            }

            bool? newValue = ParseToggleValue(onOff);
            if (!newValue.HasValue)
            {
                return TextCommandResult.Error("Invalid value. Use: on, off, force, 1, 0, true, or false");
            }

            _config.EnablePlayerHaunting = newValue.Value;
            if (!newValue.Value)
            {
                _hauntingService.Reset();
            }
            _saveConfig();

            return TextCommandResult.Success($"Player haunting {(newValue.Value ? "ENABLED - devastation sources will relocate toward players" : "DISABLED")}");
        }

        private TextCommandResult HandleStatusCommand(TextCommandCallingArgs args)
        {
            string statusText = _getIsPaused() ? "PAUSED" : "RUNNING";
            var hauntingStatus = _hauntingService.GetStatus();

            string hauntingStr;
            if (!hauntingStatus.Enabled)
            {
                hauntingStr = "OFF";
            }
            else if (hauntingStatus.BurstRemaining > 0)
            {
                hauntingStr = $"ON (burst: {hauntingStatus.BurstRemaining} remaining)";
            }
            else
            {
                hauntingStr = $"ON (next burst in {hauntingStatus.SecondsUntilNextBurst:F1}s)";
            }

            var lines = new List<string>
            {
                $"Devastation status: {statusText}",
                $"Speed multiplier: {_config.SpeedMultiplier:F2}x",
                $"Active sources: {_sourceManager.Count}/{_config.MaxSources}",
                $"Tracked blocks for regen: {_regrowingBlocks?.Count ?? 0}",
                $"Surface spreading: {(_config.RequireSourceAirContact ? "ON" : "OFF")}",
                $"Min Y level: {_config.MinYLevel}",
                $"Child spawn delay: {_config.ChildSpawnDelaySeconds}s",
                $"Max failed attempts: {_config.MaxFailedSpawnAttempts}",
                $"Player haunting: {hauntingStr}"
            };

            return SendChatLines(args, lines);
        }

        #endregion

        #region Helpers

        private (int range, int amount) ParseRangeAndAmount(TextCommandCallingArgs args)
        {
            int range = _config.DefaultRange;
            int amount = _config.DefaultAmount;

            for (int i = 0; i < args.Parsers.Count - 1; i += 2)
            {
                string keyword = args.Parsers[i].GetValue() as string;
                if (string.IsNullOrEmpty(keyword)) continue;

                int? value = args.Parsers[i + 1].GetValue() as int?;
                if (!value.HasValue) continue;

                if (keyword == "range")
                {
                    range = Math.Clamp(value.Value, 1, 128);
                }
                else if (keyword == "amount")
                {
                    amount = Math.Clamp(value.Value, 1, 100);
                }
            }

            return (range, amount);
        }

        private bool? ParseToggleValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            string lower = value.ToLower();
            if (lower == "on" || value == "1" || lower == "true") return true;
            if (lower == "off" || value == "0" || lower == "false") return false;
            return null;
        }

        private string GetSourceStatusLabel(DevastationSource source)
        {
            if (source.IsSaturated)
            {
                return "[saturated]";
            }

            if (source.ChildrenSpawned > 0)
            {
                return $"[seeding {source.ChildrenSpawned}]";
            }
            else if (source.IsReadyToSeed)
            {
                return "[seeding]";
            }
            else
            {
                return "[growing]";
            }
        }

        private TextCommandResult SendChatLines(TextCommandCallingArgs args, IEnumerable<string> lines)
        {
            var player = args?.Caller?.Player as IServerPlayer;
            var safeLines = lines?.Where(l => !string.IsNullOrWhiteSpace(l)).ToList() ?? new List<string>();

            if (player != null)
            {
                foreach (string line in safeLines)
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, line, EnumChatType.CommandSuccess);
                }

                return TextCommandResult.Success("Details sent to chat (scroll to view)");
            }

            return TextCommandResult.Success(string.Join("\n", safeLines));
        }

        #endregion
    }
}

