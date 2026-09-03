# MechaTrader consolidation and maintainability ledger

This file is the canonical coordination record for the repository consolidation and
maintainability migration. Every coordinator, Codex subagent, AGY CLI worker, and Claude
Code worker must read this file before doing assigned work.

The durable execution plan and risk controls are in `MIGRATION_PLAN.md`. This ledger owns
live state; the plan owns process. Chat is not a source of truth.

## Control

- Overall status: `PHASE_A_ACTIVE`
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
- Last full verification: existing seven-gate `check.ps1` `PASS` at `3530cf1cf215d29c5699720b29385c5e82af2772` (isolated integration worktree, post-`PA-ROOT-02` merge); the redesigned strict browser gate is `MERGED` and `VERIFIED` on `master` at `6cbcd23284c0d3e86f95ed9b9959bfbf66c0508b`
- Preflight advisory synthesis: `COMPLETE`
- Execution authorization after synthesis: `PHASE_A_ONLY` (user-authorized 2026-09-03; phases B-F remain unauthorized)

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
| `ROOT` | Codex coordinator | Current frontier model | Architecture, assignments, integration, destructive decisions | `IDLE_AFTER_PA-ROOT-02` (see `D-029`: Codex exhausted its usage quota mid-job; Claude Code finished and integrated the job under direct user instruction) |
| `LUNA-A` | Codex subagent | `gpt-5.6-luna`, effort `high` | Mechanical backend work | `UNSPAWNED` |
| `LUNA-B` | Codex subagent | `gpt-5.6-luna`, effort `high` | Mechanical frontend work | `UNSPAWNED` |
| `LUNA-C` | Codex subagent | `gpt-5.6-luna`, effort `high` | Tests, tooling, generated documentation | `BLOCKED_PA-LUNA-01` |
| `AGY` | AGY CLI 1.1.25 | `gemini-3.8-flash-high`, effort `high` | Repetitive inventory and migration tasks | `VERIFIED_PA-AGY-01` |
| `KIMI` | Kimi CLI 0.39.1 | configured default `cmkey/kimi-k3` | Bounded implementation and independent review | `COMPLETED_PREFLIGHT` |
| `CURSOR` | Cursor 3.18.25 | Grok 4.6 used for preflight; exact CLI selection unverified | User-relayed IDE work until CLI invocation is verified | `COMPLETED_PREFLIGHT` |
| `CLAUDE` | Claude Code | `sonnet` (self-reported resolved model: Sonnet 5) | Independent architecture and regression review; substituted for `ROOT` on `PA-ROOT-02` per `D-029` | `COMPLETED_PA-ROOT-02` |
| `CLAUDE-DESKTOP` | Claude Desktop | Sonnet 5 recorded by preflight handoff | User-relayed review or bounded implementation | `COMPLETED_PREFLIGHT` |

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
11. Phase D cannot start until Phase C is verified. The C# view/command DTO files and the
    browser bridge/ops scripts are one semantic ownership boundary even though their paths
    do not overlap.
12. Once the integration branch exists, `master` is frozen for product changes. An urgent
    master fix pauses all workers and must be brought forward and fully reverified before
    work resumes.

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

`MIGRATION_PLAN.md` version 3 is authoritative for wave contents and safety gates.

| Phase | Purpose | Depends on | Status |
|---|---|---|---|
| `BACKUP` | Remote recovery snapshots for both current folders | None | `VERIFIED` |
| `A` | Establish known-green original, browser safety net, deterministic and save fixtures | `BACKUP` | `ACTIVE` |
| `B` | Consolidate into the main repository without deleting the original MapLab folder | `A` | `PLANNED` |
| `C` | Mechanical backend decomposition, one original large file per checkpoint | `B` | `PLANNED` |
| `D` | Mechanical classic-script frontend decomposition with browser checks after each step | `C` | `PLANNED` |
| `E` | AI context files, generated codemap, scoped documentation, and verification modes | `C`, `D` | `PLANNED` |
| `F` | Independent cleanup commits and final retirement of the sibling MapLab directory | `E` | `PLANNED` |

Phase F is the only phase allowed to delete the original MapLab directory. ES-module
conversion, semantic backend redesign, Git history rewriting, and LFS migration are not
part of this plan.

