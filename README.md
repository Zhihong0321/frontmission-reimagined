# Mecha Trader — Alpha 1

An overland trading game in the shape of *大航海時代IV*: read a market, plan a route,
haul cargo, live off the margin. Trucks instead of ships, Europe instead of the ocean,
mech-industry commodities instead of spices. No combat.

Alpha 1 exists to answer one question — **is the trade loop actually a game?** — before
any effort goes into art, map rendering or an engine. The answer is checked by machine,
not by opinion: see [Acceptance](#acceptance).

## Run it

```bash
dotnet run --project src/MechaTrader.Host
```

Then open <http://localhost:5080>.

If `dotnet` is not found, the SDK is installed user-scope and is not on PATH:

```bash
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
```

## Acceptance

```bash
./check.ps1
```

Runs the build, the tests, the balance harness and a scripted playthrough over HTTP,
then prints one verdict line. Exit code 0 means Alpha 1 is done.

## Architecture

The governing rule: **the simulation is a standalone library and every front-end is a
view over it.** The browser UI talks to it over JSON today; a Godot 4 scene will call it
in-process later. Neither is allowed to own a rule.

```
MechaTrader.Core       pure C# simulation - no filesystem, no console, no engine, no clock
MechaTrader.Content    the only project that touches disk; hands Core plain JSON strings
MechaTrader.Host       ASP.NET minimal API; parses requests into commands, serves web/
web/                   plain HTML/CSS/JS, no framework, no build step
MechaTrader.BalanceSim headless economy gate; exits non-zero if the economy stops working
MechaTrader.Core.Tests 44 tests, including a grep that enforces Core's purity
data/                  all game content as JSON
```

`Core` referencing nothing and reaching nowhere is what makes the Godot port a project
reference instead of a rewrite. It is enforced by a test rather than by discipline.

State changes only ever happen by applying a `Command` through `CommandProcessor`, and
all randomness comes from a seeded generator stored *in* the game state. So a seed plus
a list of commands reproduces a run exactly — which is what lets the balance harness,
save/load and deterministic replay all work for free.

## How the economy works

Prices are not a random walk. Each city holds stock of each good, and price falls out of
how that stock compares to what the city naturally holds:

```
price = basePrice × clamp((equilibrium / stock) ^ elasticity, 0.4, 2.5)
```

Each day, local production and consumption push stock around while a drift term pulls it
back toward equilibrium. Stock therefore settles at
`equilibrium + (production − consumption) / driftRate` — so **a city that makes a good
sells it cheap and a city that eats one pays dearly**, with no price table authored
anywhere. Change a city's industries and its whole market re-derives.

Orders are priced against the depth they consume, unit by unit, so dumping a full hold
craters the local price. Order size is a real decision, not a formality.

Cities are generated from industry archetypes (`salvage`, `refining`, `precision`,
`assembly`, …) rather than hand-written markets, so adding a city is six lines in
`data/cities.json`.

## Where the pressure comes from

The genre's tension normally comes from risk of loss at sea. With combat cut, that slot
is filled by **truck upkeep and distance-based fuel**: money leaks every day, so standing
still is a decision with a price and a bad route costs real credits. Without it the loop
is a spreadsheet that always says yes.

## Content

All in `data/`, all hot-editable — no rebuild needed for the host to pick up changes on
restart.

| File | What it holds |
|---|---|
| `config.json` | starting cash, start city, and every economy tuning constant |
| `goods.json` | 8 commodities: base price, cargo volume, price elasticity |
| `cities.json` | 20 European cities: real coordinates, population, industries |
| `industries.json` | archetypes that generate each city's market |
| `routes.json` | 29 road links; distances derive from coordinates |
| `terrain.json` | speed and fuel multipliers — alpine passes and the Channel are chokepoints |
| `trucks.json` | capacity, speed, upkeep, fuel burn, price |

## Next

Out of scope here, in rough priority order:

1. **Rival trading houses.** `GreedyTrader` in `Core/Ai` is already the seed of one. A
   trade sim with no competitor is solitaire; rivals make opportunity itself scarce.
2. **Depot investment and ownership** — the mechanic *大航海時代IV* is actually built on.
3. Save/load to disk (the state already round-trips through JSON).
4. Godot 4 front-end, then art, then Steam.
