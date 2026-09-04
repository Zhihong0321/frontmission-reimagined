using System.Text.Json;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// The truck station: vehicles are instances, fittings sit on one vehicle, the yard
/// buys back at the resale fraction, and the convoy can never sell itself immobile.
/// </summary>
public class StationTests
{
    private const ulong Seed = 616;

    private static readonly WorldData World = TestWorld.Shipping;

    private static Game NewGame() => Game.New(World, Seed);

    [Fact]
    public void StartingTrucksAreInstancesWithStableIds()
    {
        var game = NewGame();
        Assert.Equal(World.Config.StartTruckIds.Count, game.State.Caravan.Trucks.Count);
        Assert.All(game.State.Caravan.Trucks, t => Assert.False(string.IsNullOrWhiteSpace(t.Id)));
        Assert.Equal(game.State.Caravan.Trucks.Count, game.State.Caravan.Trucks.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void AFittingChangesOnlyTheTruckItIsOn()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        game.Apply(new BuyTruckCommand(World.Config.StartTruckIds[0]));

        var first = game.State.Caravan.Trucks[0];
        var second = game.State.Caravan.Trucks[1];
        var upgrade = World.TruckUpgrades.First(u => u.CapacityBonus > 0);
        var before = CaravanMath.Spec(second, World);
        var cashBefore = game.State.Cash;

        var result = game.Apply(new UpgradeTruckCommand(first.Id, upgrade.Id));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(cashBefore - upgrade.Price, game.State.Cash);
        Assert.Equal(before, CaravanMath.Spec(second, World));
        Assert.Equal(World.Truck(first.TypeId).Capacity + upgrade.CapacityBonus, CaravanMath.Spec(first, World).Capacity, 6);
        Assert.Equal(CaravanMath.Spec(first, World).Capacity + before.Capacity, CaravanMath.Capacity(game.State.Caravan, World), 6);
    }

    [Fact]
    public void TheSameFittingCannotBeFittedTwiceAndTheWrongKindIsRefused()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var truck = game.State.Caravan.Trucks[0];
        var kind = World.Truck(truck.TypeId).EffectiveKind;
        var fits = World.TruckUpgrades.First(u => u.Fits(kind));

        Assert.True(game.Apply(new UpgradeTruckCommand(truck.Id, fits.Id)).Ok);

        var before = JsonSerializer.Serialize(game.State);
        Assert.False(game.Apply(new UpgradeTruckCommand(truck.Id, fits.Id)).Ok);
        Assert.Equal(before, JsonSerializer.Serialize(game.State));

        var wrong = World.TruckUpgrades.FirstOrDefault(u => !u.Fits(kind));
        if (wrong is not null)
        {
            Assert.False(game.Apply(new UpgradeTruckCommand(truck.Id, wrong.Id)).Ok);
            Assert.Equal(before, JsonSerializer.Serialize(game.State));
        }
    }

    [Fact]
    public void SellingATruckPaysTheResaleFractionOfItAndItsFittings()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var type = World.Trucks.First(t => t.EffectiveKind == "truck");
        game.Apply(new BuyTruckCommand(type.Id));
        var bought = game.State.Caravan.Trucks[^1];
        var upgrade = World.TruckUpgrades.First(u => u.Fits(type.EffectiveKind));
        game.Apply(new UpgradeTruckCommand(bought.Id, upgrade.Id));

        var expected = (long)Math.Round((type.Price + upgrade.Price) * World.ResaleFraction);
        var cashBefore = game.State.Cash;

        var result = game.Apply(new SellTruckCommand(bought.Id));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(cashBefore + expected, game.State.Cash);
        Assert.Null(game.State.Truck(bought.Id));
        Assert.Equal(expected, CaravanMath.ResaleValue(new TruckState { TypeId = type.Id, UpgradeIds = { upgrade.Id } }, World));
    }

    [Fact]
    public void TheLastVehicleCannotBeSold()
    {
        var game = NewGame();
        Assert.Single(game.State.Caravan.Trucks);
        var only = game.State.Caravan.Trucks[0];

        var before = JsonSerializer.Serialize(game.State);
        var result = game.Apply(new SellTruckCommand(only.Id));

        Assert.False(result.Ok);
        Assert.Equal(before, JsonSerializer.Serialize(game.State));
        Assert.False(game.View().Station.Fleet.Single().CanSell);
    }

    [Fact]
    public void ATruckStillCarryingTheHoldCannotBeSold()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var big = World.Trucks.OrderByDescending(t => t.Capacity).First(t => t.EffectiveKind == "truck");
        game.Apply(new BuyTruckCommand(big.Id));
        var truck = game.State.Caravan.Trucks[^1];

        var good = World.Goods[0];
        var units = (int)Math.Floor(CaravanMath.Capacity(game.State.Caravan, World) / good.UnitVolume) - 1;
        game.State.Caravan.Cargo[good.Id] = new CargoLot { Units = units, TotalCost = units, Quality = 70 };

        var result = game.Apply(new SellTruckCommand(truck.Id));
        Assert.False(result.Ok);
        Assert.Contains("cargo", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StationTradesOnlyWhileParked()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var truck = game.State.Caravan.Trucks[0];
        var upgrade = World.TruckUpgrades.First(u => u.Fits(World.Truck(truck.TypeId).EffectiveKind));
        var away = World.Routes.From(World.Config.StartCityId)[0].Other(World.Config.StartCityId);
        Assert.True(game.Apply(new DepartCommand(away)).Ok);

        Assert.False(game.Apply(new UpgradeTruckCommand(truck.Id, upgrade.Id)).Ok);
        Assert.False(game.Apply(new SellTruckCommand(truck.Id)).Ok);
        Assert.False(game.View().Station.Open);
    }

    [Fact]
    public void FleetViewWordsEveryFittingAndBlocker()
    {
        var view = NewGame().View();
        var fleet = view.Station.Fleet;
        Assert.NotEmpty(fleet);
        Assert.All(fleet, t =>
        {
            Assert.Equal(World.TruckUpgrades.Count, t.Fittings.Count);
            Assert.All(t.Fittings, f => Assert.False(string.IsNullOrWhiteSpace(f.EffectText)));
            if (!t.CanSell) Assert.False(string.IsNullOrWhiteSpace(t.SellBlocker));
        });
    }

    [Fact]
    public void TruckStateSurvivesASave()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var truck = game.State.Caravan.Trucks[0];
        var upgrade = World.TruckUpgrades.First(u => u.Fits(World.Truck(truck.TypeId).EffectiveKind));
        game.Apply(new UpgradeTruckCommand(truck.Id, upgrade.Id));

        var restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(game.State))!;
        Assert.Equal(CaravanMath.Capacity(game.State.Caravan, World), CaravanMath.Capacity(restored.Caravan, World), 6);
        Assert.Equal(game.State.Caravan.TruckSerial, restored.Caravan.TruckSerial);
    }
}
