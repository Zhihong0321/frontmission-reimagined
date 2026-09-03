using System.Text.Json;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Events;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// World events move prices, city stats and stock without the front-end inventing
/// any of it. These tests hold the split: overlays are derived, stock shocks write
/// once, firing consumes the RNG, and reading the city page does none of that.
/// </summary>
public class EventTests
{
    private const ulong Seed = 4242;

    private static readonly WorldData World = TestWorld.Shipping;
    private static readonly string Start = World.Config.StartCityId;

    private static Game NewGame() => Game.New(World, Seed);

    [Fact]
    public void ANewRunHasNoEvents()
    {
        var game = NewGame();

        Assert.Empty(game.State.ActiveEvents);
        Assert.Empty(game.View().Location!.News);
        Assert.Empty(game.View().EventCityIds);
        Assert.All(World.Goods, g =>
            Assert.Equal(1.0, WorldEvents.PriceMultiplier(game.State, World, Start, g.Id)));
    }

    [Fact]
    public void APriceEventMovesTheQuoteAndLeavesTheStoredVitalAlone()
    {
        var game = NewGame();
        var def = World.Events.ById("trade-fair")
                  ?? throw new InvalidOperationException("shipping content lost trade-fair.");
        var goodId = def.Goods[0];
        var city = World.City(Start);
        var good = World.Good(goodId);
        var eco = World.Config.Economy;
        var stock = game.State.StockOf(Start, goodId);
        var terms = CrewMath.Terms(game.State.Caravan, World);

        var buyBefore = Economy.BuyUnitPrice(good, city.Market[goodId], stock, eco, terms);
        var peaceStored = CityStats.Vital(game.State, city, "peace");

        WorldEvents.Start(game.State, World, def, Start, game.State.Day);

        Assert.Equal(def.PriceMult, WorldEvents.PriceMultiplier(game.State, World, Start, goodId), 6);
        Assert.True(Economy.BuyUnitPrice(
            good, city.Market[goodId], stock, eco, terms,
            WorldEvents.PriceMultiplier(game.State, World, Start, goodId)) > buyBefore);

        Assert.Equal(peaceStored, CityStats.Vital(game.State, city, "peace"), 6);
        Assert.Equal(
            peaceStored + def.VitalDeltas["peace"],
            CityStats.Vital(game.State, World, city, "peace"), 6);
    }

    [Fact]
    public void AStockShockWritesTheShelfAndExpireDoesNotPutTheGoodsBack()
    {
        var game = NewGame();
        var def = World.Events.ById("cave-in")
                  ?? throw new InvalidOperationException("shipping content lost cave-in.");
        var city = World.Cities.First(c => c.Industries.Contains("mining"));
        var goodId = def.Goods[0];
        var shelfBefore = game.State.ShelfOf(city.Id, goodId);

        WorldEvents.Start(game.State, World, def, city.Id, game.State.Day);

        var shocked = game.State.ShelfOf(city.Id, goodId);
        Assert.True(shocked < shelfBefore);

        game.State.ActiveEvents[0].EndDay = game.State.Day;
        var events = new List<GameEvent>();
        WorldEvents.ExpireDue(game.State, World, events);

        Assert.Empty(game.State.ActiveEvents);
        Assert.Equal(shocked, game.State.ShelfOf(city.Id, goodId), 6);
        Assert.Contains(events, e => e.Kind == GameEventKind.World);
    }

    [Fact]
    public void ExpiringAVitalOverlayRestoresTheReading()
    {
        var game = NewGame();
        var def = World.Events.ById("street-unrest")
                  ?? throw new InvalidOperationException("shipping content lost street-unrest.");
        var city = World.City(Start);
        var stored = CityStats.Vital(game.State, city, "peace");

        WorldEvents.Start(game.State, World, def, Start, game.State.Day);
        Assert.True(CityStats.Vital(game.State, World, city, "peace") < stored);

        game.State.ActiveEvents[0].EndDay = game.State.Day;
        WorldEvents.ExpireDue(game.State, World, new List<GameEvent>());

        Assert.Equal(stored, CityStats.Vital(game.State, World, city, "peace"), 6);
    }

    [Fact]
    public void TheCityWirePrintsTheActiveEvent()
    {
        var game = NewGame();
        var def = World.Events.ById("trade-fair")!;
        WorldEvents.Start(game.State, World, def, Start, game.State.Day);

        var news = game.View().Location!.News;
        Assert.Single(news);
        Assert.Contains(World.City(Start).Name, news[0].Headline);
        Assert.Equal(def.DurationDays, news[0].DaysLeft);
        Assert.False(string.IsNullOrWhiteSpace(news[0].Tone));
        Assert.Contains(Start, game.View().EventCityIds);

        var row = game.View().Market.First(m => m.GoodId == def.Goods[0]);
        Assert.False(string.IsNullOrWhiteSpace(row.EventHint));
    }

