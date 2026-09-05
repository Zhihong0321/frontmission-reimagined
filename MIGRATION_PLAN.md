# MechaTrader consolidation and maintainability plan

This document is the durable execution plan. It describes what must happen, in which
order, and which safety gates prevent a partial optimization from breaking the project.

Live ownership, job status, commits, checks, and handoffs belong in
`MIGRATION_LEDGER.md`. Only the coordinator edits either control document.

## Status

- Plan version: `5`
- Plan status: `APPROVED_PROCESS_EXECUTION_GATED_BY_LEDGER` (streamlined 2026-09-05 per
  `MIGRATION_LEDGER.md` `D-055`; remaining work, checkpoints, and gates follow
  `coordination/plan-revision-2026-09-05.md`)
- Execution status: `PHASE_C_ITEMS_1_6_VERIFIED` (Phase A and B verified; Phase C items
  1-6 verified and item 7 cancelled per `D-055`; phases D-F not started)
- Current known remote recovery points:
  - RIMG: `backup-rimg-20260903` at `29de90387bb2d8fcccf5d6b787def5edac2ca923`
  - MapLab: `backup-maplab-20260903` at `df3c1baa8a83c2412607353af9994170b988dbe3`
- Current coordination commit before advisory synthesis: `24c1fca311282dadf8d803ba302c2aab468759e6`
- Current known-green application commit: `known-green/original` at `5ed5949` (see
  `MIGRATION_LEDGER.md` for the full verification record and `D-031`)

No implementation job may begin merely because it appears in this plan. The ledger must
mark the job `READY`, name its owner and write scope, and record its green base commit.

## Disk-first operating rule

From this point forward, every material plan, decision, assignment, status change,
verification result, integration result, rollback point, and scope change must be written
to physical disk before dependent work begins.

- Durable process and acceptance criteria: `MIGRATION_PLAN.md`
- Live state and evidence: `MIGRATION_LEDGER.md`
- Cross-tool task packets and handoffs: `coordination/`
- Worker implementation: committed on an assigned branch/worktree
- Worker handoff: returned to the coordinator and then recorded in the ledger
- Architectural decision that survives the migration: later promoted into `docs/decisions/`

Chat is notification, not the source of truth.

## Goal

Produce one maintainable repository in `D:\FrontMission-RIMG` that contains the finalized
player frontend, simulation, host, content, tests, assets, and AI-oriented documentation.
The result must be faster for future agents to understand and extend without changing the
game's behavior during mechanical reorganization.

## Failure outcome to prevent

The unacceptable outcome is a long-lived half-migrated state where several changes are
stacked on top of the first broken change and the only practical recovery is restarting
the entire migration.

The process therefore keeps the integration branch green. A failed worker branch is
discarded or redesigned before any dependent work begins.

## Non-goals during mechanical migration

- No gameplay features.
- No economy or balance changes.
- No save-format redesign.
- No command-protocol redesign.
- No domain renaming.
- No frontend framework adoption.
- No ES-module conversion during initial extraction.
- No Git history rewriting.
- No Git LFS migration.
- No deletion of the original MapLab directory until final cleanup.

Each non-goal may become a separate later project after the consolidated codebase is
known green.

## Critical risks and controls

### Unverified baseline

Risk: the recovery snapshot may already contain a failure, making later regressions
ambiguous.

Controls:

- Run the complete existing acceptance suite at the exact baseline.
- Exercise the current player view in a real browser.
- Record failures before changing implementation.
- Establish and tag the first known-green application commit.

### Sibling-directory false positive

Risk: the host or launcher may silently serve `D:\FrontMission-MapLab` even after an
in-repository frontend is added, making a broken consolidation appear successful.

Controls:

- Keep the old folder as a reference but remove both runtime sibling-discovery paths:
  `Program.cs::LocateMapLab` for serving and `play.ps1::Update-ChartData` for generation.
- Configure one explicit in-repository frontend path.
- Once the in-repository generator is expected, a missing generator or failed generation
  is a hard failure rather than a warning followed by stale output.
- Verify from a fresh clone in a location with no sibling MapLab folder.
- Record the served file location and a unique in-repository provenance marker during
  verification.

### Browser-blind acceptance suite

Risk: build, unit, simulation, and HTTP API checks can pass while the actual canvas or ops
interface is broken.

Controls:

- Add a browser smoke suite before moving frontend code.
- Fail on uncaught exceptions, console errors, failed scripts, failed workers, and asset
  404s.
- Assert that WORLD, MANIFEST, MECHA, and OPS initialize.
- Assert that state loads, the canvas renders, and the ops shell opens.
- Exercise at least one deterministic game command through the browser bridge.
- Force the lazy tile-worker path, explicitly probe required static assets, and sample
  the rendered canvas at multiple points rather than relying on one corner pixel.

