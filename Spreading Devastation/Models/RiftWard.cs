using ProtoBuf;
using Vintagestory.API.MathTools;

namespace SpreadingDevastation.Models
{
    /// <summary>
    /// Represents an active rift ward that protects an area from devastation.
    /// Rift wards prevent devastation spread and actively heal devastated blocks.
    /// </summary>
    [ProtoContract]
    public class RiftWard
    {
        [ProtoMember(1)]
        public BlockPos Pos;

        [ProtoMember(2)]
        public double DiscoveredTime; // Game time when this rift ward was discovered

        [ProtoMember(3)]
        public int BlocksHealed = 0; // Total blocks healed by this rift ward

        [ProtoMember(4)]
        public double LastHealTime = 0; // Last time this ward performed healing

        [ProtoMember(5)]
        public int CurrentCleanRadius = 0; // Current radius for radial clean mode (0 = starting from center)

        [ProtoMember(6)]
        public int RadialCleanFailures = 0; // Consecutive failures at current radius (used to advance radius)

        [ProtoMember(7)]
        public int MaxCleanRadiusReached = 0; // Highest radius reached (doesn't reset, used for fog clearing)

        // Raster-scan position tracking for deterministic sphere scanning
        [ProtoMember(8)]
        public int ScanX = 0; // Current X offset in raster scan (-radius to +radius)

        [ProtoMember(9)]
        public int ScanY = 0; // Current Y offset in raster scan (-radius to +radius)

        [ProtoMember(10)]
        public int ScanZ = 0; // Current Z offset in raster scan (-radius to +radius)

        [ProtoMember(11)]
        public bool RasterScanComplete = false; // Whether a full sphere scan has been completed

        // Cached active state (not serialized - recalculated on load)
        [ProtoIgnore]
        public bool CachedIsActive = false;

        [ProtoIgnore]
        public double LastActiveCheck = 0; // Last time we checked if this ward is active

        /// <summary>
        /// Checks if a position is within the protection radius of this rift ward.
        /// </summary>
        public bool IsPositionProtected(BlockPos targetPos, int protectionRadius)
        {
            if (Pos == null || targetPos == null) return false;

            // Use squared distance for efficiency (avoid sqrt)
            int dx = targetPos.X - Pos.X;
            int dy = targetPos.Y - Pos.Y;
            int dz = targetPos.Z - Pos.Z;
            int distanceSquared = dx * dx + dy * dy + dz * dz;

            return distanceSquared <= protectionRadius * protectionRadius;
        }

        /// <summary>
        /// Gets the squared distance from this rift ward to a position.
        /// </summary>
        public int GetDistanceSquared(BlockPos targetPos)
        {
            if (Pos == null || targetPos == null) return int.MaxValue;

            int dx = targetPos.X - Pos.X;
            int dy = targetPos.Y - Pos.Y;
            int dz = targetPos.Z - Pos.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
