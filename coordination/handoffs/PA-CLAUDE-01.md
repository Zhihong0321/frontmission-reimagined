# Worker handoff: `PA-CLAUDE-01`

- Status: `COMPLETE`
- Worker: `CLAUDE-DESKTOP`
- Runtime/model: Claude Desktop, Claude Sonnet 5 (`claude-sonnet-5`)
- Branch: none (read-only)
- Base commit: `752e5fad7b8e945e9eb3342adabc78c70d95a3c5`
- Result commit: `NONE`

## Files changed

- None. Read-only adversarial review; no product file was edited.

## Checks run

| Command | Result | Evidence |
|---|---|---|
| Read `MIGRATION_PLAN.md`, `MIGRATION_LEDGER.md`, `check.ps1`, `Program.cs`, `play.ps1` | N/A | full text reviewed |
| Read MapLab `chart.html`, `game-bridge.js`, `ops.js` (backup/maplab-final-20260903, local checkout confirmed at `df3c1ba`, clean) | N/A | script-order, global, worker-path, fetch-path audit |
| `grep` command dispatch table in `Commands/CommandProcessor.cs` | N/A | confirms 20 command types, incl. contracts/expos/warehouse/gear |
| `grep` command emission in `Ai/TraderPolicies.cs` | N/A | confirms automated traders emit only 8 of those 20 |
| `wc -l` on Phase C target files | N/A | `CommandProcessor.cs` 920, `ViewBuilder.cs` 1313, `ViewModels.cs` 641, `Definitions.cs` 888, `WorldLoader.cs` 1021 |

## Behavior changes

`NONE`

---

# 1. Material remaining risks not adequately controlled by plan v2

- **R1 — Two independent sibling-discovery mechanisms, plan names one.** `Program.cs::LocateMapLab` (static-file serving) and `play.ps1::Update-ChartData` (content regeneration via `make-world.js`) both independently walk up from their own script location looking for a directory literally named `FrontMission-MapLab`. Plan Phase B step 6, "remove runtime sibling discovery," reads as singular and is only illustrated by the serving risk in "Critical risks and controls." `Update-ChartData` fails **silently** when it can't find a sibling (`chart data left as-is`, exit 0) — so a clean-clone verification (no sibling folder present) will not fail even if the in-repo generator path is completely broken; it will just skip regeneration and serve whatever `world.js` is already on disk (committed or stale). This defeats the clean-clone control for content generation specifically, while appearing to satisfy it for static serving.
- **R2 — Phase C's per-item gate is weaker than Phase D's.** Phase D runs the browser smoke suite after *every* extraction step. Phase C runs only "relevant tests" per item; the full acceptance-and-browser suite runs once, at the *end* of all 7 items. Items 2 (`ViewModels.cs`, 641 lines), 4 (`ViewBuilder.cs`, 1313 lines) and 5 (`CommandProcessor.cs`, 920 lines) are exactly the JSON wire-contract surface the frontend depends on. A contract-breaking change introduced at item 2 can ride undetected through items 3–7 — several mechanical transforms stacked on the first broken one, which is the plan's own named failure outcome, just relocated inside a single phase instead of across phases.
- **R3 — The determinism/save-fixture mechanism cannot see about half the command surface.** `CommandProcessor.cs` dispatches 20 command types. `Ai/TraderPolicies.cs` (`GreedyTrader`, `RandomTrader`, `HouseTrader` — the natural source of any "representative playthrough" fingerprint) only ever emits `Buy`, `Sell`, `Depart`, `Wait`, `HireCrew`, `BuyTruck`, `UpgradeTruck`, `CityFavor`. It never emits `AcceptContract`, `DeliverContract`, `ExpoRegister`, `ExpoList`, `RentWarehouse`, `WarehouseDeposit/Withdraw`, `SetWarehouseSell/Procure`, `BuyGear`, `AssignCrew`, `DismissCrew`, or `SellTruck`. If `PA-KIMI-01`'s fingerprint design is built from a bot playtest (the cheapest natural choice), Phase C's split of `CommandProcessor.cs` is protected in those ~10 command types only by the existing xUnit suite (`ContractTests`, `ExpoTests`, `WarehouseTests`, `MiningTests`, `StationTests`) — not by the mechanism the plan names as the control for "Determinism and save compatibility."
- **R4 — Phase C and Phase D can run concurrently with no rule protecting their shared contract.** Both depend only on Phase B in the ledger's phase table. Ledger concurrency rule 6 ("two active jobs may not own the same file or directory") is purely path-based; it does not model that `View/ViewModels.cs` + `View/ViewBuilder.cs` + `Commands/CommandProcessor.cs` on one side and `ops.js` + `game-bridge.js` on the other are coupled through an implicit, unenforced JSON schema. Two concurrent workers can each produce a commit that is merge-clean and individually green, and still be semantically incompatible once integrated together.
- **R5 — No stated policy for reconciling the integration branch with `master`.** The ledger records `Integration branch: UNSET` and the plan never states whether `master` is frozen, or on what cadence the integration branch is resynced, across what is a multi-checkpoint, sequential (one-large-file-at-a-time) migration likely spanning many sessions. An unmanaged, long-lived divergence between the integration branch and an unfrozen `master` is a slower-motion instance of the exact "long-lived half-migrated state" the plan exists to prevent — it has just moved from the working tree to the branch graph.
- **R6 — Advisory findings have no forcing function.** `PA-CURSOR-01`, `PA-KIMI-01`, and this job explicitly "do not authorize migration work," but nothing requires the coordinator to disposition their output (accept / modify / reject, logged) before Phase A can move to `READY`. Without that, a review can be filed and silently bypassed.

