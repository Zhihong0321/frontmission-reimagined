using MechaTrader.Core.Sim;
using MechaTrader.Core.World;
using Xunit;

namespace MechaTrader.Core.Tests;

public class WorldLoaderTests
{
    [Fact]
    public void ShippingContentLoads()
    {
        var world = TestWorld.Shipping;

        Assert.Equal(20, world.Cities.Count);
        Assert.Equal(8, world.Goods.Count);
        Assert.NotEmpty(world.Routes.All);
        Assert.NotEmpty(world.Trucks);
    }

    [Fact]
    public void EveryCityQuotesEveryGood()
    {
        var world = TestWorld.Shipping;

        foreach (var city in world.Cities)
        {
            foreach (var good in world.Goods)
            {
                Assert.True(city.Market.ContainsKey(good.Id),
                    $"{city.Id} has no market entry for {good.Id}.");
                Assert.True(city.Market[good.Id].Equilibrium > 0,
                    $"{city.Id}/{good.Id} has a non-positive equilibrium.");
            }
        }
    }

    [Fact]
    public void EveryRouteConnectsKnownCities()
    {
        var world = TestWorld.Shipping;

        foreach (var route in world.Routes.All)
        {
            Assert.True(world.CitiesById.ContainsKey(route.FromId), $"Unknown city {route.FromId}.");
            Assert.True(world.CitiesById.ContainsKey(route.ToId), $"Unknown city {route.ToId}.");
            Assert.True(route.DistanceKm > 0, $"Route {route.FromId}->{route.ToId} has no distance.");
        }
    }

    [Fact]
    public void EveryCityIsReachableFromTheStart()
    {
        var world = TestWorld.Shipping;
        var reachable = world.Routes.Reachable(world.Config.StartCityId);

        foreach (var city in world.Cities)
        {
            Assert.True(reachable.Contains(city.Id), $"{city.Id} is cut off from the road network.");
        }
    }

    [Fact]
    public void ProducingCityIsCheaperThanConsumingCity()
    {
        // The core promise of the price model: where a good is made, it is cheap.
        var world = TestWorld.Shipping;
        var eco = world.Config.Economy;

        foreach (var good in world.Goods)
        {
            var producers = world.Cities
                .Where(c => c.Market[good.Id].Production > c.Market[good.Id].Consumption)
                .ToList();
            var consumers = world.Cities
                .Where(c => c.Market[good.Id].Consumption > c.Market[good.Id].Production)
                .ToList();

            if (producers.Count == 0 || consumers.Count == 0) continue;

            double PriceAtSteadyState(City city)
            {
                var profile = city.Market[good.Id];
                return Economy.UnitPrice(good, profile, Economy.InitialStock(profile, eco), eco);
            }

            var cheapestProducer = producers.Min(PriceAtSteadyState);
            var dearestConsumer = consumers.Max(PriceAtSteadyState);

            Assert.True(cheapestProducer < dearestConsumer,
                $"{good.Name}: producers are not cheaper than consumers " +
                $"({cheapestProducer:0.0} vs {dearestConsumer:0.0}).");
        }
    }

    [Fact]
    public void LoaderRunsEntirelyFromMemory()
    {
        // Architectural guarantee: the core never needs a filesystem. Godot will hand it
        // strings from res:// exactly like this.
        var world = WorldLoader.Load(MinimalWorld.Files);

        Assert.Equal(2, world.Cities.Count);
        Assert.Single(world.Routes.All);
        Assert.Equal("alpha", world.Config.StartCityId);
    }

    [Fact]
    public void UnknownIndustryIsRejected()
    {
        var files = MinimalWorld.With(WorldLoader.CitiesKey, """
        { "cities": [
          { "id": "alpha", "name": "Alpha", "region": "R", "lon": 0, "lat": 45, "population": 1, "industries": ["nope"] },
          { "id": "beta",  "name": "Beta",  "region": "R", "lon": 1, "lat": 45, "population": 1, "industries": ["works"] }
        ] }
        """);

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("nope", error.Message);
    }

    [Fact]
    public void RouteToUnknownCityIsRejected()
    {
        var files = MinimalWorld.With(WorldLoader.RoutesKey, """
        { "routes": [ { "from": "alpha", "to": "atlantis", "terrain": "plain" } ] }
        """);

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("atlantis", error.Message);
    }

    [Fact]
    public void UnreachableCityIsRejected()
    {
        var files = MinimalWorld.With(WorldLoader.RoutesKey, """{ "routes": [] }""");

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("unreachable", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingContentFileIsRejected()
    {
        var files = MinimalWorld.Files.Where(kv => kv.Key != WorldLoader.GoodsKey)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
    }
}

/// <summary>A hand-written two-city world, used to test loader validation in isolation.</summary>
internal static class MinimalWorld
{
    public static IReadOnlyDictionary<string, string> Files => new Dictionary<string, string>
    {
        [WorldLoader.ConfigKey] = """
        { "startCash": 1000, "startCityId": "alpha", "startTruckIds": ["van"],
          "economy": { "driftRate": 0.1, "equilibriumDays": 10, "minEquilibrium": 50, "minStock": 5,
                       "noiseSigma": 0.0, "spread": 0.05, "minPriceMult": 0.4, "maxPriceMult": 2.5,
                       "roadDetourFactor": 1.0 } }
        """,
        [WorldLoader.GoodsKey] = """
        { "goods": [ { "id": "widget", "name": "Widget", "tier": "raw", "basePrice": 10,
                       "unitVolume": 1.0, "elasticity": 0.6 } ] }
        """,
        [WorldLoader.TerrainKey] = """
        { "terrain": [ { "id": "plain", "name": "Open Road", "speedMultiplier": 1.0, "costMultiplier": 1.0 } ] }
        """,
        [WorldLoader.TrucksKey] = """
        { "trucks": [ { "id": "van", "name": "Van", "capacity": 50, "speedKmPerDay": 100,
                        "upkeepPerDay": 5, "fuelPerKm": 0.5, "price": 500 } ] }
        """,
        [WorldLoader.IndustriesKey] = """
        { "baseConsumptionPerPop": { "widget": 1 },
          "industries": [ { "id": "works", "name": "Works", "production": { "widget": 20 }, "consumption": {} } ] }
        """,
        [WorldLoader.CitiesKey] = """
        { "cities": [
          { "id": "alpha", "name": "Alpha", "region": "R", "lon": 0, "lat": 45, "population": 1, "industries": ["works"] },
          { "id": "beta",  "name": "Beta",  "region": "R", "lon": 1, "lat": 45, "population": 1, "industries": [] }
        ] }
        """,
        [WorldLoader.RoutesKey] = """
        { "routes": [ { "from": "alpha", "to": "beta", "terrain": "plain" } ] }
        """
    };

    public static Dictionary<string, string> With(string key, string json)
    {
        var copy = Files.ToDictionary(kv => kv.Key, kv => kv.Value);
        copy[key] = json;
        return copy;
    }
}
