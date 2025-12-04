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
        }

        private ICoreServerAPI sapi;
        private List<RegrowingBlocks> regrowingBlocks;
        private List<DevastationSource> devastationSources;

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
        }

        private void OnSaveGameLoading()
        {
            byte[] data = sapi.WorldManager.SaveGame.GetData("regrowingBlocks");
            regrowingBlocks = data == null ? new List<RegrowingBlocks>() : SerializerUtil.Deserialize<List<RegrowingBlocks>>(data);
            
            byte[] sourcesData = sapi.WorldManager.SaveGame.GetData("devastationSources");
            devastationSources = sourcesData == null ? new List<DevastationSource>() : SerializerUtil.Deserialize<List<DevastationSource>>(sourcesData);
        }

        private void OnSaveGameSaving()
        {
            sapi.WorldManager.SaveGame.StoreData("regrowingBlocks", SerializerUtil.Serialize(regrowingBlocks));
            sapi.WorldManager.SaveGame.StoreData("devastationSources", SerializerUtil.Serialize(devastationSources));
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
                    else
                    {
                        // Put the arg back and do normal single remove
                        BlockSelection blockSel = player.CurrentBlockSelection;
                        if (blockSel == null)
                        {
                            player.SendMessage(groupId, "Look at a block to remove it as a devastation source, or use 'remove all'", EnumChatType.CommandError);
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
                    if (devastationSources.Count == 0)
                    {
                        player.SendMessage(groupId, "No manual devastation sources set", EnumChatType.Notification);
                    }
                    else
                    {
                        player.SendMessage(groupId, $"Devastation sources ({devastationSources.Count}):", EnumChatType.Notification);
                        foreach (var source in devastationSources)
                        {
                            double successRate = source.TotalAttempts > 0 ? (double)source.SuccessfulAttempts / source.TotalAttempts * 100 : 0;
                            string type = source.IsHealing ? "HEAL" : "DEVASTATE";
                            player.SendMessage(groupId, $"  [{type}] {source.Pos} - Range: {source.Range}, Current: {source.CurrentRadius:F1}, Amount: {source.Amount}/tick, Success: {successRate:F0}%", EnumChatType.Notification);
                        }
                    }
                    break;
                    
                default:
                    player.SendMessage(groupId, "Usage: /devastate [add|heal|remove|list] [range <blocks>] [amount <count>]", EnumChatType.CommandError);
                    player.SendMessage(groupId, "Examples:", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate add range 16 amount 10  - Spread devastation", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate heal range 16 amount 10 - Heal devastation", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate remove all             - Remove all sources", EnumChatType.CommandError);
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
            
            devastationSources.Add(new DevastationSource 
            { 
                Pos = pos,
                Range = range,
                Amount = amount,
                CurrentRadius = Math.Min(3.0, range),
                IsHealing = isHealing
            });
            
            string action = isHealing ? "healing" : "devastation";
            player.SendMessage(groupId, $"Added {action} source at {pos} (range: {range}, amount: {amount} blocks per tick)", EnumChatType.CommandSuccess);
        }

        private void RegenerateBlocks(float dt)
        {
            List<RegrowingBlocks> blocksToRemove = new List<RegrowingBlocks>();
            
            if (sapi == null || regrowingBlocks == null)
            {
                return;
            }

            // Check each devastated block to see if it's time to regenerate
            foreach (RegrowingBlocks regrowingBlock in regrowingBlocks)
            {
                // Regenerate after 60 in-game hours
                if (sapi.World.Calendar.TotalHours - regrowingBlock.LastTime > 60.0)
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
                        sapi.World.BlockAccessor.SetBlock(block.Id, regrowingBlock.Pos);
                    }
                    
                    blocksToRemove.Add(regrowingBlock);
                }
            }

            // Remove regenerated blocks from the tracking list
            foreach (RegrowingBlocks item in blocksToRemove)
            {
                regrowingBlocks.Remove(item);
            }
        }

        private void SpreadDevastationFromRifts(float dt)
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
                source.SuccessfulAttempts += processed;
                source.TotalAttempts += source.Amount * 5; // We try up to 5 times per block
                
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
                    }
                    
                    // Reset counters
                    source.SuccessfulAttempts = 0;
                    source.TotalAttempts = 0;
                }
            }
            
            // Clean up removed sources
            foreach (var source in toRemove)
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
            int devastatedCount = 0;
            int maxAttempts = source.Amount * 5; // Try up to 5 times per block we want to devastate
            
            for (int attempt = 0; attempt < maxAttempts && devastatedCount < source.Amount; attempt++)
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
            int healedCount = 0;
            int maxAttempts = source.Amount * 5;
            
            for (int attempt = 0; attempt < maxAttempts && healedCount < source.Amount; attempt++)
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

