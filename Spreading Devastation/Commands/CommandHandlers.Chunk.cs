using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using SpreadingDevastation.Models;

namespace SpreadingDevastation
{
    // Partial class containing chunk management command handlers
    public partial class SpreadingDevastationModSystem
    {
        private TextCommandResult HandleChunkCommand(TextCommandCallingArgs args)
        {
            string action = args.Parsers[0].GetValue() as string;
            string value = args.Parsers[1].GetValue() as string;

            // Handle configuration commands first (don't require looking at a block)
            if (action == "spawn")
            {
                return HandleChunkSpawnCommand(args, value);
            }
            else if (action == "drain")
            {
                return HandleChunkDrainCommand(value);
            }
            else if (action == "spread")
            {
                return HandleChunkSpreadCommand(value);
            }
            else if (action == "spreadchance")
            {
                return HandleChunkSpreadChanceCommand(value);
            }
            else if (action == "list")
            {
                return HandleChunkListCommand(args);
            }
            else if (action == "perf" || action == "performance")
            {
                return HandleChunkPerfCommand(args);
            }
            else if (action == "repair")
            {
                return HandleChunkRepairCommand();
            }
            else if (action == "analyze")
            {
                return HandleChunkAnalyzeCommand(args);
            }
            else if (action == "fix")
            {
                return HandleChunkFixCommand(args);
            }
            else if (action == "unrepairable")
            {
                return HandleChunkUnrepairableCommand(args, value);
            }
            else if (action == "remove")
            {
                return HandleChunkRemoveCommand(args, value);
            }
            else
            {
                // Default action: mark chunk as devastated (requires looking at a block)
                return HandleChunkMarkCommand(args);
            }
        }

        private TextCommandResult HandleChunkSpawnCommand(TextCommandCallingArgs args, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return SendChatLines(args, new[]
                {
                    "=== Mob Spawn Settings ===",
                    $"Spawn interval: {config.ChunkSpawnIntervalMinHours:F2}-{config.ChunkSpawnIntervalMaxHours:F2} game hours (random)",
                    $"Cooldown after spawn: {config.ChunkSpawnCooldownHours:F2} game hours",
                    $"Distance from player: {config.ChunkSpawnMinDistance}-{config.ChunkSpawnMaxDistance} blocks",
                    $"Max mobs per chunk: {config.ChunkSpawnMaxMobsPerChunk}",
                    "",
                    "Subcommands:",
                    "  /dv chunk spawn interval [min] [max] - Set random interval range (hours)",
                    "  /dv chunk spawn cooldown [hours] - Set cooldown after spawn",
                    "  /dv chunk spawn distance [min] [max] - Set spawn distance from player",
                    "  /dv chunk spawn maxmobs [count] - Set max mobs per chunk",
                    "  /dv chunk spawn reset - Reset mob counts in all chunks"
                }, "Spawn settings sent to chat");
            }

            // Parse subcommand
            string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string subCmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";

