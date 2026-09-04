# Worker handoff: `PA-KIMI-01`

- Status: `COMPLETE`
- Worker: `KIMI`
- Runtime/model: Kimi CLI 0.39.1 / `cmkey/kimi-k3`
- Branch: none (read-only preflight; no worktree assigned)
- Base commit: `752e5fad7b8e945e9eb3342adabc78c70d95a3c5` (product baseline; working tree HEAD `97a2ef85` = baseline + two coordination-protocol commits, `git status` clean)
- Result commit: `NONE`

## Files changed

- `coordination/handoffs/PA-KIMI-01.md` (this handoff; the only file written)

No product code was modified, formatted, moved, or deleted. Nothing was committed.

## Implementation specification

### 1. Exact Phase A command order and prerequisites

Prerequisites:

- PowerShell 5.1+ (check.ps1 relies on `-UseBasicParsing`), .NET SDK (`dotnet`), Node.js (world.js regeneration).
- Current two-folder layout: `D:\FrontMission-RIMG` plus sibling `D:\FrontMission-MapLab` containing `make-world.js`, `chart.html`, `game-bridge.js`, `world.js`.
- Port 5080 free; no stale `MechaTrader.Host` / `dotnet run` process holding DLL locks (play.ps1 `Stop-PreviousInstances` handles this in the launcher; check.ps1 kills its own child in `finally`).
- Full (non-shallow) git history: `/api/build` asserts `gitAvailable` and a non-empty commit log.
- Env (`check.ps1` sets them; a bare host launch should too): `DOTNET_NOLOGO=1`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`.

Exact order (mirrors `check.ps1`; the `--no-build` flags make step 1 mandatory first):

1. Preflight: `git status --porcelain` empty; record `git rev-parse HEAD`.
2. Build: `dotnet build MechaTrader.sln -c Release --nologo -v q` — exit 0 with **0 warnings** (the criterion parses the warning count).
3. Unit tests: `dotnet test tests/MechaTrader.Core.Tests/MechaTrader.Core.Tests.csproj -c Release --no-build --nologo -v q`.
4. Balance harness (regenerates `FIGURES.md`): `dotnet run --project tools/MechaTrader.BalanceSim -c Release --no-build`.
5. Launch host: `dotnet run --project src/MechaTrader.Host -c Release --no-build` (hidden process, redirected logs to `%TEMP%`); poll `http://localhost:5080/api/state` until ready (45 × 700 ms).
6. API walkthrough on seed `12345`: `/api/new` → `buy` → `depart` → `wait` → `sell` → illegal `depart` (must error) → `hireCrew` + wage tick + `dismissCrew` → fresh `/api/new` seed `777` → `favor donate` (standing rises, supply index responds) → `/api/build` (version == `VERSION` file, log non-empty, `log[0].isHead`, not stale).
7. Teardown: `Stop-Process` the host; confirm port 5080 released.
8. Record post-run `git status` (BalanceSim rewrites `FIGURES.md`, so the tree is expected dirty unless that change is committed).

Phase A step 6 (browser smoke) is a separate packet (`PA-CURSOR-01`); this advisory job does not define its commands.

### 2. Current test gaps that could hide deterministic or save regressions

Current state: `SimulationInvariantTests.cs` holds three determinism facts (`SameSeedAndSameCommandsProduceIdenticalState`, `DifferentSeedsProduceDifferentHistories`, `StateSurvivesASaveLoadRoundTrip`); per-feature round trips exist in `ContractTests`, `MapTests`, `CrewTests`, `StandingTests`, `EventTests`, `CityStatsTests`, `WarehouseTests`. All compare in-memory serialized strings only.

Gaps:

