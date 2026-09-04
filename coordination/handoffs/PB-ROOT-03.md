# Worker handoff: `PB-ROOT-03`

```text
JOB_ID: PB-ROOT-03
STATUS: COMPLETE
BRANCH: codex/pb-root-03-path-switch
COMMIT: 71d68ecca4c0d41d168e060a275f1a58190f5c04
FILES_CHANGED: src/MechaTrader.Host/Program.cs; play.ps1; tests/browser/smoke.test.js; tools/clean-clone-check.ps1
CHECKS_RUN: assignment/base/hash reconciliation; PowerShell 5.1 parser; node --check; Release host build; repository-local world verifier; full nine-gate check.ps1; npm ci; Playwright Chromium install; browser smoke with exact world provenance; committed full-history clean-clone verification; two fatal launcher controls; port/process cleanup; tracked-diff and exact-scope review; repository/MapLab source immutability hashes and status
CHECK_RESULTS: PASS — zero-warning/zero-error Release build; 239 unit tests and all nine gates green; browser smoke 1/1; clean-clone generator/input failures fatal before host startup; clean-clone regeneration exact at SHA-256 26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a; clean-clone browser provenance green; port 5080 clean; only FIGURES.md timing output permitted in disposable clone
BEHAVIOR_CHANGES: /chart is served only from repository-local web/chart; launcher regenerates from repository-local data and fails on missing Node/generator/data/input/output or generator failure; browser smoke pins the served world.js provenance header and complete byte hash
RISKS: This job proves the atomic path switch only; it does not delete MapLab, refactor frontend/backend code, change host configuration beyond the /chart provider, or authorize another Phase B job
OUT_OF_SCOPE_FINDINGS: NONE
LEDGER_UPDATE_REQUEST: Mark PB-ROOT-03 REVIEW at implementation commit 71d68ecca4c0d41d168e060a275f1a58190f5c04 and integrate only this job into integration if the independent coordinator review and repeated checks remain green
```

## Base and ownership

- Green integration base and assignment parent:
  `e981a8eea80cea96da67d34d8da24cc5a7663131`.
- Assignment-only master commit:
  `ad2c620680e588d5a59d4ce12289f991608ea045`.
- `PB-ROOT-02` product merge `b108789` and `PB-ROOT-01` product merge `ec7cc79`
  remained ancestors of the worker base.
- The worker changed only the four implementation paths assigned by
  `coordination/tasks/PB-ROOT-03-path-switch.md`; this handoff is the only additional
  worker-owned path.

## Implemented transaction

1. `src/MechaTrader.Host/Program.cs` now requires the consolidated chart root and its
   critical files at startup and mounts `/chart` from `web/chart` only. The former
   parent walk and sibling MapLab provider were removed.
2. `play.ps1` now invokes `web/chart/make-world.js` with repository-local `data`, checks
   all six required inputs, and treats every generation prerequisite or failure as
   fatal. It no longer searches for, invokes, or falls back to a sibling generator.
3. `tests/browser/smoke.test.js` fetches the actually served `/chart/world.js` before UI
   navigation and requires both the stable repository-relative header and complete
   SHA-256 `26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a`.
4. `tools/clean-clone-check.ps1` now proves the Phase B state from a full-history clone
   under an isolated temp root: representative missing-generator and missing-input
   launcher failures are fatal before startup, local generation is byte-clean, all nine
   gates pass, browser provenance passes, port 5080 is released, dependency/test output
   is removed, and no unexpected tracked diff remains.

## Verification evidence

- PowerShell parsed `play.ps1` and `tools/clean-clone-check.ps1` without errors under
  Windows PowerShell; `node --check tests/browser/smoke.test.js` passed.
- `dotnet build src/MechaTrader.Host -c Release --nologo` passed with 0 warnings and
  0 errors.
- `tools/verify-worldjs.ps1` passed with the exact committed world SHA-256
  `26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a`.
- The worker full `check.ps1` passed all nine gates, including 239 unit tests and the
  generated-world verifier. Its generated `FIGURES.md` timing line was restored and is
  absent from the implementation commit.
- The worker browser run passed 1/1 after `npm ci` and pinned Chromium installation.
- After implementation commit `71d68ec`, the committed clean-clone script passed the
  two fatal launcher controls, deterministic regeneration, all nine gates, browser
  provenance, process/port cleanup, and allowed-diff inspection.
- `data/` and `web/chart/` had no worker diff. Repository generator SHA-256 remained
  `9e34b1de203d51f4bc3332d1ab8536734c343af21adc12c423da9091918fe6a0`;
  repository output remained `26063b3e...0712a`.
- Read-only MapLab remained at branch `backup/maplab-final-20260903`, HEAD
  `df3c1baa8a83c2412607353af9994170b988dbe3`, with its pre-existing status exactly
  ` M world.js`. Its generator remained SHA-256
  `87b9cbbdcb9a7dc80a23d120ce0c8ba748bb5f4834986f7f6b33948dcf23a64c`
  and its output remained
  `6680509cd8cbacc72ab3b8060efd4b8c7d3c328f8646aaeb78ddb1531c3d135c`.

No file was modified, deleted, or moved in `D:\FrontMission-MapLab`; no frontend,
backend, generated WORLD payload, data, documentation, tag, later job, or Phase C-F work
is included.
