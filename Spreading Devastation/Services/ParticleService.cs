using SpreadingDevastation.Models;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SpreadingDevastation.Services
{
    /// <summary>
    /// Handles debug particle spawning for source visualization.
    /// Single Responsibility: Visual debugging.
    /// </summary>
    public class ParticleService
    {
        private readonly ICoreServerAPI _api;
        private readonly SpreadingDevastationConfig _config;

        public ParticleService(ICoreServerAPI api, SpreadingDevastationConfig config)
        {
            _api = api;
            _config = config;
        }

        /// <summary>
        /// Spawns marker particles at a source's position.
        /// </summary>
        public void SpawnSourceMarker(DevastationSource source)
        {
            if (!_config.ShowSourceMarkers) return;
            
            // Only emit ~50% of ticks for performance
            if (_api.World.Rand.NextDouble() > 0.5) return;

            int color = GetSourceMarkerColor(source);
            BlockPos pos = source.Pos;
            Vec3d center = pos.ToVec3d().Add(0.5, 0.7, 0.5);

            var props = new SimpleParticleProperties
            {
                MinQuantity = 3,
                AddQuantity = 4,
                Color = color,
                MinPos = new Vec3d(
                    center.X - 0.05,
                    center.Y - 0.05,
                    center.Z - 0.05),
                AddPos = new Vec3d(0.2, 0.2, 0.2),
                MinVelocity = new Vec3f(-0.03f, 0.07f, -0.03f),
                AddVelocity = new Vec3f(0.06f, 0.06f, 0.06f),
                LifeLength = 0.7f,
                GravityEffect = -0.03f,
                MinSize = 0.12f,
                MaxSize = 0.22f,
                ShouldDieInLiquid = false,
                ParticleModel = EnumParticleModel.Quad
            };

            _api.World.SpawnParticles(props);
        }

        /// <summary>
        /// Gets the marker color based on source status.
        /// Blue = new/growing, Green = seeding, Red = saturated.
        /// </summary>
        public int GetSourceMarkerColor(DevastationSource source)
        {
            if (source.IsSaturated)
            {
                return ColorUtil.ToRgba(255, 255, 80, 80); // Red
            }

            if (source.IsReadyToSeed || source.ChildrenSpawned > 0 || source.IsMetastasis)
            {
                return ColorUtil.ToRgba(255, 80, 255, 120); // Green
            }

            return ColorUtil.ToRgba(255, 80, 160, 255); // Blue
        }
    }
}

