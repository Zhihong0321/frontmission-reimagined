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
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyDictionary<string, City> CitiesById { get; init; }
    public required IReadOnlyList<TerrainDef> Terrain { get; init; }
    public required IReadOnlyList<TruckDef> Trucks { get; init; }
    public required IReadOnlyDictionary<string, TruckDef> TrucksById { get; init; }
    public required IReadOnlyList<IndustryDef> Industries { get; init; }
    public required RouteGraph Routes { get; init; }

    public GoodDef Good(string id) => GoodsById.TryGetValue(id, out var g)
        ? g : throw new KeyNotFoundException($"Unknown good '{id}'.");

    public City City(string id) => CitiesById.TryGetValue(id, out var c)
        ? c : throw new KeyNotFoundException($"Unknown city '{id}'.");

    public TruckDef Truck(string id) => TrucksById.TryGetValue(id, out var t)
        ? t : throw new KeyNotFoundException($"Unknown truck type '{id}'.");
}

public sealed class WorldLoadException : Exception
{
    public WorldLoadException(string message) : base(message) { }
}
