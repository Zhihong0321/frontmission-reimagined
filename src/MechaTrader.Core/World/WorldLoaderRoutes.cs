using MechaTrader.Core.Model;

namespace MechaTrader.Core.World;

public static partial class WorldLoader
{
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
}
