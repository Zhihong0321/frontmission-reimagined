# Night Log — Alpha 1 build

Append-only. Newest entries at the bottom.

## Environment note
No .NET SDK was present on this machine (runtimes only). Installed **.NET SDK 8.0.424
user-scope** via the official `dot.net/v1/dotnet-install.ps1` script into
`C:\Users\Eternalgy\AppData\Local\Microsoft\dotnet`. No admin elevation, nothing written
outside the user profile, system-wide dotnet untouched. Remove by deleting that folder.

Because it is not on PATH, use the provided `dev.ps1` / `dev.sh` wrappers, or:
`$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"`

---

### Milestone 1 — simulation core + balance harness green

- `MechaTrader.Core`: pure C# sim (model, world loader, economy, day tick, commands,
  view models, AI policies). No filesystem, no console, no engine.
- `MechaTrader.Content`: the only project that touches disk; feeds JSON strings to Core.
- `MechaTrader.BalanceSim`: 1000-day headless run + one-hop opportunity scan + bot duel.

Three real problems the harness caught, in order:

1. **Bot priced sales at the marginal price.** Projected revenue ignored that selling a
   full hold craters the price it sells into. Added midpoint-approximation pricing for
   planning (exact quotes still settle trades) and made order size a searched decision.
   Loss went -15,841 -> -2,700.
2. **-2,700 was exactly 60 x 45 upkeep: the bot never traded at all.** Added a one-hop
   opportunity scan to the harness, which showed 0 of 464 runs on the map were
   profitable. Markets were too shallow (any real order self-destructed) and neighbours
   too similar. Tuned `equilibriumDays` 10 -> 30 for depth and `driftRate` 0.25 -> 0.08
   for gradient; spread 0.06 -> 0.045. Result: 30 profitable runs, margins to 35%.
3. **Bot still traded zero times.** It sat in Munchen waiting because no *local* run
   paid, bleeding upkeep. Gave it one hop of lookahead so it repositions empty toward
   opportunity.

Final: greedy +46,534 cr / random -17,106 cr over 60 days. Economy stable over 1000
days, all 8 goods hold a 50-78% cross-city spread, tick cost 15.5 ms for 160,000 updates.

TUNING NOTE for review: a greedy bot more than triples its money in 60 days. Good enough
to prove the loop, but likely too generous once rivals and depot costs land.

### Milestone 2 — tests green

44 tests: economy properties, command validation and its no-partial-mutation guarantee,
travel timing, save/load round trip, skill expression, and an architecture grep that
enforces Core's purity (no filesystem, console, wall clock or ambient randomness).

The architecture test initially fired a false positive: the substring `File.` matched
`industryFile.`. Switched to word-boundary regex.

### Milestone 3 — web host + browser UI

`MechaTrader.Host` (ASP.NET minimal API) serves `web/` and exposes three endpoints:
`GET /api/state`, `POST /api/command`, `POST /api/new`. No rules live at this layer —
it parses JSON into a `Command` and keeps a display log, nothing else.

`web/` is plain HTML/CSS/JS: no framework, no bundler, no npm. Verified in a real browser:
market board with surplus/deficit tags, roads panel with distance/time/fuel, hold,
depot, and event log. Clicking through buy -> depart -> arrive works; DOM checked clean
(one header, five panels, no horizontal overflow).

### Milestone 4 — ALPHA 1 ACCEPTED

`.\check.ps1` runs all four acceptance criteria and prints one verdict line. All green:

    PASS  Solution builds in Release with no warnings
    PASS  Unit tests pass                          (44 tests)
    PASS  Balance harness green                    (tick 20.3 ms; skilled +46,534 cr;
                                                    careless -17,106 cr)
    PASS  Web host serves a playable buy-haul-sell cycle

One fix needed here: `Invoke-WebRequest` under Windows PowerShell 5.1 routes through the
IE engine and cannot initialise non-interactively. Added `-UseBasicParsing`.

---

## Morning summary

Alpha 1 is done and verified. To see it:

    .\check.ps1                                    # 30s, prints one verdict line
    dotnet run --project src/MechaTrader.Host      # then open http://localhost:5080

