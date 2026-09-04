namespace MechaTrader.Core.Model;

public sealed class EconomyConfig
{
    /// <summary>Pull toward equilibrium per day, representing trade with the outside world.</summary>
    public double DriftRate { get; init; } = 0.25;

    /// <summary>Equilibrium stock = this many days of total throughput.</summary>
    public double EquilibriumDays { get; init; } = 10.0;

    public double MinEquilibrium { get; init; } = 40.0;
    public double MinStock { get; init; } = 5.0;
    public double NoiseSigma { get; init; } = 0.02;

    /// <summary>Buy/sell margin taken by the market, so you cannot round-trip in place.</summary>
    public double Spread { get; init; } = 0.06;

    public double MinPriceMult { get; init; } = 0.4;
    public double MaxPriceMult { get; init; } = 2.5;

    /// <summary>Roads are not straight lines; scales great-circle distance up.</summary>
    public double RoadDetourFactor { get; init; } = 1.25;

    /// <summary>
    /// Share of a city's intake that reaches its shelf each day. Lower means goods sold
    /// into a city take longer to come back onto the market.
    /// </summary>
    public double RestockRate { get; init; } = 0.35;
}

public sealed class GameConfig
{
    public long StartCash { get; init; } = 20000;
    public string StartCityId { get; init; } = "";
    public List<string> StartTruckIds { get; init; } = new();
    public EconomyConfig Economy { get; init; } = new();
    public WarehouseConfig Warehouse { get; init; } = new();
    public CrewBriefConfig CrewBrief { get; init; } = new();
}

/// <summary>
/// The crew's quick market brief when the convoy parks in a city: which goods in the
/// hold would clear a worthwhile margin if sold here, biggest margin first.
///
/// The toggle is content so it can later bind to a crew passive skill; the first cut
/// ships with it simply on.
/// </summary>
public sealed class CrewBriefConfig
{
    /// <summary>Master switch. True for the first cut; a crew passive skill will gate it later.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Floor on the margin over cost basis, as a fraction. 0.035 = 3.5%, meant to cover fuel.</summary>
    public double MinMargin { get; init; } = 0.035;
}
