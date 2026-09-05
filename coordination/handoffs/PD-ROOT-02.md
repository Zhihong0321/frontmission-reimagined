# Worker handoff: `PD-ROOT-02`

- Status: `COMPLETE`
- Worker: `ROOT` (coordinator, executed locally per `D-061`)
- Runtime/model: ZCode session coordinator (GLM-5.3-Flash), acting as ledger `ROOT`
- Branch: `codex/pd-root-02-chart-helpers`
- Base commit: `67b6fb5b3e07b61b9ad2b089eaea528e42be2565` (verified integration tip,
  ledger mirror included; ledger blob parity `32ab5cf6…` on both branches)
- Sub-step commits (one per helper category, bisectable):
  `29ab8d7` terrain 1/6, `3f0ffc0` render 2/6, `0e8f9fc` worker 3/6,
  `24f2ccd` routing 4/6, `661b305` input 5/6, `df715fa` hud 6/6

## Transformation (byte-level mechanical move, CP-D2 = Phase D steps 3-8)

Six pure-logic helper categories moved out of `web/chart/chart.js` (999 lines,
81,908 B; payload after the line-1 provenance comment 81,801 B, SHA-256
`65707f6b35ee53f2fec201fe488926be7779036a2fe7f08ed458fc779f974b05`) into six new
classic helper files. Each helper file = one-line provenance comment + `'use strict';`
(first statement; the comment does not break the directive prologue — `D-059`
precedent) + the moved block bytes verbatim. The residual `chart.js` keeps its
original line-1 comment and line-2 `'use strict';` byte-unchanged; its sole delta is
the deletion of the moved ranges. `chart.html`'s sole delta is the insertion of the
six `<script src="...?v=1"></script>` tags immediately before the `chart.js?v=1` tag.
No `type=module`; names, declaration order, and classic-script semantics unchanged.

Helper load order = document order of the moved blocks:
`chart-terrain.js`, `chart-render.js`, `chart-worker.js`, `chart-routing.js`,
`chart-input.js`, `chart-hud.js`, then `chart.js`.

Final moved-line ranges (1-based, inclusive, original file):

| File | Ranges | Contents |
|---|---|---|
| `chart-terrain.js` | 43-44, 47-54, 62, 109-125, 128-129, 173-203, 350-377 | mulberry, strHash, h2, vnoise, fbm2, pip, lattice, biomeAt, offroadMult, offroadCost, pickSprite, WIND, heading, POIS, buildPois, GLYPH_DENSITY, glyph state, buildGlyphs |
| `chart-render.js` | 204-210, 221-280, 378-412, 588-593, 600, 603-604, 608-805, 810-860 | drawPois, paintRows, boxBlur, finishBase, INK_D/L, drawGlyph, bakeInk, smoke, emitSmoke, resize, toScreen, toWorld, tracePath … drawChrome, frame |
| `chart-worker.js` | 285-345 | tile constants/state, startTileWorker, sendTextureToWorker, wantTile, drawDetailTiles |
| `chart-routing.js` | 434-468 | edgePoint, pointAlong, nearestRoad, route |
| `chart-input.js` | 473, 483-574 | SEC_PER_DAY, curSeeds, planTo, depart, stepEdge, spendKm, arriveAt, advanceAuto, driveFree |
| `chart-hud.js` | 865-895, 897 | $, flip, lastHud, setText, updateHud, toast, syncButtons, pickCity, showCard, hideCard, focusChart, fitAll |

Reconstruction proof interpretation per `D-061`: the categories interleave with
load-time residual code, so the binding proof is the `D-059` backfill precedent —
re-inserting every moved block at its original offset between the residual pieces
rebuilds the original payload byte-identically. Verified after EVERY sub-step
(incrementally) and after the final step: rebuilt payload SHA-256 equals
`65707f6b…4b05` exactly. The helper block payloads concatenated in final load order
plus the residual payload form an exact partition of the original payload:
6563 + 31495 + 3317 + 2459 + 7187 + 3392 + 27388 = 81,801 B.

## Per-substep evidence (moved blocks + residual, SHA-256)

