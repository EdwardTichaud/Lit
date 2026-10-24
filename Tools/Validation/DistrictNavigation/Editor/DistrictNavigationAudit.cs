using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Validation-only: run in the marked disposable code project, never in Lit.
public static class DistrictNavigationAudit
{
    [Serializable] private sealed class Point
    {
        public string scene, name, role;
        public Vector3 position;
        public bool movingEnemy;
    }
    [Serializable] private sealed class Snapshot
    {
        public string navAssetPath, navAssetGuid, navAssetHash, bakeVersion;
        public int agentTypeId;
        public Point[] points;
    }
    [Serializable] private sealed class Sample
    {
        public Point source;
        public bool found, withinMargin;
        public Vector3 sampled;
        public float horizontal, vertical;
    }
    [Serializable] private sealed class Route
    {
        public string scene, from, to, status;
        public float length;
    }
    [Serializable] private sealed class Report
    {
        public string navAssetPath, navAssetHash, bakeVersion;
        public int vertices, triangles, agentTypeId;
        public Vector3 minimum, maximum;
        public Sample[] samples;
        public Route[] routes;
    }
    public static void Run()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (Path.GetFileName(project) != "DistrictCycleRuntimeProject" || !File.Exists(Path.Combine(project, "CycleSmokeValidationProject.marker")))
            throw new InvalidOperationException("Navigation audit requires the marked disposable project.");
        string[] args = Environment.GetCommandLineArgs();
        string input = args[Array.IndexOf(args, "-navSnapshot") + 1];
        string output = args[Array.IndexOf(args, "-navReport") + 1];
        var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(input));
        if (snapshot == null || snapshot.points == null || snapshot.agentTypeId < 0) throw new InvalidOperationException("Invalid navigation snapshot.");
        if (AssetDatabase.AssetPathToGUID(snapshot.navAssetPath) != snapshot.navAssetGuid) throw new InvalidOperationException("Navigation asset GUID differs from the selected manifest.");
        string actualHash;
        using (var hash = SHA256.Create()) actualHash = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(snapshot.navAssetPath))).Replace("-", "");
        if (actualHash != snapshot.navAssetHash) throw new InvalidOperationException("Navigation asset bytes changed since coordinate export.");
        if (NavMesh.CalculateTriangulation().vertices.Length != 0) throw new InvalidOperationException("An unrelated NavMesh is already registered; refusing ambiguous results.");
        var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(snapshot.navAssetPath);
        if (data == null) throw new InvalidOperationException("Selected baked data failed to load.");
        var instance = NavMesh.AddNavMeshData(data, Vector3.zero, Quaternion.identity);
        if (!instance.valid) throw new InvalidOperationException("Selected data could not be registered.");
        try
        {
            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices.Length == 0) throw new InvalidOperationException("Selected navigation data contains no vertices.");
            var bounds = new Bounds(triangulation.vertices[0], Vector3.zero);
            foreach (var vertex in triangulation.vertices) bounds.Encapsulate(vertex);
            var filter = new NavMeshQueryFilter { agentTypeID = snapshot.agentTypeId, areaMask = NavMesh.AllAreas };
            var samples = new List<Sample>(); var routes = new List<Route>();
            foreach (var point in snapshot.points)
            {
                var sample = new Sample { source = point };
                // Interaction pivots often lie on a table. Prefer the ground
                // approach instead of accidentally selecting its isolated top.
                Vector3 origin = point.movingEnemy ? point.position : point.position - Vector3.up;
                sample.found = NavMesh.SamplePosition(origin, out var hit, point.movingEnemy ? 1f : 3f, filter);
                if (sample.found)
                {
                    sample.sampled = hit.position;
                    sample.vertical = Mathf.Abs(hit.position.y - point.position.y);
                    sample.horizontal = Vector2.Distance(new Vector2(hit.position.x, hit.position.z), new Vector2(point.position.x, point.position.z));
                    sample.withinMargin = sample.vertical <= (point.movingEnemy ? .5f : 2f) && sample.horizontal <= (point.movingEnemy ? .5f : 2f);
                }
                samples.Add(sample);
            }
            foreach (var group in samples.GroupBy(sample => sample.source.scene))
            {
                var anchor = group.FirstOrDefault(sample => sample.source.movingEnemy && sample.withinMargin) ?? group.FirstOrDefault(sample => sample.withinMargin);
                if (anchor == null) continue;
                foreach (var destination in group.Where(sample => sample != anchor && sample.found))
                {
                    var path = new NavMeshPath();
                    bool calculated = NavMesh.CalculatePath(anchor.sampled, destination.sampled, filter, path);
                    float length = 0;
                    for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    routes.Add(new Route { scene = group.Key, from = anchor.source.name, to = destination.source.name,
                        status = calculated ? path.status.ToString() : "NoPath", length = length });
                }
            }
            var report = new Report { navAssetPath = snapshot.navAssetPath, navAssetHash = actualHash, bakeVersion = snapshot.bakeVersion,
                agentTypeId = snapshot.agentTypeId, vertices = triangulation.vertices.Length, triangles = triangulation.indices.Length / 3,
                minimum = bounds.min, maximum = bounds.max, samples = samples.ToArray(), routes = routes.ToArray() };
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            int warnings = samples.Count(sample => !sample.withinMargin) + routes.Count(route => route.status != "PathComplete");
            Debug.Log($"Navigation audit: {samples.Count} samples, {routes.Count} routes, {warnings} items to inspect. This is baked-data coverage, not a physics/player walkthrough.");
        }
        finally { instance.Remove(); }
    }
}
