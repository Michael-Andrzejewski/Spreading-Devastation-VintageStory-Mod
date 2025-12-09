using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using SpreadingDevastation.Models;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SpreadingDevastation.Services
{
    /// <summary>
    /// Handles the core devastation and healing logic.
    /// Single Responsibility: Block transformation during spreading/healing.
    /// </summary>
    public class DevastationService
    {
        private readonly ICoreServerAPI _api;
        private readonly SpreadingDevastationConfig _config;
        private readonly BlockTransformationService _blockService;
        private readonly PositionService _positionService;
        private readonly List<RegrowingBlock> _regrowingBlocks;

        public DevastationService(
            ICoreServerAPI api,
            SpreadingDevastationConfig config,
            BlockTransformationService blockService,
            PositionService positionService,
            List<RegrowingBlock> regrowingBlocks)
        {
            _api = api;
            _config = config;
            _blockService = blockService;
            _positionService = positionService;
            _regrowingBlocks = regrowingBlocks;
        }

        /// <summary>
        /// Spreads devastation from a rift position (simple random spreading).
        /// </summary>
        public void SpreadFromRift(Vec3d position, int range, int amount)
        {
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

                if (TryDevastateBlock(targetPos))
                {
                    devastatedCount++;
                }
            }
        }

        /// <summary>
        /// Spreads devastation around a source position using distance-weighted selection.
        /// Returns the number of blocks devastated.
        /// </summary>
        public int SpreadFromSource(DevastationSource source)
        {
            if (source.IsSaturated) return 0;
            
            int effectiveAmount = Math.Max(1, (int)(source.Amount * _config.SpeedMultiplier));
            int devastatedCount = 0;
            int maxAttempts = effectiveAmount * 5;
            
            Vec3d position = source.Pos.ToVec3d();
            
            for (int attempt = 0; attempt < maxAttempts && devastatedCount < effectiveAmount; attempt++)
            {
                var (offsetX, offsetY, offsetZ) = _positionService.GenerateWeightedOffset(source.CurrentRadius);

                BlockPos targetPos = new BlockPos(
                    (int)position.X + offsetX,
                    (int)position.Y + offsetY,
                    (int)position.Z + offsetZ
                );

                if (TryDevastateBlock(targetPos))
                {
                    devastatedCount++;
                    source.RecordDevastation();
                }
            }
            
            return devastatedCount;
        }

        /// <summary>
        /// Heals devastation around a source position.
        /// Returns the number of blocks healed.
        /// </summary>
        public int HealFromSource(DevastationSource source)
        {
            int effectiveAmount = Math.Max(1, (int)(source.Amount * _config.SpeedMultiplier));
            int healedCount = 0;
            int maxAttempts = effectiveAmount * 5;
            
            Vec3d position = source.Pos.ToVec3d();
            
            for (int attempt = 0; attempt < maxAttempts && healedCount < effectiveAmount; attempt++)
            {
                var (offsetX, offsetY, offsetZ) = _positionService.GenerateWeightedOffset(source.CurrentRadius);

                BlockPos targetPos = new BlockPos(
                    (int)position.X + offsetX,
                    (int)position.Y + offsetY,
                    (int)position.Z + offsetZ
                );

                if (TryHealBlock(targetPos))
                {
                    healedCount++;
                }
            }
            
            return healedCount;
        }

        /// <summary>
        /// Updates source state based on success rate and handles radius expansion.
        /// </summary>
        public void UpdateSourceState(DevastationSource source, int processed)
        {
            int effectiveAmount = Math.Max(1, (int)(source.Amount * _config.SpeedMultiplier));
            source.SuccessfulAttempts += processed;
            source.TotalAttempts += effectiveAmount * 5;
            
            if (source.TotalAttempts >= 100)
            {
                double successRate = (double)source.SuccessfulAttempts / source.TotalAttempts;
                
                if (successRate < _config.LowSuccessThreshold && source.CurrentRadius < source.Range)
                {
                    double expansion = successRate < (_config.LowSuccessThreshold / 2) ? 4.0 : 2.0;
                    
                    if (source.IsRelocateProtected)
                    {
                        expansion *= 2.0;
                    }
                    
                    source.CurrentRadius = Math.Min(source.CurrentRadius + expansion, source.Range);
                    source.StallCounter = 0;
                }
                else if (successRate < _config.VeryLowSuccessThreshold && 
                         source.CurrentRadius >= source.Range && 
                         !source.IsHealing)
                {
                    if (source.IsRelocateProtected)
                    {
                        source.StallCounter = 0;
                    }
                    else
                    {
                        source.StallCounter++;
                    }
                }
                else
                {
                    source.StallCounter = 0;
                }
                
                source.SuccessfulAttempts = 0;
                source.TotalAttempts = 0;
            }
        }

        /// <summary>
        /// Processes block regeneration.
        /// Returns the number of blocks regenerated.
        /// </summary>
        public int ProcessRegeneration()
        {
            if (_regrowingBlocks == null || _regrowingBlocks.Count == 0)
            {
                return 0;
            }

            var blocksToRemove = new List<RegrowingBlock>();
            double currentHours = _api.World.Calendar.TotalHours;
            int maxRegenPerTick = 50;
            int regeneratedCount = 0;
            
            foreach (var block in _regrowingBlocks)
            {
                if (regeneratedCount >= maxRegenPerTick) break;
                
                if (block.Pos == null)
                {
                    blocksToRemove.Add(block);
                    continue;
                }
                
                double timeDiff = currentHours - block.LastTime;
                
                // Handle time going backwards
                if (timeDiff < -24.0)
                {
                    block.LastTime = currentHours;
                    continue;
                }
                
                if (timeDiff > _config.RegenerationHours)
                {
                    RegenerateBlock(block);
                    blocksToRemove.Add(block);
                    regeneratedCount++;
                }
            }
            
            foreach (var block in blocksToRemove)
            {
                _regrowingBlocks.Remove(block);
            }
            
            return regeneratedCount;
        }

        private bool TryDevastateBlock(BlockPos targetPos)
        {
            Block block = _api.World.BlockAccessor.GetBlock(targetPos);
            
            if (block == null || block.Id == 0) return false;
            if (_blockService.IsDevastated(block)) return false;
            
            var transformation = _blockService.GetDevastatedForm(block);
            if (!transformation.IsValid) return false;
            
            Block newBlock = _blockService.GetBlock(transformation.DevastatedForm);
            if (newBlock == null) return false;
            
            _api.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
            
            _regrowingBlocks.Add(new RegrowingBlock
            {
                Pos = targetPos,
                RegeneratesTo = transformation.RegeneratesTo,
                LastTime = _api.World.Calendar.TotalHours
            });
            
            return true;
        }

        private bool TryHealBlock(BlockPos targetPos)
        {
            Block block = _api.World.BlockAccessor.GetBlock(targetPos);
            
            if (block == null || block.Id == 0) return false;
            if (!_blockService.IsDevastated(block)) return false;
            
            string healedForm = _blockService.GetHealedForm(block);
            if (healedForm == null) return false;
            
            if (healedForm == "none")
            {
                _api.World.BlockAccessor.SetBlock(0, targetPos);
            }
            else
            {
                Block newBlock = _blockService.GetBlock(healedForm);
                if (newBlock != null)
                {
                    _api.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
                }
            }
            
            _regrowingBlocks.RemoveAll(rb => rb.Pos.Equals(targetPos));
            
            return true;
        }

        private void RegenerateBlock(RegrowingBlock block)
        {
            if (block.RegeneratesTo == "none")
            {
                _api.World.BlockAccessor.SetBlock(0, block.Pos);
            }
            else
            {
                Block newBlock = _blockService.GetBlock(block.RegeneratesTo);
                if (newBlock != null && newBlock.Id != 0)
                {
                    _api.World.BlockAccessor.SetBlock(newBlock.Id, block.Pos);
                }
            }
        }
    }
}

