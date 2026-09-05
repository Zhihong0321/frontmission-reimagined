# Feature note: world and content

Owns: `src/MechaTrader.Core/World/`, `src/MechaTrader.Content/`, `data/`. Tests:
`WorldLoaderTests`. Verify with `tools\verify-feature.ps1 -Feature world`.

## What it is

`MechaTrader.Content` is the only project that touches the filesystem: it reads
`data/*.json` and hands Core plain strings. `World/WorldLoader.cs` (with its partial
fragments) parses and validates them into `WorldData`, generates every city's market
from industry archetypes, and paints the terrain map (`MapPainter`, `WorldMap`).
`RouteGraph` holds adjacency and reachability.

## Key facts

- `data/` is ALL game content (15 files): config, goods, cities, industries, routes,
  terrain, trucks, contracts, expos, crew, citystats, standing, events, map, gear.
  Nothing content-shaped may be hardcoded in C#.
- Most content additions are JSON-only: a city stat (vital), a supply band, a favor
  action, a world event, a permit, a contract kind, an expo theme. A new content FILE,
  though, needs a DTO, a loader key, `RequiredKeys`, parse/validate, a `WorldData`
  property, and an entry in `MinimalWorld.Files` in `WorldLoaderTests` — or every
  loader test fails.
- `web/chart/world.js` is generated from six of the data files by
  `web/chart/make-world.js`; its full SHA-256 is pinned and proven by
  `tools/verify-worldjs.ps1`.

## Invariants and gotchas

- The loader rejects bad references (unknown good/city/route) — keep it that way.
- Never hand-edit `web/chart/world.js`; regenerate via `make-world.js` and let the
  verifier pin it.
- A city with no route to the start is rejected; unreachable content cannot ship.
- The world loads once at host startup: editing `data/*.json` needs a server restart
  to be visible.