Written this session: `README.md` (orientation), `SPEC.md` (exact formulas and schema),
`ACCEPTANCE.md` (what each gate asserts and why).

Two things worth your judgement:

1. **The economy is too generous.** A greedy bot triples its money in 60 days. Fine for
   proving the loop, likely wrong once rivals and depot costs exist. Noted in
   ACCEPTANCE.md under "Known tuning debt"; the harness will tell you if a fix goes
   too far.
2. **Rivals are the next thing, not visuals.** A trade sim with no competitor is
   solitaire — the AI policy in `Core/Ai` is already the seed of one. Recommend rivals
   plus depot ownership before any Godot work, since both change balance and the map is
   already data-real.

---

## Phase 2 — crew and recruitment

Added people. Four skills, each wired to a lever the simulation already had:
navigation → convoy speed, negotiation → buy side, sales → sell side, accounting →
truck upkeep and fuel. Every city runs a recruitment centre; four seats on the convoy;
wages charged every day whether the convoy moves or not.

Three decisions worth recording, because each had a worse obvious alternative.

**Bonuses erode the spread, they do not discount the price.** The first sketch had
negotiation take a percentage off the buy price and sales add one to the sell price.
At the shipping spread of 4.5% each way, a 5%-per-side crew makes selling in the city
you bought in *profitable* — an infinite money loop reachable by ordinary play. Crew
bonuses are now a share of the spread still conceded (`TradeTerms`), clamped to [0, 1].
A perfect crew makes an in-place round trip exactly free and nothing makes it pay. The
property is asserted over every city and good in the unit suite and again in the balance
harness, because it is the kind of thing a later tuning pass could quietly reintroduce.

**A skill is led by the best hand aboard, not the sum of the roster.** Summing would make
headcount the answer and turn hiring into a slider. Taking the maximum makes it a
question of *who*, with payroll as the counterweight, and it leaves the four-seat limit
meaning something.

**Recruitment pools are derived, never stored.** A pool is a pure function of
`(seed, cityId, hiringRound)`, so the view can draw the board and `CommandProcessor` can
re-derive the identical list to validate a hire. Nothing to persist, nothing that can
disagree with itself. Critically it does not draw from `GameState.RngState` — if it did,
opening a screen would advance the world's random sequence and determinism would be gone.
It also folds the city id with FNV-1a rather than `string.GetHashCode`, which is
randomised per process and would give a save a different pool on every launch.

Content-side, `crew.json` carries the skills, the levers they pull, the roles, wages and
the name pools. A skill names its lever rather than being recognised by id, so retuning
or renaming one is a data change, and a skill on lever `none` ships a stat before the
system behind it exists. The loader rejects an unknown lever, two skills claiming the
same lever, a role specialising in a skill that does not exist, and affinity entries
naming unknown industries.

The economy underneath is untouched: both bots still trade without crew, and the harness
still prints +44,928 / −15,805 to the credit. Crew are an investment the player opts
into, priced so that a wage is a slow bleed on small runs and obviously worth it on
large ones.

21 new tests (44 → 65), a fifth acceptance gate that hires, waits a day and pays off over
HTTP, and a crew section in the balance report showing what each city is offering.

### Two stores per city

Corrected a design error from earlier in the session. I had treated the in-place round
trip as something to be made *unprofitable* by clamping crew bonuses to the spread. It
should not be reachable at all.

A city now holds each good in two stores. `Out` is the shelf: what it sells, all a convoy
can buy, and capped accordingly. `In` is the intake: what caravans have unloaded on it.
Selling fills the intake, so it cannot cheapen the shelf, and the goods you dropped are
not on sale the same day — the city eats out of the intake first and shelves the rest at
`restockRate` (0.35/day). Both stores together are what the city owns.

The pricing follows from the split rather than being bolted onto it: the buy quote reads
the shelf, the sell quote reads shelf + intake. Since price falls as stock rises and the
total is never below the shelf, `sell ≤ buy` holds at every possible holding. It is now a
property of the model, not of the tuning. The crew clamp stays as a second, independent
guard, but it is no longer the thing standing between the game and a money printer.

