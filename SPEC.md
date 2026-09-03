# Mecha Trader — Alpha 1 specification

Precise mechanical reference. `README.md` explains *why*; this file is the *what*.

## Units and conventions

| Thing | Unit |
|---|---|
| Money | credits (`cr`), integer — all transactions round once, at settlement |
| Distance | kilometres |
| Time | whole days; the day counter is the only clock in the game |
| Cargo | volume units; each good declares `unitVolume` |
| Stock | continuous (double), held in two stores per city, shelf floored at `minStock` |

Money is integer so it never drifts; stock is continuous so prices move smoothly.

## Simulation loop

State changes only through `CommandProcessor.Execute(state, world, command)`. Commands
validate fully before mutating, so a rejected command leaves state byte-identical.

| Command | Legal when | Effect |
|---|---|---|
| `Buy(goodId, units)` | parked, shelf holds it | pays exact quote, drains the shelf, adds to hold |
| `Sell(goodId, units)` | parked, enough held | receives exact quote, fills the city's intake |
| `Depart(toId)` | parked | begins travel to a city, mining claim, or map cell |
| `Wait(days)` | always, `1 ≤ days ≤ 365` | advances the clock |
| `BuyTruck(truckTypeId)` | parked in a city, affordable | adds a truck or machine to the convoy |
| `BuyGear(gearId)` | parked in a city, affordable, hold space | adds a tool; occupies hold volume |
| `SellTruck(truckId)` | parked, not the last vehicle, convoy still land-capable, hold still fits | removes the vehicle; pays resale of it and its fittings |
| `UpgradeTruck(truckId, upgradeId)` | parked, fitting suits the kind, not already fitted, affordable | fits it; `CaravanMath.Spec` reads it |
| `AcceptContract(id)` | parked in the issuing city, offer on this round's board, not held or closed | stores the acceptance with a deadline |
| `DeliverContract(id)` | parked in the issuing city, every line aboard at or above grade | removes units, pays reward, grants traders standing |
| `ExpoRegister` | parked, expo open here, no pass yet, affordable | buys the pass for this expo |
| `ExpoList(goodId, price)` | parked, pass held, good in hold, theme admits it, city does not make it; 0 always allowed | sets or clears the ask |
| `HireCrew(candidateId)` | parked, seat free, fee affordable | signs a hand off the local board |
| `DismissCrew(crewId)` | parked, severance affordable | pays a hand off; the wage stops today |
| `AssignCrew(crewId, postId)` | hand on the payroll, post exists (empty = none), not already there; works on the road | moves the hand between posts; free |
| `CityFavor(actionId)` | parked, action exists, affordable | courts the local governor; see Standing |
| `RentWarehouse()` | parked, not already renting here | pays the authored fee, opens a storeroom |
| `WarehouseDeposit(goodId, units)` | parked, renting here, hold has it | moves cargo into the room, quality preserved |
| `WarehouseWithdraw(goodId, units)` | parked, room has it, hold space | moves cargo back into the hold |
| `SetWarehouseSell(goodId, price)` | parked, renting here | auto-sell at or above this ask; 0 clears |
| `SetWarehouseProcure(goodId, price)` | parked, renting here | auto-buy at or below this bid; 0 clears |

`Wait` is the only way time passes — including while travelling. One clock, one path.

### Automated traders

Three policies talk to the game only through `Apply` / `View`. None of them owns a rule.

| Policy | What it does |
|---|---|
| `GreedyTrader` | Un-crewed haulage. The skill-expression baseline: playing well must beat playing badly. |
| `RandomTrader` | Control. If this makes money, the economy is a printer. |
| `HouseTrader` | Same haulage, plus hire / one extra mule / donate. Headless play-tester; seed of a later rival house. Live rivals (N convoys on one clock) are not in the world yet. |

`BotRunner` plays a policy against a fresh `Game` for N days and records how the run went. `TradeScout` is the shared one-hop planner. The balance harness writes the numbers into `FIGURES.md`. How to teach the brain a new feature, and how the same policy is meant to drive rival factions and player auto-caravans, is `BRAIN.md`.

### One day, in order

1. Charge `Σ truck.upkeepPerDay × runningCostMultiplier + Σ crew.dailyWage`, plus
   `travel.fuelPerDay` if on the road.
