using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace SpreadingDevastation.Models
{
    /// <summary>
    /// Tracks devastation state for an entire chunk, including spreading frontier and spawn timing.
    /// </summary>
    [ProtoContract]
    public class DevastatedChunk
    {
        [ProtoMember(1)]
        public int ChunkX;

        [ProtoMember(2)]
        public int ChunkZ;

        [ProtoMember(3)]
        public double DevastationLevel = 0.0; // 0.0 to 1.0, percentage of blocks devastated

        [ProtoMember(4)]
        public double MarkedTime; // Game time when chunk was first marked

        [ProtoMember(5)]
        public bool IsFullyDevastated = false; // True when all convertible blocks are devastated

        [ProtoMember(6)]
        public double LastSpawnTime = 0; // Last time an entity was spawned in this chunk

        [ProtoMember(7)]
        public int BlocksDevastated = 0; // Count of blocks devastated in this chunk

        [ProtoMember(8)]
        public List<BlockPos> DevastationFrontier = new List<BlockPos>(); // Blocks at the frontier for cardinal spreading

        [ProtoMember(9)]
        public bool FrontierInitialized = false; // Whether the frontier has been seeded with starting position

        [ProtoMember(10)]
        public List<BleedBlock> BleedFrontier = new List<BleedBlock>(); // Blocks bleeding into adjacent non-devastated chunks

        [ProtoMember(11)]
        public int FillInTickCounter = 0; // Counter for periodic fill-in passes to catch missed blocks

        [ProtoMember(12)]
        public int ConsecutiveEmptyFrontierChecks = 0; // How many consecutive ticks the frontier has been empty

        [ProtoMember(13)]
        public double LastRepairAttemptTime = 0; // Game time when last repair was attempted

        [ProtoMember(14)]
        public int RepairAttemptCount = 0; // Number of repair attempts since last progress

        [ProtoMember(15)]
        public int BlocksAtLastRepair = 0; // BlocksDevastated count at last repair attempt

        [ProtoMember(16)]
        public bool IsUnrepairable = false; // True if chunk has been abandoned after too many failed repairs

        [ProtoMember(17)]
        public int MobsSpawned = 0; // Count of mobs spawned in this chunk

        [ProtoMember(18)]
        public double NextSpawnTime = 0; // Game time when next spawn is allowed (randomized interval)

        /// <summary>
        /// Gets the unique key for this chunk (for dictionary lookup).
        /// </summary>
        public long ChunkKey => ((long)ChunkX << 32) | (uint)ChunkZ;

        /// <summary>
        /// Creates a unique key from chunk coordinates.
        /// </summary>
        public static long MakeChunkKey(int chunkX, int chunkZ) => ((long)chunkX << 32) | (uint)chunkZ;
    }
}
