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

```text
JOB_ID: PC-ROOT-05
STATUS: COMPLETE
BRANCH: codex/pc-root-05-commandprocessor
COMMIT: (this commit — "Split CommandProcessor.cs mechanically")
FILES_CHANGED:
  - src/MechaTrader.Core/Commands/CommandProcessor.cs
    (881 member lines moved out; sole textual delta: `public static partial class`;
    Execute switch, class doc comment, usings, namespace untouched; 39 lines remain)
  - src/MechaTrader.Core/Commands/CommandProcessorTrade.cs      (orig lines 41-203,  Buy/Sell)
  - src/MechaTrader.Core/Commands/CommandProcessorTravel.cs     (orig lines 205-298, Depart/DescribeHere/Wait)
  - src/MechaTrader.Core/Commands/CommandProcessorCrew.cs       (orig lines 300-423, HireCrew/AssignCrew/DismissCrew)
  - src/MechaTrader.Core/Commands/CommandProcessorTruck.cs      (orig lines 425-505, BuyTruck/SellTruck/UpgradeTruck)
  - src/MechaTrader.Core/Commands/CommandProcessorGear.cs       (orig lines 507-533, BuyGear)
  - src/MechaTrader.Core/Commands/CommandProcessorFavor.cs      (orig lines 535-617, Favor)
  - src/MechaTrader.Core/Commands/CommandProcessorHelpers.cs    (orig lines 619-641, GrantDuePermits/RequireParkedCity)
  - src/MechaTrader.Core/Commands/CommandProcessorWarehouse.cs  (orig lines 643-759, RentWarehouse/Deposit/Withdraw/SetWarehousePrice)
  - src/MechaTrader.Core/Commands/CommandProcessorContract.cs   (orig lines 761-850, AcceptContract/DeliverContract)
  - src/MechaTrader.Core/Commands/CommandProcessorExpo.cs       (orig lines 852-919, ExpoRegister/ExpoList)
  - coordination/handoffs/PC-ROOT-05.md (this file)
  .csproj untouched (SDK default Compile glob picked up the new files).

CHECKS_RUN: (sequential, no parallel runs)
CHECK_RESULTS:
  - Raw-byte split equivalence: original file SHA-256
    f478a037c73980ce77180ca1fb9222cb5339a5ab6b8b322e0bf3b4812dd7622d;
    reconstruction from the new CommandProcessor.cs header (with the `partial`
    keyword reverted) plus the ten fragment class bodies, joined with the original
    blank separator lines, is byte-identical (SequenceEqual over raw UTF-8 bytes,
    no whitespace/token normalization). Per-fragment raw SHA-256:
      Trade     70d2a45f5127f42d1e9e846da423af4ff502c7e1472aa0149d12a9aac2e8cec0
      Travel    eb3a2af73ba8af7b2fcf9df5a49c7ad9269a67bb85b4c8bce313ae790a2e287d
      Crew      cd15540a784030bd6a06c04cbd764a3e67f020a561e8e02e0a4c571d7f13e5b6
      Truck     da7403b4ce566cd5074ef2e1bde2e53aba591d6fc0d9438f45f9a2385c711f64
      Gear      c90bead703a8205690d28a1eb3030a3ace8b2f20c73c502fb720b1719d7369d6
      Favor     a217a65a87d7da6b029c699924c5a3616f7cb2f3f8ac29a022e9859c3ac0819b
      Helpers   4ca663a84e8fcd4448a8c6fc2d30fb7dd22fdc886e29ac4917ebeb9f7c102fbe
      Warehouse af1187319548386c35e64cbad0d607a95e82cf1c2cae665e85b4bcdafd10d423
      Contract  75a03106551d977b53fa92df8b8fdfdd73cdcb5abef59389e39cba07f5feb2c9
      Expo      19627166651a1b99984eddeca2d0a8093c72e64f0e071634ab43271c1bd360ba
    New CommandProcessor.cs SHA-256
    fa68a8aec9907ee84caa382940a3218fa27cd7c66c456ca2e9eeceae5cc6176c.
    Fragment bodies were also asserted line-by-line against the original line
    ranges before any overall hash comparison.
  - git diff --check: clean.
  - dotnet build MechaTrader.sln -c Release: Build succeeded, 0 Warning(s),
    0 Error(s) (31.7 s).
  - Full unfiltered Core tests: 239 passed / 0 failed / 0 skipped (8 s).
  - Determinism/save filter: 10/10 passed. Fingerprint regeneration: zero tracked
    diff; F_state a96681c178a462fee913c495428eb5432720edfaad6fb2593663ef5b842bbe99,
    F_view 93a94b5cca687a89c9408b3c84c8599eb23b6c901c888a37497c376206af6626
    (both exactly as pinned).
  - tools/verify-worldjs.ps1: PASS, SHA-256
    26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a.
  - API: -Record PASS (7 fixtures, seed 555555); diff inspection showed only
    tests/api-fixtures/build.json dynamic runtime metadata changed (branch
    codex/pc-root-05-commandprocessor, commit 7f04962, timestamps, log) — the
    exact D-050 user-approved exception; the six deterministic fixtures were
    byte-identical. build.json restored to original Git bytes; verify-api-shape.ps1
    PASS; final tests/api-fixtures tracked diff zero. No fixture change committed.
  - Browser: npm ci 3 packages 0 vulnerabilities; Playwright Chromium installed;
    smoke 1/1 passed (27.0 s total, test 15.6 s; host banner "0.5.0-alpha built
    3 minutes ago from 7f04962 on codex/pc-root-05-commandprocessor (+11
    uncommitted)").
  - Full nine-gate check.ps1: ALPHA 1 ACCEPTED, all gates PASS (BalanceSim
    151.3 ms; buy-haul-sell 139 Armour Laminate to Wien 10907 cr, illegal move
    refused; recruitment; city stats/supply; build page banner 7f04962; world.js
    and API gates green).
  - Post-run hygiene: port 5080 no LISTEN (TIME_WAIT only excluded — none
    observed); no MechaTrader.Host process; FIGURES.md showed only the expected
    timing-line change and was restored via git checkout (never committed);
    tracked diff after restoration = the split files only.
  - Temp directory evidence: baseline counted 38 pre-existing verify-worldjs-*
    directories in %TEMP% (matching D-050; left untouched). This run added
    exactly 2 (verifier standalone run 10:27:13 + check.ps1 world gate 10:30:00,
    confirmed by directory and world.js mtimes; the next-newest were 08:54/08:55
    from PC-ROOT-04). Cleanup per D-050 procedure: 4 exact generated world.js
    files deleted by absolute path, remaining file count in both trees verified 0,
    then every subdirectory verified empty and removed non-recursively layer by
    layer. Final count back to exactly 38; no other temp clones or generator
    directories created by this run.
BEHAVIOR_CHANGES: NONE (mechanical split only; validation-before-mutation
  semantics, dispatch order, member order, error text, and all bytes preserved)
RISKS: None known. The split adds the C#-sanctioned `partial` keyword and
  per-fragment using/namespace wrappers; no name, signature, visibility, ordering,
  or behavior token changed.
OUT_OF_SCOPE_FINDINGS: None. The split script and original-file backup were kept
  only in %TEMP% outside the repository and are deleted after use; they are not
  part of any commit.
LEDGER_UPDATE_REQUEST: Mark PC-ROOT-05 REVIEW then MERGED/VERIFIED after
  integration checks; record worker commit, integration merge, and tree count
  670 + 10 fragments + 1 handoff-related change (handoff already existed; expect
  681 files) in the integration queue and verification ledger; add D-052.
```

