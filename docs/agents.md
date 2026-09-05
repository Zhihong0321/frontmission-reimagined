# Agent instructions (canonical)

Short startup instructions for any agent working in this repository. Read this file
first; load nothing else until the task needs it. The fuller brief lives in the root
`CLAUDE.md`; exact formulas and schema live in `SPEC.md`. Neither is required for most
tasks — start from the ownership map and the one feature note for the area you touch.

## Read order

1. This file.
2. `docs/ownership.json` — which paths, tests, and entrypoints belong to which feature.
3. The one `docs/features/<feature>.md` note for the area you are changing.
4. `docs/glossary.md` when a domain term is unfamiliar.
5. `docs/codemap.md` for a generated inventory of the code (never hand-edit it; see
   "Regenerate, don't hand-edit" below).
6. `docs/decisions/` when you need to know *why* a rule exists.

Do not open `MIGRATION_LEDGER.md`, `coordination/`, `BRAIN.md`, `NIGHT_LOG.md`, or
`SPEC.md` unless the task is coordination, the AI policies, history, or economy math
respectively. Chat histories are never a source of truth; do not load them.

## The rules that must always hold

- `MechaTrader.Core` is a pure simulation library: no I/O, no clock, no randomness
  outside the seeded RNG. Every front-end is a view over it and owns no rule.
- State changes only through `CommandProcessor` (`game.Apply`). A rejected command
  leaves state untouched.
- Seed + command sequence produces identical state. Views and reads never touch the RNG.
- All game content lives in `data/` as JSON; nothing content-shaped is hardcoded in C#.
- Money is `long`, stock is `double`; round once at settlement.
- Numbers live in `FIGURES.md` (generated). Never quote a figure from memory.
- No product behavior change without an explicitly authorized job that names it.

## Verification contract

| Mode | Command | May certify |
|---|---|---|
| Fast (iteration aid) | `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-fast.ps1` | Nothing. Zero-warning Release build plus optional affected tests. Its output says so. |
| Feature (iteration aid) | `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-feature.ps1 -Feature <name>` (`-List` enumerates) | Nothing. One feature's targeted checks. |
| Full (the only basis for any green/finished claim) | `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-full.ps1` | A green claim for the exact tree it ran on, recorded with evidence. |

`Full` is the six gates: zero-warning Release build; the complete nine-gate
`check.ps1`; `tools/MechaTrader.Fingerprint` regeneration with zero tracked fixture
diff and pinned `F_state`/`F_view`; browser smoke 1/1; clean `git diff --check`;
hygiene (port 5080 free, no `MechaTrader.Host` process, `FIGURES.md` timing-line-only
restored, temp directories baseline-compare plus this-run cleanup). `check.ps1` alone
is the acceptance suite but is not the whole battery. A Fast or feature check that
passes must never be reported as "green" or "done".

## Regenerate, don't hand-edit

- `docs/codemap.md` is produced by `tools/Generate-Codemap.ps1` from repository facts.
  After any structural change (files added/moved/split, projects changed), regenerate
  it with that one command and commit the result.
- `FIGURES.md` is produced by `tools/MechaTrader.BalanceSim`. After any content or
  economy change, run the harness; a check run rewrites it with a timing-line-only
  diff that must be restored, never committed.
- `web/chart/world.js` is generated from `data/` by `web/chart/make-world.js`;
  `tools/verify-worldjs.ps1` proves it deterministic. Never edit `world.js` by hand.

## Guardrails

- Do not create or move Git tags, rewrite history, or force push. Recovery tags
  (`known-green/*`, `backup-*`) are immutable.
- `D:\FrontMission-MapLab` is a frozen sibling reference; do not touch it.
- The archived UI (`web/archive/`) is dead code; do not revive or extend it.
- If your task seems to require work outside the paths the ownership map assigns to it,
  stop and report the expansion request instead of editing.

## Where things live

| Path | What it is |
|---|---|
| `src/MechaTrader.Core/` | The pure simulation (Model content DTOs, Sim rules, State, Commands, View DTOs, World loading, Ai policies) |
| `src/MechaTrader.Content/` | The only project that touches the filesystem (reads `data/`, build info) |
| `src/MechaTrader.Host/` | Thin ASP.NET adapter: 5 endpoints + static files; owns no rule |
| `web/chart/` | The player view: `chart.html` (Keeper's Chart), `game-bridge.js`, the ops shell (`ops.js`/`ops.css`), tile worker, generated `world.js` |
| `data/` | All game content (15 JSON files) |
| `tests/MechaTrader.Core.Tests/` | The Core suite incl. determinism/save fixtures and architecture tests |
| `tests/browser/` | Playwright smoke suite |
| `tools/` | Balance harness, Fingerprint regenerator, verifiers, the Fast/feature/Full entrypoints, the codemap generator |
| `check.ps1` | The nine-gate acceptance suite (exit code is the answer) |
| `FIGURES.md`, `VERSION` | Generated economy figures; the one place the version string lives |
