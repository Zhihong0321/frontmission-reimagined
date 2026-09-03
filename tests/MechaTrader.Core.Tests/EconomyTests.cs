using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using Xunit;

namespace MechaTrader.Core.Tests;

public class EconomyTests
{
    private static (Model.GoodDef Good, World.CityGoodProfile Profile, Model.EconomyConfig Config) Sample()
    {
        var world = TestWorld.Shipping;
        var good = world.Good("steel");
        var city = world.City("munchen");
        return (good, city.Market[good.Id], world.Config.Economy);
    }

    [Fact]
    public void ScarcityRaisesPrice()
    {
        var (good, profile, cfg) = Sample();

        var plentiful = Economy.UnitPrice(good, profile, profile.Equilibrium * 2, cfg);
        var scarce = Economy.UnitPrice(good, profile, profile.Equilibrium / 2, cfg);

        Assert.True(scarce > plentiful, $"Scarce stock should cost more ({scarce:0.0} vs {plentiful:0.0}).");
    }

    [Fact]
    public void BuyPriceExceedsSellPrice()
    {
        var (good, profile, cfg) = Sample();

        var stock = CityStock.Shelved(profile.Equilibrium);

        var buy = Economy.BuyUnitPrice(good, profile, stock, cfg, TradeTerms.Market);
        var sell = Economy.SellUnitPrice(good, profile, stock, cfg, TradeTerms.Market);

        Assert.True(buy > sell, "The spread must make an in-place round trip a loss.");
    }

    [Fact]
    public void BuyingDrainsStockAndSellingRefillsIt()
    {
        var (good, profile, cfg) = Sample();
        var stock = CityStock.Shelved(profile.Equilibrium);

        Assert.True(Economy.QuoteBuy(good, profile, stock, 50, cfg, TradeTerms.Market).ResultingStock.Out < stock.Out);
        Assert.True(Economy.QuoteSell(good, profile, stock, 50, cfg, TradeTerms.Market).ResultingStock.Total > stock.Total);
    }

    [Fact]
    public void AnOrderSettlesAtTheDaysPriceAndTheShelfMovesTomorrow()
    {
        // Bulk is never penalised inside a deal or inside a day: the second order today
        // pays what the first did. What a deal does is move the stock, so after the
        // tick the shelf is scarcer and the price is higher.
        var (good, profile, cfg) = Sample();
        var stock = CityStock.Shelved(profile.Equilibrium);

        var first = Economy.QuoteBuy(good, profile, stock, 200, cfg, TradeTerms.Market);
        Assert.Equal(200 * Economy.BuyUnitPrice(good, profile, stock, cfg, TradeTerms.Market), first.Total, 0);
        Assert.True(first.ResultingStock.Out < stock.Out, "The shelf should drain by the order.");

        var second = Economy.QuoteBuy(good, profile, first.ResultingStock, 200, cfg, TradeTerms.Market);
        Assert.Equal(first.Total, second.Total);

        var tomorrow = Economy.TickStock(second.ResultingStock, profile, cfg, new Rng(1));
        Assert.True(Economy.BuyUnitPrice(good, profile, tomorrow, cfg, TradeTerms.Market)
                    > Economy.BuyUnitPrice(good, profile, stock, cfg, TradeTerms.Market),
            "After buying out most of the shelf, tomorrow's price should be higher.");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(75)]
    [InlineData(400)]
    public void PlanningApproximationTracksTheExactQuote(int units)
    {
        // Planning code uses the approximation to rank hundreds of orders per decision;
        // it has to stay close or the AI and the UI would mislead.
        var (good, profile, cfg) = Sample();
        var stock = CityStock.Shelved(profile.Equilibrium);

        var exactCost = (double)Economy.QuoteBuy(good, profile, stock, units, cfg, TradeTerms.Market).Total;
        var approxCost = Economy.ApproximateBuyCost(good, profile, stock, units, cfg, TradeTerms.Market);
        Assert.True(Math.Abs(approxCost - exactCost) / exactCost < 0.03,
            $"Buy approximation off by {Math.Abs(approxCost - exactCost) / exactCost:P1}.");

        var exactRevenue = (double)Economy.QuoteSell(good, profile, stock, units, cfg, TradeTerms.Market).Total;
        var approxRevenue = Economy.ApproximateSellRevenue(good, profile, stock, units, cfg, TradeTerms.Market);
        Assert.True(Math.Abs(approxRevenue - exactRevenue) / exactRevenue < 0.03,
            $"Sell approximation off by {Math.Abs(approxRevenue - exactRevenue) / exactRevenue:P1}.");
    }

