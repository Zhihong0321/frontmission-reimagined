# Task packet: `PA-ROOT-03` — deterministic fingerprints, save fixtures, API-shape, world.js

## Control

- Status: `ACTIVE`
- Worker: `ROOT` (Claude Code, per `D-029`; Codex remains out of quota)
- Runtime: Claude Code acting in an isolated worker worktree
- Green base commit: `f1efe3a` (master, post-`PA-ROOT-02` integration)
- Branch: `codex/pa-root-03-determinism-fixtures`
- Worktree: `D:\FrontMission-RIMG-worktrees\PA-ROOT-03`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`

Do not begin unless this task is `ACTIVE` in the canonical ledger and assigned to ROOT.

## Objective

Close Phase A step 6 of `MIGRATION_PLAN.md`: add deterministic state/view fingerprints,
representative save fixtures, stable API-shape fixtures, content hashes, generated-world
verification, and an explicit command-coverage matrix. This is the accepted design from
`coordination/handoffs/PA-KIMI-01.md` (`D-015`), with the additional requirement from
`coordination/handoffs/PA-CLAUDE-01.md` item 7 (`D-016`): explicitly name which of the 21
command types in `Commands/Commands.cs` are exercised directly by the fingerprint script
versus covered only by the existing per-feature xUnit suite, so that gap is a recorded,
accepted risk rather than an invisible one.

## Evidence and context to read

1. Read `MIGRATION_PLAN.md` (Phase A step 6) and `MIGRATION_LEDGER.md` completely.
2. Read both preflight handoffs in full: `coordination/handoffs/PA-KIMI-01.md` and
   `coordination/handoffs/PA-CLAUDE-01.md` (item 7 specifically).
3. Read `src/MechaTrader.Core/Commands/Commands.cs` and `CommandProcessor.cs` for the
   exact, current command surface (do not trust a prior count from either advisory doc).
4. Read `tests/MechaTrader.Core.Tests/SimulationInvariantTests.cs` and `TestWorld.cs` for
   existing determinism-test conventions to extend rather than duplicate.
5. Read `src/MechaTrader.Core/Sim/Recruitment.cs`, `Contracts.cs`, `Expos.cs` for the pure
   lookup functions needed to script contract/expo/crew commands without hardcoding ids.
6. Read `data/trucks.json`, `data/gear.json`, `data/crew.json` for concrete ids.

## Allowed write scope

- `tests/MechaTrader.Core.Tests/DeterminismFingerprintTests.cs`
- `tests/MechaTrader.Core.Tests/SaveFixtureTests.cs`
- `tests/MechaTrader.Core.Tests/Fixtures/**`
- `tools/MechaTrader.Fingerprint/**` (new console project)
- `tools/verify-worldjs.ps1`
- `tools/verify-api-shape.ps1`
- `tools/clean-clone-check.ps1`
- `tests/api-fixtures/**` (recorded raw API responses)
- `check.ps1` (coordinator-approved extension only: append new gates, do not remove or
  weaken any of the existing seven)
- `MechaTrader.sln` (register the new console project)
- `coordination/handoffs/PA-ROOT-03.md`

## Prohibited write scope

- `MIGRATION_PLAN.md` and `MIGRATION_LEDGER.md`
- `D:\FrontMission-MapLab\**` (read-only reference for command scripting; no edits)
- Any existing product source, data, or test file not listed above
- Anything outside the allowed scope

## Required behavior and assertions

1. `DeterminismFingerprintTests.cs`: build a command script against `TestWorld.Shipping`
   (the real shipped content — `ContentLoader.LoadWorld()`) that scripts as many of the 21
   command types as can be done deterministically using the game's own pure lookup
   functions (`Recruitment.PoolFor`, `Contracts.BoardFor`, `Expos.Running`/`Next`) to pick
   valid dynamic ids, exactly as the existing script already does for cities via the road
   graph. Compute `F_state` (SHA-256 of `JsonSerializer.Serialize(Game.State)`) and
   `F_view` (SHA-256 of a canonicalized `Game.View()` JSON) after running the script on a
   fixed seed, and assert each equals a recorded golden hash constant. A second fact
   asserts same-seed reproducibility independent of the golden value, matching the
   existing `DeterminismTests` pattern.
2. Include an explicit command-coverage matrix (a documented constant list) enumerating
   all 21 command types and marking each `Scripted` or `FeatureTestsOnly: <test class>`.
   Every `FeatureTestsOnly` entry must name a real, currently passing test class.
3. `F_content`: hash each of the 15 `WorldLoader.RequiredKeys` data files and assert
   against a recorded manifest (filename + sha256).
4. `SaveFixtureTests.cs` + three fixtures under `Fixtures/saves/`: `day1-new-run.json`
   (`Game.New` only), `trade-cycle.json` (one buy/depart/wait/sell cycle), and
   `late-run-mixed.json` (~60+ days touching crew, warehouse, contract, expo, standing,
   and a mining deposit). Each fixture test loads the JSON, resumes via `Game.Resume`,
   applies a short continuation script, and asserts no exception plus pinned values
   (`cash`, `day`, `location.id`) match recorded golden values.
5. `tools/MechaTrader.Fingerprint`: a console project that can regenerate the golden
   `F_state`/`F_view`/`F_content` values and the three save fixtures on demand, so a
   future intentional change can re-baseline without hand-editing hex strings.
6. `tools/verify-worldjs.ps1`: copy `make-world.js` into an isolated temp directory (its
   output path is `__dirname`-relative, so it must not run in place), regenerate
   `world.js` from `data/` there, and assert the SHA-256 of the regenerated file equals
   the SHA-256 of the live `D:\FrontMission-MapLab\world.js`. Exit non-zero on mismatch or
   on any node/script failure.
7. `tools/verify-api-shape.ps1`: launch the host exactly as `check.ps1` criterion 4 does,
   run a fixed seed + script API walkthrgo against `/api/state`, `/api/new`,
   `/api/command` (one per scripted command type reachable via the HTTP surface),
   `/api/map`, and `/api/build`; capture raw response text (never re-serialized
   PowerShell objects); compare against recorded fixtures in `tests/api-fixtures/` using
   two layers — shape (key presence/type, order-independent) and pinned value baseline
   (`cash`, `day`, `location.id`) — with an explicit, minimal noise allowlist
   (`log[*].message`, `build.builtAgo`, any timestamp field). Confirm port 5080 released.
8. `tools/clean-clone-check.ps1`: full local clone (`git clone --no-hardlinks`, not
   `--depth`) into an isolated `%TEMP%` path with no `FrontMission-MapLab` or foreign
   `data/config.json` in its ancestor chain; run `check.ps1` plus the two new verify
   scripts there; assert `GET /chart/` returns 404 (no sibling present) and that the
   resolved web root and data directory are inside the clone; assert `git status` after
   the run shows only the expected `FIGURES.md` diff.
9. Extend `check.ps1` with two additional gates calling `verify-worldjs.ps1` and
   `verify-api-shape.ps1`. Do not alter, remove, or weaken any of the existing seven.
   `clean-clone-check.ps1` stays a standalone tool, not wired into `check.ps1`'s default
   run (it is a one-time Phase-boundary gate, not a per-run cost).

## Non-goals

- Do not fix, weaken, or work around any existing product behavior.
- Do not wire `clean-clone-check.ps1` into `check.ps1`'s default run.
- Do not start Phase B or touch `D:\FrontMission-MapLab\**`.
- Do not claim 21/21 scripted coverage if some command types cannot be deterministically
  scripted without a product change — name the gap instead per requirement 2.

## Required checks

1. `dotnet build MechaTrader.sln -c Release --nologo -v q` — 0 warnings.
2. `dotnet test tests/MechaTrader.Core.Tests/MechaTrader.Core.Tests.csproj -c Release --no-build --nologo -v q` — all green, including the new fixture/fingerprint facts.
3. `tools/verify-worldjs.ps1` passes against the current `D:\FrontMission-MapLab\world.js`.
4. `tools/verify-api-shape.ps1` passes with port 5080 released afterward.
5. Full existing seven-gate `powershell -File .\check.ps1`, extended to nine gates, all green.
6. `tools/clean-clone-check.ps1` passes once, as the Phase A step 7 clean-environment gate.
7. `git diff --check` and exact allowed-path scope review.
8. Commit implementation and `coordination/handoffs/PA-ROOT-03.md` on the assigned branch.

## Stop conditions

Stop `BLOCKED` without expanding scope if:

- A command type cannot be scripted without a product behavior change — record it as
  `FeatureTestsOnly` instead of forcing it.
- Any required check remains red after two focused repair attempts.
- Passing requires a product, MapLab, or existing-test change.
- Another worker modifies an owned path.

## Deliverables

- One or more bounded commits on the assigned branch (new tests, fixtures, tools, and a
  `check.ps1` extension).
- A structured handoff at `coordination/handoffs/PA-ROOT-03.md`, including the full
  command-coverage matrix.
- No product behavior changes.