The tick reduces exactly to the old single-pool formula when the intake is empty, and
draws the same single random number, so an untraded world replays identically. That is
asserted against a copy of the old formula in
`AnUntradedCityTicksExactlyAsItDidWithOneStore`, and it is why every price band in the
balance report is unchanged.

One real consequence: you can no longer buy more than a city has. Orders are capped at
the shelf, which is also why the greedy bot improved from 44,928 to 47,404 — it used to
be able to place an order larger than the city could fill and pay the resulting price
spike for it.

74 tests (65 → 74). The market panel now shows the shelf, with an intake badge when a
city is holding goods somebody unloaded on it.

## Phase 3 — cities that have a state of their own

Before this, a city was a market board with a name on it. It had a population, but only
because market generation needed a number to multiply by; nothing about the place itself
was legible, and there was nowhere for an event to land.

The brief was to lay the stats down as *founding* values first and deliberately write no
events yet. That turned out to be the right order, because it forced the question that
matters: which of these numbers is content and which is state.

### The split

**Vitals are authored, then carried live.** Population, peacefulness and economic growth
are written per city in `cities.json` under `stats`, keyed against a catalogue in the new
`citystats.json`. `Game.New` copies each city's block into `GameState.CityVitals`, and
from that moment reads go through state. Content is the floor underneath, consulted only
when a save has never heard of a stat — which is what lets a stat be added later without
invalidating a save.

Nothing moves a vital. That is the point: when something does, it has exactly one place
to write, save/load already carries it, and the page already draws it.

**Supply figures are derived and stored nowhere.** Power grid, basic, industrial and
luxury each read a slice of the city's own market:

```
index = 100 × Σ basePrice × (shelf + intake) / Σ basePrice × max(steadyStateStock, minStock)
```

Two decisions in that line carry all the weight. Weighting by base price rather than
counting units stops a heap of twelve-credit scrap papering over a plate shortage.
Anchoring on the city's *own* steady state rather than a global scale means a city that
structurally imports a good still reads 100 when nothing is wrong — the figure says
"short of its own normal", which is the only reading that is comparable between a mining
town and a trade hub.

The consequence is that the city already breathes, with no event system in sight. Thirty
days of drift moved Praha's power grid to 106% and its basics to 110%; a 226-unit plating
haul dropped its industrial band to 88% on the spot.

### Population stopped being two numbers

The obvious way to add a population stat would have been to author a head count beside
the existing `population` scale factor. That is two numbers for one fact, and they would
have drifted the first time anything changed either.

Instead the scale factor *moved into* the stat block and became the vital. A city has one
size; market generation and the city page read the same field. The catalogue carries a
`displayScale`, so the simulation sees 1.5 and the player reads "6.0M". Every population
value is byte-identical to before, which is why the balance figures did not move: skilled
play still finishes 47,404 cr and careless play still finishes −16,224 cr.

### What the front-end was not allowed to learn

The city page draws meters and prints strings. It does not know what peacefulness is,
when a power grid counts as strained, or that population is displayed in millions. A
vital arrives with its display string already formatted and signed; a band arrives with a
`tone` — `bad`, `warn`, `ok`, `good`, `muted` — and the stylesheet's only decision is what
a tone looks like. Adding a fourth vital is an entry in `citystats.json` and a number per
city. No C#, no CSS.

### The news slot is empty on purpose

The brief asked for news on the page. With no events written, the honest thing was to lay
down the shape — `CityNewsView`, an empty list, and a panel that says the wire from Praha
is quiet — rather than manufacture headlines out of market readings to look busy. A
derived "headline" would have been an event system built by the back door, and it would
have had to be unpicked the moment real events arrive.

### Gate

A sixth acceptance criterion, and it earned its place immediately: the first version
bought 60 units of scrap and asserted the industrial band fell. It did not, because the
band is value-weighted and 720 credits of scrap rounds to nothing against a band worth
tens of thousands. The check now takes the haul the game itself recommends, which is both
the honest size of a trade and retune-proof. That failure was the criterion working.

92 tests (78 → 92). Six gates green, FIGURES.md unchanged.

## Phase 3b — a launcher on the desktop, and a build page that tells the truth

Two asks, one problem underneath: *how do I know the thing I just launched contains the
change I just made?*

### The launcher

