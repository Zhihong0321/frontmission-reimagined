namespace MechaTrader.Core.View;
/// <summary>
/// The road network as drawable geometry. Static for a given world, so a front-end
/// fetches it once and only re-reads the per-turn view afterwards.
/// </summary>
public sealed record MapView(
    IReadOnlyList<MapCityView> Cities,
    IReadOnlyList<MapRoadView> Roads,
    int Width,
    int Height,
    double CellKm,
    double OriginX,
    double OriginY,
    string Biomes,
    string RoadsMask);

public sealed record MapCityView(string Id, string Name, string Region, double X, double Y);

public sealed record MapRoadView(string FromId, string ToId, string TerrainId, string TerrainName);

