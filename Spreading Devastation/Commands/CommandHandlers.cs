using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using SpreadingDevastation.Models;

namespace SpreadingDevastation
{
    // Partial class containing all command handlers for the /dv and /devastate commands
    public partial class SpreadingDevastationModSystem
    {
        #region Command Handlers

        /// <summary>
        /// Registers the devastate command tree under the provided command name (e.g., "devastate" and alias "dv").
        /// </summary>
        private void RegisterDevastateCommand(ICoreServerAPI api, string commandName)
        {
            api.ChatCommands.Create(commandName)
                .WithDescription("Manage devastation sources")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("add")
                    .WithDescription("Add a devastation source at the block you're looking at")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWordRange("range", "range"),
                              api.ChatCommands.Parsers.OptionalInt("rangeValue"),
                              api.ChatCommands.Parsers.OptionalWordRange("amount", "amount"),
                              api.ChatCommands.Parsers.OptionalInt("amountValue"))
                    .HandleWith(args => HandleAddCommand(args, false))
                .EndSubCommand()
                .BeginSubCommand("heal")
                    .WithDescription("Add a healing source at the block you're looking at")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWordRange("range", "range"),
                              api.ChatCommands.Parsers.OptionalInt("rangeValue"),
                              api.ChatCommands.Parsers.OptionalWordRange("amount", "amount"),
                              api.ChatCommands.Parsers.OptionalInt("amountValue"))
                    .HandleWith(args => HandleAddCommand(args, true))
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove devastation sources")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("target"))
                    .HandleWith(HandleRemoveCommand)
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("List devastation sources")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("countOrSummary"))
                    .HandleWith(HandleListCommand)
                .EndSubCommand()
                .BeginSubCommand("maxsources")
                    .WithDescription("Set maximum number of sources")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("count"))
                    .HandleWith(HandleMaxSourcesCommand)
                .EndSubCommand()
                .BeginSubCommand("maxattempts")
                    .WithDescription("Set max failed spawn attempts before saturation")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("count"))
                    .HandleWith(HandleMaxAttemptsCommand)
                .EndSubCommand()
                .BeginSubCommand("aircontact")
                    .WithDescription("Toggle surface spreading (require air contact for new sources)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("onoff"))
                    .HandleWith(HandleAirContactCommand)
                .EndSubCommand()
                .BeginSubCommand("markers")
                    .WithDescription("Toggle magenta source markers for debugging")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("onoff"))
                    .HandleWith(HandleMarkersCommand)
                .EndSubCommand()
                .BeginSubCommand("miny")
                    .WithDescription("Set minimum Y level for new sources")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .HandleWith(HandleMinYCommand)
                .EndSubCommand()
                .BeginSubCommand("stop")
                    .WithDescription("Pause all devastation spreading")
                    .HandleWith(args => { isPaused = true; return TextCommandResult.Success("Devastation spreading STOPPED. Use '/devastate start' to resume."); })
                .EndSubCommand()
                .BeginSubCommand("start")
                    .WithDescription("Resume devastation spreading")
                    .HandleWith(args => { isPaused = false; return TextCommandResult.Success("Devastation spreading STARTED."); })
                .EndSubCommand()
                .BeginSubCommand("status")
                    .WithDescription("Show current devastation status")
                    .HandleWith(HandleStatusCommand)
                .EndSubCommand()
                .BeginSubCommand("speed")
                    .WithDescription("Set devastation spread speed multiplier")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("multiplier"))
                    .HandleWith(HandleSpeedCommand)
                .EndSubCommand()
                .BeginSubCommand("chunk")
                    .WithDescription("Mark the chunk you're looking at as devastated, or configure chunk settings")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleChunkCommand)
                .EndSubCommand()
                .BeginSubCommand("riftward")
                    .WithDescription("Configure rift ward settings (speed, list, info)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleRiftWardCommand)
                .EndSubCommand()
                .BeginSubCommand("fog")
                    .WithDescription("Configure devastation fog and sky effect")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("setting"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleFogCommand)
                .EndSubCommand()
                .BeginSubCommand("reset")
                    .WithDescription("Reset all config values to defaults")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("confirm"))
                    .HandleWith(HandleResetCommand)
                .EndSubCommand()
                .BeginSubCommand("blockinfo")
                    .WithDescription("Show block code of the block you're looking at (for debugging)")
                    .HandleWith(HandleBlockInfoCommand)
                .EndSubCommand()
                .BeginSubCommand("insanity")
                    .WithDescription("Manage animal insanity in devastated chunks")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleInsanityCommand)
                .EndSubCommand()
                .BeginSubCommand("testsuite")
                    .WithDescription("Run automated tests on mod features")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("testname"))
                    .HandleWith(HandleTestSuiteCommand)
                .EndSubCommand();
        }

        private TextCommandResult HandleBlockInfoCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Player not found");

            var blockSel = player.CurrentBlockSelection;
            if (blockSel == null) return TextCommandResult.Error("Not looking at a block");

            Block block = sapi.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block == null) return TextCommandResult.Error("Block not found");

            string fullCode = block.Code.ToString();
            string path = block.Code.Path;
            string domain = block.Code.Domain;

            bool canDevastate = TryGetDevastatedForm(block, out string devForm, out string regenTo);

            var lines = new List<string>
            {
                "=== Block Info ===",
                $"Full code: {fullCode}",
                $"Domain: {domain}",
                $"Path: {path}",
                $"Block ID: {block.Id}",
                $"Can devastate: {canDevastate}",
            };

            if (canDevastate)
            {
                lines.Add($"Devastated form: {devForm}");
                lines.Add($"Regenerates to: {regenTo}");
            }

            return SendChatLines(args, lines, "Block info sent to chat");
        }

        private TextCommandResult HandleInsanityCommand(TextCommandCallingArgs args)
        {
            string action = args.Parsers[0].GetValue() as string ?? "";
            string value = args.Parsers[1].GetValue() as string ?? "";

            switch (action.ToLowerInvariant())
            {
                case "enable":
                case "on":
                    config.AnimalInsanityEnabled = true;
                    SaveConfig();
                    return TextCommandResult.Success("Animal insanity ENABLED - animals in devastated chunks will go hostile");

                case "disable":
                case "off":
                    config.AnimalInsanityEnabled = false;
                    SaveConfig();
                    return TextCommandResult.Success("Animal insanity DISABLED - animals will behave normally");

                case "clear":
                case "cure":
                    int cured = ClearAllInsanity();
                    return TextCommandResult.Success($"Cleared insanity from {cured} entities (Note: they may still be aggressive until they reset)");

                case "chance":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current insanity chance: {config.AnimalInsanityChance:P0}. Use '/dv insanity chance [0.0-1.0]' to set.");
                    }
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double newChance))
                    {
                        config.AnimalInsanityChance = Math.Clamp(newChance, 0.0, 1.0);
                        SaveConfig();
                        return TextCommandResult.Success($"Insanity chance set to {config.AnimalInsanityChance:P0}");
                    }
                    return TextCommandResult.Error("Invalid number. Use a value between 0.0 and 1.0");

                case "interval":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current check interval: {config.AnimalInsanityCheckIntervalSeconds:F1}s. Use '/dv insanity interval [seconds]' to set.");
                    }
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double newInterval))
                    {
                        config.AnimalInsanityCheckIntervalSeconds = Math.Clamp(newInterval, 0.5, 60.0);
                        SaveConfig();
                        return TextCommandResult.Success($"Insanity check interval set to {config.AnimalInsanityCheckIntervalSeconds:F1} seconds");
                    }
                    return TextCommandResult.Error("Invalid number. Use a value between 0.5 and 60.0");

                case "radius":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current search radius: {config.AnimalInsanitySearchRadius} blocks. Use '/dv insanity radius [blocks]' to set.");
                    }
                    if (int.TryParse(value, out int newRadius))
                    {
                        config.AnimalInsanitySearchRadius = Math.Clamp(newRadius, 8, 256);
                        SaveConfig();
                        return TextCommandResult.Success($"Insanity search radius set to {config.AnimalInsanitySearchRadius} blocks");
                    }
                    return TextCommandResult.Error("Invalid number for radius");

                case "include":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current include patterns: {config.AnimalInsanityEntityCodes}");
                    }
                    config.AnimalInsanityEntityCodes = value;
                    insanityIncludePatterns = null; // Clear cache to force re-parse
                    SaveConfig();
                    return TextCommandResult.Success($"Insanity include patterns set to: {value}");

                case "exclude":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current exclude patterns: {config.AnimalInsanityExcludeCodes}");
                    }
                    config.AnimalInsanityExcludeCodes = value;
                    insanityExcludePatterns = null; // Clear cache to force re-parse
                    SaveConfig();
                    return TextCommandResult.Success($"Insanity exclude patterns set to: {value}");

                case "":
                case "info":
                case "status":
                default:
                    var lines = new List<string>
                    {
                        "=== Animal Insanity Settings ===",
                        $"Enabled: {(config.AnimalInsanityEnabled ? "YES" : "NO")}",
                        $"Insane entities tracked: {insaneEntityIds.Count}",
                        $"Check interval: {config.AnimalInsanityCheckIntervalSeconds:F1}s",
                        $"Search radius: {config.AnimalInsanitySearchRadius} blocks",
                        $"Insanity chance: {config.AnimalInsanityChance:P0}",
                        "",
                        "Include patterns: " + config.AnimalInsanityEntityCodes,
                        "Exclude patterns: " + config.AnimalInsanityExcludeCodes,
                        "",
                        "Commands:",
                        "  /dv insanity [on|off] - Enable or disable",
                        "  /dv insanity clear - Cure all insane animals",
                        "  /dv insanity chance [0-1] - Set insanity chance",
                        "  /dv insanity interval [sec] - Set check interval",
                        "  /dv insanity radius [blocks] - Set search radius",
                        "  /dv insanity include [codes] - Set affected animals",
                        "  /dv insanity exclude [codes] - Set immune animals"
                    };
                    return SendChatLines(args, lines, "Insanity info sent to chat");
            }
        }

        private TextCommandResult HandleSpeedCommand(TextCommandCallingArgs args)
        {
            string rawArg = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrWhiteSpace(rawArg))
            {
                return SendChatLines(args, new[]
                {
                    $"Current devastation speed: {config.SpeedMultiplier:F2}x",
                    "Usage: /dv speed [multiplier] (e.g., 0.5 for half speed, 5 for 5x speed)"
                }, "Speed info sent to chat (scrollable)");
            }

            if (!double.TryParse(rawArg, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv speed [multiplier] (e.g., 0.5, 1, 5)");
            }

            double newSpeed = Math.Clamp(parsedSpeed, 0.01, 100.0);
            config.SpeedMultiplier = newSpeed;
            SaveConfig();
            return TextCommandResult.Success($"Devastation speed set to {config.SpeedMultiplier:F2}x");
        }

        private TextCommandResult HandleRiftWardCommand(TextCommandCallingArgs args)
        {
            string action = args.Parsers[0].GetValue() as string ?? "";
            string value = args.Parsers[1].GetValue() as string ?? "";

            switch (action.ToLowerInvariant())
            {
                case "speed":
                    return HandleRiftWardSpeedCommand(value);

                case "list":
                    return HandleRiftWardListCommand(args);

                case "rate":
                    return HandleRiftWardRateCommand(value);

                case "mode":
                    return HandleRiftWardModeCommand(value);

                case "radius":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Rift ward protection radius: {config.RiftWardProtectionRadius} blocks. Use '/dv riftward radius [blocks]' to set.");
                    }
                    if (int.TryParse(value, out int newRadius))
                    {
                        config.RiftWardProtectionRadius = Math.Clamp(newRadius, 8, 1024);
                        SaveConfig();
                        RebuildProtectedChunkCache();
                        return TextCommandResult.Success($"Rift ward protection radius set to {config.RiftWardProtectionRadius} blocks");
                    }
                    return TextCommandResult.Error("Invalid number for radius");

                case "":
                case "info":
                case "status":
                    double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
                    string speedSource = config.RiftWardSpeedMultiplier > 0 ? "custom" : "global";
                    string healingMode = GetEffectiveCleanMode();
                    return SendChatLines(args, new[]
                    {
                        "=== Rift Ward Settings ===",
                        $"Protection radius: {config.RiftWardProtectionRadius} blocks",
                        $"Healing enabled: {config.RiftWardHealingEnabled}",
                        $"Healing mode: {healingMode} (raster=efficient scan, radial=outward, random=anywhere)",
                        $"Base healing rate: {config.RiftWardHealingRate:F1} blocks per sec",
                        $"Speed multiplier: {effectiveSpeed:F2}x ({speedSource})",
                        $"Effective rate: {config.RiftWardHealingRate * effectiveSpeed:F1} blocks per sec",
                        $"Active rift wards: {activeRiftWards?.Count ?? 0}",
                        "",
                        "Commands:",
                        "  /dv riftward radius [blocks] - Set protection radius",
                        "  /dv riftward speed [multiplier] - Set healing speed (or 'global' to use /dv speed)",
                        "  /dv riftward rate [value] - Set base healing rate",
                        "  /dv riftward mode [raster|radial|random] - Set healing pattern mode",
                        "  /dv riftward list - Show all tracked rift wards"
                    }, "Rift ward info sent to chat");

                default:
                    return TextCommandResult.Error($"Unknown riftward action: {action}. Use: radius, speed, rate, mode, list, or info");
            }
        }

        private TextCommandResult HandleRiftWardSpeedCommand(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
                string speedSource = config.RiftWardSpeedMultiplier > 0 ? "custom" : "global";
                return TextCommandResult.Success($"Rift ward healing speed: {effectiveSpeed:F2}x ({speedSource}). Use '/dv riftward speed [multiplier]' to set, or 'global' to use devastation speed.");
            }

            if (value.ToLowerInvariant() == "global" || value.ToLowerInvariant() == "default")
            {
                config.RiftWardSpeedMultiplier = -1;
                SaveConfig();
                return TextCommandResult.Success($"Rift ward healing now uses global devastation speed ({config.SpeedMultiplier:F2}x)");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv riftward speed [multiplier] (e.g., 5, 10, or 'global')");
            }

            double newSpeed = Math.Clamp(parsedSpeed, 0.01, 1000.0);
            config.RiftWardSpeedMultiplier = newSpeed;
            SaveConfig();
            return TextCommandResult.Success($"Rift ward healing speed set to {config.RiftWardSpeedMultiplier:F2}x (effective rate: {config.RiftWardHealingRate * newSpeed:F1} blocks per sec)");
        }

        private TextCommandResult HandleRiftWardRateCommand(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TextCommandResult.Success($"Rift ward base healing rate: {config.RiftWardHealingRate:F1} blocks per sec. Use '/dv riftward rate [value]' to set.");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRate))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv riftward rate [value] (e.g., 10, 50, 100)");
            }

            double newRate = Math.Clamp(parsedRate, 0.1, 10000.0);
            config.RiftWardHealingRate = newRate;
            SaveConfig();
            double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
            return TextCommandResult.Success($"Rift ward base healing rate set to {config.RiftWardHealingRate:F1} blocks per sec (effective: {config.RiftWardHealingRate * effectiveSpeed:F1} blocks per sec)");
        }

        private TextCommandResult HandleRiftWardModeCommand(string value)
        {
            string currentMode = GetEffectiveCleanMode();

            if (string.IsNullOrWhiteSpace(value))
            {
                return TextCommandResult.Success($"Rift ward healing mode: {currentMode}. Use '/dv riftward mode [raster|radial|random]' to change.");
            }

            switch (value.ToLowerInvariant())
            {
                case "raster":
                case "scan":
                case "s":
                    config.RiftWardCleanMode = "raster";
                    SaveConfig();
                    // Reset all ward scan progress when switching to raster mode
                    foreach (var ward in activeRiftWards)
                    {
                        ward.CurrentCleanRadius = 0;
                        ward.ScanX = 0;
                        ward.ScanY = 0;
                        ward.ScanZ = 0;
                        ward.RasterScanComplete = false;
                    }
                    return TextCommandResult.Success("Rift ward healing mode set to raster. Expanding globe scan - efficient and thorough.");

                case "radial":
                case "r":
                    config.RiftWardCleanMode = "radial";
                    SaveConfig();
                    // Reset all ward clean progress when switching to radial mode
                    foreach (var ward in activeRiftWards)
                    {
                        ward.CurrentCleanRadius = 0;
                        ward.RadialCleanFailures = 0;
                    }
                    return TextCommandResult.Success("Rift ward healing mode set to radial. Devastation will be cleared outward from the center (legacy mode).");

                case "random":
                case "rand":
                    config.RiftWardCleanMode = "random";
                    SaveConfig();
                    return TextCommandResult.Success("Rift ward healing mode set to random. Devastation will be cleared randomly within protection radius.");

                default:
                    return TextCommandResult.Error($"Unknown mode: {value}. Use 'raster', 'radial', or 'random'.");
            }
        }

        private TextCommandResult HandleRiftWardListCommand(TextCommandCallingArgs args)
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0)
            {
                return TextCommandResult.Success("No rift wards are currently tracked.");
            }

            string mode = GetEffectiveCleanMode();
            var lines = new List<string> { $"=== Tracked Rift Wards ({activeRiftWards.Count}) - Mode: {mode} ===" };
            foreach (var ward in activeRiftWards)
            {
                bool isActive = IsRiftWardActive(ward.Pos);
                string status = isActive ? "ACTIVE" : "inactive";
                string progressInfo = "";
                if (mode == "raster")
                {
                    string scanStatus = ward.RasterScanComplete ? "complete" : $"radius {ward.CurrentCleanRadius}/{config.RiftWardProtectionRadius}";
                    progressInfo = $", scan: {scanStatus}";
                }
                else if (mode == "radial")
                {
                    progressInfo = $", cleaned {ward.MaxCleanRadiusReached}/{config.RiftWardProtectionRadius}";
                }
                lines.Add($"  {ward.Pos} - {status}, healed {ward.BlocksHealed} blocks{progressInfo}");
            }
            return SendChatLines(args, lines, "Rift ward list sent to chat");
        }

        #endregion
    }
}
