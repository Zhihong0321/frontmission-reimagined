namespace MechaTrader.Core.View;
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

