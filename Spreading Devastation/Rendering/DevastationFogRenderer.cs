using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using SpreadingDevastation.Network;

namespace SpreadingDevastation.Rendering
{
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
        public void UpdateConfig(FogConfigPacket config)
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
}
