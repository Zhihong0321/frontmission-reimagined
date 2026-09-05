using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
    private static CommandResult RentWarehouse(GameState state, WorldData world)
    {
        var parked = RequireParkedCity(state, out var cityId);
        if (parked is not null) return parked;

        if (state.Warehouses.ContainsKey(cityId))
            return CommandResult.Fail("The house already rents a storeroom here.");

        var cfg = world.Config.Warehouse;
        if (state.Cash < cfg.RentCost)
            return CommandResult.Fail($"Not enough credits: {cfg.RentCost:N0} needed, {state.Cash:N0} held.");

        state.Cash -= cfg.RentCost;
        state.Warehouses[cityId] = new WarehouseState { CityId = cityId };
        var city = world.City(cityId);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Expense,
                $"Rented a storeroom in {city.Name} for {cfg.RentCost:N0} cr " +
                $"({cfg.Capacity:0.#} vol, {cfg.DailyRent:N0} cr/day).")
        });
    }

    private static CommandResult WarehouseDeposit(GameState state, WorldData world, WarehouseDepositCommand cmd)
    {
        var parked = RequireParkedCity(state, out var cityId);
        if (parked is not null) return parked;
        if (!state.Warehouses.TryGetValue(cityId, out var warehouse))
            return CommandResult.Fail("The house does not rent a storeroom here.");
        if (cmd.Units <= 0) return CommandResult.Fail("Quantity must be at least 1.");
        if (!world.GoodsById.TryGetValue(cmd.GoodId, out var good))
            return CommandResult.Fail($"No such commodity '{cmd.GoodId}'.");
        if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot) || lot.Units < cmd.Units)
            return CommandResult.Fail($"Only {state.Caravan.Held(good.Id):N0} {good.Name} in the hold.");

        var volume = cmd.Units * good.UnitVolume;
        if (volume > WarehouseMath.FreeVolume(warehouse, world) + 1e-9)
            return CommandResult.Fail("The storeroom has no room for that.");

        if (!warehouse.Stock.TryGetValue(good.Id, out var stored))
            warehouse.Stock[good.Id] = stored = new CargoLot();

        var costBasis = (long)Math.Round(lot.AverageCost * cmd.Units);
        stored.Add(cmd.Units, costBasis, lot.Quality);

        lot.Units -= cmd.Units;
        lot.TotalCost = Math.Max(0, lot.TotalCost - costBasis);
        if (lot.Units == 0)
        {
            state.Caravan.Cargo.Remove(good.Id);
            state.Caravan.ExpoAsks.Remove(good.Id);
        }

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Trade,
                $"Deposited {cmd.Units:N0} {good.Name} into the {world.City(cityId).Name} storeroom.")
        });
    }

    private static CommandResult WarehouseWithdraw(GameState state, WorldData world, WarehouseWithdrawCommand cmd)
    {
        var parked = RequireParkedCity(state, out var cityId);
        if (parked is not null) return parked;
        if (!state.Warehouses.TryGetValue(cityId, out var warehouse))
            return CommandResult.Fail("The house does not rent a storeroom here.");
        if (cmd.Units <= 0) return CommandResult.Fail("Quantity must be at least 1.");
        if (!world.GoodsById.TryGetValue(cmd.GoodId, out var good))
            return CommandResult.Fail($"No such commodity '{cmd.GoodId}'.");
        if (!warehouse.Stock.TryGetValue(good.Id, out var stored) || stored.Units < cmd.Units)
            return CommandResult.Fail($"Only {warehouse.Held(good.Id):N0} {good.Name} in the storeroom.");

        var volume = cmd.Units * good.UnitVolume;
        if (volume > CaravanMath.FreeVolume(state.Caravan, world) + 1e-9)
            return CommandResult.Fail("The hold has no room for that.");

        if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot))
            state.Caravan.Cargo[good.Id] = lot = new CargoLot();

        var costBasis = (long)Math.Round(stored.AverageCost * cmd.Units);
        lot.Add(cmd.Units, costBasis, stored.Quality);

        stored.Units -= cmd.Units;
        stored.TotalCost = Math.Max(0, stored.TotalCost - costBasis);
        if (stored.Units == 0) warehouse.Stock.Remove(good.Id);

        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Trade,
                $"Withdrew {cmd.Units:N0} {good.Name} from the {world.City(cityId).Name} storeroom.")
        });
    }

    private static CommandResult SetWarehousePrice(
        GameState state, WorldData world, string goodId, long price, bool sell)
    {
        var parked = RequireParkedCity(state, out var cityId);
        if (parked is not null) return parked;
        if (!state.Warehouses.TryGetValue(cityId, out var warehouse))
            return CommandResult.Fail("The house does not rent a storeroom here.");
        if (price < 0) return CommandResult.Fail("Price cannot be negative.");
        if (!world.GoodsById.TryGetValue(goodId, out var good))
            return CommandResult.Fail($"No such commodity '{goodId}'.");

        var book = sell ? warehouse.AutoSellPrice : warehouse.AutoProcurePrice;
        if (price == 0) book.Remove(good.Id);
        else book[good.Id] = price;

        var verb = sell ? "auto-sell" : "auto-procure";
        var detail = price == 0 ? "order cleared" : $"at {price:N0} cr";
        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Info,
                $"{world.City(cityId).Name} storeroom {verb} {good.Name}: {detail}.")
        });
    }
}
