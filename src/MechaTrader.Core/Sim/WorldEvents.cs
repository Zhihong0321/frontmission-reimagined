using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>
/// World events: what is happening to cities and markets, as opposed to what the
/// player did.
///
/// Templates are content. Live instances are a list on <see cref="GameState"/>. Price
/// multipliers and vital overlays are derived from that list, the same way rank is
/// derived from standing: storing either would give the game two answers to the same
/// question. A stock shock is the exception — it writes the shelf (or intake) once on
/// fire, because goods do not teleport back when the headline fades.
///
/// A template may name goods, whole categories, or nothing (every good). A shortage is
/// a template with reliefStanding: selling a covered good into the afflicted city while
/// it runs earns citizen standing, which the sell command reads through
/// <see cref="ReliefFor"/>.
///
/// Firing consumes the day's RNG. Reading the city page must never, which is why
/// every overlay is a pure function of (state, world).
/// </summary>
public static class WorldEvents
{
    /// <summary>
    /// One day of the event clock: drop what has expired, then maybe fire something
    /// new. Called from the day tick after the day counter advances, so a dispatch
    /// dated day N is news that broke that morning.
    /// </summary>
    public static void Tick(GameState state, WorldData world, Rng rng, List<GameEvent> events)
    {
        ExpireDue(state, world, events);
        TryFire(state, world, rng, events);
    }

    /// <summary>Drop instances whose last day has passed, and tell the player.</summary>
    public static void ExpireDue(GameState state, WorldData world, List<GameEvent> events)
    {
        for (var i = state.ActiveEvents.Count - 1; i >= 0; i--)
        {
            var evt = state.ActiveEvents[i];
            if (evt.EndDay > state.Day) continue;

            state.ActiveEvents.RemoveAt(i);
            events.Add(new GameEvent(state.Day, GameEventKind.World, EndedMessage(world, evt)));
        }
    }

    /// <summary>
    /// Put a known template on the board at a city (or globally). Applies the stock
    /// shock immediately. Does not draw the RNG — tests use this so they do not have
    /// to hunt a seed.
    /// </summary>
    public static ActiveEvent Start(GameState state, WorldData world, EventDef def, string cityId, int day)
    {
        var evt = new ActiveEvent
        {
            DefId = def.Id,
            CityId = def.Global ? "" : cityId,
            StartDay = day,
            EndDay = day + def.DurationDays
        };

        ApplyStockShock(state, world, def, evt);
        state.ActiveEvents.Add(evt);
        return evt;
    }

    /// <summary>
    /// Combined price multiplier for one city/good. 1 when nothing is running.
    /// Product of every matching active template, so two overlapping events stack.
    /// </summary>
    public static double PriceMultiplier(GameState state, WorldData world, string cityId, string goodId)
    {
        var mult = 1.0;

        foreach (var evt in state.ActiveEvents)
        {
            var def = world.Events.ById(evt.DefId);
            if (def is null || !def.TouchesPrice) continue;
            if (!Affects(world, evt, def, cityId, goodId)) continue;
            mult *= def.PriceMult;
        }

        return mult;
    }

    /// <summary>
    /// Citizen standing earned per unit of this good sold into this city right now:
    /// the sum over every running shortage that covers it. Zero when none does.
    /// </summary>
    public static double ReliefPerUnit(GameState state, WorldData world, string cityId, string goodId)
    {
        var perUnit = 0.0;
        foreach (var evt in state.ActiveEvents)
        {
            var def = world.Events.ById(evt.DefId);
            if (def is null || !def.IsShortage) continue;
            if (!Affects(world, evt, def, cityId, goodId)) continue;
            perUnit += def.ReliefStanding / Math.Max(1e-9, def.ReliefUnits);
        }
        return perUnit;
    }

    /// <summary>The running shortages that this good would relieve here, for the page.</summary>
    public static IEnumerable<EventDef> ReliefFor(GameState state, WorldData world, string cityId, string goodId)
    {
        foreach (var evt in state.ActiveEvents)
        {
            var def = world.Events.ById(evt.DefId);
            if (def is null || !def.IsShortage) continue;
            if (Affects(world, evt, def, cityId, goodId)) yield return def;
        }
    }

