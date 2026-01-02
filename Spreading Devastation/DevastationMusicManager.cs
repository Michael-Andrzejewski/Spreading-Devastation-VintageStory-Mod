using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace SpreadingDevastation
{
    /// <summary>
    /// Client-side manager that controls music playback when the player is in devastated chunks.
    /// Plays eerie ambient music and suppresses normal ambient sounds.
    /// </summary>
    public class DevastationMusicManager : IRenderer
    {
        private ICoreClientAPI capi;
        private SpreadingDevastationModSystem modSystem;

        // Music track state
        private DevastationMusicTrack currentTrack;
        private bool isPlaying = false;
        private float currentIntensity = 0f;
        private float targetIntensity = 0f;

        // Configuration (synced from server)
        private bool enabled = true;
        private float volume = 0.8f;
        private float fadeInSpeed = 0.3f;  // Per second
        private float fadeOutSpeed = 0.5f; // Per second
        private float intensityThreshold = 0.1f; // Minimum fog intensity to trigger music

        // Ambient sound suppression
        private float ambientSuppression = 0.8f; // How much to reduce ambient sounds (0-1)

        public double RenderOrder => 0.1; // After fog renderer
        public int RenderRange => 0;

        public DevastationMusicManager(ICoreClientAPI capi, SpreadingDevastationModSystem modSystem)
        {
            this.capi = capi;
            this.modSystem = modSystem;

            // Create the devastation music track
            currentTrack = new DevastationMusicTrack(capi, this);
        }

        /// <summary>
        /// Updates the music configuration from server-sent config.
        /// </summary>
        public void UpdateConfig(MusicConfigPacket config)
        {
            if (config == null) return;

            enabled = config.Enabled;
            volume = config.Volume;
            fadeInSpeed = config.FadeInSpeed;
            fadeOutSpeed = config.FadeOutSpeed;
            intensityThreshold = config.IntensityThreshold;
            ambientSuppression = config.AmbientSuppression;

            // Update track configuration
            currentTrack?.UpdateConfig(config);
        }

        /// <summary>
        /// Gets the current music intensity (0 = silent, 1 = full volume).
        /// </summary>
        public float CurrentIntensity => currentIntensity;

        /// <summary>
        /// Gets the ambient suppression amount (0 = no suppression, 1 = full suppression).
        /// </summary>
        public float GetAmbientSuppression()
        {
            return currentIntensity * ambientSuppression;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!enabled || capi?.World?.Player?.Entity == null)
            {
                if (isPlaying)
                {
                    StopMusic();
                }
                return;
            }

            // Get fog intensity from the mod system as our reference
            float fogIntensity = modSystem.GetFogTargetScore();

            // Determine target intensity based on fog
            if (fogIntensity >= intensityThreshold)
            {
                // Scale music intensity with fog, from threshold to 1.0
                float scaledIntensity = (fogIntensity - intensityThreshold) / (1f - intensityThreshold);
                targetIntensity = Math.Min(1f, scaledIntensity * 1.2f); // Allow slight overshoot for deeper areas
            }
            else
            {
                targetIntensity = 0f;
            }

            // Smoothly interpolate current intensity toward target
            if (currentIntensity < targetIntensity)
            {
                currentIntensity = Math.Min(targetIntensity, currentIntensity + deltaTime * fadeInSpeed);
            }
            else if (currentIntensity > targetIntensity)
            {
                currentIntensity = Math.Max(targetIntensity, currentIntensity - deltaTime * fadeOutSpeed);
            }

            // Start/stop music based on intensity
            if (currentIntensity > 0.01f)
            {
                if (!isPlaying)
                {
                    StartMusic();
                }

                // Update volume based on intensity
                currentTrack?.SetVolume(currentIntensity * volume);
            }
            else if (isPlaying)
            {
                StopMusic();
            }
        }

        private void StartMusic()
        {
            if (isPlaying) return;

            try
            {
                currentTrack?.Start();
                isPlaying = true;
            }
            catch (Exception ex)
            {
                capi.Logger.Warning("[Spreading Devastation] Failed to start devastation music: {0}", ex.Message);
            }
        }

        private void StopMusic()
        {
            if (!isPlaying) return;

            try
            {
                currentTrack?.FadeOutAndStop(2f);
                isPlaying = false;
                currentIntensity = 0f;
            }
            catch (Exception ex)
            {
                capi.Logger.Warning("[Spreading Devastation] Failed to stop devastation music: {0}", ex.Message);
            }
        }

        public void Dispose()
        {
            StopMusic();
            currentTrack?.Dispose();
            currentTrack = null;
        }
    }

    /// <summary>
    /// Custom music track for devastated areas.
    /// Plays the temporal storm ambient sounds from the base game.
    /// </summary>
    public class DevastationMusicTrack : IDisposable
    {
        private ICoreClientAPI capi;
        private DevastationMusicManager manager;
        private ILoadedSound sound;
        private bool isLoading = false;
        private bool shouldLoop = true;
        private bool soundFileMissing = false;
        private bool warnedAboutMissingSound = false;

        // Sound file location - defaults to base game temporal storm sounds
        private AssetLocation soundLocation;
        private string soundFile = "effect/tempstab-verylow"; // Base game temporal storm sound

        public DevastationMusicTrack(ICoreClientAPI capi, DevastationMusicManager manager)
        {
            this.capi = capi;
            this.manager = manager;

            // Default to base game temporal storm sound
            UpdateSoundLocation();
        }

        public void UpdateConfig(MusicConfigPacket config)
        {
            if (!string.IsNullOrEmpty(config.SoundFile) && config.SoundFile != soundFile)
            {
                soundFile = config.SoundFile;
                UpdateSoundLocation();
                // Reset missing flag when config changes - file might exist now
                soundFileMissing = false;
                warnedAboutMissingSound = false;
            }
            shouldLoop = config.Loop;
        }

        private void UpdateSoundLocation()
        {
            // Check if it's a base game sound (starts with "effect/" or other known paths)
            // or a custom mod sound
            if (soundFile.StartsWith("effect/") || soundFile.StartsWith("music/") || soundFile.StartsWith("ambient/"))
            {
                // Base game sound - use "game" domain
                soundLocation = new AssetLocation("game", "sounds/" + soundFile.ToLowerInvariant());
            }
            else
            {
                // Custom mod sound - use mod's domain
                soundLocation = new AssetLocation("spreadingdevastation", "sounds/music/" + soundFile.ToLowerInvariant());
            }
        }

        public void Start()
        {
            // Don't attempt to load if we already know the file is missing
            if (soundFileMissing) return;
            if (isLoading || (sound != null && sound.IsPlaying)) return;

            isLoading = true;

            try
            {
                // Load and start the sound
                sound?.Dispose();
                sound = null;

                sound = capi.World.LoadSound(new SoundParams()
                {
                    Location = soundLocation,
                    ShouldLoop = shouldLoop,
                    RelativePosition = true,
                    DisposeOnFinish = false,
                    Volume = 0f, // Start silent, will fade in
                    // Use SoundGlitchunaffected so the sound isn't distorted by temporal effects
                    SoundType = EnumSoundType.SoundGlitchunaffected
                });

                if (sound != null)
                {
                    sound.Start();
                }
                else
                {
                    soundFileMissing = true;
                    if (!warnedAboutMissingSound)
                    {
                        capi.Logger.Warning("[Spreading Devastation] Could not load devastation ambient sound: {0}", soundLocation);
                        warnedAboutMissingSound = true;
                    }
                }
            }
            catch (Exception ex)
            {
                soundFileMissing = true;
                if (!warnedAboutMissingSound)
                {
                    capi.Logger.Warning("[Spreading Devastation] Error loading devastation ambient sound: {0}", ex.Message);
                    warnedAboutMissingSound = true;
                }
            }
            finally
            {
                isLoading = false;
            }
        }

        public void SetVolume(float volume)
        {
            if (sound != null && sound.IsPlaying)
            {
                sound.SetVolume(volume);
            }
        }

        public void FadeOutAndStop(float seconds)
        {
            if (sound != null && sound.IsPlaying)
            {
                sound.FadeOutAndStop(seconds);
            }
        }

        public void Dispose()
        {
            sound?.Stop();
            sound?.Dispose();
            sound = null;
        }
    }
}
