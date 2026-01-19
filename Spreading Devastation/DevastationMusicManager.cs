using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SpreadingDevastation
{
    /// <summary>
    /// Client-side manager that controls ambient sound playback when the player is in devastated chunks.
    /// Cycles through different sounds with periods of silence for variety.
    /// </summary>
    public class DevastationMusicManager : IRenderer
    {
        private ICoreClientAPI capi;
        private SpreadingDevastationModSystem modSystem;

        // Sound state
        private ILoadedSound currentSound;
        private string currentSoundFile = null;
        private bool isPlaying = false;
        private bool isInSilence = false;
        private float currentIntensity = 0f;
        private float targetIntensity = 0f;

        // Timing
        private double soundStartTime = 0;
        private double silenceStartTime = 0;
        private double currentSoundDuration = 0; // How long current sound should play
        private double currentSilenceDuration = 0; // How long current silence should last

        // Configuration (synced from server)
        // Default to false until we receive config from server - prevents music playing before config arrives
        private bool enabled = false;
        private float volume = 0.6f;
        private float fadeInSpeed = 0.3f;
        private float fadeOutSpeed = 0.5f;
        private float intensityThreshold = 0.1f;

        // Sound variety settings
        private List<string> soundFiles = new List<string>
        {
            "effect/tempstab-verylow",
            "effect/tempstab-low",
            "effect/tempstab-drain"
        };
        private float minSoundDuration = 30f;  // Minimum seconds before switching
        private float maxSoundDuration = 90f;  // Maximum seconds before switching
        private float minSilenceDuration = 5f;  // Minimum silence between sounds
        private float maxSilenceDuration = 20f; // Maximum silence between sounds
        private float silenceChance = 0.3f;     // Chance of silence after each sound

        private Random rand = new Random();
        private int lastSoundIndex = -1; // Track last played to avoid immediate repeats

        // Debug/force play
        private string forcedSoundFile = null;
        private bool forcePlayActive = false;

        public double RenderOrder => 0.1;
        public int RenderRange => 0;

        // Public properties for debug commands
        public bool IsEnabled => enabled;
        public bool IsPlaying => isPlaying;
        public bool IsInSilence => isInSilence;
        public string CurrentSoundFile => currentSoundFile;
        public float CurrentIntensity => currentIntensity;
        public float TargetIntensity => targetIntensity;
        public float CurrentVolume => currentIntensity * volume;
        public double TimeInCurrentState => isInSilence
            ? ((capi?.World?.ElapsedMilliseconds ?? 0) / 1000.0 - silenceStartTime)
            : ((capi?.World?.ElapsedMilliseconds ?? 0) / 1000.0 - soundStartTime);
        public double CurrentStateDuration => isInSilence ? currentSilenceDuration : currentSoundDuration;
        public IReadOnlyList<string> AvailableSounds => soundFiles;

        public DevastationMusicManager(ICoreClientAPI capi, SpreadingDevastationModSystem modSystem)
        {
            this.capi = capi;
            this.modSystem = modSystem;
        }

        public void UpdateConfig(MusicConfigPacket config)
        {
            if (config == null) return;

            enabled = config.Enabled;
            volume = config.Volume;
            fadeInSpeed = config.FadeInSpeed;
            fadeOutSpeed = config.FadeOutSpeed;
            intensityThreshold = config.IntensityThreshold;

            // If config specifies custom sounds, parse them
            if (!string.IsNullOrEmpty(config.SoundFile))
            {
                // Config can specify multiple sounds separated by |
                var configSounds = config.SoundFile.Split('|');
                if (configSounds.Length > 0)
                {
                    soundFiles.Clear();
                    foreach (var s in configSounds)
                    {
                        var trimmed = s.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            soundFiles.Add(trimmed);
                        }
                    }
                }
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!enabled || capi?.World?.Player?.Entity == null)
            {
                if (isPlaying) StopCurrentSound(true);
                return;
            }

            double currentTime = capi.World.ElapsedMilliseconds / 1000.0;

            // Get fog intensity from the mod system
            float fogIntensity = modSystem.GetFogTargetScore();

            // Determine target intensity
            if (fogIntensity >= intensityThreshold)
            {
                float scaledIntensity = (fogIntensity - intensityThreshold) / (1f - intensityThreshold);
                targetIntensity = Math.Min(1f, scaledIntensity * 1.2f);
            }
            else
            {
                targetIntensity = 0f;
            }

            // Smoothly interpolate current intensity
            if (currentIntensity < targetIntensity)
            {
                currentIntensity = Math.Min(targetIntensity, currentIntensity + deltaTime * fadeInSpeed);
            }
            else if (currentIntensity > targetIntensity)
            {
                currentIntensity = Math.Max(targetIntensity, currentIntensity - deltaTime * fadeOutSpeed);
            }

            // Handle sound state
            if (currentIntensity > 0.01f)
            {
                if (isInSilence)
                {
                    // Check if silence period is over
                    if (currentTime - silenceStartTime >= currentSilenceDuration)
                    {
                        isInSilence = false;
                        StartNextSound();
                    }
                }
                else if (isPlaying)
                {
                    // Update volume
                    currentSound?.SetVolume(currentIntensity * volume);

                    // Check if it's time to switch sounds (unless force play is active)
                    if (!forcePlayActive && currentTime - soundStartTime >= currentSoundDuration)
                    {
                        TransitionToNextState();
                    }
                }
                else
                {
                    // Not playing and not in silence - start a sound
                    StartNextSound();
                }
            }
            else
            {
                // Intensity too low - stop everything
                if (isPlaying)
                {
                    StopCurrentSound(true);
                }
                isInSilence = false;
                currentIntensity = 0f;
            }
        }

        private void TransitionToNextState()
        {
            StopCurrentSound(true);

            // Decide whether to have silence or play another sound
            if (rand.NextDouble() < silenceChance)
            {
                // Enter silence period
                isInSilence = true;
                silenceStartTime = capi.World.ElapsedMilliseconds / 1000.0;
                currentSilenceDuration = minSilenceDuration + rand.NextDouble() * (maxSilenceDuration - minSilenceDuration);
            }
            else
            {
                // Play next sound immediately
                StartNextSound();
            }
        }

        private void StartNextSound()
        {
            if (soundFiles.Count == 0) return;

            // Select next sound (avoid immediate repeat if possible)
            int nextIndex;
            if (soundFiles.Count == 1)
            {
                nextIndex = 0;
            }
            else
            {
                do
                {
                    nextIndex = rand.Next(soundFiles.Count);
                } while (nextIndex == lastSoundIndex && soundFiles.Count > 1);
            }

            lastSoundIndex = nextIndex;
            string soundFile = forcePlayActive && forcedSoundFile != null ? forcedSoundFile : soundFiles[nextIndex];

            // Determine play duration
            currentSoundDuration = minSoundDuration + rand.NextDouble() * (maxSoundDuration - minSoundDuration);
            soundStartTime = capi.World.ElapsedMilliseconds / 1000.0;

            LoadAndPlaySound(soundFile);
        }

        private void LoadAndPlaySound(string soundFile)
        {
            try
            {
                currentSound?.Dispose();
                currentSound = null;
                currentSoundFile = null;

                AssetLocation location = GetSoundLocation(soundFile);

                currentSound = capi.World.LoadSound(new SoundParams()
                {
                    Location = location,
                    ShouldLoop = true,
                    RelativePosition = true,
                    DisposeOnFinish = false,
                    Volume = 0f,
                    SoundType = EnumSoundType.SoundGlitchunaffected
                });

                if (currentSound != null)
                {
                    currentSound.Start();
                    currentSound.SetVolume(currentIntensity * volume);
                    currentSoundFile = soundFile;
                    isPlaying = true;
                }
                else
                {
                    capi.Logger.Warning("[Spreading Devastation] Could not load sound: {0}", location);
                }
            }
            catch (Exception ex)
            {
                capi.Logger.Warning("[Spreading Devastation] Error loading sound: {0}", ex.Message);
            }
        }

        private AssetLocation GetSoundLocation(string soundFile)
        {
            if (soundFile.StartsWith("effect/") || soundFile.StartsWith("music/") || soundFile.StartsWith("ambient/"))
            {
                return new AssetLocation("game", "sounds/" + soundFile.ToLowerInvariant());
            }
            else
            {
                return new AssetLocation("spreadingdevastation", "sounds/music/" + soundFile.ToLowerInvariant());
            }
        }

        private void StopCurrentSound(bool fade)
        {
            if (currentSound != null)
            {
                if (fade && currentSound.IsPlaying)
                {
                    currentSound.FadeOutAndStop(1.5f);
                }
                else
                {
                    currentSound.Stop();
                    currentSound.Dispose();
                }
                currentSound = null;
            }
            currentSoundFile = null;
            isPlaying = false;
        }

        // Debug commands

        /// <summary>
        /// Force play a specific sound file. Set to null to return to normal cycling.
        /// </summary>
        public void ForcePlay(string soundFile)
        {
            if (string.IsNullOrEmpty(soundFile))
            {
                forcePlayActive = false;
                forcedSoundFile = null;
                return;
            }

            forcedSoundFile = soundFile;
            forcePlayActive = true;

            // Stop current and start the forced sound
            StopCurrentSound(false);
            isInSilence = false;
            LoadAndPlaySound(soundFile);
            currentSoundDuration = float.MaxValue; // Don't auto-switch while forcing
        }

        /// <summary>
        /// Force a silence period of the given duration.
        /// </summary>
        public void ForceSilence(float duration)
        {
            StopCurrentSound(true);
            forcePlayActive = false;
            isInSilence = true;
            silenceStartTime = capi.World.ElapsedMilliseconds / 1000.0;
            currentSilenceDuration = duration;
        }

        /// <summary>
        /// Skip to the next sound immediately.
        /// </summary>
        public void SkipToNext()
        {
            forcePlayActive = false;
            TransitionToNextState();
        }

        /// <summary>
        /// Get a status string for debug display.
        /// </summary>
        public string GetStatusString()
        {
            if (!enabled) return "Music disabled";
            if (targetIntensity < 0.01f) return "Not in devastated area";

            string state;
            if (isInSilence)
            {
                double remaining = currentSilenceDuration - (capi.World.ElapsedMilliseconds / 1000.0 - silenceStartTime);
                state = $"Silence ({remaining:F1}s remaining)";
            }
            else if (isPlaying)
            {
                double remaining = currentSoundDuration - (capi.World.ElapsedMilliseconds / 1000.0 - soundStartTime);
                string forceStr = forcePlayActive ? " [FORCED]" : "";
                state = $"Playing: {currentSoundFile}{forceStr} ({remaining:F1}s remaining)";
            }
            else
            {
                state = "Idle";
            }

            return $"{state} | Intensity: {currentIntensity:F2} | Volume: {currentIntensity * volume:F2}";
        }

        public void Dispose()
        {
            StopCurrentSound(false);
        }
    }
}