A shortcut on the desktop pointing at `Play.cmd`, installed by `Install-Launcher.cmd`.

The one decision worth recording is what it points at. Pointing a shortcut at a built
binary is the obvious move and it is wrong here — it is precisely how you end up playing
a build from two hours ago and wondering why nothing changed. `Play.cmd` rebuilds before
it serves, so a launcher aimed at it cannot start a stale build. The shortcut is
therefore a wrapper around a build step, not around an executable.

Installer overwrites rather than refusing, so running it again repairs a broken or moved
shortcut. `-Remove` takes it off. `-Destination` puts it somewhere else.

### The build page

`/api/build`, a badge in the header and a Build panel in the aside. It lives in
`MechaTrader.Content`, alongside `ContentLoader`, for the same reason: that is the one
project allowed to touch a filesystem, and none of this is a game rule. Core does not
know what a build is.

Version comes from a one-line `VERSION` file at the root — one place to bump, read by the
page, the startup banner and the acceptance gate alike. Commit data comes from shelling
out to git. Everything degrades rather than throws: a copy with no git, no `.git` and no
sources still reports its assembly version and when it was compiled, which is what a
shipped build will look like.

### The part that needed a second pass

The first cut measured staleness one way: newest file under `src`, `tools`, `tests`,
`data` against the binary's compile time. That is correct for code and wrong for content.

Editing `data/cities.json` triggers no rebuild — MSBuild has nothing to do — so the
binary's timestamp stays put while the file's moves forward. The page would have shouted
"stale" at a server that had, in fact, just read that exact file at startup. A staleness
warning that fires when nothing is wrong is worse than no warning, because it trains you
to ignore the one that matters.

So there are two clocks now. Code is judged against **when the binary was compiled**, and
says *rebuild*. Content is judged against **when this server process started**, and says
*restart*. Both roll up into one `stale` flag and one sentence naming the offending file,
so the page stays simple while the judgement underneath is honest.

Verified end to end rather than by inspection: `touch src/.../Economy.cs` and the badge
went red with `src/MechaTrader.Core/Sim/Economy.cs changed just now, after this build was
compiled - rebuild to pick it up`.

### Gate

A seventh criterion. Its load-bearing assertion is not that the version renders — it is
that a *freshly built* server reports itself **current**. Criterion 1 rebuilds and nothing
is edited afterwards, so a working detector has no choice but to agree. If it disagrees,
the detector is broken and the whole feature is noise.

Uncommitted work is reported but deliberately not asserted on: a dirty tree is the normal
state while building, and the page's job is to mention it, not to object to it.

It earned its keep immediately. First run after wiring it up: **FAIL**, with the reason
printed in full — `src/MechaTrader.Core/Sim/Economy.cs changed 3 minutes ago, after this
build was compiled`, on a solution that had just been rebuilt.

The detector was reading build time from the entry assembly. But a change confined to the
simulation recompiles `MechaTrader.Core.dll` and leaves `MechaTrader.Host.dll` alone — the
host's own sources did not change, so MSBuild correctly does not touch it. Every
Core-only change would have been reported as an un-built change forever, which is the
worst possible failure mode for this feature: permanently red, therefore permanently
ignored. Build time is now the newest of every `MechaTrader*.dll` beside the running
binary, which is what "when was this application last compiled" actually means.

Two criteria written this session, two real bugs caught on their first run, both in the
thing being asserted rather than in the assertion. That is the gate doing its job.

100 tests (92 → 100). Seven gates green.

---

### City standing — governor, favor, permits, reserved shelf

The city page had vitals and supply but no one to talk to. Standing is how the player
relates to a city, stored per city, starting at zero. Rank, the reserved-shelf share and
which permits are due are derived from that number; permits once granted stick as ids.

Three petitions, one command (`CityFavor`): donate is a gift, invest writes the growth
vital (the first thing that moves a city stat), aid ships the shortest supply into
intake so it cannot cheapen a buy. Shop permit at 40, factory at 70. Building those is
a later act; holding the paper is the grant.

Reserved share is `standing × 0.4%`, capped at 40%. The player can still buy the whole
shelf; other caravans only see what is left. No other caravans exist yet, but the number
is real and on the market row, so the privilege is already legible.

