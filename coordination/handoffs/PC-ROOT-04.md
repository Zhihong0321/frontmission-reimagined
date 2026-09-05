# PC-ROOT-04 assignment / handoff

JOB_ID: PC-ROOT-04
STATUS: READY
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
