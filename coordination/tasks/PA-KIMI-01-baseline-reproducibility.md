# Task packet: `PA-KIMI-01` — baseline reproducibility design

## Control

- Status: `READY`
- Worker: `KIMI`
- Runtime: Kimi CLI 0.39.1, user launched
- Required model: `cmkey/kimi-k3`
- Job type: read-only preflight analysis in plan mode
- Product baseline commit: `752e5fad7b8e945e9eb3342adabc78c70d95a3c5`
- Branch: none
- Worktree: `D:\FrontMission-RIMG`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`

## Objective

Design the exact reproducible known-green baseline procedure for Phase A, concentrating on
the .NET simulation, deterministic state fingerprints, save compatibility fixtures,
content generation, host startup, process cleanup, and clean-clone verification.

## Required evidence

Read completely:

- `D:\FrontMission-RIMG\MIGRATION_PLAN.md`
- `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- `D:\FrontMission-RIMG\check.ps1`
- `D:\FrontMission-RIMG\play.ps1`
- `D:\FrontMission-RIMG\src\MechaTrader.Core\Game.cs`
- `D:\FrontMission-RIMG\src\MechaTrader.Core\State\GameState.cs`
- `D:\FrontMission-RIMG\src\MechaTrader.Host\GameSession.cs`
- `D:\FrontMission-RIMG\tests\MechaTrader.Core.Tests\SimulationInvariantTests.cs`
- Relevant save/resume and deterministic tests found by search
- `D:\FrontMission-MapLab\make-world.js`

## Write scope

None. Do not edit, format, create, move, or delete files. Do not commit. Remain in plan
mode.

## Required output

Return one concise implementation specification containing:

1. Exact Phase A command order and prerequisites.
2. Current test gaps that could hide deterministic or save regressions.
3. Proposed deterministic fingerprints and representative save fixtures.
4. How to capture and compare API response shape without brittle noise.
5. How to verify `world.js` regeneration and detect stale content.
6. How to run from a clean checkout without accidentally using external files.
7. Exact proposed files and commands for the later implementation job.
8. Any blocker that should prevent structural migration.

Format the result using `coordination\HANDOFF_TEMPLATE.md` with result commit `NONE`.

## Stop conditions

- Do not run the full acceptance suite in this advisory job.
- Do not start a persistent server.
- Do not implement fixtures or scripts.
- Do not make balance or gameplay recommendations.
