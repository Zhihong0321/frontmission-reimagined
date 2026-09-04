using MechaTrader.Core.Commands;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.World;
using Xunit;

namespace MechaTrader.Core.Tests;

public class MapTests
{
    [Fact]
    public void ShippingCitiesSitOnLand()
    {
        var world = TestWorld.Shipping;
        foreach (var city in world.Cities)
        {
            var cell = world.Map.CellOfCity(city.Id);
            Assert.True(cell.Land, $"{city.Name} landed on {cell.Biome} with no road.");
        }
    }

    [Fact]
    public void AlpineRoadCellsStayWalkableOnLand()
    {
        var world = TestWorld.Shipping;
        var alpine = world.Routes.All.First(r => r.Terrain.Id == "alpine");
        var from = world.Map.CellOfCity(alpine.FromId);
        var to = world.Map.CellOfCity(alpine.ToId);

        var game = Game.New(world, 1);
        var plan = MapMath.Pathfind(game.State.Caravan, world, from, to);
        Assert.NotNull(plan);
        Assert.Equal(VehicleCapability.Land, plan!.Layer);
    }

    [Fact]
    public void MountainsSlowButNoLongerBlockLand()
    {
        var world = TestWorld.Shipping;
        var mountain = world.Map.Cells.FirstOrDefault(c => c.Biome == MapBiome.Mountain && !c.HasRoad);
        Assert.NotNull(mountain);
        Assert.True(mountain!.Walkable(VehicleCapability.Land),
            "off-road mountains must be passable, not a hard wall");
        Assert.True(world.Map.RatesFor(mountain.Biome).SpeedMultiplier < 1.0,
            "off-road mountain travel must be slower than open ground");
        Assert.True(mountain.Walkable(VehicleCapability.Air));
    }

    [Fact]
    public void WaterAndDeepStillBlockLand()
    {
        var world = TestWorld.Shipping;
        var water = world.Map.Cells.First(c => c.Biome == MapBiome.Water && !c.HasRoad);
        Assert.False(water.Walkable(VehicleCapability.Land));
        Assert.True(water.Walkable(VehicleCapability.Water));
    }

    [Fact]
    public void MountainPassesStayFasterThanOffRoadMountains()
    {
        var world = TestWorld.Shipping;
        var alpine = world.Routes.All.First(r => r.Terrain.Id == "alpine");
        var mountain = world.Map.RatesFor(MapBiome.Mountain);
        Assert.True(alpine.Terrain.SpeedMultiplier > mountain.SpeedMultiplier,
            "an alpine pass must beat walking the range off-road");
    }

    [Fact]
    public void UnderwaterCellsBlockAir()
    {
        var world = TestWorld.Shipping;
        var deep = world.Map.Cells.FirstOrDefault(c => c.Biome == MapBiome.Deep);
        Assert.NotNull(deep);
        Assert.False(deep!.Walkable(VehicleCapability.Air));
        Assert.True(deep.Walkable(VehicleCapability.Water));
    }

    [Fact]
    public void DepartToDeepWaterIsRejected()
    {
        var world = TestWorld.Shipping;
        var deep = world.Map.Cells.First(c => c.Biome == MapBiome.Deep);
        var game = Game.New(world, 7);
        var result = game.Apply(new DepartCommand(deep.Id));
        Assert.False(result.Ok);
    }

    [Fact]
    public void DepartToOpenCountryParksOnThatCell()
    {
        var world = TestWorld.Shipping;
        var start = world.Map.CellOfCity(world.Config.StartCityId);
        var cityIds = world.Map.CityCells.Values.Select(c => c.Id).ToHashSet();
        var land = world.Map.Cells.First(c =>
            c.Land && !cityIds.Contains(c.Id)
            && (c.Col != start.Col || c.Row != start.Row)
            && Math.Abs(c.Col - start.Col) + Math.Abs(c.Row - start.Row) <= 8);
        var game = Game.New(world, 3);
        Assert.True(game.Apply(new DepartCommand(land.Id)).Ok, "path to open country");
        Assert.True(game.Apply(new WaitCommand(30)).Ok);
        Assert.Null(game.State.Caravan.Travel);
        Assert.Null(game.State.Caravan.LocationId);
        Assert.Equal(land.Id, game.State.Caravan.CellId);
    }

