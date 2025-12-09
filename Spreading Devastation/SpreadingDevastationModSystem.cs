using System;
using System.Collections.Generic;
using SpreadingDevastation.Commands;
using SpreadingDevastation.Models;
using SpreadingDevastation.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SpreadingDevastation
{
    /// <summary>
    /// Main mod system that coordinates all services.
    /// Follows Single Responsibility: Only handles initialization, tick management, and persistence.
    /// </summary>
    public class SpreadingDevastationModSystem : ModSystem
    {
        // Core API
        private ICoreServerAPI _api;
        
        // Configuration
        private SpreadingDevastationConfig _config;
        
        // Data
        private List<RegrowingBlock> _regrowingBlocks;
        private List<DevastationSource> _devastationSources;
        private bool _isPaused;
        
        // Services
        private BlockTransformationService _blockService;
        private PositionService _positionService;
        private SourceManager _sourceManager;
        private DevastationService _devastationService;
        private MetastasisService _metastasisService;
        private HauntingService _hauntingService;
        private ParticleService _particleService;
        private CommandHandler _commandHandler;
        
        // Tick counters
        private int _cleanupTickCounter;

        public override void StartServerSide(ICoreServerAPI api)
        {
            _api = api;
            
            LoadConfig();
            
            // Register tick listeners
            api.Event.RegisterGameTickListener(OnMainTick, 10);
            api.Event.RegisterGameTickListener(OnRegenerationTick, 1000);
            
            // Register save/load events
            api.Event.SaveGameLoaded += OnSaveGameLoading;
            api.Event.GameWorldSave += OnSaveGameSaving;
        }

        private void LoadConfig()
        {
            try
            {
                _config = _api.LoadModConfig<SpreadingDevastationConfig>("SpreadingDevastationConfig.json");
                if (_config == null)
                {
                    _config = new SpreadingDevastationConfig();
                    _api.StoreModConfig(_config, "SpreadingDevastationConfig.json");
                    _api.Logger.Notification("SpreadingDevastation: Created default config file");
                }
                else
                {
                    _api.Logger.Notification("SpreadingDevastation: Loaded config file");
                }
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"SpreadingDevastation: Error loading config: {ex.Message}");
                _config = new SpreadingDevastationConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                _api.StoreModConfig(_config, "SpreadingDevastationConfig.json");
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"SpreadingDevastation: Error saving config: {ex.Message}");
            }
        }

        private void InitializeServices()
        {
            // Initialize data structures if needed
            _regrowingBlocks ??= new List<RegrowingBlock>();
            _devastationSources ??= new List<DevastationSource>();
            
            // Create services with dependency injection
            _blockService = new BlockTransformationService(_api);
            _positionService = new PositionService(_api, _config, _blockService);
            _sourceManager = new SourceManager(_api, _config, _devastationSources);
            _devastationService = new DevastationService(_api, _config, _blockService, _positionService, _regrowingBlocks);
            _metastasisService = new MetastasisService(_api, _config, _positionService, _sourceManager);
            _hauntingService = new HauntingService(_api, _config, _positionService, _sourceManager);
            _particleService = new ParticleService(_api, _config);
            
            // Create command handler
            _commandHandler = new CommandHandler(
                _api,
                _config,
                _sourceManager,
                _hauntingService,
                _regrowingBlocks,
                SaveConfig,
                () => _isPaused,
                value => _isPaused = value);
            
            _commandHandler.RegisterCommands();
            
            // Register config reload command
            _api.ChatCommands.Create("devastationconfig")
                .WithDescription("Reload configuration from file")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => {
                    LoadConfig();
                    return TextCommandResult.Success("Configuration reloaded from ModConfig/SpreadingDevastationConfig.json");
                });
        }

        #region Save/Load

        private void OnSaveGameLoading()
        {
            try
            {
                // Load regrowing blocks (with backward compatibility for old field name)
                byte[] regrowData = _api.WorldManager.SaveGame.GetData("regrowingBlocks");
                if (regrowData != null)
                {
                    // Try to deserialize as new type first
                    try
                    {
                        _regrowingBlocks = SerializerUtil.Deserialize<List<RegrowingBlock>>(regrowData);
                    }
                    catch
                    {
                        // Fall back to legacy format
                        var legacyBlocks = SerializerUtil.Deserialize<List<LegacyRegrowingBlocks>>(regrowData);
                        _regrowingBlocks = new List<RegrowingBlock>();
                        if (legacyBlocks != null)
                        {
                            foreach (var legacy in legacyBlocks)
                            {
                                _regrowingBlocks.Add(new RegrowingBlock
                                {
                                    Pos = legacy.Pos,
                                    RegeneratesTo = legacy.Out,
                                    LastTime = legacy.LastTime
                                });
                            }
                        }
                    }
                }
                else
                {
                    _regrowingBlocks = new List<RegrowingBlock>();
                }

                // Load devastation sources
                byte[] sourcesData = _api.WorldManager.SaveGame.GetData("devastationSources");
                if (sourcesData != null)
                {
                    _devastationSources = SerializerUtil.Deserialize<List<DevastationSource>>(sourcesData);
                }
                else
                {
                    _devastationSources = new List<DevastationSource>();
                }

                // Load pause state
                byte[] pausedData = _api.WorldManager.SaveGame.GetData("devastationPaused");
                _isPaused = pausedData != null && SerializerUtil.Deserialize<bool>(pausedData);

                // Load next source ID
                byte[] nextIdData = _api.WorldManager.SaveGame.GetData("devastationNextSourceId");
                int nextSourceId = nextIdData != null ? SerializerUtil.Deserialize<int>(nextIdData) : 1;

                // Initialize services
                InitializeServices();
                _sourceManager.SetNextSourceId(nextSourceId);

                _api.Logger.Notification($"SpreadingDevastation: Loaded {_devastationSources?.Count ?? 0} sources, {_regrowingBlocks?.Count ?? 0} regrowing blocks");
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"SpreadingDevastation: Error loading save data: {ex.Message}");
                _regrowingBlocks ??= new List<RegrowingBlock>();
                _devastationSources ??= new List<DevastationSource>();
                InitializeServices();
            }
        }

        private void OnSaveGameSaving()
        {
            try
            {
                _api.WorldManager.SaveGame.StoreData("regrowingBlocks", 
                    SerializerUtil.Serialize(_regrowingBlocks ?? new List<RegrowingBlock>()));
                _api.WorldManager.SaveGame.StoreData("devastationSources", 
                    SerializerUtil.Serialize(_devastationSources ?? new List<DevastationSource>()));
                _api.WorldManager.SaveGame.StoreData("devastationPaused", 
                    SerializerUtil.Serialize(_isPaused));
                _api.WorldManager.SaveGame.StoreData("devastationNextSourceId", 
                    SerializerUtil.Serialize(_sourceManager?.GetNextSourceId() ?? 1));

                _api.Logger.VerboseDebug($"SpreadingDevastation: Saved {_devastationSources?.Count ?? 0} sources, {_regrowingBlocks?.Count ?? 0} regrowing blocks");
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"SpreadingDevastation: Error saving data: {ex.Message}");
            }
        }

        #endregion

        #region Tick Handlers

        private void OnMainTick(float dt)
        {
            if (_isPaused) return;
            if (_api == null || _devastationSources == null) return;

            try
            {
                ProcessRifts();
                ProcessSources();
                ProcessCleanup();
                _hauntingService?.Update();
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"SpreadingDevastation: Error in main tick: {ex.Message}");
            }
        }

        private void OnRegenerationTick(float dt)
        {
            if (_api == null) return;

            try
            {
                _devastationService?.ProcessRegeneration();
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"SpreadingDevastation: Error in regeneration tick: {ex.Message}");
            }
        }

        private void ProcessRifts()
        {
            ModSystemRifts riftSystem = _api.ModLoader.GetModSystem<ModSystemRifts>();
            if (riftSystem?.riftsById == null) return;

            foreach (Rift rift in riftSystem.riftsById.Values)
            {
                _devastationService.SpreadFromRift(rift.Position, 8, 1);
            }
        }

        private void ProcessSources()
        {
            // Remove invalid sources first
            _sourceManager.RemoveInvalidSources();

            double currentGameTime = _api.World.Calendar.TotalHours;

            foreach (var source in _sourceManager.GetAllSources())
            {
                // Spawn marker particles
                _particleService?.SpawnSourceMarker(source);

                // Process spreading or healing
                int processed = source.IsHealing
                    ? _devastationService.HealFromSource(source)
                    : _devastationService.SpreadFromSource(source);

                // Update source state
                _devastationService.UpdateSourceState(source, processed);

                // Process metastasis for non-healing sources
                if (!source.IsHealing && !source.IsSaturated)
                {
                    _metastasisService.ProcessMetastasis(source, currentGameTime);

                    // Handle stalled sources
                    if (_metastasisService.ShouldAttemptStalledSpawn(source))
                    {
                        _metastasisService.ProcessStalledMetastasis(source, currentGameTime);
                    }
                }
            }
        }

        private void ProcessCleanup()
        {
            _cleanupTickCounter++;
            if (_cleanupTickCounter >= 500)
            {
                _cleanupTickCounter = 0;
                _sourceManager?.CleanupSaturatedSources();
            }
        }

        #endregion
    }

    #region Legacy Compatibility

    /// <summary>
    /// Legacy format for backward compatibility during migration.
    /// </summary>
    [ProtoBuf.ProtoContract]
    internal class LegacyRegrowingBlocks
    {
        [ProtoBuf.ProtoMember(1)]
        public Vintagestory.API.MathTools.BlockPos Pos { get; set; }

        [ProtoBuf.ProtoMember(2)]
        public string Out { get; set; }

        [ProtoBuf.ProtoMember(3)]
        public double LastTime { get; set; }
    }

    #endregion
}
