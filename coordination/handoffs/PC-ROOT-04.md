# PC-ROOT-04 assignment / handoff

JOB_ID: PC-ROOT-04
STATUS: BLOCKED (awaiting explicit API build.json exception)
OWNER: ROOT (coordinator executes in isolated worker worktree; no delegated worker)
GREEN_BASE: c954cb350b60ce6239ef6b8d604da5be4c7d162d
BRANCH: codex/pc-root-04-viewbuilder
WORKTREE: D:\FrontMission-RIMG-worktrees\PC-ROOT-04

Read the complete canonical MIGRATION_PLAN.md and MIGRATION_LEDGER.md before work.
Only split src/MechaTrader.Core/View/ViewBuilder.cs mechanically into new Core .cs files;
the only other worker write is this handoff. Preserve member bytes/order/docs and line
endings. Use public static partial class and original header usings as needed (D-048).
No existing-file deletion/move/rename, no semantic cleanup, no other product/test/data/
web/chart/MapLab or prior PC-ROOT-01/02/03 fragment changes. No tags, history rewriting,
force push, Phase C items 5-7 or Phase D-F.

Run sequentially in worker and integrated states: Release build (zero warnings), full
unfiltered Core tests (239), determinism/save filter, Fingerprint regeneration (zero diff),
verify-worldjs.ps1 (26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a),
verify-api-shape.ps1 record then verify (zero fixture diff), npm ci / Playwright Chromium /
browser smoke, complete nine-gate check.ps1. Prove byte equivalence and remove temporary
comparison script/backup before commit; preserve evidence here. Restore FIGURES timing
only; require diff check, port 5080 free, no Host process or new temporary clones.
Stop on assertion remaining red after two focused repairs; preserve diagnosis, do not
integrate/push failed product state. Coordinator uses normal no-ff merge, verifies full
tree count (662 + new .cs fragments + this handoff), repeats checks, mirrors ledger bytes
on master/integration, pushes fast-forward and verifies remote refs/parity. Then stop.

## Mechanical equivalence evidence

Original source has 1313 code lines plus trailing blank line. Eight partial fragments in original order: ViewBuilder.cs (14-140), ViewBuilderMarket.cs (141-323), ViewBuilderRoutes.cs (324-476), ViewBuilderCity.cs (477-711), ViewBuilderCrew.cs (712-978), ViewBuilderMap.cs (979-1074), ViewBuilderStation.cs (1075-1137), ViewBuilderContracts.cs (1138-1312).

PASS: all 1299 class-body lines concatenate byte-for-byte, with no whitespace normalization; body SHA-256 41bd5b6759f1ff6af10f34992c2b04ff27e8db9cb6e358774524d4bc866d79e4. Original header plus concatenated read-back bodies plus original closing tail reconstruct the entire original file exactly. CRLF preserved, all member/doc blocks retain order. Only class partial keyword and copied four usings/namespace/class wrappers added. SDK default Compile glob verified; csproj unchanged. Script and original backup existed only in process memory, so no temporary files require deletion.

Cleanup baseline correction: initial coordinator check incorrectly treated all pre-existing verify-worldjs temp directories as this job's residue. Before any world verifier run, found 38 old directories (latest creation 2026-09-05T08:55:51.4690794+08:00). These are outside job scope and left untouched. Subsequent checks compare against this baseline; no claim of globally zero historical temp directories.

Worker generator gate passed its pinned hash, but the existing verifier left two generated
world.js copies in task-created verify-worldjs-5a1ad765cce84770b7d1ea8b23f31663. Automatic
approval review rejected recursive deletion (reason: blocked by policy). After inventory
confirmed only the two 8587-byte generated files and empty parent directories remained,
coordinator removed the two exact files and then verified-empty directories non-recursively.
Cleanup passed. No source change or verifier repair was made; 38 older directories remain
untouched. This corrects the initial overly broad cleanup assertion, which had flagged
historical directories before any generator ran. Subsequent checks track new residue only.
API record gate: six deterministic API fixtures remained byte-identical. Only build.json
changed (dynamic commit/branch/time/log metadata), consistent with the verifier's existing
shape-only build contract. User prompt says ANY fixture diff is a red light, so integration
is held pending explicit clarification; no exception is silently assumed. Original
build.json restored before verification, and no fixture changes will be committed.
## Structured worker result

JOB_ID: PC-ROOT-04
STATUS: BLOCKED
BRANCH: codex/pc-root-04-viewbuilder
COMMIT: Not created; product work remains uncommitted pending strict fixture-gate clarification.
FILES_CHANGED: ViewBuilder.cs plus ViewBuilderMarket.cs, ViewBuilderRoutes.cs,
ViewBuilderCity.cs, ViewBuilderCrew.cs, ViewBuilderMap.cs, ViewBuilderStation.cs,
ViewBuilderContracts.cs; this handoff only.
CHECKS_RUN: raw byte reconstruction; Release build; unfiltered Core tests;
determinism/save filter; Fingerprint all; world verifier; API -Record then original-baseline
verify; npm ci; Playwright Chromium install; browser smoke; full nine-gate check.ps1;
diff, fixture, FIGURES, port/process and new-temp-directory cleanup.
CHECK_RESULTS: Release 0 warnings/errors; Core 239/239; filter 10/10; Fingerprint fixtures
zero diff (F_state a96681c178a462fee913c495428eb5432720edfaad6fb2593663ef5b842bbe99;
F_view 93a94b5cca687a89c9408b3c84c8599eb23b6c901c888a37497c376206af6626);
world SHA-256 26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a;
API baseline verify PASS, but literal -Record zero-diff assertion RED only for dynamic
build.json. Six deterministic fixtures unchanged; original build.json restored.
Browser 1/1 in 38.7 s (test 30.1 s); nine gates all PASS (BalanceSim 308.4 ms).
FIGURES timing restored. Port 5080 free, no Host, no new temp clones. Second verifier
residue verify-worldjs-3f7947954d7845d09a5832612ccb34bf also inventoried and removed
by exact-file deletion followed by checked-empty, nonrecursive directory removal.
BEHAVIOR_CHANGES: NONE
RISKS: Integration verification has NOT run. Product remains on green-base worker
checkout with uncommitted split; integration unchanged at c954cb350b60ce6239ef6b8d604da5be4c7d162d.
OUT_OF_SCOPE_FINDINGS: Existing API recorder rewrites noisy build.json, while existing
verifier checks its shape only; prompt forbids ANY fixture diff. No authorized product
or test edit can reconcile those instructions. Existing world verifier silently leaves
its generated world.js copies in TEMP; all this job's copies removed, 38 older directories
untouched. No two failed semantic repair attempts were made; this is an authorization
conflict, not a regression or a weakened assertion.
LEDGER_UPDATE_REQUEST: Await user clarification of dynamic build.json exception. If
allowed, record authorization first, commit green worker, normal no-ff integration and
all integration gates remain to be completed. If strict zero-diff is retained, preserve
this diagnosis and do not integrate. C items 5-7 and D-F remain unauthorized.