    [Fact]
    public void DepartWhileTravellingReroutes()
    {
        var world = TestWorld.Shipping;
        var start = world.Config.StartCityId;
        var other = world.Cities.First(c => c.Id != start).Id;
        var game = Game.New(world, 3);
        Assert.True(game.Apply(new DepartCommand(other)).Ok);

        var cityIds = world.Map.CityCells.Values.Select(c => c.Id).ToHashSet();
        var land = world.Map.Cells.First(c => c.Land && !cityIds.Contains(c.Id));
        Assert.True(game.Apply(new DepartCommand(land.Id)).Ok, "reroute");
        Assert.Equal(land.Id, game.State.Caravan.Travel!.ToId);
    }

    [Fact]
    public void DepartToCurrentCellWhileTravellingHalts()
    {
        var world = TestWorld.Shipping;
        var start = world.Config.StartCityId;
        var other = world.Cities.First(c => c.Id != start).Id;
        var game = Game.New(world, 3);
        Assert.True(game.Apply(new DepartCommand(other)).Ok);
        var here = MapMath.Position(game.State, world);
        Assert.True(game.Apply(new DepartCommand(here.Id)).Ok);
        Assert.Null(game.State.Caravan.Travel);
        Assert.Equal(start, game.State.Caravan.LocationId);
    }

    [Fact]
    public void DepartPathIsDenseSmoothAndStartsWhereTheConvoyIs()
    {
        var world = TestWorld.Shipping;
        var game = Game.New(world, 5);
        var (startX, startY) = MapMath.PositionPoint(game.State, world);
        var dest = world.Cities.First(c => c.Id != world.Config.StartCityId).Id;

        Assert.True(game.Apply(new DepartCommand(dest)).Ok, "depart to a city");
        var travel = game.State.Caravan.Travel!;
        Assert.True(travel.Waypoints.Count >= 2, "a real route has waypoints");

        Assert.Equal(startX, travel.Waypoints[0].X, 3);
        Assert.Equal(startY, travel.Waypoints[0].Y, 3);
        Assert.Equal(dest, travel.ToId);

        var maxGap = 0.0;
        for (var i = 1; i < travel.Waypoints.Count; i++)
        {
            var w0 = travel.Waypoints[i - 1];
            var w1 = travel.Waypoints[i];
            maxGap = Math.Max(maxGap, Math.Sqrt((w1.X - w0.X) * (w1.X - w0.X) + (w1.Y - w0.Y) * (w1.Y - w0.Y)));

            // the midpoint of every leg must be on land, so the drawn route never cheats
            var (sc, sr) = world.Map.SubCellAt((w0.X + w1.X) / 2, (w0.Y + w1.Y) / 2);
            Assert.True(world.Map.SubWalkable(sc, sr, VehicleCapability.Land),
                $"midpoint of leg {i} is not land");
        }
        Assert.True(maxGap <= 15.0, $"waypoints too coarse: max gap {maxGap:0.#} km");
    }

    [Fact]
    public void SubCellDepartureParksOnParentCell()
    {
        var world = TestWorld.Shipping;
        var game = Game.New(world, 7);
        var start = world.Map.CellOfCity(world.Config.StartCityId);

        TerrainCell? target = null;
        for (var col = start.Col + 3; col < world.Map.Width; col++)
        {
            var cell = world.Map[col, start.Row];
            if (cell.Land)
            {
                target = cell;
                break;
            }
        }
        Assert.NotNull(target);

        var sc = target!.Col * WorldMap.SubDiv + 2;
        var sr = target.Row * WorldMap.SubDiv + 2;
        var id = $"s{sc},{sr}";

        Assert.True(game.Apply(new DepartCommand(id)).Ok, "depart to a sub-cell");
        var days = game.State.Caravan.Travel!.DaysRemaining;
        game.Apply(new WaitCommand(days));
        Assert.Null(game.State.Caravan.Travel);
        Assert.Equal(target.Id, game.State.Caravan.CellId);
    }
}

