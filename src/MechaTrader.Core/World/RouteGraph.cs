namespace MechaTrader.Core.World;

/// <summary>Adjacency over the road network. Undirected.</summary>
public sealed class RouteGraph
{
    private readonly Dictionary<string, List<Route>> _adjacency = new();

    public IReadOnlyList<Route> All { get; }

    public RouteGraph(IReadOnlyList<Route> routes)
    {
        All = routes;
        foreach (var r in routes)
        {
            if (!_adjacency.TryGetValue(r.FromId, out var a)) _adjacency[r.FromId] = a = new List<Route>();
            a.Add(r);
            if (!_adjacency.TryGetValue(r.ToId, out var b)) _adjacency[r.ToId] = b = new List<Route>();
            b.Add(r);
        }
    }

    public IReadOnlyList<Route> From(string cityId)
        => _adjacency.TryGetValue(cityId, out var list) ? list : Array.Empty<Route>();

    public Route? Between(string a, string b)
    {
        foreach (var r in From(a))
            if (r.Other(a) == b) return r;
        return null;
    }

    public bool AreAdjacent(string a, string b) => Between(a, b) is not null;

    /// <summary>Cities reachable from a start, for connectivity validation.</summary>
    public HashSet<string> Reachable(string startId)
    {
        var seen = new HashSet<string> { startId };
        var queue = new Queue<string>();
        queue.Enqueue(startId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var r in From(current))
            {
                var next = r.Other(current);
                if (seen.Add(next)) queue.Enqueue(next);
            }
        }
        return seen;
    }
}
