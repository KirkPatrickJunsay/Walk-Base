# Building Sprites — drop-in art pipeline

Walk Track draws every building procedurally by default, but the renderer will use a **PNG
sprite** instead whenever one is present. This lets you skin the whole town with a hand-drawn
art pack (e.g. a [Kenney](https://kenney.nl/assets?q=isometric) CC0 isometric set) **without
touching any code** — just drop files in and rebuild.

## How it works
- Sprites live in `Resources/Raw/Sprites/` (bundled as `MauiAsset`).
- `SpriteCache` loads `Sprites/<key>.png` on demand and caches it.
- `IsometricRenderer.DrawBuilding` draws the sprite if found, else falls back to the vector shape.
- **Per-building, per-level, incremental:** add `house_l1.png` and only Level-1 houses change;
  everything else keeps its vector look until you add its file.

## Sizing & anchoring
- Tiles are **128 × 64 px** (2:1 isometric diamond).
- A sprite is scaled to **128 px wide** (height kept proportional) and **bottom-anchored** to the tile.
- So: export each building on a **transparent background**, with its **base diamond ≈ 128 px wide**
  sitting at the **bottom-centre** of the image; let the building rise upward (taller image = taller building).
- Higher-resolution PNGs are fine (e.g. 256 px wide) — they're scaled down; keep the 2:1 base ratio.
- If a pack's art sits a few px too high/low, tweak the one anchor line in
  `IsometricRenderer.DrawBuilding` (`top.Y + IsoMath.TileHeight / 2f - h`).

## File names (drop any subset)
Levels: `_l1` = base, `_l2`/`_l3` = upgrades. A building with no `_l2`/`_l3` file reuses `_l1`.

| Building   | Files |
|------------|-------|
| Town Hall  | `town_hall_l1.png` `town_hall_l2.png` `town_hall_l3.png` |
| House      | `house_l1.png` `house_l2.png` `house_l3.png` |
| Tent       | `tent_l1.png` `tent_l2.png` `tent_l3.png` |
| Farm       | `farm_l1.png` `farm_l2.png` `farm_l3.png` |
| Bakery     | `bakery_l1.png` `bakery_l2.png` `bakery_l3.png` |
| Workshop   | `workshop_l1.png` `workshop_l2.png` `workshop_l3.png` |
| Watchtower | `watchtower_l1.png` `watchtower_l2.png` `watchtower_l3.png` |
| Market     | `market_l1.png` `market_l2.png` `market_l3.png` |
| Library    | `library_l1.png` `library_l2.png` `library_l3.png` |
| Well       | `well_l1.png` `well_l2.png` |
| Fountain   | `fountain_l1.png` `fountain_l2.png` |
| Garden     | `garden_l1.png` `garden_l2.png` |
| Path       | `path_l1.png` |
| Hedge      | `hedge_l1.png` |
| Flower Bed | `flower_bed_l1.png` |
| Lamppost   | `lamppost_l1.png` |

## Art license & attribution

The shipped building sprites are composited from the **Kenney "Sketch Town"** pack
(https://kenney.nl), released under **Creative Commons Zero (CC0 1.0)** —
https://creativecommons.org/publicdomain/zero/1.0/. CC0 places the work in the public domain:
free for commercial use, modification, and redistribution, with **no attribution required**.
Kenney's license note: *"This content is free to use in personal, educational and commercial
projects. Support us by crediting Kenney or www.kenney.nl (this is not mandatory)."*

We credit Kenney anyway, as good practice: in-app at **Settings → About → Credits**. If you swap
in a different art pack, check its license — not every pack is CC0 — and update that credit.

## Using a Kenney pack
1. Download a CC0 isometric pack (e.g. "Isometric Buildings", "Isometric City") from kenney.nl.
2. Pick the tile that best matches each building above, recolour/crop if you like.
3. Rename it to the matching key (e.g. a townhall tile → `town_hall_l1.png`).
4. Export as a transparent PNG with the base ≈128 px wide, bottom-centre anchored.
5. Drop them in `Resources/Raw/Sprites/` and rebuild — done.

The Build-tab/popup icons (`Resources/Images/bld_<id>.svg`) are separate; update those too if you
want the menu thumbnails to match the new art.

## Updating art safely after release (won't break installed saves)

Saves store only a building's **id** + **level** + grid position — never a sprite filename or any
art. Sprites are resolved at render time, and an app update fully replaces the bundled assets. So:

- ✅ **Re-skinning art is always safe.** Replace a PNG's *contents* under the **same filename**
  (e.g. a nicer `house_l1.png`). Every installed town picks up the new art on update — no migration,
  no risk. This is the recommended way to change building art.
- ✅ **Adding buildings/levels is safe.** New ids/files are simply unused by old saves.
- ✅ **A missing sprite never crashes.** If a referenced PNG isn't bundled, `SpriteCache` caches
  `null` and the renderer draws the procedural vector placeholder instead.
- ⚠️ **Renaming or removing a building *id* is the one risky change.** A player may already have it
  placed. The app is built to tolerate this — orphaned buildings render a placeholder, can still be
  sold (for 0), can't be upgraded (`PurchaseResult.UnknownBuilding`), and count toward variety — but
  it looks broken to the user. **Prefer keeping ids stable.** If you must retire a building, either
  leave a catalog stub for it, or add a one-time data migration that converts/removes the placed rows.
- 🔁 **Always clean-rebuild (`rm -rf obj bin`) when adding/removing files under `Resources/Raw/Sprites/`**
  — incremental builds can cache a deleted asset into the APK.

`ForwardCompatibilityTests` pins the "save outlives the catalog" guarantees so a refactor can't
silently reintroduce a crash on someone's installed town.