    /// <summary>
    /// Combined vital overlay for one city. 0 when nothing is running. The stored
    /// vital is untouched; <see cref="CityStats.Vital(GameState, WorldData, City, string)"/>
    /// adds this and clamps.
    /// </summary>
    public static double VitalDelta(GameState state, WorldData world, string cityId, string vitalId)
    {
        var delta = 0.0;

        foreach (var evt in state.ActiveEvents)
        {
            var def = world.Events.ById(evt.DefId);
            if (def is null || !def.TouchesVitals) continue;
            if (!AffectsCity(evt, def, cityId)) continue;
            if (def.VitalDeltas.TryGetValue(vitalId, out var amount))
                delta += amount;
        }

        return delta;
    }

    /// <summary>Ready-to-print reason a quoted price is not the city's resting one.</summary>
    public static string PriceHint(GameState state, WorldData world, string cityId, string goodId)
    {
        var parts = new List<string>();

        foreach (var evt in state.ActiveEvents)
        {
            var def = world.Events.ById(evt.DefId);
            if (def is null || !def.TouchesPrice) continue;
            if (!Affects(world, evt, def, cityId, goodId)) continue;

            var pct = (int)Math.Round((def.PriceMult - 1.0) * 100);
            var sign = pct > 0 ? "+" : "";
            parts.Add($"{def.Name} {sign}{pct}%");
        }

        return string.Join(" · ", parts);
    }

    public static string Format(string template, City? city, GoodDef? good, string? categoryName = null)
    {
        var text = template;
        if (city is not null)
        {
            text = text.Replace("{city}", city.Name)
                       .Replace("{region}", city.Region)
                       .Replace("{governor}", city.GovernorName);
        }
        if (good is not null)
            text = text.Replace("{good}", good.Name);
        if (categoryName is not null)
            text = text.Replace("{category}", categoryName.ToLowerInvariant());
        return text;
    }

    /// <summary>The first good a template names, for headline copy. Null when it names none.</summary>
    public static GoodDef? HeadlineGood(WorldData world, EventDef def)
    {
        if (def.Goods.Count > 0 && world.GoodsById.TryGetValue(def.Goods[0], out var good)) return good;
        return null;
    }

    /// <summary>The first category a template names, resolved to a display name. Null when it names none.</summary>
    public static string? HeadlineCategory(WorldData world, EventDef def)
        => def.Categories.Count > 0 ? world.CategoryName(def.Categories[0]) : null;

    public static IReadOnlyList<string> EventCityIds(GameState state)
    {
        var ids = new List<string>();
        foreach (var evt in state.ActiveEvents)
        {
            if (string.IsNullOrEmpty(evt.CityId)) continue;
            if (!ids.Contains(evt.CityId)) ids.Add(evt.CityId);
        }
        return ids;
    }

    private static void TryFire(GameState state, WorldData world, Rng rng, List<GameEvent> events)
    {
        var config = world.Events;
        if (config.Events.Count == 0) return;
        if (state.ActiveEvents.Count >= config.MaxConcurrent) return;
        if (rng.NextDouble() >= config.DailyChance) return;

        var eligible = new List<(EventDef Def, List<City> Cities)>();
        foreach (var def in config.Events)
        {
            if (def.Global)
            {
                if (AlreadyRunning(state, def, "")) continue;
                eligible.Add((def, new List<City>()));
                continue;
            }

            var cities = Candidates(state, def, world);
            if (cities.Count == 0) continue;
            eligible.Add((def, cities));
        }

        if (eligible.Count == 0) return;

        var picked = Pick(eligible, rng);
        var cityId = "";
        City? city = null;
        if (!picked.Def.Global && picked.Cities.Count > 0)
        {
            city = picked.Cities[rng.NextInt(picked.Cities.Count)];
            cityId = city.Id;
        }

        var evt = Start(state, world, picked.Def, cityId, state.Day);
        events.Add(new GameEvent(state.Day, GameEventKind.World,
            StartedMessage(world, picked.Def, city)));
    }