2. Tick every city's every good: eat, produce, shelve part of the intake, settle
   toward equilibrium, apply noise (below).
3. Increment the day counter.
4. Expire world events whose last day has passed; maybe fire a new one.
5. If travelling, decrement `daysRemaining`; on zero, arrive and emit an event.
6. Update the solvency flag.

## Two stores per city

A city holds each good in two places, and both together are what it owns:

| Store | What it is | Who touches it |
|---|---|---|
| `Out` — the shelf | what the city has for sale | a buy drains it; production, restocking and outside-world trade fill it |
| `In` — the intake | what caravans have unloaded here | a sell fills it; the city eats it first and shelves the rest over days |

**What you sell does not go where what you buy comes from.** That is the whole point.
Unloading a hold cannot cheapen the shelf, so there is no sell-then-buy-back loop to
find — not a loop that loses money to the spread, but no loop at all. Goods you sold
reach the shelf only after the city has eaten what it needed and shelved the rest, at
`restockRate` per day.

## Price model

```
effectiveStock = max(stock, minStock)
multiplier     = clamp((equilibrium / effectiveStock) ^ elasticity, minPriceMult, maxPriceMult)
price          = basePrice × priceModifier × eventMult × multiplier

buyPrice  = price(shelf)          × (1 + spread × buySpreadShare)
sellPrice = price(shelf + intake) × (1 − spread × sellSpreadShare)
```

The two sides read different stores. You are quoted to **buy** against the shelf, because
that is what is actually for sale. You are quoted to **sell** against everything the city
owns, because a city that has just taken three hundred units off another caravan will not
pay well for more, shelved or not.

That asymmetry is load-bearing. Price falls as stock rises and the total is never below
the shelf, so `sellPrice ≤ buyPrice` at every possible holding — an in-place round trip
can never pay, whatever the tuning.

`buySpreadShare` and `sellSpreadShare` are the crew's doing and default to 1 (the
market's full cut). Both are clamped to [0, 1], a second and independent guard on the
same property. Category knowledge and bargain traits erode a further slice of that
spread, still clamped. See **Crew** below.

### Tiers

Every good names a **tier**, 1 to 5, declared under `tiers` in `goods.json`. A tier
carries a display colour, `minStanding` (the total standing across every segment a city
wants before it sells that grade to you), `minPricePerVolume` and `equilibriumScale`.

```
locked(good, city) = standing.total(city) < tier(good).minStanding     ← Buy rejects; Sell does not
equilibrium        = max(minEquilibrium × tier.equilibriumScale, equilibriumDays × (P + C))
```

The loader enforces the value rule: every good's `basePrice / unitVolume` must sit at or
above its tier's floor and below the next tier's, so a higher grade is always denser
value per unit of hold. `TradeScout`, the road estimates and the harness never plan a
locked grade.

### Quality and S-tier

Every shelf carries an **average grade** (0–100). What a city makes today grades

```
made = clamp(quality.base + roll × quality.random + craft/100 × quality.cityVitalWeight, 0, 100)
```

where `roll` is uniform [0, 1) drawn from the day's RNG per city per good in content
order, and `craft` is the city's live `quality.cityVitalId` vital (Workmanship). A new
world opens every shelf at the roll's midpoint, so day 1 is already graded the way that
city grades. Knowledge never rewrites that average. `quality.nominal` (70) is the grade
that sells at 1.0x.

```
saleable = floor(shelf − minStock)
if take >= saleable: selected = average          ← buying the whole pile
else:                selected = lerp(average, top-k of a uniform pile, knowledge)
```

`knowledge` is the best eye on the roster for that good's category, as a 0–1 factor,
plus any product-trait quality bonus, still clamped to 1. A small order with a high
eye takes the top of the pile; the remaining shelf's average falls by conservation of
quality-mass. Buying 200 of 200 at 72% still yields 72%, even with a perfect eye.

S-tier is a read of selected quality, not a stored flag: at or above `sTierAt` (90)
the lot sells at `1 + sTierSellBonus` (+30%). Below that the sell multiplier is 1.0
at nominal and rises linearly toward S-tier.

**The shop charges for the grade you pick.** A buy settles at `QuoteBuy × sellMult(selected)`,
where `sellMult` is the same multiplier a sale uses. A finer crate is worth more
everywhere and free nowhere: with the multiplier on both ends, cherry-picking cannot
turn a shelf into an in-place income, and `sell ≤ buy` survives grade. Storeroom
auto-procure pays the multiplier of the average crate; planners size orders against the
best single crate so a buy is never rejected for cash.

