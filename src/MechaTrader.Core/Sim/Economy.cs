using MechaTrader.Core.Model;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>Result of pricing an order: what it costs and where it leaves local stock.</summary>
public readonly record struct Quote(int Units, long Total, double ResultingStock)
{
    public double UnitAverage => Units > 0 ? (double)Total / Units : 0.0;
}

/// <summary>
/// The price model.
///
/// Price is emergent from stock rather than a random walk: a city that produces a good
/// settles above its equilibrium and sells it cheap, a city that consumes one settles
/// below and pays dear. That single relationship generates the entire trade map without
/// anyone hand-authoring a price table, and it is why dumping a full hold craters the
/// local market — each unit sold is priced against the stock left by the one before it.
/// </summary>
public static class Economy
{
    public static double UnitPrice(GoodDef good, CityGoodProfile profile, double stock, EconomyConfig cfg)
    {
        var effective = Math.Max(stock, cfg.MinStock);
        var ratio = profile.Equilibrium / effective;
        var mult = Math.Clamp(Math.Pow(ratio, good.Elasticity), cfg.MinPriceMult, cfg.MaxPriceMult);
        return good.BasePrice * profile.PriceModifier * mult;
    }

    public static double BuyUnitPrice(GoodDef good, CityGoodProfile profile, double stock, EconomyConfig cfg)
        => UnitPrice(good, profile, stock, cfg) * (1.0 + cfg.Spread);

    public static double SellUnitPrice(GoodDef good, CityGoodProfile profile, double stock, EconomyConfig cfg)
        => UnitPrice(good, profile, stock, cfg) * (1.0 - cfg.Spread);

    /// <summary>
    /// Exact cost of buying <paramref name="units"/>, walking the price up as stock drains.
    /// </summary>
    public static Quote QuoteBuy(GoodDef good, CityGoodProfile profile, double stock, int units, EconomyConfig cfg)
    {
        if (units <= 0) return new Quote(0, 0, stock);

        double total = 0;
        var s = stock;

        for (var i = 0; i < units; i++)
        {
            total += BuyUnitPrice(good, profile, s, cfg);
            s = Math.Max(cfg.MinStock, s - 1.0);
        }

        return new Quote(units, (long)Math.Round(total), s);
    }

    /// <summary>
    /// Exact revenue from selling <paramref name="units"/>, walking the price down as
    /// stock fills. Sell 500 into a small market and the last unit is worth a fraction
    /// of the first.
    /// </summary>
    public static Quote QuoteSell(GoodDef good, CityGoodProfile profile, double stock, int units, EconomyConfig cfg)
    {
        if (units <= 0) return new Quote(0, 0, stock);

        double total = 0;
        var s = stock;

        for (var i = 0; i < units; i++)
        {
            s += 1.0;
            total += SellUnitPrice(good, profile, s, cfg);
        }

        return new Quote(units, (long)Math.Round(total), s);
    }

    /// <summary>
    /// Marginal price times quantity. Only honest for small orders: it ignores the fact
    /// that a large order moves the price against you. Used for display, never for
    /// planning a full hold.
    /// </summary>
    public static double EstimateBuyCost(GoodDef good, CityGoodProfile profile, double stock, int units, EconomyConfig cfg)
        => BuyUnitPrice(good, profile, stock, cfg) * units;

    public static double EstimateSellRevenue(GoodDef good, CityGoodProfile profile, double stock, int units, EconomyConfig cfg)
        => SellUnitPrice(good, profile, stock, cfg) * units;

    private const int DefaultApproximationSteps = 8;

    /// <summary>
    /// Midpoint approximation of <see cref="QuoteBuy"/>, accurate to about a percent but
    /// independent of order size. Planning code ranks hundreds of candidate orders per
    /// decision, and walking every unit for each would dominate the frame; settlement
    /// still goes through the exact quote.
    /// </summary>
    public static double ApproximateBuyCost(
        GoodDef good, CityGoodProfile profile, double stock, int units, EconomyConfig cfg,
        int steps = DefaultApproximationSteps)
    {
        if (units <= 0) return 0;

        steps = Math.Clamp(Math.Min(steps, units), 1, DefaultApproximationSteps);
        var chunk = (double)units / steps;

        double total = 0;
        var s = stock;

        for (var i = 0; i < steps; i++)
        {
            var midpoint = Math.Max(cfg.MinStock, s - chunk * 0.5);
            total += BuyUnitPrice(good, profile, midpoint, cfg) * chunk;
            s = Math.Max(cfg.MinStock, s - chunk);
        }

        return total;
    }

    /// <summary>Midpoint approximation of <see cref="QuoteSell"/>. See <see cref="ApproximateBuyCost"/>.</summary>
    public static double ApproximateSellRevenue(
        GoodDef good, CityGoodProfile profile, double stock, int units, EconomyConfig cfg,
        int steps = DefaultApproximationSteps)
    {
        if (units <= 0) return 0;

        steps = Math.Clamp(Math.Min(steps, units), 1, DefaultApproximationSteps);
        var chunk = (double)units / steps;

        double total = 0;
        var s = stock;

        for (var i = 0; i < steps; i++)
        {
            var midpoint = s + chunk * 0.5;
            total += SellUnitPrice(good, profile, midpoint, cfg) * chunk;
            s += chunk;
        }

        return total;
    }

    /// <summary>Largest order affordable within a cash and volume budget.</summary>
    public static int MaxAffordableUnits(
        GoodDef good, CityGoodProfile profile, double stock, long cash, double freeVolume, EconomyConfig cfg)
    {
        var volumeCap = good.UnitVolume > 0 ? (int)Math.Floor(freeVolume / good.UnitVolume) : int.MaxValue;
        if (volumeCap <= 0 || cash <= 0) return 0;

        double spent = 0;
        var s = stock;
        var units = 0;

        while (units < volumeCap)
        {
            var next = BuyUnitPrice(good, profile, s, cfg);
            if (spent + next > cash) break;
            spent += next;
            s = Math.Max(cfg.MinStock, s - 1.0);
            units++;
        }

        return units;
    }

    /// <summary>
    /// One day of local production, consumption, outside-world trade and noise.
    /// </summary>
    public static double TickStock(double stock, CityGoodProfile profile, EconomyConfig cfg, Rng rng)
    {
        var next = stock + profile.Production - profile.Consumption;
        next += (profile.Equilibrium - next) * cfg.DriftRate;

        if (cfg.NoiseSigma > 0)
            next *= 1.0 + rng.NextSigned() * cfg.NoiseSigma;

        return Math.Max(cfg.MinStock, next);
    }

    /// <summary>
    /// Where a market sits once transients have settled. Used to open a new game on a
    /// living economy rather than on an artificial day-zero flat line.
    /// </summary>
    public static double InitialStock(CityGoodProfile profile, EconomyConfig cfg)
        => Math.Max(cfg.MinStock, profile.SteadyStateStock(cfg.DriftRate));
}
