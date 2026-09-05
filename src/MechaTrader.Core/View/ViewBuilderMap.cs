using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.View;

public static partial class ViewBuilder
{
    /// <summary>
    /// The map, in the projected kilometre coordinates the loader already computed.
    /// Normalising to a viewport is the front-end's business, not the simulation's.
    /// </summary>
    public static MapView BuildMap(WorldData world)
    {
        var cities = world.Cities
            .Select(c => new MapCityView(c.Id, c.Name, c.Region, Math.Round(c.X, 1), Math.Round(c.Y, 1)))
            .ToList();

        var roads = world.Routes.All
            .Select(r => new MapRoadView(r.FromId, r.ToId, r.Terrain.Id, r.Terrain.Name))
            .ToList();

        var map = world.Map;
        var biomes = new char[map.Cells.Count];
        var mask = new char[map.Cells.Count];
        for (var i = 0; i < map.Cells.Count; i++)
        {
            biomes[i] = MapBiome.Code(map.Cells[i].Biome);
            mask[i] = map.Cells[i].HasRoad ? '1' : '0';
        }

        return new MapView(
            cities, roads,
            map.Width, map.Height, map.CellKm,
            Math.Round(map.OriginX, 1), Math.Round(map.OriginY, 1),
            new string(biomes), new string(mask));
    }

    private static List<TruckOfferView> BuildShipyard(WorldData world)
        => world.Trucks
            .Select(t => new TruckOfferView(
                t.Id, t.Name, t.EffectiveKind, t.Price, t.Capacity, t.SpeedKmPerDay,
                t.UpkeepPerDay, t.FuelPerKm, t.MineYield))
            .ToList();

    private static List<GearOfferView> BuildOutfitters(GameState state, WorldData world)
    {
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        return world.Gear.Select(g => new GearOfferView(
            g.Id, g.Name, g.Price, g.Volume, g.MineYield,
            Affordable: state.Cash >= g.Price,
            Fits: g.Volume <= free + 1e-9)).ToList();
    }

    private static SiteView BuildSite(GameState state, WorldData world, MiningSite site)
    {
        var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g : null;
        var can = CaravanMath.CanMine(state.Caravan, world);
        var yield = CaravanMath.MineYield(state.Caravan, world);
        var free = CaravanMath.FreeVolume(state.Caravan, world);
        var room = good is null || good.UnitVolume <= 0 ? 0 : (int)Math.Floor(free / good.UnitVolume);
        var expected = (int)Math.Min(Math.Min(site.Remaining, yield), room);

        string hint;
        if (!can) hint = "The convoy has no mining gear or machine.";
        else if (site.Remaining <= 0) hint = "This claim is played out.";
        else if (room <= 0) hint = "The hold is full.";
        else hint = $"Waiting a day will extract about {expected:N0} {good?.Name ?? site.GoodId}.";

        return new SiteView(
            site.Id,
            $"{good?.Name ?? site.GoodId} deposit",
            site.GoodId,
            good?.Name ?? site.GoodId,
            site.Remaining,
            expected,
            can,
            hint);
    }

    private static FieldView BuildField(GameState state, WorldData world)
    {
        var cell = MapMath.Position(state, world);
        return new FieldView(cell.Id, cell.Biome, Math.Round(cell.X, 1), Math.Round(cell.Y, 1));
    }

    private static List<MiningSiteView> BuildMiningSites(GameState state, WorldData world)
    {
        var views = new List<MiningSiteView>(state.MiningSites.Count);
        foreach (var site in state.MiningSites)
        {
            var cell = world.Map[site.Col, site.Row];
            var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g.Name : site.GoodId;
            views.Add(new MiningSiteView(
                site.Id,
                $"{good} deposit",
                Math.Round(cell.X, 1),
                Math.Round(cell.Y, 1),
                site.Remaining,
                site.Remaining <= 0));
        }
        return views;
    }

}
