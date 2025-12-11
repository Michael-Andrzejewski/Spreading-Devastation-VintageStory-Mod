# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the mod (requires VINTAGE_STORY environment variable or edit .csproj paths)
dotnet build

# The build automatically packages into SpreadingDevastation.zip in bin/Debug/net8.0/ or bin/Release/net8.0/
```

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