### Frontend execution-order changes

Risk: converting classic scripts to modules changes global bindings, strictness, and load
timing.

Controls:

- Initial extraction preserves classic-script execution and original statement order.
- First extract inline CSS, then one unchanged `chart.js`, then bounded ordered scripts.
- ES modules require a later independent plan and approval.

### Determinism and save compatibility

Risk: moving code while changing iteration order, RNG calls, JSON names, defaults, or
record constructors silently changes simulation output or invalidates saves.

Controls:

- Capture deterministic state fingerprints before refactoring.
- Keep representative serialized saves as compatibility fixtures.
- Pin both state and view output, content inputs, generated `world.js`, and stable API
  response shapes while maintaining an explicit allowlist for inherently noisy fields.
- Maintain a command-coverage matrix. It must say which command types are protected by
  deterministic fingerprints and which are protected only by the full Core test suite.
- During mechanical splits preserve namespaces, names, signatures, ordering, visibility,
  and public entrypoints.
- Renaming and abstraction changes are separate jobs.

### Dynamic asset deletion

Risk: manifest-driven or computed asset paths are missed by textual search.

Controls:

- Audit the manifest, metadata, runtime network requests, and files together.
- Treat any static 404 as a failure.
- Quarantine before deletion.
- Delete only in a dedicated cleanup commit after clean-clone browser verification.

### Parallel semantic conflicts

Risk: non-overlapping Git diffs can still make incompatible architectural assumptions.

Controls:

- Parallelize analysis freely.
- Limit concurrent writers to independent surfaces.
- Never assign two workers to split the same original large file concurrently.
- Treat the C# view/command DTO surface and the browser bridge/ops scripts as one semantic
  contract even though their Git paths do not overlap.
- Complete Phase C before Phase D. Do not run backend wire-contract decomposition and
  frontend extraction concurrently.
- Only the coordinator integrates commits.
- Every job names an exact green base commit and exclusive write scope.

### Fast-check false confidence

Risk: a targeted check is mistaken for whole-product verification.

Controls:

- Fast checks are iteration aids only.
- High-risk integrations require browser and full acceptance checks.
- Every phase ends at a recorded known-green checkpoint.

### Recovery-point invalidation

Risk: history rewriting or force pushing makes the current recovery tags unreliable.

Controls:

- No force pushes.
- No filter-repo, rebase of published recovery history, or LFS history migration.
- Any future repository-size cleanup gets its own backup and plan.

## Transaction used for every checkpoint (revised per `D-055`)

1. The checkpoint's scope, prohibited paths, write scope, and Full gate list come from
   the approved roadmap (`coordination/plan-revision-2026-09-05.md`); the executing
   session confirms them against the live tree before any product change.
2. Coordinator records the current known-green base commit and creates or verifies the
   isolated branch and worktree.
3. Worker performs the checkpoint's bounded transformation, iterating with `Fast`
   checks only (zero-warning Release build plus affected tests). Fast never certifies
   completion. Move-class tasks (moving a class or file) must additionally prove
   byte-level equivalence of the moved members; other task types rely on the Full
   battery.
4. Worker returns one short handoff (results, exceptions, cleanup evidence).
5. Coordinator reviews the diff against the confirmed scope, integrates with an
   ordinary merge into the integration branch, and runs the Full battery once at the
   integration state.
6. If green, the ledger records one verification row for the checkpoint and advances
   the known-green commit; if red after two focused repairs, the checkpoint stops,
   is marked `BLOCKED`, is not pushed, and work resumes from the previous green point.

## Stop-loss rule

If a bounded job remains red after two focused repair attempts:

- Stop the worker.
- Mark the job `BLOCKED`.
- Do not merge it.
- Preserve its branch only if useful for diagnosis.
- Redesign or reduce the job boundary.
- Resume from the previous known-green commit.

No new worker is assigned to build on a red integration state.

## Phase-level recovery rule

If a later full check proves that an earlier phase checkpoint was falsely green:

1. Stop every dependent job immediately.
2. Mark the checkpoint `INVALIDATED` in the ledger; do not delete or move its tag.
3. Preserve the failing integration state on a named diagnostic branch.
4. Create a replacement integration branch from the most recent still-verified
   `known-green/*` tag.
5. Replay only independently reviewed green commits, then rerun the full checkpoint.

This rolls back at most to the last verified phase, not automatically to the original
project. Published recovery history is never rewritten.

## Master and integration branch policy

Before the integration branch is created, the ledger must name its branch and exact base.
After creation, `master` is frozen for product changes at `known-green/original` until the
migration finishes. Coordination-only records may still be committed before execution.

