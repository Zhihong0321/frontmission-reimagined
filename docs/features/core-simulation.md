# Feature note: core simulation

Owns: `src/MechaTrader.Core/Game.cs`, `Model/`, `Sim/`, `State/`, `Events/`, `Ai/`.
Tests: Economy/CityStats/Standing/Event/Warehouse/Quality/Map/Product/Station/
CrewBrief/SimulationInvariant/Playtest. Verify with
`tools\verify-feature.ps1 -Feature economy` (build + the economy-scoped tests) and
`-Feature balance` after any economy change.

## What it is

`Game` (`Game.cs`) is the whole simulation behind one surface: `New(world, seed)`,
`Resume(world, state)`, `Apply(command) -> CommandResult`, `View() -> GameView`,
`NetWorth()`. Everything under `Sim/` is pure math over `(GameState, WorldData)`;
`Model/` holds the content DTOs deserialized straight from `data/*.json`; `State/`
holds the resume-complete run state; `Ai/` holds the bot policies that talk only
through `Apply`.

## Key facts

- The price model lives in `Sim/Economy.cs`: `price = base x clamp((equilibrium /
  max(stock, minStock))^elasticity, 0.4, 2.5) x eventMult`; buy reads the shelf,
  sell reads shelf + intake; the spread is shared with the crew via `TradeTerms`.
- Prices move only at the day tick (`Sim/DayTick.cs`); an order settles at one price.
- The RNG is one seeded `ulong` in `GameState` (`Sim/Rng.cs`); the day tick iterates
  cities and goods in content load order, so the random sequence is stable.
- Recruitment pools, contract boards, expo calendars, supply figures and event
  overlays are all derived on demand — they never touch the RNG and are never stored.
- `Ai/TraderPolicies.cs`: GreedyTrader (skill baseline), RandomTrader (control),
  HouseTrader (the play-tester). Policies own no rule.

## Invariants and gotchas

- No `System.IO`, `File.`, `Console.`, `DateTime.Now`, or `new Random(` in Core —
  `ArchitectureTests` greps for them and the csproj must stay reference-free.
- A rejected command leaves state byte-identical; never mutate `GameState` from a
  view, host, or policy.
- Money is `long`, stock is `double`; round once at settlement.
- After any content or economy change run the balance harness (`-Feature balance`);
  it rewrites `FIGURES.md` — restore the timing-line diff, never commit it, and quote
  `FIGURES.md` (not memory) for any number.
