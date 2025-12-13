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
        
        /// <summary>Minimum Y level for new metastasis sources (default: -999)</summary>
        public int MinYLevel { get; set; } = -999;
        
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
        /// Does NOT restrict which blocks can be devastated. (default: false)
        /// </summary>
        public bool RequireSourceAirContact { get; set; } = false;
        
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

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            
            // Load config
            LoadConfig();
            
            // Check for manual sources every 10ms (100 times per second)
            api.Event.RegisterGameTickListener(SpreadDevastationFromRifts, 10);

            // Process devastated chunks (spawning and rapid spreading) every 500ms
            api.Event.RegisterGameTickListener(ProcessDevastatedChunks, 500);

            // Process rift wards (check active state and healing) every 500ms
            api.Event.RegisterGameTickListener(ProcessRiftWards, 500);

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
        /// Sends multi-line command output as individual chat messages so the in-game
        /// chat log can be scrolled, while still returning a result for consoles.
        /// </summary>
        private TextCommandResult SendChatLines(TextCommandCallingArgs args, IEnumerable<string> lines, string playerAck = "Details sent to chat (scroll to view)")
        {
            var player = args?.Caller?.Player as IServerPlayer;
            var safeLines = lines?.Where(l => !string.IsNullOrWhiteSpace(l)).ToList() ?? new List<string>();

            if (player != null)
            {
                foreach (string line in safeLines)
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, line, EnumChatType.CommandSuccess);
                }

                return TextCommandResult.Success(playerAck);
            }

            return TextCommandResult.Success(string.Join("\n", safeLines));
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
                              api.ChatCommands.Parsers.OptionalWord("value"))
                    .HandleWith(HandleChunkCommand)
                .EndSubCommand()
                .BeginSubCommand("riftward")
                    .WithDescription("Configure rift ward settings (speed, list, info)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalWord("value"))
                    .HandleWith(HandleRiftWardCommand)
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
                    "Usage: /dv speed <multiplier> (e.g., 0.5 for half speed, 5 for 5x speed)"
                }, "Speed info sent to chat (scrollable)");
            }

            if (!double.TryParse(rawArg, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv speed <multiplier> (e.g., 0.5, 1, 5)");
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

                case "":
                case "info":
                case "status":
                    double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
                    string speedSource = config.RiftWardSpeedMultiplier > 0 ? "custom" : "global";
                    return SendChatLines(args, new[]
                    {
                        "=== Rift Ward Settings ===",
                        $"Protection radius: {config.RiftWardProtectionRadius} blocks",
                        $"Healing enabled: {config.RiftWardHealingEnabled}",
                        $"Base healing rate: {config.RiftWardHealingRate:F1} blocks/sec",
                        $"Speed multiplier: {effectiveSpeed:F2}x ({speedSource})",
                        $"Effective rate: {config.RiftWardHealingRate * effectiveSpeed:F1} blocks/sec",
                        $"Active rift wards: {activeRiftWards?.Count ?? 0}",
                        "",
                        "Commands:",
                        "  /dv riftward speed <multiplier> - Set healing speed (or 'global' to use /dv speed)",
                        "  /dv riftward rate <blocks/sec> - Set base healing rate",
                        "  /dv riftward list - Show all tracked rift wards"
                    }, "Rift ward info sent to chat");

                default:
                    return TextCommandResult.Error($"Unknown riftward action: {action}. Use: speed, rate, list, or info");
            }
        }

        private TextCommandResult HandleRiftWardSpeedCommand(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
                string speedSource = config.RiftWardSpeedMultiplier > 0 ? "custom" : "global";
                return TextCommandResult.Success($"Rift ward healing speed: {effectiveSpeed:F2}x ({speedSource}). Use '/dv riftward speed <multiplier>' to set, or 'global' to use devastation speed.");
            }

            if (value.ToLowerInvariant() == "global" || value.ToLowerInvariant() == "default")
            {
                config.RiftWardSpeedMultiplier = -1;
                SaveConfig();
                return TextCommandResult.Success($"Rift ward healing now uses global devastation speed ({config.SpeedMultiplier:F2}x)");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv riftward speed <multiplier> (e.g., 5, 10, or 'global')");
            }

            double newSpeed = Math.Clamp(parsedSpeed, 0.01, 1000.0);
            config.RiftWardSpeedMultiplier = newSpeed;
            SaveConfig();
            return TextCommandResult.Success($"Rift ward healing speed set to {config.RiftWardSpeedMultiplier:F2}x (effective rate: {config.RiftWardHealingRate * newSpeed:F1} blocks/sec)");
        }

        private TextCommandResult HandleRiftWardRateCommand(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TextCommandResult.Success($"Rift ward base healing rate: {config.RiftWardHealingRate:F1} blocks/sec. Use '/dv riftward rate <blocks>' to set.");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRate))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv riftward rate <blocks/sec> (e.g., 10, 50, 100)");
            }

            double newRate = Math.Clamp(parsedRate, 0.1, 10000.0);
            config.RiftWardHealingRate = newRate;
            SaveConfig();
            double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
            return TextCommandResult.Success($"Rift ward base healing rate set to {config.RiftWardHealingRate:F1} blocks/sec (effective: {config.RiftWardHealingRate * effectiveSpeed:F1} blocks/sec)");
        }

        private TextCommandResult HandleRiftWardListCommand(TextCommandCallingArgs args)
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0)
            {
                return TextCommandResult.Success("No rift wards are currently tracked.");
            }

            var lines = new List<string> { $"=== Tracked Rift Wards ({activeRiftWards.Count}) ===" };
            foreach (var ward in activeRiftWards)
            {
                bool isActive = IsRiftWardActive(ward.Pos);
                string status = isActive ? "ACTIVE" : "inactive";
                lines.Add($"  {ward.Pos} - {status}, healed {ward.BlocksHealed} blocks");
            }
            return SendChatLines(args, lines, "Rift ward list sent to chat");
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
                    "Usage: /devastate maxsources <number> (e.g., 20, 50, 100)"
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
                    "Usage: /devastate maxattempts <number> (e.g., 5, 10, 20)"
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
                    "Usage: /devastate miny <level> (e.g., 0, -64, 50)"
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
                        "  /dv chunk spawn interval <min> <max> - Set random interval range (hours)",
                        "  /dv chunk spawn cooldown <hours> - Set cooldown after spawn",
                        "  /dv chunk spawn distance <min> <max> - Set spawn distance from player",
                        "  /dv chunk spawn maxmobs <count> - Set max mobs per chunk",
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
                            "Usage: /dv chunk spawn interval <min> <max>",
                            "Example: /dv chunk spawn interval 0.5 1.0 (30-60 in-game minutes)"
                        }, "Interval info sent to chat");
                    }

                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double minHours) ||
                        !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double maxHours))
                    {
                        return TextCommandResult.Error("Invalid numbers. Usage: /dv chunk spawn interval <min> <max>");
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
                            "Usage: /dv chunk spawn cooldown <hours>",
                            "Example: /dv chunk spawn cooldown 4 (4 hour minimum between spawns)"
                        }, "Cooldown info sent to chat");
                    }

                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double cooldown))
                    {
                        return TextCommandResult.Error("Invalid number. Usage: /dv chunk spawn cooldown <hours>");
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
                            "Usage: /dv chunk spawn distance <min> <max>",
                            "Example: /dv chunk spawn distance 16 48"
                        }, "Distance info sent to chat");
                    }

                    if (!int.TryParse(parts[1], out int minDist) || !int.TryParse(parts[2], out int maxDist))
                    {
                        return TextCommandResult.Error("Invalid numbers. Usage: /dv chunk spawn distance <min> <max>");
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
                            "Usage: /dv chunk spawn maxmobs <count>",
                            "Example: /dv chunk spawn maxmobs 5"
                        }, "Max mobs info sent to chat");
                    }

                    if (!int.TryParse(parts[1], out int maxMobs))
                    {
                        return TextCommandResult.Error("Invalid number. Usage: /dv chunk spawn maxmobs <count>");
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
                        "Usage: /dv chunk drain <rate> (e.g., 0.001 for ~0.2%/sec, 0.01 for ~2%/sec)"
                    }, "Drain rate info sent to chat");
                }

                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double rate))
                {
                    return TextCommandResult.Error("Invalid number. Usage: /dv chunk drain <rate>");
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
                        "Usage: /dv chunk spreadchance <percent> (e.g., 5 for 5%)"
                    }, "Spread chance info sent to chat");
                }

                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                {
                    return TextCommandResult.Error("Invalid number. Usage: /dv chunk spreadchance <percent>");
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
                        "  /dv chunk drain <rate> - Set stability drain rate",
                        "  /dv chunk spread [on|off] - Toggle chunk spreading",
                        "  /dv chunk spreadchance <percent> - Set spread chance"
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
        /// No longer scans for new rift wards - that's handled by block placement events.
        /// </summary>
        private void ProcessRiftWards(float dt)
        {
            if (sapi == null || activeRiftWards == null || activeRiftWards.Count == 0) return;

            double currentTime = sapi.World.Calendar.TotalHours;
            double checkIntervalHours = config.RiftWardScanIntervalSeconds / 3600.0;

            // Periodically verify rift wards are still active (have fuel) and remove nearby sources
            if (currentTime - lastRiftWardScanTime >= checkIntervalHours)
            {
                lastRiftWardScanTime = currentTime;
                VerifyRiftWardActiveState();

                // Also remove any newly spawned devastation sources within rift ward radii
                // This catches sources that spawn from rifts after the ward was placed
                RemoveSourcesInAllRiftWardRadii();
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

                // If ward just became active, notify and remove nearby sources
                if (isActive && ward.BlocksHealed == 0 && ward.LastHealTime == 0)
                {
                    BroadcastMessage($"Rift ward at {ward.Pos} is now ACTIVE and protecting!");
                    int removedSources = RemoveSourcesInRiftWardRadius(ward);
                    if (removedSources > 0)
                    {
                        BroadcastMessage($"Rift ward neutralized {removedSources} devastation source(s)!");
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
        /// Checks if a rift ward at the given position is active (has fuel and is on).
        /// Uses reflection to access the BlockEntityRiftWard properties since it's in the game DLL.
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

                // Use reflection to check the "On" property
                var onProperty = blockEntity.GetType().GetProperty("On");
                if (onProperty != null)
                {
                    var isOn = onProperty.GetValue(blockEntity);
                    if (isOn is bool onValue && onValue)
                    {
                        return true;
                    }
                }

                // Fallback: check HasFuel property
                var hasFuelProperty = blockEntity.GetType().GetProperty("HasFuel");
                if (hasFuelProperty != null)
                {
                    var hasFuel = hasFuelProperty.GetValue(blockEntity);
                    if (hasFuel is bool fuelValue && fuelValue)
                    {
                        return true;
                    }
                }

                // Also check fuelDays field directly as another fallback
                var fuelDaysField = blockEntity.GetType().GetField("fuelDays",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (fuelDaysField != null)
                {
                    var fuelDays = fuelDaysField.GetValue(blockEntity);
                    if (fuelDays is double daysValue && daysValue > 0)
                    {
                        return true;
                    }
                    if (fuelDays is float floatDays && floatDays > 0)
                    {
                        return true;
                    }
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
                }
            }
        }

        /// <summary>
        /// Heals devastated blocks within the rift ward's protection radius.
        /// </summary>
        private int HealBlocksAroundRiftWard(RiftWard ward, int blocksToHeal)
        {
            if (ward.Pos == null) return 0;

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

        #endregion
    }
}
