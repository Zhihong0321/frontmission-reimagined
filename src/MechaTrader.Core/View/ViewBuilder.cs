using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

/// <summary>
/// Projects game state into a display snapshot. All presentation-oriented derivation
/// happens here so no front-end has to understand the price model to render a market.
/// </summary>
public static partial class ViewBuilder
{
    public static GameView Build(GameState state, WorldData world)
    {
        var caravan = state.Caravan;

        var location = caravan.LocationId is { } id ? world.City(id) : null;
        var site = caravan.SiteId is { } siteId ? state.Site(siteId) : null;
        var parked = caravan.Travel is null;

        return new GameView(
            Day: state.Day,
            Cash: state.Cash,
            NetWorth: NetWorth(state, world),
            Bankrupt: state.Bankrupt,
            Location: location is null ? null : BuildLocation(state, world, location),
            Site: site is null || !parked ? null : BuildSite(state, world, site),
            Field: location is null && site is null && parked ? BuildField(state, world) : null,
            Travel: BuildTravel(state, world),
            Convoy: BuildConvoy(state, world),
            Market: location is null ? Array.Empty<MarketRowView>() : BuildMarket(state, world, location),
            Cargo: BuildCargo(state, world),
            Routes: parked ? BuildDestinations(state, world, location) : Array.Empty<RouteView>(),
            Shipyard: location is null ? Array.Empty<TruckOfferView>() : BuildShipyard(world),
            Outfitters: location is null ? Array.Empty<GearOfferView>() : BuildOutfitters(state, world),
            Station: BuildStation(state, world, location),
            Crew: BuildCrew(state, world, location),
            Warehouse: BuildWarehouse(state, world, location),
            Contracts: BuildContracts(state, world, location),
            Expo: location is null ? null : BuildExpo(state, world, location),
            Tiers: world.Tiers.Select(t => new TierView(t.Tier, t.Name, t.Color, t.MinStanding)).ToList(),
            EventCityIds: WorldEvents.EventCityIds(state),
            MiningSites: BuildMiningSites(state, world),
            CrewBrief: BuildCrewBrief(state, world),
            SellOutlook: BuildSellOutlook(state, world));
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
                var eventMult = WorldEvents.PriceMultiplier(state, world, cityId, goodId);
                var terms = CrewMath.Terms(state.Caravan, world, good.Category);
                var revenue = Economy.EstimateSellRevenue(
                    good, profile, stock, lot.Units, eco, terms, eventMult);
                total += revenue * QualityMath.SellMultiplier(lot.Quality, world.Quality);
            }
            else
            {
                // On the road there is no market to price against; fall back to cost basis.
                total += lot.TotalCost;
            }
        }

        foreach (var (whCityId, warehouse) in state.Warehouses)
        {
            if (!world.CitiesById.TryGetValue(whCityId, out var whCity)) continue;
            foreach (var (goodId, lot) in warehouse.Stock)
            {
                if (lot.Units <= 0) continue;
                var good = world.Good(goodId);
                var profile = whCity.Market[goodId];
                var stock = state.StockOf(whCityId, goodId);
                var eventMult = WorldEvents.PriceMultiplier(state, world, whCityId, goodId);
                var revenue = Economy.EstimateSellRevenue(
                    good, profile, stock, lot.Units, eco, TradeTerms.Market, eventMult);
                total += revenue * QualityMath.SellMultiplier(lot.Quality, world.Quality);
            }
        }

        return (long)Math.Round(total);
    }

    private static TravelView? BuildTravel(GameState state, WorldData world)
    {
        if (state.Caravan.Travel is not { } t) return null;

        var path = new List<MapPointView>();
        foreach (var w in t.Waypoints)
        {
            path.Add(new MapPointView(Math.Round(w.X, 1), Math.Round(w.Y, 1)));
        }

        var (cx, cy) = MapMath.TravelCoords(t);

        return new TravelView(
            t.FromName,
            t.ToName,
            t.TotalDays,
            t.DaysRemaining,
            Math.Round(t.FuelPerDay, 1),
            path,
            Math.Round(cx, 1),
            Math.Round(cy, 1));
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
            Trucks: caravan.Trucks.Select(t => world.Truck(t.TypeId).Name).ToList(),
            Gear: caravan.GearIds
                .Select(id => world.GearById.TryGetValue(id, out var g) ? g.Name : id)
                .ToList(),
            CanMine: CaravanMath.CanMine(caravan, world),
            MineYield: CaravanMath.MineYield(caravan, world));
    }

}
