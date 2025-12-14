# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the mod from the project directory
cd "Spreading Devastation"
dotnet build

# The build automatically:
# 1. Packages into SpreadingDevastation.zip in bin/Debug/net8.0/ or bin/Release/net8.0/
# 2. Deploys to C:\Users\maaro\AppData\Roaming\VintagestoryData\Mods\
```

**IMPORTANT**: After any code changes, always run `dotnet build` from the `Spreading Devastation` directory. The build will automatically deploy the mod to the Vintage Story mods folder, replacing any previous version.

Note: The .csproj references Vintage Story DLLs at hardcoded paths (`C:\Users\maaro\AppData\Roaming\Vintagestory\`). You may need to update these paths or set up proper environment variables.

## Architecture

This is a **Vintage Story mod** (single-file C# mod) that makes temporal rifts spread landscape devastation. The entire mod is contained in one file: `Spreading Devastation/SpreadingDevastationModSystem.cs`.

### Core Components

- **SpreadingDevastationModSystem** - Main mod class, extends `ModSystem`. Registers game tick listeners and chat commands.
- **SpreadingDevastationConfig** - Configuration class saved to `ModConfig/SpreadingDevastationConfig.json`. Controls speed, thresholds, and behavior.
- **DevastationSource** - Represents a point that spreads devastation. Tracks position, range, adaptive radius, metastasis state, and statistics.
- **RegrowingBlocks** - Tracks devastated blocks for eventual regeneration back to original form.

### Key Mechanics

1. **Tick System**: Runs every 10ms (100 ticks/second). Processes rifts and manual sources each tick.
2. **Adaptive Radius**: Sources start with small search radius (3 blocks), expand as local area fills up. Controlled by success rate thresholds.
3. **Distance-Weighted Selection**: Uses `GenerateWeightedDistance()` with inverse square distribution to bias block selection toward center.
4. **Metastasis System**: Sources spawn child sources when they devastate enough blocks and reach max radius. Creates spreading "cancer-like" growth pattern.
5. **Block Conversions**: Maps defined in `TryGetDevastatedForm()` and `TryGetHealedForm()`. Soil→devastatedsoil, rock→drock, plants→devgrowth variants.

### Commands

All commands require `controlserver` privilege. Both `/devastate` and `/dv` work:
- `/dv add [range X] [amount Y]` - Add devastation source at looked-at block
- `/dv heal [range X] [amount Y]` - Add healing source
- `/dv remove [all|saturated|metastasis]` - Remove sources
- `/dv list [count|summary]` - List sources
- `/dv stop` / `/dv start` - Pause/resume all processing
- `/dv status` - Show current state
- `/devastationspeed <multiplier>` - Set global speed multiplier
- `/devastationconfig` - Reload config file

### Data Persistence

Uses ProtoBuf serialization. Data stored in world save:
- `regrowingBlocks` - List of blocks pending regeneration
- `devastationSources` - All active devastation/healing sources
- `devastationPaused` - Pause state
- `devastationNextSourceId` - Counter for unique source IDs

### Configuration Options

Key config fields in `SpreadingDevastationConfig`:
- `SpeedMultiplier` - Global speed (default: 1.0)
- `MaxSources` - Cap on total sources (default: 20)
- `MetastasisThreshold` - Blocks before spawning child (default: 300)
- `RegenerationHours` - Time before blocks heal (default: 60.0)
- `RequireSourceAirContact` - Surface-only spreading (default: false)
- `ChildSpawnDelaySeconds` - Cooldown between spawning children (default: 120)

## Chat Message Formatting

**CRITICAL**: Vintage Story's chat system uses HTML-like rich text formatting. Any text that looks like an HTML tag will be interpreted as formatting, which can break chat display.

### Rules for Chat Messages

1. **Use square brackets `[]` instead of angle brackets `<>` for placeholders**:
   - WRONG: `"Usage: /dv speed <multiplier>"`
   - RIGHT: `"Usage: /dv speed [multiplier]"`

2. **Avoid forward slashes after words** (looks like closing HTML tags):
   - WRONG: `"Rate: 10 blocks/sec"` - interpreted as `</blocks>` closing tag
   - RIGHT: `"Rate: 10 blk/s"` or `"Rate: 10 blocks per sec"`

3. **Common patterns to avoid**:
   - `<value>`, `<number>`, `<name>` → use `[value]`, `[number]`, `[name]`
   - `blocks/sec`, `items/min` → use `blk/s`, `items per min`
   - Any `<word>` pattern in strings sent to chat

### Command Argument Parsers

When commands need multiple space-separated values, use `OptionalAll` instead of `OptionalWord`:

```csharp
// WRONG - only captures first word after "color"
.WithArgs(api.ChatCommands.Parsers.OptionalWord("setting"),
          api.ChatCommands.Parsers.OptionalWord("value"))

// RIGHT - captures all remaining text: "0.5 0.3 0.2"
.WithArgs(api.ChatCommands.Parsers.OptionalWord("setting"),
          api.ChatCommands.Parsers.OptionalAll("value"))
```

Use `OptionalAll` when the command accepts:
- Multiple numeric values (e.g., `/dv fog color 0.5 0.3 0.2`)
- Subcommands with their own arguments (e.g., `/dv chunk spawn interval 0.5 1.0`)
