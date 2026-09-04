# Worker handoff: `PA-ROOT-03`

- Status: `COMPLETE`
- Worker: `ROOT` (Claude Code, Sonnet 5, per `D-029` — Codex remains out of quota)
- Branch: `codex/pa-root-03-determinism-fixtures`
- Base commit: `5f6f50d` (assignment commit containing the task packet)
- Result commit: `defea0d` (branch HEAD; five commits total — see below)

## Files changed

- `tests/MechaTrader.Core.Tests/DeterminismFingerprintTests.cs`
- `tests/MechaTrader.Core.Tests/SaveFixtureTests.cs`
- `tests/MechaTrader.Core.Tests/Fixtures/saves/{day1-new-run,trade-cycle,late-run-mixed}.json`, `manifest.json`
- `tests/MechaTrader.Core.Tests/MechaTrader.Core.Tests.csproj` (added a `ProjectReference` to the new tool)
- `tests/api-fixtures/{new,buy,depart,wait,sell,map,build}.json`
- `tools/MechaTrader.Fingerprint/{MechaTrader.Fingerprint.csproj,Program.cs,Scripts.cs,Fingerprints.cs}` (new console project)
- `tools/verify-worldjs.ps1`, `tools/verify-api-shape.ps1`, `tools/clean-clone-check.ps1`
- `check.ps1` (extended with gates 8-9)
- `MechaTrader.sln` (registered the new console project)
- `coordination/handoffs/PA-ROOT-03.md`

Five commits: `0b82c86` (implementation), `1e7171a` (world.js gate must skip, not fail,
when no MapLab is reachable — found by `clean-clone-check.ps1` itself), `2250df2` (a
Windows PowerShell 5.1 `git clone` stderr-redirect bug in `clean-clone-check.ps1`, also
found by running it), `70edb46` (this handoff), and `defea0d` (`F_content` was sensitive
to git's line-ending checkout mode, not just JSON content — found when the coordinator
fast-forwarded master to this branch and reran `check.ps1` there: the main
`D:\FrontMission-RIMG` checkout and the `D:\FrontMission-RIMG-worktrees\PA-ROOT-03`
checkout of the identical commit hash the same files' bytes differently, purely on line
endings, and one xUnit fact failed on master that had passed in the worktree).

## What this closes

Phase A step 6 of `MIGRATION_PLAN.md`: deterministic state/view fingerprints, save
fixtures, API-shape fixtures, content hashes, generated-world verification, and an
explicit command-coverage matrix — the accepted `PA-KIMI-01` design (`D-015`) plus the
`PA-CLAUDE-01` coverage-disclosure requirement (`D-016` item 7). It also closes Phase A
step 7 (verify from a clean, isolated environment).

## Command-coverage matrix

