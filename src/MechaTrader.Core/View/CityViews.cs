namespace MechaTrader.Core.View;
/// <summary>
/// The city page: who this place is, how it is doing, and what it is hearing.
///
/// Everything on it arrives already resolved - a vital carries the string to print as
/// well as the number, and a supply figure carries the band it falls in. The front-end
/// draws a bar and a label; it does not know what peacefulness is or when a power grid
/// counts as strained.
/// </summary>
public sealed record LocationView(
    string Id,
    string Name,
    string Region,
    IReadOnlyList<string> Industries,
    CityStandingView Standing,
    IReadOnlyList<CityVitalView> Vitals,
    IReadOnlyList<CitySupplyView> Supplies,
    IReadOnlyList<CityNewsView> News);

/// <summary>
/// How the city holds you, segment by segment, and what the total is worth. Every
/// number arrives resolved: the front-end draws bars and some buttons, it does not
/// know what Favored means or how much of the shelf is held back.
/// </summary>
public sealed record CityStandingView(
    string GovernorName,
    string GovernorTitle,
    // The total across every segment; rank, permits, reserve and tier locks read this.
    double Value,
    double Max,
    string Rank,
    string Tone,
    double Fill,
    string ReservedDisplay,
    double ReservedRatio,
    IReadOnlyList<StandingSegmentView> Segments,
    IReadOnlyList<CityPermitView> Permits,
    IReadOnlyList<CityFavorActionView> Actions,
    IReadOnlyList<TierGateView> TierGates);

public sealed record StandingSegmentView(
    string Id,
    string Name,
    string Blurb,
    double Value,
    double Max,
    double Fill);

/// <summary>Whether this city sells a grade to the player today, and what it would take.</summary>
public sealed record TierGateView(
    int Tier,
    string Name,
    string Color,
    double MinStanding,
    bool Open,
    double ToGo);

public sealed record CityPermitView(
    string Id,
    string Name,
    string Blurb,
    double StandingRequired,
    bool Granted);

public sealed record CityFavorActionView(
    string Id,
    string Name,
    string Blurb,
    long Cost,
    bool Affordable,
    string SegmentName,
    string EffectText);

/// <summary>
/// One authored city stat. <see cref="Value"/> is the live figure and
/// <see cref="Founding"/> is where the city started, so a page can show how far a place
/// has moved since the run began.
/// </summary>
public sealed record CityVitalView(
    string Id,
    string Name,
    // Ready to print, scaled and signed as content asked: "6.0M", "58%", "+2.4%/yr".
    string Display,
    // Same formatting as Display, for the founding value. The page prints this so the
    // browser never has to know the scale.
    string FoundingDisplay,
    string Unit,
    string Blurb,
    // What the number means, and how it should read: bad, warn, ok, good or muted.
    string Band,
    string Tone,
    double Value,
    double Founding,
    double Delta,
    string DeltaDisplay,
    // Where the value sits in its declared range, 0 to 1. Meter length, nothing more.
    double Fill);

/// <summary>
/// One supply figure, read off the city's own market. <see cref="Index"/> is a
/// percentage of what the city would be holding if no convoy had ever called: 100 is
/// nominal, low is short, high is glutted.
/// </summary>
public sealed record CitySupplyView(
    string Id,
    string Name,
    string Blurb,
    string Band,
    string Tone,
    double Index,
    // Nominal sits at the half-way mark, so a full bar reads as twice a normal holding.
    double Fill,
    double Production,
    double Consumption,
    double NetFlow,
    double Stock,
    // Days the city could run on what it holds; null when nothing here consumes any of it.
    double? DaysOfCover,
    // "surplus", "deficit" or "balanced" - whether the city makes or eats this band.
    string Flow,
    IReadOnlyList<string> Goods);

/// <summary>
/// One dispatch on the city wire. Built from the active event set; an empty list
/// means the wire is actually quiet, not that the page should invent copy.
/// </summary>
public sealed record CityNewsView(
    int Day,
    string Kind,
    string Tone,
    string Headline,
    string Detail,
    int DaysLeft);

