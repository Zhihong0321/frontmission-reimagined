using System.Text.Json;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// Standing is how the player relates to a city. These tests guard that courtship is
/// an honest transaction, that permits and the reserved shelf fall out of standing
/// rather than being authored twice, and that aid never cheapens the shelf it is
/// trying to help.
/// </summary>
public class StandingTests
{
    private const ulong Seed = 4242;

    private static readonly World.WorldData World = TestWorld.Shipping;
    private static readonly string Start = World.Config.StartCityId;

    private static Game NewGame() => Game.New(World, Seed);

    private static FavorActionDef Action(string id)
        => World.Standing.Action(id) ?? throw new InvalidOperationException($"No action '{id}'.");

    [Fact]
    public void ANewRunHasNoStandingAndNoPermits()
    {
        var game = NewGame();

        Assert.Equal(0, game.State.StandingOf(Start));
        Assert.Empty(game.State.CityPermits);
        Assert.All(World.Standing.Permits, p => Assert.False(game.State.HasPermit(Start, p.Id)));
    }

    [Fact]
    public void EveryCityArrivesWithAGovernorReadyToPrint()
    {
        var location = Assert.IsType<View.LocationView>(NewGame().View().Location);
        var standing = location.Standing;

        Assert.False(string.IsNullOrWhiteSpace(standing.GovernorName));
        Assert.False(string.IsNullOrWhiteSpace(standing.GovernorTitle));
        Assert.Equal(0, standing.Value);
        Assert.Equal(World.Standing.Max, standing.Max);
        Assert.Equal(World.Standing.Segments.Count, standing.Segments.Count);
        Assert.Equal(World.Tiers.Count, standing.TierGates.Count);
        Assert.False(string.IsNullOrWhiteSpace(standing.Rank));
        Assert.InRange(standing.Fill, 0.0, 1.0);
        Assert.Equal(World.Standing.Actions.Count, standing.Actions.Count);
        Assert.Equal(World.Standing.Permits.Count, standing.Permits.Count);
        Assert.All(standing.Permits, p => Assert.False(p.Granted));
        Assert.All(standing.Actions, a => Assert.False(string.IsNullOrWhiteSpace(a.EffectText)));
    }

    [Fact]
    public void DonateCostsTheAuthoredFeeAndRaisesStanding()
    {
        var game = NewGame();
        var donate = Action("donate");
        var cashBefore = game.State.Cash;
        var growthBefore = CityStats.Vital(game.State, World.City(Start), "growth");

        var result = game.Apply(new CityFavorCommand("donate"));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(cashBefore - donate.Cost, game.State.Cash);
        Assert.Equal(donate.Standing, game.State.StandingOf(Start));
        Assert.Equal(growthBefore, CityStats.Vital(game.State, World.City(Start), "growth"));
        Assert.All(World.Goods, g =>
            Assert.Equal(0, game.State.StockOf(Start, g.Id).In));
    }

    [Fact]
    public void InvestMovesTheNamedVital()
    {
        var game = NewGame();
        var invest = Action("invest");
        var city = World.City(Start);
        var before = CityStats.Vital(game.State, city, invest.VitalId);

        var result = game.Apply(new CityFavorCommand("invest"));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(invest.Standing, game.State.StandingOf(Start));
        Assert.Equal(before + invest.VitalDelta, CityStats.Vital(game.State, city, invest.VitalId), 6);
    }

    [Fact]
    public void AidFillsIntakeOfTheShortestSupplyAndLeavesTheShelfAlone()
    {
        var game = NewGame();
        var aid = Action("aid");
        var city = World.City(Start);

        var weakest = World.CityStats.Supplies
            .Select(s => (Def: s, Reading: CityStats.Supply(game.State, World, city, s)))
            .OrderBy(x => x.Reading.Index)
            .First();

        var shelfBefore = weakest.Def.Goods.ToDictionary(id => id, id => game.State.ShelfOf(Start, id));

        var result = game.Apply(new CityFavorCommand("aid"));

        Assert.True(result.Ok, result.Error);

        foreach (var goodId in weakest.Def.Goods)
        {
            Assert.Equal(shelfBefore[goodId], game.State.ShelfOf(Start, goodId));
            Assert.Equal(aid.StockPerGood, game.State.StockOf(Start, goodId).In, 6);
        }
    }

    [Fact]
    public void PermitsGrantAtThresholdAndStick()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var donate = Action("donate");
        var shop = World.Standing.Permits.First(p => p.Id == "shop");

