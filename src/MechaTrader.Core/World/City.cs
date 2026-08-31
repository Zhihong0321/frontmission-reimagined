using MechaTrader.Core.Model;

namespace MechaTrader.Core.World;

/// <summary>Per-good economic profile of one city, generated from its industries.</summary>
public sealed class CityGoodProfile
{
    public required string GoodId { get; init; }

    /// <summary>Units produced locally per day.</summary>
    public required double Production { get; init; }

    /// <summary>Units consumed locally per day.</summary>
    public required double Consumption { get; init; }

    /// <summary>Stock level the city naturally holds; the anchor for pricing.</summary>
    public required double Equilibrium { get; init; }

    public double PriceModifier { get; init; } = 1.0;

    /// <summary>
    /// Where stock settles with no player interference:
    /// drift pulls toward Equilibrium while net flow pushes away from it.
    /// Producers settle above (cheap), consumers below (expensive).
    /// </summary>
    public double SteadyStateStock(double driftRate)
        => Equilibrium + (Production - Consumption) / driftRate;
}

/// <summary>A city with projected map coordinates and a generated market.</summary>
public sealed class City
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Region { get; init; }
    public required double Lon { get; init; }
    public required double Lat { get; init; }

    /// <summary>Projected map position in kilometres. X east, Y south.</summary>
    public required double X { get; init; }
    public required double Y { get; init; }

    public required double Population { get; init; }
    public required IReadOnlyList<string> Industries { get; init; }
    public required IReadOnlyDictionary<string, CityGoodProfile> Market { get; init; }
}

/// <summary>An undirected road link between two cities.</summary>
public sealed class Route
{
    public required string FromId { get; init; }
    public required string ToId { get; init; }
    public required TerrainDef Terrain { get; init; }
    public required double DistanceKm { get; init; }

    public string Other(string cityId) => cityId == FromId ? ToId : FromId;
}
