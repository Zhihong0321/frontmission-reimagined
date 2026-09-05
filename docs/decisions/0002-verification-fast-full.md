# ADR 0002: Two-tier verification — Fast/feature are iteration aids; Full is the only green

- Status: Accepted (per `MIGRATION_LEDGER.md` `D-055`, refined by CP-E1)
- Date: 2026-09-05
- Context: Before D-055 every task ran a dual-state full battery, which was too heavy
  for the remaining work; but targeted checks create false confidence when mistaken
  for whole-product verification. The migration also accumulated several one-off
  verifiers (world.js, API shape, browser smoke, clean clone) with no single entry
  point.

## Decision

Two verification tiers, each with a script entrypoint:

- **Fast** (`tools/verify-fast.ps1`): zero-warning Release build plus the affected
  tests. An iteration aid ONLY — it can never certify a green or finished state. Its
  output states this.
- **Feature** (`tools/verify-feature.ps1 -Feature <name>`): one feature's targeted
  checks. Same restriction.
- **Full** (`tools/verify-full.ps1`): the six gates, all mandatory:
  1. zero-warning Release build;
  2. the complete nine-gate `check.ps1`;
  3. `tools/MechaTrader.Fingerprint` regeneration with zero tracked fixture diff and
     pinned `F_state`/`F_view`;
  4. browser smoke 1/1 (`npm ci` + Playwright Chromium);
  5. clean `git diff --check`;
  6. hygiene: port 5080 free, no `MechaTrader.Host` process, `FIGURES.md`
     timing-line-only restored, temp directories baseline-compare plus this-run
     cleanup.

Any `MERGED`/`VERIFIED` claim must cite a same-run Full result on the exact tree.

## Interpretation note (superset wording vs. the six-gate list)

`MIGRATION_PLAN.md` Phase E step 9 words Full as "a strict superset of the original
seven check.ps1 gates plus the browser, deterministic, save, API-shape,
generated-world, asset, and clean-path checks." The approved revision's six-gate list
above is the binding minimum (recorded for CP-E1 under `D-057`). All superset elements
are covered inside those six gates: deterministic and save checks run inside the
239-test Core suite (gate 2) and the Fingerprint regeneration (gate 3); API-shape and
generated-world checks ARE check.ps1 gates 8-9; browser and asset checks are the
smoke suite (gate 4). The one superset element outside the default battery is the
clean-path check: `tools/verify-full.ps1 -IncludeCleanClone` runs
`tools/clean-clone-check.ps1` for events that require an isolated no-sibling clone
proof (e.g., the MapLab-retirement checkpoint, where the roadmap already mandates it).

## Consequences

- One command per tier; no bespoke battery per checkpoint.
- A Fast pass must never be reported as green; scripts print the restriction.
- Gates are added to, never weakened; the 239-test pin stays as the tripwire.