If an urgent product fix must land on `master`, all workers pause. The coordinator records
the new master commit, brings it forward into the integration branch at a green checkpoint
without rewriting published history, and reruns the full checkpoint before workers resume.

## Execution phases

### Phase A — establish the known-green original

1. Record an accept, modify, or reject disposition for every preflight advisory handoff.
2. Inventory source, generated output, archives, assets, path discovery, and secrets.
3. Run the complete existing acceptance suite in the documented command order and record
   the exact commit and post-run generated-file changes.
4. Run the current player view in a browser and record observable behavior.
5. Add the browser smoke suite as a standalone safety-net change.
6. Add deterministic state and view fingerprints, representative save fixtures, stable
   API shape fixtures, content hashes, generated-world verification, and the command
   coverage matrix.
7. Verify both API and browser behavior from a clean environment reproducing the current
   two-folder layout.
8. Commit and tag `known-green/original`.
9. Create the integration branch from that commit. Create each worker worktree from the
   current integration known-green base only when its job is `READY`; speculative
   worktrees are not required.

No structural migration begins until this phase is green.

### Phase B — consolidate without deleting the fallback

1. Import the MapLab recovery tree into the main repository at the approved path.
2. The approved active frontend path is `web/chart/`.
3. Preserve frontend bytes and relative layout during the import.
4. Move or copy the generator into the repository and prove generation is deterministic
   from the in-repository `data/` path.
5. In one bounded path-switch transaction, update the host to serve only `web/chart/`,
   update `play.ps1::Update-ChartData` to use only the in-repository generator, and remove
   `Program.cs::LocateMapLab` plus every sibling fallback.
6. Make missing or failed required generation fatal and add a provenance assertion proving
   `/chart/` came from the consolidated copy.
7. Do not accept a mid-phase manual playtest unless its served-source provenance is
   recorded; the untouched sibling folder otherwise makes the result ambiguous.
8. Run build, full Core tests, deterministic, save, API, static-asset, and browser checks.
9. Clone the integration commit into a separate location with no sibling MapLab folder or
   unrelated ancestor `data/` directory.
10. Launch, regenerate `world.js`, and verify there using a full-history clone.
11. Commit and tag `known-green/consolidated`.

The original `D:\FrontMission-MapLab` directory remains untouched in this phase.

### Phase C — mechanical backend decomposition (closed per `D-055`)

Original tentative order, with final disposition:

1. `Definitions.cs` — verified (`b7e2c8d`)
2. `ViewModels.cs` — verified (`fa8592a`)
3. `WorldLoader.cs` — verified (`ff32d4f`)
4. `ViewBuilder.cs` — verified (`290615f`)
5. `CommandProcessor.cs` — verified (`6441f88`)
6. Balance harness — verified (`93a2196`)
7. Oversized test classes — **CANCELLED** per `D-055` (only `CrewTests.cs`, 695 lines,
   is materially oversized; the 239-test pin in `check.ps1` remains the tripwire)

Items 1-6 were executed under the version 4 dual-state battery. With item 7 cancelled,
Phase C is complete at integration `3830abd`; the closeout tag
`known-green/backend-split` is created at that commit in the CP-0 session with same-run
Full evidence (revision §2.3). Phase C retains its non-negotiables: mechanical moves
only, preserved public entrypoints, pinned fingerprints and save fixtures.

### Phase D — mechanical frontend decomposition (revised per `D-055`)

Phase D depends on Phase C's verified checkpoint. The twelve original steps are
compressed into three checkpoints; the original steps remain the content checklist:

- `CP-D1`: extract inline CSS without rewriting it and extract inline chart JavaScript
  into one unchanged classic `chart.js` (steps 1-2); byte-level mechanical moves,
  execution order unchanged.
- `CP-D2`: extract pure terrain, rendering, camera/input, routing, HUD, and worker
  helpers (steps 3-8); each sub-step is committed separately inside the checkpoint so
  a red state can be bisected without extra Full batteries.
- `CP-D3`: establish an explicit shared ops namespace, extract ops helpers, extract
  one ops page at a time, and extract stateful boot code last (steps 9-12).

Each checkpoint runs the Full battery once at the integration state; worker iteration
uses Fast only. Fail on console or network errors and compare required visible
behavior. End with the complete acceptance and browser suites, then tag
`known-green/frontend-split`. Skipping Phase D entirely is not authorized by this
revision; it would be a separate explicit decision that also relaxes the matching
completion criterion.

### Phase E — AI context and verification workflow (executed before Phase D per `D-055`)

