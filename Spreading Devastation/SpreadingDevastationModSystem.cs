using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SpreadingDevastation
{
    /// <summary>
    /// Configuration class for the Spreading Devastation mod.
    /// This file is automatically created in the ModConfig folder when the mod first loads.
    /// </summary>
    public class SpreadingDevastationConfig
    {
        // === Global Settings ===
        
        /// <summary>
        /// Global speed multiplier for all devastation spread. Default: 1.0
        /// Higher values = faster spreading. Range: 0.01 to 100.0
        /// </summary>
        public double SpeedMultiplier { get; set; } = 1.0;
        
        /// <summary>
        /// Whether devastation processing starts paused. Default: false
        /// Use /devastate stop/start to toggle in-game.
        /// </summary>
        public bool StartPaused { get; set; } = false;
        
        /// <summary>
        /// Maximum number of devastation sources (caps metastasis growth). Default: 20
        /// Higher values allow more simultaneous spreading but may impact performance.
        /// </summary>
        public int MaxSources { get; set; } = 20;
        
        // === Surface Spreading Settings ===
        
        /// <summary>
        /// If true, new metastasis sources can only spawn at positions adjacent to air. Default: false
        /// When enabled, keeps devastation spreading along surfaces rather than tunneling deep underground.
        /// Note: This affects where NEW sources spawn, not which blocks can be devastated.
        /// </summary>
        public bool RequireSourceAirContact { get; set; } = false;
        
        /// <summary>
        /// Minimum Y level for spawning new metastasis sources. Default: 0 (no limit)
        /// Set higher (e.g., 80) to prevent deep underground spreading.
        /// </summary>
        public int MinSourceYLevel { get; set; } = 0;
        
        // === Default Source Settings (for /devastate add command) ===
        
        /// <summary>
        /// Default range (in blocks) for new devastation sources. Default: 8
        /// </summary>
        public int DefaultRange { get; set; } = 8;
        
        /// <summary>
        /// Default amount of blocks to devastate per tick for new sources. Default: 1
        /// </summary>
        public int DefaultAmount { get; set; } = 1;
        
        /// <summary>
        /// Starting radius for new sources before they expand. Default: 3.0
        /// </summary>
        public double DefaultStartRadius { get; set; } = 3.0;
        
        // === Metastasis Settings ===
        
        /// <summary>
        /// Number of blocks to devastate before spawning metastasis. Default: 300
        /// Lower values = faster spreading, higher values = more thorough local devastation.
        /// </summary>
        public int MetastasisThreshold { get; set; } = 300;
        
        /// <summary>
        /// Maximum generation level for metastasis (prevents infinite spreading). Default: 10
        /// 0 = original source, 1 = first metastasis, etc.
        /// </summary>
        public int MaxGenerationLevel { get; set; } = 10;
        
        // === Regeneration Settings ===
        
        /// <summary>
        /// In-game hours before devastated blocks regenerate. Default: 60.0
        /// </summary>
        public double RegenerationHours { get; set; } = 60.0;
        
        /// <summary>
        /// Maximum blocks to regenerate per tick (prevents lag spikes). Default: 50
        /// </summary>
        public int MaxRegenPerTick { get; set; } = 50;
        
        // === Tick Intervals (in milliseconds) ===
        
        /// <summary>
        /// How often to process devastation spreading (ms). Default: 10
        /// Lower = more frequent updates, higher = better performance.
        /// </summary>
        public int DevastationTickIntervalMs { get; set; } = 10;
        
        /// <summary>
        /// How often to check for block regeneration (ms). Default: 1000
        /// </summary>
        public int RegenerationTickIntervalMs { get; set; } = 1000;
        
        // === Saturation and Stalling ===
        
        /// <summary>
        /// Saturation percentage (0.0-1.0) required before spawning metastasis. Default: 0.75
        /// </summary>
        public double SaturationThreshold { get; set; } = 0.75;
        
        /// <summary>
        /// Success rate below which to expand search radius. Default: 0.2
        /// </summary>
        public double ExpandRadiusThreshold { get; set; } = 0.2;
        
        /// <summary>
        /// Success rate below which to consider source stalled. Default: 0.05
        /// </summary>
        public double StallThreshold { get; set; } = 0.05;
        
        /// <summary>
        /// Number of stall cycles before triggering long-range metastasis search. Default: 10
        /// </summary>
        public int StallCyclesBeforeAction { get; set; } = 10;
        
        /// <summary>
        /// Number of stall cycles before marking source as truly saturated. Default: 30
        /// </summary>
        public int StallCyclesBeforeSaturated { get; set; } = 30;
        
        // === Long-Range Search Settings ===
        
        /// <summary>
        /// Maximum search distance for long-range metastasis (in blocks). Default: 128
        /// </summary>
        public int MaxLongRangeSearchDistance { get; set; } = 128;
        
        /// <summary>
        /// Minimum non-devastated blocks required for long-range metastasis spawn. Default: 10
        /// </summary>
        public int LongRangeMinBlocksRequired { get; set; } = 10;
        
        // === Pillar Search Settings ===
        
        /// <summary>
        /// Height of vertical pillar to search when finding metastasis positions. Default: 10
        /// Searches ±(PillarSearchHeight/2) blocks from source Y level.
        /// </summary>
        public int PillarSearchHeight { get; set; } = 10;
        
        // === Child Spawn Settings ===
        
        /// <summary>
        /// Delay in seconds between spawning child sources. Default: 120
        /// After spawning a child, source must wait this long before spawning another.
        /// Affected by speed multiplier (higher speed = shorter delay).
        /// </summary>
        public double ChildSpawnDelaySeconds { get; set; } = 120.0;
        
        /// <summary>
        /// Radius variation for child sources as a fraction. Default: 0.5
        /// Child sources spawn with parent's range ± this percentage.
        /// E.g., 0.5 means 50-150% of parent range (range 8 → 4-12).
        /// </summary>
        public double MetastasisRadiusVariation { get; set; } = 0.5;
        
        /// <summary>
        /// Number of failed child spawn attempts before marking source as saturated. Default: 10
        /// Each metastasis cycle that fails to find a valid spawn position counts as one attempt.
        /// </summary>
        public int MaxFailedChildSpawnAttempts { get; set; } = 10;
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
            public int ChildrenSpawned = 0; // Number of child sources successfully spawned
            
            [ProtoMember(20)]
            public double LastChildSpawnTime = 0; // World time (TotalHours) when last child was spawned
            
            [ProtoMember(21)]
            public int FailedChildSpawnAttempts = 0; // Number of failed attempts to spawn children
        }

        private ICoreServerAPI sapi;
        private List<RegrowingBlocks> regrowingBlocks;
        private List<DevastationSource> devastationSources;
        private int nextSourceId = 1; // Counter for generating unique source IDs
        private int cleanupTickCounter = 0; // Counter for periodic cleanup
        
        // Config loaded from JSON file
        private SpreadingDevastationConfig config;
        
        // Runtime state (saved to world, not config file)
        private double speedMultiplier = 1.0; // Current speed (can be changed via command)
        private bool isPaused = false; // When true, all devastation processing stops
        private int maxSources = 20; // Current max sources cap
        private bool requireSourceAirContact = false; // If true, new metastasis sources must spawn near air
        private int minSourceYLevel = 0; // Current min Y level

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            
            // Load config from ModConfig folder (or create with defaults)
            LoadConfig();
            
            // Apply config defaults to runtime state
            ApplyConfigDefaults();
            
            // Check for rifts and manual sources (interval from config)
            api.Event.RegisterGameTickListener(SpreadDevastationFromRifts, config.DevastationTickIntervalMs);
            
            // Check for block regeneration (interval from config)
            api.Event.RegisterGameTickListener(RegenerateBlocks, config.RegenerationTickIntervalMs);
            
            // Save/load events
            api.Event.SaveGameLoaded += OnSaveGameLoading;
            api.Event.GameWorldSave += OnSaveGameSaving;
            
            // Register commands
            api.RegisterCommand("devastate", "Manage devastation sources", 
                "[add|remove|list]", OnDevastateCommand, Privilege.controlserver);
            api.RegisterCommand("devastationspeed", "Set devastation spread speed multiplier", 
                "[multiplier]", OnDevastationSpeedCommand, Privilege.controlserver);
            api.RegisterCommand("devastationconfig", "Reload devastation config from file", 
                "", OnDevastationConfigCommand, Privilege.controlserver);
        }
        
        private void LoadConfig()
        {
            try
            {
                config = sapi.LoadModConfig<SpreadingDevastationConfig>("SpreadingDevastationConfig.json");
                
                if (config == null)
                {
                    // First time - create default config
                    config = new SpreadingDevastationConfig();
                    sapi.Logger.Notification("SpreadingDevastation: Created default config file");
                }
                
                // Always save to ensure new properties are added to existing config files
                sapi.StoreModConfig(config, "SpreadingDevastationConfig.json");
                sapi.Logger.Notification("SpreadingDevastation: Config loaded successfully");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error loading config: {ex.Message}");
                config = new SpreadingDevastationConfig();
            }
        }
        
        private void ApplyConfigDefaults()
        {
            // Apply config values as defaults for runtime state
            // These can be overridden by world save data or commands
            speedMultiplier = config.SpeedMultiplier;
            isPaused = config.StartPaused;
            maxSources = config.MaxSources;
            requireSourceAirContact = config.RequireSourceAirContact;
            minSourceYLevel = config.MinSourceYLevel;
        }
        
        private void OnDevastationConfigCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            LoadConfig();
            player.SendMessage(groupId, "SpreadingDevastation config reloaded from file.", EnumChatType.CommandSuccess);
            player.SendMessage(groupId, $"Note: Some settings (tick intervals) require a server restart to take effect.", EnumChatType.Notification);
            player.SendMessage(groupId, $"Config location: ModConfig/SpreadingDevastationConfig.json", EnumChatType.Notification);
        }

        private void OnSaveGameLoading()
        {
            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData("regrowingBlocks");
                regrowingBlocks = data == null ? new List<RegrowingBlocks>() : SerializerUtil.Deserialize<List<RegrowingBlocks>>(data);
                
                byte[] sourcesData = sapi.WorldManager.SaveGame.GetData("devastationSources");
                devastationSources = sourcesData == null ? new List<DevastationSource>() : SerializerUtil.Deserialize<List<DevastationSource>>(sourcesData);
                
                byte[] speedData = sapi.WorldManager.SaveGame.GetData("devastationSpeedMultiplier");
                speedMultiplier = speedData == null ? 1.0 : SerializerUtil.Deserialize<double>(speedData);
                
                byte[] pausedData = sapi.WorldManager.SaveGame.GetData("devastationPaused");
                isPaused = pausedData == null ? false : SerializerUtil.Deserialize<bool>(pausedData);
                
                byte[] maxSourcesData = sapi.WorldManager.SaveGame.GetData("devastationMaxSources");
                maxSources = maxSourcesData == null ? 20 : SerializerUtil.Deserialize<int>(maxSourcesData);
                
                byte[] nextIdData = sapi.WorldManager.SaveGame.GetData("devastationNextSourceId");
                nextSourceId = nextIdData == null ? 1 : SerializerUtil.Deserialize<int>(nextIdData);
                
                byte[] airContactData = sapi.WorldManager.SaveGame.GetData("devastationRequireSourceAirContact");
                requireSourceAirContact = airContactData == null ? false : SerializerUtil.Deserialize<bool>(airContactData);
                
                byte[] minYData = sapi.WorldManager.SaveGame.GetData("devastationMinSourceYLevel");
                minSourceYLevel = minYData == null ? 0 : SerializerUtil.Deserialize<int>(minYData);
                
                sapi.Logger.Notification($"SpreadingDevastation: Loaded {devastationSources?.Count ?? 0} sources, {regrowingBlocks?.Count ?? 0} regrowing blocks");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error loading save data: {ex.Message}");
                // Initialize empty lists if loading failed
                regrowingBlocks = regrowingBlocks ?? new List<RegrowingBlocks>();
                devastationSources = devastationSources ?? new List<DevastationSource>();
            }
        }

        private void OnSaveGameSaving()
        {
            try
            {
                sapi.WorldManager.SaveGame.StoreData("regrowingBlocks", SerializerUtil.Serialize(regrowingBlocks ?? new List<RegrowingBlocks>()));
                sapi.WorldManager.SaveGame.StoreData("devastationSources", SerializerUtil.Serialize(devastationSources ?? new List<DevastationSource>()));
                sapi.WorldManager.SaveGame.StoreData("devastationSpeedMultiplier", SerializerUtil.Serialize(speedMultiplier));
                sapi.WorldManager.SaveGame.StoreData("devastationPaused", SerializerUtil.Serialize(isPaused));
                sapi.WorldManager.SaveGame.StoreData("devastationMaxSources", SerializerUtil.Serialize(maxSources));
                sapi.WorldManager.SaveGame.StoreData("devastationNextSourceId", SerializerUtil.Serialize(nextSourceId));
                sapi.WorldManager.SaveGame.StoreData("devastationRequireSourceAirContact", SerializerUtil.Serialize(requireSourceAirContact));
                sapi.WorldManager.SaveGame.StoreData("devastationMinSourceYLevel", SerializerUtil.Serialize(minSourceYLevel));
                
                sapi.Logger.VerboseDebug($"SpreadingDevastation: Saved {devastationSources?.Count ?? 0} sources, {regrowingBlocks?.Count ?? 0} regrowing blocks");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error saving data: {ex.Message}");
            }
        }

        private void OnDevastationSpeedCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            string speedArg = args.PopWord();
            
            if (string.IsNullOrEmpty(speedArg))
            {
                player.SendMessage(groupId, $"Current devastation speed: {speedMultiplier:F2}x", EnumChatType.Notification);
                player.SendMessage(groupId, "Usage: /devastationspeed <multiplier>  (e.g., 0.5 for half speed, 5 for 5x speed)", EnumChatType.Notification);
                return;
            }
            
            if (double.TryParse(speedArg, out double newSpeed))
            {
                // Clamp to reasonable values
                newSpeed = Math.Clamp(newSpeed, 0.01, 100.0);
                speedMultiplier = newSpeed;
                player.SendMessage(groupId, $"Devastation speed set to {speedMultiplier:F2}x", EnumChatType.CommandSuccess);
            }
            else
            {
                player.SendMessage(groupId, "Invalid speed value. Use a number like 0.5, 1, 2, 5, etc.", EnumChatType.CommandError);
            }
        }

        private void OnDevastateCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            string subcommand = args.PopWord();
            
            switch (subcommand)
            {
                case "add":
                    AddDevastationSource(player, groupId, args, false);
                    break;
                    
                case "heal":
                    AddDevastationSource(player, groupId, args, true);
                    break;
                    
                case "remove":
                    string removeArg = args.PopWord();
                    if (removeArg == "all")
                    {
                        int count = devastationSources.Count;
                        devastationSources.Clear();
                        player.SendMessage(groupId, $"Removed all {count} devastation sources", EnumChatType.CommandSuccess);
                    }
                    else if (removeArg == "saturated")
                    {
                        int removed = devastationSources.RemoveAll(s => s.IsSaturated);
                        player.SendMessage(groupId, $"Removed {removed} saturated devastation sources", EnumChatType.CommandSuccess);
                    }
                    else if (removeArg == "metastasis")
                    {
                        int removed = devastationSources.RemoveAll(s => s.IsMetastasis);
                        player.SendMessage(groupId, $"Removed {removed} metastasis sources (kept original sources)", EnumChatType.CommandSuccess);
                    }
                    else
                    {
                        // Put the arg back and do normal single remove
                        BlockSelection blockSel = player.CurrentBlockSelection;
                        if (blockSel == null)
                        {
                            player.SendMessage(groupId, "Look at a block to remove it as a devastation source, or use 'remove all/saturated/metastasis'", EnumChatType.CommandError);
                            return;
                        }
                        
                        BlockPos pos = blockSel.Position;
                        int removed = devastationSources.RemoveAll(s => s.Pos.Equals(pos));
                        
                        if (removed > 0)
                        {
                            player.SendMessage(groupId, $"Removed devastation source at {pos}", EnumChatType.CommandSuccess);
                        }
                        else
                        {
                            player.SendMessage(groupId, "No devastation source found at this location", EnumChatType.CommandError);
                        }
                    }
                    break;
                    
                case "list":
                    HandleListCommand(player, groupId, args);
                    break;
                    
                case "maxsources":
                    HandleMaxSourcesCommand(player, groupId, args);
                    break;
                
                case "stop":
                    isPaused = true;
                    player.SendMessage(groupId, "Devastation spreading STOPPED. Use '/devastate start' to resume.", EnumChatType.CommandSuccess);
                    break;
                    
                case "start":
                    isPaused = false;
                    player.SendMessage(groupId, "Devastation spreading STARTED.", EnumChatType.CommandSuccess);
                    break;
                    
                case "status":
                    string statusText = isPaused ? "PAUSED" : "RUNNING";
                    player.SendMessage(groupId, $"Devastation status: {statusText}", EnumChatType.Notification);
                    player.SendMessage(groupId, $"Speed multiplier: {speedMultiplier:F2}x", EnumChatType.Notification);
                    player.SendMessage(groupId, $"Active sources: {devastationSources.Count}/{maxSources}", EnumChatType.Notification);
                    player.SendMessage(groupId, $"Tracked blocks for regen: {regrowingBlocks?.Count ?? 0}", EnumChatType.Notification);
                    player.SendMessage(groupId, $"Source air contact required: {requireSourceAirContact}", EnumChatType.Notification);
                    player.SendMessage(groupId, $"Min source Y level: {minSourceYLevel}", EnumChatType.Notification);
                    break;
                    
                case "aircontact":
                    HandleAirContactCommand(player, groupId, args);
                    break;
                    
                case "miny":
                    HandleMinYCommand(player, groupId, args);
                    break;
                    
                case "debug":
                    HandleDebugCommand(player, groupId, args);
                    break;
                    
                case "maxattempts":
                    HandleMaxAttemptsCommand(player, groupId, args);
                    break;
                
                case "forcespawn":
                    HandleForceSpawnCommand(player, groupId, args);
                    break;
                    
                default:
                    player.SendMessage(groupId, "Usage: /devastate [add|heal|remove|list|maxsources|stop|start|status|aircontact|miny]", EnumChatType.CommandError);
                    player.SendMessage(groupId, "Examples:", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate add range 16 amount 10  - Spread devastation (will spawn metastasis)", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate heal range 16 amount 10 - Heal devastation", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate remove all              - Remove all sources", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate remove saturated        - Remove only saturated sources", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate remove metastasis       - Remove metastasis (keep originals)", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate list [count|summary]    - List sources (default 10, 'summary' for generation counts)", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate maxsources <number>     - Set max sources cap (currently {0})", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate stop                    - Pause all devastation", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate start                   - Resume devastation", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate aircontact [on|off]     - New sources must spawn near air (surface spreading)", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate miny <level>            - Min Y level for new metastasis sources (0=no limit)", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate debug                   - Debug info for source at looked-at block", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate maxattempts <num>       - Max failed spawn attempts before saturated", EnumChatType.CommandError);
                    player.SendMessage(groupId, "  /devastate forcespawn              - Force spawn child from looked-at source", EnumChatType.CommandError);
                    player.SendMessage(groupId, "Config: /devastationconfig           - Reload config from file", EnumChatType.CommandError);
                    break;
            }
        }

        private void AddDevastationSource(IServerPlayer player, int groupId, CmdArgs args, bool isHealing)
        {
            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                player.SendMessage(groupId, $"Look at a block to mark it as a {(isHealing ? "healing" : "devastation")} source", EnumChatType.CommandError);
                return;
            }
            
            BlockPos pos = blockSel.Position.Copy();
            
            // Check if already exists
            if (devastationSources.Any(s => s.Pos.Equals(pos)))
            {
                player.SendMessage(groupId, "This block is already a source (use remove to change it)", EnumChatType.CommandError);
                return;
            }
            
            // Parse optional parameters (using config defaults)
            int range = config.DefaultRange;
            int amount = config.DefaultAmount;
            
            string arg;
            while ((arg = args.PopWord()) != null)
            {
                if (arg == "range")
                {
                    string rangeStr = args.PopWord();
                    if (rangeStr != null && int.TryParse(rangeStr, out int parsedRange))
                    {
                        range = Math.Clamp(parsedRange, 1, 128);
                    }
                }
                else if (arg == "amount")
                {
                    string amountStr = args.PopWord();
                    if (amountStr != null && int.TryParse(amountStr, out int parsedAmount))
                    {
                        amount = Math.Clamp(parsedAmount, 1, 100);
                    }
                }
            }
            
            // If at cap, remove oldest source to make room
            if (devastationSources.Count >= maxSources)
            {
                RemoveOldestSources(1);
                player.SendMessage(groupId, $"At source cap ({maxSources}) - removed oldest source to make room", EnumChatType.Notification);
            }
            
            devastationSources.Add(new DevastationSource 
            { 
                Pos = pos,
                Range = range,
                Amount = amount,
                CurrentRadius = Math.Min(config.DefaultStartRadius, range),
                IsHealing = isHealing,
                SourceId = GenerateSourceId(),
                MetastasisThreshold = config.MetastasisThreshold,
                MaxGenerationLevel = config.MaxGenerationLevel,
                IsProtected = true // Manually added sources are protected from auto-removal
            });
            
            string action = isHealing ? "healing" : "devastation";
            player.SendMessage(groupId, $"Added {action} source at {pos} (range: {range}, amount: {amount} blocks per tick)", EnumChatType.CommandSuccess);
        }

        private void HandleMaxSourcesCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            string maxArg = args.PopWord();
            
            if (string.IsNullOrEmpty(maxArg))
            {
                player.SendMessage(groupId, $"Current max sources cap: {maxSources}", EnumChatType.Notification);
                player.SendMessage(groupId, $"Active sources: {devastationSources.Count}/{maxSources}", EnumChatType.Notification);
                player.SendMessage(groupId, "Usage: /devastate maxsources <number>  (e.g., 20, 50, 100)", EnumChatType.Notification);
                return;
            }
            
            if (int.TryParse(maxArg, out int newMax))
            {
                // Clamp to reasonable values
                newMax = Math.Clamp(newMax, 1, 1000);
                maxSources = newMax;
                player.SendMessage(groupId, $"Max sources cap set to {maxSources}", EnumChatType.CommandSuccess);
                
                if (devastationSources.Count >= maxSources)
                {
                    player.SendMessage(groupId, $"Warning: Already at or above cap ({devastationSources.Count} sources). No new metastasis will spawn.", EnumChatType.Notification);
                }
            }
            else
            {
                player.SendMessage(groupId, "Invalid number. Use a value like 20, 50, 100, etc.", EnumChatType.CommandError);
            }
        }

        private void HandleAirContactCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            string arg = args.PopWord();
            
            if (string.IsNullOrEmpty(arg))
            {
                player.SendMessage(groupId, $"Source air contact requirement: {(requireSourceAirContact ? "ON" : "OFF")}", EnumChatType.Notification);
                player.SendMessage(groupId, "When ON, new metastasis sources can only spawn at positions adjacent to air.", EnumChatType.Notification);
                player.SendMessage(groupId, "This keeps devastation spreading along surfaces rather than deep underground.", EnumChatType.Notification);
                player.SendMessage(groupId, "Note: Any block can still be devastated, this only affects where NEW sources spawn.", EnumChatType.Notification);
                player.SendMessage(groupId, "Usage: /devastate aircontact [on|off]", EnumChatType.Notification);
                return;
            }
            
            if (arg.ToLower() == "on" || arg == "true" || arg == "1")
            {
                requireSourceAirContact = true;
                player.SendMessage(groupId, "Source air contact: ON - new metastasis sources must spawn near air", EnumChatType.CommandSuccess);
            }
            else if (arg.ToLower() == "off" || arg == "false" || arg == "0")
            {
                requireSourceAirContact = false;
                player.SendMessage(groupId, "Source air contact: OFF - sources can spawn anywhere", EnumChatType.CommandSuccess);
            }
            else
            {
                player.SendMessage(groupId, "Invalid value. Use 'on' or 'off'.", EnumChatType.CommandError);
            }
        }

        private void HandleForceSpawnCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            BlockSelection blockSel = player.CurrentBlockSelection;
            
            // Find source - either looked at or first one
            DevastationSource sourceToSpawn = null;
            
            if (blockSel != null)
            {
                sourceToSpawn = devastationSources.FirstOrDefault(s => s.Pos.Equals(blockSel.Position));
            }
            
            if (sourceToSpawn == null && devastationSources.Count > 0)
            {
                sourceToSpawn = devastationSources.First(s => !s.IsSaturated && !s.IsHealing);
            }
            
            if (sourceToSpawn == null)
            {
                player.SendMessage(groupId, "No active devastation source to force spawn from", EnumChatType.CommandError);
                return;
            }
            
            player.SendMessage(groupId, $"Force spawning from source #{sourceToSpawn.SourceId} at {sourceToSpawn.Pos}...", EnumChatType.Notification);
            
            // Bypass all conditions and try to spawn
            sourceToSpawn.IsSaturated = false; // Reset saturated flag
            sourceToSpawn.FailedChildSpawnAttempts = 0; // Reset failure counter
            
            // Try spawning
            int beforeCount = devastationSources.Count;
            SpawnMetastasisSources(sourceToSpawn);
            int afterCount = devastationSources.Count;
            
            int spawned = afterCount - beforeCount;
            if (spawned > 0)
            {
                player.SendMessage(groupId, $"Successfully spawned {spawned} child source(s)!", EnumChatType.CommandSuccess);
            }
            else
            {
                player.SendMessage(groupId, $"Failed to spawn children. FailedAttempts now: {sourceToSpawn.FailedChildSpawnAttempts}", EnumChatType.CommandError);
                player.SendMessage(groupId, "Use /devastate debug to see why position search is failing", EnumChatType.Notification);
            }
        }
        
        private void HandleMaxAttemptsCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            string arg = args.PopWord();
            
            if (string.IsNullOrEmpty(arg))
            {
                player.SendMessage(groupId, $"Max child spawn attempts: {config.MaxFailedChildSpawnAttempts}", EnumChatType.Notification);
                player.SendMessage(groupId, "Sources must fail this many times to find spawn positions before being marked as saturated.", EnumChatType.Notification);
                player.SendMessage(groupId, "Usage: /devastate maxattempts <number>  (e.g., 10, 20, 50)", EnumChatType.Notification);
                return;
            }
            
            if (int.TryParse(arg, out int newMax))
            {
                newMax = Math.Clamp(newMax, 1, 1000);
                config.MaxFailedChildSpawnAttempts = newMax;
                player.SendMessage(groupId, $"Max child spawn attempts set to {newMax}", EnumChatType.CommandSuccess);
                
                // Save config
                sapi.StoreModConfig(config, "SpreadingDevastationConfig.json");
            }
            else
            {
                player.SendMessage(groupId, "Invalid number. Use a value like 10, 20, 50, etc.", EnumChatType.CommandError);
            }
        }
        
        private void HandleDebugCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            BlockSelection blockSel = player.CurrentBlockSelection;
            
            // Find source to debug - either looked at or first one
            DevastationSource sourceToDebug = null;
            
            if (blockSel != null)
            {
                sourceToDebug = devastationSources.FirstOrDefault(s => s.Pos.Equals(blockSel.Position));
            }
            
            if (sourceToDebug == null && devastationSources.Count > 0)
            {
                sourceToDebug = devastationSources.First();
                player.SendMessage(groupId, "(No source at looked-at block, showing first source)", EnumChatType.Notification);
            }
            
            if (sourceToDebug == null)
            {
                player.SendMessage(groupId, "No devastation sources to debug", EnumChatType.CommandError);
                return;
            }
            
            player.SendMessage(groupId, $"=== Debug Source #{sourceToDebug.SourceId} at {sourceToDebug.Pos} ===", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Status: {GetSourceStatusLabel(sourceToDebug)}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Range: {sourceToDebug.Range}, CurrentRadius: {sourceToDebug.CurrentRadius:F1}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  BlocksSinceLastMetastasis: {sourceToDebug.BlocksSinceLastMetastasis}/{sourceToDebug.MetastasisThreshold}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  BlocksDevastatedTotal: {sourceToDebug.BlocksDevastatedTotal}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  FailedChildSpawnAttempts: {sourceToDebug.FailedChildSpawnAttempts}/{config.MaxFailedChildSpawnAttempts}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  ChildrenSpawned: {sourceToDebug.ChildrenSpawned}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  IsSaturated: {sourceToDebug.IsSaturated}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  StallCounter: {sourceToDebug.StallCounter}", EnumChatType.Notification);
            
            // Calculate saturation
            double saturation = CalculateLocalDevastationPercent(sourceToDebug.Pos.ToVec3d(), sourceToDebug.CurrentRadius);
            player.SendMessage(groupId, $"  LocalSaturation: {saturation:P1} (threshold: {config.SaturationThreshold:P0})", EnumChatType.Notification);
            
            // Check spawn conditions
            player.SendMessage(groupId, "  --- Spawn Conditions ---", EnumChatType.Notification);
            bool cond1 = !sourceToDebug.IsHealing;
            bool cond2 = !sourceToDebug.IsSaturated;
            bool cond3 = sourceToDebug.BlocksSinceLastMetastasis >= sourceToDebug.MetastasisThreshold;
            bool cond4 = sourceToDebug.CurrentRadius >= sourceToDebug.Range;
            bool cond5 = saturation >= config.SaturationThreshold;
            player.SendMessage(groupId, $"  NotHealing: {cond1}, NotSaturated: {cond2}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  BlocksReached: {cond3} ({sourceToDebug.BlocksSinceLastMetastasis}>={sourceToDebug.MetastasisThreshold})", EnumChatType.Notification);
            player.SendMessage(groupId, $"  RadiusReached: {cond4} ({sourceToDebug.CurrentRadius:F1}>={sourceToDebug.Range})", EnumChatType.Notification);
            player.SendMessage(groupId, $"  SaturationReached: {cond5} ({saturation:P1}>={config.SaturationThreshold:P0})", EnumChatType.Notification);
            player.SendMessage(groupId, $"  ALL CONDITIONS MET: {cond1 && cond2 && cond3 && cond4 && cond5}", EnumChatType.Notification);
            
            // Test spawn position search
            player.SendMessage(groupId, "  --- Testing Spawn Position Search ---", EnumChatType.Notification);
            double minSearchRadius = sourceToDebug.CurrentRadius * 1.5;
            double maxSearchRadius = sourceToDebug.CurrentRadius * 2.5;
            player.SendMessage(groupId, $"  Search radius: {minSearchRadius:F1} to {maxSearchRadius:F1}", EnumChatType.Notification);
            
            bool blockedByAirContact;
            var testPositions = FindMetastasisSpawnPositions(sourceToDebug, 3, out blockedByAirContact);
            player.SendMessage(groupId, $"  Positions found: {testPositions.Count}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  BlockedByAirContact: {blockedByAirContact}", EnumChatType.Notification);
            
            if (testPositions.Count > 0)
            {
                foreach (var pos in testPositions)
                {
                    int nearby = CountNonDevastatedNearby(pos, 4);
                    player.SendMessage(groupId, $"    Found: {pos} (nonDev nearby: {nearby})", EnumChatType.Notification);
                }
            }
            else
            {
                // Try long-range
                bool blockedLongRange;
                var longRangePositions = FindLongRangeMetastasisPositions(sourceToDebug, 3, out blockedLongRange);
                player.SendMessage(groupId, $"  Long-range positions found: {longRangePositions.Count}", EnumChatType.Notification);
                player.SendMessage(groupId, $"  Long-range blockedByAirContact: {blockedLongRange}", EnumChatType.Notification);
            }
        }
        
        private void HandleMinYCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            string arg = args.PopWord();
            
            if (string.IsNullOrEmpty(arg))
            {
                player.SendMessage(groupId, $"Minimum Y level for new metastasis sources: {minSourceYLevel}", EnumChatType.Notification);
                player.SendMessage(groupId, "New devastation sources cannot spawn below this Y level.", EnumChatType.Notification);
                player.SendMessage(groupId, "Set to 0 for no limit, or higher (e.g., 80) to prevent deep underground spreading.", EnumChatType.Notification);
                player.SendMessage(groupId, "Usage: /devastate miny <level>  (e.g., 0, 50, 80)", EnumChatType.Notification);
                return;
            }
            
            if (int.TryParse(arg, out int newMinY))
            {
                // Clamp to reasonable values (0 to 256)
                newMinY = Math.Clamp(newMinY, 0, 256);
                minSourceYLevel = newMinY;
                
                if (newMinY == 0)
                {
                    player.SendMessage(groupId, "Min Y level set to 0 (no limit) - sources can spawn at any depth", EnumChatType.CommandSuccess);
                }
                else
                {
                    player.SendMessage(groupId, $"Min Y level set to {minSourceYLevel} - new sources cannot spawn below Y={minSourceYLevel}", EnumChatType.CommandSuccess);
                }
            }
            else
            {
                player.SendMessage(groupId, "Invalid number. Use a value like 0, 50, 80, etc.", EnumChatType.CommandError);
            }
        }

        private void HandleListCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (devastationSources.Count == 0)
            {
                player.SendMessage(groupId, "No manual devastation sources set", EnumChatType.Notification);
                return;
            }
            
            string arg = args.PopWord();
            
            // Check if it's a summary request
            if (arg == "summary")
            {
                ShowListSummary(player, groupId);
                return;
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
            
            player.SendMessage(groupId, $"Devastation sources ({devastationSources.Count}/{maxSources} cap, {originalCount} original, {metastasisCount} metastasis, {saturatedCount} saturated):", EnumChatType.Notification);
            
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
                string statusInfo = GetSourceStatusLabel(source);
                string progressInfo = $"{source.BlocksSinceLastMetastasis}/{source.MetastasisThreshold}";
                string idInfo = !string.IsNullOrEmpty(source.SourceId) ? $"#{source.SourceId}" : "";
                
                player.SendMessage(groupId, 
                    $"  [{type}] [{genInfo}] [{statusInfo}]{idInfo} {source.Pos} R:{source.CurrentRadius:F0}/{source.Range} Tot:{source.BlocksDevastatedTotal} Prog:{progressInfo}", 
                    EnumChatType.Notification);
            }
            
            if (devastationSources.Count > limit)
            {
                player.SendMessage(groupId, $"  ... and {devastationSources.Count - limit} more. Use '/devastate list {limit + 10}' or '/devastate list summary'", EnumChatType.Notification);
            }
        }

        private void ShowListSummary(IServerPlayer player, int groupId)
        {
            int protectedCount = devastationSources.Count(s => s.IsProtected);
            int metastasisCount = devastationSources.Count(s => s.IsMetastasis);
            int healingCount = devastationSources.Count(s => s.IsHealing);
            
            // Count by status using the new labels
            int growingCount = devastationSources.Count(s => !s.IsHealing && !s.IsSaturated && 
                s.BlocksSinceLastMetastasis < s.MetastasisThreshold);
            int seedingCount = devastationSources.Count(s => !s.IsHealing && !s.IsSaturated && 
                s.BlocksSinceLastMetastasis >= s.MetastasisThreshold);
            int saturatedCount = devastationSources.Count(s => s.IsSaturated);
            int totalChildrenSpawned = devastationSources.Sum(s => s.ChildrenSpawned);
            
            player.SendMessage(groupId, $"=== Devastation Summary ({devastationSources.Count}/{maxSources} cap) ===", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Protected (manual): {protectedCount} (never auto-removed)", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Metastasis children: {metastasisCount}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Healing sources: {healingCount}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Growing (devastating): {growingCount}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Seeding (spawning children): {seedingCount}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Saturated (done): {saturatedCount}", EnumChatType.Notification);
            player.SendMessage(groupId, $"  Total children spawned: {totalChildrenSpawned}", EnumChatType.Notification);
            
            // Group by generation level
            var byGeneration = devastationSources
                .GroupBy(s => s.GenerationLevel)
                .OrderBy(g => g.Key)
                .ToList();
            
            if (byGeneration.Count > 0)
            {
                player.SendMessage(groupId, "  By Generation:", EnumChatType.Notification);
                foreach (var gen in byGeneration)
                {
                    int growing = gen.Count(s => !s.IsSaturated && s.BlocksSinceLastMetastasis < s.MetastasisThreshold);
                    int seeding = gen.Count(s => !s.IsSaturated && s.BlocksSinceLastMetastasis >= s.MetastasisThreshold);
                    int sat = gen.Count(s => s.IsSaturated);
                    long totalBlocks = gen.Sum(s => (long)s.BlocksDevastatedTotal);
                    string genLabel = gen.Key == 0 ? "Origin" : $"Gen {gen.Key}";
                    player.SendMessage(groupId, $"    {genLabel}: {gen.Count()} ({growing} growing, {seeding} seeding, {sat} sat) - {totalBlocks:N0} blocks", EnumChatType.Notification);
                }
            }
            
            // Total stats
            long grandTotalBlocks = devastationSources.Sum(s => (long)s.BlocksDevastatedTotal);
            player.SendMessage(groupId, $"  Total blocks devastated: {grandTotalBlocks:N0}", EnumChatType.Notification);
            
            if (devastationSources.Count >= maxSources)
            {
                player.SendMessage(groupId, "  ⚠ At source cap - oldest sources will be removed for new metastasis", EnumChatType.Notification);
            }
        }
        
        /// <summary>
        /// Gets a human-readable status label for a devastation source.
        /// </summary>
        private string GetSourceStatusLabel(DevastationSource source)
        {
            if (source.IsHealing)
            {
                return "healing";
            }
            
            if (source.IsSaturated)
            {
                return "saturated";
            }
            
            // Check if ready to spawn children
            if (source.BlocksSinceLastMetastasis >= source.MetastasisThreshold || source.ChildrenSpawned > 0)
            {
                // Seeding - ready to spawn or has spawned children
                string baseStatus = source.ChildrenSpawned > 0 ? $"seeding {source.ChildrenSpawned}" : "seeding";
                
                // Show failed attempts if any
                if (source.FailedChildSpawnAttempts > 0)
                {
                    baseStatus += $" (fail:{source.FailedChildSpawnAttempts}/{config.MaxFailedChildSpawnAttempts})";
                }
                return baseStatus;
            }
            
            // Still growing/devastating
            return "growing";
        }

        private string GenerateSourceId()
        {
            return (nextSourceId++).ToString();
        }

        private void RegenerateBlocks(float dt)
        {
            if (sapi == null || regrowingBlocks == null || regrowingBlocks.Count == 0)
            {
                return;
            }

            try
            {
                List<RegrowingBlocks> blocksToRemove = new List<RegrowingBlocks>();
                double currentHours = sapi.World.Calendar.TotalHours;
                
                // Rate limit: only regenerate up to configured blocks per tick to prevent lag spikes
                int maxRegenPerTick = config.MaxRegenPerTick;
                int regeneratedCount = 0;
                
                // Check each devastated block to see if it's time to regenerate
                foreach (RegrowingBlocks regrowingBlock in regrowingBlocks)
                {
                    if (regeneratedCount >= maxRegenPerTick) break;
                    
                    // Skip blocks with invalid data
                    if (regrowingBlock.Pos == null) 
                    {
                        blocksToRemove.Add(regrowingBlock);
                        continue;
                    }
                    
                    // Regenerate after configured in-game hours
                    // Also handle time going backwards gracefully (if LastTime > currentHours, reset it)
                    double timeDiff = currentHours - regrowingBlock.LastTime;
                    
                    // If time went backwards significantly (e.g., time manipulation), update the LastTime
                    if (timeDiff < -24.0) // More than a day backwards
                    {
                        regrowingBlock.LastTime = currentHours;
                        continue;
                    }
                    
                    if (timeDiff > config.RegenerationHours)
                    {
                        if (regrowingBlock.Out == "none")
                        {
                            // Replace with air
                            sapi.World.BlockAccessor.SetBlock(0, regrowingBlock.Pos);
                        }
                        else
                        {
                            // Replace with the original block type
                            Block block = sapi.World.GetBlock(new AssetLocation("game", regrowingBlock.Out));
                            if (block != null && block.Id != 0)
                            {
                                sapi.World.BlockAccessor.SetBlock(block.Id, regrowingBlock.Pos);
                            }
                            // If block not found, still remove from list to prevent infinite loops
                        }
                        
                        blocksToRemove.Add(regrowingBlock);
                        regeneratedCount++;
                    }
                }

                // Remove regenerated blocks from the tracking list
                foreach (RegrowingBlocks item in blocksToRemove)
                {
                    regrowingBlocks.Remove(item);
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash the server
                sapi.Logger.Error("SpreadingDevastation: Error in RegenerateBlocks: " + ex.Message);
            }
        }

        private void SpreadDevastationFromRifts(float dt)
        {
            // Skip all processing if paused
            if (isPaused) return;
            
            // Safety check
            if (sapi == null || devastationSources == null) return;
            
            try
            {
                // Get the rift system
                ModSystemRifts riftSystem = sapi.ModLoader.GetModSystem<ModSystemRifts>();
            
            // Spread from rifts (if rift system exists) - 1 block per rift per tick
            // Rifts don't use adaptive radius, they always search full 8 blocks
            if (riftSystem?.riftsById != null)
            {
                foreach (Rift rift in riftSystem.riftsById.Values)
                {
                    SpreadDevastationFromRift(rift.Position, 8, 1);
                }
            }
            
            // Spread from manual devastation sources
            List<DevastationSource> toRemove = new List<DevastationSource>();
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
                
                // Spread or heal the specified amount of blocks per tick
                int processed = source.IsHealing 
                    ? HealDevastationAroundPosition(source.Pos.ToVec3d(), source)
                    : SpreadDevastationAroundPosition(source.Pos.ToVec3d(), source);
                
                // Track success rate and adjust radius
                int effectiveAmount = Math.Max(1, (int)(source.Amount * speedMultiplier));
                source.SuccessfulAttempts += processed;
                source.TotalAttempts += effectiveAmount * 5; // We try up to 5 times per block
                
                // Check every 100 attempts if we should expand
                if (source.TotalAttempts >= 100)
                {
                    double successRate = (double)source.SuccessfulAttempts / source.TotalAttempts;
                    
                    // If success rate is below threshold, expand the search radius
                    if (successRate < config.ExpandRadiusThreshold && source.CurrentRadius < source.Range)
                    {
                        // Expand faster if really low success rate
                        double expansion = successRate < config.StallThreshold ? 4.0 : 2.0;
                        source.CurrentRadius = Math.Min(source.CurrentRadius + expansion, source.Range);
                        source.StallCounter = 0; // Reset stall counter when still expanding
                    }
                    // Track stalling: at max radius with very low success rate
                    else if (successRate < config.StallThreshold && source.CurrentRadius >= source.Range && !source.IsHealing)
                    {
                        source.StallCounter++;
                        
                        // After configured stall cycles, take action to keep spreading
                        if (source.StallCounter >= config.StallCyclesBeforeAction)
                        {
                            // Always try to spawn metastasis when stalled - this moves the frontier forward
                            // Even if local saturation is low (lots of air/water), we should try to leap past it
                            bool? spawnResult = SpawnMetastasisSourcesWithLongRangeSearch(source);
                            
                            if (spawnResult == true)
                            {
                                // Successfully spawned metastasis
                                source.StallCounter = 0;
                            }
                            else if (spawnResult == null)
                            {
                                // Blocked by air contact but viable land exists - keep trying
                                // Don't increment stall counter, don't mark saturated
                                source.StallCounter = config.StallCyclesBeforeAction; // Stay at action threshold to keep trying
                            }
                            else if (source.StallCounter >= config.StallCyclesBeforeSaturated)
                            {
                                // Tried multiple times with long-range search and couldn't find any viable land
                                // This source is truly in an impassable spot (surrounded by ocean/void)
                                source.IsSaturated = true;
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
                            SpawnMetastasisSources(source);
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
            if (devastationSources.Count < maxSources / 2) return;
            
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

        private void SpreadDevastationFromRift(Vec3d position, int range, int amount)
        {
            // Rifts use simple random spreading (no distance weighting or adaptive radius)
            int devastatedCount = 0;
            int maxAttempts = amount * 5;
            
            for (int attempt = 0; attempt < maxAttempts && devastatedCount < amount; attempt++)
            {
                int dirX = RandomNumberGenerator.GetInt32(2) == 1 ? 1 : -1;
                int dirY = RandomNumberGenerator.GetInt32(2) == 1 ? 1 : -1;
                int dirZ = RandomNumberGenerator.GetInt32(2) == 1 ? 1 : -1;

                int offsetX = RandomNumberGenerator.GetInt32(range) * dirX;
                int offsetY = RandomNumberGenerator.GetInt32(range) * dirY;
                int offsetZ = RandomNumberGenerator.GetInt32(range) * dirZ;

                BlockPos targetPos = new BlockPos(
                    (int)position.X + offsetX,
                    (int)position.Y + offsetY,
                    (int)position.Z + offsetZ
                );

                Block block = sapi.World.BlockAccessor.GetBlock(targetPos);
                
                if (block.Id == 0 || IsAlreadyDevastated(block)) continue;

                string devastatedBlock, regeneratesTo;
                if (TryGetDevastatedForm(block, out devastatedBlock, out regeneratesTo))
                {
                    Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedBlock));
                    sapi.World.BlockAccessor.SetBlock(newBlock.Id, targetPos);
                    
                    regrowingBlocks.Add(new RegrowingBlocks
                    {
                        Pos = targetPos,
                        Out = regeneratesTo,
                        LastTime = sapi.World.Calendar.TotalHours
                    });
                    
                    devastatedCount++;
                }
            }
        }

        private int SpreadDevastationAroundPosition(Vec3d position, DevastationSource source)
        {
            // Skip if this source is fully saturated
            if (source.IsSaturated) return 0;
            
            // Apply speed multiplier to effective amount
            int effectiveAmount = Math.Max(1, (int)(source.Amount * speedMultiplier));
            
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
            int effectiveAmount = Math.Max(1, (int)(source.Amount * speedMultiplier));
            
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
                    
                    // Remove from regrowing blocks list (instant heal, no need to track)
                    regrowingBlocks.RemoveAll(rb => rb.Pos.Equals(targetPos));
                    
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
        /// Checks if a block position has at least one adjacent air block.
        /// Used when requireAirContact is enabled to keep devastation on surfaces.
        /// </summary>
        private bool HasAirContact(BlockPos pos)
        {
            // Check all 6 adjacent positions for air
            BlockPos[] adjacentPositions = new BlockPos[]
            {
                new BlockPos(pos.X + 1, pos.Y, pos.Z),
                new BlockPos(pos.X - 1, pos.Y, pos.Z),
                new BlockPos(pos.X, pos.Y + 1, pos.Z),
                new BlockPos(pos.X, pos.Y - 1, pos.Z),
                new BlockPos(pos.X, pos.Y, pos.Z + 1),
                new BlockPos(pos.X, pos.Y, pos.Z - 1)
            };
            
            foreach (BlockPos adjPos in adjacentPositions)
            {
                Block adjBlock = sapi.World.BlockAccessor.GetBlock(adjPos);
                if (adjBlock == null || adjBlock.Id == 0)
                {
                    return true; // Found air
                }
            }
            
            return false; // No air contact
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
        /// Finds good positions on the edge of the current devastation for spawning metastasis points.
        /// Returns positions that have non-devastated blocks nearby.
        /// </summary>
        private List<BlockPos> FindMetastasisSpawnPositions(DevastationSource source, int count)
        {
            return FindMetastasisSpawnPositions(source, count, out _);
        }
        
        /// <summary>
        /// Finds good positions on the edge of the current devastation for spawning metastasis points.
        /// Uses pillar-checking strategy: for each X,Z position, searches vertically ±(PillarSearchHeight/2)
        /// from the source's Y level to find a valid position with air contact.
        /// Also outputs whether positions were blocked primarily by air contact requirement.
        /// </summary>
        private List<BlockPos> FindMetastasisSpawnPositions(DevastationSource source, int count, out bool blockedByAirContact)
        {
            List<BlockPos> candidates = new List<BlockPos>();
            List<BlockPos> selected = new List<BlockPos>();
            blockedByAirContact = false;
            
            // Search BEYOND the current devastated area to find fresh land
            // Use 1.5x to 2.5x the current radius to look well outside the devastated zone
            double minSearchRadius = source.CurrentRadius * 1.5;
            double maxSearchRadius = source.CurrentRadius * 2.5;
            int pillarHalfHeight = config.PillarSearchHeight / 2;
            
            // Track rejection reasons
            int rejectedByAirContact = 0;
            int rejectedByDevastated = 0;
            int rejectedByYLevel = 0;
            
            // If air contact is required, probe many more positions to find surface spots
            int baseProbeCount = count * 8;
            int probeCount = requireSourceAirContact ? baseProbeCount * 4 : baseProbeCount;
            
            // Find candidate positions BEYOND the edge of the devastated area
            for (int i = 0; i < probeCount; i++)
            {
                // Generate random X,Z position at varying distance beyond current radius
                double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                double searchRadius = minSearchRadius + (maxSearchRadius - minSearchRadius) * (RandomNumberGenerator.GetInt32(100) / 100.0);
                
                int offsetX = (int)(searchRadius * Math.Cos(angle));
                int offsetZ = (int)(searchRadius * Math.Sin(angle));
                
                int baseX = source.Pos.X + offsetX;
                int baseY = source.Pos.Y; // Start at source Y level
                int baseZ = source.Pos.Z + offsetZ;
                
                // Pillar search: check Y levels from source Y, alternating up and down
                // Order: 0, +1, -1, +2, -2, +3, -3, etc.
                BlockPos bestPillarPos = null;
                int bestViability = 0;
                bool pillarHadViableButBuried = false;
                bool pillarFullyDevastated = true;
                
                    // Extended pillar search: check more Y levels to find ground/viable positions
                    // Search down more aggressively to find terrain
                    int extendedSearchDown = pillarHalfHeight + 10; // Search further down to find ground
                    
                    for (int yOffset = 0; yOffset <= Math.Max(pillarHalfHeight, extendedSearchDown); yOffset++)
                    {
                        // Check both positive and negative offsets (except 0 which is only checked once)
                        // But search more down than up since we're often above terrain
                        int[] yOffsetsToCheck;
                        if (yOffset == 0)
                        {
                            yOffsetsToCheck = new int[] { 0 };
                        }
                        else if (yOffset <= pillarHalfHeight)
                        {
                            yOffsetsToCheck = new int[] { -yOffset, yOffset }; // Prioritize down
                        }
                        else
                        {
                            yOffsetsToCheck = new int[] { -yOffset }; // Only search down beyond half height
                        }
                        
                        foreach (int dy in yOffsetsToCheck)
                        {
                            BlockPos candidatePos = new BlockPos(baseX, baseY + dy, baseZ);
                            
                            // Check Y level limit for new sources
                            if (minSourceYLevel > 0 && candidatePos.Y < minSourceYLevel)
                            {
                                continue;
                            }
                            
                            // Check what's at this position
                            Block block = sapi.World.BlockAccessor.GetBlock(candidatePos);
                            
                            // Skip air blocks - we want to place sources in solid areas
                            if (block == null || block.Id == 0)
                            {
                                continue;
                            }
                            
                            // Score this position - how many non-devastated blocks are nearby?
                            int nonDevastatedNearby = CountNonDevastatedNearby(candidatePos, 4);
                            
                            // Check if area has viable blocks to devastate
                            // Only need 1+ non-devastated blocks to be worth spawning here
                            if (nonDevastatedNearby < 1)
                            {
                                // Area is completely devastated at this Y level
                                continue;
                            }
                            
                            // Found viable area at this Y level
                            pillarFullyDevastated = false;
                            
                            // Check air contact requirement for new sources
                            if (requireSourceAirContact && !HasAirContact(candidatePos))
                            {
                                // Position is buried, but area was viable - note this
                                pillarHadViableButBuried = true;
                                continue;
                            }
                            
                            // Valid candidate! Track the best one (prefer positions closer to source Y)
                            if (bestPillarPos == null || nonDevastatedNearby > bestViability)
                            {
                                bestPillarPos = candidatePos;
                                bestViability = nonDevastatedNearby;
                            }
                        }
                    }
                
                // Record why this probe failed (if it did)
                if (bestPillarPos != null)
                {
                    candidates.Add(bestPillarPos);
                }
                else if (pillarHadViableButBuried)
                {
                    rejectedByAirContact++;
                }
                else if (pillarFullyDevastated)
                {
                    rejectedByDevastated++;
                }
                else
                {
                    rejectedByYLevel++;
                }
            }
            
            // Determine if we're blocked primarily by air contact (not devastation)
            // This means there ARE viable areas, we just can't reach surface
            if (candidates.Count == 0 && rejectedByAirContact > rejectedByDevastated)
            {
                blockedByAirContact = true;
            }
            
            // Shuffle and pick the requested count
            candidates = candidates.OrderBy(x => RandomNumberGenerator.GetInt32(1000)).ToList();
            
            // Try to space them out - don't pick positions too close together
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
                    if (dist < minSearchRadius * 0.5) // At least half the radius apart
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                if (!tooClose)
                {
                    selected.Add(candidate);
                }
            }
            
            return selected;
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
        /// Spawns metastasis sources at the edge of a saturated source.
        /// Now spawns one child at a time with configurable delay between spawns.
        /// </summary>
        private void SpawnMetastasisSources(DevastationSource parentSource)
        {
            // Don't spawn from healing sources
            if (parentSource.IsHealing) return;
            
            // Check spawn delay (only after first child has been spawned)
            if (parentSource.ChildrenSpawned > 0)
            {
                double currentTime = sapi.World.Calendar.TotalHours;
                double delayHours = (config.ChildSpawnDelaySeconds / 3600.0) / speedMultiplier; // Convert seconds to hours, adjust for speed
                double timeSinceLastSpawn = currentTime - parentSource.LastChildSpawnTime;
                
                if (timeSinceLastSpawn < delayHours)
                {
                    // Still in cooldown - don't spawn yet
                    return;
                }
            }
            
            // Spawn one child at a time now
            int desiredCount = 1;
            
            // If at or near cap, remove oldest/saturated sources to make room
            int slotsNeeded = desiredCount - (maxSources - devastationSources.Count);
            if (slotsNeeded > 0)
            {
                RemoveOldestSources(slotsNeeded);
            }
            
            // Calculate how many we can actually spawn
            int maxPossible = maxSources - devastationSources.Count;
            int metastasisCount = Math.Min(desiredCount, maxPossible);
            
            if (metastasisCount <= 0)
            {
                // Still can't spawn (shouldn't happen, but safety check)
                return;
            }
            
            // Find good positions for new sources (track why positions might be rejected)
            bool blockedByAirContactEdge;
            List<BlockPos> spawnPositions = FindMetastasisSpawnPositions(parentSource, metastasisCount, out blockedByAirContactEdge);
            
            bool blockedByAirContactLongRange = false;
            if (spawnPositions.Count == 0)
            {
                // No good positions found at edge - try long-range search before giving up
                spawnPositions = FindLongRangeMetastasisPositions(parentSource, metastasisCount, out blockedByAirContactLongRange);
            }
            
            if (spawnPositions.Count == 0)
            {
                // No positions found - but WHY?
                if (blockedByAirContactEdge || blockedByAirContactLongRange)
                {
                    // Blocked by air contact requirement, but viable areas exist
                    // DON'T mark as saturated - keep trying to find surface access
                    // Reset counter to try again later
                    parentSource.BlocksSinceLastMetastasis = 0;
                    return;
                }
                
                // Failed to find viable positions - increment counter and try again later
                parentSource.FailedChildSpawnAttempts++;
                parentSource.BlocksSinceLastMetastasis = 0; // Reset to try again
                
                // Only mark as saturated after multiple failed attempts
                if (parentSource.FailedChildSpawnAttempts >= config.MaxFailedChildSpawnAttempts)
                {
                    parentSource.IsSaturated = true;
                }
                return;
            }
            
            // Successfully found positions - reset failure counter
            parentSource.FailedChildSpawnAttempts = 0;
            
            // Ensure parent has a source ID for tracking
            if (string.IsNullOrEmpty(parentSource.SourceId))
            {
                parentSource.SourceId = GenerateSourceId();
            }
            
            // Calculate child range with variation (50-150% of parent range)
            double variation = config.MetastasisRadiusVariation;
            double minRange = parentSource.Range * (1.0 - variation);
            double maxRange = parentSource.Range * (1.0 + variation);
            int childRange = (int)Math.Round(minRange + (maxRange - minRange) * (RandomNumberGenerator.GetInt32(100) / 100.0));
            childRange = Math.Max(4, childRange); // Minimum range of 4
            
            // Spawn new sources at each position
            foreach (BlockPos spawnPos in spawnPositions)
            {
                DevastationSource metastasis = new DevastationSource
                {
                    Pos = spawnPos.Copy(),
                    Range = childRange,
                    Amount = parentSource.Amount,
                    CurrentRadius = Math.Min(config.DefaultStartRadius, childRange), // Start small
                    IsHealing = false,
                    IsMetastasis = true,
                    GenerationLevel = parentSource.GenerationLevel + 1,
                    MetastasisThreshold = parentSource.MetastasisThreshold,
                    MaxGenerationLevel = parentSource.MaxGenerationLevel,
                    SourceId = GenerateSourceId(),
                    ParentSourceId = parentSource.SourceId
                };
                
                devastationSources.Add(metastasis);
                parentSource.ChildrenSpawned++;
            }
            
            // Record spawn time for delay tracking
            parentSource.LastChildSpawnTime = sapi.World.Calendar.TotalHours;
            
            // Reset parent's counter for next metastasis cycle
            parentSource.BlocksSinceLastMetastasis = 0;
            
            // DON'T mark as saturated immediately - let it continue spawning children
            // It will be marked saturated when it truly can't find more positions
        }
        
        /// <summary>
        /// Spawns metastasis with extended long-range search for when a source is stalled.
        /// Returns true if at least one metastasis was spawned, or null if blocked by air contact (should keep trying).
        /// </summary>
        private bool? SpawnMetastasisSourcesWithLongRangeSearch(DevastationSource parentSource)
        {
            if (parentSource.IsHealing) return false;
            
            // Check spawn delay (only after first child has been spawned)
            if (parentSource.ChildrenSpawned > 0)
            {
                double currentTime = sapi.World.Calendar.TotalHours;
                double delayHours = (config.ChildSpawnDelaySeconds / 3600.0) / speedMultiplier;
                double timeSinceLastSpawn = currentTime - parentSource.LastChildSpawnTime;
                
                if (timeSinceLastSpawn < delayHours)
                {
                    // Still in cooldown - return true to indicate we're not stuck, just waiting
                    return true;
                }
            }
            
            int desiredCount = 1; // Spawn one at a time
            
            // Make room if needed
            int slotsNeeded = desiredCount - (maxSources - devastationSources.Count);
            if (slotsNeeded > 0)
            {
                RemoveOldestSources(slotsNeeded);
            }
            
            int maxPossible = maxSources - devastationSources.Count;
            int metastasisCount = Math.Min(desiredCount, maxPossible);
            
            if (metastasisCount <= 0) return false;
            
            // Try normal edge positions first (track rejection reason)
            bool blockedByAirContactEdge;
            List<BlockPos> spawnPositions = FindMetastasisSpawnPositions(parentSource, metastasisCount, out blockedByAirContactEdge);
            
            // If that failed, try long-range search
            bool blockedByAirContactLongRange = false;
            if (spawnPositions.Count == 0)
            {
                spawnPositions = FindLongRangeMetastasisPositions(parentSource, metastasisCount, out blockedByAirContactLongRange);
            }
            
            if (spawnPositions.Count == 0)
            {
                // Return null if blocked by air contact (should keep trying), false if truly saturated
                if (blockedByAirContactEdge || blockedByAirContactLongRange)
                {
                    return null; // Keep trying - viable land exists but can't reach surface
                }
                return false; // No viable land found
            }
            
            // Ensure parent has a source ID
            if (string.IsNullOrEmpty(parentSource.SourceId))
            {
                parentSource.SourceId = GenerateSourceId();
            }
            
            // Calculate child range with variation (50-150% of parent range)
            double variation = config.MetastasisRadiusVariation;
            double minRange = parentSource.Range * (1.0 - variation);
            double maxRange = parentSource.Range * (1.0 + variation);
            int childRange = (int)Math.Round(minRange + (maxRange - minRange) * (RandomNumberGenerator.GetInt32(100) / 100.0));
            childRange = Math.Max(4, childRange); // Minimum range of 4
            
            // Spawn new sources
            foreach (BlockPos spawnPos in spawnPositions)
            {
                DevastationSource metastasis = new DevastationSource
                {
                    Pos = spawnPos.Copy(),
                    Range = childRange,
                    Amount = parentSource.Amount,
                    CurrentRadius = Math.Min(config.DefaultStartRadius, childRange),
                    IsHealing = false,
                    IsMetastasis = true,
                    GenerationLevel = parentSource.GenerationLevel + 1,
                    MetastasisThreshold = parentSource.MetastasisThreshold,
                    MaxGenerationLevel = parentSource.MaxGenerationLevel,
                    SourceId = GenerateSourceId(),
                    ParentSourceId = parentSource.SourceId
                };
                
                devastationSources.Add(metastasis);
                parentSource.ChildrenSpawned++;
            }
            
            // Record spawn time for delay tracking
            parentSource.LastChildSpawnTime = sapi.World.Calendar.TotalHours;
            
            parentSource.BlocksSinceLastMetastasis = 0;
            // DON'T mark as saturated - let parent continue spawning children
            return true;
        }
        
        /// <summary>
        /// Searches much further out (2x to 8x the normal range) to find undevastated land.
        /// This allows the devastation to "leap over" obstacles like oceans or voids.
        /// </summary>
        private List<BlockPos> FindLongRangeMetastasisPositions(DevastationSource source, int count)
        {
            return FindLongRangeMetastasisPositions(source, count, out _);
        }
        
        /// <summary>
        /// Searches much further out (2x to 8x the normal range) to find undevastated land.
        /// This allows the devastation to "leap over" obstacles like oceans or voids.
        /// Also outputs whether positions were blocked primarily by air contact requirement.
        /// </summary>
        private List<BlockPos> FindLongRangeMetastasisPositions(DevastationSource source, int count, out bool blockedByAirContact)
        {
            List<BlockPos> candidates = new List<BlockPos>();
            List<BlockPos> selected = new List<BlockPos>();
            blockedByAirContact = false;
            
            // Track rejection reasons across all distances
            int totalRejectedByAirContact = 0;
            int totalRejectedByDevastated = 0;
            
            // Search at multiple distance rings, starting further out
            int[] searchDistances = new int[] { 
                source.Range * 2, 
                source.Range * 4, 
                source.Range * 6,
                source.Range * 8 
            };
            
            int pillarHalfHeight = config.PillarSearchHeight / 2;
            
            foreach (int searchDist in searchDistances)
            {
                // Cap maximum search distance to prevent searching too far
                int cappedDist = Math.Min(searchDist, config.MaxLongRangeSearchDistance);
                
                // More probes for long-range search, even more if air contact required
                int baseProbeCount = count * 16;
                int probeCount = requireSourceAirContact ? baseProbeCount * 4 : baseProbeCount;
                
                for (int i = 0; i < probeCount; i++)
                {
                    // Generate random X,Z position at this distance (horizontal only)
                    double angle = RandomNumberGenerator.GetInt32(360) * Math.PI / 180.0;
                    
                    int offsetX = (int)(cappedDist * Math.Cos(angle));
                    int offsetZ = (int)(cappedDist * Math.Sin(angle));
                    
                    int baseX = source.Pos.X + offsetX;
                    int baseY = source.Pos.Y; // Start at source Y level
                    int baseZ = source.Pos.Z + offsetZ;
                    
                    // Pillar search: check Y levels from source Y, alternating up and down
                    BlockPos bestPillarPos = null;
                    int bestViability = 0;
                    bool pillarHadViableButBuried = false;
                    bool pillarFullyDevastated = true;
                    
                    for (int yOffset = 0; yOffset <= pillarHalfHeight; yOffset++)
                    {
                        int[] yOffsetsToCheck = yOffset == 0 ? new int[] { 0 } : new int[] { yOffset, -yOffset };
                        
                        foreach (int dy in yOffsetsToCheck)
                        {
                            BlockPos candidatePos = new BlockPos(baseX, baseY + dy, baseZ);
                            
                            // Check Y level limit for new sources
                            if (minSourceYLevel > 0 && candidatePos.Y < minSourceYLevel)
                            {
                                continue;
                            }
                            
                            // Check what's at this position
                            Block block = sapi.World.BlockAccessor.GetBlock(candidatePos);
                            
                            // Skip air blocks
                            if (block == null || block.Id == 0)
                            {
                                continue;
                            }
                            
                            // Check if there's viable land here (need more blocks for long-range)
                            int nonDevastatedNearby = CountNonDevastatedNearby(candidatePos, 6);
                            
                            if (nonDevastatedNearby <= config.LongRangeMinBlocksRequired)
                            {
                                continue; // Area is mostly devastated at this Y level
                            }
                            
                            // Found viable area at this Y level
                            pillarFullyDevastated = false;
                            
                            // Check air contact requirement for new sources
                            if (requireSourceAirContact && !HasAirContact(candidatePos))
                            {
                                pillarHadViableButBuried = true;
                                continue;
                            }
                            
                            // Valid candidate! Track the best one
                            if (bestPillarPos == null || nonDevastatedNearby > bestViability)
                            {
                                bestPillarPos = candidatePos;
                                bestViability = nonDevastatedNearby;
                            }
                        }
                    }
                    
                    // Record why this probe failed (if it did)
                    if (bestPillarPos != null)
                    {
                        candidates.Add(bestPillarPos);
                    }
                    else if (pillarHadViableButBuried)
                    {
                        totalRejectedByAirContact++;
                    }
                    else if (pillarFullyDevastated)
                    {
                        totalRejectedByDevastated++;
                    }
                }
                
                // If we found candidates at this distance, stop searching further
                if (candidates.Count >= count)
                {
                    break;
                }
            }
            
            // Determine if we're blocked primarily by air contact (not devastation)
            if (candidates.Count == 0 && totalRejectedByAirContact > totalRejectedByDevastated)
            {
                blockedByAirContact = true;
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
                if (!tooClose)
                {
                    foreach (var existingSource in devastationSources)
                    {
                        double dist = Math.Sqrt(
                            Math.Pow(candidate.X - existingSource.Pos.X, 2) +
                            Math.Pow(candidate.Y - existingSource.Pos.Y, 2) +
                            Math.Pow(candidate.Z - existingSource.Pos.Z, 2)
                        );
                        if (dist < source.Range * 0.5) // Not too close to existing sources
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }
                
                if (!tooClose)
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

            return devastatedBlock != "";
        }
    }
}

