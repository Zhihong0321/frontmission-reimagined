using MechaTrader.Core.Events;
using MechaTrader.Core.Model;
using MechaTrader.Core.Sim;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Commands;

public static partial class CommandProcessor
{
    private static CommandResult Depart(GameState state, WorldData world, DepartCommand cmd)
    {
        if (!MapMath.TryResolve(state, world, cmd.ToCityId, out var dest))
            return CommandResult.Fail($"No such destination '{cmd.ToCityId}'.");

        var fromCell = MapMath.Position(state, world);
        if (dest.Cell.Col == fromCell.Col && dest.Cell.Row == fromCell.Row)
        {
            if (state.Caravan.Travel is null)
                return CommandResult.Fail("The convoy is already there.");

            MapMath.Park(state, world, fromCell);
            return CommandResult.Success(Array.Empty<GameEvent>());
        }

        // From the convoy's exact position to the destination point. A map click
        // ("s<sc>,<sr>") may land just off walkable ground and gets a gentle snap;
        // a named cell, city or claim must be reached as-is.
        var (startX, startY) = MapMath.PositionPoint(state, world);
        var (endX, endY) = dest.Point;
        var snapEnd = cmd.ToCityId is { Length: > 1 } && cmd.ToCityId[0] == 's';

        var plan = MapMath.PathfindFine(state.Caravan, world, (startX, startY), (endX, endY), snapEnd);
        if (plan is null)
            return CommandResult.Fail($"No route the convoy can travel reaches {dest.Name}.");

        var from = DescribeHere(state, world);
        var reroute = state.Caravan.Travel is not null;

        state.Caravan.Travel = new TravelState
        {
            FromId = from.Id,
            ToId = dest.Id,
            FromKind = from.Kind,
            ToKind = dest.Kind,
            FromName = from.Name,
            ToName = dest.Name,
            TotalDays = plan.Days,
            DaysRemaining = plan.Days,
            KmPerDay = plan.DistanceKm / Math.Max(1, plan.Days),
            FuelPerDay = plan.Fuel / Math.Max(1, plan.Days),
            ToCellId = dest.Cell.Id,
            Waypoints = plan.Path.ToList()
        };
        state.Caravan.LocationId = null;
        state.Caravan.SiteId = null;
        state.Caravan.CellId = null;

        // The stall does not travel: leaving town takes every listing down.
        state.Caravan.ExpoAsks.Clear();

        if (dest.Kind == "cell")
            return CommandResult.Success(Array.Empty<GameEvent>());

        var verb = reroute ? "Rerouted toward" : "Departed for";
        return CommandResult.Success(new[]
        {
            new GameEvent(state.Day, GameEventKind.Travel,
                $"{verb} {dest.Name} via {plan.Layer}: " +
                $"{plan.DistanceKm:N0} km, {plan.Days} day(s), about {plan.Fuel:N0} cr of fuel.")
        });
    }

    private static MapDestination DescribeHere(GameState state, WorldData world)
    {
        if (state.Caravan.LocationId is { } cityId)
        {
            var city = world.City(cityId);
            return new MapDestination(city.Id, "city", city.Name, world.Map.CellOfCity(city.Id));
        }

        if (state.Caravan.SiteId is { } siteId && state.Site(siteId) is { } site)
        {
            var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g.Name : site.GoodId;
            return new MapDestination(site.Id, "site", $"{good} deposit", world.Map[site.Col, site.Row]);
        }

        var cell = MapMath.Position(state, world);
        return new MapDestination(cell.Id, "cell", "open country", cell);
    }

    private static CommandResult Wait(GameState state, WorldData world, WaitCommand cmd)
    {
        if (cmd.Days <= 0) return CommandResult.Fail("Days must be at least 1.");
        if (cmd.Days > 365) return CommandResult.Fail("Cannot skip more than a year at once.");

        var events = new List<GameEvent>();
        for (var i = 0; i < cmd.Days; i++)
        {
            DayTick.Advance(state, world, events);
        }

        return CommandResult.Success(events);
    }
}