# 2. Unsafe dependencies or ordering between phases

- Phase C and Phase D share only a "depends on B" edge (see R4); nothing orders or mutually excludes them despite the coupling above.
- Inside Phase C, item 5 (`CommandProcessor.cs`) is the file least covered by the fingerprint mechanism (R3) and structurally the most central (every subsystem routes through it), yet it sits mid-sequence behind only a per-item "relevant tests" gate rather than the phase-end full suite.
- Phase E ("verification workflow… add `Fast`, feature-specific, and `Full` entrypoints") depends on C and D but the plan does not pin `Full` to be a strict superset of today's seven `check.ps1` gates plus the new browser suite. Once alternate verification entrypoints exist, later phases have an easy path to running a narrower check than the one that actually protects the product.
- Phase B step ordering imports and repoints before it removes sibling discovery (steps 1–4 before step 6), and the original `D:\FrontMission-MapLab` is explicitly kept on disk throughout. Nothing stops `Play.cmd`/`play.ps1` from being run mid-Phase-B by anyone checking progress; `Update-ChartData` will still prefer the untouched original sibling, so a mid-phase manual playtest can silently exercise the *old* tree while the in-repo copy goes unexercised — false confidence that Phase B is on track.

# 3. Ways parallel workers could produce semantically incompatible but merge-clean commits

- The primary vector is R4: a Phase C worker mechanically splitting `ViewBuilder.cs`/`ViewModels.cs`/`CommandProcessor.cs` and a Phase D worker mechanically splitting `ops.js`/`game-bridge.js` touch disjoint paths (merge-clean by rule 6) but share an unenforced JSON schema.
- A concrete mechanism for that incompatibility to occur *without* any C#-level test noticing: `Program.cs` sets `DefaultIgnoreCondition = JsonIgnoreCondition.Never`, so every DTO property currently serializes, including nulls. A mechanical split performed with IDE-assisted refactoring can easily convert a positional record parameter to a property with a default, add an inferred `[JsonPropertyName]`, or reorder members — all invisible to a C#-only equality/unit test, all capable of changing the emitted JSON that `ops.js` parses by field name. This is exactly why R2 (browser suite only at Phase C's end) compounds rather than mitigates the risk: nothing forces a same-day check of the actual wire format after this class of change.

# 4. Missing rollback, observability, browser, deterministic, save, asset, or clean-clone gates

