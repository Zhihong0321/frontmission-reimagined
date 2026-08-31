using MechaTrader.Core.Commands;
using MechaTrader.Core.Sim;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Ai;

/// <summary>An automated trader. Returns the next command it wants to issue.</summary>
public interface ITraderPolicy
{
    string Name { get; }
    Command? Decide(Game game, Rng rng);
}

/// <summary>
/// Buys the best margin it can reach in one hop, hauls it, sells it, repeats. When
/// nothing local pays, it repositions empty toward wherever the next run is.
///
/// This exists to answer the only question that matters before any art gets made:
/// does playing well beat playing badly? If this policy cannot out-earn
/// <see cref="RandomTrader"/>, the economy has no skill expression and no amount of
/// visual polish will produce a game. It is also the seed of the rival trading houses
/// planned for the next milestone.
/// </summary>
public sealed class GreedyTrader : ITraderPolicy
{
    public string Name => "greedy";

    /// <summary>Fractions of a full affordable hold to evaluate for each candidate run.</summary>
    private static readonly double[] OrderSizes = { 1.0, 0.75, 0.5, 0.3, 0.15 };

    private string? _pendingDestination;

    public Command? Decide(Game game, Rng rng)
    {
        var state = game.State;

        if (state.Caravan.Travel is { } travel)
            return new WaitCommand(Math.Max(1, travel.DaysRemaining));

        if (_pendingDestination is { } destination)
        {
            _pendingDestination = null;
            return new DepartCommand(destination);
        }

        // Clear the hold before evaluating anything new: this is a pure haulage loop.
        foreach (var (goodId, lot) in state.Caravan.Cargo)
        {
            if (lot.Units > 0) return new SellCommand(goodId, lot.Units);
        }

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return new WaitCommand(1);

        var loaded = BestRunFrom(game, cityId);
        if (loaded is { } run && run.Net > 0)
        {
            _pendingDestination = run.DestinationId;
            return new BuyCommand(run.GoodId, run.Units);
        }

        // Nothing here pays. Sitting still still costs upkeep, so go find the trade
        // rather than waiting for one to arrive.
        var reposition = BestRepositioning(game, cityId);
        if (reposition is not null) return new DepartCommand(reposition);

        return new WaitCommand(1);
    }

    private readonly record struct Run(string GoodId, string DestinationId, int Units, double Net, int Days);

    /// <summary>The most profitable loaded run leaving a given city, at current prices.</summary>
    private static Run? BestRunFrom(Game game, string cityId)
    {
        var state = game.State;
        var world = game.World;
        var eco = world.Config.Economy;

        var origin = world.City(cityId);
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        var upkeep = CaravanMath.DailyUpkeep(state.Caravan, world);

        Run? best = null;

        foreach (var route in world.Routes.From(cityId))
        {
            var destinationId = route.Other(cityId);
            var destination = world.City(destinationId);

            var days = CaravanMath.TravelDays(state.Caravan, world, route);
            if (days <= 0 || days == int.MaxValue) continue;

            var fixedCost = CaravanMath.TravelFuel(state.Caravan, world, route) + upkeep * days;

            foreach (var good in world.Goods)
            {
                var originProfile = origin.Market[good.Id];
                var originStock = state.StockOf(cityId, good.Id);

                var destinationProfile = destination.Market[good.Id];
                var destinationStock = state.StockOf(destinationId, good.Id);

                if (Economy.SellUnitPrice(good, destinationProfile, destinationStock, eco)
                    <= Economy.BuyUnitPrice(good, originProfile, originStock, eco)) continue;

                var maxUnits = Economy.MaxAffordableUnits(good, originProfile, originStock, state.Cash, free, eco);
                if (maxUnits <= 0) continue;

                // Order size is itself a decision. Buying the maximum walks the purchase
                // price up and then craters the sale price on arrival, so the best run is
                // often well short of a full hold. Both sides are priced against the depth
                // the order actually consumes rather than the marginal price.
                foreach (var fraction in OrderSizes)
                {
                    var units = (int)(maxUnits * fraction);
                    if (units <= 0) continue;

                    var cost = Economy.ApproximateBuyCost(good, originProfile, originStock, units, eco);
                    var revenue = Economy.ApproximateSellRevenue(
                        good, destinationProfile, destinationStock, units, eco);

                    var net = revenue - cost - fixedCost;
                    if (best is null || net > best.Value.Net)
                        best = new Run(good.Id, destinationId, units, net, days);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// One hop of lookahead: which neighbour is worth moving to empty, judged by the
    /// best run available once we get there, less the cost of getting there.
    /// </summary>
    private static string? BestRepositioning(Game game, string cityId)
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
}

/// <summary>
/// Trades at random. The control group: if this makes money, the economy is a
/// money printer and the trade loop has no tension.
/// </summary>
public sealed class RandomTrader : ITraderPolicy
{
    public string Name => "random";

    public Command? Decide(Game game, Rng rng)
    {
        var state = game.State;
        var world = game.World;

        if (state.Caravan.Travel is { } travel)
            return new WaitCommand(Math.Max(1, travel.DaysRemaining));

        var cityId = state.Caravan.LocationId;
        if (cityId is null) return new WaitCommand(1);

        var roll = rng.NextDouble();

        if (roll < 0.35)
        {
            foreach (var (goodId, lot) in state.Caravan.Cargo)
            {
                if (lot.Units <= 0) continue;
                var units = Math.Max(1, rng.NextInt(lot.Units) + 1);
                return new SellCommand(goodId, units);
            }
        }

        if (roll < 0.70)
        {
            var good = world.Goods[rng.NextInt(world.Goods.Count)];
            var profile = world.City(cityId).Market[good.Id];
            var stock = state.StockOf(cityId, good.Id);
            var free = CaravanMath.FreeVolume(state.Caravan, world);

            var max = Economy.MaxAffordableUnits(good, profile, stock, state.Cash, free, world.Config.Economy);
            if (max > 0) return new BuyCommand(good.Id, Math.Max(1, rng.NextInt(max) + 1));
        }

        var routes = world.Routes.From(cityId);
        if (routes.Count == 0) return new WaitCommand(1);

        var chosen = routes[rng.NextInt(routes.Count)];
        return new DepartCommand(chosen.Other(cityId));
    }
}