Governors are authored per city. A city that omits a name gets a stable pick from the
crew name pools, so loader tests that replace `cities.json` do not all have to invent
one.

---

### World events — price, city, stats

The city wire was empty on purpose: a derived headline would have been an event system
built by the back door. Events are now content. Templates live in `events.json`; live
instances live on the save. Price multipliers and vital overlays are derived from the
active set, so they vanish when the instance expires. A stock shock writes the shelf
once, because goods do not teleport back.

Nine templates: mill walkout, cave-in, grid strain, bumper harvest, trade fair, street
unrest, sensor orders, yard overflow, and a continent-wide cell scare. The daily roll
is 18%, cap 3. Settlement, the market board, the AI and the city page all read the
same overlay. Invest still writes the stored vital underneath.

The city wire prints the dispatch, the market row names the premium, the map rings
cities that have news. Day 1 is still quiet; the clock is what fills the wire.

127 tests, seven gates.

---

### Headless play-tester — HouseTrader on the Game API

Rivals in the player's world still need more than one convoy. That refactor is not this
slice. What landed is the thing that has to exist first: a C# policy that plays the
whole command set, and a runner that reports how the game went, not only whether it
made money.

`GreedyTrader` stays the un-crewed skill baseline. `TradeScout` is the one-hop planner
it already had, pulled out so a second policy cannot drift. `HouseTrader` is that
planner plus three cheap extras, issued only while parked with an empty hold:

- hire the local candidate who most improves a lever the next run uses, if cash still
  holds a starting-capital reserve after the fee
- one extra mule, only when the best run is already hitting the hold rather than the
  wallet
- donate, never invest or aid, and only when standing is still zero and the books are
  fat enough that a gift is not the run

It never touches `GameState.RngState`. Recruitment pools are derived from the seed,
same as the city page. `BotRunner` now records command mix, reject reasons, cities,
goods, travel vs parked, the wealth path, crew/trucks/standing at the end, and whether
a world event was live. The balance harness runs the house on the same 60 days × 5
seeds, writes a Playtest section into `FIGURES.md`, and fails if the house loses money,
gets stuck, never leaves town, or never touches crew / trucks / standing.

