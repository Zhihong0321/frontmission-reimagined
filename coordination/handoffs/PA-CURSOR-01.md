# Worker handoff: `PA-CURSOR-01`

- Status: `COMPLETE`
- Worker: `CURSOR`
- Runtime/model: Cursor IDE — Grok 4.6 (highest effort)
- Branch: none (read-only advisory job)
- Base commit: `752e5fad7b8e945e9eb3342adabc78c70d95a3c5`
- Result commit: `NONE`

## Files changed

- `coordination/handoffs/PA-CURSOR-01.md` (this file only)

## Checks run

| Command | Result | Evidence |
|---|---|---|
| Read all required evidence files | PASS | All eight files read in full |

## Behavior changes

`NONE`

## Risks and uncertainty

- The ops-test iframe harness in `_ops-test.html` does not run in the CI environment and could drift silently from the suite design below.
- `world.js` is generated at launch time by `make-world.js`. If it is absent or stale the canvas fails completely with no useful console message; this gap is called out in Section 1.
- The `art/manifest.js` load is silent-fail-by-design (`img.onerror = () => {}`). A 404 on the manifest itself is not currently detectable from the outside.

## Out-of-scope findings

- `Program.cs` `LocateMapLab` walks parent directories looking for a sibling `FrontMission-MapLab` folder. This sibling-discovery walk is the exact false-positive risk described in `MIGRATION_PLAN.md §Sibling-directory false positive` and must be removed in Phase B before any verification can be considered clean.
- `play.ps1` `Update-ChartData` also walks parent directories for `FrontMission-MapLab/make-world.js`. If both directories are present at a new location, `world.js` is silently regenerated and the version in the repository is ignored. This must also be replaced with an explicit in-repository path in Phase B.
- `_ops-test.html` is a development harness that tests the ops shell via an iframe. It provides a reusable pattern for the automated suite but is not wired into any CI gate.

## Requested ledger update

`PA-CURSOR-01` read-only preflight analysis complete. No migration work performed. Handoff written to disk. No ledger status change required beyond recording receipt of this handoff.

---

## 1. Current browser-test gaps — ordered by false-green risk

### G1 — No browser gate exists at all (highest false-green risk)

`check.ps1` has seven gates; none open a browser or exercise JavaScript. Every gate that could be green while the canvas is completely blank:

- Gate 1 (build): `dotnet build` does not touch `chart.html`, `game-bridge.js`, `ops.js`, `world.js`, or `chart-tiles-worker.js`.
- Gate 2 (unit tests): the test suite is pure C#; browser globals are never exercised.
- Gate 3 (balance harness): headless simulation, no UI.
- Gate 4 (host serves a playable buy-haul-sell cycle): checks `page.StatusCode -eq 200` for `http://localhost:5080` (the `web/index.html` redirect), not `/chart/`. The `/chart/` static files are served from the sibling `FrontMission-MapLab` directory by `LocateMapLab`. A `200` on the redirect says nothing about whether the scripts in that folder execute.
- Gates 5–7 (crew, city, build): JSON API calls only; no browser execution.

Consequence: a consolidation that breaks `world.js`, mis-serves `chart.html`, or fails to spawn the tile worker produces seven green gates and a broken player view.

### G2 — Sibling-directory discovery (`LocateMapLab` / `Update-ChartData`)

`Program.cs` lines 206–214 walk parent directories for any folder named `FrontMission-MapLab` containing `chart.html`. `play.ps1` lines 64–79 do the same for `make-world.js`. Both will silently fall back to the original sibling folder after Phase B imports the frontend, making a broken in-repository copy invisible. The check script never probes which physical path was actually served.

### G3 — `world.js` generated externally, not committed

`world.js` is produced by `make-world.js` and placed in `FrontMission-MapLab/`. It is not served from `web/` and is not built by `dotnet build`. If it is absent, or if its content diverges from `data/`, the canvas fails at the `window.WORLD` reference with a JavaScript ReferenceError — but no existing gate catches that error.

### G4 — `art/manifest.js` 404 is silent

`chart.html` line 190 loads `art/manifest.js` unconditionally. The `spritesReady` logic (`img.onerror = () => res(0)`) treats any failed image load as a zero-weight sprite. The manifest file itself has no `onerror` handler at the `<script>` tag level; a 404 results in a silent `window.MANIFEST` being `undefined`, which is handled gracefully but suppresses all sprite rendering. No existing gate catches this.

