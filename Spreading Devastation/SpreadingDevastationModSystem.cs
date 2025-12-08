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
        
        /// <summary>Hours before devastated blocks regenerate (default: 60.0)</summary>
        public double RegenerationHours { get; set; } = 60.0;
        
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

        private ICoreServerAPI sapi;
        private List<RegrowingBlocks> regrowingBlocks;
        private List<DevastationSource> devastationSources;
        private SpreadingDevastationConfig config;
        private bool isPaused = false; // When true, all devastation processing stops
        private int nextSourceId = 1; // Counter for generating unique source IDs
        private int cleanupTickCounter = 0; // Counter for periodic cleanup

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            
            // Load config
            LoadConfig();
            
            // Check for rifts and manual sources every 10ms (10x faster - 100 times per second)
            api.Event.RegisterGameTickListener(SpreadDevastationFromRifts, 10);
            
            // Check for block regeneration every 1000ms (once per second)
            api.Event.RegisterGameTickListener(RegenerateBlocks, 1000);
            
            // Save/load events
            api.Event.SaveGameLoaded += OnSaveGameLoading;
            api.Event.GameWorldSave += OnSaveGameSaving;
            
            // Register commands using the modern ChatCommand API
            api.ChatCommands.Create("devastate")
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
                .EndSubCommand();
            
            api.ChatCommands.Create("devastationspeed")
                .WithDescription("Set devastation spread speed multiplier")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.OptionalDouble("multiplier"))
                .HandleWith(HandleSpeedCommand);
            
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
                sapi.WorldManager.SaveGame.StoreData("devastationPaused", SerializerUtil.Serialize(isPaused));
                sapi.WorldManager.SaveGame.StoreData("devastationNextSourceId", SerializerUtil.Serialize(nextSourceId));
                
                sapi.Logger.VerboseDebug($"SpreadingDevastation: Saved {devastationSources?.Count ?? 0} sources, {regrowingBlocks?.Count ?? 0} regrowing blocks");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"SpreadingDevastation: Error saving data: {ex.Message}");
            }
        }

        #region Command Handlers

        private TextCommandResult HandleSpeedCommand(TextCommandCallingArgs args)
        {
            double? speedArg = args.Parsers[0].GetValue() as double?;
            
            if (!speedArg.HasValue)
            {
                return TextCommandResult.Success($"Current devastation speed: {config.SpeedMultiplier:F2}x\nUsage: /devastationspeed <multiplier> (e.g., 0.5 for half speed, 5 for 5x speed)");
            }
            
            double newSpeed = Math.Clamp(speedArg.Value, 0.01, 100.0);
            config.SpeedMultiplier = newSpeed;
            SaveConfig();
            return TextCommandResult.Success($"Devastation speed set to {config.SpeedMultiplier:F2}x");
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
                return ShowListSummary();
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
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Devastation sources ({devastationSources.Count}/{config.MaxSources} cap, {originalCount} original, {metastasisCount} metastasis, {saturatedCount} saturated):");
            
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
                
                sb.AppendLine($"  [{type}] [{genInfo}] {statusLabel}{idInfo} {source.Pos} R:{source.CurrentRadius:F0}/{source.Range} Tot:{source.BlocksDevastatedTotal}");
            }
            
            if (devastationSources.Count > limit)
            {
                sb.AppendLine($"  ... and {devastationSources.Count - limit} more. Use '/devastate list {limit + 10}' or '/devastate list summary'");
            }
            
            return TextCommandResult.Success(sb.ToString());
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

        private TextCommandResult ShowListSummary()
        {
            int protectedCount = devastationSources.Count(s => s.IsProtected);
            int metastasisCount = devastationSources.Count(s => s.IsMetastasis);
            int saturatedCount = devastationSources.Count(s => s.IsSaturated);
            int healingCount = devastationSources.Count(s => s.IsHealing);
            int growingCount = devastationSources.Count(s => !s.IsSaturated && !s.IsHealing && 
                (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range));
            int seedingCount = devastationSources.Count(s => !s.IsSaturated && !s.IsHealing && 
                s.BlocksSinceLastMetastasis >= s.MetastasisThreshold && s.CurrentRadius >= s.Range);
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Devastation Summary ({devastationSources.Count}/{config.MaxSources} cap) ===");
            sb.AppendLine($"  Protected (manual): {protectedCount} (never auto-removed)");
            sb.AppendLine($"  Metastasis children: {metastasisCount}");
            sb.AppendLine($"  Healing sources: {healingCount}");
            sb.AppendLine($"  Growing: {growingCount}");
            sb.AppendLine($"  Seeding (ready to spawn): {seedingCount}");
            sb.AppendLine($"  Saturated (done): {saturatedCount}");
            
            // Group by generation level
            var byGeneration = devastationSources
                .GroupBy(s => s.GenerationLevel)
                .OrderBy(g => g.Key)
                .ToList();
            
            if (byGeneration.Count > 0)
            {
                sb.AppendLine("  By Generation:");
                foreach (var gen in byGeneration)
                {
                    int growing = gen.Count(s => !s.IsSaturated && (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range));
                    int seeding = gen.Count(s => !s.IsSaturated && s.BlocksSinceLastMetastasis >= s.MetastasisThreshold && s.CurrentRadius >= s.Range);
                    int sat = gen.Count(s => s.IsSaturated);
                    long totalBlocks = gen.Sum(s => (long)s.BlocksDevastatedTotal);
                    string genLabel = gen.Key == 0 ? "Origin" : $"Gen {gen.Key}";
                    sb.AppendLine($"    {genLabel}: {gen.Count()} ({growing} growing, {seeding} seeding, {sat} saturated) - {totalBlocks:N0} blocks");
                }
            }
            
            // Total stats
            long grandTotalBlocks = devastationSources.Sum(s => (long)s.BlocksDevastatedTotal);
            sb.AppendLine($"  Total blocks devastated: {grandTotalBlocks:N0}");
            
            if (devastationSources.Count >= config.MaxSources)
            {
                sb.AppendLine("  ⚠ At source cap - oldest sources will be removed for new metastasis");
            }
            
            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult HandleMaxSourcesCommand(TextCommandCallingArgs args)
        {
            int? maxArg = args.Parsers[0].GetValue() as int?;
            
            if (!maxArg.HasValue)
            {
                return TextCommandResult.Success($"Current max sources cap: {config.MaxSources}\nActive sources: {devastationSources.Count}/{config.MaxSources}\nUsage: /devastate maxsources <number> (e.g., 20, 50, 100)");
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
                return TextCommandResult.Success($"Current max failed spawn attempts: {config.MaxFailedSpawnAttempts}\nUsage: /devastate maxattempts <number> (e.g., 5, 10, 20)");
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
                return TextCommandResult.Success($"Surface spreading (air contact): {status}\nUsage: /devastate aircontact [on|off]");
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

        private TextCommandResult HandleMinYCommand(TextCommandCallingArgs args)
        {
            int? levelArg = args.Parsers[0].GetValue() as int?;
            
            if (!levelArg.HasValue)
            {
                return TextCommandResult.Success($"Current minimum Y level: {config.MinYLevel}\nUsage: /devastate miny <level> (e.g., 0, -64, 50)");
            }
            
            config.MinYLevel = levelArg.Value;
            SaveConfig();
            
            return TextCommandResult.Success($"Minimum Y level for new sources set to {config.MinYLevel}");
        }

        private TextCommandResult HandleStatusCommand(TextCommandCallingArgs args)
        {
            string statusText = isPaused ? "PAUSED" : "RUNNING";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Devastation status: {statusText}");
            sb.AppendLine($"Speed multiplier: {config.SpeedMultiplier:F2}x");
            sb.AppendLine($"Active sources: {devastationSources.Count}/{config.MaxSources}");
            sb.AppendLine($"Tracked blocks for regen: {regrowingBlocks?.Count ?? 0}");
            sb.AppendLine($"Surface spreading: {(config.RequireSourceAirContact ? "ON" : "OFF")}");
            sb.AppendLine($"Min Y level: {config.MinYLevel}");
            sb.AppendLine($"Child spawn delay: {config.ChildSpawnDelaySeconds}s");
            sb.AppendLine($"Max failed attempts: {config.MaxFailedSpawnAttempts}");
            return TextCommandResult.Success(sb.ToString());
        }

        #endregion

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
                
                // Rate limit: only regenerate up to 50 blocks per tick to prevent lag spikes from time manipulation
                int maxRegenPerTick = 50;
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
                    
                    // Regenerate after configured hours
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

            return devastatedBlock != "";
        }
    }
}
