using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>What one vehicle is worth to the convoy once its fittings are counted.</summary>
public readonly record struct TruckSpec(
    double Capacity,
    double SpeedKmPerDay,
    double UpkeepPerDay,
    double FuelPerKm,
    double MineYield);

/// <summary>
/// Derived properties of the convoy. Kept as pure functions over state plus content
/// rather than cached on the state, so there is no possibility of them going stale.
///
/// A truck is an instance with fittings; <see cref="Spec"/> resolves the type plus every
/// upgrade on it, and everything else reads through that, so a new fitting effect is a
/// content field and one line here.
/// </summary>
public static class CaravanMath
{
    /// <summary>The vehicle's numbers with its fittings applied.</summary>
    public static TruckSpec Spec(TruckState truck, WorldData world)
    {
        var type = world.Truck(truck.TypeId);
        var capacity = type.Capacity;
        var speed = type.SpeedKmPerDay;
        var upkeep = type.UpkeepPerDay;
        var fuel = type.FuelPerKm;
        var mine = type.MineYield;

        foreach (var id in truck.UpgradeIds)
        {
            if (!world.TruckUpgradesById.TryGetValue(id, out var upgrade)) continue;
            capacity += upgrade.CapacityBonus;
            speed *= upgrade.SpeedMult;
            fuel *= upgrade.FuelMult;
            upkeep += upgrade.UpkeepDelta;
            mine += upgrade.MineYieldBonus;
        }

        return new TruckSpec(
            Math.Max(0.0, capacity),
            Math.Max(0.0, speed),
            Math.Max(0.0, upkeep),
            Math.Max(0.0, fuel),
            Math.Max(0.0, mine));
    }

    /// <summary>What the station pays for a vehicle and everything bolted to it.</summary>
    public static long ResaleValue(TruckState truck, WorldData world)
    {
        double value = world.Truck(truck.TypeId).Price;
        foreach (var id in truck.UpgradeIds)
        {
            if (world.TruckUpgradesById.TryGetValue(id, out var upgrade)) value += upgrade.Price;
        }
        return (long)Math.Round(value * Math.Clamp(world.ResaleFraction, 0.0, 1.0));
    }

    public static double Capacity(CaravanState caravan, WorldData world)
    {
        double total = 0;
        foreach (var truck in caravan.Trucks) total += Spec(truck, world).Capacity;
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

        foreach (var gearId in caravan.GearIds)
        {
            if (world.GearById.TryGetValue(gearId, out var gear))
                total += gear.Volume;
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
        foreach (var truck in caravan.Trucks) slowest = Math.Min(slowest, Spec(truck, world).SpeedKmPerDay);
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
        foreach (var truck in caravan.Trucks) total += Spec(truck, world).UpkeepPerDay;
        return total;
    }

    public static double FuelPerKm(CaravanState caravan, WorldData world)
    {
        double total = 0;
        foreach (var truck in caravan.Trucks) total += Spec(truck, world).FuelPerKm;
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

    /// <summary>
    /// True when every vehicle in the convoy can travel the named layer. An empty
    /// capability list on a truck means land.
    /// </summary>
    public static bool CanTravel(CaravanState caravan, WorldData world, string layer)
    {
        if (caravan.Trucks.Count == 0) return false;
        foreach (var truck in caravan.Trucks)
        {
            if (!world.Truck(truck.TypeId).HasCapability(layer)) return false;
        }
        return true;
    }

    /// <summary>True when any gear or machine on the convoy can work a deposit.</summary>
    public static bool CanMine(CaravanState caravan, WorldData world)
        => MineYield(caravan, world) > 0;

    public static double MineYield(CaravanState caravan, WorldData world)
    {
        double total = 0;
        foreach (var truck in caravan.Trucks)
            total += Spec(truck, world).MineYield;
        foreach (var id in caravan.GearIds)
        {
            if (world.GearById.TryGetValue(id, out var gear))
                total += gear.MineYield;
        }
        return total;
    }

    /// <summary>
    /// Whether the convoy could part with this vehicle: something else must still be
    /// able to carry the hold, and the convoy must still be able to move at all.
    /// Returns null when it can, otherwise the reason it cannot.
    /// </summary>
    public static string? SellBlocker(CaravanState caravan, WorldData world, TruckState truck)
    {
        var remaining = new CaravanState
        {
            Trucks = caravan.Trucks.Where(t => t.Id != truck.Id).ToList(),
            GearIds = caravan.GearIds,
            Cargo = caravan.Cargo,
            Crew = caravan.Crew
        };

        if (remaining.Trucks.Count == 0)
            return "The convoy cannot sell its last vehicle.";

        if (!CanTravel(remaining, world, VehicleCapability.Land))
            return "Without it nothing left in the convoy could take the road.";

        var used = UsedVolume(remaining, world);
        var capacity = Capacity(remaining, world);
        if (used > capacity + 1e-9)
            return $"The rest of the convoy holds {capacity:0.#}; the hold carries {used:0.#}. Sell cargo first.";

        return null;
    }

    /// <summary>Mint the next vehicle instance for a type. Advances the convoy's serial.</summary>
    public static TruckState NewTruck(CaravanState caravan, string typeId)
    {
        caravan.TruckSerial++;
        return new TruckState { Id = $"{typeId}-{caravan.TruckSerial}", TypeId = typeId };
    }
}
