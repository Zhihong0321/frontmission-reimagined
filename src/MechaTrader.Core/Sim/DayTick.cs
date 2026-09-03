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
        var rng = new Rng(state.RngState);
        var mining = state.Caravan.Travel is null && state.Caravan.SiteId is not null;

        ChargeRunningCosts(state, world);
        TickMarkets(state, world, rng);
        WarehouseMath.Tick(state, world, events);
        Expos.Tick(state, world, rng, events);

        state.Day++;

        WorldEvents.Tick(state, world, rng, events);
        LapseContracts(state, world, events);
        AdvanceTravel(state, world, events);
        if (mining) MapMath.Extract(state, world, events);

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

        cost += WarehouseMath.DailyRent(state, world);

        state.Cash -= (long)Math.Round(cost);
    }

    /// <summary>
    /// Every city's every good, in content order so the random sequence is stable. What
    /// a city makes today grades by its craft plus a roll, so the same works floor turns
    /// out a slightly different crate each day; a city with no craft vital declared
    /// grades at the catalogue's opening quality and draws nothing extra.
    /// </summary>
    private static void TickMarkets(GameState state, WorldData world, Rng rng)
    {
        var eco = world.Config.Economy;
        var quality = world.Quality;
        var craftId = quality.CityVitalId;
        var rolls = !string.IsNullOrWhiteSpace(craftId) && quality.Random > 0;

        foreach (var city in world.Cities)
        {
            if (!state.Stock.TryGetValue(city.Id, out var market)) continue;

            var craft = string.IsNullOrWhiteSpace(craftId) ? 50.0 : CityStats.Vital(state, world, city, craftId);

            foreach (var good in world.Goods)
            {
                if (!city.Market.TryGetValue(good.Id, out var profile)) continue;
                var grade = rolls
                    ? QualityMath.ProductionQuality(quality, craft, rng.NextDouble())
                    : QualityMath.OpeningQuality(quality, craft);
                market[good.Id] = Economy.TickStock(market[good.Id], profile, eco, rng, grade);
            }
        }
    }

    /// <summary>A contract past its deadline is torn up, and the houses remember.</summary>
    private static void LapseContracts(GameState state, WorldData world, List<GameEvent> events)
    {
        if (state.Contracts.Count == 0) return;
        var traders = world.Standing.SegmentOr("traders");

        for (var i = state.Contracts.Count - 1; i >= 0; i--)
        {
            var contract = state.Contracts[i];
            if (state.Day <= contract.Deadline) continue;

            state.Contracts.RemoveAt(i);
            state.ContractsClosed.Add(contract.Id);
            var lost = Standing.Grant(state, world.Standing, contract.CityId, traders, -world.Standing.ContractLapsePenalty);
            var city = world.CitiesById.TryGetValue(contract.CityId, out var c) ? c.Name : contract.CityId;
            var offer = Contracts.Resolve(world, state.Seed, contract.Id);
            var what = offer?.KindName ?? "contract";
            events.Add(new GameEvent(state.Day, GameEventKind.Standing,
                $"{what} for {city} lapsed. Traders standing {lost:+0.#;-0.#;0}."));
        }
    }

    private static void AdvanceTravel(GameState state, WorldData world, List<GameEvent> events)
    {
        if (state.Caravan.Travel is not { } travel) return;

        travel.DaysRemaining--;
        if (travel.DaysRemaining > 0) return;

        state.Caravan.Travel = null;
        state.Caravan.LocationId = null;
        state.Caravan.SiteId = null;
        state.Caravan.CellId = null;

        if (travel.ToKind == "city" && world.CitiesById.ContainsKey(travel.ToId))
        {
            state.Caravan.LocationId = travel.ToId;
            var city = world.City(travel.ToId);
            events.Add(new GameEvent(state.Day, GameEventKind.Arrival,
                $"Convoy arrived at {city.Name}, {city.Region}."));

            if (CrewBrief.Enabled(world))
            {
                var brief = CrewBrief.For(state, world);
                if (brief.Count > 0)
                {
                    events.Add(new GameEvent(state.Day, GameEventKind.Crew,
                        $"The crew read the market: {CrewBrief.Summary(brief, world.Config.CrewBrief.MinMargin)}"));
                }
            }
            return;
        }

        if (travel.ToKind == "site" && state.Site(travel.ToId) is { } site)
        {
            state.Caravan.SiteId = site.Id;
            var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g.Name : site.GoodId;
            events.Add(new GameEvent(state.Day, GameEventKind.Arrival,
                $"Convoy arrived at a {good} deposit."));
            return;
        }

        state.Caravan.CellId = travel.ToCellId;
        if (travel.ToKind != "cell")
        {
            events.Add(new GameEvent(state.Day, GameEventKind.Arrival,
                $"Convoy arrived in {travel.ToName}."));
        }
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
