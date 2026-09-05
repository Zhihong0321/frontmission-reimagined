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

---

## Structured handoff (completed after worker checks)

```text
JOB_ID: PC-ROOT-06
STATUS: COMPLETE
BRANCH: codex/pc-root-06-balancesim
COMMIT: 1a04180 ("Split BalanceSim Program.cs mechanically"; this handoff is
  completed on the REVIEW tip that follows it)
FILES_CHANGED:
  - tools/MechaTrader.BalanceSim/Program.cs
    (901 -> 119 lines; header usings, namespace, file-level class doc, constants
    SimulationDays/BotDays/BotSeeds/MinPriceRatio/MaxPriceRatio/RequiredSpread/
    RequiredSpreadGoods/PerformanceBudgetMs, and the whole public entrypoint Main
    retained; sole textual delta `public static partial class Program` plus the
    removed member blocks)
  - tools/MechaTrader.BalanceSim/ProgramReports.cs   (orig lines 119-218, GoodReport/EconomyReport/RunEconomy/MeasureTickCost)
  - tools/MechaTrader.BalanceSim/ProgramProbes.cs    (orig lines 219-445, PrintOpportunities/NaiveHaul/NaiveHaulProbe/PrintNaiveHauls/AssertNaiveHauls)
  - tools/MechaTrader.BalanceSim/ProgramCrew.cs      (orig lines 446-561, PrintCrew)
  - tools/MechaTrader.BalanceSim/ProgramFigures.cs   (orig lines 562-740, WriteFigures/FigureSeed/RepositoryRoot)
  - tools/MechaTrader.BalanceSim/ProgramBots.cs      (orig lines 741-750, RunBots)
  - tools/MechaTrader.BalanceSim/ProgramPrinters.cs  (orig lines 751-804, PrintGlobalFlow/PrintPriceTable/PrintBotRow)
  - tools/MechaTrader.BalanceSim/ProgramPlaytest.cs  (orig lines 805-885, MaxHouseRejectionRate/AssertPlaytest/AppendPlaytest)
  - tools/MechaTrader.BalanceSim/ProgramHelpers.cs   (orig lines 886-900, Median/Header)
  - coordination/handoffs/PC-ROOT-06.md (this file)
  .csproj untouched (SDK default Compile glob picked up the new files).
  Each fragment wrapper copies the original file's complete 8-using block,
  namespace, and a bare `public static partial class Program` declaration; the
  class doc comment stays only in Program.cs. Inter-member blank separator lines
  ride inside the chunk ranges above (contiguous tiling of original lines
  21-900; reconstruction is a plain concatenation with no separator
  re-insertion).
CHECKS_RUN: (sequential, no parallel runs)
CHECK_RESULTS:
  - Raw-byte split equivalence: original file SHA-256
    a2d7f855df3a10946be3487dcca92dce5c079a3a0de9688af5078c62e2ce7dc8;
    reconstruction from the new Program.cs header (with the `partial` keyword
    reverted) plus the eight fragment class bodies in order, closed with the
    original `}`, is byte-identical (equal byte arrays, no whitespace/token
    normalization); every fragment body was also asserted line-by-line against
    its original line range before the overall hash comparison. Per-fragment
    raw SHA-256:
      Reports   6a8e249d8f410c3c876b3a57eb17a3919e80d4e3e77931d7cbdff5c61587562c
      Probes    0bf27e1cb10f932f98d901b9840969a836bd28be15d9c0d8bb02d8de97d82967
      Crew      15a02affdc588a5c58ea26806a7c692d155dcc22d144d3c2f31dd1feabf8c194
      Figures   5b7fbabc5585e6531254033ecda32f5f84a77c7ee057bfea070ba78ba61d6512
      Bots      f5798b5875e300811b8aa4057ae7cf39dfdc986a8e4696b3851df35c847a81c9
      Printers  755a29b5f466115853bec0b385728517be54a3ba44b56f337a7d1767c17fa3f1
      Playtest  d6c1e19f8c569cf50c8543cc20e3d88e6787535d1155f1f62ed14c621e78c7c7
      Helpers   0f46e6cc5834cb6c7368289e1e68680ab2eea6f04765bc24089ee8611a64cbfa
    New Program.cs SHA-256
    373cc534760b8220a4b98809ed74e4f31aca2d21543d02a7b7456d297664ee66 (119 lines).
  - Dynamic output equality: the pre-split program (checked out from assignment
    commit e59fd7c) and the split program were both run in this worktree and
    their full console outputs compared — identical except the `tick time:`
    lines (159.0 ms vs 285.8 ms, pure timing). Split-generated FIGURES.md vs
    the committed pre-split-generated FIGURES.md differs only in the
    `1000-day tick` timing line (~220 ms -> ~180 ms), restored after every run,
    never committed. BalanceSim exits 0 with BALANCE OK in both states with
    identical figures lines (skilled 566,917 / careless -13,044 / edge 579,961 /
    house 687,071 cr).
  - git diff --check: clean.
  - dotnet build MechaTrader.sln -c Release: Build succeeded, 0 Warning(s),
    0 Error(s) (24.4 s; re-confirmed 0/0 after the comparison round-trip).
  - Full unfiltered Core tests: 239 passed / 0 failed / 0 skipped (5 s).
  - Determinism/save filter: 10/10 passed. Fingerprint regeneration: zero
    tracked diff; F_state a96681c178a462fee913c495428eb5432720edfaad6fb2593663ef5b842bbe99,
    F_view 93a94b5cca687a89c9408b3c84c8599eb23b6c901c888a37497c376206af6626
    (both exactly as pinned).
  - tools/verify-worldjs.ps1: PASS, SHA-256
    26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a.
  - API: -Record PASS (7 fixtures, seed 555555); diff inspection showed only
    dynamic build.json metadata changed (builtAtUtc/builtAgo/branch/commit/log
    for e59fd7c on codex/pc-root-06-balancesim) — the pre-authorized D-050
    exception; the other six deterministic fixtures were untouched. build.json
    restored to original Git bytes; baseline verify PASS; final
    tests/api-fixtures tracked diff zero.
  - Browser: npm ci 3 packages 0 vulnerabilities; chromium present;
    npm test 1/1 passed (30.5 s, test 20.5 s), host banner `e59fd7c on
    codex/pc-root-06-balancesim (+9 uncommitted)`.
  - Nine gates: all PASS (gate 3 BalanceSim tick 177.6 ms, skilled 566,917,
    careless -13,044, house 687,071; host buy-haul-sell with illegal move
    refused; recruitment; city stats/supply; build page banner e59fd7c;
    world.js hash; API baseline).
  - Hygiene after every run: port 5080 not listening (TIME_WAIT excluded), no
    MechaTrader.Host process, no unexpected tracked diff, FIGURES.md timing
    restored. Temp directories: baseline 38 pre-existing verify-worldjs-*
    directories untouched; this worker phase created exactly 2 (15:02:48 and
    15:05:53 mtimes, each the known clone-a / different-absolute-clone-b pair
    of generated 8587-byte world.js files), cleaned by exact-file deletion then
    verified-empty nonrecursive directory removal back to the 38 baseline.
  - Worker tree: 690 files = 681 base + 8 fragments + 1 handoff.
BEHAVIOR_CHANGES: NONE (raw-byte source equivalence plus runtime console/FIGURES
  equality as evidenced above; partial keyword is the sole textual delta).
RISKS: none known. Fragment files carry the full original using block (superset
  of each fragment's needs); no build warnings result and the union cannot
  introduce ambiguities absent from the original file.
OUT_OF_SCOPE_FINDINGS: D: ran out of disk space during initial worktree
  creation (environmental, resolved by the user freeing 10+ GB; a stranded
  branch pointing at the green base was reused). No product or scope findings.
LEDGER_UPDATE_REQUEST: Mark PC-ROOT-06 REVIEW then MERGED/VERIFIED after
  integration checks; record worker commit 1a04180, the integration merge, and
  tree count 681 + 8 fragments + 1 handoff (690) in the integration queue and
  verification ledger; add D-054.
```

