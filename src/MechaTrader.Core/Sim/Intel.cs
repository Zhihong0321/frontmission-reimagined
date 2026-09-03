using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

/// <summary>A city within reach of the informant, and how far it is by road.</summary>
public sealed record NearbyCity(City City, double DistanceKm, int Days);

/// <summary>
/// What the information post says one good fetches in one nearby city. The figures are
/// the informant's, not the market's: they are off by up to <see cref="Error"/> either
/// way, and the true price is never on this record.
/// </summary>
public sealed record PriceReport(
    City City,
    double DistanceKm,
    int Days,
    double Buy,
    double Sell,
    string Flow,
    double Error);

/// <summary>
/// The information post: price reports from the nearest markets.
///
/// Reports are derived, never stored. Reach and error are reads of the best hand on
/// the post; the noise on each figure is a hash of (seed, city, good, day), so the
/// report is stable for a day, differs between goods and cities, and never touches
/// <see cref="GameState.RngState"/>. Building the page cannot advance the world.
/// </summary>
public static class Intel
{
    /// <summary>Best intelligence among the hands on the information post; 0 if nobody is.</summary>
    public static int Level(IReadOnlyList<CrewMember> roster, CrewConfig cfg)
    {
        var skill = cfg.SkillFor(CrewLever.Intel);
        return skill is null ? 0 : CrewMath.Level(CrewMath.OnPost(roster, cfg, CrewLever.Intel), skill.Id);
    }

    /// <summary>The hand whose intelligence the reports rest on, or null.</summary>
    public static CrewMember? Informant(IReadOnlyList<CrewMember> roster, CrewConfig cfg)
    {
        var skill = cfg.SkillFor(CrewLever.Intel);
        return skill is null ? null : CrewMath.Leader(roster, cfg, skill.Id);
    }

    /// <summary>
    /// How many markets the post reads. Zero with nobody on it; otherwise minCities
    /// rising to maxCities with intelligence.
    /// </summary>
    public static int Reach(IReadOnlyList<CrewMember> roster, CrewConfig cfg)
    {
        if (Level(roster, cfg) <= 0) return 0;

        var min = Math.Max(0, cfg.Intel.MinCities);
        var max = Math.Max(min, cfg.Intel.MaxCities);
        return min + (int)Math.Round((max - min) * CrewMath.Factor(roster, cfg, CrewLever.Intel));
    }

    /// <summary>
    /// Worst-case relative error on a reported price: maxError at level zero, scaled
    /// down by intelligence to nothing at max skill.
    /// </summary>
    public static double Error(IReadOnlyList<CrewMember> roster, CrewConfig cfg)
        => Math.Clamp(cfg.Intel.MaxError, 0.0, 1.0)
           * (1.0 - CrewMath.Effect(roster, cfg, CrewLever.Intel) / Math.Max(1e-9, cfg.SkillFor(CrewLever.Intel)?.MaxEffect ?? 1.0));

    /// <summary>
    /// The nearest cities by road, closest first. Distance is the shortest road path;
    /// days are the sum of what the convoy would spend on each leg of it.
    /// </summary>
    public static IReadOnlyList<NearbyCity> Nearby(WorldData world, CaravanState caravan, string fromCityId, int count)
    {
        if (count <= 0) return Array.Empty<NearbyCity>();

        var dist = new Dictionary<string, double> { [fromCityId] = 0.0 };
        var days = new Dictionary<string, int> { [fromCityId] = 0 };
        var done = new HashSet<string>();

        while (true)
        {
            string? current = null;
            var best = double.MaxValue;
            foreach (var (id, d) in dist)
            {
                if (!done.Contains(id) && d < best) { best = d; current = id; }
            }
            if (current is null) break;
            done.Add(current);

            foreach (var route in world.Routes.From(current))
            {
                var next = route.Other(current);
                var candidate = best + route.DistanceKm;
                if (!dist.TryGetValue(next, out var known) || candidate < known)
                {
                    dist[next] = candidate;
                    days[next] = days[current] + Math.Max(1, CaravanMath.TravelDays(caravan, world, route));
                }
            }
        }

        return dist
            .Where(kv => kv.Key != fromCityId && world.CitiesById.ContainsKey(kv.Key))
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(count)
            .Select(kv => new NearbyCity(world.City(kv.Key), kv.Value, days[kv.Key]))
            .ToList();
    }