- **No golden fingerprints on disk.** `JsonSerializer.Serialize(game.State)` is compared only within one process run. A refactor that changes serialization order or RNG consumption is invisible unless two runs are compared; nothing pins "seed + script → hash" as a fixture.
- **No pre-recorded save fixtures.** Every round trip is serialize → deserialize → serialize in the same build. There is no "load a JSON file written by the current version and resume from it" test, so forward compatibility of the persisted format (ledger invariant: existing save/resume stays compatible) is unverified.
- **Narrow command coverage.** The shared determinism script covers only `buy/depart/wait/sell` on two goods. `buyTruck`, `buyGear`, `hireCrew/dismissCrew/assignCrew`, `favor`, `rentWarehouse`/`warehouse*`, `acceptContract`/`deliverContract`, `expo*`, mining, `sellTruck`, `upgradeTruck` are not in one end-to-end deterministic script; their tests are per-feature only.
- **No cross-process determinism check.** Tests run in one CLR process; dictionary/hash-set enumeration order and property order are self-consistent there, so order regressions pass silently.
- **No view-level fingerprint.** Determinism of `State` does not pin `ViewBuilder.Build` output, which is exactly what Phase C splits.
- **No API response-shape contract.** `check.ps1` is the only consumer and asserts presence/absence of errors, not JSON shape.
- **No `world.js` artifact check.** The generated frontend world is never validated against `data/`; stale content passes silently (play.ps1 even logs "regen failed - continuing anyway").
- **No clean-clone verification automation** for the sibling-directory false-positive risk (`LocateMapLab` walks *up* from the web root).
- **Shallow-clone fragility**: `/api/build` and `BuildInfoTests` need git history; a partial clone fails criterion 7 for environmental reasons, not product reasons.

### 3. Proposed deterministic fingerprints and representative save fixtures

Fingerprints (all SHA-256 hex over UTF-8):

- `F_state(seed, script)` — hash of `JsonSerializer.Serialize(Game.New(world, seed))` after applying a fixed command script, using real content via `ContentLoader.LoadWorld()` (not only `TestWorld.Shipping`).
- `F_view(seed, script)` — hash of a canonicalized JSON of `Game.View()` after the same script.
- `F_content` — per-file `(filename, sha256)` manifest over the 15 `WorldLoader.RequiredKeys` files (`config, goods, terrain, trucks, industries, cities, routes, crew, citystats, standing, events, map, gear, contracts, expos`).
- `F_worldjs` — hash of `world.js`; regenerate via `node make-world.js <dataDir>` into a temp dir and hash the output for the expected value.
- `F_figures` — hash of `FIGURES.md` after a BalanceSim run (it is regenerated by criterion 3, so it is part of the known-green tree state).

Two comparison layers, deliberately different strictness:

- Byte-level fingerprint (default `S.T.J` serialize): catches iteration-order, property-order, and RNG-consumption regressions — exactly what mechanical splits must preserve.
- Semantic fixture comparison (deserialize then compare values): proves load compatibility independent of JSON key order, so future format improvements are not blocked by a false-positive order diff.

Representative save fixtures (recorded by the current build, stored under the test project, manifest with `{file, sha256, seed, script}`):

1. `day1-new-run.json` — `Game.New` only, fixed seed, zero commands.
2. `trade-cycle.json` — one buy → depart → wait → sell cycle (mirrors the existing `BuildScript`).
3. `late-run-mixed.json` — ~60+ day state touching crew, warehouse, contract, expo, standing, and a mining deposit.

Fixture test behavior: load JSON → `Game.Resume(ContentLoader.LoadWorld(), state)` → apply a short continuation script → assert no exception, derived state equals the in-memory round trip, and pinned values (`cash`, `day`, `location.id`) match recorded golden values.

### 4. How to capture and compare API response shape without brittle noise

Facts: every game endpoint returns `Snapshot(View, Log, Error)` (`Program.cs:54,62-63,66`); `ConfigureHttpJsonOptions` sets `DefaultIgnoreCondition = Never`, so `error` is always present (often `null`). `/api/build` carries time-derived fields (`builtAgo`, timestamps) that are inherently noisy. `/api/map` returns `ViewBuilder.BuildMap`.

Approach:

