# 流程精简修订案(2026-09-05)

文件:`coordination/plan-revision-2026-09-05.md`
状态:**草案,等待用户批准**。批准前不提交、不推送;批准后仅提交协调文件,仍不执行任何迁移步骤。
起草:ROOT 协调器。前提:用户结论——迁移已运行超过 48 小时,Phase C item 1-5 是同一动作重复五次、每 item 双态全量验证电池,流程过重、重复感强烈,要求省略/合并剩余未完成流程。该结论是本修订的前提,本文不重新辩论,只把它变成可执行、可审计、不牺牲安全底线的裁决。

---

## 0. 与启动来文假设的差异(以现场为准,只读核对于 2026-09-05)

来文按 PC-ROOT-05 之后的现场书写,但上一会话已完成 PC-ROOT-06。三处差异均为合法前进(已记录于账本 D-053/D-054),非异常:

| 来文预期 | 实际现场 | 原因 |
|---|---|---|
| master=793138e / integration=900dd25 / 账本 blob ce199ba6 | master=91f87a4 / integration=3830abd / blob **e8bd6992**(四方一致) | PC-ROOT-06 授权+验收两条协调提交与账本镜像 |
| item 6(BalanceSim)未做 | **item 6 已 VERIFIED**:产品合并 93a2196(双父 900dd25+aa958e9),工人 1a04180,REVIEW aa958e9,树 690 文件 | D-053/D-054 |
| 下一条决定编号 = D-053 | **下一条 = D-055**(D-053=授权,D-054=验收已占用) | 以账本实际编号为准 |

其余核对全部符合预期:known-green/consolidated 标签对象 e31ceb71 → 590b25c 未动;MapLab df3c1ba 恰为 ` M world.js`;PC-ROOT-01..06 与 PB-INTEGRATION-01 worktrees 全部 tracked 干净;主仓 Graft 环境文件(M .gitignore/CLAUDE.md,未跟踪 .ignore/AGENTS.md)保持不动。integration 树 690 文件(原 681 + 8 片段 + 1 handoff)。

**因此:原第 1 项裁决对象"取消 item 6"已无对象**——成本已支付、双态电池已跑完、价值已入账。本修订对 item 6 无事可做,也不再涉及。

---

## 1. 现状清点

**已完成(VERIFIED,含哈希):**

| 阶段/项 | 产品绿点 | 备注 |
|---|---|---|
| Phase A 全部(九闸 check.ps1、确定性/存档指纹、API/world.js 夹具、浏览器 smoke、clean-clone) | `5ed5949`(known-green/original) | 2026-09-04 |
| Phase B 全部(403 文件字节导入、仓库内 make-world.js、原子路径切换) | `590b25c`(known-green/consolidated) | 标签 e31ceb71 未动 |
| Phase C item 1 Definitions.cs | `b7e2c8d` | D-044 |
| Phase C item 2 ViewModels.cs | `fa8592a` | D-046 |
| Phase C item 3 WorldLoader.cs | `ff32d4f` | D-048 |
| Phase C item 4 ViewBuilder.cs | `290615f` | D-050 |
| Phase C item 5 CommandProcessor.cs | `6441f88` | D-052 |
| Phase C item 6 BalanceSim Program.cs | `93a2196` | D-054,2026-09-05 |

**未完成:** Phase C item 7(超大测试类拆分);Phase D(前端 12 步);Phase E(AI 上下文 10 项);Phase F(清理与 MapLab 退役 10 步)。

**待拆对象的实测规模(2026-09-05,integration 树):**
- 测试类最大为 `tests/MechaTrader.Core.Tests/CrewTests.cs` 695 行,其余 ≤403 行——item 7 的实际目标仅此一个显著超标文件。
- 前端:`web/chart/ops.js` 99.5 KB(1233 行)、`web/chart/chart.html` 93.8 KB(1194 行,内联 style+script 为 Phase D 的主要提取对象),`web/chart/` 合计约 255 KB。

**分支状态:** master=origin/master=91f87a4(仅协调记录);integration=origin/integration=3830abd(产品树 690 文件);两个恢复标签未动;MapLab 未动。

---

## 2. 逐项裁决

