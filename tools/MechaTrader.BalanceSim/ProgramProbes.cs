using System.Diagnostics;
using MechaTrader.Content;
using MechaTrader.Core;
using MechaTrader.Core.Ai;
using MechaTrader.Core.Events;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.BalanceSim;

public static partial class Program
{
    /// <summary>
    /// The most profitable single-hop hauls available once the world has settled.
    /// This is the direct measure of whether there is a game here: if the best run on
    /// the whole map cannot clear its own fuel, no policy and no player can profit.
    /// </summary>
    private static void PrintOpportunities(WorldData world, List<string> failures)
    {
        var game = Game.New(world, 20260901UL);
        var events = new List<GameEvent>();
        for (var day = 0; day < 200; day++)
        {
            DayTick.Advance(game.State, world, events);
            events.Clear();
        }

        var state = game.State;
        var eco = world.Config.Economy;
        var caravan = state.Caravan;
        var terms = CrewMath.Terms(caravan, world);
        var upkeep = CaravanMath.DailyUpkeep(caravan, world);
        var capacity = CaravanMath.Capacity(caravan, world);

        var runs = new List<(string Label, double Margin, double Net, int Days, int Units)>();

        foreach (var route in world.Routes.All)
        {
            var days = CaravanMath.TravelDays(caravan, world, route);
            var fixedCost = CaravanMath.TravelFuel(caravan, world, route) + upkeep * days;

            foreach (var (fromId, toId) in new[] { (route.FromId, route.ToId), (route.ToId, route.FromId) })
            {
                var from = world.City(fromId);
                var to = world.City(toId);

                foreach (var good in world.Goods)
                {
                    // A stranger cannot buy a locked grade; the opening map is judged on what it can.
                    if (!Standing.TierOpen(world.TierOf(good), Standing.Of(state, fromId))) continue;

                    var buyMult = WorldEvents.PriceMultiplier(state, world, fromId, good.Id);
                    var sellMult = WorldEvents.PriceMultiplier(state, world, toId, good.Id);

                    var gradeMult = QualityMath.SellMultiplier(state.StockOf(fromId, good.Id).OutQuality, world.Quality);
                    var units = Economy.MaxAffordableUnits(
                        good, from.Market[good.Id], state.StockOf(fromId, good.Id),
                        world.Config.StartCash, capacity, eco, terms, buyMult, gradeMult);
                    if (units <= 0) continue;

                    var cost = Economy.ApproximateBuyCost(
                        good, from.Market[good.Id], state.StockOf(fromId, good.Id), units, eco, terms, buyMult) * gradeMult;
                    var revenue = Economy.ApproximateSellRevenue(
                        good, to.Market[good.Id], state.StockOf(toId, good.Id), units, eco, terms, sellMult) * gradeMult;

                    if (cost <= 0) continue;

                    var net = revenue - cost - fixedCost;
                    var margin = net / cost;

                    runs.Add(($"{good.Name} {from.Name}->{to.Name}", margin, net, days, units));
                }
            }
        }

        var top = runs.OrderByDescending(r => r.Net).Take(6).ToList();

        Console.WriteLine();
        Console.WriteLine($"{"run",-44}{"units",7}{"days",6}{"net",12}{"margin",9}");
        Console.WriteLine(new string('-', 78));
        foreach (var r in top)
        {
            Console.WriteLine($"{r.Label,-44}{r.Units,7:N0}{r.Days,6}{r.Net,12:N0}{r.Margin,9:P0}");
        }

        var profitable = runs.Count(r => r.Net > 0);
        Console.WriteLine();
        Console.WriteLine($"profitable one-hop runs on the map: {profitable} of {runs.Count}");

        if (profitable == 0)
            failures.Add("No single-hop run on the entire map is profitable after fuel and upkeep.");
    }

    /// <summary>One plain haul: a city's own surplus, bought at full purse, sold next door.</summary>
    private sealed record NaiveHaul(string Label, bool DestMakes, double Return, double Net);

