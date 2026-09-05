# Feature note: view layer and host API

Owns: `src/MechaTrader.Core/View/` (view models + `ViewBuilder*`), and the host
adapter `src/MechaTrader.Host/` (`Program.cs` endpoints, `GameSession.cs`). Tests:
`tests/api-fixtures/`, `BuildInfoTests`. Verify with
`tools\verify-feature.ps1 -Feature api` (and `-Feature host`).

## What it is

`ViewBuilder` turns `GameState` into `GameView` display snapshots — a pure read that
never mutates state or touches the RNG. The view models (`GameViewModels.cs` and the
`*Views.cs` partials) are the wire contract. The host is a thin ASP.NET adapter: five
endpoints (`/api/state`, `/api/new`, `/api/command`, `/api/map`, `/api/build`), static
file serving for `web/`, and the startup banner. No rule lives in either.

## Key facts

- The same view JSON feeds the browser today and a future Godot client; that is the
  whole point of the Core-purity rule.
- API baselines: `tests/api-fixtures/` pins the raw response bytes for a fixed seed +
  command script (`new`, `buy`, `sell`, `depart`, `wait`, `map`). `/api/build` is
  shape-checked only — its commit log and wall-clock `builtAgo` genuinely vary.
- `tools/verify-api-shape.ps1` compares live responses to the fixtures; `-Record`
  re-records them (an explicit, authorized re-baseline, never a casual run).
- Build info (`/api/build`) comes from `MechaTrader.Content/BuildInfo.cs` + `VERSION`.

## Invariants and gotchas

- If the browser needs a number, derive it in `ViewBuilder` and put it on a view
  model; the shell never computes a rule.
- Building a view must not advance the world: no RNG draws, no stored writes.
- View-model changes can silently break the front-end — after touching `View/` or the
  host, run the browser smoke (`-Feature browser`), not just the API fixtures.
- The host locks build output while running: stop `dotnet` processes before rebuilding.
