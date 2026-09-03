using MechaTrader.Core;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using Xunit;

namespace MechaTrader.Core.Tests;

public class WarehouseTests
{
    private static readonly World.WorldData World = TestWorld.Shipping;

    [Fact]
    public void RentingAStoreroomCostsTheAuthoredFee()
    {
        var game = Game.New(World, 11);
        var fee = World.Config.Warehouse.RentCost;
        var cash = game.State.Cash;

        var result = game.Apply(new RentWarehouseCommand());
        Assert.True(result.Ok, result.Error);
        Assert.Equal(cash - fee, game.State.Cash);
        Assert.True(game.State.Warehouses.ContainsKey(World.Config.StartCityId));
    }

    [Fact]
    public void ASecondRentInTheSameCityIsRefusedAndLeavesStateUntouched()
    {
        var game = Game.New(World, 11);
        Assert.True(game.Apply(new RentWarehouseCommand()).Ok);

        var before = System.Text.Json.JsonSerializer.Serialize(game.State);
        var result = game.Apply(new RentWarehouseCommand());
        Assert.False(result.Ok);
        var after = System.Text.Json.JsonSerializer.Serialize(game.State);
        Assert.Equal(before, after);
    }

    [Fact]
    public void DepositThenWithdrawPreservesQualityAndUnits()
    {
        var game = Game.New(World, 13);
        var good = World.Goods[0];
        Assert.True(game.Apply(new BuyCommand(good.Id, 5)).Ok);
        Assert.True(game.Apply(new RentWarehouseCommand()).Ok);

        var lot = game.State.Caravan.Cargo[good.Id];
        var units = lot.Units;
        var quality = lot.Quality;

        Assert.True(game.Apply(new WarehouseDepositCommand(good.Id, units)).Ok);
        Assert.False(game.State.Caravan.Cargo.ContainsKey(good.Id));
        Assert.Equal(units, game.State.Warehouses[World.Config.StartCityId].Held(good.Id));

        Assert.True(game.Apply(new WarehouseWithdrawCommand(good.Id, units)).Ok);
        var back = game.State.Caravan.Cargo[good.Id];
        Assert.Equal(units, back.Units);
        Assert.Equal(quality, back.Quality, 6);
    }

    [Fact]
    public void AutoSellAtALowAskClearsTheRoomOnWait()
    {
        var game = Game.New(World, 17);
        var good = World.Goods[0];
        Assert.True(game.Apply(new BuyCommand(good.Id, 4)).Ok);
        Assert.True(game.Apply(new RentWarehouseCommand()).Ok);
        Assert.True(game.Apply(new WarehouseDepositCommand(good.Id, 4)).Ok);
        Assert.True(game.Apply(new SetWarehouseSellCommand(good.Id, 1)).Ok);

        var result = game.Apply(new WaitCommand(1));
        Assert.True(result.Ok, result.Error);
        Assert.Equal(0, game.State.Warehouses[World.Config.StartCityId].Held(good.Id));
    }
}
