namespace MechaTrader.Core.View;
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