### G5 — Tile worker spawned lazily, not verified at boot

`chart-tiles-worker.js` is only spawned when the viewport zoom exceeds `ZOOM_TILE_AT = 3` (`wantTile` → `startTileWorker`). A worker that 404s or throws in its `onmessage` handler fails silently at any zoom below that threshold. A browser gate that does not zoom in will never see this failure.

### G6 — `ops.js` and `ops.css` load correctness not checked

`ops.js` defines `window.OPS`. `game-bridge.js` defines `window.MECHA`. Both are classic scripts with `?v=N` cache-busters in `chart.html`. A broken or stale-cached script leaves `OPS` or `MECHA` undefined; the canvas loop guards on `window.MECHA` and `window.OPS` but produces no error — the HUD simply never updates and `Tab` does nothing. No existing gate asserts either global is defined or callable.

### G7 — No console-error or network-error signal in any gate

Uncaught exceptions, `console.error` calls, failed `fetch` calls and asset 404s all pass the current seven gates silently. The `_ops-test.html` harness collects `window.addEventListener('error', ...)` but is not wired to CI.

### G8 — API boot not checked against the in-repository path

Gate 4 posts to `/api/new` and `/api/command`. It does not assert that the static files served at `/chart/` came from the in-repository `web/chart/` path (Phase B target) rather than the sibling folder. After Phase B there will be two candidate paths and no gate distinguishes them.

---

## 2. Recommended test runner

**Node.js with Playwright** (one `devDependency`, zero production dependencies).

Justification:

- Node.js is already required at runtime (the `make-world.js` generator calls `node`). It is available in the existing development environment and is verified by `play.ps1`.
- Playwright provides headless Chromium with a documented API for console interception, network interception, `Worker` lifecycle events, and `page.evaluate` for global-presence checks. It installs its own browser binaries so no browser is required in the repository.
- The alternative (Puppeteer) is functionally equivalent but Playwright's `Worker` events and `request.failure` interception are better suited to the tile-worker and asset checks.
- No ES-module conversion is required. Playwright test files are plain CommonJS.
- The test file is a single self-contained script; no testing framework is needed. A `playwright.config.js` is the only additional file.
- Dependency footprint: `npm install --save-dev playwright` adds one package. `npx playwright install chromium` downloads ~130 MB of browser binaries that do not belong in Git (add `node_modules/` and `.playwright/` to `.gitignore`).

---

## 3. Exact assertions

All assertions are expressed as Playwright API calls. The server is started externally before the test runs (see Section 4). `BASE` = `http://localhost:5080`.

### 3.1 Page load

```
GET ${BASE}/chart/  →  HTTP 200
response.headers['content-type']  contains  'text/html'
page load does not time out within 30 000 ms
```

### 3.2 Required globals

After the `#loading` overlay is removed (or after `page.waitForFunction(() => !document.getElementById('loading'), { timeout: 60000 })`):

```javascript
await page.waitForFunction(() => typeof window.WORLD === 'object' && window.WORLD !== null,
  { timeout: 60000 });
await page.waitForFunction(() => typeof window.MECHA === 'object' && window.MECHA !== null,
  { timeout: 60000 });
await page.waitForFunction(() => typeof window.OPS === 'object' && window.OPS !== null,
  { timeout: 60000 });
await page.waitForFunction(() => typeof window.MANIFEST !== 'undefined',
  { timeout: 30000 });
// WORLD must carry the expected sub-keys
const worldOk = await page.evaluate(() =>
  Array.isArray(window.WORLD.cities) &&
  window.WORLD.cities.length > 0 &&
  typeof window.WORLD.map === 'object' &&
  Array.isArray(window.WORLD.routes) &&
  typeof window.WORLD.truck === 'object'
);
assert(worldOk, 'WORLD lacks required sub-keys');
```

### 3.3 API boot

```javascript
const state = await page.evaluate(() =>
  fetch('/api/state').then(r => r.json()));
assert(state.view !== undefined, '/api/state must return a view');
assert(typeof state.view.day === 'number', 'view.day must be a number');
assert(typeof state.view.cash === 'number', 'view.cash must be a number');
```