Note on tree count: the assignment packet commit (7f04962) added this handoff file
to the 670-file tree (671); the split then replaces one tracked file and adds ten
fragments, so the worker-tip tree must hold 681 files. The integration merge adds
no file beyond the worker tip.

---

## Coordinator REVIEW and merge decision (ROOT, 2026-09-05)

`ACCEPT — proceed to integration merge.`

- Scope: `git diff --name-status b086e6c..3c5f014` touches only
  `src/MechaTrader.Core/Commands/CommandProcessor.cs`, the ten new
  `src/MechaTrader.Core/Commands/CommandProcessor*.cs` fragments, and this
  handoff — exactly the authorized write scope. No prohibited path, no deletion,
  no move, no rename, no test/data/web/MapLab change, `.csproj` untouched.
- Ancestry: worker tip `3c5f01413188176f0b0360dc2606d3f5df105cce` descends from
  assignment `7f04962` and green base `b086e6c063c4dc62385e19beba2fe5654feff55f`.
- Equivalence evidence: reviewed and accepted — reconstruction from the split
  output is byte-identical to the original file (SHA-256
  `f478a037c73980ce77180ca1fb9222cb5339a5ab6b8b322e0bf3b4812dd7622d`), with the
  sole textual deltas being the `partial` keyword and per-fragment
  using/namespace wrappers, per the D-048/D-049 precedent.
- Worker checks: all seven sequential gates green as recorded above; hygiene
  checks (port, process, FIGURES, temp residue) clean; API fixture handling
  stayed inside the D-050 dynamic build.json exception with zero final diff.
- Worker tree: 681 files = 670 base + 1 handoff + 10 fragments.
- Decision: ordinary `git merge --no-ff codex/pc-root-05-commandprocessor` into
  `integration` in `D:\FrontMission-RIMG-worktrees\PB-INTEGRATION-01` (per the
  D-046 lesson: no plumbing, no tree reconstruction), expected two-parent merge,
  merged tree 681 files, then a full sequential repeat of the worker checks
  before any ledger update or push. No tag. Stop after this item.