**Prices move at the day tick, never inside a deal.** A `CityStock` carries the shelf
and intake as the day opened (`OpenOut`, `OpenIn`); every quote reads those. An order
settles at one price for the whole lot: 200 units cost exactly 200× the unit price the
board shows, and the second order today pays what the first did. Bulk is never
penalised. What a deal does is move `Out` / `In`, so the tick folds the day's trades
into tomorrow's opening figures and tomorrow's shelf is scarcer or fuller. A stock
shock reprices at once (the news and the price break the same morning).

`ApproximateBuyCost` / `ApproximateSellRevenue` are therefore exact (unit price ×
units) and exist so planners and settlement read one rule.

### Daily stock tick, per city per good

```
eaten    = min(intake, consumption)                  ← the city eats what it just bought first
intake  -= eaten
shelf   += production − (consumption − eaten)

shelved  = intake × restockRate                      ← the lag that kills the buy-back loop
intake  -= shelved
shelf   += shelved

shelf   += (equilibrium − (shelf + intake)) × driftRate
shelf   *= 1 + noise
shelf    = max(minStock, shelf)
```

With an empty intake this is exactly the pre-split single-pool tick and draws the same
one random number, so a world nobody has traded in behaves and replays identically —
`AnUntradedCityTicksExactlyAsItDidWithOneStore` checks that against a copy of the old
formula.

Setting the day-over-day change of an untraded city to zero gives the closed form:

```
steadyState = equilibrium + (production − consumption) / driftRate
```

A producer settles **above** equilibrium and is cheap; a consumer settles **below** and
is dear. This is the entire trade map, and it is a consequence of the formula rather
than an authored table. `Game.New` seeds every market at its steady state so day 1
already has real gradients.

Two constants control the two properties that matter, and they are independent:

- `equilibriumDays` → **market depth**: how large an order a city absorbs before the
  price moves. Raise it to make big hauls viable.
- `driftRate` → **price gradient**: how far producers and consumers diverge. *Lower* it
  to widen margins.

## Relationship

Standing with a city is **segments**, declared under `segments` in `standing.json`:
governor, citizens, traders, and one held back. Each is live state, 0 to `segmentMax`.

```
total(city)          = Σ segment values                          ← derived, never stored
rank / reserve / permits / tier locks read total
donate, invest       → governor           (action.segmentId)
aid                  → citizens
shortage relief      → citizens: reliefStanding × units / reliefUnits per running shortage
volume               → traders: tradersPerThousandCr × credits sold into the city
contract delivered   → traders: kind.standing
contract lapsed      → traders: −contractLapsePenalty
```

`Standing.Grant` is the one write; it clamps to the segment ceiling. Permits are checked
against the total after any grant, whichever segment moved it.

## Station

A convoy's trucks are **instances** (`TruckState`: id, type, fitted upgrade ids), so a
fitting sits on one vehicle and not the next. `CaravanMath.Spec` resolves a vehicle:

```
capacity = type.capacity + Σ capacityBonus
speed    = type.speed × Π speedMult
fuel     = type.fuelPerKm × Π fuelMult
upkeep   = type.upkeep + Σ upkeepDelta
mine     = type.mineYield + Σ mineYieldBonus
resale   = (type.price + Σ upgrade.price) × trucks.resaleFraction
```

`UpgradeTruck` fits one of each per vehicle and only kinds the fitting names. `SellTruck`
refuses the last vehicle, a sale that would leave nothing land-capable, and a sale that
would leave the hold larger than the convoy.

## Contracts

A city's board is `Contracts.BoardFor(world, city, seed, day)`: `offersPerCity` offers,
re-rolled every `refreshDays`, each a pure function of `(seed, city, round, index)`.
Only acceptances are state (`ContractState`: id, city, accepted day, deadline).

```
wanted(city)  = goods the city does not produce, weighted by tierWeights
kind          = weighted pick of contracts.kinds
lines         = kind.goods distinct goods, each unitsMin..unitsMax
value         = Σ restingMid(city, good) × units          ← content-derived, never state
reward        = value × rewardMult          (or value × priceMult for a supply order)
deadline      = accept day + deadlineDaysMin..deadlineDaysMax
```

