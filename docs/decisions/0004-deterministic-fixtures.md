# ADR 0004: Determinism is pinned by fixtures, not by hope

- Status: Accepted (Phase A, `MIGRATION_LEDGER.md` `D-015`/`D-016`; exceptions
  `D-050`)
- Date: 2026-09-05 (promoted into ADR form by CP-E1)
- Context: Mechanical refactoring (moving code between files) can silently change
  iteration order, RNG consumption, JSON names, defaults, or record construction —
  and with them simulation output and save compatibility. Tests that only assert
  "reasonable behavior" cannot catch that.

## Decision

The simulation's observable determinism surface is pinned four ways:

1. **State and view fingerprints** — `F_state` (`a96681c1...be99`) and `F_view`
   (`93a94b5c...6626`), asserted by `DeterminismFingerprintTests` and re-proven on
   every Full run by regenerating with `tools/MechaTrader.Fingerprint` and requiring
   zero tracked diff.
2. **Save fixtures** — representative serialized saves that must round-trip
   byte-identically (`SaveFixtureTests`).
3. **API shape/value baselines** — raw response bytes for a fixed seed + command
   script under `tests/api-fixtures/` (`/api/build` is shape-checked only: its commit
   log and wall-clock `builtAgo` genuinely vary).
4. **Generated world verification** — the `world.js` payload and full-file hashes
   (see ADR 0003).

A 21/21 command-coverage matrix states which command types the fingerprints protect
and which only the full Core suite protects.

## Consequences

- Any behavioral change to command processing, saves, world generation, or views
  shows up as a red fixture — the change is then either wrong or requires an explicit
  authorized re-baseline.
- Re-baselining happens only via the Fingerprint tool / `-Record` mode, never by
  hand-editing hex strings or JSON, and only as its own approved job.
- Known, bounded exception: `/api/build`'s `build.json` dynamic metadata (approved
  `D-050`); everything else stays byte-stable.
