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
| 6 | City page is alive | same host | every vital arrives ready to print; a settled world reads nominal supply; the governor is named; a donate raises standing; the recommended haul visibly drains the band it came from |
| 7 | Build page is honest | same host | `/api/build` names the VERSION file's version, HEAD and its commit log, and reports this freshly built server as current |

## What criterion 3 actually asserts

The balance harness is the real gate, because a trade game can compile, pass unit tests
and still not be a game. It runs the world 1000 days unattended and fails if any of the
following stops being true.

**Numerically sane.** No stock goes NaN, infinite or negative. No price leaves the band
0.3×–3.5× of base at any city on any sampled day.

**Worth traversing.** At least 5 goods hold a ≥20% median price spread across the
map. If everywhere costs the same, the map is scenery.

**Actually tradeable.** At least one single-hop run on the map clears its own fuel and
upkeep. The harness prints the six best runs so a tuning change can be read at a glance.

**Fast.** 1000 days × 20 cities × N goods completes inside 500 ms.

**Rewards skill.** A greedy trading policy must finish 60 days *up*, and a random policy
must finish *down*, averaged over 5 seeds. This is the criterion that says the loop is a
game rather than a spreadsheet — if both made money, upkeep would be too low; if neither
did, no player could win either.

**Play-tests the rest of the command set.** A `HouseTrader` — same haulage, plus hire /
one extra mule / donate / one economy fitting — must also finish 60 days up, reject fewer than 10% of its
commands, visit more than one city, and actually issue at least one of hire, buy-truck
or favor across the seed set. If it never touches those systems, they are untested by
play. Greedy and random stay the un-crewed baseline; HouseTrader is the play-tester,
not a live rival on the player's map.

Current margins live in **`FIGURES.md`**, which this harness regenerates on every run.
Both baseline bots trade without crew, so those figures are the un-crewed baseline: crew
are an investment the player opts into, not a change to the underlying economy. The
Playtest section is how the house run went. Do not quote a margin from memory — quote
that file.

The harness also asserts the no-arbitrage property directly, against a maxed crew, in
every city, for every good, with an empty intake and a glutted one. The unit suite
holds the same line through grade: the shop charges for the grade you pick, so the
S-tier multiplier sits on both ends of an in-place round trip and a knowledgeable crew
cannot sell a shelf back to itself at a profit. Scouts, the opening-run figure and the
road estimates all skip grades a city will not sell to a stranger, so the run the game
recommends on day 1 is always one it will actually let you buy.

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

## What criterion 6 actually asserts

City stats come in two halves, and this criterion is aimed at the half that can rot
silently.

The authored half is checked for completeness: every vital the catalogue declares reaches
the browser with a name, a printable display string and a meter fill inside [0, 1]. A
stat that renders as a blank card is not an error anywhere else in the stack.

The derived half is checked for life. A fresh run is settled by construction, so every
supply band in the opening city must read within a point of nominal - if that drifts, the
index has stopped meaning "compared with this city's own normal" and has quietly become
a number nobody can interpret. Then the criterion buys the run the game itself recommends
and requires that cargo's shelf to fall. The printed supply index is a whole percent of
a mixed band, so a real load can drain the market without changing the label; a shelf
that does not move when a convoy buys from it is decoration. The city wire is empty on
day 1; events fire as the clock advances.

It takes the recommended haul rather than a fixed order for the same reason criterion 4
does: content can be retuned without invalidating the check. The haul is re-scouted
after the donate, because the gift spends cash and the day-1 recommendation may no
longer be affordable. It is also the honest size of trade - a small enough order rounds
to no visible change on a whole-percent label, which is correct behaviour for a figure
displayed in whole percent; the gate therefore asserts the shelf itself fell.

## What criterion 7 actually asserts

The build page answers one question — *am I testing what is on disk?* — and it is the
kind of feature that rots without anyone noticing: git moves, a path assumption breaks,
and the page keeps rendering something plausible.

So the criterion checks that the served version matches the `VERSION` file exactly, that
git was actually readable, that the commit log came back non-empty, and that the commit
it calls HEAD is the first entry in that log.

The load-bearing assertion is the last one: **this build must not report itself stale.**
Criterion 1 rebuilt the solution and nothing has been edited since, so a correct detector
has to say the server is current. If it says otherwise the detector is broken — and a
staleness warning that fires when nothing is wrong is worse than no warning at all,
because it teaches you to ignore the one that matters.

Uncommitted work is reported but not asserted on: a dirty tree is the normal state during
development, and the page's job is to tell you about it, not to object.

## Known tuning debt

A greedy bot more than triples its money in 60 days. That is good enough to prove the
loop, but almost certainly too generous once rival houses and depot upkeep exist. Tighten
by raising fuel or upkeep, or by narrowing `driftRate`, and re-run the harness — the
numbers above will move together and the gate will tell you if you went too far.
