using System.Diagnostics;
using MechaTrader.Content;
using MechaTrader.Core;
using MechaTrader.Core.Ai;
using MechaTrader.Core.Events;
using MechaTrader.Core.Sim;
using MechaTrader.Core.World;

namespace MechaTrader.BalanceSim;

/// <summary>
/// The headless gate on the economy. Runs the world unattended for a long stretch and
/// asserts it stays sane, stays interesting, and stays fast; then asserts that playing
/// well beats playing badly. Exits non-zero if any of that fails, so it can be run in
/// CI or from a script without a human reading the output.
/// </summary>
public static class Program
{
    private const int SimulationDays = 1000;
    private const int BotDays = 60;
    private const int BotSeeds = 5;

    private const double MinPriceRatio = 0.30;
    private const double MaxPriceRatio = 3.50;
    private const double RequiredSpread = 0.20;
    private const int RequiredSpreadGoods = 5;
    private const int PerformanceBudgetMs = 500;

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var world = ContentLoader.LoadWorld();
        var failures = new List<string>();

        Header("WORLD");
        Console.WriteLine($"{world.Cities.Count} cities, {world.Goods.Count} goods, " +
                          $"{world.Routes.All.Count} routes, {world.Industries.Count} industries");
        PrintGlobalFlow(world);

        Header($"ECONOMY, {SimulationDays} DAYS UNATTENDED");
        var report = RunEconomy(world, SimulationDays, failures);
        PrintPriceTable(world, report);

        Console.WriteLine();
        Console.WriteLine($"tick time: {report.ElapsedMs:0.0} ms " +
                          $"({SimulationDays} days x {world.Cities.Count} cities x {world.Goods.Count} goods " +
                          $"= {SimulationDays * world.Cities.Count * world.Goods.Count:N0} updates)");

        if (report.ElapsedMs > PerformanceBudgetMs)
            failures.Add($"Simulation took {report.ElapsedMs:0}ms, budget is {PerformanceBudgetMs}ms.");

        var interestingGoods = report.Goods.Count(g => g.MedianSpread >= RequiredSpread);
        Console.WriteLine($"goods with a tradeable cross-city spread (>= {RequiredSpread:P0}): " +
                          $"{interestingGoods} of {world.Goods.Count}");

        if (interestingGoods < RequiredSpreadGoods)
            failures.Add($"Only {interestingGoods} goods carry a {RequiredSpread:P0} spread; " +
                         $"at least {RequiredSpreadGoods} are needed for the map to be worth traversing.");

        Header("BEST ONE-HOP RUNS ON DAY 200");
        PrintOpportunities(world, failures);

        Header($"SKILL EXPRESSION, {BotDays} DAYS x {BotSeeds} SEEDS");
        var greedy = RunBots(world, () => new GreedyTrader());
        var random = RunBots(world, () => new RandomTrader());

        PrintBotRow("greedy", greedy);
        PrintBotRow("random", random);

        var greedyMean = greedy.Average(r => (double)r.Profit);
        var randomMean = random.Average(r => (double)r.Profit);

        if (greedyMean <= 0)
            failures.Add($"A greedy trader averages {greedyMean:N0} cr over {BotDays} days. " +
                         "Skilled play must be profitable or there is no game.");

        if (randomMean >= 0)
            failures.Add($"A random trader averages {randomMean:N0} cr over {BotDays} days. " +
                         "Careless play must lose money or the loop has no tension.");

        if (greedyMean <= randomMean)
            failures.Add("A greedy trader does not out-earn a random one; the economy has no skill expression.");

        Header("RESULT");
        if (failures.Count == 0)
        {
            Console.WriteLine("BALANCE OK");
            Console.WriteLine($"  skilled play: {greedyMean:N0} cr over {BotDays} days");
            Console.WriteLine($"  careless play: {randomMean:N0} cr over {BotDays} days");
            Console.WriteLine($"  edge: {greedyMean - randomMean:N0} cr");
            return 0;
        }

