# MechaTrader consolidation and maintainability ledger

This file is the canonical coordination record for the repository consolidation and
maintainability migration. Every coordinator, Codex subagent, AGY CLI worker, and Claude
Code worker must read this file before doing assigned work.

The durable execution plan and risk controls are in `MIGRATION_PLAN.md`. This ledger owns
live state; the plan owns process. Chat is not a source of truth.

## Control

- Overall status: `PLANNED`
- Backup status: `VERIFIED`
- Ledger owner: `/root` coordinator
- Canonical plan path: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`
- Coordination directory: `D:\FrontMission-RIMG\coordination`
- Ledger write policy: single writer; only the coordinator edits this file
- Worker policy: workers read this file and return a structured handoff to the coordinator
- Canonical ledger path: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Created: 2026-09-03
- RIMG baseline commit: `29de90387bb2d8fcccf5d6b787def5edac2ca923`
- RIMG recovery tag: `backup-rimg-20260903`
- MapLab baseline commit: `df3c1baa8a83c2412607353af9994170b988dbe3`
- MapLab recovery branch: `backup/maplab-final-20260903`
- MapLab recovery tag: `backup-maplab-20260903`
- GitHub repository: `https://github.com/Zhihong0321/frontmission-reimagined`
- Integration branch: `UNSET`
- Last full verification: `NOT_RUN`

## Disk-first policy

Before dependent work begins, the coordinator must write every material plan, decision,
assignment, status change, scope change, verification result, integration result, and
rollback point to this ledger or `MIGRATION_PLAN.md`.

Workers must treat chat as notification only and re-read the physical files before work.

Creating this ledger did not authorize migration work. The user subsequently authorized
the version-control backup job only. No migration or refactor job may start until its
status is `READY` and the coordinator explicitly assigns it to a named worker.

## Intended final state

1. `D:\FrontMission-RIMG` is the single product repository.
2. The finalized player frontend from `D:\FrontMission-MapLab` is integrated into it.
3. The old `D:\FrontMission-MapLab` directory is removed only after the integrated copy
   is committed, launches successfully, and passes the required verification.
4. Dead archives, rejected experiments, and generated screenshots are removed from the
   active source tree or ignored as appropriate.
5. Backend and frontend behavior remain unchanged during mechanical file splitting.
6. New work can be performed from a small feature-specific context instead of requiring
   the complete project history.

## Non-negotiable invariants

- `MechaTrader.Core` remains a pure deterministic simulation library.
- State changes happen only through the command-processing boundary.
- Rejected commands leave state unchanged.
- Seed plus command sequence produces identical state.
- The frontend displays simulation results and does not invent game rules.
- Existing save/resume behavior remains compatible unless a separately approved job says
  otherwise.
- No source directory is deleted before its replacement is committed and verified.
- Refactoring jobs do not introduce product features or balancing changes.
- Workers do not modify files outside their assigned write scope.
- Workers do not edit this ledger.

## Status vocabulary

| Status | Meaning |
|---|---|
| `NOT_STARTED` | Migration is not authorized to run. |
| `PLANNED` | Work is described but may not start. |
| `READY` | Dependencies are satisfied and the coordinator may assign it. |
| `ACTIVE` | One named worker owns the job and its write scope. |
| `REVIEW` | Worker finished; commit and evidence await coordinator review. |
| `MERGED` | Coordinator integrated the job commit. |
| `VERIFIED` | Required checks passed after integration. |
| `BLOCKED` | Work cannot proceed; the reason is recorded. |
| `CANCELLED` | Work is intentionally abandoned. |

Allowed normal transition:

`PLANNED -> READY -> ACTIVE -> REVIEW -> MERGED -> VERIFIED`

Only the coordinator changes job status.

## Agent roster

| Agent ID | Runtime | Requested configuration | Primary role | State |
|---|---|---|---|---|
| `ROOT` | Codex coordinator | Current frontier model | Architecture, assignments, integration, destructive decisions | `IDLE` |
| `LUNA-A` | Codex subagent | `gpt-5.6-luna`, effort `high` | Mechanical backend work | `UNSPAWNED` |
| `LUNA-B` | Codex subagent | `gpt-5.6-luna`, effort `high` | Mechanical frontend work | `UNSPAWNED` |
| `LUNA-C` | Codex subagent | `gpt-5.6-luna`, effort `high` | Tests, tooling, generated documentation | `UNSPAWNED` |
| `AGY` | AGY CLI 1.1.25 | `gemini-3.8-flash-high`, effort `high` | Repetitive inventory and migration tasks | `UNSPAWNED` |
| `KIMI` | Kimi CLI 0.39.1 | configured default `cmkey/kimi-k3` | Bounded implementation and independent review | `UNSPAWNED` |
| `CURSOR` | Cursor 3.18.25 | requested Grok 4.6; exact CLI selection unverified | User-relayed IDE work until CLI invocation is verified | `AVAILABLE_MANUAL` |
| `CLAUDE` | Claude Code 2.1.229 | `sonnet`, effort `high` | Independent architecture and regression review | `UNSPAWNED` |
| `CLAUDE-DESKTOP` | Claude Desktop | user-selected model | User-relayed review or bounded implementation | `AVAILABLE_MANUAL` |

