using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Source-project export helper: install temporarily in Assets/Editor only.
// Does not bake, save, or move props. Never install in the isolated query project.
[InitializeOnLoad]
internal static class DistrictCycleNavigationProbeSession
{
    [Serializable] private sealed class Point
    {
        public string scene, name, role;
        public Vector3 position, rootPosition;
        public bool movingEnemy;
    }
    [Serializable] private sealed class Snapshot
    {
        public string navAssetPath, navAssetGuid, navAssetHash, bakeVersion;
        public int agentTypeId;
        public Point[] points;
    }
    static DistrictCycleNavigationProbeSession() => EditorApplication.update += Poll;
    private static void Poll()
    {
        const string request = "Library/DistrictCycleValidation/NavigationProbe.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request);
        try { Export(); }
        catch (Exception error) { File.WriteAllText("Library/DistrictCycleValidation/NavigationProbe.error", error.ToString()); }
    }
    private static void Export()
    {
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
        if (manifest == null || manifest.BakedNavMeshData == null) throw new InvalidOperationException("District_1 manifest has no baked navigation data.");
        var snapshot = new Snapshot { navAssetPath = AssetDatabase.GetAssetPath(manifest.BakedNavMeshData),
            bakeVersion = manifest.BakedNavMeshVersion, agentTypeId = manifest.BakedNavMeshAgentTypeId };
        snapshot.navAssetGuid = AssetDatabase.AssetPathToGUID(snapshot.navAssetPath);
        using (var hash = SHA256.Create()) snapshot.navAssetHash = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(snapshot.navAssetPath))).Replace("-", "");
        var points = new List<Point>();
        foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith("District_1_Cycle_", StringComparison.Ordinal)))
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var visited = new HashSet<GameObject>();
                foreach (var component in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
                {
                    if (component == null) continue;
                    var enemy = component as EnemyController;
                    if (enemy == null && !(component is ICharacterDetectedInteractable) &&
                        !(component is GhostController) && !(component is CycleZoneTrigger) && !(component is SceneMarker marker && marker.UsesCharacter) &&
                        !component.GetType().Name.Contains("EntryTrigger")) continue;
                    if (!visited.Add(component.gameObject)) continue;
                    Vector3 position = component.transform.position;
                    if (enemy == null && component is ICharacterDetectedInteractable interactable)
                    {
                        Collider collider = interactable.GetInteractionDetectionCollider();
                        if (collider is BoxCollider box) position = box.transform.TransformPoint(box.center);
                        else if (collider is SphereCollider sphere) position = sphere.transform.TransformPoint(sphere.center);
                        else if (collider is CapsuleCollider capsule) position = capsule.transform.TransformPoint(capsule.center);
                        else if (collider is MeshCollider mesh && mesh.sharedMesh != null) position = mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center);
                        else if (interactable.GetInteractionAnchor() != null) position = interactable.GetInteractionAnchor().position;
                    }
                    points.Add(new Point { scene = scene.name, name = component.name, role = component.GetType().Name,
                        position = position, rootPosition = component.transform.position, movingEnemy = enemy != null &&
                            (enemy.BossEncounter == null || !enemy.BossEncounter.SuppressDefaultEnemyBrain) });
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        snapshot.points = points.ToArray();
        File.WriteAllText("Library/DistrictCycleValidation/NavigationProbe.json", JsonUtility.ToJson(snapshot, true));
    }
}
