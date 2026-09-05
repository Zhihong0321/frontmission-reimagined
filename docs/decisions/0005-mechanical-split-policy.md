# ADR 0005: Mechanical file splits move bytes; they do not improve them

- Status: Accepted (Phase C precedent: `MIGRATION_LEDGER.md` `D-043`-`D-054`)
- Date: 2026-09-05 (promoted into ADR form by CP-E1)
- Context: Several original files grew past cohesion (Definitions, ViewModels,
  WorldLoader, ViewBuilder, CommandProcessor, the balance harness Program.cs).
  Splitting them naively risks semantic drift hidden inside an innocent-looking diff.

## Decision

When a file is split for maintainability, the split is mechanical: members move
unchanged into cohesive new files, typically as `partial class` fragments
(C#-sanctioned since `D-048`). Namespaces, names, signatures, ordering, visibility,
public entrypoints, doc comments (attached to their members), encoding, and line
endings are preserved byte-for-byte. The only textual deltas are the `partial`
keyword and per-fragment wrappers (usings + namespace + class declaration). The
original `Execute`-style dispatch or `Main` stays in the original file.

Equivalence is proven, not assumed: for move-class tasks the original file must be
reconstructed byte-identically from the split output (SHA-256 compared in both worker
and merged states).

## Consequences

- Review of a split diff is mechanical; semantics cannot hide inside it.
- Renames, abstraction introductions, and cleanup are separate jobs with their own
  authorization — never folded into a split.
- The pinned fingerprints, save fixtures, and the balance harness's output equality
  (timing line excepted) back the byte-level proof at the behavior level.