`DeliverContract` needs the convoy parked in the issuing city with every line aboard in
full at or above `minGrade`; it removes the units, pays the reward and grants traders
standing. The day tick tears up anything past its deadline and charges
`contractLapsePenalty` to traders standing.

## Expos

Every city runs its own expo. The calendar is derived: city `c` has a stable offset in
`[0, cycleDays)`, round `r` opens on `r × cycleDays + offset + 1` for the theme's
`durationDays`, and the theme is a weighted pick hashed from `(seed, city, round)`.
State is the pass (`"cityId:round"`), the asks on the stall, and the last day's report.

```
buff        = buffMax + (buffMin − buffMax) × (categories − 2) / 3       ← 2 categories buff hardest
fee         = feeBase + feePerPop × population
buyers/day  = round((buyersBase + buyersPerPop × population) × (1 + buff))
willingness = basePrice × (1 + buff × premiumMult) × sellMult(lot grade) × (1 ± noise)
```

Each buyer picks one theme category, one listed good in it, and buys `1..lotMax` units
if `ask ≤ willingness`; within `closeBand` above it they say so. Buyers anchor on
**base price**, not the local shelf, because they come from across the map. A city's
own produce is never allowed on a stall in its own expo. The stall trades on the day
tick and only draws the RNG when something is listed; leaving town clears the stall.
The hall on the Expo tab replays `LastExpoDay`; it decides nothing.

## Travel

The map is a terrain grid (`data/map.json`) with three stacked layers. A convoy uses a
layer only when **every** vehicle on it has that capability. Trucks and the Digger have
`land` only in this pass; air and water pathfinding is in, waiting on a vehicle that
declares the capability.

```
cell.land  = hasRoad OR biome is plain/hill/forest/swamp/desert/tundra/mountain
cell.water = biome is water or deep
cell.air   = biome is not deep
```

Mountain is passable off-road but slow (`map.offRoad.mountain`, ~0.4× speed and a fuel
tariff), so alpine passes are still the fast corridors through a range. Open water and
deep block land unless an authored road punches a corridor through (the Channel ferry,
straits). Underwater (`deep`) cells block air.

Actual travel is A* on a sub-cell lattice — each 50 km cell split into 4×4 sub-cells
(`WorldMap.SubDiv`) — so routes can bend at 12.5 km resolution instead of jumping cell
centre to cell centre. The node path is then string-pulled: a chord is kept only while
every sampled sub-cell on it is walkable *and* its terrain-weighted time does not
exceed the sub-path it replaces, so the smoother cuts stair-step corners but never
trades a fast road for a slow off-road short-cut. The smoothed polyline is resampled at
~10 km steps; distance, days and fuel are integrated over that final polyline, so the
drawn route and the arrival day are one story. Off-road cells use `map.offRoad`
speed/cost; road overlay cells use the route's terrain multipliers, which is why the
authored graph stays the fast land network rather than the only legal moves.

```
convoySpeed = min(truck.speedKmPerDay) × speedMultiplier
edgeTime    = distanceKm / (convoySpeed × cell.speedMultiplier)
days        = max(1, ceil(Σ edgeTime))
fuel        = Σ distanceKm × Σ truck.fuelPerKm × cell.costMultiplier × runningCostMultiplier
```

`Depart` sets `locationId`, `siteId` and `cellId` to null and stores the smoothed
waypoint polyline. A map click arrives as a sub-cell id (`s<sc>,<sr>`) and may snap to
the nearest walkable ground; a named cell, city or claim must be reached as-is.
`Wait` is still the only clock. Arrival writes the matching id back (city, claim, or
open-country cell).

Authored route distances (lon/lat × `roadDetourFactor`) remain the road-graph length
used by the one-hop planner; the player walks the grid.

## Mining

Deposits are generated at `Game.New` from the run seed (a dedicated RNG, not
`RngState`) onto hill or mountain-adjacent land cells that are not cities. They are
stored in `GameState.MiningSites` because they deplete.

Parked on a claim, each `Wait` day extracts:

```
yield = min(siteRemaining, floor(freeHold / ore.volume), Σ mineYield of gear + machines)
```

No mining gear and no mining machine → extract 0. Ore lands in the hold at cost 0.
Played-out claims stay on the map.

