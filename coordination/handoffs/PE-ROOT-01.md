# Worker handoff: `PE-ROOT-01`

- Status: `COMPLETE`
- Worker: `ROOT` (coordinator, executed locally per `D-057`)
- Runtime/model: ZCode session coordinator (GLM-5.3-Flash), acting as ledger `ROOT`
- Branch: `codex/pe-root-01-ai-workflow`
- Base commit: `3830abd6611ae18cc05c333526b837143e4e34e5` (verified integration tip, tag `known-green/backend-split`)
- Result commit: implementation commit + this handoff commit (hashes recorded in the ledger acceptance row)

## Files changed (all new; no existing file modified)

- `docs/agents.md` — short canonical agent instructions (Phase E item 1; placed under
  `docs/` deliberately: the repo-root `CLAUDE.md`/`AGENTS.md` are locally-modified /
  untracked environment files in the main checkout that `D-057` forbids touching)
- `docs/ownership.json` — machine-readable feature ownership map, 10 features (item 2)
- `tools/Generate-Codemap.ps1` + generated `docs/codemap.md` (item 3; regenerated after
  every structural change, mandated after Phase D/F per `D-055`)
- `docs/features/core-simulation.md`, `commands-and-saves.md`, `world-and-content.md`,
  `view-and-api.md`, `frontend-chart.md`, `balance-harness.md`, `verification.md`
  (item 4; scoped feature notes)
- `docs/glossary.md` (item 5; compact domain glossary)
- `docs/decisions/0001`-`0006` (item 6; durable reasoning promoted into ADRs: Core
  purity, Fast/Full verification incl. the E9-vs-revision-§3 interpretation, repository
  local frontend, deterministic fixtures, mechanical split policy, recovery/history policy)
- `docs/agents.md` "Context discipline" + `docs/features/verification.md` carry item 7
  (stop auto-loading histories/large specs for unrelated tasks)
- `tools/verify-fast.ps1`, `tools/verify-feature.ps1`, `tools/verify-full.ps1`
  (items 8-10; Fast/feature/Full entrypoints; Full prints the six-gate battery and the
  "only a same-run Full may back a VERIFIED claim" rule; `-IncludeCleanClone` covers the
  plan-E9 clean-path superset element)
- `coordination/handoffs/PE-ROOT-01.md` (this file)

## Checks run

| Command | Result | Evidence |
|---|---|---|
| `PSParser` tokenize of the 4 new `.ps1` scripts | `PASS` | 0 syntax errors each |
| `ConvertFrom-Json` on `docs/ownership.json` | `PASS` | valid JSON |
| `tools/Generate-Codemap.ps1` | `PASS` | wrote `docs/codemap.md` (284 lines, commit `3830abd`) |
| `tools/verify-fast.ps1` (Fast) | `PASS` | Release build exit 0, 0 warnings; disclaimer banner printed |
| `tools/verify-feature.ps1 -List` | `PASS` | 22 features enumerated |
| `tools/verify-feature.ps1 -Feature architecture` | `PASS` | build 0 warnings; ArchitectureTests green |

## Behavior changes

`NONE` — every changed path is a new file under `docs/`, `tools/` (new scripts only),
and `coordination/handoffs/`. No `src/` or `tools/` existing semantics touched, no
`data/`, no `web/chart/` bytes, no test files, no `check.ps1` change, no `FIGURES.md`.

## Risks and uncertainty

- `tools/verify-full.ps1` had not been executed end-to-end at handoff time (Full is
  integration-only); its first run is part of the integration battery per the CP-E1
  authorization. Syntax-checked and modeled on `check.ps1`/`clean-clone-check.ps1` idioms.
- The Full gate-3 zero-diff check scopes `git status --porcelain -- tests/`; if a future
  fixture lived outside `tests/` this would need widening.

## Out-of-scope findings

- Root `CLAUDE.md` still references `D:\FrontMission-MapLab\chart.html` as the live map
  in places (pre-Phase-B wording); it is outside this job's write scope and locally
  modified in the main checkout, so it was left untouched. `docs/agents.md` now states
  the current repository-local reality.
- `tools/` contains ~35 historical `.png` screenshots from earlier debugging sessions;
  candidate inventory for the CP-F1 cleanup checkpoint (recorded, not acted on).

## Requested ledger update

`PE-ROOT-01` `REVIEW -> MERGED -> VERIFIED` after the integration Full battery; then
tag `known-green/ai-workflow` at the integration merge commit; next checkpoint CP-D1
remains unauthorized.
