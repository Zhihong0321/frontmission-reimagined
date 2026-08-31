using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

/// <summary>
/// Projects game state into a display snapshot. All presentation-oriented derivation
/// happens here so no front-end has to understand the price model to render a market.
/// </summary>
public static class ViewBuilder
{
    public static GameView Build(GameState state, WorldData world)
    {
        var caravan = state.Caravan;
        var eco = world.Config.Economy;

        var location = caravan.LocationId is { } id ? world.City(id) : null;

        return new GameView(
            Day: state.Day,
            Cash: state.Cash,
            NetWorth: NetWorth(state, world),
            Bankrupt: state.Bankrupt,
            Location: location is null
                ? null
                : new LocationView(location.Id, location.Name, location.Region, location.Industries),
            Travel: BuildTravel(state, world),
            Convoy: BuildConvoy(state, world),
            Market: location is null ? Array.Empty<MarketRowView>() : BuildMarket(state, world, location),
            Cargo: BuildCargo(state, world),
            Routes: location is null ? Array.Empty<RouteView>() : BuildRoutes(state, world, location),
            Shipyard: location is null ? Array.Empty<TruckOfferView>() : BuildShipyard(world));
    }

    /// <summary>
    /// Cash plus what the hold would fetch here and now. A truer score than cash alone,
    /// since a convoy mid-run is usually cash-poor and cargo-rich.
    /// </summary>
    public static long NetWorth(GameState state, WorldData world)
    {
        var total = (double)state.Cash;
        var eco = world.Config.Economy;
        var cityId = state.Caravan.LocationId;

        foreach (var (goodId, lot) in state.Caravan.Cargo)
        {
            if (lot.Units <= 0) continue;

            if (cityId is not null)
            {
                var good = world.Good(goodId);
                var profile = world.City(cityId).Market[goodId];
                var stock = state.StockOf(cityId, goodId);
                total += Economy.EstimateSellRevenue(good, profile, stock, lot.Units, eco);
            }
            else
            {
                // On the road there is no market to price against; fall back to cost basis.
                total += lot.TotalCost;
            }
        }

        return (long)Math.Round(total);
    }

    private static TravelView? BuildTravel(GameState state, WorldData world)
    {
        if (state.Caravan.Travel is not { } t) return null;

        return new TravelView(
            world.City(t.FromId).Name,
            world.City(t.ToId).Name,
            t.TotalDays,
            t.DaysRemaining,
            Math.Round(t.FuelPerDay, 1));
    }

    private static ConvoyView BuildConvoy(GameState state, WorldData world)
    {
        var caravan = state.Caravan;

        return new ConvoyView(
            Capacity: CaravanMath.Capacity(caravan, world),
            Used: Math.Round(CaravanMath.UsedVolume(caravan, world), 1),
            Free: Math.Round(CaravanMath.FreeVolume(caravan, world), 1),
            SpeedKmPerDay: CaravanMath.SpeedKmPerDay(caravan, world),
            DailyUpkeep: CaravanMath.DailyUpkeep(caravan, world),
            Trucks: caravan.TruckTypeIds.Select(t => world.Truck(t).Name).ToList());
    }

    private static List<MarketRowView> BuildMarket(GameState state, WorldData world, City city)
    {
        var eco = world.Config.Economy;
        var rows = new List<MarketRowView>(world.Goods.Count);

        foreach (var good in world.Goods)
        {
            var profile = city.Market[good.Id];
            var stock = state.StockOf(city.Id, good.Id);
            var net = profile.Production - profile.Consumption;

            var flow = net > 0.5 ? "surplus" : net < -0.5 ? "deficit" : "balanced";

            var lot = state.Caravan.Cargo.TryGetValue(good.Id, out var l) ? l : null;

            rows.Add(new MarketRowView(
                GoodId: good.Id,
                Name: good.Name,
                Tier: good.Tier,
                Buy: Math.Round(Economy.BuyUnitPrice(good, profile, stock, eco), 1),
                Sell: Math.Round(Economy.SellUnitPrice(good, profile, stock, eco), 1),
                BasePrice: good.BasePrice,
                Stock: Math.Round(stock),
                Held: lot?.Units ?? 0,
                AverageCost: Math.Round(lot?.AverageCost ?? 0, 1),
                UnitVolume: good.UnitVolume,
                Flow: flow));
        }

        return rows;
    }

    private static List<CargoRowView> BuildCargo(GameState state, WorldData world)
    {
        var rows = new List<CargoRowView>();

        foreach (var good in world.Goods)
        {
            if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot) || lot.Units <= 0) continue;

            rows.Add(new CargoRowView(
                good.Id,
                good.Name,
                lot.Units,
                Math.Round(lot.AverageCost, 1),
                Math.Round(lot.Units * good.UnitVolume, 1)));
        }

        return rows;
    }

    private static List<RouteView> BuildRoutes(GameState state, WorldData world, City city)
    {
        var rows = new List<RouteView>();

        foreach (var route in world.Routes.From(city.Id))
        {
            var otherId = route.Other(city.Id);
            var other = world.City(otherId);

            rows.Add(new RouteView(
                ToId: otherId,
                ToName: other.Name,
                ToRegion: other.Region,
                DistanceKm: route.DistanceKm,
                TerrainName: route.Terrain.Name,
                Days: CaravanMath.TravelDays(state.Caravan, world, route),
                EstimatedFuel: Math.Round(CaravanMath.TravelFuel(state.Caravan, world, route))));
        }

        return rows.OrderBy(r => r.Days).ThenBy(r => r.ToName, StringComparer.Ordinal).ToList();
    }

    private static List<TruckOfferView> BuildShipyard(WorldData world)
        => world.Trucks
            .Select(t => new TruckOfferView(
                t.Id, t.Name, t.Price, t.Capacity, t.SpeedKmPerDay, t.UpkeepPerDay, t.FuelPerKm))
            .ToList();
}
