using MechaTrader.Core.Model;
using MechaTrader.Core.State;
using MechaTrader.Core.World;

namespace MechaTrader.Core.Sim;

public sealed record TravelPlan(
    IReadOnlyList<TrackPoint> Path,
    double DistanceKm,
    int Days,
    double Fuel,
    string Layer);

public readonly record struct MapDestination(
    string Id,
    string Kind,
    string Name,
    TerrainCell Cell,
    double X = double.NaN,
    double Y = double.NaN)
{
    /// <summary>Exact destination point in world km; defaults to the cell centre.</summary>
    public (double X, double Y) Point
        => (double.IsNaN(X) ? Cell.X : X, double.IsNaN(Y) ? Cell.Y : Y);
}

/// <summary>
/// Geographic helpers: where the convoy is, where a destination is, and the path
/// between them. Pure over state plus content, like <see cref="CaravanMath"/>.
/// </summary>
public static class MapMath
{
    private static readonly (int dc, int dr)[] Neighbors =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (1, -1), (-1, 1), (-1, -1)
    };

    public static TerrainCell Position(GameState state, WorldData world)
    {
        if (state.Caravan.Travel is { } travel)
        {
            var (x, y) = TravelCoords(travel);
            return world.Map.At(x, y);
        }

        var caravan = state.Caravan;
        if (caravan.LocationId is { } cityId) return world.Map.CellOfCity(cityId);
        if (caravan.SiteId is { } siteId && state.Site(siteId) is { } site)
            return world.Map[site.Col, site.Row];
        if (caravan.CellId is { } cellId && world.Map.TryParseCellId(cellId, out var cell))
            return cell;
        return world.Map.CellOfCity(world.Config.StartCityId);
    }

    /// <summary>The convoy's exact position in world km: interpolated while on the road, else a cell centre.</summary>
    public static (double X, double Y) PositionPoint(GameState state, WorldData world)
    {
        if (state.Caravan.Travel is not null)
            return TravelCoords(state.Caravan.Travel);

        var cell = Position(state, world);
        return (cell.X, cell.Y);
    }

    /// <summary>Kilometre point of an in-progress journey, interpolated along the path.</summary>
    public static (double X, double Y) TravelCoords(TravelState travel)
    {
        if (travel.Waypoints.Count == 0) return (0, 0);
        var pts = new List<(double x, double y)>(travel.Waypoints.Count);
        foreach (var w in travel.Waypoints)
        {
            pts.Add((w.X, w.Y));
        }

        if (pts.Count == 0) return (0, 0);
        var done = travel.TotalDays <= 0 ? 1.0 : (travel.TotalDays - travel.DaysRemaining) / (double)travel.TotalDays;
        return Along(pts, done);
    }

    public static (double X, double Y) Along(IReadOnlyList<(double x, double y)> path, double t)
    {
        t = Math.Clamp(t, 0, 1);
        if (path.Count == 0) return (0, 0);
        if (path.Count == 1) return path[0];

        double total = 0;
        var lengths = new double[path.Count - 1];
        for (var i = 0; i < lengths.Length; i++)
        {
            var dx = path[i + 1].x - path[i].x;
            var dy = path[i + 1].y - path[i].y;
            lengths[i] = Math.Sqrt(dx * dx + dy * dy);
            total += lengths[i];
        }

        if (total <= 0) return path[0];

        var along = t * total;
        for (var i = 0; i < lengths.Length; i++)
        {
            if (along > lengths[i] && i < lengths.Length - 1)
            {
                along -= lengths[i];
                continue;
            }

            var u = lengths[i] <= 0 ? 1 : along / lengths[i];
            return (
                path[i].x + (path[i + 1].x - path[i].x) * u,
                path[i].y + (path[i + 1].y - path[i].y) * u);
        }

        return path[^1];
    }

    /// <summary>Park the convoy on this cell. A city or claim that occupies it wins.</summary>
    public static void Park(GameState state, WorldData world, TerrainCell cell)
    {
        state.Caravan.Travel = null;
        state.Caravan.LocationId = null;
        state.Caravan.SiteId = null;
        state.Caravan.CellId = null;

        foreach (var kv in world.Map.CityCells)
        {
            if (kv.Value.Col == cell.Col && kv.Value.Row == cell.Row)
            {
                state.Caravan.LocationId = kv.Key;
                return;
            }
        }

        foreach (var claim in state.MiningSites)
        {
            if (claim.Col == cell.Col && claim.Row == cell.Row)
            {
                state.Caravan.SiteId = claim.Id;
                return;
            }
        }

        state.Caravan.CellId = cell.Id;
    }

    public static bool TryResolve(GameState state, WorldData world, string id, out MapDestination dest)
    {
        dest = default;
        if (world.CitiesById.TryGetValue(id, out var city))
        {
            dest = new MapDestination(city.Id, "city", city.Name, world.Map.CellOfCity(city.Id));
            return true;
        }

        if (state.Site(id) is { } site && world.Map.InBounds(site.Col, site.Row))
        {
            var good = world.GoodsById.TryGetValue(site.GoodId, out var g) ? g.Name : site.GoodId;
            dest = new MapDestination(site.Id, "site", $"{good} deposit", world.Map[site.Col, site.Row]);
            return true;
        }

        if (world.Map.TryParseCellId(id, out var cell))
        {
            foreach (var kv in world.Map.CityCells)
            {
                if (kv.Value.Col == cell.Col && kv.Value.Row == cell.Row)
                    return TryResolve(state, world, kv.Key, out dest);
            }

            foreach (var claim in state.MiningSites)
            {
                if (claim.Col == cell.Col && claim.Row == cell.Row)
                    return TryResolve(state, world, claim.Id, out dest);
            }

            dest = new MapDestination(cell.Id, "cell", "open country", cell);
            return true;
        }

        // Sub-cell ids ("s<sc>,<sr>") are the fine-grained "click that spot" destinations
        // the chart sends when the player picks a point on the map. The parent cell is
        // what the convoy parks on; the point is where it actually drives to.
        if (TryParseSubCellId(id, out var sc, out var sr) && world.Map.SubInBounds(sc, sr))
        {
            var parent = world.Map.ParentCell(sc, sr);
            var (x, y) = world.Map.SubCenter(sc, sr);
            dest = new MapDestination(id, "cell", "open country", parent, x, y);
            return true;
        }

        return false;
    }

    private static bool TryParseSubCellId(string id, out int sc, out int sr)
    {
        sc = sr = 0;
        if (id.Length < 2 || id[0] != 's') return false;
        var comma = id.IndexOf(',');
        if (comma <= 1 || comma == id.Length - 1) return false;
        if (!int.TryParse(id.AsSpan(1, comma - 1), out sc)) return false;
        if (!int.TryParse(id.AsSpan(comma + 1), out sr)) return false;
        return true;
    }

    public static TravelPlan? Pathfind(CaravanState caravan, WorldData world, TerrainCell from, TerrainCell to)
    {
        foreach (var layer in VehicleCapability.Layers)
        {
            if (!CaravanMath.CanTravel(caravan, world, layer)) continue;
            var plan = PathfindLayer(caravan, world, from, to, layer);
            if (plan is not null) return plan;
        }

        return null;
    }

    /// <summary>
    /// High-definition routing for actual travel: A* on the sub-cell grid, then
    /// string-pulled and densified so the drawn route and the moving convoy look smooth.
    /// The destination snaps to walkable ground only when <paramref name="snapEnd"/> —
    /// clicking a spot on the map may land in a non-walkable sub-cell, but a named
    /// cell, city or claim must be reachable as-is.
    /// </summary>
    public static TravelPlan? PathfindFine(
        CaravanState caravan, WorldData world,
        (double X, double Y) start, (double X, double Y) end, bool snapEnd)
    {
        foreach (var layer in VehicleCapability.Layers)
        {
            if (!CaravanMath.CanTravel(caravan, world, layer)) continue;
            var plan = PathfindLayerFine(caravan, world, start, end, snapEnd, layer);
            if (plan is not null) return plan;
        }

        return null;
    }

    private static TravelPlan? PathfindLayerFine(
        CaravanState caravan, WorldData world,
        (double X, double Y) start, (double X, double Y) end, bool snapEnd, string layer)
    {
        var map = world.Map;
        var convoySpeed = CaravanMath.SpeedKmPerDay(caravan, world);
        if (convoySpeed <= 0) return null;

        var (s0, t0) = map.SubCellAt(start.X, start.Y);
        var (g0, g1) = map.SubCellAt(end.X, end.Y);

        var startNode = map.NearestWalkableSubCell(s0, t0, layer, 2 * WorldMap.SubDiv);
        (int sc, int sr)? endNode;
        if (snapEnd)
            endNode = map.NearestWalkableSubCell(g0, g1, layer, 2 * WorldMap.SubDiv);
        else
            endNode = map.SubWalkable(g0, g1, layer) ? (g0, g1) : null;

        if (startNode is not { } st || endNode is not { } gl) return null;

        var (sx, sy) = st;
        var (gx, gy) = gl;

        if (sx == gx && sy == gy)
        {
            var raw = new List<(double x, double y)> { (start.X, start.Y), (end.X, end.Y) };
            return BuildPlanFine(caravan, world, raw, layer, convoySpeed);
        }

        var open = new PriorityQueue<(int sc, int sr), double>();
        var came = new Dictionary<(int, int), (int, int)>();
        var cost = new Dictionary<(int, int), double> { [st] = 0 };
        var closed = new HashSet<(int, int)>();
        open.Enqueue(st, SubDist(map, st, gl) / convoySpeed);

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (!closed.Add(current)) continue;
            if (current == gl)
            {
                var nodes = Backtrace(came, st, gl);
                var raw = NodesToPoints(map, nodes, start, end);
                var smoothed = Smooth(map, raw, layer, convoySpeed);
                var densified = Densify(smoothed, map);
                return BuildPlanFine(caravan, world, densified, layer, convoySpeed);
            }

            var (cc, cr) = current;
            var (cx, cy) = map.SubCenter(cc, cr);
            foreach (var (dc, dr) in Neighbors)
            {
                var nc = cc + dc;
                var nr = cr + dr;
                if (!map.SubInBounds(nc, nr)) continue;
                if (!map.SubWalkable(nc, nr, layer)) continue;

                var (bx, by) = map.SubCenter(nc, nr);
                var dist = Math.Sqrt((bx - cx) * (bx - cx) + (by - cy) * (by - cy));
                var speed = convoySpeed * map.SubSpeed(nc, nr);
                if (speed <= 0) continue;
                var step = dist / speed;
                var tentative = cost[current] + step;
                var key = (nc, nr);
                if (cost.TryGetValue(key, out var known) && tentative >= known) continue;

                cost[key] = tentative;
                came[key] = current;
                var h = SubDist(map, (nc, nr), gl) / convoySpeed;
                open.Enqueue(key, tentative + h);
            }
        }

        return null;
    }

    private static List<(int, int)> Backtrace(
        Dictionary<(int, int), (int, int)> came, (int, int) start, (int, int) goal)
    {
        var nodes = new List<(int, int)> { goal };
        var cursor = goal;
        while (cursor != start)
        {
            cursor = came[cursor];
            nodes.Add(cursor);
        }
        nodes.Reverse();
        return nodes;
    }

    private static List<(double x, double y)> NodesToPoints(
        WorldMap map, List<(int sc, int sr)> nodes,
        (double X, double Y) start, (double X, double Y) end)
    {
        var pts = new List<(double x, double y)> { (start.X, start.Y) };
        foreach (var (sc, sr) in nodes)
        {
            var (x, y) = map.SubCenter(sc, sr);
            var last = pts[^1];
            if (Math.Abs(x - last.x) > 1e-6 || Math.Abs(y - last.y) > 1e-6)
                pts.Add((x, y));
        }
        var tail = pts[^1];
        if (Math.Abs(end.X - tail.x) > 1e-6 || Math.Abs(end.Y - tail.y) > 1e-6)
            pts.Add((end.X, end.Y));
        return pts;
    }

    /// <summary>
    /// String-pulling: from each anchor, push a chord as far as it stays walkable and
    /// does not cost more time than the sub-path it replaces. The time budget stops the
    /// smoother from cutting a fast road corner across slow off-road terrain.
    /// </summary>
    private static List<(double x, double y)> Smooth(
        WorldMap map, List<(double x, double y)> pts, string layer, double convoySpeed)
    {
        if (pts.Count <= 2) return pts;

        var result = new List<(double x, double y)> { pts[0] };
        var anchor = 0;
        while (anchor < pts.Count - 1)
        {
            var best = anchor + 1;
            for (var probe = anchor + 2; probe < pts.Count; probe++)
            {
                if (!ChordWalkable(map, pts[anchor], pts[probe], layer)) break;
                if (!ChordWithinBudget(map, pts, anchor, probe, layer, convoySpeed)) break;
                best = probe;
            }
            result.Add(pts[best]);
            anchor = best;
        }
        return result;
    }

    private static bool ChordWalkable(
        WorldMap map, (double x, double y) a, (double x, double y) b, string layer)
    {
        var dx = b.x - a.x;
        var dy = b.y - a.y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len <= 1e-9) return true;

        var step = map.SubStep / 2;
        var n = Math.Max(1, (int)Math.Ceiling(len / step));
        for (var i = 1; i <= n; i++)
        {
            var u = (double)i / n;
            var (sc, sr) = map.SubCellAt(a.x + dx * u, a.y + dy * u);
            if (!map.SubWalkable(sc, sr, layer)) return false;
        }
        return true;
    }

    private static bool ChordWithinBudget(
        WorldMap map, List<(double x, double y)> pts, int a, int b, string layer, double convoySpeed)
    {
        var chord = SegmentTime(map, pts[a], pts[b], layer, convoySpeed);
        double replaced = 0;
        for (var i = a; i < b; i++)
            replaced += SegmentTime(map, pts[i], pts[i + 1], layer, convoySpeed);
        return chord <= replaced * 1.05;
    }

    private static double SegmentTime(
        WorldMap map, (double x, double y) a, (double x, double y) b, string layer, double convoySpeed)
    {
        var dx = b.x - a.x;
        var dy = b.y - a.y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len <= 1e-9) return 0;

        var step = map.SubStep / 2;
        var n = Math.Max(1, (int)Math.Ceiling(len / step));
        var seg = len / n;
        var time = 0.0;
        for (var i = 0; i < n; i++)
        {
            var u = ((double)i + 0.5) / n;
            var (sc, sr) = map.SubCellAt(a.x + dx * u, a.y + dy * u);
            var speed = convoySpeed * map.SubSpeed(sc, sr);
            if (speed > 1e-9) time += seg / speed;
        }
        return time;
    }

    /// <summary>Resample a polyline at a fixed step so the front-end's interpolation is smooth.</summary>
    private static List<(double x, double y)> Densify(List<(double x, double y)> pts, WorldMap map)
    {
        if (pts.Count < 2) return pts;

        var step = Math.Min(10.0, map.CellKm / 4);
        var pos = new double[pts.Count];
        pos[0] = 0;
        for (var i = 1; i < pts.Count; i++)
            pos[i] = pos[i - 1] + PtDist(pts[i - 1], pts[i]);
        var total = pos[^1];
        if (total <= step) return pts;

        var result = new List<(double x, double y)> { pts[0] };
        var target = step;
        var idx = 1;
        while (target < total - 1e-9)
        {
            while (idx < pts.Count - 1 && pos[idx] < target) idx++;
            var segLen = pos[idx] - pos[idx - 1];
            if (segLen <= 1e-9)
            {
                target += step;
                continue;
            }
            var u = (target - pos[idx - 1]) / segLen;
            var (ax, ay) = pts[idx - 1];
            var (bx, by) = pts[idx];
            result.Add((ax + (bx - ax) * u, ay + (by - ay) * u));
            target += step;
        }

        var tail = result[^1];
        var final = pts[^1];
        if (Math.Abs(tail.x - final.x) > 1e-6 || Math.Abs(tail.y - final.y) > 1e-6)
            result.Add(final);
        return result;
    }

    /// <summary>Total distance, time and fuel along the actual drawn polyline.</summary>
    private static TravelPlan BuildPlanFine(
        CaravanState caravan, WorldData world,
        List<(double x, double y)> pts, string layer, double convoySpeed)
    {
        var map = world.Map;
        double distance = 0;
        double time = 0;
        double fuel = 0;
        var fuelPerKm = CaravanMath.FuelPerKm(caravan, world);
        var running = CrewMath.RunningCostMultiplier(caravan, world);
        var step = map.SubStep / 2;

        for (var i = 1; i < pts.Count; i++)
        {
            var (ax, ay) = pts[i - 1];
            var (bx, by) = pts[i];
            var dx = bx - ax;
            var dy = by - ay;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len <= 1e-9) continue;
            distance += len;

            var n = Math.Max(1, (int)Math.Ceiling(len / step));
            var seg = len / n;
            for (var k = 0; k < n; k++)
            {
                var u = ((double)k + 0.5) / n;
                var (sc, sr) = map.SubCellAt(ax + dx * u, ay + dy * u);
                var speed = convoySpeed * map.SubSpeed(sc, sr);
                if (speed > 1e-9) time += seg / speed;
                fuel += seg * fuelPerKm * map.SubCost(sc, sr) * running;
            }
        }

        var days = Math.Max(1, (int)Math.Ceiling(time));
        var waypoints = pts
            .Select(p => new TrackPoint { X = Math.Round(p.x, 3), Y = Math.Round(p.y, 3) })
            .ToList();
        return new TravelPlan(waypoints, distance, days, fuel, layer);
    }

    private static double SubDist(WorldMap map, (int sc, int sr) a, (int sc, int sr) b)
    {
        var (ax, ay) = map.SubCenter(a.sc, a.sr);
        var (bx, by) = map.SubCenter(b.sc, b.sr);
        var dx = bx - ax;
        var dy = by - ay;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double PtDist((double x, double y) a, (double x, double y) b)
    {
        var dx = b.x - a.x;
        var dy = b.y - a.y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static List<MiningSite> PlaceDeposits(WorldData world, ulong seed)
    {
        var cfg = world.Map.Mining;
        var sites = new List<MiningSite>();
        if (cfg.SpotCount <= 0) return sites;

        var rng = new Rng(seed ^ 0x4D494E454D494E45UL);
        var cityCells = new HashSet<(int, int)>();
        foreach (var cell in world.Map.CityCells.Values)
            cityCells.Add((cell.Col, cell.Row));

        var preferred = new List<TerrainCell>();
        var fallback = new List<TerrainCell>();

        foreach (var cell in world.Map.Cells)
        {
            if (!cell.Land || cityCells.Contains((cell.Col, cell.Row))) continue;
            fallback.Add(cell);
            if (cell.Biome == MapBiome.Hill || TouchesMountain(world.Map, cell))
                preferred.Add(cell);
        }

        var pool = preferred.Count > 0 ? preferred : fallback;
        if (pool.Count == 0) return sites;

        Shuffle(pool, rng);
        var take = Math.Min(cfg.SpotCount, pool.Count);
        var span = Math.Max(0.0, cfg.ReserveMax - cfg.ReserveMin);

        for (var i = 0; i < take; i++)
        {
            var cell = pool[i];
            var reserve = cfg.ReserveMin + rng.NextDouble() * span;
            sites.Add(new MiningSite
            {
                Id = $"mine-{i}",
                Col = cell.Col,
                Row = cell.Row,
                GoodId = cfg.GoodId,
                Remaining = Math.Round(reserve)
            });
        }

        return sites;
    }

    public static int Extract(GameState state, WorldData world, List<Events.GameEvent> events)
    {
        var siteId = state.Caravan.SiteId;
        if (siteId is null) return 0;
        var site = state.Site(siteId);
        if (site is null) return 0;

        var yield = CaravanMath.MineYield(state.Caravan, world);
        if (yield <= 0 || site.Remaining <= 0) return 0;
        if (!world.GoodsById.TryGetValue(site.GoodId, out var good)) return 0;

        var free = CaravanMath.FreeVolume(state.Caravan, world);
        var room = good.UnitVolume <= 0 ? 0 : (int)Math.Floor(free / good.UnitVolume);
        var units = (int)Math.Min(Math.Min(site.Remaining, yield), room);
        if (units <= 0) return 0;

        site.Remaining = Math.Max(0, site.Remaining - units);

        if (!state.Caravan.Cargo.TryGetValue(good.Id, out var lot))
            state.Caravan.Cargo[good.Id] = lot = new CargoLot();
        lot.Add(units, 0, world.Quality.Nominal);

        events.Add(new Events.GameEvent(state.Day, Events.GameEventKind.Mine,
            $"Extracted {units:N0} {good.Name} from the claim ({site.Remaining:N0} remaining)."));
        return units;
    }

    private static TravelPlan? PathfindLayer(
        CaravanState caravan, WorldData world, TerrainCell from, TerrainCell to, string layer)
    {
        var map = world.Map;
        if (!from.Walkable(layer) || !to.Walkable(layer)) return null;

        var start = (from.Col, from.Row);
        var goal = (to.Col, to.Row);
        if (start == goal)
            return new TravelPlan(
                new[] { new TrackPoint { X = from.X, Y = from.Y } }, 0, 1, 0, layer);

        var convoySpeed = CaravanMath.SpeedKmPerDay(caravan, world);
        if (convoySpeed <= 0) return null;

        var open = new PriorityQueue<(int col, int row), double>();
        var came = new Dictionary<(int, int), (int, int)>();
        var cost = new Dictionary<(int, int), double> { [start] = 0 };
        var closed = new HashSet<(int, int)>();
        open.Enqueue(start, Heuristic(from, to) / convoySpeed);

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (!closed.Add(current)) continue;
            if (current == goal) return BuildPlan(caravan, world, came, from, to, layer, convoySpeed);

            var here = map[current.col, current.row];
            foreach (var (dc, dr) in Neighbors)
            {
                var nc = current.col + dc;
                var nr = current.row + dr;
                if (!map.InBounds(nc, nr)) continue;
                var next = map[nc, nr];
                if (!next.Walkable(layer)) continue;

                var dist = Distance(here, next);
                var speed = convoySpeed * map.SpeedMultiplier(next);
                if (speed <= 0) continue;
                var step = dist / speed;
                var tentative = cost[current] + step;
                var key = (nc, nr);
                if (cost.TryGetValue(key, out var known) && tentative >= known) continue;

                cost[key] = tentative;
                came[key] = current;
                var h = Heuristic(next, to) / (convoySpeed * 1.0);
                open.Enqueue(key, tentative + h);
            }
        }

        return null;
    }

    private static TravelPlan BuildPlan(
        CaravanState caravan, WorldData world,
        Dictionary<(int, int), (int, int)> came,
        TerrainCell from, TerrainCell to, string layer, double convoySpeed)
    {
        var map = world.Map;
        var cells = new List<TerrainCell>();
        var cursor = (to.Col, to.Row);
        cells.Add(to);
        while (cursor != (from.Col, from.Row))
        {
            cursor = came[cursor];
            cells.Add(map[cursor.Item1, cursor.Item2]);
        }
        cells.Reverse();

        double distance = 0;
        double time = 0;
        double fuel = 0;
        var fuelPerKm = CaravanMath.FuelPerKm(caravan, world);
        var running = CrewMath.RunningCostMultiplier(caravan, world);

        for (var i = 1; i < cells.Count; i++)
        {
            var a = cells[i - 1];
            var b = cells[i];
            var dist = Distance(a, b);
            var speed = convoySpeed * map.SpeedMultiplier(b);
            distance += dist;
            time += dist / Math.Max(speed, 1e-9);
            fuel += dist * fuelPerKm * map.CostMultiplier(b) * running;
        }

        var days = Math.Max(1, (int)Math.Ceiling(time));
        var waypoints = cells
            .Select(c => new TrackPoint { X = Math.Round(c.X, 3), Y = Math.Round(c.Y, 3) })
            .ToList();
        return new TravelPlan(waypoints, distance, days, fuel, layer);
    }

    private static double Distance(TerrainCell a, TerrainCell b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Heuristic(TerrainCell a, TerrainCell b) => Distance(a, b);

    private static bool TouchesMountain(WorldMap map, TerrainCell cell)
    {
        foreach (var (dc, dr) in Neighbors)
        {
            var n = map.TryGet(cell.Col + dc, cell.Row + dr);
            if (n is { Biome: MapBiome.Mountain }) return true;
        }
        return false;
    }

    private static void Shuffle<T>(IList<T> items, Rng rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
