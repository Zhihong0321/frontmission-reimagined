using MechaTrader.Core.Sim;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Ai;

/// <summary>
/// One profitable loaded hop, priced against the depth the order would actually
/// consume rather than the marginal quote.
/// </summary>
public readonly record struct ScoutedRun(
    string GoodId,
    string DestinationId,
    int Units,
    double Net,
    int Days);

/// <summary>
/// One-hop planning used by the automated traders. Same approximation the road
/// scouting estimates use; settlement still walks unit by unit.
/// </summary>
public static class TradeScout
{
    /// <summary>Fractions of a full affordable hold to evaluate for each candidate run.</summary>
    internal static readonly double[] OrderSizes = { 1.0, 0.75, 0.5, 0.3, 0.15 };

    /// <summary>The most profitable loaded run leaving a given city, at current prices.</summary>
    public static ScoutedRun? BestRunFrom(Game game, string cityId)
    {
        var state = game.State;
        var world = game.World;
        var eco = world.Config.Economy;

        var origin = world.City(cityId);
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        var upkeep = CaravanMath.DailyUpkeep(state.Caravan, world);
        var regard = Standing.Of(state, cityId);

        ScoutedRun? best = null;

        foreach (var route in world.Routes.From(cityId))
        {
            var destinationId = route.Other(cityId);
            var destination = world.City(destinationId);

            var days = CaravanMath.TravelDays(state.Caravan, world, route);
            if (days <= 0 || days == int.MaxValue) continue;

            var fixedCost = CaravanMath.TravelFuel(state.Caravan, world, route) + upkeep * days;

            foreach (var good in world.Goods)
            {
                // The city will not sell a locked grade to us; do not plan around it.
                if (!Standing.TierOpen(world.TierOf(good), regard)) continue;

                var originProfile = origin.Market[good.Id];
                var originStock = state.StockOf(cityId, good.Id);
                var originMult = WorldEvents.PriceMultiplier(state, world, cityId, good.Id);

                var destinationProfile = destination.Market[good.Id];
                var destinationStock = state.StockOf(destinationId, good.Id);
                var destMult = WorldEvents.PriceMultiplier(state, world, destinationId, good.Id);
                var terms = CrewMath.Terms(state.Caravan, world, good.Category);

                if (Economy.SellUnitPrice(good, destinationProfile, destinationStock, eco, terms, destMult)
                    <= Economy.BuyUnitPrice(good, originProfile, originStock, eco, terms, originMult)) continue;

                var saleable = Economy.UnitsOnTheShelf(originStock, eco);
                var selection = CrewMath.SelectionFactor(state.Caravan.Crew, world.Crew, good.Category);
                var bestCrate = QualityMath.SellMultiplier(
                    QualityMath.SelectedQuality(originStock.OutQuality, saleable, 1, selection, world.Quality), world.Quality);
                var maxUnits = Economy.MaxAffordableUnits(
                    good, originProfile, originStock, state.Cash, free, eco, terms, originMult, bestCrate);
                if (maxUnits <= 0) continue;

                // Order size is itself a decision. Buying the maximum walks the purchase
                // price up and then craters the sale price on arrival, so the best run is
                // often well short of a full hold. Both sides are priced against the depth
                // the order actually consumes rather than the marginal price.
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

                    var net = revenue - cost - fixedCost;
                    if (best is null || net > best.Value.Net)
                        best = new ScoutedRun(good.Id, destinationId, units, net, days);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// One hop of lookahead: which neighbour is worth moving to empty, judged by the
    /// best run available once we get there, less the cost of getting there.
    /// </summary>
    public static string? BestRepositioning(Game game, string cityId)
    {
        var state = game.State;
        var world = game.World;

        var upkeep = CaravanMath.DailyUpkeep(state.Caravan, world);

        string? best = null;
        var bestValue = 0.0;

        foreach (var route in world.Routes.From(cityId))
        {
            var neighbourId = route.Other(cityId);

            var days = CaravanMath.TravelDays(state.Caravan, world, route);
            if (days <= 0 || days == int.MaxValue) continue;

            var moveCost = CaravanMath.TravelFuel(state.Caravan, world, route) + upkeep * days;

            var onward = BestRunFrom(game, neighbourId);
            if (onward is not { } run || run.Net <= 0) continue;

            var value = run.Net - moveCost;
            if (value > bestValue)
            {
                bestValue = value;
                best = neighbourId;
            }
        }

        return best;
    }

    /// <summary>
    /// True when the best loaded run is limited by hold space rather than cash or the
    /// shelf: the signal that another truck would actually move more goods.
    /// </summary>
    public static bool BestRunIsVolumeCapped(Game game, string cityId)
    {
        var run = BestRunFrom(game, cityId);
        if (run is not { } loaded || loaded.Net <= 0) return false;

        var state = game.State;
        var world = game.World;
        var good = world.Good(loaded.GoodId);
        var origin = world.City(cityId);
        var profile = origin.Market[good.Id];
        var stock = state.StockOf(cityId, good.Id);
        var terms = CrewMath.Terms(state.Caravan, world);
        var eco = world.Config.Economy;
        var mult = WorldEvents.PriceMultiplier(state, world, cityId, good.Id);
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        var gradeMult = QualityMath.SellMultiplier(stock.OutQuality, world.Quality);

        var withHold = Economy.MaxAffordableUnits(
            good, profile, stock, state.Cash, free, eco, terms, mult, gradeMult);
        var withRoom = Economy.MaxAffordableUnits(
            good, profile, stock, state.Cash, 1_000_000, eco, terms, mult, gradeMult);

        return withHold > 0 && withHold < withRoom;
    }
}
