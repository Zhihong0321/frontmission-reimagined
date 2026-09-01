using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>
/// Derived properties of the convoy. Kept as pure functions over state plus content
/// rather than cached on the state, so there is no possibility of them going stale.
/// </summary>
public static class CaravanMath
{
    public static double Capacity(CaravanState caravan, WorldData world)
    {
        double total = 0;
        foreach (var id in caravan.TruckTypeIds) total += world.Truck(id).Capacity;
        return total;
    }

    public static double UsedVolume(CaravanState caravan, WorldData world)
    {
        double total = 0;
        foreach (var (goodId, lot) in caravan.Cargo)
        {
            if (lot.Units <= 0) continue;
            total += lot.Units * world.Good(goodId).UnitVolume;
        }
        return total;
    }

    public static double FreeVolume(CaravanState caravan, WorldData world)
        => Math.Max(0, Capacity(caravan, world) - UsedVolume(caravan, world));

    /// <summary>
    /// The convoy moves at the pace of its slowest truck, then at whatever pace the
    /// navigator can talk it into.
    /// </summary>
    public static double SpeedKmPerDay(CaravanState caravan, WorldData world)
        => TruckSpeedKmPerDay(caravan, world) * CrewMath.SpeedMultiplier(caravan, world);

    public static double TruckSpeedKmPerDay(CaravanState caravan, WorldData world)
    {
        double slowest = double.MaxValue;
        foreach (var id in caravan.TruckTypeIds) slowest = Math.Min(slowest, world.Truck(id).SpeedKmPerDay);
        return slowest == double.MaxValue ? 0 : slowest;
    }

    /// <summary>
    /// Everything the convoy costs to keep for a day: truck upkeep, trimmed by whoever
    /// keeps the books, plus the payroll. Wages are never discounted by accounting - the
    /// crew do not take a cut of their own wage bill.
    /// </summary>
    public static double DailyUpkeep(CaravanState caravan, WorldData world)
        => TruckUpkeep(caravan, world) * CrewMath.RunningCostMultiplier(caravan, world)
           + CrewMath.DailyWages(caravan.Crew);

    public static double TruckUpkeep(CaravanState caravan, WorldData world)
    {
        double total = 0;
        foreach (var id in caravan.TruckTypeIds) total += world.Truck(id).UpkeepPerDay;
        return total;
    }

    public static double FuelPerKm(CaravanState caravan, WorldData world)
    {
        double total = 0;
        foreach (var id in caravan.TruckTypeIds) total += world.Truck(id).FuelPerKm;
        return total;
    }

    /// <summary>Whole days to cover a route, never less than one.</summary>
    public static int TravelDays(CaravanState caravan, WorldData world, Route route)
    {
        var speed = SpeedKmPerDay(caravan, world) * route.Terrain.SpeedMultiplier;
        if (speed <= 0) return int.MaxValue;
        return Math.Max(1, (int)Math.Ceiling(route.DistanceKm / speed));
    }

    /// <summary>Total fuel cost of a route, before upkeep. Bought on the crew's terms.</summary>
    public static double TravelFuel(CaravanState caravan, WorldData world, Route route)
        => route.DistanceKm * FuelPerKm(caravan, world) * route.Terrain.CostMultiplier
           * CrewMath.RunningCostMultiplier(caravan, world);
}