## Active jobs and path ownership

Both Phase A assignments transitioned `PLANNED -> READY -> ACTIVE` on 2026-09-03 after
the user authorized Phase A. Their product green base is the coordination-only commit
`7f8897c15f5ab3b17dbe522e0e474af046a766e9`; the worker branches begin at the subsequent
assignment commit containing this ledger state and their immutable task packets.

Launch identities: `PA-LUNA-01` is Codex agent `/root/pa_luna_01`; `PA-AGY-01` is managed
AGY exec cell `17`, with its CLI log at `coordination/runs/PA-AGY-01/agy.log` in the
assigned worktree.

| Job | Status | Worker | Green base | Worktree | Branch | Write scope | Started |
|---|---|---|---|---|---|---|---|
| `PA-ROOT-02` | `VERIFIED` | `ROOT` (Claude Code completed and integrated per `D-029`) | `5e74f671bdf6925d51ccd51e0bf6bed5ac7aa98f` | `D:\FrontMission-RIMG-worktrees\PA-ROOT-02` | `codex/pa-root-02-browser-redesign` | `tests/browser/**`; `coordination/handoffs/PA-ROOT-02.md` | 2026-09-04 |

No job is currently `ACTIVE`. The next Phase A jobs (deterministic and save fixtures) are
unassigned; the browser gate is no longer the blocker for them.

## Completed manual advisory jobs

These jobs were read-only and required no worktree. Their results have been reviewed and
dispositioned; they do not by themselves authorize migration work.

| Job | Worker | Physical task packet | Handoff | Status | Disposition |
|---|---|---|---|---|---|
| `PA-CURSOR-01` | Cursor Grok 4.6 | `coordination/tasks/PA-CURSOR-01-browser-safety.md` | `coordination/handoffs/PA-CURSOR-01.md` | `VERIFIED` | `ACCEPT_WITH_MODIFICATIONS` (`D-014`) |
| `PA-CLAUDE-01` | Claude Desktop Sonnet 5 | `coordination/tasks/PA-CLAUDE-01-adversarial-plan-review.md` | `coordination/handoffs/PA-CLAUDE-01.md` | `VERIFIED` | `ACCEPT` (`D-016`) |
| `PA-KIMI-01` | Kimi CLI `cmkey/kimi-k3` | `coordination/tasks/PA-KIMI-01-baseline-reproducibility.md` | `coordination/handoffs/PA-KIMI-01.md` | `VERIFIED` | `ACCEPT_WITH_MODIFICATIONS` (`D-015`) |

## Deferred coordinator-managed jobs

These workers were reserved until all three manual advisory handoffs were synthesized.
That evidence gate is satisfied and the user has now authorized Phase A only. The jobs
below have moved to the active-jobs table with committed packets and exclusive scopes.

| Candidate job | Worker | Intended work | Release condition | Status |
|---|---|---|---|---|
| `PA-LUNA-01` | Codex `gpt-5.6-luna`, effort `high` | Implement the standalone browser smoke suite | Diagnostic branch pushed at `f94f2e05267782b2f92e18576a93480d6cb24f26`; prior integration reverted by `a6408fc` + `10b2875`; blocked handoff retained on master | `BLOCKED` |
| `PA-AGY-01` | AGY `gemini-3.8-flash-high`, effort `high` | Asset, generated-output, archive, and path-reference inventory | Report integrated as `081f42c`; secrets-scanned managed log integrated as `47ee7ce` | `VERIFIED` |

The coordinator launches both jobs. The user does not relay their prompts.

## Integration queue

