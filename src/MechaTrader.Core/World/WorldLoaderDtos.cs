using MechaTrader.Core.Model;

namespace MechaTrader.Core.World;

public static partial class WorldLoader
{
    private sealed class GoodsFile
    {
        public List<CategoryDef> Categories { get; init; } = new();
        public List<TierDef> Tiers { get; init; } = new();
        public QualityConfig? Quality { get; init; }
        public List<GoodDef> Goods { get; init; } = new();
    }
    private sealed class TerrainFile { public List<TerrainDef> Terrain { get; init; } = new(); }

    private sealed class TrucksFile
    {
        public double ResaleFraction { get; init; } = 0.5;
        public List<TruckDef> Trucks { get; init; } = new();
        public List<TruckUpgradeDef> Upgrades { get; init; } = new();
    }

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
        public List<string> Industries { get; init; } = new();

        /// <summary>Founding vitals, keyed by the ids citystats.json declares.</summary>
        public Dictionary<string, double> Stats { get; init; } = new();

        public string Governor { get; init; } = "";
        public string GovernorTitle { get; init; } = "";
    }

    private sealed class RouteDto
    {
        public string From { get; init; } = "";
        public string To { get; init; } = "";
        public string Terrain { get; init; } = "plain";
        public double? DistanceKm { get; init; }
    }
}
