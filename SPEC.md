# Mecha Trader — Alpha 1 specification

Precise mechanical reference. `README.md` explains *why*; this file is the *what*.

## Units and conventions

| Thing | Unit |
|---|---|
| Money | credits (`cr`), integer — all transactions round once, at settlement |
| Distance | kilometres |
| Time | whole days; the day counter is the only clock in the game |
| Cargo | volume units; each good declares `unitVolume` |
| Stock | continuous (double), floored at `minStock` |

Money is integer so it never drifts; stock is continuous so prices move smoothly.

## Simulation loop

State changes only through `CommandProcessor.Execute(state, world, command)`. Commands
validate fully before mutating, so a rejected command leaves state byte-identical.

| Command | Legal when | Effect |
|---|---|---|
| `Buy(goodId, units)` | parked | pays exact quote, drains local stock, adds to hold |
| `Sell(goodId, units)` | parked, enough held | receives exact quote, refills local stock |
| `Depart(toCityId)` | parked, road exists | begins travel; `locationId` becomes null |
| `Wait(days)` | always, `1 ≤ days ≤ 365` | advances the clock |
| `BuyTruck(truckTypeId)` | parked, affordable | adds a truck to the convoy |

`Wait` is the only way time passes — including while travelling. One clock, one path.

### One day, in order

1. Charge `Σ truck.upkeepPerDay`, plus `travel.fuelPerDay` if on the road.
2. Tick every city's every good (below).
3. Increment the day counter.
4. If travelling, decrement `daysRemaining`; on zero, arrive and emit an event.
5. Update the solvency flag.

## Price model

```
effectiveStock = max(stock, minStock)
multiplier     = clamp((equilibrium / effectiveStock) ^ elasticity, minPriceMult, maxPriceMult)
price          = basePrice × priceModifier × multiplier

buyPrice  = price × (1 + spread)
sellPrice = price × (1 − spread)
```

**Orders are priced against the depth they consume.** `QuoteBuy` walks unit by unit,
lowering stock as it goes; `QuoteSell` walks upward. A large order therefore moves the
price against you, and buying 200 units costs strictly more than 200× the first unit.

Planning code (AI, UI estimates) uses `ApproximateBuyCost` / `ApproximateSellRevenue`,
an 8-step midpoint rule that tracks the exact quote within 3% at any order size.
Settlement always uses the exact walk.

### Daily stock tick

```
next = stock + production − consumption
next = next + (equilibrium − next) × driftRate     // trade with the world outside the map
next = next × (1 + noise)                          // seeded, ±noiseSigma
stock = max(next, minStock)
```

Setting the day-over-day change to zero gives the closed form:

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
days     = max(1, ceil(distanceKm / (convoySpeed × terrain.speedMultiplier)))
fuel     = distanceKm × Σ truck.fuelPerKm × terrain.costMultiplier
```

Convoy speed is the **slowest** truck's speed. Fuel is spread evenly across the days of
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

## Content schema

`WorldLoader.Load` takes a `Dictionary<string, string>` of file key → JSON text. It never
touches a filesystem; `MechaTrader.Content` does that, and Godot will do it from `res://`.

Validation is fail-fast at load: unknown good, industry, terrain or city references;
duplicate ids; self-looping or zero-length routes; a start city that does not exist; and
**any city unreachable from the start** are all hard errors.

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

Change any of these and run `dotnet run --project tools/MechaTrader.BalanceSim`. It
prints world flow, per-good price bands, the six best one-hop runs, and the
skilled-versus-careless margin, then exits non-zero if the economy stopped working.
