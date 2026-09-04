namespace MechaTrader.Core.View;
/// <summary>
/// The crew's quick market brief when the convoy parks in a city: every good in the
/// hold that would clear a worthwhile margin if sold here, biggest margin first.
/// Built only while parked in a city and the feature is enabled; empty otherwise.
/// </summary>
public sealed record CrewBriefView(
    // The floor on the margin, as a fraction (0.035 = 3.5%). Content passes through.
    double MinMargin,
    IReadOnlyList<CrewBriefRowView> Rows);

/// <summary>One line of the crew's market brief: a held good, and what selling it here would clear.</summary>
public sealed record CrewBriefRowView(
    string GoodId,
    string Name,
    string Category,
    int Units,
    double AverageCost,
    double Sell,
    // Null when the lot cost nothing (mined): any offer is pure gain.
    double? MarginPct,
    long Profit);

/// <summary>
/// One line of the map's sell outlook: what the convoy's whole hold would clear if
/// sold in this city today, priced at that city's market and net of what was paid
/// for the lots. Only cities that would turn a profit are listed.
/// </summary>
public sealed record SellOutlookView(string CityId, long Profit);

