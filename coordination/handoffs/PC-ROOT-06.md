# PC-ROOT-06 — Assignment packet: mechanical Balance harness (BalanceSim Program.cs) split

Status at packet creation: `ACTIVE` (assignment). This file is committed on the worker
branch before any product change and later completed into the structured handoff
below, following the PC-ROOT-04/PC-ROOT-05 pattern.

- Job: `PC-ROOT-06` (Phase C item 6)
- Owner: `ROOT` (executes locally, no delegation)
- Green base: `900dd254c7003a53fad65068eeab8830941f0bd2` (verified PC-ROOT-05
  integration tip including its ledger mirror)
- Worktree: `D:\FrontMission-RIMG-worktrees\PC-ROOT-06`
- Branch: `codex/pc-root-06-balancesim`
- Target: `tools/MechaTrader.BalanceSim/Program.cs` (901 lines,
  `public static class Program` with public entrypoint `Main` and private static
  helpers), split as `public static partial class Program`.

## Observed pre-split member inventory (full file read 2026-09-05)

The complete file was read before this packet was written. Structure matches the
authorization: single `public static class Program`, no other type in the file.

- Header: 8 `using` directives (`System.Diagnostics`, `MechaTrader.Content`,
  `MechaTrader.Core`, `MechaTrader.Core.Ai`, `MechaTrader.Core.Events`,
  `MechaTrader.Core.Sim`, `MechaTrader.Core.State`, `MechaTrader.Core.World`),
  `namespace MechaTrader.BalanceSim;`, file-level class doc comment (lines 12-18),
  class declaration (line 19), opening brace (line 20).
- Constants: `SimulationDays`, `BotDays`, `BotSeeds` (lines 21-23);
  `MinPriceRatio`, `MaxPriceRatio`, `RequiredSpread`, `RequiredSpreadGoods`,
  `PerformanceBudgetMs` (lines 25-29); `FigureSeed` (line 735, adjacent to
  `WriteFigures`); `MaxHouseRejectionRate` (line 805, adjacent to `AssertPlaytest`).
- Public entrypoint: `Main` (lines 31-117) — stays whole in the original file.
- Private static methods in original order: `RunEconomy` (125-200),
  `MeasureTickCost` (202-216), `PrintOpportunities` (doc 219-223, method 224-298),
  `NaiveHaulProbe` (doc 303-308, method 309-371), `PrintNaiveHauls` (373-404),
  `AssertNaiveHauls` (doc 406-411, method 412-444), `PrintCrew` (doc 446-453,
  method 454-560), `WriteFigures` (doc 562-570, method 571-733), `RepositoryRoot`
  (doc 737, method 738-739), `RunBots` (741-749), `PrintGlobalFlow` (751-769),
  `PrintPriceTable` (771-782), `PrintBotRow` (784-803), `AssertPlaytest` (807-826),
  `AppendPlaytest` (828-884), `Median` (886-892), `Header` (894-900).
- Private nested records in original order: `GoodReport` (119-121),
  `EconomyReport` (123), `NaiveHaul` (doc 300, record 301).
- Closing brace: line 901.

## Authorization scope

Worker write scope (exclusive):

- `tools/MechaTrader.BalanceSim/Program.cs`
- new `.cs` fragment files created by this split, only under
  `tools/MechaTrader.BalanceSim/`
- `coordination/handoffs/PC-ROOT-06.md`

Master remains coordination-only for the whole job.

Prohibited: Phase C item 7 (oversized test classes), phases D-F; modifying
PC-ROOT-01/02/03/04/05 split outputs; other product files, tests, `data/`,
`web/chart/`, `src/`, `D:\FrontMission-MapLab`; FIGURES.md, `check.ps1`, performance
budgets, assertion thresholds, or constant values (`SimulationDays`,
`PerformanceBudgetMs` etc. must be byte-preserved); semantic cleanup, renames,
abstractions, behavior changes; changes to namespace, type names, member names,
signatures, visibility, or public entrypoint `Main`; changes to execution order,
validation order, output order, RNG calls, iteration order, error/output text, or
floating-point operations; deletion/move/rename of existing files; history rewriting,
force pushes, tag creation or movement. Fixture regeneration is limited to the
zero-diff verification flow plus the D-050 user-approved dynamic `build.json` metadata
exception; no fixture change is committed. BalanceSim rewrites FIGURES.md: after each
run only timing-line differences are allowed and must be restored, never committed;
any non-timing FIGURES.md difference is treated as split non-equivalence.

## Mechanical split rules (per D-048/D-050/D-052 precedent)

- Keep `Main` and its entire method body in the original `Program.cs`, unmodified;
  it is the public entrypoint and the execution-order carrier.
- Extract consecutive, complete member blocks in original file order; doc comments
  travel with their owning member; the file-level class doc comment stays in
  `Program.cs`.
- Every method, constant, nested record, and comment block must be preserved
  byte-for-byte; do not reorder members for aesthetics.
- Each fragment copies the original file's `using` block and `namespace`; the only
  textual deltas allowed are the `partial` keyword and the per-fragment file wrappers.
- Preserve original encoding, line endings (CRLF), and whitespace.
- No csproj change (SDK default Compile glob picks up new files).
- Verify by script: read back the split result, concatenate the class bodies, and
  rebuild the original file with no whitespace/token normalization; the original
  member bytes, order, and doc comments must match exactly. Record SHA-256, fragment
  order, and original line ranges in this handoff. Temporary scripts and the raw
  backup stay out of the commit (memory or %TEMP% only, deleted before job end).
- `git diff --check` must pass.
- Gate 3 of `check.ps1` runs the split BalanceSim itself: its console output and
  FIGURES.md content must equal the pre-split program except timing lines.

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
   change (D-050 exception, pre-authorized); restore `build.json` to original Git
   bytes; run `tools/verify-api-shape.ps1` (no -Record); six deterministic fixtures
   unchanged; final `tests/api-fixtures` tracked diff zero.
6. `npm ci --prefix tests/browser`; `npx --prefix tests/browser playwright install
   chromium`; `npm test --prefix tests/browser` — Chromium smoke must pass.
7. `powershell -NoProfile -ExecutionPolicy Bypass -File .\check.ps1` — all nine
   gates PASS (BalanceSim contention may be retried once in isolation after
   confirming no parallel load; no budget or assertion weakening).

After every run: port 5080 not listening (TIME_WAIT excluded), no
`MechaTrader.Host` process, no new temp clone/generator directories from this run,
no unexpected tracked diff, `FIGURES.md` only timing-line changes (restored, never
committed). Temp directories: baseline the existing `%TEMP%\verify-worldjs-*`
directories first (38 pre-existing at 2026-09-05 baseline — not created by this job,
not cleaned); clean only directories evidenced as created by this run (mtime versus
this run's timestamps), exact files first after inspecting absolute paths and
content, then verified-empty nonrecursive directory removal; never delete unknown
directories; distinguish "no new residue from this run" from "the system temp
directory is globally empty".

## Stop conditions

- Any required assertion failing after two focused repairs: mark BLOCKED in the
  ledger, do not integrate or push the red product branch, preserve diagnosis.
- Stop after this item. No item 7, no Phase D-F, no tag, no further work.