    [Fact]
    public void PriceStaysWithinConfiguredClamps()
    {
        var (good, profile, cfg) = Sample();

        var floor = good.BasePrice * profile.PriceModifier * cfg.MinPriceMult;
        var ceiling = good.BasePrice * profile.PriceModifier * cfg.MaxPriceMult;

        foreach (var stock in new[] { 0.0, 1.0, profile.Equilibrium, profile.Equilibrium * 10_000 })
        {
            var price = Economy.UnitPrice(good, profile, stock, cfg);
            Assert.InRange(price, floor - 1e-6, ceiling + 1e-6);
        }
    }

    [Fact]
    public void MaxAffordableUnitsRespectsBothCashAndVolume()
    {
        var (good, profile, cfg) = Sample();
        var stock = CityStock.Shelved(profile.Equilibrium);

        var volumeLimited = Economy.MaxAffordableUnits(good, profile, stock, 10_000_000, 25, cfg, TradeTerms.Market);
        Assert.Equal((int)(25 / good.UnitVolume), volumeLimited);

        var cashLimited = Economy.MaxAffordableUnits(good, profile, stock, 500, 100_000, cfg, TradeTerms.Market);
        var cost = Economy.QuoteBuy(good, profile, stock, cashLimited, cfg, TradeTerms.Market).Total;
        Assert.True(cost <= 500, $"Affordable order cost {cost} but only 500 was available.");

        var oneMore = Economy.QuoteBuy(good, profile, stock, cashLimited + 1, cfg, TradeTerms.Market).Total;
        Assert.True(oneMore > 500, "One more unit should have been unaffordable.");
    }

    [Fact]
    public void StockSettlesAtItsPredictedSteadyState()
    {
        // The tick and the closed-form steady state must agree, since world generation
        // uses the latter to open a new game on a settled economy.
        var world = TestWorld.Shipping;
        var cfg = world.Config.Economy;
        var profile = world.City("madrid").Market["ore"];

        var rng = new Rng(1);
        var stock = CityStock.Shelved(10.0);
        for (var i = 0; i < 2000; i++) stock = Economy.TickStock(stock, profile, cfg, rng);

        var predicted = profile.SteadyStateStock(cfg.DriftRate);
        Assert.True(Math.Abs(stock.Total - predicted) / predicted < 0.10,
            $"Settled at {stock.Total:0} but predicted {predicted:0}.");
    }

    /* ---------- the two stores ---------- */

    [Fact]
    public void SellingIntoACityDoesNotMoveItsShelfPrice()
    {
        // The property the whole design rests on. Goods sold to a city land in its
        // intake, not on its shelf, so unloading cannot cheapen what the city is
        // selling - which is what would otherwise make a sell/buy-back loop pay.
        var (good, profile, cfg) = Sample();
        var stock = CityStock.Shelved(profile.Equilibrium);

        var buyBefore = Economy.BuyUnitPrice(good, profile, stock, cfg, TradeTerms.Market);

        var afterDumping = Economy.QuoteSell(good, profile, stock, 200, cfg, TradeTerms.Market).ResultingStock;
        var buyAfter = Economy.BuyUnitPrice(good, profile, afterDumping, cfg, TradeTerms.Market);

        Assert.Equal(stock.Out, afterDumping.Out);
        Assert.Equal(buyBefore, buyAfter);
        Assert.Equal(200, afterDumping.In, 6);
    }