## Gear and machines

`trucks.json` entries may declare `kind` (`truck` | `machine`), `capabilities`, and
`mineYield`. Empty capabilities mean `land`. The Digger is a slow machine with `mine`.

`gear.json` is portable tools. `BuyGear` in a city; gear occupies hold volume.
`CanMine` if the summed `mineYield` is positive.

## Determinism

`Rng` is xorshift64\*, and its entire state is one `ulong` living in `GameState`. There
is no static randomness and no wall-clock access anywhere in `MechaTrader.Core` — both
are enforced by `ArchitectureTests`. Consequences:

- seed + command list ⇒ identical state, always;
- `GameState` round-trips through `System.Text.Json` with no custom converters;
- the balance harness, replays and save/load all work without extra machinery.

The day tick iterates cities and goods in content load order, so the random sequence is
stable across runs.

## Crew

Crew change the *terms* the convoy trades and travels on. They never change what a city
produces, what it holds, or what the mid price is.

### Skills, levers and effect

A skill declares which **lever** it pulls, and `maxEffect` is what that lever gives at
`maxSkill`, scaling linearly below it. The simulation reads the lever, never the skill
id, so a skill can be renamed, retuned or added in `crew.json` alone. A skill on lever
`none` is carried, paid for and displayed but does nothing yet — that is how a stat ships
before the system behind it exists.

```
level(skill)  = max over the roster of member.skills[skill]        ← the best hand leads
factor        = clamp(level / maxSkill, 0, 1)
effect        = skill.maxEffect × factor
```

| Lever | Shipping skill | `maxEffect` | What `effect` does |
|---|---|---|---|
| `speed` | Navigation | 0.35 | `speedMultiplier = 1 + effect` |
| `buy` | Negotiation | 0.80 | `buySpreadShare = 1 − effect − knowledgeBargain − bargain traits` |
| `sell` | Sales | 0.80 | `sellSpreadShare = 1 − effect − knowledgeBargain − bargain traits` |
| `upkeep` | Accounting | 0.40 | `runningCostMultiplier = 1 − effect − best repair trait`, on truck upkeep and fuel |
| `intel` | Intelligence | 1.00 | price reports from nearby cities: reach and accuracy, see **Posts** below |
| `none` | — | — | nothing, deliberately |

### Posts

A post is a job somebody has to be put on (`crew.json` `posts`). Each post **claims**
levers. A claimed lever is pulled only by the hands on that post; a lever nobody claims
is convoy-wide. Shipping content:

| Post | Claims | So |
|---|---|---|
| Trading | `buy`, `sell` | only a hand at the counter haggles or closes; their category knowledge, bargain traits and cherry-picking eye count, nobody else's |
| Information | `intel` | only a hand on it reads other markets |
| (none) | — | `speed` and `upkeep` are unclaimed: everyone aboard reads the road and runs the books |

```
onPost(lever)  = roster, if no post claims the lever
               = hands whose postId == the claiming post, otherwise
level(skill)   = max over onPost(skill.lever)                 ← the best eligible hand leads
```

A hand signs on to `role.post`, else the post claiming their primary skill's lever,
else none (a broker goes to the counter, a scout to information, a navigator nowhere).
`AssignCrew` moves them afterwards. `CrewMember.PostId` is state and rides in the save.

### The information post — price reports

The post reports what each of the nearest cities pays for every good. Reports are
**derived, never stored**, and never touch `GameState.RngState`.

```
factor  = level(intelligence) / maxSkill            (0 if nobody is on the post)
reach   = 0                                          if nobody is on the post
        = minCities + round((maxCities − minCities) × factor)
error   = maxError × (1 − factor)                    worst-case relative miss, either way
noise   = hash(seed, city, good, day, side) → [−1, 1]
reported buy  = trueBuy  × (1 + error × noise₁)
reported sell = trueSell × (1 + error × noise₂)
```

Coverage is the `reach` nearest cities by shortest road distance (Dijkstra over
`routes.json`); the days shown are the sum of the convoy's own days per leg. The true
prices are `Economy.BuyUnitPrice` / `SellUnitPrice` at that city's current stock with
the convoy's own terms and any event multiplier, so a report is "what you would get",
not the market's mid. The noise is fixed for the day and independent per side, so a
dull informant is wrong in no tidy direction and tomorrow's report differs from today's.
With `crew.intel` at `2 / 8 / 0.4`: a level-1 scout reads 3 cities within ±36%; a
level-10 one reads 8 within ±0%.

