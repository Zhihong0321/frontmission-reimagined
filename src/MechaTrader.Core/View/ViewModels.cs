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
    SiteView? Site,
    FieldView? Field,
    TravelView? Travel,
    ConvoyView Convoy,
    IReadOnlyList<MarketRowView> Market,
    IReadOnlyList<CargoRowView> Cargo,
    IReadOnlyList<RouteView> Routes,
    IReadOnlyList<TruckOfferView> Shipyard,
    IReadOnlyList<GearOfferView> Outfitters,
    StationView Station,
    CrewView Crew,
    WarehouseView Warehouse,
    ContractsView Contracts,
    ExpoView? Expo,
    IReadOnlyList<TierView> Tiers,
    IReadOnlyList<string> EventCityIds,
    IReadOnlyList<MiningSiteView> MiningSites,
    CrewBriefView CrewBrief,
    IReadOnlyList<SellOutlookView> SellOutlook);

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

/// <summary>One product grade, for legends and colouring. Content passed through.</summary>
public sealed record TierView(int Tier, string Name, string Color, double MinStanding);

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

public sealed record TravelView(
    string FromName,
    string ToName,
    int TotalDays,
    int DaysRemaining,
    double FuelPerDay,
    IReadOnlyList<MapPointView> Path,
    double ConvoyX,
    double ConvoyY);

public sealed record MapPointView(double X, double Y);

public sealed record ConvoyView(
    double Capacity,
    double Used,
    double Free,
    double SpeedKmPerDay,
    double DailyUpkeep,
    IReadOnlyList<string> Trucks,
    IReadOnlyList<string> Gear,
    bool CanMine,
    double MineYield);

/// <summary>One row of the local market board.</summary>
public sealed record MarketRowView(
    string GoodId,
    string Name,
    string CategoryId,
    string Category,
    int Tier,
    string TierName,
    string TierColor,
    // True when this city will not sell the grade to somebody of the player's standing.
    bool Locked,
    double UnlockStanding,
    double Buy,
    double Sell,
    double BasePrice,
    // What the quote is made of (see Economy.UnitPrice / BuyUnitPrice / SellUnitPrice):
    // base -> city market (stock, events) -> the market spread -> the crew's haggling ->
    // the crate grade the pick adds on the buy side. Market* carries no spread or grade,
    // NoCrew* is the full-spread counterfactual, so the crew's share is visible.
    double MarketBuy,
    double MarketSell,
    double NoCrewBuy,
    double NoCrewSell,
    double PickMult,
    // Stock is everything the city owns; Shelf is the part actually for sale and the
    // only part a buy can draw on. Reserved is the share of that shelf held for the
    // player (other caravans cannot take it first). Intake is what caravans have
    // unloaded here and the city has not shelved yet.
    double Stock,
    double Shelf,
    double Reserved,
    double Intake,
    int Held,
    double AverageCost,
    double HeldQuality,
    bool HeldSTier,
    double AverageQuality,
    double PickQuality,
    double Knowledge,
    bool STierPossible,
    double UnitVolume,
    // Largest order buyable right now, and what each limit alone allows (see
    // Economy.MaxAffordableUnits): hold space, cash at the quoted unit price, and the
    // city's shelf. MaxBuy is their minimum, so the UI can show which one binds.
    int MaxBuy,
    int MaxByHold,
    int MaxByCash,
    int MaxByShelf,
    // "surplus", "deficit" or "balanced" - whether this city makes or eats the good.
    string Flow,
    // Why this quote is not the city's resting price, already formatted. Empty if none.
    string EventHint,
    // Citizen standing per unit sold here right now (a running shortage). Zero if none.
    double ReliefPerUnit,
    string ReliefHint,
    // What the information post reports this good fetches in the nearest cities, closest
    // first. Empty when nobody is on the post. Figures carry the informant's error.
    IReadOnlyList<PriceReportView> Elsewhere);

/// <summary>
/// One line of the informant's report: a city, how far it is, and what they say it
/// pays. ErrorPct is the worst-case miss either way; the true price is not on here.
/// </summary>
public sealed record PriceReportView(
    string CityId,
    string CityName,
    string Region,
    double DistanceKm,
    int Days,
    double Buy,
    double Sell,
    string Flow,
    double ErrorPct);

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

public sealed record TruckOfferView(
    string Id,
    string Name,
    string Kind,
    long Price,
    double Capacity,
    double SpeedKmPerDay,
    double UpkeepPerDay,
    double FuelPerKm,
    double MineYield);

/// <summary>
/// The truck station: what it sells, and the convoy's own fleet with what each vehicle
/// could take and what the station would pay for it. Fleet rows are filled on the road
/// too, so the caravan page can list them; the station only trades while parked.
/// </summary>
public sealed record StationView(
    bool Open,
    IReadOnlyList<TruckOfferView> Offers,
    IReadOnlyList<FleetTruckView> Fleet,
    double ResaleFraction);

public sealed record FleetTruckView(
    string Id,
    string TypeId,
    string Name,
    string Kind,
    double Capacity,
    double SpeedKmPerDay,
    double UpkeepPerDay,
    double FuelPerKm,
    double MineYield,
    IReadOnlyList<string> Upgrades,
    long ResaleValue,
    bool CanSell,
    // Why the station will not take it, already worded. Empty when it can.
    string SellBlocker,
    IReadOnlyList<TruckFittingView> Fittings);

public sealed record TruckFittingView(
    string Id,
    string Name,
    string Blurb,
    long Price,
    string EffectText,
    bool Installed,
    bool Fits,
    bool Affordable);

public sealed record GearOfferView(
    string Id,
    string Name,
    long Price,
    double Volume,
    double MineYield,
    bool Affordable,
    bool Fits);