    private static bool AlreadyRunning(GameState state, EventDef def, string cityId)
    {
        foreach (var evt in state.ActiveEvents)
        {
            if (evt.DefId == def.Id && evt.CityId == cityId) return true;
        }
        return false;
    }

    private static List<City> Candidates(GameState state, EventDef def, WorldData world)
    {
        var list = new List<City>();
        foreach (var city in world.Cities)
        {
            if (AlreadyRunning(state, def, city.Id)) continue;
            if (def.Cities.Count > 0 && !Contains(def.Cities, city.Id)) continue;
            if (def.Regions.Count > 0 && !Contains(def.Regions, city.Region)) continue;
            if (def.Industries.Count > 0 && !Overlaps(def.Industries, city.Industries)) continue;
            list.Add(city);
        }
        return list;
    }

    private static (EventDef Def, List<City> Cities) Pick(
        IReadOnlyList<(EventDef Def, List<City> Cities)> eligible, Rng rng)
    {
        double total = 0;
        foreach (var entry in eligible) total += entry.Def.Weight;

        var roll = rng.NextDouble() * total;
        foreach (var entry in eligible)
        {
            roll -= entry.Def.Weight;
            if (roll < 0) return entry;
        }

        return eligible[^1];
    }

    private static bool Affects(WorldData world, ActiveEvent evt, EventDef def, string cityId, string goodId)
        => AffectsCity(evt, def, cityId) && AffectsGood(world, def, goodId);

    private static bool AffectsCity(ActiveEvent evt, EventDef def, string cityId)
        => def.Global || evt.CityId == cityId;

    /// <summary>A template with no goods and no categories covers every good.</summary>
    public static bool AffectsGood(WorldData world, EventDef def, string goodId)
    {
        if (!def.NamesGoods) return true;
        if (Contains(def.Goods, goodId)) return true;
        if (def.Categories.Count > 0 && world.GoodsById.TryGetValue(goodId, out var good))
            return Contains(def.Categories, good.Category);
        return false;
    }

    private static void ApplyStockShock(GameState state, WorldData world, EventDef def, ActiveEvent evt)
    {
        if (!def.TouchesStock) return;

        var minStock = world.Config.Economy.MinStock;

        foreach (var city in world.Cities)
        {
            if (!AffectsCity(evt, def, city.Id)) continue;

            foreach (var good in world.Goods)
            {
                if (!AffectsGood(world, def, good.Id)) continue;

                var stock = state.StockOf(city.Id, good.Id);
                if (def.ShockIntake)
                {
                    // A shock reprices at once: the news and the price break the same morning.
                    var intake = Math.Max(0.0, stock.In * def.StockMult + def.StockDelta);
                    state.SetStock(city.Id, good.Id, (stock with { In = intake }).Opened());
                }
                else
                {
                    var shelf = Math.Max(minStock, stock.Out * def.StockMult + def.StockDelta);
                    state.SetStock(city.Id, good.Id, (stock with { Out = shelf }).Opened());
                }
            }
        }
    }

    private static string StartedMessage(WorldData world, EventDef def, City? city)
    {
        var headline = Format(def.Headline, city, HeadlineGood(world, def), HeadlineCategory(world, def));
        return string.IsNullOrWhiteSpace(headline) ? def.Name : headline;
    }

    private static string EndedMessage(WorldData world, ActiveEvent evt)
    {
        var def = world.Events.ById(evt.DefId);
        var name = def?.Name ?? evt.DefId;

        if (string.IsNullOrEmpty(evt.CityId) || !world.CitiesById.TryGetValue(evt.CityId, out var city))
            return $"{name} has passed.";

        return $"{name} in {city.Name} has passed.";
    }

    private static bool Contains(IReadOnlyList<string> list, string value)
    {
        foreach (var item in list)
        {
            if (item == value) return true;
        }
        return false;
    }

    private static bool Overlaps(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        foreach (var item in a)
        {
            if (Contains(b, item)) return true;
        }
        return false;
    }
}
