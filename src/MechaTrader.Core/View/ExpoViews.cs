namespace MechaTrader.Core.View;
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
