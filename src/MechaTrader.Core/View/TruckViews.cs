namespace MechaTrader.Core.View;
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
