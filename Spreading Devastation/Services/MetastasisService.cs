using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using SpreadingDevastation.Models;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SpreadingDevastation.Services
{
    /// <summary>
    /// Handles metastasis (child source spawning) logic.
    /// Single Responsibility: Managing child source creation and saturation.
    /// </summary>
    public class MetastasisService
    {
        private readonly ICoreServerAPI _api;
        private readonly SpreadingDevastationConfig _config;
        private readonly PositionService _positionService;
        private readonly SourceManager _sourceManager;

        public MetastasisService(
            ICoreServerAPI api,
            SpreadingDevastationConfig config,
            PositionService positionService,
            SourceManager sourceManager)
        {
            _api = api;
            _config = config;
            _positionService = positionService;
            _sourceManager = sourceManager;
        }

        /// <summary>
        /// Checks if a source should attempt to spawn children based on its state.
        /// </summary>
        public bool ShouldAttemptSpawn(DevastationSource source)
        {
            if (source.IsHealing || source.IsSaturated) return false;
            
            return source.BlocksSinceLastMetastasis >= source.MetastasisThreshold &&
                   source.CurrentRadius >= source.Range;
        }

        /// <summary>
        /// Checks if a stalled source should attempt emergency metastasis.
        /// </summary>
        public bool ShouldAttemptStalledSpawn(DevastationSource source)
        {
            return source.StallCounter >= 10 && !source.IsHealing;
        }

        /// <summary>
        /// Attempts to spawn a child source from a parent.
        /// Returns true if successful.
        /// </summary>
        public bool TrySpawnChild(DevastationSource parent, double currentGameTime)
        {
            if (parent.IsHealing) return false;
            
            // Check spawn delay
            double effectiveDelay = _config.ChildSpawnDelaySeconds / Math.Max(0.1, _config.SpeedMultiplier);
            double delayInHours = effectiveDelay / 3600.0;
            
            if (parent.LastChildSpawnTime > 0)
            {
                double timeSinceLastSpawn = currentGameTime - parent.LastChildSpawnTime;
                if (timeSinceLastSpawn < delayInHours)
                {
                    return false;
                }
            }
            
            // Ensure capacity
            if (!_sourceManager.EnsureCapacity(1))
            {
                return false;
            }
            
            // Find spawn position
            BlockPos spawnPos = FindSpawnPosition(parent);
            if (spawnPos == null)
            {
                return false;
            }
            
            // Ensure parent has an ID
            if (string.IsNullOrEmpty(parent.SourceId))
            {
                parent.SourceId = _sourceManager.GenerateSourceId();
            }
            
            // Create child
            var child = CreateChildSource(parent, spawnPos);
            _sourceManager.AddSource(child);
            
            // Update parent
            parent.ChildrenSpawned++;
            parent.LastChildSpawnTime = currentGameTime;
            parent.BlocksSinceLastMetastasis = 0;
            parent.FailedSpawnAttempts = 0;
            
            // Mark saturated after enough children
            if (parent.ChildrenSpawned >= 3)
            {
                parent.IsSaturated = true;
            }
            
            return true;
        }

        /// <summary>
        /// Handles failed spawn attempt for stall detection.
        /// </summary>
        public void RecordFailedSpawn(DevastationSource source)
        {
            source.FailedSpawnAttempts++;
            
            if (source.FailedSpawnAttempts >= _config.MaxFailedSpawnAttempts)
            {
                source.IsSaturated = true;
            }
        }

        /// <summary>
        /// Checks saturation and spawns children if ready.
        /// </summary>
        public void ProcessMetastasis(DevastationSource source, double currentGameTime)
        {
            if (!ShouldAttemptSpawn(source)) return;
            
            double saturation = _positionService.CalculateLocalDevastationPercent(
                source.Pos.ToVec3d(), 
                source.CurrentRadius);
            
            if (saturation >= _config.SaturationThreshold)
            {
                TrySpawnChild(source, currentGameTime);
            }
        }

        /// <summary>
        /// Processes stalled source metastasis.
        /// </summary>
        public void ProcessStalledMetastasis(DevastationSource source, double currentGameTime)
        {
            if (!ShouldAttemptStalledSpawn(source)) return;
            
            bool spawned = TrySpawnChild(source, currentGameTime);
            
            if (spawned)
            {
                source.StallCounter = 0;
            }
            else
            {
                RecordFailedSpawn(source);
            }
        }

        private BlockPos FindSpawnPosition(DevastationSource parent)
        {
            var allSources = _sourceManager.GetAllSources();
            
            // Try pillar strategy first
            BlockPos pos = _positionService.FindMetastasisPositionPillar(parent, allSources);
            
            if (pos == null)
            {
                // Fall back to long-range search
                var longRangePositions = _positionService.FindLongRangeMetastasisPositions(parent, allSources, 1);
                if (longRangePositions.Count > 0)
                {
                    pos = longRangePositions[0];
                }
            }
            
            // Check min Y level
            if (pos != null && pos.Y < _config.MinYLevel)
            {
                return null;
            }
            
            return pos;
        }

        private DevastationSource CreateChildSource(DevastationSource parent, BlockPos position)
        {
            int childRange = CalculateChildRange(parent.Range);
            
            return new DevastationSource
            {
                Pos = position.Copy(),
                Range = childRange,
                Amount = parent.Amount,
                CurrentRadius = 3.0,
                IsHealing = false,
                IsMetastasis = true,
                GenerationLevel = parent.GenerationLevel + 1,
                MetastasisThreshold = _config.MetastasisThreshold,
                MaxGenerationLevel = parent.MaxGenerationLevel,
                SourceId = _sourceManager.GenerateSourceId(),
                ParentSourceId = parent.SourceId
            };
        }

        private int CalculateChildRange(int parentRange)
        {
            double variation = _config.MetastasisRadiusVariation;
            double minMultiplier = 1.0 - variation;
            double maxMultiplier = 1.0 + variation;
            
            double multiplier = minMultiplier + (RandomNumberGenerator.GetInt32(1001) / 1000.0) * (maxMultiplier - minMultiplier);
            
            int childRange = (int)Math.Round(parentRange * multiplier);
            return Math.Clamp(childRange, 3, 128);
        }
    }
}

