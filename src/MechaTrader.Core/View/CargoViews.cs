namespace MechaTrader.Core.View;
public sealed record CargoRowView(
    string GoodId,
    string Name,
    string Category,
    int Tier,
    string TierName,
    string TierColor,
    int Units,
    double AverageCost,
    double Quality,
    bool STier,
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

