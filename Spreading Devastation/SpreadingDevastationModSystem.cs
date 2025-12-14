using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SpreadingDevastation
{
    /// <summary>
    /// Configuration class for the Spreading Devastation mod.
    /// Saved to ModConfig/SpreadingDevastationConfig.json
    /// </summary>
    public class SpreadingDevastationConfig
    {
        /// <summary>Global speed multiplier for all devastation spread (default: 1.0)</summary>
        public double SpeedMultiplier { get; set; } = 1.0;
        
        /// <summary>Maximum number of devastation sources (caps metastasis growth) (default: 20)</summary>
        public int MaxSources { get; set; } = 20;
        
        /// <summary>Minimum Y level for new metastasis sources (default: 100)</summary>
        public int MinYLevel { get; set; } = 100;
        
        /// <summary>Default range for new devastation sources (default: 8)</summary>
        public int DefaultRange { get; set; } = 8;
        
        /// <summary>Default amount of blocks to process per tick (default: 1)</summary>
        public int DefaultAmount { get; set; } = 1;
        
        /// <summary>Blocks to devastate before spawning metastasis (default: 300)</summary>
        public int MetastasisThreshold { get; set; } = 300;

        /// <summary>Local saturation percentage to trigger metastasis spawn (default: 0.75)</summary>
        public double SaturationThreshold { get; set; } = 0.75;
        
        /// <summary>Low success rate threshold for expanding search radius (default: 0.2)</summary>
        public double LowSuccessThreshold { get; set; } = 0.2;
        
        /// <summary>Very low success rate threshold for stalling detection (default: 0.05)</summary>
        public double VeryLowSuccessThreshold { get; set; } = 0.05;
        
        /// <summary>
        /// When enabled, new metastasis source positions must be adjacent to air.
        /// This keeps spreading along surfaces rather than through solid rock.
        /// Does NOT restrict which blocks can be devastated. (default: true)
        /// </summary>
        public bool RequireSourceAirContact { get; set; } = true;
        
        /// <summary>
        /// Variation in child source range as a fraction of parent range.
        /// Child range = parent range * (1 ± this value)
        /// E.g., 0.5 means 50-150% of parent range. (default: 0.5)
        /// </summary>
        public double MetastasisRadiusVariation { get; set; } = 0.5;
        
        /// <summary>
        /// Delay in seconds between spawning child sources (default: 120.0)
        /// Affected by speed multiplier.
        /// </summary>
        public double ChildSpawnDelaySeconds { get; set; } = 120.0;
        
        /// <summary>
        /// Number of failed spawn attempts before marking a source as saturated (default: 10)
        /// </summary>
        public int MaxFailedSpawnAttempts { get; set; } = 10;
        
        /// <summary>
        /// Vertical search range (±blocks) when using pillar strategy for metastasis (default: 5)
        /// </summary>
        public int PillarSearchHeight { get; set; } = 5;
        
        /// <summary>
        /// Show magenta debug particles on devastation sources to visualize them (default: true)
        /// </summary>
        public bool ShowSourceMarkers { get; set; } = true;

        // === Devastated Chunk Settings ===

        /// <summary>
        /// Minimum interval in game hours between enemy spawns in devastated chunks (default: 0.5)
        /// Actual spawn time is randomized between this and ChunkSpawnIntervalMaxHours.
        /// </summary>
        public double ChunkSpawnIntervalMinHours { get; set; } = 0.5;

        /// <summary>
        /// Maximum interval in game hours between enemy spawns in devastated chunks (default: 1.0)
        /// Actual spawn time is randomized between ChunkSpawnIntervalMinHours and this.
        /// </summary>
        public double ChunkSpawnIntervalMaxHours { get; set; } = 1.0;

        /// <summary>
        /// Cooldown in game hours after a spawn before the next spawn can occur (default: 4.0)
        /// This is a hard minimum regardless of the random interval.
        /// </summary>
        public double ChunkSpawnCooldownHours { get; set; } = 4.0;

        /// <summary>
        /// Minimum distance in blocks from the nearest player for mob spawns (default: 16)
        /// </summary>
        public int ChunkSpawnMinDistance { get; set; } = 16;

        /// <summary>
        /// Maximum distance in blocks from the nearest player for mob spawns (default: 48)
        /// </summary>
        public int ChunkSpawnMaxDistance { get; set; } = 48;

        /// <summary>
        /// Maximum number of mobs that can be spawned in a single chunk (default: 3)
        /// Once this limit is reached, no more spawns will occur in that chunk.
        /// </summary>
        public int ChunkSpawnMaxMobsPerChunk { get; set; } = 3;

        /// <summary>
        /// Temporal stability drain rate per 500ms tick when player is in devastated chunk (default: 0.001)
        /// </summary>
        public double ChunkStabilityDrainRate { get; set; } = 0.001;

        /// <summary>
        /// Whether devastated chunks can spread to nearby chunks (default: true)
        /// </summary>
        public bool ChunkSpreadEnabled { get; set; } = true;

        /// <summary>
        /// Chance (0.0-1.0) for a devastated chunk to spread each check interval (default: 0.05 = 5%)
        /// </summary>
        public double ChunkSpreadChance { get; set; } = 0.05;

        /// <summary>
        /// Base interval in seconds between chunk spread checks (default: 60). Affected by SpeedMultiplier.
        /// </summary>
        public double ChunkSpreadIntervalSeconds { get; set; } = 60.0;

        /// <summary>
        /// Maximum depth below surface that chunk devastation can spread (default: 10).
        /// Only applies when RequireSourceAirContact is true.
        /// Set to -1 to disable depth limiting.
        /// </summary>
        public int ChunkMaxDepthBelowSurface { get; set; } = 10;

        /// <summary>
        /// Maximum number of blocks that devastation can "bleed" past chunk edges into non-devastated chunks (default: 3).
        /// This creates organic, uneven borders instead of straight lines along chunk boundaries.
        /// Set to 0 to disable edge bleeding.
        /// </summary>
        public int ChunkEdgeBleedDepth { get; set; } = 3;

        /// <summary>
        /// Chance (0.0-1.0) for each edge block to bleed into the adjacent chunk (default: 0.3 = 30%).
        /// Lower values create more sparse, irregular borders.
        /// </summary>
        public double ChunkEdgeBleedChance { get; set; } = 0.3;

        // === Rift Ward Settings ===

        /// <summary>
        /// Protection radius in blocks for rift wards (default: 128).
        /// Rift wards prevent devastation from spreading within this radius.
        /// </summary>
        public int RiftWardProtectionRadius { get; set; } = 128;

        /// <summary>
        /// Base healing rate in blocks per second for rift wards (default: 10.0).
        /// This is multiplied by the rift ward speed multiplier.
        /// </summary>
        public double RiftWardHealingRate { get; set; } = 10.0;

        /// <summary>
        /// Speed multiplier for rift ward healing (default: -1 = use global SpeedMultiplier).
        /// Set to a positive value to use a custom speed independent of global devastation speed.
        /// </summary>
        public double RiftWardSpeedMultiplier { get; set; } = -1;

        /// <summary>
        /// Whether rift wards should actively heal devastated blocks (default: true).
        /// If false, rift wards only prevent new devastation.
        /// </summary>
        public bool RiftWardHealingEnabled { get; set; } = true;

        /// <summary>
        /// Interval in seconds between rift ward scans for new/removed rift wards (default: 30.0).
        /// Lower values detect rift wards faster but use more CPU.
        /// </summary>
        public double RiftWardScanIntervalSeconds { get; set; } = 30.0;

        /// <summary>
        /// Rift ward cleaning mode: "raster", "radial", or "random" (default: "raster").
        /// - raster: Deterministic sphere scan that processes every block exactly once (most efficient)
        /// - radial: Random sampling that expands outward from center (legacy mode)
        /// - random: Random sampling throughout the entire protection radius
        /// </summary>
        public string RiftWardCleanMode { get; set; } = "raster";

        /// <summary>
        /// [DEPRECATED] Use RiftWardCleanMode instead. Kept for config compatibility.
        /// If RiftWardCleanMode is not set, this determines radial (true) vs random (false).
        /// </summary>
        public bool RiftWardRadialCleanEnabled { get; set; } = true;

        // === Devastation Fog Effect Settings ===

        /// <summary>
        /// Whether to show fog/sky color effect in devastated chunks (default: true).
        /// </summary>
        public bool FogEffectEnabled { get; set; } = true;

        /// <summary>
        /// Fog color R component (0.0-1.0). Default rusty orange: 0.55
        /// </summary>
        public float FogColorR { get; set; } = 0.55f;

        /// <summary>
        /// Fog color G component (0.0-1.0). Default rusty orange: 0.25
        /// </summary>
        public float FogColorG { get; set; } = 0.25f;

        /// <summary>
        /// Fog color B component (0.0-1.0). Default rusty orange: 0.15
        /// </summary>
        public float FogColorB { get; set; } = 0.15f;

        /// <summary>
        /// Fog density in devastated areas (default: 0.004). Higher = thicker fog.
        /// </summary>
        public float FogDensity { get; set; } = 0.004f;

        /// <summary>
        /// Minimum fog level in devastated areas (default: 0.15). Higher = more baseline fog.
        /// </summary>
        public float FogMin { get; set; } = 0.15f;

        /// <summary>
        /// How strongly the fog color is applied (0.0-1.0, default: 0.7).
        /// </summary>
        public float FogColorWeight { get; set; } = 0.7f;

        /// <summary>
        /// How strongly the fog density is applied (0.0-1.0, default: 0.5).
        /// </summary>
        public float FogDensityWeight { get; set; } = 0.5f;

        /// <summary>
        /// How strongly the minimum fog is applied (0.0-1.0, default: 0.6).
        /// </summary>
        public float FogMinWeight { get; set; } = 0.6f;

        /// <summary>
        /// How fast the fog effect transitions in/out in seconds (default: 0.5).
        /// </summary>
        public float FogTransitionSpeed { get; set; } = 0.5f;

        // === Storm Wall Effect Settings ===

        /// <summary>
        /// Whether to show visible storm wall at devastated chunk boundaries (default: true).
        /// </summary>
        public bool StormWallEnabled { get; set; } = true;

        /// <summary>
        /// Height of the storm wall in blocks (default: 64).
        /// </summary>
        public float StormWallHeight { get; set; } = 64f;

        /// <summary>
        /// Base opacity of the storm wall at ground level (0.0-1.0, default: 0.05).
        /// Wall fades from this at ground to StormWallTopOpacity at top.
        /// </summary>
        public float StormWallBaseOpacity { get; set; } = 0.05f;

        /// <summary>
        /// Opacity of the storm wall at the top (0.0-1.0, default: 0.4).
        /// </summary>
        public float StormWallTopOpacity { get; set; } = 0.4f;

        /// <summary>
        /// Storm wall color R component (0.0-1.0). Default rusty orange: 0.6
        /// </summary>
        public float StormWallColorR { get; set; } = 0.6f;

        /// <summary>
        /// Storm wall color G component (0.0-1.0). Default rusty orange: 0.3
        /// </summary>
        public float StormWallColorG { get; set; } = 0.3f;

        /// <summary>
        /// Storm wall color B component (0.0-1.0). Default rusty orange: 0.15
        /// </summary>
        public float StormWallColorB { get; set; } = 0.15f;

        /// <summary>
        /// Maximum render distance for the storm wall in blocks (default: 256).
        /// </summary>
        public float StormWallRenderDistance { get; set; } = 256f;

        /// <summary>
        /// Speed of the swirling animation in the storm wall (default: 0.5).
        /// </summary>
        public float StormWallAnimationSpeed { get; set; } = 0.5f;

        // === Storm Wall Particle Settings ===

        /// <summary>
        /// Whether to show particles at storm wall boundaries (default: true).
        /// </summary>
        public bool StormWallParticlesEnabled { get; set; } = true;

        /// <summary>
        /// Maximum distance from player to spawn storm wall particles (default: 48).
        /// </summary>
        public float StormWallParticleDistance { get; set; } = 48f;

        /// <summary>
        /// Number of ash/ember particles to spawn per second near boundaries (default: 15).
        /// </summary>
        public int AshParticlesPerSecond { get; set; } = 15;

        /// <summary>
        /// Number of dust particles to spawn per second near boundaries (default: 10).
        /// </summary>
        public int DustParticlesPerSecond { get; set; } = 10;

        /// <summary>
        /// Chance per second for lightning effect at boundaries (0.0-1.0, default: 0.1).
        /// </summary>
        public float LightningChancePerSecond { get; set; } = 0.1f;
    }

    public class SpreadingDevastationModSystem : ModSystem
    {
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

        /// <summary>
        /// Network packet sent from server to client containing storm wall configuration.
        /// </summary>
        [ProtoContract]
        public class StormWallConfigPacket
        {
            [ProtoMember(1)]
            public bool Enabled = true;
            [ProtoMember(2)]
            public float Height = 64f;
            [ProtoMember(3)]
            public float BaseOpacity = 0.05f;
            [ProtoMember(4)]
            public float TopOpacity = 0.4f;
            [ProtoMember(5)]
            public float ColorR = 0.6f;
            [ProtoMember(6)]
            public float ColorG = 0.3f;
            [ProtoMember(7)]
            public float ColorB = 0.15f;
            [ProtoMember(8)]
            public float RenderDistance = 256f;
            [ProtoMember(9)]
            public float AnimationSpeed = 0.5f;
            [ProtoMember(10)]
            public bool ParticlesEnabled = true;
            [ProtoMember(11)]
            public float ParticleDistance = 48f;
            [ProtoMember(12)]
            public int AshParticlesPerSecond = 15;
            [ProtoMember(13)]
            public int DustParticlesPerSecond = 10;
            [ProtoMember(14)]
            public float LightningChance = 0.1f;
        }

        /// <summary>
        /// Network packet sent from server to client for dome commands.
        /// </summary>
        [ProtoContract]
        public class DomeCommandPacket
        {
            [ProtoMember(1)]
            public string Action = ""; // add, remove, clear, list
            [ProtoMember(2)]
            public double X;
            [ProtoMember(3)]
            public double Y;
            [ProtoMember(4)]
            public double Z;
            [ProtoMember(5)]
            public float Radius = 64f;
        }

        private ICoreServerAPI sapi;
        private List<RegrowingBlocks> regrowingBlocks;
        private List<DevastationSource> devastationSources;
        private Dictionary<long, DevastatedChunk> devastatedChunks; // Chunk-based devastation tracking
        private SpreadingDevastationConfig config;
        private bool isPaused = false; // When true, all devastation processing stops
        private int nextSourceId = 1; // Counter for generating unique source IDs
        private int cleanupTickCounter = 0; // Counter for periodic cleanup
        private const int CHUNK_SIZE = 32; // VS chunk size in blocks
        private double lastChunkSpreadCheckTime = 0; // Track last chunk spread check

        // Performance monitoring
        private Stopwatch perfStopwatch = new Stopwatch();
        private Queue<double> chunkProcessingTimes = new Queue<double>(); // Rolling window of processing times (ms)
        private Queue<double> tickDeltaTimes = new Queue<double>(); // Rolling window of actual tick intervals
        private const int PERF_SAMPLE_SIZE = 20; // Number of samples to average
        private double totalProcessingTimeMs = 0; // Total time spent in chunk processing this session
        private int totalTicksProcessed = 0; // Total ticks processed this session
        private double peakProcessingTimeMs = 0; // Peak single-tick processing time

        // Stuck chunk repair queue
        private Queue<long> chunksNeedingRepair = new Queue<long>(); // Chunk keys that need repair (stuck chunks)

        // Rift Ward tracking
        private List<RiftWard> activeRiftWards = new List<RiftWard>();
        private double lastRiftWardScanTime = 0; // Track last scan for new rift wards
        private HashSet<long> protectedChunkKeys = new HashSet<long>(); // Cache of chunk keys protected by rift wards
        private bool initialRiftWardScanCompleted = false; // Whether initial world scan for rift wards has completed
        private double lastFullRiftWardScanTime = 0; // Track last full world scan for rift wards

        // Network channel for client-server sync
        private const string NETWORK_CHANNEL_NAME = "spreadingdevastation";
        private IServerNetworkChannel serverNetworkChannel;
        private bool fogConfigDirty = true; // Flag to track if fog config needs to be sent
        private bool stormWallConfigDirty = true; // Flag to track if storm wall config needs to be sent

        // Client-side fields
        private ICoreClientAPI capi;
        private IClientNetworkChannel clientNetworkChannel;
        private HashSet<long> clientDevastatedChunks = new HashSet<long>(); // Chunk keys received from server
        private DevastationFogRenderer fogRenderer;
        private FogConfigPacket clientFogConfig = new FogConfigPacket(); // Fog config received from server
        private DevastationStormWallRenderer stormWallRenderer;
        private StormWallConfigPacket clientStormWallConfig = new StormWallConfigPacket(); // Storm wall config received from server

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            // Register network channel and message types (runs on both client and server)
            api.Network
                .RegisterChannel(NETWORK_CHANNEL_NAME)
                .RegisterMessageType(typeof(DevastatedChunkSyncPacket))
                .RegisterMessageType(typeof(FogConfigPacket))
                .RegisterMessageType(typeof(StormWallConfigPacket))
                .RegisterMessageType(typeof(DomeCommandPacket));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            // Get the network channel and set up message handlers
            clientNetworkChannel = api.Network.GetChannel(NETWORK_CHANNEL_NAME)
                .SetMessageHandler<DevastatedChunkSyncPacket>(OnDevastatedChunkSync)
                .SetMessageHandler<FogConfigPacket>(OnFogConfigSync)
                .SetMessageHandler<StormWallConfigPacket>(OnStormWallConfigSync)
                .SetMessageHandler<DomeCommandPacket>(OnDomeCommandReceived);

            // Create and register the fog renderer
            fogRenderer = new DevastationFogRenderer(api, this);
            api.Event.RegisterRenderer(fogRenderer, EnumRenderStage.Before, "devastationfog");

            // Create and register the storm wall renderer (renders after opaque terrain but before transparent)
            stormWallRenderer = new DevastationStormWallRenderer(api, this);
            api.Event.RegisterRenderer(stormWallRenderer, EnumRenderStage.AfterOIT, "devastationstormwall");

            api.Logger.Notification("SpreadingDevastation: Client-side fog and storm wall renderers initialized");
        }

        /// <summary>
        /// Called when the client receives fog configuration from the server.
        /// </summary>
        private void OnFogConfigSync(FogConfigPacket packet)
        {
            clientFogConfig = packet;
            fogRenderer?.UpdateConfig(packet);
        }

        /// <summary>
        /// Gets the current fog config for the client.
        /// </summary>
        public FogConfigPacket GetFogConfig()
        {
            return clientFogConfig;
        }

        /// <summary>
        /// Called when the client receives storm wall configuration from the server.
        /// </summary>
        private void OnStormWallConfigSync(StormWallConfigPacket packet)
        {
            clientStormWallConfig = packet;
            stormWallRenderer?.UpdateConfig(packet);
        }

        /// <summary>
        /// Called when the client receives a dome command from the server.
        /// </summary>
        private void OnDomeCommandReceived(DomeCommandPacket packet)
        {
            if (stormWallRenderer == null)
            {
                capi?.ShowChatMessage("Dome renderer not available");
                return;
            }

            switch (packet.Action.ToLowerInvariant())
            {
                case "add":
                    stormWallRenderer.AddDome(packet.X, packet.Y, packet.Z, packet.Radius);
                    capi?.ShowChatMessage($"Dome placed at ({packet.X:F0}, {packet.Y:F0}, {packet.Z:F0}) with radius {packet.Radius:F0}. Total: {stormWallRenderer.GetDomeCount()}");
                    break;
                case "remove":
                    if (stormWallRenderer.RemoveNearestDome(packet.X, packet.Y, packet.Z))
                    {
                        capi?.ShowChatMessage($"Removed nearest dome. Remaining: {stormWallRenderer.GetDomeCount()}");
                    }
                    else
                    {
                        capi?.ShowChatMessage("No domes to remove");
                    }
                    break;
                case "clear":
                    int count = stormWallRenderer.GetDomeCount();
                    stormWallRenderer.ClearDomes();
                    capi?.ShowChatMessage($"Cleared {count} dome(s)");
                    break;
                case "list":
                    var domes = stormWallRenderer.GetDomes();
                    if (domes.Count == 0)
                    {
                        capi?.ShowChatMessage("No domes placed");
                    }
                    else
                    {
                        capi?.ShowChatMessage($"=== {domes.Count} Dome(s) ===");
                        for (int i = 0; i < domes.Count; i++)
                        {
                            var d = domes[i];
                            capi?.ShowChatMessage($"  [{i + 1}] Pos: ({d.X:F0}, {d.Y:F0}, {d.Z:F0}) Radius: {d.Radius:F0}");
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Gets the current storm wall config for the client.
        /// </summary>
        public StormWallConfigPacket GetStormWallConfig()
        {
            return clientStormWallConfig;
        }

        /// <summary>
        /// Gets the set of devastated chunk keys for the storm wall renderer.
        /// </summary>
        public HashSet<long> GetClientDevastatedChunks()
        {
            return clientDevastatedChunks;
        }

        /// <summary>
        /// Called when the client receives devastated chunk data from the server.
        /// </summary>
        private void OnDevastatedChunkSync(DevastatedChunkSyncPacket packet)
        {
            clientDevastatedChunks.Clear();

            if (packet.ChunkXs != null && packet.ChunkZs != null)
            {
                int count = Math.Min(packet.ChunkXs.Count, packet.ChunkZs.Count);
                for (int i = 0; i < count; i++)
                {
                    long chunkKey = DevastatedChunk.MakeChunkKey(packet.ChunkXs[i], packet.ChunkZs[i]);
                    clientDevastatedChunks.Add(chunkKey);
                }
            }
        }

        /// <summary>
        /// Checks if the player is currently in a devastated chunk (client-side).
        /// </summary>
        public bool IsPlayerInDevastatedChunk()
        {
            if (capi?.World?.Player?.Entity == null) return false;

            var playerPos = capi.World.Player.Entity.Pos;
            int chunkX = (int)playerPos.X / CHUNK_SIZE;
            int chunkZ = (int)playerPos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            return clientDevastatedChunks.Contains(chunkKey);
        }

        /// <summary>
        /// Gets the distance to the nearest devastated chunk edge (for fog intensity).
        /// Returns 0 if in a devastated chunk, positive distance to nearest devastated chunk edge.
        /// </summary>
        public float GetDistanceToDevastatedChunk()
        {
            if (capi?.World?.Player?.Entity == null) return float.MaxValue;

            var playerPos = capi.World.Player.Entity.Pos;
            int playerChunkX = (int)playerPos.X / CHUNK_SIZE;
            int playerChunkZ = (int)playerPos.Z / CHUNK_SIZE;
            long playerChunkKey = DevastatedChunk.MakeChunkKey(playerChunkX, playerChunkZ);

            // If player is in a devastated chunk, return 0
            if (clientDevastatedChunks.Contains(playerChunkKey)) return 0f;

            // Find the nearest devastated chunk
            float nearestDistSq = float.MaxValue;
            foreach (long chunkKey in clientDevastatedChunks)
            {
                int chunkX = (int)(chunkKey >> 32);
                int chunkZ = (int)(chunkKey & 0xFFFFFFFF);

                // Calculate distance to chunk center
                float chunkCenterX = chunkX * CHUNK_SIZE + CHUNK_SIZE / 2f;
                float chunkCenterZ = chunkZ * CHUNK_SIZE + CHUNK_SIZE / 2f;

                float dx = (float)playerPos.X - chunkCenterX;
                float dz = (float)playerPos.Z - chunkCenterZ;
                float distSq = dx * dx + dz * dz;

                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                }
            }

            return (float)Math.Sqrt(nearestDistSq);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            // Load config
            LoadConfig();

            // Get the server network channel for syncing devastated chunks to clients
            serverNetworkChannel = api.Network.GetChannel(NETWORK_CHANNEL_NAME);

            // Check for manual sources every 10ms (100 times per second)
            api.Event.RegisterGameTickListener(SpreadDevastationFromRifts, 10);

            // Process devastated chunks (spawning and rapid spreading) every 500ms
            api.Event.RegisterGameTickListener(ProcessDevastatedChunks, 500);

            // Process rift wards (check active state and healing) every 500ms
            api.Event.RegisterGameTickListener(ProcessRiftWards, 500);

            // Sync devastated chunks to clients every 5 seconds
            api.Event.RegisterGameTickListener(SyncDevastatedChunksToClients, 5000);

            // Listen for block placement/removal to track rift wards efficiently
            api.Event.DidPlaceBlock += OnBlockPlaced;
            api.Event.DidBreakBlock += OnBlockBroken;

            // Save/load events
            api.Event.SaveGameLoaded += OnSaveGameLoading;
            api.Event.GameWorldSave += OnSaveGameSaving;

            // Register commands using the modern ChatCommand API (with shorthand alias)
            RegisterDevastateCommand(api, "devastate");
            RegisterDevastateCommand(api, "dv");

            api.ChatCommands.Create("devastationconfig")
                .WithDescription("Reload configuration from file")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => {
                    LoadConfig();
                    return TextCommandResult.Success("Configuration reloaded from ModConfig/SpreadingDevastationConfig.json");
                });
        }

        /// <summary>
        /// Syncs devastated chunk data to all connected clients.
        /// Each player receives only the chunks within render distance of their position.
        /// </summary>
        private void SyncDevastatedChunksToClients(float dt)
        {
            if (serverNetworkChannel == null) return;

            const int SYNC_RADIUS_CHUNKS = 8; // Sync chunks within 8 chunk radius (~256 blocks)

            // Only create and send fog config packet when it has changed
            FogConfigPacket fogPacket = null;
            if (fogConfigDirty)
            {
                fogPacket = new FogConfigPacket
                {
                    Enabled = config.FogEffectEnabled,
                    ColorR = config.FogColorR,
                    ColorG = config.FogColorG,
                    ColorB = config.FogColorB,
                    Density = config.FogDensity,
                    Min = config.FogMin,
                    ColorWeight = config.FogColorWeight,
                    DensityWeight = config.FogDensityWeight,
                    MinWeight = config.FogMinWeight,
                    TransitionSpeed = config.FogTransitionSpeed
                };
                fogConfigDirty = false;
            }

            // Only create and send storm wall config packet when it has changed
            StormWallConfigPacket stormWallPacket = null;
            if (stormWallConfigDirty)
            {
                stormWallPacket = new StormWallConfigPacket
                {
                    Enabled = config.StormWallEnabled,
                    Height = config.StormWallHeight,
                    BaseOpacity = config.StormWallBaseOpacity,
                    TopOpacity = config.StormWallTopOpacity,
                    ColorR = config.StormWallColorR,
                    ColorG = config.StormWallColorG,
                    ColorB = config.StormWallColorB,
                    RenderDistance = config.StormWallRenderDistance,
                    AnimationSpeed = config.StormWallAnimationSpeed,
                    ParticlesEnabled = config.StormWallParticlesEnabled,
                    ParticleDistance = config.StormWallParticleDistance,
                    AshParticlesPerSecond = config.AshParticlesPerSecond,
                    DustParticlesPerSecond = config.DustParticlesPerSecond,
                    LightningChance = config.LightningChancePerSecond
                };
                stormWallConfigDirty = false;
            }

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers.Cast<IServerPlayer>())
            {
                if (player?.Entity == null) continue;

                // Send fog config only when it changed
                if (fogPacket != null)
                {
                    serverNetworkChannel.SendPacket(fogPacket, player);
                }

                // Send storm wall config only when it changed
                if (stormWallPacket != null)
                {
                    serverNetworkChannel.SendPacket(stormWallPacket, player);
                }

                // Send chunk data if there are any devastated chunks
                if (devastatedChunks == null || devastatedChunks.Count == 0) continue;

                var playerPos = player.Entity.Pos;
                int playerChunkX = (int)playerPos.X / CHUNK_SIZE;
                int playerChunkZ = (int)playerPos.Z / CHUNK_SIZE;

                var packet = new DevastatedChunkSyncPacket();

                foreach (var chunk in devastatedChunks.Values)
                {
                    // Only sync chunks within range of player
                    int dx = chunk.ChunkX - playerChunkX;
                    int dz = chunk.ChunkZ - playerChunkZ;

                    if (Math.Abs(dx) <= SYNC_RADIUS_CHUNKS && Math.Abs(dz) <= SYNC_RADIUS_CHUNKS)
                    {
                        packet.ChunkXs.Add(chunk.ChunkX);
                        packet.ChunkZs.Add(chunk.ChunkZ);
                    }
                }

                // Send packet to this specific player
                serverNetworkChannel.SendPacket(packet, player);
            }
        }

        /// <summary>
        /// Marks the fog configuration as changed, so it will be sent to clients on next sync.
        /// </summary>
        private void BroadcastFogConfig()
        {
            fogConfigDirty = true;
        }

        /// <summary>
        /// Marks the storm wall configuration as changed, so it will be sent to clients on next sync.
        /// </summary>
        private void BroadcastStormWallConfig()
        {
            stormWallConfigDirty = true;
        }

        private void LoadConfig()
        {
            try
            {
                config = sapi.LoadModConfig<SpreadingDevastationConfig>("SpreadingDevastationConfig.json");
                if (config == null)
                {
                    config = new SpreadingDevastationConfig();
                    sapi.StoreModConfig(config, "SpreadingDevastationConfig.json");
                    sapi.Logger.Notification("SpreadingDevastation: Created default config file");
                }
                else
                {
                    sapi.Logger.Notification("SpreadingDevastation: Loaded config file");
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error loading config: {ex.Message}");
                config = new SpreadingDevastationConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                sapi.StoreModConfig(config, "SpreadingDevastationConfig.json");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error saving config: {ex.Message}");
            }
        }

        private void OnSaveGameLoading()
        {
            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData("regrowingBlocks");
                regrowingBlocks = data == null ? new List<RegrowingBlocks>() : SerializerUtil.Deserialize<List<RegrowingBlocks>>(data);

                byte[] sourcesData = sapi.WorldManager.SaveGame.GetData("devastationSources");
                devastationSources = sourcesData == null ? new List<DevastationSource>() : SerializerUtil.Deserialize<List<DevastationSource>>(sourcesData);

                byte[] pausedData = sapi.WorldManager.SaveGame.GetData("devastationPaused");
                isPaused = pausedData == null ? false : SerializerUtil.Deserialize<bool>(pausedData);

                byte[] nextIdData = sapi.WorldManager.SaveGame.GetData("devastationNextSourceId");
                nextSourceId = nextIdData == null ? 1 : SerializerUtil.Deserialize<int>(nextIdData);

                // Load devastated chunks
                byte[] chunksData = sapi.WorldManager.SaveGame.GetData("devastatedChunks");
                devastatedChunks = new Dictionary<long, DevastatedChunk>();
                if (chunksData != null)
                {
                    var chunkList = SerializerUtil.Deserialize<List<DevastatedChunk>>(chunksData);
                    foreach (var chunk in chunkList)
                    {
                        devastatedChunks[chunk.ChunkKey] = chunk;
                    }
                }

                // Load rift wards
                byte[] riftWardsData = sapi.WorldManager.SaveGame.GetData("riftWards");
                activeRiftWards = riftWardsData == null ? new List<RiftWard>() : SerializerUtil.Deserialize<List<RiftWard>>(riftWardsData);

                // Rebuild protected chunk cache
                RebuildProtectedChunkCache();

                // Restore rift ward active states and apply their effects
                InitializeRiftWardsOnLoad();

                sapi.Logger.Notification($"SpreadingDevastation: Loaded {devastationSources?.Count ?? 0} sources, {regrowingBlocks?.Count ?? 0} regrowing blocks, {devastatedChunks?.Count ?? 0} devastated chunks, {activeRiftWards?.Count ?? 0} rift wards");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error loading save data: {ex.Message}");
                // Initialize empty collections if loading failed
                regrowingBlocks = regrowingBlocks ?? new List<RegrowingBlocks>();
                devastationSources = devastationSources ?? new List<DevastationSource>();
                devastatedChunks = devastatedChunks ?? new Dictionary<long, DevastatedChunk>();
                activeRiftWards = activeRiftWards ?? new List<RiftWard>();
            }
        }

        private void OnSaveGameSaving()
        {
            try
            {
                sapi.WorldManager.SaveGame.StoreData("regrowingBlocks", SerializerUtil.Serialize(regrowingBlocks ?? new List<RegrowingBlocks>()));
                sapi.WorldManager.SaveGame.StoreData("devastationSources", SerializerUtil.Serialize(devastationSources ?? new List<DevastationSource>()));
                sapi.WorldManager.SaveGame.StoreData("devastationPaused", SerializerUtil.Serialize(isPaused));
                sapi.WorldManager.SaveGame.StoreData("devastationNextSourceId", SerializerUtil.Serialize(nextSourceId));

                // Save devastated chunks as a list
                var chunkList = devastatedChunks?.Values.ToList() ?? new List<DevastatedChunk>();
                sapi.WorldManager.SaveGame.StoreData("devastatedChunks", SerializerUtil.Serialize(chunkList));

                // Save rift wards
                sapi.WorldManager.SaveGame.StoreData("riftWards", SerializerUtil.Serialize(activeRiftWards ?? new List<RiftWard>()));

                sapi.Logger.VerboseDebug($"SpreadingDevastation: Saved {devastationSources?.Count ?? 0} sources, {regrowingBlocks?.Count ?? 0} regrowing blocks, {devastatedChunks?.Count ?? 0} devastated chunks, {activeRiftWards?.Count ?? 0} rift wards");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error saving data: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends multi-line command output using SendMessage, then returns empty success.
        /// </summary>
        private TextCommandResult SendChatLines(TextCommandCallingArgs args, IEnumerable<string> lines, string playerAck = null)
        {
            var player = args?.Caller?.Player as IServerPlayer;
            var safeLines = lines?.Where(l => !string.IsNullOrWhiteSpace(l)).ToList() ?? new List<string>();

            if (player != null && safeLines.Count > 0)
            {
                // Send as a single message with newlines
                string combined = string.Join("\n", safeLines);
                player.SendMessage(GlobalConstants.GeneralChatGroup, combined, EnumChatType.Notification);
            }

            // Return empty success to avoid duplicate output
            return TextCommandResult.Success();
        }

        #region Command Handlers

        /// <summary>
        /// Registers the devastate command tree under the provided command name (e.g., "devastate" and alias "dv").
        /// </summary>
        private void RegisterDevastateCommand(ICoreServerAPI api, string commandName)
        {
            api.ChatCommands.Create(commandName)
                .WithDescription("Manage devastation sources")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("add")
                    .WithDescription("Add a devastation source at the block you're looking at")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWordRange("range", "range"),
                              api.ChatCommands.Parsers.OptionalInt("rangeValue"),
                              api.ChatCommands.Parsers.OptionalWordRange("amount", "amount"),
                              api.ChatCommands.Parsers.OptionalInt("amountValue"))
                    .HandleWith(args => HandleAddCommand(args, false))
                .EndSubCommand()
                .BeginSubCommand("heal")
                    .WithDescription("Add a healing source at the block you're looking at")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWordRange("range", "range"),
                              api.ChatCommands.Parsers.OptionalInt("rangeValue"),
                              api.ChatCommands.Parsers.OptionalWordRange("amount", "amount"),
                              api.ChatCommands.Parsers.OptionalInt("amountValue"))
                    .HandleWith(args => HandleAddCommand(args, true))
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove devastation sources")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("target"))
                    .HandleWith(HandleRemoveCommand)
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("List devastation sources")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("countOrSummary"))
                    .HandleWith(HandleListCommand)
                .EndSubCommand()
                .BeginSubCommand("maxsources")
                    .WithDescription("Set maximum number of sources")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("count"))
                    .HandleWith(HandleMaxSourcesCommand)
                .EndSubCommand()
                .BeginSubCommand("maxattempts")
                    .WithDescription("Set max failed spawn attempts before saturation")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("count"))
                    .HandleWith(HandleMaxAttemptsCommand)
                .EndSubCommand()
                .BeginSubCommand("aircontact")
                    .WithDescription("Toggle surface spreading (require air contact for new sources)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("onoff"))
                    .HandleWith(HandleAirContactCommand)
                .EndSubCommand()
                .BeginSubCommand("markers")
                    .WithDescription("Toggle magenta source markers for debugging")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("onoff"))
                    .HandleWith(HandleMarkersCommand)
                .EndSubCommand()
                .BeginSubCommand("miny")
                    .WithDescription("Set minimum Y level for new sources")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .HandleWith(HandleMinYCommand)
                .EndSubCommand()
                .BeginSubCommand("stop")
                    .WithDescription("Pause all devastation spreading")
                    .HandleWith(args => { isPaused = true; return TextCommandResult.Success("Devastation spreading STOPPED. Use '/devastate start' to resume."); })
                .EndSubCommand()
                .BeginSubCommand("start")
                    .WithDescription("Resume devastation spreading")
                    .HandleWith(args => { isPaused = false; return TextCommandResult.Success("Devastation spreading STARTED."); })
                .EndSubCommand()
                .BeginSubCommand("status")
                    .WithDescription("Show current devastation status")
                    .HandleWith(HandleStatusCommand)
                .EndSubCommand()
                .BeginSubCommand("speed")
                    .WithDescription("Set devastation spread speed multiplier")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("multiplier"))
                    .HandleWith(HandleSpeedCommand)
                .EndSubCommand()
                .BeginSubCommand("chunk")
                    .WithDescription("Mark the chunk you're looking at as devastated, or configure chunk settings")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleChunkCommand)
                .EndSubCommand()
                .BeginSubCommand("riftward")
                    .WithDescription("Configure rift ward settings (speed, list, info)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleRiftWardCommand)
                .EndSubCommand()
                .BeginSubCommand("fog")
                    .WithDescription("Configure devastation fog/sky effect")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("setting"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleFogCommand)
                .EndSubCommand()
                .BeginSubCommand("stormwall")
                    .WithDescription("Configure devastation storm wall visual effect")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("setting"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleStormWallCommand)
                .EndSubCommand()
                .BeginSubCommand("dome")
                    .WithDescription("Place a devastation dome effect (client-side only for testing)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalAll("args"))
                    .HandleWith(HandleDomeCommand)
                .EndSubCommand()
                .BeginSubCommand("reset")
                    .WithDescription("Reset all config values to defaults")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("confirm"))
                    .HandleWith(HandleResetCommand)
                .EndSubCommand();
        }

        private TextCommandResult HandleSpeedCommand(TextCommandCallingArgs args)
        {
            string rawArg = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrWhiteSpace(rawArg))
            {
                return SendChatLines(args, new[]
                {
                    $"Current devastation speed: {config.SpeedMultiplier:F2}x",
                    "Usage: /dv speed [multiplier] (e.g., 0.5 for half speed, 5 for 5x speed)"
                }, "Speed info sent to chat (scrollable)");
            }

            if (!double.TryParse(rawArg, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv speed [multiplier] (e.g., 0.5, 1, 5)");
            }

            double newSpeed = Math.Clamp(parsedSpeed, 0.01, 100.0);
            config.SpeedMultiplier = newSpeed;
            SaveConfig();
            return TextCommandResult.Success($"Devastation speed set to {config.SpeedMultiplier:F2}x");
        }

        private TextCommandResult HandleRiftWardCommand(TextCommandCallingArgs args)
        {
            string action = args.Parsers[0].GetValue() as string ?? "";
            string value = args.Parsers[1].GetValue() as string ?? "";

            switch (action.ToLowerInvariant())
            {
                case "speed":
                    return HandleRiftWardSpeedCommand(value);

                case "list":
                    return HandleRiftWardListCommand(args);

                case "rate":
                    return HandleRiftWardRateCommand(value);

                case "mode":
                    return HandleRiftWardModeCommand(value);

                case "radius":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Rift ward protection radius: {config.RiftWardProtectionRadius} blocks. Use '/dv riftward radius [blocks]' to set.");
                    }
                    if (int.TryParse(value, out int newRadius))
                    {
                        config.RiftWardProtectionRadius = Math.Clamp(newRadius, 8, 1024);
                        SaveConfig();
                        RebuildProtectedChunkCache();
                        return TextCommandResult.Success($"Rift ward protection radius set to {config.RiftWardProtectionRadius} blocks");
                    }
                    return TextCommandResult.Error("Invalid number for radius");

                case "":
                case "info":
                case "status":
                    double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
                    string speedSource = config.RiftWardSpeedMultiplier > 0 ? "custom" : "global";
                    string healingMode = GetEffectiveCleanMode();
                    return SendChatLines(args, new[]
                    {
                        "=== Rift Ward Settings ===",
                        $"Protection radius: {config.RiftWardProtectionRadius} blocks",
                        $"Healing enabled: {config.RiftWardHealingEnabled}",
                        $"Healing mode: {healingMode} (raster=efficient scan, radial=outward, random=anywhere)",
                        $"Base healing rate: {config.RiftWardHealingRate:F1} blk/s",
                        $"Speed multiplier: {effectiveSpeed:F2}x ({speedSource})",
                        $"Effective rate: {config.RiftWardHealingRate * effectiveSpeed:F1} blk/s",
                        $"Active rift wards: {activeRiftWards?.Count ?? 0}",
                        "",
                        "Commands:",
                        "  /dv riftward radius [blocks] - Set protection radius",
                        "  /dv riftward speed [multiplier] - Set healing speed (or 'global' to use /dv speed)",
                        "  /dv riftward rate [blk/s] - Set base healing rate",
                        "  /dv riftward mode [raster|radial|random] - Set healing pattern mode",
                        "  /dv riftward list - Show all tracked rift wards"
                    }, "Rift ward info sent to chat");

                default:
                    return TextCommandResult.Error($"Unknown riftward action: {action}. Use: radius, speed, rate, mode, list, or info");
            }
        }

        private TextCommandResult HandleRiftWardSpeedCommand(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
                string speedSource = config.RiftWardSpeedMultiplier > 0 ? "custom" : "global";
                return TextCommandResult.Success($"Rift ward healing speed: {effectiveSpeed:F2}x ({speedSource}). Use '/dv riftward speed [multiplier]' to set, or 'global' to use devastation speed.");
            }

            if (value.ToLowerInvariant() == "global" || value.ToLowerInvariant() == "default")
            {
                config.RiftWardSpeedMultiplier = -1;
                SaveConfig();
                return TextCommandResult.Success($"Rift ward healing now uses global devastation speed ({config.SpeedMultiplier:F2}x)");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv riftward speed [multiplier] (e.g., 5, 10, or 'global')");
            }

            double newSpeed = Math.Clamp(parsedSpeed, 0.01, 1000.0);
            config.RiftWardSpeedMultiplier = newSpeed;
            SaveConfig();
            return TextCommandResult.Success($"Rift ward healing speed set to {config.RiftWardSpeedMultiplier:F2}x (effective rate: {config.RiftWardHealingRate * newSpeed:F1} blk/s)");
        }

        private TextCommandResult HandleRiftWardRateCommand(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TextCommandResult.Success($"Rift ward base healing rate: {config.RiftWardHealingRate:F1} blk/s. Use '/dv riftward rate [value]' to set.");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRate))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv riftward rate [value] (e.g., 10, 50, 100)");
            }

            double newRate = Math.Clamp(parsedRate, 0.1, 10000.0);
            config.RiftWardHealingRate = newRate;
            SaveConfig();
            double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
            return TextCommandResult.Success($"Rift ward base healing rate set to {config.RiftWardHealingRate:F1} blk/s (effective: {config.RiftWardHealingRate * effectiveSpeed:F1} blk/s)");
        }

        private TextCommandResult HandleRiftWardModeCommand(string value)
        {
            string currentMode = GetEffectiveCleanMode();

            if (string.IsNullOrWhiteSpace(value))
            {
                return TextCommandResult.Success($"Rift ward healing mode: {currentMode}. Use '/dv riftward mode [raster|radial|random]' to change.");
            }

            switch (value.ToLowerInvariant())
            {
                case "raster":
                case "scan":
                case "s":
                    config.RiftWardCleanMode = "raster";
                    SaveConfig();
                    // Reset all ward scan progress when switching to raster mode
                    foreach (var ward in activeRiftWards)
                    {
                        ward.CurrentCleanRadius = 0;
                        ward.ScanX = 0;
                        ward.ScanY = 0;
                        ward.ScanZ = 0;
                        ward.RasterScanComplete = false;
                    }
                    return TextCommandResult.Success("Rift ward healing mode set to raster. Expanding globe scan - efficient and thorough.");

                case "radial":
                case "r":
                    config.RiftWardCleanMode = "radial";
                    SaveConfig();
                    // Reset all ward clean progress when switching to radial mode
                    foreach (var ward in activeRiftWards)
                    {
                        ward.CurrentCleanRadius = 0;
                        ward.RadialCleanFailures = 0;
                    }
                    return TextCommandResult.Success("Rift ward healing mode set to radial. Devastation will be cleared outward from the center (legacy mode).");

                case "random":
                case "rand":
                    config.RiftWardCleanMode = "random";
                    SaveConfig();
                    return TextCommandResult.Success("Rift ward healing mode set to random. Devastation will be cleared randomly within protection radius.");

                default:
                    return TextCommandResult.Error($"Unknown mode: {value}. Use 'raster', 'radial', or 'random'.");
            }
        }

        private TextCommandResult HandleRiftWardListCommand(TextCommandCallingArgs args)
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0)
            {
                return TextCommandResult.Success("No rift wards are currently tracked.");
            }

            string mode = GetEffectiveCleanMode();
            var lines = new List<string> { $"=== Tracked Rift Wards ({activeRiftWards.Count}) - Mode: {mode} ===" };
            foreach (var ward in activeRiftWards)
            {
                bool isActive = IsRiftWardActive(ward.Pos);
                string status = isActive ? "ACTIVE" : "inactive";
                string progressInfo = "";
                if (mode == "raster")
                {
                    string scanStatus = ward.RasterScanComplete ? "complete" : $"radius {ward.CurrentCleanRadius}/{config.RiftWardProtectionRadius}";
                    progressInfo = $", scan: {scanStatus}";
                }
                else if (mode == "radial")
                {
                    progressInfo = $", cleaned {ward.MaxCleanRadiusReached}/{config.RiftWardProtectionRadius}";
                }
                lines.Add($"  {ward.Pos} - {status}, healed {ward.BlocksHealed} blocks{progressInfo}");
            }
            return SendChatLines(args, lines, "Rift ward list sent to chat");
        }

        private TextCommandResult HandleFogCommand(TextCommandCallingArgs args)
        {
            string setting = (args.Parsers[0].GetValue() as string ?? "").ToLowerInvariant();
            string value = args.Parsers[1].GetValue() as string ?? "";

            switch (setting)
            {
                case "enabled":
                case "on":
                case "off":
                    if (setting == "on" || (setting == "enabled" && value.ToLowerInvariant() == "on"))
                    {
                        config.FogEffectEnabled = true;
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success("Devastation fog effect ENABLED");
                    }
                    else if (setting == "off" || (setting == "enabled" && value.ToLowerInvariant() == "off"))
                    {
                        config.FogEffectEnabled = false;
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success("Devastation fog effect DISABLED");
                    }
                    return TextCommandResult.Error("Usage: /dv fog enabled [on|off]");

                case "color":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog color: R={config.FogColorR:F2} G={config.FogColorG:F2} B={config.FogColorB:F2}. Use '/dv fog color [r] [g] [b]' (0.0-1.0)");
                    }
                    // Parse r g b from value (space-separated)
                    var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 &&
                        float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                    {
                        config.FogColorR = Math.Clamp(r, 0f, 1f);
                        config.FogColorG = Math.Clamp(g, 0f, 1f);
                        config.FogColorB = Math.Clamp(b, 0f, 1f);
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success($"Fog color set to R={config.FogColorR:F2} G={config.FogColorG:F2} B={config.FogColorB:F2}");
                    }
                    return TextCommandResult.Error("Usage: /dv fog color [r] [g] [b] (values 0.0-1.0, e.g., '0.55 0.25 0.15')");

                case "density":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog density: {config.FogDensity:F4}. Use '/dv fog density [value]' (e.g., 0.004)");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float density))
                    {
                        config.FogDensity = Math.Clamp(density, 0f, 1f);
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success($"Fog density set to {config.FogDensity:F4}");
                    }
                    return TextCommandResult.Error("Invalid number for fog density");

                case "min":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog minimum: {config.FogMin:F2}. Use '/dv fog min [value]' (0.0-1.0)");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fogMin))
                    {
                        config.FogMin = Math.Clamp(fogMin, 0f, 1f);
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success($"Fog minimum set to {config.FogMin:F2}");
                    }
                    return TextCommandResult.Error("Invalid number for fog minimum");

                case "weight":
                case "weights":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog weights: color={config.FogColorWeight:F2} density={config.FogDensityWeight:F2} min={config.FogMinWeight:F2}. Use '/dv fog weight [color|density|min] [value]'");
                    }
                    var weightParts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (weightParts.Length >= 2 &&
                        float.TryParse(weightParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float weightVal))
                    {
                        weightVal = Math.Clamp(weightVal, 0f, 1f);
                        switch (weightParts[0].ToLowerInvariant())
                        {
                            case "color":
                                config.FogColorWeight = weightVal;
                                SaveConfig();
                                BroadcastFogConfig();
                                return TextCommandResult.Success($"Fog color weight set to {config.FogColorWeight:F2}");
                            case "density":
                                config.FogDensityWeight = weightVal;
                                SaveConfig();
                                BroadcastFogConfig();
                                return TextCommandResult.Success($"Fog density weight set to {config.FogDensityWeight:F2}");
                            case "min":
                                config.FogMinWeight = weightVal;
                                SaveConfig();
                                BroadcastFogConfig();
                                return TextCommandResult.Success($"Fog min weight set to {config.FogMinWeight:F2}");
                        }
                    }
                    return TextCommandResult.Error("Usage: /dv fog weight [color|density|min] [value] (0.0-1.0)");

                case "transition":
                case "speed":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog transition speed: {config.FogTransitionSpeed:F2}s. Use '/dv fog transition [seconds]'");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float transSpeed))
                    {
                        config.FogTransitionSpeed = Math.Clamp(transSpeed, 0.1f, 10f);
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success($"Fog transition speed set to {config.FogTransitionSpeed:F2}s");
                    }
                    return TextCommandResult.Error("Invalid number for transition speed");

                case "reset":
                case "defaults":
                    // Reset all fog values to defaults
                    config.FogEffectEnabled = true;
                    config.FogColorR = 0.55f;
                    config.FogColorG = 0.25f;
                    config.FogColorB = 0.15f;
                    config.FogDensity = 0.004f;
                    config.FogMin = 0.15f;
                    config.FogColorWeight = 0.7f;
                    config.FogDensityWeight = 0.5f;
                    config.FogMinWeight = 0.6f;
                    config.FogTransitionSpeed = 0.5f;
                    SaveConfig();
                    BroadcastFogConfig();
                    return TextCommandResult.Success("Fog settings reset to defaults (rusty orange fog, enabled)");

                case "":
                case "info":
                case "status":
                    return SendChatLines(args, new[]
                    {
                        "=== Devastation Fog Settings ===",
                        $"Enabled: {config.FogEffectEnabled}",
                        $"Color (RGB): {config.FogColorR:F2}, {config.FogColorG:F2}, {config.FogColorB:F2}",
                        $"Density: {config.FogDensity:F4}",
                        $"Minimum fog: {config.FogMin:F2}",
                        $"Weights: color={config.FogColorWeight:F2}, density={config.FogDensityWeight:F2}, min={config.FogMinWeight:F2}",
                        $"Transition speed: {config.FogTransitionSpeed:F2}s",
                        "",
                        "Commands:",
                        "  /dv fog on|off - Enable/disable fog effect",
                        "  /dv fog color [r] [g] [b] - Set fog color (0.0-1.0)",
                        "  /dv fog density [value] - Set fog density",
                        "  /dv fog min [value] - Set minimum fog level",
                        "  /dv fog weight [color|density|min] [value] - Set effect weights",
                        "  /dv fog transition [seconds] - Set transition speed",
                        "  /dv fog reset - Reset all fog settings to defaults"
                    }, "Fog settings sent to chat");

                default:
                    return TextCommandResult.Error($"Unknown fog setting: {setting}. Use: on, off, color, density, min, weight, transition, reset, or info");
            }
        }

        private TextCommandResult HandleStormWallCommand(TextCommandCallingArgs args)
        {
            string setting = (args.Parsers[0].GetValue() as string ?? "").ToLowerInvariant();
            string value = args.Parsers[1].GetValue() as string ?? "";

            switch (setting)
            {
                case "on":
                case "enabled":
                    if (string.IsNullOrWhiteSpace(value) || value == "on" || value == "true")
                    {
                        config.StormWallEnabled = true;
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success("Storm wall effect ENABLED");
                    }
                    else if (value == "off" || value == "false")
                    {
                        config.StormWallEnabled = false;
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success("Storm wall effect DISABLED");
                    }
                    return TextCommandResult.Error("Usage: /dv stormwall enabled [on|off]");

                case "off":
                case "disabled":
                    config.StormWallEnabled = false;
                    SaveConfig();
                    BroadcastStormWallConfig();
                    return TextCommandResult.Success("Storm wall effect DISABLED");

                case "height":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Storm wall height: {config.StormWallHeight:F1} blocks. Use '/dv stormwall height [value]'");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float height))
                    {
                        config.StormWallHeight = Math.Clamp(height, 8f, 256f);
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success($"Storm wall height set to {config.StormWallHeight:F1} blocks");
                    }
                    return TextCommandResult.Error("Invalid number for height");

                case "color":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Storm wall color: R={config.StormWallColorR:F2} G={config.StormWallColorG:F2} B={config.StormWallColorB:F2}. Use '/dv stormwall color [r] [g] [b]' (0.0-1.0)");
                    }
                    var colorParts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (colorParts.Length >= 3 &&
                        float.TryParse(colorParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cr) &&
                        float.TryParse(colorParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cg) &&
                        float.TryParse(colorParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float cb))
                    {
                        config.StormWallColorR = Math.Clamp(cr, 0f, 1f);
                        config.StormWallColorG = Math.Clamp(cg, 0f, 1f);
                        config.StormWallColorB = Math.Clamp(cb, 0f, 1f);
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success($"Storm wall color set to R={config.StormWallColorR:F2} G={config.StormWallColorG:F2} B={config.StormWallColorB:F2}");
                    }
                    return TextCommandResult.Error("Usage: /dv stormwall color [r] [g] [b] (values 0.0-1.0)");

                case "opacity":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Storm wall opacity: base={config.StormWallBaseOpacity:F2} top={config.StormWallTopOpacity:F2}. Use '/dv stormwall opacity [base] [top]'");
                    }
                    var opacityParts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (opacityParts.Length >= 2 &&
                        float.TryParse(opacityParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float baseOp) &&
                        float.TryParse(opacityParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float topOp))
                    {
                        config.StormWallBaseOpacity = Math.Clamp(baseOp, 0f, 1f);
                        config.StormWallTopOpacity = Math.Clamp(topOp, 0f, 1f);
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success($"Storm wall opacity set to base={config.StormWallBaseOpacity:F2} top={config.StormWallTopOpacity:F2}");
                    }
                    else if (opacityParts.Length == 1 && float.TryParse(opacityParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float singleOp))
                    {
                        config.StormWallTopOpacity = Math.Clamp(singleOp, 0f, 1f);
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success($"Storm wall top opacity set to {config.StormWallTopOpacity:F2}");
                    }
                    return TextCommandResult.Error("Usage: /dv stormwall opacity [base] [top] or /dv stormwall opacity [top]");

                case "distance":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Storm wall render distance: {config.StormWallRenderDistance:F0} blocks. Use '/dv stormwall distance [value]'");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float dist))
                    {
                        config.StormWallRenderDistance = Math.Clamp(dist, 32f, 1024f);
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success($"Storm wall render distance set to {config.StormWallRenderDistance:F0} blocks");
                    }
                    return TextCommandResult.Error("Invalid number for render distance");

                case "animation":
                case "speed":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Storm wall animation speed: {config.StormWallAnimationSpeed:F2}. Use '/dv stormwall animation [value]'");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float anim))
                    {
                        config.StormWallAnimationSpeed = Math.Clamp(anim, 0f, 5f);
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success($"Storm wall animation speed set to {config.StormWallAnimationSpeed:F2}");
                    }
                    return TextCommandResult.Error("Invalid number for animation speed");

                case "particles":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Storm wall particles: {(config.StormWallParticlesEnabled ? "ON" : "OFF")}. Use '/dv stormwall particles [on|off]'");
                    }
                    if (value == "on" || value == "true")
                    {
                        config.StormWallParticlesEnabled = true;
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success("Storm wall particles ENABLED");
                    }
                    else if (value == "off" || value == "false")
                    {
                        config.StormWallParticlesEnabled = false;
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success("Storm wall particles DISABLED");
                    }
                    return TextCommandResult.Error("Usage: /dv stormwall particles [on|off]");

                case "ash":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Ash particles per second: {config.AshParticlesPerSecond}. Use '/dv stormwall ash [value]'");
                    }
                    if (int.TryParse(value, out int ash))
                    {
                        config.AshParticlesPerSecond = Math.Clamp(ash, 0, 100);
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success($"Ash particles per second set to {config.AshParticlesPerSecond}");
                    }
                    return TextCommandResult.Error("Invalid number for ash particles");

                case "dust":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Dust particles per second: {config.DustParticlesPerSecond}. Use '/dv stormwall dust [value]'");
                    }
                    if (int.TryParse(value, out int dust))
                    {
                        config.DustParticlesPerSecond = Math.Clamp(dust, 0, 100);
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success($"Dust particles per second set to {config.DustParticlesPerSecond}");
                    }
                    return TextCommandResult.Error("Invalid number for dust particles");

                case "lightning":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Lightning chance per second: {config.LightningChancePerSecond:F2}. Use '/dv stormwall lightning [value]' (0.0-1.0)");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float lightning))
                    {
                        config.LightningChancePerSecond = Math.Clamp(lightning, 0f, 1f);
                        SaveConfig();
                        BroadcastStormWallConfig();
                        return TextCommandResult.Success($"Lightning chance set to {config.LightningChancePerSecond:F2} per second");
                    }
                    return TextCommandResult.Error("Invalid number for lightning chance");

                case "reset":
                case "defaults":
                    config.StormWallEnabled = true;
                    config.StormWallHeight = 64f;
                    config.StormWallBaseOpacity = 0.05f;
                    config.StormWallTopOpacity = 0.4f;
                    config.StormWallColorR = 0.6f;
                    config.StormWallColorG = 0.3f;
                    config.StormWallColorB = 0.15f;
                    config.StormWallRenderDistance = 256f;
                    config.StormWallAnimationSpeed = 0.5f;
                    config.StormWallParticlesEnabled = true;
                    config.StormWallParticleDistance = 48f;
                    config.AshParticlesPerSecond = 15;
                    config.DustParticlesPerSecond = 10;
                    config.LightningChancePerSecond = 0.1f;
                    SaveConfig();
                    BroadcastStormWallConfig();
                    return TextCommandResult.Success("Storm wall settings reset to defaults");

                case "":
                case "info":
                case "status":
                    return SendChatLines(args, new[]
                    {
                        "=== Devastation Storm Wall Settings ===",
                        $"Enabled: {config.StormWallEnabled}",
                        $"Height: {config.StormWallHeight:F1} blocks",
                        $"Color (RGB): {config.StormWallColorR:F2}, {config.StormWallColorG:F2}, {config.StormWallColorB:F2}",
                        $"Opacity: base={config.StormWallBaseOpacity:F2}, top={config.StormWallTopOpacity:F2}",
                        $"Render distance: {config.StormWallRenderDistance:F0} blocks",
                        $"Animation speed: {config.StormWallAnimationSpeed:F2}",
                        "",
                        "=== Particle Settings ===",
                        $"Particles enabled: {config.StormWallParticlesEnabled}",
                        $"Particle distance: {config.StormWallParticleDistance:F0} blocks",
                        $"Ash particles: {config.AshParticlesPerSecond} per second",
                        $"Dust particles: {config.DustParticlesPerSecond} per second",
                        $"Lightning chance: {config.LightningChancePerSecond:F2} per second",
                        "",
                        "Commands:",
                        "  /dv stormwall on|off - Enable/disable storm wall",
                        "  /dv stormwall height [blocks] - Set wall height",
                        "  /dv stormwall color [r] [g] [b] - Set wall color",
                        "  /dv stormwall opacity [base] [top] - Set opacity gradient",
                        "  /dv stormwall distance [blocks] - Set render distance",
                        "  /dv stormwall animation [speed] - Set animation speed",
                        "  /dv stormwall particles [on|off] - Enable/disable particles",
                        "  /dv stormwall ash|dust [count] - Set particles per second",
                        "  /dv stormwall lightning [chance] - Set lightning chance",
                        "  /dv stormwall reset - Reset to defaults"
                    }, "Storm wall settings sent to chat");

                default:
                    return TextCommandResult.Error($"Unknown storm wall setting: {setting}. Use: on, off, height, color, opacity, distance, animation, particles, ash, dust, lightning, reset, or info");
            }
        }

        private TextCommandResult HandleDomeCommand(TextCommandCallingArgs args)
        {
            // This command sends a packet to the client to manage domes
            // The dome rendering happens client-side
            string action = (args.Parsers[0].GetValue() as string ?? "").ToLowerInvariant();
            string argsStr = args.Parsers[1].GetValue() as string ?? "";

            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Player entity not found");
            }

            var pos = player.Entity.Pos;

            switch (action)
            {
                case "add":
                case "place":
                    // Parse optional radius (default 64 blocks)
                    float radius = 64f;
                    if (!string.IsNullOrWhiteSpace(argsStr))
                    {
                        if (float.TryParse(argsStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedRadius))
                        {
                            radius = Math.Clamp(parsedRadius, 8f, 500f);
                        }
                    }

                    serverNetworkChannel.SendPacket(new DomeCommandPacket
                    {
                        Action = "add",
                        X = pos.X,
                        Y = pos.Y,
                        Z = pos.Z,
                        Radius = radius
                    }, player);
                    return TextCommandResult.Success($"Dome command sent (radius: {radius:F0})");

                case "remove":
                case "delete":
                    serverNetworkChannel.SendPacket(new DomeCommandPacket
                    {
                        Action = "remove",
                        X = pos.X,
                        Y = pos.Y,
                        Z = pos.Z
                    }, player);
                    return TextCommandResult.Success("Remove dome command sent");

                case "clear":
                case "removeall":
                    serverNetworkChannel.SendPacket(new DomeCommandPacket
                    {
                        Action = "clear"
                    }, player);
                    return TextCommandResult.Success("Clear domes command sent");

                case "list":
                    serverNetworkChannel.SendPacket(new DomeCommandPacket
                    {
                        Action = "list"
                    }, player);
                    return TextCommandResult.Success("List domes command sent");

                case "":
                case "help":
                case "info":
                    return SendChatLines(args, new[]
                    {
                        "=== Devastation Dome Commands ===",
                        "Place dome effects to test the visual appearance.",
                        "Domes are client-side only (for visual testing).",
                        "",
                        "Commands:",
                        "  /dv dome add [radius] - Place dome at your position (default radius: 64)",
                        "  /dv dome remove - Remove nearest dome",
                        "  /dv dome clear - Remove all domes",
                        "  /dv dome list - List all domes"
                    }, "Dome help sent to chat");

                default:
                    return TextCommandResult.Error($"Unknown dome action: {action}. Use: add, remove, clear, list, or help");
            }
        }

        private TextCommandResult HandleResetCommand(TextCommandCallingArgs args)
        {
            string confirm = (args.Parsers[0].GetValue() as string ?? "").ToLowerInvariant();

            if (confirm != "confirm")
            {
                return SendChatLines(args, new[]
                {
                    "This will reset ALL config values to defaults:",
                    "  - Speed multiplier",
                    "  - Max sources, metastasis threshold",
                    "  - Chunk spreading settings",
                    "  - Rift ward settings",
                    "  - Fog effect settings",
                    "",
                    "Type '/dv reset confirm' to proceed."
                });
            }

            // Create fresh default config
            config = new SpreadingDevastationConfig();
            SaveConfig();

            // Mark fog config as dirty to sync to clients
            BroadcastFogConfig();

            // Rebuild rift ward cache with new radius
            RebuildProtectedChunkCache();

            return TextCommandResult.Success("All config values have been reset to defaults.");
        }

        private TextCommandResult HandleAddCommand(TextCommandCallingArgs args, bool isHealing)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");
            
            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error($"Look at a block to mark it as a {(isHealing ? "healing" : "devastation")} source");
            }
            
            BlockPos pos = blockSel.Position.Copy();
            
            // Check if already exists
            if (devastationSources.Any(s => s.Pos.Equals(pos)))
            {
                return TextCommandResult.Error("This block is already a source (use remove to change it)");
            }
            
            // Parse optional parameters from remaining args
            int range = config.DefaultRange;
            int amount = config.DefaultAmount;
            
            // Parse arguments - they come as pairs: "range" <value> "amount" <value>
            for (int i = 0; i < args.Parsers.Count - 1; i += 2)
            {
                string keyword = args.Parsers[i].GetValue() as string;
                if (string.IsNullOrEmpty(keyword)) continue;
                
                int? value = args.Parsers[i + 1].GetValue() as int?;
                if (!value.HasValue) continue;
                
                if (keyword == "range")
                {
                    range = Math.Clamp(value.Value, 1, 128);
                }
                else if (keyword == "amount")
                {
                    amount = Math.Clamp(value.Value, 1, 100);
                }
            }
            
            // If at cap, remove oldest source to make room
            if (devastationSources.Count >= config.MaxSources)
            {
                RemoveOldestSources(1);
            }
            
            devastationSources.Add(new DevastationSource 
            { 
                Pos = pos,
                Range = range,
                Amount = amount,
                CurrentRadius = Math.Min(3.0, range),
                IsHealing = isHealing,
                SourceId = GenerateSourceId(),
                IsProtected = true, // Manually added sources are protected from auto-removal
                MetastasisThreshold = config.MetastasisThreshold
            });
            
            string action = isHealing ? "healing" : "devastation";
            return TextCommandResult.Success($"Added {action} source at {pos} (range: {range}, amount: {amount} blocks per tick)");
        }

        private TextCommandResult HandleRemoveCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string removeArg = args.Parsers[0].GetValue() as string;
            
            if (removeArg == "all")
            {
                int count = devastationSources.Count;
                devastationSources.Clear();
                return TextCommandResult.Success($"Removed all {count} devastation sources");
            }
            else if (removeArg == "saturated")
            {
                int removed = devastationSources.RemoveAll(s => s.IsSaturated);
                return TextCommandResult.Success($"Removed {removed} saturated devastation sources");
            }
            else if (removeArg == "metastasis")
            {
                int removed = devastationSources.RemoveAll(s => s.IsMetastasis);
                return TextCommandResult.Success($"Removed {removed} metastasis sources (kept original sources)");
            }
            else
            {
                if (player == null) return TextCommandResult.Error("This command must be run by a player");
                
                BlockSelection blockSel = player.CurrentBlockSelection;
                if (blockSel == null)
                {
                    return TextCommandResult.Error("Look at a block to remove it as a devastation source, or use 'remove all/saturated/metastasis'");
                }
                
                BlockPos pos = blockSel.Position;
                int removed = devastationSources.RemoveAll(s => s.Pos.Equals(pos));
                
                if (removed > 0)
                {
                    return TextCommandResult.Success($"Removed devastation source at {pos}");
                }
                else
                {
                    return TextCommandResult.Error("No devastation source found at this location");
                }
            }
        }

        private TextCommandResult HandleListCommand(TextCommandCallingArgs args)
        {
            if (devastationSources.Count == 0)
            {
                return TextCommandResult.Success("No manual devastation sources set");
            }
            
            string arg = args.Parsers[0].GetValue() as string;
            
            // Check if it's a summary request
            if (arg == "summary")
            {
                return ShowListSummary(args);
            }
            
            // Parse limit (default to 10)
            int limit = 10;
            if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out int parsedLimit))
            {
                limit = Math.Clamp(parsedLimit, 1, 100);
            }
            
            int originalCount = devastationSources.Count(s => !s.IsMetastasis);
            int metastasisCount = devastationSources.Count(s => s.IsMetastasis);
            int saturatedCount = devastationSources.Count(s => s.IsSaturated);

            var lines = new List<string>
            {
                $"Devastation sources ({devastationSources.Count}/{config.MaxSources} cap, {originalCount} original, {metastasisCount} metastasis, {saturatedCount} saturated):"
            };
            
            // Sort: active first, then by generation, then by total blocks devastated
            var sortedSources = devastationSources
                .OrderBy(s => s.IsSaturated ? 1 : 0)
                .ThenBy(s => s.GenerationLevel)
                .ThenByDescending(s => s.BlocksDevastatedTotal)
                .Take(limit)
                .ToList();
            
            foreach (var source in sortedSources)
            {
                string type = source.IsHealing ? "HEAL" : "DEV";
                string genInfo = source.IsMetastasis ? $"G{source.GenerationLevel}" : "Orig";
                string statusLabel = GetSourceStatusLabel(source);
                string idInfo = !string.IsNullOrEmpty(source.SourceId) ? $"#{source.SourceId}" : "";
                
                lines.Add($"  [{type}] [{genInfo}] {statusLabel}{idInfo} {source.Pos} R:{source.CurrentRadius:F0}/{source.Range} Tot:{source.BlocksDevastatedTotal}");
            }
            
            if (devastationSources.Count > limit)
            {
                lines.Add($"  ... and {devastationSources.Count - limit} more. Use '/devastate list {limit + 10}' or '/devastate list summary'");
            }
            
            return SendChatLines(args, lines, "Devastation sources listed in chat (scroll to read)");
        }

        private string GetSourceStatusLabel(DevastationSource source)
        {
            if (source.IsSaturated)
            {
                return "[saturated]";
            }
            
            bool readyToSeed = source.BlocksSinceLastMetastasis >= source.MetastasisThreshold && 
                              source.CurrentRadius >= source.Range;
            
            if (source.ChildrenSpawned > 0)
            {
                return $"[seeding {source.ChildrenSpawned}]";
            }
            else if (readyToSeed)
            {
                return "[seeding]";
            }
            else
            {
                return "[growing]";
            }
        }

        private TextCommandResult ShowListSummary(TextCommandCallingArgs args)
        {
            int protectedCount = devastationSources.Count(s => s.IsProtected);
            int metastasisCount = devastationSources.Count(s => s.IsMetastasis);
            int saturatedCount = devastationSources.Count(s => s.IsSaturated);
            int healingCount = devastationSources.Count(s => s.IsHealing);
            int growingCount = devastationSources.Count(s => !s.IsSaturated && !s.IsHealing && 
                (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range));
            int seedingCount = devastationSources.Count(s => !s.IsSaturated && !s.IsHealing && 
                s.BlocksSinceLastMetastasis >= s.MetastasisThreshold && s.CurrentRadius >= s.Range);

            var lines = new List<string>
            {
                $"=== Devastation Summary ({devastationSources.Count}/{config.MaxSources} cap) ===",
                $"  Protected (manual): {protectedCount} (never auto-removed)",
                $"  Metastasis children: {metastasisCount}",
                $"  Healing sources: {healingCount}",
                $"  Growing: {growingCount}",
                $"  Seeding (ready to spawn): {seedingCount}",
                $"  Saturated (done): {saturatedCount}"
            };
            
            // Group by generation level
            var byGeneration = devastationSources
                .GroupBy(s => s.GenerationLevel)
                .OrderBy(g => g.Key)
                .ToList();
            
            if (byGeneration.Count > 0)
            {
                lines.Add("  By Generation:");
                foreach (var gen in byGeneration)
                {
                    int growing = gen.Count(s => !s.IsSaturated && (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range));
                    int seeding = gen.Count(s => !s.IsSaturated && s.BlocksSinceLastMetastasis >= s.MetastasisThreshold && s.CurrentRadius >= s.Range);
                    int sat = gen.Count(s => s.IsSaturated);
                    long totalBlocks = gen.Sum(s => (long)s.BlocksDevastatedTotal);
                    string genLabel = gen.Key == 0 ? "Origin" : $"Gen {gen.Key}";
                    lines.Add($"    {genLabel}: {gen.Count()} ({growing} growing, {seeding} seeding, {sat} saturated) - {totalBlocks:N0} blocks");
                }
            }
            
            // Total stats
            long grandTotalBlocks = devastationSources.Sum(s => (long)s.BlocksDevastatedTotal);
            lines.Add($"  Total blocks devastated: {grandTotalBlocks:N0}");
            
            if (devastationSources.Count >= config.MaxSources)
            {
                lines.Add("  ⚠ At source cap - oldest sources will be removed for new metastasis");
            }
            
            return SendChatLines(args, lines, "Devastation summary sent to chat (scrollable)");
        }

        private TextCommandResult HandleMaxSourcesCommand(TextCommandCallingArgs args)
        {
            int? maxArg = args.Parsers[0].GetValue() as int?;
            
            if (!maxArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current max sources cap: {config.MaxSources}",
                    $"Active sources: {devastationSources.Count}/{config.MaxSources}",
                    "Usage: /devastate maxsources [number] (e.g., 20, 50, 100)"
                }, "Max sources info sent to chat");
            }
            
            int newMax = Math.Clamp(maxArg.Value, 1, 1000);
            config.MaxSources = newMax;
            SaveConfig();
            
            string warning = "";
            if (devastationSources.Count >= config.MaxSources)
            {
                warning = $"\nWarning: Already at or above cap ({devastationSources.Count} sources). No new metastasis will spawn.";
            }
            
            return TextCommandResult.Success($"Max sources cap set to {config.MaxSources}{warning}");
        }

        private TextCommandResult HandleMaxAttemptsCommand(TextCommandCallingArgs args)
        {
            int? attemptsArg = args.Parsers[0].GetValue() as int?;
            
            if (!attemptsArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current max failed spawn attempts: {config.MaxFailedSpawnAttempts}",
                    "Usage: /devastate maxattempts [number] (e.g., 5, 10, 20)"
                }, "Max attempts info sent to chat");
            }
            
            int newAttempts = Math.Clamp(attemptsArg.Value, 1, 100);
            config.MaxFailedSpawnAttempts = newAttempts;
            SaveConfig();
            
            return TextCommandResult.Success($"Max failed spawn attempts set to {config.MaxFailedSpawnAttempts}");
        }

        private TextCommandResult HandleAirContactCommand(TextCommandCallingArgs args)
        {
            string onOff = args.Parsers[0].GetValue() as string;
            
            if (string.IsNullOrEmpty(onOff))
            {
                string status = config.RequireSourceAirContact ? "ON" : "OFF";
                return SendChatLines(args, new[]
                {
                    $"Surface spreading (air contact): {status}",
                    "Usage: /devastate aircontact [on|off]"
                }, "Air contact setting sent to chat");
            }
            
            if (onOff.ToLower() == "on" || onOff == "1" || onOff.ToLower() == "true")
            {
                config.RequireSourceAirContact = true;
                SaveConfig();
                return TextCommandResult.Success("Surface spreading ENABLED - new metastasis sources must be adjacent to air");
            }
            else if (onOff.ToLower() == "off" || onOff == "0" || onOff.ToLower() == "false")
            {
                config.RequireSourceAirContact = false;
                SaveConfig();
                return TextCommandResult.Success("Surface spreading DISABLED - metastasis sources can spawn anywhere");
            }
            else
            {
                return TextCommandResult.Error("Invalid value. Use: on, off, 1, 0, true, or false");
            }
        }

        private TextCommandResult HandleMarkersCommand(TextCommandCallingArgs args)
        {
            string onOff = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrEmpty(onOff))
            {
                string status = config.ShowSourceMarkers ? "ON" : "OFF";
                return SendChatLines(args, new[]
                {
                    $"Source markers: {status}",
                    "Usage: /devastate markers [on|off]"
                }, "Marker setting sent to chat");
            }

            if (onOff.Equals("on", StringComparison.OrdinalIgnoreCase) || onOff == "1" || onOff.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                config.ShowSourceMarkers = true;
                SaveConfig();
                return TextCommandResult.Success("Source markers ENABLED");
            }
            else if (onOff.Equals("off", StringComparison.OrdinalIgnoreCase) || onOff == "0" || onOff.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                config.ShowSourceMarkers = false;
                SaveConfig();
                return TextCommandResult.Success("Source markers DISABLED");
            }
            else
            {
                return TextCommandResult.Error("Invalid value. Use: on, off, 1, 0, true, or false");
            }
        }

        private TextCommandResult HandleMinYCommand(TextCommandCallingArgs args)
        {
            int? levelArg = args.Parsers[0].GetValue() as int?;
            
            if (!levelArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current minimum Y level: {config.MinYLevel}",
                    "Usage: /devastate miny [level] (e.g., 0, -64, 50)"
                }, "Min Y info sent to chat");
            }
            
            config.MinYLevel = levelArg.Value;
            SaveConfig();
            
            return TextCommandResult.Success($"Minimum Y level for new sources set to {config.MinYLevel}");
        }

        private TextCommandResult HandleStatusCommand(TextCommandCallingArgs args)
        {
            string statusText = isPaused ? "PAUSED" : "RUNNING";
            var lines = new List<string>
            {
                $"Devastation status: {statusText}",
                $"Speed multiplier: {config.SpeedMultiplier:F2}x",
                $"Active sources: {devastationSources.Count}/{config.MaxSources}",
                $"Tracked blocks for regen: {regrowingBlocks?.Count ?? 0}",
                $"Surface spreading: {(config.RequireSourceAirContact ? "ON" : "OFF")}",
                $"Min Y level: {config.MinYLevel}",
                $"Child spawn delay: {config.ChildSpawnDelaySeconds}s",
                $"Max failed attempts: {config.MaxFailedSpawnAttempts}"
            };
            return SendChatLines(args, lines, "Status sent to chat (scrollable)");
        }

        private TextCommandResult HandleChunkCommand(TextCommandCallingArgs args)
        {
            string action = args.Parsers[0].GetValue() as string;
            string value = args.Parsers[1].GetValue() as string;

            // Handle configuration commands first (don't require looking at a block)
            if (action == "spawn")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return SendChatLines(args, new[]
                    {
                        "=== Mob Spawn Settings ===",
                        $"Spawn interval: {config.ChunkSpawnIntervalMinHours:F2}-{config.ChunkSpawnIntervalMaxHours:F2} game hours (random)",
                        $"Cooldown after spawn: {config.ChunkSpawnCooldownHours:F2} game hours",
                        $"Distance from player: {config.ChunkSpawnMinDistance}-{config.ChunkSpawnMaxDistance} blocks",
                        $"Max mobs per chunk: {config.ChunkSpawnMaxMobsPerChunk}",
                        "",
                        "Subcommands:",
                        "  /dv chunk spawn interval [min] [max] - Set random interval range (hours)",
                        "  /dv chunk spawn cooldown [hours] - Set cooldown after spawn",
                        "  /dv chunk spawn distance [min] [max] - Set spawn distance from player",
                        "  /dv chunk spawn maxmobs [count] - Set max mobs per chunk",
                        "  /dv chunk spawn reset - Reset mob counts in all chunks"
                    }, "Spawn settings sent to chat");
                }

                // Parse subcommand
                string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string subCmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";

                if (subCmd == "interval")
                {
                    if (parts.Length < 3)
                    {
                        return SendChatLines(args, new[]
                        {
                            $"Current interval: {config.ChunkSpawnIntervalMinHours:F2}-{config.ChunkSpawnIntervalMaxHours:F2} game hours",
                            "Usage: /dv chunk spawn interval [min] [max]",
                            "Example: /dv chunk spawn interval 0.5 1.0 (30-60 in-game minutes)"
                        }, "Interval info sent to chat");
                    }

                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double minHours) ||
                        !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double maxHours))
                    {
                        return TextCommandResult.Error("Invalid numbers. Usage: /dv chunk spawn interval [min] [max]");
                    }

                    if (minHours > maxHours)
                    {
                        (minHours, maxHours) = (maxHours, minHours); // Swap if reversed
                    }

                    config.ChunkSpawnIntervalMinHours = Math.Clamp(minHours, 0.01, 100.0);
                    config.ChunkSpawnIntervalMaxHours = Math.Clamp(maxHours, 0.01, 100.0);
                    SaveConfig();
                    return TextCommandResult.Success($"Spawn interval set to {config.ChunkSpawnIntervalMinHours:F2}-{config.ChunkSpawnIntervalMaxHours:F2} game hours");
                }
                else if (subCmd == "cooldown")
                {
                    if (parts.Length < 2)
                    {
                        return SendChatLines(args, new[]
                        {
                            $"Current cooldown: {config.ChunkSpawnCooldownHours:F2} game hours",
                            "Usage: /dv chunk spawn cooldown [hours]",
                            "Example: /dv chunk spawn cooldown 4 (4 hour minimum between spawns)"
                        }, "Cooldown info sent to chat");
                    }

                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double cooldown))
                    {
                        return TextCommandResult.Error("Invalid number. Usage: /dv chunk spawn cooldown [hours]");
                    }

                    config.ChunkSpawnCooldownHours = Math.Clamp(cooldown, 0.0, 100.0);
                    SaveConfig();
                    return TextCommandResult.Success($"Spawn cooldown set to {config.ChunkSpawnCooldownHours:F2} game hours");
                }
                else if (subCmd == "distance")
                {
                    if (parts.Length < 3)
                    {
                        return SendChatLines(args, new[]
                        {
                            $"Current distance: {config.ChunkSpawnMinDistance}-{config.ChunkSpawnMaxDistance} blocks from player",
                            "Usage: /dv chunk spawn distance [min] [max]",
                            "Example: /dv chunk spawn distance 16 48"
                        }, "Distance info sent to chat");
                    }

                    if (!int.TryParse(parts[1], out int minDist) || !int.TryParse(parts[2], out int maxDist))
                    {
                        return TextCommandResult.Error("Invalid numbers. Usage: /dv chunk spawn distance [min] [max]");
                    }

                    if (minDist > maxDist)
                    {
                        (minDist, maxDist) = (maxDist, minDist); // Swap if reversed
                    }

                    config.ChunkSpawnMinDistance = Math.Clamp(minDist, 1, 256);
                    config.ChunkSpawnMaxDistance = Math.Clamp(maxDist, 1, 256);
                    SaveConfig();
                    return TextCommandResult.Success($"Spawn distance set to {config.ChunkSpawnMinDistance}-{config.ChunkSpawnMaxDistance} blocks from player");
                }
                else if (subCmd == "maxmobs")
                {
                    if (parts.Length < 2)
                    {
                        return SendChatLines(args, new[]
                        {
                            $"Current max mobs per chunk: {config.ChunkSpawnMaxMobsPerChunk}",
                            "Usage: /dv chunk spawn maxmobs [count]",
                            "Example: /dv chunk spawn maxmobs 5"
                        }, "Max mobs info sent to chat");
                    }

                    if (!int.TryParse(parts[1], out int maxMobs))
                    {
                        return TextCommandResult.Error("Invalid number. Usage: /dv chunk spawn maxmobs [count]");
                    }

                    config.ChunkSpawnMaxMobsPerChunk = Math.Clamp(maxMobs, 0, 100);
                    SaveConfig();
                    return TextCommandResult.Success($"Max mobs per chunk set to {config.ChunkSpawnMaxMobsPerChunk}");
                }
                else if (subCmd == "reset")
                {
                    int resetCount = 0;
                    foreach (var chunk in devastatedChunks.Values)
                    {
                        if (chunk.MobsSpawned > 0)
                        {
                            chunk.MobsSpawned = 0;
                            chunk.NextSpawnTime = 0;
                            resetCount++;
                        }
                    }
                    return TextCommandResult.Success($"Reset mob counts in {resetCount} chunks. Spawning can resume.");
                }
                else
                {
                    return TextCommandResult.Error("Unknown spawn subcommand. Use: interval, cooldown, distance, maxmobs, reset");
                }
            }
            else if (action == "drain")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current stability drain rate: {config.ChunkStabilityDrainRate:F4} per 500ms tick",
                        $"(~{config.ChunkStabilityDrainRate * 2 * 100:F2}% per second)",
                        "Usage: /dv chunk drain [rate] (e.g., 0.001 for ~0.2%/sec, 0.01 for ~2%/sec)"
                    }, "Drain rate info sent to chat");
                }

                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double rate))
                {
                    return TextCommandResult.Error("Invalid number. Usage: /dv chunk drain [rate]");
                }

                config.ChunkStabilityDrainRate = Math.Clamp(rate, 0.0, 1.0);
                SaveConfig();
                return TextCommandResult.Success($"Chunk stability drain rate set to {config.ChunkStabilityDrainRate:F4} (~{config.ChunkStabilityDrainRate * 2 * 100:F2}%/sec)");
            }
            else if (action == "spread")
            {
                if (string.IsNullOrEmpty(value))
                {
                    string status = config.ChunkSpreadEnabled ? "ON" : "OFF";
                    return SendChatLines(args, new[]
                    {
                        $"Chunk spread: {status}",
                        $"Spread chance: {config.ChunkSpreadChance * 100:F1}% every {config.ChunkSpreadIntervalSeconds:F0}s (at 1x speed)",
                        "Usage: /dv chunk spread [on|off]"
                    }, "Spread setting sent to chat");
                }

                if (value.Equals("on", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    config.ChunkSpreadEnabled = true;
                    SaveConfig();
                    return TextCommandResult.Success("Chunk spreading ENABLED - devastated chunks can spread to neighbors");
                }
                else if (value.Equals("off", StringComparison.OrdinalIgnoreCase) || value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    config.ChunkSpreadEnabled = false;
                    SaveConfig();
                    return TextCommandResult.Success("Chunk spreading DISABLED - devastated chunks will not spread");
                }
                else
                {
                    return TextCommandResult.Error("Invalid value. Use: on, off, 1, 0, true, or false");
                }
            }
            else if (action == "spreadchance")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current spread chance: {config.ChunkSpreadChance * 100:F1}%",
                        $"Check interval: {config.ChunkSpreadIntervalSeconds:F0}s (at 1x speed)",
                        "Usage: /dv chunk spreadchance [percent] (e.g., 5 for 5%)"
                    }, "Spread chance info sent to chat");
                }

                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                {
                    return TextCommandResult.Error("Invalid number. Usage: /dv chunk spreadchance [percent]");
                }

                config.ChunkSpreadChance = Math.Clamp(percent / 100.0, 0.0, 1.0);
                SaveConfig();
                return TextCommandResult.Success($"Chunk spread chance set to {config.ChunkSpreadChance * 100:F1}%");
            }
            else if (action == "list")
            {
                if (devastatedChunks.Count == 0)
                {
                    return TextCommandResult.Success("No devastated chunks");
                }

                var lines = new List<string> { $"Devastated chunks ({devastatedChunks.Count}):" };
                foreach (var chunk in devastatedChunks.Values.Take(20))
                {
                    string status = chunk.IsFullyDevastated ? "fully devastated" : $"{chunk.DevastationLevel:P0} devastated";
                    int frontierCount = chunk.DevastationFrontier?.Count ?? 0;
                    lines.Add($"  ({chunk.ChunkX}, {chunk.ChunkZ}): {status}, {chunk.BlocksDevastated} blocks, frontier: {frontierCount}");
                }
                if (devastatedChunks.Count > 20)
                {
                    lines.Add($"  ... and {devastatedChunks.Count - 20} more");
                }
                return SendChatLines(args, lines, "Chunk list sent to chat");
            }
            else if (action == "perf" || action == "performance")
            {
                var stats = GetPerformanceStats();
                int activeChunks = devastatedChunks.Values.Count(c => !c.IsFullyDevastated);
                int stuckChunks = devastatedChunks.Values.Count(c => !c.IsFullyDevastated && c.FrontierInitialized && (c.DevastationFrontier == null || c.DevastationFrontier.Count == 0) && c.BlocksDevastated < 1000);

                var lines = new List<string>
                {
                    "=== Chunk Devastation Performance ===",
                    $"Avg processing time: {stats.avgTime:F2}ms per tick",
                    $"Peak processing time: {stats.peakTime:F2}ms",
                    $"Avg tick interval: {stats.avgDt:F0}ms (expected: 500ms)",
                    "",
                    $"Total chunks: {devastatedChunks.Count}",
                    $"Active chunks: {activeChunks}",
                    $"Fully devastated: {devastatedChunks.Count - activeChunks}",
                    $"Stuck chunks detected: {stuckChunks}",
                    $"Chunks queued for repair: {chunksNeedingRepair.Count}",
                    "",
                    $"Total ticks processed: {totalTicksProcessed}",
                    $"Total processing time: {totalProcessingTimeMs / 1000.0:F1}s"
                };

                return SendChatLines(args, lines, "Performance stats sent to chat");
            }
            else if (action == "repair")
            {
                // Force repair all stuck chunks
                int stuckCount = 0;
                foreach (var chunk in devastatedChunks.Values)
                {
                    if (!chunk.IsFullyDevastated &&
                        (chunk.DevastationFrontier == null || chunk.DevastationFrontier.Count == 0))
                    {
                        long chunkKey = chunk.ChunkKey;
                        if (!chunksNeedingRepair.Contains(chunkKey))
                        {
                            chunksNeedingRepair.Enqueue(chunkKey);
                            stuckCount++;
                        }
                    }
                }

                if (stuckCount > 0)
                {
                    return TextCommandResult.Success($"Queued {stuckCount} stuck chunks for repair");
                }
                else
                {
                    return TextCommandResult.Success("No stuck chunks found");
                }
            }
            else if (action == "analyze")
            {
                // Analyze a specific chunk the player is looking at
                IServerPlayer player = args.Caller.Player as IServerPlayer;
                if (player == null) return TextCommandResult.Error("This command must be run by a player");

                BlockSelection blockSel = player.CurrentBlockSelection;
                if (blockSel == null)
                {
                    return TextCommandResult.Error("Look at a block to analyze its chunk");
                }

                BlockPos pos = blockSel.Position;
                int chunkX = pos.X / CHUNK_SIZE;
                int chunkZ = pos.Z / CHUNK_SIZE;
                long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

                if (!devastatedChunks.TryGetValue(chunkKey, out var chunk))
                {
                    return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is not marked as devastated");
                }

                // Gather detailed diagnostic info
                var lines = new List<string>
                {
                    $"=== Chunk Analysis: ({chunkX}, {chunkZ}) ===",
                    $"Chunk key: {chunkKey}",
                    $"Blocks devastated: {chunk.BlocksDevastated}",
                    $"Devastation level: {chunk.DevastationLevel:P1}",
                    $"IsFullyDevastated: {chunk.IsFullyDevastated}",
                    $"IsUnrepairable: {chunk.IsUnrepairable}",
                    $"FrontierInitialized: {chunk.FrontierInitialized}",
                    $"Frontier count: {chunk.DevastationFrontier?.Count ?? 0}",
                    $"Bleed frontier count: {chunk.BleedFrontier?.Count ?? 0}",
                    $"FillInTickCounter: {chunk.FillInTickCounter}",
                    $"ConsecutiveEmptyFrontierChecks: {chunk.ConsecutiveEmptyFrontierChecks}",
                    $"RepairAttemptCount: {chunk.RepairAttemptCount}/5",
                    $"Marked time: {chunk.MarkedTime:F2} hours",
                    ""
                };

                // Check frontier blocks validity
                if (chunk.DevastationFrontier != null && chunk.DevastationFrontier.Count > 0)
                {
                    lines.Add($"First 5 frontier blocks:");
                    int shown = 0;
                    foreach (var frontierPos in chunk.DevastationFrontier.Take(5))
                    {
                        Block block = sapi.World.BlockAccessor.GetBlock(frontierPos);
                        string blockName = block?.Code?.ToString() ?? "null";
                        bool isDevastated = block != null && IsAlreadyDevastated(block);
                        lines.Add($"  {frontierPos}: {blockName} (devastated: {isDevastated})");
                        shown++;
                    }
                }
                else
                {
                    lines.Add("Frontier is EMPTY - this chunk cannot spread!");
                }

                // Scan chunk for actual devastated blocks
                int startX = chunkX * CHUNK_SIZE;
                int startZ = chunkZ * CHUNK_SIZE;
                int devastatedBlocksFound = 0;
                int convertibleBlocksFound = 0;
                BlockPos sampleDevastatedPos = null;

                for (int dx = 0; dx < CHUNK_SIZE && devastatedBlocksFound < 100; dx += 4)
                {
                    for (int dz = 0; dz < CHUNK_SIZE && devastatedBlocksFound < 100; dz += 4)
                    {
                        int x = startX + dx;
                        int z = startZ + dz;
                        int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
                        if (surfaceY <= 0) continue;

                        for (int yOff = -3; yOff <= 3; yOff++)
                        {
                            BlockPos checkPos = new BlockPos(x, surfaceY + yOff, z);
                            Block block = sapi.World.BlockAccessor.GetBlock(checkPos);
                            if (block == null || block.Id == 0) continue;

                            if (IsAlreadyDevastated(block))
                            {
                                devastatedBlocksFound++;
                                if (sampleDevastatedPos == null) sampleDevastatedPos = checkPos.Copy();
                            }
                            else if (TryGetDevastatedForm(block, out _, out _))
                            {
                                convertibleBlocksFound++;
                            }
                        }
                    }
                }

                lines.Add("");
                lines.Add($"Chunk scan (sampled every 4 blocks):");
                lines.Add($"  Devastated blocks found: {devastatedBlocksFound}");
                lines.Add($"  Convertible blocks found: {convertibleBlocksFound}");
                if (sampleDevastatedPos != null)
                {
                    lines.Add($"  Sample devastated at: {sampleDevastatedPos}");
                }

                // Diagnose the problem
                lines.Add("");
                lines.Add("Diagnosis:");
                if (chunk.IsFullyDevastated)
                {
                    lines.Add("  Chunk is marked fully devastated - no more spreading needed");
                }
                else if (chunk.DevastationFrontier == null || chunk.DevastationFrontier.Count == 0)
                {
                    if (devastatedBlocksFound == 0)
                    {
                        lines.Add("  PROBLEM: No frontier AND no devastated blocks found!");
                        lines.Add("  This chunk was likely created but never initialized properly.");
                    }
                    else if (convertibleBlocksFound == 0)
                    {
                        lines.Add("  Frontier empty but no convertible blocks - chunk may be done");
                    }
                    else
                    {
                        lines.Add("  PROBLEM: Has devastated blocks but empty frontier!");
                        lines.Add("  Use '/dv chunk repair' to fix, or '/dv chunk fix' on this chunk");
                    }
                }
                else
                {
                    lines.Add("  Chunk appears healthy - frontier has blocks to spread from");
                }

                // Check if queued for repair
                if (chunksNeedingRepair.Contains(chunkKey))
                {
                    lines.Add("  Note: This chunk is queued for repair");
                }

                return SendChatLines(args, lines, "Chunk analysis sent to chat");
            }
            else if (action == "fix")
            {
                // Force fix the specific chunk the player is looking at
                IServerPlayer player = args.Caller.Player as IServerPlayer;
                if (player == null) return TextCommandResult.Error("This command must be run by a player");

                BlockSelection blockSel = player.CurrentBlockSelection;
                if (blockSel == null)
                {
                    return TextCommandResult.Error("Look at a block to fix its chunk");
                }

                BlockPos pos = blockSel.Position;
                int chunkX = pos.X / CHUNK_SIZE;
                int chunkZ = pos.Z / CHUNK_SIZE;
                long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

                if (!devastatedChunks.TryGetValue(chunkKey, out var chunk))
                {
                    return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is not marked as devastated");
                }

                // Force re-initialize and reset repair state
                int oldFrontierCount = chunk.DevastationFrontier?.Count ?? 0;
                chunk.FrontierInitialized = false;
                chunk.IsFullyDevastated = false;
                chunk.IsUnrepairable = false; // Reset unrepairable flag
                chunk.RepairAttemptCount = 0;
                chunk.ConsecutiveEmptyFrontierChecks = 0;
                InitializeChunkFrontier(chunk);

                int newFrontierCount = chunk.DevastationFrontier?.Count ?? 0;
                return TextCommandResult.Success($"Fixed chunk ({chunkX}, {chunkZ}): frontier {oldFrontierCount} -> {newFrontierCount} blocks (repair state reset)");
            }
            else if (action == "unrepairable")
            {
                // List or manage unrepairable chunks
                if (value == "list" || string.IsNullOrEmpty(value))
                {
                    var unrepairableChunks = devastatedChunks.Values.Where(c => c.IsUnrepairable).ToList();
                    if (unrepairableChunks.Count == 0)
                    {
                        return TextCommandResult.Success("No unrepairable chunks found");
                    }

                    var lines = new List<string> { $"=== Unrepairable Chunks ({unrepairableChunks.Count}) ===" };
                    foreach (var chunk in unrepairableChunks.Take(20))
                    {
                        lines.Add($"  ({chunk.ChunkX}, {chunk.ChunkZ}): {chunk.BlocksDevastated} blocks, {chunk.RepairAttemptCount} repair attempts");
                    }
                    if (unrepairableChunks.Count > 20)
                    {
                        lines.Add($"  ... and {unrepairableChunks.Count - 20} more");
                    }
                    return SendChatLines(args, lines, "Unrepairable chunks list sent to chat");
                }
                else if (value == "clear")
                {
                    int count = 0;
                    foreach (var chunk in devastatedChunks.Values.Where(c => c.IsUnrepairable))
                    {
                        chunk.IsUnrepairable = false;
                        chunk.RepairAttemptCount = 0;
                        chunk.ConsecutiveEmptyFrontierChecks = 0;
                        count++;
                    }
                    return TextCommandResult.Success($"Reset {count} unrepairable chunks - they will be retried");
                }
                else if (value == "remove")
                {
                    int count = devastatedChunks.Values.Count(c => c.IsUnrepairable);
                    var keysToRemove = devastatedChunks.Where(kvp => kvp.Value.IsUnrepairable).Select(kvp => kvp.Key).ToList();
                    foreach (var key in keysToRemove)
                    {
                        devastatedChunks.Remove(key);
                    }
                    return TextCommandResult.Success($"Removed {count} unrepairable chunks from tracking");
                }
                else
                {
                    return TextCommandResult.Error("Usage: /dv chunk unrepairable [list|clear|remove]");
                }
            }
            else if (action == "remove")
            {
                // Remove requires looking at a block OR "all" value
                if (value == "all")
                {
                    int count = devastatedChunks.Count;
                    devastatedChunks.Clear();
                    return TextCommandResult.Success($"Removed all {count} devastated chunks");
                }

                IServerPlayer player = args.Caller.Player as IServerPlayer;
                if (player == null) return TextCommandResult.Error("This command must be run by a player");

                BlockSelection blockSel = player.CurrentBlockSelection;
                if (blockSel == null)
                {
                    return TextCommandResult.Error("Look at a block to remove its chunk, or use '/dv chunk remove all'");
                }

                BlockPos pos = blockSel.Position;
                int chunkX = pos.X / CHUNK_SIZE;
                int chunkZ = pos.Z / CHUNK_SIZE;
                long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

                if (devastatedChunks.ContainsKey(chunkKey))
                {
                    devastatedChunks.Remove(chunkKey);
                    return TextCommandResult.Success($"Removed devastated chunk at ({chunkX}, {chunkZ})");
                }
                else
                {
                    return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is not marked as devastated");
                }
            }
            else
            {
                // Default action: mark chunk as devastated (requires looking at a block)
                IServerPlayer player = args.Caller.Player as IServerPlayer;
                if (player == null) return TextCommandResult.Error("This command must be run by a player");

                BlockSelection blockSel = player.CurrentBlockSelection;
                if (blockSel == null)
                {
                    // No block selected - show help
                    return SendChatLines(args, new[]
                    {
                        "Chunk commands:",
                        "  /dv chunk - Mark looked-at chunk as devastated",
                        "  /dv chunk remove [all] - Remove devastation from chunk",
                        "  /dv chunk list - List all devastated chunks",
                        "  /dv chunk analyze - Detailed diagnostics for looked-at chunk",
                        "  /dv chunk fix - Force re-initialize looked-at chunk",
                        "  /dv chunk perf - Show performance stats",
                        "  /dv chunk repair - Queue all stuck chunks for repair",
                        "  /dv chunk unrepairable [list|clear|remove] - Manage unrepairable chunks",
                        "  /dv chunk spawn - Show mob spawn settings and subcommands",
                        "  /dv chunk drain [rate] - Set stability drain rate",
                        "  /dv chunk spread [on|off] - Toggle chunk spreading",
                        "  /dv chunk spreadchance [percent] - Set spread chance"
                    }, "Chunk help sent to chat");
                }

                BlockPos pos = blockSel.Position.Copy();
                int chunkX = pos.X / CHUNK_SIZE;
                int chunkZ = pos.Z / CHUNK_SIZE;
                long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

                if (devastatedChunks.ContainsKey(chunkKey))
                {
                    return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is already marked as devastated");
                }

                var newChunk = new DevastatedChunk
                {
                    ChunkX = chunkX,
                    ChunkZ = chunkZ,
                    MarkedTime = sapi.World.Calendar.TotalHours,
                    DevastationLevel = 0.0,
                    IsFullyDevastated = false,
                    FrontierInitialized = true,
                    DevastationFrontier = new List<BlockPos> { pos }
                };

                // Devastate the starting block immediately
                Block startBlock = sapi.World.BlockAccessor.GetBlock(pos);
                if (startBlock != null && startBlock.Id != 0 && !IsAlreadyDevastated(startBlock))
                {
                    if (TryGetDevastatedForm(startBlock, out string devastatedBlock, out string regeneratesTo))
                    {
                        Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedBlock));
                        if (newBlock != null)
                        {
                            sapi.World.BlockAccessor.SetBlock(newBlock.Id, pos);
                            regrowingBlocks.Add(new RegrowingBlocks
                            {
                                Pos = pos,
                                Out = regeneratesTo,
                                LastTime = sapi.World.Calendar.TotalHours
                            });
                            newChunk.BlocksDevastated++;
                        }
                    }
                }

                devastatedChunks[chunkKey] = newChunk;
                return TextCommandResult.Success($"Marked chunk at ({chunkX}, {chunkZ}) as devastated starting from {pos}. Devastation will spread in cardinal directions.");
            }
        }

        #endregion

        private string GenerateSourceId()
        {
            return (nextSourceId++).ToString();
        }

        private void SpreadDevastationFromRifts(float dt)
        {
            // Skip all processing if paused
            if (isPaused) return;
            
            // Safety check
            if (sapi == null || devastationSources == null) return;
            
            try
            {
                // Spread from manual devastation sources
                List<DevastationSource> toRemove = new List<DevastationSource>();
                double currentGameTime = sapi.World.Calendar.TotalHours;
                
                foreach (DevastationSource source in devastationSources)
                {
                    // Check if the block still exists
                    Block block = sapi.World.BlockAccessor.GetBlock(source.Pos);
                    if (block == null || block.Id == 0)
                    {
                        // Block was removed, remove from sources
                        toRemove.Add(source);
                        continue;
                    }

                    // Debug marker: color-coded particles at source position
                    SpawnSourceMarkerParticles(source);
                    
                    // Spread or heal the specified amount of blocks per tick
                    int processed = source.IsHealing 
                        ? HealDevastationAroundPosition(source.Pos.ToVec3d(), source)
                        : SpreadDevastationAroundPosition(source.Pos.ToVec3d(), source);
                    
                    // Track success rate and adjust radius
                    int effectiveAmount = Math.Max(1, (int)(source.Amount * config.SpeedMultiplier));
                    source.SuccessfulAttempts += processed;
                    source.TotalAttempts += effectiveAmount * 5; // We try up to 5 times per block
                    
                    // Check every 100 attempts if we should expand
                    if (source.TotalAttempts >= 100)
                    {
                        double successRate = (double)source.SuccessfulAttempts / source.TotalAttempts;
                        
                        // If success rate is below threshold, expand the search radius
                        if (successRate < config.LowSuccessThreshold && source.CurrentRadius < source.Range)
                        {
                            // Expand faster if really low success rate
                            double expansion = successRate < (config.LowSuccessThreshold / 2) ? 4.0 : 2.0;
                            source.CurrentRadius = Math.Min(source.CurrentRadius + expansion, source.Range);
                            source.StallCounter = 0; // Reset stall counter when still expanding
                        }
                        // Track stalling: at max radius with very low success rate
                        else if (successRate < config.VeryLowSuccessThreshold && source.CurrentRadius >= source.Range && !source.IsHealing)
                        {
                            source.StallCounter++;
                            
                            // After 10 stall cycles (plenty of time to try), take action to keep spreading
                            if (source.StallCounter >= 10)
                            {
                                // Always try to spawn metastasis when stalled - this moves the frontier forward
                                bool spawned = TrySpawnSingleChild(source, currentGameTime);
                                
                                if (spawned)
                                {
                                    source.StallCounter = 0;
                                }
                                else
                                {
                                    source.FailedSpawnAttempts++;
                                    
                                    if (source.FailedSpawnAttempts >= config.MaxFailedSpawnAttempts)
                                    {
                                        // Tried many times and couldn't find any viable land
                                        source.IsSaturated = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Good success rate, reset stall counter
                            source.StallCounter = 0;
                        }
                        
                        // Reset attempt counters
                        source.SuccessfulAttempts = 0;
                        source.TotalAttempts = 0;
                    }
                    
                    // Check for metastasis spawning (only for non-healing sources)
                    if (!source.IsHealing && !source.IsSaturated)
                    {
                        // Spawn metastasis when we've devastated enough blocks AND radius has reached max
                        if (source.BlocksSinceLastMetastasis >= source.MetastasisThreshold && 
                            source.CurrentRadius >= source.Range)
                        {
                            // Check actual saturation in the area
                            double saturation = CalculateLocalDevastationPercent(source.Pos.ToVec3d(), source.CurrentRadius);
                            
                            if (saturation >= config.SaturationThreshold)
                            {
                                TrySpawnSingleChild(source, currentGameTime);
                            }
                        }
                    }
                }
            
                // Clean up removed sources
                foreach (var source in toRemove)
                {
                    devastationSources.Remove(source);
                }
            
                // Periodic cleanup: remove saturated sources to free slots for active spreading
                // Run every ~500 ticks (5 seconds at 10ms tick rate)
                cleanupTickCounter++;
                if (cleanupTickCounter >= 500)
                {
                    cleanupTickCounter = 0;
                    CleanupSaturatedSources();
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash the server
                sapi.Logger.Error("SpreadingDevastation: Error in SpreadDevastationFromRifts: " + ex.Message);
            }
        }
        
        /// <summary>
        /// Periodically removes saturated sources that have no active children nearby.
        /// This frees up slots for the spreading frontier to continue advancing.
        /// </summary>
        private void CleanupSaturatedSources()
        {
            if (devastationSources == null || devastationSources.Count == 0) return;
            
            // Only cleanup if we're using more than half of the source cap
            // This ensures we have room for new metastasis
            if (devastationSources.Count < config.MaxSources / 2) return;
            
            // Find saturated, non-protected sources that can be removed
            var saturatedSources = devastationSources
                .Where(s => s.IsSaturated && !s.IsProtected && !s.IsHealing)
                .ToList();
            
            if (saturatedSources.Count == 0) return;
            
            // Remove up to 1/4 of saturated sources each cleanup cycle
            int toRemove = Math.Max(1, saturatedSources.Count / 4);
            
            // Prioritize removing sources with highest generation level (furthest from origin)
            var sourcesToRemove = saturatedSources
                .OrderByDescending(s => s.GenerationLevel)
                .ThenByDescending(s => s.BlocksDevastatedTotal)
                .Take(toRemove)
                .ToList();
            
            foreach (var source in sourcesToRemove)
            {
                devastationSources.Remove(source);
            }
        }

        private int SpreadDevastationAroundPosition(Vec3d position, DevastationSource source)
        {
            // Skip if this source is fully saturated
            if (source.IsSaturated) return 0;
            
            // Apply speed multiplier to effective amount
            int effectiveAmount = Math.Max(1, (int)(source.Amount * config.SpeedMultiplier));
            
            int devastatedCount = 0;
            int maxAttempts = effectiveAmount * 5; // Try up to 5 times per block we want to devastate
            
            for (int attempt = 0; attempt < maxAttempts && devastatedCount < effectiveAmount; attempt++)
            {
                // Generate distance-weighted random offset
                // Weight towards closer blocks for natural outward spreading
                double distance = GenerateWeightedDistance(source.CurrentRadius);
                
                // Convert distance to actual offset with random direction
                double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                double angleY = (RandomNumberGenerator.GetInt32(180) - 90) * Math.PI / 180.0;
                
                int offsetX = (int)(distance * Math.Cos(angle) * Math.Cos(angleY));
                int offsetY = (int)(distance * Math.Sin(angleY));
                int offsetZ = (int)(distance * Math.Sin(angle) * Math.Cos(angleY));

                // Calculate the target position
                BlockPos targetPos = new BlockPos(
                    (int)position.X + offsetX,
                    (int)position.Y + offsetY,
                    (int)position.Z + offsetZ
                );

                // Get the block at this position
                Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                
                if (block.Id == 0) continue; // Skip air blocks, try again

                // Check if already devastated
                if (IsAlreadyDevastated(block))
                {
                    continue; // Already devastated, try again
                }

                // Check if protected by rift ward
                if (IsBlockProtectedByRiftWard(targetPos))
                {
                    continue; // Protected by rift ward, try again
                }

                string devastatedBlock = "";
                string regeneratesTo = "";

                if (TryGetDevastatedForm(block, out devastatedBlock, out regeneratesTo))
                {
                    Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedBlock));
                    sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);

                    // Track this block for regeneration
                    regrowingBlocks.Add(new RegrowingBlocks
                    {
                        Pos = targetPos,
                        Out = regeneratesTo,
                        LastTime = sapi.World.Calendar.TotalHours
                    });

                    devastatedCount++;

                    // Track for metastasis system
                    source.BlocksDevastatedTotal++;
                    source.BlocksSinceLastMetastasis++;
                }
            }

            return devastatedCount;
        }

        private double GenerateWeightedDistance(double maxDistance)
        {
            // Generate distance with bias toward closer blocks
            // Uses inverse square weighting: closer blocks much more likely
            // Formula: distance = maxDistance * (1 - sqrt(random))
            // This gives exponential bias toward center
            
            double random = RandomNumberGenerator.GetInt32(10000) / 10000.0; // 0.0 to 1.0
            double weighted = 1.0 - Math.Sqrt(random); // Bias toward 0
            return maxDistance * weighted;
        }

        private int HealDevastationAroundPosition(Vec3d position, DevastationSource source)
        {
            // Apply speed multiplier to effective amount
            int effectiveAmount = Math.Max(1, (int)(source.Amount * config.SpeedMultiplier));
            
            int healedCount = 0;
            int maxAttempts = effectiveAmount * 5;
            
            for (int attempt = 0; attempt < maxAttempts && healedCount < effectiveAmount; attempt++)
            {
                // Generate distance-weighted random offset (same as devastation)
                double distance = GenerateWeightedDistance(source.CurrentRadius);
                
                double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                double angleY = (RandomNumberGenerator.GetInt32(180) - 90) * Math.PI / 180.0;
                
                int offsetX = (int)(distance * Math.Cos(angle) * Math.Cos(angleY));
                int offsetY = (int)(distance * Math.Sin(angleY));
                int offsetZ = (int)(distance * Math.Sin(angle) * Math.Cos(angleY));

                BlockPos targetPos = new BlockPos(
                    (int)position.X + offsetX,
                    (int)position.Y + offsetY,
                    (int)position.Z + offsetZ
                );

                Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                
                if (block.Id == 0) continue;

                // Check if this is a devastated block
                if (!IsAlreadyDevastated(block))
                {
                    continue; // Not devastated, try again
                }

                // Heal the block
                string healedBlock = "";
                
                if (TryGetHealedForm(block, out healedBlock))
                {
                    if (healedBlock == "none")
                    {
                        // Remove the block (convert to air)
                        sapi.World.BlockAccessor.SetBlock(0, targetPos);
                    }
                    else
                    {
                        Block newBlock = sapi.World.GetBlock(new AssetLocation("game", healedBlock));
                        if (newBlock != null)
                        {
                            sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
                        }
                    }

                    // Note: We don't remove from regrowingBlocks here to avoid O(n) overhead
                    // Healed blocks will simply be skipped when regeneration runs

                    healedCount++;
                }
            }

            return healedCount;
        }

        private bool TryGetHealedForm(Block block, out string healedBlock)
        {
            healedBlock = "";
            string path = block.Code.Path;
            
            if (path.StartsWith("devastatedsoil-0"))
            {
                healedBlock = "soil-verylow-none";
            }
            else if (path.StartsWith("devastatedsoil-1"))
            {
                healedBlock = "sludgygravel";
            }
            else if (path.StartsWith("devastatedsoil-2"))
            {
                healedBlock = "sludgygravel";
            }
            else if (path.StartsWith("devastatedsoil-3"))
            {
                healedBlock = "log-grown-aged-ud";
            }
            else if (path.StartsWith("drock"))
            {
                healedBlock = "rock-obsidian";
            }
            else if (path.StartsWith("devastationgrowth-") || 
                     path.StartsWith("devgrowth-shrike") || 
                     path.StartsWith("devgrowth-shard") || 
                     path.StartsWith("devgrowth-bush"))
            {
                healedBlock = "none"; // Remove these growths
            }
            else if (path.StartsWith("devgrowth-thorns"))
            {
                healedBlock = "leavesbranchy-grown-oak";
            }

            return healedBlock != "";
        }

        private bool IsAlreadyDevastated(Block block)
        {
            if (block == null || block.Code == null) return false;
            
            string path = block.Code.Path;
            return path.StartsWith("devastatedsoil-") ||
                   path.StartsWith("drock") ||
                   path.StartsWith("devastationgrowth-") ||
                   path.StartsWith("devgrowth-");
        }

        /// <summary>
        /// Calculates what percentage of blocks in a spherical area are devastated.
        /// Returns a value between 0.0 and 1.0
        /// </summary>
        private double CalculateLocalDevastationPercent(Vec3d position, double radius)
        {
            int devastatedCount = 0;
            int totalConvertibleCount = 0;
            int sampleRadius = (int)Math.Ceiling(radius);
            
            // Sample blocks in a sphere around the position
            for (int x = -sampleRadius; x <= sampleRadius; x++)
            {
                for (int y = -sampleRadius; y <= sampleRadius; y++)
                {
                    for (int z = -sampleRadius; z <= sampleRadius; z++)
                    {
                        // Check if within sphere
                        double dist = Math.Sqrt(x * x + y * y + z * z);
                        if (dist > radius) continue;
                        
                        BlockPos targetPos = new BlockPos(
                            (int)position.X + x,
                            (int)position.Y + y,
                            (int)position.Z + z
                        );
                        
                        Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                        if (block == null || block.Id == 0) continue; // Skip air
                        
                        // Check if this block is devastated
                        if (IsAlreadyDevastated(block))
                        {
                            devastatedCount++;
                            totalConvertibleCount++;
                        }
                        // Check if this block CAN be devastated
                        else if (TryGetDevastatedForm(block, out _, out _))
                        {
                            totalConvertibleCount++;
                        }
                    }
                }
            }
            
            if (totalConvertibleCount == 0) return 1.0; // Nothing to devastate = fully saturated
            return (double)devastatedCount / totalConvertibleCount;
        }

        /// <summary>
        /// Checks if a position is adjacent to at least one air block.
        /// </summary>
        private bool IsAdjacentToAir(BlockPos pos)
        {
            BlockPos[] neighbors = new BlockPos[]
            {
                new BlockPos(pos.X + 1, pos.Y, pos.Z),
                new BlockPos(pos.X - 1, pos.Y, pos.Z),
                new BlockPos(pos.X, pos.Y + 1, pos.Z),
                new BlockPos(pos.X, pos.Y - 1, pos.Z),
                new BlockPos(pos.X, pos.Y, pos.Z + 1),
                new BlockPos(pos.X, pos.Y, pos.Z - 1)
            };
            
            foreach (var neighbor in neighbors)
            {
                Block block = sapi.World.BlockAccessor.GetBlock(neighbor);
                if (block != null && block.Id == 0) // Air block
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Tries to spawn a single child source with delay enforcement.
        /// Returns true if a child was spawned.
        /// </summary>
        private bool TrySpawnSingleChild(DevastationSource parentSource, double currentGameTime)
        {
            // Don't spawn from healing sources
            if (parentSource.IsHealing) return false;
            
            // Enforce spawn delay (affected by speed multiplier)
            double effectiveDelay = config.ChildSpawnDelaySeconds / Math.Max(0.1, config.SpeedMultiplier);
            double delayInHours = effectiveDelay / 3600.0; // Convert seconds to hours
            
            if (parentSource.LastChildSpawnTime > 0)
            {
                double timeSinceLastSpawn = currentGameTime - parentSource.LastChildSpawnTime;
                if (timeSinceLastSpawn < delayInHours)
                {
                    return false; // Still on cooldown
                }
            }
            
            // Check if at or near cap
            if (devastationSources.Count >= config.MaxSources)
            {
                RemoveOldestSources(1);
            }
            
            if (devastationSources.Count >= config.MaxSources)
            {
                return false; // Still can't spawn after cleanup
            }
            
            // Find a position for the new source using pillar strategy
            BlockPos spawnPos = FindMetastasisPositionPillar(parentSource);
            
            if (spawnPos == null)
            {
                // Pillar strategy failed, try long-range search
                var longRangePositions = FindLongRangeMetastasisPositions(parentSource, 1);
                if (longRangePositions.Count > 0)
                {
                    spawnPos = longRangePositions[0];
                }
            }
            
            if (spawnPos == null)
            {
                return false; // No viable position found
            }
            
            // Check min Y level
            if (spawnPos.Y < config.MinYLevel)
            {
                return false;
            }
            
            // Ensure parent has a source ID for tracking
            if (string.IsNullOrEmpty(parentSource.SourceId))
            {
                parentSource.SourceId = GenerateSourceId();
            }
            
            // Calculate child range with variation
            int childRange = CalculateChildRange(parentSource.Range);
            
            // Create the new metastasis source
            DevastationSource metastasis = new DevastationSource
            {
                Pos = spawnPos.Copy(),
                Range = childRange,
                Amount = parentSource.Amount,
                CurrentRadius = 3.0, // Start small
                IsHealing = false,
                IsMetastasis = true,
                GenerationLevel = parentSource.GenerationLevel + 1,
                MetastasisThreshold = config.MetastasisThreshold,
                MaxGenerationLevel = parentSource.MaxGenerationLevel,
                SourceId = GenerateSourceId(),
                ParentSourceId = parentSource.SourceId
            };
            
            devastationSources.Add(metastasis);
            
            // Update parent
            parentSource.ChildrenSpawned++;
            parentSource.LastChildSpawnTime = currentGameTime;
            parentSource.BlocksSinceLastMetastasis = 0;
            parentSource.FailedSpawnAttempts = 0; // Reset on successful spawn
            
            // Mark parent as saturated after spawning enough children
            // This ensures each source contributes to spreading before being replaced
            if (parentSource.ChildrenSpawned >= 3)
            {
                parentSource.IsSaturated = true;
            }
            
            return true;
        }

        /// <summary>
        /// Calculates child range with variation (50-150% of parent by default).
        /// </summary>
        private int CalculateChildRange(int parentRange)
        {
            double variation = config.MetastasisRadiusVariation;
            double minMultiplier = 1.0 - variation;
            double maxMultiplier = 1.0 + variation;
            
            // Generate random multiplier between min and max
            double multiplier = minMultiplier + (RandomNumberGenerator.GetInt32(1001) / 1000.0) * (maxMultiplier - minMultiplier);
            
            int childRange = (int)Math.Round(parentRange * multiplier);
            return Math.Clamp(childRange, 3, 128); // Ensure reasonable bounds
        }

        /// <summary>
        /// Uses pillar strategy to find metastasis spawn positions.
        /// Searches at the same Y level as parent, checking vertical pillars for valid positions.
        /// Searches BEYOND the current devastated radius to find fresh land.
        /// </summary>
        private BlockPos FindMetastasisPositionPillar(DevastationSource source)
        {
            int pillarHeight = config.PillarSearchHeight;
            int probeCount = 32;
            
            // Search beyond the current radius, but not too far
            double searchMinRadius = source.CurrentRadius * 1.2; // Start just beyond devastated area
            double searchMaxRadius = source.Range * 2; // Search up to 2x the range
            
            List<BlockPos> candidates = new List<BlockPos>();
            
            for (int i = 0; i < probeCount; i++)
            {
                // Generate random angle
                double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                
                // Generate distance between searchMinRadius and searchMaxRadius
                double distance = searchMinRadius + (RandomNumberGenerator.GetInt32(1000) / 1000.0) * (searchMaxRadius - searchMinRadius);
                
                int offsetX = (int)(distance * Math.Cos(angle));
                int offsetZ = (int)(distance * Math.Sin(angle));
                
                // Search at parent's Y level, checking a vertical pillar
                int baseY = source.Pos.Y;
                
                for (int yOffset = -pillarHeight; yOffset <= pillarHeight; yOffset++)
                {
                    BlockPos candidatePos = new BlockPos(
                        source.Pos.X + offsetX,
                        baseY + yOffset,
                        source.Pos.Z + offsetZ
                    );
                    
                    // Check min Y level
                    if (candidatePos.Y < config.MinYLevel) continue;
                    
                    // Check if position is valid
                    if (!IsValidMetastasisPosition(candidatePos)) continue;
                    
                    // Check air contact requirement
                    if (config.RequireSourceAirContact && !IsAdjacentToAir(candidatePos)) continue;
                    
                    // Score this position - how many non-devastated blocks are nearby?
                    int nonDevastatedNearby = CountNonDevastatedNearby(candidatePos, 4);
                    
                    // Only consider positions with enough non-devastated blocks
                    if (nonDevastatedNearby > 5)
                    {
                        candidates.Add(candidatePos);
                        break; // Found a good position in this pillar, move to next angle
                    }
                }
            }
            
            if (candidates.Count == 0) return null;
            
            // Pick the best candidate (most non-devastated blocks nearby)
            // Also ensure it's not too close to existing sources
            var validCandidates = candidates
                .Where(c => !IsTooCloseToExistingSources(c, source.Range * 0.5))
                .OrderByDescending(c => CountNonDevastatedNearby(c, 4))
                .ToList();
            
            return validCandidates.Count > 0 ? validCandidates[0] : null;
        }

        /// <summary>
        /// Checks if a position is valid for spawning a metastasis source.
        /// </summary>
        private bool IsValidMetastasisPosition(BlockPos pos)
        {
            Block block = sapi.World.BlockAccessor.GetBlock(pos);
            
            // Must be a solid block (not air, not water, etc.)
            if (block == null || block.Id == 0) return false;
            
            // Can be any solid block - we're not restricting what gets devastated
            // Just checking that there's something there to spread from
            return true;
        }

        /// <summary>
        /// Checks if a position is too close to existing devastation sources.
        /// </summary>
        private bool IsTooCloseToExistingSources(BlockPos pos, double minDistance)
        {
            foreach (var existingSource in devastationSources)
            {
                double dist = Math.Sqrt(
                    Math.Pow(pos.X - existingSource.Pos.X, 2) +
                    Math.Pow(pos.Y - existingSource.Pos.Y, 2) +
                    Math.Pow(pos.Z - existingSource.Pos.Z, 2)
                );
                
                if (dist < minDistance)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Counts how many non-devastated convertible blocks are near a position.
        /// </summary>
        private int CountNonDevastatedNearby(BlockPos pos, int radius)
        {
            int count = 0;
            
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos targetPos = new BlockPos(pos.X + x, pos.Y + y, pos.Z + z);
                        Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                        
                        if (block == null || block.Id == 0) continue;
                        if (IsAlreadyDevastated(block)) continue;
                        
                        if (TryGetDevastatedForm(block, out _, out _))
                        {
                            count++;
                        }
                    }
                }
            }
            
            return count;
        }

        /// <summary>
        /// Removes the oldest/most saturated sources to make room for new metastasis.
        /// Protected and healing sources are never removed.
        /// Prioritizes: saturated sources first, then by generation level (oldest first), then by blocks devastated.
        /// </summary>
        private void RemoveOldestSources(int count)
        {
            if (count <= 0 || devastationSources.Count == 0) return;
            
            // Sort sources by priority for removal:
            // 1. Saturated sources (remove first - they're done)
            // 2. Higher generation level (newer metastasis children first - keep roots alive longer)
            // 3. Higher blocks devastated (more complete)
            var sourcesToRemove = devastationSources
                .Where(s => !s.IsHealing && !s.IsProtected) // Don't remove healing or protected sources
                .OrderByDescending(s => s.IsSaturated ? 1 : 0) // Saturated first
                .ThenByDescending(s => s.GenerationLevel) // Newest generation first (remove children before parents)
                .ThenByDescending(s => s.BlocksDevastatedTotal) // Most complete first
                .Take(count)
                .ToList();
            
            foreach (var source in sourcesToRemove)
            {
                devastationSources.Remove(source);
            }
        }
        
        /// <summary>
        /// Searches much further out (2x to 8x the normal range) to find undevastated land.
        /// This allows the devastation to "leap over" obstacles like oceans or voids.
        /// </summary>
        private List<BlockPos> FindLongRangeMetastasisPositions(DevastationSource source, int count)
        {
            List<BlockPos> candidates = new List<BlockPos>();
            List<BlockPos> selected = new List<BlockPos>();
            
            // Search at multiple distance rings, starting further out
            int[] searchDistances = new int[] { 
                source.Range * 2, 
                source.Range * 4, 
                source.Range * 6,
                source.Range * 8 
            };
            
            foreach (int searchDist in searchDistances)
            {
                // Cap maximum search distance to prevent searching too far
                int cappedDist = Math.Min(searchDist, 128);
                int probeCount = count * 16; // More probes for long-range search
                
                for (int i = 0; i < probeCount; i++)
                {
                    // Generate random position at this distance
                    double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                    double angleY = (RandomNumberGenerator.GetInt32(60) - 30) * Math.PI / 180.0; // Flatter angle for long-range
                    
                    int offsetX = (int)(cappedDist * Math.Cos(angle) * Math.Cos(angleY));
                    int offsetY = (int)(cappedDist * Math.Sin(angleY));
                    int offsetZ = (int)(cappedDist * Math.Sin(angle) * Math.Cos(angleY));
                    
                    BlockPos candidatePos = new BlockPos(
                        source.Pos.X + offsetX,
                        source.Pos.Y + offsetY,
                        source.Pos.Z + offsetZ
                    );
                    
                    // Check min Y level
                    if (candidatePos.Y < config.MinYLevel) continue;
                    
                    // Check air contact requirement
                    if (config.RequireSourceAirContact && !IsAdjacentToAir(candidatePos)) continue;
                    
                    // Check if there's viable land here (need more blocks for long-range to be worth it)
                    int nonDevastatedNearby = CountNonDevastatedNearby(candidatePos, 6);
                    
                    if (nonDevastatedNearby > 10) // Higher threshold for long-range jumps
                    {
                        candidates.Add(candidatePos);
                    }
                }
                
                // If we found candidates at this distance, stop searching further
                if (candidates.Count >= count)
                {
                    break;
                }
            }
            
            // Select best candidates, spaced apart
            candidates = candidates.OrderByDescending(c => CountNonDevastatedNearby(c, 6)).ToList();
            
            foreach (var candidate in candidates)
            {
                if (selected.Count >= count) break;
                
                bool tooClose = false;
                foreach (var existing in selected)
                {
                    double dist = Math.Sqrt(
                        Math.Pow(candidate.X - existing.X, 2) +
                        Math.Pow(candidate.Y - existing.Y, 2) +
                        Math.Pow(candidate.Z - existing.Z, 2)
                    );
                    if (dist < source.Range) // Must be at least one range apart
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                // Also check not too close to existing sources
                if (!tooClose && !IsTooCloseToExistingSources(candidate, source.Range * 0.5))
                {
                    selected.Add(candidate);
                }
            }
            
            return selected;
        }

        private bool TryGetDevastatedForm(Block block, out string devastatedBlock, out string regeneratesTo)
        {
            devastatedBlock = "";
            regeneratesTo = "";

            // Determine what this block should become when devastated
            string path = block.Code.Path;
            
            if (path.StartsWith("soil-"))
            {
                devastatedBlock = "devastatedsoil-0";
                regeneratesTo = "soil-verylow-none";
            }
            else if (path.StartsWith("rock-"))
            {
                devastatedBlock = "drock";
                regeneratesTo = "rock-obsidian";
            }
            else if (path.StartsWith("tallgrass-"))
            {
                devastatedBlock = "devastationgrowth-normal";
                regeneratesTo = "none";
            }
            else if (path.StartsWith("smallberrybush-") || path.StartsWith("largeberrybush-"))
            {
                devastatedBlock = "devgrowth-thorns";
                regeneratesTo = "leavesbranchy-grown-oak";
            }
            else if (path.StartsWith("flower-") || path.StartsWith("fern-"))
            {
                devastatedBlock = "devgrowth-shrike";
                regeneratesTo = "none";
            }
            else if (path.StartsWith("crop-"))
            {
                devastatedBlock = "devgrowth-shard";
                regeneratesTo = "none";
            }
            else if (path.StartsWith("leavesbranchy-") || path.StartsWith("leaves-"))
            {
                devastatedBlock = "devgrowth-bush";
                regeneratesTo = "none";
            }
            else if (path.StartsWith("gravel-"))
            {
                devastatedBlock = "devastatedsoil-1";
                regeneratesTo = "sludgygravel";
            }
            else if (path.StartsWith("sand-"))
            {
                devastatedBlock = "devastatedsoil-2";
                regeneratesTo = "sludgygravel";
            }
            else if (path.StartsWith("log-"))
            {
                devastatedBlock = "devastatedsoil-3";
                regeneratesTo = "log-grown-aged-ud";
            }
            // Forest floor becomes devastated soil (forestfloor0, forestfloor1, etc - no hyphen)
            else if (path.StartsWith("forestfloor"))
            {
                devastatedBlock = "devastatedsoil-0";
                regeneratesTo = "soil-verylow-none";
            }
            // Peat becomes devastated soil
            else if (path.StartsWith("peat-"))
            {
                devastatedBlock = "devastatedsoil-0";
                regeneratesTo = "peat-none";
            }
            // Muddy gravel becomes devastated soil
            else if (path == "muddygravel")
            {
                devastatedBlock = "devastatedsoil-1";
                regeneratesTo = "sludgygravel";
            }
            // Raw clay becomes devastated soil
            else if (path.StartsWith("rawclay-"))
            {
                devastatedBlock = "devastatedsoil-0";
                regeneratesTo = "rawclay-blue-none";
            }
            // Tall plants (cattails/cooper's reed, etc) become devastated briars/thorns
            else if (path.StartsWith("tallplant-"))
            {
                devastatedBlock = "devgrowth-thorns";
                regeneratesTo = "none";
            }
            // Water plants
            else if (path.StartsWith("waterlily"))
            {
                devastatedBlock = "devgrowth-thorns";
                regeneratesTo = "none";
            }
            // Loose stones, boulders, flints - skip these (they'll remain as-is, spreading passes through)
            // Not converting them since they're decorative surface items
            else if (path.StartsWith("looseboulders-") || path.StartsWith("looseflints-") ||
                     path.StartsWith("loosestones-") || path.StartsWith("looseores-"))
            {
                // Return false - don't convert these blocks, spreading will pass through them
            }

            return devastatedBlock != "";
        }

        /// <summary>
        /// Spawns a small burst of color-coded particles at a source block to help visualize/debug sources.
        /// </summary>
        private void SpawnSourceMarkerParticles(DevastationSource source)
        {
            if (sapi == null) return;
            if (config != null && !config.ShowSourceMarkers) return;

            // Emit more frequently for stronger visibility (about 50% of ticks)
            if (sapi.World.Rand.NextDouble() > 0.5) return;

            // Color coding: blue = new/growing, green = seeding, red = saturated
            int markerColor = GetSourceMarkerColor(source);

            BlockPos pos = source.Pos;
            Vec3d center = pos.ToVec3d().Add(0.5, 0.7, 0.5);

            SimpleParticleProperties props = new SimpleParticleProperties
            {
                MinQuantity = 3,
                AddQuantity = 4,
                Color = markerColor,
                MinPos = new Vec3d(
                    center.X - 0.05,
                    center.Y - 0.05,
                    center.Z - 0.05),
                AddPos = new Vec3d(0.2, 0.2, 0.2),
                MinVelocity = new Vec3f(-0.03f, 0.07f, -0.03f),
                AddVelocity = new Vec3f(0.06f, 0.06f, 0.06f),
                LifeLength = 0.7f,
                GravityEffect = -0.03f, // Slight lift for a floating look
                MinSize = 0.12f,
                MaxSize = 0.22f,
                ShouldDieInLiquid = false,
                ParticleModel = EnumParticleModel.Quad
            };

            sapi.World.SpawnParticles(props);
        }

        /// <summary>
        /// Returns an RGBA color for debug markers based on source status.
        /// Blue = new/growing, Green = seeding, Red = saturated.
        /// </summary>
        private int GetSourceMarkerColor(DevastationSource source)
        {
            // Saturated: fully done spreading
            if (source.IsSaturated)
            {
                return ColorUtil.ToRgba(255, 255, 80, 80); // red
            }

            // Seeding: either currently spawning children or ready to spawn
            bool readyToSeed = !source.IsHealing &&
                               source.CurrentRadius >= source.Range &&
                               source.BlocksSinceLastMetastasis >= source.MetastasisThreshold;

            if (readyToSeed || source.ChildrenSpawned > 0 || source.IsMetastasis)
            {
                return ColorUtil.ToRgba(255, 80, 255, 120); // green
            }

            // Default: new/growing
            return ColorUtil.ToRgba(255, 80, 160, 255); // blue
        }

        #region Chunk-Based Devastation

        /// <summary>
        /// Processes all devastated chunks - spawns corrupted entities and spreads devastation rapidly.
        /// Includes performance monitoring and stuck chunk detection/repair.
        /// </summary>
        private void ProcessDevastatedChunks(float dt)
        {
            if (isPaused || sapi == null || devastatedChunks == null || devastatedChunks.Count == 0) return;

            perfStopwatch.Restart();

            try
            {
                double currentTime = sapi.World.Calendar.TotalHours;

                // Track delta time for performance monitoring (dt is in seconds, convert to ms)
                double dtMs = dt * 1000.0;
                tickDeltaTimes.Enqueue(dtMs);
                if (tickDeltaTimes.Count > PERF_SAMPLE_SIZE)
                    tickDeltaTimes.Dequeue();

                // Process any chunks needing repair (stuck chunks detected)
                ProcessChunksNeedingRepair();

                // Drain temporal stability from players in devastated chunks
                DrainPlayerTemporalStability(dt);

                // Check for chunk spreading to nearby chunks
                TrySpreadToNearbyChunks(currentTime);

                foreach (var chunk in devastatedChunks.Values.ToList()) // ToList() to allow modification during iteration
                {
                    // Skip chunks protected by rift wards - no processing needed
                    if (IsChunkProtectedByRiftWard(chunk.ChunkX, chunk.ChunkZ)) continue;

                    // Spawn corrupted entities periodically
                    TrySpawnCorruptedEntitiesInChunk(chunk, currentTime);

                    // Rapidly spread devastation within the chunk
                    if (!chunk.IsFullyDevastated)
                    {
                        SpreadDevastationInChunk(chunk);
                    }

                    // Skip unrepairable chunks entirely
                    if (chunk.IsUnrepairable) continue;

                    // Track consecutive empty frontier checks
                    bool frontierEmpty = chunk.DevastationFrontier == null || chunk.DevastationFrontier.Count == 0;
                    if (frontierEmpty && chunk.FrontierInitialized && !chunk.IsFullyDevastated)
                    {
                        chunk.ConsecutiveEmptyFrontierChecks++;
                    }
                    else
                    {
                        chunk.ConsecutiveEmptyFrontierChecks = 0;
                    }

                    // Check for stuck chunks: need 3+ consecutive empty checks to avoid false positives
                    if (!chunk.IsFullyDevastated &&
                        chunk.FrontierInitialized &&
                        chunk.ConsecutiveEmptyFrontierChecks >= 3 &&
                        chunk.BlocksDevastated < 1000) // Must have < 1000 blocks to be considered stuck
                    {
                        // Check repair cooldown (60 seconds real time, assuming ~1 tick per second)
                        double timeSinceLastRepair = currentTime - chunk.LastRepairAttemptTime;
                        if (timeSinceLastRepair < 0.01) continue; // ~36 seconds in game time at default speed

                        long chunkKey = chunk.ChunkKey;
                        if (!chunksNeedingRepair.Contains(chunkKey))
                        {
                            chunksNeedingRepair.Enqueue(chunkKey);
                            sapi.Logger.Warning($"SpreadingDevastation: Detected stuck chunk at ({chunk.ChunkX}, {chunk.ChunkZ}) with {chunk.BlocksDevastated} blocks (attempt {chunk.RepairAttemptCount + 1}), queuing for repair");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error processing devastated chunks: {ex.Message}");
            }
            finally
            {
                perfStopwatch.Stop();
                RecordTickPerformance(perfStopwatch.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Repairs chunks that were detected as stuck (no frontier, few blocks devastated).
        /// </summary>
        private void ProcessChunksNeedingRepair()
        {
            // Process up to 1 repair per tick
            if (chunksNeedingRepair.Count == 0) return;

            long chunkKey = chunksNeedingRepair.Dequeue();
            if (!devastatedChunks.TryGetValue(chunkKey, out var chunk)) return;

            // Skip if already fixed, fully devastated, or marked unrepairable
            if (chunk.IsFullyDevastated) return;
            if (chunk.IsUnrepairable) return;
            if (chunk.DevastationFrontier?.Count > 0) return;

            double currentTime = sapi.World.Calendar.TotalHours;

            // Check if this repair made progress since last attempt
            bool madeProgress = chunk.BlocksDevastated > chunk.BlocksAtLastRepair;

            if (madeProgress)
            {
                // Progress was made, reset the repair counter
                chunk.RepairAttemptCount = 0;
            }
            else
            {
                // No progress, increment repair counter
                chunk.RepairAttemptCount++;
            }

            // Check if we've exceeded max repair attempts without progress
            if (chunk.RepairAttemptCount >= 5)
            {
                chunk.IsUnrepairable = true;
                sapi.Logger.Warning($"SpreadingDevastation: Chunk at ({chunk.ChunkX}, {chunk.ChunkZ}) marked as unrepairable after {chunk.RepairAttemptCount} failed repair attempts (only {chunk.BlocksDevastated} blocks devastated)");
                return;
            }

            // Record this repair attempt
            chunk.LastRepairAttemptTime = currentTime;
            chunk.BlocksAtLastRepair = chunk.BlocksDevastated;
            chunk.ConsecutiveEmptyFrontierChecks = 0; // Reset so we don't immediately re-queue

            // Re-initialize the frontier
            chunk.FrontierInitialized = false;
            InitializeChunkFrontier(chunk);

            int newFrontierCount = chunk.DevastationFrontier?.Count ?? 0;
            sapi.Logger.Notification($"SpreadingDevastation: Repaired stuck chunk at ({chunk.ChunkX}, {chunk.ChunkZ}), frontier now has {newFrontierCount} blocks (attempt {chunk.RepairAttemptCount}/5)");
        }

        /// <summary>
        /// Records performance metrics for this tick.
        /// </summary>
        private void RecordTickPerformance(double processingTimeMs)
        {
            chunkProcessingTimes.Enqueue(processingTimeMs);
            if (chunkProcessingTimes.Count > PERF_SAMPLE_SIZE)
                chunkProcessingTimes.Dequeue();

            totalProcessingTimeMs += processingTimeMs;
            totalTicksProcessed++;

            if (processingTimeMs > peakProcessingTimeMs)
                peakProcessingTimeMs = processingTimeMs;
        }

        /// <summary>
        /// Gets current performance statistics for debugging.
        /// </summary>
        private (double avgTime, double peakTime, double throttle, int skipped, double avgDt) GetPerformanceStats()
        {
            double avgTime = chunkProcessingTimes.Count > 0 ? chunkProcessingTimes.Average() : 0;
            double avgDt = tickDeltaTimes.Count > 0 ? tickDeltaTimes.Average() : 0;
            return (avgTime, peakProcessingTimeMs, 1.0, 0, avgDt); // No throttling, return fixed values
        }

        /// <summary>
        /// Drains temporal stability from players standing in devastated chunks.
        /// Acts as if they're in an unstable temporal region.
        /// </summary>
        private void DrainPlayerTemporalStability(float dt)
        {
            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers.Cast<IServerPlayer>())
            {
                if (player.Entity == null) continue;

                // Check if player is in a devastated chunk
                int chunkX = (int)player.Entity.Pos.X / CHUNK_SIZE;
                int chunkZ = (int)player.Entity.Pos.Z / CHUNK_SIZE;
                long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

                if (!devastatedChunks.ContainsKey(chunkKey)) continue;

                // Skip if player is protected by a rift ward
                BlockPos playerBlockPos = player.Entity.Pos.AsBlockPos;
                if (IsBlockProtectedByRiftWard(playerBlockPos)) continue;

                // Get the temporal stability behavior
                var stabilityBehavior = player.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
                if (stabilityBehavior == null) continue;

                // Drain stability at configured rate (default 0.001 per 500ms tick = ~0.2% per second)
                double drainRate = config.ChunkStabilityDrainRate;
                double currentStability = stabilityBehavior.OwnStability;
                double newStability = Math.Max(0, currentStability - drainRate);

                stabilityBehavior.OwnStability = newStability;
            }
        }

        /// <summary>
        /// Cardinal direction offsets for chunk spreading (N/S/E/W only, no diagonals)
        /// </summary>
        private static readonly int[][] ChunkCardinalOffsets = new int[][]
        {
            new int[] { 1, 0 },   // East (+X)
            new int[] { -1, 0 },  // West (-X)
            new int[] { 0, 1 },   // South (+Z)
            new int[] { 0, -1 }   // North (-Z)
        };

        /// <summary>
        /// Checks if devastated chunks should spread to nearby chunks.
        /// Chunks only spread in cardinal directions (N/S/E/W) and only if
        /// there is at least one devastated block at the edge bordering the target chunk.
        /// Base interval is 60 seconds, affected by speed multiplier.
        /// </summary>
        private void TrySpreadToNearbyChunks(double currentTime)
        {
            if (!config.ChunkSpreadEnabled || devastatedChunks.Count == 0) return;

            // Calculate effective interval (base 60 seconds, divided by speed multiplier)
            // At 1x speed: check every 60 seconds
            // At 100x speed: check every 0.6 seconds
            double effectiveIntervalHours = (config.ChunkSpreadIntervalSeconds / 3600.0) / Math.Max(0.01, config.SpeedMultiplier);

            if (currentTime - lastChunkSpreadCheckTime < effectiveIntervalHours) return;
            lastChunkSpreadCheckTime = currentTime;

            // Check each devastated chunk for spreading (limit to prevent overwhelming the system)
            var chunksToAdd = new List<DevastatedChunk>();
            int maxNewChunksPerCheck = 3; // Limit new chunks per check to prevent database overwhelm

            foreach (var chunk in devastatedChunks.Values)
            {
                // Stop if we've already queued enough new chunks
                if (chunksToAdd.Count >= maxNewChunksPerCheck) break;

                // Roll for spread chance
                if (sapi.World.Rand.NextDouble() >= config.ChunkSpreadChance) continue;

                // Try to spread to a random cardinal direction (no diagonals)
                var shuffledDirections = ChunkCardinalOffsets.OrderBy(x => sapi.World.Rand.Next()).ToArray();

                foreach (var direction in shuffledDirections)
                {
                    int offsetX = direction[0];
                    int offsetZ = direction[1];

                    int newChunkX = chunk.ChunkX + offsetX;
                    int newChunkZ = chunk.ChunkZ + offsetZ;
                    long newChunkKey = DevastatedChunk.MakeChunkKey(newChunkX, newChunkZ);

                    // Skip if already devastated or already queued
                    if (devastatedChunks.ContainsKey(newChunkKey)) continue;
                    if (chunksToAdd.Any(c => c.ChunkKey == newChunkKey)) continue;

                    // Skip if chunk is protected by a rift ward
                    if (IsChunkProtectedByRiftWard(newChunkX, newChunkZ)) continue;

                    // Check if there's at least one devastated block at the edge of the source chunk
                    // bordering the target chunk direction
                    if (!HasDevastatedBlockAtEdge(chunk, offsetX, offsetZ)) continue;

                    // Create new devastated chunk with frontier seeded from edge of source chunk
                    var newChunk = new DevastatedChunk
                    {
                        ChunkX = newChunkX,
                        ChunkZ = newChunkZ,
                        MarkedTime = currentTime,
                        DevastationLevel = 0.0,
                        IsFullyDevastated = false,
                        FrontierInitialized = true,
                        DevastationFrontier = FindChunkEdgeFrontier(chunk, offsetX, offsetZ)
                    };

                    chunksToAdd.Add(newChunk);
                    break; // Only spread to one direction per chunk per check
                }
            }

            // Add new chunks after iteration
            foreach (var newChunk in chunksToAdd)
            {
                devastatedChunks[newChunk.ChunkKey] = newChunk;
                sapi.Logger.VerboseDebug($"SpreadingDevastation: Chunk ({newChunk.ChunkX}, {newChunk.ChunkZ}) became devastated from spread");
            }
        }

        /// <summary>
        /// Checks if a chunk has at least one devastated block at the edge bordering the specified direction.
        /// </summary>
        /// <param name="chunk">The source chunk to check</param>
        /// <param name="offsetX">Direction to check: 1 for east edge, -1 for west edge, 0 for no X edge</param>
        /// <param name="offsetZ">Direction to check: 1 for south edge, -1 for north edge, 0 for no Z edge</param>
        private bool HasDevastatedBlockAtEdge(DevastatedChunk chunk, int offsetX, int offsetZ)
        {
            int startX = chunk.ChunkX * CHUNK_SIZE;
            int startZ = chunk.ChunkZ * CHUNK_SIZE;

            // Determine which edge to check based on direction
            int edgeX, edgeZ;
            bool checkXEdge = offsetX != 0;
            bool checkZEdge = offsetZ != 0;

            // Sample positions along the edge
            int sampleCount = 16;
            for (int i = 0; i < sampleCount; i++)
            {
                if (checkXEdge)
                {
                    // Check east (offsetX=1) or west (offsetX=-1) edge
                    edgeX = offsetX > 0 ? startX + CHUNK_SIZE - 1 : startX;
                    edgeZ = startZ + sapi.World.Rand.Next(CHUNK_SIZE);
                }
                else
                {
                    // Check south (offsetZ=1) or north (offsetZ=-1) edge
                    edgeX = startX + sapi.World.Rand.Next(CHUNK_SIZE);
                    edgeZ = offsetZ > 0 ? startZ + CHUNK_SIZE - 1 : startZ;
                }

                int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(edgeX, 0, edgeZ));
                if (surfaceY <= 0) continue;

                // Check a vertical range around surface
                for (int yOffset = -3; yOffset <= 10; yOffset++)
                {
                    int y = surfaceY - yOffset;
                    if (y < 1) continue;

                    BlockPos pos = new BlockPos(edgeX, y, edgeZ);
                    Block block = sapi.World.BlockAccessor.GetBlock(pos);

                    if (block != null && IsAlreadyDevastated(block))
                    {
                        return true; // Found a devastated block at this edge
                    }
                }
            }

            return false; // No devastated blocks found at this edge
        }

        /// <summary>
        /// Attempts to spawn corrupted entities in a devastated chunk.
        /// Spawns at a random interval, with cooldown, within configured distance of nearest player.
        /// </summary>
        private void TrySpawnCorruptedEntitiesInChunk(DevastatedChunk chunk, double currentTime)
        {
            // Skip chunks cleansed by rift wards - no mob spawning once the expanding shell reaches the chunk
            if (IsChunkCleansedByRiftWard(chunk.ChunkX, chunk.ChunkZ)) return;

            // Check cooldown first - if cooldown has passed, reset the mob counter
            if (chunk.LastSpawnTime > 0)
            {
                double timeSinceLastSpawn = currentTime - chunk.LastSpawnTime;
                if (timeSinceLastSpawn >= config.ChunkSpawnCooldownHours)
                {
                    // Cooldown passed - reset mob counter to allow new spawns
                    chunk.MobsSpawned = 0;
                }
                else
                {
                    // Still in cooldown - no spawning allowed
                    return;
                }
            }

            // Check if we've hit the max mobs limit for this spawn cycle
            if (chunk.MobsSpawned >= config.ChunkSpawnMaxMobsPerChunk) return;

            // Check if we've reached the next scheduled spawn time
            if (chunk.NextSpawnTime > 0 && currentTime < chunk.NextSpawnTime) return;

            // Find the nearest player within spawn range
            IServerPlayer nearestPlayer = null;
            double nearestDistanceSq = double.MaxValue;
            int chunkCenterX = chunk.ChunkX * CHUNK_SIZE + CHUNK_SIZE / 2;
            int chunkCenterZ = chunk.ChunkZ * CHUNK_SIZE + CHUNK_SIZE / 2;

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers.Cast<IServerPlayer>())
            {
                if (player.Entity == null) continue;
                double distX = player.Entity.Pos.X - chunkCenterX;
                double distZ = player.Entity.Pos.Z - chunkCenterZ;
                double distSq = distX * distX + distZ * distZ;

                // Player must be within max spawn distance of chunk center
                if (distSq < config.ChunkSpawnMaxDistance * config.ChunkSpawnMaxDistance && distSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distSq;
                    nearestPlayer = player;
                }
            }

            if (nearestPlayer == null) return;

            // Find a valid spawn position near the player (within min-max distance)
            BlockPos spawnPos = FindSpawnPositionNearPlayer(nearestPlayer, config.ChunkSpawnMinDistance, config.ChunkSpawnMaxDistance);
            if (spawnPos == null) return;

            // Verify the spawn position is within a devastated chunk
            int spawnChunkX = spawnPos.X / CHUNK_SIZE;
            int spawnChunkZ = spawnPos.Z / CHUNK_SIZE;
            long spawnChunkKey = DevastatedChunk.MakeChunkKey(spawnChunkX, spawnChunkZ);
            if (!devastatedChunks.ContainsKey(spawnChunkKey)) return;

            // Also check if spawn position is cleansed by rift ward (block-level check)
            if (IsBlockCleansedByRiftWard(spawnPos)) return;

            // Update spawn tracking
            chunk.LastSpawnTime = currentTime;
            chunk.MobsSpawned++;

            // Schedule next spawn with random interval
            double randomInterval = config.ChunkSpawnIntervalMinHours +
                sapi.World.Rand.NextDouble() * (config.ChunkSpawnIntervalMaxHours - config.ChunkSpawnIntervalMinHours);
            chunk.NextSpawnTime = currentTime + Math.Max(randomInterval, config.ChunkSpawnCooldownHours);

            // Randomly choose between corrupted drifter and corrupted locust
            string entityCode = sapi.World.Rand.NextDouble() < 0.7 ? "drifter-corrupt" : "locust-corrupt";

            try
            {
                EntityProperties entityType = sapi.World.GetEntityType(new AssetLocation("game", entityCode));
                if (entityType == null)
                {
                    sapi.Logger.Warning($"SpreadingDevastation: Entity type '{entityCode}' not found");
                    return;
                }

                Entity entity = sapi.World.ClassRegistry.CreateEntity(entityType);
                entity.ServerPos.SetPos(spawnPos.X + 0.5, spawnPos.Y + 1, spawnPos.Z + 0.5);
                entity.Pos.SetFrom(entity.ServerPos);
                sapi.World.SpawnEntity(entity);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"SpreadingDevastation: Failed to spawn {entityCode}: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds a valid spawn position near a player within the specified distance range.
        /// </summary>
        private BlockPos FindSpawnPositionNearPlayer(IServerPlayer player, int minDistance, int maxDistance)
        {
            if (player?.Entity == null) return null;

            double playerX = player.Entity.Pos.X;
            double playerZ = player.Entity.Pos.Z;

            // Try up to 20 random positions in the ring around the player
            for (int attempt = 0; attempt < 20; attempt++)
            {
                // Generate random angle and distance within the ring
                double angle = sapi.World.Rand.NextDouble() * 2 * Math.PI;
                double distance = minDistance + sapi.World.Rand.NextDouble() * (maxDistance - minDistance);

                int x = (int)(playerX + Math.Cos(angle) * distance);
                int z = (int)(playerZ + Math.Sin(angle) * distance);

                // Find the surface Y
                int y = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
                if (y <= 0) continue;

                BlockPos pos = new BlockPos(x, y, z);
                Block block = sapi.World.BlockAccessor.GetBlock(pos);
                Block aboveBlock = sapi.World.BlockAccessor.GetBlock(pos.UpCopy());

                // Need solid ground with air above
                if (block != null && block.Id != 0 && aboveBlock != null && aboveBlock.Id == 0)
                {
                    return pos;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a valid spawn position within a devastated chunk (legacy fallback).
        /// </summary>
        private BlockPos FindSpawnPositionInChunk(DevastatedChunk chunk)
        {
            int startX = chunk.ChunkX * CHUNK_SIZE;
            int startZ = chunk.ChunkZ * CHUNK_SIZE;

            // Try up to 10 random positions
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int x = startX + sapi.World.Rand.Next(CHUNK_SIZE);
                int z = startZ + sapi.World.Rand.Next(CHUNK_SIZE);

                // Find the surface Y
                int y = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
                if (y <= 0) continue;

                BlockPos pos = new BlockPos(x, y, z);
                Block block = sapi.World.BlockAccessor.GetBlock(pos);
                Block aboveBlock = sapi.World.BlockAccessor.GetBlock(pos.UpCopy());

                // Need solid ground with air above
                if (block != null && block.Id != 0 && aboveBlock != null && aboveBlock.Id == 0)
                {
                    return pos;
                }
            }

            return null;
        }

        /// <summary>
        /// Six cardinal directions for adjacent block checking (east, west, up, down, south, north)
        /// </summary>
        private static readonly BlockPos[] CardinalOffsets = new BlockPos[]
        {
            new BlockPos(1, 0, 0),   // East (+X)
            new BlockPos(-1, 0, 0),  // West (-X)
            new BlockPos(0, 1, 0),   // Up (+Y)
            new BlockPos(0, -1, 0),  // Down (-Y)
            new BlockPos(0, 0, 1),   // South (+Z)
            new BlockPos(0, 0, -1)   // North (-Z)
        };

        /// <summary>
        /// Rapidly spreads devastation within a chunk using cardinal-adjacent spreading.
        /// Devastation only spreads to blocks adjacent (in 6 cardinal directions) to existing devastation.
        /// </summary>
        private void SpreadDevastationInChunk(DevastatedChunk chunk)
        {
            if (chunk.IsFullyDevastated) return;

            // Initialize frontier if needed (for chunks from older saves without frontier data)
            if (!chunk.FrontierInitialized || chunk.DevastationFrontier == null || chunk.DevastationFrontier.Count == 0)
            {
                InitializeChunkFrontier(chunk);
            }

            // If frontier is empty after initialization, check if chunk is actually done
            // Don't mark as fully devastated unless we've actually devastated a significant number of blocks
            if (chunk.DevastationFrontier.Count == 0)
            {
                if (chunk.BlocksDevastated >= 1000) // Reasonable minimum for a "fully devastated" chunk
                {
                    chunk.IsFullyDevastated = true;
                    return;
                }
                // Frontier is empty but chunk isn't done - this is a stuck chunk, don't process further this tick
                // The stuck chunk detection will pick it up and repair it
                return;
            }

            int startX = chunk.ChunkX * CHUNK_SIZE;
            int startZ = chunk.ChunkZ * CHUNK_SIZE;
            int endX = startX + CHUNK_SIZE;
            int endZ = startZ + CHUNK_SIZE;

            // Scale with speed multiplier: base 10 blocks per 500ms tick = 20/sec at 1x speed
            int blocksToProcess = Math.Max(1, (int)(10 * config.SpeedMultiplier));
            int successCount = 0;
            int attempts = 0;
            int maxAttempts = blocksToProcess * 10; // More attempts since we're being selective

            List<BlockPos> newFrontierBlocks = new List<BlockPos>();
            List<BlockPos> blocksToRemoveFromFrontier = new List<BlockPos>();

            while (successCount < blocksToProcess && attempts < maxAttempts && chunk.DevastationFrontier.Count > 0)
            {
                attempts++;

                // Pick a random frontier block to spread from
                int frontierIndex = sapi.World.Rand.Next(chunk.DevastationFrontier.Count);
                BlockPos frontierPos = chunk.DevastationFrontier[frontierIndex];

                // Try each cardinal direction from this frontier block
                bool foundCandidate = false;
                // Shuffle the cardinal directions to avoid bias
                var shuffledOffsets = CardinalOffsets.OrderBy(x => sapi.World.Rand.Next()).ToArray();

                foreach (var offset in shuffledOffsets)
                {
                    BlockPos targetPos = new BlockPos(
                        frontierPos.X + offset.X,
                        frontierPos.Y + offset.Y,
                        frontierPos.Z + offset.Z
                    );

                    // Check if target is within chunk bounds (X and Z only - Y can go anywhere)
                    if (targetPos.X < startX || targetPos.X >= endX ||
                        targetPos.Z < startZ || targetPos.Z >= endZ)
                    {
                        continue; // Outside chunk bounds
                    }

                    // Check Y bounds
                    if (targetPos.Y < 1) continue;

                    // Check minY level constraint
                    if (targetPos.Y < config.MinYLevel) continue;

                    // Check depth below surface constraint (when aircontact is enabled)
                    if (config.RequireSourceAirContact && config.ChunkMaxDepthBelowSurface >= 0)
                    {
                        int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(targetPos);
                        if (surfaceY > 0 && targetPos.Y < surfaceY - config.ChunkMaxDepthBelowSurface)
                        {
                            continue; // Too deep below surface
                        }
                    }

                    // Get the block at this position
                    Block block = sapi.World.BlockAccessor.GetBlock(targetPos);

                    if (block == null || block.Id == 0) continue; // Skip air blocks

                    // Check if already devastated
                    if (IsAlreadyDevastated(block))
                    {
                        continue; // Already devastated
                    }

                    // Check if protected by rift ward
                    if (IsBlockProtectedByRiftWard(targetPos))
                    {
                        continue; // Protected by rift ward
                    }

                    string devastatedBlock, regeneratesTo;
                    if (TryGetDevastatedForm(block, out devastatedBlock, out regeneratesTo))
                    {
                        Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedBlock));
                        if (newBlock != null)
                        {
                            sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);

                            // Track for regeneration
                            regrowingBlocks.Add(new RegrowingBlocks
                            {
                                Pos = targetPos,
                                Out = regeneratesTo,
                                LastTime = sapi.World.Calendar.TotalHours
                            });

                            // Add this newly devastated block to the frontier
                            newFrontierBlocks.Add(targetPos.Copy());

                            successCount++;
                            chunk.BlocksDevastated++;
                            foundCandidate = true;

                            break; // Move to next attempt
                        }
                        else
                        {
                            // Log missing block for debugging
                            sapi.Logger.Warning($"SpreadingDevastation: Could not find devastated block '{devastatedBlock}' for source block '{block.Code.Path}'");
                        }
                    }
                }

                // If we couldn't find any valid candidate from this frontier block, check if it should be removed
                if (!foundCandidate)
                {
                    bool hasValidNeighbors = false;
                    foreach (var offset in CardinalOffsets)
                    {
                        BlockPos neighborPos = new BlockPos(
                            frontierPos.X + offset.X,
                            frontierPos.Y + offset.Y,
                            frontierPos.Z + offset.Z
                        );

                        // Check if within chunk bounds
                        if (neighborPos.X < startX || neighborPos.X >= endX ||
                            neighborPos.Z < startZ || neighborPos.Z >= endZ ||
                            neighborPos.Y < 1)
                        {
                            continue;
                        }

                        // Check minY level constraint
                        if (neighborPos.Y < config.MinYLevel) continue;

                        // Check depth below surface constraint (when aircontact is enabled)
                        if (config.RequireSourceAirContact && config.ChunkMaxDepthBelowSurface >= 0)
                        {
                            int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(neighborPos);
                            if (surfaceY > 0 && neighborPos.Y < surfaceY - config.ChunkMaxDepthBelowSurface)
                            {
                                continue; // Too deep below surface
                            }
                        }

                        Block neighborBlock = sapi.World.BlockAccessor.GetBlock(neighborPos);
                        if (neighborBlock != null && neighborBlock.Id != 0 && !IsAlreadyDevastated(neighborBlock))
                        {
                            if (TryGetDevastatedForm(neighborBlock, out _, out _))
                            {
                                hasValidNeighbors = true;
                                break;
                            }
                        }
                    }

                    if (!hasValidNeighbors && !blocksToRemoveFromFrontier.Any(p => p.X == frontierPos.X && p.Y == frontierPos.Y && p.Z == frontierPos.Z))
                    {
                        blocksToRemoveFromFrontier.Add(frontierPos);
                    }
                }
            }

            // Update frontier: add new blocks, remove exhausted ones
            foreach (var newBlock in newFrontierBlocks)
            {
                if (!chunk.DevastationFrontier.Any(p => p.X == newBlock.X && p.Y == newBlock.Y && p.Z == newBlock.Z))
                {
                    chunk.DevastationFrontier.Add(newBlock);
                }

                // Check if this new block is at a chunk edge - if so, potentially start a bleed
                if (config.ChunkEdgeBleedDepth > 0 && sapi.World.Rand.NextDouble() < config.ChunkEdgeBleedChance)
                {
                    TryStartEdgeBleed(chunk, newBlock, startX, startZ, endX, endZ);
                }
            }

            foreach (var oldBlock in blocksToRemoveFromFrontier)
            {
                chunk.DevastationFrontier.RemoveAll(p => p.X == oldBlock.X && p.Y == oldBlock.Y && p.Z == oldBlock.Z);
            }

            // Process bleed frontier - spread limited devastation into adjacent non-devastated chunks
            if (chunk.BleedFrontier != null && chunk.BleedFrontier.Count > 0)
            {
                ProcessBleedFrontier(chunk);
            }

            // Periodic fill-in pass: every 20 ticks (~10 seconds), scan for missed blocks
            // This catches blocks that were skipped due to air gaps, pruning, or other edge cases
            chunk.FillInTickCounter++;
            if (chunk.FillInTickCounter >= 20 && !chunk.IsFullyDevastated)
            {
                chunk.FillInTickCounter = 0;
                TryFillInMissedBlocks(chunk, startX, startZ, endX, endZ);
            }

            // Prune frontier if it gets too large (prioritize edge blocks to maintain spread)
            if (chunk.DevastationFrontier.Count > 500)
            {
                // Prioritize keeping blocks at chunk edges (for chunk-to-chunk spread)
                // and blocks far from center (to maintain outward spread)
                int centerX = startX + CHUNK_SIZE / 2;
                int centerZ = startZ + CHUNK_SIZE / 2;

                // Score blocks: higher score = keep. Edge blocks get bonus, distance from center adds to score
                chunk.DevastationFrontier = chunk.DevastationFrontier
                    .OrderByDescending(p =>
                    {
                        int distFromCenter = Math.Abs(p.X - centerX) + Math.Abs(p.Z - centerZ);
                        // Bonus for being at chunk edge (within 2 blocks of edge)
                        bool atEdge = p.X < startX + 2 || p.X >= endX - 2 || p.Z < startZ + 2 || p.Z >= endZ - 2;
                        return distFromCenter + (atEdge ? 100 : 0);
                    })
                    .Take(300)
                    .ToList();
            }

            // Update devastation level estimate
            // Rough estimate: a chunk surface area is 32x32 = 1024 blocks, times ~10 depth = ~10000 convertible blocks
            chunk.DevastationLevel = Math.Min(1.0, chunk.BlocksDevastated / 5000.0);

            // Mark as fully devastated if frontier is empty AND we've devastated enough blocks
            if (chunk.DevastationFrontier.Count == 0 && chunk.BlocksDevastated >= 1000)
            {
                chunk.IsFullyDevastated = true;
            }
        }

        /// <summary>
        /// Scans the chunk for convertible blocks that are adjacent to devastated blocks but were missed.
        /// This handles cases where air gaps, pruning, or other edge cases left holes in the devastation.
        /// </summary>
        private void TryFillInMissedBlocks(DevastatedChunk chunk, int startX, int startZ, int endX, int endZ)
        {
            int blocksFound = 0;
            int maxBlocksToFind = 20; // Limit per pass to avoid lag
            int sampleAttempts = 50; // Random sampling attempts

            for (int attempt = 0; attempt < sampleAttempts && blocksFound < maxBlocksToFind; attempt++)
            {
                // Pick a random position in the chunk
                int x = startX + sapi.World.Rand.Next(CHUNK_SIZE);
                int z = startZ + sapi.World.Rand.Next(CHUNK_SIZE);
                int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
                if (surfaceY <= 0) continue;

                // Check a vertical range around surface
                int minY = config.RequireSourceAirContact && config.ChunkMaxDepthBelowSurface >= 0
                    ? Math.Max(config.MinYLevel, surfaceY - config.ChunkMaxDepthBelowSurface)
                    : config.MinYLevel;

                for (int yOffset = -5; yOffset <= 5 && blocksFound < maxBlocksToFind; yOffset++)
                {
                    int y = surfaceY + yOffset;
                    if (y < minY || y < 1) continue;

                    BlockPos pos = new BlockPos(x, y, z);
                    Block block = sapi.World.BlockAccessor.GetBlock(pos);

                    // Skip if null, air, or already devastated
                    if (block == null || block.Id == 0 || IsAlreadyDevastated(block)) continue;

                    // Check if this block can be devastated
                    if (!TryGetDevastatedForm(block, out string devastatedForm, out string regeneratesTo)) continue;

                    // Check if any cardinal neighbor is devastated (this block should have been caught)
                    bool hasDevastatedNeighbor = false;
                    foreach (var offset in CardinalOffsets)
                    {
                        BlockPos neighborPos = new BlockPos(pos.X + offset.X, pos.Y + offset.Y, pos.Z + offset.Z);
                        Block neighborBlock = sapi.World.BlockAccessor.GetBlock(neighborPos);
                        if (neighborBlock != null && IsAlreadyDevastated(neighborBlock))
                        {
                            hasDevastatedNeighbor = true;
                            break;
                        }
                    }

                    if (hasDevastatedNeighbor)
                    {
                        // This block was missed! Devastate it and add to frontier
                        Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedForm));
                        if (newBlock != null)
                        {
                            sapi.World.BlockAccessor.SetBlock(newBlock.Id, pos);
                            regrowingBlocks.Add(new RegrowingBlocks
                            {
                                Pos = pos.Copy(),
                                Out = regeneratesTo,
                                LastTime = sapi.World.Calendar.TotalHours
                            });
                            chunk.BlocksDevastated++;
                            blocksFound++;

                            // Add to frontier
                            if (!chunk.DevastationFrontier.Any(p => p.X == pos.X && p.Y == pos.Y && p.Z == pos.Z))
                            {
                                chunk.DevastationFrontier.Add(pos.Copy());
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a newly devastated block is at a chunk edge and starts a bleed into the adjacent chunk if so.
        /// </summary>
        private void TryStartEdgeBleed(DevastatedChunk chunk, BlockPos edgeBlock, int startX, int startZ, int endX, int endZ)
        {
            if (chunk.BleedFrontier == null)
            {
                chunk.BleedFrontier = new List<BleedBlock>();
            }

            // Check each horizontal cardinal direction to see if we're at an edge
            // Only bleed horizontally (not up/down)
            BlockPos[] horizontalOffsets = new BlockPos[]
            {
                new BlockPos(1, 0, 0),   // East
                new BlockPos(-1, 0, 0),  // West
                new BlockPos(0, 0, 1),   // South
                new BlockPos(0, 0, -1)   // North
            };

            foreach (var offset in horizontalOffsets)
            {
                BlockPos adjacentPos = new BlockPos(
                    edgeBlock.X + offset.X,
                    edgeBlock.Y,
                    edgeBlock.Z + offset.Z
                );

                // Check if the adjacent position is outside this chunk
                if (adjacentPos.X >= startX && adjacentPos.X < endX &&
                    adjacentPos.Z >= startZ && adjacentPos.Z < endZ)
                {
                    continue; // Still inside chunk, not an edge
                }

                // Check if the adjacent chunk is NOT devastated
                int adjacentChunkX = adjacentPos.X / CHUNK_SIZE;
                int adjacentChunkZ = adjacentPos.Z / CHUNK_SIZE;
                // Handle negative coordinates
                if (adjacentPos.X < 0) adjacentChunkX = (adjacentPos.X + 1) / CHUNK_SIZE - 1;
                if (adjacentPos.Z < 0) adjacentChunkZ = (adjacentPos.Z + 1) / CHUNK_SIZE - 1;

                long adjacentChunkKey = DevastatedChunk.MakeChunkKey(adjacentChunkX, adjacentChunkZ);

                if (devastatedChunks.ContainsKey(adjacentChunkKey))
                {
                    continue; // Adjacent chunk is already devastated, no need to bleed
                }

                // Check if the adjacent block can be devastated
                Block adjacentBlock = sapi.World.BlockAccessor.GetBlock(adjacentPos);
                if (adjacentBlock == null || adjacentBlock.Id == 0 || IsAlreadyDevastated(adjacentBlock))
                {
                    continue;
                }

                // Check if protected by rift ward
                if (IsBlockProtectedByRiftWard(adjacentPos))
                {
                    continue; // Protected by rift ward
                }

                if (!TryGetDevastatedForm(adjacentBlock, out string devastatedForm, out string regeneratesTo))
                {
                    continue;
                }

                // Check if we already have this position in the bleed frontier
                if (chunk.BleedFrontier.Any(b => b.Pos.X == adjacentPos.X && b.Pos.Y == adjacentPos.Y && b.Pos.Z == adjacentPos.Z))
                {
                    continue;
                }

                // Devastate the adjacent block and add it to bleed frontier
                Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedForm));
                if (newBlock != null)
                {
                    sapi.World.BlockAccessor.SetBlock(newBlock.Id, adjacentPos);

                    regrowingBlocks.Add(new RegrowingBlocks
                    {
                        Pos = adjacentPos.Copy(),
                        Out = regeneratesTo,
                        LastTime = sapi.World.Calendar.TotalHours
                    });

                    // Add to bleed frontier with remaining spread budget
                    chunk.BleedFrontier.Add(new BleedBlock
                    {
                        Pos = adjacentPos.Copy(),
                        RemainingSpread = config.ChunkEdgeBleedDepth - 1 // -1 because we just placed one
                    });
                }
            }
        }

        /// <summary>
        /// Processes the bleed frontier, spreading limited devastation into adjacent non-devastated chunks.
        /// Each bleed block can only spread a limited number of times before stopping.
        /// </summary>
        private void ProcessBleedFrontier(DevastatedChunk chunk)
        {
            if (chunk.BleedFrontier == null || chunk.BleedFrontier.Count == 0) return;

            // Process a limited number of bleed blocks per tick
            int maxToProcess = Math.Max(1, (int)(3 * config.SpeedMultiplier));
            int processed = 0;

            List<BleedBlock> newBleedBlocks = new List<BleedBlock>();
            List<BleedBlock> bleedBlocksToRemove = new List<BleedBlock>();

            // Shuffle to avoid directional bias
            var shuffledBleed = chunk.BleedFrontier.OrderBy(x => sapi.World.Rand.Next()).ToList();

            foreach (var bleedBlock in shuffledBleed)
            {
                if (processed >= maxToProcess) break;

                // If no remaining spread, mark for removal
                if (bleedBlock.RemainingSpread <= 0)
                {
                    bleedBlocksToRemove.Add(bleedBlock);
                    continue;
                }

                // Try to spread to an adjacent block (horizontal only for natural look)
                BlockPos[] horizontalOffsets = new BlockPos[]
                {
                    new BlockPos(1, 0, 0),
                    new BlockPos(-1, 0, 0),
                    new BlockPos(0, 0, 1),
                    new BlockPos(0, 0, -1),
                    new BlockPos(0, 1, 0),  // Also allow up/down for terrain following
                    new BlockPos(0, -1, 0)
                };

                var shuffledOffsets = horizontalOffsets.OrderBy(x => sapi.World.Rand.Next()).ToArray();
                bool foundTarget = false;

                foreach (var offset in shuffledOffsets)
                {
                    BlockPos targetPos = new BlockPos(
                        bleedBlock.Pos.X + offset.X,
                        bleedBlock.Pos.Y + offset.Y,
                        bleedBlock.Pos.Z + offset.Z
                    );

                    // Check Y bounds
                    if (targetPos.Y < 1) continue;
                    if (targetPos.Y < config.MinYLevel) continue;

                    // Check depth constraint
                    if (config.RequireSourceAirContact && config.ChunkMaxDepthBelowSurface >= 0)
                    {
                        int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(targetPos);
                        if (surfaceY > 0 && targetPos.Y < surfaceY - config.ChunkMaxDepthBelowSurface)
                        {
                            continue;
                        }
                    }

                    // Bleed can spread anywhere - into non-devastated chunks OR back into the source chunk
                    // This creates organic edges on both sides of chunk boundaries

                    Block targetBlock = sapi.World.BlockAccessor.GetBlock(targetPos);
                    if (targetBlock == null || targetBlock.Id == 0 || IsAlreadyDevastated(targetBlock))
                    {
                        continue;
                    }

                    // Check if protected by rift ward
                    if (IsBlockProtectedByRiftWard(targetPos))
                    {
                        continue; // Protected by rift ward
                    }

                    if (!TryGetDevastatedForm(targetBlock, out string devastatedForm, out string regeneratesTo))
                    {
                        continue;
                    }

                    // Devastate the block
                    Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedForm));
                    if (newBlock != null)
                    {
                        sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);

                        regrowingBlocks.Add(new RegrowingBlocks
                        {
                            Pos = targetPos.Copy(),
                            Out = regeneratesTo,
                            LastTime = sapi.World.Calendar.TotalHours
                        });

                        // Add new bleed block with decremented spread budget
                        newBleedBlocks.Add(new BleedBlock
                        {
                            Pos = targetPos.Copy(),
                            RemainingSpread = bleedBlock.RemainingSpread - 1
                        });

                        foundTarget = true;
                        processed++;
                        break;
                    }
                }

                // If we couldn't spread anywhere, remove this bleed block
                if (!foundTarget)
                {
                    bleedBlocksToRemove.Add(bleedBlock);
                }
            }

            // Update bleed frontier
            foreach (var newBleed in newBleedBlocks)
            {
                if (!chunk.BleedFrontier.Any(b => b.Pos.X == newBleed.Pos.X && b.Pos.Y == newBleed.Pos.Y && b.Pos.Z == newBleed.Pos.Z))
                {
                    chunk.BleedFrontier.Add(newBleed);
                }
            }

            foreach (var oldBleed in bleedBlocksToRemove)
            {
                chunk.BleedFrontier.Remove(oldBleed);
            }

            // Prune bleed frontier if too large
            if (chunk.BleedFrontier.Count > 200)
            {
                chunk.BleedFrontier = chunk.BleedFrontier
                    .OrderByDescending(b => b.RemainingSpread)
                    .Take(100)
                    .ToList();
            }
        }

        /// <summary>
        /// Initializes the frontier for a chunk that doesn't have one (e.g., from older saves or chunk spread).
        /// Searches for existing devastated blocks to use as the frontier.
        /// </summary>
        private void InitializeChunkFrontier(DevastatedChunk chunk)
        {
            chunk.FrontierInitialized = true;
            chunk.DevastationFrontier = new List<BlockPos>();

            int startX = chunk.ChunkX * CHUNK_SIZE;
            int startZ = chunk.ChunkZ * CHUNK_SIZE;

            // Search for existing devastated blocks that could be frontier (have non-devastated neighbors)
            // Sample the chunk to find devastated blocks
            for (int attempt = 0; attempt < 100; attempt++)
            {
                int x = startX + sapi.World.Rand.Next(CHUNK_SIZE);
                int z = startZ + sapi.World.Rand.Next(CHUNK_SIZE);
                int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
                if (surfaceY <= 0) continue;

                // Check a vertical range around surface
                for (int yOffset = -3; yOffset <= 10; yOffset++)
                {
                    int y = surfaceY - yOffset;
                    if (y < 1) continue;

                    BlockPos pos = new BlockPos(x, y, z);
                    Block block = sapi.World.BlockAccessor.GetBlock(pos);

                    if (block != null && IsAlreadyDevastated(block))
                    {
                        // Check if this block has any non-devastated neighbors (making it a frontier block)
                        foreach (var offset in CardinalOffsets)
                        {
                            BlockPos neighborPos = new BlockPos(pos.X + offset.X, pos.Y + offset.Y, pos.Z + offset.Z);
                            Block neighborBlock = sapi.World.BlockAccessor.GetBlock(neighborPos);
                            if (neighborBlock != null && neighborBlock.Id != 0 && !IsAlreadyDevastated(neighborBlock))
                            {
                                if (TryGetDevastatedForm(neighborBlock, out _, out _))
                                {
                                    // This is a valid frontier block
                                    if (!chunk.DevastationFrontier.Any(p => p.X == pos.X && p.Y == pos.Y && p.Z == pos.Z))
                                    {
                                        chunk.DevastationFrontier.Add(pos.Copy());
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                // Stop if we've found enough frontier blocks
                if (chunk.DevastationFrontier.Count >= 20) break;
            }

            // If no devastated blocks found, pick a random surface position to start from
            // and actually devastate that block so the frontier has a valid starting point
            if (chunk.DevastationFrontier.Count == 0)
            {
                int x = startX + CHUNK_SIZE / 2;
                int z = startZ + CHUNK_SIZE / 2;
                int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
                if (surfaceY > 0)
                {
                    BlockPos startPos = new BlockPos(x, surfaceY, z);

                    // Actually devastate the starting block so spreading can work
                    Block startBlock = sapi.World.BlockAccessor.GetBlock(startPos);
                    if (startBlock != null && startBlock.Id != 0 && !IsAlreadyDevastated(startBlock))
                    {
                        if (TryGetDevastatedForm(startBlock, out string devastatedBlock, out string regeneratesTo))
                        {
                            Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedBlock));
                            if (newBlock != null)
                            {
                                sapi.World.BlockAccessor.SetBlock(newBlock.Id, startPos);
                                regrowingBlocks.Add(new RegrowingBlocks
                                {
                                    Pos = startPos.Copy(),
                                    Out = regeneratesTo,
                                    LastTime = sapi.World.Calendar.TotalHours
                                });
                                chunk.BlocksDevastated++;
                                chunk.DevastationFrontier.Add(startPos);
                            }
                        }
                        else
                        {
                            // Block can't be devastated, try to find a nearby convertible block
                            foreach (var offset in CardinalOffsets)
                            {
                                BlockPos nearbyPos = new BlockPos(startPos.X + offset.X, startPos.Y + offset.Y, startPos.Z + offset.Z);
                                Block nearbyBlock = sapi.World.BlockAccessor.GetBlock(nearbyPos);
                                if (nearbyBlock != null && nearbyBlock.Id != 0 && !IsAlreadyDevastated(nearbyBlock))
                                {
                                    if (TryGetDevastatedForm(nearbyBlock, out string devForm, out string regenTo))
                                    {
                                        Block devBlock = sapi.World.GetBlock(new AssetLocation("game", devForm));
                                        if (devBlock != null)
                                        {
                                            sapi.World.BlockAccessor.SetBlock(devBlock.Id, nearbyPos);
                                            regrowingBlocks.Add(new RegrowingBlocks
                                            {
                                                Pos = nearbyPos.Copy(),
                                                Out = regenTo,
                                                LastTime = sapi.World.Calendar.TotalHours
                                            });
                                            chunk.BlocksDevastated++;
                                            chunk.DevastationFrontier.Add(nearbyPos);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (startBlock != null && IsAlreadyDevastated(startBlock))
                    {
                        // Block is already devastated, just add to frontier
                        chunk.DevastationFrontier.Add(startPos);
                    }
                }
            }
        }

        /// <summary>
        /// Finds blocks in the new chunk that are cardinally adjacent to devastated blocks in the source chunk.
        /// This ensures devastation spreads continuously - only blocks touching existing devastation can spread.
        /// </summary>
        /// <param name="sourceChunk">The existing devastated chunk</param>
        /// <param name="offsetX">Direction to new chunk in X (-1, 0, or 1)</param>
        /// <param name="offsetZ">Direction to new chunk in Z (-1, 0, or 1)</param>
        private List<BlockPos> FindChunkEdgeFrontier(DevastatedChunk sourceChunk, int offsetX, int offsetZ)
        {
            List<BlockPos> edgeFrontier = new List<BlockPos>();

            int sourceStartX = sourceChunk.ChunkX * CHUNK_SIZE;
            int sourceStartZ = sourceChunk.ChunkZ * CHUNK_SIZE;

            // Determine which edge of the SOURCE chunk to search for devastated blocks
            // offsetX > 0 means new chunk is to the east, so we check the east edge (x = startX + CHUNK_SIZE - 1)
            // offsetX < 0 means new chunk is to the west, so we check the west edge (x = startX)
            // Same logic for Z

            // Search along the edge of the source chunk for devastated blocks
            int edgeLength = CHUNK_SIZE;
            for (int i = 0; i < edgeLength; i++)
            {
                int sourceEdgeX, sourceEdgeZ;

                if (offsetX != 0)
                {
                    // Checking east or west edge of source chunk
                    sourceEdgeX = offsetX > 0 ? sourceStartX + CHUNK_SIZE - 1 : sourceStartX;
                    sourceEdgeZ = sourceStartZ + i;
                }
                else
                {
                    // Checking north or south edge of source chunk
                    sourceEdgeX = sourceStartX + i;
                    sourceEdgeZ = offsetZ > 0 ? sourceStartZ + CHUNK_SIZE - 1 : sourceStartZ;
                }

                int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(sourceEdgeX, 0, sourceEdgeZ));
                if (surfaceY <= 0) continue;

                // Check a vertical range around surface level for devastated blocks in source chunk
                for (int yOffset = -5; yOffset <= 10; yOffset++)
                {
                    int y = surfaceY - yOffset;
                    if (y < 1) continue;

                    BlockPos sourcePos = new BlockPos(sourceEdgeX, y, sourceEdgeZ);
                    Block sourceBlock = sapi.World.BlockAccessor.GetBlock(sourcePos);

                    // Found a devastated block at the edge of source chunk
                    if (sourceBlock != null && IsAlreadyDevastated(sourceBlock))
                    {
                        // Now find the adjacent block in the NEW chunk (one step in the offset direction)
                        BlockPos newChunkPos = new BlockPos(
                            sourceEdgeX + offsetX,
                            y,
                            sourceEdgeZ + offsetZ
                        );

                        Block newChunkBlock = sapi.World.BlockAccessor.GetBlock(newChunkPos);

                        // Check if this block in the new chunk can be devastated
                        if (newChunkBlock != null && newChunkBlock.Id != 0 && !IsAlreadyDevastated(newChunkBlock))
                        {
                            if (TryGetDevastatedForm(newChunkBlock, out _, out _))
                            {
                                if (!edgeFrontier.Any(p => p.X == newChunkPos.X && p.Y == newChunkPos.Y && p.Z == newChunkPos.Z))
                                {
                                    edgeFrontier.Add(newChunkPos.Copy());
                                }
                            }
                        }
                        // Also check if the block is already devastated (can still be a frontier)
                        else if (newChunkBlock != null && IsAlreadyDevastated(newChunkBlock))
                        {
                            if (!edgeFrontier.Any(p => p.X == newChunkPos.X && p.Y == newChunkPos.Y && p.Z == newChunkPos.Z))
                            {
                                edgeFrontier.Add(newChunkPos.Copy());
                            }
                        }
                    }
                }
            }

            return edgeFrontier;
        }

        #endregion

        #region Rift Ward Protection

        /// <summary>
        /// Called when a block is placed. Checks if it's a rift ward and tracks it.
        /// </summary>
        private void OnBlockPlaced(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (blockSel?.Position == null) return;

            Block block = sapi.World.BlockAccessor.GetBlock(blockSel.Position);
            if (IsRiftWardBlock(block))
            {
                // Track this rift ward position (it might not be active yet until fueled)
                long posKey = GetPositionKey(blockSel.Position);
                if (!activeRiftWards.Any(rw => GetPositionKey(rw.Pos) == posKey))
                {
                    activeRiftWards.Add(new RiftWard
                    {
                        Pos = blockSel.Position.Copy(),
                        DiscoveredTime = sapi.World.Calendar.TotalHours
                    });
                    sapi.Logger.Notification($"SpreadingDevastation: Rift ward placed at {blockSel.Position}");
                    RebuildProtectedChunkCache();
                }
            }
        }

        /// <summary>
        /// Called when a block is broken. Removes rift ward from tracking if applicable.
        /// </summary>
        private void OnBlockBroken(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (blockSel?.Position == null) return;

            // Check if we were tracking a rift ward at this position
            long posKey = GetPositionKey(blockSel.Position);
            var ward = activeRiftWards.FirstOrDefault(rw => GetPositionKey(rw.Pos) == posKey);
            if (ward != null)
            {
                activeRiftWards.Remove(ward);
                sapi.Logger.Notification($"SpreadingDevastation: Rift ward at {blockSel.Position} was broken (healed {ward.BlocksHealed} blocks)");
                RebuildProtectedChunkCache();
            }
        }

        /// <summary>
        /// Processes rift ward active state checks and healing.
        /// Also handles initial world scan and periodic re-scans for rift wards.
        /// </summary>
        private void ProcessRiftWards(float dt)
        {
            if (sapi == null) return;

            double currentTime = sapi.World.Calendar.TotalHours;
            double checkIntervalHours = config.RiftWardScanIntervalSeconds / 3600.0;
            double fullScanIntervalHours = 5.0 / 60.0; // Re-scan every 5 minutes in game time

            // Perform initial scan for existing rift wards (or periodic re-scan)
            if (!initialRiftWardScanCompleted || (currentTime - lastFullRiftWardScanTime >= fullScanIntervalHours))
            {
                ScanForExistingRiftWards();
            }

            // Skip the rest if no rift wards are tracked
            if (activeRiftWards == null || activeRiftWards.Count == 0) return;

            // Periodically verify rift wards are still active (have fuel) and apply protection
            if (currentTime - lastRiftWardScanTime >= checkIntervalHours)
            {
                lastRiftWardScanTime = currentTime;
                VerifyRiftWardActiveState();

                // Remove any newly spawned devastation sources within rift ward radii
                // This catches sources that spawn from rifts after the ward was placed
                RemoveSourcesInAllRiftWardRadii();

                // Clear any new devastated chunks within rift ward radii
                // This maintains temporal stability in protected areas
                RemoveDevastatedChunksInAllRiftWardRadii();
            }

            // Process healing for each active rift ward
            if (config.RiftWardHealingEnabled)
            {
                ProcessRiftWardHealing(dt);
            }
        }

        /// <summary>
        /// Verifies that tracked rift wards are still active (have fuel and are on).
        /// Only checks existing tracked wards - no scanning for new ones.
        /// </summary>
        private void VerifyRiftWardActiveState()
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0) return;

            var wardsToRemove = new List<RiftWard>();
            bool anyBecameActive = false;

            foreach (var ward in activeRiftWards)
            {
                Block block = sapi.World.BlockAccessor.GetBlock(ward.Pos);

                // Check if block still exists
                if (!IsRiftWardBlock(block))
                {
                    wardsToRemove.Add(ward);
                    sapi.Logger.Notification($"SpreadingDevastation: Rift ward at {ward.Pos} no longer exists (healed {ward.BlocksHealed} blocks)");
                    continue;
                }

                // Check active state
                bool isActive = IsRiftWardActive(ward.Pos);

                // If ward just became active, notify and apply protection effects
                if (isActive && ward.BlocksHealed == 0 && ward.LastHealTime == 0)
                {
                    BroadcastMessage($"Rift ward at {ward.Pos} is now ACTIVE and protecting!");

                    // Remove devastation sources
                    int removedSources = RemoveSourcesInRiftWardRadius(ward);
                    if (removedSources > 0)
                    {
                        BroadcastMessage($"Rift ward neutralized {removedSources} devastation source(s)!");
                    }

                    // Clear devastated chunks (restores temporal stability)
                    int clearedChunks = RemoveDevastatedChunksInRiftWardRadius(ward);
                    if (clearedChunks > 0)
                    {
                        BroadcastMessage($"Rift ward restored temporal stability to {clearedChunks} chunk(s)!");
                    }

                    anyBecameActive = true;
                }

                // If ward ran out of fuel, remove from active tracking
                // (but don't remove entirely - it might be refueled)
                if (!isActive && ward.LastHealTime > 0)
                {
                    sapi.Logger.Notification($"SpreadingDevastation: Rift ward at {ward.Pos} ran out of fuel (healed {ward.BlocksHealed} blocks)");
                    // Reset heal time so we can detect when it becomes active again
                    ward.LastHealTime = 0;
                }
            }

            foreach (var ward in wardsToRemove)
            {
                activeRiftWards.Remove(ward);
            }

            if (wardsToRemove.Count > 0 || anyBecameActive)
            {
                RebuildProtectedChunkCache();
            }
        }

        /// <summary>
        /// Checks if a block is a rift ward block.
        /// </summary>
        private bool IsRiftWardBlock(Block block)
        {
            if (block == null || block.Code == null) return false;

            // Check for game:riftward block
            return block.Code.Path == "riftward" ||
                   block.Code.ToString() == "game:riftward" ||
                   block.Code.Path.Contains("riftward");
        }

        /// <summary>
        /// Broadcasts a message to all online players and logs it.
        /// </summary>
        private void BroadcastMessage(string message)
        {
            sapi.Logger.Notification($"SpreadingDevastation: {message}");
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player is IServerPlayer serverPlayer)
                {
                    serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, $"[RiftWard] {message}", EnumChatType.Notification);
                }
            }
        }

        /// <summary>
        /// Checks if a rift ward at the given position is active (has fuel AND is turned on).
        /// Uses reflection to access the BlockEntityRiftWard properties since it's in the game DLL.
        /// The ward has an "On" property (bool) that indicates if it's running, separate from fuel.
        /// Players can toggle the ward on/off by right-clicking even when it has fuel.
        /// </summary>
        private bool IsRiftWardActive(BlockPos pos)
        {
            if (pos == null) return false;

            try
            {
                // Get the block entity at this position
                var blockEntity = sapi.World.BlockAccessor.GetBlockEntity(pos);
                if (blockEntity == null) return false;

                // Check if it's a rift ward block entity by type name
                var typeName = blockEntity.GetType().Name;
                if (!typeName.Contains("RiftWard")) return false;

                // Check the "On" property - this is the primary toggle state
                // The ward must be explicitly turned on to be active
                var onProperty = blockEntity.GetType().GetProperty("On");
                if (onProperty != null)
                {
                    var isOn = onProperty.GetValue(blockEntity);
                    if (isOn is bool onValue)
                    {
                        // If On property exists, it's the authoritative state
                        // Ward must be On AND have fuel to be active
                        if (!onValue) return false; // Ward is turned off

                        // Ward is on, also verify it has fuel
                        var hasFuelProperty = blockEntity.GetType().GetProperty("HasFuel");
                        if (hasFuelProperty != null)
                        {
                            var hasFuel = hasFuelProperty.GetValue(blockEntity);
                            if (hasFuel is bool fuelValue)
                            {
                                return fuelValue; // Return true only if On AND HasFuel
                            }
                        }

                        // If HasFuel property doesn't exist, check fuelDays directly
                        var fuelDaysField = blockEntity.GetType().GetField("fuelDays",
                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                        if (fuelDaysField != null)
                        {
                            var fuelDays = fuelDaysField.GetValue(blockEntity);
                            if (fuelDays is double daysValue) return daysValue > 0;
                            if (fuelDays is float floatDays) return floatDays > 0;
                        }

                        // On is true but couldn't verify fuel - assume active
                        return true;
                    }
                }

                // Fallback for older versions or different implementations:
                // If "On" property doesn't exist, fall back to fuel check only
                var fallbackHasFuelProperty = blockEntity.GetType().GetProperty("HasFuel");
                if (fallbackHasFuelProperty != null)
                {
                    var hasFuel = fallbackHasFuelProperty.GetValue(blockEntity);
                    if (hasFuel is bool fuelValue)
                    {
                        return fuelValue;
                    }
                }

                // Final fallback: check fuelDays field directly
                var fallbackFuelDaysField = blockEntity.GetType().GetField("fuelDays",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (fallbackFuelDaysField != null)
                {
                    var fuelDays = fallbackFuelDaysField.GetValue(blockEntity);
                    if (fuelDays is double daysValue) return daysValue > 0;
                    if (fuelDays is float floatDays) return floatDays > 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"SpreadingDevastation: Error checking rift ward active state at {pos}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a unique key for a block position.
        /// </summary>
        private long GetPositionKey(BlockPos pos)
        {
            if (pos == null) return 0;
            return ((long)pos.X << 40) | ((long)(pos.Y & 0xFFFF) << 24) | (uint)(pos.Z & 0xFFFFFF);
        }

        /// <summary>
        /// Processes healing for all active rift wards.
        /// </summary>
        private void ProcessRiftWardHealing(float dt)
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0) return;

            double currentTime = sapi.World.Calendar.TotalHours;
            double activeCheckIntervalHours = 1.0 / 3600.0; // Check active state once per second (1 second = 1/3600 hours)

            // Calculate blocks to heal per rift ward this tick
            // dt is in seconds, RiftWardHealingRate is blocks per second
            // Use RiftWardSpeedMultiplier if set (>0), otherwise use global SpeedMultiplier
            double speedMult = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
            double blocksToHealThisTick = config.RiftWardHealingRate * dt * speedMult;

            // Track if any healing occurred - we'll update chunk status after
            bool anyHealing = false;

            foreach (var ward in activeRiftWards)
            {
                // Use cached active state, only check via reflection once per second
                if (currentTime - ward.LastActiveCheck >= activeCheckIntervalHours)
                {
                    ward.CachedIsActive = IsRiftWardActive(ward.Pos);
                    ward.LastActiveCheck = currentTime;
                }

                // Only heal if the rift ward is active (has fuel)
                if (!ward.CachedIsActive) continue;

                // Track fractional healing across ticks
                int blocksToProcess = (int)blocksToHealThisTick;
                if (blocksToProcess < 1 && sapi.World.Rand.NextDouble() < blocksToHealThisTick)
                {
                    blocksToProcess = 1;
                }

                if (blocksToProcess > 0)
                {
                    int healed = HealBlocksAroundRiftWard(ward, blocksToProcess);
                    ward.BlocksHealed += healed;
                    ward.LastHealTime = currentTime;
                    if (healed > 0) anyHealing = true;
                }
            }

            // If healing occurred, check for chunks that can be removed from devastation tracking
            // This removes fog effects from cleaned areas more responsively
            if (anyHealing)
            {
                CleanFullyHealedChunksInRiftWardRadii();
            }
        }

        /// <summary>
        /// Removes chunks within the rift ward's current clean radius from devastatedChunks.
        /// This makes fog disappear progressively as the expanding shell reaches each chunk.
        /// </summary>
        private void CleanFullyHealedChunksInRiftWardRadii()
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0) return;
            if (devastatedChunks == null || devastatedChunks.Count == 0) return;

            var chunksToRemove = new List<long>();
            string mode = GetEffectiveCleanMode();

            foreach (var ward in activeRiftWards)
            {
                if (ward.Pos == null || !ward.CachedIsActive) continue;

                // Determine the clean radius based on mode
                int cleanRadius;
                if (mode == "raster")
                {
                    // For raster mode, use CurrentCleanRadius - the actual expanding shell position
                    // This ensures fog/effects only clear once the shell has actually reached that area
                    // If scan is complete, use the full protection radius
                    cleanRadius = ward.RasterScanComplete ? config.RiftWardProtectionRadius : ward.CurrentCleanRadius;
                }
                else if (mode == "radial")
                {
                    // For radial mode, use MaxCleanRadiusReached (legacy behavior)
                    cleanRadius = ward.MaxCleanRadiusReached;
                }
                else
                {
                    // For random mode, use full protection radius since cleaning is random throughout
                    cleanRadius = config.RiftWardProtectionRadius;
                }

                // Need at least some radius to clear chunks
                if (cleanRadius < 1) continue;

                int minChunkX = (ward.Pos.X - cleanRadius) / CHUNK_SIZE;
                int maxChunkX = (ward.Pos.X + cleanRadius) / CHUNK_SIZE;
                int minChunkZ = (ward.Pos.Z - cleanRadius) / CHUNK_SIZE;
                int maxChunkZ = (ward.Pos.Z + cleanRadius) / CHUNK_SIZE;

                for (int cx = minChunkX; cx <= maxChunkX; cx++)
                {
                    for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
                    {
                        long chunkKey = DevastatedChunk.MakeChunkKey(cx, cz);
                        if (!devastatedChunks.ContainsKey(chunkKey)) continue;
                        if (chunksToRemove.Contains(chunkKey)) continue;

                        // Check if chunk center is within the cleaned radius
                        int chunkCenterX = cx * CHUNK_SIZE + CHUNK_SIZE / 2;
                        int chunkCenterZ = cz * CHUNK_SIZE + CHUNK_SIZE / 2;
                        int dx = chunkCenterX - ward.Pos.X;
                        int dz = chunkCenterZ - ward.Pos.Z;
                        int distanceSquared = dx * dx + dz * dz;

                        // Remove chunk from devastatedChunks when within clean radius
                        // This clears fog progressively as the expanding shell reaches each chunk
                        if (distanceSquared <= cleanRadius * cleanRadius)
                        {
                            chunksToRemove.Add(chunkKey);
                        }
                    }
                }
            }

            // Remove chunks - this clears fog effects
            foreach (long chunkKey in chunksToRemove)
            {
                devastatedChunks.Remove(chunkKey);
            }
        }

        /// <summary>
        /// Heals devastated blocks within the rift ward's protection radius.
        /// Dispatches to raster, radial, or random mode based on config.
        /// </summary>
        private int HealBlocksAroundRiftWard(RiftWard ward, int blocksToHeal)
        {
            if (ward.Pos == null) return 0;

            string mode = GetEffectiveCleanMode();
            switch (mode)
            {
                case "raster":
                    return HealBlocksRasterScan(ward, blocksToHeal);
                case "radial":
                    return HealBlocksRadialClean(ward, blocksToHeal);
                case "random":
                default:
                    return HealBlocksRandom(ward, blocksToHeal);
            }
        }

        /// <summary>
        /// Heals devastated blocks using radial clean mode - expands outward from the center
        /// in concentric shells, creating a smooth clearing pattern.
        /// </summary>
        private int HealBlocksRadialClean(RiftWard ward, int blocksToHeal)
        {
            int healedCount = 0;
            int maxRadius = config.RiftWardProtectionRadius;
            int maxFailuresPerRadius = 100; // How many failed attempts before advancing radius

            // Calculate Y bounds for scanning - scan full column within protection sphere
            // Underground can be quite deep, so scan from near bedrock up to reasonable height
            int minY = Math.Max(1, ward.Pos.Y - maxRadius);
            int maxY = Math.Min(sapi.World.BlockAccessor.MapSizeY - 1, ward.Pos.Y + maxRadius);

            // Process blocks at current radius shell
            for (int attempt = 0; attempt < blocksToHeal * 5 && healedCount < blocksToHeal; attempt++)
            {
                // Track the maximum radius we've reached (for fog clearing - doesn't reset)
                if (ward.CurrentCleanRadius > ward.MaxCleanRadiusReached)
                {
                    ward.MaxCleanRadiusReached = ward.CurrentCleanRadius;
                }

                // If we've exceeded max radius, we're done - reset to check again from center
                if (ward.CurrentCleanRadius > maxRadius)
                {
                    ward.MaxCleanRadiusReached = maxRadius; // Ensure max is set to full radius
                    ward.CurrentCleanRadius = 0;
                    ward.RadialCleanFailures = 0;
                    return healedCount;
                }

                int r = ward.CurrentCleanRadius;
                BlockPos targetPos;

                if (r == 0)
                {
                    // At center, scan the entire column including below the ward
                    // Pick a random Y in the full column range
                    int targetY = minY + sapi.World.Rand.Next(maxY - minY + 1);
                    targetPos = new BlockPos(ward.Pos.X, targetY, ward.Pos.Z);
                }
                else
                {
                    // Generate position on current radius shell
                    double angle = sapi.World.Rand.NextDouble() * 2 * Math.PI;

                    int offsetX = (int)(r * Math.Cos(angle));
                    int offsetZ = (int)(r * Math.Sin(angle));

                    // Calculate the max Y offset allowed at this horizontal radius to stay within sphere
                    // For a sphere: x² + y² + z² <= r²  =>  y² <= maxRadius² - (horizontal distance)²
                    int horizontalDistSq = offsetX * offsetX + offsetZ * offsetZ;
                    int maxYOffsetSq = maxRadius * maxRadius - horizontalDistSq;
                    int maxYOffset = maxYOffsetSq > 0 ? (int)Math.Sqrt(maxYOffsetSq) : 0;

                    // Pick a random Y within the allowed vertical range
                    int yMin = Math.Max(minY, ward.Pos.Y - maxYOffset);
                    int yMax = Math.Min(maxY, ward.Pos.Y + maxYOffset);
                    int targetY = yMin + sapi.World.Rand.Next(Math.Max(1, yMax - yMin + 1));

                    targetPos = new BlockPos(
                        ward.Pos.X + offsetX,
                        targetY,
                        ward.Pos.Z + offsetZ
                    );
                }

                if (targetPos.Y < 1)
                {
                    ward.RadialCleanFailures++;
                    if (ward.RadialCleanFailures >= maxFailuresPerRadius)
                    {
                        ward.CurrentCleanRadius++;
                        ward.RadialCleanFailures = 0;
                    }
                    continue;
                }

                Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                if (block == null || block.Id == 0)
                {
                    ward.RadialCleanFailures++;
                    if (ward.RadialCleanFailures >= maxFailuresPerRadius)
                    {
                        ward.CurrentCleanRadius++;
                        ward.RadialCleanFailures = 0;
                    }
                    continue;
                }

                // Check if this is a devastated block
                if (!IsAlreadyDevastated(block))
                {
                    ward.RadialCleanFailures++;
                    if (ward.RadialCleanFailures >= maxFailuresPerRadius)
                    {
                        ward.CurrentCleanRadius++;
                        ward.RadialCleanFailures = 0;
                    }
                    continue;
                }

                // Found a devastated block - heal it
                if (TryGetHealedForm(block, out string healedBlock))
                {
                    if (healedBlock == "none")
                    {
                        sapi.World.BlockAccessor.SetBlock(0, targetPos);
                    }
                    else
                    {
                        Block newBlock = sapi.World.GetBlock(new AssetLocation("game", healedBlock));
                        if (newBlock != null)
                        {
                            sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
                        }
                    }

                    healedCount++;
                    ward.RadialCleanFailures = 0; // Reset failures on success
                }
            }

            return healedCount;
        }

        /// <summary>
        /// Heals devastated blocks using random selection within the protection radius.
        /// </summary>
        private int HealBlocksRandom(RiftWard ward, int blocksToHeal)
        {
            int healedCount = 0;
            int maxAttempts = blocksToHeal * 10;
            int radius = config.RiftWardProtectionRadius;

            for (int attempt = 0; attempt < maxAttempts && healedCount < blocksToHeal; attempt++)
            {
                // Generate random position within the protection radius
                // Use spherical distribution for more even coverage
                double distance = sapi.World.Rand.NextDouble() * radius;
                double angle = sapi.World.Rand.NextDouble() * 2 * Math.PI;
                double angleY = (sapi.World.Rand.NextDouble() - 0.5) * Math.PI;

                int offsetX = (int)(distance * Math.Cos(angle) * Math.Cos(angleY));
                int offsetY = (int)(distance * Math.Sin(angleY));
                int offsetZ = (int)(distance * Math.Sin(angle) * Math.Cos(angleY));

                BlockPos targetPos = new BlockPos(
                    ward.Pos.X + offsetX,
                    ward.Pos.Y + offsetY,
                    ward.Pos.Z + offsetZ
                );

                if (targetPos.Y < 1) continue;

                Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                if (block == null || block.Id == 0) continue;

                // Check if this is a devastated block
                if (!IsAlreadyDevastated(block)) continue;

                // Heal the block
                if (TryGetHealedForm(block, out string healedBlock))
                {
                    if (healedBlock == "none")
                    {
                        sapi.World.BlockAccessor.SetBlock(0, targetPos);
                    }
                    else
                    {
                        Block newBlock = sapi.World.GetBlock(new AssetLocation("game", healedBlock));
                        if (newBlock != null)
                        {
                            sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
                        }
                    }

                    // Note: We don't remove from regrowingBlocks here to avoid O(n) overhead
                    // The regrowingBlocks list is for tracking purposes; healed blocks will simply
                    // be skipped when regeneration runs since they're no longer devastated
                    healedCount++;
                }
            }

            return healedCount;
        }

        /// <summary>
        /// Heals devastated blocks using deterministic spherical shell expansion.
        /// Starts at the center and expands outward in concentric spherical shells,
        /// processing every block in each shell before moving to the next radius.
        /// This creates a smooth "expanding globe" visual effect while being 100% thorough.
        /// </summary>
        private int HealBlocksRasterScan(RiftWard ward, int blocksToHeal)
        {
            int healedCount = 0;
            int maxRadius = config.RiftWardProtectionRadius;

            // CurrentCleanRadius tracks which spherical shell we're currently processing
            // ScanX, ScanY, ScanZ track position within that shell
            int r = ward.CurrentCleanRadius;

            // Initialize scan position for current shell if starting fresh
            if (ward.ScanX == 0 && ward.ScanY == 0 && ward.ScanZ == 0 && !ward.RasterScanComplete)
            {
                ward.ScanY = -r;
                ward.ScanX = -r;
                ward.ScanZ = -r;
            }

            int positionsProcessed = 0;
            int maxPositionsPerTick = blocksToHeal * 100; // Process many positions since most won't be in current shell or devastated

            while (positionsProcessed < maxPositionsPerTick && healedCount < blocksToHeal)
            {
                r = ward.CurrentCleanRadius;

                // Check if we've completed all shells
                if (r > maxRadius)
                {
                    ward.RasterScanComplete = true;
                    ward.MaxCleanRadiusReached = maxRadius;
                    // Reset for next pass
                    ward.CurrentCleanRadius = 0;
                    ward.ScanX = 0;
                    ward.ScanY = 0;
                    ward.ScanZ = 0;
                    return healedCount;
                }

                // Get current scan position within this shell
                int x = ward.ScanX;
                int y = ward.ScanY;
                int z = ward.ScanZ;

                // Advance to next position
                bool shellComplete = AdvanceShellPosition(ward, r);

                if (shellComplete)
                {
                    // Move to next shell
                    ward.CurrentCleanRadius++;
                    ward.MaxCleanRadiusReached = Math.Max(ward.MaxCleanRadiusReached, r);
                    int nextR = ward.CurrentCleanRadius;
                    ward.ScanY = -nextR;
                    ward.ScanX = -nextR;
                    ward.ScanZ = -nextR;
                    continue;
                }

                // Check if this position is in the current shell (not a smaller or larger radius)
                int distSq = x * x + y * y + z * z;
                int shellMinSq = r * r;
                int shellMaxSq = (r + 1) * (r + 1);

                // Position must be >= r distance and < r+1 distance to be in this shell
                if (distSq < shellMinSq || distSq >= shellMaxSq)
                {
                    continue; // Not in current shell, skip
                }

                positionsProcessed++;

                // Calculate actual world position
                BlockPos targetPos = new BlockPos(ward.Pos.X + x, ward.Pos.Y + y, ward.Pos.Z + z);

                // Skip positions below world or above map
                if (targetPos.Y < 1 || targetPos.Y >= sapi.World.BlockAccessor.MapSizeY)
                {
                    continue;
                }

                // Get the block at this position
                Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                if (block == null || block.Id == 0)
                {
                    continue;
                }

                // Check if this is a devastated block
                if (!IsAlreadyDevastated(block))
                {
                    continue;
                }

                // Found a devastated block - heal it
                if (TryGetHealedForm(block, out string healedBlock))
                {
                    if (healedBlock == "none")
                    {
                        sapi.World.BlockAccessor.SetBlock(0, targetPos);
                    }
                    else
                    {
                        Block newBlock = sapi.World.GetBlock(new AssetLocation("game", healedBlock));
                        if (newBlock != null)
                        {
                            sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
                        }
                    }

                    healedCount++;
                }
            }

            return healedCount;
        }

        /// <summary>
        /// Advances the scan position within a spherical shell.
        /// Returns true if the shell is complete (wrapped around).
        /// </summary>
        private bool AdvanceShellPosition(RiftWard ward, int r)
        {
            if (r == 0)
            {
                // Radius 0 is just the center block - immediately complete
                return true;
            }

            // Advance Z first, then X, then Y
            ward.ScanZ++;
            if (ward.ScanZ > r)
            {
                ward.ScanZ = -r;
                ward.ScanX++;
                if (ward.ScanX > r)
                {
                    ward.ScanX = -r;
                    ward.ScanY++;
                    if (ward.ScanY > r)
                    {
                        // Shell complete
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the effective rift ward clean mode, considering both new and legacy config.
        /// </summary>
        private string GetEffectiveCleanMode()
        {
            // If explicit mode is set, use it
            if (!string.IsNullOrEmpty(config.RiftWardCleanMode))
            {
                string mode = config.RiftWardCleanMode.ToLowerInvariant();
                if (mode == "raster" || mode == "radial" || mode == "random")
                {
                    return mode;
                }
            }

            // Fall back to legacy boolean
            return config.RiftWardRadialCleanEnabled ? "radial" : "random";
        }

        /// <summary>
        /// Rebuilds the cache of chunk keys that are protected by rift wards.
        /// </summary>
        private void RebuildProtectedChunkCache()
        {
            protectedChunkKeys.Clear();

            foreach (var ward in activeRiftWards)
            {
                if (ward.Pos == null) continue;

                int radius = config.RiftWardProtectionRadius;

                // Calculate which chunks are covered by this rift ward
                int minChunkX = (ward.Pos.X - radius) / CHUNK_SIZE;
                int maxChunkX = (ward.Pos.X + radius) / CHUNK_SIZE;
                int minChunkZ = (ward.Pos.Z - radius) / CHUNK_SIZE;
                int maxChunkZ = (ward.Pos.Z + radius) / CHUNK_SIZE;

                for (int cx = minChunkX; cx <= maxChunkX; cx++)
                {
                    for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
                    {
                        protectedChunkKeys.Add(DevastatedChunk.MakeChunkKey(cx, cz));
                    }
                }
            }
        }

        /// <summary>
        /// Removes all devastation sources within a rift ward's protection radius.
        /// This prevents temporal instability and mob spawning in protected areas.
        /// </summary>
        private int RemoveSourcesInRiftWardRadius(RiftWard ward)
        {
            if (ward?.Pos == null || devastationSources == null || devastationSources.Count == 0)
                return 0;

            int radiusSquared = config.RiftWardProtectionRadius * config.RiftWardProtectionRadius;

            int removed = devastationSources.RemoveAll(source =>
            {
                if (source.Pos == null) return false;

                int dx = source.Pos.X - ward.Pos.X;
                int dy = source.Pos.Y - ward.Pos.Y;
                int dz = source.Pos.Z - ward.Pos.Z;
                int distanceSquared = dx * dx + dy * dy + dz * dz;

                return distanceSquared <= radiusSquared;
            });

            if (removed > 0)
            {
                sapi.Logger.Notification($"SpreadingDevastation: Rift ward at {ward.Pos} removed {removed} devastation source(s)");
            }

            return removed;
        }

        /// <summary>
        /// Removes devastation sources within all active rift ward protection radii.
        /// Called periodically to catch newly spawned sources (e.g., from rifts).
        /// </summary>
        private int RemoveSourcesInAllRiftWardRadii()
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0)
                return 0;

            int totalRemoved = 0;
            foreach (var ward in activeRiftWards)
            {
                if (ward.CachedIsActive)
                {
                    totalRemoved += RemoveSourcesInRiftWardRadius(ward);
                }
            }
            return totalRemoved;
        }

        /// <summary>
        /// Initializes rift wards after loading save data.
        /// Restores active states and applies protection effects.
        /// </summary>
        private void InitializeRiftWardsOnLoad()
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0) return;

            int activeCount = 0;
            int totalSourcesRemoved = 0;
            int totalChunksCleared = 0;

            foreach (var ward in activeRiftWards)
            {
                if (ward.Pos == null) continue;

                // Check and cache the active state
                ward.CachedIsActive = IsRiftWardActive(ward.Pos);
                ward.LastActiveCheck = sapi.World.Calendar.TotalHours;

                if (ward.CachedIsActive)
                {
                    activeCount++;

                    // Remove any devastation sources within this ward's radius
                    int sourcesRemoved = RemoveSourcesInRiftWardRadius(ward);
                    totalSourcesRemoved += sourcesRemoved;

                    // Clear devastated chunks within this ward's radius (restores temporal stability)
                    int chunksCleared = RemoveDevastatedChunksInRiftWardRadius(ward);
                    totalChunksCleared += chunksCleared;
                }
            }

            if (activeCount > 0)
            {
                sapi.Logger.Notification($"SpreadingDevastation: Restored {activeCount} active rift ward(s), removed {totalSourcesRemoved} source(s), cleared {totalChunksCleared} devastated chunk(s)");
            }
        }

        /// <summary>
        /// Scans loaded chunks around players for rift ward blocks and adds them to tracking.
        /// This catches rift wards that existed before the mod was installed, or rift wards
        /// that weren't previously tracked for any reason.
        /// </summary>
        private void ScanForExistingRiftWards()
        {
            if (sapi == null) return;

            int scanRadius = 8; // Scan 8 chunks in each direction around each player (256 blocks)
            int newWardsFound = 0;
            var existingPositions = new HashSet<long>(activeRiftWards.Select(w => GetPositionKey(w.Pos)));

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity == null) continue;

                var playerPos = player.Entity.Pos.AsBlockPos;
                int playerChunkX = playerPos.X / CHUNK_SIZE;
                int playerChunkZ = playerPos.Z / CHUNK_SIZE;

                // Scan chunks around the player
                for (int cx = playerChunkX - scanRadius; cx <= playerChunkX + scanRadius; cx++)
                {
                    for (int cz = playerChunkZ - scanRadius; cz <= playerChunkZ + scanRadius; cz++)
                    {
                        // Get the chunk if it's loaded
                        var chunk = sapi.World.BlockAccessor.GetChunk(cx, 0, cz);
                        if (chunk == null) continue;

                        // Scan all blocks in this chunk column for rift wards
                        int startX = cx * CHUNK_SIZE;
                        int startZ = cz * CHUNK_SIZE;
                        int endX = startX + CHUNK_SIZE;
                        int endZ = startZ + CHUNK_SIZE;

                        // Scan at surface level and a bit below (rift wards are usually at ground level)
                        int seaLevel = sapi.World.SeaLevel;
                        int minY = Math.Max(1, seaLevel - 50);
                        int maxY = Math.Min(sapi.World.BlockAccessor.MapSizeY - 1, seaLevel + 100);

                        for (int x = startX; x < endX; x++)
                        {
                            for (int z = startZ; z < endZ; z++)
                            {
                                for (int y = minY; y < maxY; y++)
                                {
                                    var pos = new BlockPos(x, y, z);
                                    var block = sapi.World.BlockAccessor.GetBlock(pos);

                                    if (IsRiftWardBlock(block))
                                    {
                                        long posKey = GetPositionKey(pos);
                                        if (!existingPositions.Contains(posKey))
                                        {
                                            var newWard = new RiftWard
                                            {
                                                Pos = pos.Copy(),
                                                DiscoveredTime = sapi.World.Calendar.TotalHours
                                            };

                                            // Check if it's active
                                            newWard.CachedIsActive = IsRiftWardActive(pos);
                                            newWard.LastActiveCheck = sapi.World.Calendar.TotalHours;

                                            activeRiftWards.Add(newWard);
                                            existingPositions.Add(posKey);
                                            newWardsFound++;

                                            sapi.Logger.Notification($"SpreadingDevastation: Discovered existing rift ward at {pos} (active: {newWard.CachedIsActive})");

                                            if (newWard.CachedIsActive)
                                            {
                                                // Apply protection effects
                                                RemoveSourcesInRiftWardRadius(newWard);
                                                RemoveDevastatedChunksInRiftWardRadius(newWard);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (newWardsFound > 0)
            {
                sapi.Logger.Notification($"SpreadingDevastation: Scan found {newWardsFound} previously untracked rift ward(s)");
                RebuildProtectedChunkCache();
            }

            initialRiftWardScanCompleted = true;
            lastFullRiftWardScanTime = sapi.World.Calendar.TotalHours;
        }

        /// <summary>
        /// Removes devastated chunks within a rift ward's current cleansed radius.
        /// In raster mode, this respects the expanding shell - only clears chunks the shell has reached.
        /// In other modes, uses the full protection radius.
        /// </summary>
        private int RemoveDevastatedChunksInRiftWardRadius(RiftWard ward)
        {
            if (ward?.Pos == null || devastatedChunks == null || devastatedChunks.Count == 0)
                return 0;

            // Determine the effective radius based on mode
            string mode = GetEffectiveCleanMode();
            int radius;
            if (mode == "raster")
            {
                // In raster mode, only clear chunks within the current shell radius
                radius = ward.RasterScanComplete ? config.RiftWardProtectionRadius : ward.CurrentCleanRadius;
            }
            else if (mode == "radial")
            {
                radius = ward.MaxCleanRadiusReached;
            }
            else
            {
                radius = config.RiftWardProtectionRadius;
            }

            // If radius is 0, nothing to clear yet
            if (radius < 1) return 0;

            int radiusSquared = radius * radius;

            // Find chunks that overlap with the current cleansed radius
            int minChunkX = (ward.Pos.X - radius) / CHUNK_SIZE;
            int maxChunkX = (ward.Pos.X + radius) / CHUNK_SIZE;
            int minChunkZ = (ward.Pos.Z - radius) / CHUNK_SIZE;
            int maxChunkZ = (ward.Pos.Z + radius) / CHUNK_SIZE;

            var chunksToRemove = new List<long>();

            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
                {
                    long chunkKey = DevastatedChunk.MakeChunkKey(cx, cz);
                    if (devastatedChunks.ContainsKey(chunkKey))
                    {
                        // Check if the chunk center is within the cleansed radius
                        int chunkCenterX = cx * CHUNK_SIZE + CHUNK_SIZE / 2;
                        int chunkCenterZ = cz * CHUNK_SIZE + CHUNK_SIZE / 2;

                        int dx = chunkCenterX - ward.Pos.X;
                        int dz = chunkCenterZ - ward.Pos.Z;
                        int distanceSquared = dx * dx + dz * dz;

                        if (distanceSquared <= radiusSquared)
                        {
                            chunksToRemove.Add(chunkKey);
                        }
                    }
                }
            }

            foreach (long chunkKey in chunksToRemove)
            {
                devastatedChunks.Remove(chunkKey);
            }

            if (chunksToRemove.Count > 0)
            {
                sapi.Logger.Notification($"SpreadingDevastation: Rift ward at {ward.Pos} cleared {chunksToRemove.Count} devastated chunk(s) - temporal stability restored");
            }

            return chunksToRemove.Count;
        }

        /// <summary>
        /// Removes devastated chunks within all active rift ward protection radii.
        /// Called periodically to maintain temporal stability in protected areas.
        /// </summary>
        private int RemoveDevastatedChunksInAllRiftWardRadii()
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0)
                return 0;

            int totalRemoved = 0;
            foreach (var ward in activeRiftWards)
            {
                if (ward.CachedIsActive)
                {
                    totalRemoved += RemoveDevastatedChunksInRiftWardRadius(ward);
                }
            }
            return totalRemoved;
        }

        /// <summary>
        /// Checks if a block position is protected by any active rift ward.
        /// </summary>
        private bool IsBlockProtectedByRiftWard(BlockPos pos)
        {
            if (pos == null || activeRiftWards == null || activeRiftWards.Count == 0) return false;

            // Quick chunk-level check first
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!protectedChunkKeys.Contains(chunkKey)) return false;

            // Detailed per-ward check
            int radiusSquared = config.RiftWardProtectionRadius * config.RiftWardProtectionRadius;

            foreach (var ward in activeRiftWards)
            {
                if (ward.Pos == null) continue;

                int dx = pos.X - ward.Pos.X;
                int dy = pos.Y - ward.Pos.Y;
                int dz = pos.Z - ward.Pos.Z;
                int distanceSquared = dx * dx + dy * dy + dz * dz;

                if (distanceSquared <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a chunk is protected by any active rift ward.
        /// This is a quick check that only looks at chunk-level protection.
        /// </summary>
        private bool IsChunkProtectedByRiftWard(int chunkX, int chunkZ)
        {
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);
            return protectedChunkKeys.Contains(chunkKey);
        }

        /// <summary>
        /// Checks if a block position has been cleansed by a rift ward's expanding shell.
        /// In raster mode, this respects the current shell radius.
        /// Used for determining if mob spawning and other effects should be blocked.
        /// </summary>
        private bool IsBlockCleansedByRiftWard(BlockPos pos)
        {
            if (pos == null || activeRiftWards == null || activeRiftWards.Count == 0) return false;

            string mode = GetEffectiveCleanMode();

            foreach (var ward in activeRiftWards)
            {
                if (ward.Pos == null || !ward.CachedIsActive) continue;

                int dx = pos.X - ward.Pos.X;
                int dy = pos.Y - ward.Pos.Y;
                int dz = pos.Z - ward.Pos.Z;
                int distanceSquared = dx * dx + dy * dy + dz * dz;

                // Determine the effective cleansed radius for this ward
                int cleansedRadius;
                if (mode == "raster")
                {
                    // In raster mode, use the current shell radius
                    cleansedRadius = ward.RasterScanComplete ? config.RiftWardProtectionRadius : ward.CurrentCleanRadius;
                }
                else if (mode == "radial")
                {
                    cleansedRadius = ward.MaxCleanRadiusReached;
                }
                else
                {
                    // Random mode - use full radius
                    cleansedRadius = config.RiftWardProtectionRadius;
                }

                if (distanceSquared <= cleansedRadius * cleansedRadius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a chunk has been cleansed by a rift ward's expanding shell.
        /// In raster mode, this respects the current shell radius.
        /// Used for determining if mob spawning and other effects should be blocked.
        /// </summary>
        private bool IsChunkCleansedByRiftWard(int chunkX, int chunkZ)
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0) return false;

            string mode = GetEffectiveCleanMode();
            int chunkCenterX = chunkX * CHUNK_SIZE + CHUNK_SIZE / 2;
            int chunkCenterZ = chunkZ * CHUNK_SIZE + CHUNK_SIZE / 2;

            foreach (var ward in activeRiftWards)
            {
                if (ward.Pos == null || !ward.CachedIsActive) continue;

                int dx = chunkCenterX - ward.Pos.X;
                int dz = chunkCenterZ - ward.Pos.Z;
                int distanceSquared = dx * dx + dz * dz;

                // Determine the effective cleansed radius for this ward
                int cleansedRadius;
                if (mode == "raster")
                {
                    cleansedRadius = ward.RasterScanComplete ? config.RiftWardProtectionRadius : ward.CurrentCleanRadius;
                }
                else if (mode == "radial")
                {
                    cleansedRadius = ward.MaxCleanRadiusReached;
                }
                else
                {
                    cleansedRadius = config.RiftWardProtectionRadius;
                }

                if (distanceSquared <= cleansedRadius * cleansedRadius)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// Client-side renderer that applies fog and sky color effects when the player is in devastated chunks.
    /// Creates a rusty, corrupted atmosphere similar to the base game Devastation area.
    /// </summary>
    public class DevastationFogRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private SpreadingDevastationModSystem modSystem;
        private AmbientModifier devastationAmbient;
        private bool isAmbientRegistered = false;

        // Config values (updated via UpdateConfig)
        private bool enabled = true;
        private float fogColorR = 0.55f;
        private float fogColorG = 0.25f;
        private float fogColorB = 0.15f;
        private float fogDensity = 0.004f;
        private float fogMin = 0.15f;
        private float fogColorWeight = 0.7f;
        private float fogDensityWeight = 0.5f;
        private float fogMinWeight = 0.6f;
        private float transitionSpeed = 2.0f; // 1/0.5 seconds

        // Current effect weight (0 = no effect, 1 = full effect)
        private float currentWeight = 0f;

        public double RenderOrder => 0.0; // Render early in the pipeline
        public int RenderRange => 0; // Not used

        public DevastationFogRenderer(ICoreClientAPI capi, SpreadingDevastationModSystem modSystem)
        {
            this.capi = capi;
            this.modSystem = modSystem;

            // Create ambient modifier for devastation effect
            // Must initialize ALL properties to avoid null reference exceptions in AmbientManager
            devastationAmbient = new AmbientModifier()
            {
                // Fog properties we want to modify
                FogColor = new WeightedFloatArray(new float[] { fogColorR, fogColorG, fogColorB, 1.0f }, 0),
                FogDensity = new WeightedFloat(fogDensity, 0),
                FogMin = new WeightedFloat(fogMin, 0),
                AmbientColor = new WeightedFloatArray(new float[] { fogColorR + 0.15f, fogColorG + 0.25f, fogColorB + 0.25f }, 0),

                // Other properties must be initialized with weight 0 to avoid null crashes
                FlatFogDensity = new WeightedFloat(0, 0),
                FlatFogYPos = new WeightedFloat(0, 0),
                CloudBrightness = new WeightedFloat(1, 0),
                CloudDensity = new WeightedFloat(0, 0),
                SceneBrightness = new WeightedFloat(1, 0),
                FogBrightness = new WeightedFloat(1, 0),
                LerpSpeed = new WeightedFloat(1, 0)
            };
        }

        /// <summary>
        /// Updates the fog configuration from server-sent config.
        /// </summary>
        public void UpdateConfig(SpreadingDevastationModSystem.FogConfigPacket config)
        {
            if (config == null) return;

            enabled = config.Enabled;
            fogColorR = config.ColorR;
            fogColorG = config.ColorG;
            fogColorB = config.ColorB;
            fogDensity = config.Density;
            fogMin = config.Min;
            fogColorWeight = config.ColorWeight;
            fogDensityWeight = config.DensityWeight;
            fogMinWeight = config.MinWeight;
            transitionSpeed = config.TransitionSpeed > 0 ? 1f / config.TransitionSpeed : 2f;

            // Update the ambient modifier values
            devastationAmbient.FogColor.Value = new float[] { fogColorR, fogColorG, fogColorB, 1.0f };
            devastationAmbient.FogDensity.Value = fogDensity;
            devastationAmbient.FogMin.Value = fogMin;
            devastationAmbient.AmbientColor.Value = new float[] {
                Math.Min(1f, fogColorR + 0.15f),
                Math.Min(1f, fogColorG + 0.25f),
                Math.Min(1f, fogColorB + 0.25f)
            };
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi?.World?.Player?.Entity == null) return;

            // Check if effect is enabled and player is in a devastated chunk
            bool inDevastatedChunk = enabled && modSystem.IsPlayerInDevastatedChunk();

            // Calculate target weight
            float targetWeight = inDevastatedChunk ? 1.0f : 0.0f;

            // Smoothly transition towards target weight
            if (currentWeight < targetWeight)
            {
                currentWeight = Math.Min(currentWeight + deltaTime * transitionSpeed, targetWeight);
            }
            else if (currentWeight > targetWeight)
            {
                currentWeight = Math.Max(currentWeight - deltaTime * transitionSpeed, targetWeight);
            }

            // Update ambient modifier weights based on config
            devastationAmbient.FogColor.Weight = currentWeight * fogColorWeight;
            devastationAmbient.FogDensity.Weight = currentWeight * fogDensityWeight;
            devastationAmbient.FogMin.Weight = currentWeight * fogMinWeight;
            devastationAmbient.AmbientColor.Weight = currentWeight * fogColorWeight * 0.5f; // Ambient is more subtle

            // Register or update the ambient modifier
            if (currentWeight > 0.001f)
            {
                if (!isAmbientRegistered)
                {
                    capi.Ambient.CurrentModifiers["devastation"] = devastationAmbient;
                    isAmbientRegistered = true;
                }
            }
            else
            {
                if (isAmbientRegistered)
                {
                    capi.Ambient.CurrentModifiers.Remove("devastation");
                    isAmbientRegistered = false;
                }
            }
        }

        public void Dispose()
        {
            if (isAmbientRegistered && capi?.Ambient?.CurrentModifiers != null)
            {
                capi.Ambient.CurrentModifiers.Remove("devastation");
                isAmbientRegistered = false;
            }
        }
    }

    /// <summary>
    /// Client-side renderer that draws a visible storm wall at the boundaries of devastated chunks.
    /// The wall rises from ground level with increasing opacity, creating a blurry fog barrier effect.
    /// Also spawns particles (ash, dust, lightning) near the player at boundaries.
    /// </summary>
    public class DevastationStormWallRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private SpreadingDevastationModSystem modSystem;
        private MeshRef wallMeshRef;
        private Matrixf modelMatrix = new Matrixf();
        private float animationTime = 0f;
        private Random rand = new Random();

        // Config values (updated via UpdateConfig)
        private bool enabled = true;
        private float wallHeight = 64f;
        private float baseOpacity = 0.05f;
        private float topOpacity = 0.4f;
        private float colorR = 0.6f;
        private float colorG = 0.3f;
        private float colorB = 0.15f;
        private float renderDistance = 256f;
        private float animationSpeed = 0.5f;
        private bool particlesEnabled = true;
        private float particleDistance = 48f;
        private int ashParticlesPerSecond = 15;
        private int dustParticlesPerSecond = 10;
        private float lightningChance = 0.1f;

        // Particle timing
        private float ashParticleAccum = 0f;
        private float dustParticleAccum = 0f;
        private float lightningAccum = 0f;

        // Cached boundary segments for rendering (rebuilt when chunks change)
        private List<BoundarySegment> boundarySegments = new List<BoundarySegment>();
        private HashSet<long> lastKnownChunks = new HashSet<long>();
        private const int CHUNK_SIZE = 32;

        // Wall mesh parameters
        private const int WALL_VERTICAL_SEGMENTS = 8; // Number of vertical divisions for gradient
        private const float WALL_THICKNESS = 0.5f; // Slight thickness for better visibility

        // Dome rendering - list of dome center positions and radii
        private List<DomeData> domes = new List<DomeData>();
        private float domeParticleAccum = 0f;

        public double RenderOrder => 0.98; // Render late, after most terrain
        public int RenderRange => 0;

        public struct DomeData
        {
            public double X, Y, Z; // Center position
            public float Radius; // Dome radius in blocks
        }

        private struct BoundarySegment
        {
            public float X1, Z1, X2, Z2; // Start and end points of the boundary edge
            public float BaseY; // Ground level at this segment
            public bool IsNorthSouth; // True if this is a N-S edge, false for E-W
        }

        public DevastationStormWallRenderer(ICoreClientAPI capi, SpreadingDevastationModSystem modSystem)
        {
            this.capi = capi;
            this.modSystem = modSystem;
        }

        /// <summary>
        /// Updates configuration from server-sent packet.
        /// </summary>
        public void UpdateConfig(SpreadingDevastationModSystem.StormWallConfigPacket config)
        {
            if (config == null) return;

            enabled = config.Enabled;
            wallHeight = config.Height;
            baseOpacity = config.BaseOpacity;
            topOpacity = config.TopOpacity;
            colorR = config.ColorR;
            colorG = config.ColorG;
            colorB = config.ColorB;
            renderDistance = config.RenderDistance;
            animationSpeed = config.AnimationSpeed;
            particlesEnabled = config.ParticlesEnabled;
            particleDistance = config.ParticleDistance;
            ashParticlesPerSecond = config.AshParticlesPerSecond;
            dustParticlesPerSecond = config.DustParticlesPerSecond;
            lightningChance = config.LightningChance;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!enabled || capi?.World?.Player?.Entity == null) return;

            var playerPos = capi.World.Player.Entity.Pos;

            // Update animation time
            animationTime += deltaTime * animationSpeed;

            // Always render domes if any exist
            if (domes.Count > 0)
            {
                SpawnDomeParticles(deltaTime, playerPos);
            }

            var devastatedChunks = modSystem.GetClientDevastatedChunks();
            if (devastatedChunks == null || devastatedChunks.Count == 0) return;

            // Check if chunks have changed and rebuild boundary segments if needed
            if (!ChunksMatch(devastatedChunks))
            {
                RebuildBoundarySegments(devastatedChunks);
                lastKnownChunks = new HashSet<long>(devastatedChunks);
            }

            // Spawn particles near player
            if (particlesEnabled)
            {
                SpawnBoundaryParticles(deltaTime, playerPos);
            }

            // Render the storm wall
            RenderStormWall(playerPos);
        }

        /// <summary>
        /// Adds a dome at the specified position with the given radius.
        /// </summary>
        public void AddDome(double x, double y, double z, float radius)
        {
            domes.Add(new DomeData { X = x, Y = y, Z = z, Radius = radius });
        }

        /// <summary>
        /// Removes the dome closest to the specified position.
        /// </summary>
        public bool RemoveNearestDome(double x, double y, double z)
        {
            if (domes.Count == 0) return false;

            int nearestIdx = 0;
            double nearestDistSq = double.MaxValue;
            for (int i = 0; i < domes.Count; i++)
            {
                double dx = domes[i].X - x;
                double dy = domes[i].Y - y;
                double dz = domes[i].Z - z;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestIdx = i;
                }
            }

            domes.RemoveAt(nearestIdx);
            return true;
        }

        /// <summary>
        /// Clears all domes.
        /// </summary>
        public void ClearDomes()
        {
            domes.Clear();
        }

        /// <summary>
        /// Gets the number of active domes.
        /// </summary>
        public int GetDomeCount()
        {
            return domes.Count;
        }

        /// <summary>
        /// Gets a copy of all dome data for display.
        /// </summary>
        public List<DomeData> GetDomes()
        {
            return new List<DomeData>(domes);
        }

        /// <summary>
        /// Spawns particles to create the dome/globe effect.
        /// </summary>
        private void SpawnDomeParticles(float deltaTime, EntityPos playerPos)
        {
            foreach (var dome in domes)
            {
                // Check if player is within render distance of this dome
                double dx = playerPos.X - dome.X;
                double dz = playerPos.Z - dome.Z;
                double distSq = dx * dx + dz * dz;

                // Render dome if within 2x the radius + render distance
                double maxDist = dome.Radius * 2 + renderDistance;
                if (distSq > maxDist * maxDist) continue;

                // Scale particle rate with dome surface area (2πR²)
                // For a 64 radius dome, we want ~500 particles/sec to be visible
                // Surface area at R=64 is ~25000, so rate = surfaceArea * 0.02
                float surfaceArea = 2f * (float)Math.PI * dome.Radius * dome.Radius;
                float particlesPerSecond = Math.Max(100f, surfaceArea * 0.02f);

                // Spawn particles for this dome
                domeParticleAccum += deltaTime * particlesPerSecond;

                while (domeParticleAccum >= 1f)
                {
                    domeParticleAccum -= 1f;
                    SpawnSingleDomeParticle(dome, playerPos);
                }
            }
        }

        /// <summary>
        /// Spawns a single particle on the dome/globe surface.
        /// </summary>
        private void SpawnSingleDomeParticle(DomeData dome, EntityPos playerPos)
        {
            // Generate random point on full sphere surface using spherical coordinates
            float theta = (float)(rand.NextDouble() * Math.PI * 2); // 0 to 2π (around)
            float phi = (float)(rand.NextDouble() * Math.PI); // 0 to π (full sphere)

            // Convert spherical to cartesian coordinates
            float sinPhi = (float)Math.Sin(phi);
            float cosPhi = (float)Math.Cos(phi);
            float sinTheta = (float)Math.Sin(theta);
            float cosTheta = (float)Math.Cos(theta);

            // Position on dome surface with some thickness variation
            float radiusVariation = 1f + (float)(rand.NextDouble() - 0.5) * 0.1f;
            float actualRadius = dome.Radius * radiusVariation;
            double x = dome.X + actualRadius * sinPhi * cosTheta;
            double y = dome.Y + actualRadius * cosPhi; // Y is up
            double z = dome.Z + actualRadius * sinPhi * sinTheta;

            // Particles drift slowly outward and have slight swirl
            float outwardSpeed = 0.01f;
            float swirlSpeed = 0.005f;

            // Opacity varies - more opaque near equator (where wall is visible from outside)
            float distFromEquator = Math.Abs(cosPhi); // 0 at equator, 1 at poles
            float equatorBias = 1f - distFromEquator; // 1 at equator, 0 at poles
            int alpha = (int)(80 + equatorBias * 120); // 80-200 range, brightest at equator

            // Add some color variation - rusty orange colors
            float rVar = colorR + (float)(rand.NextDouble() - 0.5) * 0.15f;
            float gVar = colorG + (float)(rand.NextDouble() - 0.5) * 0.1f;
            float bVar = colorB + (float)(rand.NextDouble() - 0.5) * 0.05f;

            // Scale particle size with dome radius for better visibility
            float sizeScale = Math.Max(1f, dome.Radius / 32f);

            SimpleParticleProperties props = new SimpleParticleProperties
            {
                MinPos = new Vec3d(x, y, z),
                AddPos = new Vec3d(3 * sizeScale, 3 * sizeScale, 3 * sizeScale),
                MinVelocity = new Vec3f(
                    sinPhi * cosTheta * outwardSpeed - sinTheta * swirlSpeed,
                    0.005f,
                    sinPhi * sinTheta * outwardSpeed + cosTheta * swirlSpeed
                ),
                AddVelocity = new Vec3f(0.01f, 0.01f, 0.01f),
                MinSize = 3.0f * sizeScale,
                MaxSize = 8.0f * sizeScale,
                MinQuantity = 1,
                AddQuantity = 0,
                LifeLength = 8f, // Longer life for fog buildup
                GravityEffect = -0.001f, // Very slight upward float
                Color = ColorUtil.ToRgba(alpha, (int)(rVar * 255), (int)(gVar * 255), (int)(bVar * 255)),
                ParticleModel = EnumParticleModel.Quad,
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -0.1f),
                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 0.5f)
            };

            capi.World.SpawnParticles(props);
        }

        private bool ChunksMatch(HashSet<long> current)
        {
            if (current.Count != lastKnownChunks.Count) return false;
            foreach (var key in current)
            {
                if (!lastKnownChunks.Contains(key)) return false;
            }
            return true;
        }

        /// <summary>
        /// Rebuilds the list of boundary segments between devastated and non-devastated chunks.
        /// </summary>
        private void RebuildBoundarySegments(HashSet<long> devastatedChunks)
        {
            boundarySegments.Clear();

            foreach (long chunkKey in devastatedChunks)
            {
                int chunkX = (int)(chunkKey >> 32);
                int chunkZ = (int)(chunkKey & 0xFFFFFFFF);

                // Check all four adjacent chunks
                // North edge (Z-)
                long northKey = SpreadingDevastationModSystem.DevastatedChunk.MakeChunkKey(chunkX, chunkZ - 1);
                if (!devastatedChunks.Contains(northKey))
                {
                    float worldX = chunkX * CHUNK_SIZE;
                    float worldZ = chunkZ * CHUNK_SIZE;
                    boundarySegments.Add(new BoundarySegment
                    {
                        X1 = worldX,
                        Z1 = worldZ,
                        X2 = worldX + CHUNK_SIZE,
                        Z2 = worldZ,
                        BaseY = GetGroundLevel(worldX + CHUNK_SIZE / 2, worldZ),
                        IsNorthSouth = false
                    });
                }

                // South edge (Z+)
                long southKey = SpreadingDevastationModSystem.DevastatedChunk.MakeChunkKey(chunkX, chunkZ + 1);
                if (!devastatedChunks.Contains(southKey))
                {
                    float worldX = chunkX * CHUNK_SIZE;
                    float worldZ = (chunkZ + 1) * CHUNK_SIZE;
                    boundarySegments.Add(new BoundarySegment
                    {
                        X1 = worldX,
                        Z1 = worldZ,
                        X2 = worldX + CHUNK_SIZE,
                        Z2 = worldZ,
                        BaseY = GetGroundLevel(worldX + CHUNK_SIZE / 2, worldZ),
                        IsNorthSouth = false
                    });
                }

                // West edge (X-)
                long westKey = SpreadingDevastationModSystem.DevastatedChunk.MakeChunkKey(chunkX - 1, chunkZ);
                if (!devastatedChunks.Contains(westKey))
                {
                    float worldX = chunkX * CHUNK_SIZE;
                    float worldZ = chunkZ * CHUNK_SIZE;
                    boundarySegments.Add(new BoundarySegment
                    {
                        X1 = worldX,
                        Z1 = worldZ,
                        X2 = worldX,
                        Z2 = worldZ + CHUNK_SIZE,
                        BaseY = GetGroundLevel(worldX, worldZ + CHUNK_SIZE / 2),
                        IsNorthSouth = true
                    });
                }

                // East edge (X+)
                long eastKey = SpreadingDevastationModSystem.DevastatedChunk.MakeChunkKey(chunkX + 1, chunkZ);
                if (!devastatedChunks.Contains(eastKey))
                {
                    float worldX = (chunkX + 1) * CHUNK_SIZE;
                    float worldZ = chunkZ * CHUNK_SIZE;
                    boundarySegments.Add(new BoundarySegment
                    {
                        X1 = worldX,
                        Z1 = worldZ,
                        X2 = worldX,
                        Z2 = worldZ + CHUNK_SIZE,
                        BaseY = GetGroundLevel(worldX, worldZ + CHUNK_SIZE / 2),
                        IsNorthSouth = true
                    });
                }
            }
        }

        private float GetGroundLevel(float x, float z)
        {
            // Use sea level as base height - more performant than querying terrain
            // The wall will start from a reasonable baseline and rise up
            return capi.World.SeaLevel;
        }

        /// <summary>
        /// Renders the storm wall effect at all boundary segments within range.
        /// </summary>
        private void RenderStormWall(EntityPos playerPos)
        {
            if (boundarySegments.Count == 0) return;

            var renderApi = capi.Render;
            var shader = renderApi.StandardShader;

            shader.Use();
            shader.Uniform("rgbaFogIn", capi.Ambient.BlendedFogColor);
            shader.Uniform("fogMinIn", capi.Ambient.BlendedFogMin);
            shader.Uniform("fogDensityIn", capi.Ambient.BlendedFogDensity);
            shader.ExtraGodray = 0;
            shader.RgbaAmbientIn = capi.Ambient.BlendedAmbientColor;
            shader.RgbaLightIn = new Vec4f(1, 1, 1, 1);
            shader.RgbaTint = new Vec4f(1, 1, 1, 1);
            shader.DontWarpVertices = 0;
            shader.AddRenderFlags = 0;
            shader.NormalShaded = 0;

            // Enable blending for transparency
            renderApi.GlToggleBlend(true, EnumBlendMode.Standard);

            float renderDistSq = renderDistance * renderDistance;

            foreach (var segment in boundarySegments)
            {
                // Calculate distance to segment center
                float segCenterX = (segment.X1 + segment.X2) / 2f;
                float segCenterZ = (segment.Z1 + segment.Z2) / 2f;
                float dx = (float)playerPos.X - segCenterX;
                float dz = (float)playerPos.Z - segCenterZ;
                float distSq = dx * dx + dz * dz;

                if (distSq > renderDistSq) continue;

                // Distance-based fade
                float distFade = 1f - (distSq / renderDistSq) * 0.5f;

                // Render this wall segment
                RenderWallSegment(segment, playerPos, distFade);
            }

            shader.Stop();
        }

        /// <summary>
        /// Renders a single wall segment as a vertical quad with gradient transparency.
        /// </summary>
        private void RenderWallSegment(BoundarySegment segment, EntityPos playerPos, float distFade)
        {
            int vertexCount = 2 * (WALL_VERTICAL_SEGMENTS + 1);
            int indexCount = 6 * WALL_VERTICAL_SEGMENTS * 2; // Double for both faces

            // Pre-allocate arrays for mesh data
            float[] xyz = new float[vertexCount * 3];
            float[] uv = new float[vertexCount * 2];
            byte[] rgba = new byte[vertexCount * 4]; // VS uses 4 bytes per vertex (R, G, B, A)
            int[] indices = new int[indexCount];

            float segmentHeight = wallHeight / WALL_VERTICAL_SEGMENTS;

            // Animation offset based on position and time
            float animOffset = (float)Math.Sin(animationTime + segment.X1 * 0.1f + segment.Z1 * 0.1f) * 0.5f;

            int vertIdx = 0;
            for (int i = 0; i <= WALL_VERTICAL_SEGMENTS; i++)
            {
                float t = (float)i / WALL_VERTICAL_SEGMENTS;
                float y = segment.BaseY + i * segmentHeight;

                // Gradient opacity: more transparent at bottom, more opaque at top
                float opacity = baseOpacity + (topOpacity - baseOpacity) * t * t; // Quadratic for more dramatic rise
                opacity *= distFade;

                // Add some wave animation
                float waveOffset = (float)Math.Sin(animationTime * 2f + t * 3f + animOffset) * 0.3f;

                // Vary color slightly with height for depth
                float r = Math.Min(1f, colorR + t * 0.1f + waveOffset * 0.05f);
                float g = Math.Min(1f, colorG + t * 0.05f);
                float b = Math.Min(1f, colorB + t * 0.05f);

                byte rByte = (byte)(r * 255);
                byte gByte = (byte)(g * 255);
                byte bByte = (byte)(b * 255);
                byte aByte = (byte)(opacity * 255);

                // First vertex (start of segment)
                int v1 = vertIdx * 3;
                xyz[v1] = segment.X1;
                xyz[v1 + 1] = y;
                xyz[v1 + 2] = segment.Z1;
                uv[vertIdx * 2] = 0;
                uv[vertIdx * 2 + 1] = t;
                // RGBA byte order
                rgba[vertIdx * 4] = rByte;
                rgba[vertIdx * 4 + 1] = gByte;
                rgba[vertIdx * 4 + 2] = bByte;
                rgba[vertIdx * 4 + 3] = aByte;
                vertIdx++;

                // Second vertex (end of segment)
                int v2 = vertIdx * 3;
                xyz[v2] = segment.X2;
                xyz[v2 + 1] = y;
                xyz[v2 + 2] = segment.Z2;
                uv[vertIdx * 2] = 1;
                uv[vertIdx * 2 + 1] = t;
                // RGBA byte order
                rgba[vertIdx * 4] = rByte;
                rgba[vertIdx * 4 + 1] = gByte;
                rgba[vertIdx * 4 + 2] = bByte;
                rgba[vertIdx * 4 + 3] = aByte;
                vertIdx++;
            }

            // Build indices for triangles (both front and back faces for double-sided rendering)
            int idxPtr = 0;
            for (int i = 0; i < WALL_VERTICAL_SEGMENTS; i++)
            {
                int baseIdx = i * 2;
                // Front face (counter-clockwise)
                indices[idxPtr++] = baseIdx;
                indices[idxPtr++] = baseIdx + 2;
                indices[idxPtr++] = baseIdx + 1;
                indices[idxPtr++] = baseIdx + 1;
                indices[idxPtr++] = baseIdx + 2;
                indices[idxPtr++] = baseIdx + 3;
                // Back face (clockwise)
                indices[idxPtr++] = baseIdx;
                indices[idxPtr++] = baseIdx + 1;
                indices[idxPtr++] = baseIdx + 2;
                indices[idxPtr++] = baseIdx + 1;
                indices[idxPtr++] = baseIdx + 3;
                indices[idxPtr++] = baseIdx + 2;
            }

            // Create mesh data with vertex colors
            MeshData mesh = new MeshData(vertexCount, indexCount, false, true, true, false);
            mesh.SetMode(EnumDrawMode.Triangles);
            mesh.SetXyz(xyz);
            mesh.SetUv(uv);
            mesh.SetRgba(rgba);
            mesh.SetIndices(indices);
            mesh.VerticesCount = vertexCount;
            mesh.IndicesCount = indexCount;

            // Upload and render
            if (wallMeshRef != null)
            {
                capi.Render.DeleteMesh(wallMeshRef);
            }
            wallMeshRef = capi.Render.UploadMesh(mesh);

            // Bind a white texture so the shader works (uses vertex colors)
            capi.Render.BindTexture2d(0);

            // Set up model matrix - translate from world coords to camera-relative coords
            modelMatrix.Identity();
            modelMatrix.Translate(
                (float)(-playerPos.X + capi.World.Player.Entity.CameraPos.X),
                (float)(-playerPos.Y + capi.World.Player.Entity.CameraPos.Y),
                (float)(-playerPos.Z + capi.World.Player.Entity.CameraPos.Z)
            );

            capi.Render.StandardShader.ModelMatrix = modelMatrix.Values;
            capi.Render.StandardShader.ViewMatrix = capi.Render.CameraMatrixOriginf;
            capi.Render.StandardShader.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

            capi.Render.RenderMesh(wallMeshRef);
        }

        /// <summary>
        /// Spawns particles near the player at devastation boundaries.
        /// Creates a visible fog wall effect using dense particle columns.
        /// </summary>
        private void SpawnBoundaryParticles(float deltaTime, EntityPos playerPos)
        {
            float particleDistSq = particleDistance * particleDistance;

            // Spawn fog wall particles on ALL nearby boundary segments
            // This creates the visible storm wall effect
            foreach (var segment in boundarySegments)
            {
                float segCenterX = (segment.X1 + segment.X2) / 2f;
                float segCenterZ = (segment.Z1 + segment.Z2) / 2f;
                float dx = (float)playerPos.X - segCenterX;
                float dz = (float)playerPos.Z - segCenterZ;
                float distSq = dx * dx + dz * dz;

                if (distSq > particleDistSq) continue;

                // Spawn dense fog wall particles along this segment
                // More particles closer to player, fewer further away
                float distFactor = 1f - (distSq / particleDistSq);
                int wallParticlesThisTick = (int)(20 * distFactor * deltaTime); // Dense fog particles

                for (int i = 0; i < wallParticlesThisTick; i++)
                {
                    SpawnFogWallParticle(segment, playerPos);
                }
            }

            // Find nearest segment for the special effect particles (ash, dust, lightning)
            BoundarySegment? nearestSegment = null;
            float nearestDistSq = particleDistSq;

            foreach (var segment in boundarySegments)
            {
                float segCenterX = (segment.X1 + segment.X2) / 2f;
                float segCenterZ = (segment.Z1 + segment.Z2) / 2f;
                float dx = (float)playerPos.X - segCenterX;
                float dz = (float)playerPos.Z - segCenterZ;
                float distSq = dx * dx + dz * dz;

                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestSegment = segment;
                }
            }

            if (!nearestSegment.HasValue) return;
            var seg = nearestSegment.Value;

            // Spawn ash particles
            ashParticleAccum += deltaTime * ashParticlesPerSecond;
            while (ashParticleAccum >= 1f)
            {
                ashParticleAccum -= 1f;
                SpawnAshParticle(seg, playerPos);
            }

            // Spawn dust particles
            dustParticleAccum += deltaTime * dustParticlesPerSecond;
            while (dustParticleAccum >= 1f)
            {
                dustParticleAccum -= 1f;
                SpawnDustParticle(seg, playerPos);
            }

            // Spawn lightning occasionally
            lightningAccum += deltaTime;
            if (lightningAccum >= 1f)
            {
                lightningAccum = 0f;
                if (rand.NextDouble() < lightningChance)
                {
                    SpawnLightningEffect(seg, playerPos);
                }
            }
        }

        /// <summary>
        /// Spawns a fog particle that forms part of the visible storm wall.
        /// </summary>
        private void SpawnFogWallParticle(BoundarySegment segment, EntityPos playerPos)
        {
            // Random position along the boundary segment
            float t = (float)rand.NextDouble();
            float x = segment.X1 + (segment.X2 - segment.X1) * t;
            float z = segment.Z1 + (segment.Z2 - segment.Z1) * t;

            // Height varies from ground to wall height, weighted toward upper portions
            float heightT = (float)rand.NextDouble();
            heightT = heightT * heightT; // Square for more particles higher up
            float y = segment.BaseY + heightT * wallHeight;

            // Opacity increases with height (matching the gradient we wanted for the mesh)
            float heightFraction = heightT;
            float particleOpacity = baseOpacity + (topOpacity - baseOpacity) * heightFraction;

            // Clamp to reasonable alpha range for visibility
            int alpha = (int)(particleOpacity * 200 + 55); // 55-255 range
            alpha = Math.Min(255, Math.Max(55, alpha));

            // Vary color slightly
            float rVar = colorR + (float)(rand.NextDouble() - 0.5) * 0.1f;
            float gVar = colorG + (float)(rand.NextDouble() - 0.5) * 0.1f;
            float bVar = colorB + (float)(rand.NextDouble() - 0.5) * 0.05f;

            // ColorUtil.ToRgba takes (alpha, red, green, blue) - ARGB format
            SimpleParticleProperties props = new SimpleParticleProperties
            {
                MinPos = new Vec3d(x, y, z),
                AddPos = new Vec3d(3, 2, 3), // Spread out a bit
                MinVelocity = new Vec3f(-0.05f, 0.02f, -0.05f), // Very slow drift
                AddVelocity = new Vec3f(0.1f, 0.04f, 0.1f),
                MinSize = 1.5f, // Larger particles for fog effect
                MaxSize = 3.0f,
                MinQuantity = 1,
                AddQuantity = 0,
                LifeLength = 4f, // Longer life for persistent wall effect
                GravityEffect = -0.005f, // Very slight upward drift
                Color = ColorUtil.ToRgba(alpha, (int)(rVar * 255), (int)(gVar * 255), (int)(bVar * 255)),
                ParticleModel = EnumParticleModel.Quad,
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.2f), // Slow fade
                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 0.5f) // Grow slightly
            };

            capi.World.SpawnParticles(props);
        }

        private void SpawnAshParticle(BoundarySegment segment, EntityPos playerPos)
        {
            // Spawn along the boundary segment
            float t = (float)rand.NextDouble();
            float x = segment.X1 + (segment.X2 - segment.X1) * t;
            float z = segment.Z1 + (segment.Z2 - segment.Z1) * t;
            float y = segment.BaseY + (float)rand.NextDouble() * wallHeight * 0.5f;

            // ColorUtil.ToRgba takes (alpha, red, green, blue) - ARGB format
            SimpleParticleProperties props = new SimpleParticleProperties
            {
                MinPos = new Vec3d(x, y, z),
                AddPos = new Vec3d(2, 2, 2),
                MinVelocity = new Vec3f(-0.2f, 0.3f, -0.2f),
                AddVelocity = new Vec3f(0.4f, 0.3f, 0.4f),
                MinSize = 0.1f,
                MaxSize = 0.3f,
                MinQuantity = 1,
                AddQuantity = 1,
                LifeLength = 2f,
                GravityEffect = -0.02f, // Float upward slightly
                Color = ColorUtil.ToRgba(200, 255, 120, 40), // Orange-red ash (A=200, R=255, G=120, B=40)
                ParticleModel = EnumParticleModel.Quad,
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.5f)
            };

            capi.World.SpawnParticles(props);
        }

        private void SpawnDustParticle(BoundarySegment segment, EntityPos playerPos)
        {
            float t = (float)rand.NextDouble();
            float x = segment.X1 + (segment.X2 - segment.X1) * t;
            float z = segment.Z1 + (segment.Z2 - segment.Z1) * t;
            float y = segment.BaseY + (float)rand.NextDouble() * 10f;

            // Swirling motion
            float angle = (float)(animationTime * 2f + t * Math.PI * 2);
            float swirl = 0.3f;

            // ColorUtil.ToRgba takes (alpha, red, green, blue) - ARGB format
            SimpleParticleProperties props = new SimpleParticleProperties
            {
                MinPos = new Vec3d(x, y, z),
                AddPos = new Vec3d(4, 2, 4),
                MinVelocity = new Vec3f((float)Math.Sin(angle) * swirl, 0.1f, (float)Math.Cos(angle) * swirl),
                AddVelocity = new Vec3f(0.2f, 0.2f, 0.2f),
                MinSize = 0.2f,
                MaxSize = 0.5f,
                MinQuantity = 1,
                AddQuantity = 1,
                LifeLength = 3f,
                GravityEffect = 0.01f,
                Color = ColorUtil.ToRgba(180, 160, 100, 60), // Brown-rust dust (A=180, R=160, G=100, B=60)
                ParticleModel = EnumParticleModel.Quad,
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.3f)
            };

            capi.World.SpawnParticles(props);
        }

        private void SpawnLightningEffect(BoundarySegment segment, EntityPos playerPos)
        {
            // Pick a random point along the segment
            float t = (float)rand.NextDouble();
            float x = segment.X1 + (segment.X2 - segment.X1) * t;
            float z = segment.Z1 + (segment.Z2 - segment.Z1) * t;
            float y = segment.BaseY + wallHeight * 0.3f + (float)rand.NextDouble() * wallHeight * 0.5f;

            // Create a burst of bright particles for lightning
            for (int i = 0; i < 15; i++)
            {
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                float speed = 0.5f + (float)rand.NextDouble() * 1f;

                // ColorUtil.ToRgba takes (alpha, red, green, blue) - ARGB format
                SimpleParticleProperties props = new SimpleParticleProperties
                {
                    MinPos = new Vec3d(x, y, z),
                    AddPos = new Vec3d(1, 1, 1),
                    MinVelocity = new Vec3f((float)Math.Sin(angle) * speed, (float)(rand.NextDouble() - 0.5) * speed, (float)Math.Cos(angle) * speed),
                    AddVelocity = new Vec3f(0.3f, 0.3f, 0.3f),
                    MinSize = 0.1f,
                    MaxSize = 0.25f,
                    MinQuantity = 1,
                    AddQuantity = 0,
                    LifeLength = 0.3f,
                    GravityEffect = 0,
                    Color = ColorUtil.ToRgba(255, 255, 240, 200), // Bright white-yellow (A=255, R=255, G=240, B=200)
                    ParticleModel = EnumParticleModel.Quad,
                    SelfPropelled = true
                };

                capi.World.SpawnParticles(props);
            }

            // Optional: Play a subtle crackle sound
            // capi.World.PlaySoundAt(new AssetLocation("sounds/effect/electricity"), x, y, z, null, false, 16f, 0.5f);
        }

        public void Dispose()
        {
            if (wallMeshRef != null)
            {
                capi.Render.DeleteMesh(wallMeshRef);
                wallMeshRef = null;
            }
        }
    }
}
