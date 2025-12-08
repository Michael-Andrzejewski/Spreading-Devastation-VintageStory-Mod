using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SpreadingDevastation
{
    public class SpreadingDevastationModSystem : ModSystem
    {
        [ProtoContract]
        public class RegrowingBlocks
        {
            [ProtoMember(1)]
            public BlockPos Pos;

            [ProtoMember(2)]
            public string Out;

            [ProtoMember(3)]
            public double LastTime;
        }

        [ProtoContract]
        public class DevastationSource
        {
            [ProtoMember(1)]
            public BlockPos Pos;

            [ProtoMember(2)]
            public int Range = 8; // Default 8 blocks

            [ProtoMember(3)]
            public int Amount = 1; // Default 1 block per tick (every 10ms)

            [ProtoMember(4)]
            public double CurrentRadius = 3.0; // Start small, expand outward

            [ProtoMember(5)]
            public int SuccessfulAttempts = 0; // Track how many blocks we actually devastated

            [ProtoMember(6)]
            public int TotalAttempts = 0; // Track total attempts

            [ProtoMember(7)]
            public bool IsHealing = false; // True = heals devastation, False = spreads devastation

            // Metastasis system fields
            [ProtoMember(8)]
            public bool IsMetastasis = false; // True if this is a child source spawned from another

            [ProtoMember(9)]
            public int GenerationLevel = 0; // 0 = original, 1 = first metastasis, 2 = child of metastasis, etc.

            [ProtoMember(10)]
            public int BlocksDevastatedTotal = 0; // Total blocks devastated by this source

            [ProtoMember(11)]
            public int BlocksSinceLastMetastasis = 0; // Blocks devastated since last metastasis spawn

            [ProtoMember(12)]
            public int MetastasisThreshold = 300; // Spawn metastasis after this many blocks

            [ProtoMember(13)]
            public bool IsSaturated = false; // True when this source has fully saturated its area

            [ProtoMember(14)]
            public int MaxGenerationLevel = 10; // Prevent infinite spreading after this many generations

            [ProtoMember(15)]
            public string ParentSourceId = null; // ID of the parent source that spawned this one (for tree visualization)

            [ProtoMember(16)]
            public string SourceId = null; // Unique ID for this source

            [ProtoMember(17)]
            public int StallCounter = 0; // Counts consecutive low-success cycles at max radius

            [ProtoMember(18)]
            public bool IsProtected = false; // Protected sources (manually added) are never auto-removed
        }

        private ICoreServerAPI sapi;
        private List<RegrowingBlocks> regrowingBlocks;
        private List<DevastationSource> devastationSources;
        private double speedMultiplier = 1.0; // Global speed multiplier for all devastation spread
        private bool isPaused = false; // When true, all devastation processing stops
        private int maxSources = 20; // Maximum number of devastation sources (caps metastasis growth)
        private int nextSourceId = 1; // Counter for generating unique source IDs
        private int cleanupTickCounter = 0; // Counter for periodic cleanup

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            
            // Check for rifts and manual sources every 10ms (10x faster - 100 times per second)
            api.Event.RegisterGameTickListener(SpreadDevastationFromRifts, 10);
            
            // Check for block regeneration every 1000ms (once per second)
            api.Event.RegisterGameTickListener(RegenerateBlocks, 1000);
            
            // Save/load events
            api.Event.SaveGameLoaded += OnSaveGameLoading;
            api.Event.GameWorldSave += OnSaveGameSaving;
            
            // Register commands
            api.RegisterCommand("devastate", "Manage devastation sources", 
                "[add|remove|list]", OnDevastateCommand, Privilege.controlserver);
            api.RegisterCommand("devastationspeed", "Set devastation spread speed multiplier", 
                "[multiplier]", OnDevastationSpeedCommand, Privilege.controlserver);
        }

        private void OnSaveGameLoading()
        {
            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData("regrowingBlocks");
                regrowingBlocks = data == null ? new List<RegrowingBlocks>() : SerializerUtil.Deserialize<List<RegrowingBlocks>>(data);
                
                byte[] sourcesData = sapi.WorldManager.SaveGame.GetData("devastationSources");
                devastationSources = sourcesData == null ? new List<DevastationSource>() : SerializerUtil.Deserialize<List<DevastationSource>>(sourcesData);
                
                byte[] speedData = sapi.WorldManager.SaveGame.GetData("devastationSpeedMultiplier");
                speedMultiplier = speedData == null ? 1.0 : SerializerUtil.Deserialize<double>(speedData);
                
                byte[] pausedData = sapi.WorldManager.SaveGame.GetData("devastationPaused");
                isPaused = pausedData == null ? false : SerializerUtil.Deserialize<bool>(pausedData);
                
                byte[] maxSourcesData = sapi.WorldManager.SaveGame.GetData("devastationMaxSources");
                maxSources = maxSourcesData == null ? 20 : SerializerUtil.Deserialize<int>(maxSourcesData);
                
                byte[] nextIdData = sapi.WorldManager.SaveGame.GetData("devastationNextSourceId");
                nextSourceId = nextIdData == null ? 1 : SerializerUtil.Deserialize<int>(nextIdData);
                
                sapi.Logger.Notification($"SpreadingDevastation: Loaded {devastationSources?.Count ?? 0} sources, {regrowingBlocks?.Count ?? 0} regrowing blocks");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error loading save data: {ex.Message}");
                // Initialize empty lists if loading failed
                regrowingBlocks = regrowingBlocks ?? new List<RegrowingBlocks>();
                devastationSources = devastationSources ?? new List<DevastationSource>();
            }
        }

        private void OnSaveGameSaving()
        {
            try
            {
                sapi.WorldManager.SaveGame.StoreData("regrowingBlocks", SerializerUtil.Serialize(regrowingBlocks ?? new List<RegrowingBlocks>()));
                sapi.WorldManager.SaveGame.StoreData("devastationSources", SerializerUtil.Serialize(devastationSources ?? new List<DevastationSource>()));
                sapi.WorldManager.SaveGame.StoreData("devastationSpeedMultiplier", SerializerUtil.Serialize(speedMultiplier));
                sapi.WorldManager.SaveGame.StoreData("devastationPaused", SerializerUtil.Serialize(isPaused));
                sapi.WorldManager.SaveGame.StoreData("devastationMaxSources", SerializerUtil.Serialize(maxSources));
                sapi.WorldManager.SaveGame.StoreData("devastationNextSourceId", SerializerUtil.Serialize(nextSourceId));
                
                sapi.Logger.VerboseDebug($"SpreadingDevastation: Saved {devastationSources?.Count ?? 0} sources, {regrowingBlocks?.Count ?? 0} regrowing blocks");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error saving data: {ex.Message}");
            }
        }

        private void OnDevastationSpeedCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            string speedArg = args.PopWord();
            
            if (string.IsNullOrEmpty(speedArg))
            {
                player.SendMessage(groupId, $"Current devastation speed: {speedMultiplier:F2}x", EnumChatType.Notification);
                player.SendMessage(groupId, "Usage: /devastationspeed <multiplier>  (e.g., 0.5 for half speed, 5 for 5x speed)", EnumChatType.Notification);
                return;
            }
            
            if (double.TryParse(speedArg, out double newSpeed))
            {
                // Clamp to reasonable values
                newSpeed = Math.Clamp(newSpeed, 0.01, 100.0);
                speedMultiplier = newSpeed;
                player.SendMessage(groupId, $"Devastation speed set to {speedMultiplier:F2}x", EnumChatType.CommandSuccess);
            }
            else
            {
                player.SendMessage(groupId, "Invalid speed value. Use a number like 0.5, 1, 2, 5, etc.", EnumChatType.CommandError);
            }
        }

        private void OnDevastateCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            string subcommand = args.PopWord();
            
            switch (subcommand)
            {
                case "add":
                    AddDevastationSource(player, groupId, args, false);
                    break;
                    
                case "heal":
                    AddDevastationSource(player, groupId, args, true);
                    break;
                    
                case "remove":
                    string removeArg = args.PopWord();
                    if (removeArg == "all")
                    {
                        int count = devastationSources.Count;
                        devastationSources.Clear();
                        player.SendMessage(groupId, $"Removed all {count} devastation sources", EnumChatType.CommandSuccess);
                    }
                    else if (removeArg == "saturated")
                    {
                        int removed = devastationSources.RemoveAll(s => s.IsSaturated);
                        player.SendMessage(groupId, $"Removed {removed} saturated devastation sources", EnumChatType.CommandSuccess);
                    }
                    else if (removeArg == "metastasis")
                    {
                        int removed = devastationSources.RemoveAll(s => s.IsMetastasis);
                        player.SendMessage(groupId, $"Removed {removed} metastasis sources (kept original sources)", EnumChatType.CommandSuccess);
                    }
                    else
                    {
                        // Put the arg back and do normal single remove
                        BlockSelection blockSel = player.CurrentBlockSelection;
                        if (blockSel == null)
                        {
                            player.SendMessage(groupId, "Look at a block to remove it as a devastation source, or use 'remove all/saturated/metastasis'", EnumChatType.CommandError);
                            return;
                        }
                        
                        BlockPos pos = blockSel.Position;
                        int removed = devastationSources.RemoveAll(s => s.Pos.Equals(pos));
                        
                        if (removed > 0)
                        {
                            player.SendMessage(groupId, $"Removed devastation source at {pos}", EnumChatType.CommandSuccess);
                        }
                        else
                        {
                            player.SendMessage(groupId, "No devastation source found at this location", EnumChatType.CommandError);
                        }
                    }
                    break;
                    
                case "list":
                    HandleListCommand(player, groupId, args);
                    break;
                    
                case "maxsources":
                    HandleMaxSourcesCommand(player, groupId, args);
                    break;
                
                case "stop":
                    isPaused = true;
                    player.SendMessage(groupId, "Devastation spreading STOPPED. Use '/devastate start' to resume.", EnumChatType.CommandSuccess);
                    break;
                    
                case "start":
                    isPaused = false;
                    player.SendMessage(groupId, "Devastation spreading STARTED.", EnumChatType.CommandSuccess);
                    break;
                    
                case "status":
                    string statusText = isPaused ? "PAUSED" : "RUNNING";
                    player.SendMessage(groupId, $"Devastation status: {statusText}", EnumChatType.Notification);
                    player.SendMessage(groupId, $"Speed multiplier: {speedMultiplier:F2}x", EnumChatType.Notification);
                    player.SendMessage(groupId, $"Active sources: {devastationSources.Count}/{maxSources}", EnumChatType.Notification);
                    player.SendMessage(groupId, $"Tracked blocks for regen: {regrowingBlocks?.Count ?? 0}", EnumChatType.Notification);
                    break;
                    
                default:
                    player.SendMessage(groupId, "Usage: /devastate [add|heal|remove|list|maxsources|stop|start|status]", EnumChatType.CommandError);
                    player.SendMessage(groupId, "Examples:", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate add range 16 amount 10  - Spread devastation (will spawn metastasis)", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate heal range 16 amount 10 - Heal devastation", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate remove all              - Remove all sources", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate remove saturated        - Remove only saturated sources", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate remove metastasis       - Remove metastasis (keep originals)", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate list [count|summary]    - List sources (default 10, 'summary' for generation counts)", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate maxsources <number>     - Set max sources cap (currently {0})", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate stop                    - Pause all devastation", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate start                   - Resume devastation", EnumChatType.CommandError);
                    break;
            }
        }

        private void AddDevastationSource(IServerPlayer player, int groupId, CmdArgs args, bool isHealing)
        {
            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                player.SendMessage(groupId, $"Look at a block to mark it as a {(isHealing ? "healing" : "devastation")} source", EnumChatType.CommandError);
                return;
            }
            
            BlockPos pos = blockSel.Position.Copy();
            
            // Check if already exists
            if (devastationSources.Any(s => s.Pos.Equals(pos)))
            {
                player.SendMessage(groupId, "This block is already a source (use remove to change it)", EnumChatType.CommandError);
                return;
            }
            
            // Parse optional parameters
            int range = 8;
            int amount = 1;
            
            string arg;
            while ((arg = args.PopWord()) != null)
            {
                if (arg == "range")
                {
                    string rangeStr = args.PopWord();
                    if (rangeStr != null && int.TryParse(rangeStr, out int parsedRange))
                    {
                        range = Math.Clamp(parsedRange, 1, 128);
                    }
                }
                else if (arg == "amount")
                {
                    string amountStr = args.PopWord();
                    if (amountStr != null && int.TryParse(amountStr, out int parsedAmount))
                    {
                        amount = Math.Clamp(parsedAmount, 1, 100);
                    }
                }
            }
            
            // If at cap, remove oldest source to make room
            if (devastationSources.Count >= maxSources)
            {
                RemoveOldestSources(1);
                player.SendMessage(groupId, $"At source cap ({maxSources}) - removed oldest source to make room", EnumChatType.Notification);
            }
            
            devastationSources.Add(new DevastationSource 
            { 
                Pos = pos,
                Range = range,
                Amount = amount,
                CurrentRadius = Math.Min(3.0, range),
                IsHealing = isHealing,
                SourceId = GenerateSourceId(),
                IsProtected = true // Manually added sources are protected from auto-removal
            });
            
            string action = isHealing ? "healing" : "devastation";
            player.SendMessage(groupId, $"Added {action} source at {pos} (range: {range}, amount: {amount} blocks per tick)", EnumChatType.CommandSuccess);
        }

        private void HandleMaxSourcesCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            string maxArg = args.PopWord();
            
            if (string.IsNullOrEmpty(maxArg))
            {
                player.SendMessage(groupId, $"Current max sources cap: {maxSources}", EnumChatType.Notification);
                player.SendMessage(groupId, $"Active sources: {devastationSources.Count}/{maxSources}", EnumChatType.Notification);
                player.SendMessage(groupId, "Usage: /devastate maxsources <number>  (e.g., 20, 50, 100)", EnumChatType.Notification);
                return;
            }
            
            if (int.TryParse(maxArg, out int newMax))
            {
                // Clamp to reasonable values
                newMax = Math.Clamp(newMax, 1, 1000);
                maxSources = newMax;
                player.SendMessage(groupId, $"Max sources cap set to {maxSources}", EnumChatType.CommandSuccess);
                
                if (devastationSources.Count >= maxSources)
                {
                    player.SendMessage(groupId, $"Warning: Already at or above cap ({devastationSources.Count} sources). No new metastasis will spawn.", EnumChatType.Notification);
                }
            }
            else
            {
                player.SendMessage(groupId, "Invalid number. Use a value like 20, 50, 100, etc.", EnumChatType.CommandError);
            }
        }

        private void HandleListCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (devastationSources.Count == 0)
            {
                player.SendMessage(groupId, "No manual devastation sources set", EnumChatType.Notification);
                return;
            }
            
            string arg = args.PopWord();
            
            // Check if it's a summary request
            if (arg == "summary")
            {
                ShowListSummary(player, groupId);
                return;
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
            
            player.SendMessage(groupId, $"Devastation sources ({devastationSources.Count}/{maxSources} cap, {originalCount} original, {metastasisCount} metastasis, {saturatedCount} saturated):", EnumChatType.Notification);
            
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
                string statusInfo = source.IsSaturated ? " [SAT]" : "";
                string progressInfo = $"{source.BlocksSinceLastMetastasis}/{source.MetastasisThreshold}";
                string idInfo = !string.IsNullOrEmpty(source.SourceId) ? $"#{source.SourceId}" : "";
                
                player.SendMessage(groupId, 
                    $"  [{type}] [{genInfo}]{statusInfo}{idInfo} {source.Pos} R:{source.CurrentRadius:F0}/{source.Range} Tot:{source.BlocksDevastatedTotal} Prog:{progressInfo}", 
                    EnumChatType.Notification);
            }
            
            if (devastationSources.Count > limit)
            {
                player.SendMessage(groupId, $"  ... and {devastationSources.Count - limit} more. Use '/devastate list {limit + 10}' or '/devastate list summary'", EnumChatType.Notification);
            }
        }

        private void ShowListSummary(IServerPlayer player, int groupId)
        {
            int protectedCount = devastationSources.Count(s => s.IsProtected);
            int metastasisCount = devastationSources.Count(s => s.IsMetastasis);
            int saturatedCount = devastationSources.Count(s => s.IsSaturated);
            int healingCount = devastationSources.Count(s => s.IsHealing);
            int activeCount = devastationSources.Count(s => !s.IsSaturated && !s.IsHealing);
            
            player.SendMessage(groupId, $"=== Devastation Summary ({devastationSources.Count}/{maxSources} cap) ===", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Protected (manual): {protectedCount} (never auto-removed)", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Metastasis children: {metastasisCount}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Healing sources: {healingCount}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Active spreading: {activeCount}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Saturated (done): {saturatedCount}", EnumChatType.Notification);
            
            // Group by generation level
            var byGeneration = devastationSources
                .GroupBy(s => s.GenerationLevel)
                .OrderBy(g => g.Key)
                .ToList();
            
            if (byGeneration.Count > 0)
            {
                player.SendMessage(groupId, "  By Generation:", EnumChatType.Notification);
                foreach (var gen in byGeneration)
                {
                    int active = gen.Count(s => !s.IsSaturated);
                    int sat = gen.Count(s => s.IsSaturated);
                    long totalBlocks = gen.Sum(s => (long)s.BlocksDevastatedTotal);
                    string genLabel = gen.Key == 0 ? "Origin" : $"Gen {gen.Key}";
                    player.SendMessage(groupId, $"    {genLabel}: {gen.Count()} ({active} active, {sat} sat) - {totalBlocks:N0} blocks", EnumChatType.Notification);
                }
            }
            
            // Total stats
            long grandTotalBlocks = devastationSources.Sum(s => (long)s.BlocksDevastatedTotal);
            player.SendMessage(groupId, $"  Total blocks devastated: {grandTotalBlocks:N0}", EnumChatType.Notification);
            
            if (devastationSources.Count >= maxSources)
            {
                player.SendMessage(groupId, "  ⚠ At source cap - oldest sources will be removed for new metastasis", EnumChatType.Notification);
            }
        }

        private string GenerateSourceId()
        {
            return (nextSourceId++).ToString();
        }

        private void RegenerateBlocks(float dt)
        {
            if (sapi == null || regrowingBlocks == null || regrowingBlocks.Count == 0)
            {
                return;
            }

            try
            {
                List<RegrowingBlocks> blocksToRemove = new List<RegrowingBlocks>();
                double currentHours = sapi.World.Calendar.TotalHours;
                
                // Rate limit: only regenerate up to 50 blocks per tick to prevent lag spikes from time manipulation
                int maxRegenPerTick = 50;
                int regeneratedCount = 0;
                
                // Check each devastated block to see if it's time to regenerate
                foreach (RegrowingBlocks regrowingBlock in regrowingBlocks)
                {
                    if (regeneratedCount >= maxRegenPerTick) break;
                    
                    // Skip blocks with invalid data
                    if (regrowingBlock.Pos == null) 
                    {
                        blocksToRemove.Add(regrowingBlock);
                        continue;
                    }
                    
                    // Regenerate after 60 in-game hours
                    // Also handle time going backwards gracefully (if LastTime > currentHours, reset it)
                    double timeDiff = currentHours - regrowingBlock.LastTime;
                    
                    // If time went backwards significantly (e.g., time manipulation), update the LastTime
                    if (timeDiff < -24.0) // More than a day backwards
                    {
                        regrowingBlock.LastTime = currentHours;
                        continue;
                    }
                    
                    if (timeDiff > 60.0)
                    {
                        if (regrowingBlock.Out == "none")
                        {
                            // Replace with air
                            sapi.World.BlockAccessor.SetBlock(0, regrowingBlock.Pos);
                        }
                        else
                        {
                            // Replace with the original block type
                            Block block = sapi.World.GetBlock(new AssetLocation("game", regrowingBlock.Out));
                            if (block != null && block.Id != 0)
                            {
                                sapi.World.BlockAccessor.SetBlock(block.Id, regrowingBlock.Pos);
                            }
                            // If block not found, still remove from list to prevent infinite loops
                        }
                        
                        blocksToRemove.Add(regrowingBlock);
                        regeneratedCount++;
                    }
                }

                // Remove regenerated blocks from the tracking list
                foreach (RegrowingBlocks item in blocksToRemove)
                {
                    regrowingBlocks.Remove(item);
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash the server
                sapi.Logger.Error("SpreadingDevastation: Error in RegenerateBlocks: " + ex.Message);
            }
        }

        private void SpreadDevastationFromRifts(float dt)
        {
            // Skip all processing if paused
            if (isPaused) return;
            
            // Safety check
            if (sapi == null || devastationSources == null) return;
            
            try
            {
                // Get the rift system
                ModSystemRifts riftSystem = sapi.ModLoader.GetModSystem<ModSystemRifts>();
            
            // Spread from rifts (if rift system exists) - 1 block per rift per tick
            // Rifts don't use adaptive radius, they always search full 8 blocks
            if (riftSystem?.riftsById != null)
            {
                foreach (Rift rift in riftSystem.riftsById.Values)
                {
                    SpreadDevastationFromRift(rift.Position, 8, 1);
                }
            }
            
            // Spread from manual devastation sources
            List<DevastationSource> toRemove = new List<DevastationSource>();
            foreach (DevastationSource source in devastationSources)
            {
                // Check if the block still exists
                Block block = sapi.World.BlockAccessor.GetBlock(source.Pos);
                if (block == null || block.Id == 0)
                {
                    // Block was removed, remove from sources
                    toRemove.Add(source);
                    continue;
                }
                
                // Spread or heal the specified amount of blocks per tick
                int processed = source.IsHealing 
                    ? HealDevastationAroundPosition(source.Pos.ToVec3d(), source)
                    : SpreadDevastationAroundPosition(source.Pos.ToVec3d(), source);
                
                // Track success rate and adjust radius
                int effectiveAmount = Math.Max(1, (int)(source.Amount * speedMultiplier));
                source.SuccessfulAttempts += processed;
                source.TotalAttempts += effectiveAmount * 5; // We try up to 5 times per block
                
                // Check every 100 attempts if we should expand
                if (source.TotalAttempts >= 100)
                {
                    double successRate = (double)source.SuccessfulAttempts / source.TotalAttempts;
                    
                    // If success rate is below 20%, expand the search radius
                    if (successRate < 0.2 && source.CurrentRadius < source.Range)
                    {
                        // Expand faster if really low success rate
                        double expansion = successRate < 0.1 ? 4.0 : 2.0;
                        source.CurrentRadius = Math.Min(source.CurrentRadius + expansion, source.Range);
                        source.StallCounter = 0; // Reset stall counter when still expanding
                    }
                    // Track stalling: at max radius with very low success rate
                    else if (successRate < 0.05 && source.CurrentRadius >= source.Range && !source.IsHealing)
                    {
                        source.StallCounter++;
                        
                        // After 10 stall cycles (plenty of time to try), take action to keep spreading
                        if (source.StallCounter >= 10)
                        {
                            // Always try to spawn metastasis when stalled - this moves the frontier forward
                            // Even if local saturation is low (lots of air/water), we should try to leap past it
                            bool spawned = SpawnMetastasisSourcesWithLongRangeSearch(source);
                            
                            if (spawned)
                            {
                                source.StallCounter = 0;
                            }
                            else if (source.StallCounter >= 30)
                            {
                                // Tried 3 times with long-range search and couldn't find any viable land
                                // This source is truly in an impassable spot (surrounded by ocean/void)
                                source.IsSaturated = true;
                            }
                        }
                    }
                    else
                    {
                        // Good success rate, reset stall counter
                        source.StallCounter = 0;
                    }
                    
                    // Reset attempt counters
                    source.SuccessfulAttempts = 0;
                    source.TotalAttempts = 0;
                }
                
                // Check for metastasis spawning (only for non-healing sources)
                if (!source.IsHealing && !source.IsSaturated)
                {
                    // Spawn metastasis when we've devastated enough blocks AND radius has reached max
                    if (source.BlocksSinceLastMetastasis >= source.MetastasisThreshold && 
                        source.CurrentRadius >= source.Range)
                    {
                        // Check actual saturation in the area
                        double saturation = CalculateLocalDevastationPercent(source.Pos.ToVec3d(), source.CurrentRadius);
                        
                        if (saturation >= 0.75) // 75%+ devastated
                        {
                            SpawnMetastasisSources(source);
                        }
                    }
                }
            }
            
            // Clean up removed sources
            foreach (var source in toRemove)
            {
                devastationSources.Remove(source);
            }
            
            // Periodic cleanup: remove saturated sources to free slots for active spreading
            // Run every ~500 ticks (5 seconds at 10ms tick rate)
            cleanupTickCounter++;
            if (cleanupTickCounter >= 500)
            {
                cleanupTickCounter = 0;
                CleanupSaturatedSources();
            }
            }
            catch (Exception ex)
            {
                // Log but don't crash the server
                sapi.Logger.Error("SpreadingDevastation: Error in SpreadDevastationFromRifts: " + ex.Message);
            }
        }
        
        /// <summary>
        /// Periodically removes saturated sources that have no active children nearby.
        /// This frees up slots for the spreading frontier to continue advancing.
        /// </summary>
        private void CleanupSaturatedSources()
        {
            if (devastationSources == null || devastationSources.Count == 0) return;
            
            // Only cleanup if we're using more than half of the source cap
            // This ensures we have room for new metastasis
            if (devastationSources.Count < maxSources / 2) return;
            
            // Find saturated, non-protected sources that can be removed
            var saturatedSources = devastationSources
                .Where(s => s.IsSaturated && !s.IsProtected && !s.IsHealing)
                .ToList();
            
            if (saturatedSources.Count == 0) return;
            
            // Remove up to 1/4 of saturated sources each cleanup cycle
            int toRemove = Math.Max(1, saturatedSources.Count / 4);
            
            // Prioritize removing sources with highest generation level (furthest from origin)
            var sourcesToRemove = saturatedSources
                .OrderByDescending(s => s.GenerationLevel)
                .ThenByDescending(s => s.BlocksDevastatedTotal)
                .Take(toRemove)
                .ToList();
            
            foreach (var source in sourcesToRemove)
            {
                devastationSources.Remove(source);
            }
        }

        private void SpreadDevastationFromRift(Vec3d position, int range, int amount)
        {
            // Rifts use simple random spreading (no distance weighting or adaptive radius)
            int devastatedCount = 0;
            int maxAttempts = amount * 5;
            
            for (int attempt = 0; attempt < maxAttempts && devastatedCount < amount; attempt++)
            {
                int dirX = RandomNumberGenerator.GetInt32(2) == 1 ? 1 : -1;
                int dirY = RandomNumberGenerator.GetInt32(2) == 1 ? 1 : -1;
                int dirZ = RandomNumberGenerator.GetInt32(2) == 1 ? 1 : -1;

                int offsetX = RandomNumberGenerator.GetInt32(range) * dirX;
                int offsetY = RandomNumberGenerator.GetInt32(range) * dirY;
                int offsetZ = RandomNumberGenerator.GetInt32(range) * dirZ;

                BlockPos targetPos = new BlockPos(
                    (int)position.X + offsetX,
                    (int)position.Y + offsetY,
                    (int)position.Z + offsetZ
                );

                Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                
                if (block.Id == 0 || IsAlreadyDevastated(block)) continue;

                string devastatedBlock, regeneratesTo;
                if (TryGetDevastatedForm(block, out devastatedBlock, out regeneratesTo))
                {
                    Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedBlock));
                    sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
                    
                    regrowingBlocks.Add(new RegrowingBlocks
                    {
                        Pos = targetPos,
                        Out = regeneratesTo,
                        LastTime = sapi.World.Calendar.TotalHours
                    });
                    
                    devastatedCount++;
                }
            }
        }

        private int SpreadDevastationAroundPosition(Vec3d position, DevastationSource source)
        {
            // Skip if this source is fully saturated
            if (source.IsSaturated) return 0;
            
            // Apply speed multiplier to effective amount
            int effectiveAmount = Math.Max(1, (int)(source.Amount * speedMultiplier));
            
            int devastatedCount = 0;
            int maxAttempts = effectiveAmount * 5; // Try up to 5 times per block we want to devastate
            
            for (int attempt = 0; attempt < maxAttempts && devastatedCount < effectiveAmount; attempt++)
            {
                // Generate distance-weighted random offset
                // Weight towards closer blocks for natural outward spreading
                double distance = GenerateWeightedDistance(source.CurrentRadius);
                
                // Convert distance to actual offset with random direction
                double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                double angleY = (RandomNumberGenerator.GetInt32(180) - 90) * Math.PI / 180.0;
                
                int offsetX = (int)(distance * Math.Cos(angle) * Math.Cos(angleY));
                int offsetY = (int)(distance * Math.Sin(angleY));
                int offsetZ = (int)(distance * Math.Sin(angle) * Math.Cos(angleY));

                // Calculate the target position
                BlockPos targetPos = new BlockPos(
                    (int)position.X + offsetX,
                    (int)position.Y + offsetY,
                    (int)position.Z + offsetZ
                );

                // Get the block at this position
                Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                
                if (block.Id == 0) continue; // Skip air blocks, try again

                // Check if already devastated
                if (IsAlreadyDevastated(block))
                {
                    continue; // Already devastated, try again
                }

                string devastatedBlock = "";
                string regeneratesTo = "";
                
                if (TryGetDevastatedForm(block, out devastatedBlock, out regeneratesTo))
                {
                    Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedBlock));
                    sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
                    
                    // Track this block for regeneration
                    regrowingBlocks.Add(new RegrowingBlocks
                    {
                        Pos = targetPos,
                        Out = regeneratesTo,
                        LastTime = sapi.World.Calendar.TotalHours
                    });
                    
                    devastatedCount++;
                    
                    // Track for metastasis system
                    source.BlocksDevastatedTotal++;
                    source.BlocksSinceLastMetastasis++;
                }
            }
            
            return devastatedCount;
        }
        
        private double GenerateWeightedDistance(double maxDistance)
        {
            // Generate distance with bias toward closer blocks
            // Uses inverse square weighting: closer blocks much more likely
            // Formula: distance = maxDistance * (1 - sqrt(random))
            // This gives exponential bias toward center
            
            double random = RandomNumberGenerator.GetInt32(10000) / 10000.0; // 0.0 to 1.0
            double weighted = 1.0 - Math.Sqrt(random); // Bias toward 0
            return maxDistance * weighted;
        }

        private int HealDevastationAroundPosition(Vec3d position, DevastationSource source)
        {
            // Apply speed multiplier to effective amount
            int effectiveAmount = Math.Max(1, (int)(source.Amount * speedMultiplier));
            
            int healedCount = 0;
            int maxAttempts = effectiveAmount * 5;
            
            for (int attempt = 0; attempt < maxAttempts && healedCount < effectiveAmount; attempt++)
            {
                // Generate distance-weighted random offset (same as devastation)
                double distance = GenerateWeightedDistance(source.CurrentRadius);
                
                double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                double angleY = (RandomNumberGenerator.GetInt32(180) - 90) * Math.PI / 180.0;
                
                int offsetX = (int)(distance * Math.Cos(angle) * Math.Cos(angleY));
                int offsetY = (int)(distance * Math.Sin(angleY));
                int offsetZ = (int)(distance * Math.Sin(angle) * Math.Cos(angleY));

                BlockPos targetPos = new BlockPos(
                    (int)position.X + offsetX,
                    (int)position.Y + offsetY,
                    (int)position.Z + offsetZ
                );

                Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                
                if (block.Id == 0) continue;

                // Check if this is a devastated block
                if (!IsAlreadyDevastated(block))
                {
                    continue; // Not devastated, try again
                }

                // Heal the block
                string healedBlock = "";
                
                if (TryGetHealedForm(block, out healedBlock))
                {
                    if (healedBlock == "none")
                    {
                        // Remove the block (convert to air)
                        sapi.World.BlockAccessor.SetBlock(0, targetPos);
                    }
                    else
                    {
                        Block newBlock = sapi.World.GetBlock(new AssetLocation("game", healedBlock));
                        if (newBlock != null)
                        {
                            sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
                        }
                    }
                    
                    // Remove from regrowing blocks list (instant heal, no need to track)
                    regrowingBlocks.RemoveAll(rb => rb.Pos.Equals(targetPos));
                    
                    healedCount++;
                }
            }
            
            return healedCount;
        }

        private bool TryGetHealedForm(Block block, out string healedBlock)
        {
            healedBlock = "";
            string path = block.Code.Path;
            
            if (path.StartsWith("devastatedsoil-0"))
            {
                healedBlock = "soil-verylow-none";
            }
            else if (path.StartsWith("devastatedsoil-1"))
            {
                healedBlock = "sludgygravel";
            }
            else if (path.StartsWith("devastatedsoil-2"))
            {
                healedBlock = "sludgygravel";
            }
            else if (path.StartsWith("devastatedsoil-3"))
            {
                healedBlock = "log-grown-aged-ud";
            }
            else if (path.StartsWith("drock"))
            {
                healedBlock = "rock-obsidian";
            }
            else if (path.StartsWith("devastationgrowth-") || 
                     path.StartsWith("devgrowth-shrike") || 
                     path.StartsWith("devgrowth-shard") || 
                     path.StartsWith("devgrowth-bush"))
            {
                healedBlock = "none"; // Remove these growths
            }
            else if (path.StartsWith("devgrowth-thorns"))
            {
                healedBlock = "leavesbranchy-grown-oak";
            }

            return healedBlock != "";
        }

        private bool IsAlreadyDevastated(Block block)
        {
            if (block == null || block.Code == null) return false;
            
            string path = block.Code.Path;
            return path.StartsWith("devastatedsoil-") ||
                   path.StartsWith("drock") ||
                   path.StartsWith("devastationgrowth-") ||
                   path.StartsWith("devgrowth-");
        }

        /// <summary>
        /// Calculates what percentage of blocks in a spherical area are devastated.
        /// Returns a value between 0.0 and 1.0
        /// </summary>
        private double CalculateLocalDevastationPercent(Vec3d position, double radius)
        {
            int devastatedCount = 0;
            int totalConvertibleCount = 0;
            int sampleRadius = (int)Math.Ceiling(radius);
            
            // Sample blocks in a sphere around the position
            for (int x = -sampleRadius; x <= sampleRadius; x++)
            {
                for (int y = -sampleRadius; y <= sampleRadius; y++)
                {
                    for (int z = -sampleRadius; z <= sampleRadius; z++)
                    {
                        // Check if within sphere
                        double dist = Math.Sqrt(x * x + y * y + z * z);
                        if (dist > radius) continue;
                        
                        BlockPos targetPos = new BlockPos(
                            (int)position.X + x,
                            (int)position.Y + y,
                            (int)position.Z + z
                        );
                        
                        Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                        if (block == null || block.Id == 0) continue; // Skip air
                        
                        // Check if this block is devastated
                        if (IsAlreadyDevastated(block))
                        {
                            devastatedCount++;
                            totalConvertibleCount++;
                        }
                        // Check if this block CAN be devastated
                        else if (TryGetDevastatedForm(block, out _, out _))
                        {
                            totalConvertibleCount++;
                        }
                    }
                }
            }
            
            if (totalConvertibleCount == 0) return 1.0; // Nothing to devastate = fully saturated
            return (double)devastatedCount / totalConvertibleCount;
        }

        /// <summary>
        /// Finds good positions on the edge of the current devastation for spawning metastasis points.
        /// Returns positions that have non-devastated blocks nearby.
        /// </summary>
        private List<BlockPos> FindMetastasisSpawnPositions(DevastationSource source, int count)
        {
            List<BlockPos> candidates = new List<BlockPos>();
            List<BlockPos> selected = new List<BlockPos>();
            
            double edgeRadius = source.CurrentRadius;
            int probeCount = count * 8; // Probe many positions, pick the best ones
            
            // Find candidate positions on the edge of the devastated area
            for (int i = 0; i < probeCount; i++)
            {
                // Generate random position on sphere surface at edge radius
                double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                double angleY = (RandomNumberGenerator.GetInt32(180) - 90) * Math.PI / 180.0;
                
                int offsetX = (int)(edgeRadius * Math.Cos(angle) * Math.Cos(angleY));
                int offsetY = (int)(edgeRadius * Math.Sin(angleY));
                int offsetZ = (int)(edgeRadius * Math.Sin(angle) * Math.Cos(angleY));
                
                BlockPos candidatePos = new BlockPos(
                    source.Pos.X + offsetX,
                    source.Pos.Y + offsetY,
                    source.Pos.Z + offsetZ
                );
                
                // Score this position - how many non-devastated blocks are nearby?
                int nonDevastatedNearby = CountNonDevastatedNearby(candidatePos, 4);
                
                // Only consider positions with some non-devastated blocks to spread to
                if (nonDevastatedNearby > 2)
                {
                    candidates.Add(candidatePos);
                }
            }
            
            // Shuffle and pick the requested count
            candidates = candidates.OrderBy(x => RandomNumberGenerator.GetInt32(1000)).ToList();
            
            // Try to space them out - don't pick positions too close together
            foreach (var candidate in candidates)
            {
                if (selected.Count >= count) break;
                
                bool tooClose = false;
                foreach (var existing in selected)
                {
                    double dist = Math.Sqrt(
                        Math.Pow(candidate.X - existing.X, 2) +
                        Math.Pow(candidate.Y - existing.Y, 2) +
                        Math.Pow(candidate.Z - existing.Z, 2)
                    );
                    if (dist < edgeRadius * 0.5) // At least half the radius apart
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                if (!tooClose)
                {
                    selected.Add(candidate);
                }
            }
            
            return selected;
        }

        /// <summary>
        /// Counts how many non-devastated convertible blocks are near a position.
        /// </summary>
        private int CountNonDevastatedNearby(BlockPos pos, int radius)
        {
            int count = 0;
            
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos targetPos = new BlockPos(pos.X + x, pos.Y + y, pos.Z + z);
                        Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                        
                        if (block == null || block.Id == 0) continue;
                        if (IsAlreadyDevastated(block)) continue;
                        
                        if (TryGetDevastatedForm(block, out _, out _))
                        {
                            count++;
                        }
                    }
                }
            }
            
            return count;
        }

        /// <summary>
        /// Removes the oldest/most saturated sources to make room for new metastasis.
        /// Protected and healing sources are never removed.
        /// Prioritizes: saturated sources first, then by generation level (oldest first), then by blocks devastated.
        /// </summary>
        private void RemoveOldestSources(int count)
        {
            if (count <= 0 || devastationSources.Count == 0) return;
            
            // Sort sources by priority for removal:
            // 1. Saturated sources (remove first - they're done)
            // 2. Higher generation level (newer metastasis children first - keep roots alive longer)
            // 3. Higher blocks devastated (more complete)
            var sourcesToRemove = devastationSources
                .Where(s => !s.IsHealing && !s.IsProtected) // Don't remove healing or protected sources
                .OrderByDescending(s => s.IsSaturated ? 1 : 0) // Saturated first
                .ThenByDescending(s => s.GenerationLevel) // Newest generation first (remove children before parents)
                .ThenByDescending(s => s.BlocksDevastatedTotal) // Most complete first
                .Take(count)
                .ToList();
            
            foreach (var source in sourcesToRemove)
            {
                devastationSources.Remove(source);
            }
        }

        /// <summary>
        /// Spawns metastasis sources at the edge of a saturated source.
        /// </summary>
        private void SpawnMetastasisSources(DevastationSource parentSource)
        {
            // Don't spawn from healing sources
            if (parentSource.IsHealing) return;
            
            int desiredCount = 2 + RandomNumberGenerator.GetInt32(3); // 2 to 4
            
            // If at or near cap, remove oldest/saturated sources to make room
            int slotsNeeded = desiredCount - (maxSources - devastationSources.Count);
            if (slotsNeeded > 0)
            {
                RemoveOldestSources(slotsNeeded);
            }
            
            // Calculate how many we can actually spawn
            int maxPossible = maxSources - devastationSources.Count;
            int metastasisCount = Math.Min(desiredCount, maxPossible);
            
            if (metastasisCount <= 0)
            {
                // Still can't spawn (shouldn't happen, but safety check)
                return;
            }
            
            // Find good positions for new sources
            List<BlockPos> spawnPositions = FindMetastasisSpawnPositions(parentSource, metastasisCount);
            
            if (spawnPositions.Count == 0)
            {
                // No good positions found at edge - try long-range search before giving up
                spawnPositions = FindLongRangeMetastasisPositions(parentSource, metastasisCount);
            }
            
            if (spawnPositions.Count == 0)
            {
                // Truly no viable land found - mark as saturated
                parentSource.IsSaturated = true;
                return;
            }
            
            // Ensure parent has a source ID for tracking
            if (string.IsNullOrEmpty(parentSource.SourceId))
            {
                parentSource.SourceId = GenerateSourceId();
            }
            
            // Spawn new sources at each position
            foreach (BlockPos spawnPos in spawnPositions)
            {
                DevastationSource metastasis = new DevastationSource
                {
                    Pos = spawnPos.Copy(),
                    Range = parentSource.Range,
                    Amount = parentSource.Amount,
                    CurrentRadius = 3.0, // Start small
                    IsHealing = false,
                    IsMetastasis = true,
                    GenerationLevel = parentSource.GenerationLevel + 1,
                    MetastasisThreshold = parentSource.MetastasisThreshold,
                    MaxGenerationLevel = parentSource.MaxGenerationLevel,
                    SourceId = GenerateSourceId(),
                    ParentSourceId = parentSource.SourceId
                };
                
                devastationSources.Add(metastasis);
            }
            
            // Reset parent's counter for next metastasis cycle
            parentSource.BlocksSinceLastMetastasis = 0;
            
            // Mark parent as saturated since it has spawned children to continue the spread
            parentSource.IsSaturated = true;
        }
        
        /// <summary>
        /// Spawns metastasis with extended long-range search for when a source is stalled.
        /// Returns true if at least one metastasis was spawned.
        /// </summary>
        private bool SpawnMetastasisSourcesWithLongRangeSearch(DevastationSource parentSource)
        {
            if (parentSource.IsHealing) return false;
            
            int desiredCount = 1 + RandomNumberGenerator.GetInt32(2); // 1 to 2 (fewer when searching long-range)
            
            // Make room if needed
            int slotsNeeded = desiredCount - (maxSources - devastationSources.Count);
            if (slotsNeeded > 0)
            {
                RemoveOldestSources(slotsNeeded);
            }
            
            int maxPossible = maxSources - devastationSources.Count;
            int metastasisCount = Math.Min(desiredCount, maxPossible);
            
            if (metastasisCount <= 0) return false;
            
            // Try normal edge positions first
            List<BlockPos> spawnPositions = FindMetastasisSpawnPositions(parentSource, metastasisCount);
            
            // If that failed, try long-range search
            if (spawnPositions.Count == 0)
            {
                spawnPositions = FindLongRangeMetastasisPositions(parentSource, metastasisCount);
            }
            
            if (spawnPositions.Count == 0) return false;
            
            // Ensure parent has a source ID
            if (string.IsNullOrEmpty(parentSource.SourceId))
            {
                parentSource.SourceId = GenerateSourceId();
            }
            
            // Spawn new sources
            foreach (BlockPos spawnPos in spawnPositions)
            {
                DevastationSource metastasis = new DevastationSource
                {
                    Pos = spawnPos.Copy(),
                    Range = parentSource.Range,
                    Amount = parentSource.Amount,
                    CurrentRadius = 3.0,
                    IsHealing = false,
                    IsMetastasis = true,
                    GenerationLevel = parentSource.GenerationLevel + 1,
                    MetastasisThreshold = parentSource.MetastasisThreshold,
                    MaxGenerationLevel = parentSource.MaxGenerationLevel,
                    SourceId = GenerateSourceId(),
                    ParentSourceId = parentSource.SourceId
                };
                
                devastationSources.Add(metastasis);
            }
            
            parentSource.BlocksSinceLastMetastasis = 0;
            parentSource.IsSaturated = true; // This source has done its job
            return true;
        }
        
        /// <summary>
        /// Searches much further out (2x to 8x the normal range) to find undevastated land.
        /// This allows the devastation to "leap over" obstacles like oceans or voids.
        /// </summary>
        private List<BlockPos> FindLongRangeMetastasisPositions(DevastationSource source, int count)
        {
            List<BlockPos> candidates = new List<BlockPos>();
            List<BlockPos> selected = new List<BlockPos>();
            
            // Search at multiple distance rings, starting further out
            int[] searchDistances = new int[] { 
                source.Range * 2, 
                source.Range * 4, 
                source.Range * 6,
                source.Range * 8 
            };
            
            foreach (int searchDist in searchDistances)
            {
                // Cap maximum search distance to prevent searching too far
                int cappedDist = Math.Min(searchDist, 128);
                int probeCount = count * 16; // More probes for long-range search
                
                for (int i = 0; i < probeCount; i++)
                {
                    // Generate random position at this distance
                    double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                    double angleY = (RandomNumberGenerator.GetInt32(60) - 30) * Math.PI / 180.0; // Flatter angle for long-range
                    
                    int offsetX = (int)(cappedDist * Math.Cos(angle) * Math.Cos(angleY));
                    int offsetY = (int)(cappedDist * Math.Sin(angleY));
                    int offsetZ = (int)(cappedDist * Math.Sin(angle) * Math.Cos(angleY));
                    
                    BlockPos candidatePos = new BlockPos(
                        source.Pos.X + offsetX,
                        source.Pos.Y + offsetY,
                        source.Pos.Z + offsetZ
                    );
                    
                    // Check if there's viable land here (need more blocks for long-range to be worth it)
                    int nonDevastatedNearby = CountNonDevastatedNearby(candidatePos, 6);
                    
                    if (nonDevastatedNearby > 10) // Higher threshold for long-range jumps
                    {
                        candidates.Add(candidatePos);
                    }
                }
                
                // If we found candidates at this distance, stop searching further
                if (candidates.Count >= count)
                {
                    break;
                }
            }
            
            // Select best candidates, spaced apart
            candidates = candidates.OrderByDescending(c => CountNonDevastatedNearby(c, 6)).ToList();
            
            foreach (var candidate in candidates)
            {
                if (selected.Count >= count) break;
                
                bool tooClose = false;
                foreach (var existing in selected)
                {
                    double dist = Math.Sqrt(
                        Math.Pow(candidate.X - existing.X, 2) +
                        Math.Pow(candidate.Y - existing.Y, 2) +
                        Math.Pow(candidate.Z - existing.Z, 2)
                    );
                    if (dist < source.Range) // Must be at least one range apart
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                // Also check not too close to existing sources
                if (!tooClose)
                {
                    foreach (var existingSource in devastationSources)
                    {
                        double dist = Math.Sqrt(
                            Math.Pow(candidate.X - existingSource.Pos.X, 2) +
                            Math.Pow(candidate.Y - existingSource.Pos.Y, 2) +
                            Math.Pow(candidate.Z - existingSource.Pos.Z, 2)
                        );
                        if (dist < source.Range * 0.5) // Not too close to existing sources
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }
                
                if (!tooClose)
                {
                    selected.Add(candidate);
                }
            }
            
            return selected;
        }

        private bool TryGetDevastatedForm(Block block, out string devastatedBlock, out string regeneratesTo)
        {
            devastatedBlock = "";
            regeneratesTo = "";

            // Determine what this block should become when devastated
            string path = block.Code.Path;
            
            if (path.StartsWith("soil-"))
            {
                devastatedBlock = "devastatedsoil-0";
                regeneratesTo = "soil-verylow-none";
            }
            else if (path.StartsWith("rock-"))
            {
                devastatedBlock = "drock";
                regeneratesTo = "rock-obsidian";
            }
            else if (path.StartsWith("tallgrass-"))
            {
                devastatedBlock = "devastationgrowth-normal";
                regeneratesTo = "none";
            }
            else if (path.StartsWith("smallberrybush-") || path.StartsWith("largeberrybush-"))
            {
                devastatedBlock = "devgrowth-thorns";
                regeneratesTo = "leavesbranchy-grown-oak";
            }
            else if (path.StartsWith("flower-") || path.StartsWith("fern-"))
            {
                devastatedBlock = "devgrowth-shrike";
                regeneratesTo = "none";
            }
            else if (path.StartsWith("crop-"))
            {
                devastatedBlock = "devgrowth-shard";
                regeneratesTo = "none";
            }
            else if (path.StartsWith("leavesbranchy-") || path.StartsWith("leaves-"))
            {
                devastatedBlock = "devgrowth-bush";
                regeneratesTo = "none";
            }
            else if (path.StartsWith("gravel-"))
            {
                devastatedBlock = "devastatedsoil-1";
                regeneratesTo = "sludgygravel";
            }
            else if (path.StartsWith("sand-"))
            {
                devastatedBlock = "devastatedsoil-2";
                regeneratesTo = "sludgygravel";
            }
            else if (path.StartsWith("log-"))
            {
                devastatedBlock = "devastatedsoil-3";
                regeneratesTo = "log-grown-aged-ud";
            }

            return devastatedBlock != "";
        }
    }
}

