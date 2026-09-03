using System.Text.Json;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// Trade expos: a derived calendar per city, a pass with a fee, a stall that only takes
/// goods the city does not make and the theme admits, buyers who trade on the day tick,
/// and a report the page can replay without drawing again.
/// </summary>
public class ExpoTests
{
    private const ulong Seed = 818;

    private static readonly WorldData World = TestWorld.Shipping;
    private static readonly string Start = World.Config.StartCityId;

    private static Game NewGame() => Game.New(World, Seed);

    /// <summary>Advance until the start city's expo is open, then return it.</summary>
    private static ExpoInstance OpenExpo(Game game)
    {
        var city = World.City(Start);
        for (var i = 0; i < World.Expos.CycleDays * 2; i++)
        {
            var running = Expos.Running(World, city, game.State.Seed, game.State.Day);
            if (running is not null) return running;
            game.Apply(new WaitCommand(1));
        }
        throw new InvalidOperationException("No expo opened within two cycles.");
    }

    private static GoodDef ListableGood(ExpoInstance expo)
    {
        var city = World.City(Start);
        return World.Goods.First(g => Expos.ThemeCovers(expo.Theme, g) && !Expos.CityMakes(city, g.Id));
    }

    [Fact]
    public void EveryCityHasADerivedCalendarThatNeverTouchesState()
    {
        var game = NewGame();
        var before = JsonSerializer.Serialize(game.State);

        foreach (var city in World.Cities)
        {
            var next = Expos.Next(World, city, Seed, 1);
            Assert.NotNull(next);
            Assert.True(next!.StartDay >= 1);
            Assert.Equal(next.StartDay + next.Theme.DurationDays, next.EndDay);
            Assert.Same(next.Theme, Expos.Next(World, city, Seed, 1)!.Theme);
        }

        _ = game.View().Expo;
        Assert.Equal(before, JsonSerializer.Serialize(game.State));
        Assert.DoesNotContain("theme", before, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NarrowThemesBuffHarderThanBroadOnes()
    {
        var cfg = World.Expos;
        var narrow = cfg.Themes.OrderBy(t => t.Categories.Count).First();
        var broad = cfg.Themes.OrderByDescending(t => t.Categories.Count).First();
        Assert.True(Expos.Buff(cfg, narrow) > Expos.Buff(cfg, broad));
        Assert.Equal(cfg.BuffMax, Expos.Buff(cfg, narrow), 6);
    }

    [Fact]
    public void APassCostsTheFeeAndOnlySellsWhileTheExpoIsOpen()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var city = World.City(Start);

        if (Expos.Running(World, city, Seed, game.State.Day) is null)
        {
            var closed = game.Apply(new ExpoRegisterCommand());
            Assert.False(closed.Ok);
        }

        var expo = OpenExpo(game);
        var fee = Expos.Fee(World.Expos, city);
        var cashBefore = game.State.Cash;

        var bought = game.Apply(new ExpoRegisterCommand());
        Assert.True(bought.Ok, bought.Error);
        Assert.Equal(cashBefore - fee, game.State.Cash);
        Assert.Contains(expo.PassId, game.State.ExpoPasses);

        var again = game.Apply(new ExpoRegisterCommand());
        Assert.False(again.Ok);
        Assert.True(game.View().Expo!.PassHeld);
    }

    [Fact]
    public void ACitysOwnProduceIsNeverAllowedOnItsStall()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var expo = OpenExpo(game);
        game.Apply(new ExpoRegisterCommand());

        var city = World.City(Start);
        var local = World.Goods.FirstOrDefault(g => Expos.CityMakes(city, g.Id) && Expos.ThemeCovers(expo.Theme, g))
                    ?? World.Goods.First(g => Expos.CityMakes(city, g.Id));
        game.State.Caravan.Cargo[local.Id] = new CargoLot { Units = 10, TotalCost = 10, Quality = 70 };

        var before = JsonSerializer.Serialize(game.State);
        var refused = game.Apply(new ExpoListCommand(local.Id, 1000));

        Assert.False(refused.Ok);
        Assert.Contains("own produce", refused.Error!);
        Assert.Equal(before, JsonSerializer.Serialize(game.State));
        Assert.True(game.View().Expo!.Listings.First(l => l.GoodId == local.Id).CityMakes);
    }

    [Fact]
    public void BuyersTradeOnTheDayTickAndTheReportReplaysIt()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var expo = OpenExpo(game);
        game.Apply(new ExpoRegisterCommand());

        var good = ListableGood(expo);
        game.State.Caravan.Cargo[good.Id] = new CargoLot { Units = 200, TotalCost = 200, Quality = 70 };

        // A giveaway ask: every buyer of this category says yes.
        var listed = game.Apply(new ExpoListCommand(good.Id, 1));
        Assert.True(listed.Ok, listed.Error);

