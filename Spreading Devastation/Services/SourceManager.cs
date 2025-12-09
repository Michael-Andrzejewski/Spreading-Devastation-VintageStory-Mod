using System;
using System.Collections.Generic;
using System.Linq;
using SpreadingDevastation.Models;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SpreadingDevastation.Services
{
    /// <summary>
    /// Manages devastation source lifecycle: creation, removal, cleanup.
    /// Single Responsibility: Source collection management.
    /// </summary>
    public class SourceManager
    {
        private readonly ICoreServerAPI _api;
        private readonly SpreadingDevastationConfig _config;
        private readonly List<DevastationSource> _sources;
        private int _nextSourceId = 1;

        public SourceManager(ICoreServerAPI api, SpreadingDevastationConfig config, List<DevastationSource> sources)
        {
            _api = api;
            _config = config;
            _sources = sources;
        }

        public IList<DevastationSource> GetAllSources() => _sources;
        public int Count => _sources.Count;
        public int MaxSources => _config.MaxSources;

        /// <summary>
        /// Sets the next source ID counter (for persistence).
        /// </summary>
        public void SetNextSourceId(int nextId)
        {
            _nextSourceId = nextId;
        }

        /// <summary>
        /// Gets the current next source ID (for persistence).
        /// </summary>
        public int GetNextSourceId() => _nextSourceId;

        /// <summary>
        /// Generates a unique source ID.
        /// </summary>
        public string GenerateSourceId()
        {
            return (_nextSourceId++).ToString();
        }

        /// <summary>
        /// Adds a source to the collection.
        /// </summary>
        public void AddSource(DevastationSource source)
        {
            _sources.Add(source);
        }

        /// <summary>
        /// Removes a source from the collection.
        /// </summary>
        public bool RemoveSource(DevastationSource source)
        {
            return _sources.Remove(source);
        }

        /// <summary>
        /// Checks if a source exists at the given position.
        /// </summary>
        public bool ExistsAtPosition(BlockPos pos)
        {
            return _sources.Any(s => s.Pos.Equals(pos));
        }

        /// <summary>
        /// Removes sources at a position.
        /// </summary>
        public int RemoveAtPosition(BlockPos pos)
        {
            return _sources.RemoveAll(s => s.Pos.Equals(pos));
        }

        /// <summary>
        /// Removes all sources.
        /// </summary>
        public int RemoveAll()
        {
            int count = _sources.Count;
            _sources.Clear();
            return count;
        }

        /// <summary>
        /// Removes all saturated sources.
        /// </summary>
        public int RemoveSaturated()
        {
            return _sources.RemoveAll(s => s.IsSaturated);
        }

        /// <summary>
        /// Removes all metastasis sources.
        /// </summary>
        public int RemoveMetastasis()
        {
            return _sources.RemoveAll(s => s.IsMetastasis);
        }

        /// <summary>
        /// Ensures there's capacity for new sources.
        /// Returns true if capacity is available.
        /// </summary>
        public bool EnsureCapacity(int needed = 1)
        {
            if (_sources.Count + needed <= _config.MaxSources)
            {
                return true;
            }

            RemoveOldestSources(needed);
            return _sources.Count < _config.MaxSources;
        }

        /// <summary>
        /// Creates and adds a new devastation source.
        /// </summary>
        public DevastationSource CreateSource(BlockPos pos, int range, int amount, bool isHealing, bool isProtected = false)
        {
            var source = new DevastationSource
            {
                Pos = pos.Copy(),
                Range = range,
                Amount = amount,
                CurrentRadius = Math.Min(3.0, range),
                IsHealing = isHealing,
                SourceId = GenerateSourceId(),
                IsProtected = isProtected,
                MetastasisThreshold = _config.MetastasisThreshold
            };

            _sources.Add(source);
            return source;
        }

        /// <summary>
        /// Removes sources whose blocks no longer exist.
        /// Returns the list of removed sources.
        /// </summary>
        public List<DevastationSource> RemoveInvalidSources()
        {
            var toRemove = new List<DevastationSource>();

            foreach (var source in _sources)
            {
                Block block = _api.World.BlockAccessor.GetBlock(source.Pos);
                if (block == null || block.Id == 0)
                {
                    toRemove.Add(source);
                }
            }

            foreach (var source in toRemove)
            {
                _sources.Remove(source);
            }

            return toRemove;
        }

        /// <summary>
        /// Cleans up saturated sources to free slots.
        /// Only runs when above half capacity.
        /// </summary>
        public void CleanupSaturatedSources()
        {
            if (_sources.Count < _config.MaxSources / 2) return;

            var saturatedSources = _sources
                .Where(s => s.IsSaturated && !s.IsProtected && !s.IsHealing)
                .ToList();

            if (saturatedSources.Count == 0) return;

            int toRemove = Math.Max(1, saturatedSources.Count / 4);

            var sourcesToRemove = saturatedSources
                .OrderByDescending(s => s.GenerationLevel)
                .ThenByDescending(s => s.BlocksDevastatedTotal)
                .Take(toRemove)
                .ToList();

            foreach (var source in sourcesToRemove)
            {
                _sources.Remove(source);
            }
        }

        /// <summary>
        /// Gets sources suitable for haunting relocation.
        /// </summary>
        public IEnumerable<DevastationSource> GetMovableSources()
        {
            return _sources.Where(s => !s.IsSaturated && !s.IsHealing && !s.IsProtected);
        }

        /// <summary>
        /// Gets summary statistics about sources.
        /// </summary>
        public SourceStatistics GetStatistics()
        {
            return new SourceStatistics
            {
                Total = _sources.Count,
                MaxSources = _config.MaxSources,
                Protected = _sources.Count(s => s.IsProtected),
                Metastasis = _sources.Count(s => s.IsMetastasis),
                Saturated = _sources.Count(s => s.IsSaturated),
                Healing = _sources.Count(s => s.IsHealing),
                Growing = _sources.Count(s => !s.IsSaturated && !s.IsHealing &&
                    (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range)),
                Seeding = _sources.Count(s => s.IsReadyToSeed),
                TotalBlocksDevastated = _sources.Sum(s => (long)s.BlocksDevastatedTotal)
            };
        }

        /// <summary>
        /// Gets sources grouped by generation level.
        /// </summary>
        public IEnumerable<IGrouping<int, DevastationSource>> GetSourcesByGeneration()
        {
            return _sources
                .GroupBy(s => s.GenerationLevel)
                .OrderBy(g => g.Key);
        }

        private void RemoveOldestSources(int count)
        {
            if (count <= 0 || _sources.Count == 0) return;

            var sourcesToRemove = _sources
                .Where(s => !s.IsHealing && !s.IsProtected)
                .OrderByDescending(s => s.IsSaturated ? 1 : 0)
                .ThenByDescending(s => s.GenerationLevel)
                .ThenByDescending(s => s.BlocksDevastatedTotal)
                .Take(count)
                .ToList();

            foreach (var source in sourcesToRemove)
            {
                _sources.Remove(source);
            }
        }
    }

    /// <summary>
    /// Statistics about devastation sources.
    /// </summary>
    public class SourceStatistics
    {
        public int Total { get; set; }
        public int MaxSources { get; set; }
        public int Protected { get; set; }
        public int Metastasis { get; set; }
        public int Saturated { get; set; }
        public int Healing { get; set; }
        public int Growing { get; set; }
        public int Seeding { get; set; }
        public long TotalBlocksDevastated { get; set; }
    }
}

