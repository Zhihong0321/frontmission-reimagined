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

}