    [Fact]
    public void AGlobalEventTouchesEveryCity()
    {
        var game = NewGame();
        var def = World.Events.ById("cell-scare")
                  ?? throw new InvalidOperationException("shipping content lost cell-scare.");

        WorldEvents.Start(game.State, World, def, "", game.State.Day);

        foreach (var city in World.Cities)
        {
            Assert.Equal(def.PriceMult, WorldEvents.PriceMultiplier(game.State, World, city.Id, "cells"), 6);
            Assert.Equal(1.0, WorldEvents.PriceMultiplier(game.State, World, city.Id, "steel"));
        }

        Assert.NotEmpty(game.View().Location!.News);
        Assert.Empty(game.View().EventCityIds);
    }

    [Fact]
    public void SellQuoteStillCannotBeatBuyQuoteUnderAPriceEvent()
    {
        var game = NewGame();
        var def = World.Events.ById("mill-walkout")!;
        var city = World.Cities.First(c => c.Industries.Contains("refining"));
        WorldEvents.Start(game.State, World, def, city.Id, game.State.Day);

        var eco = World.Config.Economy;
        var terms = CrewMath.Terms(game.State.Caravan, World);
        var good = World.Good(def.Goods[0]);
        var stock = game.State.StockOf(city.Id, good.Id);
        var mult = WorldEvents.PriceMultiplier(game.State, World, city.Id, good.Id);

        var buy = Economy.BuyUnitPrice(good, city.Market[good.Id], stock, eco, terms, mult);
        var sell = Economy.SellUnitPrice(good, city.Market[good.Id], stock, eco, terms, mult);
        Assert.True(sell <= buy + 1e-9);
    }

    [Fact]
    public void ReadingTheCityPageChangesNothing()
    {
        var game = NewGame();
        var def = World.Events.ById("trade-fair")!;
        WorldEvents.Start(game.State, World, def, Start, game.State.Day);

        var rng = game.State.RngState;
        var before = JsonSerializer.Serialize(game.State);
        _ = game.View();

        Assert.Equal(rng, game.State.RngState);
        Assert.Equal(before, JsonSerializer.Serialize(game.State));
    }

    [Fact]
    public void EventsSurviveASave()
    {
        var game = NewGame();
        var def = World.Events.ById("trade-fair")!;
        WorldEvents.Start(game.State, World, def, Start, game.State.Day);

        var restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(game.State))!;
        var resumed = Game.Resume(World, restored);

        Assert.Equal(
            WorldEvents.PriceMultiplier(game.State, World, Start, def.Goods[0]),
            WorldEvents.PriceMultiplier(resumed.State, World, Start, def.Goods[0]), 6);
        Assert.Single(resumed.View().Location!.News);
    }

    [Fact]
    public void ACertainDailyRollFiresOnTheWait()
    {
        var files = MinimalWorld.With(WorldLoader.EventsKey, """
        { "maxConcurrent": 1, "dailyChance": 1,
          "events": [
            { "id": "boom", "name": "Boom", "kind": "market", "headline": "Boom in {city}",
              "detail": "Prices jump.", "tone": "warn", "durationDays": 3, "weight": 1,
              "goods": ["widget"], "priceMult": 1.2 }
          ] }
        """);
        var world = WorldLoader.Load(files);
        var game = Game.New(world, 7);

        Assert.Empty(game.State.ActiveEvents);
        var result = game.Apply(new WaitCommand(1));

        Assert.True(result.Ok, result.Error);
        Assert.Single(game.State.ActiveEvents);
        Assert.Equal("boom", game.State.ActiveEvents[0].DefId);
        Assert.Contains(result.Events, e => e.Kind == GameEventKind.World);
        Assert.NotEmpty(game.View().Location!.News);
    }

    [Fact]
    public void InvestStillWritesTheStoredVitalUnderAnOverlay()
    {
        var game = NewGame();
        var unrest = World.Events.ById("street-unrest")!;
        var city = World.City(Start);
        var storedBefore = CityStats.Vital(game.State, city, "peace");

        WorldEvents.Start(game.State, World, unrest, Start, game.State.Day);

        var invest = World.Standing.Action("invest")!;
        var result = game.Apply(new CityFavorCommand("invest"));
        Assert.True(result.Ok, result.Error);

        Assert.Equal(storedBefore, CityStats.Vital(game.State, city, "peace"), 6);
        Assert.Equal(
            invest.VitalDelta,
            CityStats.Vital(game.State, city, invest.VitalId) - CityStats.Founding(city, invest.VitalId), 6);
    }
}
