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
    /// Handles position finding and validation for devastation spreading and metastasis.
    /// </summary>
    public class PositionService
    {
        private readonly ICoreServerAPI _api;
        private readonly SpreadingDevastationConfig _config;
        private readonly BlockTransformationService _blockService;

        public PositionService(ICoreServerAPI api, SpreadingDevastationConfig config, BlockTransformationService blockService)
        {
            _api = api;
            _config = config;
            _blockService = blockService;
        }

        /// <summary>
        /// Generates a weighted random offset from a center point.
        /// Biased toward closer distances for natural outward spreading.
        /// </summary>
        public (int x, int y, int z) GenerateWeightedOffset(double maxDistance)
        {
            double distance = GenerateWeightedDistance(maxDistance);
            
            double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
            double angleY = (RandomNumberGenerator.GetInt32(180) - 90) * Math.PI / 180.0;
            
            int offsetX = (int)(distance * Math.Cos(angle) * Math.Cos(angleY));
            int offsetY = (int)(distance * Math.Sin(angleY));
            int offsetZ = (int)(distance * Math.Sin(angle) * Math.Cos(angleY));

            return (offsetX, offsetY, offsetZ);
        }

        /// <summary>
        /// Generates a distance with bias toward closer blocks.
        /// Uses inverse square weighting for exponential bias toward center.
        /// </summary>
        public double GenerateWeightedDistance(double maxDistance)
        {
            double random = RandomNumberGenerator.GetInt32(10000) / 10000.0;
            double weighted = 1.0 - Math.Sqrt(random);
            return maxDistance * weighted;
        }

        /// <summary>
        /// Checks if a position is adjacent to at least one air block.
        /// </summary>
        public bool IsAdjacentToAir(BlockPos pos)
        {
            var offsets = new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };
            
            foreach (var (dx, dy, dz) in offsets)
            {
                var neighbor = new BlockPos(pos.X + dx, pos.Y + dy, pos.Z + dz);
                Block block = _api.World.BlockAccessor.GetBlock(neighbor);
                if (block != null && block.Id == 0)
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Checks if a position is too close to any existing devastation sources.
        /// </summary>
        public bool IsTooCloseToSources(BlockPos pos, IEnumerable<DevastationSource> sources, double minDistance, DevastationSource excludeSource = null)
        {
            foreach (var source in sources)
            {
                if (source == excludeSource) continue;
                
                double dist = CalculateDistance(pos, source.Pos);
                if (dist < minDistance) return true;
            }
            return false;
        }

        /// <summary>
        /// Calculates Euclidean distance between two positions.
        /// </summary>
        public double CalculateDistance(BlockPos a, BlockPos b)
        {
            return Math.Sqrt(
                Math.Pow(a.X - b.X, 2) +
                Math.Pow(a.Y - b.Y, 2) +
                Math.Pow(a.Z - b.Z, 2)
            );
        }

        /// <summary>
        /// Calculates horizontal distance between two positions.
        /// </summary>
        public double CalculateHorizontalDistance(BlockPos a, BlockPos b)
        {
            return Math.Sqrt(
                Math.Pow(a.X - b.X, 2) +
                Math.Pow(a.Z - b.Z, 2)
            );
        }

        /// <summary>
        /// Counts non-devastated convertible blocks within a radius.
        /// </summary>
        public int CountNonDevastatedNearby(BlockPos pos, int radius)
        {
            int count = 0;
            
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos targetPos = new BlockPos(pos.X + x, pos.Y + y, pos.Z + z);
                        Block block = _api.World.BlockAccessor.GetBlock(targetPos);
                        
                        if (block == null || block.Id == 0) continue;
                        if (_blockService.IsDevastated(block)) continue;
                        
                        if (_blockService.GetDevastatedForm(block).IsValid)
                        {
                            count++;
                        }
                    }
                }
            }
            
            return count;
        }

        /// <summary>
        /// Calculates local devastation percentage in a spherical area.
        /// </summary>
        public double CalculateLocalDevastationPercent(Vec3d position, double radius)
        {
            int devastatedCount = 0;
            int totalConvertibleCount = 0;
            int sampleRadius = (int)Math.Ceiling(radius);
            
            for (int x = -sampleRadius; x <= sampleRadius; x++)
            {
                for (int y = -sampleRadius; y <= sampleRadius; y++)
                {
                    for (int z = -sampleRadius; z <= sampleRadius; z++)
                    {
                        double dist = Math.Sqrt(x * x + y * y + z * z);
                        if (dist > radius) continue;
                        
                        BlockPos targetPos = new BlockPos(
                            (int)position.X + x,
                            (int)position.Y + y,
                            (int)position.Z + z
                        );
                        
                        Block block = _api.World.BlockAccessor.GetBlock(targetPos);
                        if (block == null || block.Id == 0) continue;
                        
                        if (_blockService.IsDevastated(block))
                        {
                            devastatedCount++;
                            totalConvertibleCount++;
                        }
                        else if (_blockService.GetDevastatedForm(block).IsValid)
                        {
                            totalConvertibleCount++;
                        }
                    }
                }
            }
            
            if (totalConvertibleCount == 0) return 1.0;
            return (double)devastatedCount / totalConvertibleCount;
        }

        /// <summary>
        /// Finds a valid position for metastasis using pillar strategy.
        /// </summary>
        public BlockPos FindMetastasisPositionPillar(DevastationSource source, IList<DevastationSource> allSources)
        {
            int pillarHeight = _config.PillarSearchHeight;
            int probeCount = 32;
            
            double searchMinRadius = source.CurrentRadius * 1.2;
            double searchMaxRadius = source.Range * 2;
            
            var candidates = new List<BlockPos>();
            
            for (int i = 0; i < probeCount; i++)
            {
                double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                double distance = searchMinRadius + (RandomNumberGenerator.GetInt32(1000) / 1000.0) * (searchMaxRadius - searchMinRadius);
                
                int offsetX = (int)(distance * Math.Cos(angle));
                int offsetZ = (int)(distance * Math.Sin(angle));
                int baseY = source.Pos.Y;
                
                for (int yOffset = -pillarHeight; yOffset <= pillarHeight; yOffset++)
                {
                    BlockPos candidatePos = new BlockPos(
                        source.Pos.X + offsetX,
                        baseY + yOffset,
                        source.Pos.Z + offsetZ
                    );
                    
                    if (!IsValidMetastasisPosition(candidatePos)) continue;
                    
                    int nonDevastatedNearby = CountNonDevastatedNearby(candidatePos, 4);
                    if (nonDevastatedNearby > 5)
                    {
                        candidates.Add(candidatePos);
                        break;
                    }
                }
            }
            
            if (candidates.Count == 0) return null;
            
            return candidates
                .Where(c => !IsTooCloseToSources(c, allSources, source.Range * 0.5))
                .OrderByDescending(c => CountNonDevastatedNearby(c, 4))
                .FirstOrDefault();
        }

        /// <summary>
        /// Searches at longer ranges to find undevastated land for metastasis.
        /// </summary>
        public List<BlockPos> FindLongRangeMetastasisPositions(DevastationSource source, IList<DevastationSource> allSources, int count)
        {
            var candidates = new List<BlockPos>();
            var selected = new List<BlockPos>();
            
            int[] searchDistances = { source.Range * 2, source.Range * 4, source.Range * 6, source.Range * 8 };
            
            foreach (int searchDist in searchDistances)
            {
                int cappedDist = Math.Min(searchDist, 128);
                int probeCount = count * 16;
                
                for (int i = 0; i < probeCount; i++)
                {
                    double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                    double angleY = (RandomNumberGenerator.GetInt32(60) - 30) * Math.PI / 180.0;
                    
                    int offsetX = (int)(cappedDist * Math.Cos(angle) * Math.Cos(angleY));
                    int offsetY = (int)(cappedDist * Math.Sin(angleY));
                    int offsetZ = (int)(cappedDist * Math.Sin(angle) * Math.Cos(angleY));
                    
                    BlockPos candidatePos = new BlockPos(
                        source.Pos.X + offsetX,
                        source.Pos.Y + offsetY,
                        source.Pos.Z + offsetZ
                    );
                    
                    if (!IsValidMetastasisPosition(candidatePos)) continue;
                    
                    int nonDevastatedNearby = CountNonDevastatedNearby(candidatePos, 6);
                    if (nonDevastatedNearby > 10)
                    {
                        candidates.Add(candidatePos);
                    }
                }
                
                if (candidates.Count >= count) break;
            }
            
            foreach (var candidate in candidates.OrderByDescending(c => CountNonDevastatedNearby(c, 6)))
            {
                if (selected.Count >= count) break;
                
                bool tooClose = selected.Any(existing => CalculateDistance(candidate, existing) < source.Range);
                
                if (!tooClose && !IsTooCloseToSources(candidate, allSources, source.Range * 0.5))
                {
                    selected.Add(candidate);
                }
            }
            
            return selected;
        }

        /// <summary>
        /// Finds a valid position at a specific XZ coordinate by searching vertically.
        /// </summary>
        public BlockPos FindValidPositionAtXZ(int x, int baseY, int z, int searchHeight)
        {
            for (int yOffset = 0; yOffset <= searchHeight; yOffset++)
            {
                BlockPos posAbove = new BlockPos(x, baseY + yOffset, z);
                if (IsValidHauntingPosition(posAbove))
                {
                    return posAbove;
                }
                
                if (yOffset > 0)
                {
                    BlockPos posBelow = new BlockPos(x, baseY - yOffset, z);
                    if (IsValidHauntingPosition(posBelow))
                    {
                        return posBelow;
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Checks if a position is valid for spawning a metastasis source.
        /// </summary>
        public bool IsValidMetastasisPosition(BlockPos pos)
        {
            if (pos.Y < _config.MinYLevel) return false;
            
            Block block = _api.World.BlockAccessor.GetBlock(pos);
            if (block == null || block.Id == 0) return false;
            
            if (_config.RequireSourceAirContact && !IsAdjacentToAir(pos)) return false;
            
            return true;
        }

        /// <summary>
        /// Checks if a position is valid for haunting relocation.
        /// Stricter requirements than normal metastasis.
        /// </summary>
        public bool IsValidHauntingPosition(BlockPos pos)
        {
            if (!IsValidMetastasisPosition(pos)) return false;
            
            int nonDevastated = CountNonDevastatedNearby(pos, 6);
            return nonDevastated > 15;
        }
    }
}

