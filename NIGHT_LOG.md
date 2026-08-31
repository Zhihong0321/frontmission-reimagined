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
