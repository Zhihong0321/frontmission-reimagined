using MechaTrader.Core.Model;

namespace MechaTrader.Core.World;

/// <summary>
/// Paints <c>map.json</c> regions onto a cell grid, snaps cities, and stamps authored
/// roads as a walkable overlay. Pure: takes already-parsed content, no I/O.
/// </summary>
public static class MapPainter
{
    public static WorldMap Paint(
        MapFile file,
        IReadOnlyList<City> cities,
        IReadOnlyList<Route> routes,
        IReadOnlyDictionary<string, City> citiesById)
    {
        if (file.Width < 2 || file.Height < 2)
            throw new WorldLoadException("map.json width and height must be at least 2.");
        if (file.CellKm <= 0)
            throw new WorldLoadException("map.json cellKm must be positive.");
        if (!MapBiome.All.Contains(file.DefaultBiome))
            throw new WorldLoadException($"map.json defaultBiome '{file.DefaultBiome}' is not a known biome.");

        var originX = MapProjection.X(file.OriginLon);
        var originY = MapProjection.Y(file.OriginLat);
        var count = file.Width * file.Height;
        var biomes = new string[count];
        Array.Fill(biomes, file.DefaultBiome);

        foreach (var region in file.Regions)
        {
            if (!MapBiome.All.Contains(region.Biome))
                throw new WorldLoadException($"map.json region uses unknown biome '{region.Biome}'.");

            foreach (var ring in region.Rings)
            {
                if (ring.Count < 3) continue;
                var poly = new List<(double x, double y)>(ring.Count);
                foreach (var point in ring)
                {
                    if (point.Length < 2)
                        throw new WorldLoadException("map.json region ring point needs lon and lat.");
                    poly.Add((MapProjection.X(point[0]), MapProjection.Y(point[1])));
                }

                for (var row = 0; row < file.Height; row++)
                {
                    for (var col = 0; col < file.Width; col++)
                    {
                        var (cx, cy) = CellCenter(originX, originY, file.CellKm, col, row);
                        if (Contains(poly, cx, cy))
                            biomes[row * file.Width + col] = region.Biome;
                    }
                }
            }
        }

        var roads = new TerrainDef?[count];
        foreach (var route in routes)
        {
            var from = citiesById[route.FromId];
            var to = citiesById[route.ToId];
            var a = CellOf(originX, originY, file.CellKm, file.Width, file.Height, from.X, from.Y);
            var b = CellOf(originX, originY, file.CellKm, file.Width, file.Height, to.X, to.Y);
            foreach (var (col, row) in Line(a.col, a.row, b.col, b.row))
            {
                if (col < 0 || row < 0 || col >= file.Width || row >= file.Height) continue;
                var i = row * file.Width + col;
                var current = roads[i];
                if (current is null || route.Terrain.SpeedMultiplier < current.SpeedMultiplier)
                    roads[i] = route.Terrain;
            }
        }

        var cityCells = new Dictionary<string, TerrainCell>();
        var cells = new TerrainCell[count];
        for (var row = 0; row < file.Height; row++)
        {
            for (var col = 0; col < file.Width; col++)
            {
                var i = row * file.Width + col;
                var (cx, cy) = CellCenter(originX, originY, file.CellKm, col, row);
                cells[i] = new TerrainCell
                {
                    Col = col,
                    Row = row,
                    Biome = biomes[i],
                    X = cx,
                    Y = cy,
                    Road = roads[i]
                };
            }
        }

        var map = new WorldMap
        {
            CellKm = file.CellKm,
            OriginX = originX,
            OriginY = originY,
            Width = file.Width,
            Height = file.Height,
            Cells = cells,
            Mining = file.Mining ?? new MiningMapConfig(),
            OffRoad = file.OffRoad.Count > 0
                ? file.OffRoad
                : new Dictionary<string, OffRoadRates>
                {
                    [MapBiome.Plain] = new() { SpeedMultiplier = 0.85, CostMultiplier = 1.15 },
                    [MapBiome.Hill] = new() { SpeedMultiplier = 0.65, CostMultiplier = 1.35 },
                    [MapBiome.Mountain] = new() { SpeedMultiplier = 0.40, CostMultiplier = 1.90 }
                },
            CityCells = cityCells
        };

        foreach (var city in cities)
        {
            var snapped = map.At(city.X, city.Y);
            // A city that painted onto water or mountain is still a city: force land
            // so the start is never stranded. Roads already do this for coastal ports.
            if (!snapped.Land)
            {
                var i = map.Index(snapped.Col, snapped.Row);
                snapped = new TerrainCell
                {
                    Col = snapped.Col,
                    Row = snapped.Row,
                    Biome = MapBiome.Plain,
                    X = snapped.X,
                    Y = snapped.Y,
                    Road = snapped.Road ?? DummyOpenRoad
                };
                cells[i] = snapped;
            }

            cityCells[city.Id] = snapped;
        }

        return map;
    }

    private static readonly TerrainDef DummyOpenRoad = new()
    {
        Id = "plain",
        Name = "Open Road",
        SpeedMultiplier = 1.0,
        CostMultiplier = 1.0
    };

    private static (double x, double y) CellCenter(double originX, double originY, double cellKm, int col, int row)
        => (originX + (col + 0.5) * cellKm, originY + (row + 0.5) * cellKm);

    private static (int col, int row) CellOf(
        double originX, double originY, double cellKm, int width, int height, double x, double y)
    {
        var col = (int)Math.Floor((x - originX) / cellKm);
        var row = (int)Math.Floor((y - originY) / cellKm);
        return (Math.Clamp(col, 0, width - 1), Math.Clamp(row, 0, height - 1));
    }

    private static IEnumerable<(int col, int row)> Line(int c0, int r0, int c1, int r1)
    {
        var dx = Math.Abs(c1 - c0);
        var sx = c0 < c1 ? 1 : -1;
        var dy = -Math.Abs(r1 - r0);
        var sy = r0 < r1 ? 1 : -1;
        var err = dx + dy;
        var c = c0;
        var r = r0;
        while (true)
        {
            yield return (c, r);
            if (c == c1 && r == r1) yield break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; c += sx; }
            if (e2 <= dx) { err += dx; r += sy; }
        }
    }

    private static bool Contains(IReadOnlyList<(double x, double y)> ring, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var (xi, yi) = ring[i];
            var (xj, yj) = ring[j];
            if ((yi > y) != (yj > y) &&
                x < (xj - xi) * (y - yi) / ((yj - yi) + 1e-12) + xi)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}

public sealed class MapFile
{
    public double CellKm { get; init; } = 50;
    public double OriginLon { get; init; }
    public double OriginLat { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string DefaultBiome { get; init; } = MapBiome.Plain;
    public MiningMapConfig Mining { get; init; } = new();
    public Dictionary<string, OffRoadRates> OffRoad { get; init; } = new();
    public List<MapRegionDto> Regions { get; init; } = new();
}

public sealed class MapRegionDto
{
    public string Biome { get; init; } = "";
    public List<List<double[]>> Rings { get; init; } = new();
}

public sealed class GearFile
{
    public List<GearDef> Gear { get; init; } = new();
}
