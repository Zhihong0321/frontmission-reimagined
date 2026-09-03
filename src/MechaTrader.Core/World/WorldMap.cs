using MechaTrader.Core.Model;

namespace MechaTrader.Core.World;

/// <summary>
/// One cell on the geographic grid. Biome is authored (painted from regions); layer
/// flags and road walkability are derived, never stored as a second map.
/// </summary>
public sealed class TerrainCell
{
    public required int Col { get; init; }
    public required int Row { get; init; }
    public required string Biome { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public TerrainDef? Road { get; init; }

    public string Id => $"{Col},{Row}";

    public bool HasRoad => Road is not null;

    public bool Land => HasRoad || Biome is MapBiome.Plain or MapBiome.Hill
        or MapBiome.Forest or MapBiome.Desert or MapBiome.Tundra or MapBiome.Swamp
        or MapBiome.Mountain;
    public bool Water => Biome is MapBiome.Water or MapBiome.Deep;
    public bool Air => Biome != MapBiome.Deep;

    public bool Walkable(string layer) => layer switch
    {
        VehicleCapability.Land => Land,
        VehicleCapability.Water => Water,
        VehicleCapability.Air => Air,
        _ => false
    };
}

public static class MapBiome
{
    public const string Plain = "plain";
    public const string Hill = "hill";
    public const string Mountain = "mountain";
    public const string Forest = "forest";
    public const string Desert = "desert";
    public const string Tundra = "tundra";
    public const string Swamp = "swamp";
    public const string Water = "water";
    public const string Deep = "deep";

    public static readonly IReadOnlyList<string> All =
        new[] { Plain, Hill, Mountain, Forest, Desert, Tundra, Swamp, Water, Deep };

    public static char Code(string biome) => biome switch
    {
        Hill => 'H',
        Mountain => 'M',
        Forest => 'F',
        Desert => 'A',
        Tundra => 'T',
        Swamp => 'S',
        Water => 'W',
        Deep => 'D',
        _ => 'P'
    };