    /// <summary>
    /// The owner's complaint, measured. Before the flat-price fix, buying what a city
    /// makes and selling it next door lost money on 84% of the map and the worst case
    /// lost half the purse. This probe is the durable version of that measurement: no
    /// planning, no crew, full purse, straight to a road neighbour.
    /// </summary>
    private static List<NaiveHaul> NaiveHaulProbe(WorldData world)
    {
        var game = Game.New(world, 20260901UL);
        var state = game.State;
        var eco = world.Config.Economy;
        var caravan = state.Caravan;
        var terms = CrewMath.Terms(caravan, world);
        var upkeep = CaravanMath.DailyUpkeep(caravan, world);
        var capacity = CaravanMath.Capacity(caravan, world);
        var cash = world.Config.StartCash;

        var runs = new List<NaiveHaul>();

        foreach (var city in world.Cities)
        {
            foreach (var route in world.Routes.All)
            {
                if (route.FromId != city.Id && route.ToId != city.Id) continue;
                var neighbor = world.City(route.Other(city.Id));

                foreach (var good in world.Goods)
                {
                    var profile = city.Market[good.Id];

                    // "Buy in city product": the city's own produce, in surplus.
                    if (profile.Production <= profile.Consumption) continue;
                    if (!Standing.TierOpen(world.TierOf(good), Standing.Of(state, city.Id))) continue;

                    var stockFrom = state.StockOf(city.Id, good.Id);
                    var stockTo = state.StockOf(neighbor.Id, good.Id);

                    var buyMult = WorldEvents.PriceMultiplier(state, world, city.Id, good.Id);
                    var sellMult = WorldEvents.PriceMultiplier(state, world, neighbor.Id, good.Id);

                    var gradeBuy = QualityMath.SellMultiplier(stockFrom.OutQuality, world.Quality);
                    var gradeSell = QualityMath.SellMultiplier(stockTo.OutQuality, world.Quality);

                    var days = CaravanMath.TravelDays(caravan, world, route);
                    var fixedCost = CaravanMath.TravelFuel(caravan, world, route) + upkeep * days;

                    var units = Economy.MaxAffordableUnits(
                        good, profile, stockFrom, cash, capacity, eco, terms, buyMult, gradeBuy);
                    if (units <= 0) continue;

                    var cost = Economy.ApproximateBuyCost(
                        good, profile, stockFrom, units, eco, terms, buyMult) * gradeBuy;
                    var revenue = Economy.ApproximateSellRevenue(
                        good, neighbor.Market[good.Id], stockTo, units, eco, terms, sellMult) * gradeSell;

                    if (cost <= 0) continue;

                    var net = revenue - cost - fixedCost;
                    runs.Add(new NaiveHaul(
                        $"{good.Name}: {city.Name}->{neighbor.Name}",
                        neighbor.Market[good.Id].Production > 0.0,
                        net / cost,
                        net));
                }
            }
        }

        return runs;
    }

    private static void PrintNaiveHauls(List<NaiveHaul> runs)
    {
        if (runs.Count == 0)
        {
            Console.WriteLine("no naive producer->neighbour hauls to measure");
            return;
        }

        var nonMaker = runs.Where(r => !r.DestMakes).ToList();
        var maker = runs.Where(r => r.DestMakes).ToList();

        Console.WriteLine();
        Console.WriteLine($"naive producer->neighbour runs: {runs.Count}, " +
                          $"losing {100.0 * runs.Count(r => r.Return < 0) / runs.Count:0.0}%, " +
                          $"median {Median(runs.Select(r => r.Return).ToList()):+0.0%;-0.0%}");
        Console.WriteLine($"  to a city that does NOT make the good: {nonMaker.Count} runs, " +
                          $"{100.0 * nonMaker.Count(r => r.Return < 0) / nonMaker.Count:0.0}% lose, " +
                          $"median {Median(nonMaker.Select(r => r.Return).ToList()):+0.0%;-0.0%}");
        if (maker.Count > 0)
        {
            Console.WriteLine($"  to a city that makes it too: {maker.Count} runs, " +
                              $"{100.0 * maker.Count(r => r.Return < 0) / maker.Count:0.0}% lose, " +
                              $"median {Median(maker.Select(r => r.Return).ToList()):+0.0%;-0.0%} " +
                              "(the direction mistake)");
        }

        Console.WriteLine();
        Console.WriteLine($"{"worst naive hauls",-44}{"net",12}");
        Console.WriteLine(new string('-', 56));
        foreach (var r in runs.OrderBy(r => r.Net).Take(5))
            Console.WriteLine($"{r.Label,-44}{r.Net,12:N0} ({r.Return:+0.0%;-0.0%})");
    }

    /// <summary>
    /// The guard that keeps the owner's "this is torture" complaint from coming back:
    /// a plain haul of a maker's surplus to a city that does not make it must pay, most
    /// plain producer->neighbour hauls must not lose, and no naive full-hold haul may
    /// lose half the starting purse.
    /// </summary>
    private static void AssertNaiveHauls(WorldData world, List<NaiveHaul> runs, List<string> failures)
    {
        if (runs.Count == 0)
        {
            failures.Add("No naive producer->neighbour haul exists to measure; the probe is empty.");
            return;
        }

        var nonMaker = runs.Where(r => !r.DestMakes).ToList();
        var medianNonMaker = Median(nonMaker.Select(r => r.Return).ToList());

        if (medianNonMaker <= 0)
        {
            failures.Add($"The median haul of a maker's surplus to a city that does not make it is " +
                         $"{medianNonMaker:P0}. A plain good-direction trade must pay, or the owner's " +
                         $"complaint is back by construction.");
        }

        var losingShare = (double)runs.Count(r => r.Return < 0) / runs.Count;
        if (losingShare >= 0.5)
        {
            failures.Add($"{losingShare:P0} of naive producer->neighbour hauls lose money " +
                         "(the pre-fix figure was 84%, the post-fix figure 32%).");
        }

        var worst = runs.Min(r => r.Net);
        var purse = world.Config.StartCash;
        if (worst <= -0.5 * purse)
        {
            failures.Add($"A naive full-hold haul lost {worst:N0} cr, half the {purse:N0} cr start purse. " +
                         "The 'losses up to 50% of capital' complaint is back.");
        }
    }

}
