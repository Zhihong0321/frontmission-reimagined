# Keeper's Chart — the Mecha Trader map

This is the **live game map**, not a demo. Play.cmd serves it at `/chart/`.

File: `chart.html`. It talks to MechaTrader.Core through `game-bridge.js` (`/api/state`,
`/api/map`, `/api/command`). WASD drives; click pathfinds. Mountains and water block
unless you are on a road.

**The ops shell** (`ops.js` + `ops.css`) is every non-map screen: a nav rail on the left
and an ERP-style workspace docked over the chart with pages for Overview, City (market,
governor, stats, roads, recruitment, depot, storeroom, wire), Caravan, Crew (roster and
character sheets) and Ledger. `Tab` opens it, `Esc` closes it. It owns no rule: it renders
the `/api/state` snapshot and posts commands through `MECHA.command`. A new screen is a
`registerPage` / `registerTab` call at the bottom of `ops.js`; bump `?v=N` in `chart.html`
after editing either file.

The old isometric ops console (`web/iso`, the old `web/index.html`) is archived at
`D:\FrontMission-RIMG\web\archive\iso-ops-console\`. Do not use it.

`world.js` is generated from the game's `data/` folder:

```
node make-world.js            # reads D:\FrontMission-RIMG\data, writes world.js
```

## Controls

| Input | Effect |
|---|---|
| W A S D | drive the convoy. Off-road speed follows the biome; mountains and water block unless you are on a road. |
| click | pathfind to that point (Core A*). Click again to reroute. |
| arrows / drag | pan the camera · wheel zooms · F follows the convoy · H shows the whole chart |
| space | pause · 1 / 2 / 3 set pace (1×, 2×, 4×) |
| L / G / C | travel-cost layer · graticule · Host claims |
| Tab / Esc | open / close the ops shell. While it is open the keyboard belongs to it, not to the chart. |

## For a new AI session

Read `map-design-sop.md` first. Then `D:\FrontMission-RIMG\CLAUDE.md`. The chart is the
player view; Core still owns every rule.

## Asset generator (`generator/`)

A local app around GPT-image-2 (key `GPT-IMAGE-2_KEY` in the vault, read at request time).

**Double-click `Generator.cmd`.** It picks the Python that has Pillow (installing Pillow if
needed), starts the server and opens http://127.0.0.1:5091 in your browser. Keep that window
open while you work; close it to stop. **Double-click `Map.cmd`** to open the chart: through
the generator when it is running (so the sprites are served), or through the game host.

- **Style block** (top of the left panel) is prepended to every prompt: view, medium, world,
  palette, light, isolation. Change it only when you want the whole set to change.
- **Catalog** (`generator/catalog.json`): categories → types → one subject line each, plus the
  biomes the category decorates and its footprint in km on the map. Editable in the UI.
- **Background**: `chroma` asks for flat pure green and keys it out in post (clean edges,
  default); `transparent` uses the API's own alpha (fringes in fine gaps).
- **Post-processing** (`server.py`): key/despill or matte clean-up, trim to content, square
  canvas, 256 px sprite. Output: `art/gen/<category>/<type>-<stamp>-<k>.png` (full),
  `.s256.png` (sprite), `.json` (prompt, params, usage, approval).
- **Approve** a card in the gallery to include it; `art/manifest.js` is rebuilt on every
  approval and `chart.html` loads it on start. A biome that has approved sprites uses
  them instead of the procedural glyphs; footprint km controls their size on the map.
- **How the map places each category** (per-category `spriteShare`, `stepKm`, `weights` in
  the catalog): trees fill forests on a 9 km lattice; rocks take a quarter of the hill
  marks; mountain parts cover the mountains with ridge segments weighted highest; ground
  features take a third of the marks in plain, desert, tundra and swamp; wrecks scatter
  thinly on plains and sit on road shoulders; ruins form dead settlements beside long
  roads and fill every city ring; the first approved unit becomes the convoy sprite.
- **Textures** (category kind `texture`): no chroma key; the image is mirror-tiled into a
  seamless 1024 tile. Approving one writes `art/tex-<biome>.png`, which the map multiplies
  over that biome. Newest approved per biome wins.

## Optional image assets (`art/`)

The renderer is procedural. If a PNG exists at one of these paths it is used instead, and
a missing file is silently ignored, so you can try assets one at a time.

| File | Spec | What it does |
|---|---|---|
| `art/tex-plain.png` `art/tex-hill.png` `art/tex-forest.png` `art/tex-swamp.png` `art/tex-desert.png` `art/tex-tundra.png` `art/tex-mountain.png` `art/tex-water.png` | **seamless tileable** texture, 512×512, top-down, neutral mid-grey tones, no objects, no shadows with a direction | multiplied over that biome's ground at 60 % |
| `art/truck.png` | top-down truck facing **right**, solid black background is fine (drawn small, background hidden by the glow), about 96×48 | replaces the procedural convoy sprite |
