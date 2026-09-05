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


}
