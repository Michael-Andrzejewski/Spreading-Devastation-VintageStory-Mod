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
    // SpreadingDevastationConfig class moved to SpreadingDevastationConfig.cs

    public partial class SpreadingDevastationModSystem : ModSystem
    {
        // Data classes moved to DataClasses.cs:
        // RegrowingBlocks, DevastationSource, DevastatedChunk, BleedBlock, RiftWard,
        // DevastatedChunkSyncPacket, FogConfigPacket, TestStatus, TestResult, TestContext

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

        // Temporal stability system hook for gear visual (server and client)
        private SystemTemporalStability temporalStabilitySystemServer;
        private SystemTemporalStability temporalStabilitySystemClient;

        // Client-side fields
        private ICoreClientAPI capi;
        private IClientNetworkChannel clientNetworkChannel;
        private HashSet<long> clientDevastatedChunks = new HashSet<long>(); // Chunk keys received from server
        private DevastationFogRenderer fogRenderer;
        private FogConfigPacket clientFogConfig = new FogConfigPacket(); // Fog config received from server

        // Animal insanity tracking
        private double lastInsanityCheckTime = 0; // Track last time we checked for animals to drive insane
        private HashSet<long> insaneEntityIds = new HashSet<long>(); // Entity IDs that have been driven insane (for quick lookup)
        private string[] insanityIncludePatterns = null; // Cached parsed patterns from config
        private string[] insanityExcludePatterns = null; // Cached parsed patterns from config
        private const string INSANITY_ATTRIBUTE = "devastationInsane"; // WatchedAttribute key for insane state

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            // Register network channel and message types (runs on both client and server)
            api.Network
                .RegisterChannel(NETWORK_CHANNEL_NAME)
                .RegisterMessageType(typeof(DevastatedChunkSyncPacket))
                .RegisterMessageType(typeof(FogConfigPacket));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            // Get the network channel and set up message handlers
            clientNetworkChannel = api.Network.GetChannel(NETWORK_CHANNEL_NAME)
                .SetMessageHandler<DevastatedChunkSyncPacket>(OnDevastatedChunkSync)
                .SetMessageHandler<FogConfigPacket>(OnFogConfigSync);

            // Create and register the fog renderer
            fogRenderer = new DevastationFogRenderer(api, this);
            api.Event.RegisterRenderer(fogRenderer, EnumRenderStage.Before, "devastationfog");

            // Hook into the client-side temporal stability system after player enters world
            // This makes the gear spin counter-clockwise in devastated chunks
            api.Event.PlayerEntitySpawn += (entity) => {
                // Only hook once when the local player spawns (check if entity is the local player)
                var localPlayer = api.World.Player;
                if (temporalStabilitySystemClient == null && localPlayer != null && entity is EntityPlayer playerEntity && playerEntity.PlayerUID == localPlayer.PlayerUID)
                {
                    temporalStabilitySystemClient = api.ModLoader.GetModSystem<SystemTemporalStability>();
                    if (temporalStabilitySystemClient != null)
                    {
                        temporalStabilitySystemClient.OnGetTemporalStability += OnGetTemporalStabilityOverrideClient;
                        api.Logger.Notification("SpreadingDevastation: Hooked into client temporal stability system for gear visual");
                    }
                }
            };

            api.Logger.Notification("SpreadingDevastation: Client-side fog renderer initialized");
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

            // Process animal insanity every 1 second (actual interval controlled by config)
            api.Event.RegisterGameTickListener(ProcessAnimalInsanity, 1000);

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
                    return TextCommandResult.Success("Configuration reloaded from SpreadingDevastationConfig.json");
                });

            // Hook into the temporal stability system to make the gear spin correctly in devastated areas
            // This is done after save game loads to ensure the system is available
            api.Event.SaveGameLoaded += () => {
                temporalStabilitySystemServer = api.ModLoader.GetModSystem<SystemTemporalStability>();
                if (temporalStabilitySystemServer != null)
                {
                    temporalStabilitySystemServer.OnGetTemporalStability += OnGetTemporalStabilityOverrideServer;
                    sapi.Logger.Notification("SpreadingDevastation: Hooked into server temporal stability system");
                }
            };
        }

        /// <summary>
        /// Server-side hook for the temporal stability system. Returns a low stability value when
        /// the position is in a devastated chunk (and not protected by a rift ward).
        /// This affects stability drain rate on the server.
        /// </summary>
        private float OnGetTemporalStabilityOverrideServer(float stability, double x, double y, double z)
        {
            // Early exit if no devastated chunks
            if (devastatedChunks == null || devastatedChunks.Count == 0) return stability;

            // Check if this position is in a devastated chunk
            int chunkX = (int)x / CHUNK_SIZE;
            int chunkZ = (int)z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!devastatedChunks.ContainsKey(chunkKey)) return stability;

            // Check if protected by rift ward - if so, return normal stability
            BlockPos blockPos = new BlockPos((int)x, (int)y, (int)z);
            if (IsBlockProtectedByRiftWard(blockPos)) return stability;

            // Return a very low stability value to make the gear spin counter-clockwise
            // Using 0.0 ensures maximum instability visual effect
            return 0f;
        }

        /// <summary>
        /// Client-side hook for the temporal stability system. Returns a low stability value when
        /// the position is in a devastated chunk. This makes the temporal gear spin counter-clockwise.
        /// Uses the synced clientDevastatedChunks set to determine if in a devastated area.
        /// </summary>
        private float OnGetTemporalStabilityOverrideClient(float stability, double x, double y, double z)
        {
            // Early exit if no devastated chunks synced from server
            if (clientDevastatedChunks == null || clientDevastatedChunks.Count == 0) return stability;

            // Check if this position is in a devastated chunk
            int chunkX = (int)x / CHUNK_SIZE;
            int chunkZ = (int)z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!clientDevastatedChunks.Contains(chunkKey)) return stability;

            // Note: Rift ward protection is handled server-side - protected chunks
            // are removed from devastatedChunks before syncing to clients

            // Return a very low stability value to make the gear spin counter-clockwise
            // Using 0.0 ensures maximum instability visual effect
            return 0f;
        }

        /// <summary>
        /// Syncs devastated chunk data to all connected clients.
        /// Each player receives only the chunks within render distance of their position.
        /// </summary>
        private void SyncDevastatedChunksToClients(float dt)
        {
            if (serverNetworkChannel == null) return;

            // Early exit if nothing to sync
            bool hasChunks = devastatedChunks != null && devastatedChunks.Count > 0;
            if (!fogConfigDirty && !hasChunks) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            const int SYNC_RADIUS_CHUNKS = 8; // Sync chunks within 8 chunk radius (~256 blocks)

            // Only create fog config packet when it has changed
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

            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i] as IServerPlayer;
                if (player?.Entity == null) continue;

                // Send fog config only when it changed
                if (fogPacket != null)
                {
                    serverNetworkChannel.SendPacket(fogPacket, player);
                }

                // Send chunk data if there are any devastated chunks
                if (!hasChunks) continue;

                var playerPos = player.Entity.Pos;
                int playerChunkX = (int)playerPos.X / CHUNK_SIZE;
                int playerChunkZ = (int)playerPos.Z / CHUNK_SIZE;

                var packet = new DevastatedChunkSyncPacket();

                foreach (var kvp in devastatedChunks)
                {
                    var chunk = kvp.Value;
                    // Only sync chunks within range of player
                    int dx = chunk.ChunkX - playerChunkX;
                    int dz = chunk.ChunkZ - playerChunkZ;

                    if (dx >= -SYNC_RADIUS_CHUNKS && dx <= SYNC_RADIUS_CHUNKS &&
                        dz >= -SYNC_RADIUS_CHUNKS && dz <= SYNC_RADIUS_CHUNKS)
                    {
                        packet.ChunkXs.Add(chunk.ChunkX);
                        packet.ChunkZs.Add(chunk.ChunkZ);
                    }
                }

                // Only send if there are nearby chunks
                if (packet.ChunkXs.Count > 0)
                {
                    serverNetworkChannel.SendPacket(packet, player);
                }
            }
        }

        /// <summary>
        /// Marks the fog configuration as changed, so it will be sent to clients on next sync.
        /// </summary>
        private void BroadcastFogConfig()
        {
            fogConfigDirty = true;
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

                // Clear cached insanity patterns so they get re-parsed from new config
                insanityIncludePatterns = null;
                insanityExcludePatterns = null;
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
                // Send each line as a separate message to avoid VTML parsing issues
                foreach (var line in safeLines)
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, line, EnumChatType.Notification);
                }
            }

            // Return empty success to avoid duplicate output
            return TextCommandResult.Success();
        }