---

## Coordinator REVIEW and merge decision (ROOT, 2026-09-05)

`ACCEPT — proceed to integration merge.`

- Scope: `git diff --name-status 900dd254..1a04180` touches only
  `tools/MechaTrader.BalanceSim/Program.cs`, the eight new
  `tools/MechaTrader.BalanceSim/Program*.cs` fragments, and this handoff
  (added by assignment commit e59fd7c) — exactly the authorized write scope.
  No prohibited path, no deletion, no move, no rename, no
  test/data/web/src/MapLab change, `.csproj` untouched, no FIGURES.md or
  check.ps1 change, no constant value changed.
- Ancestry: assignment packet e59fd7c and worker implementation
  1a04180 descend from green base 900dd254c7003a53fad65068eeab8830941f0bd2.
- Equivalence evidence: reviewed and accepted — reconstruction from the split
  output is byte-identical to th

---

## Structured handoff (completed after worker checks)

```text
JOB_ID: PC-ROOT-06
STATUS: COMPLETE
BRANCH: codex/pc-root-06-balancesim
COMMIT: 1a04180 ("Split BalanceSim Program.cs mechanically"; this handoff is
  completed on the REVIEW tip that follows it)
FILES_CHANGED:
  - tools/MechaTrader.BalanceSim/Program.cs
    (901 -> 119 lines; header usings, namespace, file-level class doc, constants
    SimulationDays/BotDays/BotSeeds/MinPriceRatio/MaxPriceRatio/RequiredSpread/
    RequiredSpreadGoods/PerformanceBudgetMs, and the whole public entrypoint Main
    retained; sole textual delta `public static partial class Program` plus the
    removed member blocks)
  - tools/MechaTrader.BalanceSim/ProgramReports.cs   (orig lines 119-218, GoodReport/EconomyReport/RunEconomy/MeasureTickCost)
  - tools/MechaTrader.BalanceSim/ProgramProbes.cs    (orig lines 219-445, PrintOpportunities/NaiveHaul/NaiveHaulProbe/PrintNaiveHauls/AssertNaiveHauls)
  - tools/MechaTrader.BalanceSim/ProgramCrew.cs      (orig lines 446-561, PrintCrew)
  - tools/MechaTrader.BalanceSim/ProgramFigures.cs   (orig lines 562-740, WriteFigures/FigureSeed/RepositoryRoot)
  - tools/MechaTrader.BalanceSim/ProgramBots.cs      (orig lines 741-750, RunBots)
  - tools/MechaTrader.BalanceSim/ProgramPrinters.cs  (orig lines 751-804, PrintGlobalFlow/PrintPriceTable/PrintBotRow)
  - tools/MechaTrader.BalanceSim/ProgramPlaytest.cs  (orig lines 805-885, MaxHouseRejectionRate/AssertPlaytest/AppendPlaytest)
  - tools/MechaTrader.BalanceSim/ProgramHelpers.cs   (orig lines 886-900, Median/Header)
  - coordination/handoffs/PC-ROOT-06.md (this file)
  .csproj untouched (SDK default Compile glob picked up the new files).
  Each fragment wrapper copies the original file's complete 8-using block,
  namespace, and a bare `public static partial class Program` declaration; the
  class doc comment stays only in Program.cs. Inter-member blank separator lines
  ride inside the chunk ranges above (contiguous tiling of original lines
  21-900; reconstruction is a plain concatenation with no separator
  re-insertion).
CHECKS_RUN: (sequential, no parallel runs)
CHECK_RESULTS:
  - Raw-byte split equivalence: original file SHA-256
    a2d7f855df3a10946be3487dcca92dce5c079a3a0de9688af5078c62e2ce7dc8;
    reconstruction from the new Program.cs header (with the `partial` keyword
    reverted) plus the eight fragment class bodies in order, closed with the
    original `}`, is byte-identical (equal byte arrays, no whitespace/token
    normalization); every fragment body was also asserted line-by-line against
    its original line range before the overall hash comparison. Per-fragment
    raw SHA-256:
      Reports   6a8e249d8f410c3c876b3a57eb17a3919e80d4e3e77931d7cbdff5c61587562c
      Probes    0bf27e1cb10f932f98d901b9840969a836bd28be15d9c0d8bb02d8de97d82967
      Crew      15a02affdc588a5c58ea26806a7c692d155dcc22d144d3c2f31dd1feabf8c194
      Figures   5b7fbabc5585e6531254033ecda32f5f84a77c7ee057bfea070ba78ba61d6512
      Bots      f5798b5875e300811b8aa4057ae7cf39dfdc986a8e4696b3851df35c847a81c9
      Printers  755a29b5f466115853bec0b385728517be54a3ba44b56f337a7d1767c17fa3f1
      Playtest  d6c1e19f8c569cf50c8543cc20e3d88e6787535d1155f1f62ed14c621e78c7c7
      Helpers   0f46e6cc5834cb6c7368289e1e68680ab2eea6f04765bc24089ee8611a64cbfa
    New Program.cs SHA-256
    373cc534760b8220a4b98809ed74e4f31aca2d21543d02a7b7456d297664ee66 (119 lines).
  - Dynamic output equality: the pre-split program (checked out from assignment
    commit e59fd7c) and the split program were both run in this worktree and
    their full console outputs compared - identical except the `tick time:`
    lines (159.0 ms vs 285.8 ms, pure timing). Split-generated FIGURES.md vs
    the committed pre-split-generated FIGURES.md differs only in the
    `1000-day tick` timing line (~220 ms -> ~180 ms), restored after every run,
    never committed. BalanceSim exits 0 with BALANCE OK in both states with
    identical figures lines (skilled 566,917 / careless -13,044 / edge 579,961 /
    house 687,071 cr).
  - git diff --check: clean.
  - dotnet build MechaTrader.sln -c Release: Build succeeded, 0 Warning(s),
    0 Error(s) (24.4 s; re-confirmed 0/0 after the comparison round-trip).
  - Full unfiltered Core tests: 239 passed / 0 failed / 0 skipped (5 s).
  - Determinism/save filter: 10/10 passed. Fingerprint regeneration: zero
    tracked diff; F_state a96681c178a462fee913c495428eb5432720edfaad6fb2593663ef5b842bbe99,
    F_view 93a94b5cca687a89c9408b3c84c8599eb23b6c901c888a37497c376206af6626
    (both exactly as pinned).
  - tools/verify-worldjs.ps1: PASS, SHA-256
    26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a.
  - API: -Record PASS (7 fixtures, seed 555555); diff inspection showed only
    dynamic build.json metadata changed (builtAtUtc/builtAgo/branch/commit/log
    for e59fd7c on codex/pc-root-06-balancesim) - the pre-authorized D-050
    exception; the other six deterministic fixtures were untouched. build.json
    restored to original Git bytes; baseline verify PASS; final
    tests/api-fixtures tracked diff zero.
  - Browser: npm ci 3 packages 0 vulnerabilities; chromium present;
    npm test 1/1 passed (30.5 s, test 20.5 s), host banner `e59fd7c on
    codex/pc-root-06-balancesim (+9 uncommitted)`.
  - Nine gates: all PASS (gate 3 BalanceSim tick 177.6 ms, skilled 566,917,
    careless -13,044, house 687,071; host buy-haul-sell with illegal move
    refused; recruitment; city stats/supply; build page banner e59fd7c;
    world.js hash; API baseline).
  - Hygiene after every run: port 5080 not listening (TIME_WAIT excluded), no
    MechaTrader.Host process, no unexpected tracked diff, FIGURES.md timing
    restored. Temp directories: baseline 38 pre-existing verify-worldjs-*
    directories untouched; this worker phase created exactly 2 (15:02:48 and
    15:05:53 mtimes, each the known clone-a / different-absolute-clone-b pair
    of generated 8587-byte world.js files), cleaned by exact-file deletion then
    verified-empty nonrecursive directory removal back to the 38 baseline.
  - Worker tree: 690 files = 681 base + 8 fragments + 1 handoff.
BEHAVIOR_CHANGES: NONE (raw-byte source equivalence plus runtime console/FIGURES
  equality as evidenced above; partial keyword is the sole textual delta).
RISKS: none known. Fragment files carry the full original using block (superset
  of each fragment's needs); no build warnings result and the union cannot
  introduce ambiguities absent from the original file.
OUT_OF_SCOPE_FINDINGS: D: ran out of disk space during initial worktree
  creation (environmental, resolved by the user freeing 10+ GB; a stranded
  branch pointing at the green base was reused). No product or scope findings.
LEDGER_UPDATE_REQUEST: Mark PC-ROOT-06 REVIEW then MERGED/VERIFIED after
  integration checks; record worker commit 1a04180, the integration merge, and
  tree count 681 + 8 fragments + 1 handoff (690) in the integration queue and
  verification ledger; add D-054.
```

