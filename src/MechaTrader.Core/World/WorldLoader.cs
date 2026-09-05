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
public static partial class WorldLoader
{
    public const string ConfigKey = "config";
    public const string GoodsKey = "goods";
    public const string TerrainKey = "terrain";
    public const string TrucksKey = "trucks";
    public const string IndustriesKey = "industries";
    public const string CitiesKey = "cities";
    public const string RoutesKey = "routes";
    public const string CrewKey = "crew";
    public const string CityStatsKey = "citystats";
    public const string StandingKey = "standing";
    public const string EventsKey = "events";
    public const string MapKey = "map";
    public const string GearKey = "gear";
    public const string ContractsKey = "contracts";
    public const string ExposKey = "expos";

    public static readonly IReadOnlyList<string> RequiredKeys = new[]
    {
        ConfigKey, GoodsKey, TerrainKey, TrucksKey, IndustriesKey, CitiesKey, RoutesKey, CrewKey,
        CityStatsKey, StandingKey, EventsKey, MapKey, GearKey, ContractsKey, ExposKey
    };

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
        var goodsFile = Parse<GoodsFile>(files[GoodsKey], GoodsKey);
        var goods = goodsFile.Goods;
        var quality = goodsFile.Quality ?? new QualityConfig();
        var terrain = Parse<TerrainFile>(files[TerrainKey], TerrainKey).Terrain;
        var trucksFile = Parse<TrucksFile>(files[TrucksKey], TrucksKey);
        var trucks = trucksFile.Trucks;
        var industryFile = Parse<IndustriesFile>(files[IndustriesKey], IndustriesKey);
        var cityDtos = Parse<CitiesFile>(files[CitiesKey], CitiesKey).Cities;
        var routeDtos = Parse<RoutesFile>(files[RoutesKey], RoutesKey).Routes;
        var crew = Parse<CrewConfig>(files[CrewKey], CrewKey);
        var cityStats = Parse<CityStatsConfig>(files[CityStatsKey], CityStatsKey);
        var standing = Parse<StandingConfig>(files[StandingKey], StandingKey);
        var events = Parse<EventsConfig>(files[EventsKey], EventsKey);
        var mapFile = Parse<MapFile>(files[MapKey], MapKey);
        var gear = Parse<GearFile>(files[GearKey], GearKey).Gear;
        var contracts = Parse<ContractsConfig>(files[ContractsKey], ContractsKey);
        var expos = Parse<ExposConfig>(files[ExposKey], ExposKey);

        if (goods.Count == 0) throw new WorldLoadException("goods.json defines no goods.");
        if (cityDtos.Count == 0) throw new WorldLoadException("cities.json defines no cities.");

        var goodsById = ToLookup(goods, g => g.Id, "good");
        var categories = ResolveCategories(goodsFile.Categories, goods);
        var categoriesById = categories.Count == 0
            ? new Dictionary<string, CategoryDef>()
            : ToLookup(categories, c => c.Id, "category");
        ValidateGoods(goods, categoriesById);
        var tiers = ResolveTiers(goodsFile.Tiers, goods);
        var tiersById = ValidateTiers(tiers, goods);
        ValidateQuality(quality);

        var terrainById = ToLookup(terrain, t => t.Id, "terrain");
        var trucksById = ToLookup(trucks, t => t.Id, "truck");
        var industriesById = ToLookup(industryFile.Industries, i => i.Id, "industry");

        ValidateIndustryGoods(industryFile, goodsById);
        ValidateCrew(crew, industriesById, categoriesById);
        ValidateCityStats(cityStats, goodsById);
        ValidateStanding(standing, cityStats);
        var gearById = ValidateGear(gear);

        var cities = new List<City>(cityDtos.Count);
        foreach (var dto in cityDtos)
        {
            cities.Add(BuildCity(
                dto, goods, tiersById, industriesById, industryFile.BaseConsumptionPerPop, config.Economy, cityStats, crew));
        }

        var citiesById = ToLookup(cities, c => c.Id, "city");

        var routes = new List<Route>(routeDtos.Count);
        foreach (var dto in routeDtos)
        {
            routes.Add(BuildRoute(dto, citiesById, terrainById, config.Economy));
        }

        var graph = new RouteGraph(routes);

        ValidateWorld(config, cities, citiesById, trucksById, graph);
        ValidateEvents(events, goodsById, categoriesById, industriesById, citiesById, cityStats);
        ValidateTrucks(trucks);
        var upgradesById = ValidateUpgrades(trucksFile);
        ValidateContracts(contracts, tiersById);
        ValidateExpos(expos, categoriesById);
        ValidateQualityVital(quality, cityStats);

        var map = MapPainter.Paint(mapFile, cities, routes, citiesById);
        ValidateMap(map, goodsById);

        return new WorldData
        {
            Config = config,
            Goods = goods,
            GoodsById = goodsById,
            Categories = categories,
            CategoriesById = categoriesById,
            Tiers = tiers,
            TiersById = tiersById,
            Quality = quality,
            Cities = cities,
            CitiesById = citiesById,
            Terrain = terrain,
            Trucks = trucks,
            TrucksById = trucksById,
            TruckUpgrades = trucksFile.Upgrades,
            TruckUpgradesById = upgradesById,
            ResaleFraction = trucksFile.ResaleFraction,
            Industries = industryFile.Industries,
            Routes = graph,
            Crew = crew,
            CityStats = cityStats,
            Standing = standing,
            Events = events,
            Contracts = contracts,
            Expos = expos,
            Map = map,
            Gear = gear,
            GearById = gearById
        };
    }
}