        Console.WriteLine($"BALANCE FAILED ({failures.Count} problem(s))");
        foreach (var failure in failures) Console.WriteLine($"  - {failure}");
        return 1;
    }

    private sealed record GoodReport(
        string Id, string Name, double BasePrice,
        double MinPrice, double MaxPrice, double MeanPrice, double MedianSpread);

    private sealed record EconomyReport(IReadOnlyList<GoodReport> Goods, double ElapsedMs);

    private static EconomyReport RunEconomy(WorldData world, int days, List<string> failures)
    {
        var game = Game.New(world, 20260901UL);
        var state = game.State;
        var eco = world.Config.Economy;

        var minPrice = world.Goods.ToDictionary(g => g.Id, _ => double.MaxValue);
        var maxPrice = world.Goods.ToDictionary(g => g.Id, _ => double.MinValue);
        var sumPrice = world.Goods.ToDictionary(g => g.Id, _ => 0.0);
        var samples = world.Goods.ToDictionary(g => g.Id, _ => 0);
        var spreads = world.Goods.ToDictionary(g => g.Id, _ => new List<double>());

        var events = new List<GameEvent>();

        for (var day = 0; day < days; day++)
        {
            DayTick.Advance(state, world, events);
            events.Clear();

            var sampleDay = day % 5 == 0;
            if (!sampleDay) continue;

            foreach (var good in world.Goods)
            {
                double dayMin = double.MaxValue, dayMax = double.MinValue;

                foreach (var city in world.Cities)
                {
                    var stock = state.StockOf(city.Id, good.Id);

                    if (double.IsNaN(stock) || double.IsInfinity(stock) || stock < 0)
                        failures.Add($"{city.Id}/{good.Id} stock became {stock} on day {day}.");

                    var price = Economy.UnitPrice(good, city.Market[good.Id], stock, eco);

                    if (double.IsNaN(price) || double.IsInfinity(price) || price <= 0)
                        failures.Add($"{city.Id}/{good.Id} price became {price} on day {day}.");

                    var ratio = price / good.BasePrice;
                    if (ratio < MinPriceRatio || ratio > MaxPriceRatio)
                    {
                        failures.Add($"{city.Id}/{good.Id} price hit {ratio:0.00}x base on day {day}, " +
                                     $"outside [{MinPriceRatio:0.00}x, {MaxPriceRatio:0.00}x].");
                    }

                    if (price < minPrice[good.Id]) minPrice[good.Id] = price;
                    if (price > maxPrice[good.Id]) maxPrice[good.Id] = price;
                    if (price < dayMin) dayMin = price;
                    if (price > dayMax) dayMax = price;

                    sumPrice[good.Id] += price;
                    samples[good.Id]++;
                }

                if (dayMin > 0) spreads[good.Id].Add(dayMax / dayMin - 1.0);
            }
        }

        // Time a clean run with no sampling or validation, so the number reported is
        // the cost of the simulation itself rather than the cost of measuring it.
        var elapsedMs = MeasureTickCost(world, days);

        // Report at most a handful of distinct price violations; a broken tuning pass
        // would otherwise emit thousands of near-identical lines.
        if (failures.Count > 8) failures.RemoveRange(8, failures.Count - 8);

        var reports = world.Goods.Select(g => new GoodReport(
            g.Id, g.Name, g.BasePrice,
            minPrice[g.Id], maxPrice[g.Id],
            samples[g.Id] > 0 ? sumPrice[g.Id] / samples[g.Id] : 0,
            Median(spreads[g.Id]))).ToList();

        return new EconomyReport(reports, elapsedMs);
    }

    private static double MeasureTickCost(WorldData world, int days)
    {
        var game = Game.New(world, 20260901UL);
        var events = new List<GameEvent>();

        var stopwatch = Stopwatch.StartNew();
        for (var day = 0; day < days; day++)
        {
            DayTick.Advance(game.State, world, events);
            events.Clear();
        }
        stopwatch.Stop();

        return stopwatch.Elapsed.TotalMilliseconds;
    }


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
                    var units = Economy.MaxAffordableUnits(
                        good, from.Market[good.Id], state.StockOf(fromId, good.Id),
                        world.Config.StartCash, capacity, eco);
                    if (units <= 0) continue;

                    var cost = Economy.ApproximateBuyCost(
                        good, from.Market[good.Id], state.StockOf(fromId, good.Id), units, eco);
                    var revenue = Economy.ApproximateSellRevenue(
                        good, to.Market[good.Id], state.StockOf(toId, good.Id), units, eco);

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

    private static IReadOnlyList<BotRunResult> RunBots(WorldData world, Func<ITraderPolicy> factory)
    {
        var results = new List<BotRunResult>(BotSeeds);
        for (var i = 0; i < BotSeeds; i++)
        {
            results.Add(BotRunner.Run(world, factory(), BotDays, (ulong)(1000 + i * 7919)));
        }
        return results;
    }

    private static void PrintGlobalFlow(WorldData world)
    {
        Console.WriteLine();
        Console.WriteLine($"{"good",-16}{"produced",12}{"consumed",12}{"net",10}");
        Console.WriteLine(new string('-', 50));

        foreach (var good in world.Goods)
        {
            double produced = 0, consumed = 0;
            foreach (var city in world.Cities)
            {
                var profile = city.Market[good.Id];
                produced += profile.Production;
                consumed += profile.Consumption;
            }

            Console.WriteLine($"{good.Name,-16}{produced,12:N0}{consumed,12:N0}{produced - consumed,10:N0}");
        }
    }

    private static void PrintPriceTable(WorldData world, EconomyReport report)
    {
        Console.WriteLine();
        Console.WriteLine($"{"good",-16}{"base",8}{"min",9}{"max",9}{"mean",9}{"spread",9}");
        Console.WriteLine(new string('-', 60));

        foreach (var g in report.Goods)
        {
            Console.WriteLine($"{g.Name,-16}{g.BasePrice,8:N0}{g.MinPrice,9:N0}{g.MaxPrice,9:N0}" +
                              $"{g.MeanPrice,9:N0}{g.MedianSpread,9:P0}");
        }
    }

    private static void PrintBotRow(string label, IReadOnlyList<BotRunResult> runs)
    {
        var mean = runs.Average(r => (double)r.Profit);
        var best = runs.Max(r => r.Profit);
        var worst = runs.Min(r => r.Profit);
        var rejected = runs.Sum(r => r.CommandsRejected);

        Console.WriteLine($"{label,-8} mean {mean,12:N0} cr   best {best,12:N0}   worst {worst,12:N0}" +
                          $"   rejected {rejected,4}");
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static void Header(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 60));
    }
}
