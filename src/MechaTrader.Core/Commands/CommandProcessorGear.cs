using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
    private static CommandResult BuyGear(GameState state, WorldData world, BuyGearCommand cmd)
    {
        if (state.Caravan.Travel is not null)
            return CommandResult.Fail("Tools are sold in a city, not on the road.");

        if (state.Caravan.LocationId is null)
            return CommandResult.Fail("Tools are sold in a city.");

        if (!world.GearById.TryGetValue(cmd.GearId, out var gear))
            return CommandResult.Fail($"No such tool '{cmd.GearId}'.");

        if (state.Cash < gear.Price)
            return CommandResult.Fail($"Not enough credits: {gear.Price:N0} needed, {state.Cash:N0} held.");

        var free = CaravanMath.FreeVolume(state.Caravan, world);
        if (gear.Volume > free + 1e-9)
            return CommandResult.Fail($"Not enough hold space: need {gear.Volume:0.#} of {free:0.#} free.");

        state.Cash -= gear.Price;
        state.Caravan.GearIds.Add(gear.Id);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Bought {gear.Name} for {gear.Price:N0} cr.")
        });
    }
}
