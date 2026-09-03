# Worker handoff: `PA-ROOT-02`

- Status: `COMPLETE`
- Worker: `ROOT`
- Runtime/model: Codex coordinator began this job in the isolated worker worktree and
  stopped mid-task after reaching its usage quota, leaving the four implementation files
  and `.gitignore` present but uncommitted and unchecked. Claude Code (Sonnet 5), invoked
  directly by the user to continue the interrupted job, completed the redesign, ran every
  required check, and made the commit below.
- Branch: `codex/pa-root-02-browser-redesign`
- Base commit: `5e74f671bdf6925d51ccd51e0bf6bed5ac7aa98f` (task-packet green base);
  worktree HEAD when this job resumed was `15b9aff` (assignment commit containing the
  packet, cherry-picked from `master`)
- Result commit: see `git log -1` on this branch after this handoff is committed

## Files changed

- `tests/browser/package.json`
- `tests/browser/package-lock.json`
- `tests/browser/playwright.config.js`
- `tests/browser/smoke.test.js`
- `tests/browser/.gitignore`
- `coordination/handoffs/PA-ROOT-02.md`

## What the redesign changed

The worktree already held a near-verbatim copy of the diagnostic suite from
`origin/codex/pa-luna-01-browser-smoke` at `f94f2e0` (all strict assertions, the optional
two-PNG allowlist, and the manifest HEAD probes were already correct), but the tile-worker
trigger had not yet been redesigned: it still drove a synthetic `page.mouse.wheel` loop up
to the chart's maximum zoom, the exact path that exposes the pre-existing, out-of-scope
negative-radius canvas `arc` error and caused `PA-LUNA-01` to block.

This job made the one change the packet asked for:

1. Navigate to `/chart/?view=14.4,50.1,4` instead of `/chart/`. `chart.html`'s `boot()`
   parses `?view=lon,lat,zoom` and, when `zoom > ZOOM_TILE_AT` (3), calls
   `startTileWorker()`/`wantTile()` synchronously during boot — before the base map even
   finishes painting — so the deep link exercises the same production trigger the frontend
   itself uses, with no synthetic input. `14.4,50.1` is Praha's real map coordinate
   (`data/cities.json`), so the prewarm window lands on real, tiled terrain and a tile
   request actually resolves.
2. Replaced the one-shot `page.waitForEvent('worker', ...)` raced against the wheel loop
   with a poll over the `workers` array that the pre-navigation `page.on('worker', ...)`
   listener already fills. Because the deep link can spawn the worker before any
   post-navigation code runs, a fresh one-shot listener registered after `goto` could miss
   the event entirely; the persistent listener registered before `goto` cannot.

No other assertion, tolerance, or lifecycle behavior was changed.

## Checks run

| Command | Result | Evidence |
|---|---|---|
| `node --check tests/browser/smoke.test.js` | PASS | CommonJS smoke test parses |
| `git diff --check` (staged) | PASS | No whitespace errors |
| Write-scope review | PASS | Only the six packet-allowed paths are staged |
| `npm ci --prefix tests/browser` | PASS | 3 packages added, 0 vulnerabilities |
| `npx --prefix tests/browser playwright install chromium` | PASS | Chromium already present, no-op install |
| `npm test --prefix tests/browser` (run 1) | PASS | 1 passed in 30.5s; worker `ready`, one `tile` without `err`, no worker errors, no page/console/network/API failures |
| `npm test --prefix tests/browser` (run 2) | PASS | 1 passed in 16.2s, same assertions green |
| Port 5080 listener check after each run | PASS | No `LISTENING` entry for 5080 after either run |

## Behavior changes

`NONE`. Test infrastructure only; no product, data, launcher, or sibling MapLab file was
touched.

## Risks and uncertainty

- The strict suite still tolerates exactly two missing fallback sprites
  (`art/tex-deep.png`, `art/truck.png`) per `D-026`; this was not reopened.
- This suite is not yet wired into `check.ps1` — the task packet's non-goals explicitly
  defer that.
- The underlying negative-radius canvas `arc` error that blocked `PA-LUNA-01` still exists
  in the frontend; this job avoids triggering it rather than fixing it, exactly as the
  packet's non-goals require. It remains a known, out-of-scope frontend defect.

## Out-of-scope findings

- (Carried forward from `PA-LUNA-01`, unchanged) Driving `cam.z` to the chart's maximum
  zoom via a large wheel gesture still raises an uncaught negative-radius canvas `arc`
  error. This redesign routes around it by construction; it has not been diagnosed or
  fixed and would need separate authorization.

## Requested ledger update

Mark `PA-ROOT-02` `REVIEW` (commit ready for coordinator integration review) with evidence
that the strict browser gate — every assertion carried over from the `PA-LUNA-01` diagnostic,
retriggered via the production `?view=` deep link instead of a synthetic wheel gesture — is
green on two consecutive runs, with port 5080 confirmed released both times. Phase A's
browser gate is no longer `BLOCKED`; dependent Phase A work may proceed once the coordinator
integrates this branch and reruns the full existing acceptance suite as the packet's
integration-queue entry requires.
