using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

public static partial class ViewBuilder
{
    private static List<RouteView> BuildDestinations(GameState state, WorldData world, City? city)
    {
        var rows = new List<RouteView>();
        var here = MapMath.Position(state, world);

        if (city is not null)
        {
            foreach (var route in world.Routes.From(city.Id))
            {
                var otherId = route.Other(city.Id);
                var other = world.City(otherId);
                var destCell = world.Map.CellOfCity(otherId);
                var plan = MapMath.Pathfind(state.Caravan, world, here, destCell);
                var days = plan?.Days ?? CaravanMath.TravelDays(state.Caravan, world, route);
                var fuel = plan?.Fuel ?? CaravanMath.TravelFuel(state.Caravan, world, route);
                var best = BestCargoFor(state, world, city, other, days, fuel);

                rows.Add(new RouteView(
                    ToId: otherId,
                    ToName: other.Name,
                    ToRegion: other.Region,
                    DistanceKm: Math.Round(plan?.DistanceKm ?? route.DistanceKm),
                    TerrainName: route.Terrain.Name,
                    Days: days,
                    EstimatedFuel: Math.Round(fuel),
                    BestGoodId: best.GoodId,
                    BestGoodName: best.GoodName,
                    BestUnits: best.Units,
                    BestProfit: best.Profit));
            }
        }
        else
        {
            foreach (var other in world.Cities)
            {
                var destCell = world.Map.CellOfCity(other.Id);
                if (destCell.Col == here.Col && destCell.Row == here.Row) continue;
                var plan = MapMath.Pathfind(state.Caravan, world, here, destCell);
                if (plan is null) continue;

                rows.Add(new RouteView(
                    ToId: other.Id,
                    ToName: other.Name,
                    ToRegion: other.Region,
                    DistanceKm: Math.Round(plan.DistanceKm),
                    TerrainName: plan.Layer,
                    Days: plan.Days,
                    EstimatedFuel: Math.Round(plan.Fuel),
                    BestGoodId: null,
                    BestGoodName: null,
                    BestUnits: 0,
                    BestProfit: 0));
            }
        }

        foreach (var site in state.MiningSites)
        {
            if (state.Caravan.SiteId == site.Id) continue;
            var cell = world.Map[site.Col, site.Row];
            var plan = MapMath.Pathfind(state.Caravan, world, here, cell);
            if (plan is null) continue;
            var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g.Name : site.GoodId;
            var status = site.Remaining <= 0 ? "played out" : $"{site.Remaining:0} left";

            rows.Add(new RouteView(
                ToId: site.Id,
                ToName: $"{good} deposit",
                ToRegion: status,
                DistanceKm: Math.Round(plan.DistanceKm),
                TerrainName: "claim",
                Days: plan.Days,
                EstimatedFuel: Math.Round(plan.Fuel),
                BestGoodId: null,
                BestGoodName: null,
                BestUnits: 0,
                BestProfit: 0));
        }

        return rows.OrderByDescending(r => r.BestProfit).ThenBy(r => r.Days).ToList();
    }

    private readonly record struct CargoAdvice(string? GoodId, string? GoodName, int Units, long Profit);

    /// <summary>
    /// What is worth hauling down one road, sized to what the convoy can actually pay
    /// for and carry.
    ///
    /// The player can only see the market they are standing in, so without this they
    /// would be choosing roads blind. Both legs are priced against the depth the order
    /// consumes, and fuel and upkeep are deducted, so the number shown is what the run
    /// would really clear rather than a headline margin.
    /// </summary>
    private static CargoAdvice BestCargoFor(
        GameState state, WorldData world, City origin, City destination, int days, double fuel)
    {
        if (days <= 0 || days == int.MaxValue) return new CargoAdvice(null, null, 0, 0);

        var eco = world.Config.Economy;
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        var fixedCost = fuel + CaravanMath.DailyUpkeep(state.Caravan, world) * days;
        var regard = Standing.Of(state, origin.Id);

        var best = new CargoAdvice(null, null, 0, 0);

        foreach (var good in world.Goods)
        {
            // Never recommend a grade the city will not sell us.
            if (!Standing.TierOpen(world.TierOf(good), regard)) continue;

            var originProfile = origin.Market[good.Id];
            var originStock = state.StockOf(origin.Id, good.Id);
            var originMult = WorldEvents.PriceMultiplier(state, world, origin.Id, good.Id);

            var destinationProfile = destination.Market[good.Id];
            var destinationStock = state.StockOf(destination.Id, good.Id);
            var destMult = WorldEvents.PriceMultiplier(state, world, destination.Id, good.Id);
            var terms = CrewMath.Terms(state.Caravan, world, good.Category);

            var saleable = Economy.UnitsOnTheShelf(originStock, eco);
            var selection = CrewMath.SelectionFactor(state.Caravan.Crew, world.Crew, good.Category);
            var bestCrate = QualityMath.SellMultiplier(
                QualityMath.SelectedQuality(originStock.OutQuality, saleable, 1, selection, world.Quality), world.Quality);
            var maxUnits = Economy.MaxAffordableUnits(
                good, originProfile, originStock, state.Cash, free, eco, terms, originMult, bestCrate);
            if (maxUnits <= 0) continue;

            foreach (var fraction in OrderSizes)
            {
                var units = (int)(maxUnits * fraction);
                if (units <= 0) continue;

                var cost = Economy.ApproximateBuyCost(
                    good, originProfile, originStock, units, eco, terms, originMult);
                var revenue = Economy.ApproximateSellRevenue(
                    good, destinationProfile, destinationStock, units, eco, terms, destMult);

                var pickQ = QualityMath.SelectedQuality(
                    originStock.OutQuality, saleable, units, selection, world.Quality);
                var gradeMult = QualityMath.SellMultiplier(pickQ, world.Quality);
                cost *= gradeMult;
                revenue *= gradeMult;

                var profit = (long)Math.Round(revenue - cost - fixedCost);
                if (profit > best.Profit)
                    best = new CargoAdvice(good.Id, good.Name, units, profit);
            }
        }

        return best;
    }

    private static readonly double[] OrderSizes = { 1.0, 0.75, 0.5, 0.3, 0.15 };

}
