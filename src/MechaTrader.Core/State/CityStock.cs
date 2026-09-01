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
/// read off <see cref="Total"/> while the buy price is read off <see cref="Out"/> alone,
/// and price falls as stock rises, the sell quote can never exceed the buy quote at any
/// holding whatsoever - no tuning, and no crew, can invert it.
/// </summary>
public readonly record struct CityStock(double Out, double In)
{
    /// <summary>Everything the city owns of this good, shelved or not.</summary>
    public double Total => Out + In;

    public static CityStock Shelved(double units) => new(units, 0.0);
}
