# Worker handoff: `PB-ROOT-01`

```text
JOB_ID: PB-ROOT-01
STATUS: COMPLETE
BRANCH: codex/pb-root-01-maplab-import
COMMIT: 7517a82306f9a9fa44135082b150ece67068ce69
FILES_CHANGED: .gitattributes; web/chart/** (403 imported files)
CHECKS_RUN: source identity/status; exact relative-path/count/byte comparison; SHA-256 comparison for all 403 files; staged and committed raw Git-blob comparison for all 403 files; git diff --check; allowed-path and runtime-path diff review; post-copy sibling identity/status/hash review
CHECK_RESULTS: PASS — source and destination 403 files, 293,783,792 bytes, SHA-256 403/403; committed blobs 403/403 raw-byte identical; git diff --check PASS; runtime/config changes 0; sibling unchanged
BEHAVIOR_CHANGES: NONE
RISKS: The imported copy is deliberately dormant until a separately authorized Phase B path-switch job. It preserves the existing known missing art/truck.png request and includes all art metadata/contact sheets byte-for-byte; neither was cleaned up in this job. The scoped Git attribute disables text conversion for web/chart/** so mixed source line endings remain exact.
OUT_OF_SCOPE_FINDINGS: NONE
LEDGER_UPDATE_REQUEST: Mark PB-ROOT-01 REVIEW at import commit 7517a82306f9a9fa44135082b150ece67068ce69 and record the successful byte/blob/source immutability checks. Do not start another Phase B job until coordinator integration review is green.
```

## Source identity

- Read-only source: `D:\FrontMission-MapLab`
- Branch: `backup/maplab-final-20260903`
- HEAD: `df3c1baa8a83c2412607353af9994170b988dbe3`
- Status before and after: exactly ` M world.js`
- `world.js`: 8,590 bytes; SHA-256
  `6680509cd8cbacc72ab3b8060efd4b8c7d3c328f8646aaeb78ddb1531c3d135c`
- The sole `world.js` delta is the previously authorized generated-comment path separator
  change recorded in ledger decision `D-030`; no sibling operation was performed.

## Import result

- Import commit: `7517a82306f9a9fa44135082b150ece67068ce69`
- Parent/assignment commit: `b280ff1fb8023a49979d65560aa7b92ffc0cb346`
- Destination: `web/chart/`
- Imported files: 403
- Imported bytes: 293,783,792
- Root files: `_ops-test.html`, `chart-tiles-worker.js`, `chart.html`,
  `game-bridge.js`, `ops.css`, `ops.js`, `opstest.html`, `world.js`
- Tree: complete `art/` subtree, 395 files
- Excluded as required: `.gitignore`, `Generator.cmd`, `Map.cmd`, `README.md`,
  `make-world.js`, `map-design-sop.md`, and `generator/**`

## Verification evidence

1. Pre-copy source branch, commit, status, `world.js` diff, length, and hash matched the
   task packet exactly; `web/chart/` did not exist.
2. Source/destination relative-path sets, lengths, and SHA-256 hashes matched for all
   403 files, with no missing or extra destination file.
3. After staging, each index blob id matched `git hash-object --no-filters` for the live
   source file: 403/403.
4. After commit, each `HEAD:web/chart/**` tree blob id again matched the raw live source
   blob and each working-tree SHA-256 matched: 403/403.
5. `.gitattributes` contains one scoped rule for `/web/chart/**`: raw bytes are not
   normalized, while `cr-at-eol` allows `git diff --check` to correctly accept the
   source's preserved CRLF lines.
6. `git diff --check b280ff1..7517a82` passed.
7. Commit scope is only `.gitattributes` and `web/chart/**`; diff review reported zero
   changes under `src`, `play.ps1`, `check.ps1`, `tests`, `tools`, or `data`.
8. The sibling branch, HEAD, sole status delta, and `world.js` SHA-256 were unchanged
   after copy, staging, commit, and post-commit verification.

No browser or full application acceptance was claimed: the host intentionally continues
to serve the sibling directory until the later bounded Phase B path-switch transaction.
