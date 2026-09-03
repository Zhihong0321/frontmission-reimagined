# Current figures

**Generated** by `dotnet run --project tools/MechaTrader.BalanceSim`, which
`check.ps1` runs on every verification. Do not edit by hand - your edit will be
overwritten, and the point of this file is that it cannot go stale.

## World

- 20 cities, 41 goods in 11 categories and 5 tiers, 29 roads, 13 industry archetypes
- 5 truck fittings, 3 contract kinds, 8 expo themes on a 24-day cycle
- Standing: 4 segments of 100; Exotic needs 60, Masterwork needs 150
- 4 truck types, 5 crew skills, 17 hiring roles
- Start: Praha, 20,000 cr, Mule-class Hauler
  - Mule-class Hauler: 200 capacity, 220 km/day, 45 cr/day upkeep, 0.85 cr/km fuel

## Opening position

3 profitable opening run(s) on day 1, best cargo priced both legs:

- Praha to Wien: 139 x Armour Laminate, +11,473 cr over 2 day(s), 262 km of Highland
- Praha to Munchen: 139 x Armour Laminate, +11,453 cr over 2 day(s), 283 km of Highland
- Praha to Berlin: 139 x Armour Laminate, +11,443 cr over 2 day(s), 321 km of Highland

## Economy

- 1000-day tick: ~220 ms for 820,000 market updates (budget 500 ms)
- 41 of 41 goods hold a cross-city spread of at least 20%

| good | base | min | max | mean | median cross-city spread |
|---|---|---|---|---|---|
| Scrap Alloy | 12 | 7 | 16 | 14 | 59% |
| Copper Ore | 28 | 15 | 39 | 31 | 65% |
| Grain | 14 | 12 | 33 | 16 | 49% |
| Silica Sand | 18 | 15 | 24 | 20 | 56% |
| Haulage Fuel | 38 | 31 | 89 | 46 | 55% |
| Rations | 20 | 15 | 48 | 24 | 46% |
| Field Dressings | 30 | 25 | 82 | 35 | 49% |
| Hand Tools | 34 | 29 | 83 | 39 | 53% |
| Rare Earth Ore | 42 | 22 | 60 | 50 | 73% |
| Refined Steel | 58 | 31 | 97 | 71 | 62% |
| Copper Stock | 48 | 26 | 67 | 57 | 59% |
| Ceramic Plating | 95 | 47 | 161 | 113 | 73% |
| Optical Glass | 55 | 43 | 79 | 65 | 72% |
| Polymer Sheet | 70 | 56 | 100 | 82 | 65% |
| Circuit Boards | 90 | 72 | 226 | 105 | 63% |
| Machine Parts | 75 | 34 | 105 | 88 | 62% |
| Antibiotics | 80 | 65 | 220 | 89 | 56% |
| Power Tools | 85 | 69 | 206 | 91 | 60% |
| Preserved Meals | 45 | 39 | 112 | 54 | 46% |
| Power Cells | 150 | 129 | 391 | 201 | 59% |
| Load Bearings | 120 | 91 | 177 | 136 | 74% |
| Actuator Servos | 230 | 175 | 374 | 256 | 78% |
| Optical Sensors | 340 | 256 | 569 | 377 | 82% |
| Armour Laminate | 160 | 124 | 231 | 186 | 72% |
| Titanium Billet | 210 | 103 | 304 | 213 | 71% |
| Control Chips | 260 | 209 | 690 | 301 | 68% |
| Drive Train | 240 | 100 | 345 | 263 | 71% |
| Precision Toolkit | 190 | 149 | 437 | 184 | 45% |
| Heat Shield Tiles | 180 | 87 | 209 | 181 | 39% |
| Capacitor Bank | 220 | 172 | 520 | 239 | 55% |
| Trauma Serum | 150 | 121 | 413 | 162 | 55% |
| Gyro Assembly | 520 | 397 | 786 | 543 | 79% |
| Targeting Array | 700 | 522 | 1,064 | 720 | 82% |
| Nanoweave Plate | 480 | 382 | 719 | 537 | 73% |
| Fusion Cell | 640 | 491 | 1,496 | 689 | 55% |
| Fabricator Rig | 900 | 354 | 1,368 | 951 | 83% |
| Regen Serum | 420 | 325 | 1,198 | 437 | 51% |
| Logic Cores | 580 | 436 | 1,632 | 637 | 84% |
| Neural Core | 1,500 | 1,145 | 4,365 | 1,638 | 86% |
| Mech Frame | 2,400 | 987 | 2,841 | 2,380 | 48% |
| Reactor Core | 1,800 | 1,419 | 4,479 | 1,898 | 42% |

## Naive routes

A plain haul of a city's own surplus to a road neighbour, full purse, no planning,
no crew. The check that keeps the 'sell next door and lose half the purse'
complaint from coming back.

- 417 producer->neighbour runs: 29% lose, median +18.5%
- hauling to a city that does not make the good: 292 runs, 5% lose, median +26.8%
- hauling to a city that makes it too: 125 runs, 83% lose, median -13.5% - the direction mistake
- worst naive loss: -5,275 cr (-26% of the 20,000 cr start purse)

## Skill expression

Over 60 days x 5 seeds on 20,000 starting capital. Neither bot hires crew, so this is the un-crewed baseline.

- Greedy (plays well): 566,917 cr
- Random (plays badly): -13,044 cr
- Edge: 579,961 cr

## Playtest

HouseTrader, same 60 days x 5 seeds on 20,000 starting capital. Haulage plus hire / extra mule / an economy fitting / donate. Contracts and the expo stall are player-only for now (see BRAIN.md). Live rivals are not in this world yet.

- Mean profit: 687,071 cr (best 809,834, worst 569,476)
- Rejection rate: 0%
- Cities visited: 7.6 average; 11 distinct goods traded
- Net worth range: 18,283 – 829,834 cr
- End crew: 4.0; end trucks: 2.0; max standing: 102.9
- World events seen in 5 of 5 seeds; bankruptcies: 5
- Systems touched: crew, trucks, station

Command mix across the seed set:

- `buy`: 133
- `buytruck`: 5
- `depart`: 133
- `hirecrew`: 20
- `sell`: 128
- `upgradetruck`: 10
- `wait`: 133

## Crew

- 4 seats; every city's board re-rolls every 10 days
- 63 candidates across the map per round
- Wages 129-231 cr/day, signing fees 2,580-4,620 cr

| skill | lever | effect at level 10 |
|---|---|---|
| Navigation | `speed` | 35% |
| Negotiation | `buy` | 80% |
| Sales | `sell` | 80% |
| Accounting | `upkeep` | 40% |
| Intelligence | `intel` | 100% |