---

## Coordinator REVIEW and merge decision (ROOT, 2026-09-05)

`ACCEPT - proceed to integration merge.`

- Scope: `git diff --name-status 900dd254..1a04180` touches only
  `tools/MechaTrader.BalanceSim/Program.cs`, the eight new
  `tools/MechaTrader.BalanceSim/Program*.cs` fragments, and this handoff
  (added by assignment commit e59fd7c) - exactly the authorized write scope.
  No prohibited path, no deletion, no move, no rename, no
  test/data/web/src/MapLab change, `.csproj` untouched, no FIGURES.md or
  check.ps1 change, no constant value changed.
- Ancestry: assignment packet e59fd7c and worker implementation
  1a04180 descend from green base 900dd254c7003a53fad65068eeab8830941f0bd2.
- Equivalence evidence: reviewed and accepted - reconstruction from the split
  output is byte-identical to the original file (SHA-256
  a2d7f855df3a10946be3487dcca92dce5c079a3a0de9688af5078c62e2ce7dc8), with the
  sole textual deltas being the `partial` keyword and per-fragment
  using/namespace wrappers, per the D-048/D-050/D-052 precedent; runtime
  console output and FIGURES.md content equal the pre-split program except
  timing lines.
- Worker checks: all seven sequential gates green as recorded above; hygiene
  checks (port, process, FIGURES, temp residue) clean; API fixture handling
  stayed inside the pre-authorized D-050 dynamic build.json exception with
  zero final diff.
- Worker tree: 690 files = 681 base + 1 handoff + 8 fragments.
- Decision: ordinary `git merge --no-ff codex/pc-root-06-balancesim` into
  `integration` in `D:\FrontMission-RIMG-worktrees\PB-INTEGRATION-01` (per the
  D-046 lesson: no plumbing, no tree reconstruction), after confirming the
  worktree is on `integration`, tracked-clean, and origin/integration is
  un-advanced (resetting to origin/integration only after identity and
  cleanliness are confirmed). Expected two-parent merge, merged tree 690
  files, then a full sequential repeat of the worker checks before any ledger
  update or push. No tag. Stop after this item.