### 3.4 Canvas

```javascript
const canvasOk = await page.evaluate(() => {
  const cv = document.getElementById('map');
  if (!cv || cv.tagName !== 'CANVAS') return 'missing';
  if (cv.width === 0 || cv.height === 0) return 'zero-size';
  const ctx = cv.getContext('2d');
  const d = ctx.getImageData(0, 0, 4, 4).data;
  // At least one non-black pixel means the base has been painted
  for (let i = 0; i < d.length; i += 4) {
    if (d[i] > 10 || d[i+1] > 10 || d[i+2] > 10) return 'painted';
  }
  return 'black';
});
assert(canvasOk === 'painted', `Canvas expected painted, got: ${canvasOk}`);
```

### 3.5 Ops shell

```javascript
// Tab opens the shell
await page.keyboard.press('Tab');
await page.waitForFunction(() => window.OPS && window.OPS.isOpen(), { timeout: 5000 });
const shellVisible = await page.locator('#ops').isVisible();
assert(shellVisible, 'ops shell #ops must be visible after Tab');

// A city page renders (overview page must exist)
const navItems = await page.locator('#ops .ops-nav-item').count();
assert(navItems >= 3, 'ops shell must have at least 3 nav items');

// Close
await page.keyboard.press('Tab');
await page.waitForFunction(() => window.OPS && !window.OPS.isOpen(), { timeout: 5000 });
```

### 3.6 Worker

```javascript
// Zoom past ZOOM_TILE_AT = 3 to trigger tile worker spawn
const workerPromise = page.waitForEvent('worker', { timeout: 15000 });
await page.evaluate(() => {
  // Simulate zoom to 4× by calling wantTile which calls startTileWorker
  // Access cam via the page context — cam.z is declared in the inline script
  cam.z = 4;
  wantTile(0, 0);
});
const worker = await workerPromise;
assert(worker.url().includes('chart-tiles-worker.js'),
  'tile worker must be chart-tiles-worker.js');
```

Fallback (if `cam` / `wantTile` are not accessible due to future strict-mode scoping): intercept the network request for `chart-tiles-worker.js` during a zoom interaction instead.

### 3.7 Assets

Collect all failed requests during page load and boot. Register the listener before navigation:

```javascript
const failed404 = [];
page.on('requestfailed', req => {
  if (req.failure().errorText.includes('net::ERR_ABORTED')) return; // cancelled by navigation
  failed404.push(req.url());
});
page.on('response', res => {
  if (res.status() === 404) failed404.push(res.url());
});
await page.goto(`${BASE}/chart/`);
// wait for boot to complete...
assert(failed404.length === 0,
  `Static asset 404s detected: ${failed404.join(', ')}`);
// Explicitly probe required assets
for (const path of [
  '/chart/world.js',
  '/chart/game-bridge.js',
  '/chart/ops.js',
  '/chart/ops.css',
  '/chart/chart-tiles-worker.js',
  '/chart/art/manifest.js',
]) {
  const res = await page.request.get(`${BASE}${path}`);
  assert(res.status() === 200, `Required asset ${path} returned ${res.status()}`);
}
```

### 3.8 Navigation

```javascript
// At least one city beacon must be clickable and produce the depart card
const citiesPresent = await page.evaluate(() =>
  typeof CITIES !== 'undefined' && CITIES.length > 0);
assert(citiesPresent, 'CITIES array must be non-empty');

// Pick a city that is not the starting city and click its screen position
const plan = await page.evaluate(() => {
  const target = CITIES.find(c => c.id !== conv.node);
  if (!target) return null;
  const { sx, sy } = toScreen(target.x, target.y);
  return { id: target.id, sx, sy };
});
assert(plan !== null, 'Must find a non-current city to click');
await page.mouse.click(plan.sx, plan.sy);
await page.waitForSelector('#card.show', { timeout: 5000 });
const cardVisible = await page.locator('#card').isVisible();
assert(cardVisible, 'Depart card must appear after clicking a city');
```

### 3.9 Console failures

Register before navigation:

