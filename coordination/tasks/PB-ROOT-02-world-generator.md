# Task packet: `PB-ROOT-02` — repository-local deterministic world generator

## Control

- Status: `ACTIVE`
- Worker: `ROOT`
- Runtime: current Codex coordinator acting in an isolated worker worktree
- Green integration base: `2726f5862b09fc869193f0aba707240bf4af2707`
  (`PB-ROOT-01` verified; product merge `ec7cc79f88b423f9af25acafb78b28e1618264b6`)
- Branch: `codex/pb-root-02-world-generator`
- Worktree: `D:\FrontMission-RIMG-worktrees\PB-ROOT-02`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`
- Read-only source: `D:\FrontMission-MapLab\make-world.js`

Do not begin unless this task is `ACTIVE` in the canonical ledger and assigned to ROOT.

## Objective

Perform only the second bounded Phase B job: bring the finalized MapLab
`make-world.js` into `web/chart/`, make the smallest portability adjustment needed for
location-independent clean-clone generation, regenerate `web/chart/world.js` from this
repository's `data/`, and make the dedicated verifier prove exact deterministic bytes
and source immutability.

This job does not switch any runtime path. It must not claim browser, serving, launcher,
or runtime-path verification.

## Required source and base identity

Before editing, verify all of the following and stop if any differs:

1. The assigned branch descends from integration commit
   `2726f5862b09fc869193f0aba707240bf4af2707`.
2. `ec7cc79f88b423f9af25acafb78b28e1618264b6` is an ancestor and remains the
   `PB-ROOT-01` product merge.
3. `D:\FrontMission-MapLab` remains on branch
   `backup/maplab-final-20260903` at
   `df3c1baa8a83c2412607353af9994170b988dbe3`, with exactly its previously
   authorized tracked `world.js` delta.
4. `D:\FrontMission-MapLab\make-world.js` is exactly 1,552 bytes with SHA-256
   `87b9cbbdcb9a7dc80a23d120ce0c8ba748bb5f4834986f7f6b33948dcf23a64c`.
5. Existing `web/chart/world.js` is exactly 8,590 bytes with SHA-256
   `6680509cd8cbacc72ab3b8060efd4b8c7d3c328f8646aaeb78ddb1531c3d135c`.
6. The six generator inputs exist under repository-local `data/`:
   `cities.json`, `routes.json`, `terrain.json`, `map.json`, `trucks.json`, and
   `config.json`.

The sibling checkout is read-only evidence. Do not run its generator in place and do
not perform any Git or filesystem mutation under `D:\FrontMission-MapLab`.

## Exact allowed write scope

- `web/chart/make-world.js` — new repository-local generator, copied from the pinned
  MapLab source and changed only as described below
- `web/chart/world.js` — regenerated output; only the provenance header may differ from
  the already imported file, while the complete `window.WORLD` payload remains exact
- `tools/verify-worldjs.ps1` — dedicated deterministic, location-independent,
  source-immutability verification for this generator/output pair
- `coordination/handoffs/PB-ROOT-02.md`

## Required minimal generator adjustment

Preserve the finalized generator's extraction logic and output serialization exactly.
Only these location-dependent details may change:

1. Replace the hard-coded default input path `D:/FrontMission-RIMG/data` with a path
   derived from the generator's repository location: `web/chart/` to repository-local
   `data/`. An explicit input argument may remain supported.
2. Replace the generated comment's embedded absolute input path with one stable,
   repository-relative provenance comment so identical repository content produces
   identical complete `world.js` bytes in every clone/worktree location.
3. Update the generator's usage comment only as needed to describe that default.

Do not reorder properties, change parsing, change JSON serialization, format the WORLD
payload, or alter any generated data value. After the first line, regenerated
`world.js` must be byte-for-byte identical to the imported file at the green base.

## Dedicated verification requirements

Update `tools/verify-worldjs.ps1` narrowly so it no longer discovers or reads a sibling
MapLab generator/output. It must fail rather than skip when the repository-local
generator, output, Node.js, or any required input is missing.

The verifier must:

1. Hash the live repository generator, checked-in output, and all six input files before
   generation.
2. Build at least two isolated clone-shaped temporary layouts in different absolute
   locations, each containing `web/chart/make-world.js` and repository-local `data/`.
3. Run the copied generator without an input argument from a working directory other
   than `web/chart/`, proving its default is based on its own repository location.
4. Require both isolated runs to produce identical full `world.js` bytes.
5. Require those full bytes to equal checked-in `web/chart/world.js` exactly.
6. Require the generated header to be stable and contain no machine-specific absolute
   data path.
7. Require the complete payload after the first line to equal the green-base imported
   WORLD payload exactly.
8. Re-hash the live generator, output, and all six inputs after the test and fail if any
   source byte changed.
9. Clean only its own uniquely created temporary directory in a `finally` block.

The script must remain compatible with Windows PowerShell 5.1 and exit non-zero on any
failure.

## Prohibited write scope

- `D:\FrontMission-MapLab\**` — no edits, generation, checkout, reset, stash, index or
  branch operation, cleanup, or any other mutation
- `data/**` — generator inputs are read-only
- `src/MechaTrader.Host/Program.cs`, `play.ps1`, host serving configuration, browser
  paths, runtime paths, and every sibling-discovery implementation
- `check.ps1`, browser tests, other product/test/tool files, and every existing file not
  listed in the exact allowed write scope
- `MIGRATION_PLAN.md`, `MIGRATION_LEDGER.md`, and every coordination file except this
  job's handoff
- Any deletion, move, rename, refactor, frontend/backend change, path-switch transaction,
  later Phase B job, or Phase C-F work

## Required checks

1. Pinned source/base identity and ancestry checks above.
2. Raw source-copy comparison showing the imported generator differs from the pinned
   source only at the minimal default-path, generated-header, and usage-comment lines.
3. Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\verify-worldjs.ps1`
   at least twice; both runs must pass with the same full output SHA-256.
4. Independently run the repository-local generator in a separate isolated clone-shaped
   temp layout and compare full output bytes to `web/chart/world.js`.
5. Compare payload bytes after line 1 between green base `2726f58:web/chart/world.js`
   and the new generated file; they must be identical.
6. Before/after SHA-256 comparison of the six live `data/` inputs, the checked-in
   generator/output, and the MapLab source/status proving verification changed none of
   them.
7. `node --check web/chart/make-world.js` passes.
8. `git diff --check` passes.
9. Commit scope is exactly the four allowed paths, with no runtime, launcher, host,
   sibling-discovery, input-data, or unrelated changes.

Do not run or cite browser smoke, host launch, or runtime-path verification. The imported
frontend remains dormant until a later separately authorized path-switch transaction.

## Stop conditions

Stop `BLOCKED` without expanding scope if:

- Any pinned source/base identity differs unexpectedly.
- Preserving the WORLD payload requires changing input data or extraction logic.
- Full-byte deterministic generation cannot be achieved with only the minimal
  default-path/header adjustment.
- Passing requires editing `Program.cs`, `play.ps1`, host/browser/runtime paths,
  sibling-discovery logic, `check.ps1`, or any other out-of-scope file.
- Any required check remains red after two focused repair attempts.

## Deliverables

- One bounded worker commit on `codex/pb-root-02-world-generator`.
- Repository-local `web/chart/make-world.js` and its exact deterministic
  `web/chart/world.js` output.
- Dedicated deterministic/source-immutability proof in `tools/verify-worldjs.ps1`.
- Structured handoff at `coordination/handoffs/PB-ROOT-02.md` using the ledger schema.
- No sibling mutation, runtime behavior change, path switch, deletion, or later-phase
  work.
