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
        Assert.True(world.Goods.Count >= 17, "The catalog shrank below the original seventeen goods.");
        Assert.Equal(5, world.Tiers.Count);
        Assert.NotEmpty(world.TruckUpgrades);
        Assert.NotEmpty(world.Contracts.Kinds);
        Assert.NotEmpty(world.Expos.Themes);
        Assert.Equal(4, world.Standing.Segments.Count);
        Assert.NotEmpty(world.Routes.All);
        Assert.NotEmpty(world.Trucks);
        Assert.All(world.Cities, c => Assert.False(string.IsNullOrWhiteSpace(c.GovernorName)));
        Assert.NotEmpty(world.Standing.Actions);
        Assert.NotEmpty(world.Standing.Permits);
        Assert.NotEmpty(world.Events.Events);
        Assert.True(world.Map.Width > 1);
        Assert.NotEmpty(world.Gear);
        Assert.All(world.Cities, c => Assert.True(world.Map.CellOfCity(c.Id).Land));
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
          { "id": "alpha", "name": "Alpha", "region": "R", "lon": 0, "lat": 45, "industries": ["nope"], "stats": { "population": 1 } },
          { "id": "beta",  "name": "Beta",  "region": "R", "lon": 1, "lat": 45, "industries": ["works"], "stats": { "population": 1 } }
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
    public void CrewSkillWiredToAnUnknownLeverIsRejected()
    {
        // A lever typo would otherwise ship as a stat the player pays for that quietly
        // does nothing.
        var files = MinimalWorld.With(WorldLoader.CrewKey,
            MinimalWorld.Files[WorldLoader.CrewKey].Replace("\"lever\": \"buy\"", "\"lever\": \"telepathy\""));

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("telepathy", error.Message);
    }

    [Fact]
    public void TwoCrewSkillsClaimingOneLeverAreRejected()
    {
        // Two skills on one lever would make the effective bonus depend on declaration
        // order, which is exactly the kind of silent tuning bug content should not allow.
        var second = "{ \"id\": \"barter\", \"name\": \"Barter\", \"lever\": \"buy\", \"maxEffect\": 0.5 }";

        var files = MinimalWorld.With(WorldLoader.CrewKey,
            MinimalWorld.Files[WorldLoader.CrewKey].Replace("\"skills\": [ ", "\"skills\": [ " + second + ", "));

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("lever", error.Message);
    }

    [Fact]
    public void CityStatOutsideItsDeclaredRangeIsRejected()
    {
        // A founding value the catalogue does not allow is a content bug, and one that
        // would otherwise show up much later as a bar drawn past the end of its track.
        var files = MinimalWorld.With(WorldLoader.CitiesKey, """
        { "cities": [
          { "id": "alpha", "name": "Alpha", "region": "R", "lon": 0, "lat": 45, "industries": ["works"], "stats": { "population": 1, "peace": 400 } },
          { "id": "beta",  "name": "Beta",  "region": "R", "lon": 1, "lat": 45, "industries": [], "stats": { "population": 1 } }
        ] }
        """);

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("peace", error.Message);
    }

    [Fact]
    public void CityStatThatIsNotInTheCatalogueIsRejected()
    {
        var files = MinimalWorld.With(WorldLoader.CitiesKey, """
        { "cities": [
          { "id": "alpha", "name": "Alpha", "region": "R", "lon": 0, "lat": 45, "industries": ["works"], "stats": { "population": 1, "moralw": 50 } },
          { "id": "beta",  "name": "Beta",  "region": "R", "lon": 1, "lat": 45, "industries": [], "stats": { "population": 1 } }
        ] }
        """);

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("moralw", error.Message);
    }

    [Fact]
    public void SupplyReadingAnUnknownGoodIsRejected()
    {
        var files = MinimalWorld.With(WorldLoader.CityStatsKey,
            MinimalWorld.Files[WorldLoader.CityStatsKey].Replace("\"widget\"", "\"unobtainium\""));

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("unobtainium", error.Message);
    }

    [Fact]
    public void FavorActionNamingAnUnknownVitalIsRejected()
    {
        var files = MinimalWorld.With(WorldLoader.StandingKey,
            MinimalWorld.Files[WorldLoader.StandingKey].Replace("\"peace\"", "\"mood\""));

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("mood", error.Message);
    }

    [Fact]
    public void EventNamingAnUnknownGoodIsRejected()
    {
        var files = MinimalWorld.With(WorldLoader.EventsKey,
            MinimalWorld.Files[WorldLoader.EventsKey].Replace("\"widget\"", "\"unobtainium\""));

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("unobtainium", error.Message);
    }

    [Fact]
    public void EventWithNoEffectIsRejected()
    {
        var files = MinimalWorld.With(WorldLoader.EventsKey, """
        { "maxConcurrent": 1, "dailyChance": 0.5,
          "events": [
            { "id": "noop", "name": "Nothing", "headline": "Quiet day", "durationDays": 2, "weight": 1 }
          ] }
        """);

        var error = Assert.Throws<WorldLoadException>(() => WorldLoader.Load(files));
        Assert.Contains("no effect", error.Message);
    }

    [Fact]
    public void CityAuthoringNoStatsFallsBackToTheCatalogueDefaults()
    {
        // Content should be able to add a stat without every city having to be edited
        // in the same commit.
        var files = MinimalWorld.With(WorldLoader.CitiesKey, """
        { "cities": [
          { "id": "alpha", "name": "Alpha", "region": "R", "lon": 0, "lat": 45, "industries": ["works"], "stats": { "population": 1 } },
          { "id": "beta",  "name": "Beta",  "region": "R", "lon": 1, "lat": 45, "industries": [], "stats": { "population": 1 } }
        ] }
        """);

        var world = WorldLoader.Load(files);
        var peace = world.CityStats.Vital("peace")!;

        Assert.Equal(peace.Default, world.City("alpha").Vitals["peace"]);
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
        { "quality": { "nominal": 70, "base": 50, "random": 15, "cityVitalId": "", "cityVitalWeight": 0, "spread": 22, "sTierAt": 90, "sTierSellBonus": 0.3 },
          "tiers": [ { "tier": 1, "name": "Common", "color": "#fff", "minStanding": 0, "minPricePerVolume": 0 },
                     { "tier": 2, "name": "Fine",   "color": "#0f0", "minStanding": 30, "minPricePerVolume": 40, "equilibriumScale": 0.5 } ],
          "goods": [ { "id": "widget", "name": "Widget", "tier": 1, "basePrice": 10,
                       "unitVolume": 1.0, "elasticity": 0.6 },
                     { "id": "gadget", "name": "Gadget", "tier": 2, "basePrice": 50,
                       "unitVolume": 1.0, "elasticity": 0.6 } ] }
        """,
        [WorldLoader.TerrainKey] = """
        { "terrain": [ { "id": "plain", "name": "Open Road", "speedMultiplier": 1.0, "costMultiplier": 1.0 } ] }
        """,
        [WorldLoader.TrucksKey] = """
        { "resaleFraction": 0.5,
          "trucks": [ { "id": "van", "name": "Van", "capacity": 50, "speedKmPerDay": 100,
                        "upkeepPerDay": 5, "fuelPerKm": 0.5, "price": 500 } ],
          "upgrades": [ { "id": "rack", "name": "Rack", "price": 100, "kinds": ["truck"], "capacityBonus": 10 } ] }
        """,
        [WorldLoader.IndustriesKey] = """
        { "baseConsumptionPerPop": { "widget": 1, "gadget": 0.2 },
          "industries": [ { "id": "works", "name": "Works", "production": { "widget": 20 }, "consumption": {} } ] }
        """,
        [WorldLoader.CitiesKey] = """
        { "cities": [
          { "id": "alpha", "name": "Alpha", "region": "R", "lon": 0, "lat": 45, "industries": ["works"], "stats": { "population": 1, "peace": 40 } },
          { "id": "beta",  "name": "Beta",  "region": "R", "lon": 1, "lat": 45, "industries": [], "stats": { "population": 1, "peace": 80 } }
        ] }
        """,
        [WorldLoader.RoutesKey] = """
        { "routes": [ { "from": "alpha", "to": "beta", "terrain": "plain" } ] }
        """,
        [WorldLoader.CrewKey] = """
        { "maxSkill": 10, "crewCapacity": 2, "refreshDays": 10, "signingFeeDays": 20, "severanceDays": 5,
          "wage": { "base": 5, "perSkillPoint": 6 },
          "skills": [ { "id": "haggling", "name": "Haggling", "lever": "buy", "maxEffect": 0.5 } ],
          "roles": [ { "id": "hand", "name": "Hand", "primary": "haggling" } ],
          "candidates": { "basePerCity": 1, "perPopulation": 1, "maxPerCity": 3,
                          "primaryMin": 4, "primaryMax": 9, "secondaryMin": 1, "secondaryMax": 4 },
          "industryAffinity": { "works": { "haggling": 1 } },
          "firstNames": [ "Ada" ], "surnames": [ "Brandt" ] }
        """,
        [WorldLoader.CityStatsKey] = """
        { "populationVitalId": "population",
          "vitals": [
            { "id": "population", "name": "Population", "unit": "M", "default": 1,
              "min": 0.1, "max": 5, "decimals": 1, "displayScale": 2 },
            { "id": "peace", "name": "Peacefulness", "unit": "%", "default": 50, "min": 0, "max": 100,
              "bands": [ { "id": "uneasy", "name": "Uneasy", "upTo": 50, "tone": "warn" },
                         { "id": "calm",   "name": "Calm",   "tone": "good" } ] }
          ],
          "supplies": [ { "id": "trade", "name": "Widget Supply", "goods": [ "widget" ] } ],
          "supplyBands": [ { "id": "short",  "name": "Short",  "upTo": 85, "tone": "bad" },
                           { "id": "steady", "name": "Steady", "tone": "good" } ] }
        """,
        [WorldLoader.StandingKey] = """
        { "max": 200, "segmentMax": 100, "reservePerPoint": 0.01, "reserveMax": 0.4,
          "tradersPerThousandCr": 0, "contractLapsePenalty": 2,
          "segments": [ { "id": "governor", "name": "Governor" }, { "id": "citizens", "name": "Citizens" }, { "id": "traders", "name": "Traders" } ],
          "ranks": [
            { "id": "stranger", "name": "Stranger", "upTo": 50, "tone": "muted" },
            { "id": "patron",   "name": "Patron",   "tone": "good" }
          ],
          "permits": [
            { "id": "shop",    "name": "Shop permit",    "standing": 40, "blurb": "A stall." },
            { "id": "factory", "name": "Factory permit", "standing": 80, "blurb": "A works." }
          ],
          "actions": [
            { "id": "donate", "name": "Donate", "cost": 100, "standing": 10, "blurb": "A gift." },
            { "id": "invest", "name": "Invest", "cost": 200, "standing": 8, "vitalId": "peace", "vitalDelta": 5, "blurb": "Capital." },
            { "id": "aid",    "name": "Aid",    "cost": 150, "standing": 6, "stockPerGood": 10, "blurb": "Ship in goods." }
          ] }
        """,
        [WorldLoader.EventsKey] = """
        { "maxConcurrent": 2, "dailyChance": 0,
          "events": [
            { "id": "boom", "name": "Boom", "kind": "market", "headline": "Boom in {city}",
              "detail": "Prices jump.", "tone": "warn", "durationDays": 3, "weight": 1,
              "goods": ["widget"], "priceMult": 1.2, "vitalDeltas": { "peace": -5 } }
          ] }
        """,
        [WorldLoader.MapKey] = """
        { "cellKm": 50, "originLon": -0.5, "originLat": 45.5, "width": 6, "height": 4,
          "defaultBiome": "plain",
          "mining": { "spotCount": 1, "goodId": "widget", "reserveMin": 40, "reserveMax": 40 },
          "offRoad": { "plain": { "speedMultiplier": 0.85, "costMultiplier": 1.15 },
                       "hill":  { "speedMultiplier": 0.65, "costMultiplier": 1.35 } },
          "regions": [ { "biome": "hill", "rings": [[[0.3, 44.6], [0.7, 44.6], [0.7, 44.9], [0.3, 44.9]]] } ] }
        """,
        [WorldLoader.GearKey] = """
        { "gear": [ { "id": "pick", "name": "Pick", "price": 50, "volume": 2,
                      "capabilities": ["mine"], "mineYield": 5 } ] }
        """,
        [WorldLoader.ContractsKey] = """
        { "refreshDays": 10, "offersPerCity": 2, "deadlineDaysMin": 10, "deadlineDaysMax": 20,
          "tierWeights": { "1": 1, "2": 1 },
          "kinds": [ { "id": "supply", "name": "Supply", "weight": 1, "unitsMin": 5, "unitsMax": 10, "priceMult": 1.2, "standing": 3, "blurb": "A standing order." } ] }
        """,
        [WorldLoader.ExposKey] = """
        { "cycleDays": 10, "feeBase": 100, "feePerPop": 0, "buyersBase": 3, "buyersPerPop": 0,
          "buffMax": 0.5, "buffMin": 0.1, "premiumMult": 0.3, "noise": 0.1, "closeBand": 0.2, "lotMax": 5,
          "themes": [ { "id": "fair", "title": "Fair", "categories": ["a", "b"], "durationDays": 4, "weight": 1 } ],
          "remarks": { "bought": ["Sold."], "tooDear": ["No."] } }
        """
    };

    public static Dictionary<string, string> With(string key, string json)
    {
        var copy = Files.ToDictionary(kv => kv.Key, kv => kv.Value);
        copy[key] = json;
        return copy;
    }
}
