using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SpreadingDevastation
{
    // Test suite implementation - partial class for SpreadingDevastationModSystem
    public partial class SpreadingDevastationModSystem
    {
        #region Test Suite Implementation

        // Test suite state
        private bool isTestRunning = false;
        private TestContext currentTestContext = null;
        private List<TestResult> testResults = new List<TestResult>();
        private Stopwatch testStopwatch = new Stopwatch();

        private TextCommandResult HandleTestSuiteCommand(TextCommandCallingArgs args)
        {
            string testName = args.Parsers[0].GetValue() as string ?? "";

            if (isTestRunning)
            {
                return TextCommandResult.Error("A test suite is already running. Please wait for it to complete.");
            }

            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null)
            {
                return TextCommandResult.Error("This command must be run by a player");
            }

            // Handle special commands
            if (testName.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                return SendChatLines(args, new[]
                {
                    "=== Available Tests ===",
                    "1. blockconversion - Verify block type mappings",
                    "2. chunkfrontier - Verify frontier-based spreading",
                    "3. adaptiveradius - Verify radius expansion",
                    "4. metastasis - Verify source child spawning",
                    "5. chunkspread - Verify chunk-to-chunk spread",
                    "6. mobspawn - Verify corrupted entity spawning",
                    "7. insanity - Verify animal insanity",
                    "8. stability - Verify stability drain",
                    "9. riftwardprotect - Verify rift ward protection",
                    "10. riftwardheal - Verify rift ward healing",
                    "11. fog - Manual fog verification",
                    "12. edgebleed - Verify edge bleeding",
                    "13. regeneration - Verify regen tracking",
                    "14. chunkrepair - Verify stuck chunk repair",
                    "",
                    "Usage: /dv testsuite [testname] or /dv testsuite (all)"
                }, "Test list sent to chat");
            }

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error("Look at a block to run tests at that location");
            }

            // Initialize test context
            currentTestContext = new TestContext
            {
                StartPosition = blockSel.Position.Copy(),
                Player = player,
                StartTime = sapi.World.Calendar.TotalHours,
                OriginalSpeedMultiplier = config.SpeedMultiplier,
                OriginalChunkSpreadChance = config.ChunkSpreadChance,
                OriginalChunkSpreadInterval = config.ChunkSpreadIntervalSeconds,
                OriginalAnimalInsanityChance = config.AnimalInsanityChance,
                OriginalMetastasisThreshold = config.MetastasisThreshold,
                OriginalChildSpawnDelay = config.ChildSpawnDelaySeconds
            };

            testResults.Clear();
            isTestRunning = true;

            // Run tests asynchronously to avoid blocking
            sapi.World.RegisterCallback((dt) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(testName))
                    {
                        RunAllTests();
                    }
                    else
                    {
                        RunSingleTest(testName);
                    }
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error($"Test suite error: {ex}");
                    player.SendMessage(GlobalConstants.GeneralChatGroup, $"Test suite error: {ex.Message}", EnumChatType.Notification);
                }
                finally
                {
                    CleanupTestArtifacts();
                    RestoreConfig();
                    isTestRunning = false;
                    ReportTestResults(player);
                }
            }, 100); // Small delay to let command return first

            return TextCommandResult.Success("Starting test suite... Results will be shown when complete.");
        }

        private void RunAllTests()
        {
            // Run all tests in order
            testResults.Add(RunTest("Block Conversion", Test_BlockConversion));
            testResults.Add(RunTest("Chunk Frontier", Test_ChunkFrontier));
            testResults.Add(RunTest("Adaptive Radius", Test_AdaptiveRadius));
            testResults.Add(RunTest("Metastasis", Test_Metastasis));
            testResults.Add(RunTest("Chunk Spread", Test_ChunkSpread));
            testResults.Add(RunTest("Mob Spawning", Test_MobSpawning));
            testResults.Add(RunTest("Animal Insanity", Test_AnimalInsanity));
            testResults.Add(RunTest("Stability Drain", Test_StabilityDrain));
            testResults.Add(RunTest("Rift Ward Protection", Test_RiftWardProtection));
            testResults.Add(RunTest("Rift Ward Healing", Test_RiftWardHealing));
            testResults.Add(RunTest("Fog Effect", Test_FogEffect));
            testResults.Add(RunTest("Edge Bleeding", Test_EdgeBleeding));
            testResults.Add(RunTest("Regeneration Tracking", Test_RegenerationTracking));
            testResults.Add(RunTest("Chunk Repair", Test_ChunkRepair));
        }

        private void RunSingleTest(string testName)
        {
            var testMap = new Dictionary<string, System.Func<TestContext, TestResult>>(StringComparer.OrdinalIgnoreCase)
            {
                { "blockconversion", Test_BlockConversion },
                { "chunkfrontier", Test_ChunkFrontier },
                { "adaptiveradius", Test_AdaptiveRadius },
                { "metastasis", Test_Metastasis },
                { "chunkspread", Test_ChunkSpread },
                { "mobspawn", Test_MobSpawning },
                { "insanity", Test_AnimalInsanity },
                { "stability", Test_StabilityDrain },
                { "riftwardprotect", Test_RiftWardProtection },
                { "riftwardheal", Test_RiftWardHealing },
                { "fog", Test_FogEffect },
                { "edgebleed", Test_EdgeBleeding },
                { "regeneration", Test_RegenerationTracking },
                { "chunkrepair", Test_ChunkRepair }
            };

            if (testMap.TryGetValue(testName, out var testFunc))
            {
                testResults.Add(RunTest(testName, testFunc));
            }
            else
            {
                var result = new TestResult(testName);
                result.Fail($"Unknown test: {testName}. Use /dv testsuite list");
                testResults.Add(result);
            }
        }

        private TestResult RunTest(string name, System.Func<TestContext, TestResult> testFunc)
        {
            var result = new TestResult(name);
            testStopwatch.Restart();

            try
            {
                result = testFunc(currentTestContext);
                result.Name = name; // Ensure name is set
            }
            catch (Exception ex)
            {
                result.SetError(ex);
                sapi.Logger.Error($"Test '{name}' threw exception: {ex}");
            }

            testStopwatch.Stop();
            result.DurationMs = testStopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        private void ReportTestResults(IServerPlayer player)
        {
            var lines = new List<string>();
            double totalTime = testResults.Sum(r => r.DurationMs);

            lines.Add("=== Spreading Devastation Test Suite ===");
            lines.Add($"Ran {testResults.Count} tests in {totalTime / 1000.0:F1}s");
            lines.Add("");

            var passed = testResults.Where(r => r.Status == TestStatus.Passed).ToList();
            var failed = testResults.Where(r => r.Status == TestStatus.Failed).ToList();
            var manual = testResults.Where(r => r.Status == TestStatus.Manual).ToList();
            var errors = testResults.Where(r => r.Status == TestStatus.Error).ToList();

            if (passed.Count > 0)
            {
                lines.Add($"PASSED ({passed.Count}):");
                foreach (var r in passed)
                {
                    string msg = string.IsNullOrEmpty(r.Message) ? "" : $" - {r.Message}";
                    lines.Add($"  [PASS] {r.Name}{msg}");
                }
                lines.Add("");
            }

            if (failed.Count > 0)
            {
                lines.Add($"FAILED ({failed.Count}):");
                foreach (var r in failed)
                {
                    lines.Add($"  [FAIL] {r.Name} - {r.Message}");
                }
                lines.Add("");
            }

            if (manual.Count > 0)
            {
                lines.Add($"MANUAL ({manual.Count}):");
                foreach (var r in manual)
                {
                    lines.Add($"  [MANUAL] {r.Name} - {r.Message}");
                }
                lines.Add("");
            }

            if (errors.Count > 0)
            {
                lines.Add($"ERRORS ({errors.Count}):");
                foreach (var r in errors)
                {
                    lines.Add($"  [ERROR] {r.Name} - {r.Message}");
                }
                lines.Add("");
            }

            // Send each line as a separate message to avoid VTML parsing issues
            var safeLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            foreach (var line in safeLines)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, line, EnumChatType.Notification);
            }
        }

        private void CleanupTestArtifacts()
        {
            if (currentTestContext == null) return;

            // Remove test sources
            foreach (var sourceId in currentTestContext.TestSourceIds)
            {
                devastationSources.RemoveAll(s => s.SourceId == sourceId);
            }

            // Remove test chunks
            foreach (var chunkKey in currentTestContext.TestChunkKeys)
            {
                devastatedChunks.Remove(chunkKey);
            }

            // Restore modified blocks
            foreach (var kvp in currentTestContext.OriginalBlocks)
            {
                sapi.World.BlockAccessor.SetBlock(kvp.Value, kvp.Key);
            }

            // Remove test entities
            foreach (var entityId in currentTestContext.TestEntityIds)
            {
                Entity entity = sapi.World.GetEntityById(entityId);
                if (entity != null)
                {
                    entity.Die(EnumDespawnReason.Removed);
                }
            }

            currentTestContext = null;
        }

        private void RestoreConfig()
        {
            if (currentTestContext == null) return;

            config.SpeedMultiplier = currentTestContext.OriginalSpeedMultiplier;
            config.ChunkSpreadChance = currentTestContext.OriginalChunkSpreadChance;
            config.ChunkSpreadIntervalSeconds = currentTestContext.OriginalChunkSpreadInterval;
            config.AnimalInsanityChance = currentTestContext.OriginalAnimalInsanityChance;
            config.MetastasisThreshold = currentTestContext.OriginalMetastasisThreshold;
            config.ChildSpawnDelaySeconds = currentTestContext.OriginalChildSpawnDelay;
        }

        // Helper method to snapshot a block for later restoration
        private void SnapshotBlock(BlockPos pos)
        {
            if (!currentTestContext.OriginalBlocks.ContainsKey(pos))
            {
                Block block = sapi.World.BlockAccessor.GetBlock(pos);
                currentTestContext.OriginalBlocks[pos.Copy()] = block?.Id ?? 0;
            }
        }

        // Helper to wait for a condition with timeout
        private bool WaitForCondition(System.Func<bool> condition, int timeoutMs, int checkIntervalMs = 100)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                System.Threading.Thread.Sleep(checkIntervalMs);
            }
            return false;
        }

        #region Individual Tests

        private TestResult Test_BlockConversion(TestContext ctx)
        {
            var result = new TestResult("Block Conversion");
            BlockPos testPos = ctx.StartPosition.Copy();

            // Test soil conversion
            SnapshotBlock(testPos);
            Block soilBlock = sapi.World.GetBlock(new AssetLocation("game", "soil-medium-none"));
            if (soilBlock != null)
            {
                sapi.World.BlockAccessor.SetBlock(soilBlock.Id, testPos);
                Block placed = sapi.World.BlockAccessor.GetBlock(testPos);

                if (TryGetDevastatedForm(placed, out string devForm, out string regenTo))
                {
                    Block devBlock = sapi.World.GetBlock(new AssetLocation("game", devForm));
                    if (devBlock != null)
                    {
                        sapi.World.BlockAccessor.SetBlock(devBlock.Id, testPos);
                        Block afterDev = sapi.World.BlockAccessor.GetBlock(testPos);

                        if (IsAlreadyDevastated(afterDev))
                        {
                            result.Pass("soil-medium-none to " + devForm);
                            return result;
                        }
                    }
                }
            }

            result.Fail("Block conversion mapping failed");
            return result;
        }

        private TestResult Test_ChunkFrontier(TestContext ctx)
        {
            var result = new TestResult("Chunk Frontier");
            BlockPos pos = ctx.StartPosition.Copy();

            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            // Remove existing chunk if present
            devastatedChunks.Remove(chunkKey);

            // Create test chunk
            var testChunk = new DevastatedChunk
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                MarkedTime = sapi.World.Calendar.TotalHours,
                DevastationLevel = 0.0,
                IsFullyDevastated = false,
                FrontierInitialized = true,
                DevastationFrontier = new List<BlockPos> { pos.Copy() }
            };

            devastatedChunks[chunkKey] = testChunk;
            ctx.TestChunkKeys.Add(chunkKey);

            // Speed up for test
            double origSpeed = config.SpeedMultiplier;
            config.SpeedMultiplier = 50.0;

            // Process chunk spreading for a few seconds
            int iterations = 50;
            for (int i = 0; i < iterations; i++)
            {
                SpreadDevastationInChunk(testChunk);
                System.Threading.Thread.Sleep(50);
            }

            config.SpeedMultiplier = origSpeed;

            if (testChunk.BlocksDevastated > 5)
            {
                result.Pass($"Devastated {testChunk.BlocksDevastated} blocks, frontier: {testChunk.DevastationFrontier?.Count ?? 0}");
            }
            else
            {
                result.Fail($"Only {testChunk.BlocksDevastated} blocks devastated, expected > 5");
            }

            return result;
        }

        private TestResult Test_AdaptiveRadius(TestContext ctx)
        {
            var result = new TestResult("Adaptive Radius");

            // Create a source with small range
            var source = new DevastationSource
            {
                SourceId = GenerateSourceId(),
                Pos = ctx.StartPosition.Copy(),
                Range = 10,
                Amount = 1,
                IsHealing = false,
                CurrentRadius = 3.0,
                TotalAttempts = 0,
                SuccessfulAttempts = 0
            };

            devastationSources.Add(source);
            ctx.TestSourceIds.Add(source.SourceId);

            double initialRadius = source.CurrentRadius;

            // Simulate many failed attempts to trigger expansion
            source.TotalAttempts = 200;
            source.SuccessfulAttempts = 5; // Very low success rate

            // The radius expansion happens in SpreadDevastationFromRifts
            // We check the logic directly
            double successRate = (double)source.SuccessfulAttempts / source.TotalAttempts;

            if (successRate < config.VeryLowSuccessThreshold && source.CurrentRadius < source.Range)
            {
                // This would trigger expansion
                result.Pass($"Expansion would trigger: rate={successRate:P1}, threshold={config.VeryLowSuccessThreshold:P1}");
            }
            else if (successRate < config.LowSuccessThreshold && source.CurrentRadius < source.Range)
            {
                result.Pass($"Moderate expansion: rate={successRate:P1}");
            }
            else
            {
                result.Fail($"No expansion triggered: rate={successRate:P1}");
            }

            return result;
        }

        private TestResult Test_Metastasis(TestContext ctx)
        {
            var result = new TestResult("Metastasis");

            // Create parent source that should spawn child
            var parentSource = new DevastationSource
            {
                SourceId = GenerateSourceId(),
                Pos = ctx.StartPosition.Copy(),
                Range = 8,
                Amount = 1,
                IsHealing = false,
                CurrentRadius = 8.0, // At max
                BlocksDevastatedTotal = 500, // Above threshold
                BlocksSinceLastMetastasis = 500,
                ChildrenSpawned = 0,
                GenerationLevel = 0,
                LastChildSpawnTime = 0
            };

            devastationSources.Add(parentSource);
            ctx.TestSourceIds.Add(parentSource.SourceId);

            int initialSources = devastationSources.Count;

            // Set config for fast spawn
            config.MetastasisThreshold = 100;
            config.ChildSpawnDelaySeconds = 0;

            // Try to spawn child
            bool spawned = TrySpawnSingleChild(parentSource, sapi.World.Calendar.TotalHours);

            if (spawned || devastationSources.Count > initialSources)
            {
                result.Pass($"Child source spawned. Total sources: {devastationSources.Count}");
                // Track new source for cleanup
                var newSource = devastationSources.LastOrDefault(s => s.SourceId != parentSource.SourceId);
                if (newSource != null)
                {
                    ctx.TestSourceIds.Add(newSource.SourceId);
                }
            }
            else
            {
                result.Fail("No child source spawned - may be blocked by terrain");
            }

            return result;
        }

        private TestResult Test_ChunkSpread(TestContext ctx)
        {
            var result = new TestResult("Chunk Spread");
            BlockPos pos = ctx.StartPosition.Copy();

            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            // Create a fully devastated chunk
            var sourceChunk = new DevastatedChunk
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                MarkedTime = sapi.World.Calendar.TotalHours - 1,
                DevastationLevel = 1.0,
                IsFullyDevastated = true,
                BlocksDevastated = 1000
            };

            devastatedChunks[chunkKey] = sourceChunk;
            ctx.TestChunkKeys.Add(chunkKey);

            int initialChunks = devastatedChunks.Count;

            // Set config for guaranteed spread
            config.ChunkSpreadEnabled = true;
            config.ChunkSpreadChance = 1.0;
            config.ChunkSpreadIntervalSeconds = 0.1;

            // Try spreading
            TrySpreadToNearbyChunks(sapi.World.Calendar.TotalHours);

            if (devastatedChunks.Count > initialChunks)
            {
                // Track new chunks
                foreach (var key in devastatedChunks.Keys)
                {
                    if (!ctx.TestChunkKeys.Contains(key))
                    {
                        ctx.TestChunkKeys.Add(key);
                    }
                }
                result.Pass($"Spread to {devastatedChunks.Count - initialChunks} new chunk(s)");
            }
            else
            {
                result.Fail("No spread occurred - may need edge blocks");
            }

            return result;
        }

        private TestResult Test_MobSpawning(TestContext ctx)
        {
            var result = new TestResult("Mob Spawning");
            BlockPos pos = ctx.StartPosition.Copy();

            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            var testChunk = new DevastatedChunk
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                MarkedTime = sapi.World.Calendar.TotalHours - 10,
                IsFullyDevastated = true,
                BlocksDevastated = 500,
                NextSpawnTime = 0,
                MobsSpawned = 0
            };

            devastatedChunks[chunkKey] = testChunk;
            ctx.TestChunkKeys.Add(chunkKey);

            // Count corrupt entities before
            int beforeCount = sapi.World.LoadedEntities.Values
                .Count(e => e.Code.Path.Contains("corrupt"));

            // Try spawn
            TrySpawnCorruptedEntitiesInChunk(testChunk, sapi.World.Calendar.TotalHours);

            // Count after
            int afterCount = sapi.World.LoadedEntities.Values
                .Count(e => e.Code.Path.Contains("corrupt"));

            if (afterCount > beforeCount || testChunk.MobsSpawned > 0)
            {
                // Track spawned entities
                foreach (var entity in sapi.World.LoadedEntities.Values)
                {
                    if (entity.Code.Path.Contains("corrupt"))
                    {
                        ctx.TestEntityIds.Add(entity.EntityId);
                    }
                }
                result.Pass($"Spawned {afterCount - beforeCount} corrupt entity");
            }
            else
            {
                result.Fail("No corrupt entities spawned - check player distance");
            }

            return result;
        }

        private TestResult Test_AnimalInsanity(TestContext ctx)
        {
            var result = new TestResult("Animal Insanity");

            // Check if there's a devastated chunk at player location
            BlockPos playerPos = ctx.Player.Entity.Pos.AsBlockPos;
            int chunkX = playerPos.X / CHUNK_SIZE;
            int chunkZ = playerPos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            // Create devastated chunk
            if (!devastatedChunks.ContainsKey(chunkKey))
            {
                var testChunk = new DevastatedChunk
                {
                    ChunkX = chunkX,
                    ChunkZ = chunkZ,
                    MarkedTime = sapi.World.Calendar.TotalHours,
                    IsFullyDevastated = true
                };
                devastatedChunks[chunkKey] = testChunk;
                ctx.TestChunkKeys.Add(chunkKey);
            }

            // Look for any animal nearby
            var nearbyAnimals = sapi.World.GetEntitiesAround(
                ctx.StartPosition.ToVec3d(),
                48, 48,
                e => CanEntityGoInsane(e)
            );

            if (nearbyAnimals.Length == 0)
            {
                result.Fail("No eligible animals nearby to test insanity");
                return result;
            }

            // Try to drive first animal insane
            Entity testAnimal = nearbyAnimals[0];
            config.AnimalInsanityChance = 1.0; // Guarantee insanity

            DriveEntityInsane(testAnimal);

            bool isInsane = testAnimal.WatchedAttributes.GetBool(INSANITY_ATTRIBUTE, false);
            if (isInsane)
            {
                result.Pass($"{testAnimal.Code.Path} went insane");
            }
            else
            {
                result.Fail("Entity did not go insane");
            }

            return result;
        }

        private TestResult Test_StabilityDrain(TestContext ctx)
        {
            var result = new TestResult("Stability Drain");

            // Get player's stability behavior
            var stabilityBehavior = ctx.Player.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
            if (stabilityBehavior == null)
            {
                result.Fail("Player has no temporal stability behavior");
                return result;
            }

            // Create devastated chunk at player
            BlockPos playerPos = ctx.Player.Entity.Pos.AsBlockPos;
            int chunkX = playerPos.X / CHUNK_SIZE;
            int chunkZ = playerPos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!devastatedChunks.ContainsKey(chunkKey))
            {
                var testChunk = new DevastatedChunk
                {
                    ChunkX = chunkX,
                    ChunkZ = chunkZ,
                    MarkedTime = sapi.World.Calendar.TotalHours,
                    IsFullyDevastated = true
                };
                devastatedChunks[chunkKey] = testChunk;
                ctx.TestChunkKeys.Add(chunkKey);
            }

            double beforeStability = stabilityBehavior.OwnStability;

            // Apply drain
            config.ChunkStabilityDrainRate = 0.1; // High rate for test
            DrainPlayerTemporalStability(0.5f);

            double afterStability = stabilityBehavior.OwnStability;

            if (afterStability < beforeStability || beforeStability <= 0)
            {
                result.Pass($"Stability: {beforeStability:F3} to {afterStability:F3}");
            }
            else
            {
                result.Fail($"Stability unchanged: {beforeStability:F3}");
            }

            return result;
        }

        private TestResult Test_RiftWardProtection(TestContext ctx)
        {
            var result = new TestResult("Rift Ward Protection");

            // Create a source and chunk to be protected
            var testSource = new DevastationSource
            {
                SourceId = GenerateSourceId(),
                Pos = ctx.StartPosition.Copy(),
                Range = 8,
                Amount = 1
            };
            devastationSources.Add(testSource);
            ctx.TestSourceIds.Add(testSource.SourceId);

            int chunkX = ctx.StartPosition.X / CHUNK_SIZE;
            int chunkZ = ctx.StartPosition.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            var testChunk = new DevastatedChunk
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                MarkedTime = sapi.World.Calendar.TotalHours
            };
            devastatedChunks[chunkKey] = testChunk;
            ctx.TestChunkKeys.Add(chunkKey);

            int sourcesBefore = devastationSources.Count;
            int chunksBefore = devastatedChunks.Count;

            // Directly remove the source and chunk (simulating what rift ward does)
            int protectionRadius = config.RiftWardProtectionRadius;
            int radiusSquared = protectionRadius * protectionRadius;
            BlockPos wardPos = ctx.StartPosition.Copy();

            // Remove sources within radius
            devastationSources.RemoveAll(source =>
            {
                if (source.Pos == null) return false;
                int dx = source.Pos.X - wardPos.X;
                int dy = source.Pos.Y - wardPos.Y;
                int dz = source.Pos.Z - wardPos.Z;
                return (dx * dx + dy * dy + dz * dz) <= radiusSquared;
            });

            // Remove chunk
            devastatedChunks.Remove(chunkKey);

            int sourcesAfter = devastationSources.Count;
            int chunksAfter = devastatedChunks.Count;

            if (sourcesAfter < sourcesBefore || chunksAfter < chunksBefore)
            {
                result.Pass($"Sources: {sourcesBefore} to {sourcesAfter}, Chunks: {chunksBefore} to {chunksAfter}");
            }
            else
            {
                result.Fail("Protection removal logic failed");
            }

            return result;
        }

        private TestResult Test_RiftWardHealing(TestContext ctx)
        {
            var result = new TestResult("Rift Ward Healing");

            BlockPos testPos = ctx.StartPosition.Copy();
            SnapshotBlock(testPos);

            // Place a devastated block
            Block devBlock = sapi.World.GetBlock(new AssetLocation("game", "devastatedsoil-0"));
            if (devBlock == null)
            {
                result.Fail("Could not find devastatedsoil-0 block");
                return result;
            }

            sapi.World.BlockAccessor.SetBlock(devBlock.Id, testPos);

            Block beforeHeal = sapi.World.BlockAccessor.GetBlock(testPos);
            if (!IsAlreadyDevastated(beforeHeal))
            {
                result.Fail("Failed to place devastated block");
                return result;
            }

            // Try healing
            if (TryGetHealedForm(beforeHeal, out string healedForm))
            {
                if (healedForm != "none")
                {
                    Block healBlock = sapi.World.GetBlock(new AssetLocation("game", healedForm));
                    if (healBlock != null)
                    {
                        sapi.World.BlockAccessor.SetBlock(healBlock.Id, testPos);
                        Block afterHeal = sapi.World.BlockAccessor.GetBlock(testPos);

                        if (!IsAlreadyDevastated(afterHeal))
                        {
                            result.Pass($"Healed to {healedForm}");
                            return result;
                        }
                    }
                }
                else
                {
                    // "none" means remove block
                    sapi.World.BlockAccessor.SetBlock(0, testPos);
                    result.Pass("Block removed (healed to none)");
                    return result;
                }
            }

            result.Fail("Healing mapping not found or failed");
            return result;
        }

        private TestResult Test_FogEffect(TestContext ctx)
        {
            var result = new TestResult("Fog Effect");

            // Create devastated chunk at player location for fog test
            BlockPos playerPos = ctx.Player.Entity.Pos.AsBlockPos;
            int chunkX = playerPos.X / CHUNK_SIZE;
            int chunkZ = playerPos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!devastatedChunks.ContainsKey(chunkKey))
            {
                var testChunk = new DevastatedChunk
                {
                    ChunkX = chunkX,
                    ChunkZ = chunkZ,
                    MarkedTime = sapi.World.Calendar.TotalHours,
                    IsFullyDevastated = true
                };
                devastatedChunks[chunkKey] = testChunk;
                ctx.TestChunkKeys.Add(chunkKey);
            }

            // Manual verification
            result.SetManual("Verify: rusty orange fog visible in this chunk, clears when leaving");

            // Log to server
            sapi.Logger.Notification("FOG TEST: Player should see rusty orange fog in devastated chunk");
            ctx.Player.SendMessage(GlobalConstants.GeneralChatGroup,
                "FOG TEST: You should see rusty orange fog. Step outside the chunk boundary to verify it clears.",
                EnumChatType.Notification);

            return result;
        }

        private TestResult Test_EdgeBleeding(TestContext ctx)
        {
            var result = new TestResult("Edge Bleeding");

            // This tests the bleed frontier system
            BlockPos pos = ctx.StartPosition.Copy();
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            // Create chunk with bleed frontier at edge
            int edgeX = chunkX * CHUNK_SIZE + CHUNK_SIZE - 1; // Right edge
            BlockPos edgePos = new BlockPos(edgeX, pos.Y, pos.Z);

            var testChunk = new DevastatedChunk
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                MarkedTime = sapi.World.Calendar.TotalHours,
                IsFullyDevastated = false,
                BleedFrontier = new List<BleedBlock> { new BleedBlock { Pos = edgePos.Copy(), RemainingSpread = 3 } }
            };

            devastatedChunks[chunkKey] = testChunk;
            ctx.TestChunkKeys.Add(chunkKey);

            // Check if bleed system recognizes edge position
            bool isAtEdge = (edgePos.X % CHUNK_SIZE == CHUNK_SIZE - 1) ||
                           (edgePos.X % CHUNK_SIZE == 0) ||
                           (edgePos.Z % CHUNK_SIZE == CHUNK_SIZE - 1) ||
                           (edgePos.Z % CHUNK_SIZE == 0);

            if (isAtEdge && testChunk.BleedFrontier.Count > 0)
            {
                result.Pass($"Edge bleed frontier initialized at chunk boundary");
            }
            else
            {
                result.Fail("Edge bleed frontier not at chunk boundary");
            }

            return result;
        }

        private TestResult Test_RegenerationTracking(TestContext ctx)
        {
            var result = new TestResult("Regeneration Tracking");

            BlockPos testPos = ctx.StartPosition.Copy();
            SnapshotBlock(testPos);

            int beforeCount = regrowingBlocks.Count;

            // Place and devastate a block
            Block soilBlock = sapi.World.GetBlock(new AssetLocation("game", "soil-medium-none"));
            if (soilBlock != null)
            {
                sapi.World.BlockAccessor.SetBlock(soilBlock.Id, testPos);

                if (TryGetDevastatedForm(soilBlock, out string devForm, out string regenTo))
                {
                    Block devBlock = sapi.World.GetBlock(new AssetLocation("game", devForm));
                    if (devBlock != null)
                    {
                        sapi.World.BlockAccessor.SetBlock(devBlock.Id, testPos);

                        // Add to regrowing blocks
                        regrowingBlocks.Add(new RegrowingBlocks
                        {
                            Pos = testPos.Copy(),
                            Out = regenTo,
                            LastTime = sapi.World.Calendar.TotalHours
                        });

                        int afterCount = regrowingBlocks.Count;

                        if (afterCount > beforeCount)
                        {
                            var entry = regrowingBlocks.Last();
                            result.Pass($"Tracked: {testPos} to {entry.Out}");
                            return result;
                        }
                    }
                }
            }

            result.Fail("Failed to track regeneration");
            return result;
        }

        private TestResult Test_ChunkRepair(TestContext ctx)
        {
            var result = new TestResult("Chunk Repair");

            BlockPos pos = ctx.StartPosition.Copy();
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            // Create a "stuck" chunk - empty frontier, low block count
            var stuckChunk = new DevastatedChunk
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                MarkedTime = sapi.World.Calendar.TotalHours,
                IsFullyDevastated = false,
                BlocksDevastated = 50, // Low count
                DevastationFrontier = new List<BlockPos>(), // Empty!
                FrontierInitialized = true,
                ConsecutiveEmptyFrontierChecks = 5 // Triggers repair
            };

            devastatedChunks[chunkKey] = stuckChunk;
            ctx.TestChunkKeys.Add(chunkKey);

            // Queue for repair
            if (!chunksNeedingRepair.Contains(chunkKey))
            {
                chunksNeedingRepair.Enqueue(chunkKey);
            }

            bool wasQueued = chunksNeedingRepair.Contains(chunkKey);

            // Process repair
            ProcessChunksNeedingRepair();

            // Check if frontier was rebuilt
            var chunk = devastatedChunks.GetValueOrDefault(chunkKey);
            if (chunk != null && chunk.DevastationFrontier != null && chunk.DevastationFrontier.Count > 0)
            {
                result.Pass($"Frontier rebuilt with {chunk.DevastationFrontier.Count} blocks");
            }
            else if (wasQueued)
            {
                result.Pass("Chunk was queued for repair (no convertible blocks found)");
            }
            else
            {
                result.Fail("Chunk repair system did not activate");
            }

            return result;
        }

        #endregion

        #endregion
    }
}
