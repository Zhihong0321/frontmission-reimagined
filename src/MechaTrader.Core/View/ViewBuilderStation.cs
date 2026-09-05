using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

public static partial class ViewBuilder
{
    /// <summary>
    /// The station: offers (only while parked), and the fleet with what each vehicle
    /// could take and what the yard would pay. Effects are worded here so the shell
    /// never reads an upgrade's numbers.
    /// </summary>
    private static StationView BuildStation(GameState state, WorldData world, City? location)
    {
        var caravan = state.Caravan;
        var fleet = new List<FleetTruckView>(caravan.Trucks.Count);

        foreach (var truck in caravan.Trucks)
        {
            var type = world.Truck(truck.TypeId);
            var spec = CaravanMath.Spec(truck, world);
            var blocker = location is null ? "The station only trades while parked in a city." : CaravanMath.SellBlocker(caravan, world, truck);

            var fittings = world.TruckUpgrades.Select(u => new TruckFittingView(
                Id: u.Id,
                Name: u.Name,
                Blurb: u.Blurb,
                Price: u.Price,
                EffectText: UpgradeEffectText(u),
                Installed: truck.UpgradeIds.Contains(u.Id),
                Fits: u.Fits(type.EffectiveKind),
                Affordable: state.Cash >= u.Price)).ToList();

            fleet.Add(new FleetTruckView(
                Id: truck.Id,
                TypeId: truck.TypeId,
                Name: type.Name,
                Kind: type.EffectiveKind,
                Capacity: Math.Round(spec.Capacity, 1),
                SpeedKmPerDay: Math.Round(spec.SpeedKmPerDay, 1),
                UpkeepPerDay: Math.Round(spec.UpkeepPerDay, 1),
                FuelPerKm: Math.Round(spec.FuelPerKm, 3),
                MineYield: Math.Round(spec.MineYield, 1),
                Upgrades: truck.UpgradeIds
                    .Select(id => world.TruckUpgradesById.TryGetValue(id, out var u) ? u.Name : id)
                    .ToList(),
                ResaleValue: CaravanMath.ResaleValue(truck, world),
                CanSell: blocker is null,
                SellBlocker: blocker ?? "",
                Fittings: fittings));
        }

        return new StationView(
            Open: location is not null,
            Offers: location is null ? Array.Empty<TruckOfferView>() : BuildShipyard(world),
            Fleet: fleet,
            ResaleFraction: world.ResaleFraction);
    }

    private static string UpgradeEffectText(TruckUpgradeDef u)
    {
        var parts = new List<string>();
        if (Math.Abs(u.CapacityBonus) > 1e-9) parts.Add($"{(u.CapacityBonus > 0 ? "+" : "")}{u.CapacityBonus:0.#} hold");
        if (Math.Abs(u.SpeedMult - 1.0) > 1e-9) parts.Add($"{(u.SpeedMult - 1.0) * 100:+0;-0}% speed");
        if (Math.Abs(u.FuelMult - 1.0) > 1e-9) parts.Add($"{(u.FuelMult - 1.0) * 100:+0;-0}% fuel");
        if (Math.Abs(u.UpkeepDelta) > 1e-9) parts.Add($"{u.UpkeepDelta:+0.#;-0.#} cr/day upkeep");
        if (Math.Abs(u.MineYieldBonus) > 1e-9) parts.Add($"+{u.MineYieldBonus:0.#} u/day mining");
        return parts.Count == 0 ? u.Blurb : string.Join(" · ", parts);
    }

}