| Order | Job | Commit | Target | Required checks | Result |
|---|---|---|---|---|---|
| 1 | `PA-LUNA-01` | diagnostic `f94f2e0`; rejected integration `1fc5206` + `30b2b69`; rollback `a6408fc` + `10b2875` | `master` during Phase A | Same required checks; stop after two focused repairs | `BLOCKED_ROLLED_BACK` — strict asset gate exposed a pre-existing uncaught negative-radius canvas `arc` error during incremental zoom before tile-worker creation; assertions were not weakened and incomplete test files were removed from master by recoverable Git reverts |
| 2 | `PA-AGY-01` | worker `a4b9f4b`; integrated report `081f42c`; managed log `47ee7ce` | `master` during Phase A | Scope/diff/report evidence review; secrets-safe log review; `git diff --check`; before/after RIMG and MapLab status | `VERIFIED` — report/handoff only, no product changes; MapLab clean; log scan found no key/token/private-key patterns |
| 3 | `PA-ROOT-02` | worker `e4adc7b`; integrated `master` commit `6cbcd23284c0d3e86f95ed9b9959bfbf66c0508b`; integration-worktree proof `3530cf1cf215d29c5699720b29385c5e82af2772` | `master` during Phase A | Strict browser assertions from `PA-LUNA-01`; deep-link tile-worker proof; no product changes; targeted browser check x2; full existing acceptance | `VERIFIED` — deep-link (`/chart/?view=14.4,50.1,4`) drives the boot-time `startTileWorker`/`wantTile` prewarm without a synthetic wheel gesture; strict suite green twice in the worker worktree and once more after cherry-pick into an isolated integration worktree; port 5080 confirmed released after every run; post-merge `check.ps1` all seven gates green with only the expected `FIGURES.md` timing-line diff (not committed) |

## Verification ledger

No migration verification has run.

