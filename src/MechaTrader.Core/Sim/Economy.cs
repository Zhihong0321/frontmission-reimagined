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
///
/// Prices move at the day tick, never inside a deal. Every quote reads the shelf and
/// intake as the day opened (<see cref="CityStock.PriceShelf"/>, <see cref="CityStock.PriceTotal"/>);
/// an order settles at one price for the whole lot and bulk is never penalised. What a
/// deal does is move the stock, so tomorrow meets a different shelf. Depth lives in the
/// size of the stocks, not in a walk inside the order.
/// </summary>
public static class Economy
{
    public static double UnitPrice(
        GoodDef good, CityGoodProfile profile, double stock, EconomyConfig cfg, double eventMult = 1.0)
    {
        var effective = Math.Max(stock, cfg.MinStock);
        var ratio = profile.Equilibrium / effective;
        var mult = Math.Clamp(Math.Pow(ratio, good.Elasticity), cfg.MinPriceMult, cfg.MaxPriceMult);
        return good.BasePrice * profile.PriceModifier * eventMult * mult;
    }

    /// <summary>
    /// What the convoy actually pays. <paramref name="terms"/> is how much of the
    /// market's spread it still concedes after the crew have argued; it can shrink the
    /// spread to nothing but never invert it, so buying and selling in the same city is
    /// at best free and never a profit.
    /// </summary>
    /// <summary>Priced off the shelf as the day opened: you can only be sold what is on it.</summary>
    public static double BuyUnitPrice(
        GoodDef good, CityGoodProfile profile, CityStock stock, EconomyConfig cfg, TradeTerms terms,
        double eventMult = 1.0)
        => UnitPrice(good, profile, stock.PriceShelf, cfg, eventMult) * (1.0 + cfg.Spread * terms.BuySpreadShare);

    /// <summary>Priced off everything the city held as the day opened, shelved or not.</summary>
    public static double SellUnitPrice(
        GoodDef good, CityGoodProfile profile, CityStock stock, EconomyConfig cfg, TradeTerms terms,
        double eventMult = 1.0)
        => UnitPrice(good, profile, stock.PriceTotal, cfg, eventMult) * (1.0 - cfg.Spread * terms.SellSpreadShare);

    /// <summary>
    /// How many units the city can actually hand over, keeping the shelf above the floor
    /// the price model needs.
    /// </summary>
    public static int UnitsOnTheShelf(CityStock stock, EconomyConfig cfg)
        => (int)Math.Floor(Math.Max(0.0, stock.Out - cfg.MinStock));

    /// <summary>
    /// Cost of buying <paramref name="units"/>: one price for the whole lot, the day's
    /// price. The shelf drains by the order, so tomorrow's shelf is scarcer and dearer;
    /// bulk is never penalised inside a deal or inside a day.
    /// </summary>
    public static Quote QuoteBuy(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms,
        double eventMult = 1.0)
    {
        if (units <= 0) return new Quote(0, 0, stock);

        var total = BuyUnitPrice(good, profile, stock, cfg, terms, eventMult) * units;
        var resulting = stock with { Out = Math.Max(cfg.MinStock, stock.Out - units) };
        return new Quote(units, (long)Math.Round(total), resulting);
    }

    /// <summary>
    /// Revenue from selling <paramref name="units"/>: one price for the whole lot, the
    /// day's price. The lot lands in the intake, so tomorrow the city is fuller and pays
    /// less. Sell ≤ buy still holds per unit at every holding (see the class remarks).
    /// </summary>
    public static Quote QuoteSell(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms,
        double eventMult = 1.0)
    {
        if (units <= 0) return new Quote(0, 0, stock);

        var total = SellUnitPrice(good, profile, stock, cfg, terms, eventMult) * units;
        // Straight into the intake: the shelf, and so the buy price, does not move.
        var resulting = stock with { In = stock.In + units };
        return new Quote(units, (long)Math.Round(total), resulting);
    }

    /// <summary>Unit price times quantity: exactly what a single order settles at.</summary>
    public static double EstimateBuyCost(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms,
        double eventMult = 1.0)
        => BuyUnitPrice(good, profile, stock, cfg, terms, eventMult) * units;

