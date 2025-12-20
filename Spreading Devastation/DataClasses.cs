using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SpreadingDevastation
{
    /// <summary>
    /// Tracks a devastated block that will eventually regenerate back to its original form.
    /// </summary>
    [ProtoContract]
    public class RegrowingBlocks
    {
        [ProtoMember(1)]
        public BlockPos Pos;

        [ProtoMember(2)]
        public string Out;

        [ProtoMember(3)]
        public double LastTime;
    }

    /// <summary>
    /// Represents a point source of devastation or healing that spreads outward.
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

    /// <summary>
    /// Represents a chunk that has been marked for devastation spreading.
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

        // Helper to get chunk key for dictionary lookup
        public long ChunkKey => ((long)ChunkX << 32) | (uint)ChunkZ;

        public static long MakeChunkKey(int chunkX, int chunkZ) => ((long)chunkX << 32) | (uint)chunkZ;
    }

    /// <summary>
    /// Represents a block that has "bled" outside its source chunk into a non-devastated chunk.
    /// These blocks have a limited infection budget and can only spread a few more times.
    /// </summary>
    [ProtoContract]
    public class BleedBlock
    {
        [ProtoMember(1)]
        public BlockPos Pos;

        [ProtoMember(2)]
        public int RemainingSpread; // How many more blocks this can infect (decrements with each spread)
    }

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

    /// <summary>
    /// Network packet sent from server to client containing devastated chunk positions.
    /// Used for client-side fog/sky effects when player is in devastated areas.
    /// </summary>
    [ProtoContract]
    public class DevastatedChunkSyncPacket
    {
        /// <summary>
        /// List of chunk X coordinates that are devastated near the player.
        /// </summary>
        [ProtoMember(1)]
        public List<int> ChunkXs = new List<int>();

        /// <summary>
        /// List of chunk Z coordinates that are devastated near the player.
        /// Indices correspond to ChunkXs.
        /// </summary>
        [ProtoMember(2)]
        public List<int> ChunkZs = new List<int>();
    }

    /// <summary>
    /// Network packet sent from server to client containing fog configuration.
    /// </summary>
    [ProtoContract]
    public class FogConfigPacket
    {
        [ProtoMember(1)]
        public bool Enabled = true;
        [ProtoMember(2)]
        public float ColorR = 0.55f;
        [ProtoMember(3)]
        public float ColorG = 0.25f;
        [ProtoMember(4)]
        public float ColorB = 0.15f;
        [ProtoMember(5)]
        public float Density = 0.004f;
        [ProtoMember(6)]
        public float Min = 0.15f;
        [ProtoMember(7)]
        public float ColorWeight = 0.7f;
        [ProtoMember(8)]
        public float DensityWeight = 0.5f;
        [ProtoMember(9)]
        public float MinWeight = 0.6f;
        [ProtoMember(10)]
        public float TransitionSpeed = 0.5f;
    }

    #region Test Suite Data Structures

    /// <summary>
    /// Status of a test execution
    /// </summary>
    public enum TestStatus
    {
        Pending,
        Running,
        Passed,
        Failed,
        Manual,  // Requires manual verification
        Error    // Test threw an exception
    }

    /// <summary>
    /// Result of a single test
    /// </summary>
    public class TestResult
    {
        public string Name { get; set; }
        public TestStatus Status { get; set; } = TestStatus.Pending;
        public string Message { get; set; } = "";
        public double DurationMs { get; set; } = 0;
        public Exception Exception { get; set; } = null;

        public TestResult(string name)
        {
            Name = name;
        }

        public void Pass(string message = "")
        {
            Status = TestStatus.Passed;
            Message = message;
        }

        public void Fail(string message)
        {
            Status = TestStatus.Failed;
            Message = message;
        }

        public void SetManual(string message)
        {
            Status = TestStatus.Manual;
            Message = message;
        }

        public void SetError(Exception ex)
        {
            Status = TestStatus.Error;
            Exception = ex;
            Message = ex.Message;
        }
    }

    /// <summary>
    /// Context shared across all tests in a test run
    /// </summary>
    public class TestContext
    {
        public BlockPos StartPosition { get; set; }
        public IServerPlayer Player { get; set; }
        public double StartTime { get; set; }

        // Snapshot of original state for cleanup
        public Dictionary<BlockPos, int> OriginalBlocks { get; set; } = new Dictionary<BlockPos, int>();
        public List<string> TestSourceIds { get; set; } = new List<string>();
        public List<long> TestChunkKeys { get; set; } = new List<long>();
        public List<long> TestEntityIds { get; set; } = new List<long>();

        // Original config values for restoration
        public double OriginalSpeedMultiplier { get; set; }
        public double OriginalChunkSpreadChance { get; set; }
        public double OriginalChunkSpreadInterval { get; set; }
        public double OriginalAnimalInsanityChance { get; set; }
        public int OriginalMetastasisThreshold { get; set; }
        public double OriginalChildSpawnDelay { get; set; }
    }

    #endregion
}
