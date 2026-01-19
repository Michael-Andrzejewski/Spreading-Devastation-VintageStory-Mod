# Vintage Story: Aged to Normal Block Mapping

This document maps all aged block codes to their non-aged counterparts.
Generated from Vintage Story v1.21.6 block codes.

## Wood Types Available

The following wood types are used as variants for most wooden blocks:
- `acacia`, `baldcypress`, `birch`, `ebony`, `kapok`, `larch`, `maple`, `oak`, `pine`, `purpleheart`, `redwood`, `walnut`

---

## Block Mappings

### Planks
| Aged Block | Normal Blocks |
|------------|---------------|
| `planks-aged-{ns\|ud\|we}` | `planks-{woodtype}-{ns\|ud\|we}` |
| `planks-agedebony-{ns\|ud\|we}` | `planks-ebony-{ns\|ud\|we}` |

**Note:** `planks-rottenebony` also exists as a variant.

---

### Plank Slabs
| Aged Block | Normal Blocks |
|------------|---------------|
| `plankslab-aged-{direction}-{free\|snow}` | `plankslab-{woodtype}-{direction}-{free\|snow}` |

Directions: `north`, `east`, `south`, `west`, `up`, `down`

---

### Plank Stairs
| Aged Block | Normal Blocks |
|------------|---------------|
| `plankstairs-aged-{up\|down}-{direction}-{free\|snow}` | `plankstairs-{woodtype}-{up\|down}-{direction}-{free\|snow}` |

---

### Wooden Paths
| Aged Block | Normal Blocks |
|------------|---------------|
| `woodenpath-aged-{ns\|we}` | `woodenpath-{woodtype}-{ns\|we}` |

---

### Logs (Grown and Placed)
| Aged Block | Normal Blocks |
|------------|---------------|
| `log-grown-aged-{ud\|ns\|we}` | `log-grown-{woodtype}-{ud\|ns\|we}` |
| `log-placed-aged-{ud\|ns\|we}` | `log-placed-{woodtype}-{ud\|ns\|we}` |

---

### Debarked Logs
| Aged Block | Normal Blocks |
|------------|---------------|
| `debarkedlog-aged-{ud\|ns\|we}` | `debarkedlog-{woodtype}-{ud\|ns\|we}` |
| `debarkedlog-veryaged-{ud\|ns\|we}` | `debarkedlog-{woodtype}-{ud\|ns\|we}` |
| `debarkedlog-veryagedrotten-{ud\|ns\|we}` | `debarkedlog-{woodtype}-{ud\|ns\|we}` |

---

### Log Quads
| Aged Block | Normal Blocks |
|------------|---------------|
| `logquad-barkedcorner-aged-{direction}` | `logquad-barkedcorner-{woodtype}-{direction}` |
| `logquad-debarkedcorner-aged-{direction}` | `logquad-debarkedcorner-{woodtype}-{direction}` |
| `logquad-placed-aged-{ud\|ns\|we}` | `logquad-placed-{woodtype}-{ud\|ns\|we}` |
| `logquad-debarked-aged-{ud\|ns\|we}` | `logquad-debarked-{woodtype}-{ud\|ns\|we}` |

---

### Carved Logs
| Aged Block | Normal Blocks |
|------------|---------------|
| `carvedlog-acacia-aged-{position}-{style}-{orientation}` | No non-aged equivalent exists |

**Note:** Carved logs only come in aged acacia variant. No normal wood carved logs exist.

---

### Support Beams
| Aged Block | Normal Blocks |
|------------|---------------|
| `supportbeam-aged` | `supportbeam-{woodtype}` |
| `supportbeam-veryaged` | `supportbeam-{woodtype}` |
| `supportbeam-veryagedrotten` | `supportbeam-{woodtype}` |

**Note:** Metal support beams also exist: `supportbeam-tarnishedmetal-{metaltype}`

---

### Beds
| Aged Block | Normal Blocks |
|------------|---------------|
| `bed-woodaged-{head\|feet}-{direction}` | `bed-wood-{head\|feet}-{direction}` |

---

### Chairs
| Aged Block | Normal Blocks |
|------------|---------------|
| `chair-aged` | `chair-plain`, `chair-{color}` |

