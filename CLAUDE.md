# Mecha Trader — project brief

Onboarding for a fresh session. It describes what exists, how it is put together, and
the rules that must hold. It deliberately contains **no roadmap**: the user decides what
happens next and will tell you.

## Start here — the first ninety seconds

1. **The one rule:** `MechaTrader.Core` is a pure simulation library. Every front-end is
   a view over it and owns no rule. A test enforces this.
2. **Verify with one command:** `.\check.ps1` — seven gates, one verdict line, exit code
   is the answer. Run it before you claim anything works.
3. **Read before you touch:** the *Core API* and *Change-impact map* sections below tell
   you which files a change lands in. Read those two instead of reading the codebase.
4. **Numbers live in `FIGURES.md`**, which is generated. Never quote a figure from
   memory or from this file — quote that one.
5. **Where the rest is:** `SPEC.md` for exact formulas and schema (read it before
   touching the economy), `ACCEPTANCE.md` for what each gate asserts, `BRAIN.md` when
   the job is to teach the AI a new feature or to turn it into factions / auto-caravans,
   `NIGHT_LOG.md` only when you want to know *why* something is the way it is.

**The live map is Keeper's Chart:** `D:\FrontMission-MapLab\chart.html`, served at `/chart/`.
It is the game view (WASD + click pathfind via Core). It is not a demo.

**Every other screen is the ops shell:** `D:\FrontMission-MapLab\ops.js` + `ops.css`, an
ERP-style workspace (nav rail, tabs, data grid, detail pane) docked over the chart. `Tab`
opens it. City, caravan, crew, character sheets and the ledger live there as
`registerPage` / `registerTab` entries; a new screen is one more entry, never a new page.

The old isometric ops console is dead: `web/archive/iso-ops-console/`. Do not open it to
fix the map. Do not merge it. Do not put `web/iso` back.

The fastest way to find every place a change lands:

```bash
grep -rn "TheThingYouAreChanging" src tools tests --include=*.cs | grep -v "/obj/\|/bin/"
```

## What this is

An overland trading game in the shape of *大航海時代IV*: read a market, plan a route,
haul cargo, live off the margin. Trucks instead of ships, Europe instead of the ocean,
mech-industry commodities instead of spices. **No combat** — deliberately cut.

Target is a Steam release. Stack is .NET 8 with a browser front-end today; the intended
end state is a Godot 4 (C#) client over the same simulation library.

Current state: playable end to end on **Keeper's Chart** (`D:\FrontMission-MapLab\chart.html`,
`/chart/`). Crew and recruitment in, cities carry founding stats and a live supply reading,
standing with each city's governor (donate / invest / aid, permits, reserved shelf), world
events that move prices, city stats and stock, a headless HouseTrader play-tester on the
same Game API, a terrain map with generated mining claims, gear and a mining machine, an
a 41-good catalog in 11 categories and 5 numbered tiers (colour, value floor, standing
lock), per-hand category knowledge and special traits, shop grade that follows each
city's craft with cherry-picking and S-tier (the shop charges for the grade you pick),
category shortage / glut events that pay citizen standing to whoever relieves them,
relationship as four segments of 100, crew posts (a hand is put on trading or
information; the information post reports prices from up to 8 nearby cities with an
error that shrinks with intelligence), a truck station (instances, fittings, resale), a
contract board per city, a trade expo per city with a priced stall and a replayed hall,
rented storerooms with auto-sell / auto-procure, all acceptance checks green. The ops shell
over the chart carries the market (buy / sell pane), governor, city stats, roads, recruitment,
depot, storeroom, wire, caravan, crew roster, character sheets and the ledger. The old
isometric ops console is in `web/archive/`.

## Run and verify