            if (subCmd == "interval")
            {
                if (parts.Length < 3)
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current interval: {config.ChunkSpawnIntervalMinHours:F2}-{config.ChunkSpawnIntervalMaxHours:F2} game hours",
                        "Usage: /dv chunk spawn interval [min] [max]",
                        "Example: /dv chunk spawn interval 0.5 1.0 (30-60 in-game minutes)"
                    }, "Interval info sent to chat");
                }

                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double minHours) ||
                    !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double maxHours))
                {
                    return TextCommandResult.Error("Invalid numbers. Usage: /dv chunk spawn interval [min] [max]");
                }

                if (minHours > maxHours)
                {
                    (minHours, maxHours) = (maxHours, minHours); // Swap if reversed
                }

                config.ChunkSpawnIntervalMinHours = Math.Clamp(minHours, 0.01, 100.0);
                config.ChunkSpawnIntervalMaxHours = Math.Clamp(maxHours, 0.01, 100.0);
                SaveConfig();
                return TextCommandResult.Success($"Spawn interval set to {config.ChunkSpawnIntervalMinHours:F2}-{config.ChunkSpawnIntervalMaxHours:F2} game hours");
            }
            else if (subCmd == "cooldown")
            {
                if (parts.Length < 2)
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current cooldown: {config.ChunkSpawnCooldownHours:F2} game hours",
                        "Usage: /dv chunk spawn cooldown [hours]",
                        "Example: /dv chunk spawn cooldown 4 (4 hour minimum between spawns)"
                    }, "Cooldown info sent to chat");
                }

                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double cooldown))
                {
                    return TextCommandResult.Error("Invalid number. Usage: /dv chunk spawn cooldown [hours]");
                }

                config.ChunkSpawnCooldownHours = Math.Clamp(cooldown, 0.0, 100.0);
                SaveConfig();
                return TextCommandResult.Success($"Spawn cooldown set to {config.ChunkSpawnCooldownHours:F2} game hours");
            }
            else if (subCmd == "distance")
            {
                if (parts.Length < 3)
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current distance: {config.ChunkSpawnMinDistance}-{config.ChunkSpawnMaxDistance} blocks from player",
                        "Usage: /dv chunk spawn distance [min] [max]",
                        "Example: /dv chunk spawn distance 16 48"
                    }, "Distance info sent to chat");
                }

                if (!int.TryParse(parts[1], out int minDist) || !int.TryParse(parts[2], out int maxDist))
                {
                    return TextCommandResult.Error("Invalid numbers. Usage: /dv chunk spawn distance [min] [max]");
                }

                if (minDist > maxDist)
                {
                    (minDist, maxDist) = (maxDist, minDist); // Swap if reversed
                }

                config.ChunkSpawnMinDistance = Math.Clamp(minDist, 1, 256);
                config.ChunkSpawnMaxDistance = Math.Clamp(maxDist, 1, 256);
                SaveConfig();
                return TextCommandResult.Success($"Spawn distance set to {config.ChunkSpawnMinDistance}-{config.ChunkSpawnMaxDistance} blocks from player");
            }
            else if (subCmd == "maxmobs")
            {
                if (parts.Length < 2)
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current max mobs per chunk: {config.ChunkSpawnMaxMobsPerChunk}",
                        "Usage: /dv chunk spawn maxmobs [count]",
                        "Example: /dv chunk spawn maxmobs 5"
                    }, "Max mobs info sent to chat");
                }

                if (!int.TryParse(parts[1], out int maxMobs))
                {
                    return TextCommandResult.Error("Invalid number. Usage: /dv chunk spawn maxmobs [count]");
                }

                config.ChunkSpawnMaxMobsPerChunk = Math.Clamp(maxMobs, 0, 100);
                SaveConfig();
                return TextCommandResult.Success($"Max mobs per chunk set to {config.ChunkSpawnMaxMobsPerChunk}");
            }
            else if (subCmd == "reset")
            {
                int resetCount = 0;
                foreach (var chunk in devastatedChunks.Values)
                {
                    if (chunk.MobsSpawned > 0)
                    {
                        chunk.MobsSpawned = 0;
                        chunk.NextSpawnTime = 0;
                        resetCount++;
                    }
                }
                return TextCommandResult.Success($"Reset mob counts in {resetCount} chunks. Spawning can resume.");
            }
            else
            {
                return TextCommandResult.Error("Unknown spawn subcommand. Use: interval, cooldown, distance, maxmobs, reset");
            }
        }

        private TextCommandResult HandleChunkDrainCommand(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return TextCommandResult.Success($"Current stability drain rate: {config.ChunkStabilityDrainRate:F4} per 500ms tick (~{config.ChunkStabilityDrainRate * 2 * 100:F2}% per second). Use '/dv chunk drain [rate]' to set.");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double rate))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv chunk drain [rate]");
            }

            config.ChunkStabilityDrainRate = Math.Clamp(rate, 0.0, 1.0);
            SaveConfig();
            return TextCommandResult.Success($"Chunk stability drain rate set to {config.ChunkStabilityDrainRate:F4} (~{config.ChunkStabilityDrainRate * 2 * 100:F2}% per sec)");
        }

        private TextCommandResult HandleChunkSpreadCommand(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                string status = config.ChunkSpreadEnabled ? "ON" : "OFF";
                return TextCommandResult.Success($"Chunk spread: {status}. Spread chance: {config.ChunkSpreadChance * 100:F1}% every {config.ChunkSpreadIntervalSeconds:F0}s (at 1x speed). Use '/dv chunk spread [on|off]'");
            }

            if (value.Equals("on", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                config.ChunkSpreadEnabled = true;
                SaveConfig();
                return TextCommandResult.Success("Chunk spreading ENABLED - devastated chunks can spread to neighbors");
            }
            else if (value.Equals("off", StringComparison.OrdinalIgnoreCase) || value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                config.ChunkSpreadEnabled = false;
                SaveConfig();
                return TextCommandResult.Success("Chunk spreading DISABLED - devastated chunks will not spread");
            }
            else
            {
                return TextCommandResult.Error("Invalid value. Use: on, off, 1, 0, true, or false");
            }
        }

        private TextCommandResult HandleChunkSpreadChanceCommand(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return TextCommandResult.Success($"Current spread chance: {config.ChunkSpreadChance * 100:F1}%. Check interval: {config.ChunkSpreadIntervalSeconds:F0}s (at 1x speed). Use '/dv chunk spreadchance [percent]'");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv chunk spreadchance [percent]");
            }

            config.ChunkSpreadChance = Math.Clamp(percent / 100.0, 0.0, 1.0);
            SaveConfig();
            return TextCommandResult.Success($"Chunk spread chance set to {config.ChunkSpreadChance * 100:F1}%");
        }

        private TextCommandResult HandleChunkListCommand(TextCommandCallingArgs args)
        {
            if (devastatedChunks.Count == 0)
            {
                return TextCommandResult.Success("No devastated chunks");
            }

            var lines = new List<string> { $"Devastated chunks ({devastatedChunks.Count}):" };
            foreach (var chunk in devastatedChunks.Values.Take(20))
            {
                string status = chunk.IsFullyDevastated ? "fully devastated" : $"{chunk.DevastationLevel:P0} devastated";
                int frontierCount = chunk.DevastationFrontier?.Count ?? 0;
                lines.Add($"  ({chunk.ChunkX}, {chunk.ChunkZ}): {status}, {chunk.BlocksDevastated} blocks, frontier: {frontierCount}");
            }
            if (devastatedChunks.Count > 20)
            {
                lines.Add($"  ... and {devastatedChunks.Count - 20} more");
            }
            return SendChatLines(args, lines, "Chunk list sent to chat");
        }

        private TextCommandResult HandleChunkPerfCommand(TextCommandCallingArgs args)
        {
            var stats = GetPerformanceStats();
            int activeChunks = devastatedChunks.Values.Count(c => !c.IsFullyDevastated);
            int stuckChunks = devastatedChunks.Values.Count(c => !c.IsFullyDevastated && c.FrontierInitialized && (c.DevastationFrontier == null || c.DevastationFrontier.Count == 0) && c.BlocksDevastated < 1000);

            var lines = new List<string>
            {
                "=== Chunk Devastation Performance ===",
                $"Avg processing time: {stats.avgTime:F2}ms per tick",
                $"Peak processing time: {stats.peakTime:F2}ms",
                $"Avg tick interval: {stats.avgDt:F0}ms (expected: 500ms)",
                "",
                $"Total chunks: {devastatedChunks.Count}",
                $"Active chunks: {activeChunks}",
                $"Fully devastated: {devastatedChunks.Count - activeChunks}",
                $"Stuck chunks detected: {stuckChunks}",
                $"Chunks queued for repair: {chunksNeedingRepair.Count}",
                "",
                $"Total ticks processed: {totalTicksProcessed}",
                $"Total processing time: {totalProcessingTimeMs / 1000.0:F1}s"
            };

            return SendChatLines(args, lines, "Performance stats sent to chat");
        }

        private TextCommandResult HandleChunkRepairCommand()
        {
            // Force repair all stuck chunks
            int stuckCount = 0;
            foreach (var chunk in devastatedChunks.Values)
            {
                if (!chunk.IsFullyDevastated &&
                    (chunk.DevastationFrontier == null || chunk.DevastationFrontier.Count == 0))
                {
                    long chunkKey = chunk.ChunkKey;
                    if (!chunksNeedingRepair.Contains(chunkKey))
                    {
                        chunksNeedingRepair.Enqueue(chunkKey);
                        stuckCount++;
                    }
                }
            }

            if (stuckCount > 0)
            {
                return TextCommandResult.Success($"Queued {stuckCount} stuck chunks for repair");
            }
            else
            {
                return TextCommandResult.Success("No stuck chunks found");
            }
        }

        private TextCommandResult HandleChunkAnalyzeCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error("Look at a block to analyze its chunk");
            }

            BlockPos pos = blockSel.Position;
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!devastatedChunks.TryGetValue(chunkKey, out var chunk))
            {
                return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is not marked as devastated");
            }

            // Gather detailed diagnostic info
            var lines = new List<string>
            {
                $"=== Chunk Analysis: ({chunkX}, {chunkZ}) ===",
                $"Chunk key: {chunkKey}",
                $"Blocks devastated: {chunk.BlocksDevastated}",
                $"Devastation level: {chunk.DevastationLevel:P1}",
                $"IsFullyDevastated: {chunk.IsFullyDevastated}",
                $"IsUnrepairable: {chunk.IsUnrepairable}",
                $"FrontierInitialized: {chunk.FrontierInitialized}",
                $"Frontier count: {chunk.DevastationFrontier?.Count ?? 0}",
                $"Bleed frontier count: {chunk.BleedFrontier?.Count ?? 0}",
                $"FillInTickCounter: {chunk.FillInTickCounter}",
                $"ConsecutiveEmptyFrontierChecks: {chunk.ConsecutiveEmptyFrontierChecks}",
                $"RepairAttemptCount: {chunk.RepairAttemptCount}/5",
                $"Marked time: {chunk.MarkedTime:F2} hours",
                ""
            };

            // Check frontier blocks validity
            if (chunk.DevastationFrontier != null && chunk.DevastationFrontier.Count > 0)
            {
                lines.Add($"First 5 frontier blocks:");
                foreach (var frontierPos in chunk.DevastationFrontier.Take(5))
                {
                    Block block = sapi.World.BlockAccessor.GetBlock(frontierPos);
                    string blockName = block?.Code?.ToString() ?? "null";
                    bool isDevastated = block != null && IsAlreadyDevastated(block);
                    lines.Add($"  {frontierPos}: {blockName} (devastated: {isDevastated})");
                }
            }
            else
            {
                lines.Add("Frontier is EMPTY - this chunk cannot spread!");
            }

            // Scan chunk for actual devastated blocks
            int startX = chunkX * CHUNK_SIZE;
            int startZ = chunkZ * CHUNK_SIZE;
            int devastatedBlocksFound = 0;
            int convertibleBlocksFound = 0;
            BlockPos sampleDevastatedPos = null;

            for (int dx = 0; dx < CHUNK_SIZE && devastatedBlocksFound < 100; dx += 4)
            {
                for (int dz = 0; dz < CHUNK_SIZE && devastatedBlocksFound < 100; dz += 4)
                {
                    int x = startX + dx;
                    int z = startZ + dz;
                    int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
                    if (surfaceY <= 0) continue;

                    for (int yOff = -3; yOff <= 3; yOff++)
                    {
                        BlockPos checkPos = new BlockPos(x, surfaceY + yOff, z);
                        Block block = sapi.World.BlockAccessor.GetBlock(checkPos);
                        if (block == null || block.Id == 0) continue;

                        if (IsAlreadyDevastated(block))
                        {
                            devastatedBlocksFound++;
                            if (sampleDevastatedPos == null) sampleDevastatedPos = checkPos.Copy();
                        }
                        else if (TryGetDevastatedForm(block, out _, out _))
                        {
                            convertibleBlocksFound++;
                        }
                    }
                }
            }

            lines.Add("");
            lines.Add($"Chunk scan (sampled every 4 blocks):");
            lines.Add($"  Devastated blocks found: {devastatedBlocksFound}");
            lines.Add($"  Convertible blocks found: {convertibleBlocksFound}");
            if (sampleDevastatedPos != null)
            {
                lines.Add($"  Sample devastated at: {sampleDevastatedPos}");
            }

            // Diagnose the problem
            lines.Add("");
            lines.Add("Diagnosis:");
            if (chunk.IsFullyDevastated)
            {
                lines.Add("  Chunk is marked fully devastated - no more spreading needed");
            }
            else if (chunk.DevastationFrontier == null || chunk.DevastationFrontier.Count == 0)
            {
                if (devastatedBlocksFound == 0)
                {
                    lines.Add("  PROBLEM: No frontier AND no devastated blocks found!");
                    lines.Add("  This chunk was likely created but never initialized properly.");
                }
                else if (convertibleBlocksFound == 0)
                {
                    lines.Add("  Frontier empty but no convertible blocks - chunk may be done");
                }
                else
                {
                    lines.Add("  PROBLEM: Has devastated blocks but empty frontier!");
                    lines.Add("  Use '/dv chunk repair' to fix, or '/dv chunk fix' on this chunk");
                }
            }
            else
            {
                lines.Add("  Chunk appears healthy - frontier has blocks to spread from");
            }

            // Check if queued for repair
            if (chunksNeedingRepair.Contains(chunkKey))
            {
                lines.Add("  Note: This chunk is queued for repair");
            }

            return SendChatLines(args, lines, "Chunk analysis sent to chat");
        }

        private TextCommandResult HandleChunkFixCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error("Look at a block to fix its chunk");
            }

            BlockPos pos = blockSel.Position;
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!devastatedChunks.TryGetValue(chunkKey, out var chunk))
            {
                return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is not marked as devastated");
            }

            // Force re-initialize and reset repair state
            int oldFrontierCount = chunk.DevastationFrontier?.Count ?? 0;
            chunk.FrontierInitialized = false;
            chunk.IsFullyDevastated = false;
            chunk.IsUnrepairable = false; // Reset unrepairable flag
            chunk.RepairAttemptCount = 0;
            chunk.ConsecutiveEmptyFrontierChecks = 0;
            InitializeChunkFrontier(chunk);

            int newFrontierCount = chunk.DevastationFrontier?.Count ?? 0;
            return TextCommandResult.Success($"Fixed chunk ({chunkX}, {chunkZ}): frontier {oldFrontierCount} to {newFrontierCount} blocks (repair state reset)");
        }

        private TextCommandResult HandleChunkUnrepairableCommand(TextCommandCallingArgs args, string value)
        {
            if (value == "list" || string.IsNullOrEmpty(value))
            {
                var unrepairableChunks = devastatedChunks.Values.Where(c => c.IsUnrepairable).ToList();
                if (unrepairableChunks.Count == 0)
                {
                    return TextCommandResult.Success("No unrepairable chunks found");
                }

                var lines = new List<string> { $"=== Unrepairable Chunks ({unrepairableChunks.Count}) ===" };
                foreach (var chunk in unrepairableChunks.Take(20))
                {
                    lines.Add($"  ({chunk.ChunkX}, {chunk.ChunkZ}): {chunk.BlocksDevastated} blocks, {chunk.RepairAttemptCount} repair attempts");
                }
                if (unrepairableChunks.Count > 20)
                {
                    lines.Add($"  ... and {unrepairableChunks.Count - 20} more");
                }
                return SendChatLines(args, lines, "Unrepairable chunks list sent to chat");
            }
            else if (value == "clear")
            {
                int count = 0;
                foreach (var chunk in devastatedChunks.Values.Where(c => c.IsUnrepairable))
                {
                    chunk.IsUnrepairable = false;
                    chunk.RepairAttemptCount = 0;
                    chunk.ConsecutiveEmptyFrontierChecks = 0;
                    count++;
                }
                return TextCommandResult.Success($"Reset {count} unrepairable chunks - they will be retried");
            }
            else if (value == "remove")
            {
                int count = devastatedChunks.Values.Count(c => c.IsUnrepairable);
                var keysToRemove = devastatedChunks.Where(kvp => kvp.Value.IsUnrepairable).Select(kvp => kvp.Key).ToList();
                foreach (var key in keysToRemove)
                {
                    devastatedChunks.Remove(key);
                }
                return TextCommandResult.Success($"Removed {count} unrepairable chunks from tracking");
            }
            else
            {
                return TextCommandResult.Error("Usage: /dv chunk unrepairable [list|clear|remove]");
            }
        }

        private TextCommandResult HandleChunkRemoveCommand(TextCommandCallingArgs args, string value)
        {
            if (value == "all")
            {
                int count = devastatedChunks.Count;
                devastatedChunks.Clear();
                return TextCommandResult.Success($"Removed all {count} devastated chunks");
            }

            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error("Look at a block to remove its chunk, or use '/dv chunk remove all'");
            }

            BlockPos pos = blockSel.Position;
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (devastatedChunks.ContainsKey(chunkKey))
            {
                devastatedChunks.Remove(chunkKey);
                return TextCommandResult.Success($"Removed devastated chunk at ({chunkX}, {chunkZ})");
            }
            else
            {
                return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is not marked as devastated");
            }
        }

        private TextCommandResult HandleChunkMarkCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                // No block selected - show help
                return SendChatLines(args, new[]
                {
                    "Chunk commands:",
                    "  /dv chunk - Mark looked-at chunk as devastated",
                    "  /dv chunk remove [all] - Remove devastation from chunk",
                    "  /dv chunk list - List all devastated chunks",
                    "  /dv chunk analyze - Detailed diagnostics for looked-at chunk",
                    "  /dv chunk fix - Force re-initialize looked-at chunk",
                    "  /dv chunk perf - Show performance stats",
                    "  /dv chunk repair - Queue all stuck chunks for repair",
                    "  /dv chunk unrepairable [list|clear|remove] - Manage unrepairable chunks",
                    "  /dv chunk spawn - Show mob spawn settings and subcommands",
                    "  /dv chunk drain [rate] - Set stability drain rate",
                    "  /dv chunk spread [on|off] - Toggle chunk spreading",
                    "  /dv chunk spreadchance [percent] - Set spread chance"
                }, "Chunk help sent to chat");
            }

            BlockPos pos = blockSel.Position.Copy();
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (devastatedChunks.ContainsKey(chunkKey))
            {
                return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is already marked as devastated");
            }

            var newChunk = new DevastatedChunk
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                MarkedTime = sapi.World.Calendar.TotalHours,
                DevastationLevel = 0.0,
                IsFullyDevastated = false,
                FrontierInitialized = true,
                DevastationFrontier = new List<BlockPos> { pos }
            };

            // Devastate the starting block immediately
            Block startBlock = sapi.World.BlockAccessor.GetBlock(pos);
            if (startBlock != null && startBlock.Id != 0 && !IsAlreadyDevastated(startBlock))
            {
                if (TryGetDevastatedForm(startBlock, out string devastatedBlock, out string regeneratesTo))
                {
                    Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedBlock));
                    if (newBlock != null)
                    {
                        sapi.World.BlockAccessor.SetBlock(newBlock.Id, pos);
                        regrowingBlocks.Add(new RegrowingBlocks
                        {
                            Pos = pos,
                            Out = regeneratesTo,
                            LastTime = sapi.World.Calendar.TotalHours
                        });
                        newChunk.BlocksDevastated++;
                    }
                }
            }

            devastatedChunks[chunkKey] = newChunk;
            return TextCommandResult.Success($"Marked chunk at ({chunkX}, {chunkZ}) as devastated starting from {pos}. Devastation will spread in cardinal directions.");
        }
    }
}