public class MiningTests
{
    [Fact]
    public void SameSeedProducesTheSameDeposits()
    {
        var world = TestWorld.Shipping;
        var a = Game.New(world, 42);
        var b = Game.New(world, 42);
        Assert.Equal(
            a.State.MiningSites.Select(s => (s.Id, s.Col, s.Row, s.Remaining)),
            b.State.MiningSites.Select(s => (s.Id, s.Col, s.Row, s.Remaining)));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentDeposits()
    {
        var world = TestWorld.Shipping;
        var a = Game.New(world, 42);
        var b = Game.New(world, 43);
        Assert.NotEqual(
            a.State.MiningSites.Select(s => (s.Col, s.Row)).ToArray(),
            b.State.MiningSites.Select(s => (s.Col, s.Row)).ToArray());
    }

    [Fact]
    public void BuildingAViewDoesNotConsumeRngOrMoveDeposits()
    {
        var game = Game.New(TestWorld.Shipping, 99);
        var rng = game.State.RngState;
        var snapshot = game.State.MiningSites.Select(s => s.Remaining).ToArray();
        _ = game.View();
        Assert.Equal(rng, game.State.RngState);
        Assert.Equal(snapshot, game.State.MiningSites.Select(s => s.Remaining).ToArray());
    }

    [Fact]
    public void WaitingAtADepositWithoutGearExtractsNothing()
    {
        var world = TestWorld.Shipping;
        var game = Game.New(world, 11);
        var site = game.State.MiningSites[0];
        var remaining = site.Remaining;

        Assert.True(game.Apply(new DepartCommand(site.Id)).Ok, "path to the claim");
        var days = game.State.Caravan.Travel!.DaysRemaining;
        game.Apply(new WaitCommand(days));
        Assert.Equal(site.Id, game.State.Caravan.SiteId);

        game.Apply(new WaitCommand(1));
        Assert.Equal(remaining, game.State.Site(site.Id)!.Remaining);
        Assert.Equal(0, game.State.Caravan.Held(site.GoodId));
    }

    [Fact]
    public void GearLetsAParkedConvoyExtractOre()
    {
        var world = TestWorld.Shipping;
        var game = Game.New(world, 11);
        game.State.Cash = 100_000;

        var gear = world.Gear.First(g => g.MineYield > 0);
        Assert.True(game.Apply(new BuyGearCommand(gear.Id)).Ok);

        var site = game.State.MiningSites[0];
        Assert.True(game.Apply(new DepartCommand(site.Id)).Ok);
        game.Apply(new WaitCommand(game.State.Caravan.Travel!.DaysRemaining));

        var before = game.State.Site(site.Id)!.Remaining;
        game.Apply(new WaitCommand(1));
        var after = game.State.Site(site.Id)!.Remaining;

        Assert.True(after < before, "reserve should fall");
        Assert.True(game.State.Caravan.Held(site.GoodId) > 0, "ore should land in the hold");
        Assert.Equal(0, game.State.Caravan.Cargo[site.GoodId].TotalCost);
    }

    [Fact]
    public void BuyGearIsRejectedWithoutHoldSpace()
    {
        var world = TestWorld.Shipping;
        var game = Game.New(world, 3);
        game.State.Cash = 100_000;
        var gear = world.Gear.OrderByDescending(g => g.Volume).First();
        game.State.Caravan.Cargo["scrap"] = new State.CargoLot { Units = 196, TotalCost = 0 };
        var result = game.Apply(new BuyGearCommand(gear.Id));
        Assert.False(result.Ok);
        Assert.Contains("hold", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DepositsSurviveASaveLoadRoundTrip()
    {
        var world = TestWorld.Shipping;
        var game = Game.New(world, 21);
        game.State.Cash = 100_000;
        game.Apply(new BuyGearCommand(world.Gear[0].Id));
        var site = game.State.MiningSites[0];
        game.Apply(new DepartCommand(site.Id));
        game.Apply(new WaitCommand(game.State.Caravan.Travel!.DaysRemaining + 1));

        var saved = System.Text.Json.JsonSerializer.Serialize(game.State);
        var restored = System.Text.Json.JsonSerializer.Deserialize<State.GameState>(saved)!;
        var resumed = Game.Resume(world, restored);

        Assert.Equal(game.State.MiningSites.Count, resumed.State.MiningSites.Count);
        Assert.Equal(game.State.Caravan.SiteId, resumed.State.Caravan.SiteId);
        Assert.Equal(
            game.State.MiningSites.Select(s => s.Remaining),
            resumed.State.MiningSites.Select(s => s.Remaining));
    }
}