Category knowledge is per person, per category, 0–`maxKnowledge`. The best eye on the
roster leads, the same rule as skills. It does two jobs: it is a share of the spread
still paid (`knowledgeBargain` at max), and it is the factor that cherry-picks grade.
A buy or sell grants a little XP in that category (and in negotiation or sales) to
everyone aboard; product traits learn that category faster.

Special traits are content (`crew.json` `traits`). Kinds: `product` (a category, or
any), `traveling` (speed), `repair` (upkeep), `bargain` (buy or sell). The best trait
of a kind leads. A candidate has a `traitChance` of walking in with one.

Specialist roles name a `categoryId` and roll high knowledge there.

```
dailyWage = wage.base
          + wage.perSkillPoint × Σ skill levels
          + wage.perKnowledgeTen × Σ floor(knowledge / 10)
          + Σ trait.wagePoints
```

### Storerooms

A rented room is per-city state. Capacity and rent are content (`config.warehouse`).
Auto-sell / auto-procure prices of 0 are off. The room ticks on `Wait` at **market**
terms: no crew eye, no bargain. Deposit and withdraw preserve quality.

A skill is led by the **best single hand**, not the sum: three mediocre drivers are not
one good one. Wages are the counterweight, and they are never discounted by the
accounting lever — nobody takes a cut of their own pay.

### Wages, fees and severance

```
signingFee  = dailyWage × signingFeeDays
severance   = dailyWage × severanceDays
```

The wage is frozen at hire and stored in the save, so retuning content later cannot
silently rewrite a contract already signed.

### Recruitment centres

Every city runs one. A pool is a **pure function** of `(seed, cityId, hiringRound)` and
is never stored:

```
hiringRound = (day − 1) / refreshDays
poolSize    = clamp(basePerCity + round(population × perPopulation), 1, maxPerCity)
```

The view derives the pool to draw the board and `CommandProcessor` derives the same list
again to validate a hire, so the two cannot disagree. It does **not** draw from
`GameState.RngState` — building a view must not advance the world, or looking at a screen
would change the game. The city id is folded in with FNV-1a rather than
`string.GetHashCode`, which is randomised per process.

A candidate rolls their role's `primary` skill in `[primaryMin, primaryMax]` and the rest
in `[secondaryMin, secondaryMax]`; a role with no primary splits the difference on
everything and peaks at nothing. The city's industries then add `industryAffinity`
bonuses, so a trade hub grows brokers and a plant town grows bookkeepers.

`GameState.RecruitedIds` records who has taken a contract. It is the only trace of
hiring that is stored, and it also stops a dismissed hand reappearing on the board.

## City stats

A city carries two kinds of stat and they behave differently on purpose.

### Vitals — authored, then carried live

A **vital** is authored per city in `cities.json` under `stats`, keyed by the ids
`citystats.json` declares. At `Game.New` every city's founding block is copied into
`GameState.CityVitals`. From that moment content is only the starting point: reads go
through state, and content is the fallback for a save that has never heard of a vital.