| Goal | Command |
|---|---|
| Play | double-click the desktop launcher, or `Play.cmd` (builds, serves, opens `/chart/` — Keeper's Chart) |
| Put the launcher on the desktop | double-click `Install-Launcher.cmd` (run again to repair) |
| See which build is running | the badge in the game's header, or `GET /api/build` |
| Verify everything | `.\check.ps1` — seven gates, one verdict line, exit code 0 or 1 |
| Economy report + regenerate `FIGURES.md` | `dotnet run --project tools/MechaTrader.BalanceSim` |
| Tests | `dotnet test` |

`.NET SDK 8.0.424` is installed system-wide at `C:\Program Files\dotnet`. Plain `dotnet`
works from anywhere; no PATH manipulation is needed.

## The one architectural rule

**The simulation is a standalone library. Every front-end is a view over it.**

The browser talks to `MechaTrader.Core` over JSON; a Godot scene will call it in-process.
Neither is allowed to own a rule. This is what makes the eventual engine work a project
reference rather than a rewrite, and it is enforced by a test, not by discipline.

## Project map

```
MechaTrader.sln
Play.cmd / play.ps1        launcher: build, serve, open browser
check.ps1                  acceptance gate; seven checks, exit code is the answer
BRAIN.md                   how to update the AI (HouseTrader → factions / auto-caravans)
VERSION                    the one place the version string is set
Install-Launcher.cmd / install-launcher.ps1   puts the desktop shortcut in place
FIGURES.md                 GENERATED by the balance harness. Never hand-edit.
README.md                  orientation for a human
SPEC.md                    exact formulas, command list, data schema, tuning constants
ACCEPTANCE.md              what each gate asserts and why
NIGHT_LOG.md               build history and the reasoning behind past decisions

data/                      ALL game content. No content is hardcoded in C#.
  config.json              start cash/city/trucks + every economy tuning constant + warehouse rent
  goods.json               commodities: category, tier (1-5), base price, cargo volume, elasticity; tiers; quality knobs
  cities.json              real lon/lat, population, industry list
  industries.json          archetypes that generate every city's market
  routes.json              road links; distance derives from coordinates
  terrain.json             speed and fuel multipliers per road type
  trucks.json              capacity, speed, upkeep, fuel burn, price; optional kind/capabilities/mineYield; fittings; resale
  contracts.json           contract kinds, board size, deadline range, tier weights
  expos.json               expo cycle, fees, buyer behaviour, themes, remarks
  crew.json                skills, levers, specialist roles, category knowledge, traits, wages, name pools
  citystats.json           the city stat catalogue: vitals, supply bands, band labels
  standing.json            segments, ranks on the total, permits, favor actions (with segment), reserved share
  events.json              world event templates: price, city stats, stock shocks
  map.json                 terrain grid: origin, cell size, biome regions, mining placement
  gear.json                portable tools: price, hold volume, capabilities, mine yield

src/MechaTrader.Core/      PURE simulation. No I/O, no console, no clock, no engine.
  Game.cs                  the facade: New / Resume / Apply / View / NetWorth
  Model/Definitions.cs     content DTOs deserialized straight from JSON; CrewLever
  World/WorldLoader.cs     JSON strings -> validated WorldData; generates city markets and paints the map
  World/WorldData.cs       all resolved content; lookup helpers
  World/WorldMap.cs        terrain grid, layer flags, city snap
  World/MapPainter.cs      region polygons + road overlay -> cells
  World/City.cs            City, Route, CityGoodProfile (+ SteadyStateStock, governor)
  World/RouteGraph.cs      adjacency, Between, AreAdjacent, Reachable
  State/CityStock.cs       a city's two stores of one good: shelf (Out), intake (In), shelf grade
  State/GameState.cs       everything to resume: day, cash, rng, stock, caravan, crew, vitals, standing, events, mines, warehouses
  State/CrewMember.cs      somebody on the payroll; skills are a dict, so stats can grow
  Sim/Economy.cs           THE price model. Quotes, approximations, daily stock tick.
  Sim/TradeTerms.cs        how much of the market's spread the convoy still pays
  Sim/CrewMath.cs          what the roster is worth: speed, terms, running costs, wages, knowledge; posts gate levers
  Sim/Intel.cs             the information post: nearest cities by road, derived price reports with error
  Sim/QualityMath.cs       shop grade, cherry-pick, S-tier sell multiplier
  Sim/TradeXp.cs           small knowledge/skill XP on a buy or sell
  Sim/WarehouseMath.cs     rented room volume and unattended auto-trade
  Sim/Recruitment.cs       pure candidate generation; pools are derived, never stored
  Sim/CityStats.cs         what a city IS: live vitals, and supply read off its market
  Sim/Standing.cs          how the player relates to a city: segments, total, rank, reserved shelf, permits due, tier locks; Grant is the one write
  Sim/Contracts.cs         derived contract boards; offer resolution; delivery check
  Sim/Expos.cs             derived expo calendar; buff, fee, buyers; the stall's day tick
  Sim/WorldEvents.cs       what is happening to the world: fire, expire, price and vital overlays
  Sim/DayTick.cs           one day: charge costs, tick markets, events, advance travel, mine, solvency
  Sim/CaravanMath.cs       derived convoy properties over truck instances + fittings (Spec, resale, sell blocker)
  Sim/MapMath.cs           pathfinding, deposit placement, extract
  Sim/Rng.cs               seeded xorshift64*, state lives in GameState
  Commands/Commands.cs     the command records + CommandResult
  Commands/CommandProcessor.cs  the ONLY place state changes
  Events/GameEvent.cs      what the player is told; no display assumptions
  View/ViewModels.cs       front-end DTOs (GameView, MapView, and their parts)
  View/ViewBuilder.cs      state -> display snapshot; also the road scouting estimates
  Ai/TradeScout.cs         one-hop planning shared by the automated traders
  Ai/TraderPolicies.cs     GreedyTrader (skill baseline), RandomTrader (control), HouseTrader (play-tester)
  Ai/BotRunner.cs          plays a policy against a fresh game for N days; records how the run went

src/MechaTrader.Content/   the ONLY project that touches the filesystem
  ContentLoader.cs         finds data/, reads files, hands Core plain strings
  BuildInfo.cs             which build is running: version, commit log, staleness

src/MechaTrader.Host/      thin ASP.NET adapter — no rules live here
  Program.cs               5 endpoints, static file serving, startup banner
  GameSession.cs           holds the one game; parses JSON into Commands; display log

web/                       static files the host serves. The player view is NOT here.
  index.html               stub that redirects to /chart/
  archive/iso-ops-console/ DEAD isometric ops console. Do not use.
  artlab/                  rejected 2.5D sprite tool. Do not reuse for the map.
  favicon.ico              the browser tab icon, and the desktop shortcut's icon

D:\FrontMission-MapLab\    live map (sibling repo). Read its map-design-sop.md before touching it.
  chart.html               Keeper's Chart — the player view
  game-bridge.js           talks to /api/state, /api/map, /api/command; owns no rule
  ops.js                   the ops shell: page/tab registry, data grid, detail pane, every
                           non-map screen (overview, city, caravan, crew, ledger)
  ops.css                  the shell's theme: dark blue-grey, ERP density, blue accent
  world.js                 generated from data/ by make-world.js

tools/MechaTrader.BalanceSim/  headless economy gate; also writes FIGURES.md
tests/MechaTrader.Core.Tests/  incl. ArchitectureTests, which enforce Core's purity
```

## Core API

The surface you will actually call. Types: `CityStock` is `(double Out, double In)` with
`.Total`; `TradeTerms` is the crew's share of the market spread, `TradeTerms.Market` when
there is no crew.

```csharp
// Game.cs — the whole simulation behind one surface
Game.New(WorldData world, ulong seed)               // fresh run; markets open settled
Game.Resume(WorldData world, GameState state)       // from a save
game.Apply(Command command) -> CommandResult        // the only way to change anything
game.View() -> GameView                             // display snapshot; pure read
game.NetWorth() -> long

// Sim/Economy.cs — prices. Buy reads the shelf, sell reads shelf + intake.
Economy.UnitPrice(good, profile, double stock, cfg, eventMult=1) -> double        // mid price
Economy.BuyUnitPrice (good, profile, CityStock, cfg, terms, eventMult=1) -> double
Economy.SellUnitPrice(good, profile, CityStock, cfg, terms, eventMult=1) -> double
Economy.UnitsOnTheShelf(CityStock, cfg) -> int                       // hard cap on a buy
Economy.QuoteBuy /QuoteSell (good, profile, CityStock, units, cfg, terms) -> Quote
Economy.ApproximateBuyCost/ApproximateSellRevenue(...) -> double     // planning only
Economy.MaxAffordableUnits(good, profile, CityStock, cash, freeVolume, cfg, terms) -> int
Economy.TickStock(CityStock, profile, cfg, Rng) -> CityStock         // one day
Economy.InitialStock(profile, cfg) -> double

// Sim/CaravanMath.cs — everything derived from the convoy, pure over state + content
CaravanMath.Capacity / UsedVolume / FreeVolume (caravan, world) -> double
CaravanMath.SpeedKmPerDay(caravan, world) -> double        // trucks x crew navigation
CaravanMath.DailyUpkeep (caravan, world) -> double         // truck upkeep x crew + wages
CaravanMath.TruckSpeedKmPerDay / TruckUpkeep(caravan, world) -> double   // before crew
CaravanMath.TravelDays(caravan, world, route) -> int
CaravanMath.TravelFuel(caravan, world, route) -> double
CaravanMath.CanTravel / CanMine / MineYield (caravan, world)

// Sim/MapMath.cs — geography. Pure over state plus the painted grid.
MapMath.Position(state, world) -> TerrainCell
MapMath.Pathfind(caravan, world, from, to) -> TravelPlan?
MapMath.PlaceDeposits(world, seed) -> List<MiningSite>

// Sim/CrewMath.cs — what the roster is worth. A skill is led by the BEST hand aboard
// that is allowed to pull it: a post (crew.json `posts`) claims levers, and a claimed
// lever is pulled only by the hands on that post. Unclaimed levers are convoy-wide.
CrewMath.Level(roster, skillId) -> int                     // raw, posts ignored
CrewMath.Level(roster, cfg, skillId) -> int                // gated by the skill's lever's post
CrewMath.Leader(roster, cfg, skillId) -> CrewMember?
CrewMath.OnPost(roster, cfg, lever) -> the hands who pull that lever
CrewMath.PullsLever(member, cfg, lever) -> bool

// Sim/Intel.cs — the information post. Pure reads; never touches the RNG.
Intel.Level / Reach / Error / Informant (roster, cfg)     // 0 / 0 / maxError / null with nobody posted
Intel.Nearby(world, caravan, fromCityId, count) -> nearest cities by road, with days
Intel.Coverage(state, world, fromCityId) -> the cities the post reads right now
Intel.Reports(state, world, coverage, good) -> PriceReport per city (buy, sell, error)
Intel.Noise(seed, cityId, goodId, day, side) -> [-1, 1]  // deterministic per day
CrewMath.Effect(roster, cfg, lever) -> double              // lever = CrewLever.Speed etc
CrewMath.Terms(caravan, world, categoryId?) -> TradeTerms
CrewMath.KnowledgeFactor / SelectionFactor / BestKnowledge (roster, cfg, categoryId)
CrewMath.SpeedMultiplier / RunningCostMultiplier(caravan, world) -> double
CrewMath.DailyWages(roster) -> long
CrewMath.WageFor(skills, cfg) -> long

// Sim/CityStats.cs — what a city is. Pure reads; building the page advances nothing.
CityStats.Vital(state, city, vitalId) -> double                 // stored, falling back to founding
CityStats.Vital(state, world, city, vitalId) -> double          // stored + event overlay, clamped
CityStats.Founding(city, vitalId) -> double
CityStats.Band(bands, value) -> StatBandDef?           // first band the value falls under
CityStats.Supply(state, world, city, CitySupplyDef) -> SupplyReading
//   SupplyReading is (Index, Production, Consumption, Stock, Nominal) with .NetFlow and
//   .DaysOfCover. Index is 100 x what the city holds / what it would hold undisturbed.

// Sim/Recruitment.cs — pure function of (seed, city, round). Never touches the RNG.
Recruitment.PoolFor(world, city, ulong seed, int day) -> IReadOnlyList<CrewCandidate>
Recruitment.RoundFor / DaysUntilRefresh(day, cfg) -> int

// Sim/Standing.cs — how the player relates to a city. Pure reads plus one write.
Standing.Of(state, cityId) -> double                 // total across segments
Standing.Segment(state, cityId, segmentId) -> double
Standing.Grant(state, config, cityId, segmentId, amount) -> landed   // clamps to segmentMax
Standing.TierOpen(tier, standingTotal) -> bool
Standing.Rank(config, standing) -> StatBandDef?
Standing.ReservedRatio(config, standing) -> double
Standing.ReservedUnits / PublicUnits (shelfUnits, ratio) -> int
Standing.Due(config, standing) -> permits whose threshold this value has crossed

// Sim/WorldEvents.cs — what is happening to the world. Overlays are derived; shocks write.
WorldEvents.PriceMultiplier(state, world, cityId, goodId) -> double   // 1 if nothing running
WorldEvents.ReliefPerUnit(state, world, cityId, goodId) -> double     // citizen standing per unit sold

// Sim/Contracts.cs — derived boards. Never touches the RNG.
Contracts.BoardFor(world, city, seed, day) -> IReadOnlyList<ContractOffer>
Contracts.Resolve(world, seed, contractId) -> ContractOffer?
Contracts.DeliveryBlocker(state, world, offer) -> string?

// Sim/Expos.cs — derived calendar; the stall trades on the day tick.
Expos.Running / Next(world, city, seed, day) -> ExpoInstance?
Expos.Buff(cfg, theme) / Fee(cfg, city) / BuyersPerDay(cfg, city, buff)
Expos.CityMakes(city, goodId) / ThemeCovers(theme, good)

// Sim/CaravanMath.cs — trucks are instances
CaravanMath.Spec(truckState, world) -> TruckSpec       // type + fittings
CaravanMath.ResaleValue / SellBlocker / NewTruck
WorldEvents.VitalDelta(state, world, cityId, vitalId) -> double
WorldEvents.PriceHint(state, world, cityId, goodId) -> string         // already formatted
WorldEvents.Start(state, world, def, cityId, day) -> ActiveEvent     // tests; applies stock shock
WorldEvents.Tick / ExpireDue (state, world, rng?, events)            // day tick

// State/GameState.cs
state.StockOf(cityId, goodId) -> CityStock
state.TotalStockOf(cityId, goodId) -> double     // what the sell price reads
state.ShelfOf(cityId, goodId) -> double          // what the buy price reads
state.SetStock(cityId, goodId, CityStock)
state.VitalOf(cityId, vitalId) -> double?        // null = this run never heard of it
state.SetVital(cityId, vitalId, double)          // the one place a city stat changes
state.StandingOf(cityId) -> double               // 0 if never courted
state.SetStanding(cityId, double)
state.HasPermit / GrantPermit (cityId, permitId)
state.ActiveEvents                                 // live world events; overlays are derived

// Commands — all validate fully, then mutate
BuyCommand(goodId, units)         SellCommand(goodId, units)     // Buy refuses a locked tier; pays for the grade picked
SellTruckCommand(truckId)         UpgradeTruckCommand(truckId, upgradeId)
AcceptContractCommand(id)         DeliverContractCommand(id)
ExpoRegisterCommand()             ExpoListCommand(goodId, price)  // 0 clears
DepartCommand(toCityId)           WaitCommand(days)
BuyTruckCommand(truckTypeId)      HireCrewCommand(candidateId)    // signs on to the role's default post
AssignCrewCommand(crewId, postId) // "" stands down; free; works on the road
BuyGearCommand(gearId)            DismissCrewCommand(crewId)
CityFavorCommand(actionId)
RentWarehouseCommand()            WarehouseDepositCommand(goodId, units)
WarehouseWithdrawCommand(goodId, units)
SetWarehouseSellCommand(goodId, price)    // 0 clears
SetWarehouseProcureCommand(goodId, price) // 0 clears

// Ai — policies talk only through Game.Apply. They own no rule.
ITraderPolicy.Decide(game, rng) -> Command?
BotRunner.Run(world, policy, days, seed) -> BotRunResult
TradeScout.BestRunFrom / BestRepositioning (game, cityId)
GreedyTrader            // un-crewed haulage; skill-expression baseline
RandomTrader            // control; careless play must lose
HouseTrader             // haulage plus hire / extra mule / donate; the play-tester
```

## Change-impact map

Threading a new parameter or field through the codebase is the most common shape of work
here, and the call sites are not obvious. These are the clusters, in the order you should
edit them.

| Change | Every place it lands |
|---|---|
| **A pricing signature** (anything in `Economy`) | `Sim/Economy.cs` internals → `Commands/CommandProcessor.cs` (Buy, Sell) → `View/ViewBuilder.cs` (NetWorth, BuildMarket, BestCargoFor) → `Ai/TradeScout.cs` (BestRunFrom) → `Ai/TraderPolicies.cs` (RandomTrader, HouseTrader) → `tools/BalanceSim` (PrintOpportunities, PrintCrew) → `EconomyTests`, `CrewTests` |
| **What a city holds** (`CityStock`) | `State/CityStock.cs` → `GameState` accessors → `Game.New` → `Economy.TickStock` → `Sim/DayTick.cs` → `CommandProcessor` → `ViewBuilder` → `BalanceSim` → `CommandTests`, `EconomyTests`, `SimulationInvariantTests` |
| **A new command** | `Commands/Commands.cs` → a case in `CommandProcessor.Execute` → a branch in `GameSession.TryParse` (+ the `CommandRequest` record) → a button in `D:\FrontMission-MapLab\ops.js` that calls `send({ type, … })` (map moves go through `game-bridge.js` instead) → bump `?v=N` on `ops.js` (or `game-bridge.js`) in `chart.html` → **`BRAIN.md` then `HouseTrader`** if an auto-caravan or rival should also issue it. Do not add it to `web/archive/iso-ops-console/`. |
| **The brain** (what an automated trader does) | `BRAIN.md` first → `Ai/TradeScout.cs` if the best hop changed → `HouseTrader` in `Ai/TraderPolicies.cs` for extras → `Ai/BotRunner.cs` if a new command kind needs a label → `PlaytestTests` → `BalanceSim` if the harness should fail when the house never uses it. Do not teach `GreedyTrader`. Live factions / player auto-caravans are the same policy on N bodies; that split is not in state yet |
| **Anything new on screen** | `View/ViewModels.cs` → populate in `View/ViewBuilder.cs` → a `registerPage` / `registerTab` entry (or a column, card or side pane) in `D:\FrontMission-MapLab\ops.js`; only map geometry goes into `chart.html` / `game-bridge.js`. Bump `?v=N` on the script you changed in `chart.html`. The shell never computes a rule: if the number is not on a view model, add it to `ViewBuilder` first. The archived ops console is not the player view. |
| **A new content file** | `data/x.json` → a DTO in `Model/Definitions.cs` → `WorldLoader`: key constant, `RequiredKeys`, parse, validate → a property on `WorldData` → **add it to `MinimalWorld.Files` in `WorldLoaderTests`** or every loader test fails |
| **A new city stat** (a vital) | `data/citystats.json`: an entry under `vitals` → a value per city in `data/cities.json` under `stats` → nothing in C# unless the simulation must *read* it. Adding one to `MinimalWorld` is only needed if a loader test asserts on it |
| **A new supply band** | `data/citystats.json`: an entry under `supplies` naming goods that exist. No C# at all |
| **Something a city stat must actually drive** | `Sim/CityStats.cs` (the reader) → wherever the rule lives (`Economy`, `CaravanMath`, `DayTick`) → `View/ViewBuilder.cs` if the effect should be legible → `CityStatsTests` |
| **Standing / governor / permits** | `data/standing.json` → DTOs in `Definitions.cs` → `WorldLoader` + `WorldData.Standing` → `Sim/Standing.cs` → `GameState` standing/permits → `CityFavorCommand` in `CommandProcessor` → `ViewBuilder.BuildStanding` → `StandingTests`. The Governor tab in `ops.js` renders whatever the view carries — do not revive the archive to add chrome. A new favor action is a JSON line. **Add standing.json to `MinimalWorld.Files`.** |
| **World events** | `data/events.json` → `EventDef` / `EventsConfig` in `Definitions.cs` → `WorldLoader` + `WorldData.Events` → `Sim/WorldEvents.cs` → `GameState.ActiveEvents` → `DayTick` → `Economy` (`eventMult`) and `CityStats.Vital(state, world, …)` → `ViewBuilder` (wire, market hint, map ids) → `EventTests`. A new template is a JSON object. **Add events.json to `MinimalWorld.Files`.** Do not put event chrome on the archived console. |
| **Terrain / travel layers** | `data/map.json` → `MapFile` / `WorldMap` → `MapPainter` + `WorldLoader` → `Sim/MapMath.cs` → `Depart` / `DayTick` → `ViewBuilder.BuildMap` + mining markers → `chart.html` / `game-bridge.js`. **Add map.json to `MinimalWorld.Files`.** Do not touch `web/archive/iso-ops-console/`. |
| **Gear / mining machines** | `data/gear.json` + `trucks.json` `kind`/`capabilities`/`mineYield` → `TruckDef`/`GearDef` → `BuyGearCommand` → `CaravanMath.CanMine`/`MineYield` → `MiningTests`. **Add gear.json to `MinimalWorld.Files`.** The Depot tab in `ops.js` lists `Shipyard` and `Outfitters` as they arrive. |
| **Catalog / categories / quality** | `data/goods.json` (categories + quality knobs + goods) → `GoodDef.Category` / `CategoryDef` / `QualityConfig` → `WorldLoader` + `WorldData` → `Sim/QualityMath.cs` → `CommandProcessor` Buy/Sell → `ViewBuilder` market/cargo → `QualityTests`. A new good still needs an industry that makes or eats it. The Market tab and trade pane in `ops.js` pick up new rows and quality fields from `MarketRowView`. |
| **Posts / the information post** | `data/crew.json` `posts` (id, levers) + `intel` knobs + a role's optional `post` → `CrewPostDef` / `IntelConfig` → `WorldLoader.ValidateCrew` → `CrewMember.PostId` → `CrewMath.OnPost` (every gated read goes through it) → `Sim/Intel.cs` (reach, error, nearby, reports) → `HireCrew` (default post) / `AssignCrewCommand` → `CrewView.Posts` / `CrewView.Intel` / `MarketRowView.Elsewhere` in `ViewBuilder` → the post `<select>` on the Crew roster and character sheet, and the "Nearby markets" card in `tradePane`, in `ops.js` → `CrewTests` (posts, information). A new post is JSON: name the levers it claims. A hand-built `CrewMember` in a test must set `PostId` or the counter never sees it. HouseTrader's `LeverGain` scores a candidate only on the levers of the post they would sign on to. |
| **Category knowledge / special traits** | `data/crew.json` roles (`categoryId`), `traits`, knowledge knobs → `CrewMember.Knowledge` / `TraitIds` → `Recruitment` → `CrewMath` Terms/SelectionFactor/speed/upkeep → `TradeXp` on Buy/Sell → `QualityTests`, `CrewTests`. The Crew page and character sheet (`personPage` in `ops.js`) render skills, knowledge and traits from `CrewMemberView` / `CandidateView`. |
| **A product tier / grade lock** | `data/goods.json` `tiers` (colour, `minStanding`, `minPricePerVolume`, `equilibriumScale`) → `TierDef` → `WorldLoader.ValidateTiers` → `Standing.TierOpen` in `CommandProcessor.Buy`, `TradeScout`, `ViewBuilder.BestCargoFor`, `BalanceSim` → `MarketRowView.Locked` / `TierGateView` → `ProductTests`. The shell colours names from `tierColor`; it never decides a lock. |
| **Production grade / craft** | `goods.json` `quality` (`base`, `random`, `cityVitalId`, `cityVitalWeight`) → `QualityMath.ProductionQuality` → `DayTick.TickMarkets` (draws per city per good) and `Game.New` (opening midpoint) → `ProductTests`. A new vital that should drive grade is a `cityVitalId` change, no C#. |
| **Relationship segments** | `standing.json` `segments`, `segmentMax`, per-action `segmentId` → `GameState.CityStanding[city][segment]` → `Standing.Grant` (the one write) → `CommandProcessor` (Favor, Sell relief + volume, DeliverContract, lapse in `DayTick`) → `ViewBuilder.BuildStanding` segments + tier gates → `ProductTests`, `StandingTests`. Rank / reserve / permits read the total; never store it. |
| **Shortage / glut events** | `events.json` `categories`, `reliefStanding`, `reliefUnits` → `EventDef` → `WorldEvents.AffectsGood` (category) + `ReliefPerUnit` → `CommandProcessor.Sell` → `MarketRowView.ReliefPerUnit` → `ProductTests`. A new template is JSON; `{category}` formats in the headline. |
| **Station / fittings** | `trucks.json` `upgrades`, `resaleFraction` → `TruckUpgradeDef` → `CaravanMath.Spec` / `ResaleValue` / `SellBlocker` → `SellTruck` / `UpgradeTruck` → `StationView` → the Station tab in `ops.js` → `StationTests`. A new fitting effect is a field on the def and one line in `Spec`. HouseTrader picks the economy fitting by effect. |
| **Contracts** | `data/contracts.json` → `ContractsConfig` → `Sim/Contracts.cs` (derived boards, never stored) → `GameState.Contracts` / `ContractsClosed` → `AcceptContract` / `DeliverContract` → `DayTick.LapseContracts` → `ContractsView` → Contracts tab (city) + Contracts tab (caravan) in `ops.js` → `ContractTests`. **Add contracts.json to `MinimalWorld.Files`.** |
| **Expos** | `data/expos.json` → `ExposConfig` → `Sim/Expos.cs` (derived calendar; `Tick` is the stall's day) → `GameState.ExpoPasses` / `Caravan.ExpoAsks` / `LastExpoDay` → `ExpoRegister` / `ExpoList` → `DayTick` (before the day advances) → `ExpoView` → Expo tab in `ops.js` (the hall replays the report) → `ExpoTests`. **Add expos.json to `MinimalWorld.Files`.** |
| **Storerooms** | `config.warehouse` → `WarehouseState` on `GameState` → `RentWarehouse` / deposit / withdraw / auto prices → `WarehouseMath.Tick` in `DayTick` → `WarehouseTests`. HouseTrader does not rent. The Storeroom tab in `ops.js` covers rent, deposit, withdraw and the two standing prices. |
| **Anything about which build is running** | `src/MechaTrader.Content/BuildInfo.cs` → `GET /api/build` → show it on `chart.html` if the player should see it → `BuildInfoTests`. Nothing in Core: the simulation does not know what a build is |
| **Convoy-derived numbers** | `Sim/CaravanMath.cs` only — `DayTick`, `ViewBuilder`, `TradeScout` and `Depart` all read through it, so they pick the change up for free |

## What the tests hold down

Read this instead of reading the suite. `tests/MechaTrader.Core.Tests/`:

- **`ArchitectureTests`** — greps Core for `System.IO`, `File.`, `Directory.`, `Console.`,
  `DateTime.Now`, `new Random(`; asserts `MechaTrader.Core.csproj` has no references.
- **`WorldLoaderTests`** — shipping content loads; every city quotes every good; every
  city is reachable; producers are cheaper than consumers; loader rejects bad references.
  Holds `MinimalWorld`, the in-memory two-city world every loader test builds on.
- **`EconomyTests`** — scarcity raises price; the spread; large orders move the price;
  the approximation tracks the exact quote within 3%; the two stores (selling never moves
  the shelf price, sell quote never beats buy quote, intake shelves over days, an untraded
  city ticks exactly as it did before the split).
- **`CommandTests`** — buy/sell/depart/wait/buy-truck happy paths and every rejection;
  rejected commands leave state byte-identical; upkeep is charged daily.
- **`CrewTests`** — pools are deterministic, city-local and refresh on schedule; reading
  the board does not advance the world; hiring costs, capacity, wages, severance; the
  no-arbitrage properties with a maxed crew. Posts: only a hand at the counter haggles;
  road skills stay convoy-wide; a hire lands on the role's default post; assignment is a
  command that refuses a repeat, refuses an unknown post untouched, works on the road and
  survives a save. Information: nobody posted means no reports; reach grows with
  intelligence and covers the nearest cities in road order; a perfect informant reports
  the exact quote; a dull one stays within the error bound; reading reports never touches
  the RNG and is stable for the day.
- **`CityStatsTests`** — every city has every declared vital and starts on it; population
  still scales industry; live values override founding, survive a save, and fall back when
  a save predates a stat; bands take the first slice a value falls under; every supply
  opens at nominal and moves the right way when the player buys or sells; reading the
  city page changes nothing; every vital reaches the screen ready to print.
- **`StandingTests`** — a new run starts at zero with no permits; donate / invest / aid
  cost the authored fee; invest writes the named vital; aid fills intake of the shortest
  supply and leaves the shelf alone; permits grant at threshold and stick; reserved share
  is derived never stored; the player can still buy the reserved shelf; reading the city
  page changes nothing; standing survives a save; favor does not touch the RNG.
- **`EventTests`** — a new run is quiet; a price event moves the quote; a vital overlay
  does not write the stored value and drops when the event ends; a stock shock writes the
  shelf and is not undone; the city wire prints the dispatch; a global event hits every
  city; sell still cannot beat buy under a price event; reading the page changes nothing;
  events survive a save; a certain daily roll fires on wait; invest still writes the
  stored vital under an overlay.
- **`MapTests` / `MiningTests`** — cities sit on land; alpine roads stay walkable; mountains
  block land and deep water blocks air; same seed yields the same deposits; waiting on a
  claim without gear extracts nothing; gear extracts ore at cost 0; deposits survive save/load;
  reading the view does not consume RNG.
- **`QualityTests`** — buying the whole shelf keeps the average even at full knowledge;
  knowledge does not change a random draw; a small pick with knowledge is above average
  and lowers what is left; S-tier sells at +30%; category knowledge improves the bargain;
  a buy grants category XP.
- **`WarehouseTests`** — rent costs the authored fee; a second rent is refused and leaves
  state untouched; deposit/withdraw preserve quality; a low auto-sell ask clears the room
  on wait.
- **`BuildInfoTests`** — the build page reports the VERSION file, HEAD and the commit
  log; build output never counts as a change; code staleness is judged against the
  binary's compile time and content staleness against process start; a folder with no
  repository degrades to "unknown" instead of throwing.
- **`SimulationInvariantTests`** — same seed + same commands ⇒ identical state; save/load
  round trip; skilled play profits, careless play loses; 1000 days stays finite.
- **`PlaytestTests`** — HouseTrader is deterministic; reading Decide does not consume the
  game RNG; GreedyTrader still never hires; given cash and a run, HouseTrader hires;
  a 60-day house run leaves town and touches at least one of crew / trucks / standing.

## Invariants — do not break these

**1. `MechaTrader.Core` stays pure.** No `System.IO`, `File.`, `Directory.`, `Console.`,
`DateTime.Now`, or `new Random(`. `ArchitectureTests.CoreStaysFreeOfSideChannels` greps
for these and fails the build. `MechaTrader.Core.csproj` must have zero `ProjectReference`
and zero `PackageReference`. Content reaches Core as strings via `WorldLoader.Load`.

**2. State changes only through `CommandProcessor`.** Validate fully, then mutate. A
rejected command must leave state byte-identical — `RejectedCommandsLeaveStateUntouched`
checks this. Never mutate `GameState` from a view, a host, or the AI.

**3. Determinism.** Seed + command list ⇒ identical state, always. The RNG's entire state
is one `ulong` in `GameState`; there is no static randomness anywhere. The day tick
iterates cities and goods in content load order so the random sequence is stable. This is
what makes replay, save/load, the balance harness and regression tests possible.

**4. Time advances only via `WaitCommand`.** One clock. It works whether parked or
travelling; `Depart` starts a journey but spends no days itself.

**5. Content lives in `data/`, never in C#.** No city, good, price, or route may be
hardcoded. Tests and `check.ps1` derive cities from `world.Config.StartCityId` and the
road graph rather than naming them, so retuning the map cannot silently invalidate them.
Keep it that way.

**6. Money is `long`, stock is `double`.** Round once, at settlement. Never accumulate
money in floating point.

**7. What the player sells never lands where the player buys.** A city holds each good in
two stores (`CityStock`): `Out`, the shelf it sells from, and `In`, the intake holding
what caravans have unloaded on it. A buy drains the shelf and is capped by it; a sell
fills the intake; the tick eats out of the intake and shelves the rest at `restockRate`.
So unloading a hold cannot cheapen the shelf, and there is no sell-then-buy-back loop —
not one that loses to the spread, none at all.

**8. The sell quote can never exceed the buy quote.** The buy price reads the shelf as
the day opened, the sell price reads that shelf plus the opening intake, and price falls
as stock rises — so the inequality holds at every possible holding, by construction
rather than by tuning. Prices move only at the day tick (and on a stock shock); an
order settles flat. Never reintroduce a within-order price walk. Crew bonuses are a
second, independent guard: they are a *share of the spread still paid* (`TradeTerms`),
clamped to [0, 1], so they close the gap but never invert it. Never re-express a crew
bonus as a direct multiplier on the price.

**9. Recruitment pools are derived, never stored.** A pure function of
`(seed, cityId, round)` that must never touch `GameState.RngState`, or building a view
would advance the world.

**10. A city's founding stats are content; its live stats are state.** `cities.json`
says where a city starts, `GameState.CityVitals` says where it is. Every stored read goes
through the three-argument `CityStats.Vital`, which falls back to content only when a
save has never heard of that stat. The page reads the four-argument form, which adds any
active event overlay and clamps. Nothing may edit content to move a city, and nothing
may write a stored vital outside `CommandProcessor` — invest and aid write through
`SetVital`; events overlay, they do not write.

**11. Supply figures are derived, never stored.** They read the city's market on demand,
like a recruitment pool reads the seed. Storing one would give the game two answers to
the same question, and the stored one would be the stale one.

**12. Standing is per-city state; rank, reserved share and permit *eligibility* are
derived.** `GameState.CityStanding` is the number. Rank and the reserved-shelf fraction
are reads of that number, never stored. Permits, once granted, are the exception: they
stick as ids in `GameState.CityPermits` so a later drop in standing does not take the
paper back. Aid that ships goods lands in intake, never on the shelf.

**13. Event instances are state; price and vital *effects* are derived.**
`GameState.ActiveEvents` is the list. A price multiplier and a vital overlay are reads
of that list, never stored beside it, so they vanish when the instance expires. A stock
shock is the exception: it writes the shelf (or intake) once on fire, because goods do
not teleport back. Firing consumes the day's RNG; building a view must not.

**14. Mining claims are state; layer walkability and mine yield are derived.**
`GameState.MiningSites` is the list, generated once from the run seed. Remaining reserve
is stored because it depletes. Whether a cell is land/air/water, and how much a convoy
extracts, are reads of biome + road overlay + gear/machines, never stored beside them.
Extracting happens on `Wait` while parked on a claim; there is no second clock.

**15. Shop grade is state; S-tier is derived.** `CityStock.OutQuality` is the average
of the shelf. Knowledge never rewrites it. Buying the whole saleable pile always takes
that average; a smaller order with knowledge skips worse crates and conservation then
lowers what is left. S-tier is a read of a lot's quality against `sTierAt`, never a
stored flag.

**16. A storeroom is per-city state; auto fills are writes on the day tick.** Rent,
stock and the two auto prices are stored. Unattended orders use market terms: no crew
eye, no bargain. Deposit and withdraw preserve quality.

**17. The shop charges for the grade you pick.** A buy settles at the quote times the
same grade multiplier a sale uses, so the S-tier premium sits on both ends and a
knowledgeable crew cannot sell a shelf back to itself. Never apply the grade multiplier
on one side only. Planners size orders against the best single crate so a buy is never
rejected for cash.

**18. Relationship segments are state; the total is derived.** `CityStanding[city]` holds
one number per declared segment. Rank, the reserved shelf, permit eligibility and tier
locks read the sum on demand. `Standing.Grant` is the only write and clamps to
`segmentMax`. A tier lock is never stored per city.

**19. Contract boards and expo calendars are derived; only acceptances, passes and asks
are state.** Both are pure functions of `(seed, city, round)` and never touch
`GameState.RngState`. An offer's terms are re-resolved from its id on delivery, so a
front-end cannot hand the processor altered terms. The expo stall draws the RNG only
on a day something is listed; the last day's report is stored so the hall can replay it
without drawing again.

**20. A city's own produce never sits on a stall at its own expo.** `Expos.CityMakes`
is the guard, checked on `ExpoList` and again on the tick. Buyers anchor on base price,
not the local shelf, so the residual case (a good bought here that the city does not
make) is bounded by the buff premium minus the spread, the fee, and the days spent.

**21. A post is state; who pulls a lever is derived. Price reports are derived, never
stored, and never true.** `CrewMember.PostId` is the one stored fact. Every gated read
goes through `CrewMath.OnPost`, so a broker off the counter haggles for nobody and the
terms, the cherry-pick eye and the knowledge bargain all agree on it. The information
post's reports are a read of the nearest cities' live stock with the convoy's own
terms, offset by a hash of (seed, city, good, day): stable for a day, different
tomorrow, and never a draw on `GameState.RngState`. Nothing may store a report, and
nothing may hand the true price to the front-end under the informant's name.

## How it fits together

```
Command ──> CommandProcessor ──> GameState ──> ViewBuilder ──> GameView ──> JSON ──> browser
                   │                  ▲
                   └── DayTick ───────┘   (markets, costs, travel, solvency)
```

**A day, in order:** charge upkeep + fuel + payroll + storeroom rent → tick every city's
every good (roll today's production grade from the city's craft; eat from intake,
produce, shelve `restockRate` of the intake, settle toward equilibrium, noise) →
unattended storeroom buy/sell → the expo stall's buyers, if one is open → increment day
→ expire/fire world events → tear up lapsed contracts → decrement travel and maybe
arrive → if the convoy spent the day on a claim, extract ore → update solvency flag.

**The price model** (full detail in `SPEC.md`):

```
price = basePrice × clamp((equilibrium / max(stock, minStock)) ^ elasticity, 0.4, 2.5) × eventMult
buy   = price(shelf)          × (1 + spread × buyShare)
sell  = price(shelf + intake) × (1 − spread × sellShare)
```

The two sides read different stores, which is what makes `sell ≤ buy` structural.
`buyShare`/`sellShare` are 1 with no crew and fall toward 0 as negotiation and sales
rise; they are the only thing crew do to a price.

**Prices move at the day tick, never inside a deal.** Every quote reads the shelf and
intake as the day opened (`CityStock.OpenOut` / `OpenIn`); an order settles at one price
for the whole lot and the second order today pays what the first did. What a deal does
is move the stock, so tomorrow's shelf is scarcer or fuller. Bulk is never penalised.
`ApproximateBuyCost` / `ApproximateSellRevenue` are exact (unit price × units).

Daily stock tick settles at `equilibrium + (production − consumption) / driftRate`, so a
city that produces a good sells it cheap and a city that consumes one pays dearly. The
entire trade map falls out of that; no price table is authored anywhere. Two independent
knobs: `equilibriumDays` controls market **depth**, `driftRate` controls price
**gradient** (lower = wider margins).

**Pressure without combat:** truck upkeep, distance-based fuel and payroll. Money leaks
daily, so standing still has a price and a bad route costs real credits. Market depth
is in the size of the stocks and the day boundary, not in a penalty inside an order.

## Extending things

**A city** — `id`, `name`, `region`, `lon`, `lat`, `industries`, `stats`, and a
`governor` in `cities.json`, plus at least one entry in `routes.json`. Its whole market
is generated from its industry archetypes. The loader rejects any city unreachable from
the start. A city that omits `governor` gets a stable name from the crew name pools.

**A good** — an entry in `goods.json`, then reference it from at least one industry in
`industries.json`. Every city automatically gets a market row for it.

**An industry** — an entry in `industries.json` with per-good production/consumption at
population 1.0. Every city using it re-derives.

**A crew skill** — an entry in `crew.json` under `skills`, naming one of the levers in
`CrewLever` (`speed`, `buy`, `sell`, `upkeep`, or `none` for a stat that does nothing
yet). Every city's candidates roll it immediately and the crew panel renders it; no C#
changes unless the skill needs a lever that does not exist. A *new* lever means adding
the constant, a case in `CrewMath`, and a case in `ViewBuilder.EffectText`.

**A hiring role** — an entry in `crew.json` under `roles` with a `primary` skill id
(empty means generalist) and optional `categoryId` for a knowledge spike. Optionally
bias which cities grow it via `industryAffinity`.

**A special trait** — an entry under `traits` in `crew.json` (kind `product`,
`traveling`, `repair` or `bargain`). Candidates roll at most one. No C# unless a new
kind of effect is needed.

**A storeroom** — `config.warehouse` sets rent, daily rent and capacity. Renting is a
command; auto prices are commands. The city page grows the panel.

**A city stat** — an entry under `vitals` in `citystats.json` (id, name, unit, default,
min, max, decimals, `displayScale`, blurb, bands) and a value per city under `stats` in
`cities.json`. Cities that do not author it fall back to the catalogue default. It renders
on the city page and rides in every save immediately; it changes nothing in the simulation
until something is written that reads it.

**A supply band** — an entry under `supplies` in `citystats.json` naming goods that exist.
Every city derives it from its own market at once. No C# either way.

**A favor action** — an entry under `actions` in `standing.json` (id, name, cost,
standing gain, optional `vitalId`/`vitalDelta`, optional `stockPerGood`).
`CityFavorCommand` looks it up. The city page grows a button.

**A product tier** — an entry under `tiers` in `goods.json`; goods name it by number. The
loader holds every good to the tier's value floor. `minStanding` locks it behind the
total. **A category** — an entry under `categories`, then goods, an industry that makes
it and one that eats it, and optionally a role and a product trait in `crew.json`.

**A relationship segment** — an entry under `segments` in `standing.json`. Nothing reads
it until an action or a rule names it by id; the fourth is deliberately unused.

**A truck fitting** — an entry under `upgrades` in `trucks.json` with the kinds it fits and
any of `capacityBonus`, `speedMult`, `fuelMult`, `upkeepDelta`, `mineYieldBonus`.

**A contract kind** — an entry under `kinds` in `contracts.json`. **An expo theme** — an
entry under `themes` in `expos.json` naming 2 to 5 categories. Both are JSON only.

**A permit** — an entry under `permits` in `standing.json` with the standing threshold.
It appears on the city page locked or granted. Building the shop or factory is a later
act; holding the paper is the grant.

**A world event** — an entry under `events` in `events.json` (id, headline, duration,
weight, optional city/industry/region/good filters, `priceMult`, `vitalDeltas`,
`stockMult`/`stockDelta`). `WorldEvents.Tick` looks it up. The city wire, the market
hint and a map ring appear with no C# if the template already uses those three knobs.
A new *kind* of effect (something other than price, vital overlay or stock shock) is
a C# change.

**The version string** — one line in `VERSION` at the repository root. The build page,
the startup banner and acceptance criterion 7 all read that file, so there is nowhere
else to change it.

**A command / anything on screen / a new content file** — see the change-impact map above.

**The front-end must never compute a game rule.** If the browser needs to know something,
derive it in `ViewBuilder` and put it on a view model.

After any content or economy change, run the balance harness. It fails if the economy
stopped working, and it rewrites `FIGURES.md` — quote that file, never your memory.

## Environment gotchas

These have each cost time before:

- **A running server locks the build output.** Stop `dotnet` processes before building,
  or MSBuild fails to copy DLLs.
- **The build badge is the answer to "am I testing the latest?"** Green means nothing on
  disk is newer than what is running; amber means uncommitted work; red means you edited
  something and did not rebuild. Trust it before debugging a change that "did nothing".
- **The desktop shortcut points at `Play.cmd`, not at a binary.** That is deliberate:
  `Play.cmd` rebuilds before it serves, so the launcher cannot start a stale build.
- **The browser caches `chart.html`, `game-bridge.js`, `ops.js` and `ops.css`.** `chart.html`
  carries a `?v=N` query on each; bump the one you changed, or you will debug stale code.
  `web/index.html` is only a redirect to `/chart/`.
- **The ops shell re-renders on every snapshot**, including each travelling day. It defers
  while an input inside it has focus and restores scroll positions, so keep per-screen
  state in `S` (selection, quantities, sort) rather than in the DOM.
- **Windows PowerShell 5.1 `Invoke-WebRequest` needs `-UseBasicParsing`.** Without it, it
  routes through the IE engine and dies in a non-interactive session.
- **`Set-Location` does not change the working directory child processes inherit.** Launch
  scripts by full path; `Play.cmd` resolves its own directory with `%~dp0`.
- **The world loads once, at host startup.** Editing `data/*.json` needs a server restart.
- **`main` is a two-column grid** — that layout belongs to the *archived* ops console.
  Do not add panels to `web/index.html`; it only redirects to `/chart/`.
- **Heredocs with `'''` inside break the Bash tool.** Write a `.py` patch file to the
  scratchpad and run it instead, for any multi-line edit with awkward quoting.
