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
                    var stock = state.StockOf(city.Id, good.Id).Total;

                    if (double.IsNaN(stock) || double.IsInfinity(stock) || stock < 0)
                        failures.Add($"{city.Id}/{good.Id} stock became {stock} on day {day}.");

                    var price = Economy.UnitPrice(
                        good, city.Market[good.Id], stock, eco,
                        WorldEvents.PriceMultiplier(state, world, city.Id, good.Id));

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

    /// <summary>
    /// What the recruitment centres are offering and what the best of them is worth.
    ///
    /// The hard check here is the one that keeps the price levers safe: however good a
    /// crew gets, buying and selling in the same city must never turn a profit. Crew
    /// erode the market spread rather than move the mid price, so this should hold by
    /// construction - which is exactly why it is worth asserting.
    /// </summary>
    private static void PrintCrew(WorldData world, List<string> failures)
    {
        var cfg = world.Crew;
        const ulong seed = 20260901UL;

        Console.WriteLine();
        Console.WriteLine($"{cfg.Skills.Count} skills, {cfg.Roles.Count} roles, " +
                          $"capacity {cfg.CrewCapacity}, pool refreshes every {cfg.RefreshDays} days");

        Console.WriteLine();
        Console.WriteLine($"{"skill",-14}{"lever",-9}{"at max",10}   effect at max skill");
        Console.WriteLine(new string('-', 78));
        foreach (var skill in cfg.Skills)
        {
            Console.WriteLine($"{skill.Name,-14}{skill.Lever,-9}{skill.MaxEffect,10:P0}   {skill.Blurb}");
        }

        var pools = world.Cities
            .Select(c => (City: c, Pool: Recruitment.PoolFor(world, c, seed, 1)))
            .ToList();

        var candidates = pools.SelectMany(p => p.Pool).ToList();
        if (candidates.Count == 0)
        {
            failures.Add("No city offers a single recruit; the recruitment centres are empty.");
            return;
        }

        var emptyCities = pools.Where(p => p.Pool.Count == 0).Select(p => p.City.Id).ToList();
        if (emptyCities.Count > 0)
            failures.Add($"Cities with no recruits at all: {string.Join(", ", emptyCities)}.");

        Console.WriteLine();
        Console.WriteLine($"day 1 pool: {candidates.Count} candidates across {world.Cities.Count} cities, " +
                          $"wages {candidates.Min(c => c.DailyWage):N0}-{candidates.Max(c => c.DailyWage):N0} cr/day, " +
                          $"signing fees {candidates.Min(c => c.SigningFee):N0}-{candidates.Max(c => c.SigningFee):N0} cr");

        Console.WriteLine();
        Console.WriteLine($"{"best hire per lever",-34}{"city",-12}{"level",7}{"wage",9}{"fee",9}   effect");
        Console.WriteLine(new string('-', 90));

        foreach (var skill in cfg.Skills)
        {
            var pick = pools
                .SelectMany(p => p.Pool.Select(c => (p.City, Candidate: c)))
                .OrderByDescending(x => x.Candidate.Skills.TryGetValue(skill.Id, out var v) ? v : 0)
                .ThenBy(x => x.Candidate.DailyWage)
                .First();

            var level = pick.Candidate.Skills[skill.Id];
            var effect = skill.MaxEffect * level / cfg.MaxSkill;

            Console.WriteLine($"{skill.Name + " (" + pick.Candidate.Name + ")",-34}{pick.City.Name,-12}" +
                              $"{level,7}{pick.Candidate.DailyWage,9:N0}{pick.Candidate.SigningFee,9:N0}" +
                              $"   {effect,6:P0} on {skill.Lever}");
        }

        // A maxed-out roster: the strongest terms the game can ever offer.
        // Each ceiling hand sits on the post that claims their lever, or the terms would
        // never see them.
        var maxed = cfg.Skills.Select(s => new CrewMember
        {
            Id = $"ceiling-{s.Id}",
            Name = s.Name,
            PostId = cfg.PostFor(s.Lever)?.Id ?? "",
            Skills = cfg.Skills.ToDictionary(x => x.Id, x => x.Id == s.Id ? cfg.MaxSkill : 1)
        }).ToList();

        var terms = CrewMath.Terms(maxed, cfg);
        var eco = world.Config.Economy;

        // Checked with a full intake as well as an empty one: the buy side reads the
        // shelf and the sell side reads the total, so a glutted city is the case where
        // the two are furthest apart.
        var worstCase = world.Cities
            .SelectMany(city => world.Goods.Select(good => (city, good)))
            .SelectMany(pair =>
            {
                var profile = pair.city.Market[pair.good.Id];
                var shelf = Economy.InitialStock(profile, eco);

                // Empty intake and a glutted one: the second is where the shelf and the
                // total are furthest apart, and so where an inversion would show first.
                return new[] { 0.0, profile.Equilibrium * 5 }.Select(intake =>
                {
                    var stock = new CityStock(shelf, intake);
                    var buy = Economy.BuyUnitPrice(pair.good, profile, stock, eco, terms);
                    var sell = Economy.SellUnitPrice(pair.good, profile, stock, eco, terms);
                    return sell - buy;
                });
            })
            .Max();

        Console.WriteLine();
        Console.WriteLine($"with a perfect crew: buy spread share {terms.BuySpreadShare:P0}, " +
                          $"sell spread share {terms.SellSpreadShare:P0}, " +
                          $"speed x{CrewMath.SpeedMultiplier(maxed, cfg):0.00}, " +
                          $"running costs x{CrewMath.RunningCostMultiplier(maxed, cfg):0.00}");
        Console.WriteLine($"best in-place round trip with that crew: {worstCase:0.000} cr/unit " +
                          "(must not be positive)");

        if (worstCase > 0)
        {
            failures.Add($"A perfect crew can buy and sell in the same city for {worstCase:0.00} cr/unit. " +
                         "Crew must erode the spread, never invert it.");
        }
    }

    /// <summary>
    /// Rewrites FIGURES.md from what this run just measured.
    ///
    /// Every number a new session needs about the shape of the game lives there and
    /// nowhere else. It is generated rather than written by hand because the numbers in
    /// a hand-maintained brief go stale the first time somebody retunes the economy and
    /// forgets a line - and a stale figure in an onboarding document is worse than an
    /// absent one, because it will be believed.
    /// </summary>
    private static string WriteFigures(
        WorldData world,
        EconomyReport report,
        IReadOnlyList<BotRunResult> greedy,
        IReadOnlyList<BotRunResult> random,
        IReadOnlyList<BotRunResult> house,
        IReadOnlyList<NaiveHaul> naive)
    {
        var path = Path.Combine(RepositoryRoot(), "FIGURES.md");

        var start = world.City(world.Config.StartCityId);
        var opening = Game.New(world, FigureSeed).View().Routes
            .Where(r => r.BestProfit > 0)
            .ToList();

        var crew = world.Crew;
        var candidates = world.Cities
            .SelectMany(c => Recruitment.PoolFor(world, c, FigureSeed, 1))
            .ToList();

        var text = new System.Text.StringBuilder();

        text.AppendLine("# Current figures");
        text.AppendLine();
        text.AppendLine("**Generated** by `dotnet run --project tools/MechaTrader.BalanceSim`, which");
        text.AppendLine("`check.ps1` runs on every verification. Do not edit by hand - your edit will be");
        text.AppendLine("overwritten, and the point of this file is that it cannot go stale.");
        text.AppendLine();

        text.AppendLine("## World");
        text.AppendLine();
        text.AppendLine($"- {world.Cities.Count} cities, {world.Goods.Count} goods in {world.Categories.Count} categories " +
                        $"and {world.Tiers.Count} tiers, {world.Routes.All.Count} roads, {world.Industries.Count} industry archetypes");
        text.AppendLine($"- {world.TruckUpgrades.Count} truck fittings, {world.Contracts.Kinds.Count} contract kinds, " +
                        $"{world.Expos.Themes.Count} expo themes on a {world.Expos.CycleDays}-day cycle");
        text.AppendLine($"- Standing: {world.Standing.Segments.Count} segments of {world.Standing.SegmentMax:0}; " +
                        string.Join(", ", world.Tiers.Where(t => t.MinStanding > 0).Select(t => $"{t.Name} needs {t.MinStanding:0}")));
        text.AppendLine($"- {world.Trucks.Count} truck types, {crew.Skills.Count} crew skills, " +
                        $"{crew.Roles.Count} hiring roles");
        text.AppendLine($"- Start: {start.Name}, {world.Config.StartCash:N0} cr, " +
                        $"{string.Join(" + ", world.Config.StartTruckIds.Select(id => world.Truck(id).Name))}");

        foreach (var id in world.Config.StartTruckIds.Distinct())
        {
            var truck = world.Truck(id);
            text.AppendLine($"  - {truck.Name}: {truck.Capacity:N0} capacity, " +
                            $"{truck.SpeedKmPerDay:N0} km/day, {truck.UpkeepPerDay:N0} cr/day upkeep, " +
                            $"{truck.FuelPerKm:0.00} cr/km fuel");
        }

        text.AppendLine();
        text.AppendLine("## Opening position");
        text.AppendLine();

        if (opening.Count == 0)
        {
            text.AppendLine("- No profitable run leaves the start city on day 1.");
        }
        else
        {
            text.AppendLine($"{opening.Count} profitable opening run(s) on day 1, best cargo priced both legs:");
            text.AppendLine();
            foreach (var run in opening)
            {
                text.AppendLine($"- {start.Name} to {run.ToName}: {run.BestUnits:N0} x {run.BestGoodName}, " +
                                $"+{run.BestProfit:N0} cr over {run.Days} day(s), " +
                                $"{run.DistanceKm:N0} km of {run.TerrainName}");
            }
        }

        text.AppendLine();
        text.AppendLine("## Economy");
        text.AppendLine();
        // Rounded hard: the measurement wobbles a few ms between runs, and this file is
        // rewritten on every verification. An exact figure would churn in git forever
        // while telling nobody anything they did not already know.
        var tickBucket = Math.Max(10, (int)Math.Round(report.ElapsedMs / 10.0) * 10);

        text.AppendLine($"- {SimulationDays}-day tick: ~{tickBucket} ms for " +
                        $"{SimulationDays * world.Cities.Count * world.Goods.Count:N0} market updates " +
                        $"(budget {PerformanceBudgetMs} ms)");
        text.AppendLine($"- {report.Goods.Count(g => g.MedianSpread >= RequiredSpread)} of " +
                        $"{world.Goods.Count} goods hold a cross-city spread of at least {RequiredSpread:P0}");
        text.AppendLine();
        text.AppendLine("| good | base | min | max | mean | median cross-city spread |");
        text.AppendLine("|---|---|---|---|---|---|");

        foreach (var g in report.Goods)
        {
            text.AppendLine($"| {g.Name} | {g.BasePrice:N0} | {g.MinPrice:N0} | {g.MaxPrice:N0} | " +
                            $"{g.MeanPrice:N0} | {g.MedianSpread:P0} |");
        }

        text.AppendLine();
        text.AppendLine("## Naive routes");
        text.AppendLine();
        text.AppendLine("A plain haul of a city's own surplus to a road neighbour, full purse, no planning,");
        text.AppendLine("no crew. The check that keeps the 'sell next door and lose half the purse'");
        text.AppendLine("complaint from coming back.");
        text.AppendLine();

        var nonMaker = naive.Where(r => !r.DestMakes).ToList();
        var maker = naive.Where(r => r.DestMakes).ToList();
        var purse = world.Config.StartCash;
        var worstNet = naive.Min(r => r.Net);
        double LosingShare(IReadOnlyList<NaiveHaul> slice)
            => slice.Count == 0 ? 0.0 : (double)slice.Count(r => r.Return < 0) / slice.Count;

        text.AppendLine($"- {naive.Count} producer->neighbour runs: " +
                        $"{LosingShare(naive):P0} lose, " +
                        $"median {Median(naive.Select(r => r.Return).ToList()):+0.0%;-0.0%}");
        if (nonMaker.Count > 0)
        {
            text.AppendLine($"- hauling to a city that does not make the good: {nonMaker.Count} runs, " +
                            $"{LosingShare(nonMaker):P0} lose, " +
                            $"median {Median(nonMaker.Select(r => r.Return).ToList()):+0.0%;-0.0%}");
        }
        if (maker.Count > 0)
        {
            text.AppendLine($"- hauling to a city that makes it too: {maker.Count} runs, " +
                            $"{LosingShare(maker):P0} lose, " +
                            $"median {Median(maker.Select(r => r.Return).ToList()):+0.0%;-0.0%} " +
                            "- the direction mistake");
        }
        text.AppendLine($"- worst naive loss: {worstNet:N0} cr" +
                        $"{((purse > 0) ? $" ({worstNet / purse:P0} of the {purse:N0} cr start purse)" : "")}");

        text.AppendLine();
        text.AppendLine("## Skill expression");
        text.AppendLine();
        text.AppendLine($"Over {BotDays} days x {BotSeeds} seeds on {world.Config.StartCash:N0} starting capital. " +
                        "Neither bot hires crew, so this is the un-crewed baseline.");
        text.AppendLine();
        text.AppendLine($"- Greedy (plays well): {greedy.Average(r => (double)r.Profit):N0} cr");
        text.AppendLine($"- Random (plays badly): {random.Average(r => (double)r.Profit):N0} cr");
        text.AppendLine($"- Edge: {greedy.Average(r => (double)r.Profit) - random.Average(r => (double)r.Profit):N0} cr");

        AppendPlaytest(text, world, house);

        text.AppendLine();
        text.AppendLine("## Crew");
        text.AppendLine();
        text.AppendLine($"- {crew.CrewCapacity} seats; every city's board re-rolls every {crew.RefreshDays} days");
        text.AppendLine($"- {candidates.Count} candidates across the map per round");

        if (candidates.Count > 0)
        {
            text.AppendLine($"- Wages {candidates.Min(c => c.DailyWage):N0}-{candidates.Max(c => c.DailyWage):N0} cr/day, " +
                            $"signing fees {candidates.Min(c => c.SigningFee):N0}-{candidates.Max(c => c.SigningFee):N0} cr");
        }

        text.AppendLine();
        text.AppendLine("| skill | lever | effect at level " + crew.MaxSkill + " |");
        text.AppendLine("|---|---|---|");

        foreach (var skill in crew.Skills)
        {
            text.AppendLine($"| {skill.Name} | `{skill.Lever}` | {skill.MaxEffect:P0} |");
        }

        File.WriteAllText(path, text.ToString());
        return path;
    }

    private const ulong FigureSeed = 20260901UL;

    /// <summary>The repository root, found from the data folder the content loader located.</summary>
    private static string RepositoryRoot()
        => Directory.GetParent(ContentLoader.FindDataDirectory())!.FullName;

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
        var cities = runs.Average(r => (double)r.CitiesVisited.Count);

        var systems = new List<string>();
        if (runs.Any(r => r.UsedCrew)) systems.Add("crew");
        if (runs.Any(r => r.UsedTrucks)) systems.Add("trucks");
        if (runs.Any(r => r.UsedFavor)) systems.Add("standing");
        if (runs.Any(r => r.UsedStation)) systems.Add("station");
        if (runs.Any(r => r.UsedContracts)) systems.Add("contracts");
        if (runs.Any(r => r.UsedExpo)) systems.Add("expo");
        var systemText = systems.Count == 0 ? "haulage" : string.Join("+", systems);

        Console.WriteLine($"{label,-8} mean {mean,12:N0} cr   best {best,12:N0}   worst {worst,12:N0}" +
                          $"   rejected {rejected,4}   cities {cities,4:0.0}   {systemText}");
    }

    private const double MaxHouseRejectionRate = 0.10;

    private static void AssertPlaytest(IReadOnlyList<BotRunResult> house, double houseMean, List<string> failures)
    {
        if (houseMean <= 0)
            failures.Add($"A house trader averages {houseMean:N0} cr over {BotDays} days. " +
                         "The play-tester must still finish up or it is not a player.");

        var rejectRate = house.Average(r => r.RejectionRate);
        if (rejectRate > MaxHouseRejectionRate)
            failures.Add($"A house trader rejects {rejectRate:P0} of its commands (budget {MaxHouseRejectionRate:P0}). " +
                         "A stuck policy is not play-testing the game.");

        var cities = house.Average(r => (double)r.CitiesVisited.Count);
        if (cities < 2)
            failures.Add($"A house trader visits {cities:0.0} cities on average. " +
                         "Play-testing a trade game requires leaving town.");

        if (!house.Any(r => r.UsedCrew || r.UsedTrucks || r.UsedFavor))
            failures.Add("A house trader never hired, bought a truck, or courted a governor. " +
                         "Those systems are then untested by play.");
    }

    private static void AppendPlaytest(
        System.Text.StringBuilder text,
        WorldData world,
        IReadOnlyList<BotRunResult> house)
    {
        text.AppendLine();
        text.AppendLine("## Playtest");
        text.AppendLine();
        text.AppendLine($"HouseTrader, same {BotDays} days x {BotSeeds} seeds on {world.Config.StartCash:N0} starting capital. " +
                        "Haulage plus hire / extra mule / an economy fitting / donate. Contracts and the expo stall are " +
                        "player-only for now (see BRAIN.md). Live rivals are not in this world yet.");
        text.AppendLine();

        var mean = house.Average(r => (double)r.Profit);
        var best = house.Max(r => r.Profit);
        var worst = house.Min(r => r.Profit);
        var rejectRate = house.Average(r => r.RejectionRate);
        var cities = house.Average(r => (double)r.CitiesVisited.Count);
        var goods = house.SelectMany(r => r.GoodsTraded).Distinct().Count();
        var peak = house.Max(r => r.PeakNetWorth);
        var trough = house.Min(r => r.TroughNetWorth);
        var crew = house.Average(r => (double)r.EndCrewCount);
        var trucks = house.Average(r => (double)r.EndTruckCount);
        var standing = house.Max(r => r.MaxStanding);
        var events = house.Count(r => r.SawWorldEvent);
        var bankrupt = house.Count(r => r.WentBankrupt);

        var systems = new List<string>();
        if (house.Any(r => r.UsedCrew)) systems.Add("crew");
        if (house.Any(r => r.UsedTrucks)) systems.Add("trucks");
        if (house.Any(r => r.UsedFavor)) systems.Add("standing");
        if (house.Any(r => r.UsedStation)) systems.Add("station");
        if (house.Any(r => r.UsedContracts)) systems.Add("contracts");
        if (house.Any(r => r.UsedExpo)) systems.Add("expo");
        var systemText = systems.Count == 0 ? "none" : string.Join(", ", systems);

        text.AppendLine($"- Mean profit: {mean:N0} cr (best {best:N0}, worst {worst:N0})");
        text.AppendLine($"- Rejection rate: {rejectRate:P0}");
        text.AppendLine($"- Cities visited: {cities:0.0} average; {goods} distinct goods traded");
        text.AppendLine($"- Net worth range: {trough:N0} – {peak:N0} cr");
        text.AppendLine($"- End crew: {crew:0.0}; end trucks: {trucks:0.0}; max standing: {standing:0.#}");
        text.AppendLine($"- World events seen in {events} of {house.Count} seeds; bankruptcies: {bankrupt}");
        text.AppendLine($"- Systems touched: {systemText}");
        text.AppendLine();

        var mix = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var run in house)
        {
            foreach (var (kind, count) in run.CommandMix)
                mix[kind] = mix.TryGetValue(kind, out var n) ? n + count : count;
        }

        text.AppendLine("Command mix across the seed set:");
        text.AppendLine();
        foreach (var (kind, count) in mix.OrderBy(kv => kv.Key))
            text.AppendLine($"- `{kind}`: {count:N0}");
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