// Command handlers moved to SpreadingDevastationModSystem.Commands.cs (partial class)


        private string GenerateSourceId()
        {
            return (nextSourceId++).ToString();
        }

        private void SpreadDevastationFromRifts(float dt)
        {
            // Skip all processing if paused or no sources exist
            if (isPaused) return;
            if (sapi == null) return;
            if (devastationSources == null || devastationSources.Count == 0) return;

            try
            {
                // Spread from manual devastation sources
                List<DevastationSource> toRemove = null; // Lazy init - only create if needed
                double currentGameTime = sapi.World.Calendar.TotalHours;
                
                foreach (DevastationSource source in devastationSources)
                {
                    // Check if the block still exists
                    Block block = sapi.World.BlockAccessor.GetBlock(source.Pos);
                    if (block == null || block.Id == 0)
                    {
                        // Block was removed, remove from sources
                        if (toRemove == null) toRemove = new List<DevastationSource>();
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
                if (toRemove != null)
                {
                    foreach (var source in toRemove)
                    {
                        devastationSources.Remove(source);
                    }
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

                    // Play conversion sound for the original block type
                    PlayBlockConversionSound(block, targetPos);

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
            // Big berry bushes (covers bigberrybush-ripe-blueberry, etc.)
            else if (path.StartsWith("bigberrybush-"))
            {
                devastatedBlock = "devgrowth-thorns";
                regeneratesTo = "leavesbranchy-grown-oak";
            }
            // Wild berry bushes (covers wildberrybush variants)
            else if (path.StartsWith("wildberrybush-"))
            {
                devastatedBlock = "devgrowth-thorns";
                regeneratesTo = "leavesbranchy-grown-oak";
            }
            // Log sections (placed logs, branches, etc. - e.g., logsection-placed-redwood-ne-ud)
            else if (path.StartsWith("logsection-"))
            {
                devastatedBlock = "devastatedsoil-3";
                regeneratesTo = "log-grown-aged-ud";
            }
            // Fruit tree foliage (fruittree-foliage, fruittree-foliage-ripe, etc.)
            else if (path.StartsWith("fruittree-foliage"))
            {
                devastatedBlock = "devgrowth-bush";
                regeneratesTo = "none";
            }
            // Fruit tree branches (fruittree-branch-*, etc.)
            else if (path.StartsWith("fruittree-branch"))
            {
                devastatedBlock = "devastatedsoil-3";
                regeneratesTo = "log-grown-aged-ud";
            }
            // Fruit tree stems/trunks
            else if (path.StartsWith("fruittree-stem") || path.StartsWith("fruittree-trunk"))
            {
                devastatedBlock = "devastatedsoil-3";
                regeneratesTo = "log-grown-aged-ud";
            }
            // Mushrooms (mushroom-bolete-normal, mushroom-fieldmushroom-harvested, etc.)
            else if (path.StartsWith("mushroom-"))
            {
                devastatedBlock = "devgrowth-shard";
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
        /// Determines the appropriate conversion sound type for a block based on its path.
        /// Returns the sound file name without path or extension.
        /// </summary>
        private string GetBlockConversionSoundType(Block block)
        {
            if (block == null) return "soil"; // Default fallback

            string path = block.Code.Path;

            // Soil-like blocks
            if (path.StartsWith("soil-") || path.StartsWith("forestfloor") ||
                path.StartsWith("peat-") || path.StartsWith("rawclay-") || path == "muddygravel")
            {
                return "soil";
            }
            // Stone/rock blocks
            else if (path.StartsWith("rock-"))
            {
                return "stone";
            }
            // Gravel blocks
            else if (path.StartsWith("gravel-"))
            {
                return "gravel";
            }
            // Sand blocks
            else if (path.StartsWith("sand-"))
            {
                return "sand";
            }
            // Wood/log blocks
            else if (path.StartsWith("log-") || path.StartsWith("logsection-") ||
                     path.StartsWith("fruittree-branch") || path.StartsWith("fruittree-stem") ||
                     path.StartsWith("fruittree-trunk"))
            {
                return "wood";
            }
            // Leaves/foliage blocks
            else if (path.StartsWith("leavesbranchy-") || path.StartsWith("leaves-") ||
                     path.StartsWith("fruittree-foliage"))
            {
                return "leaves";
            }
            // Plant blocks (grass, flowers, ferns, mushrooms, crops)
            else if (path.StartsWith("tallgrass-") || path.StartsWith("flower-") ||
                     path.StartsWith("fern-") || path.StartsWith("crop-") ||
                     path.StartsWith("mushroom-") || path.StartsWith("tallplant-") ||
                     path.StartsWith("waterlily"))
            {
                return "plant";
            }
            // Berry bushes and similar
            else if (path.StartsWith("smallberrybush-") || path.StartsWith("largeberrybush-") ||
                     path.StartsWith("bigberrybush-") || path.StartsWith("wildberrybush-"))
            {
                return "leaves"; // Berry bushes sound like leaves
            }

            // Default to soil sound
            return "soil";
        }

        /// <summary>
        /// Plays the appropriate block conversion sound at the specified position.
        /// Only plays sounds on the server; clients will hear it via network sync.
        /// </summary>
        private void PlayBlockConversionSound(Block originalBlock, BlockPos pos)
        {
            if (sapi == null || originalBlock == null || pos == null) return;
            if (config != null && !config.EnableConversionSounds) return;

            string soundType = GetBlockConversionSoundType(originalBlock);
            AssetLocation soundLoc = new AssetLocation("spreadingdevastation", $"sounds/block/convert-{soundType}");

            // Play sound at block position with slight pitch randomization
            // Range of 32 blocks, volume controlled by config
            float volume = config?.ConversionSoundVolume ?? 0.5f;
            sapi.World.PlaySoundAt(soundLoc, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, null, true, 32f, volume);
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
            // Early exit - skip all work if nothing to process
            if (isPaused || sapi == null) return;
            if (devastatedChunks == null || devastatedChunks.Count == 0) return;

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
                if (chunksNeedingRepair.Count > 0)
                    ProcessChunksNeedingRepair();

                // Drain temporal stability from players in devastated chunks
                DrainPlayerTemporalStability(dt);

                // Check for chunk spreading to nearby chunks
                TrySpreadToNearbyChunks(currentTime);

                foreach (var chunk in devastatedChunks.Values) // Dictionary values are safe to iterate while modifying chunk properties
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
        /// Processes animal insanity - checks for animals in devastated chunks and drives them insane.
        /// Insane animals become permanently hostile to players.
        /// </summary>
        private void ProcessAnimalInsanity(float dt)
        {
            // Early exits for performance
            if (!config.AnimalInsanityEnabled) return;
            if (isPaused || sapi == null) return;
            if (devastatedChunks == null || devastatedChunks.Count == 0) return;

            // Check interval (config is in seconds, we need to compare against real time)
            double currentTime = sapi.World.ElapsedMilliseconds / 1000.0;
            if (currentTime - lastInsanityCheckTime < config.AnimalInsanityCheckIntervalSeconds) return;
            lastInsanityCheckTime = currentTime;

            // Parse patterns from config if not already cached
            if (insanityIncludePatterns == null)
            {
                insanityIncludePatterns = config.AnimalInsanityEntityCodes
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToArray();
            }
            if (insanityExcludePatterns == null)
            {
                insanityExcludePatterns = config.AnimalInsanityExcludeCodes
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToArray();
            }

            // Process animals near each player
            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers.Cast<IServerPlayer>())
            {
                if (player.Entity == null) continue;

                var playerPos = player.Entity.ServerPos.XYZ;
                int searchRadius = config.AnimalInsanitySearchRadius;

                // Get all entities around the player
                Entity[] nearbyEntities = sapi.World.GetEntitiesAround(
                    playerPos,
                    searchRadius,
                    searchRadius,
                    entity => entity.IsCreature && entity.Alive && !(entity is EntityPlayer)
                );

                foreach (var entity in nearbyEntities)
                {
                    // Skip if already insane (quick HashSet lookup first)
                    if (insaneEntityIds.Contains(entity.EntityId)) continue;

                    // Also check WatchedAttribute (for persistence across sessions)
                    if (entity.WatchedAttributes.GetBool(INSANITY_ATTRIBUTE, false))
                    {
                        // Already insane but not in our HashSet (happens after load), add it
                        insaneEntityIds.Add(entity.EntityId);
                        continue;
                    }

                    // Check if entity is in a devastated chunk
                    int chunkX = (int)entity.ServerPos.X / CHUNK_SIZE;
                    int chunkZ = (int)entity.ServerPos.Z / CHUNK_SIZE;
                    long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

                    if (!devastatedChunks.ContainsKey(chunkKey)) continue;

                    // Check if protected by rift ward
                    if (IsBlockProtectedByRiftWard(entity.ServerPos.AsBlockPos)) continue;

                    // Check if this entity type can go insane
                    if (!CanEntityGoInsane(entity)) continue;

                    // Roll for insanity chance
                    if (sapi.World.Rand.NextDouble() >= config.AnimalInsanityChance) continue;

                    // Drive the animal insane!
                    DriveEntityInsane(entity);
                }
            }
        }

        /// <summary>
        /// Checks if an entity type is eligible to go insane based on config patterns.
        /// </summary>
        private bool CanEntityGoInsane(Entity entity)
        {
            if (entity?.Code?.Path == null) return false;

            // Ensure patterns are initialized (may be called from tests before ProcessAnimalInsanity runs)
            if (insanityIncludePatterns == null)
            {
                insanityIncludePatterns = config.AnimalInsanityEntityCodes
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToArray();
            }
            if (insanityExcludePatterns == null)
            {
                insanityExcludePatterns = config.AnimalInsanityExcludeCodes
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToArray();
            }

            string entityCode = entity.Code.Path.ToLowerInvariant();

            // Check exclusions first (these are typically already-hostile mobs)
            foreach (var pattern in insanityExcludePatterns)
            {
                if (string.IsNullOrEmpty(pattern)) continue;
                if (pattern == "*" || entityCode.StartsWith(pattern) || entityCode.Contains(pattern))
                {
                    return false;
                }
            }

            // Check if wildcard match all
            if (insanityIncludePatterns.Length == 1 && insanityIncludePatterns[0] == "*")
            {
                return true;
            }

            // Check inclusions
            foreach (var pattern in insanityIncludePatterns)
            {
                if (string.IsNullOrEmpty(pattern)) continue;
                if (entityCode.StartsWith(pattern) || entityCode.Contains(pattern))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Drives an entity insane, making it permanently hostile to players.
        /// </summary>
        private void DriveEntityInsane(Entity entity)
        {
            // Mark as insane (persists in save)
            entity.WatchedAttributes.SetBool(INSANITY_ATTRIBUTE, true);
            insaneEntityIds.Add(entity.EntityId);

            // Try to trigger the aggressive emotion state
            var emotionBehavior = entity.GetBehavior<EntityBehaviorEmotionStates>();
            if (emotionBehavior != null)
            {
                // Trigger aggressiveondamage state with guaranteed activation (1.0 random value passes any chance check)
                // The 0 source ID indicates environmental cause rather than another entity
                emotionBehavior.TryTriggerState("aggressiveondamage", 0.0, 0);
            }

            // Also try to set the entity's target to nearest player for immediate aggression
            TrySetEntityTargetToNearestPlayer(entity);

            sapi.Logger.Debug($"SpreadingDevastation: {entity.Code.Path} at ({entity.ServerPos.X:F0}, {entity.ServerPos.Y:F0}, {entity.ServerPos.Z:F0}) driven insane by devastation");
        }

        /// <summary>
        /// Attempts to set an entity's AI target to the nearest player for immediate aggression.
        /// </summary>
        private void TrySetEntityTargetToNearestPlayer(Entity entity)
        {
            if (entity == null) return;

            // Find nearest player
            double nearestDistSq = double.MaxValue;
            EntityPlayer nearestPlayer = null;

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers.Cast<IServerPlayer>())
            {
                if (player.Entity == null) continue;

                double dx = player.Entity.ServerPos.X - entity.ServerPos.X;
                double dy = player.Entity.ServerPos.Y - entity.ServerPos.Y;
                double dz = player.Entity.ServerPos.Z - entity.ServerPos.Z;
                double distSq = dx * dx + dy * dy + dz * dz;

                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestPlayer = player.Entity;
                }
            }

            // If a player is nearby (within 32 blocks), set them as the target
            if (nearestPlayer != null && nearestDistSq < 32 * 32)
            {
                // Set the attacked by entity attribute so AI tasks will target the player
                entity.Attributes.SetLong("guardTargetEntityId", nearestPlayer.EntityId);
            }
        }

        /// <summary>
        /// Gets the count of currently tracked insane entities.
        /// </summary>
        public int GetInsaneEntityCount()
        {
            return insaneEntityIds.Count;
        }

        /// <summary>
        /// Clears insanity from all entities (for admin commands).
        /// </summary>
        public int ClearAllInsanity()
        {
            int count = 0;

            // Clear insanity from all loaded entities
            foreach (var entity in sapi.World.LoadedEntities.Values)
            {
                if (entity == null) continue;
                if (entity.WatchedAttributes.GetBool(INSANITY_ATTRIBUTE, false))
                {
                    entity.WatchedAttributes.RemoveAttribute(INSANITY_ATTRIBUTE);
                    count++;
                }
            }

            insaneEntityIds.Clear();
            return count;
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
                int[] shuffledDirIndices = GetShuffledIndices(ChunkCardinalOffsets.Length, sapi.World.Rand);

                for (int dirIdx = 0; dirIdx < shuffledDirIndices.Length; dirIdx++)
                {
                    var direction = ChunkCardinalOffsets[shuffledDirIndices[dirIdx]];
                    int offsetX = direction[0];
                    int offsetZ = direction[1];

                    int newChunkX = chunk.ChunkX + offsetX;
                    int newChunkZ = chunk.ChunkZ + offsetZ;
                    long newChunkKey = DevastatedChunk.MakeChunkKey(newChunkX, newChunkZ);

                    // Skip if already devastated or already queued
                    if (devastatedChunks.ContainsKey(newChunkKey)) continue;
                    // Check chunksToAdd without LINQ - direct loop
                    bool alreadyQueued = false;
                    for (int i = 0; i < chunksToAdd.Count; i++)
                    {
                        if (chunksToAdd[i].ChunkKey == newChunkKey) { alreadyQueued = true; break; }
                    }
                    if (alreadyQueued) continue;

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
        /// Four horizontal cardinal directions (no up/down) for edge bleeding
        /// </summary>
        private static readonly BlockPos[] HorizontalOffsets = new BlockPos[]
        {
            new BlockPos(1, 0, 0),   // East
            new BlockPos(-1, 0, 0),  // West
            new BlockPos(0, 0, 1),   // South
            new BlockPos(0, 0, -1)   // North
        };

        // Reusable array indices for in-place shuffling (avoids allocations)
        private readonly int[] shuffleIndices6 = new int[] { 0, 1, 2, 3, 4, 5 };
        private readonly int[] shuffleIndices4 = new int[] { 0, 1, 2, 3 };

        /// <summary>
        /// Fisher-Yates shuffle in place - avoids allocations unlike LINQ OrderBy.
        /// </summary>
        private void ShuffleInPlace<T>(T[] array, Random rand)
        {
            int n = array.Length;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                T temp = array[j];
                array[j] = array[i];
                array[i] = temp;
            }
        }

        /// <summary>
        /// Shuffles index array in place and returns it for iterating another array.
        /// </summary>
        private int[] GetShuffledIndices(int count, Random rand)
        {
            int[] indices = count == 6 ? shuffleIndices6 : (count == 4 ? shuffleIndices4 : new int[count]);
            if (count != 6 && count != 4)
            {
                for (int i = 0; i < count; i++) indices[i] = i;
            }
            // Fisher-Yates shuffle
            for (int i = count - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                int temp = indices[j];
                indices[j] = indices[i];
                indices[i] = temp;
            }
            return indices;
        }

        /// <summary>
        /// Checks if a position exists in a frontier list using position key comparison.
        /// More efficient than LINQ .Any() with position comparison.
        /// </summary>
        private bool FrontierContainsPosition(List<BlockPos> frontier, int x, int y, int z)
        {
            if (frontier == null) return false;
            for (int i = 0; i < frontier.Count; i++)
            {
                var p = frontier[i];
                if (p.X == x && p.Y == y && p.Z == z) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a bleed frontier contains a position.
        /// </summary>
        private bool BleedFrontierContainsPosition(List<BleedBlock> frontier, int x, int y, int z)
        {
            if (frontier == null) return false;
            for (int i = 0; i < frontier.Count; i++)
            {
                var b = frontier[i];
                if (b.Pos.X == x && b.Pos.Y == y && b.Pos.Z == z) return true;
            }
            return false;
        }

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
                // Shuffle the cardinal directions to avoid bias (in-place, no allocation)
                int[] shuffledIndices = GetShuffledIndices(CardinalOffsets.Length, sapi.World.Rand);

                for (int idx = 0; idx < shuffledIndices.Length; idx++)
                {
                    var offset = CardinalOffsets[shuffledIndices[idx]];
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

                            // Play conversion sound for the original block type
                            PlayBlockConversionSound(block, targetPos);

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

                    if (!hasValidNeighbors && !FrontierContainsPosition(blocksToRemoveFromFrontier, frontierPos.X, frontierPos.Y, frontierPos.Z))
                    {
                        blocksToRemoveFromFrontier.Add(frontierPos);
                    }
                }
            }

            // Update frontier: add new blocks, remove exhausted ones
            for (int i = 0; i < newFrontierBlocks.Count; i++)
            {
                var newBlock = newFrontierBlocks[i];
                if (!FrontierContainsPosition(chunk.DevastationFrontier, newBlock.X, newBlock.Y, newBlock.Z))
                {
                    chunk.DevastationFrontier.Add(newBlock);
                }

                // Check if this new block is at a chunk edge - if so, potentially start a bleed
                if (config.ChunkEdgeBleedDepth > 0 && sapi.World.Rand.NextDouble() < config.ChunkEdgeBleedChance)
                {
                    TryStartEdgeBleed(chunk, newBlock, startX, startZ, endX, endZ);
                }
            }

            // Remove exhausted frontier blocks - iterate backwards to safely remove
            for (int i = 0; i < blocksToRemoveFromFrontier.Count; i++)
            {
                var oldBlock = blocksToRemoveFromFrontier[i];
                for (int j = chunk.DevastationFrontier.Count - 1; j >= 0; j--)
                {
                    var p = chunk.DevastationFrontier[j];
                    if (p.X == oldBlock.X && p.Y == oldBlock.Y && p.Z == oldBlock.Z)
                    {
                        chunk.DevastationFrontier.RemoveAt(j);
                        break; // Only remove first match
                    }
                }
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
                int edgeStartX = startX + 2;
                int edgeEndX = endX - 2;
                int edgeStartZ = startZ + 2;
                int edgeEndZ = endZ - 2;

                // Sort in place - higher score = keep (edge blocks get bonus, distance from center adds to score)
                chunk.DevastationFrontier.Sort((a, b) =>
                {
                    int distA = Math.Abs(a.X - centerX) + Math.Abs(a.Z - centerZ);
                    int distB = Math.Abs(b.X - centerX) + Math.Abs(b.Z - centerZ);
                    bool atEdgeA = a.X < edgeStartX || a.X >= edgeEndX || a.Z < edgeStartZ || a.Z >= edgeEndZ;
                    bool atEdgeB = b.X < edgeStartX || b.X >= edgeEndX || b.Z < edgeStartZ || b.Z >= edgeEndZ;
                    int scoreA = distA + (atEdgeA ? 100 : 0);
                    int scoreB = distB + (atEdgeB ? 100 : 0);
                    return scoreB.CompareTo(scoreA); // Descending order
                });
                // Keep top 300
                if (chunk.DevastationFrontier.Count > 300)
                {
                    chunk.DevastationFrontier.RemoveRange(300, chunk.DevastationFrontier.Count - 300);
                }
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

                            // Play conversion sound for the original block type
                            PlayBlockConversionSound(block, pos);

                            regrowingBlocks.Add(new RegrowingBlocks
                            {
                                Pos = pos.Copy(),
                                Out = regeneratesTo,
                                LastTime = sapi.World.Calendar.TotalHours
                            });
                            chunk.BlocksDevastated++;
                            blocksFound++;

                            // Add to frontier
                            if (!FrontierContainsPosition(chunk.DevastationFrontier, pos.X, pos.Y, pos.Z))
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
            // Using static HorizontalOffsets array to avoid allocation
            for (int i = 0; i < HorizontalOffsets.Length; i++)
            {
                var offset = HorizontalOffsets[i];
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
                if (BleedFrontierContainsPosition(chunk.BleedFrontier, adjacentPos.X, adjacentPos.Y, adjacentPos.Z))
                {
                    continue;
                }

                // Devastate the adjacent block and add it to bleed frontier
                Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedForm));
                if (newBlock != null)
                {
                    sapi.World.BlockAccessor.SetBlock(newBlock.Id, adjacentPos);

                    // Play conversion sound for the original block type
                    PlayBlockConversionSound(adjacentBlock, adjacentPos);

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

            List<BleedBlock> newBleedBlocks = null; // Lazy init
            List<BleedBlock> bleedBlocksToRemove = null; // Lazy init

            // Process bleed blocks in random order using shuffled indices (no allocation)
            int bleedCount = chunk.BleedFrontier.Count;
            int[] bleedIndices = new int[bleedCount];
            for (int i = 0; i < bleedCount; i++) bleedIndices[i] = i;
            // Fisher-Yates shuffle
            for (int i = bleedCount - 1; i > 0; i--)
            {
                int j = sapi.World.Rand.Next(i + 1);
                int temp = bleedIndices[j];
                bleedIndices[j] = bleedIndices[i];
                bleedIndices[i] = temp;
            }

            for (int bi = 0; bi < bleedCount && processed < maxToProcess; bi++)
            {
                var bleedBlock = chunk.BleedFrontier[bleedIndices[bi]];

                // If no remaining spread, mark for removal
                if (bleedBlock.RemainingSpread <= 0)
                {
                    if (bleedBlocksToRemove == null) bleedBlocksToRemove = new List<BleedBlock>();
                    bleedBlocksToRemove.Add(bleedBlock);
                    continue;
                }

                // Try to spread to an adjacent block (using CardinalOffsets - same 6 directions)
                int[] shuffledOffsetIndices = GetShuffledIndices(CardinalOffsets.Length, sapi.World.Rand);
                bool foundTarget = false;

                for (int oi = 0; oi < shuffledOffsetIndices.Length; oi++)
                {
                    var offset = CardinalOffsets[shuffledOffsetIndices[oi]];
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

                        // Play conversion sound for the original block type
                        PlayBlockConversionSound(targetBlock, targetPos);

                        regrowingBlocks.Add(new RegrowingBlocks
                        {
                            Pos = targetPos.Copy(),
                            Out = regeneratesTo,
                            LastTime = sapi.World.Calendar.TotalHours
                        });

                        // Add new bleed block with decremented spread budget
                        if (newBleedBlocks == null) newBleedBlocks = new List<BleedBlock>();
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
                    if (bleedBlocksToRemove == null) bleedBlocksToRemove = new List<BleedBlock>();
                    bleedBlocksToRemove.Add(bleedBlock);
                }
            }

            // Update bleed frontier
            if (newBleedBlocks != null)
            {
                for (int i = 0; i < newBleedBlocks.Count; i++)
                {
                    var newBleed = newBleedBlocks[i];
                    if (!BleedFrontierContainsPosition(chunk.BleedFrontier, newBleed.Pos.X, newBleed.Pos.Y, newBleed.Pos.Z))
                    {
                        chunk.BleedFrontier.Add(newBleed);
                    }
                }
            }

            if (bleedBlocksToRemove != null)
            {
                for (int i = 0; i < bleedBlocksToRemove.Count; i++)
                {
                    chunk.BleedFrontier.Remove(bleedBlocksToRemove[i]);
                }
            }

            // Prune bleed frontier if too large - keep highest spread values
            if (chunk.BleedFrontier.Count > 200)
            {
                // Sort in place instead of creating new list
                chunk.BleedFrontier.Sort((a, b) => b.RemainingSpread.CompareTo(a.RemainingSpread));
                chunk.BleedFrontier.RemoveRange(100, chunk.BleedFrontier.Count - 100);
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
                                    if (!FrontierContainsPosition(chunk.DevastationFrontier, pos.X, pos.Y, pos.Z))
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

                                // Play conversion sound for the original block type
                                PlayBlockConversionSound(startBlock, startPos);

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

                                            // Play conversion sound for the original block type
                                            PlayBlockConversionSound(nearbyBlock, nearbyPos);

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
                                if (!FrontierContainsPosition(edgeFrontier, newChunkPos.X, newChunkPos.Y, newChunkPos.Z))
                                {
                                    edgeFrontier.Add(newChunkPos.Copy());
                                }
                            }
                        }
                        // Also check if the block is already devastated (can still be a frontier)
                        else if (newChunkBlock != null && IsAlreadyDevastated(newChunkBlock))
                        {
                            if (!FrontierContainsPosition(edgeFrontier, newChunkPos.X, newChunkPos.Y, newChunkPos.Z))
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
        /// Also checks if the block is placed in a devastated chunk and queues it for re-devastation.
        /// </summary>
        private void OnBlockPlaced(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (blockSel?.Position == null) return;

            Block block = sapi.World.BlockAccessor.GetBlock(blockSel.Position);

            // Check if it's a rift ward
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

            // Check if block was placed in a devastated chunk - if so, queue it for re-devastation
            TryQueueBlockForRedevastation(blockSel.Position, block);
        }

        /// <summary>
        /// Checks if a newly placed block is in a devastated chunk and queues it for re-devastation.
        /// Blocks protected by rift wards or that cannot be devastated are skipped.
        /// </summary>
        private void TryQueueBlockForRedevastation(BlockPos pos, Block block)
        {
            if (pos == null || block == null) return;
            if (isPaused) return;
            if (devastatedChunks == null || devastatedChunks.Count == 0) return;

            // Check if this position is in a devastated chunk
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!devastatedChunks.TryGetValue(chunkKey, out var chunk)) return;

            // Skip if protected by rift ward
            if (IsBlockProtectedByRiftWard(pos)) return;

            // Skip if block is already devastated
            if (IsAlreadyDevastated(block)) return;

            // Check if the block can be devastated
            if (!TryGetDevastatedForm(block, out _, out _)) return;

            // Add to the chunk's frontier for processing
            if (chunk.DevastationFrontier == null)
            {
                chunk.DevastationFrontier = new List<BlockPos>();
            }

            // Only add if not already in frontier
            if (!FrontierContainsPosition(chunk.DevastationFrontier, pos.X, pos.Y, pos.Z))
            {
                chunk.DevastationFrontier.Add(pos.Copy());

                // Un-mark as fully devastated so processing continues
                if (chunk.IsFullyDevastated)
                {
                    chunk.IsFullyDevastated = false;
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

            // Only scan for existing rift wards once on initial load, and only if there are players online
            // The OnBlockPlaced event handles new rift ward placements efficiently
            if (!initialRiftWardScanCompleted && sapi.World.AllOnlinePlayers.Length > 0)
            {
                ScanForExistingRiftWardsOptimized();
            }

            // Skip the rest if no rift wards are tracked
            if (activeRiftWards == null || activeRiftWards.Count == 0) return;

            // Periodically verify rift wards are still active (have fuel) and apply protection
            if (currentTime - lastRiftWardScanTime >= checkIntervalHours)
            {
                lastRiftWardScanTime = currentTime;
                VerifyRiftWardActiveState();

                // Only check for sources/chunks if there are active wards with sources to remove
                if (devastationSources != null && devastationSources.Count > 0)
                {
                    RemoveSourcesInAllRiftWardRadii();
                }

                if (devastatedChunks != null && devastatedChunks.Count > 0)
                {
                    RemoveDevastatedChunksInAllRiftWardRadii();
                }
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
                // First check if the chunk is loaded - if not, skip verification for this ward
                // This prevents incorrectly removing wards when the player leaves the area
                int chunkX = ward.Pos.X / CHUNK_SIZE;
                int chunkZ = ward.Pos.Z / CHUNK_SIZE;
                var chunk = sapi.World.BlockAccessor.GetChunk(chunkX, ward.Pos.Y / CHUNK_SIZE, chunkZ);
                if (chunk == null)
                {
                    // Chunk not loaded - keep the ward in the list but skip verification
                    // Protection still applies based on stored position
                    continue;
                }

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
        /// Optimized version of rift ward scanning that uses sparse sampling instead of scanning every block.
        /// This runs once on game load and is much more efficient than the full scan.
        /// New rift ward placements are tracked via the OnBlockPlaced event.
        /// </summary>
        private void ScanForExistingRiftWardsOptimized()
        {
            if (sapi == null) return;

            // Mark as completed immediately to prevent re-running
            initialRiftWardScanCompleted = true;
            lastFullRiftWardScanTime = sapi.World.Calendar.TotalHours;

            int scanRadius = 4; // Smaller radius - 4 chunks in each direction (128 blocks)
            int newWardsFound = 0;
            HashSet<long> existingPositions = null;

            // Only build the HashSet if we have existing wards
            if (activeRiftWards.Count > 0)
            {
                existingPositions = new HashSet<long>();
                for (int i = 0; i < activeRiftWards.Count; i++)
                {
                    existingPositions.Add(GetPositionKey(activeRiftWards[i].Pos));
                }
            }

            // Reusable BlockPos to avoid allocations
            BlockPos checkPos = new BlockPos(0, 0, 0);

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity == null) continue;

                var playerPos = player.Entity.Pos.AsBlockPos;
                int playerChunkX = playerPos.X / CHUNK_SIZE;
                int playerChunkZ = playerPos.Z / CHUNK_SIZE;

                // Scan chunks around the player with sparse sampling
                for (int cx = playerChunkX - scanRadius; cx <= playerChunkX + scanRadius; cx++)
                {
                    for (int cz = playerChunkZ - scanRadius; cz <= playerChunkZ + scanRadius; cz++)
                    {
                        // Get the chunk if it's loaded
                        var chunk = sapi.World.BlockAccessor.GetChunk(cx, 0, cz);
                        if (chunk == null) continue;

                        int startX = cx * CHUNK_SIZE;
                        int startZ = cz * CHUNK_SIZE;

                        // Rift wards are rare - sample every 2 blocks in X/Z and scan a narrow Y range near surface
                        // This reduces scan from 32*32*150 = 153,600 to 16*16*20 = 5,120 per chunk (30x reduction)
                        for (int dx = 0; dx < CHUNK_SIZE; dx += 2)
                        {
                            for (int dz = 0; dz < CHUNK_SIZE; dz += 2)
                            {
                                int x = startX + dx;
                                int z = startZ + dz;

                                // Get surface Y at this position
                                int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(checkPos.Set(x, 0, z));
                                if (surfaceY <= 0) continue;

                                // Scan 10 blocks above and 5 blocks below surface (rift wards are usually at ground level)
                                int minY = Math.Max(1, surfaceY - 5);
                                int maxY = Math.Min(sapi.World.BlockAccessor.MapSizeY - 1, surfaceY + 10);

                                for (int y = minY; y <= maxY; y++)
                                {
                                    checkPos.Set(x, y, z);
                                    var block = sapi.World.BlockAccessor.GetBlock(checkPos);

                                    if (block != null && IsRiftWardBlock(block))
                                    {
                                        long posKey = GetPositionKey(checkPos);
                                        if (existingPositions == null || !existingPositions.Contains(posKey))
                                        {
                                            var newWard = new RiftWard
                                            {
                                                Pos = checkPos.Copy(),
                                                DiscoveredTime = sapi.World.Calendar.TotalHours
                                            };

                                            // Check if it's active
                                            newWard.CachedIsActive = IsRiftWardActive(checkPos);
                                            newWard.LastActiveCheck = sapi.World.Calendar.TotalHours;

                                            activeRiftWards.Add(newWard);
                                            if (existingPositions == null) existingPositions = new HashSet<long>();
                                            existingPositions.Add(posKey);
                                            newWardsFound++;

                                            sapi.Logger.Notification($"SpreadingDevastation: Discovered existing rift ward at {checkPos} (active: {newWard.CachedIsActive})");

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
                sapi.Logger.Notification($"SpreadingDevastation: Initial scan found {newWardsFound} rift ward(s)");
                RebuildProtectedChunkCache();
            }
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

        // Test suite moved to SpreadingDevastationModSystem.Tests.cs (partial class)
    }

    // DevastationFogRenderer class moved to DevastationFogRenderer.cs
}
