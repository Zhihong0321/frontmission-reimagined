# Domain glossary

Compact glossary of MechaTrader terms. Where a deeper definition exists, the feature
note is named.

| Term | Meaning |
|---|---|
| Convoy / caravan | The player's trucks + crew + cargo as one traveling unit; derived properties live in `CaravanMath` |
| Shelf (`Out`) / intake (`In`) | A city's two stores of a good: the shelf it sells from, and the intake holding what caravans unloaded; see `CityStock` |
| Spread | The buy/sell margin the market charges; crew share it via `TradeTerms` (a share of the spread still paid, clamped [0,1]) |
| Equilibrium / drift | The market's resting stock and how fast prices widen away from it; the two depth/gradient knobs of the price model |
| Good / category / tier | A commodity (41), its category (11), and its value tier 1-5 (colour, value floor, standing lock) |
| Quality / S-tier | Per-lot production grade drawn at the day tick; S-tier is a derived read of lot quality, sells at a premium |
| Vital | A city stat (population, craft, ...): founding value is content, live value is state; events overlay, never write |
| Supply band | A city's supply reading over named goods; derived from its market, index 100 = undisturbed |
| Standing / segment / rank | Relationship with a city: 4 segments of 100 stored; total, rank, reserved shelf and permit eligibility are derived |
| Permit | A one-way unlock granted at a standing threshold; sticks even if standing drops |
| Favor action | A JSON-defined act toward a city (donate / invest / aid) that grants standing in a named segment |
| World event | A template-driven state of the world: price multiplier, vital overlay, or stock shock; instances are state, effects derived |
| Contract board | A derived per-city list of delivery offers; terms re-resolve from the id on delivery |
| Expo | A derived per-city trade fair on a 24-day cycle: stall fee, buff, buyers; the stall trades on the day tick |
| Storeroom / warehouse | Per-city rented storage with deposit/withdraw and unattended auto-sell/auto-procure prices |
| Mining claim | A generated site with stored reserve; extracting happens on `Wait` while parked, with gear/machines |
| Gear | Portable tools (price, volume, capabilities, mine yield) |
| Truck / fitting / station | Truck instances, their upgrade fittings, and the buy/sell/upgrade screen |
| Post / lever | A crew job that claims skill levers; only hands on a post pull its levers; unclaimed levers are convoy-wide |
| Intel | The information post's price reports from nearby cities — derived, error-bounded, never true, never stored |
| Knowledge / trait | Per-category crew expertise and special traits affecting bargaining, travel, repair |
| Greedy / Random / HouseTrader | The three bot policies: skill baseline, careless control, and the play-tester used by the balance harness |
| Skilled / careless / house figures | The FIGURES.md money results for Greedy / Random / HouseTrader runs; skill must beat luck |
| Day tick | The one daily advance: costs, market ticks, storerooms, expo stall, events, contracts, travel, mining, solvency |
| `WorldData` / `GameState` | Resolved content (read-only after load) vs. the mutable run state — the only two things rules may read |
| `GameView` / view model | The display snapshot `ViewBuilder` derives; the entire front-end wire contract |
| Keeper's Chart | The player map view (`web/chart/chart.html`), served at `/chart/` |
| Ops shell | The ERP-style workspace (`ops.js`/`ops.css`) docked over the chart; every non-map screen is a page/tab entry |
| Tile worker | `chart-tiles-worker.js`; renders map tiles off the main thread |
| `world.js` / WORLD | The generated in-browser world payload from `make-world.js`; full SHA-256 pinned by `tools/verify-worldjs.ps1` |
| `F_state` / `F_view` | The pinned determinism fingerprints (state + view) checked on every Full run |
| Nine gates | The `check.ps1` acceptance checks: build, tests, balance, host haul, recruitment, city page, build page, world sync, API baseline |
| Full battery | The six-gate integration verification; the only basis for a green claim (see `docs/features/verification.md`) |
| `FIGURES.md` | The generated economy figures file; the only valid source for any number |
| BalanceSim | `tools/MechaTrader.BalanceSim`, the headless economy gate that writes FIGURES.md |
| Fingerprint tool | `tools/MechaTrader.Fingerprint`, regenerates determinism/save fixtures on demand (a re-baseline is an explicit decision) |
