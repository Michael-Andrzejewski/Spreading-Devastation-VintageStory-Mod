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

        /// <summary>
        /// When enabled, devastation spreads diagonally (26 directions) instead of only cardinal (6 directions).
        /// This helps catch isolated blocks like leaves that only touch diagonally.
        /// (default: true)
        /// </summary>
        public bool DiagonalSpreadingEnabled { get; set; } = true;

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
        /// Temporal stability drain rate per 500ms tick when player is in devastated chunk (default: 0.0001)
        /// </summary>
        public double ChunkStabilityDrainRate { get; set; } = 0.0001;

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
        /// Fog density in devastated areas (default: 0.025). Higher = thicker fog.
        /// The official Devastation location uses 0.05.
        /// </summary>
        public float FogDensity { get; set; } = 0.025f;

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

        /// <summary>
        /// Flat fog density for horizon obscuring (default: 0.015).
        /// This creates a horizontal fog layer that obscures distant terrain and the horizon.
        /// Set to 0 to disable flat fog.
        /// </summary>
        public float FlatFogDensity { get; set; } = 0.015f;

        /// <summary>
        /// How strongly the flat fog density is applied (0.0-1.0, default: 0.8).
        /// </summary>
        public float FlatFogDensityWeight { get; set; } = 0.8f;

        /// <summary>
        /// Y offset for flat fog position relative to player (default: -50).
        /// Negative values place the fog layer below the player.
        /// </summary>
        public float FlatFogYOffset { get; set; } = -50f;

        /// <summary>
        /// Fog intensity multiplier for edge chunks (chunks with non-devastated neighbors).
        /// Default: 0.4 (40% intensity at edges). Range 0.0-1.0.
        /// </summary>
        public float FogEdgeIntensity { get; set; } = 0.4f;

        /// <summary>
        /// Fog intensity multiplier for interior chunks (chunks fully surrounded by devastation).
        /// Default: 1.2 (120% intensity in interior). Can exceed 1.0 for extra intensity.
        /// </summary>
        public float FogInteriorIntensity { get; set; } = 1.2f;

        /// <summary>
        /// Distance in blocks over which fog transitions from edge to full intensity.
        /// At distance 0 from non-devastated area, fog is at edge intensity.
        /// At this distance or beyond, fog is at full intensity.
        /// Default: 48 blocks (1.5 chunks).
        /// </summary>
        public float FogDistanceFullIntensity { get; set; } = 48f;

        /// <summary>
        /// Distance in blocks outside devastated areas where fog effect begins.
        /// At this distance, fog starts appearing (0% intensity).
        /// As you get closer, fog increases until entering the devastated chunk.
        /// Default: 20 blocks.
        /// </summary>
        public float FogApproachDistance { get; set; } = 20f;

        /// <summary>
        /// How fast the fog effect interpolates toward target intensity (per second).
        /// Higher values = faster response, lower values = smoother transitions.
        /// Default: 0.5 (takes ~2 seconds to fully transition).
        /// </summary>
        public float FogInterpolationSpeed { get; set; } = 0.5f;

        // === Animal Insanity Settings ===

        /// <summary>
        /// Whether animals in devastated chunks become permanently hostile (default: true).
        /// Insane animals will attack players on sight and remain hostile even after leaving devastated areas.
        /// </summary>
        public bool AnimalInsanityEnabled { get; set; } = true;

        /// <summary>
        /// Interval in seconds between animal insanity checks (default: 3.0).
        /// Lower values make animals go insane faster but use more CPU.
        /// </summary>
        public double AnimalInsanityCheckIntervalSeconds { get; set; } = 3.0;

        /// <summary>
        /// Radius in blocks around each player to search for animals to drive insane (default: 48).
        /// Animals outside this radius won't be affected even if in devastated chunks.
        /// </summary>
        public int AnimalInsanitySearchRadius { get; set; } = 48;

        /// <summary>
        /// Chance (0.0-1.0) for an animal to go insane each check while in a devastated chunk (default: 0.3).
        /// Lower values create more gradual onset of insanity.
        /// </summary>
        public double AnimalInsanityChance { get; set; } = 0.3;

        /// <summary>
        /// Comma-separated list of entity code prefixes for animals that can go insane (default: common animals).
        /// Use wildcards like "wolf*" to match variants. Set to "*" to affect all creatures.
        /// </summary>
        public string AnimalInsanityEntityCodes { get; set; } = "wolf,bear,hyena,boar,pig,sheep,goat,chicken,hare,fox,raccoon,deer,gazelle,moose,aurochs";

        /// <summary>
        /// Comma-separated list of entity code prefixes to exclude from insanity (default: hostile mobs).
        /// These entities will never go insane (they're typically already hostile).
        /// </summary>
        public string AnimalInsanityExcludeCodes { get; set; } = "drifter,locust,bell,bowtorn,eidolon,shiver";

        // === Trader Removal Settings ===

        /// <summary>
        /// Whether traders in devastated areas should be killed (default: true).
        /// Traders (humanoid-trader-*) within devastated chunks will die.
        /// Since traders spawn with their carts during world generation, killing them
        /// and devastating their structures prevents respawning.
        /// </summary>
        public bool TraderRemovalEnabled { get; set; } = true;

        /// <summary>
        /// Interval in seconds between trader removal checks (default: 5.0).
        /// Lower values remove traders faster but use slightly more CPU.
        /// </summary>
        public double TraderRemovalCheckIntervalSeconds { get; set; } = 5.0;

        /// <summary>
        /// Comma-separated list of entity code patterns for traders to remove (default: humanoid-trader).
        /// Uses prefix matching - "humanoid-trader" matches all trader variants.
        /// </summary>
        public string TraderEntityCodes { get; set; } = "humanoid-trader";

        // === Block Conversion Sound Settings ===

        /// <summary>
        /// Whether to play sounds when blocks are converted by devastation (default: true).
        /// </summary>
        public bool EnableConversionSounds { get; set; } = true;

        /// <summary>
        /// Volume of block conversion sounds (0.0-1.0, default: 0.5).
        /// </summary>
        public float ConversionSoundVolume { get; set; } = 0.5f;

        // === Particle Effect Settings ===

        /// <summary>
        /// Whether to show smoke particles when blocks are converted to devastated forms (default: true).
        /// </summary>
        public bool DevastationParticlesEnabled { get; set; } = true;

        /// <summary>
        /// Whether to show blue particles when blocks are healed/cleaned (default: true).
        /// </summary>
        public bool HealingParticlesEnabled { get; set; } = true;

        /// <summary>
        /// Whether to show devastation particles at chunk borders where devastated chunks meet protected chunks (default: true).
        /// Creates a visible barrier effect at the edge of protected areas.
        /// </summary>
        public bool ChunkBorderParticlesEnabled { get; set; } = true;

        /// <summary>
        /// Number of particles to spawn per devastated block conversion (default: 5).
        /// </summary>
        public int DevastationParticleCount { get; set; } = 5;

        /// <summary>
        /// Number of particles to spawn per healed block (default: 8).
        /// </summary>
        public int HealingParticleCount { get; set; } = 8;

        /// <summary>
        /// Maximum number of chunk border blocks to emit particles from per tick (default: 10).
        /// Limits CPU usage for chunk border particle effects.
        /// </summary>
        public int MaxChunkBorderParticlesPerTick { get; set; } = 10;

        /// <summary>
        /// Interval in seconds between chunk border particle emissions (default: 0.5).
        /// </summary>
        public double ChunkBorderParticleIntervalSeconds { get; set; } = 0.5;

        /// <summary>
        /// Whether particles only spawn on blocks exposed to air above (default: true).
        /// This significantly improves performance by not spawning particles underground.
        /// </summary>
        public bool ParticlesRequireAirAbove { get; set; } = true;

        /// <summary>
        /// Maximum number of particles to spawn per second across all conversions (default: 50).
        /// When this limit is reached, only conversions near players will spawn particles.
        /// </summary>
        public int MaxParticlesPerSecond { get; set; } = 50;

        /// <summary>
        /// Distance in chunks within which particles will always spawn when particle limit is reached (default: 3).
        /// Blocks outside this distance from any player won't emit particles when at the particle limit.
        /// </summary>
        public int ParticlePlayerProximityChunks { get; set; } = 3;

        /// <summary>
        /// Multiplier for particle size (default: 1.0). Higher values = larger particles.
        /// </summary>
        public float ParticleSizeMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Multiplier for particle density/quantity (default: 1.0). Higher values = more particles per conversion.
        /// </summary>
        public float ParticleDensityMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Multiplier for particle lifetime (default: 1.0). Higher values = particles last longer and fade more slowly.
        /// </summary>
        public float ParticleLifetimeMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Starting opacity for particles (0.0-1.0, default: 1.0).
        /// 1.0 = fully opaque at start, 0.5 = 50% transparent at start.
        /// Particles fade linearly from this opacity to fully transparent over their lifetime.
        /// </summary>
        public float ParticleOpacity { get; set; } = 1.0f;

        // === Devastation Weather Settings ===

        /// <summary>
        /// Whether to create ominous weather (storm clouds, thunder, lightning) above devastated regions (default: true).
        /// Weather intensity increases with devastation level in the region.
        /// </summary>
        public bool WeatherEffectsEnabled { get; set; } = true;

        /// <summary>
        /// Interval in seconds between weather updates (default: 30.0).
        /// Lower values respond faster to devastation changes but may cause more frequent weather transitions.
        /// </summary>
        public double WeatherUpdateIntervalSeconds { get; set; } = 30.0;

        /// <summary>
        /// Minimum devastation intensity (0.0-1.0) to trigger any weather effects (default: 0.15).
        /// Below this threshold, normal weather patterns continue.
        /// </summary>
        public float WeatherMinIntensity { get; set; } = 0.15f;

        /// <summary>
        /// Devastation threshold for Tier 1 weather - dark storm clouds with light thunder (default: 0.15).
        /// </summary>
        public float WeatherTier1Threshold { get; set; } = 0.15f;

        /// <summary>
        /// Devastation threshold for Tier 2 weather - storm clouds with heavy thunder (default: 0.30).
        /// </summary>
        public float WeatherTier2Threshold { get; set; } = 0.30f;

        /// <summary>
        /// Devastation threshold for Tier 3 weather - heavy storms with thunder and lightning (default: 0.50).
        /// </summary>
        public float WeatherTier3Threshold { get; set; } = 0.50f;

        /// <summary>
        /// Weather pattern code for Tier 1 (default: "cumulonimbus").
        /// Dark storm clouds. Available patterns: clearsky, mediumhaze, stronghaze, cumulonimbus, cumulonimbusr, cumulonimbusrf
        /// </summary>
        public string WeatherTier1Pattern { get; set; } = "cumulonimbus";

        /// <summary>
        /// Weather pattern code for Tier 2 (default: "overcast").
        /// Full cloud coverage.
        /// </summary>
        public string WeatherTier2Pattern { get; set; } = "overcast";

        /// <summary>
        /// Weather pattern code for Tier 3 (default: "overcastundulating").
        /// Overcast with undulating clouds.
        /// </summary>
        public string WeatherTier3Pattern { get; set; } = "overcastundulating";

        /// <summary>
        /// Weather event code for Tier 1 (default: "lightthunder").
        /// Available events: noevent, lightthunder, heavythunder, smallhail, largehail
        /// </summary>
        public string WeatherTier1Event { get; set; } = "lightthunder";

        /// <summary>
        /// Weather event code for Tier 2 (default: "heavythunder").
        /// </summary>
        public string WeatherTier2Event { get; set; } = "heavythunder";

        /// <summary>
        /// Weather event code for Tier 3 (default: "heavythunder").
        /// </summary>
        public string WeatherTier3Event { get; set; } = "heavythunder";

        /// <summary>
        /// Wind pattern code for Tier 1 (default: "mediumbreeze").
        /// Available patterns: still, lightbreeze, mediumbreeze, strongbreeze, storm
        /// </summary>
        public string WeatherTier1Wind { get; set; } = "mediumbreeze";

        /// <summary>
        /// Wind pattern code for Tier 2 (default: "strongbreeze").
        /// </summary>
        public string WeatherTier2Wind { get; set; } = "strongbreeze";

        /// <summary>
        /// Wind pattern code for Tier 3 (default: "storm").
        /// </summary>
        public string WeatherTier3Wind { get; set; } = "storm";

        /// <summary>
        /// Whether weather transitions should be instant (true) or gradual (false).
        /// Gradual transitions blend smoothly between weather patterns. (default: false)
        /// </summary>
        public bool WeatherInstantTransitions { get; set; } = false;

        /// <summary>
        /// Hysteresis amount for weather tier thresholds (default: 0.05).
        /// Weather tier activates at threshold, but only deactivates at (threshold - hysteresis).
        /// This prevents flickering when intensity hovers near a threshold.
        /// </summary>
        public float WeatherHysteresis { get; set; } = 0.05f;

        // === Devastation Music Settings ===

        /// <summary>
        /// Whether to play eerie ambient music when in devastated chunks (default: true).
        /// Music fades in as you enter devastated areas and fades out when you leave.
        /// </summary>
        public bool MusicEnabled { get; set; } = true;

        /// <summary>
        /// Base volume for devastation ambient sounds (0.0-1.0, default: 0.5).
        /// The actual volume scales with fog intensity.
        /// </summary>
        public float MusicVolume { get; set; } = 0.5f;

        /// <summary>
        /// Sound file path for devastation ambient sound (without .ogg extension).
        /// Use "effect/tempstab-verylow" for intense temporal storm sound (default).
        /// Use "effect/tempstab-low" for a less intense version.
        /// Use "effect/tempstab-drain" for the stability drain sound.
        /// For custom sounds, use just the filename and place in assets/spreadingdevastation/sounds/music/.
        /// </summary>
        public string MusicSoundFile { get; set; } = "effect/tempstab-verylow";

        /// <summary>
        /// How fast the music fades in per second (default: 0.3).
        /// Lower values = slower fade in.
        /// </summary>
        public float MusicFadeInSpeed { get; set; } = 0.3f;

        /// <summary>
        /// How fast the music fades out per second (default: 0.5).
        /// Lower values = slower fade out.
        /// </summary>
        public float MusicFadeOutSpeed { get; set; } = 0.5f;

        /// <summary>
        /// Minimum fog intensity (0.0-1.0) to trigger music playback (default: 0.1).
        /// Music won't play until fog reaches this intensity level.
        /// </summary>
        public float MusicIntensityThreshold { get; set; } = 0.1f;

        /// <summary>
        /// Whether the music should loop continuously (default: true).
        /// </summary>
        public bool MusicLoop { get; set; } = true;

        /// <summary>
        /// How much to suppress normal ambient sounds when in devastated areas (0.0-1.0, default: 0.8).
        /// 1.0 = full suppression (silence other ambient sounds), 0.0 = no suppression.
        /// Note: This affects the volume of block ambient sounds like water, fire, etc.
        /// </summary>
        public float AmbientSoundSuppression { get; set; } = 0.8f;

        // === Render Distance Edge Spawning Settings ===

        /// <summary>
        /// Whether to spawn devastation at render distance edge when none visible for a period (default: false).
        /// When enabled, if no devastation is visible within render distance for EdgeSpawningDelayMinutes,
        /// a new devastated chunk will spawn at the edge of render distance, positioned toward the last known devastation.
        /// </summary>
        public bool EdgeSpawningEnabled { get; set; } = false;

        /// <summary>
        /// Minutes without visible devastation before edge spawn triggers (default: 10.0).
        /// Uses real-world time, not in-game time.
        /// </summary>
        public double EdgeSpawningDelayMinutes { get; set; } = 10.0;

        /// <summary>
        /// How often to check for visible devastation in seconds (default: 60.0).
        /// Lower values detect devastation faster but use slightly more CPU.
        /// </summary>
        public double EdgeSpawningCheckIntervalSeconds { get; set; } = 60.0;

        // === Temporal Storm Settings ===

        /// <summary>
        /// Whether temporal storm effects on devastation are enabled (default: true).
        /// When enabled, temporal storms will spawn devastation near players and speed up spread.
        /// </summary>
        public bool TemporalStormEffectsEnabled { get; set; } = true;

        /// <summary>
        /// Speed multiplier for devastation spread during temporal storms (default: 5.0).
        /// This multiplies the base SpeedMultiplier during active storms.
        /// Set to 1.0 to disable speed boost during storms.
        /// </summary>
        public double TemporalStormSpeedMultiplier { get; set; } = 5.0;

        /// <summary>
        /// Radius in blocks around each player where new devastated blocks can spawn during storms (default: 64).
        /// Devastation spawns at random positions within this radius.
        /// </summary>
        public int TemporalStormSpawnRadius { get; set; } = 64;

        /// <summary>
        /// Interval in seconds between storm devastation spawns around players (default: 20.0).
        /// Lower values spawn more frequently during storms.
        /// </summary>
        public double TemporalStormSpawnIntervalSeconds { get; set; } = 20.0;

        /// <summary>
        /// Whether storm-spawned devastation creates new devastated chunks (default: true).
        /// When true, spawned blocks will mark their chunk as devastated.
        /// When false, only converts individual blocks without chunk-wide effects.
        /// </summary>
        public bool TemporalStormCreatesChunks { get; set; } = true;
    }
}