`Commands/Commands.cs` currently declares **21** command types (not 20 — this handoff
counted them directly from source rather than trusting either advisory doc's figure).
All 21 are `Scripted`: issued directly by `Scripts.RunFullSurfaceScript`, which every
`DeterminismFingerprintTests` fact and the `late-run-mixed` fixture run. None rely solely
on the per-feature xUnit suite for coverage — there is no `FeatureTestsOnly` row.

| Command type | How | What the script does with it |
|---|---|---|
| `BuyCommand` | Scripted | Hauls, funds the warehouse, funds each contract line, and stocks the expo stall |
| `SellCommand` | Scripted | Sells the hauled lot on arrival at each stop |
| `DepartCommand` | Scripted | Two-hop haul from the start city |
| `WaitCommand` | Scripted | Travel days, the expo start delay, and a settling tail |
| `BuyTruckCommand` | Scripted | Buys a second, non-starting truck type |
| `SellTruckCommand` | Scripted | Resells that truck the same day |
| `UpgradeTruckCommand` | Scripted | Attempts to fit Economy Tune to the starting truck from whatever cash remains |
| `BuyGearCommand` | Scripted | Buys the first catalogued gear item |
| `HireCrewCommand` | Scripted | Hires the pool's first affordable+room-aboard candidate, or its first candidate otherwise |
| `DismissCrewCommand` | Scripted | Pays that hire off again while cash is still healthy |
| `AssignCrewCommand` | Scripted | Moves the hire off whatever post hiring defaulted them to |
| `CityFavorCommand` | Scripted | Donates to the start city's governor |
| `RentWarehouseCommand` | Scripted | Rents a storeroom at the start city |
| `WarehouseDepositCommand` | Scripted | Stashes part of the haul |
| `WarehouseWithdrawCommand` | Scripted | Pulls some of it back out |
| `SetWarehouseSellCommand` | Scripted | Sets an auto-sell ask on the stashed good |
| `SetWarehouseProcureCommand` | Scripted | Sets an auto-buy bid on a second good |
| `AcceptContractCommand` | Scripted | Accepts the start city's cheapest-looking board offer |
| `DeliverContractCommand` | Scripted | Attempts to buy that offer's lines locally and deliver the same day |
| `ExpoRegisterCommand` | Scripted | Registers for the next expo cycle at the start city |
| `ExpoListCommand` | Scripted | Lists a good the start city does not make |

**Issuance vs. success are different claims.** A fixed 20,000cr start budget cannot fund
every one of these at once in the same run (a truck flip alone costs 40% of a truck's
price; a signing fee or a "supply" contract can each run into the thousands), so the
script issues every command type unconditionally wherever its target exists at all
(content guarantees a candidate, an offer, and an expo instance always exist at the
start city) rather than only when affordable, and lets `CommandProcessor` decide. At the
frozen golden seed (`424242`), 19 of 21 succeed; `DeliverContractCommand` and
`UpgradeTruckCommand` are issued and legitimately rejected for insufficient credits. A
rejection still exercises `CommandProcessor`'s validation path for that type — which is
what "coverage" means here — and both types' success paths are separately, thoroughly
proven by `ContractTests` and `StationTests` in the existing suite. `DeterminismFingerprintTests.CoverageMatrixNamesEveryLiveCommandTypeExactlyOnce`
asserts the matrix against `Command`'s live subtypes by reflection, so a future command
addition fails this test until dispositioned here, and `EveryScriptedCommandTypeIsActuallyIssued`
asserts every `Scripted` row was really issued.

## Checks run

| Command | Result | Evidence |
|---|---|---|
| `dotnet build MechaTrader.sln -c Release --nologo -v q` | PASS | 0 warnings |
| `dotnet test tests/MechaTrader.Core.Tests/...` | PASS | 239 passed (229 existing + 10 new facts) |
| `tools/verify-worldjs.ps1` | PASS | payload hash matches after a user-authorized one-time regeneration of the (pre-existing, out-of-scope-stale) live `world.js` — see Risks |
| `tools/verify-api-shape.ps1` (record, then verify) | PASS | 7 raw responses recorded at seed 555555; live replay byte-identical to all 6 non-`build` fixtures; `/api/build` shape-checked (git/wall-clock fields excluded from exact match) |
| `powershell -File .\check.ps1` (nine gates) | PASS | all nine green, re-run after `defea0d`, including the two new gates |
| `tools/clean-clone-check.ps1` | PASS | full nine-gate suite green in an isolated full clone (a third, independent checkout), re-run after `defea0d`; `/chart/` correctly 404s (no sibling MapLab reachable — expected pre-migration state); post-run `git status` showed only `FIGURES.md` |
| Port 5080 listener check | PASS | no `LISTENING` entry after every host-launching run above |
| `git diff --check` (each commit) | PASS | no whitespace errors |
| Write-scope review | PASS | only packet-allowed paths touched across all five commits |
| Cross-checkout test run | PASS (after `defea0d`) | `dotnet test` run separately against `D:\FrontMission-RIMG-worktrees\PA-ROOT-03`, `D:\FrontMission-RIMG`, and the clean-clone temp path all agree |

## Behavior changes

`NONE` to product code, data, or the sibling MapLab source. The one file changed inside
`D:\FrontMission-MapLab\` is the **generated** `world.js`, regenerated in place from the
current `data/` — the same action `play.ps1::Update-ChartData` already performs
automatically on every normal launch — under explicit user authorization obtained mid-job
(see Risks; this is why `MapLab.` is not listed as prohibited-and-violated: the packet's
prohibition was written assuming no such regeneration would be needed, and the user
approved this specific exception before it happened).

## Risks and uncertainty

- **A third finding, also resolved:** `F_content` originally hashed each data file's raw
  UTF-8 bytes. Git's checkout line-ending behavior can differ between a clone and a
  worktree of the identical commit — it did here — so the same committed bytes checked
  out as CRLF in one place and LF in another, and the "content" hash differed though the
  JSON was semantically identical. This surfaced only when the coordinator fast-forwarded
  master to this branch and reran `check.ps1` directly on `D:\FrontMission-RIMG` (a
  different checkout than the worktree this job used): one xUnit fact failed there that
  had passed in the worktree. Fixed by normalizing `\r\n` to `\n` before hashing in
  `Fingerprints.FContent`; `F_state`/`F_view` were never affected since they hash
  `JsonSerializer` output over parsed objects, not raw file bytes. Golden values were
  re-captured from both checkouts and confirmed identical, and again from a fresh
  `clean-clone-check.ps1` clone (a third independent checkout).
- **Out-of-scope finding, now resolved with authorization:** `verify-worldjs.ps1`
  initially found the live `D:\FrontMission-MapLab\world.js` genuinely stale relative to
  `data/` — confirmed by hand (regenerating from the main checkout's `data/` produced a
  different payload hash than the live file), not a false positive. This predates this
  job. Regenerating MapLab's `world.js` is outside this packet's write scope
  (`D:\FrontMission-MapLab\**` is listed as prohibited), so this was surfaced to the user
  rather than fixed unilaterally; the user authorized a one-time regeneration, which was
  then performed exactly as `play.ps1` does it (`node make-world.js <dataDir>`, run from
  MapLab so the output lands beside the script). No other MapLab file was touched.
- **A second, more durable finding survived that fix:** `make-world.js` embeds its own
  invocation path as a literal `// GENERATED ... from <dataDir>` comment on line 1 of its
  output. A whole-file hash comparison (my first draft of `verify-worldjs.ps1`) therefore
  reports a false mismatch whenever the check runs from anywhere other than the exact
  absolute path `world.js` was last generated from — for example, any worktree. The
  shipped `verify-worldjs.ps1` hashes only the payload after line 1, which is
  path-independent and was verified consistent when run from both this worktree and the
  main checkout.
- `verify-worldjs.ps1` and the `/chart/` 404 check share the same structural limitation:
  neither can do anything useful without a reachable `FrontMission-MapLab` sibling.
  `verify-worldjs.ps1` treats "no MapLab reachable" as an expected `SKIP`
  (exit 0) rather than a failure — this was not the original design and was found by
  running `clean-clone-check.ps1` against the first commit, which failed there for
  exactly this reason before the fix.
- `check.ps1`'s own header comment and `MIGRATION_LEDGER.md`/`CLAUDE.md` describe a
  "seven-gate" suite; it is now nine. `MIGRATION_LEDGER.md` is updated by this handoff's
  ledger update (coordinator-only edit, done separately). `CLAUDE.md` was not in this
  packet's write scope and still says "seven gates" — flagged as an out-of-scope finding
  below rather than edited.
- `tests/api-fixtures/*.json` and the API-shape gate assume the seed-555555 opening
  scout (`bestProfit > 0`) stays available; if content is retuned such that no profitable
  opening run exists at that seed, `verify-api-shape.ps1` fails loudly with a clear
  message rather than silently, matching `check.ps1`'s own existing convention for the
  same risk.
- `clean-clone-check.ps1` is not wired into `check.ps1`'s default run (per the packet's
  non-goals) — it is a one-time Phase-boundary gate, not a per-run cost, matching how it
  was actually used here (once, to close Phase A step 7).

## Out-of-scope findings

- `CLAUDE.md`'s project brief still says "seven gates, one verdict line" in its Start
  Here and Run/Verify sections; it should be updated to nine now that `check.ps1` has
  grown two gates. Not fixed here: `CLAUDE.md` was not in this packet's allowed write
  scope.

## Requested ledger update

Mark `PA-ROOT-03` `REVIEW` (commits ready for coordinator integration) with evidence that
all 21 command types are scripted, the determinism/save/API/world.js gates are green
(nine-gate `check.ps1`, plus a full isolated-clone run), and Phase A steps 6 and 7 are
closed. Record the `world.js` regeneration and its user authorization in the decision
log, and the `CLAUDE.md` "seven gates" staleness as an open item.
