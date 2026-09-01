using System.Text.Json;
using MechaTrader.Core.Model;

namespace MechaTrader.Core.World;

/// <summary>
/// Turns raw JSON content into a validated <see cref="WorldData"/>.
///
/// Deliberately takes already-read strings rather than file paths: the core must not
/// depend on a filesystem, so the same loader serves the ASP.NET host (reads from disk),
/// Godot (reads from res://) and the tests (in-memory literals).
/// </summary>
public static class WorldLoader
{
    public const string ConfigKey = "config";
    public const string GoodsKey = "goods";
    public const string TerrainKey = "terrain";
    public const string TrucksKey = "trucks";
    public const string IndustriesKey = "industries";
    public const string CitiesKey = "cities";
    public const string RoutesKey = "routes";
    public const string CrewKey = "crew";

    public static readonly IReadOnlyList<string> RequiredKeys = new[]
    {
        ConfigKey, GoodsKey, TerrainKey, TrucksKey, IndustriesKey, CitiesKey, RoutesKey, CrewKey
    };

    private const double KmPerDegreeLat = 111.32;
    private const double ReferenceLat = 47.5; // central Europe; keeps the projection honest

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static WorldData Load(IReadOnlyDictionary<string, string> files)
    {
        foreach (var key in RequiredKeys)
        {
            if (!files.ContainsKey(key))
                throw new WorldLoadException($"Missing content file '{key}'.");
        }

        var config = Parse<GameConfig>(files[ConfigKey], ConfigKey);
        var goods = Parse<GoodsFile>(files[GoodsKey], GoodsKey).Goods;
        var terrain = Parse<TerrainFile>(files[TerrainKey], TerrainKey).Terrain;
        var trucks = Parse<TrucksFile>(files[TrucksKey], TrucksKey).Trucks;
        var industryFile = Parse<IndustriesFile>(files[IndustriesKey], IndustriesKey);
        var cityDtos = Parse<CitiesFile>(files[CitiesKey], CitiesKey).Cities;
        var routeDtos = Parse<RoutesFile>(files[RoutesKey], RoutesKey).Routes;
        var crew = Parse<CrewConfig>(files[CrewKey], CrewKey);

        if (goods.Count == 0) throw new WorldLoadException("goods.json defines no goods.");
        if (cityDtos.Count == 0) throw new WorldLoadException("cities.json defines no cities.");

        var goodsById = ToLookup(goods, g => g.Id, "good");
        var terrainById = ToLookup(terrain, t => t.Id, "terrain");
        var trucksById = ToLookup(trucks, t => t.Id, "truck");
        var industriesById = ToLookup(industryFile.Industries, i => i.Id, "industry");

        ValidateIndustryGoods(industryFile, goodsById);
        ValidateCrew(crew, industriesById);

        var cities = new List<City>(cityDtos.Count);
        foreach (var dto in cityDtos)
        {
            cities.Add(BuildCity(dto, goods, industriesById, industryFile.BaseConsumptionPerPop, config.Economy));
        }

        var citiesById = ToLookup(cities, c => c.Id, "city");

        var routes = new List<Route>(routeDtos.Count);
        foreach (var dto in routeDtos)
        {
            routes.Add(BuildRoute(dto, citiesById, terrainById, config.Economy));
        }

        var graph = new RouteGraph(routes);

        ValidateWorld(config, cities, citiesById, trucksById, graph);

        return new WorldData
        {
            Config = config,
            Goods = goods,
            GoodsById = goodsById,
            Cities = cities,
            CitiesById = citiesById,
            Terrain = terrain,
            Trucks = trucks,
            TrucksById = trucksById,
            Industries = industryFile.Industries,
            Routes = graph,
            Crew = crew
        };
    }

    private static City BuildCity(
        CityDto dto,
        IReadOnlyList<GoodDef> goods,
        IReadOnlyDictionary<string, IndustryDef> industriesById,
        IReadOnlyDictionary<string, double> baseConsumption,
        EconomyConfig eco)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
            throw new WorldLoadException("A city is missing its id.");
        if (dto.Population <= 0)
            throw new WorldLoadException($"City '{dto.Id}' must have a positive population.");

        var market = new Dictionary<string, CityGoodProfile>(goods.Count);

