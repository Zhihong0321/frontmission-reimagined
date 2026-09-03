using System.Text.Json.Nodes;
using MechaTrader.Content;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Events;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// The crew's parked-city brief: which goods in the hold clear the configured fuel
/// line if sold here. Built as a pure read, so these tests seed the hold directly.
/// </summary>
public class CrewBriefTests
{
    private const ulong Seed = 5150;
    private static readonly WorldData World = TestWorld.Shipping;

    private static Game NewGame() => Game.New(World, Seed);

    private static string Start => World.Config.StartCityId;

    /// <summary>What the convoy would actually net per unit today, in the named city.</summary>
    private static double SellUnit(Game game, string cityId, string goodId)
    {
        var city = World.City(cityId);
        var good = World.Good(goodId);
        var stock = game.State.StockOf(cityId, goodId);
        var terms = CrewMath.Terms(game.State.Caravan, World, good.Category);
        var eventMult = WorldEvents.PriceMultiplier(game.State, World, cityId, goodId);
        return Economy.SellUnitPrice(good, city.Market[goodId], stock, World.Config.Economy, terms, eventMult)
               * QualityMath.SellMultiplier(70.0, World.Quality);
    }

    [Fact]
    public void ListsGoodsThatClearTheFuelLine()
    {
        var game = NewGame();
        var sell = SellUnit(game, Start, "optics");

        // Costed well under the local offer, so the margin is far above the 3.5% line.
        game.State.Caravan.Cargo["optics"] = new CargoLot { Units = 10, TotalCost = (long)(sell * 10 * 0.9) };

        var brief = game.View().CrewBrief;

        Assert.Equal(0.035, brief.MinMargin, 3);
        var row = brief.Rows.Single(r => r.GoodId == "optics");
        Assert.Equal(10, row.Units);
        Assert.True(row.MarginPct > 3.5, $"margin {row.MarginPct} should clear the 3.5% line");
        Assert.True(row.Profit > 0);
    }

    [Fact]
    public void KeepsGoodsBelowTheFuelLineOffTheList()
    {
        var game = NewGame();
        var sell = SellUnit(game, Start, "copper");

        // Costed just under the offer: the margin is ~1%, below the 3.5% line.
        game.State.Caravan.Cargo["copper"] = new CargoLot { Units = 10, TotalCost = (long)(sell * 10 * 0.99) };

        var brief = game.View().CrewBrief;

        Assert.DoesNotContain(brief.Rows, r => r.GoodId == "copper");
    }

    [Fact]
    public void MinedLotAlwaysClears()
    {
        var game = NewGame();
        game.State.Caravan.Cargo["scrap"] = new CargoLot { Units = 5, TotalCost = 0 };

        var brief = game.View().CrewBrief;

        var row = brief.Rows.Single(r => r.GoodId == "scrap");
        Assert.Null(row.MarginPct);
        Assert.True(row.Profit > 0);
    }

    [Fact]
    public void EmptyOnTheRoad()
    {
        var game = NewGame();
        game.State.Caravan.Cargo["scrap"] = new CargoLot { Units = 5, TotalCost = 0 };

        var neighbour = World.Routes.From(Start)[0].Other(Start);
        game.Apply(new DepartCommand(neighbour));

        Assert.True(game.State.Caravan.IsTraveling);
        Assert.Empty(game.View().CrewBrief.Rows);
    }

    [Fact]
    public void RowsSortedMostProfitableFirst()
    {
        var game = NewGame();
        foreach (var goodId in new[] { "optics", "copper", "steel" })
        {
            var sell = SellUnit(game, Start, goodId);
            game.State.Caravan.Cargo[goodId] = new CargoLot { Units = 4, TotalCost = (long)(sell * 4 * 0.9) };
        }

        var brief = game.View().CrewBrief;

        Assert.Equal(3, brief.Rows.Count);
        Assert.Equal(
            brief.Rows.OrderByDescending(r => r.MarginPct ?? double.MaxValue).ToList(),
            brief.Rows.ToList());
    }

    [Fact]
    public void ArrivalPrintsTheCrewReadToTheWire()
    {
        var game = NewGame();
        var neighbour = World.Routes.From(Start)[0].Other(Start);

        // Costed at a token amount: whatever the neighbour pays on arrival clears 3.5%.
        game.State.Caravan.Cargo["optics"] = new CargoLot { Units = 5, TotalCost = 5 };

        Assert.True(game.Apply(new DepartCommand(neighbour)).Ok);
        var result = game.Apply(new WaitCommand(game.State.Caravan.Travel!.DaysRemaining));

        Assert.True(result.Ok, result.Error);
        Assert.Contains(result.Events,
            e => e.Kind == GameEventKind.Crew && e.Message.Contains("read the market"));
    }

    /// <summary>The same content, but with the crew brief toggled off.</summary>
    private static WorldData WorldWithBriefDisabled()
    {
        var files = new Dictionary<string, string>(ContentLoader.ReadAll(
            Path.Combine(TestWorld.RepositoryRoot(), "data")));

        var node = JsonNode.Parse(files["config"])!;
        node["crewBrief"]!["enabled"] = false;
        files["config"] = node.ToJsonString();

        return WorldLoader.Load(files);
    }

    [Fact]
    public void ToggleOffSilencesTheBrief()
    {
        var world = WorldWithBriefDisabled();
        var game = Game.New(world, Seed);

        game.State.Caravan.Cargo["scrap"] = new CargoLot { Units = 5, TotalCost = 0 };

        Assert.False(world.Config.CrewBrief.Enabled);
        Assert.Empty(game.View().CrewBrief.Rows);
    }
}