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
