using ProtoBuf;
using Vintagestory.API.MathTools;

namespace SpreadingDevastation.Models
{
    /// <summary>
    /// Represents a point that spreads devastation (or healing).
    /// Tracks position, range, adaptive radius, metastasis state, and statistics.
    /// </summary>
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

        [ProtoMember(19)]
        public int ChildrenSpawned = 0; // Number of children this source has successfully spawned

        [ProtoMember(20)]
        public double LastChildSpawnTime = 0; // Game time when last child was spawned

        [ProtoMember(21)]
        public int FailedSpawnAttempts = 0; // Count of failed metastasis spawn attempts
    }
}
