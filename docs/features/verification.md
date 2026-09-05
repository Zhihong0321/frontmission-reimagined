# Feature note: verification

Owns: `check.ps1`, `tools/verify-fast.ps1`, `tools/verify-feature.ps1`,
`tools/verify-full.ps1`, `tools/Generate-Codemap.ps1`, `tools/verify-worldjs.ps1`,
`tools/verify-api-shape.ps1`, `tools/clean-clone-check.ps1`,
`tools/MechaTrader.Fingerprint/`, `tests/MechaTrader.Core.Tests/DeterminismFingerprintTests.cs`,
`SaveFixtureTests.cs`, `tests/api-fixtures/`, `tests/browser/`.

## What it is

The proof stack, in three tiers:

- **Fast** (`tools/verify-fast.ps1`): zero-warning Release build plus optional
  affected tests. An iteration aid; its output states it cannot certify anything.
- **Feature** (`tools/verify-feature.ps1 -Feature <name>`): one feature's targeted
  checks (dotnet test filters, the world/API verifiers, the browser smoke). Also an
  iteration aid.
- **Full** (`tools/verify-full.ps1`): the six gates — zero-warning Release build; the
  complete nine-gate `check.ps1`; Fingerprint regeneration with zero tracked fixture
  diff and pinned `F_state`/`F_view`; browser smoke 1/1; clean `git diff --check`;
  hygiene (port 5080 free, no Host process, FIGURES timing-line restored, temp dirs
  baseline-compare plus this-run cleanup). `-IncludeCleanClone` additionally runs the
  full-history no-sibling clone proof.

## Key facts

- The nine gates of `check.ps1`: build clean; unit tests (239); balance harness;
  web-host buy-haul-sell; recruitment; city page; build page; world.js sync; API
  shape/value baseline. Exit code is the answer.
- The fingerprint tool regenerates the determinism fixtures on demand; the gate is
  that regeneration produces ZERO diff (the pins are already correct).
- The browser smoke pins served-file provenance and fails on any console/network
  error or asset 404 (two known pre-existing fallbacks excepted).
- `tools/clean-clone-check.ps1` clones the repo to a temp location with no sibling
  MapLab, proves fatal launcher controls, regenerates `world.js`, and runs the full
  suite there.

## Invariants and gotchas

- Only a same-run Full result may back a green/finished claim. Fast/feature outputs
  say so themselves; repeat it when reporting.
- Never weaken an existing gate; gates are added to, not redefined.
- Every verification run must end clean: port 5080 released, no `MechaTrader.Host`
  process, FIGURES timing line restored, new temp directories removed without
  touching pre-existing ones.
