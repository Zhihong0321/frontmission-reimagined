namespace MechaTrader.Core.View;
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

