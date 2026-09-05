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
    private static IReadOnlyList<BotRunResult> RunBots(WorldData world, Func<ITraderPolicy> factory)
    {
        var results = new List<BotRunResult>(BotSeeds);
        for (var i = 0; i < BotSeeds; i++)
        {
            results.Add(BotRunner.Run(world, factory(), BotDays, (ulong)(1000 + i * 7919)));
        }
        return results;
    }

}
