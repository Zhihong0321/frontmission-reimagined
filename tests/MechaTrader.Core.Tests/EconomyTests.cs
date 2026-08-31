using MechaTrader.Core.Sim;
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

        var buy = Economy.BuyUnitPrice(good, profile, profile.Equilibrium, cfg);
        var sell = Economy.SellUnitPrice(good, profile, profile.Equilibrium, cfg);

        Assert.True(buy > sell, "The spread must make an in-place round trip a loss.");
    }

    [Fact]
    public void BuyingDrainsStockAndSellingRefillsIt()
    {
        var (good, profile, cfg) = Sample();
        var stock = profile.Equilibrium;

        Assert.True(Economy.QuoteBuy(good, profile, stock, 50, cfg).ResultingStock < stock);
        Assert.True(Economy.QuoteSell(good, profile, stock, 50, cfg).ResultingStock > stock);
    }

    [Fact]
    public void LargeOrdersMoveThePriceAgainstYou()
    {
        // The property the whole trade loop rests on: you cannot dump a full hold at
        // the price the first unit quoted.
        var (good, profile, cfg) = Sample();
        var stock = profile.Equilibrium;

        var exactCost = Economy.QuoteBuy(good, profile, stock, 200, cfg).Total;
        var marginalCost = Economy.EstimateBuyCost(good, profile, stock, 200, cfg);
        Assert.True(exactCost > marginalCost, "Buying 200 units should cost more than 200x the first unit.");

        var exactRevenue = Economy.QuoteSell(good, profile, stock, 200, cfg).Total;
        var marginalRevenue = Economy.EstimateSellRevenue(good, profile, stock, 200, cfg);
        Assert.True(exactRevenue < marginalRevenue, "Selling 200 units should earn less than 200x the first unit.");
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
        var stock = profile.Equilibrium;

        var exactCost = (double)Economy.QuoteBuy(good, profile, stock, units, cfg).Total;
        var approxCost = Economy.ApproximateBuyCost(good, profile, stock, units, cfg);
        Assert.True(Math.Abs(approxCost - exactCost) / exactCost < 0.03,
            $"Buy approximation off by {Math.Abs(approxCost - exactCost) / exactCost:P1}.");

        var exactRevenue = (double)Economy.QuoteSell(good, profile, stock, units, cfg).Total;
        var approxRevenue = Economy.ApproximateSellRevenue(good, profile, stock, units, cfg);
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
        var stock = profile.Equilibrium;

        var volumeLimited = Economy.MaxAffordableUnits(good, profile, stock, 10_000_000, 25, cfg);
        Assert.Equal((int)(25 / good.UnitVolume), volumeLimited);

        var cashLimited = Economy.MaxAffordableUnits(good, profile, stock, 500, 100_000, cfg);
        var cost = Economy.QuoteBuy(good, profile, stock, cashLimited, cfg).Total;
        Assert.True(cost <= 500, $"Affordable order cost {cost} but only 500 was available.");

        var oneMore = Economy.QuoteBuy(good, profile, stock, cashLimited + 1, cfg).Total;
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
        var stock = 10.0;
        for (var i = 0; i < 2000; i++) stock = Economy.TickStock(stock, profile, cfg, rng);

        var predicted = profile.SteadyStateStock(cfg.DriftRate);
        Assert.True(Math.Abs(stock - predicted) / predicted < 0.10,
            $"Settled at {stock:0} but predicted {predicted:0}.");
    }
}
