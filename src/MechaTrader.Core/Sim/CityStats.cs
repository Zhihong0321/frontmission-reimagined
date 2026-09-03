using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>
/// What a city is, as opposed to what it sells.
///
/// There are two kinds of stat here and they behave differently on purpose.
///
/// A <b>vital</b> - population, peacefulness, growth - is authored per city as a
/// founding value and then carried live in <see cref="GameState"/>. Reads go through
/// <see cref="Vital"/>, which falls back to content only when a save has never heard
/// of that stat. Writes go through <c>GameState.SetVital</c> from the command
/// processor — invest and aid are the first things that move one.
///
/// A <b>supply</b> figure is not authored anywhere. It reads a slice of the city's own
/// market and states what the city holds as a percentage of what it would hold if left
/// alone - so it already breathes, every day, without a single event being written. One
/// hundred is nominal; below that the city is short, above it the city is glutted. That
/// makes the number comparable between a mining town and a trade hub, which a raw stock
/// count never is.
///
/// Pure over (state, world) throughout: reading a city's stats must never advance
/// anything, for the same reason building a recruitment board must not.
/// </summary>
public static class CityStats
{
    /// <summary>
    /// One city's live vital, falling back to its founding value. The fallback is what
    /// makes a save written before a vital existed still load: content is the floor
    /// under state, never a competing source of truth.
    /// </summary>
    public static double Vital(GameState state, City city, string vitalId)
        => state.VitalOf(city.Id, vitalId)
           ?? (city.Vitals.TryGetValue(vitalId, out var founding) ? founding : 0.0);

    /// <summary>
    /// The city's vital as it reads right now: stored value plus any active event
    /// overlay, clamped to the catalogue range. Invest and aid write the stored
    /// value through the three-argument form; the page reads this one.
    /// </summary>
    public static double Vital(GameState state, WorldData world, City city, string vitalId)
    {
        var stored = Vital(state, city, vitalId);
        var delta = WorldEvents.VitalDelta(state, world, city.Id, vitalId);
        if (delta == 0.0) return stored;

        var def = world.CityStats.Vital(vitalId);
        return def is null ? stored + delta : Math.Clamp(stored + delta, def.Min, def.Max);
    }

    public static double Founding(City city, string vitalId)
        => city.Vitals.TryGetValue(vitalId, out var value) ? value : 0.0;

    /// <summary>
    /// The band a value falls in - the first one it is under, which is why the loader
    /// insists the list ascends. Null when content declares no bands for this stat.
    /// </summary>
    public static StatBandDef? Band(IReadOnlyList<StatBandDef> bands, double value)
    {
        foreach (var band in bands)
        {
            if (band.UpTo is null || value < band.UpTo.Value) return band;
        }
        return bands.Count > 0 ? bands[^1] : null;
    }

    /// <summary>
    /// One supply figure for one city: what it makes, what it eats, what it is sitting
    /// on and how that compares with its own resting level.
    /// </summary>
    public readonly record struct SupplyReading(
        double Index,
        double Production,
        double Consumption,
        double Stock,
        double Nominal)
    {
        public double NetFlow => Production - Consumption;

        /// <summary>Days the city could run on what it holds, or null if nothing here eats it.</summary>
        public double? DaysOfCover => Consumption > 0 ? Stock / Consumption : null;
    }

    /// <summary>
    /// Reads one supply band off the city's live market.
    ///
    /// Both sides are weighted by base price rather than counted by unit, because a band
    /// can mix a twelve-credit scrap alloy with a ninety-five-credit plate and counting
    /// those as equals would let a heap of scrap paper over a plate shortage. Nominal is
    /// the stock the city would settle at with no convoy interfering, so a city that
    /// structurally imports a good still reads one hundred when nothing is wrong - the
    /// figure says "short of its own normal", not "short compared with a mining town".
    /// </summary>
    public static SupplyReading Supply(GameState state, WorldData world, City city, CitySupplyDef supply)
    {
        var eco = world.Config.Economy;

        double production = 0, consumption = 0, stock = 0, held = 0, nominal = 0;

        foreach (var goodId in supply.Goods)
        {
            if (!world.GoodsById.TryGetValue(goodId, out var good)) continue;
            if (!city.Market.TryGetValue(goodId, out var profile)) continue;

            var onHand = state.TotalStockOf(city.Id, goodId);
            var resting = Math.Max(profile.SteadyStateStock(eco.DriftRate), eco.MinStock);

            production += profile.Production;
            consumption += profile.Consumption;
            stock += onHand;

            held += good.BasePrice * onHand;
            nominal += good.BasePrice * resting;
        }

        var index = nominal > 0 ? 100.0 * held / nominal : 0.0;

        return new SupplyReading(index, production, consumption, stock, nominal);
    }
}
