# ADR 0006: Published history is immutable; recovery points are named tags

- Status: Accepted (plan risk controls; `MIGRATION_LEDGER.md` `D-018`, `D-020`)
- Date: 2026-09-05 (promoted into ADR form by CP-E1)
- Context: History rewriting or force pushes invalidate every recovery tag and make
  the "long-lived half-migrated state" failure mode unrecoverable. The migration runs
  long enough that green checkpoints must be trustworthy without re-deriving them.

## Decision

- No force pushes; no filter-repo; no rebase of published recovery history; no LFS
  migration during this migration.
- `master` is frozen for product changes while the `integration` branch carries the
  migration; coordination-only records may still land on `master`.
- Every phase ends at a named, pushed, annotated recovery tag
  (`known-green/original`, `known-green/consolidated`, `known-green/backend-split`,
  `known-green/ai-workflow`, later `known-green/frontend-split` and
  `known-green/final`), each backed by a same-run Full battery. Existing tags are
  never moved or deleted.
- A falsely green checkpoint is `INVALIDATED` in the ledger, its tag is left alone, a
  diagnostic branch preserves the failing state, and work resumes from the most
  recent still-verified tag by replaying independently reviewed commits.
- The single-writer ledger (`MIGRATION_LEDGER.md`) records every authorization,
  verification, and checkpoint; the migration plan and this docs/decisions directory
  carry the durable reasoning.

## Consequences

- Rollback is always possible to a named, verified point without archaeology.
- A checkpoint that cannot get green is stopped after two focused repairs, marked
  `BLOCKED`, and not pushed; red states are never published.
- Repository-size or storage cleanups require their own backup and plan before
  touching history.
