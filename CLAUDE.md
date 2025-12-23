# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Quick Reference - Key Files

| File | Purpose |
|------|---------|
| `SpreadingDevastationModSystem.cs` | Core logic: spreading, metastasis, chunks, rift wards, particles |
| `SpreadingDevastationModSystem.Commands.cs` | All `/dv` command handlers |
| `SpreadingDevastationModSystem.Tests.cs` | In-game test suite (`/dv testsuite`) |
| `SpreadingDevastationConfig.cs` | Configuration class with all settings |
| `DataClasses.cs` | ProtoBuf data structures (DevastationSource, DevastatedChunk, etc.) |
| `DevastationFogRenderer.cs` | Client-side fog/atmosphere rendering |

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

## Troubleshooting Build Issues

### After Git Revert or When Builds Seem Stale

The incremental build system may not detect changes after `git revert` (file timestamps unchanged). If the build says "succeeded" but the mod isn't updated:

```bash
# Force a full rebuild
dotnet build --no-incremental
```

Always verify the deployed zip has a current timestamp:
```bash
ls -la "C:/Users/maaro/AppData/Roaming/VintagestoryData/Mods/SpreadingDevastation.zip"
```

### When Reverting to an Older Version

The mod config and cache may retain settings from newer versions, causing issues:

1. **Backup/delete the config file:**
   - Location: `C:\Users\maaro\AppData\Roaming\VintagestoryData\ModConfig\SpreadingDevastationConfig.json`
   - The mod will recreate it with defaults on next load

2. **Clear the mod cache (if issues persist):**
   - Location: `C:\Users\maaro\AppData\Roaming\VintagestoryData\Cache\unpack\`

### Quick Checklist After Reverting

- [ ] Run `dotnet build --no-incremental`
- [ ] Verify deployed zip timestamp matches current time
- [ ] Backup/delete mod config file
- [ ] Clear cache folder if issues persist

## Architecture

This is a **Vintage Story mod** that makes temporal rifts spread landscape devastation. The mod uses partial classes split across multiple files (see Quick Reference above), with core logic in `SpreadingDevastationModSystem.cs`.

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

2. **Avoid forward slashes after ANY letters** (looks like closing HTML tags):
   - WRONG: `"Rate: 10 blocks/sec"` - interpreted as `</blocks>` closing tag
   - WRONG: `"Rate: 10 blk/s"` - still interpreted as `</blk>` closing tag
   - RIGHT: `"Rate: 10 blocks per sec"` or `"Rate: 10 blocks (per sec)"`

3. **Common patterns to avoid**:
   - `<value>`, `<number>`, `<name>` → use `[value]`, `[number]`, `[name]`
   - `blocks/sec`, `blk/s`, `items/min` → use `blocks per sec`, `items per min`
   - `on/off`, `fog/sky` → use `[on|off]`, `fog and sky`
   - `->` arrows → use `to` (the `>` character can break VTML parsing)
   - Any `word/anything` pattern in strings sent to chat (the `/` after letters looks like a closing tag)

4. **For multi-line output, send each line as a separate message**:
   - WRONG: `string.Join("\n", lines)` or `string.Join("<br>", lines)` - can break VTML parsing
   - RIGHT: `foreach (var line in lines) { player.SendMessage(..., line, ...); }` - each line is a separate message

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

## Common Gotchas

1. **ProtoBuf requires `[ProtoMember]` attributes** - Any new field in DataClasses.cs needs a unique ProtoMember index or it won't serialize
2. **Client vs Server code** - `sapi` is server-only, `capi` is client-only. Check for null before using. Network sync required for client-side effects.
3. **Block codes include domain** - Use `new AssetLocation("game", blockPath)` not just the path string
4. **Particle spawning is server-side** - Particles are spawned via `sapi.World.SpawnParticles()` and automatically synced to clients
5. **Config changes need `SaveConfig()` call** - And `BroadcastFogConfig()` if fog settings changed
