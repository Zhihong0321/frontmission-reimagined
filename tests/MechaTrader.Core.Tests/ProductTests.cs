using System.Text.Json;
using MechaTrader.Core.Commands;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;
using Xunit;

namespace MechaTrader.Core.Tests;

/// <summary>
/// Product depth: five grades with a rising value density, a shelf that grades by the
/// city that made it, shortage events that cover a whole category and reward whoever
/// relieves them, and grades a city will not sell to a stranger.
/// </summary>
public class ProductTests
{
    private const ulong Seed = 515;

    private static readonly WorldData World = TestWorld.Shipping;
    private static readonly string Start = World.Config.StartCityId;

    private static Game NewGame() => Game.New(World, Seed);

    [Fact]
    public void EveryGoodSitsInsideItsTiersValueBand()
    {
        var tiers = World.Tiers.OrderBy(t => t.Tier).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, tiers.Select(t => t.Tier).ToArray());

        for (var i = 1; i < tiers.Count; i++)
            Assert.True(tiers[i].MinPricePerVolume > tiers[i - 1].MinPricePerVolume, "Tier floors must rise.");

        foreach (var good in World.Goods)
        {
            var tier = World.TierOf(good);
            var next = tiers.FirstOrDefault(t => t.Tier == tier.Tier + 1);
            Assert.True(good.PricePerVolume >= tier.MinPricePerVolume, $"{good.Id} below its tier floor.");
            if (next is not null)
                Assert.True(good.PricePerVolume < next.MinPricePerVolume, $"{good.Id} is worth tier {next.Tier} money.");
        }
    }

    [Fact]
    public void EveryCategoryHasAProducerAndEveryTierHasAConsumer()
    {
        foreach (var category in World.Categories)
        {
            var made = World.Goods.Where(g => g.Category == category.Id)
                .Any(g => World.Cities.Any(c => c.Market[g.Id].Production > 0));
            Assert.True(made, $"Nobody makes anything in {category.Id}.");
        }

        foreach (var tier in World.Tiers)
        {
            var eaten = World.Goods.Where(g => g.Tier == tier.Tier)
                .Any(g => World.Cities.Any(c => c.Market[g.Id].Consumption > 0));
            Assert.True(eaten, $"Nobody consumes anything of tier {tier.Tier}.");
        }
    }

    [Fact]
    public void ALoaderRejectsAGoodPricedLikeAHigherTier()
    {
        // A "widget" worth 500 per volume is tier 2 money in the minimal world.
        var files = MinimalWorld.With(WorldLoader.GoodsKey,
            MinimalWorld.Files[WorldLoader.GoodsKey].Replace("\"basePrice\": 10,", "\"basePrice\": 500,"));

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("territory", error.Message);
    }

    [Fact]
    public void ProductionGradeRisesWithCraftAndStaysInRange()
    {
        var q = World.Quality;
        var rough = QualityMath.ProductionQuality(q, 0, 0.0);
        var master = QualityMath.ProductionQuality(q, 100, 1.0);

        Assert.Equal(q.Base, rough, 6);
        Assert.Equal(Math.Min(100, q.Base + q.Random + q.CityVitalWeight), master, 6);
        Assert.True(master >= q.STierAt, "A master works floor on its best day should reach S-tier on its own.");
        Assert.InRange(QualityMath.ProductionQuality(q, 50, 0.5), q.Base, 100);
    }

    [Fact]
    public void AMasterCityShelvesBetterCratesThanARoughOne()
    {
        var game = NewGame();
        var craftId = World.Quality.CityVitalId;
        Assert.False(string.IsNullOrWhiteSpace(craftId), "Shipping content should name a craft vital.");

        var best = World.Cities.OrderByDescending(c => CityStats.Founding(c, craftId)).First();
        var worst = World.Cities.OrderBy(c => CityStats.Founding(c, craftId)).First();
        var good = World.Goods[0];

        Assert.True(game.State.StockOf(best.Id, good.Id).OutQuality > game.State.StockOf(worst.Id, good.Id).OutQuality,
            $"{best.Name} (craft {CityStats.Founding(best, craftId)}) should open on a better shelf than {worst.Name}.");
    }

    [Fact]
    public void TheDailyRollMovesGradeWithinTheAuthoredWidth()
    {
        var game = NewGame();
        var city = World.City(Start);
        var good = World.Goods.First(g => city.Market[g.Id].Production > 0);
        var opening = game.State.StockOf(Start, good.Id).OutQuality;

        game.Apply(new WaitCommand(30));

        var q = World.Quality;
        var craft = CityStats.Vital(game.State, city, q.CityVitalId);
        var lo = QualityMath.ProductionQuality(q, craft, 0.0);
        var hi = QualityMath.ProductionQuality(q, craft, 1.0);
        var after = game.State.StockOf(Start, good.Id).OutQuality;

        Assert.InRange(after, lo - 1e-6, hi + 1e-6);
        Assert.NotEqual(opening, after);
    }

    [Fact]
    public void ACityWillNotSellALockedGradeToAStranger()
    {
        var game = NewGame();
        var locked = World.Tiers.Where(t => t.MinStanding > 0).OrderBy(t => t.MinStanding).First();
        var good = World.Goods.First(g => g.Tier == locked.Tier);
        game.State.Cash = 10_000_000;
        game.State.SetStock(Start, good.Id, new CityStock(50, 0, 70));

        var before = JsonSerializer.Serialize(game.State);
        var refused = game.Apply(new BuyCommand(good.Id, 1));

        Assert.False(refused.Ok);
        Assert.Contains("standing", refused.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, JsonSerializer.Serialize(game.State));
        Assert.True(game.View().Market.First(r => r.GoodId == good.Id).Locked);

        // Any segment counts: the streets can open the shelf as well as the office.
        var citizens = World.Standing.SegmentOr("citizens");
        game.State.SetStanding(Start, citizens, Math.Min(World.Standing.SegmentMax, locked.MinStanding));
        if (Standing.Of(game.State, Start) < locked.MinStanding)
            game.State.SetStanding(Start, World.Standing.DefaultSegmentId, locked.MinStanding - Standing.Of(game.State, Start));

        var allowed = game.Apply(new BuyCommand(good.Id, 1));
        Assert.True(allowed.Ok, allowed.Error);
        Assert.False(game.View().Market.First(r => r.GoodId == good.Id).Locked);
    }

    [Fact]
    public void SellingStillWorksForALockedGrade()
    {
        var game = NewGame();
        var locked = World.Tiers.Where(t => t.MinStanding > 0).OrderBy(t => t.MinStanding).First();
        var good = World.Goods.First(g => g.Tier == locked.Tier);
        game.State.Caravan.Cargo[good.Id] = new CargoLot { Units = 3, TotalCost = 300, Quality = 70 };

        var result = game.Apply(new SellCommand(good.Id, 3));
        Assert.True(result.Ok, result.Error);
    }

    [Fact]
    public void ACategoryEventCoversEveryGoodInTheCategoryAndNoOther()
    {
        var game = NewGame();
        var def = World.Events.Events.First(e => e.Categories.Count > 0 && e.TouchesPrice);
        var city = World.Cities.First(c => def.Industries.Count == 0 || c.Industries.Any(def.Industries.Contains));

        WorldEvents.Start(game.State, World, def, city.Id, game.State.Day);

        foreach (var good in World.Goods)
        {
            var mult = WorldEvents.PriceMultiplier(game.State, World, city.Id, good.Id);
            if (def.Categories.Contains(good.Category)) Assert.Equal(def.PriceMult, mult, 9);
            else Assert.Equal(1.0, mult, 9);
        }

        var news = game.View().Location is { } here && here.Id == city.Id
            ? here.News
            : null;
        if (news is not null)
            Assert.DoesNotContain(news, n => n.Headline.Contains("{category}"));
    }

    [Fact]
    public void RelievingAShortageEarnsCitizenStanding()
    {
        var game = NewGame();
        var def = World.Events.Events.First(e => e.IsShortage);
        var good = World.Goods.First(g => def.Categories.Contains(g.Category) || def.Goods.Contains(g.Id));
        var citizens = World.Standing.SegmentOr("citizens");

        WorldEvents.Start(game.State, World, def, Start, game.State.Day);
        game.State.Caravan.Cargo[good.Id] = new CargoLot { Units = 100, TotalCost = 100, Quality = 70 };

        var perUnit = WorldEvents.ReliefPerUnit(game.State, World, Start, good.Id);
        Assert.True(perUnit > 0);
        Assert.True(game.View().Market.First(r => r.GoodId == good.Id).ReliefPerUnit > 0);

        var units = (int)Math.Ceiling(def.ReliefUnits);
        var result = game.Apply(new SellCommand(good.Id, units));
        Assert.True(result.Ok, result.Error);

        Assert.Equal(def.ReliefStanding, Standing.Segment(game.State, Start, citizens), 3);
        Assert.Equal(0.0, Standing.Segment(game.State, Start, World.Standing.DefaultSegmentId), 6);
    }

    [Fact]
    public void SellingWithoutAShortageEarnsNoCitizenStanding()
    {
        var game = NewGame();
        game.State.ActiveEvents.Clear();
        var good = World.Goods[0];
        var citizens = World.Standing.SegmentOr("citizens");
        game.State.Caravan.Cargo[good.Id] = new CargoLot { Units = 50, TotalCost = 100, Quality = 70 };

        game.Apply(new SellCommand(good.Id, 50));

        Assert.Equal(0.0, Standing.Segment(game.State, Start, citizens), 6);
    }

    [Fact]
    public void RankReadsTheTotalAcrossSegments()
    {
        var game = NewGame();
        var known = World.Standing.Ranks.First(r => r.Id == "known");
        var stranger = World.Standing.Ranks[0];
        var each = stranger.UpTo!.Value / 2.0 + 1;

        game.State.SetStanding(Start, World.Standing.Segments[0].Id, each);
        game.State.SetStanding(Start, World.Standing.Segments[1].Id, each);

        var view = game.View().Location!.Standing;
        Assert.Equal(known.Name, view.Rank);
        Assert.Equal(each * 2, view.Value, 3);
        // The total is never stored: the per-city record holds only segment keys.
        Assert.All(game.State.CityStanding[Start].Keys, k => Assert.Contains(k, World.Standing.Segments.Select(sg => sg.Id)));
    }

    [Fact]
    public void FavorActionsLandInTheSegmentTheyName()
    {
        var game = NewGame();
        var aid = World.Standing.Action("aid")!;
        var segment = World.Standing.SegmentOr(aid.SegmentId);

        var result = game.Apply(new CityFavorCommand("aid"));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(aid.Standing, Standing.Segment(game.State, Start, segment), 6);
        foreach (var other in World.Standing.Segments.Where(s => s.Id != segment))
            Assert.Equal(0.0, Standing.Segment(game.State, Start, other.Id), 6);
    }

    [Fact]
    public void ASegmentStopsAtItsCeilingWhileTheTotalKeepsCounting()
    {
        var game = NewGame();
        var cfg = World.Standing;
        game.State.SetStanding(Start, cfg.Segments[0].Id, cfg.SegmentMax - 1);

        var landed = Standing.Grant(game.State, cfg, Start, cfg.Segments[0].Id, 50);
        Assert.Equal(1.0, landed, 6);
        Assert.Equal(cfg.SegmentMax, Standing.Segment(game.State, Start, cfg.Segments[0].Id), 6);

        Standing.Grant(game.State, cfg, Start, cfg.Segments[1].Id, 10);
        Assert.Equal(cfg.SegmentMax + 10, Standing.Of(game.State, Start), 6);
    }

    [Fact]
    public void TierAndSegmentStateSurviveASave()
    {
        var game = NewGame();
        game.Apply(new CityFavorCommand("aid"));
        game.Apply(new CityFavorCommand("donate"));

        var saved = JsonSerializer.Serialize(game.State);
        var restored = JsonSerializer.Deserialize<GameState>(saved)!;

        Assert.Equal(Standing.Of(game.State, Start), Standing.Of(restored, Start), 6);
        foreach (var segment in World.Standing.Segments)
            Assert.Equal(Standing.Segment(game.State, Start, segment.Id), Standing.Segment(restored, Start, segment.Id), 6);
    }

    [Fact]
    public void EveryMarketRowCarriesItsTierColour()
    {
        var view = NewGame().View();
        Assert.All(view.Market, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.TierName));
            Assert.False(string.IsNullOrWhiteSpace(r.TierColor));
            Assert.InRange(r.Tier, 1, 5);
        });
        Assert.Equal(World.Tiers.Count, view.Tiers.Count);
    }

    [Fact]
    public void MarketRowsCarryThePriceBreakdownAndBuyMax()
    {
        var view = NewGame().View();
        Assert.All(view.Market, r =>
        {
            // The city's own price is positive, and the full-spread counterfactual sits
            // on the right side of it: spread pushes the buy up and pulls the sell down.
            Assert.InRange(r.MarketBuy, 0.01, double.MaxValue);
            Assert.InRange(r.MarketSell, 0.01, double.MaxValue);
            Assert.True(r.NoCrewBuy > r.MarketBuy, $"{r.Name}: full spread must raise the buy above the market price");
            Assert.True(r.NoCrewSell < r.MarketSell, $"{r.Name}: full spread must lower the sell below the market price");
            Assert.InRange(r.PickMult, 0.01, 2.0);

            // Buy max is the smallest of the three limits, and no larger than any of them.
            Assert.True(r.MaxBuy >= 0, $"{r.Name}: max buy cannot be negative");
            Assert.True(r.MaxBuy <= r.MaxByHold, $"{r.Name}: max buy cannot exceed hold space");
            Assert.True(r.MaxBuy <= r.MaxByCash, $"{r.Name}: max buy cannot exceed cash");
            Assert.True(r.MaxBuy <= r.MaxByShelf, $"{r.Name}: max buy cannot exceed the shelf");
            Assert.Equal(Math.Min(r.MaxByHold, Math.Min(r.MaxByCash, r.MaxByShelf)), r.MaxBuy);
        });
    }
}
