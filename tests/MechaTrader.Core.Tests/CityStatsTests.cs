using System.Text.Json;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// City stats come in two halves and these tests hold both of them down.
///
/// The founding half is authored content copied into state once, so what matters is
/// that every city gets a complete block, that the copy is faithful, and that it
/// survives a save. The supply half is derived from the market every time it is read,
/// so what matters is that it sits at nominal when nothing has happened, moves the
/// right way when a convoy trades, and never advances the world just by being looked at.
/// </summary>
public class CityStatsTests
{
    private const ulong Seed = 90210;

    private static readonly World.WorldData World = TestWorld.Shipping;
    private static readonly string Start = World.Config.StartCityId;

    private static Game NewGame() => Game.New(World, Seed);

    /// <summary>The band a supply figure reads at rest, whatever content calls it.</summary>
    private static CitySupplyDef Supply(string id) => World.CityStats.Supplies.First(s => s.Id == id);

    // ---------- founding stats ----------

    [Fact]
    public void EveryCityHasEveryDeclaredVital()
    {
        // A city missing a stat would render as a blank card rather than as an error,
        // which is exactly the kind of hole nobody notices until it ships.
        foreach (var city in World.Cities)
        {
            foreach (var vital in World.CityStats.Vitals)
            {
                Assert.True(city.Vitals.ContainsKey(vital.Id),
                    $"{city.Id} has no founding value for '{vital.Id}'.");

                var value = city.Vitals[vital.Id];
                Assert.InRange(value, vital.Min, vital.Max);
            }
        }
    }

    [Fact]
    public void PopulationStillScalesIndustry()
    {
        // The population vital is not decoration: it is the number market generation
        // multiplies by, and it now arrives through the stat block.
        var busiest = World.Cities.OrderByDescending(c => c.Population).First();
        var quietest = World.Cities.OrderBy(c => c.Population).First();

        Assert.Equal(busiest.Vitals[World.CityStats.PopulationVitalId], busiest.Population, 6);

        var busiestDemand = World.Goods.Sum(g => busiest.Market[g.Id].Consumption);
        var quietestDemand = World.Goods.Sum(g => quietest.Market[g.Id].Consumption);

        Assert.True(busiestDemand > quietestDemand,
            "the largest city should eat more than the smallest.");
    }

    [Fact]
    public void ANewRunOpensOnTheFoundingStats()
    {
        var game = NewGame();

        foreach (var city in World.Cities)
        {
            foreach (var (vitalId, founding) in city.Vitals)
            {
                Assert.Equal(founding, CityStats.Vital(game.State, city, vitalId), 6);
                Assert.Equal(founding, game.State.VitalOf(city.Id, vitalId));
            }
        }
    }

    [Fact]
    public void LiveStatsOverrideFoundingAndSurviveASave()
    {
        // Nothing moves a vital yet, but the day it does, this is the path it takes:
        // written to state, read back through state, carried by the save.
        var game = NewGame();
        var city = World.City(Start);
        var vitalId = World.CityStats.Vitals.Last().Id;
        var moved = city.Vitals[vitalId] + 3.5;

        game.State.SetVital(city.Id, vitalId, moved);

        var json = JsonSerializer.Serialize(game.State);
        var restored = Game.Resume(World, JsonSerializer.Deserialize<GameState>(json)!);

        Assert.Equal(moved, CityStats.Vital(restored.State, city, vitalId), 6);
        Assert.Equal(city.Vitals[vitalId], CityStats.Founding(city, vitalId), 6);
    }

    [Fact]
    public void AStateThatNeverHeardOfAVitalFallsBackToTheFoundingValue()
    {
        // A save written before a stat existed must still load. Content is the floor
        // under state, not a rival source of truth.
        var game = NewGame();
        var city = World.City(Start);

        game.State.CityVitals.Remove(city.Id);

        foreach (var (vitalId, founding) in city.Vitals)
        {
            Assert.Equal(founding, CityStats.Vital(game.State, city, vitalId), 6);
        }
    }

    // ---------- bands ----------

    [Fact]
    public void AValueTakesTheFirstBandItFallsUnder()
    {
        var bands = new List<StatBandDef>
        {
            new() { Id = "low", Name = "Low", UpTo = 10 },
            new() { Id = "mid", Name = "Mid", UpTo = 20 },
            new() { Id = "high", Name = "High" }
        };

        Assert.Equal("low", CityStats.Band(bands, -5)!.Id);
        Assert.Equal("low", CityStats.Band(bands, 9.99)!.Id);
        Assert.Equal("mid", CityStats.Band(bands, 10)!.Id);   // upper bound is exclusive
        Assert.Equal("high", CityStats.Band(bands, 20)!.Id);
        Assert.Equal("high", CityStats.Band(bands, 1e6)!.Id); // the open band catches the top
        Assert.Null(CityStats.Band(Array.Empty<StatBandDef>(), 5));
    }

    // ---------- supply, derived ----------

    [Fact]
    public void EverySupplyOpensAtNominal()
    {
        // A fresh world is settled by construction, so every city should read close to
        // 100 before anyone has traded. If this drifts, the index has stopped meaning
        // "compared with this city's own normal".
        var game = NewGame();

        foreach (var city in World.Cities)
        {
            foreach (var supply in World.CityStats.Supplies)
            {
                var reading = CityStats.Supply(game.State, World, city, supply);

                Assert.True(Math.Abs(reading.Index - 100.0) < 1.0,
                    $"{city.Id}/{supply.Id} opened at {reading.Index:0.0}, not nominal.");
            }
        }
    }