    public static double EstimateSellRevenue(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms,
        double eventMult = 1.0)
        => SellUnitPrice(good, profile, stock, cfg, terms, eventMult) * units;

    /// <summary>
    /// Planning cost of an order. Orders settle flat at the day's price, so this is the
    /// unit price times the quantity; kept as a named call so planners and settlement
    /// read one rule.
    /// </summary>
    public static double ApproximateBuyCost(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms,
        double eventMult = 1.0)
        => units <= 0 ? 0 : BuyUnitPrice(good, profile, stock, cfg, terms, eventMult) * units;

    /// <summary>Planning revenue of an order. See <see cref="ApproximateBuyCost"/>.</summary>
    public static double ApproximateSellRevenue(
        GoodDef good, CityGoodProfile profile, CityStock stock, int units, EconomyConfig cfg, TradeTerms terms,
        double eventMult = 1.0)
        => units <= 0 ? 0 : SellUnitPrice(good, profile, stock, cfg, terms, eventMult) * units;

    /// <summary>
    /// Largest order affordable within a cash and volume budget, and within what the
    /// city actually has on the shelf. <paramref name="gradeMult"/> is the shop's grade
    /// premium on the crates taken (see <c>QualityMath.SellMultiplier</c>); pass the
    /// best single crate's multiplier to size conservatively.
    /// </summary>
    public static int MaxAffordableUnits(
        GoodDef good, CityGoodProfile profile, CityStock stock, long cash, double freeVolume, EconomyConfig cfg,
        TradeTerms terms, double eventMult = 1.0, double gradeMult = 1.0)
    {
        var volumeCap = good.UnitVolume > 0 ? (int)Math.Floor(freeVolume / good.UnitVolume) : int.MaxValue;
        var cap = Math.Min(volumeCap, UnitsOnTheShelf(stock, cfg));
        if (cap <= 0 || cash <= 0) return 0;

        var unit = BuyUnitPrice(good, profile, stock, cfg, terms, eventMult) * Math.Max(0.0, gradeMult);
        if (unit <= 0) return cap;
        return Math.Min(cap, (int)Math.Floor(cash / unit));
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
    public static CityStock TickStock(
        CityStock stock, CityGoodProfile profile, EconomyConfig cfg, Rng rng, double nominalQuality = 70.0)
    {
        var intake = stock.In;
        var shelf = stock.Out;
        var quality = stock.OutQuality;

        var eatenFromIntake = Math.Min(intake, profile.Consumption);
        intake -= eatenFromIntake;
        var shelfDelta = profile.Production - (profile.Consumption - eatenFromIntake);
        if (shelfDelta > 0)
        {
            quality = QualityMath.BlendAdded(shelf, quality, shelfDelta, nominalQuality);
            shelf += shelfDelta;
        }
        else
        {
            shelf += shelfDelta;
        }

        var shelved = intake * cfg.RestockRate;
        intake -= shelved;
        quality = QualityMath.BlendAdded(shelf, quality, shelved, nominalQuality);
        shelf += shelved;

        // Trade with the outside world settles the city's whole holding, and lands on
        // the shelf, which is where the outside world buys and sells.
        var drift = (profile.Equilibrium - (shelf + intake)) * cfg.DriftRate;
        if (drift > 0)
        {
            quality = QualityMath.BlendAdded(shelf, quality, drift, nominalQuality);
            shelf += drift;
        }
        else
        {
            shelf += drift;
        }

        if (cfg.NoiseSigma > 0)
            shelf *= 1.0 + rng.NextSigned() * cfg.NoiseSigma;

        shelf = Math.Max(cfg.MinStock, shelf);
        // Today's figures are tomorrow's prices: the tick is the only place a quote moves.
        return new CityStock(shelf, Math.Max(0.0, intake), Math.Clamp(quality, 0.0, 100.0)).Opened();
    }

    /// <summary>
    /// Where a market sits once transients have settled. Used to open a new game on a
    /// living economy rather than on an artificial day-zero flat line.
    /// </summary>
    public static double InitialStock(CityGoodProfile profile, EconomyConfig cfg)
        => Math.Max(cfg.MinStock, profile.SteadyStateStock(cfg.DriftRate));
}