| # | 对象 | 裁决 | 理由(含量化) | 放弃的东西(风险如实列) |
|---|---|---|---|---|
| 1 | Phase C item 6 | **无裁决(已完成 VERIFIED)** | 见 §0。取消需回退已验证、已推送的绿状态,无人受益 | — |
| 2 | Phase C item 7 | **取消** | 实测仅 CrewTests.cs(695 行)显著超标;测试文件不进产品,拆分纯美观。check.ps1 第 2 闸钉死 239 个测试,拆丢测试立即变红,存在绊线。省 2 次全量电池 | 测试代码可维护性。若未来 CrewTests 增长(>~1500 行)或被频繁冲突,可重新作为单任务单独授权,代价一条账本记录 |
| 3 | known-green/backend-split 标签 | **二选一取"建汇总标签"**:在 Phase C 实际完工点=integration tip `3830abd` 建 annotated tag 并推送 | item 7 取消后 Phase C 即刻完工;D-054 记录的九闸+浏览器全绿即该状态的当次运行证据(2026-09-05,哈希未变)。Phase 边界标签是本迁移既有恢复点惯例(original/consolidated 均有),后端拆分这一阶段边界理应有同等待遇 | 不建标签可省一次操作;但"后端拆分完成"将只能靠账本文本指认,失去具名恢复点。**条件**:若实际执行时距 D-054 的验证运行已隔日,先重跑一次 Full 再建(证据不过期原则)。属后续单独会话的执行动作,本会话不建 tag |
| 4 | Phase D(前端 12 步) | **压缩:12 步 → 3 个检查点**(默认执行;整体推迟仅在用户明确选择放弃"前端聚合边界"完成标准时成立) | CP-D1=内联 CSS+chart.js 提取(原 1-2 步);CP-D2=terrain/render/camera/input/routing/HUD/worker 六类纯提取(原 3-8 步);CP-D3=ops 命名空间+ops helpers+ops 页面+stateful boot(原 9-12 步)。每站集成态一次 Full,工人期只跑 Fast。全量电池 24 次 → 3 次 | 粒度变粗:若 CP-D2 变红,需站内二分定位(站内子步骤分开提交,可二分但慢);classic script 提取的执行顺序/严格模式问题可能逃过 smoke 断言面(smoke 只断核心流)——此风险与原计划相同,但发现粒度变粗、修复窗口变晚 |
| 5 | Phase E(AI 上下文 10 项) | **提前至 D 之前,依赖由"C、D"改为"仅 C"** | E 的 10 项中 8 项是对现状的文档化/工具化,不依赖前端拆分;codemap 以"从仓库事实生成"为本性,D/F 之后重新生成即可(一条命令);Fast/Full 入口(E8-9)正是本修订验证体系要落成脚本的部分,先做它,后面每个检查点直接受益。省:不因等待 D 而空转 | 若 D 之后执行,D 会造成 codemap 短暂过期——重新生成为工具成本,可忽略 |
| 6 | Phase F(清理 10 步) | **保留,10 步 → 2 个检查点** | CP-F1=仓库内清理(ArtLab 移除、archived UI 移除、隔离无用截图/资产、删除已证未用文件;维持 2-3 个独立删除提交,共用一次 Full);CP-F2=MapLab 退役(fresh-clone 验证、无兄弟路径证明、恢复标签可解析、删除 D:\FrontMission-MapLab、最终全量验收、打 known-green/final)。删除前精确目标清单照旧先记入账本 | 删除提交粒度变粗,部分回滚略难(缓解:每类删除独立提交+清单先记账);MapLab 删除仍独占一站并有当次 Full 门槛,底线不降 |
| 7 | 流程仪式(最大开销来源) | **裁剪,四条全采纳** | a) 双态全量电池 → **每检查点集成态一次 Full**,工人迭代期只许 Fast;省一半电池。b) 逐字节重建证明 → 仅"移动类/文件"任务保留,其余以 Full 电池+指纹零 diff 为准。c) 每任务任务包+结构化 handoff+授权/验收两条 D 决定 → 检查点授权由本修订路线图一次性给出(范围/禁改/门槛已写死,见 §3),执行后每检查点一份简短 handoff+一条验证行;偏离路线图范围即视为未授权。d) 卫生项(端口/进程/FIGURES/临时目录)保留,证据简化为"基线对比+本次新增已清理"一句话 | a) 工人态挡不住的问题后移到集成态才暴露,集成返工变慢——缓解:stop-loss 两次修复规则不变,红了即停不推送。b) 非移动类改动失去最强静态证据——缓解:九闸行为面+指纹覆盖行为敏感面,剩余工作(前端/文档/删除)本就没有 C# 字节等价可证。c) 单条记录的信息密度下降——缓解:简短 handoff 仍强制记录检查结果与例外 |

