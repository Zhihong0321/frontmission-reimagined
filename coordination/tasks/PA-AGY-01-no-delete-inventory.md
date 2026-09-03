# Task packet: `PA-AGY-01` — no-delete inventory and path audit

## Control

- Status: `ACTIVE`
- Worker: `AGY`
- Runtime: AGY CLI 1.1.25
- Required model: `gemini-3.8-flash-high`
- Required effort: `high`
- Green base commit: `7f8897c15f5ab3b17dbe522e0e474af046a766e9`
- Branch: `codex/pa-agy-01-inventory`
- Worktree: `D:\FrontMission-RIMG-worktrees\PA-AGY-01`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`

Do not begin unless this task is `READY` or `ACTIVE` in the canonical ledger and the
recorded owner matches this packet. The worker branch begins at the coordination-only
assignment commit containing this packet; the product green base above is its parent.

## Objective

Produce a source-grounded Phase A inventory of source, generated output, archives, assets,
path discovery, and potential secret-bearing files across the RIMG worktree and the
read-only sibling `D:\FrontMission-MapLab`. This is an evidence report only: do not delete,
move, copy, regenerate, format, or edit product files, and do not make cleanup decisions.

## Evidence and context to read

1. Read the canonical plan completely.
2. Read the canonical ledger completely.
3. Read only these additional control/evidence files first:
   - `D:\FrontMission-RIMG\coordination\README.md`
   - `D:\FrontMission-RIMG\coordination\handoffs\PA-KIMI-01.md`
   - `D:\FrontMission-RIMG\coordination\handoffs\PA-CURSOR-01.md`
   - `.gitignore` from both repositories
4. Then inspect repository files as needed to produce the report.

There is no applicable `AGENTS.md` in either repository at assignment time. If one
appears, read it before inspecting files under its scope.

## Allowed write scope

- `coordination/reports/PA-AGY-01-inventory.md`
- `coordination/handoffs/PA-AGY-01.md`
- `coordination/runs/PA-AGY-01/**`

## Prohibited write scope

- `MIGRATION_PLAN.md`
- `MIGRATION_LEDGER.md`
- Every source, test, data, asset, generated-output, launcher, and sibling MapLab file
- Anything not explicitly listed under allowed write scope

## Required report schema

Write `coordination/reports/PA-AGY-01-inventory.md` with these sections and evidence:

1. **Snapshot identity** — exact RIMG branch/HEAD/status, exact MapLab branch/HEAD/status,
   tool versions used, and audit timestamp.
2. **Top-level classification** — tracked/untracked/ignored source, generated output,
   archives/backups, assets, logs/transcripts, and ambiguous items, including file counts
   and aggregate byte sizes. Do not enumerate dependency/build caches file-by-file.
3. **Generated-output map** — each generator, its inputs, outputs, whether outputs are
   tracked/ignored/present, consumers, stale-output risk, and exact file/line evidence.
4. **Runtime path-discovery map** — every upward walk, sibling lookup, absolute path,
   working-directory assumption, and environment-derived product path found in launchers,
   host, tests, and generators. Record source file, line, resolved current path, fallback
   behavior, and Phase A/B relevance.
5. **Asset reconciliation** — manifest-declared, HTML/CSS/JS literal, and computed asset
   references compared with files on disk. Separate confirmed missing, confirmed referenced,
   and orphan *candidates*; computed/dynamic uncertainty must be explicit. Hash/size exact
   duplicate candidates, but do not declare them safe to delete.
6. **Archive/dead-output candidates** — evidence for each candidate, Git recoverability,
   runtime/reference hits, and confidence. Label all as candidates pending later approval.
7. **Secrets hygiene** — names/paths and finding categories only. Never print secret values.
   Distinguish tracked, untracked, and ignored files; flag suspicious tracked content for
   coordinator review using redacted fingerprints or key names only.
8. **Required gates and follow-ups** — ranked no-delete recommendations for remaining
   Phase A evidence and later Phase B path/provenance checks.
9. **No-delete attestation** — exact statement that no product or sibling file was changed,
   generated, moved, or deleted, supported by before/after Git status for both repositories.

Use tables where they make the inventory auditable. Prefer `rg`, `git ls-files`,
`git status --ignored`, and hash/size metadata. Never open or echo the contents of known
secret files; inspect names, Git classification, and redacted match metadata only.

## Non-goals

- Do not decide or perform cleanup, quarantine, consolidation, or refactoring.
- Do not regenerate `world.js`, `FIGURES.md`, screenshots, or any asset.
- Do not install dependencies or run builds/tests/application launchers.
- Do not edit `D:\FrontMission-MapLab` or remove any directory.
- Do not start Phase B.

## Required checks

1. Record before/after `git status --short --branch` in both repositories.
2. `git diff --check` — pass for the report/handoff.
3. Verify `git diff --name-only` contains only allowed report/handoff/run paths.
4. Commit the report and handoff on the assigned branch.

## Stop conditions

Stop and return `BLOCKED` without expanding scope if:

- A required write falls outside allowed paths.
- The exact green product base cannot be confirmed as an ancestor of branch HEAD.
- Either repository is unexpectedly dirty before inspection.
- Inspection would require reading or exposing a secret value.
- Any command would generate, format, move, or delete a product/sibling file.
- Required checks remain red after two focused repair attempts.

## Deliverables

- One report-and-handoff commit on the assigned branch.
- No product changes and no unrelated formatting or cleanup.
- A handoff at `coordination/handoffs/PA-AGY-01.md` using
  `coordination/HANDOFF_TEMPLATE.md`, committed with the report.
