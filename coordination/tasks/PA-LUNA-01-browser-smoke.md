# Task packet: `PA-LUNA-01` — standalone browser smoke safety net

## Control

- Status: `ACTIVE`
- Worker: `LUNA-C`
- Runtime: Codex subagent
- Required model: `gpt-5.6-luna`
- Required effort: `high`
- Green base commit: `7f8897c15f5ab3b17dbe522e0e474af046a766e9`
- Branch: `codex/pa-luna-01-browser-smoke`
- Worktree: `D:\FrontMission-RIMG-worktrees\PA-LUNA-01`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`

Do not begin unless this task is `READY` or `ACTIVE` in the canonical ledger and the
recorded owner matches this packet. The worker branch begins at the coordination-only
assignment commit containing this packet; the product green base above is its parent.

## Objective

Implement one standalone Phase A safety-net component: a reliable Playwright Chromium
smoke suite for the existing two-folder layout. It must prove the current `/chart/`
frontend boots, required globals and static assets load, the canvas is materially painted,
the ops shell opens, a lazy tile worker starts, and one deterministic command traverses
the browser bridge without uncaught, console, API, or unexpected network failures.

## Evidence and context to read

1. Read the canonical plan completely.
2. Read the canonical ledger completely.
3. Read only these additional files before implementation:
   - `D:\FrontMission-RIMG\coordination\README.md`
   - `D:\FrontMission-RIMG\coordination\handoffs\PA-CURSOR-01.md`
   - `D:\FrontMission-RIMG\coordination\handoffs\PA-KIMI-01.md`
   - `D:\FrontMission-RIMG\check.ps1`
   - `D:\FrontMission-RIMG\src\MechaTrader.Host\Program.cs`
   - `D:\FrontMission-MapLab\chart.html`
   - `D:\FrontMission-MapLab\game-bridge.js`
   - `D:\FrontMission-MapLab\ops.js`
   - `D:\FrontMission-MapLab\chart-tiles-worker.js`

There is no applicable `AGENTS.md` in the repository at assignment time. If one appears,
read it before editing files under its scope.

## Allowed write scope

- `tests/browser/package.json`
- `tests/browser/package-lock.json`
- `tests/browser/playwright.config.js`
- `tests/browser/smoke.test.js`
- `tests/browser/.gitignore`
- `coordination/handoffs/PA-LUNA-01.md`

## Prohibited write scope

- `MIGRATION_PLAN.md`
- `MIGRATION_LEDGER.md`
- `check.ps1`
- All source, content, frontend, generated-output, and sibling MapLab files
- Anything not explicitly listed under allowed write scope

## Required behavior preservation

- This is test-only infrastructure; product/runtime behavior must not change.
- Exercise the existing sibling `D:\FrontMission-MapLab` frontend without copying,
  regenerating, formatting, or editing it.
- Keep the tests CommonJS/classic-compatible; do not introduce ES-module conversion.
- Do not encode Phase B's future in-repository provenance marker yet.

## Required implementation details

1. Select and lock a currently compatible `@playwright/test` version for installed Node
   `v24.19.0`; do not copy the advisory's old version blindly. Commit the lockfile.
2. Use Chromium, one worker, headless mode, and a bounded timeout. The host is started by
   the test command/config or a documented test script and must be stopped reliably.
3. Target `http://127.0.0.1:5080/chart/` in the present two-folder layout.
4. Capture listeners before navigation for uncaught page errors, `console.error`, failed
   requests, HTTP 404s, and API responses at status 400+.
5. Assert `WORLD`, `MANIFEST`, `MECHA`, and `OPS` initialize and that WORLD has non-empty
   cities/routes plus map/truck data.
6. Assert `/api/state` returns a view with numeric day and cash.
7. Assert the canvas has nonzero dimensions and sample multiple distributed regions or
   points; a single corner sample is insufficient.
8. Open and close the ops shell via the real Tab interaction and assert it is visible.
9. Trigger the lazy `chart-tiles-worker.js` path by zoom/query or user interaction, observe
   its worker/network lifecycle, and surface worker exceptions.
10. Explicitly probe `world.js`, `game-bridge.js`, `ops.js`, `ops.css`,
    `chart-tiles-worker.js`, and `art/manifest.js` for HTTP 200.
11. Through `window.MECHA`, start a fixed-seed game and issue a deterministic, legal `wait`
    command; assert the returned snapshot advances the day and has no error.
12. Fail with concise diagnostics containing collected browser/network evidence. Tolerate
    only a narrowly documented optional-art failure proven intentional by current code;
    do not blanket-ignore failures.

## Non-goals

- Do not edit or append `check.ps1`; integration into the full gate is a later packet.
- Do not add deterministic/save/API fixtures beyond assertions inside this smoke test.
- Do not consolidate, copy, refactor, move, or delete product/frontend files.
- Do not remove or alter `D:\FrontMission-MapLab`.
- Do not start Phase B.

## Required checks

1. `git diff --check` — pass.
2. `npm ci --prefix tests/browser` — pass.
3. `npx --prefix tests/browser playwright install chromium` — pass.
4. `npm test --prefix tests/browser` — pass against the current two-folder layout and
   leave no host process listening on port 5080.
5. `git status --short` — only allowed files before commit; clean after commit/handoff.

Do not run the complete `check.ps1`; the coordinator owns full acceptance sequencing.

## Stop conditions

Stop and return `BLOCKED` without expanding scope if:

- A required change falls outside allowed paths.
- The exact green product base cannot be confirmed as an ancestor of branch HEAD.
- Port 5080 is occupied by an unrelated process; report its PID without killing it.
- The smoke test reveals a current product failure that cannot be represented without
  weakening a required assertion.
- Required checks remain red after two focused repair attempts.
- Another worker modified the same owned path.

## Deliverables

- One implementation commit on the assigned branch.
- No unrelated formatting or cleanup.
- A handoff at `coordination/handoffs/PA-LUNA-01.md` using
  `coordination/HANDOFF_TEMPLATE.md`, committed with the implementation.
