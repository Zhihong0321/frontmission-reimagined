# Phase A Inventory and Path Audit Report: `PA-AGY-01`

**Control Metadata**
- **Task ID**: `PA-AGY-01`
- **Worker**: `AGY`
- **Model**: `gemini-3.8-flash-high` (High effort)
- **Runtime**: AGY CLI 1.1.25
- **Assigned Branch**: `codex/pa-agy-01-inventory`
- **Product Green Base Commit**: `7f8897c15f5ab3b17dbe522e0e474af046a766e9` (verified direct ancestor)
- **Branch Assignment Commit**: `1f881202450bdae9b8823b7361086137dc33dfb3`
- **Assigned Worktree**: `D:\FrontMission-RIMG-worktrees\PA-AGY-01`
- **Canonical Ledger**: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- **Canonical Plan**: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`
- **Execution Mode**: Evidence report only — no delete, move, copy, regenerate, format, or edit of product/sibling files.

---

## 1. Snapshot Identity

### 1.1 Repository Identifiers & Status
| Attribute | Primary Worktree (`D:\FrontMission-RIMG-worktrees\PA-AGY-01`) | Sibling Repository (`D:\FrontMission-MapLab`) |
|---|---|---|
| **Branch** | `codex/pa-agy-01-inventory` | `backup/maplab-final-20260903` |
| **HEAD Commit** | `1f881202450bdae9b8823b7361086137dc33dfb3` | `df3c1baa8a83c2412607353af9994170b988dbe3` |
| **Product Green Base** | `7f8897c15f5ab3b17dbe522e0e474af046a766e9` (verified direct ancestor) | `df3c1baa8a83c2412607353af9994170b988dbe3` (recovery snapshot baseline) |
| **Remote Tracking** | Local worktree branch | `origin/backup/maplab-final-20260903` (up to date) |
| **Before Audit Status** | Clean product tree; untracked allowed dir `coordination/runs/PA-AGY-01/agy.log` | Clean (`git status --short --branch` empty) |
| **Remote Recovery Tag** | `backup-rimg-20260903` (`29de90387bb2d8fcccf5d6b787def5edac2ca923`) | `backup-maplab-20260903` (`df3c1baa8a83c2412607353af9994170b988dbe3`) |

### 1.2 Tool Versions & Audit Timestamp
- **Audit Timestamp**: `2026-09-03T23:51:00+08:00`
- **Git**: `2.55.0.windows.4`
- **.NET SDK**: `8.0.424`
- **Node.js**: `v24.19.0`
- **Python**: `3.12.10`
- **Ripgrep (`rg`)**: `15.0.0 (rev 3a612f88b8)` with PCRE2 10.45
- **PowerShell**: `7.6.4` (Core) / Windows PowerShell 5.1 available
- **AGY CLI**: `1.1.25`

---

## 2. Top-Level Classification

### 2.1 Repository Sizing Overview
- **`FrontMission-RIMG` (Worktree)**:
  - Tracked Files: **185** files | **60,828,665** bytes (~**58.01 MB**)
  - Untracked Allowed: **1** file (`coordination/runs/PA-AGY-01/agy.log`, 0 bytes)
  - Ignored / Caches (in primary repo `D:\FrontMission-RIMG`): **448** items | **43,518,418** bytes (~**41.50 MB**)
- **`FrontMission-MapLab`**:
  - Tracked Files: **413** files | **293,670,751** bytes (~**280.06 MB**)
  - Untracked: **0** files
  - Ignored / Caches: **3** items | **29,867** bytes (~**0.03 MB**)

### 2.2 Category Breakdown: `FrontMission-RIMG`
| Category | File Count | Aggregate Bytes | Size (MB) | % of Tracked | Description / Scope |
|---|---|---|---|---|---|
| **Generated Output** | 40 | 47,514,134 | 45.31 MB | 78.11% | 39 test/canvas probe PNGs in `tools/` (45.31 MB) + `FIGURES.md` (5,077 B) |
| **Archives & Backups** | 23 | 11,174,680 | 10.66 MB | 18.37% | `web/archive/iso-ops-console/` (dead isometric UI: 22 files + `kit.json`, 10.66 MB) + `web/archive/README.md` |
| **Source Code (C# & Web)** | 70 | 693,086 | 0.66 MB | 1.14% | Core simulation (`src/MechaTrader.Core/**`), Host (`src/MechaTrader.Host/**`), Content loader, Tests, BalanceSim, `web/index.html` |
| **Documentation & Coordination** | 24 | 314,566 | 0.30 MB | 0.52% | Root Markdown docs, `MIGRATION_PLAN.md`, `MIGRATION_LEDGER.md`, `coordination/**`, `VERSION` |
| **Content / Data** | 15 | 66,991 | 0.06 MB | 0.11% | `data/*.json` (15 required shipping world content files) |
| **Ambiguous (Dormant ArtLab)** | 5 | 30,975 | 0.03 MB | 0.05% | `web/artlab/` (app.js, index.html, prompt.js, style.css) + `ArtLab.cmd` (dormant tool, candidate for removal) |
| **Source Launchers** | 5 | 21,003 | 0.02 MB | 0.03% | `play.ps1`, `check.ps1`, `install-launcher.ps1`, `Play.cmd`, `Install-Launcher.cmd` |
| **Assets** | 1 | 18,405 | 0.02 MB | 0.03% | `web/favicon.ico` |
| **Config / Ignore** | 2 | 370 | <0.01 MB | <0.01% | `.gitignore` (103 B), `web/archive/.../kit.json` (categorized above) |
| **Total Tracked** | **185** | **60,828,665** | **58.01 MB** | **100.0%** | |

#### Build / Runtime Caches (Ignored, Aggregate)
- **.NET Build Outputs (`bin/`, `obj/`)**: Present in `D:\FrontMission-RIMG` (417 files, 21,033,394 bytes / ~20.06 MB). Completely clean/absent in `PA-AGY-01` worktree.
- **Dormant ArtLab Output (`web/artlab/out/`)**: Present in `D:\FrontMission-RIMG` (30 files, 22,485,024 bytes / ~21.44 MB). Ignored by `.gitignore`; absent in clean worktree.
- **Local Secrets (`.artlab-secret`)**: Present in `D:\FrontMission-RIMG` (ignored, 0 or secret length bytes); absent in worktree.

---

### 2.3 Category Breakdown: `FrontMission-MapLab`
| Category | File Count | Aggregate Bytes | Size (MB) | % of Tracked | Description / Scope |
|---|---|---|---|---|---|
| **Assets (`art/gen/*`)** | 254 | 267,848,332 | 255.44 MB | 91.21% | 127 full PNGs (210.0 MB) + 127 256px sprites `.s256.png` (57.8 MB) under `art/gen/` |
| **Generated Output** | 11 | 15,801,591 | 15.07 MB | 5.38% | 8 compiled biome textures `art/tex-*.png` (15.75 MB), `world.js` (4,561 B), `art/manifest.json` (42,242 B), `art/manifest.js` (4,767 B) |
| **Archives & Dead Output Candidates** | 4 | 9,670,761 | 9.22 MB | 3.29% | 4 sprite generation contact sheets (`art/contact-all.png`, `contact-sheet.png`, etc.) unreferenced by any runtime code |
| **Source Code (JS, CSS, Py, HTML)** | 9 | 278,115 | 0.27 MB | 0.09% | `chart.html`, `game-bridge.js`, `ops.js`, `ops.css`, `chart-tiles-worker.js`, `make-world.js`, `generator/batch.py`, `generator/server.py`, `generator/index.html` |
| **Ambiguous (Sidecars & Testbench)** | 128 | 219,498 | 0.21 MB | 0.07% | 127 `.json` sidecars in `art/gen/` (generator metadata / approval records) + `opstest.html` (1,473 B testbench) |
| **Documentation** | 2 | 31,594 | 0.03 MB | 0.01% | `README.md` (10,958 B), `map-design-sop.md` (20,636 B) |
| **Source Data** | 1 | 15,124 | 0.01 MB | <0.01% | `generator/catalog.json` (prompts, styles, weights, biomes) |
| **Source Launchers** | 2 | 1,456 | <0.01 MB | <0.01% | `Generator.cmd` (976 B), `Map.cmd` (480 B) |
| **Config** | 2 | 4,274 | <0.01 MB | <0.01% | `.gitignore` (55 B) + duplicate entry |
| **Total Tracked** | **413** | **293,670,751** | **280.06 MB** | **100.0%** | |

#### Runtime Caches & Logs (Ignored, Aggregate)
- **Python Cache (`generator/__pycache__/`)**: 1 file (`server.cpython-312.pyc`), 29,867 bytes.
- **Server Logs (`generator/server.log`, `generator/server.err`)**: 2 files, 0 bytes.

---

## 3. Generated-Output Map

| Generator Name | Executable / Script Location | Input Files / State | Output File(s) | Tracked / Ignored / Present | Consumers | Stale-Output Risk & Failure Modes | Exact Code Reference |
|---|---|---|---|---|---|---|---|
| **BalanceSim Figures Generator** | `tools/MechaTrader.BalanceSim/Program.cs` (via `dotnet run`) | `data/*.json` via `ContentLoader.LoadWorld()`, `FigureSeed = 20260901UL` | `FIGURES.md` at repository root | **Tracked**, Present (5,077 bytes) | `check.ps1` (Gate 3), human documentation, agent onboarding | **High**: Running `check.ps1` automatically regenerates this file. If balance rules or content change, it modifies `FIGURES.md`, leaving the git working tree dirty. | `Program.cs:579-580`, `Program.cs:731`, `check.ps1:48-56` |
| **World Data Bundle Generator** | `D:\FrontMission-MapLab\make-world.js` (via Node.js) | `data/cities.json`, `routes.json`, `terrain.json`, `map.json`, `trucks.json`, `config.json` | `D:\FrontMission-MapLab\world.js` | **Tracked**, Present (4,561 bytes) | `chart.html:189` (`<script src="world.js">`), `window.WORLD` | **Critical**: If `data/*.json` is updated but `world.js` is not regenerated, the frontend renders stale cities/routes/biomes. `play.ps1` attempts to regenerate it, but silently continues on failure (`play.ps1:84`). If absent or corrupted, canvas throws `ReferenceError: WORLD is not defined` and fails completely. | `make-world.js:3-31`, `play.ps1:63-86`, `chart.html:189` |
| **MapLab Texture Compiler** | `generator/server.py::rebuild_manifest` | Latest approved items in `art/gen/texture/*.png` | `art/tex-<biome>.png` (8 files: plain, hill, forest, swamp, desert, tundra, mountain, water) | **Tracked**, Present (15,754,582 bytes) | `chart.html:327-328` (`loadArt('tex-' + k)`), `chart-tiles-worker.js` | **Medium**: When approved textures in catalog change, old files are unlinked and new ones compiled. If not re-run, tiles lag behind generation. | `server.py:219-235`, `chart.html:327-328` |
| **MapLab Manifest Builder** | `generator/server.py::rebuild_manifest` | `art/gen/*/*.json` sidecars where `approved == true`, `generator/catalog.json` | `art/manifest.json`, `art/manifest.js` | **Tracked**, Present (`manifest.json` 42,242 B, `manifest.js` 4,767 B) | `chart.html:190` (`<script src="art/manifest.js">`), `window.MANIFEST` | **High**: If sprites are generated/approved but manifest is not regenerated, `chart.html` does not load them. If manifest 404s, `window.MANIFEST` is undefined and `spritesReady` silently returns 0 (`chart.html:336`). | `server.py:240-256`, `chart.html:190`, `chart.html:335-364` |
| **MapLab Sprite Generator** | `generator/server.py` & `generator/batch.py` | `generator/catalog.json`, `D:/Tools/my-vault/vault.json` (`GPT-IMAGE-2_KEY`), OpenAI image API | `art/gen/<category>/<stem>.png`, `.s256.png`, `.json` | **Tracked**, Present (381 files, 267.85 MB) | `rebuild_manifest`, `manifest.js`, `chart.html` | **Low runtime / High provenance**: Requires external network and external credentials. Generated outputs are committed to git. | `server.py:73-103`, `server.py:259-288`, `batch.py:30-40` |
| **Dormant ArtLab Image Generator** | `src/MechaTrader.Host/Program.cs` (`/api/artlab/generate`) | `.artlab-secret` at repo root, HTTP prompt request, `https://asiasouth.up.railway.app/v1/` proxy | `web/artlab/out/<timestamp>-<slug>.png`, `.txt` | **Ignored**, Present in main repo (30 files, 22.48 MB); absent in worktree | `web/artlab/app.js` (`/api/artlab/library`) | **Dormant / Deprecated**: Documented as "rejected isometric sprite lab. Do not reuse." Output is gitignored. Creates dirty state in main repo. | `Program.cs:86-166`, `Program.cs:226`, `.gitignore:9` |
| **Browser Probe Screenshot Captures** | Manual / ad-hoc probe scripts during development | Browser canvas at various zoom levels (`map.toDataURL` / headless screencaps) | `tools/*.png` (39 files: `chart-base2.png`, `probe1..11.png`, etc.) | **Tracked**, Present (47,509,057 bytes / 45.31 MB) | None (0 references in code or docs) | **Dead / Stale**: None of these 39 images are read by any build, test, launcher, or web host. They represent 78.1% of the RIMG repository size. | `tools/*.png` (filesystem only) |

