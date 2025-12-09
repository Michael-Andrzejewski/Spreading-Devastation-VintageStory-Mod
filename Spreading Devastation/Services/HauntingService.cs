using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using SpreadingDevastation.Models;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SpreadingDevastation.Services
{
    /// <summary>
    /// Handles the player haunting system that relocates sources toward players.
    /// Single Responsibility: Player-directed source movement.
    /// </summary>
    public class HauntingService
    {
        private readonly ICoreServerAPI _api;
        private readonly SpreadingDevastationConfig _config;
        private readonly PositionService _positionService;
        private readonly SourceManager _sourceManager;

        private int _tickCounter = 0;
        private int _burstRemaining = 0;

        public int BurstRemaining => _burstRemaining;

        public HauntingService(
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
        /// Updates the haunting timer and processes relocations.
        /// Should be called every tick.
        /// </summary>
        public void Update()
        {
            if (!_config.EnablePlayerHaunting) return;

            _tickCounter++;

            int ticksPerSecond = 100;
            int ticksNeeded = (int)(_config.HauntingIntervalSeconds * ticksPerSecond / Math.Max(0.1, _config.SpeedMultiplier));
            ticksNeeded = Math.Max(10, ticksNeeded);

            // Handle burst relocation
            if (_burstRemaining > 0)
            {
                bool relocated = PerformRelocate();
                if (relocated)
                {
                    _burstRemaining--;
                }
                else
                {
                    _burstRemaining = 0;
                }
                return;
            }

            // Check if it's time for a new burst
            if (_tickCounter >= ticksNeeded)
            {
                _burstRemaining = _config.HauntingBurstCount;
                _tickCounter = 0;
            }
        }

        /// <summary>
        /// Forces a haunting burst to start immediately.
        /// </summary>
        public void ForceBurst()
        {
            _burstRemaining = _config.HauntingBurstCount;
            _tickCounter = 0;
        }

        /// <summary>
        /// Resets the haunting state.
        /// </summary>
        public void Reset()
        {
            _burstRemaining = 0;
            _tickCounter = 0;
        }

        /// <summary>
        /// Gets the time until the next haunting burst.
        /// </summary>
        public double GetSecondsUntilNextBurst()
        {
            int ticksPerSecond = 100;
            int ticksNeeded = (int)(_config.HauntingIntervalSeconds * ticksPerSecond / Math.Max(0.1, _config.SpeedMultiplier));
            return Math.Max(0, (ticksNeeded - _tickCounter) / 100.0);
        }

        /// <summary>
        /// Gets haunting status information.
        /// </summary>
        public HauntingStatus GetStatus()
        {
            var status = new HauntingStatus
            {
                Enabled = _config.EnablePlayerHaunting,
                BurstRemaining = _burstRemaining,
                SecondsUntilNextBurst = GetSecondsUntilNextBurst(),
                MovableSourceCount = _sourceManager.GetMovableSources().Count()
            };

            // Find nearest player distance
            var movableSources = _sourceManager.GetMovableSources().ToList();
            var onlinePlayers = GetOnlinePlayers().ToList();

            if (movableSources.Count > 0 && onlinePlayers.Count > 0)
            {
                double minDist = double.MaxValue;
                foreach (var source in movableSources)
                {
                    foreach (var player in onlinePlayers)
                    {
                        Vec3d playerPos = player.Entity.Pos.XYZ;
                        double dist = Math.Sqrt(
                            Math.Pow(playerPos.X - source.Pos.X, 2) +
                            Math.Pow(playerPos.Z - source.Pos.Z, 2));
                        if (dist < minDist) minDist = dist;
                    }
                }
                status.NearestPlayerDistance = minDist;
            }

            return status;
        }

        private bool PerformRelocate()
        {
            var onlinePlayers = GetOnlinePlayers().ToList();
            if (onlinePlayers.Count == 0) return false;

            var movableSources = _sourceManager.GetMovableSources().ToList();
            if (movableSources.Count == 0) return false;

            // Find the best source-player pair
            DevastationSource bestSource = null;
            IServerPlayer bestPlayer = null;
            double bestDistSq = double.MaxValue;

            foreach (var source in movableSources)
            {
                foreach (var player in onlinePlayers)
                {
                    Vec3d playerPos = player.Entity.Pos.XYZ;
                    double distSq =
                        Math.Pow(playerPos.X - source.Pos.X, 2) +
                        Math.Pow(playerPos.Z - source.Pos.Z, 2);

                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestSource = source;
                        bestPlayer = player;
                    }
                }
            }

            if (bestSource == null || bestPlayer == null) return false;

            double horizontalDist = Math.Sqrt(bestDistSq);

            // Don't haunt if too close
            if (horizontalDist < _config.HauntingMinDistance)
            {
                return false;
            }

            // Calculate new position
            Vec3d sourcePos = bestSource.Pos.ToVec3d();
            Vec3d playerPos2 = bestPlayer.Entity.Pos.XYZ;

            double dx = playerPos2.X - sourcePos.X;
            double dz = playerPos2.Z - sourcePos.Z;

            double leapDist = horizontalDist * _config.HauntingLeapFraction;
            leapDist = Math.Min(leapDist, _config.HauntingMaxLeapDistance);

            // Add angular variance
            double baseAngle = Math.Atan2(dz, dx);
            double varianceRad = _config.HauntingAngleVariance * Math.PI / 180.0;
            double angleVariation = (RandomNumberGenerator.GetInt32(2001) - 1000) / 1000.0 * varianceRad;
            double finalAngle = baseAngle + angleVariation;

            int targetX = (int)(sourcePos.X + leapDist * Math.Cos(finalAngle));
            int targetZ = (int)(sourcePos.Z + leapDist * Math.Sin(finalAngle));

            // Find valid Y position
            BlockPos newPos = _positionService.FindValidPositionAtXZ(
                targetX,
                bestSource.Pos.Y,
                targetZ,
                _config.PillarSearchHeight * 2);

            if (newPos == null) return false;
            if (newPos.Y < _config.MinYLevel) return false;

            // Check not too close to other sources
            if (_positionService.IsTooCloseToSources(newPos, _sourceManager.GetAllSources(), bestSource.Range * 0.5, bestSource))
            {
                return false;
            }

            // Move the source
            bestSource.Pos = newPos.Copy();
            bestSource.ResetSpreadingState();

            return true;
        }

        private IEnumerable<IServerPlayer> GetOnlinePlayers()
        {
            return _api.World.AllOnlinePlayers
                .OfType<IServerPlayer>()
                .Where(p => p.Entity != null && p.Entity.Alive);
        }
    }

    /// <summary>
    /// Status information about the haunting system.
    /// </summary>
    public class HauntingStatus
    {
        public bool Enabled { get; set; }
        public int BurstRemaining { get; set; }
        public double SecondsUntilNextBurst { get; set; }
        public int MovableSourceCount { get; set; }
        public double? NearestPlayerDistance { get; set; }
    }
}