- Missing: per-item browser-smoke gate in Phase C for items 2, 4, 5 (R2).
- Missing: an explicit, unfiltered `dotnet test tests/MechaTrader.Core.Tests` as the literal meaning of Phase C's "run relevant tests" (R3) — "relevant" is currently left to worker/coordinator judgment.
- Missing: a clean-clone assertion that the in-repo content generator actually ran and produced fresh output, versus silently skipping (R1). Today's soft-warning behavior in `Update-ChartData` cannot distinguish "works" from "quietly broken."
- Missing: a phase-level (not just per-job) stop-loss/rollback procedure — the plan's stop-loss rule is written for one bounded job; there is no named procedure for unwinding an already-tagged `known-green/*` checkpoint if a later phase's fuller verification retroactively falsifies it.
- Missing: any stated cadence or owner for reconciling the integration branch with `master` (R5).
- Missing: a ledger gate tying Phase A's `READY` transition to disposition of the three ready advisory jobs (R6).
- Already adequate, not flagged further: asset quarantine-before-delete, no-force-push/no-history-rewrite, single-writer ledger, per-job stop-loss, and the host-side (`Program.cs`) sibling-serving control.

# 5. Specific plan changes required before Phase A or Phase B may start

1. Name `play.ps1::Update-ChartData`'s sibling walk explicitly in Phase B's step list as a second discovery mechanism to repoint, and change its "generator not found" branch from a soft warning to a hard failure once Phase B's in-repo path is expected to exist.
2. Require the browser smoke suite (not "relevant tests") after Phase C items 2, 4, and 5 specifically, matching Phase D's per-item strictness, because those three files are the JSON wire-contract surface.
3. Replace "run relevant tests" in Phase C's per-item process with an explicit requirement to run the full, unfiltered Core test project after every item.
4. Add a concurrency rule: no Phase D job may be `ACTIVE` while a Phase C job touching `View/ViewModels.cs`, `View/ViewBuilder.cs`, or `Commands/CommandProcessor.cs` has not yet reached a recorded green checkpoint.
5. Record an explicit `master`-freeze-or-resync policy for the duration of Phases A–F before the integration branch is created.
6. Add a ledger rule that Phase A cannot become `READY` until `PA-CURSOR-01`, `PA-KIMI-01`, and `PA-CLAUDE-01` each carry a decision-log disposition.
7. Require `PA-KIMI-01`'s fingerprint/save-fixture set to explicitly enumerate per-command-type coverage, naming which command types (contracts, expos, warehouse, mining, gear, crew assign/dismiss, truck sell) are covered only by the xUnit suite rather than by the fingerprint, so the gap in R3 is a recorded, accepted risk rather than an invisible one.

# 6. Verdict

`REVISE_BEFORE_START`

Plan v2 is substantially more disciplined than a typical migration plan and already controls several of the classic failure modes (unverified baseline, force-push/history-rewrite, per-job stop-loss, asset deletion). The six items above are not process nitpicks; each maps directly onto the plan's own stated failure outcome ("a long-lived half-migrated state where several changes are stacked on top of the first broken change") and each is backed by a concrete file/line-level mechanism found in this repository, not a hypothetical. None require redesigning architecture or adding scope — they are gate placement, wording precision, and one concurrency rule. Recommend folding items 1–7 into a plan v3 before Phase A is marked `READY`.

## Risks and uncertainty

- This review read the `backup/maplab-final-20260903` snapshot via the local `D:\FrontMission-MapLab` checkout, confirmed clean and at `df3c1ba` (matching the ledger's recorded MapLab baseline). It did not re-fetch the GitHub-hosted copies of the branches named in the task; if either diverges from what is on local disk, findings tied to specific line content (R1–R3) should be re-checked against the actual commit used at Phase A start.
- `ops.js` (99 KB) and `chart.html` (93 KB) were audited structurally (script load order, global bindings, worker/asset paths, fetch targets) rather than read in full; the JSON-contract risk (R2/§3) is a structural argument from file size and command-dispatch breadth, not an exhaustive diff of every field `ops.js` reads.

## Out-of-scope findings

- NONE beyond what is already captured above as required output.

## Requested ledger update

Record `PA-CLAUDE-01` as `VERIFIED` in "Ready manual advisory jobs," with result `handoff at coordination/handoffs/PA-CLAUDE-01.md`, verdict `REVISE_BEFORE_START`. Do not mark Phase A `READY` until this handoff, `PA-CURSOR-01`, and `PA-KIMI-01` are each dispositioned in the decision log per item 6 above.
