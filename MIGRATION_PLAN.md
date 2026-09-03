# MechaTrader consolidation and maintainability plan

This document is the durable execution plan. It describes what must happen, in which
order, and which safety gates prevent a partial optimization from breaking the project.

Live ownership, job status, commits, checks, and handoffs belong in
`MIGRATION_LEDGER.md`. Only the coordinator edits either control document.

## Status

- Plan version: `3`
- Plan status: `APPROVED_FOR_PLANNING_ONLY`
- Execution status: `NOT_STARTED`
- Current known remote recovery points:
  - RIMG: `backup-rimg-20260903` at `29de90387bb2d8fcccf5d6b787def5edac2ca923`
  - MapLab: `backup-maplab-20260903` at `df3c1baa8a83c2412607353af9994170b988dbe3`
- Current coordination commit before advisory synthesis: `24c1fca311282dadf8d803ba302c2aab468759e6`
- Current known-green application commit: `UNSET`

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

## Transaction used for every implementation job

1. Coordinator records the current known-green base commit.
2. Coordinator creates an isolated branch and worktree.
3. Coordinator records the job owner, allowed paths, prohibited paths, tests, and stop
   conditions in the ledger.
4. Worker reads this plan and the ledger.
5. Worker performs one bounded transformation.
6. Worker runs targeted checks and commits.
7. Worker returns the required structured handoff.
8. Coordinator reviews the diff and evidence.
9. Coordinator integrates the commit into the integration branch.
10. Coordinator runs the required integration checks.
11. If green, the ledger advances the job and known-green commit.
12. If red, the integration commit is reverted or the branch is discarded before any
    dependent job starts.

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
9. Create the integration branch and worker worktrees from that commit.

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

### Phase C — mechanical backend decomposition

Process one original large file per integration checkpoint. Tentative order:

1. `Definitions.cs`
2. `ViewModels.cs`
3. `WorldLoader.cs`
4. `ViewBuilder.cs`
5. `CommandProcessor.cs`
6. Balance harness
7. Oversized test classes

For every item:

- Move code without semantic cleanup.
- Preserve public entrypoints.
- Run the full, unfiltered `MechaTrader.Core.Tests` project after each item.
- Compare deterministic fingerprints and save fixtures.
- After items 2, 4, and 5, also run the browser smoke and API-shape checks because these
  files form the frontend wire contract.
- Integrate only while green.

End with the complete acceptance and browser suites, then tag
`known-green/backend-split`.

### Phase D — mechanical frontend decomposition

Phase D depends on Phase C's verified checkpoint, not only on Phase B.

Tentative order:

1. Extract inline CSS without rewriting it.
2. Extract inline chart JavaScript into one classic `chart.js` without rewriting it.
3. Extract pure terrain helpers.
4. Extract rendering helpers.
5. Extract camera and input.
6. Extract routing.
7. Extract HUD behavior.
8. Extract worker interaction.
9. Establish an explicit shared ops namespace.
10. Extract ops helpers.
11. Extract one ops page at a time.
12. Extract stateful boot code last.

After every step:

- Run the browser smoke suite.
- Fail on console or network errors.
- Compare required visible behavior.
- Keep the integration branch green.

End with the complete acceptance and browser suites, then tag
`known-green/frontend-split`.

### Phase E — AI context and verification workflow

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

### Phase F — cleanup and retirement

Cleanup is deliberately last and split into independent commits:

1. Remove dormant ArtLab application code and endpoints as one complete feature removal.
2. Remove the archived UI.
3. Quarantine and verify unused screenshots and assets.
4. Remove verified-unused files.
5. Test from a fresh clone.
6. Confirm no code or launcher reads the sibling MapLab path.
7. Confirm both remote recovery tags still resolve.
8. Remove `D:\FrontMission-MapLab` from the local disk.
9. Run full acceptance and browser verification again.
10. Tag `known-green/final`.

The coordinator must record exact deletion targets in the ledger before deletion and
record whether each deletion is recoverable from Git.

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
