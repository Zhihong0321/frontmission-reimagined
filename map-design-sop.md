# Map design SOP — MechaTrader "Keeper's Chart"

Standard operating procedure for any AI session (or person) continuing the map work.
Read this first. It covers: where things are, hard rules about working with the user,
the asset-generation workflow, how assets are used on the map, the map's design
principles, how to verify a change, and the backlog for the next version.

Last updated: 2026-09-02. `chart.html` is the live game map. The isometric ops console is archived.

---

## 0. Hard rules (read before anything else)

1. **Never give the user a terminal or PowerShell command.** Their `python` resolves to
   the Windows Store alias; nearly every pasted command failed and they said so in strong
   terms. Deliver double-click `.cmd` launchers or files, test them yourself, and say only
   "double-click X". The AI may use its own shell freely; the user must not.
2. **The real Python is** `C:\Users\Eternalgy\AppData\Local\Programs\Python\Python312\python.exe`
   (has Pillow 12). Launchers pin this path.
3. **The game's simulation core stays untouched** (`D:\FrontMission-RIMG\src\MechaTrader.Core`,
   pure, tested). The map is a view. Time in the game advances only through `WaitCommand`.
4. **The user rejected the old isometric ArtLab** (`web/artlab`, 2.5D). Do not reuse it.
   The old isometric *map* (`web/iso`, the old ops console) is archived at
   `D:\FrontMission-RIMG\web\archive\iso-ops-console\`. **Do not restore it. Do not merge
   it.** `chart.html` is the live game map, not a demo. There is no `demo-map.html`.
5. **The GPT-image-2 proxy is cheap but unstable.** Failures are normal: retry, never stop
   a batch on the first error. Concurrency limit is 8.
6. **Bash on this machine strips backslashes inside heredocs.** Write files with the
   editor/Write tool, not `cat <<EOF`.
7. The user wants to see, not read: after a change, look at the map yourself (screenshots)
   before reporting. Report only what you verified.

---

## 1. Where everything is

| Path | What |
|---|---|
| `D:\FrontMission-RIMG` | The game (MechaTrader). .NET 8 core sim + ASP.NET host + browser UI. Read its `CLAUDE.md` before touching it. Data in `data/` (cities, routes, terrain, map biomes, trucks). |
| `D:\FrontMission-MapLab` | **This project.** Live game map (Keeper's Chart) plus the sprite generator. `chart.html` is the player view; `game-bridge.js` talks to Core over `/api/*`. |
| `chart.html` | The game map: rendering, WASD, click-to-pathfind, HUD. Not a demo. |
| `game-bridge.js` | Core link. Depart / wait / state / map, plus `command()` and `newGame()` for the shell. Owns no rule. |
| `ops.js` / `ops.css` | The ops shell: nav rail + ERP-style workspace over the chart (overview, city, caravan, crew, ledger). Pages and tabs are registered at the bottom of `ops.js`; every figure comes resolved from `/api/state`. Not a map concern, but it shares `chart.html`, the top bar and the toast function. |
| `world.js` | Generated from the game's `data/` by `make-world.js` (cities, roads, terrain multipliers, biome polygons, truck). Regenerate when game data changes. |
| `generator/server.py` | Asset generator backend (port 5091): prompt assembly, GPT-image-2 calls, post-processing, library, manifest. Also serves this folder, so the chart is at `http://127.0.0.1:5091/chart.html`. |
| `generator/index.html` | Generator UI. |
| `generator/catalog.json` | The locked **style block** and the **catalog** (categories → types → subject lines, plus per-category placement settings). This is the single source of truth for what gets generated and how the map places it. |
| `generator/batch.py` | Headless batch runner (AI use): `batch.py tree rock --n 1 --bg chroma --quality low [--missing 1]`. |
| `art/gen/<category>/` | Every generated image: `<type>-<stamp>-<k>.png` (full, keyed), `.s256.png` (sprite), `.json` (prompt, params, usage, approval, footprint, weight). |
| `art/manifest.json` / `art/manifest.js` | APPROVED sprites + per-category placement rules. The map loads `manifest.js` on start. Rebuilt on every approval. |
| `art/tex-<biome>.png` | Approved ground textures (newest approved per biome wins). |
| `Generator.cmd` / `Map.cmd` | The user's launchers. |
| `README.md` | Short notes. `chart.html` is the live map. |
| `D:\FrontMission-RIMG\web\archive\iso-ops-console` | **DEAD.** Old isometric ops-console map. Do not open, fix, or merge. |
| `LORE.md` (in the game repo) | World canon: post-apocalyptic Europe, machine invasion ("the Host"), "the Hush", "the grey". Visual choices must fit it. |

Memory notes for AI sessions live in the Claude project memory folder; the relevant ones are
`mechatrader-project-locations`, `mechatrader-map-direction`, `mechatrader-asset-pipeline`,
`no-shell-commands-for-user`.

---

## 2. Running things

- **User:** double-click `Play.cmd` in the game repo (builds, serves, opens `/chart/`).
  For sprite work: double-click `Generator.cmd`, then `Map.cmd` if you want the chart
  through the generator so new sprites are served.
- **AI:** start the server hidden with the pinned Python path, then use the HTTP API:
  `GET /api/status`, `GET /api/catalog`, `POST /api/catalog`, `GET /api/library`,
  `POST /api/preview {category, subject}`, `POST /api/generate {...}`,
  `POST /api/update {category, id, approved?, footprintKm?, weight?, biomes?}`,
  `POST /api/delete {category, id}`, `POST /api/manifest {}`, `POST /api/reprocess {grade?}`.
- **The server does not reload, and it will not tell you it failed to restart.** Editing
  `server.py` changes nothing until the process is replaced, and `server.py` **exits with
  status 0 printing "already running"** when port 5091 is taken. So the obvious
  kill-and-relaunch can be a silent no-op against a listener you did not know about — the
  user's own `Generator.cmd` instance, most likely. This cost a whole grade pass once: the
  reprocess reported the old numbers and looked like an edit that did not apply.
  Find the real owner, kill that, then relaunch and **verify from the response, not from
  the fact that the port answers**:

  ```powershell
  Get-NetTCPConnection -LocalPort 5091 -State Listen | ForEach-Object {
    Get-CimInstance Win32_Process -Filter "ProcessId=$($_.OwningProcess)" |
      Select-Object ProcessId, CreationDate, CommandLine }
  ```

  `POST /api/reprocess {}` echoes the live `GRADE` back — if it is not the one you just
  wrote into the file, you are still talking to the old process.
- **Browser testing gotcha:** the in-app browser pane is usually hidden, which throttles
  `requestAnimationFrame` and timers. Do not judge animation or load time there. Drive the
  chart from script instead: set `cam.x/y/z`, call `frame(performance.now())`, step travel
  with `advanceAuto(0.1)` / `driveFree(0.1, dx, dy)`, then screenshot. Keyboard events do
  not reach the page in the hidden pane; click buttons or call functions.

---

## 3. Asset generation SOP

### 3.1 Principles
- **Top-down only** (map view). Never isometric, never perspective. One object, centered,
  no ground plane.
- **Style block first, subject second.** The style block in `catalog.json` is prepended to
  every prompt. Change it only to change the whole set; change only the SUBJECT line for a
  new asset. Consistency comes from the unchanged block, not from luck.
  It is at **styleVersion 2** (2026-09-02): cold, blue-grey, "no warm browns, no orange, no
  gold", sea-green for anything living. Everything generated under version 1 was warm and
  now matches only because `GRADE` cools it on the way out — so if a sprite ever looks off,
  check its `styleVersion` in the sidecar before blaming the key or the grade.
- **Chroma key by default.** Ask for a flat pure-green background and key it in post
  (`chroma_key` in `server.py`: greenness = G − max(R,B), soft threshold 40..120, despill).
  The API's `background: transparent` works but leaves colour fringes in fine gaps
  (branches). Use it only for solid shapes if chroma bleeds.
- **Never name transparency, checkerboards, white cards or PNG alpha in a prompt** — the
  model paints them. That is exactly how the old map kit failed.
- **Quality `low`, size 1024** is enough for a 256 px map sprite and is the cheap setting.
- **Elongated assets run left to right.** Anything long (drifts, orchard rows, wreck lines,
  vehicles) must be generated lying horizontally across the frame — say so in the subject
  line — and then marked `rotate: false`. The map supplies the angle: the road, or the wind.
  A long asset generated on a diagonal cannot be aimed and has to be thrown away.
- **Thin beats nothing, but only just.** If the silhouette is a scatter of fine lines it will
  vanish at map scale. Say "thick", "almost touching", "dense" and regenerate rather than
  keeping it and hoping.
- **Scale is set on the map, not in the image.** Every sprite fills ~80% of its frame; the
  catalog's `footprintKm` (per category, overridable per item) decides its size in km.

### 3.2 Adding a new asset type
1. Open the generator, pick the category (or add one in `catalog.json`: `label`, `biomes`,
   `footprintKm`, `spriteShare`, `stepKm`, `weights`, optional `kind: "texture"`,
   optional per-category `style` override).
2. Add the type with a subject line in this shape: *"a single <thing> seen from above,
   <two or three concrete visual details>"*. For combinable pieces say so ("elongated,
   combinable with other segments").
3. **Preview prompt** to read the assembled text once.
4. **Generate** 2–3 variants. Judge with the criteria in 3.3. Delete the rest.
5. **Approve** the keepers; set `km` (footprint) and `w` (pick weight) on the card.
6. Reload the map and look at the biome/rule that uses it at zoom 1.5, 2.5 and 4.

### 3.3 Judging a result (keep if all true)
- Silhouette reads at 20 px (the map's far zoom). Squint test.
- Edges clean after keying: no green halo, no eaten thin parts. If thin parts vanish,
  regenerate with "thicker branches" rather than loosening the key.
- Camera is straight down. Any visible side wall = reject.
- Tone sits with the chart: grey/rust/bone, low saturation. The post-processing grade
  (`GRADE` in `server.py`: colour 0.72, brightness 0.9, contrast 0.95) handles small
  drifts; a candy-coloured result is a prompt problem.
- No text, no frame, no ground plane, one object (clusters count as one silhouette).

### 3.4 Textures (kind `texture`)
Category `texture`, one type per biome. No key, no trim; the image is mirror-tiled into a
seamless 1024 tile. Approving writes `art/tex-<biome>.png`; the map multiplies it over that
biome at alpha 0.42, repeated every 256 km. Mirror tiling is symmetric by construction; if
symmetry ever shows, replace it with an offset/blend seamless method.

### 3.5 Batches (AI)
`batch.py <categories> --n 1 --bg chroma --quality low --missing 1` generates only types that
have no image yet; it re-runs failed types for three rounds. The server retries each call
six times. Approve in bulk through `/api/update` when the user has said "all approved";
otherwise approve in the UI with the user.

### 3.6 Naming and hygiene
- Type ids are kebab-case and describe the object, not the biome (`dead-oak`, not `forest-1`).
- Keep the sidecar JSON; it is the record of prompt, parameters and token usage.
- A contact sheet (Pillow, 6 columns, sprites on a grey ground with labels) is the fastest
  way to review a batch. Build one before approving.

---

## 4. Using assets on the map

### 4.1 Manifest schema (`art/manifest.js`, `window.MANIFEST`)
```
{ version, generated,
  categories: { <category>: { share, stepKm } },
  sprites: [ { id, category, type, file (s256), full, footprintKm, biomes[], rotate, weight } ] }
```
`biomes` come from the **current** catalog category, so retagging a category retags all its
sprites.

### 4.2 How the chart consumes it (functions in `chart.html`)
- `spritesReady`: loads every sprite; builds `SPRITES[biome]` pools, `SPRITE_RULE[biome]`
  (share = max, step = min when two categories share a biome), `RUIN_POOL`, `WRECK_POOL`,
  and `ART.units` keyed by sprite type. The convoy sprite is the one whose type matches the
  fleet's truck class (`WORLD.truck.id`, e.g. `mule`), falling back to the heaviest `unit`.
  So adding a `unit` sprite named after a class in `trucks.json` is all it takes for the
  convoy to change shape when the player drives that class.
- `buildGlyphs`: one jittered lattice pass per distinct `stepKm` (17 km default; forests 9).
  For a sprite biome, a lattice point becomes a sprite with probability `share`, otherwise a
  procedural mark (thinned by half). `pickSprite` is weight-proportional. Nothing is placed
  within 30 km of a city.
- `drawGlyph`: sprites are drawn at `footprintKm × (0.8 + 0.35·scale)`. Heading comes from
  `heading(sp, v, along)`: a sprite with `rotate: true` spins at random, one with
  `rotate: false` follows a heading — the road it was placed beside, or the prevailing wind
  `WIND` (−0.38 rad) out in the open, both with ±0.15 rad of jitter. That is why every
  elongated asset must be **generated running left to right**: the map supplies the angle.
  Below zoom 1.15 the baked 1 px/km layer (`inkBaked`) is used; above it, glyphs are drawn
  live from 150 km buckets culled to the viewport.
- `buildPois`: rule-based placements — dead settlements 22–40 km beside roads longer than
  220 km (70% of them), 2–3 ruin sprites inside every city ring, wrecks on road shoulders
  every ~70 km at 40% odds. `drawPois` draws them after roads, before cities.
- Textures: `finishBase` multiplies each `tex-<biome>` over its biome mask at bake time.

### 4.3 Adding a new category to the map
1. Catalog: category with `biomes` (if it should scatter by biome) and `spriteShare/stepKm`.
2. If it needs a **rule** instead of biome scatter (ruins, wrecks, cities, units), add a pool
   in `spritesReady` and a placement block in `buildPois` (or a dedicated draw call).
3. Decide the layer: under roads (ground features), over roads (wrecks, ruins), over cities
   (units). Draw order in `frame()`: base → ink/glyphs → cost layer → cells → claims →
   graticule → roads → route lines → POIs → trail/dust → cities/smoke → mist → convoy →
   labels → chrome.
4. Check the far zoom: a category that is invisible at the whole-chart zoom is fine; one
   that turns into noise there must be culled by zoom.

### 4.4 Performance rules
- Everything static is baked once (`base`, `inkBaked`, `costCanvas`); per frame the map
  draws two images plus vectors. Keep it that way.
- Live sprite drawing only above zoom 1.15 and only for buckets in view.
- The fps readout is bottom-left, red under 50. Check it at zoom 1.5 over a forest and over
  the Alps, which are the densest views.

---

## 5. Map design principles (what "good" means here)

**What failed before and must not return:** sprites with baked checkerboards; trees drawn at
house scale on a 50 km grid; no visual hierarchy; a map that is a backdrop for a side panel;
travel with no motion.

**Scale.** World units are km; base raster is 1 px = 1 km; the engine's cell is 50 km. A map
tree is a symbolic clump (~14 km), a ruin ~18 km, a ridge segment ~40 km, a truck ~8 km.
Roads never drop under ~3.6 px on screen. City rings are 18–21 km.

**Hierarchy, in order of what the eye must find:** the convoy → cities (amber beacon +
label plate) → roads (tarmac with light edges) → claims (hazard stripes) → terrain.

**Tone — a cold chart.** Post-apocalyptic industrial, not fantasy parchment, and since
2026-09-02 deliberately **dark blue-grey** rather than the warm ash it started as. The rule
is one line: *blue is the highest channel, red the lowest, and the ramp sits dark.* That
holds for the ground (`PAL`), the sea, the roads, the mist and the sprites alike. Amber is
the only warm thing on the map, which is what makes the convoy and the city beacons read.
Living vegetation is the one place green leads, and it is pulled toward sea-green so it
still belongs to the same picture. Monospace stencil labels, 50 km cell grid, drifting grey.
Fits `LORE.md`.

Tone lives in **two** places and they must move together:
- `PAL` / `WATER_FAR` / `foam` / road and mist colours in `chart.html` — the ground.
- `GRADE` in `server.py` — the sprites and the ground textures. `GRADE["tint"]` is a
  per-channel multiplier (`[0.88, 0.96, 1.14]` today) applied after desaturate/darken/
  contrast, and it is what drags a model's arbitrary hue onto the chart.
Change one without the other and warm sprites float on cold ground. After editing `GRADE`:
restart the server (see §2 — it does **not** reload), `POST /api/reprocess {}` to regrade
every sprite from its stored full, then `POST /api/manifest {}`, which is what rewrites the
`tex-<biome>.png` tiles graded. Stored fulls stay raw, so a grade is always reversible.

**Terrain treatment.** Biomes are authored as rectangles in the game's `map.json`; the chart
warps them (lattice-sampled noise, ±34 km) and erodes patch edges stochastically into plain
so no box shows. Coasts get a foam line and two ripple lines from a blurred land mask. Ports
get a 44 km land pad so cities never sit in water.

**Movement.** Real time with pause; one day = 2.4 s at 1×, with 2× and 4×. Time still
advances only via `WaitCommand`. **WASD** drives anywhere Core will walk (land, or
mountain/water on a road). **Click** a point: `game-bridge.js` sends `depart` with that
cell id; Core A* draws the amber route. Click again to reroute. Release WASD coasts;
it does not halt. Arrow keys pan.

**Real data vs illustrative.** Real (from the game): cities, roads and their terrain
multipliers, biome polygons, projection, truck speed/fuel/upkeep, off-road multipliers.
Illustrative (not game content yet): Host claims, mist, ruins/settlements, wrecks, pylons,
smoke. Keep the notes panel honest about this.

**Engine facts to respect when porting.** `MapView` gives cities, roads, biome string, road
mask; `TravelView` gives the cell path and convoy coordinates; A* runs over 8-connected cells
with per-biome off-road costs; `Depart` accepts a city, a claim or any cell; only `Wait`
advances time (so the client animates between days and sends one `Wait` per day).

---

## 6. Verification SOP (before saying "done")

1. Syntax: extract the inline script and `node --check` it; `py_compile` for `server.py`;
   JSON-parse the catalog.
2. Load: `window.__loadMs` after boot, target under 6 s **on the user's machine**. At 91
   sprites the hidden pane returned 5.6, 7.9 and 9.1 s for the same page, so it is as
   untrustworthy as the fps readout — measure it in a real window. What *is* measurable
   anywhere: refetching all 91 sprites takes ~1 s, so the terrain bake is nearly all of the
   load and asset count is not what will blow the budget.
   Watch one thing as the library grows: `spritesReady` races an **8 s timeout**, and a
   throttled tab has already tripped it (`window.__sprites === -1`, glyphs built from a
   half-filled pool). It degrades quietly rather than failing, so check that value, not the
   picture, when a load looks thin.
3. Standard views, screenshot each at 1920×1080:
   - forest east of Praha, lon 16.6 / lat 49.5, zoom 2.2
   - Alps, lon 10.2 / lat 46.6, zoom 1.6
   - Iberian hills, lon −4.5 / lat 40.4, zoom 2.4
   - plains Paris–Bruxelles, lon 3.4 / lat 49.9, zoom 2.6
   - Praha close, on the convoy, zoom 4
   - whole chart (`fitAll`)
4. Travel: plan Praha→Wien (2 days, ~318 km), depart, step 2 days, arrival toast; reroute
   mid-road to Berlin (turns around, passes Praha).
5. Console: zero `Uncaught`/`TypeError`. The `art/*.png` 404s for missing optional files are
   expected.
6. fps readout ≥ 50 in the forest and Alps views (only meaningful in a visible browser —
   timing `frame()` in the hidden pane gave 23 and 48 fps for the same code minutes apart,
   so do not trust a number from there). The load-bearing figure is **sprite blits per
   frame**, which is honest anywhere: ~1560 in the forest at zoom 2.2, ~1740 in the Alps. It depends on the
   lattice and `share`, not on how many types the library holds, so adding asset variety
   costs nothing per frame. Raising a category's `share` or lowering its `stepKm` does.

---

## 7. Next version backlog (user's asks, with plans)

### 7.1 Deeper zoom (current cap 10 px/km; target 10–12)
Problem: the base raster is 1 px/km, so at 10× it is a blur, and 256 px sprites at a 14 km
footprint are 140 px — still fine — but ground and coast go soft.
**Done 2026-09-03:**
1. **Detail tiles** — `chart-tiles-worker.js` + the §4b block in `chart.html`. 256 km tiles
   at 4 px/km (1024² px) painted in a Web Worker on demand when `cam.z > 3`, reusing the
   same `biomeAt`/lattice painter plus texture multiply and coast foam, cached in an LRU of
   24 `ImageBitmap`s. The `fine`, `edgeDist`, lattice arrays and PERM are transferred once;
   biome textures reach the worker as they load. Tiles draw over `base` when available, so
   the ground stays sharp at 10×. Deep-link for driving the camera: `?view=lon,lat,zoom`
   (e.g. `/chart/?view=10.2,46.6,6`) jumps the boot camera there, no follow. Pre-warm
   requests queue until the worker is ready, so a deep-linked view loads its tiles during the
   base bake. Verified end-to-end (worker paint ~150–200 ms/tile; screenshot A/B at z8 shows
   the tiled ground, avg pixel diff ≈ 68 vs the soft base).
5. **Wheel-zoom cap raised 4.5 → 10 px/km** (labels stay clamped).
Remaining:
2. **Sprites at 512.** Save an `.s512.png` alongside `.s256.png` (one line in `make_sprite`)
   and pick by zoom.
3. **Roads at high zoom:** add lane texture (cracks, patches) as a repeating pattern along
   the path, potholes from the `damage` list, and shoulder debris; pylons already appear.
4. **Cities at high zoom:** replace the ring + blocks with a generated **city tile set**
   (category `city`: `ring-road`, `district-ruin`, `depot`, `market`, `gate`), composed by
   population, so a city reads as a place when you drive into it.

### 7.2 More asset variety — **done 2026-09-02**, library is 55 → 111 approved sprites

**Second pass, same day** (the user asked for more trees, more green, a colder look, and two
structures). Twenty more sprites and two new categories:
- `tree` gained four **living** types — `living-pine-stand`, `leafy-broadleaf`,
  `green-thicket`, `mixed-copse` — weighted 4–6 against the dead types' 1–3, so a forest
  reads as woods rather than as a cemetery.
- **`copse`** is a new category for vegetation *outside* the forest rectangle
  (`roadside-copse`, `lone-tree`, `reed-bed`, `hedgerow`) on hill / plain / swamp. Its
  `spriteShare` matches what those biomes already spent, so it buys variety, not density.
- **`infra`** is a new category for standing structures (`solar-farm`, `radio-tower`) on
  plain / desert / tundra. Both read well from above; the mast's guy wires are what make it
  legible, so keep that in any similar subject line.
- `GLYPH_DENSITY` rose for forest (0.9 → 1.0), hill (0.55 → 0.62), swamp (0.6 → 0.65) and
  plain (0.10 → 0.13). Forest blits went 1430 → 1560 per frame; that is the whole cost.
- Note on category biomes: a sprite's biomes come from its **category**, so `reed-bed`
  currently also lands on dry plains, where it reads as rough grass. If that ever grates,
  split it into a `wetland` category rather than trying to tag one sprite.

**First pass:** all eighteen types on the old list landed: trees `burnt-stump-field`, `dead-orchard-rows`;
rocks `boulder-field`; mountain `ridge-corner`, `pass-notch`; ground `ash-drift-long`,
`oil-spill`, `bomb-field`; ruins `village-crossroads`, `rail-yard`, `refinery-ruin`,
`bridge-stub`; wrecks `convoy-wreck-line`, `machine-nest`; units `mule`, `ox`, `kite`,
`digger` (one per class in `trucks.json`). `rock/slab` and `mountain/glacier-tongue`, both
rejected in the first pass, were regenerated off rewritten subject lines and kept.
The heading rule is in (`heading()` in `chart.html`, §4.2).

What the pass taught, for the next one:
- 40 of 46 images were keepers. The six rejects were all one of three faults: **chroma spill**
  (green surviving in fine streaks), **off-palette colour** (a construction-yellow digger, a
  green pasture under a ruined village), or **too thin to read** (the first orchard rows).
  Grade fixes none of these — they are prompt problems, so regenerate.
- The second pass kept 20 of 20 — the difference was that the style block had already been
  cooled and the subject lines said "dense", "thick", "touching". Fewer, better-specified
  words beat more variants.
- What is still thin: only one `ash-drift-long` survived, and `infra/solar-farm` has one
  variant (the other call failed). Generate a second of each.
- Still worth adding: ruins `bridge-intact` (so a road can cross water), `dam`; ground
  `railbed` (elongated, for a rail line that is not a road); wrecks `train-wreck`; mountain
  `caldera`; infra `wind-turbines`, `substation`. Cities remain the weakest thing on the
  chart — see 7.1 item 4.
- Variants: every new type here got 2. Three (`--n 3`) and keeping the best 2 would raise
  the average; the marginal image is cheap.

### 7.3 This is already the game map

`chart.html` + `game-bridge.js` is the player view, served at `/chart/` by MechaTrader.Host.
Do not port it into `web/` or resurrect `web/archive/iso-ops-console/`. City, market, crew
and caravan screens are the ops shell (`ops.js`) docked over this chart, not a second map.
The rail takes the left 60 px below the top bar; `#controls`, `#notes` and `#btn-notes`
are offset to clear it.

Godot later: the renderer is data → drawing with a small style table; port the tables,
not the canvas code.

---

## 8. Definition of done for any map change
- The six standard views look right and are screenshotted.
- No new console errors; load time not worse than before.
- Travel and WASD still work (section 6, steps 4).
- Illustrative additions are declared in the notes panel and in this SOP.
- `README.md` updated if the user's launchers or files changed.
- The user is told only "double-click X" and what to look at.