| Sub-step | Helper file (B / SHA-256) | Block payload (B / SHA-256) | Residual chart.js after (B / payload SHA-256) |
|---|---|---|---|
| 1/6 terrain `29ab8d7` | 6,663 / `5dc2dc32…dc8e2` | 6,563 / `04d2b4ff…34f8a8` (ranges: 43-44 `2a4f6064…9985`, 47-54 `14f92518…dcb5e`, 62 `e6ab60db…cb8156`, 109-125 `6083a262…04d4f`, 128-129 `97832e67…f64cd9a2`, 173-203 `692fa406…97c393`, 350-377 `b20f0e41…aa93bf4`) | 75,345 / `c2c8fb4d…babfa` |
| 2/6 render `3f0ffc0` | 31,595 / `11747f79…1473f` | 31,495 / `3cb429be…cc07972` (204-210 `36a4739b…77732a4`, 221-280 `df7a828c…354d42`, 378-412 `51fa8258…69b1d4`, 588-593 `c9ffdaef…9558b0`, 600 `675f6e54…b326c31e`, 603-604 `ca3edaef…e99ca9`, 608-805 `b60a34e0…643d272`, 810-860 `7831449a…06dcfd`) | 43,850 / `8814bd3e…5a321` |
| 3/6 worker `0e8f9fc` | 3,417 / `3dcf0ffe…b9231` | 3,317 / `7da37de0…1082b2` (285-345, single range) | 40,533 / `4d22d919…8d4240` |
| 4/6 routing `24f2ccd` | 2,559 / `1aa9fd39…7d37` | 2,459 / `0081f993…76bd19` (434-468, single range) | 38,074 / `10d99f8a…bbe9cb8` |
| 5/6 input `661b305` | 7,287 / `bf2120c6…67644` | 7,187 / `79b9cb23…869f03` (473 `e9c4b52c…95785`, 483-574 `555d904c…e1cc2`) | 30,887 / `fb3c963a…515c68` |
| 6/6 hud `df715fa` | 3,492 / `30c6b6ed…1d67b6` | 3,392 / `895828e7…4ba81c` (865-895 `ba5b216a…cd5876`, 897 `e2cfea12…827d660`) | 27,495 / `05e95154…b75e5` |

Final state: residual `chart.js` 27,495 B (SHA-256
`cf4859f3a3c4e5d235dea0b1b884623575296793a37f8ffff07f5dc66d94e172`, payload 27,388 B
`05e9515463a5a007d214c4a63a399298f53ca9d60133130232ac816c420b75e5`);
`chart.html` 5,102 B; six helper files as tabulated.

## Files changed

- `web/chart/chart.js` (modified — only deletions of the moved ranges; lines 1-2 byte-unchanged)
- `web/chart/chart.html` (modified — only the six inserted script tags before `chart.js?v=1`)
- `web/chart/chart-terrain.js`, `chart-render.js`, `chart-worker.js`,
  `chart-routing.js`, `chart-input.js`, `chart-hud.js` (new)
- `coordination/handoffs/PD-ROOT-02.md` (this file)

`.gitattributes` `/web/chart/**` covers all six new paths (`git check-attr` confirmed
`text: unset` for each); all new/changed files contain 0 CR bytes.

## Checks run (worker phase, Fast only per `D-061`)

| Command | Result | Evidence |
|---|---|---|
| Byte surgery + per-substep reconstruction proof | `PASS` | After each of the six sub-steps: re-inserting all moved-so-far blocks at their original offsets rebuilds the original payload byte-identically (SHA-256 `65707f6b…4b05`); partition sum 81,801 B exact |
| Per-range SHA-256 recording | `PASS` | Table above (extracted from the pristine `67b6fb5` bytes) |
| `'use strict';` position check (each new file) | `PASS` | Byte 0 of each helper after the one-line provenance comment is `'use strict';\n` |
| CR-byte scan | `PASS` | 0 CR bytes in all 7 scripts and `chart.html` |
| `node --check` (all 7 `web/chart/*.js` incl. untouched `chart-tiles-worker.js`) | `PASS` | no syntax errors |
| Top-level declaration uniqueness across the 7 scripts | `PASS` | 140 top-level names, 0 duplicates |
| Script-tag order in `chart.html` | `PASS` | six helper tags in load order, all before `chart.js?v=1`; existing version parameters untouched |
| `git diff --check origin/integration..HEAD` | `PASS` | clean |
| `tools/verify-fast.ps1` | `PASS` | Release build exit 0, warnings 0; iteration-aid-only disclaimer printed |

## Behavior changes

`NONE` — bytes moved verbatim; sole textual additions are the per-file provenance
comment + `'use strict';` directive (restoring the strictness the moved code already
had inside the original single script) and the six classic `<script>` references.
All moved units are top-level pure declarations (no load-time side effects, no
load-time references to later bindings — per the `D-061` inventory); every
cross-reference between helper files and the residual is call-time only, and all
calls happen after every script has loaded.

## Risks and uncertainty

- Serving and load behavior of the six new static files are proven only by the
  integration browser smoke (Full gate 4), which is the required next step.
- Fast results above are iteration aids only and certify nothing.

## Out-of-scope findings

None.

## Requested ledger update

`PD-ROOT-02` integrated via ordinary `git merge --no-ff` into `integration` (expected
tree 715 + 6 helper files + 1 handoff = 722), one same-run Full battery (revision §3
six gates) at the merge commit, acceptance recorded as `D-062`; NO tag created
(`known-green/frontend-split` only after CP-D3); ledger mirrored to integration
afterwards.
