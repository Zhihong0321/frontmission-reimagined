# MechaTrader consolidation and maintainability ledger

This file is the canonical coordination record for the repository consolidation and
maintainability migration. Every coordinator, Codex subagent, AGY CLI worker, and Claude
Code worker must read this file before doing assigned work.

## Control

- Overall status: `PLANNED`
- Backup status: `IN_PROGRESS`
- Ledger owner: `/root` coordinator
- Ledger write policy: single writer; only the coordinator edits this file
- Worker policy: workers read this file and return a structured handoff to the coordinator
- Canonical ledger path: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Created: 2026-09-03
- Baseline commit: `UNSET`
- Integration branch: `UNSET`
- Last full verification: `NOT_RUN`

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
| `CLAUDE` | Claude Code 2.1.229 | `sonnet`, effort `high` | Independent architecture and regression review | `UNSPAWNED` |

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

### Wave 0 — recoverable baseline and repository consolidation

Status: `PLANNED`

This wave is serial and coordinator-owned. It must not start without explicit user
authorization.

| Job | Description | Depends on | Owner | Status |
|---|---|---|---|---|
| `W0-00` | Push recoverable RIMG and finalized MapLab snapshots to the configured GitHub repository | None | `ROOT` | `ACTIVE` |
| `W0-01` | Inventory modified and untracked files; classify source, generated, archive, and secret material | None | `ROOT` | `PLANNED` |
| `W0-02` | Run and record the existing acceptance baseline without unrelated fixes | `W0-01` | `ROOT` | `PLANNED` |
| `W0-03` | Integrate finalized MapLab frontend into the main repository without redesign | `W0-02` | `ROOT` | `PLANNED` |
| `W0-04` | Commit the recoverable consolidated baseline | `W0-03` | `ROOT` | `PLANNED` |
| `W0-05` | Verify integrated launch and parity before retiring the sibling directory | `W0-04` | `ROOT` | `PLANNED` |
| `W0-06` | Remove the verified obsolete sibling directory and explicitly approved dead folders | `W0-05` | `ROOT` | `PLANNED` |
| `W0-07` | Create worker branches and isolated worktrees | `W0-06` | `ROOT` | `PLANNED` |

Deletion gate for `W0-06`:

- Integrated files are committed.
- The committed repository can launch without the sibling folder.
- Required browser and API smoke checks pass.
- The full verification result is recorded.
- The exact deletion targets are recorded in the decision log before deletion.

### Wave 1 — parallel read-only decomposition

Status: `PLANNED`

| Job | Suggested worker | Output | Status |
|---|---|---|---|
| `W1-BACKEND` | `LUNA-A` | Backend feature-boundary and file-ownership proposal | `PLANNED` |
| `W1-FRONTEND` | `LUNA-B` | Chart and ops module-boundary proposal | `PLANNED` |
| `W1-VERIFY` | `LUNA-C` | Test dependency map and fast-check proposal | `PLANNED` |
| `W1-ASSETS` | `AGY` | Asset/generated/archive classification report | `PLANNED` |
| `W1-REVIEW` | `CLAUDE` | Independent risk and architecture review | `PLANNED` |

Wave 1 workers are read-only. The coordinator resolves disagreements and records the
approved structure before any splitting begins.

### Wave 2 — mechanical backend split

Status: `PLANNED`

Tentative work packages:

- Domain-specific definition files.
- Domain-specific view-model files.
- Feature-specific `CommandProcessor` partial files.
- Feature-specific `ViewBuilder` partial files.
- World-loading parse and validation modules.
- Balance harness reports and assertions.
- Mirrored feature-oriented test files.

All Wave 2 work must preserve public entrypoints and behavior.

### Wave 3 — mechanical frontend split

Status: `PLANNED`

Tentative work packages:

- Chart document, styling, boot sequence, camera, and input.
- Terrain, rendering, routing, HUD, and bridge modules.
- Ops shell, state, DOM helpers, and command transport.
- Individual ops page modules using the existing page/tab registry.

Native browser modules are the initial target. Adding a bundler requires a separate
decision.

### Wave 4 — AI context and repository map

Status: `PLANNED`

Tentative outputs:

- Short canonical agent instructions.
- Scoped feature notes.
- Compact domain glossary.
- Machine-readable feature ownership map.
- Generated codemap.
- Architecture decision records for durable decisions.
- Removal of duplicate onboarding and append-only status prose.

### Wave 5 — verification acceleration

Status: `PLANNED`

Tentative verification modes:

- `Fast`: compile and essential invariants.
- `Feature <name>`: feature tests plus architecture invariants.
- `Full`: existing complete acceptance suite.

### Wave 6 — behavior-aware architectural improvements

Status: `PLANNED`

This wave is intentionally separate from mechanical splitting. Candidate work includes
clearer domain names, command protocol centralization, dependency enforcement, and reduced
cross-feature signature threading. Each item requires its own approval and acceptance
criteria.

## Active jobs and path ownership

No jobs are active.

| Job | Worker | Worktree | Branch | Write scope | Started |
|---|---|---|---|---|---|
| None | — | — | — | — | — |

## Integration queue

The queue is empty.

| Order | Job | Commit | Target | Required checks | Result |
|---|---|---|---|---|---|
| — | — | — | — | — | — |

## Verification ledger

No migration verification has run.

| Date | Commit | Scope | Command | Result | Notes |
|---|---|---|---|---|---|
| — | — | — | — | `NOT_RUN` | — |

## Decision log

| ID | Date | Decision | Reason | Status |
|---|---|---|---|---|
| `D-001` | 2026-09-03 | Use one canonical single-writer ledger | Prevent worker merge conflicts and contradictory status | `ACCEPTED` |
| `D-002` | 2026-09-03 | Do not begin Wave 0 while creating this ledger | User explicitly requested planning before execution | `ACCEPTED` |
| `D-003` | 2026-09-03 | Require isolated worktrees for concurrent writers | Protect the dirty baseline and make integration reviewable | `PROPOSED` |
| `D-004` | 2026-09-03 | Consolidate the finalized MapLab frontend into the main repository | The frontend and backend form one product and require atomic changes | `PROPOSED` |
| `D-005` | 2026-09-03 | Store the pre-consolidation RIMG and MapLab snapshots as separate branches in `Zhihong0321/frontmission-reimagined` | Preserve both current trees before choosing or applying the final merged layout | `ACCEPTED` |

## Open decisions

These decisions must be resolved before their dependent jobs become `READY`:

1. Final in-repository location for the frontend: `client/` versus `web/chart/`.
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

## Current checkpoint

- Only the explicitly authorized version-control backup job has started.
- No worker has been spawned.
- No external coding CLI has been launched against the repository.
- No repository files or directories have been moved or deleted.
- Consolidation, cleanup, refactoring, and verification have not started.
