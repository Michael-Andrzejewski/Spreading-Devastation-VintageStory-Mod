# Spreading Devastation

A Vintage Story mod that makes temporal rifts and manually-marked blocks spread devastation 10x faster than normal.

## Features

- **Temporal rifts actively corrupt the landscape** around them at 10x speed
- **Manual devastation sources** - mark any block to spread devastation outward
- **Manual healing sources** - mark any block to heal devastation outward
- **Smart outward spreading** - starts at center, naturally expands outward
- **Adaptive radius expansion** - automatically expands search area as center fills
- **10x faster spreading** - processes every 10ms instead of 100ms
- **Devastated blocks slowly regenerate** after 60 in-game hours
- **Multiple block types affected**: soil, rock, plants, crops, and more

## Commands

### `/devastate add [range <blocks>] [amount <count>]`
- Look at any block and run this command to mark it as a **devastation source**
- Spreads devastation outward from the center
- **range**: How far devastation spreads (default: 8 blocks, max: 128)
- **amount**: How many blocks to devastate per tick (default: 1, max: 100)
  - The game ticks every 10ms, so amount 10 = 100 blocks per second!
- Requires `controlserver` privilege (admin/moderator)

### `/devastate heal [range <blocks>] [amount <count>]`
- Look at any block and run this command to mark it as a **healing source**
- Heals devastated blocks back to normal outward from the center
- Uses the same adaptive radius and distance-weighting as devastation
- **range**: How far healing spreads (default: 8 blocks, max: 128)
- **amount**: How many blocks to heal per tick (default: 1, max: 100)
- Requires `controlserver` privilege (admin/moderator)

### `/devastate remove [all]`
- Look at a marked source and run `/devastate remove` to remove it
- Or use `/devastate remove all` to remove **all sources** at once
- Stops spreading/healing from that location

### `/devastate list`
- Shows all manually-marked sources with their positions, range, amount, and type
- Shows success rate and current search radius

**Examples:**
```
/devastate add                          # Default devastation: 8 block range, 1 block per tick
/devastate add range 16 amount 10       # 16 block range, 10 blocks per tick (100/sec!)
/devastate heal range 16 amount 10      # Heal at same rate as devastation
/devastate heal amount 50               # Super fast healing!
/devastate remove all                   # Clear all sources
```

## How It Works

### Spreading Mechanic
1. **Rifts**: Every 10ms (100x per second), each active temporal rift devastates 1 random block within 8 blocks
2. **Manual Sources**: Every 10ms, each manually-marked source devastates/heals X blocks (where X = amount parameter)
3. **Outward Expansion**: Sources use distance-weighted random selection to heavily favor closer blocks
   - 75% of attempts target the inner 25% of the radius
   - Creates natural-looking outward spread
4. **Adaptive Radius**: Each source starts searching within 3 blocks, then automatically expands as the center fills
   - When success rate drops below 20%, radius expands by 2 blocks
   - When success rate drops below 10%, radius expands by 4 blocks (faster)
   - Massive performance gain - no wasted checks on already-processed areas!
5. **Smart Retries**: If a block is already devastated/healed, the system automatically tries another block (up to 5 attempts per block)

### Regeneration
- Devastated blocks are tracked for natural regeneration
- After 60 in-game hours, devastated blocks regenerate to their original forms
- Regeneration data is saved with the world

### Performance
- The system skips already-devastated blocks to focus on spreading
- Efficient retry logic ensures blocks are actually devastated (not wasted on already-corrupted areas)
- Removed blocks are automatically cleaned from the source list

### Tick Rate
- The mod runs every **10 milliseconds** (100 times per second)
- With `amount 10`, you devastate **1000 blocks per second** per source!
- With `amount 1` (default), you devastate **100 blocks per second** per source

### Block Conversions

| Original Block | Devastated Block | Regenerates To |
|---------------|------------------|----------------|
| Soil | Devastated Soil (type 0) | Very Low Soil |
| Rock | Devastated Rock | Obsidian |
| Tall Grass | Devastation Growth | Air |
| Berry Bushes | Thorny Growth | Oak Leaves |
| Flowers/Ferns | Shrike Growth | Air |
| Crops | Shard Growth | Air |
| Leaves | Bush Growth | Air |
| Gravel | Devastated Soil (type 1) | Sludgy Gravel |
| Sand | Devastated Soil (type 2) | Sludgy Gravel |
| Logs | Devastated Soil (type 3) | Aged Logs |

## Installation

1. Download `SpreadingDevastation.zip` from the releases
2. Place it in your Vintage Story `Mods` folder
3. Start the game

## Building from Source

1. Set the `VINTAGE_STORY` environment variable to your Vintage Story installation directory
2. Run `dotnet build`
3. The mod will be packaged as `SpreadingDevastation.zip` in the bin folder

## Configuration

This mod currently has no configuration options. Rifts will always spread devastation while active.

## Compatibility

- **Vintage Story**: 1.21.0+
- **Side**: Server-side (clients don't need to install it)

## Credits

Based on the spreading devastation mechanic from "Temporality Plus" by IdiotMage.

## License

Feel free to modify and redistribute.

