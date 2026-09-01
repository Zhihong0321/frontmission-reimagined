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
    IReadOnlyList<TruckOfferView> Shipyard,
    CrewView Crew);

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
    // Stock is everything the city owns; Shelf is the part actually for sale and the
    // only part a buy can draw on. Intake is what caravans have unloaded here and the
    // city has not shelved yet - visible so a glutted market is legible rather than
    // mysterious.
    double Stock,
    double Shelf,
    double Intake,
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

/// <summary>
/// The road network as drawable geometry. Static for a given world, so a front-end
/// fetches it once and only re-reads the per-turn view afterwards.
/// </summary>
public sealed record MapView(
    IReadOnlyList<MapCityView> Cities,
    IReadOnlyList<MapRoadView> Roads);

public sealed record MapCityView(string Id, string Name, string Region, double X, double Y);

public sealed record MapRoadView(string FromId, string ToId, string TerrainId, string TerrainName);

/// <summary>
/// The payroll, what it buys, and who is available locally.
///
/// Every number here is already resolved into what it does to the convoy - the
/// front-end renders "+21% convoy speed", it does not know that a level is divided by
/// maxSkill and multiplied by a lever's maxEffect.
/// </summary>
public sealed record CrewView(
    int Size,
    int Capacity,
    long DailyWages,
    IReadOnlyList<CrewMemberView> Roster,
    IReadOnlyList<CrewSkillView> Skills,
    RecruitmentView? Recruitment);

public sealed record SkillLevelView(string Id, string Name, int Level);

public sealed record CrewMemberView(
    string Id,
    string Name,
    string RoleName,
    long DailyWage,
    long Severance,
    int HiredDay,
    string HiredAt,
    IReadOnlyList<SkillLevelView> Skills);

/// <summary>One ability, at the level the best hand aboard has, and what that is worth.</summary>
public sealed record CrewSkillView(
    string Id,
    string Name,
    string Lever,
    string Blurb,
    int Level,
    int MaxLevel,
    string? LeaderName,
    string EffectText);

/// <summary>The local recruitment centre. Null while the convoy is on the road.</summary>
public sealed record RecruitmentView(
    string CityName,
    int RefreshInDays,
    IReadOnlyList<CandidateView> Candidates);

public sealed record CandidateView(
    string Id,
    string Name,
    string RoleName,
    long DailyWage,
    long SigningFee,
    bool Affordable,
    bool RoomAboard,
    IReadOnlyList<SkillLevelView> Skills);
