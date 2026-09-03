using MechaTrader.Core.Model;

namespace MechaTrader.Core.World;

/// <summary>
/// All immutable game content, fully resolved and validated. Built once by
/// <see cref="WorldLoader"/> and shared by every system and front-end.
/// </summary>
public sealed class WorldData
{
    public required GameConfig Config { get; init; }
    public required IReadOnlyList<GoodDef> Goods { get; init; }
    public required IReadOnlyDictionary<string, GoodDef> GoodsById { get; init; }

    /// <summary>Knowledge domains goods belong to. Empty when content declares none.</summary>
    public required IReadOnlyList<CategoryDef> Categories { get; init; }
    public required IReadOnlyDictionary<string, CategoryDef> CategoriesById { get; init; }

    /// <summary>Product grades, ascending. Every good names one.</summary>
    public required IReadOnlyList<TierDef> Tiers { get; init; }
    public required IReadOnlyDictionary<int, TierDef> TiersById { get; init; }

    /// <summary>How a shop's pile is graded, and what S-tier is worth on the sell.</summary>
    public required QualityConfig Quality { get; init; }

    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyDictionary<string, City> CitiesById { get; init; }
    public required IReadOnlyList<TerrainDef> Terrain { get; init; }
    public required IReadOnlyList<TruckDef> Trucks { get; init; }
    public required IReadOnlyDictionary<string, TruckDef> TrucksById { get; init; }

    /// <summary>Fittings the station sells, per vehicle. Empty when content declares none.</summary>
    public required IReadOnlyList<TruckUpgradeDef> TruckUpgrades { get; init; }
    public required IReadOnlyDictionary<string, TruckUpgradeDef> TruckUpgradesById { get; init; }

    /// <summary>What the station pays back on a vehicle and its fittings, 0 to 1.</summary>
    public required double ResaleFraction { get; init; }

    public required IReadOnlyList<IndustryDef> Industries { get; init; }
    public required RouteGraph Routes { get; init; }
    public required CrewConfig Crew { get; init; }

    /// <summary>The catalogue of city stats: what vitals exist and how supply is read.</summary>
    public required CityStatsConfig CityStats { get; init; }

    /// <summary>How the player relates to a city: segments, ranks, permits, favor actions.</summary>
    public required StandingConfig Standing { get; init; }

    /// <summary>The catalogue of world events: what can fire, and what each one does.</summary>
    public required EventsConfig Events { get; init; }

    /// <summary>The contract board: what shapes a city can ask for.</summary>
    public required ContractsConfig Contracts { get; init; }

    /// <summary>Trade expos: cycle, fees, themes, buyer behaviour and what buyers say.</summary>
    public required ExposConfig Expos { get; init; }

    /// <summary>The painted terrain grid: biomes, layer flags, road overlay, city cells.</summary>
    public required WorldMap Map { get; init; }

    /// <summary>Portable tools. Bought in a city; occupy hold volume.</summary>
    public required IReadOnlyList<GearDef> Gear { get; init; }
    public required IReadOnlyDictionary<string, GearDef> GearById { get; init; }

    public GoodDef Good(string id) => GoodsById.TryGetValue(id, out var g)
        ? g : throw new KeyNotFoundException($"Unknown good '{id}'.");

    public City City(string id) => CitiesById.TryGetValue(id, out var c)
        ? c : throw new KeyNotFoundException($"Unknown city '{id}'.");

    public TruckDef Truck(string id) => TrucksById.TryGetValue(id, out var t)
        ? t : throw new KeyNotFoundException($"Unknown truck type '{id}'.");

    public GearDef GearItem(string id) => GearById.TryGetValue(id, out var g)
        ? g : throw new KeyNotFoundException($"Unknown gear '{id}'.");

    /// <summary>The tier a good belongs to, or a plain tier 1 when content declared none.</summary>
    public TierDef TierOf(GoodDef good)
        => TiersById.TryGetValue(good.Tier, out var t) ? t : new TierDef { Tier = good.Tier, Name = $"Tier {good.Tier}" };

    public string CategoryName(string categoryId)
        => CategoriesById.TryGetValue(categoryId, out var c) ? c.Name : categoryId;
}

public sealed class WorldLoadException : Exception
{
    public WorldLoadException(string message) : base(message) { }
}
