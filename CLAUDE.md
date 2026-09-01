# Mecha Trader — project brief

Read this first. It describes what exists, how it is put together, and the rules that
must hold. It deliberately contains **no roadmap**: the user decides what happens next
and will tell you.

## What this is

An overland trading game in the shape of *大航海時代IV*: read a market, plan a route,
haul cargo, live off the margin. Trucks instead of ships, Europe instead of the ocean,
mech-industry commodities instead of spices. **No combat** — deliberately cut.

Target is a Steam release. Stack is .NET 8 with a browser front-end today; the intended
end state is a Godot 4 (C#) client over the same simulation library.

Current state: Alpha 1, playable end to end. ~3,350 lines of C#, ~620 of front-end,
44 tests, all acceptance checks green.

## Run and verify

| Goal | Command |
|---|---|
| Play | double-click `Play.cmd` (builds, serves, opens browser at `localhost:5080`) |
| Verify everything | `.\check.ps1` — four gates, one verdict line, exit code 0 or 1 |
| Economy report | `dotnet run --project tools/MechaTrader.BalanceSim` |
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
check.ps1                  acceptance gate; four checks, exit code is the answer
README.md                  orientation for a human
SPEC.md                    exact formulas, command list, data schema, tuning constants
ACCEPTANCE.md              what each gate asserts and why
NIGHT_LOG.md               build history and the reasoning behind past decisions

data/                      ALL game content. No content is hardcoded in C#.
  config.json              start cash/city/trucks + every economy tuning constant
  goods.json               8 commodities: base price, cargo volume, elasticity
  cities.json              20 cities: real lon/lat, population, industry list
  industries.json          archetypes that generate every city's market
  routes.json              29 road links; distance derives from coordinates
  terrain.json             speed and fuel multipliers per road type
  trucks.json              capacity, speed, upkeep, fuel burn, price

src/MechaTrader.Core/      PURE simulation. No I/O, no console, no clock, no engine.
  Game.cs                  the facade: New / Resume / Apply / View / NetWorth
  Model/Definitions.cs     content DTOs deserialized straight from JSON
  World/WorldLoader.cs     JSON strings -> validated WorldData; generates city markets
  World/WorldData.cs       all resolved content; lookup helpers
  World/City.cs            City, Route, CityGoodProfile (+ SteadyStateStock)
  World/RouteGraph.cs      adjacency, Between, AreAdjacent, Reachable
  State/GameState.cs       everything needed to resume: day, cash, rng, stock, caravan
  Sim/Economy.cs           THE price model. Quotes, approximations, daily stock tick.
  Sim/DayTick.cs           one day: charge costs, tick markets, advance travel, solvency
  Sim/CaravanMath.cs       derived convoy properties (capacity, speed, upkeep, ETA, fuel)
  Sim/Rng.cs               seeded xorshift64*, state lives in GameState
  Commands/Commands.cs     the 5 command records + CommandResult
  Commands/CommandProcessor.cs  the ONLY place state changes
  Events/GameEvent.cs      what the player is told; no display assumptions
  View/ViewModels.cs       front-end DTOs (GameView, MapView, and their parts)
  View/ViewBuilder.cs      state -> display snapshot; also the road scouting estimates
  Ai/TraderPolicies.cs     GreedyTrader (skill baseline) and RandomTrader (control)
  Ai/BotRunner.cs          plays a policy against a fresh game for N days

src/MechaTrader.Content/   the ONLY project that touches the filesystem
  ContentLoader.cs         finds data/, reads files, hands Core plain strings

src/MechaTrader.Host/      thin ASP.NET adapter — no rules live here
  Program.cs               4 endpoints, static file serving, startup banner
  GameSession.cs           holds the one game; parses JSON into Commands; display log

web/                       plain HTML/CSS/JS. No framework, no bundler, no npm.
  index.html               map panel, market panel, roads/hold/depot/log aside
  app.js                   transport + renderers; holds zero game rules
  style.css                tokens at :root, dark UI

tools/MechaTrader.BalanceSim/  headless economy gate; non-zero exit if the game breaks
tests/MechaTrader.Core.Tests/  44 tests incl. ArchitectureTests, which enforce purity
```

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

## How it fits together

```
Command ──> CommandProcessor ──> GameState ──> ViewBuilder ──> GameView ──> JSON ──> browser
                   │                  ▲
                   └── DayTick ───────┘   (markets, costs, travel, solvency)
```

**A day, in order:** charge upkeep + fuel → tick every city's every good → increment day
→ decrement travel and maybe arrive → update solvency flag.

**The price model** (full detail in `SPEC.md`):

```
price = basePrice × clamp((equilibrium / max(stock, minStock)) ^ elasticity, 0.4, 2.5)
buy   = price × (1 + spread)          sell = price × (1 − spread)
```

Orders are priced against the depth they consume — `QuoteBuy`/`QuoteSell` walk unit by
unit, so a large order moves the price against you. Planning code (AI, the road scouting
estimates) uses `ApproximateBuyCost`/`ApproximateSellRevenue`, an 8-step midpoint rule
within 3% of exact. **Settlement always uses the exact walk.**

Daily stock tick settles at `equilibrium + (production − consumption) / driftRate`, so a
city that produces a good sells it cheap and a city that consumes one pays dearly. The
entire trade map falls out of that; no price table is authored anywhere. Two independent
knobs: `equilibriumDays` controls market **depth**, `driftRate` controls price
**gradient** (lower = wider margins).

**Pressure without combat:** truck upkeep plus distance-based fuel. Money leaks daily, so
standing still has a price and a bad route costs real credits.

## Extending things

**A city** — six lines in `cities.json` (`id`, `name`, `region`, `lon`, `lat`,
`population`, `industries`) plus at least one entry in `routes.json`. Its whole 8-good
market is generated from its industry archetypes. The loader rejects any city unreachable
from the start.

**A good** — an entry in `goods.json`, then reference it from at least one industry in
`industries.json`. Every city automatically gets a market row for it.

**An industry** — an entry in `industries.json` with per-good production/consumption at
population 1.0. Every city using it re-derives.

**A command** — add the record to `Commands.cs`, a case to `CommandProcessor.Execute`, a
branch in `GameSession.TryParse`, and a control in `app.js`. Validate before mutating.

**Anything the UI shows** — extend the records in `ViewModels.cs` and populate them in
`ViewBuilder.cs`. The front-end must never compute a game rule; if the browser needs to
know something, derive it in `ViewBuilder`.

After any content or economy change, run the balance harness — it prints world flow,
per-good price bands, the best one-hop runs, and the skilled-vs-careless margin, then
fails if the economy stopped working.

## Current figures

Verified by `check.ps1`:

- 20 cities, 8 goods, 29 roads, 9 industry archetypes
- Start: Praha, 20,000 cr, one Mule-class Hauler (200 capacity, 220 km/day, 45 cr/day)
- Three profitable opening runs — Berlin +4,832, München +4,323, Wien +3,104
- 1000-day tick: ~20 ms for 160,000 market updates (budget 500 ms)
- All 8 goods hold a 50–78% cross-city price spread
- Greedy bot +44,928 cr / random bot −15,805 cr over 60 days on 20,000 capital

That greedy figure means skilled play roughly triples its capital in 60 days — that is
the current balance point, noted so it is not mistaken for a tuned one.

## Environment gotchas

These have each cost time before:

- **A running server locks the build output.** Stop `dotnet` processes before building,
  or MSBuild fails to copy DLLs.
- **The browser caches `app.js` and `style.css` hard.** `web/index.html` carries a `?v=N`
  query on both; bump it when you change either, or you will debug stale code.
- **Windows PowerShell 5.1 `Invoke-WebRequest` needs `-UseBasicParsing`.** Without it, it
  routes through the IE engine and dies in a non-interactive session.
- **`Set-Location` does not change the working directory child processes inherit.** Launch
  scripts by full path; `Play.cmd` resolves its own directory with `%~dp0`.
- **The world loads once, at host startup.** Editing `data/*.json` needs a server restart.
- **`main` is a two-column grid**, so the left-hand stack lives inside `.main-col` — a
  panel added as a direct child of `<main>` will claim a grid cell of its own.
