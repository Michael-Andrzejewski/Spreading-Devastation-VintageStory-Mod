using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using SpreadingDevastation.Models;

namespace SpreadingDevastation
{
    // Partial class containing source management command handlers
    public partial class SpreadingDevastationModSystem
    {
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

            // Check if already exists
            if (devastationSources.Any(s => s.Pos.Equals(pos)))
            {
                return TextCommandResult.Error("This block is already a source (use remove to change it)");
            }

            // Parse optional parameters from remaining args
            int range = config.DefaultRange;
            int amount = config.DefaultAmount;

            // Parse arguments - they come as pairs: "range" <value> "amount" <value>
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

            // If at cap, remove oldest source to make room
            if (devastationSources.Count >= config.MaxSources)
            {
                RemoveOldestSources(1);
            }

            devastationSources.Add(new DevastationSource
            {
                Pos = pos,
                Range = range,
                Amount = amount,
                CurrentRadius = Math.Min(3.0, range),
                IsHealing = isHealing,
                SourceId = GenerateSourceId(),
                IsProtected = true, // Manually added sources are protected from auto-removal
                MetastasisThreshold = config.MetastasisThreshold
            });

            string action = isHealing ? "healing" : "devastation";
            return TextCommandResult.Success($"Added {action} source at {pos} (range: {range}, amount: {amount} blocks per tick)");
        }

        private TextCommandResult HandleRemoveCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string removeArg = args.Parsers[0].GetValue() as string;