        var cashBefore = game.State.Cash;
        var heldBefore = game.State.Caravan.Held(good.Id);
        game.Apply(new WaitCommand(1));

        var report = game.State.LastExpoDay;
        Assert.NotNull(report);
        Assert.Equal(Start, report!.CityId);
        Assert.NotEmpty(report.Visits);
        Assert.True(report.UnitsSold > 0, "At an ask of 1 credit somebody should have bought.");
        Assert.Equal(heldBefore - report.UnitsSold, game.State.Caravan.Held(good.Id));
        Assert.All(report.Visits, v => Assert.False(string.IsNullOrWhiteSpace(v.Outcome)));

        var view = game.View().Expo!;
        Assert.NotNull(view.Report);
        Assert.Equal(report.Visits.Count, view.Report!.Visits.Count);
        Assert.Equal(report.Revenue, view.Report.Revenue);
    }

    [Fact]
    public void ARidiculousAskSellsNothing()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var expo = OpenExpo(game);
        game.Apply(new ExpoRegisterCommand());

        var good = ListableGood(expo);
        game.State.Caravan.Cargo[good.Id] = new CargoLot { Units = 50, TotalCost = 50, Quality = 70 };
        game.Apply(new ExpoListCommand(good.Id, (long)good.BasePrice * 1000));
        game.Apply(new WaitCommand(1));

        Assert.Equal(50, game.State.Caravan.Held(good.Id));
        Assert.Equal(0, game.State.LastExpoDay!.UnitsSold);
        Assert.Contains(game.State.LastExpoDay.Visits, v => v.Outcome == ExpoOutcome.TooDear);
    }

    [Fact]
    public void ListingNeedsAPassAndTheThemeMustAdmitTheGood()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var expo = OpenExpo(game);
        var good = ListableGood(expo);
        game.State.Caravan.Cargo[good.Id] = new CargoLot { Units = 5, TotalCost = 5, Quality = 70 };

        Assert.False(game.Apply(new ExpoListCommand(good.Id, 100)).Ok);
        game.Apply(new ExpoRegisterCommand());
        Assert.True(game.Apply(new ExpoListCommand(good.Id, 100)).Ok);

        var city = World.City(Start);
        var outside = World.Goods.FirstOrDefault(g => !Expos.ThemeCovers(expo.Theme, g) && !Expos.CityMakes(city, g.Id));
        if (outside is not null)
        {
            game.State.Caravan.Cargo[outside.Id] = new CargoLot { Units = 5, TotalCost = 5, Quality = 70 };
            var refused = game.Apply(new ExpoListCommand(outside.Id, 100));
            Assert.False(refused.Ok);
            Assert.Contains("admit", refused.Error!);
        }

        Assert.True(game.Apply(new ExpoListCommand(good.Id, 0)).Ok);
        Assert.False(game.State.Caravan.ExpoAsks.ContainsKey(good.Id));
    }

    [Fact]
    public void LeavingTownTakesTheStallDown()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var expo = OpenExpo(game);
        game.Apply(new ExpoRegisterCommand());
        var good = ListableGood(expo);
        game.State.Caravan.Cargo[good.Id] = new CargoLot { Units = 5, TotalCost = 5, Quality = 70 };
        game.Apply(new ExpoListCommand(good.Id, 100));

        game.Apply(new DepartCommand(World.Routes.From(Start)[0].Other(Start)));
        Assert.Empty(game.State.Caravan.ExpoAsks);
    }

    [Fact]
    public void AnIdleStallDoesNotTouchTheRng()
    {
        var a = NewGame();
        var b = NewGame();
        b.State.Cash = 1_000_000;
        var expo = OpenExpo(b);
        a.Apply(new WaitCommand(b.State.Day - a.State.Day));
        b.Apply(new ExpoRegisterCommand());

        // A pass with nothing listed draws nothing: the two runs stay in step.
        a.Apply(new WaitCommand(1));
        b.Apply(new WaitCommand(1));
        Assert.Equal(a.State.RngState, b.State.RngState);
        _ = expo;
    }

    [Fact]
    public void ExpoStateSurvivesASave()
    {
        var game = NewGame();
        game.State.Cash = 1_000_000;
        var expo = OpenExpo(game);
        game.Apply(new ExpoRegisterCommand());
        var good = ListableGood(expo);
        game.State.Caravan.Cargo[good.Id] = new CargoLot { Units = 20, TotalCost = 20, Quality = 70 };
        game.Apply(new ExpoListCommand(good.Id, 1));
        game.Apply(new WaitCommand(1));

        var restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(game.State))!;
        Assert.Equal(game.State.ExpoPasses, restored.ExpoPasses);
        Assert.Equal(game.State.LastExpoDay!.Visits.Count, restored.LastExpoDay!.Visits.Count);
        Assert.Equal(JsonSerializer.Serialize(game.State.Caravan.ExpoAsks), JsonSerializer.Serialize(restored.Caravan.ExpoAsks));
    }
}
