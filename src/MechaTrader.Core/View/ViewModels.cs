namespace MechaTrader.Core.View;

/// <summary>
/// Front-end facing snapshots. Deliberately plain data with no behaviour: the browser
/// serialises them to JSON today and a Godot scene will bind to the same shapes later,
/// which is what keeps the presentation layer swappable.
/// </summary>
public sealed record GameView(
    int Day,
    long Cash,
    long NetWorth,
    bool Bankrupt,
    LocationView? Location,
    TravelView? Travel,
    ConvoyView Convoy,
    IReadOnlyList<MarketRowView> Market,
    IReadOnlyList<CargoRowView> Cargo,
    IReadOnlyList<RouteView> Routes,
    IReadOnlyList<TruckOfferView> Shipyard);

public sealed record LocationView(
    string Id,
    string Name,
    string Region,
    IReadOnlyList<string> Industries);

public sealed record TravelView(
    string FromName,
    string ToName,
    int TotalDays,
    int DaysRemaining,
    double FuelPerDay);

public sealed record ConvoyView(
    double Capacity,
    double Used,
    double Free,
    double SpeedKmPerDay,
    double DailyUpkeep,
    IReadOnlyList<string> Trucks);

/// <summary>One row of the local market board.</summary>
public sealed record MarketRowView(
    string GoodId,
    string Name,
    string Tier,
    double Buy,
    double Sell,
    double BasePrice,
    double Stock,
    int Held,
    double AverageCost,
    double UnitVolume,
    // "surplus", "deficit" or "balanced" - whether this city makes or eats the good.
    string Flow);

public sealed record CargoRowView(
    string GoodId,
    string Name,
    int Units,
    double AverageCost,
    double Volume);

public sealed record RouteView(
    string ToId,
    string ToName,
    string ToRegion,
    double DistanceKm,
    string TerrainName,
    int Days,
    double EstimatedFuel,
    // Scouting report: the best cargo to run down this road right now, what it would
    // clear after fuel and upkeep, and how many units the convoy could actually afford.
    // Without this the player is reading one city's prices with no way to judge a road.
    string? BestGoodId,
    string? BestGoodName,
    int BestUnits,
    long BestProfit);

public sealed record TruckOfferView(
    string Id,
    string Name,
    long Price,
    double Capacity,
    double SpeedKmPerDay,
    double UpkeepPerDay,
    double FuelPerKm);