Dependency: Phase C only (originally "C, D"); the codemap is regenerated from
repository facts after Phase D and Phase F complete.

1. Create short canonical agent instructions.
2. Create a machine-readable feature ownership map.
3. Generate the codemap from repository facts.
4. Create scoped feature notes.
5. Create a compact domain glossary.
6. Move durable reasoning into architecture decision records.
7. Stop automatically loading histories and large specifications for unrelated tasks.
8. Add `Fast`, feature-specific, and `Full` verification entrypoints.
9. Define `Full` as a strict superset of the original seven `check.ps1` gates plus the
   browser, deterministic, save, API-shape, generated-world, asset, and clean-path checks.
10. Ensure `Full` remains mandatory at integration checkpoints; `Fast` and feature checks
    are iteration aids and cannot certify integration.
11. Tag `known-green/ai-workflow` after verification.

This phase also lands the `Fast` and `Full` script entrypoints defined under
"Verification modes" below; every later checkpoint uses them.

### Phase F — cleanup and retirement (revised per `D-055`)

The ten original steps run as two checkpoints; the steps remain the content checklist:

- `CP-F1` — repository-internal cleanup (steps 1-4): remove dormant ArtLab application
  code and endpoints as one complete feature removal, remove the archived UI,
  quarantine and verify unused screenshots and assets, and remove verified-unused
  files. Each deletion class is its own recoverable commit.
- `CP-F2` — MapLab retirement (steps 5-10): test from a fresh clone, confirm no code or
  launcher reads the sibling MapLab path, confirm both remote recovery tags resolve,
  remove `D:\FrontMission-MapLab` from the local disk, run full acceptance and browser
  verification again, and tag `known-green/final`.

The coordinator must record exact deletion targets in the ledger before deletion and
record whether each deletion is recoverable from Git.

## Verification modes (per `D-055`)

- `Fast` = `dotnet build MechaTrader.sln -c Release` with zero warnings plus the
  affected tests. Fast is an iteration aid only; it can never certify a green or
  finished state.
- `Full` = all six gates: zero-warning Release build; complete nine-gate `check.ps1`;
  `tools/MechaTrader.Fingerprint` regeneration with zero tracked diff and pinned
  `F_state`/`F_view`; browser smoke 1/1; clean `git diff --check`; hygiene (port 5080
  free, no `MechaTrader.Host` process, `FIGURES.md` timing-line-only restored,
  temp-directory baseline compare plus this-run cleanup).
- Any `MERGED`/`VERIFIED` claim must cite a same-run Full result. The version 4
  worker-state dual battery is retired; each checkpoint runs Full once at the
  integration state, and byte-exact reconstruction proofs are limited to move-class
  tasks.

## Agent allocation

Use high-reasoning frontier or independent review for:

- Baseline interpretation.
- Consolidation path design.
- Cross-cutting contracts.
- Save compatibility decisions.
- Integration conflict resolution.
- Destructive cleanup approval.

Use Luna High for:

- Bounded mechanical file extraction.
- Test-file decomposition.
- Import updates after paths are fixed.
- Generated documentation and codemap tooling.

Use AGY CLI for:

- Asset and generated-output inventory.
- Repetitive path/reference audits.
- Mechanical scripts with precise output schemas.

Use Kimi CLI with the configured `kimi-k3` model for:

- Independent bounded implementation or review jobs.
- Repository inspection that benefits from a separate model family.
- Mechanical tasks whose scope and checks fit one physical task packet.

Use Cursor with Grok 4.6 for:

- User-relayed IDE work until a model-selectable non-interactive agent invocation is
  verified.
- Independent review or a bounded implementation in its own worktree.

Use Claude Desktop for:

- User-relayed independent review or bounded implementation using a physical task packet.
- Work that does not require the coordinator to automate the desktop UI.

Use Claude Code Sonnet High for:

- Independent consolidation review.
- Frontend execution-order review.
- Regression review at known-green checkpoints.

External agents run non-interactively in coordinator-created worktrees. They do not use
dangerous permission bypasses, edit the ledger, integrate commits, or delete source
directories.

UI-only workers use the manual relay procedure in `coordination/README.md`; the user needs
to provide only the task-file path and later return the job ID plus commit or handoff path.

## Completion criteria

The migration is complete only when:

- One repository contains all active product code and required assets.
- A clean clone launches without any sibling repository.
- Full acceptance, deterministic, save-compatibility, API, asset, and browser checks pass.
- The original MapLab directory has been retired only after those checks.
- Large active files have cohesive ownership boundaries.
- Agent startup requires only short root instructions plus relevant feature context.
- The codemap is generated and current.
- The ledger names the final known-green commit and recovery tags.
- `master` contains no half-completed migration state.
