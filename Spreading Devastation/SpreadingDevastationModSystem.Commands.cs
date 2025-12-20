using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SpreadingDevastation
{
    // Command handlers - partial class for SpreadingDevastationModSystem
    public partial class SpreadingDevastationModSystem
    {
        #region Command Handlers

        /// <summary>
        /// Registers the devastate command tree under the provided command name (e.g., "devastate" and alias "dv").
        /// </summary>
        private void RegisterDevastateCommand(ICoreServerAPI api, string commandName)
        {
            api.ChatCommands.Create(commandName)
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
                .BeginSubCommand("markers")
                    .WithDescription("Toggle magenta source markers for debugging")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("onoff"))
                    .HandleWith(HandleMarkersCommand)
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
                .EndSubCommand()
                .BeginSubCommand("speed")
                    .WithDescription("Set devastation spread speed multiplier")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("multiplier"))
                    .HandleWith(HandleSpeedCommand)
                .EndSubCommand()
                .BeginSubCommand("chunk")
                    .WithDescription("Mark the chunk you're looking at as devastated, or configure chunk settings")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleChunkCommand)
                .EndSubCommand()
                .BeginSubCommand("riftward")
                    .WithDescription("Configure rift ward settings (speed, list, info)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleRiftWardCommand)
                .EndSubCommand()
                .BeginSubCommand("fog")
                    .WithDescription("Configure devastation fog and sky effect")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("setting"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleFogCommand)
                .EndSubCommand()
                .BeginSubCommand("reset")
                    .WithDescription("Reset all config values to defaults")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("confirm"))
                    .HandleWith(HandleResetCommand)
                .EndSubCommand()
                .BeginSubCommand("blockinfo")
                    .WithDescription("Show block code of the block you're looking at (for debugging)")
                    .HandleWith(HandleBlockInfoCommand)
                .EndSubCommand()
                .BeginSubCommand("insanity")
                    .WithDescription("Manage animal insanity in devastated chunks")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"),
                              api.ChatCommands.Parsers.OptionalAll("value"))
                    .HandleWith(HandleInsanityCommand)
                .EndSubCommand()
                .BeginSubCommand("testsuite")
                    .WithDescription("Run automated tests on mod features")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("testname"))
                    .HandleWith(HandleTestSuiteCommand)
                .EndSubCommand();
        }

        private TextCommandResult HandleBlockInfoCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Player not found");

            var blockSel = player.CurrentBlockSelection;
            if (blockSel == null) return TextCommandResult.Error("Not looking at a block");

            Block block = sapi.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block == null) return TextCommandResult.Error("Block not found");

            string fullCode = block.Code.ToString();
            string path = block.Code.Path;
            string domain = block.Code.Domain;

            bool canDevastate = TryGetDevastatedForm(block, out string devForm, out string regenTo);

            var lines = new List<string>
            {
                "=== Block Info ===",
                $"Full code: {fullCode}",
                $"Domain: {domain}",
                $"Path: {path}",
                $"Block ID: {block.Id}",
                $"Can devastate: {canDevastate}",
            };

            if (canDevastate)
            {
                lines.Add($"Devastated form: {devForm}");
                lines.Add($"Regenerates to: {regenTo}");
            }

            return SendChatLines(args, lines, "Block info sent to chat");
        }

        private TextCommandResult HandleInsanityCommand(TextCommandCallingArgs args)
        {
            string action = args.Parsers[0].GetValue() as string ?? "";
            string value = args.Parsers[1].GetValue() as string ?? "";

            switch (action.ToLowerInvariant())
            {
                case "enable":
                case "on":
                    config.AnimalInsanityEnabled = true;
                    SaveConfig();
                    return TextCommandResult.Success("Animal insanity ENABLED - animals in devastated chunks will go hostile");

                case "disable":
                case "off":
                    config.AnimalInsanityEnabled = false;
                    SaveConfig();
                    return TextCommandResult.Success("Animal insanity DISABLED - animals will behave normally");

                case "clear":
                case "cure":
                    int cured = ClearAllInsanity();
                    return TextCommandResult.Success($"Cleared insanity from {cured} entities (Note: they may still be aggressive until they reset)");

                case "chance":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current insanity chance: {config.AnimalInsanityChance:P0}. Use '/dv insanity chance [0.0-1.0]' to set.");
                    }
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double newChance))
                    {
                        config.AnimalInsanityChance = Math.Clamp(newChance, 0.0, 1.0);
                        SaveConfig();
                        return TextCommandResult.Success($"Insanity chance set to {config.AnimalInsanityChance:P0}");
                    }
                    return TextCommandResult.Error("Invalid number. Use a value between 0.0 and 1.0");

                case "interval":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current check interval: {config.AnimalInsanityCheckIntervalSeconds:F1}s. Use '/dv insanity interval [seconds]' to set.");
                    }
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double newInterval))
                    {
                        config.AnimalInsanityCheckIntervalSeconds = Math.Clamp(newInterval, 0.5, 60.0);
                        SaveConfig();
                        return TextCommandResult.Success($"Insanity check interval set to {config.AnimalInsanityCheckIntervalSeconds:F1} seconds");
                    }
                    return TextCommandResult.Error("Invalid number. Use a value between 0.5 and 60.0");

                case "radius":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current search radius: {config.AnimalInsanitySearchRadius} blocks. Use '/dv insanity radius [blocks]' to set.");
                    }
                    if (int.TryParse(value, out int newRadius))
                    {
                        config.AnimalInsanitySearchRadius = Math.Clamp(newRadius, 8, 256);
                        SaveConfig();
                        return TextCommandResult.Success($"Insanity search radius set to {config.AnimalInsanitySearchRadius} blocks");
                    }
                    return TextCommandResult.Error("Invalid number for radius");

                case "include":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current include patterns: {config.AnimalInsanityEntityCodes}");
                    }
                    config.AnimalInsanityEntityCodes = value;
                    insanityIncludePatterns = null; // Clear cache to force re-parse
                    SaveConfig();
                    return TextCommandResult.Success($"Insanity include patterns set to: {value}");

                case "exclude":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Current exclude patterns: {config.AnimalInsanityExcludeCodes}");
                    }
                    config.AnimalInsanityExcludeCodes = value;
                    insanityExcludePatterns = null; // Clear cache to force re-parse
                    SaveConfig();
                    return TextCommandResult.Success($"Insanity exclude patterns set to: {value}");

                case "":
                case "info":
                case "status":
                default:
                    var lines = new List<string>
                    {
                        "=== Animal Insanity Settings ===",
                        $"Enabled: {(config.AnimalInsanityEnabled ? "YES" : "NO")}",
                        $"Insane entities tracked: {insaneEntityIds.Count}",
                        $"Check interval: {config.AnimalInsanityCheckIntervalSeconds:F1}s",
                        $"Search radius: {config.AnimalInsanitySearchRadius} blocks",
                        $"Insanity chance: {config.AnimalInsanityChance:P0}",
                        "",
                        "Include patterns: " + config.AnimalInsanityEntityCodes,
                        "Exclude patterns: " + config.AnimalInsanityExcludeCodes,
                        "",
                        "Commands:",
                        "  /dv insanity [on|off] - Enable or disable",
                        "  /dv insanity clear - Cure all insane animals",
                        "  /dv insanity chance [0-1] - Set insanity chance",
                        "  /dv insanity interval [sec] - Set check interval",
                        "  /dv insanity radius [blocks] - Set search radius",
                        "  /dv insanity include [codes] - Set affected animals",
                        "  /dv insanity exclude [codes] - Set immune animals"
                    };
                    return SendChatLines(args, lines, "Insanity info sent to chat");
            }
        }

        private TextCommandResult HandleSpeedCommand(TextCommandCallingArgs args)
        {
            string rawArg = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrWhiteSpace(rawArg))
            {
                return SendChatLines(args, new[]
                {
                    $"Current devastation speed: {config.SpeedMultiplier:F2}x",
                    "Usage: /dv speed [multiplier] (e.g., 0.5 for half speed, 5 for 5x speed)"
                }, "Speed info sent to chat (scrollable)");
            }

            if (!double.TryParse(rawArg, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv speed [multiplier] (e.g., 0.5, 1, 5)");
            }

            double newSpeed = Math.Clamp(parsedSpeed, 0.01, 100.0);
            config.SpeedMultiplier = newSpeed;
            SaveConfig();
            return TextCommandResult.Success($"Devastation speed set to {config.SpeedMultiplier:F2}x");
        }

        private TextCommandResult HandleRiftWardCommand(TextCommandCallingArgs args)
        {
            string action = args.Parsers[0].GetValue() as string ?? "";
            string value = args.Parsers[1].GetValue() as string ?? "";

            switch (action.ToLowerInvariant())
            {
                case "speed":
                    return HandleRiftWardSpeedCommand(value);

                case "list":
                    return HandleRiftWardListCommand(args);

                case "rate":
                    return HandleRiftWardRateCommand(value);

                case "mode":
                    return HandleRiftWardModeCommand(value);

                case "radius":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return TextCommandResult.Success($"Rift ward protection radius: {config.RiftWardProtectionRadius} blocks. Use '/dv riftward radius [blocks]' to set.");
                    }
                    if (int.TryParse(value, out int newRadius))
                    {
                        config.RiftWardProtectionRadius = Math.Clamp(newRadius, 8, 1024);
                        SaveConfig();
                        RebuildProtectedChunkCache();
                        return TextCommandResult.Success($"Rift ward protection radius set to {config.RiftWardProtectionRadius} blocks");
                    }
                    return TextCommandResult.Error("Invalid number for radius");

                case "":
                case "info":
                case "status":
                    double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
                    string speedSource = config.RiftWardSpeedMultiplier > 0 ? "custom" : "global";
                    string healingMode = GetEffectiveCleanMode();
                    return SendChatLines(args, new[]
                    {
                        "=== Rift Ward Settings ===",
                        $"Protection radius: {config.RiftWardProtectionRadius} blocks",
                        $"Healing enabled: {config.RiftWardHealingEnabled}",
                        $"Healing mode: {healingMode} (raster=efficient scan, radial=outward, random=anywhere)",
                        $"Base healing rate: {config.RiftWardHealingRate:F1} blocks per sec",
                        $"Speed multiplier: {effectiveSpeed:F2}x ({speedSource})",
                        $"Effective rate: {config.RiftWardHealingRate * effectiveSpeed:F1} blocks per sec",
                        $"Active rift wards: {activeRiftWards?.Count ?? 0}",
                        "",
                        "Commands:",
                        "  /dv riftward radius [blocks] - Set protection radius",
                        "  /dv riftward speed [multiplier] - Set healing speed (or 'global' to use /dv speed)",
                        "  /dv riftward rate [value] - Set base healing rate",
                        "  /dv riftward mode [raster|radial|random] - Set healing pattern mode",
                        "  /dv riftward list - Show all tracked rift wards"
                    }, "Rift ward info sent to chat");

                default:
                    return TextCommandResult.Error($"Unknown riftward action: {action}. Use: radius, speed, rate, mode, list, or info");
            }
        }

        private TextCommandResult HandleRiftWardSpeedCommand(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
                string speedSource = config.RiftWardSpeedMultiplier > 0 ? "custom" : "global";
                return TextCommandResult.Success($"Rift ward healing speed: {effectiveSpeed:F2}x ({speedSource}). Use '/dv riftward speed [multiplier]' to set, or 'global' to use devastation speed.");
            }

            if (value.ToLowerInvariant() == "global" || value.ToLowerInvariant() == "default")
            {
                config.RiftWardSpeedMultiplier = -1;
                SaveConfig();
                return TextCommandResult.Success($"Rift ward healing now uses global devastation speed ({config.SpeedMultiplier:F2}x)");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv riftward speed [multiplier] (e.g., 5, 10, or 'global')");
            }

            double newSpeed = Math.Clamp(parsedSpeed, 0.01, 1000.0);
            config.RiftWardSpeedMultiplier = newSpeed;
            SaveConfig();
            return TextCommandResult.Success($"Rift ward healing speed set to {config.RiftWardSpeedMultiplier:F2}x (effective rate: {config.RiftWardHealingRate * newSpeed:F1} blocks per sec)");
        }

        private TextCommandResult HandleRiftWardRateCommand(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TextCommandResult.Success($"Rift ward base healing rate: {config.RiftWardHealingRate:F1} blocks per sec. Use '/dv riftward rate [value]' to set.");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRate))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv riftward rate [value] (e.g., 10, 50, 100)");
            }

            double newRate = Math.Clamp(parsedRate, 0.1, 10000.0);
            config.RiftWardHealingRate = newRate;
            SaveConfig();
            double effectiveSpeed = config.RiftWardSpeedMultiplier > 0 ? config.RiftWardSpeedMultiplier : config.SpeedMultiplier;
            return TextCommandResult.Success($"Rift ward base healing rate set to {config.RiftWardHealingRate:F1} blocks per sec (effective: {config.RiftWardHealingRate * effectiveSpeed:F1} blocks per sec)");
        }

        private TextCommandResult HandleRiftWardModeCommand(string value)
        {
            string currentMode = GetEffectiveCleanMode();

            if (string.IsNullOrWhiteSpace(value))
            {
                return TextCommandResult.Success($"Rift ward healing mode: {currentMode}. Use '/dv riftward mode [raster|radial|random]' to change.");
            }

            switch (value.ToLowerInvariant())
            {
                case "raster":
                case "scan":
                case "s":
                    config.RiftWardCleanMode = "raster";
                    SaveConfig();
                    // Reset all ward scan progress when switching to raster mode
                    foreach (var ward in activeRiftWards)
                    {
                        ward.CurrentCleanRadius = 0;
                        ward.ScanX = 0;
                        ward.ScanY = 0;
                        ward.ScanZ = 0;
                        ward.RasterScanComplete = false;
                    }
                    return TextCommandResult.Success("Rift ward healing mode set to raster. Expanding globe scan - efficient and thorough.");

                case "radial":
                case "r":
                    config.RiftWardCleanMode = "radial";
                    SaveConfig();
                    // Reset all ward clean progress when switching to radial mode
                    foreach (var ward in activeRiftWards)
                    {
                        ward.CurrentCleanRadius = 0;
                        ward.RadialCleanFailures = 0;
                    }
                    return TextCommandResult.Success("Rift ward healing mode set to radial. Devastation will be cleared outward from the center (legacy mode).");

                case "random":
                case "rand":
                    config.RiftWardCleanMode = "random";
                    SaveConfig();
                    return TextCommandResult.Success("Rift ward healing mode set to random. Devastation will be cleared randomly within protection radius.");

                default:
                    return TextCommandResult.Error($"Unknown mode: {value}. Use 'raster', 'radial', or 'random'.");
            }
        }

        private TextCommandResult HandleRiftWardListCommand(TextCommandCallingArgs args)
        {
            if (activeRiftWards == null || activeRiftWards.Count == 0)
            {
                return TextCommandResult.Success("No rift wards are currently tracked.");
            }

            string mode = GetEffectiveCleanMode();
            var lines = new List<string> { $"=== Tracked Rift Wards ({activeRiftWards.Count}) - Mode: {mode} ===" };
            foreach (var ward in activeRiftWards)
            {
                bool isActive = IsRiftWardActive(ward.Pos);
                string status = isActive ? "ACTIVE" : "inactive";
                string progressInfo = "";
                if (mode == "raster")
                {
                    string scanStatus = ward.RasterScanComplete ? "complete" : $"radius {ward.CurrentCleanRadius}/{config.RiftWardProtectionRadius}";
                    progressInfo = $", scan: {scanStatus}";
                }
                else if (mode == "radial")
                {
                    progressInfo = $", cleaned {ward.MaxCleanRadiusReached}/{config.RiftWardProtectionRadius}";
                }
                lines.Add($"  {ward.Pos} - {status}, healed {ward.BlocksHealed} blocks{progressInfo}");
            }
            return SendChatLines(args, lines, "Rift ward list sent to chat");
        }

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
                    return TextCommandResult.Error("Look at a block to remove it as a devastation source, or use 'remove [all|saturated|metastasis]'");
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
                return ShowListSummary(args);
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

            var lines = new List<string>
            {
                $"Devastation sources ({devastationSources.Count}/{config.MaxSources} cap, {originalCount} original, {metastasisCount} metastasis, {saturatedCount} saturated):"
            };

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

                lines.Add($"  [{type}] [{genInfo}] {statusLabel}{idInfo} {source.Pos} R:{source.CurrentRadius:F0}/{source.Range} Tot:{source.BlocksDevastatedTotal}");
            }

            if (devastationSources.Count > limit)
            {
                lines.Add($"  ... and {devastationSources.Count - limit} more. Use '/devastate list {limit + 10}' or '/devastate list summary'");
            }

            return SendChatLines(args, lines, "Devastation sources listed in chat (scroll to read)");
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

        private TextCommandResult ShowListSummary(TextCommandCallingArgs args)
        {
            int protectedCount = devastationSources.Count(s => s.IsProtected);
            int metastasisCount = devastationSources.Count(s => s.IsMetastasis);
            int saturatedCount = devastationSources.Count(s => s.IsSaturated);
            int healingCount = devastationSources.Count(s => s.IsHealing);
            int growingCount = devastationSources.Count(s => !s.IsSaturated && !s.IsHealing &&
                (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range));
            int seedingCount = devastationSources.Count(s => !s.IsSaturated && !s.IsHealing &&
                s.BlocksSinceLastMetastasis >= s.MetastasisThreshold && s.CurrentRadius >= s.Range);

            var lines = new List<string>
            {
                $"=== Devastation Summary ({devastationSources.Count}/{config.MaxSources} cap) ===",
                $"  Protected (manual): {protectedCount} (never auto-removed)",
                $"  Metastasis children: {metastasisCount}",
                $"  Healing sources: {healingCount}",
                $"  Growing: {growingCount}",
                $"  Seeding (ready to spawn): {seedingCount}",
                $"  Saturated (done): {saturatedCount}"
            };

            // Group by generation level
            var byGeneration = devastationSources
                .GroupBy(s => s.GenerationLevel)
                .OrderBy(g => g.Key)
                .ToList();

            if (byGeneration.Count > 0)
            {
                lines.Add("  By Generation:");
                foreach (var gen in byGeneration)
                {
                    int growing = gen.Count(s => !s.IsSaturated && (s.BlocksSinceLastMetastasis < s.MetastasisThreshold || s.CurrentRadius < s.Range));
                    int seeding = gen.Count(s => !s.IsSaturated && s.BlocksSinceLastMetastasis >= s.MetastasisThreshold && s.CurrentRadius >= s.Range);
                    int sat = gen.Count(s => s.IsSaturated);
                    long totalBlocks = gen.Sum(s => (long)s.BlocksDevastatedTotal);
                    string genLabel = gen.Key == 0 ? "Origin" : $"Gen {gen.Key}";
                    lines.Add($"    {genLabel}: {gen.Count()} ({growing} growing, {seeding} seeding, {sat} saturated) - {totalBlocks:N0} blocks");
                }
            }

            // Total stats
            long grandTotalBlocks = devastationSources.Sum(s => (long)s.BlocksDevastatedTotal);
            lines.Add($"  Total blocks devastated: {grandTotalBlocks:N0}");

            if (devastationSources.Count >= config.MaxSources)
            {
                lines.Add("  Warning: At source cap - oldest sources will be removed for new metastasis");
            }

            return SendChatLines(args, lines, "Devastation summary sent to chat (scrollable)");
        }

        private TextCommandResult HandleMaxSourcesCommand(TextCommandCallingArgs args)
        {
            int? maxArg = args.Parsers[0].GetValue() as int?;

            if (!maxArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current max sources cap: {config.MaxSources}",
                    $"Active sources: {devastationSources.Count}/{config.MaxSources}",
                    "Usage: /devastate maxsources [number] (e.g., 20, 50, 100)"
                }, "Max sources info sent to chat");
            }

            int newMax = Math.Clamp(maxArg.Value, 1, 1000);
            config.MaxSources = newMax;
            SaveConfig();

            string warning = "";
            if (devastationSources.Count >= config.MaxSources)
            {
                warning = $" Warning: Already at or above cap ({devastationSources.Count} sources). No new metastasis will spawn.";
            }

            return TextCommandResult.Success($"Max sources cap set to {config.MaxSources}{warning}");
        }

        private TextCommandResult HandleMaxAttemptsCommand(TextCommandCallingArgs args)
        {
            int? attemptsArg = args.Parsers[0].GetValue() as int?;

            if (!attemptsArg.HasValue)
            {
                return SendChatLines(args, new[]
                {
                    $"Current max failed spawn attempts: {config.MaxFailedSpawnAttempts}",
                    "Usage: /devastate maxattempts [number] (e.g., 5, 10, 20)"
                }, "Max attempts info sent to chat");
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
                return SendChatLines(args, new[]
                {
                    $"Surface spreading (air contact): {status}",
                    "Usage: /devastate aircontact [on|off]"
                }, "Air contact setting sent to chat");
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

        private TextCommandResult HandleMarkersCommand(TextCommandCallingArgs args)
        {
            string onOff = args.Parsers[0].GetValue() as string;

            if (string.IsNullOrEmpty(onOff))
            {
                string status = config.ShowSourceMarkers ? "ON" : "OFF";
                return SendChatLines(args, new[]
                {
                    $"Source markers: {status}",
                    "Usage: /devastate markers [on|off]"
                }, "Marker setting sent to chat");
            }

            if (onOff.Equals("on", StringComparison.OrdinalIgnoreCase) || onOff == "1" || onOff.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                config.ShowSourceMarkers = true;
                SaveConfig();
                return TextCommandResult.Success("Source markers ENABLED");
            }
            else if (onOff.Equals("off", StringComparison.OrdinalIgnoreCase) || onOff == "0" || onOff.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                config.ShowSourceMarkers = false;
                SaveConfig();
                return TextCommandResult.Success("Source markers DISABLED");
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
                return SendChatLines(args, new[]
                {
                    $"Current minimum Y level: {config.MinYLevel}",
                    "Usage: /devastate miny [level] (e.g., 0, -64, 50)"
                }, "Min Y info sent to chat");
            }

            config.MinYLevel = levelArg.Value;
            SaveConfig();

            return TextCommandResult.Success($"Minimum Y level for new sources set to {config.MinYLevel}");
        }

        private TextCommandResult HandleStatusCommand(TextCommandCallingArgs args)
        {
            string statusText = isPaused ? "PAUSED" : "RUNNING";
            var lines = new List<string>
            {
                $"Devastation status: {statusText}",
                $"Speed multiplier: {config.SpeedMultiplier:F2}x",
                $"Active sources: {devastationSources.Count}/{config.MaxSources}",
                $"Tracked blocks for regen: {regrowingBlocks?.Count ?? 0}",
                $"Surface spreading: {(config.RequireSourceAirContact ? "ON" : "OFF")}",
                $"Min Y level: {config.MinYLevel}",
                $"Child spawn delay: {config.ChildSpawnDelaySeconds}s",
                $"Max failed attempts: {config.MaxFailedSpawnAttempts}"
            };
            return SendChatLines(args, lines, "Status sent to chat (scrollable)");
        }

        private TextCommandResult HandleChunkCommand(TextCommandCallingArgs args)
        {
            string action = args.Parsers[0].GetValue() as string;
            string value = args.Parsers[1].GetValue() as string;

            // Handle configuration commands first (don't require looking at a block)
            if (action == "spawn")
            {
                return HandleChunkSpawnCommand(args, value);
            }
            else if (action == "drain")
            {
                return HandleChunkDrainCommand(value);
            }
            else if (action == "spread")
            {
                return HandleChunkSpreadCommand(value);
            }
            else if (action == "spreadchance")
            {
                return HandleChunkSpreadChanceCommand(value);
            }
            else if (action == "list")
            {
                return HandleChunkListCommand(args);
            }
            else if (action == "perf" || action == "performance")
            {
                return HandleChunkPerfCommand(args);
            }
            else if (action == "repair")
            {
                return HandleChunkRepairCommand();
            }
            else if (action == "analyze")
            {
                return HandleChunkAnalyzeCommand(args);
            }
            else if (action == "fix")
            {
                return HandleChunkFixCommand(args);
            }
            else if (action == "unrepairable")
            {
                return HandleChunkUnrepairableCommand(args, value);
            }
            else if (action == "remove")
            {
                return HandleChunkRemoveCommand(args, value);
            }
            else
            {
                return HandleChunkMarkCommand(args);
            }
        }

        private TextCommandResult HandleChunkSpawnCommand(TextCommandCallingArgs args, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return SendChatLines(args, new[]
                {
                    "=== Mob Spawn Settings ===",
                    $"Spawn interval: {config.ChunkSpawnIntervalMinHours:F2}-{config.ChunkSpawnIntervalMaxHours:F2} game hours (random)",
                    $"Cooldown after spawn: {config.ChunkSpawnCooldownHours:F2} game hours",
                    $"Distance from player: {config.ChunkSpawnMinDistance}-{config.ChunkSpawnMaxDistance} blocks",
                    $"Max mobs per chunk: {config.ChunkSpawnMaxMobsPerChunk}",
                    "",
                    "Subcommands:",
                    "  /dv chunk spawn interval [min] [max] - Set random interval range (hours)",
                    "  /dv chunk spawn cooldown [hours] - Set cooldown after spawn",
                    "  /dv chunk spawn distance [min] [max] - Set spawn distance from player",
                    "  /dv chunk spawn maxmobs [count] - Set max mobs per chunk",
                    "  /dv chunk spawn reset - Reset mob counts in all chunks"
                }, "Spawn settings sent to chat");
            }

            // Parse subcommand
            string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string subCmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";

            if (subCmd == "interval")
            {
                if (parts.Length < 3)
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current interval: {config.ChunkSpawnIntervalMinHours:F2}-{config.ChunkSpawnIntervalMaxHours:F2} game hours",
                        "Usage: /dv chunk spawn interval [min] [max]",
                        "Example: /dv chunk spawn interval 0.5 1.0 (30-60 in-game minutes)"
                    }, "Interval info sent to chat");
                }

                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double minHours) ||
                    !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double maxHours))
                {
                    return TextCommandResult.Error("Invalid numbers. Usage: /dv chunk spawn interval [min] [max]");
                }

                if (minHours > maxHours)
                {
                    (minHours, maxHours) = (maxHours, minHours); // Swap if reversed
                }

                config.ChunkSpawnIntervalMinHours = Math.Clamp(minHours, 0.01, 100.0);
                config.ChunkSpawnIntervalMaxHours = Math.Clamp(maxHours, 0.01, 100.0);
                SaveConfig();
                return TextCommandResult.Success($"Spawn interval set to {config.ChunkSpawnIntervalMinHours:F2}-{config.ChunkSpawnIntervalMaxHours:F2} game hours");
            }
            else if (subCmd == "cooldown")
            {
                if (parts.Length < 2)
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current cooldown: {config.ChunkSpawnCooldownHours:F2} game hours",
                        "Usage: /dv chunk spawn cooldown [hours]",
                        "Example: /dv chunk spawn cooldown 4 (4 hour minimum between spawns)"
                    }, "Cooldown info sent to chat");
                }

                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double cooldown))
                {
                    return TextCommandResult.Error("Invalid number. Usage: /dv chunk spawn cooldown [hours]");
                }

                config.ChunkSpawnCooldownHours = Math.Clamp(cooldown, 0.0, 100.0);
                SaveConfig();
                return TextCommandResult.Success($"Spawn cooldown set to {config.ChunkSpawnCooldownHours:F2} game hours");
            }
            else if (subCmd == "distance")
            {
                if (parts.Length < 3)
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current distance: {config.ChunkSpawnMinDistance}-{config.ChunkSpawnMaxDistance} blocks from player",
                        "Usage: /dv chunk spawn distance [min] [max]",
                        "Example: /dv chunk spawn distance 16 48"
                    }, "Distance info sent to chat");
                }

                if (!int.TryParse(parts[1], out int minDist) || !int.TryParse(parts[2], out int maxDist))
                {
                    return TextCommandResult.Error("Invalid numbers. Usage: /dv chunk spawn distance [min] [max]");
                }

                if (minDist > maxDist)
                {
                    (minDist, maxDist) = (maxDist, minDist); // Swap if reversed
                }

                config.ChunkSpawnMinDistance = Math.Clamp(minDist, 1, 256);
                config.ChunkSpawnMaxDistance = Math.Clamp(maxDist, 1, 256);
                SaveConfig();
                return TextCommandResult.Success($"Spawn distance set to {config.ChunkSpawnMinDistance}-{config.ChunkSpawnMaxDistance} blocks from player");
            }
            else if (subCmd == "maxmobs")
            {
                if (parts.Length < 2)
                {
                    return SendChatLines(args, new[]
                    {
                        $"Current max mobs per chunk: {config.ChunkSpawnMaxMobsPerChunk}",
                        "Usage: /dv chunk spawn maxmobs [count]",
                        "Example: /dv chunk spawn maxmobs 5"
                    }, "Max mobs info sent to chat");
                }

                if (!int.TryParse(parts[1], out int maxMobs))
                {
                    return TextCommandResult.Error("Invalid number. Usage: /dv chunk spawn maxmobs [count]");
                }

                config.ChunkSpawnMaxMobsPerChunk = Math.Clamp(maxMobs, 0, 100);
                SaveConfig();
                return TextCommandResult.Success($"Max mobs per chunk set to {config.ChunkSpawnMaxMobsPerChunk}");
            }
            else if (subCmd == "reset")
            {
                int resetCount = 0;
                foreach (var chunk in devastatedChunks.Values)
                {
                    if (chunk.MobsSpawned > 0)
                    {
                        chunk.MobsSpawned = 0;
                        chunk.NextSpawnTime = 0;
                        resetCount++;
                    }
                }
                return TextCommandResult.Success($"Reset mob counts in {resetCount} chunks. Spawning can resume.");
            }
            else
            {
                return TextCommandResult.Error("Unknown spawn subcommand. Use: interval, cooldown, distance, maxmobs, reset");
            }
        }

        private TextCommandResult HandleChunkDrainCommand(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return TextCommandResult.Success($"Current stability drain rate: {config.ChunkStabilityDrainRate:F4} per 500ms tick (~{config.ChunkStabilityDrainRate * 2 * 100:F2}% per second). Use '/dv chunk drain [rate]' to set.");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double rate))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv chunk drain [rate]");
            }

            config.ChunkStabilityDrainRate = Math.Clamp(rate, 0.0, 1.0);
            SaveConfig();
            return TextCommandResult.Success($"Chunk stability drain rate set to {config.ChunkStabilityDrainRate:F4} (~{config.ChunkStabilityDrainRate * 2 * 100:F2}%/sec)");
        }

        private TextCommandResult HandleChunkSpreadCommand(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                string status = config.ChunkSpreadEnabled ? "ON" : "OFF";
                return TextCommandResult.Success($"Chunk spread: {status}. Spread chance: {config.ChunkSpreadChance * 100:F1}% every {config.ChunkSpreadIntervalSeconds:F0}s (at 1x speed). Usage: /dv chunk spread [on|off]");
            }

            if (value.Equals("on", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                config.ChunkSpreadEnabled = true;
                SaveConfig();
                return TextCommandResult.Success("Chunk spreading ENABLED - devastated chunks can spread to neighbors");
            }
            else if (value.Equals("off", StringComparison.OrdinalIgnoreCase) || value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                config.ChunkSpreadEnabled = false;
                SaveConfig();
                return TextCommandResult.Success("Chunk spreading DISABLED - devastated chunks will not spread");
            }
            else
            {
                return TextCommandResult.Error("Invalid value. Use: on, off, 1, 0, true, or false");
            }
        }

        private TextCommandResult HandleChunkSpreadChanceCommand(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return TextCommandResult.Success($"Current spread chance: {config.ChunkSpreadChance * 100:F1}%. Check interval: {config.ChunkSpreadIntervalSeconds:F0}s (at 1x speed). Usage: /dv chunk spreadchance [percent]");
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
            {
                return TextCommandResult.Error("Invalid number. Usage: /dv chunk spreadchance [percent]");
            }

            config.ChunkSpreadChance = Math.Clamp(percent / 100.0, 0.0, 1.0);
            SaveConfig();
            return TextCommandResult.Success($"Chunk spread chance set to {config.ChunkSpreadChance * 100:F1}%");
        }

        private TextCommandResult HandleChunkListCommand(TextCommandCallingArgs args)
        {
            if (devastatedChunks.Count == 0)
            {
                return TextCommandResult.Success("No devastated chunks");
            }

            var lines = new List<string> { $"Devastated chunks ({devastatedChunks.Count}):" };
            foreach (var chunk in devastatedChunks.Values.Take(20))
            {
                string status = chunk.IsFullyDevastated ? "fully devastated" : $"{chunk.DevastationLevel:P0} devastated";
                int frontierCount = chunk.DevastationFrontier?.Count ?? 0;
                lines.Add($"  ({chunk.ChunkX}, {chunk.ChunkZ}): {status}, {chunk.BlocksDevastated} blocks, frontier: {frontierCount}");
            }
            if (devastatedChunks.Count > 20)
            {
                lines.Add($"  ... and {devastatedChunks.Count - 20} more");
            }
            return SendChatLines(args, lines, "Chunk list sent to chat");
        }

        private TextCommandResult HandleChunkPerfCommand(TextCommandCallingArgs args)
        {
            var stats = GetPerformanceStats();
            int activeChunks = devastatedChunks.Values.Count(c => !c.IsFullyDevastated);
            int stuckChunks = devastatedChunks.Values.Count(c => !c.IsFullyDevastated && c.FrontierInitialized && (c.DevastationFrontier == null || c.DevastationFrontier.Count == 0) && c.BlocksDevastated < 1000);

            var lines = new List<string>
            {
                "=== Chunk Devastation Performance ===",
                $"Avg processing time: {stats.avgTime:F2}ms per tick",
                $"Peak processing time: {stats.peakTime:F2}ms",
                $"Avg tick interval: {stats.avgDt:F0}ms (expected: 500ms)",
                "",
                $"Total chunks: {devastatedChunks.Count}",
                $"Active chunks: {activeChunks}",
                $"Fully devastated: {devastatedChunks.Count - activeChunks}",
                $"Stuck chunks detected: {stuckChunks}",
                $"Chunks queued for repair: {chunksNeedingRepair.Count}",
                "",
                $"Total ticks processed: {totalTicksProcessed}",
                $"Total processing time: {totalProcessingTimeMs / 1000.0:F1}s"
            };

            return SendChatLines(args, lines, "Performance stats sent to chat");
        }

        private TextCommandResult HandleChunkRepairCommand()
        {
            // Force repair all stuck chunks
            int stuckCount = 0;
            foreach (var chunk in devastatedChunks.Values)
            {
                if (!chunk.IsFullyDevastated &&
                    (chunk.DevastationFrontier == null || chunk.DevastationFrontier.Count == 0))
                {
                    long chunkKey = chunk.ChunkKey;
                    if (!chunksNeedingRepair.Contains(chunkKey))
                    {
                        chunksNeedingRepair.Enqueue(chunkKey);
                        stuckCount++;
                    }
                }
            }

            if (stuckCount > 0)
            {
                return TextCommandResult.Success($"Queued {stuckCount} stuck chunks for repair");
            }
            else
            {
                return TextCommandResult.Success("No stuck chunks found");
            }
        }

        private TextCommandResult HandleChunkAnalyzeCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error("Look at a block to analyze its chunk");
            }

            BlockPos pos = blockSel.Position;
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!devastatedChunks.TryGetValue(chunkKey, out var chunk))
            {
                return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is not marked as devastated");
            }

            // Gather detailed diagnostic info
            var lines = new List<string>
            {
                $"=== Chunk Analysis: ({chunkX}, {chunkZ}) ===",
                $"Chunk key: {chunkKey}",
                $"Blocks devastated: {chunk.BlocksDevastated}",
                $"Devastation level: {chunk.DevastationLevel:P1}",
                $"IsFullyDevastated: {chunk.IsFullyDevastated}",
                $"IsUnrepairable: {chunk.IsUnrepairable}",
                $"FrontierInitialized: {chunk.FrontierInitialized}",
                $"Frontier count: {chunk.DevastationFrontier?.Count ?? 0}",
                $"Bleed frontier count: {chunk.BleedFrontier?.Count ?? 0}",
                $"FillInTickCounter: {chunk.FillInTickCounter}",
                $"ConsecutiveEmptyFrontierChecks: {chunk.ConsecutiveEmptyFrontierChecks}",
                $"RepairAttemptCount: {chunk.RepairAttemptCount}/5",
                $"Marked time: {chunk.MarkedTime:F2} hours",
                ""
            };

            // Check frontier blocks validity
            if (chunk.DevastationFrontier != null && chunk.DevastationFrontier.Count > 0)
            {
                lines.Add($"First 5 frontier blocks:");
                foreach (var frontierPos in chunk.DevastationFrontier.Take(5))
                {
                    Block block = sapi.World.BlockAccessor.GetBlock(frontierPos);
                    string blockName = block?.Code?.ToString() ?? "null";
                    bool isDevastated = block != null && IsAlreadyDevastated(block);
                    lines.Add($"  {frontierPos}: {blockName} (devastated: {isDevastated})");
                }
            }
            else
            {
                lines.Add("Frontier is EMPTY - this chunk cannot spread!");
            }

            // Scan chunk for actual devastated blocks
            int startX = chunkX * CHUNK_SIZE;
            int startZ = chunkZ * CHUNK_SIZE;
            int devastatedBlocksFound = 0;
            int convertibleBlocksFound = 0;
            BlockPos sampleDevastatedPos = null;

            for (int dx = 0; dx < CHUNK_SIZE && devastatedBlocksFound < 100; dx += 4)
            {
                for (int dz = 0; dz < CHUNK_SIZE && devastatedBlocksFound < 100; dz += 4)
                {
                    int x = startX + dx;
                    int z = startZ + dz;
                    int surfaceY = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
                    if (surfaceY <= 0) continue;

                    for (int yOff = -3; yOff <= 3; yOff++)
                    {
                        BlockPos checkPos = new BlockPos(x, surfaceY + yOff, z);
                        Block block = sapi.World.BlockAccessor.GetBlock(checkPos);
                        if (block == null || block.Id == 0) continue;

                        if (IsAlreadyDevastated(block))
                        {
                            devastatedBlocksFound++;
                            if (sampleDevastatedPos == null) sampleDevastatedPos = checkPos.Copy();
                        }
                        else if (TryGetDevastatedForm(block, out _, out _))
                        {
                            convertibleBlocksFound++;
                        }
                    }
                }
            }

            lines.Add("");
            lines.Add($"Chunk scan (sampled every 4 blocks):");
            lines.Add($"  Devastated blocks found: {devastatedBlocksFound}");
            lines.Add($"  Convertible blocks found: {convertibleBlocksFound}");
            if (sampleDevastatedPos != null)
            {
                lines.Add($"  Sample devastated at: {sampleDevastatedPos}");
            }

            // Diagnose the problem
            lines.Add("");
            lines.Add("Diagnosis:");
            if (chunk.IsFullyDevastated)
            {
                lines.Add("  Chunk is marked fully devastated - no more spreading needed");
            }
            else if (chunk.DevastationFrontier == null || chunk.DevastationFrontier.Count == 0)
            {
                if (devastatedBlocksFound == 0)
                {
                    lines.Add("  PROBLEM: No frontier AND no devastated blocks found!");
                    lines.Add("  This chunk was likely created but never initialized properly.");
                }
                else if (convertibleBlocksFound == 0)
                {
                    lines.Add("  Frontier empty but no convertible blocks - chunk may be done");
                }
                else
                {
                    lines.Add("  PROBLEM: Has devastated blocks but empty frontier!");
                    lines.Add("  Use '/dv chunk repair' to fix, or '/dv chunk fix' on this chunk");
                }
            }
            else
            {
                lines.Add("  Chunk appears healthy - frontier has blocks to spread from");
            }

            // Check if queued for repair
            if (chunksNeedingRepair.Contains(chunkKey))
            {
                lines.Add("  Note: This chunk is queued for repair");
            }

            return SendChatLines(args, lines, "Chunk analysis sent to chat");
        }

        private TextCommandResult HandleChunkFixCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error("Look at a block to fix its chunk");
            }

            BlockPos pos = blockSel.Position;
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (!devastatedChunks.TryGetValue(chunkKey, out var chunk))
            {
                return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is not marked as devastated");
            }

            // Force re-initialize and reset repair state
            int oldFrontierCount = chunk.DevastationFrontier?.Count ?? 0;
            chunk.FrontierInitialized = false;
            chunk.IsFullyDevastated = false;
            chunk.IsUnrepairable = false; // Reset unrepairable flag
            chunk.RepairAttemptCount = 0;
            chunk.ConsecutiveEmptyFrontierChecks = 0;
            InitializeChunkFrontier(chunk);

            int newFrontierCount = chunk.DevastationFrontier?.Count ?? 0;
            return TextCommandResult.Success($"Fixed chunk ({chunkX}, {chunkZ}): frontier {oldFrontierCount} to {newFrontierCount} blocks (repair state reset)");
        }

        private TextCommandResult HandleChunkUnrepairableCommand(TextCommandCallingArgs args, string value)
        {
            if (value == "list" || string.IsNullOrEmpty(value))
            {
                var unrepairableChunks = devastatedChunks.Values.Where(c => c.IsUnrepairable).ToList();
                if (unrepairableChunks.Count == 0)
                {
                    return TextCommandResult.Success("No unrepairable chunks found");
                }

                var lines = new List<string> { $"=== Unrepairable Chunks ({unrepairableChunks.Count}) ===" };
                foreach (var chunk in unrepairableChunks.Take(20))
                {
                    lines.Add($"  ({chunk.ChunkX}, {chunk.ChunkZ}): {chunk.BlocksDevastated} blocks, {chunk.RepairAttemptCount} repair attempts");
                }
                if (unrepairableChunks.Count > 20)
                {
                    lines.Add($"  ... and {unrepairableChunks.Count - 20} more");
                }
                return SendChatLines(args, lines, "Unrepairable chunks list sent to chat");
            }
            else if (value == "clear")
            {
                int count = 0;
                foreach (var chunk in devastatedChunks.Values.Where(c => c.IsUnrepairable))
                {
                    chunk.IsUnrepairable = false;
                    chunk.RepairAttemptCount = 0;
                    chunk.ConsecutiveEmptyFrontierChecks = 0;
                    count++;
                }
                return TextCommandResult.Success($"Reset {count} unrepairable chunks - they will be retried");
            }
            else if (value == "remove")
            {
                int count = devastatedChunks.Values.Count(c => c.IsUnrepairable);
                var keysToRemove = devastatedChunks.Where(kvp => kvp.Value.IsUnrepairable).Select(kvp => kvp.Key).ToList();
                foreach (var key in keysToRemove)
                {
                    devastatedChunks.Remove(key);
                }
                return TextCommandResult.Success($"Removed {count} unrepairable chunks from tracking");
            }
            else
            {
                return TextCommandResult.Error("Usage: /dv chunk unrepairable [list|clear|remove]");
            }
        }

        private TextCommandResult HandleChunkRemoveCommand(TextCommandCallingArgs args, string value)
        {
            if (value == "all")
            {
                int count = devastatedChunks.Count;
                devastatedChunks.Clear();
                return TextCommandResult.Success($"Removed all {count} devastated chunks");
            }

            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                return TextCommandResult.Error("Look at a block to remove its chunk, or use '/dv chunk remove all'");
            }

            BlockPos pos = blockSel.Position;
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (devastatedChunks.ContainsKey(chunkKey))
            {
                devastatedChunks.Remove(chunkKey);
                return TextCommandResult.Success($"Removed devastated chunk at ({chunkX}, {chunkZ})");
            }
            else
            {
                return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is not marked as devastated");
            }
        }

        private TextCommandResult HandleChunkMarkCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("This command must be run by a player");

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null)
            {
                // No block selected - show help
                return SendChatLines(args, new[]
                {
                    "Chunk commands:",
                    "  /dv chunk - Mark looked-at chunk as devastated",
                    "  /dv chunk remove [all] - Remove devastation from chunk",
                    "  /dv chunk list - List all devastated chunks",
                    "  /dv chunk analyze - Detailed diagnostics for looked-at chunk",
                    "  /dv chunk fix - Force re-initialize looked-at chunk",
                    "  /dv chunk perf - Show performance stats",
                    "  /dv chunk repair - Queue all stuck chunks for repair",
                    "  /dv chunk unrepairable [list|clear|remove] - Manage unrepairable chunks",
                    "  /dv chunk spawn - Show mob spawn settings and subcommands",
                    "  /dv chunk drain [rate] - Set stability drain rate",
                    "  /dv chunk spread [on|off] - Toggle chunk spreading",
                    "  /dv chunk spreadchance [percent] - Set spread chance"
                }, "Chunk help sent to chat");
            }

            BlockPos pos = blockSel.Position.Copy();
            int chunkX = pos.X / CHUNK_SIZE;
            int chunkZ = pos.Z / CHUNK_SIZE;
            long chunkKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ);

            if (devastatedChunks.ContainsKey(chunkKey))
            {
                return TextCommandResult.Error($"Chunk at ({chunkX}, {chunkZ}) is already marked as devastated");
            }

            var newChunk = new DevastatedChunk
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                MarkedTime = sapi.World.Calendar.TotalHours,
                DevastationLevel = 0.0,
                IsFullyDevastated = false,
                FrontierInitialized = true,
                DevastationFrontier = new List<BlockPos> { pos }
            };

            // Devastate the starting block immediately
            Block startBlock = sapi.World.BlockAccessor.GetBlock(pos);
            if (startBlock != null && startBlock.Id != 0 && !IsAlreadyDevastated(startBlock))
            {
                if (TryGetDevastatedForm(startBlock, out string devastatedBlock, out string regeneratesTo))
                {
                    Block newBlock = sapi.World.GetBlock(new AssetLocation("game", devastatedBlock));
                    if (newBlock != null)
                    {
                        sapi.World.BlockAccessor.SetBlock(newBlock.Id, pos);
                        regrowingBlocks.Add(new RegrowingBlocks
                        {
                            Pos = pos,
                            Out = regeneratesTo,
                            LastTime = sapi.World.Calendar.TotalHours
                        });
                        newChunk.BlocksDevastated++;
                    }
                }
            }

            devastatedChunks[chunkKey] = newChunk;
            return TextCommandResult.Success($"Marked chunk at ({chunkX}, {chunkZ}) as devastated starting from {pos}. Devastation will spread in cardinal directions.");
        }

        #endregion
    }
}
