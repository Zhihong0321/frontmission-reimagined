# PC-ROOT-05 — Assignment packet: mechanical CommandProcessor.cs split

Status at packet creation: `ACTIVE` (assignment). This file is committed on the worker
branch before any product change and later completed into the structured handoff
below, following the PC-ROOT-04 pattern.

- Job: `PC-ROOT-05` (Phase C item 5)
- Owner: `ROOT` (executes locally, no delegation)
- Green base: `b086e6c063c4dc62385e19beba2fe5654feff55f` (verified PC-ROOT-04
  integration tip)
- Worktree: `D:\FrontMission-RIMG-worktrees\PC-ROOT-05`
- Branch: `codex/pc-root-05-commandprocessor`
- Target: `src/MechaTrader.Core/Commands/CommandProcessor.cs` (920 lines,
  `public static class CommandProcessor` with a command-switch `Execute` and private
  handlers/helpers), split as `public static partial class CommandProcessor`.

## Authorization scope

Worker write scope (exclusive):

- `src/MechaTrader.Core/Commands/CommandProcessor.cs`
- new `.cs` fragment files created by this split, only under
  `src/MechaTrader.Core/Commands/`
- `coordination/handoffs/PC-ROOT-05.md`

Master remains coordination-only for the whole job.

Prohibited: Phase C item 6 (Balance harness), item 7, phases D-F; modifying
PC-ROOT-01/02/03/04 split outputs; other product files, tests, `data/`, `web/chart/`,
`D:\FrontMission-MapLab`; semantic cleanup, renames, abstractions, behavior changes;
changes to namespace, type names, member names, signatures, visibility, or public
entrypoints; changes to command dispatch order, validation order, state-write order,
event order, RNG calls, iteration order, error text, or floating-point operations;
deletion/move/rename of existing files; history rewriting, force pushes, tag creation
or movement. Fixture regeneration is limited to the zero-diff verification flow plus
the D-050 user-approved dynamic `build.json` metadata exception; no fixture change is
committed.

## Mechanical split rules (per D-048/D-049 precedent)

- Keep `Execute` and its entire command switch in the original
  `CommandProcessor.cs`, unmodified.
- Extract consecutive, complete member blocks in original file order; doc comments
  travel with their owning member.
- Every method, field, nested type, and comment block must be preserved byte-for-byte;
  do not reorder members for aesthetics.
- Each fragment copies the required `using` directives and `namespace` from the
  original file; the only textual deltas allowed are the `partial` keyword and the
  per-fragment file wrappers.
- Preserve original encoding, line endings (CRLF), and whitespace.
- No csproj change (SDK default Compile glob picks up new files).
- Verify by script: read back the fragments, reassemble the class body, and compare
  against the original raw bytes with no whitespace/token normalization; record
  SHA-256, fragment order, and original line ranges in this handoff. Temporary
  scripts and the raw backup stay out of the commit (memory only).
- `git diff --check` must pass.

## Required sequential worker checks (no parallel runs)

1. `dotnet build MechaTrader.sln -c Release` — 0 warnings, 0 errors.
2. `dotnet test tests/MechaTrader.Core.Tests/MechaTrader.Core.Tests.csproj -c
   Release --no-build` — 239/239, unfiltered.
3. Determinism/save filter — 10/10; `dotnet run --project
   tools/MechaTrader.Fingerprint -c Release --no-build` — zero tracked diff;
   F_state `a96681c178a462fee913c495428eb5432720edfaad6fb2593663ef5b842bbe99`,
   F_view `93a94b5cca687a89c9408b3c84c8599eb23b6c901c888a37497c376206af6626`.
4. `tools/verify-worldjs.ps1` — SHA-256
   `26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a`.
5. `tools/verify-api-shape.ps1 -Record`; only dynamic `build.json` metadata may
   change (D-050 exception); restore `build.json` to original Git bytes; run
   `tools/verify-api-shape.ps1` (no -Record); six deterministic fixtures unchanged;
   final `tests/api-fixtures` tracked diff zero.
6. `npm ci --prefix tests/browser`; `npx --prefix tests/browser playwright install
   chromium`; `npm test --prefix tests/browser` — Chromium smoke must pass.
7. `powershell -NoProfile -ExecutionPolicy Bypass -File .\check.ps1` — all nine
   gates PASS (BalanceSim contention may be retried once in isolation; no budget or
   assertion weakening).

After every run: port 5080 not listening (TIME_WAIT excluded), no `MechaTrader.Host`
process, no new temp clone/generator directories from this run, no unexpected tracked
diff, `FIGURES.md` only timing-line changes (restored, never committed). Temp
directories: baseline the existing `%TEMP%\verify-worldjs-*` directories first; clean
only directories evidenced as created by this run, exact files first, then
verified-empty nonrecursive directory removal; never delete unknown directories.

## Stop conditions

- Any required assertion failing after two focused repairs: mark BLOCKED in the
  ledger, do not integrate or push the red product branch, preserve diagnosis.
- Stop after this item. No item 6/7, no Phase D-F, no tag, no further work.

---

## Structured handoff (completed after worker checks)

(pending — filled in below after checks and worker commit)
