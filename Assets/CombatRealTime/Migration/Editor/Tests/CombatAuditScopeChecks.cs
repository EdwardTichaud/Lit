using System;
using System.Collections.Generic;
using System.Linq;

public static class CombatAuditScopeChecks
{
    public static IEnumerable<KeyValuePair<string, Action>> Cases()
    {
        yield return Case("Unreachable vendor payload never queried", () => {
            var queried = new List<string>();
            var graph = new CombatReachabilityGraph(path => { queried.Add(path); return path == "zone" ? new[] { "enemy" } : Array.Empty<string>(); });
            graph.AddRoot("zone", "manifest");
            Check(graph.Reachable.SetEquals(new[] { "zone", "enemy" }) && !queried.Contains("vendor-demo"));
        });
        yield return Case("Network and dynamic roots transitively join gameplay scope", () => {
            var edges = new Dictionary<string, string[]> { { "network-list", new[] { "registered-enemy" } }, { "resource", new[] { "character-data" } }, { "character-data", new[] { "actor-prefab" } } };
            var graph = new CombatReachabilityGraph(path => edges.TryGetValue(path, out var found) ? found : Array.Empty<string>());
            graph.AddRoot("network-list", "registered"); graph.AddRoot("resource", "runtime-load");
            Check(graph.Reachable.Contains("registered-enemy") && graph.Reachable.Contains("actor-prefab"));
        });
        yield return Case("Cycle and shared edge queried only once per audit", () => {
            var counts = new Dictionary<string, int>();
            var graph = new CombatReachabilityGraph(path => {
                counts[path] = counts.TryGetValue(path, out int count) ? count + 1 : 1;
                return path == "a" ? new[] { "b", "b" } : new[] { "a" };
            });
            graph.AddRoot("a", "zone"); graph.AddRoot("b", "dynamic"); graph.Direct("a");
            Check(counts.Values.All(count => count == 1) && graph.DependencyQueries == 2);
        });
        yield return Case("Authoring closure does not become production closure", () => {
            var production = new CombatReachabilityGraph(path => path == "lab" ? new[] { "lab-only-enemy" } : Array.Empty<string>());
            var authoring = new CombatReachabilityGraph(production.Direct);
            production.AddRoot("zone", "gameplay"); authoring.AddRoot("lab", "authoring");
            Check(authoring.Reachable.Contains("lab-only-enemy") && !production.Reachable.Contains("lab-only-enemy"));
        });
        yield return Case("Separate audit sessions do not reuse stale edges", () => {
            bool changed = false;
            Func<string, IEnumerable<string>> edges = path => path == "zone" ? new[] { changed ? "new-enemy" : "old-enemy" } : Array.Empty<string>();
            var first = new CombatReachabilityGraph(edges); first.AddRoot("zone", "manifest"); changed = true;
            var second = new CombatReachabilityGraph(edges); second.AddRoot("zone", "manifest");
            Check(first.Reachable.Contains("old-enemy") && second.Reachable.Contains("new-enemy") && !second.Reachable.Contains("old-enemy"));
        });
        yield return Case("Parent revision needs exact source fingerprint", () => {
            var export = Export(); Check(CombatRevisionProvenance.Validate(export, "snapshot") == null);
            Check(CombatRevisionProvenance.Validate(export, "changed") != null);
        });
        yield return Case("Parent revision cannot invent a Git object ID or verifier", () => {
            var export = Export(); export.revision = "unknown"; Check(CombatRevisionProvenance.Validate(export, "snapshot") != null);
            export = Export(); export.verifiedBy = ""; Check(CombatRevisionProvenance.Validate(export, "snapshot") != null);
        });
        yield return Case("Parent revision needs explicit export and timestamp", () => {
            var export = Export(); export.exportId = ""; Check(CombatRevisionProvenance.Validate(export, "snapshot") != null);
            export = Export(); export.verifiedUtc = DateTimeOffset.UtcNow.AddDays(1).ToString("O"); Check(CombatRevisionProvenance.Validate(export, "snapshot") != null);
        });
        yield return Case("Network gate requires remote full-gameplay checks, not compilation", () => {
            var checks = CombatReadinessEvidence.RequiredChecks("network");
            Check(new[] { "real-host-client", "remote-reactions", "remote-health-thresholds", "cinematic-replication", "unsupported-families-covered" }.All(checks.Contains));
        });
    }
    private static CombatRevisionProvenance.ParentExport Export() => new CombatRevisionProvenance.ParentExport {
        revision = new string('a', 40), fingerprint = "snapshot", verifiedBy = "SYNTHETIC-TEST", verifiedUtc = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
        sourceRepository = "synthetic-fixture", exportId = "SYNTHETIC-NOT-PRODUCTION-PROVENANCE"
    };
    private static KeyValuePair<string, Action> Case(string name, Action test) => new KeyValuePair<string, Action>(name, test);
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Audit scope/provenance fixture assertion failed."); }
}