---

## 3. 剩余工作路线图(修订后)

**每个检查点的 Full 门槛(同一清单,缺一不可):**

1. `dotnet build MechaTrader.sln -c Release` — 0 警告 0 错误;
2. `check.ps1` 九闸全绿(含 239 测试、BalanceSim、host/API/world.js 闸);
3. `dotnet run --project tools/MechaTrader.Fingerprint` 再生后零 tracked diff,F_state/F_view 与钉定值逐字一致;
4. 浏览器 smoke(`npm ci` + Chromium)`npm test` 1/1;
5. `git diff --check` 干净;
6. 卫生:5080 无监听、无 Host 残留、FIGURES 仅计时行已还原、临时目录"基线对比+本次新增已清理"。

| 检查点 | 内容(写范围在执行会话确认,不得偏离) | Full 次数 |
|---|---|---|
| CP-0 | Phase C 收尾:账本记 item 7 = CANCELLED、Phase C 完工;按 §2.3 建 known-green/backend-split(证据过期则先跑一次 Full) | 0-1 |
| CP-E1 | Phase E 全部 10 项:agent 指令、feature ownership map、codemap 生成、feature notes、glossary、ADR、停止自动加载历史、**Fast/Full 脚本入口**、Full 定义落地、known-green/ai-workflow 标签(此标签仍按原计划在该绿点建) | 1 |
| CP-D1 | 内联 CSS + chart.js 提取(原 D1-2),字节级机械移动,执行顺序不变 | 1 |
| CP-D2 | terrain/render/camera/input/routing/HUD/worker 纯逻辑提取(原 D3-8),子步骤独立提交 | 1 |
| CP-D3 | ops 命名空间 + ops helpers + ops 页面 + stateful boot(原 D9-12) | 1 |
| CP-F1 | 仓库内清理(原 F1-4):2-3 个独立删除提交,清单先记账本 | 1 |
| CP-F2 | MapLab 退役(原 F5-10):fresh-clone、无兄弟证明、标签核对、删除目录、最终全量验收、known-green/final | 1 |

**运行次数对比(自当前点 91f87a4/3830abd 起):**

| 指标 | 原计划(version 4) | 修订后 | 节省 |
|---|---|---|---|
| 全量验证电池(Full) | item7 2 + D 12×2=24 + E ≈2 + F ≈2 ≈ **30 次** | CP-0 0-1 + E 1 + D 3 + F 2 = **6-7 次** | ≈23 次(约 77%) |
| 任务包/结构化 handoff | ≈16 份(每任务一份) | 6 份简短 handoff | ≈10 份 |
| 账本 D 决定 | ≈30 条(每任务授权+验收) | 1 条(D-055 本身)+ 异常时临时记录 | ≈29 条 |

**保留不变的底线(原样,不得弱化):**

- MechaTrader.Core 纯模拟库、先校验后改状态等既有不变量;
- 每个集成检查点必须跑:完整九闸 check.ps1、Fingerprint 零 diff、浏览器 smoke、git diff --check;
- 涉及行为敏感面(命令处理、存档、world 生成)的改动仍需指纹与夹具全绿;
- 不改写历史、不 force push、不移动既有恢复标签;
- MapLab 目录在 Phase F 明确执行前不动;
- 任何 VERIFIED 宣称必须有当次运行证据,禁止引用过期结果。

**Fast / Full 定义(写入修订;脚本入口由 CP-E1 落地):**

- **Fast** = `dotnet build MechaTrader.sln -c Release`(0 警告)+ 受影响测试。仅迭代辅助,**禁止**据此宣称任何绿/完成状态。
- **Full** = 上述六条门槛全绿。任何 MERGED/VERIFIED 宣称只能以当次 Full 为准。

---

## 4. MIGRATION_PLAN.md 需改动章节清单(本会话只列不改;批准后升版 version 5)

