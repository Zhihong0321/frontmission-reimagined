using System.Diagnostics;
using MechaTrader.Content;
using MechaTrader.Core;
using MechaTrader.Core.Ai;
using MechaTrader.Core.Events;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.BalanceSim;

/// <summary>
/// The headless gate on the economy. Runs the world unattended for a long stretch and
/// asserts it stays sane, stays interesting, and stays fast; then asserts that playing
/// well beats playing badly, and that a HouseTrader play-tester still finishes up while
/// touching crew, trucks or standing. Exits non-zero if any of that fails, so it can be
/// run in CI or from a script without a human reading the output.
/// </summary>
public static partial class Program
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

        Header("CREW AND RECRUITMENT");
        PrintCrew(world, failures);

        Header("BEST ONE-HOP RUNS ON DAY 200");
        PrintOpportunities(world, failures);

        Header("NAIVE HAULS - BUY A MAKER'S SURPLUS, SELL NEXT DOOR");
        var naive = NaiveHaulProbe(world);
        PrintNaiveHauls(naive);
        AssertNaiveHauls(world, naive, failures);

        Header($"SKILL EXPRESSION, {BotDays} DAYS x {BotSeeds} SEEDS");
        var greedy = RunBots(world, () => new GreedyTrader());
        var random = RunBots(world, () => new RandomTrader());
        var house = RunBots(world, () => new HouseTrader());

        PrintBotRow("greedy", greedy);
        PrintBotRow("random", random);
        PrintBotRow("house", house);

        var greedyMean = greedy.Average(r => (double)r.Profit);
        var randomMean = random.Average(r => (double)r.Profit);
        var houseMean = house.Average(r => (double)r.Profit);

        if (greedyMean <= 0)
            failures.Add($"A greedy trader averages {greedyMean:N0} cr over {BotDays} days. " +
                         "Skilled play must be profitable or there is no game.");

        if (randomMean >= 0)
            failures.Add($"A random trader averages {randomMean:N0} cr over {BotDays} days. " +
                         "Careless play must lose money or the loop has no tension.");

        if (greedyMean <= randomMean)
            failures.Add("A greedy trader does not out-earn a random one; the economy has no skill expression.");

        AssertPlaytest(house, houseMean, failures);

        var figuresPath = WriteFigures(world, report, greedy, random, house, naive);

        Header("RESULT");
        if (failures.Count == 0)
        {
            Console.WriteLine("BALANCE OK");
            Console.WriteLine($"  figures written to {figuresPath}");
            Console.WriteLine($"  skilled play: {greedyMean:N0} cr over {BotDays} days");
            Console.WriteLine($"  careless play: {randomMean:N0} cr over {BotDays} days");
            Console.WriteLine($"  edge: {greedyMean - randomMean:N0} cr");
            Console.WriteLine($"  house playtest: {houseMean:N0} cr over {BotDays} days");
            return 0;
        }

        Console.WriteLine($"BALANCE FAILED ({failures.Count} problem(s))");
        foreach (var failure in failures) Console.WriteLine($"  - {failure}");
        return 1;
    }

}
