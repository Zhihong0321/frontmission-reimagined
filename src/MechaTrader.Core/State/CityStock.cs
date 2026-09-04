namespace MechaTrader.Core.State;

/// <summary>
/// What a city holds of one good, split into the two places it can sit.
///
/// <see cref="Out"/> is the shelf: what the city has for sale, and the only thing a
/// convoy can buy from. <see cref="In"/> is the intake: what caravans have sold to the
/// city, which it consumes and gradually shelves but does not resell the same day.
///
/// The split is what makes a repeat loop impossible rather than merely unprofitable.
/// Goods sold into a city land in <see cref="In"/>, so they cannot be bought straight
/// back, and the shelf price does not move when you unload. Because the sell price is
/// read off the total while the buy price is read off the shelf alone, and price falls
/// as stock rises, the sell quote can never exceed the buy quote at any holding
/// whatsoever - no tuning, and no crew, can invert it.
///
/// <b>Prices move at the day tick, never inside a deal.</b> <see cref="OpenOut"/> and
/// <see cref="OpenIn"/> are the shelf and intake as the day opened; every quote today
/// reads those, while <see cref="Out"/> and <see cref="In"/> carry what the trades did.
/// The tick folds the two back together. A stock built without opening figures (tests,
/// shocks) prices off its live figures.
///
/// <see cref="OutQuality"/> is the average grade of the shelf, 0–100. Buying the whole
/// pile always takes that average; knowledge only lets a smaller order skip the worse
/// crates, which then lowers what is left.
/// </summary>
public readonly record struct CityStock(
    double Out,
    double In,
    double OutQuality = 70.0,
    double? OpenOut = null,
    double? OpenIn = null)
{
    /// <summary>Everything the city owns of this good, shelved or not.</summary>
    public double Total => Out + In;

    /// <summary>The shelf the day's buy price reads.</summary>
    public double PriceShelf => OpenOut ?? Out;

    /// <summary>The holding the day's sell price reads.</summary>
    public double PriceTotal => (OpenOut ?? Out) + (OpenIn ?? In);

    public static CityStock Shelved(double units, double quality = 70.0) => new(units, 0.0, quality, units, 0.0);

    /// <summary>Freeze today's figures as the ones every quote reads until the next tick.</summary>
    public CityStock Opened() => this with { OpenOut = Out, OpenIn = In };
}