1. Status 块:version `4` → `5`;执行状态行加注"剩余流程按 D-055 流程精简修订执行,见 coordination/plan-revision-2026-09-05.md"。
2. "Transaction used for every implementation job"(12 步事务)→ 改写为检查点事务:预授权范围 → 工人 Fast 迭代 → 集成态一次 Full → 简短 handoff + 单条验证行。
3. Phase C 段:标注 item 6 完成(93a2196)、item 7 取消(CANCELLED,理由引 §2.2)、known-green/backend-split 按 §2.3 安排。
4. Phase D 段:12 步 → 3 检查点(CP-D1/2/3 映射原 12 步);注明默认执行、整体推迟仅在用户明确放弃对应完成标准时成立。
5. Phase E 段:依赖 "C, D" → "C";顺序提前至 D 前;补注 D/F 后重新生成 codemap。
6. Phase F 段:10 步 → 2 检查点;保留删除清单先记账本与 MapLab 删除独站的要求。
7. 新增小节 "Verification modes":Fast/Full 定义与"VERIFIED 只认当次 Full"。
8. Completion criteria:仅当用户选择整体推迟 Phase D 时才修改第 5 条(前端聚合边界),否则不动。

---

## 5. 账本记录草案(实际编号 D-055;提交时按账本英文风格书写)

> | `D-055` | 2026-09-05 | Adopt the 2026-09-05 process-streamlining revision (coordination/plan-revision-2026-09-05.md) for all remaining migration work: Phase C item 7 CANCELLED (only CrewTests.cs 695 lines materially oversized; the 239-test pin in check.ps1 stays as the tripwire); known-green/backend-split to be created at the Phase C completion point (integration 3830abd) in a separate authorized session, with a fresh Full battery first if D-054 evidence is stale by then; Phase E re-sequenced before Phase D with dependency reduced to Phase C (codemap regenerated after later phases); Phase D compressed from twelve steps to three checkpoints (CP-D1/2/3); Phase F merged from ten steps into two checkpoints (CP-F1/CP-F2); two-tier Fast/Full verification entrypoints defined, with Full as the only basis for any VERIFIED claim; per-checkpoint single integration-state Full battery replaces the dual-state battery, byte-exact reconstruction proofs limited to move-class jobs, and per-task packets/decisions replaced by one short handoff plus one verification row per checkpoint | User concluded the remaining process was too heavy (48+ hours; items 1-5 repeated the same motion five times under dual-state batteries) and requested a written revision; ROOT drafted plan-revision-2026-09-05.md and the user approved it verbatim. Non-negotiable baselines unchanged: every checkpoint still runs the full nine-gate check.ps1, Fingerprint regeneration with zero tracked diff, browser smoke, and git diff --check; behavior-sensitive surfaces still require pinned fingerprints and fixtures; no history rewriting, no force pushes, no moving recovery tags; MapLab untouched until Phase F execution; VERIFIED claims require same-run evidence. Known consequence: ledger blob parity between master and integration intentionally diverges with this coordination-only commit until the next integration checkpoint mirrors the ledger. This decision authorizes no immediate migration execution: CP-0 through CP-F2 each still require their own session and scope confirmation per the roadmap. No tag created by this decision. | `ACCEPTED` |

Checkpoint 文本更新为:"流程精简修订已批准(D-055);剩余路线图、检查点门槛与执行边界以 coordination/plan-revision-2026-09-05.md 为准;Phase C item 7 CANCELLED,Phase C 完工待 CP-0 记录;Phase E→D→F 按修订顺序;均未启动。"

---

## 6. 批准边界(硬约束重申)

- 本会话获批准后**只做三件事**:按 §4 更新 MIGRATION_PLAN.md 至 version 5;账本新增 D-055 并更新 checkpoint 文本;显式路径 `git add` 这两个文件加本修订案文件,提交并推送 master,报告精确哈希。
- 已知后果(写入 D-055):本次账本仅提交于 master,integration 侧镜像顺延到下一个集成检查点,四方 blob 一致在该点恢复。
- 不启动 item 7(已取消)、不启动 CP-0/CP-E1..CP-F2 任何一步、不创建任何 tag(含 backend-split,属后续会话)、不做任何产品变更。
- 任何需要新授权的执行工作只存在于本路线图,等待后续单独会话与单独授权。
