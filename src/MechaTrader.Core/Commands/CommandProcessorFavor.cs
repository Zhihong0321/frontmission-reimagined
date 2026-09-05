using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
    /// <summary>
    /// Court the city. The action is looked up from content rather than branched on
    /// here, so donate / invest / aid share one command and a fourth gesture is a JSON
    /// line. Each action names the segment its standing lands in.
    /// </summary>
    private static CommandResult Favor(GameState state, WorldData world, CityFavorCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("The governor's office is back in a city.");

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return CommandResult.Fail("The convoy has no location.");

        var action = world.Standing.Action(cmd.ActionId);
        if (action is null)
            return CommandResult.Fail($"The governor's office does not take '{cmd.ActionId}' as a petition.");

        if (state.Cash < action.Cost)
        {
            return CommandResult.Fail(
                $"Not enough credits: {action.Cost:N0} needed, {state.Cash:N0} held.");
        }

        var city = world.City(cityId);
        var standingCfg = world.Standing;
        var segment = standingCfg.SegmentOr(action.SegmentId);
        var totalBefore = Standing.Of(state, cityId);
        var standingGain = Math.Min(action.Standing, Standing.Room(state, standingCfg, cityId, segment));
        var movesVital = !string.IsNullOrWhiteSpace(action.VitalId) && action.VitalDelta != 0.0;
        var shipsStock = action.StockPerGood > 0;

        if (standingGain <= 0 && !movesVital && !shipsStock)
        {
            return CommandResult.Fail(
                $"{city.GovernorTitle} {city.GovernorName} already holds you in the highest regard.");
        }

        state.Cash -= action.Cost;
        Standing.Grant(state, standingCfg, cityId, segment, standingGain);
        var totalAfter = Standing.Of(state, cityId);

        var events = new List<GameEvent>
        {
            new(state.Day, GameEventKind.Standing,
                $"{action.Name} in {city.Name}: {action.Cost:N0} cr to {city.GovernorTitle} {city.GovernorName}. " +
                $"Standing {totalBefore:0} → {totalAfter:0}.")
        };

        if (movesVital)
        {
            var def = world.CityStats.Vital(action.VitalId);
            if (def is not null)
            {
                var before = CityStats.Vital(state, city, def.Id);
                var after = Math.Clamp(before + action.VitalDelta, def.Min, def.Max);
                state.SetVital(cityId, def.Id, after);
                events.Add(new GameEvent(state.Day, GameEventKind.Standing,
                    $"{city.Name}'s {def.Name} {before:0.#} → {after:0.#}."));
            }
        }

        if (shipsStock && world.CityStats.Supplies.Count > 0)
        {
            var weakest = world.CityStats.Supplies
                .Select(s => (Def: s, Reading: CityStats.Supply(state, world, city, s)))
                .OrderBy(x => x.Reading.Index)
                .First();

            foreach (var goodId in weakest.Def.Goods)
            {
                if (!world.GoodsById.ContainsKey(goodId)) continue;
                var stock = state.StockOf(cityId, goodId);
                state.SetStock(cityId, goodId, stock with { In = stock.In + action.StockPerGood });
            }

            events.Add(new GameEvent(state.Day, GameEventKind.Standing,
                $"Shipped {action.StockPerGood:0.#} of each {weakest.Def.Name} good into {city.Name}'s intake."));
        }

        GrantDuePermits(state, world, city, events);

        return CommandResult.Success(events);
    }
}
