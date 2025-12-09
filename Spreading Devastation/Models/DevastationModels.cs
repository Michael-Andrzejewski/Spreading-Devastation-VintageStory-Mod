using ProtoBuf;
using Vintagestory.API.MathTools;

namespace SpreadingDevastation.Models
{
    /// <summary>
    /// Tracks a block that has been devastated and will regenerate over time.
    /// </summary>
    [ProtoContract]
    public class RegrowingBlock
    {
        [ProtoMember(1)]
        public BlockPos Pos { get; set; }

        [ProtoMember(2)]
        public string RegeneratesTo { get; set; }

        [ProtoMember(3)]
        public double LastTime { get; set; }
    }

    /// <summary>
    /// Represents a source of devastation that spreads or heals blocks.
    /// </summary>
    [ProtoContract]
    public class DevastationSource
    {
        [ProtoMember(1)]
        public BlockPos Pos { get; set; }

        [ProtoMember(2)]
        public int Range { get; set; } = 8;

        [ProtoMember(3)]
        public int Amount { get; set; } = 1;

        [ProtoMember(4)]
        public double CurrentRadius { get; set; } = 3.0;

        [ProtoMember(5)]
        public int SuccessfulAttempts { get; set; } = 0;

        [ProtoMember(6)]
        public int TotalAttempts { get; set; } = 0;

        [ProtoMember(7)]
        public bool IsHealing { get; set; } = false;

        // Metastasis system fields
        [ProtoMember(8)]
        public bool IsMetastasis { get; set; } = false;

        [ProtoMember(9)]
        public int GenerationLevel { get; set; } = 0;

        [ProtoMember(10)]
        public int BlocksDevastatedTotal { get; set; } = 0;

        [ProtoMember(11)]
        public int BlocksSinceLastMetastasis { get; set; } = 0;

        [ProtoMember(12)]
        public int MetastasisThreshold { get; set; } = 300;

        [ProtoMember(13)]
        public bool IsSaturated { get; set; } = false;

        [ProtoMember(14)]
        public int MaxGenerationLevel { get; set; } = 10;

        [ProtoMember(15)]
        public string ParentSourceId { get; set; } = null;

        [ProtoMember(16)]
        public string SourceId { get; set; } = null;

        [ProtoMember(17)]
        public int StallCounter { get; set; } = 0;

        [ProtoMember(18)]
        public bool IsProtected { get; set; } = false;

        [ProtoMember(19)]
        public int ChildrenSpawned { get; set; } = 0;

        [ProtoMember(20)]
        public double LastChildSpawnTime { get; set; } = 0;

        [ProtoMember(21)]
        public int FailedSpawnAttempts { get; set; } = 0;

        [ProtoMember(22)]
        public int BlocksDevastatedSinceRelocate { get; set; } = -1;

        /// <summary>
        /// Resets the spreading state for a source (useful after relocation).
        /// </summary>
        public void ResetSpreadingState()
        {
            CurrentRadius = 3.0;
            SuccessfulAttempts = 0;
            TotalAttempts = 0;
            StallCounter = 0;
            FailedSpawnAttempts = 0;
            ChildrenSpawned = 0;
            BlocksSinceLastMetastasis = 0;
            LastChildSpawnTime = 0;
            IsSaturated = false;
            BlocksDevastatedSinceRelocate = 0;
        }

        /// <summary>
        /// Checks if this source is ready to spawn children.
        /// </summary>
        public bool IsReadyToSeed => 
            !IsHealing && 
            !IsSaturated && 
            CurrentRadius >= Range && 
            BlocksSinceLastMetastasis >= MetastasisThreshold;

        /// <summary>
        /// Checks if this source is in a protected relocation state.
        /// </summary>
        public bool IsRelocateProtected => 
            BlocksDevastatedSinceRelocate >= 0 && 
            BlocksDevastatedSinceRelocate < (MetastasisThreshold / 2);

        /// <summary>
        /// Increments the devastation counters.
        /// </summary>
        public void RecordDevastation()
        {
            BlocksDevastatedTotal++;
            BlocksSinceLastMetastasis++;
            
            if (BlocksDevastatedSinceRelocate >= 0)
            {
                BlocksDevastatedSinceRelocate++;
            }
        }
    }
}

