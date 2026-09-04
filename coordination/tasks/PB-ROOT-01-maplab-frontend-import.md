# Task packet: `PB-ROOT-01` — byte-for-byte MapLab frontend import

## Control

- Status: `ACTIVE`
- Worker: `ROOT`
- Runtime: current Codex coordinator acting in an isolated worker worktree
- Product green base commit: `5ed5949` (`known-green/original`)
- Branch: `codex/pb-root-01-maplab-import`
- Worktree: `D:\FrontMission-RIMG-worktrees\PB-ROOT-01`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`
- Read-only source: `D:\FrontMission-MapLab`

Do not begin unless this task is `ACTIVE` in the canonical ledger and assigned to ROOT.

## Objective

Perform only the first bounded Phase B job: import the finalized MapLab frontend into
`web/chart/` while preserving every selected source file byte-for-byte and preserving
its relative path below the MapLab root. This job stages the consolidated copy only. It
must not make the host or launcher use that copy yet.

## Required source identity

Before copying, verify all of the following and stop if any differs:

1. `D:\FrontMission-MapLab` exists and is a Git worktree.
2. Its branch is `backup/maplab-final-20260903` and its `HEAD` is exactly
   `df3c1baa8a83c2412607353af9994170b988dbe3`.
3. Its only tracked worktree delta is `world.js`, matching the previously authorized
   regeneration recorded in `D-030`: the generated comment uses
   `D:/FrontMission-RIMG/data` instead of `D:\FrontMission-RIMG\data`.
4. The live finalized `world.js` is 8,590 bytes with SHA-256
   `6680509cd8cbacc72ab3b8060efd4b8c7d3c328f8646aaeb78ddb1531c3d135c`.
5. `web/chart/` does not already exist in the assigned worktree.

The live sibling checkout is the byte source because its sole delta is the explicitly
authorized regenerated `world.js` used by the final Phase A verification. The remote
recovery commit remains the identity anchor for every other selected file.

## Exact import set

Copy these eight files from the MapLab root to the same relative paths below
`web/chart/`:

- `_ops-test.html`
- `chart-tiles-worker.js`
- `chart.html`
- `game-bridge.js`
- `ops.css`
- `ops.js`
- `opstest.html`
- `world.js`

Copy every file and directory below `D:\FrontMission-MapLab\art\` to
`web/chart/art/` with identical relative paths and bytes. The expected selected source
set is exactly 403 files totaling 293,783,792 bytes: eight root files plus 395 files
under `art/`.

Do not copy `.gitignore`, `Generator.cmd`, `Map.cmd`, `README.md`, `make-world.js`,
`map-design-sop.md`, or anything under `generator/`; those are outside this frontend-only
job. Empty directories are not deliverables.

## Allowed write scope

- `.gitattributes` — create only one scoped rule for `/web/chart/**` when needed to
  disable Git text conversion and preserve raw source bytes in committed blobs
- `web/chart/**` — new files only, exactly the import set above
- `coordination/handoffs/PB-ROOT-01.md`

## Prohibited write scope

- `D:\FrontMission-MapLab\**` — read-only; no edits, regeneration, cleanup, checkout,
  reset, stash, index operation, branch operation, or other mutation
- `src/**`, `play.ps1`, `check.ps1`, `tests/**`, `tools/**`, `data/**`, and all other
  existing product or verification files
- `MIGRATION_PLAN.md`, `MIGRATION_LEDGER.md`, and every coordination file except this
  job's handoff
- Any deletion, move, rename, refactor, formatting change, generated-output refresh, or
  runtime-path/configuration switch
- Phases C-F and all later Phase B jobs

## Required procedure

1. Read `MIGRATION_PLAN.md`, `MIGRATION_LEDGER.md`, and this task packet completely from
   the assigned worktree.
2. Record the source Git status and source identity without changing them.
3. Enumerate the exact source set defined above by relative path. Capture its file count,
   total bytes, and raw SHA-256 per file before copying.
4. Add the scoped `/web/chart/** -text` Git attribute so `git add` cannot normalize text
   bytes. Do not add any broader attribute.
5. Copy the exact source set to `web/chart/` without transformations.
6. Compare source and destination relative-path sets, lengths, and raw SHA-256 values.
7. Stage the permitted files and verify each staged Git blob hash equals
   `git hash-object --no-filters` of the corresponding live source file. This proves the
   committed content, not only the current working-tree copy, is byte-identical.
8. Confirm no runtime source points at `web/chart/` as a result of this job and the
   existing sibling lookup paths remain unchanged.
9. Confirm the sibling source status and raw hashes are identical to the pre-copy record.
10. Commit the import and handoff on the assigned branch.

## Required checks

1. Source and destination each contain exactly 403 selected files and total
   293,783,792 bytes.
2. Relative-path set comparison: no missing or extra imported files.
3. Raw SHA-256 comparison for all 403 files: every source hash equals its destination
   hash.
4. Staged/committed blob comparison for all 403 files: every Git blob equals the raw
   source bytes with filters disabled.
5. `git diff --check` passes.
6. `git diff --name-only` is confined to `.gitattributes`, `web/chart/**`, and
   `coordination/handoffs/PB-ROOT-01.md`.
7. `git diff` confirms `src/MechaTrader.Host/Program.cs`, `play.ps1`, and every existing
   runtime/configuration file are unchanged.
8. `D:\FrontMission-MapLab` remains on the same branch and commit with the same sole
   `world.js` delta and no source hash changes.

Do not run browser or full application acceptance as evidence that the imported copy is
active: the existing host still serves the sibling MapLab tree by design in this first
job. The coordinator will review the import mechanically before a later, separately
bounded Phase B job can relocate the generator or switch runtime paths.

## Stop conditions

Stop `BLOCKED` without expanding scope if:

- Source identity, status, count, total bytes, or the pinned `world.js` hash differs.
- Any destination file cannot be made byte-identical in the Git object database.
- `web/chart/` already exists or the import would overwrite an existing file.
- Passing requires editing a runtime path, generator, test, product file, or sibling file.
- Any required check remains red after two focused repair attempts.

## Deliverables

- One bounded import commit on `codex/pb-root-01-maplab-import`.
- Exact selected source files at `web/chart/`, with raw committed bytes preserved.
- Structured handoff at `coordination/handoffs/PB-ROOT-01.md` using the ledger schema.
- No runtime behavior change and no sibling-source modification.
