# Task packet: `PC-ROOT-01` — mechanical split of `Definitions.cs`

## Control

- Status: `ACTIVE`
- Worker: `ROOT`
- Runtime: current coordinator acting in an isolated worker worktree
- Green integration base: `6b14d192858bb15bbb5de946d14c353ccfc9f9f8`
  (Phase B verified integration tip; tag `known-green/consolidated` at `590b25c`)
- Branch: `codex/pc-root-01-definitions`
- Worktree: `D:\FrontMission-RIMG-worktrees\PC-ROOT-01`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`
- Read-only sibling: `D:\FrontMission-MapLab`

Do not begin unless this task is `ACTIVE` in the canonical ledger and assigned to ROOT.

## Objective

Perform only the bounded Phase C mechanical backend decomposition item 1:

1. Split `src/MechaTrader.Core/Model/Definitions.cs` into cohesive new `.cs` files
   under `src/MechaTrader.Core/` by moving whole type declarations, unchanged.
2. Preserve namespaces, names, signatures, ordering, visibility, and public
   entrypoints exactly. No semantic cleanup, rename, refactor, or behavior change.
3. Confirm the `.csproj` uses wildcard compile inclusion (default SDK globbing) so
   no project-file change is needed.

No other Phase C item (`ViewModels.cs`, `WorldLoader.cs`, `ViewBuilder.cs`,
`CommandProcessor.cs`, balance harness, oversized test classes), no Phase D-F work,
and no product/data/test change is authorized.

## Required base identity

Before editing, verify all of the following and stop if any differs unexpectedly:

1. Local and remote `master` resolve to `4c22e94fcce082e3aa854d481b815ec34db0388d`.
2. Local and remote `integration` resolve to `6b14d192858bb15bbb5de946d14c353ccfc9f9f8`.
3. Tag `known-green/consolidated` (annotated `e31ceb71e5e87ce6b29ec4baab661bb14bc3fe23`)
   points at `590b25c808951d1fb3cb94bb3fa6bb17bb479d5f`.
4. `master:MIGRATION_LEDGER.md` and `integration:MIGRATION_LEDGER.md` are the same
   blob `8df31697c73c9b24a77ace8f8044fcfc19fa9fe8`.
5. Phase B product merges `ec7cc79`, `b108789`, `590b25c` are ancestors of
   `integration`; ledger records `D-040`/`D-041`/`D-042` exist; Phase B steps 1-11
   are `VERIFIED`.
6. `D:\FrontMission-MapLab` remains on `backup/maplab-final-20260903` at
   `df3c1baa8a83c2412607353af9994170b988dbe3` with status exactly ` M world.js`.
   Do not mutate that directory.

## Exact allowed write scope

- `src/MechaTrader.Core/Model/Definitions.cs` (remove after the split)
- New `.cs` files created by the split, only under `src/MechaTrader.Core/`
- `coordination/handoffs/PC-ROOT-01.md`

## Required implementation

- Read the whole `Definitions.cs` and group its type declarations by cohesive
  boundary (for example goods/tiers, quality/warehouse, terrain, vehicles,
  upgrades, gear, industry, economy/game config, crew, city stats, standing,
  contracts, events, expos) into new files with the same `namespace
  MechaTrader.Core.Model;` declaration.
- Copy each type declaration byte-for-byte; do not reformat, reorder members,
  alter comments, or touch method bodies.
- Keep the exact relative order of declarations across the new files (the file
  split must not reorder the types).
- Delete `Definitions.cs` afterwards (the split itself, not a separate move).
- Do not change any other file. `MechaTrader.Core.csproj` must need no edit
  because the SDK default wildcard already compiles every `*.cs` under the
  project directory.
- `git diff --check` must be clean.

## Required worker checks

1. Pinned base/sibling identity and ancestry checks above.
2. Byte-level equivalence proof: concatenating the new files' type declarations
   reproduces the original `Definitions.cs` content exactly (aside from the
   per-file `namespace` lines), or an equivalent mechanical equivalence check.
3. `git diff --check` in the worktree.
4. `dotnet build MechaTrader.sln -c Release --nologo -v q` with zero warnings and
   zero errors.
5. Full, unfiltered `dotnet test` on the `MechaTrader.Core.Tests` project
   (239+ cases all green).
6. Determinism/save-compatibility fingerprint tests via
   `tools/MechaTrader.Fingerprint` (deterministic state and saved-fixture tests).
7. `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\verify-worldjs.ps1`
   — generated `web/chart/world.js` SHA-256 must remain
   `26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a`.
8. Full nine-gate acceptance:
   `powershell -NoProfile -ExecutionPolicy Bypass -File .\check.ps1`.
   If BalanceSim is red only from load contention, re-run it isolated once before
   judging.
9. Run the checks serially (never concurrently with another suite) to avoid
   BalanceSim contention.

## Stop conditions

Stop `BLOCKED` without expanding scope if:

- Any pinned base/source identity differs unexpectedly.
- The split requires touching a prohibited path (data, chart generator/output,
  sibling MapLab, tests, other product files) or changing public API/behavior.
- Any required check remains red after two focused repair attempts.

## Deliverables

- One bounded implementation commit on `codex/pc-root-01-definitions` with a
  message like `Split Definitions.cs mechanically`.
- Structured handoff at `coordination/handoffs/PC-ROOT-01.md`.
- No sibling mutation, no data/chart/test change, no deletion beyond the split,
  no tag, no Phase C item 2+ work, no Phase D-F work.