```javascript
const consoleErrors = [];
page.on('console', msg => {
  if (msg.type() === 'error') consoleErrors.push(msg.text());
});
// Also catch uncaught JS errors
const pageErrors = [];
page.on('pageerror', err => pageErrors.push(err.message));
// After boot:
assert(consoleErrors.length === 0,
  `console.error during boot: ${consoleErrors.join('; ')}`);
assert(pageErrors.length === 0,
  `Uncaught JS errors during boot: ${pageErrors.join('; ')}`);
```

Note: `art/` image 404s currently produce `console.error` in some Chromium versions via the `<img>` `onerror` path. The asset-probe assertion in 3.7 is the authoritative check; tolerate image-load console noise only if it is traced explicitly to `art/*.png` optional textures.

### 3.10 Network failures

Covered by the `requestfailed` listener in 3.7. In addition, assert that the two API calls that game-bridge issues automatically on load succeed:

```javascript
// game-bridge calls /api/state on load and /api/new on a fresh session
const apiFailures = [];
page.on('response', res => {
  if (res.url().includes('/api/') && res.status() >= 400)
    apiFailures.push(`${res.status()} ${res.url()}`);
});
// After boot:
assert(apiFailures.length === 0,
  `API failures during boot: ${apiFailures.join('; ')}`);
```

---

## 4. Exact proposed files and commands

### Files

```
tests/
  browser/
    smoke.test.js          -- the test script (Playwright, CommonJS)
    playwright.config.js   -- baseURL, one project: chromium, single worker
    package.json           -- { "devDependencies": { "playwright": "^1.44" } }
    .gitignore             -- node_modules/  .playwright/
```

`playwright.config.js`:

```javascript
// @ts-check
const { defineConfig } = require('@playwright/test');
module.exports = defineConfig({
  testDir: '.',
  timeout: 90000,
  use: { baseURL: 'http://localhost:5080', headless: true },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
  workers: 1,
});
```

`smoke.test.js` implements all assertions from Section 3 as a single `test('chart boots and the ops shell opens', ...)` block. Failure messages must print the failing assertion and the browser console log collected to that point.

`package.json`:

```json
{
  "private": true,
  "devDependencies": {
    "@playwright/test": "^1.44.0"
  }
}
```

### Commands

Install (one-time, per machine):

```powershell
cd tests\browser
npm install
npx playwright install chromium
```

Run (requires the host to be serving on port 5080 already):

```powershell
cd tests\browser
npx playwright test smoke.test.js
```

Run as part of `check.ps1` gate 8 (to be added later):

```powershell
# start the host in background (already done by gate 4), then:
$browserOk = $false
$browserDetail = 'not reached'
try {
  $browserOut = & npx playwright test tests/browser/smoke.test.js --reporter=line 2>&1 | Out-String
  $browserOk = ($LASTEXITCODE -eq 0)
  $browserDetail = if ($browserOk) { 'all smoke assertions passed' } else { ($browserOut -split '\n' | Where-Object { $_ -match 'Error|failed|FAIL' } | Select-Object -First 3) -join '; ' }
} catch { $browserDetail = $_.Exception.Message }
Record 'Browser smoke: chart boots, ops shell opens, no console errors' $browserOk $browserDetail
```

---

## 5. Proving the consolidated frontend is used instead of the sibling MapLab folder

### Required change (Phase B prerequisite, not implemented here)

Remove `LocateMapLab` from `Program.cs` and replace it with a hard-coded in-repository path:

```csharp
// Replace the walking discovery with:
var chartDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "web", "chart");
// or, if the host is started from the repo root:
var chartDir = Path.Combine(webRoot, "chart");
```

### Verification assertion (to add to the smoke test)

After Phase B, add to `smoke.test.js`:

```javascript
// Probe a file that only exists in the in-repository copy.
// During Phase B import, add a one-line comment to the top of the in-repository
// chart.html: <!-- CONSOLIDATED-FRONTEND -->
// Then assert:
const res = await page.request.get(`${BASE}/chart/`);
const body = await res.text();
assert(body.includes('CONSOLIDATED-FRONTEND'),
  '/chart/ must be served from the in-repository copy (marker comment missing)');
```

The marker comment is a deliberate one-byte provenance signal. It costs nothing, survives minification-free classic-script serving, and makes the provenance check unambiguous. Remove it only after Phase F when the sibling folder is deleted.

### Clone verification (Phase B checkpoint)

