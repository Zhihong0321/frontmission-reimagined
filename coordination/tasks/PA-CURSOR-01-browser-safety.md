# Task packet: `PA-CURSOR-01` — browser safety-net design

## Control

- Status: `READY`
- Worker: `CURSOR`
- Runtime: Cursor IDE
- Required model: Grok 4.6
- Required effort: highest available
- Job type: read-only preflight analysis
- Product baseline commit: `752e5fad7b8e945e9eb3342adabc78c70d95a3c5`
- Branch: none
- Worktree: `D:\FrontMission-RIMG`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`

## Objective

Design the smallest reliable automated browser safety net that must be green before the
finalized MapLab frontend is imported or split. Find ways the current seven-gate check can
pass while the actual player view is broken.

## Required evidence

Read completely:

- `D:\FrontMission-RIMG\MIGRATION_PLAN.md`
- `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- `D:\FrontMission-RIMG\check.ps1`
- `D:\FrontMission-RIMG\src\MechaTrader.Host\Program.cs`
- `D:\FrontMission-RIMG\play.ps1`
- `D:\FrontMission-MapLab\chart.html`
- `D:\FrontMission-MapLab\game-bridge.js`
- `D:\FrontMission-MapLab\ops.js`
- `D:\FrontMission-MapLab\chart-tiles-worker.js`
- `D:\FrontMission-MapLab\_ops-test.html`

## Write scope

Only `D:\FrontMission-RIMG\coordination\handoffs\PA-CURSOR-01.md` may be created. Do not
edit, format, move, or delete any other file. Do not commit.

## Required output

Return one concise implementation specification containing:

1. Current browser-test gaps, ordered by ability to create a false green result.
2. Recommended test runner using the smallest justified dependency footprint.
3. Exact assertions for page load, globals, API boot, canvas, ops shell, worker, assets,
   navigation, console failures, and network failures.
4. Exact proposed files and commands for the later implementation job.
5. How to prove the consolidated frontend is used instead of the sibling MapLab folder.
6. Which checks run per small frontend extraction and which run only at phase checkpoints.
7. Any blocker that should prevent Phase B from starting.

Write the result to `coordination\handoffs\PA-CURSOR-01.md` using
`coordination\HANDOFF_TEMPLATE.md` with result commit `NONE`.

## Stop conditions

- Do not implement the test suite.
- Do not start a persistent development server.
- Do not recommend ES-module conversion as part of the first extraction.
- Do not expand into visual redesign or gameplay work.
