using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>Result of pricing an order: what it costs and where it leaves local stock.</summary>
public readonly record struct Quote(int Units, long Total, CityStock ResultingStock)
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
///
/// A city holds its goods in two places (see <see cref="CityStock"/>) and the two sides
/// of the market read different ones. The **buy** price is quoted off the shelf, because
/// that is what is actually for sale. The **sell** price is quoted off everything the
/// city owns, because a city that has just taken three hundred units off another caravan
/// will not pay you well for more, shelved or not.
///
/// That asymmetry is load-bearing: since price falls as stock rises and the total is
/// never less than the shelf, the sell quote can never exceed the buy quote. No amount of
/// crew skill or retuning can turn standing still into an income.
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

    /// <summary>
    /// What the convoy actually pays. <paramref name="terms"/> is how much of the
    /// market's spread it still concedes after the crew have argued; it can shrink the
    /// spread to nothing but never invert it, so buying and selling in the same city is
    /// at best free and never a profit.
    /// </summary>
    /// <summary>Priced off the shelf: you can only be sold what is on it.</summary>
    public static double BuyUnitPrice(
        GoodDef good, CityGoodProfile profile, CityStock stock, EconomyConfig cfg, TradeTerms terms)
        => UnitPrice(good, profile, stock.Out, cfg) * (1.0 + cfg.Spread * terms.BuySpreadShare);

    /// <summary>Priced off everything the city holds, shelved or not.</summary>
    public static double SellUnitPrice(
        GoodDef good, CityGoodProfile profile, CityStock stock, EconomyConfig cfg, TradeTerms terms)
        => UnitPrice(good, profile, stock.Total, cfg) * (1.0 - cfg.Spread * terms.SellSpreadShare);

    /// <summary>
    /// How many units the city can actually hand over, keeping the shelf above the floor
    /// the price model needs.
    /// </summary>
    public static int UnitsOnTheShelf(CityStock stock, EconomyConfig cfg)
        => (int)Math.Floor(Math.Max(0.0, stock.Out - cfg.MinStock));

    /// <summary>
    /// Exact cost of buying <paramref name="units"/>, walking the price up as stock drains.
    /// </summary>
    public static Quote QuoteBuy(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms)
    {
        if (units <= 0) return new Quote(0, 0, stock);

        double total = 0;
        var s = stock;

        for (var i = 0; i < units; i++)
        {
            total += BuyUnitPrice(good, profile, s, cfg, terms);
            s = s with { Out = Math.Max(cfg.MinStock, s.Out - 1.0) };
        }

        return new Quote(units, (long)Math.Round(total), s);
    }

    /// <summary>
    /// Exact revenue from selling <paramref name="units"/>, walking the price down as
    /// stock fills. Sell 500 into a small market and the last unit is worth a fraction
    /// of the first.
    /// </summary>
    public static Quote QuoteSell(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms)
    {
        if (units <= 0) return new Quote(0, 0, stock);

        double total = 0;
        var s = stock;

        for (var i = 0; i < units; i++)
        {
            // Straight into the intake: the shelf, and so the buy price, does not move.
            s = s with { In = s.In + 1.0 };
            total += SellUnitPrice(good, profile, s, cfg, terms);
        }

        return new Quote(units, (long)Math.Round(total), s);
    }

    /// <summary>
    /// Marginal price times quantity. Only honest for small orders: it ignores the fact
    /// that a large order moves the price against you. Used for display, never for
    /// planning a full hold.
    /// </summary>
    public static double EstimateBuyCost(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms)
        => BuyUnitPrice(good, profile, stock, cfg, terms) * units;

    public static double EstimateSellRevenue(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms)
        => SellUnitPrice(good, profile, stock, cfg, terms) * units;

    private const int DefaultApproximationSteps = 8;

    /// <summary>
    /// Midpoint approximation of <see cref="QuoteBuy"/>, accurate to about a percent but
    /// independent of order size. Planning code ranks hundreds of candidate orders per
    /// decision, and walking every unit for each would dominate the frame; settlement
    /// still goes through the exact quote.
    /// </summary>
    public static double ApproximateBuyCost(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms,
        int steps = DefaultApproximationSteps)
    {
        if (units <= 0) return 0;

        steps = Math.Clamp(Math.Min(steps, units), 1, DefaultApproximationSteps);
        var chunk = (double)units / steps;

        double total = 0;
        var s = stock;

        for (var i = 0; i < steps; i++)
        {
            var midpoint = s with { Out = Math.Max(cfg.MinStock, s.Out - chunk * 0.5) };
            total += BuyUnitPrice(good, profile, midpoint, cfg, terms) * chunk;
            s = s with { Out = Math.Max(cfg.MinStock, s.Out - chunk) };
        }

        return total;
    }

    /// <summary>Midpoint approximation of <see cref="QuoteSell"/>. See <see cref="ApproximateBuyCost"/>.</summary>
    public static double ApproximateSellRevenue(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms,
        int steps = DefaultApproximationSteps)
    {
        if (units <= 0) return 0;

        steps = Math.Clamp(Math.Min(steps, units), 1, DefaultApproximationSteps);
        var chunk = (double)units / steps;

        double total = 0;
        var s = stock;

        for (var i = 0; i < steps; i++)
        {
            var midpoint = s with { In = s.In + chunk * 0.5 };
            total += SellUnitPrice(good, profile, midpoint, cfg, terms) * chunk;
            s = s with { In = s.In + chunk };
        }

        return total;
    }

    /// <summary>
    /// Largest order affordable within a cash and volume budget, and within what the
    /// city actually has on the shelf.
    /// </summary>
    public static int MaxAffordableUnits(
        GoodDef good, CityGoodProfile profile, CityStock stock, long cash, double freeVolume, EconomyConfig cfg,
        TradeTerms terms)
    {
        var volumeCap = good.UnitVolume > 0 ? (int)Math.Floor(freeVolume / good.UnitVolume) : int.MaxValue;
        var cap = Math.Min(volumeCap, UnitsOnTheShelf(stock, cfg));
        if (cap <= 0 || cash <= 0) return 0;

        double spent = 0;
        var s = stock;
        var units = 0;

        while (units < cap)
        {
            var next = BuyUnitPrice(good, profile, s, cfg, terms);
            if (spent + next > cash) break;
            spent += next;
            s = s with { Out = Math.Max(cfg.MinStock, s.Out - 1.0) };
            units++;
        }

        return units;
    }

    /// <summary>
    /// One day of local production, consumption, restocking, outside-world trade and
    /// noise.
    ///
    /// The city eats out of its intake before touching the shelf - it bought those goods
    /// because it needed them - then shelves a fraction of what is left, at
    /// <see cref="EconomyConfig.RestockRate"/>. That lag is the whole point: what a
    /// convoy unloads is not back on sale the same day.
    ///
    /// With an empty intake this reduces exactly to the old single-pool tick, and draws
    /// the same single random number, so a world nobody has traded in behaves and
    /// replays identically.
    /// </summary>
    public static CityStock TickStock(CityStock stock, CityGoodProfile profile, EconomyConfig cfg, Rng rng)
    {
        var intake = stock.In;
        var shelf = stock.Out;

        var eatenFromIntake = Math.Min(intake, profile.Consumption);
        intake -= eatenFromIntake;
        shelf += profile.Production - (profile.Consumption - eatenFromIntake);

        var shelved = intake * cfg.RestockRate;
        intake -= shelved;
        shelf += shelved;

        // Trade with the outside world settles the city's whole holding, and lands on
        // the shelf, which is where the outside world buys and sells.
        shelf += (profile.Equilibrium - (shelf + intake)) * cfg.DriftRate;

        if (cfg.NoiseSigma > 0)
            shelf *= 1.0 + rng.NextSigned() * cfg.NoiseSigma;

        return new CityStock(Math.Max(cfg.MinStock, shelf), Math.Max(0.0, intake));
    }

    /// <summary>
    /// Where a market sits once transients have settled. Used to open a new game on a
    /// living economy rather than on an artificial day-zero flat line.
    /// </summary>
    public static double InitialStock(CityGoodProfile profile, EconomyConfig cfg)
        => Math.Max(cfg.MinStock, profile.SteadyStateStock(cfg.DriftRate));
}