The exact model resolved by the Claude `sonnet` alias must be recorded when the first
Claude job is launched.

## Concurrency rules

1. The coordinator plus at most three Codex subagents may occupy the internal agent pool.
2. AGY and Claude Code may run as external processes, but they follow the same ownership
   rules.
3. At most three workers may write concurrently.
4. Every writing worker uses a dedicated Git worktree and branch.
5. Read-only auditors may run concurrently with writers.
6. Two active jobs may not own the same file or directory.
7. Only the coordinator cherry-picks or merges worker commits.
8. Only one build or full acceptance run should execute at a time unless isolated output
   directories have been configured.
9. Generated files have one designated owner per wave.
10. If a worker discovers required work outside its scope, it stops that part and reports
    an expansion request instead of editing the file.

## Worker start protocol

Every worker prompt must begin with these requirements:

1. Read `D:\FrontMission-RIMG\MIGRATION_LEDGER.md` completely.
2. Confirm the assigned job ID, worktree, branch, and write scope.
3. Confirm the job is `READY` or `ACTIVE` and assigned to that worker.
4. Inspect relevant local instructions for files in scope.
5. Do not edit the canonical ledger.
6. Do not modify anything outside the declared write scope.
7. Preserve behavior unless the job explicitly authorizes a behavior change.
8. Run the job's required targeted checks.
9. Commit the completed work to the assigned branch.
10. Return the required structured handoff.

## Required worker handoff

Each worker must finish with this exact information:

```text
JOB_ID:
STATUS: COMPLETE | BLOCKED | FAILED
BRANCH:
COMMIT:
FILES_CHANGED:
CHECKS_RUN:
CHECK_RESULTS:
BEHAVIOR_CHANGES: NONE | description
RISKS:
OUT_OF_SCOPE_FINDINGS:
LEDGER_UPDATE_REQUEST:
```

The coordinator verifies the commit and copies the relevant information into this ledger.

## Planned waves

`MIGRATION_PLAN.md` version 2 is authoritative for wave contents and safety gates.

| Phase | Purpose | Depends on | Status |
|---|---|---|---|
| `BACKUP` | Remote recovery snapshots for both current folders | None | `VERIFIED` |
| `A` | Establish known-green original, browser safety net, deterministic and save fixtures | `BACKUP` | `PLANNED` |
| `B` | Consolidate into the main repository without deleting the original MapLab folder | `A` | `PLANNED` |
| `C` | Mechanical backend decomposition, one original large file per checkpoint | `B` | `PLANNED` |
| `D` | Mechanical classic-script frontend decomposition with browser checks after each step | `B` | `PLANNED` |
| `E` | AI context files, generated codemap, scoped documentation, and verification modes | `C`, `D` | `PLANNED` |
| `F` | Independent cleanup commits and final retirement of the sibling MapLab directory | `E` | `PLANNED` |

Phase F is the only phase allowed to delete the original MapLab directory. ES-module
conversion, semantic backend redesign, Git history rewriting, and LFS migration are not
part of this plan.

## Active jobs and path ownership

No jobs are active.

| Job | Worker | Worktree | Branch | Write scope | Started |
|---|---|---|---|---|---|
| None | — | — | — | — | — |

## Ready manual advisory jobs

These jobs are read-only, require no worktree, and may run concurrently. Their results do
not authorize migration work.

| Job | Worker | Physical task packet | Status |
|---|---|---|---|
| `PA-CURSOR-01` | Cursor Grok 4.6 | `coordination/tasks/PA-CURSOR-01-browser-safety.md` | `READY` |
| `PA-CLAUDE-01` | Claude Desktop | `coordination/tasks/PA-CLAUDE-01-adversarial-plan-review.md` | `READY` |
| `PA-KIMI-01` | Kimi CLI `cmkey/kimi-k3` | `coordination/tasks/PA-KIMI-01-baseline-reproducibility.md` | `READY` |

