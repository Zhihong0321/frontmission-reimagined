using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
    private static CommandResult ExpoRegister(GameState state, WorldData world)
    {
        var parked = RequireParkedCity(state, out var cityId, "Expo passes are sold at the hall door.");
        if (parked is not null) return parked;

        var city = world.City(cityId);
        var expo = Expos.Running(world, city, state.Seed, state.Day);
        if (expo is null) return CommandResult.Fail($"No expo is open in {city.Name} today.");

        if (state.ExpoPasses.Contains(expo.PassId))
            return CommandResult.Fail("The house already holds a pass for this expo.");

        var fee = Expos.Fee(world.Expos, city);
        if (state.Cash < fee)
            return CommandResult.Fail($"Not enough credits: {fee:N0} needed, {state.Cash:N0} held.");

        state.Cash -= fee;
        state.ExpoPasses.Add(expo.PassId);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Took a stall at the {expo.Theme.Title} in {city.Name} for {fee:N0} cr; {expo.EndDay - state.Day} day(s) left to trade.")
        });
    }

    private static CommandResult ExpoList(GameState state, WorldData world, ExpoListCommand cmd)
    {
        var parked = RequireParkedCity(state, out var cityId, "The stall is in the expo hall, not on the road.");
        if (parked is not null) return parked;

        if (cmd.Price < 0) return CommandResult.Fail("Price cannot be negative.");
        if (!world.GoodsById.TryGetValue(cmd.GoodId, out var good))
            return CommandResult.Fail($"No such commodity '{cmd.GoodId}'.");

        if (cmd.Price == 0)
        {
            if (!state.Caravan.ExpoAsks.Remove(good.Id))
                return CommandResult.Fail($"{good.Name} is not on the stall.");
            return CommandResult.Success(new[]
            {
                new GameEvent(state.Day, GameEventKind.Info, $"Took {good.Name} off the stall.")
            });
        }

        var city = world.City(cityId);
        var expo = Expos.Running(world, city, state.Seed, state.Day);
        if (expo is null) return CommandResult.Fail($"No expo is open in {city.Name} today.");
        if (!state.ExpoPasses.Contains(expo.PassId))
            return CommandResult.Fail("Buy a pass before setting out a stall.");

        if (state.Caravan.Held(good.Id) <= 0)
            return CommandResult.Fail($"No {good.Name} in the hold to list.");

        if (Expos.CityMakes(city, good.Id))
            return CommandResult.Fail($"{city.Name} makes {good.Name}; a city's own produce is never allowed on a stall at its own expo.");

        if (!Expos.ThemeCovers(expo.Theme, good))
            return CommandResult.Fail($"The {expo.Theme.Title} does not admit {world.CategoryName(good.Category).ToLowerInvariant()}.");

        state.Caravan.ExpoAsks[good.Id] = cmd.Price;

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Info,
                $"{good.Name} on the stall at {cmd.Price:N0} cr a unit.")
        });
    }
}
