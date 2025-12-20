using System;
using System.Collections.Generic;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SpreadingDevastation
{
    // Partial class containing fog and reset command handlers
    public partial class SpreadingDevastationModSystem
    {
        private TextCommandResult HandleFogCommand(TextCommandCallingArgs args)
        {
            string setting = (args.Parsers[0].GetValue() as string ?? "").ToLowerInvariant();
            string value = args.Parsers[1].GetValue() as string ?? "";

            switch (setting)
            {
                case "enabled":
                case "on":
                case "off":
                    if (setting == "on" || (setting == "enabled" && value.ToLowerInvariant() == "on"))
                    {
                        config.FogEffectEnabled = true;
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success("Devastation fog effect ENABLED");
                    }
                    else if (setting == "off" || (setting == "enabled" && value.ToLowerInvariant() == "off"))
                    {
                        config.FogEffectEnabled = false;
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success("Devastation fog effect DISABLED");
                    }
                    return TextCommandResult.Error("Usage: /dv fog enabled [on|off]");

                case "color":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog color: R={config.FogColorR:F2} G={config.FogColorG:F2} B={config.FogColorB:F2}. Use '/dv fog color [r] [g] [b]' (0.0-1.0)");
                    }
                    // Parse r g b from value (space-separated)
                    var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 &&
                        float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) &&
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                    {
                        config.FogColorR = Math.Clamp(r, 0f, 1f);
                        config.FogColorG = Math.Clamp(g, 0f, 1f);
                        config.FogColorB = Math.Clamp(b, 0f, 1f);
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success($"Fog color set to R={config.FogColorR:F2} G={config.FogColorG:F2} B={config.FogColorB:F2}");
                    }
                    return TextCommandResult.Error("Usage: /dv fog color [r] [g] [b] (values 0.0-1.0, e.g., '0.55 0.25 0.15')");

                case "density":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog density: {config.FogDensity:F4}. Use '/dv fog density [value]' (e.g., 0.004)");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float density))
                    {
                        config.FogDensity = Math.Clamp(density, 0f, 1f);
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success($"Fog density set to {config.FogDensity:F4}");
                    }
                    return TextCommandResult.Error("Invalid number for fog density");

                case "min":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog minimum: {config.FogMin:F2}. Use '/dv fog min [value]' (0.0-1.0)");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fogMin))
                    {
                        config.FogMin = Math.Clamp(fogMin, 0f, 1f);
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success($"Fog minimum set to {config.FogMin:F2}");
                    }
                    return TextCommandResult.Error("Invalid number for fog minimum");

                case "weight":
                case "weights":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog weights: color={config.FogColorWeight:F2} density={config.FogDensityWeight:F2} min={config.FogMinWeight:F2}. Use '/dv fog weight [color|density|min] [value]'");
                    }
                    var weightParts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (weightParts.Length >= 2 &&
                        float.TryParse(weightParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float weightVal))
                    {
                        weightVal = Math.Clamp(weightVal, 0f, 1f);
                        switch (weightParts[0].ToLowerInvariant())
                        {
                            case "color":
                                config.FogColorWeight = weightVal;
                                SaveConfig();
                                BroadcastFogConfig();
                                return TextCommandResult.Success($"Fog color weight set to {config.FogColorWeight:F2}");
                            case "density":
                                config.FogDensityWeight = weightVal;
                                SaveConfig();
                                BroadcastFogConfig();
                                return TextCommandResult.Success($"Fog density weight set to {config.FogDensityWeight:F2}");
                            case "min":
                                config.FogMinWeight = weightVal;
                                SaveConfig();
                                BroadcastFogConfig();
                                return TextCommandResult.Success($"Fog min weight set to {config.FogMinWeight:F2}");
                        }
                    }
                    return TextCommandResult.Error("Usage: /dv fog weight [color|density|min] [value] (0.0-1.0)");

                case "transition":
                case "speed":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Fog transition speed: {config.FogTransitionSpeed:F2}s. Use '/dv fog transition [seconds]'");
                    }
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float transSpeed))
                    {
                        config.FogTransitionSpeed = Math.Clamp(transSpeed, 0.1f, 10f);
                        SaveConfig();
                        BroadcastFogConfig();
                        return TextCommandResult.Success($"Fog transition speed set to {config.FogTransitionSpeed:F2}s");
                    }
                    return TextCommandResult.Error("Invalid number for transition speed");

                case "reset":
                case "defaults":
                    // Reset all fog values to defaults
                    config.FogEffectEnabled = true;
                    config.FogColorR = 0.55f;
                    config.FogColorG = 0.25f;
                    config.FogColorB = 0.15f;
                    config.FogDensity = 0.004f;
                    config.FogMin = 0.15f;
                    config.FogColorWeight = 0.7f;
                    config.FogDensityWeight = 0.5f;
                    config.FogMinWeight = 0.6f;
                    config.FogTransitionSpeed = 0.5f;
                    SaveConfig();
                    BroadcastFogConfig();
                    return TextCommandResult.Success("Fog settings reset to defaults (rusty orange fog, enabled)");

                case "":
                case "info":
                case "status":
                    return SendChatLines(args, new[]
                    {
                        "=== Devastation Fog Settings ===",
                        $"Enabled: {config.FogEffectEnabled}",
                        $"Color (RGB): {config.FogColorR:F2}, {config.FogColorG:F2}, {config.FogColorB:F2}",
                        $"Density: {config.FogDensity:F4}",
                        $"Minimum fog: {config.FogMin:F2}",
                        $"Weights: color={config.FogColorWeight:F2}, density={config.FogDensityWeight:F2}, min={config.FogMinWeight:F2}",
                        $"Transition speed: {config.FogTransitionSpeed:F2}s",
                        "",
                        "Commands:",
                        "  /dv fog [on|off] - Enable or disable fog effect",
                        "  /dv fog color [r] [g] [b] - Set fog color (0.0-1.0)",
                        "  /dv fog density [value] - Set fog density",
                        "  /dv fog min [value] - Set minimum fog level",
                        "  /dv fog weight [color|density|min] [value] - Set effect weights",
                        "  /dv fog transition [seconds] - Set transition speed",
                        "  /dv fog reset - Reset all fog settings to defaults"
                    }, "Fog settings sent to chat");

                default:
                    return TextCommandResult.Error($"Unknown fog setting: {setting}. Use: on, off, color, density, min, weight, transition, reset, or info");
            }
        }

        private TextCommandResult HandleResetCommand(TextCommandCallingArgs args)
        {
            string confirm = (args.Parsers[0].GetValue() as string ?? "").ToLowerInvariant();

            if (confirm != "confirm")
            {
                return SendChatLines(args, new[]
                {
                    "This will reset ALL config values to defaults:",
                    "  - Speed multiplier",
                    "  - Max sources, metastasis threshold",
                    "  - Chunk spreading settings",
                    "  - Rift ward settings",
                    "  - Fog effect settings",
                    "",
                    "Type '/dv reset confirm' to proceed."
                });
            }

            // Create fresh default config
            config = new SpreadingDevastationConfig();
            SaveConfig();

            // Mark fog config as dirty to sync to clients
            BroadcastFogConfig();

            // Rebuild rift ward cache with new radius
            RebuildProtectedChunkCache();

            return TextCommandResult.Success("All config values have been reset to defaults.");
        }
    }
}