        var times = (int)Math.Ceiling(shop.Standing / donate.Standing);
        for (var i = 0; i < times; i++)
        {
            var paid = game.Apply(new CityFavorCommand("donate"));
            Assert.True(paid.Ok, paid.Error);
        }

        Assert.True(game.State.HasPermit(Start, shop.Id));
        Assert.True(game.View().Location!.Standing.Permits.First(p => p.Id == shop.Id).Granted);

        // Standing is not lowered by anything yet; the grant is stored as an id so a
        // later drop would not take the paper back.
        Assert.Contains(shop.Id, game.State.CityPermits[Start]);
    }

    [Fact]
    public void ReservedShareIsDerivedFromStandingNeverStored()
    {
        var game = NewGame();
        game.Apply(new CityFavorCommand("donate"));

        var standing = game.State.StandingOf(Start);
        var expected = Standing.ReservedRatio(World.Standing, standing);
        var view = game.View().Location!.Standing;

        Assert.Equal(expected, view.ReservedRatio, 4);
        Assert.DoesNotContain("reserved", JsonSerializer.Serialize(game.State), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePlayerCanStillBuyTheReservedShelf()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var donate = Action("donate");
        var shop = World.Standing.Permits.First(p => p.Id == "shop");
        var times = (int)Math.Ceiling(shop.Standing / donate.Standing);
        for (var i = 0; i < times; i++) game.Apply(new CityFavorCommand("donate"));

        var good = World.Goods.OrderBy(g => g.BasePrice).First();
        var reserved = game.View().Market.First(r => r.GoodId == good.Id).Reserved;

        Assert.True(reserved > 0, "Standing at the shop threshold should reserve some of the shelf.");

        var result = game.Apply(new BuyCommand(good.Id, 1));
        Assert.True(result.Ok, result.Error);
        Assert.Equal(1, game.State.Caravan.Held(good.Id));
    }

    [Fact]
    public void FavorIsRejectedWhenThePlayerCannotAffordIt()
    {
        var game = NewGame();
        game.State.Cash = 0;

        var before = JsonSerializer.Serialize(game.State);
        var result = game.Apply(new CityFavorCommand("donate"));

        Assert.False(result.Ok);
        Assert.Equal(before, JsonSerializer.Serialize(game.State));
    }

    [Fact]
    public void DonateAtMaximumStandingIsRejected()
    {
        var game = NewGame();
        var donate = Action("donate");
        game.State.SetStanding(Start, World.Standing.SegmentOr(donate.SegmentId), World.Standing.SegmentMax);
        game.State.Cash = 100_000;

        var before = JsonSerializer.Serialize(game.State);
        var result = game.Apply(new CityFavorCommand("donate"));

        Assert.False(result.Ok);
        Assert.Contains("highest regard", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, JsonSerializer.Serialize(game.State));
    }

    [Fact]
    public void ReadingTheCityPageDoesNotAdvanceStanding()
    {
        var game = NewGame();
        var before = JsonSerializer.Serialize(game.State);

        for (var i = 0; i < 3; i++)
        {
            var view = game.View();
            Assert.NotNull(view.Location?.Standing);
        }

        Assert.Equal(before, JsonSerializer.Serialize(game.State));
    }

    [Fact]
    public void StandingSurvivesASaveLoadRoundTrip()
    {
        var game = NewGame();
        game.Apply(new CityFavorCommand("donate"));
        game.Apply(new CityFavorCommand("invest"));

        var saved = JsonSerializer.Serialize(game.State);
        var restored = JsonSerializer.Deserialize<GameState>(saved)!;
        var resumed = Game.Resume(World, restored);

        Assert.Equal(game.State.StandingOf(Start), resumed.State.StandingOf(Start));
        Assert.Equal(
            JsonSerializer.Serialize(game.State.CityPermits),
            JsonSerializer.Serialize(resumed.State.CityPermits));

        resumed.Apply(new CityFavorCommand("donate"));
        game.Apply(new CityFavorCommand("donate"));

        Assert.Equal(JsonSerializer.Serialize(game.State), JsonSerializer.Serialize(resumed.State));
    }

    [Fact]
    public void FavorDoesNotTouchTheRng()
    {
        var game = NewGame();
        var rng = game.State.RngState;

        game.Apply(new CityFavorCommand("donate"));
        game.Apply(new CityFavorCommand("invest"));
        game.Apply(new CityFavorCommand("aid"));

        Assert.Equal(rng, game.State.RngState);
    }
}
