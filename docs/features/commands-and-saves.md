# Feature note: commands and saves

Owns: `src/MechaTrader.Core/Commands/` (`Commands.cs` records; `CommandProcessor.cs`
plus its `CommandProcessor*.cs` partial fragments). Tests: `CommandTests`,
`DeterminismFingerprintTests`, `SaveFixtureTests`. Verify with
`tools\verify-feature.ps1 -Feature commands` (and `-Feature determinism` /
`-Feature save` for the pinned fixtures).

## What it is

`CommandProcessor.Execute` is the ONLY place game state changes. Every command
validates fully, then mutates; a rejected command leaves state byte-identical.
`Commands.cs` declares the command records and `CommandResult`. The processor is split
into partial files by concern (Trade, Travel, Crew, Truck, Gear, Favor, Warehouse,
Contract, Expo, Helpers) — the `Execute` switch and the class doc stay in the original
file; the split is purely mechanical.

## Key facts

- Command flow: `Command -> CommandProcessor -> GameState -> ViewBuilder -> GameView
  -> JSON -> browser`. `DayTick` hangs off the same boundary.
- `WaitCommand` is the only clock; `Depart` starts a journey but spends no days.
- Saves are `GameState` serialized; save compatibility is pinned by `SaveFixtureTests`
  and the fixtures under `tests/MechaTrader.Core.Tests/Fixtures/`.
- A new command: record in `Commands.cs` -> a case in `CommandProcessor.Execute` ->
  a branch in `GameSession.TryParse` -> front-end wiring (see the frontend note).

## Invariants and gotchas

- Validate fully before the first write. Partial mutation on a rejected command is the
  classic bug here; `RejectedCommandsLeaveStateUntouched` is the tripwire.
- The pinned fingerprints (`F_state`, `F_view`) and save fixtures are immutable
  without an explicit re-baseline decision. If a legitimate change moves them, that is
  a separate authorized re-baseline job, never a silent fixture edit.
- The 239-test pin in `check.ps1` gate 2 means deleting or skipping tests turns the
  acceptance suite red. Fixtures are regenerated only via `tools/MechaTrader.Fingerprint`.