        foreach (var good in goods)
        {
            double production = 0;
            double consumption = 0;

            foreach (var industryId in dto.Industries)
            {
                if (!industriesById.TryGetValue(industryId, out var industry))
                    throw new WorldLoadException($"City '{dto.Id}' references unknown industry '{industryId}'.");

                if (industry.Production.TryGetValue(good.Id, out var p)) production += p * dto.Population;
                if (industry.Consumption.TryGetValue(good.Id, out var c)) consumption += c * dto.Population;
            }

            if (baseConsumption.TryGetValue(good.Id, out var basePer))
                consumption += basePer * dto.Population;

            var equilibrium = Math.Max(eco.MinEquilibrium, eco.EquilibriumDays * (production + consumption));

            market[good.Id] = new CityGoodProfile
            {
                GoodId = good.Id,
                Production = production,
                Consumption = consumption,
                Equilibrium = equilibrium
            };
        }

        var kmPerDegreeLon = KmPerDegreeLat * Math.Cos(ReferenceLat * Math.PI / 180.0);

        return new City
        {
            Id = dto.Id,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? dto.Id : dto.Name,
            Region = dto.Region,
            Lon = dto.Lon,
            Lat = dto.Lat,
            X = dto.Lon * kmPerDegreeLon,
            Y = -dto.Lat * KmPerDegreeLat,
            Population = dto.Population,
            Industries = dto.Industries,
            Market = market
        };
    }

    private static Route BuildRoute(
        RouteDto dto,
        IReadOnlyDictionary<string, City> citiesById,
        IReadOnlyDictionary<string, TerrainDef> terrainById,
        EconomyConfig eco)
    {
        if (!citiesById.TryGetValue(dto.From, out var from))
            throw new WorldLoadException($"Route references unknown city '{dto.From}'.");
        if (!citiesById.TryGetValue(dto.To, out var to))
            throw new WorldLoadException($"Route references unknown city '{dto.To}'.");
        if (dto.From == dto.To)
            throw new WorldLoadException($"Route from '{dto.From}' loops back to itself.");
        if (!terrainById.TryGetValue(dto.Terrain, out var terrain))
            throw new WorldLoadException($"Route {dto.From} to {dto.To} uses unknown terrain '{dto.Terrain}'.");

        var distance = dto.DistanceKm ?? StraightLineKm(from, to) * eco.RoadDetourFactor;
        if (distance <= 0)
            throw new WorldLoadException($"Route {dto.From} to {dto.To} has non-positive distance.");

        return new Route
        {
            FromId = dto.From,
            ToId = dto.To,
            Terrain = terrain,
            DistanceKm = Math.Round(distance)
        };
    }

    private static double StraightLineKm(City a, City b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static void ValidateIndustryGoods(IndustriesFile file, IReadOnlyDictionary<string, GoodDef> goodsById)
    {
        foreach (var industry in file.Industries)
        {
            foreach (var id in industry.Production.Keys)
            {
                if (!goodsById.ContainsKey(id))
                    throw new WorldLoadException($"Industry '{industry.Id}' produces unknown good '{id}'.");
            }

            foreach (var id in industry.Consumption.Keys)
            {
                if (!goodsById.ContainsKey(id))
                    throw new WorldLoadException($"Industry '{industry.Id}' consumes unknown good '{id}'.");
            }
        }

        foreach (var id in file.BaseConsumptionPerPop.Keys)
        {
            if (!goodsById.ContainsKey(id))
                throw new WorldLoadException($"baseConsumptionPerPop references unknown good '{id}'.");
        }
    }

    /// <summary>
    /// Crew content is wired to the simulation by lever, not by skill id, so the ids
    /// themselves are free to change. What must hold is that every lever is claimed at
    /// most once and that nothing references a skill or industry that does not exist -
    /// a typo there would otherwise show up as a stat that silently does nothing.
    /// </summary>
    private static void ValidateCrew(CrewConfig crew, IReadOnlyDictionary<string, IndustryDef> industriesById)
    {
        if (crew.MaxSkill <= 0)
            throw new WorldLoadException("crew.maxSkill must be positive.");
        if (crew.CrewCapacity < 0)
            throw new WorldLoadException("crew.crewCapacity cannot be negative.");
        if (crew.RefreshDays < 1)
            throw new WorldLoadException("crew.refreshDays must be at least 1.");
        if (crew.Skills.Count == 0)
            throw new WorldLoadException("crew.json defines no skills.");
        if (crew.Roles.Count == 0)
            throw new WorldLoadException("crew.json defines no roles.");

        var skillsById = ToLookup(crew.Skills, s => s.Id, "crew skill");
        var claimed = new Dictionary<string, string>();

        foreach (var skill in crew.Skills)
        {
            if (!CrewLever.All.Contains(skill.Lever))
            {
                throw new WorldLoadException(
                    $"Crew skill '{skill.Id}' declares unknown lever '{skill.Lever}'; " +
                    $"expected one of {string.Join(", ", CrewLever.All)}.");
            }

            if (skill.Lever == CrewLever.None) continue;

            if (claimed.TryGetValue(skill.Lever, out var owner))
            {
                throw new WorldLoadException(
                    $"Crew skills '{owner}' and '{skill.Id}' both claim the '{skill.Lever}' lever.");
            }

            claimed[skill.Lever] = skill.Id;
        }

        ToLookup(crew.Roles, r => r.Id, "crew role");

        foreach (var role in crew.Roles)
        {
            if (!string.IsNullOrWhiteSpace(role.Primary) && !skillsById.ContainsKey(role.Primary))
                throw new WorldLoadException($"Crew role '{role.Id}' specialises in unknown skill '{role.Primary}'.");
        }

        foreach (var (industryId, bonuses) in crew.IndustryAffinity)
        {
            if (!industriesById.ContainsKey(industryId))
                throw new WorldLoadException($"crew.industryAffinity references unknown industry '{industryId}'.");

            foreach (var skillId in bonuses.Keys)
            {
                if (!skillsById.ContainsKey(skillId))
                {
                    throw new WorldLoadException(
                        $"crew.industryAffinity['{industryId}'] references unknown skill '{skillId}'.");
                }
            }
        }
    }

    private static void ValidateWorld(
        GameConfig config,
        IReadOnlyList<City> cities,
        IReadOnlyDictionary<string, City> citiesById,
        IReadOnlyDictionary<string, TruckDef> trucksById,
        RouteGraph graph)
    {
        if (!citiesById.ContainsKey(config.StartCityId))
            throw new WorldLoadException($"config.startCityId '{config.StartCityId}' is not a known city.");

        if (config.StartTruckIds.Count == 0)
            throw new WorldLoadException("config.startTruckIds must list at least one truck.");

        foreach (var id in config.StartTruckIds)
        {
            if (!trucksById.ContainsKey(id))
                throw new WorldLoadException($"config.startTruckIds references unknown truck '{id}'.");
        }

        // Every city must be reachable, or the player can be stranded or locked out of content.
        var reachable = graph.Reachable(config.StartCityId);
        var orphans = cities.Where(c => !reachable.Contains(c.Id)).Select(c => c.Id).ToList();
        if (orphans.Count > 0)
        {
            throw new WorldLoadException(
                $"Cities unreachable from '{config.StartCityId}': {string.Join(", ", orphans)}.");
        }
    }

    private static Dictionary<string, T> ToLookup<T>(IEnumerable<T> items, Func<T, string> keySelector, string label)
    {
        var map = new Dictionary<string, T>();
        foreach (var item in items)
        {
            var key = keySelector(item);
            if (string.IsNullOrWhiteSpace(key))
                throw new WorldLoadException($"A {label} entry is missing its id.");
            if (!map.TryAdd(key, item))
                throw new WorldLoadException($"Duplicate {label} id '{key}'.");
        }
        return map;
    }

    private static T Parse<T>(string json, string label)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new WorldLoadException($"Content file '{label}' parsed to null.");
        }
        catch (JsonException ex)
        {
            throw new WorldLoadException($"Content file '{label}' is not valid JSON: {ex.Message}");
        }
    }

    private sealed class GoodsFile { public List<GoodDef> Goods { get; init; } = new(); }
    private sealed class TerrainFile { public List<TerrainDef> Terrain { get; init; } = new(); }
    private sealed class TrucksFile { public List<TruckDef> Trucks { get; init; } = new(); }

    private sealed class IndustriesFile
    {
        public List<IndustryDef> Industries { get; init; } = new();
        public Dictionary<string, double> BaseConsumptionPerPop { get; init; } = new();
    }

    private sealed class CitiesFile { public List<CityDto> Cities { get; init; } = new(); }
    private sealed class RoutesFile { public List<RouteDto> Routes { get; init; } = new(); }

    private sealed class CityDto
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Region { get; init; } = "";
        public double Lon { get; init; }
        public double Lat { get; init; }
        public double Population { get; init; } = 1.0;
        public List<string> Industries { get; init; } = new();
    }

    private sealed class RouteDto
    {
        public string From { get; init; } = "";
        public string To { get; init; } = "";
        public string Terrain { get; init; } = "plain";
        public double? DistanceKm { get; init; }
    }
}
