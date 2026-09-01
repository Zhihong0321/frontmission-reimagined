# Alpha 1 — acceptance criteria

Every criterion is a command with an exit code. `./check.ps1` runs all of them and
prints one verdict line.

| # | Criterion | Command | Passes when |
|---|---|---|---|
| 1 | Builds clean | `dotnet build MechaTrader.sln -c Release` | exit 0, zero warnings |
| 2 | Tests pass | `dotnet test` | exit 0, all green |
| 3 | Economy holds up | `dotnet run --project tools/MechaTrader.BalanceSim` | exit 0, prints `BALANCE OK` |
| 4 | Web host is playable | `dotnet run --project src/MechaTrader.Host` | scripted buy→depart→wait→sell over HTTP succeeds; illegal moves refused |
| 5 | Crew can be hired | same host | scripted hire→wait→pay off succeeds; fee and wage charged; unknown recruit refused |

## What criterion 3 actually asserts

The balance harness is the real gate, because a trade game can compile, pass unit tests
and still not be a game. It runs the world 1000 days unattended and fails if any of the
following stops being true.

**Numerically sane.** No stock goes NaN, infinite or negative. No price leaves the band
0.3×–3.5× of base at any city on any sampled day.

**Worth traversing.** At least 5 of 8 goods hold a ≥20% median price spread across the
map. If everywhere costs the same, the map is scenery.

**Actually tradeable.** At least one single-hop run on the map clears its own fuel and
upkeep. The harness prints the six best runs so a tuning change can be read at a glance.

**Fast.** 1000 days × 20 cities × 8 goods completes inside 500 ms. Currently ~15 ms.

**Rewards skill.** A greedy trading policy must finish 60 days *up*, and a random policy
must finish *down*, averaged over 5 seeds. This is the criterion that says the loop is a
game rather than a spreadsheet — if both made money, upkeep would be too low; if neither
did, no player could win either.

Current margins live in **`FIGURES.md`**, which this harness regenerates on every run.
Both bots trade without crew, so those figures are the un-crewed baseline: crew are an
investment the player opts into, not a change to the underlying economy. Do not quote a
margin from memory — quote that file.

The harness also asserts the no-arbitrage property directly, against a maxed crew, in
every city, for every good, with an empty intake and a glutted one.

## What criterion 5 actually asserts

Run against whatever city the criterion-4 haul finished in, so it holds for any city
rather than only the opening one. It signs the first affordable recruit on that city's
board and checks that the signing fee left the account exactly, that the next day costs
at least the agreed wage, that a made-up candidate id is refused, and that a hand who has
been paid off does not reappear on the board.

The property the unit suite guards alongside it is the one that matters most: with a
maxed roster, in every city, for every good, the sell price still does not exceed the buy
price. Two independent things hold that line — the buy price reads only the shelf while
the sell price reads everything the city owns, and crew erode the market's spread but
cannot invert it — so no roster turns standing still into an income.

## Known tuning debt

A greedy bot more than triples its money in 60 days. That is good enough to prove the
loop, but almost certainly too generous once rival houses and depot upkeep exist. Tighten
by raising fuel or upkeep, or by narrowing `driftRate`, and re-run the harness — the
numbers above will move together and the gate will tell you if you went too far.
