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

}
