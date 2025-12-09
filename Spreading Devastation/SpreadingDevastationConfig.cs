namespace SpreadingDevastation
{
    /// <summary>
    /// Configuration class for the Spreading Devastation mod.
    /// Saved to ModConfig/SpreadingDevastationConfig.json
    /// </summary>
    public class SpreadingDevastationConfig
    {
        // Core settings
        public double SpeedMultiplier { get; set; } = 1.0;
        public int MaxSources { get; set; } = 20;
        public int MinYLevel { get; set; } = -999;
        public int DefaultRange { get; set; } = 8;
        public int DefaultAmount { get; set; } = 1;
        
        // Metastasis settings
        public int MetastasisThreshold { get; set; } = 300;
        public double MetastasisRadiusVariation { get; set; } = 0.5;
        public double ChildSpawnDelaySeconds { get; set; } = 120.0;
        public int MaxFailedSpawnAttempts { get; set; } = 10;
        public int PillarSearchHeight { get; set; } = 5;
        
        // Regeneration settings
        public double RegenerationHours { get; set; } = 60.0;
        
        // Thresholds
        public double SaturationThreshold { get; set; } = 0.75;
        public double LowSuccessThreshold { get; set; } = 0.2;
        public double VeryLowSuccessThreshold { get; set; } = 0.05;
        
        // Surface spreading
        public bool RequireSourceAirContact { get; set; } = false;
        
        // Debug
        public bool ShowSourceMarkers { get; set; } = true;
        
        // Player haunting
        public bool EnablePlayerHaunting { get; set; } = false;
        public double HauntingIntervalSeconds { get; set; } = 60.0;
        public int HauntingBurstCount { get; set; } = 3;
        public double HauntingAngleVariance { get; set; } = 30.0;
        public double HauntingLeapFraction { get; set; } = 0.25;
        public int HauntingMinDistance { get; set; } = 30;
        public int HauntingMaxLeapDistance { get; set; } = 64;
    }
}

