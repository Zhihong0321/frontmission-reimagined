using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
    private static CommandResult BuyTruck(GameState state, WorldData world, BuyTruckCommand cmd)
    {
        var parked = RequireParkedCity(state, out _, "Trucks can only be bought in a city.");
        if (parked is not null) return parked;

        if (!world.TrucksById.TryGetValue(cmd.TruckTypeId, out var truck))
            return CommandResult.Fail($"No such truck type '{cmd.TruckTypeId}'.");

        if (state.Cash < truck.Price)
            return CommandResult.Fail($"Not enough credits: {truck.Price:N0} needed, {state.Cash:N0} held.");

        state.Cash -= truck.Price;
        var instance = CaravanMath.NewTruck(state.Caravan, truck.Id);
        state.Caravan.Trucks.Add(instance);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Acquired a {truck.Name} for {truck.Price:N0} cr.")
        });
    }

    /// <summary>
    /// Sell a vehicle back to the station. The convoy must still be able to move and
    /// still be able to carry what is in the hold; the station pays the resale fraction
    /// of the vehicle and everything bolted to it.
    /// </summary>
    private static CommandResult SellTruck(GameState state, WorldData world, SellTruckCommand cmd)
    {
        var parked = RequireParkedCity(state, out _, "Trucks can only be sold at a city station.");
        if (parked is not null) return parked;

        var truck = state.Truck(cmd.TruckId);
        if (truck is null) return CommandResult.Fail("No vehicle by that reference is in the convoy.");

        var blocker = CaravanMath.SellBlocker(state.Caravan, world, truck);
        if (blocker is not null) return CommandResult.Fail(blocker);

        var value = CaravanMath.ResaleValue(truck, world);
        var type = world.Truck(truck.TypeId);

        state.Cash += value;
        state.Caravan.Trucks.Remove(truck);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Trade,
                $"Sold a {type.Name} back to the station for {value:N0} cr.")
        });
    }

    private static CommandResult UpgradeTruck(GameState state, WorldData world, UpgradeTruckCommand cmd)
    {
        var parked = RequireParkedCity(state, out _, "Fittings are done at a city station.");
        if (parked is not null) return parked;

        var truck = state.Truck(cmd.TruckId);
        if (truck is null) return CommandResult.Fail("No vehicle by that reference is in the convoy.");

        if (!world.TruckUpgradesById.TryGetValue(cmd.UpgradeId, out var upgrade))
            return CommandResult.Fail($"The station does not stock '{cmd.UpgradeId}'.");

        var type = world.Truck(truck.TypeId);
        if (!upgrade.Fits(type.EffectiveKind))
            return CommandResult.Fail($"{upgrade.Name} does not fit a {type.Name}.");

        if (truck.UpgradeIds.Contains(upgrade.Id))
            return CommandResult.Fail($"That {type.Name} already carries {upgrade.Name}.");

        if (state.Cash < upgrade.Price)
            return CommandResult.Fail($"Not enough credits: {upgrade.Price:N0} needed, {state.Cash:N0} held.");

        state.Cash -= upgrade.Price;
        truck.UpgradeIds.Add(upgrade.Id);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Fitted {upgrade.Name} to the {type.Name} for {upgrade.Price:N0} cr.")
        });
    }
}