    public static string FromCode(char code) => code switch
    {
        'H' => Hill,
        'M' => Mountain,
        'F' => Forest,
        'A' => Desert,
        'T' => Tundra,
        'S' => Swamp,
        'W' => Water,
        'D' => Deep,
        _ => Plain
    };
}

/// <summary>How mining deposits are placed on a fresh run. Content, from map.json.</summary>
public sealed class MiningMapConfig
{
    public int SpotCount { get; init; } = 8;
    public string GoodId { get; init; } = "ore";
    public double ReserveMin { get; init; } = 80;
    public double ReserveMax { get; init; } = 220;
}

/// <summary>Off-road speed and fuel, per biome, when no road overlay is present.</summary>
public sealed class OffRoadRates
{
    public double SpeedMultiplier { get; init; } = 1.0;
    public double CostMultiplier { get; init; } = 1.0;
}

/// <summary>
/// The painted terrain grid. Immutable after load. Cities snap to a cell; authored
/// roads punch corridors through otherwise blocked terrain.
/// </summary>
public sealed class WorldMap
{
    public required double CellKm { get; init; }
    public required double OriginX { get; init; }
    public required double OriginY { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required IReadOnlyList<TerrainCell> Cells { get; init; }
    public required MiningMapConfig Mining { get; init; }
    public required IReadOnlyDictionary<string, OffRoadRates> OffRoad { get; init; }
    public required IReadOnlyDictionary<string, TerrainCell> CityCells { get; init; }

    public TerrainCell this[int col, int row] => Cells[Index(col, row)];

    public int Index(int col, int row) => row * Width + col;

    public bool InBounds(int col, int row)
        => col >= 0 && row >= 0 && col < Width && row < Height;

    public TerrainCell? TryGet(int col, int row)
        => InBounds(col, row) ? this[col, row] : null;

    public TerrainCell CellOfCity(string cityId)
        => CityCells.TryGetValue(cityId, out var cell)
            ? cell
            : throw new KeyNotFoundException($"City '{cityId}' is not on the map.");

    public TerrainCell At(double x, double y)
    {
        var col = (int)Math.Floor((x - OriginX) / CellKm);
        var row = (int)Math.Floor((y - OriginY) / CellKm);
        col = Math.Clamp(col, 0, Width - 1);
        row = Math.Clamp(row, 0, Height - 1);
        return this[col, row];
    }

    public bool TryParseCellId(string id, out TerrainCell cell)
    {
        cell = null!;
        var comma = id.IndexOf(',');
        if (comma <= 0 || comma == id.Length - 1) return false;
        if (!int.TryParse(id.AsSpan(0, comma), out var col)) return false;
        if (!int.TryParse(id.AsSpan(comma + 1), out var row)) return false;
        if (!InBounds(col, row)) return false;
        cell = this[col, row];
        return true;
    }

    public OffRoadRates RatesFor(string biome)
        => OffRoad.TryGetValue(biome, out var rates) ? rates : new OffRoadRates();

    /// <summary>Speed multiplier of a cell: road terrain wins, otherwise off-road biome.</summary>
    public double SpeedMultiplier(TerrainCell cell)
        => cell.Road?.SpeedMultiplier ?? RatesFor(cell.Biome).SpeedMultiplier;

    /// <summary>Fuel cost multiplier of a cell: road terrain wins, otherwise off-road biome.</summary>
    public double CostMultiplier(TerrainCell cell)
        => cell.Road?.CostMultiplier ?? RatesFor(cell.Biome).CostMultiplier;

    // ── Sub-cell grid ────────────────────────────────────────────────────────
    // Pathfinding runs on a finer lattice so routes can bend at sub-cell
    // resolution; terrain, roads and rates all still come from the parent cell.

    /// <summary>Sub-cells per cell edge. 4 × 4 → 12.5 km steps on the 50 km map.</summary>
    public const int SubDiv = 4;

    public double SubStep => CellKm / SubDiv;
    public int SubWidth => Width * SubDiv;
    public int SubHeight => Height * SubDiv;

    public bool SubInBounds(int sc, int sr)
        => sc >= 0 && sr >= 0 && sc < SubWidth && sr < SubHeight;

    /// <summary>Sub-cell index containing a world point, clamped to the grid.</summary>
    public (int sc, int sr) SubCellAt(double x, double y)
    {
        var sc = (int)Math.Floor((x - OriginX) / SubStep);
        var sr = (int)Math.Floor((y - OriginY) / SubStep);
        sc = Math.Clamp(sc, 0, SubWidth - 1);
        sr = Math.Clamp(sr, 0, SubHeight - 1);
        return (sc, sr);
    }

    /// <summary>Centre of a sub-cell in world km.</summary>
    public (double x, double y) SubCenter(int sc, int sr)
        => (OriginX + (sc + 0.5) * SubStep, OriginY + (sr + 0.5) * SubStep);

    /// <summary>The terrain cell a sub-cell belongs to.</summary>
    public TerrainCell ParentCell(int sc, int sr)
        => this[sc / SubDiv, sr / SubDiv];

    public bool SubWalkable(int sc, int sr, string layer)
        => SubInBounds(sc, sr) && ParentCell(sc, sr).Walkable(layer);

    public double SubSpeed(int sc, int sr) => SpeedMultiplier(ParentCell(sc, sr));

    public double SubCost(int sc, int sr) => CostMultiplier(ParentCell(sc, sr));

    /// <summary>Nearest walkable sub-cell to a given sub-cell, spiral search.</summary>
    public (int sc, int sr)? NearestWalkableSubCell(int sc, int sr, string layer, int radius)
    {
        if (SubWalkable(sc, sr, layer)) return (sc, sr);

        for (var r = 1; r <= radius; r++)
        {
            for (var dc = -r; dc <= r; dc++)
            {
                for (var dr = -r; dr <= r; dr++)
                {
                    if (Math.Max(Math.Abs(dc), Math.Abs(dr)) != r) continue;
                    var nsc = sc + dc;
                    var nsr = sr + dr;
                    if (SubWalkable(nsc, nsr, layer)) return (nsc, nsr);
                }
            }
        }

        return null;
    }
}
