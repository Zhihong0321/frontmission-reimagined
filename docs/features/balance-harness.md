# Feature note: balance harness

Owns: `tools/MechaTrader.BalanceSim/` (Program.cs plus its partial fragments) and the
generated `FIGURES.md`. Tests: `PlaytestTests`. Verify with
`dotnet run --project tools/MechaTrader.BalanceSim -c Release` or
`tools\verify-feature.ps1 -Feature balance`.

## What it is

A headless economy gate. It runs the world for 1000 days, prices a table of naive
routes, plays Greedy vs Random for the un-crewed skill baseline, runs the HouseTrader
play-tester over 5 seeds, asserts performance and fairness budgets, and rewrites
`FIGURES.md`. `check.ps1` gate 3 runs it on every verification: the economy must stay
sane, interesting, fast, and reward skill.

## Key facts

- The budgets live in the harness (tick cost, rejection rate, skill-beats-luck); they
  are authoritative — do not tune them to make a change pass.
- FIGURES.md sections mirror the harness output: world, opening position, economy,
  naive routes, skill expression, playtest, crew. It is generated; never hand-edit.
- A check run rewrites FIGURES.md with a timing-line-only diff (`1000-day tick:
  ~N ms`). Restore it (`git checkout -- FIGURES.md`); committing it is a hygiene
  failure because the number is wall-clock noise.

## Invariants and gotchas

- Skill must beat luck: Greedy (good play) profits, Random (careless play) loses. If
  your change flips that, the economy is broken — fix the change, not the harness.
- Console output of the harness is deterministic except the `tick time:` line; that is
  why gate 3 can diff behavior across refactors.
- The harness talks only through the public `Game` API — same boundary as any
  front-end. It is also the reference for "what do these economy knobs do".