- **Shape contract (anti-breaking):** record raw response bodies for a fixed seed+script (`/api/state`, `/api/new`, each `/api/command`, `/api/map`, `/api/build`) as fixtures; the checker deserializes to strong types (or `JsonDocument`) and asserts stable keys and types exist — `view.cash` (number), `view.day` (number), `view.location` (object|null), `log` (array of `{day, kind, message}`), `error` (string|null). A `MapGet`/`MapPost` removal or a record-field rename fails here.
- **Value baseline (anti-regression):** assert pinned derived values (`cash`, `location.id`, cargo units, supply index) equal recorded golden values for the same script.
- **Canonical comparison:** parse with `JsonDocument`, sort object keys, drop an explicit minimal noise allowlist (`log[*].message`, `build.builtAgo`, timestamps, cache-buster query param), re-serialize, hash.
- **Recording discipline:** capture raw text (curl / `Invoke-WebRequest`), never re-serialized PowerShell objects — PS round-trips reorder keys and reformat numbers. `S.T.J` number formatting (`R`-style) differs from JS `Number.prototype.toString` for some values; always generate and compare with the same serializer.

### 5. How to verify `world.js` regeneration and detect stale content

Facts: `make-world.js` reads `cities.json, routes.json, terrain.json, map.json, trucks.json, config.json` from the passed data dir and writes `world.js` into its own directory (MapLab root) with a header comment containing the source data dir; `chart.html:189` loads it as `<script src="world.js">`, consumed as `window.WORLD` at `chart.html:198`. `play.ps1` regenerates it on every launch and silently continues if MapLab or node is missing.

Verification procedure:

1. Record baseline: `F_content` + current `world.js` hash.
2. Regenerate into a temp dir: `node D:\FrontMission-MapLab\make-world.js D:\FrontMission-RIMG\data`.
3. Assert exit code 0 and idempotence: regenerated hash equals the checked-in `world.js` hash. A mismatch means the tree's `world.js` was already stale — adopt the regenerated output as the new baseline on first run, then require idempotence thereafter.
4. Stale-content guard: derive `F_worldjs_expected` from the six content files (regenerate + hash) and compare against `world.js`; also assert `window.WORLD` city/route counts equal `cities.json`/`routes.json` counts.
5. Clean-clone case (Phase B goal): with no sibling MapLab, `play.ps1` skips regeneration — the verifier must treat "world.js absent/stale" as the *expected pre-migration state* and fail loudly only after in-repository regeneration is wired up.

### 6. How to run from a clean checkout without accidentally using external files

Source-of-truth upward walks that can hit foreign directories:

- `Program.LocateMapLab` (`Program.cs:205`) — finds sibling `FrontMission-MapLab/chart.html` walking up from the web root; a clone placed anywhere under an ancestor that contains MapLab silently mounts `/chart/`.
- `Program.LocateWebRoot` (`Program.cs:190`) — walks up for `web/index.html`.
- `ContentLoader.FindDataDirectory` (`ContentLoader.cs:21`) — walks up for a `data/config.json`.
- `play.ps1 Update-ChartData` (`play.ps1:63`) — walks up for `make-world.js`.
- `TestWorld.RepositoryRoot` — walks up for `MechaTrader.sln`.

Clean-checkout procedure:

1. Clone into an isolated path whose ancestor chain contains no `FrontMission-MapLab` and no other `data/` with `config.json` (e.g. `%TEMP%\rimg-clean-clone\repo`). Use a **full** clone — `--depth 1` breaks `/api/build` and criterion 7.
2. Run `check.ps1` + fingerprint/fixture/world.js checks from that clone.
3. Assert: `GET /chart/` returns 404 (no sibling ⇒ `/chart` route absent), the served web root resolves inside the clone, and `FindDataDirectory` resolves to the clone's `data/` — record all three resolved paths as evidence (this is the sibling-directory false-positive control from the plan).
4. Assert `git status` after the run shows only the expected `FIGURES.md` regeneration.

### 7. Exact proposed files and commands for the later implementation job

Files (Phase A steps 4–5 land here; owned by the implementation job, not this advisory packet):

- `tests/MechaTrader.Core.Tests/DeterminismFingerprintTests.cs` — full-command-surface fingerprint facts + content-manifest assertion.
- `tests/MechaTrader.Core.Tests/SaveFixtureTests.cs` + `tests/MechaTrader.Core.Tests/Fixtures/saves/{day1-new-run,trade-cycle,late-run-mixed}.json` + `manifest.json`.
- `tests/MechaTrader.Core.Tests/Fixtures/api/*.json` — recorded raw API responses (shape contract + value baseline).
- `tools/MechaTrader.Fingerprint/` (console project) or `tools/fingerprint.ps1` — emits `F_state/F_view/F_content/F_worldjs` so two processes/CI runs can be compared byte-for-byte.
- `tools/verify-worldjs.ps1` — regeneration + idempotence + stale detection.
- `tools/clean-clone-check.ps1` — isolated full clone, run checks, assert `/chart/` 404 and in-clone path resolution.
- `check.ps1` (coordinator-approved extension) — append fingerprint, fixture, API-shape, and world.js criteria.