Colors: `black`, `blue`, `brown`, `gray`, `green`, `orange`, `pink`, `purple`, `red`, `white`, `yellow`

---

### Tables
| Aged Block | Normal Blocks |
|------------|---------------|
| `table-aged` | `table-normal`, `table-{marble}marble` |

Marble types: `green`, `red`, `white`

---

### Display Cases
| Aged Block | Normal Blocks |
|------------|---------------|
| `displaycase-aged{1-4}` | `displaycase-generic` |
| `talldisplaycase-aged{1-5}` | `talldisplaycase-generic` |

---

### Wooden Fences
| Aged Block | Normal Blocks |
|------------|---------------|
| `woodenfence-aged-{connections}-{free\|snow}` | `woodenfence-{woodtype}-{connections}-{free\|snow}` |

Connections: `empty`, `n`, `e`, `s`, `w`, `ne`, `ns`, `nw`, `es`, `ew`, `sw`, `nes`, `new`, `nsw`, `esw`, `nesw`

---

### Wooden Fence Gates
| Aged Block | Normal Blocks |
|------------|---------------|
| `woodenfencegate-aged-{n\|w}-{opened\|closed}-{left\|right}-{free\|snow}` | `woodenfencegate-{woodtype}-{n\|w}-{opened\|closed}-{left\|right}-{free\|snow}` |

---

### Rough Hewn Fences
| Aged Block | Normal Blocks |
|------------|---------------|
| `roughhewnfence-aged-{connections}-{free\|snow}` | `roughhewnfence-{woodtype}-{connections}-{free\|snow}` |

---

### Rough Hewn Fence Gates
| Aged Block | Normal Blocks |
|------------|---------------|
| `roughhewnfencegate-aged-{n\|w}-{opened\|closed}-{free\|snow}` | `roughhewnfencegate-{woodtype}-{n\|w}-{opened\|closed}-{free\|snow}` |

---

### Doors
| Aged Block | Normal Blocks |
|------------|---------------|
| `door-solid-aged` | `door-solid-{woodtype}` |
| `door-sleek-windowed-aged` | `door-sleek-windowed-{woodtype}` |
| `door-1x3gate-aged` | `door-1x3gate-{woodtype}` |
| `door-2x2gate-aged` | `door-2x2gate-{woodtype}` |
| `door-2x3gate-aged` | `door-2x3gate-{woodtype}` |
| `door-2x4gate-aged` | `door-2x4gate-{woodtype}` |
| `door-1x2heavy-aged` | No non-aged equivalent |
| `door-1x3heavy-aged` | No non-aged equivalent |
| `door-2x4heavy-aged` | No non-aged equivalent |

**Note:** Heavy doors only come in aged variant. Basic doors use `door-plank-*` and `door-log-*` patterns.

---

### Trapdoors
| Aged Block | Normal Blocks |
|------------|---------------|
| `trapdoor-solid-aged-{1-4}` | `trapdoor-solid-{woodtype}-1` |
| `trapdoor-window-aged-{1-4}` | `trapdoor-window-{woodtype}-1` |

**Note:** Aged trapdoors have 4 damage variants (1-4), normal have only 1.

---

### Torch Holders
| Aged Block | Normal Blocks |
|------------|---------------|
| `torchholder-aged-{empty\|filled}-{direction}` | `torchholder-brass-{empty\|filled}-{direction}` |

---

### Leaded Glass Panes
| Aged Block | Normal Blocks |
|------------|---------------|
| `glasspane-leaded-aged-{ns\|ew}` | `glasspane-leaded-{woodtype}-{ns\|ew}` |

---

### Hay
| Aged Block | Normal Blocks |
|------------|---------------|
| `hay-aged-{ud\|ns\|we}` | `hay-normal-{ud\|ns\|we}` |

---

### Aged Wallpaper Planks
| Aged Block | Normal Blocks |
|------------|---------------|
| `agedwallpaperplanks-{blue\|green\|grey}-{direction}` | No non-aged equivalent |

**Note:** Wallpaper planks only exist in aged form.

---

### Aged Stone Bricks
| Aged Block | Normal Blocks |
|------------|---------------|
| `agedstonebricks-{stonetype}` | `stonebricks-{stonetype}` |

