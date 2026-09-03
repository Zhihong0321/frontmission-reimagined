# The brain — onboarding for later sessions

Read this when the job is **update the AI**, not when the job is retune a price or draw a panel.

The brain is the C# policy that decides the next `Command`. It is the play-tester today. It is meant to become the thing that:

1. runs a **competing faction** on the same map as the player
2. runs **parts of the player's faction** — automated caravans, as many teams as the player (or a house) fields

Same brain. Different bodies. No second rule set.

If you were told "a new feature landed, teach the brain" — start at *When a feature lands* below. If you were told "make it run factions / auto-caravans" — start at *Intended shape (not built yet)*.

## The one rule

The brain **owns no game rule**. It reads `Game.View()` / `Game.State` / `Game.World` and returns a `Command`. `CommandProcessor` is the only mutator. A rejected command must leave state byte-identical, the same as a player clicking a dead button.

```
ITraderPolicy.Decide(game, rng) -> Command?
        │
        ▼
Game.Apply(command)     // the only legal write
        │
        ▼
Game.View() / NetWorth  // the only legal read for a front-end;
                        // the brain may also read State + World in-process
```

Never:

- write `GameState` from a policy (`SetStock`, `Cash -=`, `Caravan.Cargo.Add`, …)
- consume `GameState.RngState` from `Decide` (building a view, or thinking, must not advance the world)
- compute a price, a wage, a travel day, or a reserved-shelf share in the brain — call `Economy`, `CrewMath`, `CaravanMath`, `Standing`, `TradeScout`
- fork a "player bot" and a "rival bot" that know different rules. Flavour and aggressiveness can differ. Legality cannot.

`GreedyTrader` and `RandomTrader` are not the brain you extend for features. They are the skill-expression baseline (playing well must beat playing badly). **`HouseTrader` is the brain.**

## What exists today

| Piece | File | Job |
|---|---|---|
| Contract | `src/MechaTrader.Core/Ai/TraderPolicies.cs` — `ITraderPolicy` | `Name` + `Decide` |
| Planner | `src/MechaTrader.Core/Ai/TradeScout.cs` | one-hop `BestRunFrom` / `BestRepositioning` / volume-cap |
| Brain | `HouseTrader` in `TraderPolicies.cs` | haulage + hire + one mule + donate |
| Baseline | `GreedyTrader` | haulage only; do not teach it new systems |
| Control | `RandomTrader` | noise; if this profits, the economy is a printer |
| Runner | `src/MechaTrader.Core/Ai/BotRunner.cs` | plays a policy on a **fresh solo `Game`** for N days; records how it went |
| Gate | `tools/MechaTrader.BalanceSim` | 60 days × 5 seeds; Playtest section of `FIGURES.md` |
| Tests | `tests/MechaTrader.Core.Tests/PlaytestTests.cs` | determinism, no RNG leak, greedy stays un-crewed, house actually hires |

The world still has **one convoy, one cash pile, one standing map**. `BotRunner` does not put a rival on the player's map. `HouseTrader.Decide` reads `state.Caravan` and `state.Cash` because that is all there is.

`Decide` currently takes a `Rng` for the interface (`RandomTrader` uses it). `HouseTrader` must ignore it. Recruitment pools are `Recruitment.PoolFor(world, city, seed, day)` — derived, never stored, never from `GameState.RngState`.

### What HouseTrader issues, and when

Parked, hold empty, no pending depart, in this order, then haulage:

| Command | When |
|---|---|
| `HireCrew` | a seat is free, cash still holds `StartCash` after the fee, a local candidate raises a lever the next profitable run uses (speed / buy / sell / upkeep). A candidate is scored only on the levers of the post they will sign on to (`CrewConfig.DefaultPost`), against the convoy's *gated* level, so a navigator's secondary sales score buys nothing. The hire lands on that post through `CommandProcessor`, not through the brain |
| `BuyTruck` | fleet is still the starting count, the best run is **volume-capped** not cash-capped, cash after the sticker still holds `StartCash`; prefers `mule` |
| `CityFavor("donate")` | standing here is still 0, cash after the gift ≥ `2 × StartCash`. Never invest or aid — those rewrite a city |
| `UpgradeTruck` | after the gift: the cheapest fitting that cuts fuel or upkeep and costs nothing in speed or hold, on the first vehicle without it, when cash after the sticker ≥ `2 × StartCash`. Picked by effect, never by id |
| `Buy` / `Depart` / `Wait` / `Sell` | same loop as `GreedyTrader`: best one-hop, else reposition empty, else wait |

On the road it only `Wait`s out the remaining days. After a buy it `Depart`s to the pending destination before it will sell (it will not dump the hold in the city it just loaded).

Not issued today: `DismissCrew`, `SellTruck`, `invest`, `aid`, a second truck type, warehouse rent / deposit / auto-prices, `AcceptContract` / `DeliverContract`, `ExpoRegister` / `ExpoList`, `AssignCrew`.