            if (removeArg == "all")
            {
                int count = devastationSources.Count;
                devastationSources.Clear();
                return TextCommandResult.Success($"Removed all {count} devastation sources");
            }
            else if (removeArg == "saturated")
            {
                int removed = devastationSources.RemoveAll(s => s.IsSaturated);
                return TextCommandResult.Success($"Removed {removed} saturated devastation sources");
            }
            else if (removeArg == "metastasis")
            {
                int removed = devastationSources.RemoveAll(s => s.IsMetastasis);
                return TextCommandResult.Success($"Removed {removed} metastasis sources (kept original sources)");
            }
            else
            {
                if (player == null) return TextCommandResult.Error("This command must be run by a player");

                BlockSelection blockSel = player.CurrentBlockSelection;
                if (blockSel == null)
                {
                    return TextCommandResult.Error("Look at a block to remove it as a devastation source, or use 'remove [all|saturated|metastasis]'");
                }

                BlockPos pos = blockSel.Position;
                int removed = devastationSources.RemoveAll(s => s.Pos.Equals(pos));

                if (removed > 0)
                {
                    return TextCommandResult.Success($"Removed devastation source at {pos}");
                }
                else
                {
                    return TextCommandResult.Error("No devastation source found at this location");
                }
            }
        }

        private TextCommandResult HandleListCommand(TextCommandCallingArgs args)
        {
            if (devastationSources.Count == 0)
            {
                return TextCommandResult.Success("No manual devastation sources set");
            }

            string arg = args.Parsers[0].GetValue() as string;

            // Check if it's a summary request
            if (arg == "summary")
            {
                return ShowListSummary(args);
            }

            // Parse limit (default to 10)
            int limit = 10;
            if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out int parsedLimit))
            {
                limit = Math.Clamp(parsedLimit, 1, 100);
            }

            int originalCount = devastationSources.Count(s => !s.IsMetastasis);
            int metastasisCount = devastationSources.Count(s => s.IsMetastasis);
            int saturatedCount = devastationSources.Count(s => s.IsSaturated);

            var lines = new List<string>
            {
                $"Devastation sources ({devastationSources.Count}/{config.MaxSources} cap, {originalCount} original, {metastasisCount} metastasis, {saturatedCount} saturated):"
            };

            // Sort: active first, then by generation, then by total blocks devastated
            var sortedSources = devastationSources
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

            if (devastationSources.Count > limit)
            {
                lines.Add($"  ... and {devastationSources.Count - limit} more. Use '/devastate list {limit + 10}' or '/devastate list summary'");
            }

            return SendChatLines(args, lines, "Devastation sources listed in chat (scroll to read)");
        }

        private string GetSourceStatusLabel(DevastationSource source)
        {
            if (source.IsSaturated)
            {
                return "[saturated]";
            }

            bool readyToSeed = source.BlocksSinceLastMetastasis >= source.MetastasisThreshold &&
                              source.CurrentRadius >= source.Range;

            if (source.ChildrenSpawned > 0)
            {
                return $"[seeding {source.ChildrenSpawned}]";
            }
            else if (readyToSeed)
            {
                return "[seeding]";
            }
            else
            {
                return "[growing]";
            }
        }

        private TextCommandResult ShowListSummary(TextCommandCallingArgs args)
        {
            int protectedCount = devastationSources.Count(s => s.IsProtected);
            int metastasisCount = devastationSources.Count(s => s.IsMetastasis);
            int saturatedCount = devastationSources.Count(s => s.IsSaturated);
            int healingCount = devastationSources.Count(s => s.IsHealing);
            int growingCount = devastationSources.Count(s => !s.IsSaturated && !s.IsHealing &&
                (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range));
            int seedingCount = devastationSources.Count(s => !s.IsSaturated && !s.IsHealing &&
                s.BlocksSinceLastMetastasis >= s.MetastasisThreshold && s.CurrentRadius >= s.Range);

            var lines = new List<string>
            {
                $"=== Devastation Summary ({devastationSources.Count}/{config.MaxSources} cap) ===",
                $"  Protected (manual): {protectedCount} (never auto-removed)",
                $"  Metastasis children: {metastasisCount}",
                $"  Healing sources: {healingCount}",
                $"  Growing: {growingCount}",
                $"  Seeding (ready to spawn): {seedingCount}",
                $"  Saturated (done): {saturatedCount}"
            };

            // Group by generation level
            var byGeneration = devastationSources
                .GroupBy(s => s.GenerationLevel)
                .OrderBy(g => g.Key)
                .ToList();

            if (byGeneration.Count > 0)
            {
                lines.Add("  By Generation:");
                foreach (var gen in byGeneration)
                {
                    int growing = gen.Count(s => !s.IsSaturated && (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range));
                    int seeding = gen.Count(s => !s.IsSaturated && s.BlocksSinceLastMetastasis >= s.MetastasisThreshold && s.CurrentRadius >= s.Range);
                    int sat = gen.Count(s => s.IsSaturated);
                    long totalBlocks = gen.Sum(s => (long)s.BlocksDevastatedTotal);
                    string genLabel = gen.Key == 0 ? "Origin" : $"Gen {gen.Key}";
                    lines.Add($"    {genLabel}: {gen.Count()} ({growing} growing, {seeding} seeding, {sat} saturated) - {totalBlocks:N0} blocks");
                }
            }

            // Total stats
            long grandTotalBlocks = devastationSources.Sum(s => (long)s.BlocksDevastatedTotal);
            lines.Add($"  Total blocks devastated: {grandTotalBlocks:N0}");

            if (devastationSources.Count >= config.MaxSources)
            {
                lines.Add("  Warning: At source cap - oldest sources will be removed for new metastasis");
            }

            return SendChatLines(args, lines, "Devastation summary sent to chat (scrollable)");
        }

        private TextCommandResult HandleMaxSourcesCommand(TextCommandCallingArgs args)
        {
            int? maxArg = args.Parsers[0].GetValue() as int?;

            if (!maxArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current max sources cap: {config.MaxSources}",
                    $"Active sources: {devastationSources.Count}/{config.MaxSources}",
                    "Usage: /devastate maxsources [number] (e.g., 20, 50, 100)"
                }, "Max sources info sent to chat");
            }

            int newMax = Math.Clamp(maxArg.Value, 1, 1000);
            config.MaxSources = newMax;
            SaveConfig();

            string warning = "";
            if (devastationSources.Count >= config.MaxSources)
            {
                warning = $" Warning: Already at or above cap ({devastationSources.Count} sources). No new metastasis will spawn.";
            }

            return TextCommandResult.Success($"Max sources cap set to {config.MaxSources}{warning}");
        }

        private TextCommandResult HandleMaxAttemptsCommand(TextCommandCallingArgs args)
        {
            int? attemptsArg = args.Parsers[0].GetValue() as int?;

            if (!attemptsArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current max failed spawn attempts: {config.MaxFailedSpawnAttempts}",
                    "Usage: /devastate maxattempts [number] (e.g., 5, 10, 20)"
                }, "Max attempts info sent to chat");
            }

            int newAttempts = Math.Clamp(attemptsArg.Value, 1, 100);
            config.MaxFailedSpawnAttempts = newAttempts;
            SaveConfig();

            return TextCommandResult.Success($"Max failed spawn attempts set to {config.MaxFailedSpawnAttempts}");
        }

        private TextCommandResult HandleAirContactCommand(TextCommandCallingArgs args)
        {
            string onOff = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrEmpty(onOff))
            {
                string status = config.RequireSourceAirContact ? "ON" : "OFF";
                return SendChatLines(args, new[]
                {
                    $"Surface spreading (air contact): {status}",
                    "Usage: /devastate aircontact [on|off]"
                }, "Air contact setting sent to chat");
            }

            if (onOff.ToLower() == "on" || onOff == "1" || onOff.ToLower() == "true")
            {
                config.RequireSourceAirContact = true;
                SaveConfig();
                return TextCommandResult.Success("Surface spreading ENABLED - new metastasis sources must be adjacent to air");
            }
            else if (onOff.ToLower() == "off" || onOff == "0" || onOff.ToLower() == "false")
            {
                config.RequireSourceAirContact = false;
                SaveConfig();
                return TextCommandResult.Success("Surface spreading DISABLED - metastasis sources can spawn anywhere");
            }
            else
            {
                return TextCommandResult.Error("Invalid value. Use: on, off, 1, 0, true, or false");
            }
        }

        private TextCommandResult HandleMarkersCommand(TextCommandCallingArgs args)
        {
            string onOff = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrEmpty(onOff))
            {
                string status = config.ShowSourceMarkers ? "ON" : "OFF";
                return SendChatLines(args, new[]
                {
                    $"Source markers: {status}",
                    "Usage: /devastate markers [on|off]"
                }, "Marker setting sent to chat");
            }

            if (onOff.Equals("on", StringComparison.OrdinalIgnoreCase) || onOff == "1" || onOff.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                config.ShowSourceMarkers = true;
                SaveConfig();
                return TextCommandResult.Success("Source markers ENABLED");
            }
            else if (onOff.Equals("off", StringComparison.OrdinalIgnoreCase) || onOff == "0" || onOff.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                config.ShowSourceMarkers = false;
                SaveConfig();
                return TextCommandResult.Success("Source markers DISABLED");
            }
            else
            {
                return TextCommandResult.Error("Invalid value. Use: on, off, 1, 0, true, or false");
            }
        }

        private TextCommandResult HandleMinYCommand(TextCommandCallingArgs args)
        {
            int? levelArg = args.Parsers[0].GetValue() as int?;

            if (!levelArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current minimum Y level: {config.MinYLevel}",
                    "Usage: /devastate miny [level] (e.g., 0, -64, 50)"
                }, "Min Y info sent to chat");
            }

            config.MinYLevel = levelArg.Value;
            SaveConfig();

            return TextCommandResult.Success($"Minimum Y level for new sources set to {config.MinYLevel}");
        }

        private TextCommandResult HandleStatusCommand(TextCommandCallingArgs args)
        {
            string statusText = isPaused ? "PAUSED" : "RUNNING";
            var lines = new List<string>
            {
                $"Devastation status: {statusText}",
                $"Speed multiplier: {config.SpeedMultiplier:F2}x",
                $"Active sources: {devastationSources.Count}/{config.MaxSources}",
                $"Tracked blocks for regen: {regrowingBlocks?.Count ?? 0}",
                $"Surface spreading: {(config.RequireSourceAirContact ? "ON" : "OFF")}",
                $"Min Y level: {config.MinYLevel}",
                $"Child spawn delay: {config.ChildSpawnDelaySeconds}s",
                $"Max failed attempts: {config.MaxFailedSpawnAttempts}"
            };
            return SendChatLines(args, lines, "Status sent to chat (scrollable)");
        }
    }
}
