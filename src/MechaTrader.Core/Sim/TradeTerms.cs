namespace MechaTrader.Core.Sim;

/// <summary>
/// How much of the market's spread the convoy actually pays.
///
/// Crew never move the mid price - that belongs to the city - they only erode the cut
/// taken on either side of it. Expressing the bonus as a share of the spread rather
/// than as a discount on the price makes an arbitrage loop impossible by construction:
/// both shares are clamped to [0, 1], so the buy price can never fall below the mid
/// price and the sell price can never rise above it, no matter how good the crew get.
/// </summary>
public readonly record struct TradeTerms
{
    /// <summary>Fraction of the buy-side spread still paid. 1 = no help, 0 = pays mid.</summary>
    public double BuySpreadShare { get; }

    /// <summary>Fraction of the sell-side spread still conceded. 1 = no help, 0 = gets mid.</summary>
    public double SellSpreadShare { get; }

    public TradeTerms(double buySpreadShare, double sellSpreadShare)
    {
        BuySpreadShare = Math.Clamp(buySpreadShare, 0.0, 1.0);
        SellSpreadShare = Math.Clamp(sellSpreadShare, 0.0, 1.0);
    }

    /// <summary>Terms with no crew behind them: the market's full spread, both ways.</summary>
    public static TradeTerms Market => new(1.0, 1.0);
}
