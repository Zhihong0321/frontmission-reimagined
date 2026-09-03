using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using Xunit;

namespace MechaTrader.Core.Tests;

public class CommandTests
{
    private const ulong Seed = 4242;

    private static Game NewGame() => Game.New(TestWorld.Shipping, Seed);

    // Derived from content rather than hardcoded, so retuning the opening city does not
    // silently invalidate the suite.
    private static readonly string Start = TestWorld.Shipping.Config.StartCityId;

    private static readonly string Neighbour =
        TestWorld.Shipping.Routes.From(Start)[0].Other(Start);

    private static readonly string NoRoadTo = TestWorld.Shipping.Cities
        .First(c => c.Id != Start && !TestWorld.Shipping.Routes.AreAdjacent(Start, c.Id)).Id;

    [Fact]
    public void BuyingMovesCashCargoAndLocalStock()
    {
        var game = NewGame();
        var cashBefore = game.State.Cash;
        var stockBefore = game.State.ShelfOf(Start, "steel");

        var result = game.Apply(new BuyCommand("steel", 20));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(20, game.State.Caravan.Held("steel"));
        Assert.True(game.State.Cash < cashBefore, "Buying should cost money.");
        Assert.True(game.State.ShelfOf(Start, "steel") < stockBefore, "Buying should drain the shelf.");
    }

    [Fact]
    public void SellingReturnsCashAndClearsTheLot()
    {
        var game = NewGame();
        game.Apply(new BuyCommand("steel", 20));

        var cashBefore = game.State.Cash;
        var result = game.Apply(new SellCommand("steel", 20));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(0, game.State.Caravan.Held("steel"));
        Assert.False(game.State.Caravan.Cargo.ContainsKey("steel"));
        Assert.True(game.State.Cash > cashBefore, "Selling should return money.");
    }

    [Fact]
    public void BuyingAndSellingInPlaceLosesMoneyToTheSpread()
    {
        var game = NewGame();
        var before = game.State.Cash;

        game.Apply(new BuyCommand("steel", 30));
        game.Apply(new SellCommand("steel", 30));

        Assert.True(game.State.Cash < before,
            "A round trip in one city must lose money, or the spread is not doing its job.");
    }

