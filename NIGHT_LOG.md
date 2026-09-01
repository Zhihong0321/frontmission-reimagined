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