| Date | Commit | Scope | Command | Result | Notes |
|---|---|---|---|---|---|
| 2026-09-03 | `29de903` | RIMG recovery snapshot | Git remote ref verification | `PASS` | Pushed as `master` and tag `backup-rimg-20260903`; application checks intentionally not run |
| 2026-09-03 | `df3c1ba` | Finalized MapLab recovery snapshot | Git remote ref verification | `PASS` | Pushed as branch `backup/maplab-final-20260903` and tag `backup-maplab-20260903` |
| 2026-09-03 | `24c1fca` | Three manual preflight jobs | Coordinator review of physical handoffs and scope compliance | `PASS` | All three changed only their assigned handoff; no product code, migration, test run, move, or deletion occurred |
| 2026-09-03 | `18bb16e` | Phase A pre-change baseline in isolated `D:\FrontMission-RIMG-worktrees\PA-BASELINE-01` | `powershell -NoProfile -ExecutionPolicy Bypass -File .\check.ps1` | `PASS` | Release build: 0 warnings; Core: 229 passed; BalanceSim: 316.5 ms and green; host/API gates all passed; post-run diff only `FIGURES.md` timing `~220 ms -> ~320 ms`; port 5080 released |
| 2026-09-03 | `47ee7ce` | `PA-AGY-01` inventory integration | Worker scope/ancestry review; `git diff --check`; redacted-pattern scan of managed log; before/after status of both repositories | `PASS` | Integrated only report, handoff, and run log; MapLab remained clean; report records 111/111 manifest sprites present, current `world.js` statically in sync, known `art/truck.png` 404, path walks, generated outputs, secret-bearing path names without values, and no-delete attestation |
| 2026-09-04 | `30b2b69` | `PA-LUNA-01` browser smoke integration in isolated `D:\FrontMission-RIMG-worktrees\PA-INTEGRATION-01` | `node --check tests/browser/smoke.test.js`; `git diff --check`; `npm ci --prefix tests/browser`; Chromium install; `npm test --prefix tests/browser` | `PASS` | Playwright 1/1 passed in 33.2 s; chart booted, multiple canvas samples painted, ops opened, globals/assets loaded, fixed-seed browser-bridge command advanced state, production tile worker emitted `ready` and a successful `tile`; port 5080 released |
| 2026-09-04 | `30b2b69` | Post-browser-safety full existing acceptance in isolated integration worktree | `powershell -NoProfile -ExecutionPolicy Bypass -File .\check.ps1` | `PASS` | Release build: 0 warnings; Core: 229 passed; BalanceSim: 176.0 ms and green; all host/API/build gates passed; only expected `FIGURES.md` timing `~220 ms -> ~180 ms` changed; port 5080 released |
| 2026-09-04 | `10b2875` | `PA-LUNA-01` stop-loss rollback | Git revert of integration commits `30b2b69` then `1fc5206`; diagnostic branch push verification | `PASS` | Incomplete browser-test files removed from master by recoverable commits; product sources/data unchanged; strict blocked implementation preserved at `origin/codex/pa-luna-01-browser-smoke` commit `f94f2e0`; coordinator-authored blocked handoff retained |
| 2026-09-04 | `e4adc7b495cac093e3170818c68f81d3580981ea` | `PA-ROOT-02` redesigned browser smoke, worker worktree | `node --check tests/browser/smoke.test.js`; `git diff --check`; write-scope review; `npm ci --prefix tests/browser`; `npx --prefix tests/browser playwright install chromium`; `npm test --prefix tests/browser` x2; port 5080 listener check x2 | `PASS` | Deep link `/chart/?view=14.4,50.1,4` (Praha's real coordinate) drives boot-time `startTileWorker`/`wantTile` without a synthetic wheel gesture; run 1 passed in 30.5s, run 2 in 16.2s; worker `ready`, one `tile` without `err`, no worker errors, no page/console/network/API failures either run; no listener on port 5080 after either run |
| 2026-09-04 | `3530cf1cf215d29c5699720b29385c5e82af2772` | `PA-ROOT-02` cherry-picked onto isolated integration worktree `D:\FrontMission-RIMG-worktrees\PA-INTEGRATION-02` (detached from `master` `15b9aff`) | `npm ci --prefix tests/browser`; `npx --prefix tests/browser playwright install chromium`; `npm test --prefix tests/browser`; `powershell -NoProfile -ExecutionPolicy Bypass -File .\check.ps1` | `PASS` | Browser smoke passed in 32.7s against the merged state; full seven-gate acceptance all green (Release build 0 warnings; Core 229 passed; BalanceSim 322.9 ms; host/API/recruitment/city/build gates passed); only diff after the run was the expected `FIGURES.md` timing line (`~220 ms -> ~320 ms`, not committed); port 5080 released |
| 2026-09-04 | `6cbcd23284c0d3e86f95ed9b9959bfbf66c0508b` | `PA-ROOT-02` merged onto `master` | Cherry-pick from verified worker commit `e4adc7b`; ancestry and scope review | `PASS` | Fast-forward-equivalent cherry-pick of the single verified commit onto `master` at `15b9aff`; no conflicts; identical diff to the worker branch |

## Decision log

| ID | Date | Decision | Reason | Status |
|---|---|---|---|---|
| `D-001` | 2026-09-03 | Use one canonical single-writer ledger | Prevent worker merge conflicts and contradictory status | `ACCEPTED` |
| `D-002` | 2026-09-03 | Do not begin Wave 0 while creating this ledger | User explicitly requested planning before execution | `ACCEPTED` |
| `D-003` | 2026-09-03 | Require isolated worktrees for concurrent writers | Protect the dirty baseline and make integration reviewable | `ACCEPTED` |
| `D-004` | 2026-09-03 | Consolidate the finalized MapLab frontend into the main repository | The frontend and backend form one product and require atomic changes | `ACCEPTED` |
| `D-005` | 2026-09-03 | Store the pre-consolidation RIMG and MapLab snapshots as separate branches in `Zhihong0321/frontmission-reimagined` | Preserve both current trees before choosing or applying the final merged layout | `ACCEPTED` |
| `D-006` | 2026-09-03 | Exclude MapLab bytecode and empty generator logs from its source snapshot | They are transient runtime output; all source, metadata, and finalized art remain in the recovery branch | `ACCEPTED` |
| `D-007` | 2026-09-03 | Store durable process in `MIGRATION_PLAN.md` and live state in this ledger | Keep the ledger operational while preserving complete physical instructions | `ACCEPTED` |
| `D-008` | 2026-09-03 | Move MapLab deletion to final cleanup | Prevent false-positive verification and preserve a working reference during migration | `ACCEPTED` |
| `D-009` | 2026-09-03 | Preserve classic-script semantics during initial frontend extraction | Avoid combining file splitting with runtime module-semantics changes | `ACCEPTED` |
| `D-010` | 2026-09-03 | Keep the integration branch green and discard failed worker branches before dependent work | Prevent a half-migrated failure chain and avoid restarting the whole migration | `ACCEPTED` |
| `D-011` | 2026-09-03 | Use committed physical task packets and handoffs for every local or user-relayed worker | Make cross-tool delegation reproducible without relying on chat context | `ACCEPTED` |
| `D-012` | 2026-09-03 | Let the coordinator launch installed CLIs; use user relay only for UI-specific workers | Reduce manual task passing while preserving access to requested IDE and desktop models | `ACCEPTED` |
| `D-013` | 2026-09-03 | Defer Codex Luna and AGY implementation/inventory jobs until the three manual preflight reviews are synthesized | Avoid duplicate analysis and prevent issuing scopes that advisory evidence may invalidate | `ACCEPTED` |
| `D-014` | 2026-09-03 | Accept the Cursor browser-safety design with implementation adjustments | Playwright covers the missing real-browser gate; implementation must sample multiple canvas points, verify the lazy worker and provenance, and choose a currently compatible dependency version rather than copying the advisory version blindly | `ACCEPTED` |
| `D-015` | 2026-09-03 | Accept the Kimi reproducibility design with an explicit command-coverage matrix | State/view/content/world/API/save baselines materially reduce false-green risk; commands outside the fingerprint script must remain visibly covered by the full Core suite | `ACCEPTED` |
| `D-016` | 2026-09-03 | Accept Claude's `REVISE_BEFORE_START` verdict and all seven required controls | Both sibling walks, per-item Core tests, wire-contract browser gates, C-before-D ordering, branch policy, advisory dispositions, and command coverage directly prevent stacked failures | `ACCEPTED` |
| `D-017` | 2026-09-03 | Use `web/chart/` as the active in-repository frontend path | It keeps the playable frontend below the existing web root and gives the host one explicit source | `ACCEPTED` |
| `D-018` | 2026-09-03 | Freeze product changes on `master` after `known-green/original` and run migration on a named integration branch | Prevent untracked divergence; urgent fixes trigger a worker pause, forward integration, and full re-verification | `ACCEPTED` |
| `D-019` | 2026-09-03 | Make Phase D depend on verified Phase C and define `Full` verification as a strict superset | Disjoint files still share an implicit JSON contract; narrow checks cannot certify integration | `ACCEPTED` |
| `D-020` | 2026-09-03 | Keep active finalized art in normal Git during this migration and defer storage optimization | The art is already remotely recoverable; LFS/history migration would invalidate recovery assumptions and is a separate project | `ACCEPTED` |
| `D-021` | 2026-09-03 | Reserve AGY `gemini-3.8-flash-high` for the no-delete inventory job | This matches the requested low-cost worker allocation and the task is repetitive, bounded analysis | `ACCEPTED` |
| `D-022` | 2026-09-03 | Authorize execution of Phase A only | The user explicitly started Phase A after plan v3 synthesis; phases B-F, consolidation, refactoring, moves, and deletion remain unauthorized | `ACCEPTED` |
| `D-023` | 2026-09-03 | Assign `PA-LUNA-01` the standalone browser smoke suite and `PA-AGY-01` the no-delete inventory report | These are approved Phase A gates with non-overlapping write scopes and independent outputs | `ACCEPTED` |
| `D-024` | 2026-09-03 | Accept and integrate `PA-AGY-01` with coordinator-normalized commit evidence | The report and handoff are in scope and useful; the handoff's self-reported result hash predates its final worker commit and its "direct ancestor" wording is imprecise, so the ledger records authoritative worker/integration hashes | `ACCEPTED_WITH_MODIFICATIONS` |
| `D-025` | 2026-09-04 | Accept `PA-LUNA-01` after one focused same-scope repair | The initial suite could miss the chart's silently swallowed worker tile error; the follow-up observes production `ready`, successful `tile`, and worker error messages, and both targeted browser and full existing acceptance checks are green | `ACCEPTED` |
| `D-026` | 2026-09-04 | Reopen `PA-LUNA-01` for a second and final focused repair before Phase A advances | The integrated suite blanket-exempts every `/chart/art/gen/**` failure even though those are manifest-declared runtime sprites; only the two proven current missing fallbacks (`art/tex-deep.png`, `art/truck.png`) may be tolerated, and manifest runtime files must be probed without weakening other 404 checks | `ACCEPTED` |
| `D-027` | 2026-09-04 | Stop `PA-LUNA-01` after repair 2, preserve diagnostic branch `codex/pa-luna-01-browser-smoke` at `f94f2e0`, and roll back its incomplete master integration with `a6408fc` + `10b2875` | The strict required smoke remains red on a pre-existing frontend canvas error; the stop-loss forbids weakening the assertion or stacking dependent Phase A work on a false-green safety net | `ACCEPTED` |
| `D-028` | 2026-09-04 | Resume Phase A with coordinator job `PA-ROOT-02`, using the existing `?view=lon,lat,zoom` deep-link prewarm path to exercise the tile worker before considering a product fix | The user authorized proceeding. The frontend already contains a bounded high-zoom deep-link worker path; testing it avoids the synthetic wheel sequence that triggered the canvas error and preserves the no-product-change preference. A product fix remains out of scope unless this redesign cannot meet the strict gate | `ACCEPTED` |
| `D-029` | 2026-09-04 | Let Claude Code finish and integrate `PA-ROOT-02` after the Codex coordinator ran out of usage quota mid-job, resolving the `sonnet`-alias open decision as Sonnet 5 | The `PA-ROOT-02` worktree held the redesign's scaffolding (four implementation files plus `.gitignore`) uncommitted and unchecked when Codex stopped. The user directly instructed Claude Code to continue, and then explicitly authorized it to also perform the coordinator-only integration steps (cherry-pick review, full `check.ps1`, ledger update) that the ledger normally reserves for `ROOT`, given `ROOT` was unavailable. Claude Code self-reports its resolved model as Sonnet 5, settling the second open decision below | `ACCEPTED` |

## Open decisions

These decisions must be resolved before their dependent jobs become `READY`:

1. Exact dead directories approved for removal in addition to the sibling MapLab folder;
   decide only after runtime/network inventory and quarantine evidence.

Resolved: whether the Claude Code CLI `sonnet` alias resolves to the intended Sonnet 5
model. `PA-ROOT-02` confirms it does; see `D-029`.

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
- Both authorized Phase A workers were launched from committed packets in isolated
  worktrees and have returned durable handoffs.
- The user-relayed Cursor, Claude Desktop, and Kimi preflight jobs are complete; all three
  physical handoffs were reviewed, accepted or accepted with modifications, and retained.
- Plan version 3 contains the resulting safety changes. Phase A only is authorized; its
  browser gate is no longer `BLOCKED`. Phases B-F remain unauthorized and gated.
- `PA-LUNA-01` and `PA-AGY-01` have exact, non-overlapping assignments recorded above;
  their immutable physical packets are committed under `coordination/tasks/` before launch.
- The existing seven-gate acceptance suite passed at `18bb16e`; the isolated run produced
  only the expected `FIGURES.md` timing-line change. This is baseline evidence, not yet the
  `known-green/original` checkpoint because browser, determinism/save, generated-world,
  API-shape, content-hash, and clean-layout Phase A gates remain open.
- `PA-AGY-01` is integrated and verified as evidence-only work. Its no-delete inventory
  identifies current path-discovery, generated-output, asset, archive, and secrets-hygiene
  facts without authorizing cleanup or Phase B.
- `PA-LUNA-01` remains `BLOCKED` and preserved at `f94f2e0`; its incomplete integration
  was rolled back. The user authorized proceeding, and replacement job `PA-ROOT-02`
  redesigned the tile-worker trigger to use the frontend's own `?view=lon,lat,zoom`
  boot-time prewarm path instead of a synthetic wheel gesture. The Codex coordinator ran
  out of usage quota mid-job with the redesign uncommitted and unchecked; Claude Code
  completed it, ran every required check twice, and — with separate explicit user
  authorization — also performed the coordinator-only integration: cherry-pick into an
  isolated worktree, full seven-gate `check.ps1`, and this ledger update. See `D-029`.
  `PA-ROOT-02` is `VERIFIED` and merged to `master` at `6cbcd23`. The strict browser gate
  is green, so the browser-gate blocker on dependent determinism/save/API jobs is lifted;
  those jobs are still unassigned and nothing beyond this integration has been started.
- No pre-existing product, data, asset, or sibling-repository file or directory has been
  moved or deleted. The rejected browser-test integration was removed only by recoverable
  Git revert commits under the stop-loss rule.
- Phase A verification has started and is recorded above. Consolidation, cleanup, and
  refactoring have not started.
- Recovery point `backup-rimg-20260903` preserves the current RIMG state.
- Recovery point `backup-maplab-20260903` preserves the finalized MapLab state.