Why `AssignCrew` is not issued: the house hires by post already (a broker signs on to the counter), and it never hires a scout because the one-hop planner reads every market exactly through `Game.State` and has no use for a noisy report. When the brain is made to plan on *reports* instead of on state (a rival house should not see the player's markets for free), the information post becomes its eyes and `AssignCrew` lands with that change, not before. Quality filtering and category knowledge are used automatically when HouseTrader buys and sells (it talks through `TradeScout` / `Economy`); it does not cherry-pick on purpose and it does not rent rooms.

Why contracts and the expo stall are player-only for now: both need a plan longer than one hop (haul a named list to a named city by a day; sit in a hall for days with an ask). `TradeScout` is a one-hop planner. Teaching the house to chase a contract means a multi-hop objective with a deadline, which is the same work as the auto-caravan milestone and should land with it, not as a heuristic bolted onto the extras chain. The scout **does** already respect tier locks: it never plans a grade the city will not sell to the house.

`SellTruck` is not issued because nothing in the extras chain ever wants a smaller convoy.

Numbers for a house run live in **`FIGURES.md`**, section Playtest. Do not quote them from this file.

## Intended shape (not built yet)

One brain, many **caravans**, grouped into **factions**.

```
Faction                          // a house: player or rival
  id, name, player?
  cash, standing, permits        // the books
  caravans[]                     // the teams

Caravan                          // one team on the road
  trucks, crew, cargo, location / travel
  controller: player | HouseTrader
```

**Competing faction.** An NPC house is a faction whose every caravan is driven by `HouseTrader`. Same markets, same events, same clock as the player. They drain the shelf, glut the intake, court the same governors. Reserved shelf finally means "held back from the other houses."

**Player automated caravans.** The player's faction can field many teams. The player may drive one by hand. The others run `HouseTrader` as auto-caravans. Creating a team is a game command (not invented in the brain). Once the team exists, the brain is just `Decide` for that caravan id.

Do not implement a second policy class for "the player's auto-convoy." Orders of magnitude, home city, or "don't compete with the player's manual run" can be parameters on the same `HouseTrader`. The commands and the legality stay one path.

### Clock (when N actors exist)

`Wait` remains the only way time passes. In a live game the **player's** Wait advances the day. Each day, every automated caravan that is parked gets a burst of zero-time commands (`Buy`, `Sell`, `Depart`, `Hire`, `Favor`, `BuyTruck`, …) until it either departs, waits, or the runner's guard trips. A caravan already on the road does nothing until it arrives — `DayTick` still decrements travel for every convoy.

The brain never calls `Wait` to skip the player's clock. `Wait` from an AI caravan means "this team is done for the day" or "sit out travel," not "advance the world." How that is encoded (a sentinel, or simply stopping the burst) is a `CommandProcessor` / runner decision when actors land — not a new economic rule.

`BotRunner` today *does* Wait, because it *is* the only actor in a solo game. That stays valid for play-testing a single team. A multi-actor runner is a new loop over the same `Decide`.

### What will have to change on the brain when actors land

`ITraderPolicy.Decide` must name **which caravan** it is driving. Until then, `state.Caravan` is an implicit "the only one." Do not sprinkle `state.Caravan` into new heuristics if you can read through a `CaravanState` / house id you were passed — it will hurt less when the split comes.

Cash and standing will be **per faction**, not global. A player auto-caravan spends the player's books; a rival spends its own. `TradeScout.BestRunFrom` already takes a `Game` and a city id; it will need to price against **that caravan's** free volume, crew terms, and the **faction's** cash.

`RecruitedIds` is currently one set for the whole run. Factions competing for the same board is a design choice for the actor milestone, not for a heuristic tweak.

## When a feature lands — update the brain

A feature that the player can click, and that a house or auto-caravan should also be able to do, is not done until `HouseTrader` can issue it (or you have written down why it must not).

Work the list in order. Skip a line only with a reason in the commit / night log.

1. **Is it a new `Command`?** It lands in `Commands.cs` + `CommandProcessor` first, with tests that a rejected call leaves state untouched. Then `GameSession.TryParse` + UI, per the change-impact map in `CLAUDE.md`. The brain is last, not first.
2. **Can the brain see what it needs to decide?** If the heuristic needs a number the player sees, it should already be on a view model or a pure `Sim/` reader. Do not make the brain invert a display string. Do not have the browser decide for it.
3. **Does `TradeScout` need it?** Anything about "what run pays" (price, volume, fuel, upkeep, event multiplier) goes in `TradeScout`, not copied into `HouseTrader`. Both `GreedyTrader` and `HouseTrader` must keep seeing the same best hop.
4. **Add a `TryX` (or extend one) on `HouseTrader` only**, in the parked-empty extras chain, **before** it fills the hold — unless the feature only makes sense mid-haul (rare). Keep extras cheap and deterministic. If two extras could fire, the order in `Decide` *is* the priority; say so in a comment.
5. **Do not teach `GreedyTrader`.** Skill expression is the un-crewed baseline. `FIGURES.md` says so. If the new feature would make greedy hire or buy trucks, the skill gate stops meaning "the economy," and starts meaning "the house."
6. **Do not consume the game RNG.** Extra `Rng` for a house style (aggressive vs timid) may use the **runner's** rng passed into `Decide`, never `state.RngState`.
7. **Telemetry.** If this is a new command kind, `BotRunner.Kind` needs a label, and `BotRunResult` needs a `UsedX` (or a count in `CommandMix` is enough). The Playtest section of `FIGURES.md` is how you see whether the house actually touched it.
8. **Gate.** If the feature is load-bearing for "is this still a game," add a harness failure in `BalanceSim` when the house never issues it across the seed set — same pattern as hire / mule / donate today. If it is optional flavour, do not fail the build for a house that skipped it.
9. **Tests in `PlaytestTests`.** Same seed ⇒ identical aggregates. `Decide` does not move `RngState`. A constructed situation (cash, seat, parked, empty hold) should be able to force the new command so the test does not depend on a 60-day roll.
10. **Verify with `.\check.ps1`.** Quote `FIGURES.md` for house numbers, never this file or your memory.

### Quick map — feature kind → brain touch

| Feature kind | Brain work |
|---|---|
| New good / city / road / industry | none if `TradeScout` already iterates `world.Goods` / `Routes` (it does) |
| Economy constant (`spread`, `driftRate`, …) | none; scout and quotes pick it up. Re-run the harness |
| New crew **skill** on an existing lever | none; `LeverGain` already walks the four levers |
| New crew **lever** | `CrewMath` + `ViewBuilder.EffectText` first, then `HouseTrader.LeverGain` |
| New truck type | only if the heuristic should buy it; today it prefers `mule` by id |
| New truck fitting (`trucks.json` `upgrades`) | none if it cuts fuel/upkeep with no speed or hold cost — `TryFitEconomy` finds it by effect. Anything else needs a reason in the extras chain |
| New product tier / tier lock | none; `TradeScout` skips locked grades via `Standing.TierOpen`. Do not teach the house to court a city *for* a grade until factions land |
| New contract kind / expo theme | none today (player-only). When the multi-hop planner lands, contracts are its first objective |
| New favor action (`standing.json`) | only if the house should use it. Default: still donate-only. Invest/aid rewrite vitals/stock — do not turn those on as a side effect |
| New permit / shop / factory that the player **builds** | new command first; then a `TryX` when the house should want one |
| World event template | none; `TradeScout` already reads `WorldEvents.PriceMultiplier` |
| New command (depot, auto-caravan spawn, dismiss-more-cleverly, …) | full list above |
| Multi-caravan / faction state | `Decide` gains a caravan/faction id; `TradeScout` prices that body's hold and that faction's cash; extras read that body's crew. See *Intended shape* |

### A new command also needs the rest of the stack

From `CLAUDE.md`, unchanged: `Commands.cs` → `CommandProcessor` → `GameSession.TryParse` → UI → bump `?v=N`. Then this file's list. If you only teach the brain, the player cannot click it. If you only teach the UI, auto-caravans and rivals never use it.

## Files you actually edit

| You are changing | Edit |
|---|---|
| Best hop / order size / volume-cap | `Ai/TradeScout.cs` |
| Hire / mule / donate / a new extra | `HouseTrader` in `Ai/TraderPolicies.cs` |
| What a run records | `Ai/BotRunner.cs` (`BotRunResult` + `Kind`) |
| Whether the harness cares | `tools/MechaTrader.BalanceSim/Program.cs` (`AssertPlaytest`, Playtest section of `FIGURES.md`) |
| Guarantees | `tests/MechaTrader.Core.Tests/PlaytestTests.cs` |
| This briefing | `BRAIN.md` (this file) — when the extras chain or the actor model changes |

`GreedyTrader` / `RandomTrader` / `SkillExpressionTests` move only if the **baseline** definition of "playing well" changed, not because a house learned a new button.

## Invariants the brain must not break

These are the project invariants that bite policies specifically. The full list is in `CLAUDE.md`.

- State changes only through `CommandProcessor`.
- Determinism: seed + command list ⇒ identical state. `HouseTrader` is a pure function of `(game, Decide-rng)`. Same seed in `BotRunner` must reproduce the `BotRunResult` aggregates (`PlaytestTests`).
- Time advances only via `WaitCommand`. A live multi-actor runner must not invent a second clock.
- Content lives in `data/`. Do not hardcode city names, good ids, or favor copy. Truck id `mule` and action id `donate` are already a smell — they fall back to "cheapest extra capacity" / "gift with no vital and no stock." Keep new heuristics on content ids the same way, or they die when someone retunes `data/`.
- Money is `long`. The brain may plan in `double` via `ApproximateBuyCost`; settlement is still the exact walk inside `Apply`.
- Recruitment pools are derived. Standing rank / reserved share / event multipliers are derived. Do not cache them on the policy across days.

## Verify

```
.\check.ps1
```

Gate 3 regenerates `FIGURES.md` and fails if the house stops profiting, gets stuck, never leaves town, or never touches crew / trucks / standing. After a brain change, read the **Playtest** section of that file and the `house` row the harness printed. If you taught a new system, confirm `Command mix` actually lists it.