    [Fact]
    public void BuyingOutTheShelfPushesThatSupplyDown()
    {
        var game = NewGame();
        var city = World.City(Start);

        // Whichever band this city holds the most of, so the trade is affordable.
        var supply = World.CityStats.Supplies
            .OrderByDescending(s => CityStats.Supply(game.State, World, city, s).Stock)
            .First();

        var goodId = supply.Goods
            .OrderByDescending(id => game.State.ShelfOf(city.Id, id))
            .First();

        var before = CityStats.Supply(game.State, World, city, supply).Index;

        var result = game.Apply(new BuyCommand(goodId, 40));
        Assert.True(result.Ok, result.Error);

        var after = CityStats.Supply(game.State, World, city, supply).Index;

        Assert.True(after < before, $"{supply.Id} read {after:0.0} after a buy, up from {before:0.0}.");
    }

    [Fact]
    public void SellingIntoACityPushesThatSupplyUp()
    {
        // The intake counts: a city that has just taken three hundred units off a
        // caravan is well supplied whether or not any of it is shelved yet.
        var game = NewGame();
        var city = World.City(Start);

        var supply = World.CityStats.Supplies
            .OrderByDescending(s => CityStats.Supply(game.State, World, city, s).Stock)
            .First();

        var goodId = supply.Goods.First();

        Assert.True(game.Apply(new BuyCommand(goodId, 20)).Ok);

        var before = CityStats.Supply(game.State, World, city, supply).Index;

        Assert.True(game.Apply(new SellCommand(goodId, 20)).Ok);

        var after = CityStats.Supply(game.State, World, city, supply).Index;

        Assert.True(after > before, $"{supply.Id} read {after:0.0} after a sale, down from {before:0.0}.");
    }

    [Fact]
    public void ASupplyKnowsWhetherTheCityMakesOrEatsIt()
    {
        // The whole trade map falls out of production against consumption, so a supply
        // figure that could not tell the two apart would be telling the player nothing.
        var game = NewGame();

        var readings = World.Cities
            .Select(c => CityStats.Supply(game.State, World, c, Supply("power")))
            .ToList();

        Assert.Contains(readings, r => r.NetFlow > 0);
        Assert.Contains(readings, r => r.NetFlow < 0);
        Assert.All(readings, r => Assert.True(r.DaysOfCover is > 0, "every city burns power cells."));
    }

    [Fact]
    public void ReadingTheCityPageDoesNotAdvanceAnything()
    {
        // Same rule the recruitment board lives under: building a view is a pure read.
        var game = NewGame();
        var before = JsonSerializer.Serialize(game.State);

        for (var i = 0; i < 5; i++)
        {
            var view = game.View();
            Assert.NotNull(view.Location);
            Assert.NotEmpty(view.Location!.Vitals);
            Assert.NotEmpty(view.Location.Supplies);
        }

        Assert.Equal(before, JsonSerializer.Serialize(game.State));
    }

    // ---------- what reaches the screen ----------

    [Fact]
    public void EveryVitalArrivesReadyToPrint()
    {
        var view = NewGame().View();
        var location = Assert.IsType<View.LocationView>(view.Location);

        Assert.Equal(World.CityStats.Vitals.Count, location.Vitals.Count);

        foreach (var vital in location.Vitals)
        {
            var def = World.CityStats.Vital(vital.Id)!;

            Assert.False(string.IsNullOrWhiteSpace(vital.Name));
            Assert.False(string.IsNullOrWhiteSpace(vital.Display));
            Assert.False(string.IsNullOrWhiteSpace(vital.FoundingDisplay));
            Assert.EndsWith(def.Unit, vital.Display);
            Assert.InRange(vital.Fill, 0.0, 1.0);

            // Nothing has moved yet, so no city has drifted from where it was founded.
            Assert.Equal(vital.Founding, vital.Value, 6);
            Assert.Equal(vital.Display, vital.FoundingDisplay);
            Assert.Equal("", vital.DeltaDisplay);

            if (def.Bands.Count > 0) Assert.False(string.IsNullOrWhiteSpace(vital.Band));
        }
    }

    [Fact]
    public void AStatAllowedToGoNegativeIsShownSigned()
    {
        // "+2.4%/yr" and "-1.1%/yr" have to be distinguishable at a glance; "2.4%/yr"
        // and "-1.1%/yr" read as different kinds of number.
        var signed = World.CityStats.Vitals.Where(v => v.Signed).ToList();
        Assert.NotEmpty(signed);

        var view = NewGame().View();

        foreach (var def in signed)
        {
            var shown = view.Location!.Vitals.First(v => v.Id == def.Id);
            if (shown.Value > 0) Assert.StartsWith("+", shown.Display);
            if (shown.Value < 0) Assert.StartsWith("-", shown.Display);
        }
    }

    [Fact]
    public void TheCityWireStartsEmpty()
    {
        // Day 1 has nothing on the wire: events fire as the clock advances.
        Assert.Empty(NewGame().View().Location!.News);
    }
}
