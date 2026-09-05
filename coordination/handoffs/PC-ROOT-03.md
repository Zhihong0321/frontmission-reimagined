# PC-ROOT-03 handoff — mechanical split of WorldLoader.cs

```text
JOB_ID: PC-ROOT-03
STATUS: COMPLETE
BRANCH: codex/pc-root-03-worldloader
COMMIT: (worker implementation commit on this branch — see ledger integration queue)
FILES_CHANGED:
  - src/MechaTrader.Core/World/WorldLoader.cs (modified: 1021 -> 153 lines; keeps usings,
    namespace, file-level doc comment, and the public API: 15 key constants, RequiredKeys,
    JsonOptions, Load; class declaration gains the `partial` keyword)
  - src/MechaTrader.Core/World/WorldLoaderCities.cs (new: BuildCity, ResolveGovernorName,
    StableHash, FoundingVitals)
  - src/MechaTrader.Core/World/WorldLoaderRoutes.cs (new: BuildRoute, StraightLineKm)
  - src/MechaTrader.Core/World/WorldLoaderValidation.cs (new: ResolveCategories, ValidateGoods,
    ResolveTiers, ValidateTiers, ValidateQualityVital, ValidateQuality, ValidateIndustryGoods,
    ValidateCrew, ValidateCityStats, ValidateStanding, ValidateEvents, ValidateBands,
    ValidateGear, ValidateTrucks, ValidateUpgrades, ValidateContracts, ValidateExpos,
    ValidateMap, ValidateWorld, ToLookup, Parse)
  - src/MechaTrader.Core/World/WorldLoaderDtos.cs (new: nested GoodsFile, TerrainFile,
    TrucksFile, IndustriesFile, CitiesFile, RoutesFile, CityDto, RouteDto)
CHECKS_RUN:
  - Byte-level split equivalence (token-level, see CHECK_RESULTS)
  - git diff --check
  - dotnet build MechaTrader.sln -c Release
  - dotnet test (full, unfiltered MechaTrader.Core.Tests)
  - dotnet test --filter DeterminismFingerprint|SaveFixture
  - dotnet run --project tools/MechaTrader.Fingerprint (fixture regeneration)
  - tools/verify-worldjs.ps1
  - tools/verify-api-shape.ps1
  - npm ci --prefix tests/browser; npx playwright install chromium; npm test --prefix tests/browser
  - powershell -NoProfile -ExecutionPolicy Bypass -File ./check.ps1 (nine gates)
CHECK_RESULTS:
  - Split equivalence: PASS — 875/875 normalized class-body lines byte-identical between the
    original single-file body and the concatenated partial-fragment bodies; every code token,
    comment, doc comment, string literal, and member order preserved; every doc comment travels
    with its member. The only textual delta is the required `partial` keyword on the class
    declaration and per-fragment `using` directives copied from the original header.
  - git diff --check: clean
  - Release build: 0 warnings, 0 errors
  - Full Core tests: 239/239 passed
  - Determinism/save filter: 10/10 passed
  - Fingerprint regeneration: save fixtures (day1-new-run, trade-cycle, late-run-mixed,
    manifest) regenerated with zero tracked diff
  - verify-worldjs: PASS, SHA-256 26063b3e3680a190b79843604107977331922c77397dfe2a1bf23a5a3160712a
  - verify-api-shape: PASS, fixtures matched (seed 555555), zero diff
  - Browser smoke: 1/1 passed (10.2 s)
  - Nine-gate check.ps1: all PASS (BalanceSim 180.6 ms; no contention re-run needed)
  - Port 5080: no LISTENING socket after runs (TIME_WAIT remnants only); no MechaTrader.Host
    process; FIGURES.md timing line reverted, not committed
BEHAVIOR_CHANGES: NONE
RISKS:
  - The class declaration changed from `public static class WorldLoader` to
    `public static partial class WorldLoader`. This is the C#-sanctioned mechanical mechanism
    for splitting one class across files; it changes no name, namespace, member, signature,
    visibility, or behavior. All private helpers and nested DTOs remain private members of the
    single WorldLoader type.
  - Split fragments restate the original file-header using directives
    (System.Text.Json where Parse/JsonException live, MechaTrader.Core.Model everywhere) so
    each file compiles standalone. The original had these usings file-wide.
OUT_OF_SCOPE_FINDINGS: NONE
LEDGER_UPDATE_REQUEST: Mark PC-ROOT-03 VERIFIED after coordinator integration and
re-verification on integration; record integration merge hash; keep Phase C items 4-7 and
phases D-F unauthorized.
```