Perform a clean clone into a directory that has no sibling `FrontMission-MapLab` folder, start the host, and assert the smoke test still passes. This is the primary structural proof.

```powershell
git clone D:\FrontMission-RIMG C:\Temp\mt-verify
cd C:\Temp\mt-verify
dotnet run --project src/MechaTrader.Host &
# wait for ready, then:
npx playwright test tests/browser/smoke.test.js
```

If `LocateMapLab` is not removed, this clone will fail at the `/chart/` URL with a 404 because there is no sibling directory. That failure is itself evidence that the gap exists.

---

## 6. Which checks run per small frontend extraction vs. at phase checkpoints

### Per-extraction checks (after every Phase D step)

These are fast and run in the worker branch before the integration PR is raised:

| Check | Command | Purpose |
|---|---|---|
| Build | `dotnet build MechaTrader.sln -c Release` | Guard against C# regressions from path changes |
| Browser smoke suite | `npx playwright test smoke.test.js` | Assert the canvas still boots, globals are present, no console errors, no 404s |
| Asset probe | included in smoke suite (Section 3.7) | Catch any 404 introduced by moving a file |

### Phase checkpoint checks (before tagging `known-green/*`)

Full suite; all must be green:

| Check | Command |
|---|---|
| Full acceptance | `.\check.ps1` |
| Browser smoke | `npx playwright test smoke.test.js` |
| Deterministic fingerprint | comparison against pre-refactoring baseline |
| Save fixture round-trip | `dotnet test` (SimulationInvariantTests) |
| Clean-clone verification | clone to a path with no sibling MapLab folder; launch; run smoke |

### Phase D step sequence and check cadence

| Step | Fast checks | Phase checkpoint |
|---|---|---|
| Extract inline CSS | build + smoke | no |
| Extract inline chart JS to `chart.js` | build + smoke | YES (first structural extraction) |
| Extract terrain helpers | build + smoke | no |
| Extract rendering helpers | build + smoke | no |
| Extract camera and input | build + smoke | no |
| Extract routing | build + smoke | no |
| Extract HUD | build + smoke | no |
| Extract worker interaction | build + smoke | YES (worker path change risk) |
| Establish shared ops namespace | build + smoke | no |
| Extract ops helpers | build + smoke | no |
| Extract one ops page at a time | build + smoke per page | no |
| Extract stateful boot code | build + smoke | YES (final Phase D checkpoint) |

---

## 7. Blockers that should prevent Phase B from starting

The following must all be resolved before Phase B (`known-green/original` is tagged):

**B1 — Browser smoke suite does not exist (this report designs it; it must be implemented and green)**

Phase A step 4 (`MIGRATION_PLAN.md`) requires the browser smoke suite to be added as a standalone safety-net change. Phase B must not begin until that change is committed and the suite passes against the current two-folder layout.

**B2 — `known-green/original` is not tagged**

Phase A requires committing and tagging the first known-green application commit. The ledger records `known-green/original` as `UNSET`. Until this tag exists, Phase B has no verified baseline to branch from.

**B3 — The clean-environment baseline has not been recorded**

Phase A step 6 requires verifying both API and browser behavior from a clean environment reproducing the two-folder layout. The ledger records `Last full verification: NOT_RUN`. A run of `.\check.ps1` against the current commit plus a passing browser smoke run must be recorded in the ledger before Phase B.

**B4 — Deterministic fingerprints and save fixtures do not exist**

Phase A steps 5 requires adding deterministic state fingerprints and representative save fixtures before structural migration begins. The ledger does not record these as complete. Phase B moves files; without a fingerprint baseline, a silent determinism regression cannot be detected.

**B5 — The in-repository frontend target path is not resolved**

Open decision `1` in the ledger (`Final approval of the proposed in-repository frontend path web/chart/`) is unresolved. Phase B step 2 targets `web/chart/` but the decision is listed as open. The path must be approved and recorded before Phase B assigns a write scope.

**Non-blocking pre-B findings (should be noted, not blocking):**

- The Artlab image-generation endpoints (`/api/artlab/*`) and the `artlab/out/` directory are present in `Program.cs`. Whether these are live product features or experimental code should be confirmed before the consolidation import, since they add a non-game-related dependency (`gpt-image-2`) to the host startup path.