public sealed record SiteView(
    string Id,
    string Name,
    string GoodId,
    string GoodName,
    double Remaining,
    double ExpectedYield,
    bool CanMine,
    string Hint);

public sealed record FieldView(
    string CellId,
    string Biome,
    double X,
    double Y);

public sealed record MiningSiteView(
    string Id,
    string Name,
    double X,
    double Y,
    double Remaining,
    bool Depleted);

/// <summary>
/// The road network as drawable geometry. Static for a given world, so a front-end
/// fetches it once and only re-reads the per-turn view afterwards.
/// </summary>
public sealed record MapView(
    IReadOnlyList<MapCityView> Cities,
    IReadOnlyList<MapRoadView> Roads,
    int Width,
    int Height,
    double CellKm,
    double OriginX,
    double OriginY,
    string Biomes,
    string RoadsMask);

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
    IReadOnlyList<CrewPostView> Posts,
    IntelView Intel,
    RecruitmentView? Recruitment);

/// <summary>A job aboard, who is on it, and who leads it. The shell offers these as choices.</summary>
public sealed record CrewPostView(
    string Id,
    string Name,
    string Blurb,
    // The skills this post gates, already named for print.
    string SkillNames,
    int Hands,
    string? LeaderName);

/// <summary>What the information post is delivering right now.</summary>
public sealed record IntelView(
    bool Active,
    string? InformantName,
    int Level,
    int MaxLevel,
    int Reach,
    int MaxReach,
    double ErrorPct,
    // Already formatted, e.g. "reads 5 markets within ±18%".
    string Summary);

public sealed record SkillLevelView(string Id, string Name, int Level);

public sealed record CrewMemberView(
    string Id,
    string Name,
    string RoleName,
    string PostId,
    string PostName,
    long DailyWage,
    long Severance,
    int HiredDay,
    string HiredAt,
    IReadOnlyList<SkillLevelView> Skills,
    IReadOnlyList<KnowledgeView> Knowledge,
    IReadOnlyList<TraitView> Traits);

public sealed record KnowledgeView(string Id, string Name, int Level, int MaxLevel);

public sealed record TraitView(string Id, string Name, string Kind, string Blurb);

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
    // The post they would take on signing; empty means none.
    string PostName,
    long DailyWage,
    long SigningFee,
    bool Affordable,
    bool RoomAboard,
    IReadOnlyList<SkillLevelView> Skills,
    IReadOnlyList<KnowledgeView> Knowledge,
    IReadOnlyList<TraitView> Traits);

/// <summary>
/// The local storeroom, if the house rents one here. <see cref="Rented"/> is false when
/// the convoy is in a city with no room; the rent figures are still filled so the page
/// can offer the lease.
/// </summary>
public sealed record WarehouseView(
    bool Rented,
    long RentCost,
    long DailyRent,
    double Capacity,
    double Used,
    IReadOnlyList<WarehouseLotView> Lots);

public sealed record WarehouseLotView(
    string GoodId,
    string Name,
    int Units,
    double Quality,
    bool STier,
    long AutoSell,
    long AutoProcure);

/// <summary>
/// The contract board here (empty on the road) and every contract the house holds,
/// wherever it was signed. An offer arrives with the hold already checked against it.
/// </summary>
public sealed record ContractsView(
    string BoardCity,
    int RefreshInDays,
    IReadOnlyList<ContractOfferView> Board,
    IReadOnlyList<HeldContractView> Held);

public sealed record ContractLineView(
    string GoodId,
    string Name,
    string TierColor,
    int Units,
    int Held,
    double HeldQuality,
    bool Satisfied);

public sealed record ContractOfferView(
    string Id,
    string CityId,
    string CityName,
    string KindId,
    string KindName,
    string Blurb,
    IReadOnlyList<ContractLineView> Lines,
    double MinGrade,
    long Reward,
    double Standing,
    int DeadlineDays,
    bool Held,
    bool Closed);

public sealed record HeldContractView(
    string Id,
    string CityId,
    string CityName,
    string KindName,
    string Blurb,
    IReadOnlyList<ContractLineView> Lines,
    double MinGrade,
    long Reward,
    double Standing,
    int Deadline,
    int DaysLeft,
    bool Here,
    bool Deliverable,
    // Why it cannot be settled right now, already worded. Empty when it can.
    string Blocker);

/// <summary>
/// The expo in this city: what is on, or when the next one opens, the stall, and a
/// replayable record of the last day's buyers. Null on the road.
/// </summary>
public sealed record ExpoView(
    string CityName,
    bool Running,
    string ThemeId,
    string Title,
    IReadOnlyList<string> Categories,
    int StartsIn,
    int DaysLeft,
    int DurationDays,
    long Fee,
    bool PassHeld,
    double Buff,
    int BuyersPerDay,
    IReadOnlyList<ExpoListingView> Listings,
    ExpoReportView? Report);

/// <summary>One good in the hold, and whether and how it could sit on the stall.</summary>
public sealed record ExpoListingView(
    string GoodId,
    string Name,
    string Category,
    string TierColor,
    int Held,
    double Quality,
    long Ask,
    // The ask a typical buyer would just accept, before their mood.
    long Suggested,
    double LocalSell,
    bool CityMakes,
    bool Covered,
    bool Eligible,
    string Reason);

public sealed record ExpoReportView(
    int Day,
    long Revenue,
    int UnitsSold,
    int Buyers,
    IReadOnlyList<ExpoVisitView> Visits);

public sealed record ExpoVisitView(
    int Sequence,
    string Buyer,
    string GoodId,
    string GoodName,
    string Outcome,
    int Units,
    long Price,
    string Remark);