    [Fact]
    public void SellingStillLowersWhatTheCityWillPayTomorrow()
    {
        // Diminishing returns on dumping survive the split at the day boundary: the
        // second hundred today fetches what the first did, but a city that took a
        // hold yesterday pays less for more of it today.
        var (good, profile, cfg) = Sample();
        var stock = CityStock.Shelved(profile.Equilibrium);

        var first = Economy.QuoteSell(good, profile, stock, 100, cfg, TradeTerms.Market);
        var second = Economy.QuoteSell(good, profile, first.ResultingStock, 100, cfg, TradeTerms.Market);
        Assert.Equal(first.Total, second.Total);

        var tomorrow = Economy.TickStock(second.ResultingStock, profile, cfg, new Rng(1));
        var third = Economy.QuoteSell(good, profile, tomorrow, 100, cfg, TradeTerms.Market);
        Assert.True(third.Total < first.Total,
            $"The day after taking two hundred, the city still paid {third.Total} against {first.Total}.");
    }

    [Fact]
    public void SellQuoteNeverBeatsTheBuyQuote()
    {
        // Structural, not a matter of tuning: the buy price reads the shelf, the sell
        // price reads everything the city owns, and the total is never below the shelf.
        var (good, profile, cfg) = Sample();

        foreach (var shelf in new[] { cfg.MinStock, profile.Equilibrium, profile.Equilibrium * 20 })
        {
            foreach (var intake in new[] { 0.0, 50.0, profile.Equilibrium * 5 })
            {
                var stock = new CityStock(shelf, intake);

                var buy = Economy.BuyUnitPrice(good, profile, stock, cfg, TradeTerms.Market);
                var sell = Economy.SellUnitPrice(good, profile, stock, cfg, TradeTerms.Market);

                Assert.True(sell <= buy,
                    $"shelf {shelf:0}, intake {intake:0}: sell {sell:0.00} beat buy {buy:0.00}.");
            }
        }
    }

    [Fact]
    public void OnlyWhatIsOnTheShelfCanBeBought()
    {
        var (good, profile, cfg) = Sample();
        var stock = new CityStock(120.0, 900.0);

        var available = Economy.UnitsOnTheShelf(stock, cfg);
        Assert.Equal((int)Math.Floor(120.0 - cfg.MinStock), available);

        // A mountain of intake does not make the order any larger.
        var affordable = Economy.MaxAffordableUnits(
            good, profile, stock, 100_000_000, 100_000, cfg, TradeTerms.Market);

        Assert.True(affordable <= available,
            $"Offered {affordable} units from a shelf holding {available}.");
    }

    [Fact]
    public void IntakeReachesTheShelfOverDaysAndTheTotalIsWhatTheCityOwns()
    {
        var (_, profile, cfg) = Sample();
        var rng = new Rng(7);

        var stock = new CityStock(profile.Equilibrium, 400.0);
        var intakeYesterday = stock.In;

        for (var day = 0; day < 12; day++)
        {
            var next = Economy.TickStock(stock, profile, cfg, rng);

            Assert.True(next.In <= intakeYesterday + 1e-9,
                $"Intake grew on its own from {intakeYesterday:0.0} to {next.In:0.0}.");
            Assert.True(next.In >= 0);
            Assert.Equal(next.Out + next.In, next.Total, 6);

            intakeYesterday = next.In;
            stock = next;
        }

        Assert.True(stock.In < 400.0 * 0.05,
            $"After twelve days {stock.In:0.0} units were still stuck in the intake.");
    }

    [Fact]
    public void AnUntradedCityTicksExactlyAsItDidWithOneStore()
    {
        // The split has to be invisible until somebody sells into a city, or every
        // balance figure in the docs would have moved underneath it.
        var (_, profile, cfg) = Sample();

        var pooled = CityStock.Shelved(profile.Equilibrium);
        var single = profile.Equilibrium;

        var poolRng = new Rng(99);
        var singleRng = new Rng(99);

        for (var day = 0; day < 200; day++)
        {
            pooled = Economy.TickStock(pooled, profile, cfg, poolRng);

            // The pre-split formula, kept here as the reference implementation.
            var next = single + profile.Production - profile.Consumption;
            next += (profile.Equilibrium - next) * cfg.DriftRate;
            next *= 1.0 + singleRng.NextSigned() * cfg.NoiseSigma;
            single = Math.Max(cfg.MinStock, next);

            Assert.Equal(single, pooled.Total, 9);
            Assert.Equal(0.0, pooled.In);
        }
    }
}