Stone types: `andesite`, `basalt`, `bauxite`, `chalk`, `chert`, `claystone`, `conglomerate`, `granite`, `limestone`, `peridotite`, `phyllite`, `sandstone`, `shale`, `slate`

**Note:** `stonebricks-suevite` exists but has no aged equivalent.

---

### Diamond Stone
| Aged Block | Normal Blocks |
|------------|---------------|
| `diamond-stone-clean-aged` | `diamond-stone-clean` |
| `diamond-stone-dark-aged` | `diamond-stone-dark` |
| `diamond-stone-light-aged` | `diamond-stone-light` |
| `diamond-stone-mixed-aged` | `diamond-stone-mixed` |

---

### Beams (Roofing)
| Aged Block | Normal Blocks |
|------------|---------------|
| `beam-plane-aged-{free\|snow}` | `beam-plane-{material}-{free\|snow}` |
| `beam-ridge-aged-{free\|snow}` | `beam-ridge-{material}-{free\|snow}` |

Materials: Wood types + `bamboo`, `slate`, `copper`, clay colors (`blackclay`, `brownclay`, `creamclay`, `fireclay`, `grayclay`, `orangeclay`, `redclay`, `tanclay`)

---

### Slanted Roofing (All Variants)

All slanted roofing blocks follow this pattern:

| Aged Block | Normal Blocks |
|------------|---------------|
| `slantedroofing-aged-{direction}-{free\|snow}` | `slantedroofing-{material}-{direction}-{free\|snow}` |
| `slantedroofing-agedthatch-{direction}-{free\|snow}` | `slantedroofing-thatch-{direction}-{free\|snow}` |

This applies to all slanted roofing variants:
- `slantedroofing-*`
- `slantedroofingbottom-*`
- `slantedroofingcornerinner-*`
- `slantedroofingcornerouter-*`
- `slantedroofinghalfleft-*`
- `slantedroofinghalfright-*`
- `slantedroofingridge-*`
- `slantedroofingridgeend-*`
- `slantedroofingridgehalfleft-*`
- `slantedroofingridgehalfright-*`
- `slantedroofingtip-*`
- `slantedroofingtop-*`

Materials: Wood types + `bamboo`, `slate`, `copper`, `sod`, `thatch`, clay colors

---

### Devastation Panels (dpanel)
| Aged Block | Normal Blocks |
|------------|---------------|
| `dpanel-panel{1-6}-aged` | `dpanel-panel{1-6}-new` |
| `dpanel-conduitstraight1-aged` | `dpanel-conduitstraight1-new` |
| `dpanel-conduithorizontal1-aged` | `dpanel-conduithorizontal1-new` |
| `dpanel-conduitrightup1-aged` | `dpanel-conduitrightup1-new` |
| `dpanel-conduitleftup1-aged` | `dpanel-conduitleftup1-new` |
| `dpanel-conduitrightdown1-aged` | `dpanel-conduitrightdown1-new` |
| `dpanel-conduitleftdown1-aged` | `dpanel-conduitleftdown1-new` |
| `dpanel-port1-aged` | `dpanel-port1-new` |

---

### Jonas Lens Tower
| Aged Block | Normal Blocks |
|------------|---------------|
| `jonaslenstower-aged` | `jonaslenstower-normal` |

---

### Cluttered Bookshelf
| Aged Block | Normal Blocks |
|------------|---------------|
| `clutteredbookshelf-agedacacia` | No non-aged equivalent |

**Note:** Only aged acacia variant exists.

---

## Summary Statistics

- **Total Aged Block Codes:** 449
- **Block Types with Aged Variants:** ~35 categories
- **Wood Types:** 12 (used as replacements for most aged wood blocks)

## Blocks with NO Non-Aged Equivalent

These aged blocks have no normal counterpart:
- `carvedlog-acacia-aged-*` (carved logs only in aged)
- `agedwallpaperplanks-*` (wallpaper only on aged planks)
- `door-1x2heavy-aged`, `door-1x3heavy-aged`, `door-2x4heavy-aged` (heavy doors only aged)
- `clutteredbookshelf-agedacacia` (only aged variant)