---

## 4. Runtime Path-Discovery Map

Every upward walk, sibling lookup, absolute path, working-directory assumption, and environment-derived path identified across launchers, host, tests, and generators is cataloged below.

| Category | Source File & Line | Resolved Current Path | Fallback Behavior | Phase A / B Relevance & Risk |
|---|---|---|---|---|
| **Sibling Lookup** | `src/MechaTrader.Host/Program.cs:205-216` (`LocateMapLab`) | `D:\FrontMission-MapLab` (via walk to `D:\` parent) | Returns `null` if not found; `/chart` route is omitted | **Critical Phase A/B False Positive**: The host silently serves the sibling folder. If `web/chart/` is added in Phase B, the host will still serve `D:\FrontMission-MapLab` unless `LocateMapLab` is completely removed. |
| **Sibling Lookup** | `play.ps1:63-86` (`Update-ChartData`) | `D:\FrontMission-MapLab\make-world.js` | Logs `"FrontMission-MapLab not found - chart data left as-is"` and continues | **Critical Phase A/B False Positive**: Silently falls back to sibling generator. In Phase B, must be replaced with mandatory in-repo generator; failure must be fatal. |
| **Sibling Lookup / Absolute** | `D:\FrontMission-MapLab\make-world.js:5` | `D:/FrontMission-RIMG/data` (via `process.argv[2] \|\| 'D:/FrontMission-RIMG/data'`) | Uses hardcoded default `'D:/FrontMission-RIMG/data'` if no argv[2] | **High Phase B Risk**: If MapLab is moved or run in a clean clone, it still attempts to read `D:/FrontMission-RIMG/data` unless overridden by argv. |
| **Upward Walk** | `src/MechaTrader.Content/ContentLoader.cs:21-37` (`FindDataDirectory`) | `D:\FrontMission-RIMG-worktrees\PA-AGY-01\data` | Walks `dir.Parent` looking for `data/config.json`; throws `DirectoryNotFoundException` if missing | Safe in-repo walk, but in an external clone under `D:\`, if root has a `data/` folder it could match. |
| **Upward Walk** | `src/MechaTrader.Host/Program.cs:190-203` (`LocateWebRoot`) | `D:\FrontMission-RIMG-worktrees\PA-AGY-01\web` | Walks `dir.Parent` looking for `web/index.html`; throws `DirectoryNotFoundException` | Safe in-repo walk for static web files. |
| **Upward Walk** | `src/MechaTrader.Host/Program.cs:218-232` (`ReadArtlabKey`) | `D:\FrontMission-RIMG\.artlab-secret` (or null in worktree) | Checks `ARTLAB_API_KEY` env var; then walks upward for `.artlab-secret`; returns `null` | Dormant secret discovery walk; scheduled for removal in Phase F. |
| **Upward Walk** | `src/MechaTrader.Content/BuildInfo.cs:190-201` (`FindRepositoryRoot`) | `D:\FrontMission-RIMG-worktrees\PA-AGY-01` | Walks `dir.Parent` looking for `MechaTrader.sln`; returns `null` if missing | Used by `/api/build` to find `VERSION` and run `git log`. |
| **Upward Walk** | `tests/MechaTrader.Core.Tests/TestWorld.cs:17-30` (`RepositoryRoot`) | `D:\FrontMission-RIMG-worktrees\PA-AGY-01` | Walks `dir.Parent` looking for `MechaTrader.sln`; throws `DirectoryNotFoundException` | Used by `ArchitectureTests.cs`, `BuildInfoTests.cs`, `CrewBriefTests.cs`. |
| **Upward Walk** | `tools/MechaTrader.BalanceSim/Program.cs:738-739` (`RepositoryRoot`) | `D:\FrontMission-RIMG-worktrees\PA-AGY-01` | `Directory.GetParent(ContentLoader.FindDataDirectory())!.FullName` | Assumes repo root is the immediate parent of `data/`. Writes `FIGURES.md`. |
| **Upward Walk** | `D:\FrontMission-MapLab\generator\server.py:35-38` | `ROOT = Path(__file__).resolve().parent.parent` (`D:\FrontMission-MapLab`) | Assumes fixed relative folder structure `generator/../art/gen` | MapLab-internal; will need path updates if generator is relocated to `tools/` in Phase B. |
| **Absolute Path (External)** | `D:\FrontMission-MapLab\generator\server.py:40` | `D:/Tools/my-vault/vault.json` | None; raises `FileNotFoundError` if missing when generating images | **Secrets / Environment Leak**: Hardcoded path to private vault on local machine. Breaks on any other machine or CI. |
| **Absolute Path (User-Specific)** | `D:\FrontMission-MapLab\Generator.cmd:7` | `C:\Users\Eternalgy\AppData\Local\Programs\Python\Python312\python.exe` | Falls back to `where python` in PATH if missing | User-specific path hardcoded in launcher. Must be generalized in Phase B. |
| **Absolute Path (Proxy Endpoint)** | `src/MechaTrader.Host/Program.cs:25` | `https://asiasouth.up.railway.app/v1/` | None; configured base address for `artlab` HttpClient | Hardcoded external proxy URL for dormant ArtLab. |
| **Working Directory Assumption** | `play.ps1:12` | `Set-Location $PSScriptRoot` (`D:\FrontMission-RIMG-worktrees\PA-AGY-01`) | Script fails if run in restricted execution policy without bypass | Ensures relative paths in launcher resolve against script root. |
| **Working Directory Assumption** | `check.ps1:16` | `Set-Location $PSScriptRoot` (`D:\FrontMission-RIMG-worktrees\PA-AGY-01`) | Script fails if run in restricted execution policy without bypass | Ensures build/test commands execute from repository root. |
| **Working Directory Assumption** | `install-launcher.ps1:23` | `$root = $PSScriptRoot` | Throws if target `.cmd` files not beside script | Pinned to script directory. |
| **Working Directory Assumption** | `Play.cmd:3`, `ArtLab.cmd:3`, `Install-Launcher.cmd:4`, `Generator.cmd:5`, `Map.cmd:5` | `cd /d "%~dp0"` | Changes working directory to script location | Standard Windows batch idiom. |
| **Environment Variable** | `check.ps1:71-72` | `$env:TEMP\mt-host.log`, `$env:TEMP\mt-host.err` | Standard system temp directory | Used to capture background host server logs. |
| **Environment Variable** | `install-launcher.ps1:16` | `[Environment]::GetFolderPath('Desktop')` | Defaults to user Desktop | Places desktop shortcuts. |
| **Environment Variable** | `Program.cs:220` | `ARTLAB_API_KEY` | Falls back to `.artlab-secret` file if unset | Overrides file-based API key for ArtLab. |

---

## 5. Asset Reconciliation

### 5.1 Manifest vs. Disk Audit (`FrontMission-MapLab`)
- **Manifest Path**: `D:\FrontMission-MapLab\art\manifest.json` and `art\manifest.js`
- **Manifest Version**: `2`
- **Manifest Generation Timestamp**: `2026-09-02T14:11:12`
- **Declared Sprite Count**: **111** sprites across 6 categories (`unit`, `tree`, `copse`, `rock`, `mountain`, `ruin`, `wreck`).
- **Disk Existence of Declared Sprites**:
  - `s.file` (`art/gen/<category>/<stem>.s256.png`): **111 / 111 present on disk** (**0 missing**).
  - `s.full` (`art/gen/<category>/<stem>.png`): **111 / 111 present on disk** (**0 missing**).
  - Missing manifest-declared files: **0**.

### 5.2 HTML / JS / CSS Asset References
| Source Reference | Code Line | Target Asset Path | Present on Disk? | Status / Finding |
|---|---|---|---|---|
| `loadArt('truck')` | `chart.html:329` | `art/truck.png` | **NO** (False) | **CONFIRMED MISSING**: `chart.html` attempts to load `art/truck.png` via `new Image()` on every boot. Handled silently by `img.onerror = () => {}`, but emits a 404 network request. It was superseded by unit sprites (`ART.truck = ...` at line 346), but the call remains. |
| `loadArt('tex-' + k)` | `chart.html:328` (`BIOME` keys) | `art/tex-plain.png`, `tex-hill.png`, `tex-forest.png`, `tex-swamp.png`, `tex-desert.png`, `tex-tundra.png`, `tex-mountain.png`, `tex-water.png` | **YES** (All 8 present) | **Confirmed Referenced**: Dynamic loop across 8 biomes. All 8 texture files present on disk (sizes range 1.7 MB - 2.1 MB each, total 15.75 MB). |
| `<script src="world.js">` | `chart.html:189` | `world.js` | **YES** (Present) | **Confirmed Referenced**: Loads `window.WORLD`. |
| `<script src="art/manifest.js">` | `chart.html:190` | `art/manifest.js` | **YES** (Present) | **Confirmed Referenced**: Loads `window.MANIFEST`. |
| `<script src="game-bridge.js?v=6">` | `chart.html:191` | `game-bridge.js` | **YES** (Present) | **Confirmed Referenced**: Loads `window.MECHA`. |
| `<script src="ops.js?v=5">` | `chart.html:192` | `ops.js` | **YES** (Present) | **Confirmed Referenced**: Loads `window.OPS`. |
| `<link href="ops.css?v=2">` | `chart.html:6` | `ops.css` | **YES** (Present) | **Confirmed Referenced**: Stylesheet for ops shell. |
| `new Worker('chart-tiles-worker.js')` | `chart.html:482` | `chart-tiles-worker.js` | **YES** (Present) | **Confirmed Referenced**: Web Worker spawned when zoom exceeds threshold (`ZOOM_TILE_AT = 3`). |
| `new Worker(...)` / `importScripts` | `chart-tiles-worker.js:1` | None | N/A | Worker operates on offscreen canvas and buffers received via `postMessage`; no external assets imported. |
| `<link href="favicon.ico">` | `web/index.html:7` | `web/favicon.ico` | **YES** (Present) | **Confirmed Referenced**: 18,405 bytes icon. |
| `kit.json` assets | `web/archive/iso-ops-console/art/map/kit.json` | 12 files: `map-style-board.png`, `bldg-warehouse.png`, `bldg-tower.png`, `bldg-tank.png`, `bldg-ruin.png`, `rock-small.png`, `rock-boulder.png`, `rock-ridge.png`, `rock-peak.png`, `tree-pine.png`, `tree-broad.png`, `convoy-truck.png` | **YES** (All 12 present) | **Archived Referenced**: All 12 files present under `web/archive/iso-ops-console/art/map/`. |

### 5.3 Orphan Candidates & Inventory Uncertainty
- **MapLab Contact Sheets** (`art/contact-*.png`):
  - `contact-all.png` (5,202,865 B)
  - `contact-sheet-2.png` (2,362,511 B)
  - `contact-sheet.png` (1,134,312 B)
  - `contact-green.png` (971,073 B)
  - **Status**: **Confirmed Orphan Candidates** (4 files, **9,670,761 bytes / ~9.22 MB**). Zero references across all HTML, JS, CSS, and Python code. Tracked in Git.
- **MapLab Texture Precursors in `art/gen/texture/`**:
  - 16 items x 3 files = 48 files (16 `.png` [27.7 MB], 16 `.s256.png` [7.4 MB], 16 `.json` [27 KB]).
  - **Status**: **Generator Intermediate Candidates** (~35.1 MB). These are raw generated textures that were compiled into `art/tex-*.png` by `server.py::rebuild_manifest`. The runtime `chart.html` loads only `art/tex-*.png`. `manifest.json` deliberately excludes them (`kind == "texture"`).
- **MapLab Metadata Sidecars** (`art/gen/*/*.json`):
  - 127 `.json` files (219,498 bytes).
  - **Status**: **Generator Sidecars**. Not loaded at runtime by the browser (browser loads `manifest.js`). Required if `generator/server.py` is ever re-run to inspect library or rebuild manifest.
- **RIMG Tool Screenshots** (`tools/*.png`):
  - 39 PNG files (**47,509,057 bytes / ~45.31 MB**).
  - **Status**: **Confirmed Orphan Candidates**. Zero references across both repositories.

### 5.4 Exact Duplicate Detection (Byte-for-Byte Hash)
A SHA-256 hash scan across all tracked files in both repositories identified exactly **one** duplicate pair:
- **Hash**: `f0374f8b4dc58003db64860d8dbafea1fe717db330293823d56fba426a6bfd8b` (Git Blob: `57307a072fb679ab2d693d3e895513198590756c`)
  - `D:\FrontMission-RIMG-worktrees\PA-AGY-01\tools\probe2.png` (69,883 bytes)
  - `D:\FrontMission-RIMG-worktrees\PA-AGY-01\tools\probe3.png` (69,883 bytes)
- **Status**: Duplicate candidate pending later approval. Do not delete in Phase A.

---

## 6. Archive / Dead-Output Candidates

All items below are candidates for quarantine/removal in later phases (Phase E/F). None may be deleted or moved during Phase A.

| Candidate Item / Path | Classification | File Count | Aggregate Size | Evidence & Reference Hits | Git Recoverability | Confidence | Recommended Phase Disposition |
|---|---|---|---|---|---|---|---|
| `tools/*.png` (RIMG) | Dead development screenshots / probes | 39 | 47,509,057 bytes (45.31 MB) | Zero references across code, tests, launchers, and docs in both repositories. Unused by build or runtime. | Fully committed to Git in RIMG baseline (`29de9038` and `7f8897c1`). Fully recoverable from recovery tag `backup-rimg-20260903`. | **Very High** (100%) | **Candidate for Phase F Quarantine & Removal**: Eliminates 78% of RIMG repo weight. |
| `art/contact-*.png` (MapLab) | Dead generator contact sheets | 4 | 9,670,761 bytes (9.22 MB) | Zero references across MapLab HTML, JS, CSS, or generator scripts. Static inspection sheets. | Fully committed in MapLab baseline (`df3c1baa`). Fully recoverable from recovery tag `backup-maplab-20260903`. | **Very High** (100%) | **Candidate for Phase F Quarantine & Removal**: Eliminates ~9.2 MB of unreferenced image files. |
| `web/archive/iso-ops-console/` (RIMG) | Archived legacy isometric UI | 22 | 11,174,680 bytes (10.66 MB) | `web/archive/README.md` explicitly notes: *"Nothing in this folder is the live game. iso-ops-console/ — old isometric map + ops UI. Dead."* `Program.cs:34` notes: *"Live player view: Keeper's Chart, not the archived iso console."* | Fully committed in RIMG baseline. Fully recoverable from recovery tag `backup-rimg-20260903`. | **Very High** (100%) | **Candidate for Phase F Removal**: Explicitly slated for removal in `MIGRATION_PLAN.md §Phase F.2`. |
| `web/artlab/` & `ArtLab.cmd` & `/api/artlab/*` | Dormant 2.5D sprite lab and endpoints | 5 tracked files + 30 gitignored output files | Tracked: 30,975 B; Ignored: 22,485,024 B | `CLAUDE.md:169` & `README.md:153`: *"rejected 2.5D sprite tool. Do not reuse."* `/api/artlab/*` endpoints in `Program.cs` add external Railway proxy dependency. | Tracked files committed in RIMG baseline. Ignored output in main repo is local cache. | **High** (95%) | **Candidate for Phase F Removal**: Explicitly slated for removal in `MIGRATION_PLAN.md §Phase F.1`. |
| `opstest.html` (MapLab) | Development test bench for ops shell | 1 | 1,473 bytes | Standalone HTML that mocks `window.MECHA` to test `ops.js`. Not wired into any automated test gate. | Committed in MapLab baseline (`df3c1baa`). | **Medium** (75%) | **Keep or Move to `tests/manual/`**: Useful as an isolated iframe/visual test harness, but ambiguous in product root. |
| `art/gen/texture/*.png` (MapLab) | Raw texture generator precursors | 32 (16 png + 16 s256) | 35,078,924 bytes (33.45 MB) | Excluded from `manifest.json`. Only compiled output (`art/tex-*.png`) is loaded by `chart.html`. Precursors only needed if re-running generator. | Committed in MapLab baseline (`df3c1baa`). | **Medium** (80%) | **Candidate for LFS or External Archive**: Needed only if re-generating textures; not required for runtime playback. |

---

## 7. Secrets Hygiene

In strict adherence to the task packet and safety policies, **NO SECRET VALUES WERE OPENED, READ, OR PRINTED**. Findings below report file names, locations, Git tracking state, and finding categories only.

### 7.1 Identified Secret-Bearing Files & References
| Target File / Path | Git Status | Location | Finding Category | Purpose / Code Reference |
|---|---|---|---|---|
| `.artlab-secret` | **IGNORED** by `.gitignore:10` | Repository root (`D:\FrontMission-RIMG\.artlab-secret`) | Local API Key File | Contains API key for ArtLab image generation. Referenced in `Program.cs:226`. Present in main checkout; absent in worktree. |
| `web/artlab/.secret` | **IGNORED** by `.gitignore:8` | `D:\FrontMission-RIMG\web\artlab\.secret` | Secondary Secret Pattern | Pattern reserved in `.gitignore`. Absent on disk. |
| `D:/Tools/my-vault/vault.json` | **EXTERNAL** (outside both repositories) | `D:\Tools\my-vault\vault.json` | External Credential Vault | Hardcoded in `D:\FrontMission-MapLab\generator\server.py:40`. Contains credential named `GPT-IMAGE-2_KEY`. Present on host disk. |
| Environment Variable: `ARTLAB_API_KEY` | **CODE REFERENCE** | `src/MechaTrader.Host/Program.cs:220` | Environment Secret Override | Checked by `ReadArtlabKey` before looking for `.artlab-secret`. |
| Credential Lookup: `GPT-IMAGE-2_KEY` | **CODE REFERENCE** | `generator/server.py:51` | Named Credential Reference | Key name looked up in external vault. |
| Railway Proxy Base Address | **CODE REFERENCE** | `src/MechaTrader.Host/Program.cs:25` | Hardcoded Upstream Proxy | `client.BaseAddress = new Uri("https://asiasouth.up.railway.app/v1/");` used by ArtLab HttpClient. |

### 7.2 Tracked File Secret Scan Results
A regex pattern audit scanning all 185 tracked files in RIMG and 413 tracked files in MapLab for secret assignments, bearer tokens, OpenAI `sk-` keys, and GitHub `ghp_` tokens yielded **ZERO tracked secret matches**.
- **Tracked Code Secret Leaks**: `NONE` (Clean).
- **Coordinator Action**: Ensure `.artlab-secret` is never staged, and ensure `D:/Tools/my-vault/vault.json` is not copied into the repository during Phase B consolidation.

---

## 8. Required Gates and Follow-ups

Ranked no-delete recommendations for remaining Phase A evidence and subsequent Phase B consolidation:

### 8.1 Remaining Phase A Recommendations
1. **Gate A1 — Browser Smoke Suite (`PA-LUNA-01`)**:
   - Must probe `http://localhost:5080/chart/`, NOT only root `/`.
   - Must detect and handle the confirmed missing `art/truck.png` asset (either asserting that `img.onerror` handles it without breaking the canvas, or noting it as a known pre-existing 404).
   - Must force the lazy tile worker by simulating zoom past `ZOOM_TILE_AT = 3` (`chart-tiles-worker.js`).
   - Must sample the canvas at multiple points rather than checking a single corner.
2. **Gate A2 — Determinism & Save Compatibility Fixtures**:
   - Implement `DeterminismFingerprintTests.cs` and `SaveFixtureTests.cs` as designed in `PA-KIMI-01.md`.
   - Record golden SHA-256 fingerprints for `State`, `View`, `Content`, and `world.js`.
3. **Gate A3 — `world.js` Idempotence Verification**:
   - Our static audit confirmed that the current `world.js` matches the 6 content files in `data/`.
   - Add automated verification in Phase A ensuring `node make-world.js <dataDir>` reproduces the exact byte sequence in a clean temp environment.

### 8.2 Phase B Consolidation & Path Provenance Checks
1. **Gate B1 — Sibling Path Elimination Transaction**:
   - Remove `Program.cs::LocateMapLab` and replace with explicit `Path.Combine(webRoot, "chart")`.
   - Remove `play.ps1::Update-ChartData` sibling walk and invoke in-repository generator.
   - Update `make-world.js` line 5 to accept relative or parameter-driven data paths without the hardcoded `D:/FrontMission-RIMG/data` fallback.
   - Fail hard (exit non-zero) if the in-repository generator or `data/` directory is missing.
2. **Gate B2 — Clean-Clone Verification Without Sibling Folder**:
   - Clone to an isolated path (e.g., `%TEMP%\clean-test\mt-repo`) where no ancestor or sibling contains `FrontMission-MapLab`.
   - Add a unique provenance marker comment to the consolidated `chart.html` (e.g. `<!-- CONSOLIDATED-FRONTEND -->`) and assert its presence in `/chart/` HTTP responses.
3. **Gate B3 — Generator Generalization & Vault Isolation**:
   - Replace the user-specific Python path (`C:\Users\Eternalgy\...`) in `Generator.cmd` with dynamic PATH resolution.
   - Decouple generator from external `D:/Tools/my-vault/vault.json` before moving into the repository.
4. **Gate B4 — Asset & Screenshot Quarantine (Phase F Prep)**:
   - Prepare a separate quarantine transaction for the 39 unreferenced PNGs in `tools/` (45.31 MB) and the 4 contact sheets in MapLab (9.67 MB), verifying no regression before deletion.

---

## 9. No-Delete Attestation

I attest that:
1. **No product, test, data, launcher, or sibling MapLab file was changed, generated, moved, reformatted, or deleted** during this audit.
2. `world.js`, `FIGURES.md`, screenshots, and textures were **not regenerated**.
3. No build, test, or application launcher was executed.
4. All investigations were strictly read-only, conducted using non-destructive file reading, `rg`, `git ls-files`, `git status --ignored`, and scratch scripts stored in the designated scratch directory (`C:\Users\Eternalgy\.gemini\antigravity-cli\brain\34db321a-94ef-4dda-9006-d9433955d160\scratch\`).

### Before / After Git Status Evidence

**Before Audit:**
- `D:\FrontMission-RIMG-worktrees\PA-AGY-01`:
  ```text
  ## codex/pa-agy-01-inventory
  ?? coordination/runs/PA-AGY-01/
  ```
- `D:\FrontMission-MapLab`:
  ```text
  ## backup/maplab-final-20260903...origin/backup/maplab-final-20260903
  ```

**After Audit:**
- `D:\FrontMission-RIMG-worktrees\PA-AGY-01`:
  ```text
  ## codex/pa-agy-01-inventory
  ?? coordination/reports/PA-AGY-01-inventory.md
  ?? coordination/runs/PA-AGY-01/
  ```
  *(Only allowed report and run files created; no product files touched)*
- `D:\FrontMission-MapLab`:
  ```text
  ## backup/maplab-final-20260903...origin/backup/maplab-final-20260903
  ```
  *(Completely clean and untouched)*