    /// <summary>The cities the post currently reads from the convoy's city.</summary>
    public static IReadOnlyList<NearbyCity> Coverage(GameState state, WorldData world, string fromCityId)
        => Nearby(world, state.Caravan, fromCityId, Reach(state.Caravan.Crew, world.Crew));

    /// <summary>
    /// What the informant reports one good fetches in each covered city, at the terms
    /// the convoy would actually trade on there. Empty when nobody is on the post.
    /// </summary>
    public static IReadOnlyList<PriceReport> Reports(
        GameState state, WorldData world, IReadOnlyList<NearbyCity> coverage, GoodDef good)
    {
        if (coverage.Count == 0) return Array.Empty<PriceReport>();

        var cfg = world.Crew;
        var eco = world.Config.Economy;
        var error = Error(state.Caravan.Crew, cfg);
        var terms = CrewMath.Terms(state.Caravan, world, good.Category);

        var rows = new List<PriceReport>(coverage.Count);
        foreach (var near in coverage)
        {
            var city = near.City;
            var profile = city.Market[good.Id];
            var stock = state.StockOf(city.Id, good.Id);
            var eventMult = WorldEvents.PriceMultiplier(state, world, city.Id, good.Id);

            var buy = Economy.BuyUnitPrice(good, profile, stock, eco, terms, eventMult);
            var sell = Economy.SellUnitPrice(good, profile, stock, eco, terms, eventMult);

            // Two independent draws, so a report can be high on one side and low on the
            // other: an informant who is wrong is not wrong in a tidy direction.
            var buyNoise = Noise(state.Seed, city.Id, good.Id, state.Day, 1);
            var sellNoise = Noise(state.Seed, city.Id, good.Id, state.Day, 2);

            var net = profile.Production - profile.Consumption;
            var flow = net > 0.5 ? "surplus" : net < -0.5 ? "deficit" : "balanced";

            rows.Add(new PriceReport(
                City: city,
                DistanceKm: near.DistanceKm,
                Days: near.Days,
                Buy: Math.Max(0.0, buy * (1.0 + error * buyNoise)),
                Sell: Math.Max(0.0, sell * (1.0 + error * sellNoise)),
                Flow: flow,
                Error: error));
        }
        return rows;
    }

    /// <summary>
    /// Deterministic noise in [-1, 1]. FNV-1a over the city and good ids, folded with
    /// the seed, the day and a side, so two runs of one save print the same report and
    /// tomorrow's differs from today's.
    /// </summary>
    public static double Noise(ulong seed, string cityId, string goodId, int day, int side)
    {
        var hash = 0xCBF29CE484222325UL;
        foreach (var c in cityId) { hash ^= c; hash *= 0x100000001B3UL; }
        hash ^= 0x7C; hash *= 0x100000001B3UL;
        foreach (var c in goodId) { hash ^= c; hash *= 0x100000001B3UL; }

        hash ^= seed + 0x9E3779B97F4A7C15UL;
        hash *= 0x100000001B3UL;
        hash ^= (ulong)day * 0xD1B54A32D192ED03UL;
        hash *= 0x100000001B3UL;
        hash ^= (ulong)side * 0xA24BAED4963EE407UL;
        hash *= 0x100000001B3UL;

        // Mix the high bits down before taking a range, or nearby ids would land close.
        hash ^= hash >> 29;
        hash *= 0xBF58476D1CE4E5B9UL;
        hash ^= hash >> 32;

        return (hash % 20001UL) / 10000.0 - 1.0;
    }
}