    [Fact]
    public void BuyIsRejectedWithoutEnoughCash()
    {
        var game = NewGame();
        var world = TestWorld.Shipping;

        // Optics are small but expensive: the hold would take them and the shelf holds
        // them, but the wallet will not. Sized to the shelf rather than hardcoded, so
        // this keeps testing the cash gate rather than tripping the shelf one.
        var good = world.Good("optics");
        var units = Economy.UnitsOnTheShelf(game.State.StockOf(Start, good.Id), world.Config.Economy);

        var result = game.Apply(new BuyCommand(good.Id, units));

        Assert.False(result.Ok);
        Assert.Contains("credits", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuyIsRejectedForMoreThanTheCityHasOnTheShelf()
    {
        var game = NewGame();
        var world = TestWorld.Shipping;

        var good = world.Good("optics");
        var onTheShelf = Economy.UnitsOnTheShelf(game.State.StockOf(Start, good.Id), world.Config.Economy);

        var result = game.Apply(new BuyCommand(good.Id, onTheShelf + 1));

        Assert.False(result.Ok);
        Assert.Contains("shelf", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SoldGoodsLandInTheCityIntakeAndNotOnItsShelf()
    {
        // The rule that makes a sell/buy-back loop impossible rather than merely
        // unprofitable: what the convoy unloads is not what the city is selling.
        var game = NewGame();

        game.Apply(new BuyCommand("steel", 20));
        var shelfAfterBuying = game.State.ShelfOf(Start, "steel");

        game.Apply(new SellCommand("steel", 20));
        var stock = game.State.StockOf(Start, "steel");

        Assert.Equal(shelfAfterBuying, stock.Out);
        Assert.Equal(20.0, stock.In, 6);
        Assert.Equal(stock.Out + stock.In, stock.Total, 6);
    }

    [Fact]
    public void BuyIsRejectedWithoutEnoughHoldSpace()
    {
        var game = NewGame();

        var result = game.Apply(new BuyCommand("scrap", 500));

        Assert.False(result.Ok);
        Assert.Contains("hold", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SellIsRejectedWithoutTheGoods()
    {
        var game = NewGame();

        var result = game.Apply(new SellCommand("steel", 5));

        Assert.False(result.Ok);
        Assert.Contains("hold", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectedCommandsLeaveStateUntouched()
    {
        var game = NewGame();

        var cash = game.State.Cash;
        var stock = game.State.StockOf(Start, "scrap");

        var day = game.State.Day;

        Assert.False(game.Apply(new BuyCommand("scrap", 500)).Ok);
        Assert.False(game.Apply(new SellCommand("scrap", 1)).Ok);
        Assert.False(game.Apply(new BuyCommand("nonsense", 1)).Ok);
        Assert.False(game.Apply(new DepartCommand("atlantis")).Ok);
        Assert.False(game.Apply(new CityFavorCommand("kneel")).Ok);
        Assert.False(game.Apply(new AssignCrewCommand("nobody", "trading")).Ok);

        Assert.Equal(cash, game.State.Cash);
        Assert.Equal(stock, game.State.StockOf(Start, "scrap"));
        Assert.Equal(day, game.State.Day);
        Assert.Empty(game.State.Caravan.Cargo);
    }

    [Fact]
    public void AverageCostSurvivesPartialSales()
    {
        var game = NewGame();

        game.Apply(new BuyCommand("steel", 20));
        game.Apply(new BuyCommand("steel", 20));

        var lot = game.State.Caravan.Cargo["steel"];
        Assert.Equal(40, lot.Units);

        var averageBefore = lot.AverageCost;
        game.Apply(new SellCommand("steel", 15));

        Assert.Equal(25, lot.Units);
        Assert.True(Math.Abs(lot.AverageCost - averageBefore) < 1.0,
            $"Average cost drifted from {averageBefore:0.00} to {lot.AverageCost:0.00} on a partial sale.");
    }

    [Fact]
    public void TradingIsRejectedWhileOnTheRoad()
    {
        var game = NewGame();
        Assert.True(game.Apply(new DepartCommand(Neighbour)).Ok);

        Assert.False(game.Apply(new BuyCommand("steel", 1)).Ok);
        Assert.False(game.Apply(new SellCommand("steel", 1)).Ok);
        Assert.False(game.Apply(new BuyTruckCommand("kite")).Ok);
        Assert.False(game.Apply(new CityFavorCommand("donate")).Ok);
    }

    [Fact]
    public void DepartIsRejectedForAnUnknownDestination()
    {
        var game = NewGame();

        var result = game.Apply(new DepartCommand("atlantis"));

        Assert.False(result.Ok);
        Assert.Contains("destination", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DepartReachesACityThatIsNotAdjacentByRoad()
    {
        var game = NewGame();

        var result = game.Apply(new DepartCommand(NoRoadTo));

        Assert.True(result.Ok, result.Error);
        Assert.NotNull(game.State.Caravan.Travel);
        Assert.Equal(NoRoadTo, game.State.Caravan.Travel!.ToId);
    }

    [Fact]
    public void ConvoyArrivesExactlyOnTheComputedDay()
    {
        var game = NewGame();
        var world = TestWorld.Shipping;

        var from = MapMath.Position(game.State, world);
        var to = world.Map.CellOfCity(Neighbour);
        var plan = MapMath.Pathfind(game.State.Caravan, world, from, to);
        Assert.NotNull(plan);

        var startDay = game.State.Day;
        Assert.True(game.Apply(new DepartCommand(Neighbour)).Ok);
        Assert.Null(game.State.Caravan.LocationId);
        var expected = game.State.Caravan.Travel!.TotalDays;

        if (expected > 1)
        {
            game.Apply(new WaitCommand(expected - 1));
            Assert.Null(game.State.Caravan.LocationId);
        }

        game.Apply(new WaitCommand(1));
        Assert.Equal(Neighbour, game.State.Caravan.LocationId);
        Assert.Null(game.State.Caravan.Travel);
        Assert.Equal(startDay + expected, game.State.Day);
    }

    [Fact]
    public void UpkeepIsChargedEveryDay()
    {
        var game = NewGame();
        var world = TestWorld.Shipping;

        var upkeep = CaravanMath.DailyUpkeep(game.State.Caravan, world);
        var before = game.State.Cash;

        game.Apply(new WaitCommand(10));

        Assert.Equal(before - (long)Math.Round(upkeep) * 10, game.State.Cash);
    }

    [Fact]
    public void BuyingATruckAddsCapacityAndCostsCash()
    {
        var game = NewGame();
        var world = TestWorld.Shipping;

        var capacityBefore = CaravanMath.Capacity(game.State.Caravan, world);
        var cashBefore = game.State.Cash;

        var result = game.Apply(new BuyTruckCommand("kite"));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(cashBefore - world.Truck("kite").Price, game.State.Cash);
        Assert.True(CaravanMath.Capacity(game.State.Caravan, world) > capacityBefore);
    }

    [Fact]
    public void ConvoyMovesAtThePaceOfItsSlowestTruck()
    {
        var game = NewGame();
        var world = TestWorld.Shipping;

        game.Apply(new BuyTruckCommand("kite")); // faster than the starting Mule

        Assert.Equal(world.Truck("mule").SpeedKmPerDay, CaravanMath.SpeedKmPerDay(game.State.Caravan, world));
    }

    [Fact]
    public void WaitRejectsNonPositiveAndAbsurdDurations()
    {
        var game = NewGame();

        Assert.False(game.Apply(new WaitCommand(0)).Ok);
        Assert.False(game.Apply(new WaitCommand(-3)).Ok);
        Assert.False(game.Apply(new WaitCommand(9999)).Ok);
    }
}
