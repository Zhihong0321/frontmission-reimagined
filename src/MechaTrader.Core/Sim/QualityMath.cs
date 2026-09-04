using MechaTrader.Core.Model;
using MechaTrader.Core.State;

namespace MechaTrader.Core.Sim;

/// <summary>
/// Shop grade and what a selected crate is worth on the sell.
///
/// The pile has an average. Knowledge never rewrites that average. Buying the whole
/// saleable shelf always takes it. A smaller order with knowledge skips the worse
/// crates (the uniform top-k of the pile, interpolated from random toward perfect
/// selection), and conservation then lowers what is left on the shelf.
///
/// What a city makes grades base + a random roll + its craft: a master works floor
/// turns out crates that can reach S-tier on their own; a rough one never does.
/// </summary>
public static class QualityMath
{
    public static bool IsSTier(double quality, QualityConfig cfg)
        => quality + 1e-9 >= cfg.STierAt;

    /// <summary>
    /// The grade of a crate made today in a city whose craft vital reads
    /// <paramref name="craft"/> (0 to 100), given a uniform roll in [0, 1).
    /// </summary>
    public static double ProductionQuality(QualityConfig cfg, double craft, double roll)
    {
        var grade = cfg.Base
                    + Math.Clamp(roll, 0.0, 1.0) * Math.Max(0.0, cfg.Random)
                    + Math.Clamp(craft, 0.0, 100.0) / 100.0 * Math.Max(0.0, cfg.CityVitalWeight);
        return Math.Clamp(grade, 0.0, 100.0);
    }

    /// <summary>
    /// The grade a city's shelf opens on: the average of its production roll, so a new
    /// world is already graded the way its works floors would grade it. No RNG.
    /// </summary>
    public static double OpeningQuality(QualityConfig cfg, double craft)
        => ProductionQuality(cfg, craft, 0.5);

    /// <summary>
    /// Sell-price multiplier for a lot of this grade. Nominal quality is 1.0; S-tier
    /// is 1 + <see cref="QualityConfig.STierSellBonus"/>. Between them it rises linearly.
    /// Below nominal it eases off toward 0.85 so a bad crate still sells, just poorly.
    /// </summary>
    public static double SellMultiplier(double quality, QualityConfig cfg)
    {
        if (quality + 1e-9 >= cfg.STierAt) return 1.0 + cfg.STierSellBonus;

        var nominal = Math.Max(1.0, cfg.Nominal);
        if (quality <= nominal)
            return 0.85 + 0.15 * Math.Clamp(quality / nominal, 0.0, 1.0);

        var span = Math.Max(1e-9, cfg.STierAt - nominal);
        var t = Math.Clamp((quality - nominal) / span, 0.0, 1.0);
        return 1.0 + t * cfg.STierSellBonus;
    }

    /// <summary>
    /// Grade of <paramref name="take"/> crates drawn from a saleable pile of
    /// <paramref name="saleable"/> at average <paramref name="average"/>, with
    /// <paramref name="knowledge"/> in [0, 1]. Buying the whole pile always returns
    /// the average, even at knowledge 1.
    /// </summary>
    public static double SelectedQuality(
        double average, int saleable, int take, double knowledge, QualityConfig cfg)
    {
        if (take <= 0) return average;
        var n = Math.Max(1, saleable);
        take = Math.Clamp(take, 1, n);
        if (take >= n) return average;

        var half = Math.Min(Math.Max(0.0, cfg.Spread), Math.Min(average, 100.0 - average));
        var lo = average - half;
        var hi = average + half;

        // Mean of the top-k order statistics of n i.i.d. Uniform[0,1], mapped onto [lo, hi].
        var perfectU = (2.0 * n - take + 1.0) / (2.0 * (n + 1.0));
        var perfect = lo + (hi - lo) * perfectU;
        var k = Math.Clamp(knowledge, 0.0, 1.0);
        return average + (perfect - average) * k;
    }

    /// <summary>
    /// Take <paramref name="units"/> off the shelf. Returns the grade of what was taken
    /// and the shelf afterwards (count already quoted, quality conserved).
    /// </summary>
    public static (double Selected, CityStock Resulting) Take(
        CityStock stock, int saleable, int units, double knowledge, QualityConfig cfg, CityStock quoted)
    {
        var selected = SelectedQuality(stock.OutQuality, saleable, units, knowledge, cfg);
        var remainingOut = quoted.Out;
        var remainMass = stock.Out * stock.OutQuality - units * selected;
        var remainQ = remainingOut > 1e-9
            ? Math.Clamp(remainMass / remainingOut, 0.0, 100.0)
            : stock.OutQuality;
        return (selected, quoted with { OutQuality = remainQ });
    }

    public static double BlendAdded(double currentUnits, double currentQuality, double added, double addedQuality)
    {
        var next = currentUnits + added;
        if (next <= 1e-9) return addedQuality;
        if (added <= 0) return currentQuality;
        return Math.Clamp((currentUnits * currentQuality + added * addedQuality) / next, 0.0, 100.0);
    }
}