Nothing in the simulation moved a vital until standing landed. Invest and aid write
growth (clamped to the vital's range) through `GameState.SetVital`. Population still
scales industry at load time from the founding value; live growth is on the page and in
the save, and is the first vital the player can actually push.

| Vital | Range | Shown as | Wired to |
|---|---|---|---|
| `population` | 0.1 – 2.5 | millions, `×4.0` | scales every industry's output and appetite |
| `peace` | 0 – 100 | percent | nothing yet |
| `growth` | −10 – 10 | signed percent per year | raised by invest and aid; not yet read by the economy |

`population` is the one vital the economy already reads, which is why it lives in the
stat block rather than beside it: a city has one size, and market generation and the
city page read the same number.

A vital declares its own presentation, so adding one is a data change:

```
id  name  unit  default  min  max  decimals  displayScale  blurb  bands[]
```

`displayScale` multiplies the raw value for display only — it is how population can be
an industry scale of 1.5 to the simulation and "6.0M" to the player. A vital whose `min`
is negative is shown signed, because for those the direction is the message.

### Supply figures — derived, never authored

A **supply** figure is not stored anywhere. It reads a slice of the city's own market and
states what the city is holding as a percentage of what it would hold if no convoy had
ever called:

```
nominal = Σ basePrice × max(steadyStateStock, minStock)     over the band's goods
held    = Σ basePrice × (shelf + intake)
index   = 100 × held / nominal
```

Weighting by base price rather than counting units is what stops a heap of twelve-credit
scrap papering over a ninety-five-credit plate shortage. Anchoring on the city's *own*
steady state is what makes the number comparable between a mining town and a trade hub: a
city that structurally imports a good still reads 100 when nothing is wrong.

So supply already breathes without a single event: it moves with the daily tick, and it
moves when the player trades. A full haul out of a city visibly drains the band it came
from; unloading one fills it, intake included.

| Supply | Reads | Bands |
|---|---|---|
| `power` | Power Cells | <55 Critical, <85 Strained, <115 Steady, else Surplus |
| `basic` | Rations | same |
| `industrial` | Scrap, Ore, Steel, Ceramics | same |
| `luxury` | Servos, Optics | same |

Alongside the index each band reports production, consumption, net flow per day, total
stock, and days of cover (null where the city consumes none of it).

### Bands

Both kinds of stat use the same band shape: an ascending list where each entry names its
exclusive upper bound and the last is open-ended. A value takes the first band it falls
under. Each band also declares a `tone` — `bad`, `warn`, `ok`, `good`, `muted` — so what
a number *means* is content and the front-end only decides what a tone looks like.

## Standing

How the player relates to a city, as opposed to what the city is. Content lives in
`standing.json`; live standing is per-city state, starting at zero. Rank, the reserved
shelf share and which permits are *due* are derived from standing on demand. Permits
once granted are sticky and stored as ids.

```
reservedRatio = clamp(standing × reservePerPoint, 0, reserveMax)
reservedUnits = floor(shelfUnits × reservedRatio)
publicUnits   = shelfUnits − reservedUnits
```

The player can still buy the whole shelf — that is the privilege. Other caravans only
see `publicUnits`. A reserved share is never stored: it is a read of standing.

| Rank | Standing | Tone |
|---|---|---|
| Stranger | < 20 | muted |
| Known | < 40 | ok |
| Favored | < 70 | good |
| Patron | 70+ | good |

| Permit | Standing | What it is |
|---|---|---|
| Shop | 40 | the governor will let you set up a shop here |
| Factory | 70 | the governor will let you set up a factory here |

Building the shop or factory is a later act. Holding the paper is the grant.

`CityFavor(actionId)` looks the action up from content. Adding a fourth gesture is a
JSON line, not a new command.

| Action | Cost | Standing | Other |
|---|---|---|---|
| `donate` | 2,500 cr | +8 | goodwill only |
| `invest` | 12,000 cr | +6 | `growth` +0.3 |
| `aid` | 6,000 cr | +5 | `growth` +0.1; +20 intake on each good in the city's shortest supply |

Aid lands in intake, not on the shelf, so it cannot cheapen a buy. Standing is clamped
to `[0, max]`. A donate that would raise nothing, and moves neither a vital nor stock,
is refused.

Each city authors a `governor` and optional `governorTitle` (default "Governor"). A city
that omits the name gets a stable pick from the crew name pools.

## World events

Templates live in `events.json`. Live instances live in `GameState.ActiveEvents`. Price
multipliers and vital overlays are derived from the active set, so they vanish when the
instance expires. A stock shock is the exception: it writes the shelf (or intake) once
on fire.

Each day, after the day counter advances:

```
if active.Count ≥ maxConcurrent: stop
if rng() ≥ dailyChance: stop
pick a weighted template that still has a legal target
pick a city from that template's filter (skip if global)
apply the stock shock, push the instance, emit a dispatch
```

An instance started on day D with `durationDays` N is active while `day < D + N`.

```
eventMult(city, good) = Π priceMult of matching active templates     (1 if none)
liveVital             = clamp(storedVital + Σ vitalDelta, min, max)
```

A template matches a city if it is `global` or its instance named that city. It matches
a good if its `goods` list is empty or contains that good. Filters (`industries`,
`regions`, `cities`) only constrain *where it can fire*, not how it reads afterwards.

`eventMult` is applied outside the stock-ratio clamp and equally to both sides of the
quote, so `sell ≤ buy` still holds by construction. Invest and aid write the stored
vital; the city page reads the overlaid one.

Adding a template is a JSON object. The loader rejects an unknown good, industry, city
or vital, a non-positive duration/weight/priceMult/stockMult, and a template that
moves nothing.

## Content schema

`WorldLoader.Load` takes a `Dictionary<string, string>` of file key → JSON text. It never
touches a filesystem; `MechaTrader.Content` does that, and Godot will do it from `res://`.

Validation is fail-fast at load: unknown good, industry, terrain or city references;
duplicate ids; self-looping or zero-length routes; a start city that does not exist; and
**any city unreachable from the start** are all hard errors. `map.json` adds a terrain
grid (unknown biome, empty mining reserve range, mining good that does not exist).
`gear.json` and `trucks.json` `kind`/`mineYield` must not be negative; kind is `truck`
or `machine`. Crew content adds four more:
an unknown lever, two skills claiming one lever, a role specialising in a skill that does
not exist, and an affinity entry naming an unknown industry or skill. City stats add five
more: a city setting a stat the catalogue does not declare, a founding value outside the
range its vital declares, a supply band reading an unknown good or no goods at all, a
`populationVitalId` naming no declared vital, and a band list that is not ascending or
whose last entry is not open-ended. Standing adds four more: `max` not positive, a
permit whose threshold sits outside 0–max, a favor action that costs a negative amount
or moves a vital the catalogue does not declare, and an empty actions list. Events add
four more: `dailyChance` outside 0–1, an unknown good/industry/city/vital on a template,
a non-positive duration, weight, `priceMult` or `stockMult`, and a template that has
no effect.

### City markets are generated, not authored

A city declares its `stats` block, a list of `industries`, and a `governor`. For each
good, with `population` read out of that block:

```
production  = Σ industry.production[good] × population
consumption = Σ industry.consumption[good] × population + baseConsumptionPerPop[good] × population
equilibrium = max(minEquilibrium, equilibriumDays × (production + consumption))
```

So adding a city is six lines of data, and rebalancing an industry re-derives every city
that uses it.

## Tuning constants

All in `data/config.json` under `economy`.

| Key | Value | Controls |
|---|---|---|
| `driftRate` | 0.08 | price gradient between producers and consumers |
| `equilibriumDays` | 30 | market depth |
| `minEquilibrium` | 150 | floor for goods a city barely touches |
| `minStock` | 5 | prevents divide-by-zero and infinite prices |
| `noiseSigma` | 0.02 | day-to-day jitter |
| `spread` | 0.045 | buy/sell margin; makes in-place round trips a loss |
| `minPriceMult` / `maxPriceMult` | 0.4 / 2.5 | hard price band |
| `roadDetourFactor` | 1.25 | straight-line distance → road distance |
| `restockRate` | 0.35 | share of a city's intake that reaches its shelf each day |

Crew constants live in `data/crew.json`.

| Key | Value | Controls |
|---|---|---|
| `maxSkill` | 10 | the ceiling a level is measured against |
| `crewCapacity` | 4 | seats on the convoy |
| `refreshDays` | 10 | how often every city's hiring board re-rolls |
| `signingFeeDays` / `severanceDays` | 20 / 5 | hiring and firing costs, in days of wage |
| `wage.base` / `wage.perSkillPoint` | 5 / 6 | what a given set of skills costs per day |
| `skills[].maxEffect` | see above | how hard each lever pulls at `maxSkill` |
| `posts[].levers` | trading: buy, sell · information: intel | which levers need a hand posted |
| `intel.minCities` / `maxCities` / `maxError` | 2 / 8 / 0.4 | reach at the bottom and top of intelligence; worst-case error at the bottom |
| `candidates.*` | 1, 2.0, 5 / 5–10 / 1–5 | pool size, and the specialist and secondary bands |

Change any of these and run `dotnet run --project tools/MechaTrader.BalanceSim`. It
rewrites `FIGURES.md` with the resulting numbers. It
prints world flow, per-good price bands, what the recruitment centres are offering, the
six best one-hop runs, the skilled-versus-careless margin, and a HouseTrader playtest,
then exits non-zero if the economy stopped working. It also asserts the no-arbitrage
property above against a perfect crew, in every city, for every good.