## Integration queue

The queue is empty.

| Order | Job | Commit | Target | Required checks | Result |
|---|---|---|---|---|---|
| — | — | — | — | — | — |

## Verification ledger

No migration verification has run.

| Date | Commit | Scope | Command | Result | Notes |
|---|---|---|---|---|---|
| 2026-09-03 | `29de903` | RIMG recovery snapshot | Git remote ref verification | `PASS` | Pushed as `master` and tag `backup-rimg-20260903`; application checks intentionally not run |
| 2026-09-03 | `df3c1ba` | Finalized MapLab recovery snapshot | Git remote ref verification | `PASS` | Pushed as branch `backup/maplab-final-20260903` and tag `backup-maplab-20260903` |

## Decision log

| ID | Date | Decision | Reason | Status |
|---|---|---|---|---|
| `D-001` | 2026-09-03 | Use one canonical single-writer ledger | Prevent worker merge conflicts and contradictory status | `ACCEPTED` |
| `D-002` | 2026-09-03 | Do not begin Wave 0 while creating this ledger | User explicitly requested planning before execution | `ACCEPTED` |
| `D-003` | 2026-09-03 | Require isolated worktrees for concurrent writers | Protect the dirty baseline and make integration reviewable | `PROPOSED` |
| `D-004` | 2026-09-03 | Consolidate the finalized MapLab frontend into the main repository | The frontend and backend form one product and require atomic changes | `PROPOSED` |
| `D-005` | 2026-09-03 | Store the pre-consolidation RIMG and MapLab snapshots as separate branches in `Zhihong0321/frontmission-reimagined` | Preserve both current trees before choosing or applying the final merged layout | `ACCEPTED` |
| `D-006` | 2026-09-03 | Exclude MapLab bytecode and empty generator logs from its source snapshot | They are transient runtime output; all source, metadata, and finalized art remain in the recovery branch | `ACCEPTED` |
| `D-007` | 2026-09-03 | Store durable process in `MIGRATION_PLAN.md` and live state in this ledger | Keep the ledger operational while preserving complete physical instructions | `ACCEPTED` |
| `D-008` | 2026-09-03 | Move MapLab deletion to final cleanup | Prevent false-positive verification and preserve a working reference during migration | `ACCEPTED` |
| `D-009` | 2026-09-03 | Preserve classic-script semantics during initial frontend extraction | Avoid combining file splitting with runtime module-semantics changes | `ACCEPTED` |
| `D-010` | 2026-09-03 | Keep the integration branch green and discard failed worker branches before dependent work | Prevent a half-migrated failure chain and avoid restarting the whole migration | `ACCEPTED` |
| `D-011` | 2026-09-03 | Use committed physical task packets and handoffs for every local or user-relayed worker | Make cross-tool delegation reproducible without relying on chat context | `ACCEPTED` |
| `D-012` | 2026-09-03 | Let the coordinator launch installed CLIs; use user relay only for UI-specific workers | Reduce manual task passing while preserving access to requested IDE and desktop models | `ACCEPTED` |

## Open decisions

These decisions must be resolved before their dependent jobs become `READY`:

1. Final approval of the proposed in-repository frontend location `web/chart/`.
2. Which generated and source art belongs in Git, Git LFS, or external storage.
3. Exact dead directories approved for removal in addition to the sibling MapLab folder.
4. Whether the Claude `sonnet` alias resolves to the user's intended Sonnet 5 model.
5. Whether AGY should use `gemini-3.8-flash-high` or another configured model.

## Coordinator resume procedure

At the beginning of every coordinating session:

1. Read this ledger.
2. Read Git status and the current branch without changing them.
3. Check whether any previously launched workers are still active.
4. Reconcile completed worker handoffs into the integration queue.
5. Run no job whose dependencies or ownership are unclear.
6. Update the ledger before assigning new work.
7. Tell the user which jobs will start before spawning workers.
8. Create and commit a physical task packet before every assignment.

## Current checkpoint

- The explicitly authorized version-control backup job is complete and remotely verified.
- No worker has been spawned.
- No external coding CLI has been launched against the repository.
- Three user-relayed read-only preflight task packets are ready on disk.
- No repository files or directories have been moved or deleted.
- Consolidation, cleanup, refactoring, and verification have not started.
- Recovery point `backup-rimg-20260903` preserves the current RIMG state.
- Recovery point `backup-maplab-20260903` preserves the finalized MapLab state.
