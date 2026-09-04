# Handoff: `PC-ROOT-01` — mechanical split of `Definitions.cs`

```text
JOB_ID: PC-ROOT-01
STATUS: COMPLETE
BRANCH: codex/pc-root-01-definitions
COMMIT: a3c26b42993d98451c1e910d273c444ad2e29d3c (worker implementation)
FILES_CHANGED:
  deleted:    src/MechaTrader.Core/Model/Definitions.cs (888 lines)
  added:      src/MechaTrader.Core/Model/GoodsDefs.cs        (CategoryDef, QualityConfig, WarehouseConfig, GoodDef, TierDef)
              src/MechaTrader.Core/Model/TerrainDefs.cs      (TerrainDef)
              src/MechaTrader.Core/Model/VehicleDefs.cs      (VehicleCapability, VehicleKind, TruckDef, TruckUpgradeDef)
              src/MechaTrader.Core/Model/GearDefs.cs         (GearDef)
              src/MechaTrader.Core/Model/IndustryDefs.cs     (IndustryDef)
              src/MechaTrader.Core/Model/EconomyConfigs.cs   (EconomyConfig, GameConfig, CrewBriefConfig)
              src/MechaTrader.Core/Model/CrewDefs.cs         (CrewLever, CrewSkillDef, CrewRoleDef, CrewPostDef, IntelConfig, CrewTraitDef, TraitKind, CrewWageDef, CandidateGenDef, CrewConfig)
              src/MechaTrader.Core/Model/CityStatsDefs.cs    (StatBandDef, CityVitalDef, CitySupplyDef, CityStatsConfig)
              src/MechaTrader.Core/Model/StandingDefs.cs     (PermitDef, FavorActionDef, StandingSegmentDef, StandingConfig)
              src/MechaTrader.Core/Model/EventsDefs.cs       (EventDef)
              src/MechaTrader.Core/Model/ContractsDefs.cs    (ContractKindDef, ContractsConfig)
              src/MechaTrader.Core/Model/ExposDefs.cs        (ExpoThemeDef, ExposConfig)
              src/MechaTrader.Core/Model/EventsConfigs.cs    (EventsConfig)
CHECKS_RUN:
  1. Byte-level equivalence: all 13 new files' type blocks are byte-identical to the
     original Definitions.cs blocks. Full-file reconstruction (namespace + blank +
     blocks in original order) verified byte-for-byte; type order across files equals
     the original declaration order.
  2. git diff --check: PASS (in worker worktree and after integration)
  3. dotnet build MechaTrader.sln -c Release --nologo -v q: 0 warnings, 0 errors
  4. dotnet test tests/MechaTrader.Core.Tests (full, unfiltered): 239 passed, 0 failed
  5. Determinism/save fingerprint tests (filter Determinism|Fingerprint|Save): 23 passed
     plus tools/MechaTrader.Fingerprint run regenerating save fixtures byte-identically
     (no tracked fixture diff)
  6. tools/verify-worldjs.ps1: PASS, SHA-256
     26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a
  7. Full nine-gate check.ps1: PASS (all nine gates green; Algebra tick ~350 ms)
  8. Post-run: FIGURES.md timing-line only, reverted; port 5080 free; no
     MechaTrader.Host process
CHECK_RESULTS: ALL PASS
BEHAVIOR_CHANGES: NONE (mechanical file split only; namespaces/names/signatures/
  ordering/visibility/public entrypoints preserved; no semantic cleanup or renames)
RISKS:
  - BalanceSim tick timing varies with machine load (~150-630 ms observed across
    sessions); the check.ps1 gate has shown contention sensitivity documented in
    D-041. Not a regression.
OUT_OF_SCOPE_FINDINGS: none blocking; no changes needed to MechaTrader.Core.csproj
  (SDK default wildcard already compiles every .cs under src/MechaTrader.Core/)
LEDGER_UPDATE_REQUEST:
  - Mark PC-ROOT-01 VERIFIED after integration verification; record integration merge
    and verification rows; update Current checkpoint to "Phase C item 1 VERIFIED;
    items 2-7 and phases D-F remain unauthorized"
```

## Coordinator notes

- Assignment commit `e2d8838` on `master`; mirrored to `integration` as `efc9067`.
- Worker implementation `a3c26b4` split the 888-line `Definitions.cs` into 13 cohesive
  files under `src/MechaTrader.Core/Model/`, 39 top-level types, all byte-identical.
- Integration merge `637a85f` (parents `efc9067` + `a3c26b4`).
- Post-integration verification on `integration` tip: Release build 0 warnings, 239/239
  Core tests, 23/23 determinism/save tests, Fingerprint fixture regeneration byte-stable,
  world.js SHA-256 `26063b3e...0712a`, full nine-gate `check.ps1` PASS, `git diff --check`
  clean, port 5080 free, no MechaTrader.Host process, no temp clones.
- Prohibited paths untouched: `data/`, `web/chart/` generator+output, tests, MapLab
  (status still exactly ` M world.js`), no tags, no Phase C item 2+ or Phase D-F work.