Commands:

- `dotnet build MechaTrader.sln -c Release --nologo -v q`
- `dotnet test tests/MechaTrader.Core.Tests/MechaTrader.Core.Tests.csproj -c Release --no-build --nologo -v q`
- `dotnet run --project tools/MechaTrader.BalanceSim -c Release --no-build`
- `dotnet run --project tools/MechaTrader.Fingerprint -c Release --no-build -- <seed> <script-file>`
- `node D:\FrontMission-MapLab\make-world.js D:\FrontMission-RIMG\data`
- `powershell -File tools/verify-worldjs.ps1`
- `powershell -File tools/clean-clone-check.ps1 -Origin <url> -Target <dir>`

### 8. Blocker assessment for structural migration

- **Gate, not wall:** no hard blocker prevents migration — recovery snapshots are `VERIFIED`, baseline commit exists, working tree is clean. All findings are Phase A completion gates.
- **Save/determinism risk unmitigated until Phase A step 5:** without golden fingerprints and save fixtures, Phase C/D mechanical splits can silently change serialization order or save compatibility and nothing catches it. Phase B should not start before step 5 is recorded green.
- **Browser-blind suite still open:** the acceptance suite never loads `/chart/` (it checks the root `web/index.html` only); `PA-CURSOR-01` must land before frontend-adjacent migration.
- **Two-layout expected difference:** today `/chart/` exists only via the sibling directory (200); a clean clone serves 404. This is the *intended* pre-migration difference and must be recorded as expected, not as a regression, or Phase B verification will be read wrong.
- **Full-history requirement:** clean-clone verification must use full clones or `/api/build` fails environmentally; the procedure (section 6) must state this.

## Checks run

| Command | Result | Evidence |
|---|---|---|
| `git rev-parse HEAD` / `git status --porcelain` | PASS | HEAD `97a2ef85`, tree clean, product baseline `752e5fa` two commits below |
| Full read of task packet evidence files | PASS | `MIGRATION_PLAN.md`, `MIGRATION_LEDGER.md`, `check.ps1`, `play.ps1`, `Game.cs`, `GameState.cs`, `GameSession.cs`, `SimulationInvariantTests.cs`, `make-world.js`, `Program.cs`, `ContentLoader.cs`, `TestWorld.cs`; save/resume tests located via search |
| Build / unit tests / acceptance suite | NOT RUN | Stop conditions forbid running the full acceptance suite in this advisory job |

## Behavior changes

`NONE`. No product code was touched; only `coordination/handoffs/PA-KIMI-01.md` was created.

## Risks and uncertainty

- This specification is static analysis; command order and behaviors were read from source, not executed. The implementation job must treat section 1/7 commands as proposals to verify on the first green run.
- `S.T.J` double formatting differs from JS number stringification; API fixtures must be recorded and compared with the same serializer (section 4).
- Dictionary/hash-set serialization order is implementation, not contract. Byte-level fingerprints intentionally capture order changes; a .NET runtime upgrade may require re-baselining rather than being a product regression.

## Out-of-scope findings

- `play.ps1 Stop-PreviousInstances` force-kills any process on port 5080 or named `MechaTrader.Host`, including unrelated listeners on that port; verification scripts should record PIDs before killing.
- `BalanceSim` rewrites `FIGURES.md` on every criterion-3 run, leaving the tree dirty; a clean-clone check must expect exactly this diff.
- The root `web/index.html` served at `/` is not the live frontend (that is `/chart/` from MapLab); PA-CURSOR-01's browser assertions should target `/chart/`, not `/`.

## Requested ledger update

Record `PA-KIMI-01` as completed advisory preflight with result commit `NONE` (proposed status `REVIEW` awaiting coordinator read of this handoff). Nothing in this handoff authorizes migration work; Phase B stays gated on Phase A steps 4–5.
