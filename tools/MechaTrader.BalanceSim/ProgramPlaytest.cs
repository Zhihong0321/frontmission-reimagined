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

}
