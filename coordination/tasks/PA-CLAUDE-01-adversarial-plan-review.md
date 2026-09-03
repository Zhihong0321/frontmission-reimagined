# Task packet: `PA-CLAUDE-01` — adversarial migration-plan review

## Control

- Status: `READY`
- Worker: `CLAUDE-DESKTOP`
- Runtime: Claude Desktop
- Required model: strongest available Sonnet-class model
- Required effort: high
- Job type: read-only preflight analysis
- Product baseline commit: `752e5fad7b8e945e9eb3342adabc78c70d95a3c5`
- Branch: none
- Worktree: none required
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`

## Objective

Adversarially review the physical migration plan for failure modes that could create a
long-lived half-migrated project, false confidence, wasted agent work, or a forced restart
from the original baseline.

## Files the user will provide

- `MIGRATION_PLAN.md`
- `MIGRATION_LEDGER.md`
- This task packet
- `check.ps1`
- `src/MechaTrader.Host/Program.cs`
- `play.ps1`
- MapLab `chart.html`
- MapLab `game-bridge.js`
- MapLab `ops.js`

## Write scope

If filesystem access is available, only
`D:\FrontMission-RIMG\coordination\handoffs\PA-CLAUDE-01.md` may be created. Otherwise
return the handoff text to the user. Do not produce product patches or rewrite any supplied
file.

## Required output

Return one concise review containing:

1. Only material remaining risks not adequately controlled by plan version 2.
2. Unsafe dependencies or ordering between phases.
3. Ways parallel workers could produce semantically incompatible but merge-clean commits.
4. Missing rollback, observability, browser, deterministic, save, asset, or clean-clone
   gates.
5. Specific plan changes required before Phase A or Phase B may start.
6. A final verdict: `SAFE_TO_BEGIN_PHASE_A` or `REVISE_BEFORE_START`.

Format the result using `coordination/HANDOFF_TEMPLATE.md` with result commit `NONE`. Save
it to `coordination/handoffs/PA-CLAUDE-01.md` when filesystem access is available.

## Stop conditions

- Do not redesign the game architecture.
- Do not suggest new gameplay or visual work.
- Do not repeat controls that are already sufficient unless identifying a concrete gap.
- Do not assume revertability alone is an adequate safety mechanism.
