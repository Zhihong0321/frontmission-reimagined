# Task packet: `PA-ROOT-02` — redesign strict browser worker trigger

## Control

- Status: `ACTIVE`
- Worker: `ROOT`
- Runtime: Codex coordinator acting in an isolated worker worktree
- Green base commit: `5e74f671bdf6925d51ccd51e0bf6bed5ac7aa98f`
- Branch: `codex/pa-root-02-browser-redesign`
- Worktree: `D:\FrontMission-RIMG-worktrees\PA-ROOT-02`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`

Do not begin unless this task is `ACTIVE` in the canonical ledger and assigned to ROOT.
The worker branch begins at the coordination-only assignment commit containing this packet.

## Objective

Produce a strict, standalone browser smoke suite for the current two-folder layout by
redesigning only the tile-worker trigger. Reuse the useful test-only implementation from
diagnostic branch `origin/codex/pa-luna-01-browser-smoke` at `f94f2e0`, but replace the
synthetic wheel sequence with the frontend's existing `?view=lon,lat,zoom` deep-link
prewarm path. Prove the worker emits `ready` and at least one successful `tile` response
without uncaught page errors. Do not change product or sibling MapLab files.

## Evidence and context to read

1. Read `D:\FrontMission-RIMG\MIGRATION_PLAN.md` and `MIGRATION_LEDGER.md` completely.
2. Read `coordination/handoffs/PA-LUNA-01.md` and the prior diagnostic test at commit
   `f94f2e05267782b2f92e18576a93480d6cb24f26`.
3. Read `D:\FrontMission-MapLab\chart.html` sections defining `startTileWorker`,
   `wantTile`, the `?view=` deep-link prewarm, and deep-link camera application.
4. Confirm no applicable `AGENTS.md` exists before editing.

## Allowed write scope

- `tests/browser/package.json`
- `tests/browser/package-lock.json`
- `tests/browser/playwright.config.js`
- `tests/browser/smoke.test.js`
- `tests/browser/.gitignore`
- `coordination/handoffs/PA-ROOT-02.md`

## Prohibited write scope

- `MIGRATION_PLAN.md` and `MIGRATION_LEDGER.md`
- `D:\FrontMission-MapLab\**`
- All RIMG product source, data, launcher, generated-output, and asset files
- `check.ps1`
- Anything outside the allowed scope

## Required behavior and assertions

1. Preserve every strict assertion from diagnostic commit `f94f2e0`: globals, API state,
   distributed canvas samples, ops shell, deterministic browser-bridge command, required
   static assets, manifest-declared runtime sprite HEAD probes, console/page/network/API
   failures, and worker error/message evidence.
2. Tolerate only the two proven current missing fallbacks:
   `/chart/art/tex-deep.png` and `/chart/art/truck.png` (query strings allowed).
3. Navigate with a valid high-zoom deep link such as `/chart/?view=14.4,50.1,4` so the
   existing boot prewarm calls `startTileWorker` and `wantTile` without synthetic wheel
   input. Attach the test-only Worker wrapper before navigation.
4. Require a worker URL ending in `chart-tiles-worker.js`, a `ready` message, at least one
   `tile` message without `err`, no `tile` message with `err`, and no worker error event.
5. Keep the host/test lifecycle bounded and prove port 5080 is released.

## Non-goals

- Do not fix or suppress the frontend canvas error.
- Do not weaken page-error, asset, or worker assertions.
- Do not consolidate files, start Phase B, or alter the sibling repository.
- Do not integrate into `check.ps1` yet.

## Required checks

1. `node --check tests/browser/smoke.test.js`.
2. `git diff --check` and exact allowed-path scope review.
3. `npm ci --prefix tests/browser`.
4. `npx --prefix tests/browser playwright install chromium`.
5. `npm test --prefix tests/browser` passes twice consecutively to reduce flake risk.
6. Port 5080 has no listener after each run.
7. Commit implementation and `coordination/handoffs/PA-ROOT-02.md` on the assigned branch.

## Stop conditions

Stop `BLOCKED` without expanding scope if:

- The deep-link path does not produce a successful tile response.
- Any strict browser assertion remains red after two focused repair attempts.
- Passing requires a product, MapLab, launcher, or `check.ps1` change.
- Another worker modifies an owned path.

## Deliverables

- One bounded browser-test commit on the assigned branch.
- A structured handoff at `coordination/handoffs/PA-ROOT-02.md`.
- No product behavior changes.
