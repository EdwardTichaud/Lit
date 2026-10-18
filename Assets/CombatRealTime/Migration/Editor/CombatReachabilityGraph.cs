using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Incremental, cycle-safe dependency closure with a per-audit direct-edge cache.</summary>
public sealed class CombatReachabilityGraph
{
    private readonly Func<string, IEnumerable<string>> dependencies;
    private readonly Dictionary<string, string[]> cache = new Dictionary<string, string[]>(StringComparer.Ordinal);
    private Dictionary<string, string> routes;
    public readonly HashSet<string> Reachable = new HashSet<string>(StringComparer.Ordinal);
    public readonly Dictionary<string, List<string>> Roots = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    public int DependencyQueries { get; private set; }
    public int CacheHits { get; private set; }
    public int EdgeCount { get; private set; }

    public CombatReachabilityGraph(Func<string, IEnumerable<string>> dependencies) { this.dependencies = dependencies; }

    public string[] Direct(string path)
    {
        if (cache.TryGetValue(path, out var found)) { CacheHits++; return found; }
        DependencyQueries++;
        found = (dependencies(path) ?? Array.Empty<string>()).Where(value => !string.IsNullOrEmpty(value) && value != path)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        cache.Add(path, found); EdgeCount += found.Length;
        return found;
    }

    public void AddRoot(string path, string reason)
    {
        routes = null;
        if (!Roots.TryGetValue(path, out var reasons)) Roots[path] = reasons = new List<string>();
        if (!reasons.Contains(reason)) reasons.Add(reason);
        Expand(new[] { path });
    }

    public void Expand(IEnumerable<string> paths)
    {
        routes = null;
        var pending = new Queue<string>(paths);
        while (pending.Count > 0)
        {
            string path = pending.Dequeue();
            if (string.IsNullOrEmpty(path) || !Reachable.Add(path)) continue;
            foreach (string dependency in Direct(path)) pending.Enqueue(dependency);
        }
    }

    public string[] RouteTo(string target)
    {
        if (routes == null)
        {
            routes = new Dictionary<string, string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            foreach (string root in Roots.Keys.OrderBy(value => value, StringComparer.Ordinal)) { routes[root] = null; pending.Enqueue(root); }
            while (pending.Count > 0)
            {
                string path = pending.Dequeue();
                foreach (string next in Direct(path)) if (!routes.ContainsKey(next)) { routes[next] = path; pending.Enqueue(next); }
            }
        }
        if (!routes.ContainsKey(target)) return Array.Empty<string>();
        var route = new List<string>();
        for (string step = target; step != null; step = routes[step]) route.Add(step);
        route.Reverse(); return route.ToArray();
    }
}
