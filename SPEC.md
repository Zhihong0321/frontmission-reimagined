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
| `Depart(toCityId)` | parked, road exists | begins travel; `locationId` becomes null |
| `Wait(days)` | always, `1 ≤ days ≤ 365` | advances the clock |
| `BuyTruck(truckTypeId)` | parked, affordable | adds a truck to the convoy |
| `HireCrew(candidateId)` | parked, seat free, fee affordable | signs a hand off the local board |
| `DismissCrew(crewId)` | parked, severance affordable | pays a hand off; the wage stops today |

`Wait` is the only way time passes — including while travelling. One clock, one path.

### One day, in order

1. Charge `Σ truck.upkeepPerDay × runningCostMultiplier + Σ crew.dailyWage`, plus
   `travel.fuelPerDay` if on the road.
2. Tick every city's every good: eat, produce, shelve part of the intake, settle
   toward equilibrium, apply noise (below).
3. Increment the day counter.
4. If travelling, decrement `daysRemaining`; on zero, arrive and emit an event.
5. Update the solvency flag.

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
price          = basePrice × priceModifier × multiplier

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
same property. See **Crew** below.

**Orders are priced against the depth they consume.** `QuoteBuy` walks unit by unit,
lowering stock as it goes; `QuoteSell` walks upward. A large order therefore moves the
price against you, and buying 200 units costs strictly more than 200× the first unit.

Planning code (AI, UI estimates) uses `ApproximateBuyCost` / `ApproximateSellRevenue`,
an 8-step midpoint rule that tracks the exact quote within 3% at any order size.
Settlement always uses the exact walk.

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

## Travel

```
convoySpeed = min(truck.speedKmPerDay) × speedMultiplier
days        = max(1, ceil(distanceKm / (convoySpeed × terrain.speedMultiplier)))
fuel        = distanceKm × Σ truck.fuelPerKm × terrain.costMultiplier × runningCostMultiplier
```

Convoy speed is the **slowest** truck's speed, then whatever the crew add to it. Because
days are whole, a speed bonus only pays when it crosses a day boundary. Fuel is spread evenly across the days of
the journey and charged daily, alongside upkeep.

Route distances derive from city coordinates: cities carry real lon/lat, projected
equirectangularly around 47.5°N, then scaled by `roadDetourFactor` (1.25) because roads
are not straight lines. A route may override `distanceKm` explicitly.

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
| `buy` | Negotiation | 0.80 | `buySpreadShare = 1 − effect` |
| `sell` | Sales | 0.80 | `sellSpreadShare = 1 − effect` |
| `upkeep` | Accounting | 0.40 | `runningCostMultiplier = 1 − effect`, on truck upkeep and fuel |
| `none` | — | — | nothing, deliberately |

A skill is led by the **best single hand**, not the sum: three mediocre drivers are not
one good one. Wages are the counterweight, and they are never discounted by the
accounting lever — nobody takes a cut of their own pay.

### Wages, fees and severance

```
dailyWage   = wage.base + wage.perSkillPoint × Σ level over all skills
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

## Content schema

`WorldLoader.Load` takes a `Dictionary<string, string>` of file key → JSON text. It never
touches a filesystem; `MechaTrader.Content` does that, and Godot will do it from `res://`.

Validation is fail-fast at load: unknown good, industry, terrain or city references;
duplicate ids; self-looping or zero-length routes; a start city that does not exist; and
**any city unreachable from the start** are all hard errors. Crew content adds four more:
an unknown lever, two skills claiming one lever, a role specialising in a skill that does
not exist, and an affinity entry naming an unknown industry or skill.

### City markets are generated, not authored

A city declares only `population` and a list of `industries`. For each good:

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
| `candidates.*` | 1, 2.0, 5 / 5–10 / 1–5 | pool size, and the specialist and secondary bands |

Change any of these and run `dotnet run --project tools/MechaTrader.BalanceSim`. It
rewrites `FIGURES.md` with the resulting numbers. It
prints world flow, per-good price bands, what the recruitment centres are offering, the
six best one-hop runs, and the skilled-versus-careless margin, then exits non-zero if the
economy stopped working. It also asserts the no-arbitrage property above against a
perfect crew, in every city, for every good.
