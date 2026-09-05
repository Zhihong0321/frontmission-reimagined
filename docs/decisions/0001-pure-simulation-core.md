# ADR 0001: MechaTrader.Core is a pure simulation library; every front-end is a view

- Status: Accepted (durable rule; enforced since Phase A; recorded in the project
  brief and `ArchitectureTests`)
- Date: 2026-09-05 (promoted into ADR form by CP-E1)
- Context: The product targets a Steam release with an intended Godot 4 (C#) client
  over the same simulation that today serves a browser front-end. Front-ends that own
  rules would force a rewrite at engine-swap time and make replay, saves, and testing
  ambiguous.

## Decision

`MechaTrader.Core` stays a pure, deterministic simulation library: no `System.IO`, no
file/console access, no wall clock, no unseeded randomness; zero project or package
references. `MechaTrader.Content` is the only project that touches the filesystem and
hands Core plain strings. Every front-end — the ASP.NET host for the browser, a future
Godot scene — is a view over Core and owns no rule.

State changes only through the command-processing boundary (`game.Apply` →
`CommandProcessor`). A rejected command leaves state untouched. Seed plus command
sequence produces identical state; views and reads never draw the RNG.

## Consequences

- The engine swap becomes a project reference, not a rewrite.
- `ArchitectureTests` mechanically enforce purity (grep + csproj assertions); the
  239-test suite and the determinism fixtures make violations loud.
- Features that seem to need I/O in Core (content, build info) get a Content-project
  or Host-side home instead.
- Derived reads (recruitment pools, contract boards, expo calendars, supply figures,
  event overlays, price reports) must stay derived: never stored, never RNG-consuming,
  or building a view would advance the world.
