# Worker handoff: `PA-LUNA-01`

- Status: `BLOCKED`
- Worker: `LUNA-C`
- Runtime/model: Codex subagent — `gpt-5.6-luna`, high effort
- Branch: `codex/pa-luna-01-browser-smoke`
- Base commit: `7f8897c15f5ab3b17dbe522e0e474af046a766e9`
- Result commit: `f94f2e05267782b2f92e18576a93480d6cb24f26`
- Diagnostic branch: pushed to `origin/codex/pa-luna-01-browser-smoke`

## Files changed on the diagnostic branch

- `tests/browser/package.json`
- `tests/browser/package-lock.json`
- `tests/browser/playwright.config.js`
- `tests/browser/smoke.test.js`
- `tests/browser/.gitignore`
- `coordination/handoffs/PA-LUNA-01.md`

The coordinator rolled the test implementation back from `master` after the stop-loss
triggered. This handoff is retained on `master` as durable evidence; all implementation
files remain recoverable on the pushed diagnostic branch.

## Checks run

| Command | Result | Evidence |
|---|---|---|
| `git diff --check` | PASS | No whitespace errors |
| `node --check tests/browser/smoke.test.js` | PASS | CommonJS smoke test parses |
| `npm ci --prefix tests/browser` | PASS | 3 packages installed; 0 vulnerabilities |
| `npx --prefix tests/browser playwright install chromium` | PASS | Chromium installed |
| `npm test --prefix tests/browser` | `BLOCKED` | Strict repair run failed on an existing negative-radius canvas `arc` page error during incremental zoom, before tile-worker creation |
| Port 5080 post-run check | PASS | No listener remained |
| Worker-branch scope review | PASS | Only the task packet's six allowed files changed |

## Review and repair history

1. Initial commit `633c75e5142888d79df97116fb5d31c43db5d7e3` produced a green
   smoke test but proved only worker creation.
2. Repair 1 commit `8401013a86404b4e4cebdfc93aa61223446be7dc` observed the production
   worker's `ready` and successful `tile` responses and passed targeted/full checks.
3. Coordinator audit found that `/chart/art/gen/**` failures were blanket-exempted, which
   could hide deletion of a manifest-declared runtime sprite.
4. Repair 2 commit `f94f2e05267782b2f92e18576a93480d6cb24f26` narrowed the allowlist
   to the two proven current missing fallbacks and added HTTP HEAD probes for every
   manifest-declared runtime sprite. That strict suite exposed the existing canvas error.

## Behavior changes

`NONE`. The worker changed test infrastructure only; the incomplete integration was
rolled back from `master`.

## Risks and uncertainty

- The current frontend intentionally requests missing `art/tex-deep.png` and
  `art/truck.png`; only those exact paths were tolerated by repair 2.
- The zoom/canvas failure requires diagnosis and a separately authorized design or product
  fix. Weakening the page-error assertion would reintroduce a false-green browser gate.

## Out-of-scope findings

- Existing frontend zoom interaction can raise an uncaught negative-radius canvas `arc`
  error before the required lazy worker path becomes observable.

## Requested ledger update

`PA-LUNA-01` is `BLOCKED` under the two-repair stop-loss. Do not integrate diagnostic
commit `f94f2e0` or advance dependent Phase A work until the browser gate is redesigned or
the pre-existing frontend failure receives separate authorization.