Live rival houses (shared clock, N caravans, AI acting on the player's Wait) stay the
next milestone. The policy is the seed of that; the world is not ready for it yet.

133 tests, seven gates green. Quote `FIGURES.md` for the house numbers, not this file.

---

### BRAIN.md — onboarding for later sessions that update the AI

The house policy is the seed of two jobs that share a body: a competing faction on the
player's map, and automated caravans inside the player's own house (many teams, some
driven by hand, some by the same `HouseTrader`). That split is not in `GameState` yet.
What landed is the briefing other sessions need so a new feature does not ship with a
brain that cannot click it: `BRAIN.md`. `CLAUDE.md` points here; the change-impact map
now has a "brain" row. Teach `HouseTrader`, never `GreedyTrader`. One policy, N bodies,
when actors exist.

---

### Catalog, knowledge, grade, storerooms

Trading was a thin price board. The catalog grew to 17 goods in 7 categories (raw
feedstock through components), each hand now carries per-category knowledge and may
walk in with a special trait (product / traveling / repair / bargain), and a shop
shows an average grade. Knowledge does not rewrite that average: buying the whole
shelf still takes it; a smaller order with a better eye skips the worse crates, and
S-tier (90%+) sells at +30%. Category knowledge also erodes a slice of the buy/sell
spread. A trade grants a little XP in that category and in negotiation or sales.

Storerooms are a rented city room with auto-sell and auto-procure prices. They tick
on Wait at market terms — nobody is at the counter, so crew knowledge does not
cherry-pick for a room you are not standing in. Map files were left alone; another
session is redrawing them.

Quote `FIGURES.md` after the next harness run, not this file.




---

### Product depth, relationship segments, the station, contracts, the expo (2026-09-02, night)

The owner's review of the first playtest asked for depth in the product, not more
map. What landed, in the order it was built:

**Five grades, eleven categories, forty-one goods.** Tier is a number now. Each tier
declares a colour, a price-per-volume floor the loader enforces (higher grade =
denser value, so a "cheap exotic" cannot be authored by mistake), an equilibrium scale
so a masterwork core does not rest in a pile of 150, and the total standing a city
wants before it sells that grade to you. Electronics, machinery, medicine and tools
joined as categories with four new industries (Fab Works, Machine Shops, Med Labs,
Tool Forges) spread across the cities. Every city now eats a little of nearly
everything through `baseConsumptionPerPop`; production rose across the board so
there is enough to haul.

**Grade is made, not fixed.** A crate produced today grades `base + roll + craft`,
where `craft` is a new city vital (Workmanship). Zurich turns out crates that touch
S-tier on their own; Lisboa never does. The roll draws the day's RNG per city per
good in content order, so replay still holds. This exposed a real hole: a shelf that
grades above nominal sold back to itself at a profit through the S-tier multiplier.
The fix is structural: **the shop charges for the grade you pick**, so the quality
multiplier sits on both ends and cherry-picking cannot be an in-place income. Knowledge
still pays through contracts that demand a grade, through expo buyers who pay for it,
and through the wider absolute margin on a finer crate.

**Shortages and gluts by category.** An event may name whole categories. A shortage
carries `reliefStanding`: selling a covered good into the afflicted city earns citizen
standing per `reliefUnits`. Gluts drop a category's price and flood the shelf.

**Relationship is four segments of 100.** Governor (donate, invest, permits), Citizens
(shortage relief, aid), Traders (contracts, volume), and one held back. Rank, the
reserved shelf, permits and tier locks all read the total. Any road to a city's regard
opens its shelf. Old thresholds were retuned for the 400 scale.

**The station.** Trucks are instances now, so a fitting sits on one truck and not the
next. Five fittings, one of each per vehicle. The station buys back at a resale
fraction of the vehicle and its fittings, and never lets the convoy sell itself
immobile or leave cargo on the ground.

**Contracts.** A board per city, derived from `(seed, city, round)` like a recruitment
pool. A city only asks for what it does not make. Three shapes: fine goods at a grade,
a procurement list, supply at a fixed price. Delivery pays cash and traders standing;
a lapse tears the contract up and costs some.

**The expo.** Every city runs its own on a 24-day cycle, theme and dates derived from
the seed. Two categories buff hard, five barely. A pass is a fee, open to anyone. The
stall trades on the day tick: buyers walk the hall, anchor on base price plus the buff
premium, and say why they did or did not buy. The one guard the owner chose: a city's
own produce is never allowed on a stall in its own expo. The hall on the Expo tab is a
replay of the stored day report; the shell decides nothing.

Docs, brain and harness updated. The house now fits an economy tune; contracts and the
stall stay player-only until the multi-hop planner exists (reason in BRAIN.md).
204 tests, harness green. Quote `FIGURES.md` for the numbers.

## 2026-09-02 - Crew posts, and word from elsewhere

**Posts.** The owner asked for assignable crew roles: Trading (buy / sell) and
Information (price checks from nearby cities, run off a human stat). A hand now holds
one post, stored on `CrewMember.PostId`. A post is content (`crew.json` `posts`) and
claims levers: a claimed lever is pulled only by the hands on that post, an unclaimed
one stays convoy-wide. Trading claims buy and sell, so the counter is the only place
negotiation, sales, bargain traits, category knowledge and the cherry-pick eye count.
Navigation and accounting are unclaimed on purpose: everyone reads the road. Every
gated read goes through one function, `CrewMath.OnPost`, so the terms, the pick and the
crew page cannot disagree about who leads. A hire lands on the post their trade implies
(the role's `post`, else the post claiming the primary skill's lever, else none) and
`AssignCrew` moves them. Free, works on the road, refuses a repeat.

**Intelligence.** A fifth skill on a new `intel` lever, a Scout role, hubs breed it. It
does nothing off the Information post. On it, the nearest cities by road (Dijkstra over
`routes.json`) report what they pay for every good: reach runs 2 to 8 cities with the
level, and each figure is off by up to 40% at the bottom, exact at the top. The noise
is a hash of (seed, city, good, day, side), so reports are stable for a day, differ
tomorrow, and never draw the game RNG - reading the pane cannot advance the world.
Reports are derived and never stored; nothing hands the true price to the shell under
the informant's name. The trade pane shows offers when selling, asks when buying,
sorted best first with the day count and the ± bound on every line.

**What broke and why it was right.** A metals specialist built in a test with no post
stopped bargaining. That is the feature: knowledge has to be at the counter. The test
now posts them. Adding a skill and a role reshuffles every recruitment pool (the roll
order is content order), so the house playtest numbers moved; the harness regenerated
`FIGURES.md`. The house hires by post already and never hires a scout, because the
one-hop planner reads state exactly; reports become its eyes when a rival must not see
the player's markets for free (reason in BRAIN.md).

---

### Prices move at the day tick, never inside a deal (2026-09-02, late)

The owner played the product update and called it torture: buy what a city makes,
sell it next door, lose up to half the purse. A probe of every such naive run (441 of
them, full hold, producer to neighbour) agreed: 84% lost, median −14%, worst −40%.

Two causes, both structural. First, an order was priced against the depth it consumed,
unit by unit, so filling a Mule walked the buy price up and dumping it walked the sell
price down. A bulk penalty, which the owner rightly said is the opposite of how trade
works. Second, most cities barely consumed most goods, so there was nowhere to sell.

What changed:

- **An order settles at one price for the whole lot, and prices move only at the day
  tick.** `CityStock` now carries the shelf and intake as the day opened; every quote
  reads those, trades move the live figures, and the tick folds them together. The
  second order today pays what the first did. A stock shock reprices at once. Sell ≤
  buy still holds by construction (both sides read the opening figures). The 3% midpoint
  approximation is gone because there is nothing to approximate.
- **Every city eats 2.5× more of everything** through `baseConsumptionPerPop`. That is
  the demand a stranger sells into.

After both, the same probe: 32% lose, median +10%, and a haul to a city that does not
make the good has a median of +19%. What still loses is hauling a good to a city that
makes it too, which is a direction mistake the market board should make visible (a
crew information post is landing from another session for exactly that).

The cost is a high skill ceiling: a perfect-information bot that re-plans every hop now
clears far more than before over 60 days. The knobs to pull that back are `driftRate`
(0.12 roughly halves it, at the price of a thinner naive margin) and `spread`; both were
swept and left alone deliberately, because the owner's complaint was about losing, not
about winning too easily. Numbers in `FIGURES.md`.

---

## Naive haul gate (2026-09-02, night) — the complaint becomes an assertion

The flat-price fix was verified, then turned into a gate so it cannot silently regress.

The verification first: a probe of every plain producer-to-neighbour haul (buy the city's
own surplus at full purse, straight to a road neighbour, no planning, no crew) found
417 runs, 28.5% losing, median +18.5%. Hauling to a city that does not make the good wins
94.9% of the time at +26.8% median; hauling to a city that makes it too loses 83% at
-13.5% median — the direction mistake the market board's informant post exists to make
visible. Worst case is -5,275 cr, a 26% drawdown on the 20,000 start purse. Nobody can
lose half the purse on a naive haul any more; that was the owner's exact complaint.

That probe is now a permanent section of the balance harness (`NaiveHaulProbe`), asserted
on every `check.ps1` run with three rules, each keyed to a failure the fix used to have:

- the median haul of a maker's surplus to a city that does not make it must be positive
  (or the good-direction trade is dead by construction)
- under half of all naive producer-to-neighbour hauls may lose (pre-fix it was 84%)
- no naive full-hold haul may lose half the starting purse (pre-fix the worst lost 50%)

The numbers also land in `FIGURES.md` under "Naive routes", so the file you quote carries
the answer to "does a plain haul still pay". 216 tests, seven gates green.

One more find for the same reason the gate exists: while verifying, the build-page
staleness detector cried stale on a perfectly current game twice - once for a
`tests/` edit, once for a `tools/` edit, neither of which rebuilds the game or host
DLLs. The scan now covers only `src/`, which is what actually feeds the running game;
uncommitted work is still reported separately through the git `Dirty` flag. A test
(`TestsAndToolsChangesDoNotMarkTheGameStale`) pins the behaviour. 217 tests, seven
gates green.
