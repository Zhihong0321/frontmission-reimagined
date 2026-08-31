using MechaTrader.Core.Events;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>
/// Advances the world by one day. The world keeps running while the convoy is on the
/// road, so arriving to find the price has moved is a normal part of play.
/// </summary>
public static class DayTick
{
    public static void Advance(GameState state, WorldData world, List<GameEvent> events)
    {
        var eco = world.Config.Economy;
        var rng = new Rng(state.RngState);

        ChargeRunningCosts(state, world);
        TickMarkets(state, world, rng);

        state.Day++;

        AdvanceTravel(state, world, events);

        state.RngState = rng.State;

        UpdateSolvency(state, events);
    }

    /// <summary>
    /// Upkeep and fuel are the money leak that replaces combat as the source of pressure.
    /// Without them, standing still is free and there is no cost to a bad route.
    /// </summary>
    private static void ChargeRunningCosts(GameState state, WorldData world)
    {
        var cost = CaravanMath.DailyUpkeep(state.Caravan, world);

        if (state.Caravan.Travel is { } travel)
            cost += travel.FuelPerDay;

        state.Cash -= (long)Math.Round(cost);
    }

    private static void TickMarkets(GameState state, WorldData world, Rng rng)
    {
        var eco = world.Config.Economy;

        // Iterate content in load order so the random sequence is stable across runs.
        foreach (var city in world.Cities)
        {
            if (!state.Stock.TryGetValue(city.Id, out var market)) continue;

            foreach (var good in world.Goods)
            {
                if (!city.Market.TryGetValue(good.Id, out var profile)) continue;
                market[good.Id] = Economy.TickStock(market[good.Id], profile, eco, rng);
            }
        }
    }

    private static void AdvanceTravel(GameState state, WorldData world, List<GameEvent> events)
    {
        if (state.Caravan.Travel is not { } travel) return;

        travel.DaysRemaining--;
        if (travel.DaysRemaining > 0) return;

        state.Caravan.LocationId = travel.ToId;
        state.Caravan.Travel = null;

        var city = world.City(travel.ToId);
        events.Add(new GameEvent(state.Day, GameEventKind.Arrival,
            $"Convoy arrived at {city.Name}, {city.Region}."));
    }

    private static void UpdateSolvency(GameState state, List<GameEvent> events)
    {
        if (state.Cash < 0 && !state.Bankrupt)
        {
            state.Bankrupt = true;
            events.Add(new GameEvent(state.Day, GameEventKind.Warning,
                "Accounts are in the red. Sell cargo to cover running costs."));
        }
        else if (state.Cash >= 0 && state.Bankrupt)
        {
            state.Bankrupt = false;
            events.Add(new GameEvent(state.Day, GameEventKind.Info, "Accounts back in the black."));
        }
    }
}
