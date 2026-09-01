# Current figures

**Generated** by `dotnet run --project tools/MechaTrader.BalanceSim`, which
`check.ps1` runs on every verification. Do not edit by hand - your edit will be
overwritten, and the point of this file is that it cannot go stale.

## World

- 20 cities, 8 goods, 29 roads, 9 industry archetypes
- 3 truck types, 4 crew skills, 5 hiring roles
- Start: Praha, 20,000 cr, Mule-class Hauler
  - Mule-class Hauler: 200 capacity, 220 km/day, 45 cr/day upkeep, 0.85 cr/km fuel

## Opening position

3 profitable opening run(s) on day 1, best cargo priced both legs:

- Praha to Berlin: 226 x Ceramic Plating, +4,832 cr over 3 day(s), 353 km of Highland
- Praha to Munchen: 226 x Ceramic Plating, +4,323 cr over 3 day(s), 380 km of Highland
- Praha to Wien: 169 x Ceramic Plating, +3,104 cr over 2 day(s), 318 km of Highland

## Economy

- 1000-day tick: ~30 ms for 160,000 market updates (budget 500 ms)
- 8 of 8 goods hold a cross-city spread of at least 20%

| good | base | min | max | mean | median cross-city spread |
|---|---|---|---|---|---|
| Scrap Alloy | 12 | 10 | 16 | 14 | 60% |
| Rare Earth Ore | 42 | 33 | 60 | 50 | 73% |
| Rations | 20 | 17 | 27 | 24 | 50% |
| Refined Steel | 58 | 46 | 82 | 69 | 66% |
| Ceramic Plating | 95 | 74 | 137 | 112 | 72% |
| Power Cells | 150 | 122 | 220 | 198 | 65% |
| Actuator Servos | 230 | 175 | 344 | 249 | 75% |
| Optical Sensors | 340 | 257 | 502 | 360 | 78% |

## Skill expression

Over 60 days x 5 seeds on 20,000 starting capital. Neither bot hires crew, so this is the un-crewed baseline.

- Greedy (plays well): 47,404 cr
- Random (plays badly): -16,224 cr
- Edge: 63,627 cr

## Crew

- 4 seats; every city's board re-rolls every 10 days
- 63 candidates across the map per round
- Wages 77-173 cr/day, signing fees 1,540-3,460 cr

| skill | lever | effect at level 10 |
|---|---|---|
| Navigation | `speed` | 35% |
| Negotiation | `buy` | 80% |
| Sales | `sell` | 80% |
| Accounting | `upkeep` | 40% |
