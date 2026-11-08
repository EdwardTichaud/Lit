using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;

namespace Lit.Fractures.Editor
{
    public static class FractureDistrictAuthoring
    {
        public const string ManifestPath = "Assets/Scenes/Maison/ZoneManifest_District_1.asset";
        public const string ScenePath = "Assets/Scenes/District_1/District_1_Fracture_Environment.unity";
        public const string PuitsPath = "Assets/Scenes/District_1/District_1_PuitsDeLaReleve_Environment.unity";
        public static readonly Vector3 EntrancePosition = new Vector3(-92.98f, -93.84f, 61.02f);

        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Scène utilisateur non sauvegardée.");
            var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
            if (EditorUtility.IsDirty(manifest)) throw new InvalidOperationException("Manifeste non sauvegardé.");
            try
            {
                bool create = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null;
                if (create && !AssetDatabase.CopyAsset(FractureGrayboxAuthoring.ScenePath, ScenePath)) throw new InvalidOperationException("Copie du labyrinthe impossible.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var lab = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FractureGraybox>()).Single();
                bool relocate = false;
                if (create)
                {
                    lab.districtIntegration = true;
                    lab.name = "District_1_Fracture_Archives";
                    UnityEngine.Object.DestroyImmediate(lab.previewCamera.gameObject); lab.previewCamera = null;
                    UnityEngine.Object.DestroyImmediate(lab.spawn.gameObject); lab.spawn = null; lab.lucianPrefab = null;
                    foreach (var child in lab.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    foreach (var child in lab.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true)) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    foreach (var child in lab.GetComponentsInChildren<Flame>(true)) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    // One opening in the original start room; keep the two lateral walls.
                    foreach (var wall in lab.GetComponentsInChildren<BoxCollider>().Where(c => c.name == "Wall" &&
                        Mathf.Abs(c.transform.position.z + 30) < .05f && Mathf.Abs(c.transform.position.x) < .1f).ToArray())
                        UnityEngine.Object.DestroyImmediate(wall.gameObject);
                    lab.transform.rotation = Quaternion.Euler(0, 270, 0);
                    lab.transform.position = EntrancePosition - lab.transform.TransformVector(new Vector3(0, 0, -30));
                    var entrance = new GameObject("Entree_revelee_par_la_lumiere"); entrance.transform.SetParent(lab.transform, true);
                    entrance.transform.SetPositionAndRotation(EntrancePosition + Vector3.up * 1.5f, lab.transform.rotation);
                    var probe = entrance.AddComponent<SphereCollider>(); probe.isTrigger = true; probe.radius = .15f;
                    var reveal = entrance.AddComponent<FractureLightEntrance>(); reveal.lightProbe = probe;
                    reveal.concealedWall = Volume(entrance.transform, "Paroi_camouflee", EntrancePosition + Vector3.up * 2, new Vector3(.25f, 4, 3.8f));
                    reveal.revealedFrame = new GameObject("Encadrement_revele"); reveal.revealedFrame.transform.SetParent(entrance.transform, false);
                    FractureDungeonArt.Fit("Prefabs/Doorway_Pillar_01 Variant.prefab", reveal.revealedFrame.transform,
                        new Bounds(EntrancePosition + Vector3.up * 2, new Vector3(.5f, 4, 4.2f)), 0);
                    var step = Volume(lab.transform, "Raccord_sol_District_1", EntrancePosition + new Vector3(.45f, -.1f, 0), new Vector3(1.6f, .2f, 3.8f));
                    step.GetComponent<Renderer>().enabled = false;
                    FractureDungeonArt.Fit("Prefabs/Floor_Flagstone_01 Variant.prefab", step.transform,
                        new Bounds(EntrancePosition + new Vector3(.45f, -.1f, 0), new Vector3(1.6f, .2f, 3.8f)), 0);
                    foreach (var gate in new[] { lab.startGate, lab.galleryGate, reveal.concealedWall })
                    {
                        var box = gate.GetComponent<BoxCollider>();
                        var obstacle = gate.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
                        obstacle.center = box.center; obstacle.size = box.size; obstacle.carving = true; obstacle.carveOnlyStationary = false;
                    }
                    EditorSceneManager.SaveScene(scene);
                }
                else
                {
                    var threshold = lab.GetComponentInChildren<FractureLightEntrance>();
                    var delta = EntrancePosition - (threshold.transform.position - Vector3.up * 1.5f);
                    if (delta.sqrMagnitude > .001f)
                    { lab.transform.position += delta; relocate = true; EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                }
                var puits = EditorSceneManager.OpenScene(PuitsPath, OpenSceneMode.Additive);
                if (create || relocate) OpenRailing(puits);
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                if (!manifest.loadingScenes.Contains(sceneAsset)) manifest.loadingScenes.Add(sceneAsset);
                var serialized = new SerializedObject(manifest);
                var names = serialized.FindProperty("loadingSceneNames");
                if (!Enumerable.Range(0, names.arraySize).Any(i => names.GetArrayElementAtIndex(i).stringValue == scene.name))
                { names.InsertArrayElementAtIndex(names.arraySize); names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = scene.name; }
                serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(manifest);
                if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                    EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
                if (create || relocate) BakeNavigation(manifest);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static GameObject Volume(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            root.transform.position = center; root.transform.SetParent(parent, true);
            root.GetComponent<MeshFilter>().sharedMesh = FractureDungeonArt.SolidWallMesh(size);
            root.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FractureGrayboxAuthoring.Folder + "/SolidWall_Stone.mat");
            root.GetComponent<BoxCollider>().size = size;
            return root;
        }
        private static void OpenRailing(Scene scene)
        {
            var rail = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider>()).Single(c =>
                c.name == "SafetyCollision" && Vector3.Distance(c.transform.TransformPoint(c.center), new Vector3(-92.9f, -93.22f, 58.52f)) < .1f);
            var bounds = new Bounds(rail.transform.TransformPoint(rail.center), Vector3.Scale(rail.size, rail.transform.lossyScale)); rail.enabled = false;
            foreach (var old in rail.transform.parent.GetComponentsInChildren<BoxCollider>().Where(c => c.name.StartsWith("SafetyCollision_Fracture_")).ToArray())
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach (var renderer in rail.transform.parent.GetComponentsInChildren<Renderer>())
                if (Mathf.Abs(renderer.bounds.center.x + 92.9f) < .12f && renderer.bounds.center.z > 51 && renderer.bounds.center.z < 56.04f &&
                    renderer.bounds.size.z < 3 && renderer.bounds.center.y > -93.8f) renderer.enabled = true;
            foreach (var range in new[] { new Vector2(bounds.min.z, 58.52f), new Vector2(63.52f, bounds.max.z) })
            {
                var segment = new GameObject("SafetyCollision_Fracture_" + range.x, typeof(BoxCollider));
                segment.transform.position = new Vector3(bounds.center.x, bounds.center.y, (range.x + range.y) * .5f);
                segment.transform.SetParent(rail.transform.parent, true);
                segment.GetComponent<BoxCollider>().size = new Vector3(bounds.size.x, bounds.size.y, range.y - range.x);
            }
            foreach (var renderer in rail.transform.parent.GetComponentsInChildren<Renderer>())
                if (Mathf.Abs(renderer.bounds.center.x + 92.9f) < .12f && renderer.bounds.center.z > 58.53f && renderer.bounds.center.z < 63.52f &&
                    renderer.bounds.size.z < 3 && renderer.bounds.center.y > -93.8f) renderer.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        private static void BakeNavigation(ZoneManifest manifest)
        {
            var corePath = "Assets/Scenes/District_1/District_1_Core.unity";
            if (!SceneManager.GetSceneByPath(corePath).isLoaded) EditorSceneManager.OpenScene(corePath, OpenSceneMode.Additive);
            foreach (var reference in manifest.loadingScenes)
            {
                string path = AssetDatabase.GetAssetPath(reference);
                if (!SceneManager.GetSceneByPath(path).isLoaded) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            var lab = FractureGraybox.District;
            var blockers = new[] { lab.startGate.GetComponent<BoxCollider>(), lab.galleryGate.GetComponent<BoxCollider>(),
                lab.GetComponentInChildren<FractureLightEntrance>().concealedWall.GetComponent<BoxCollider>() };
            var obstacles = lab.GetComponentsInChildren<NavMeshObstacle>().ToArray();
            foreach (var b in blockers) b.enabled = false;
            foreach (var o in obstacles) o.enabled = false;
            Physics.SyncTransforms();
            var host = new GameObject("__FractureDistrictBake"); var surface = host.AddComponent<NavMeshSurface>();
            try
            {
                surface.collectObjects = CollectObjects.All; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.layerMask = ~0;
                surface.BuildNavMesh();
                if (surface.navMeshData == null) throw new InvalidOperationException("Bake sans données.");
                var data = UnityEngine.Object.Instantiate(surface.navMeshData);
                string path = FractureGrayboxAuthoring.Folder + "/District_1_Fracture_NavMesh.asset";
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                if (existing == null) AssetDatabase.CreateAsset(data, path);
                else { EditorUtility.CopySerialized(data, existing); UnityEngine.Object.DestroyImmediate(data); data = existing; EditorUtility.SetDirty(data); }
                var settings = new SerializedObject(manifest);
                settings.FindProperty("bakedNavMeshData").objectReferenceValue = data;
                settings.FindProperty("bakedNavMeshAgentTypeId").intValue = surface.agentTypeID;
                settings.FindProperty("bakedNavMeshVersion").stringValue = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                settings.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(manifest);
            }
            finally
            {
                foreach (var b in blockers) b.enabled = true;
                foreach (var o in obstacles) o.enabled = true;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
        public static void Audit()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Sauvegarder les scènes utilisateur avant l'audit.");
            try
            {
                var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
                var core = EditorSceneManager.OpenScene("Assets/Scenes/District_1/District_1_Core.unity", OpenSceneMode.Single);
                foreach (var reference in manifest.loadingScenes)
                {
                    string path = AssetDatabase.GetAssetPath(reference);
                    if (!SceneManager.GetSceneByPath(path).isLoaded) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                Physics.SyncTransforms();
                var spawn = core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ZoneSpawnPoint>(true)).OrderBy(p => p.PartySlot).First();
                Vector3 at = spawn.transform.position;
                var text = new StringBuilder("spawn=" + at + "\nGrid: x -150..10 by 4m; z 140..-20 by -4m. .=void f=walkable #=obstacle /=different elevation\n");
                for (int z = 140; z >= -20; z -= 4)
                {
                    text.Append(z.ToString().PadLeft(4) + " ");
                    for (int x = -150; x <= 10; x += 4)
                    {
                        var point = new Vector3(x, at.y + 2, z);
                        var hits = Physics.RaycastAll(point, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance).ToArray();
                        if (hits.Length == 0) { text.Append('.'); continue; }
                        var hit = hits[0];
                        if (Mathf.Abs(hit.point.y - at.y) > .8f) { text.Append('/'); continue; }
                        bool blocked = Physics.CheckCapsule(hit.point + Vector3.up * .35f, hit.point + Vector3.up * 1.7f, .3f, ~0, QueryTriggerInteraction.Ignore);
                        text.Append(blocked ? '#' : 'f');
                    }
                    text.AppendLine();
                }
                File.WriteAllText("Library/FractureLabyrinthValidation/DistrictAudit.txt", text.ToString());
                var detail = new StringBuilder();
                for (int z = 80; z >= 40; z--)
                {
                    detail.Append(z.ToString().PadLeft(3) + " ");
                    for (int x = -110; x <= -80; x++)
                    {
                        var hits = Physics.RaycastAll(new Vector3(x, at.y + 2, z), Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance).ToArray();
                        detail.Append(hits.Length == 0 ? '.' : Mathf.Abs(hits[0].point.y - at.y) > .8f ? '#' :
                            Physics.CheckCapsule(hits[0].point + Vector3.up * .35f, hits[0].point + Vector3.up * 1.7f, .3f, ~0, QueryTriggerInteraction.Ignore) ? '#' : 'f');
                    }
                    detail.AppendLine();
                }
                foreach (var col in Physics.OverlapBox(new Vector3(-92, at.y + 1.5f, 54), new Vector3(4, 2, 8), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    detail.AppendLine(col.gameObject.scene.name + "/" + col.name + " center=" + col.bounds.center + " size=" + col.bounds.size + " type=" + col.GetType().Name + " parent=" + col.transform.parent?.name);
                    if (col.name == "SafetyCollision" && col.bounds.center.x < -92)
                        foreach (var r in col.transform.parent.GetComponentsInChildren<Renderer>()) detail.AppendLine("  visual " + r.name + " center=" + r.bounds.center + " size=" + r.bounds.size);
                }
                File.WriteAllText("Library/FractureLabyrinthValidation/DistrictDetail.txt", detail.ToString());
                var volumes = new StringBuilder();
                foreach (var point in new[] { new Vector3(-122.2f,-93,85), new Vector3(-127.98f,-93,35) })
                {
                    volumes.AppendLine("Point " + point);
                    foreach (var c in Physics.OverlapSphere(point, 1, ~0, QueryTriggerInteraction.Collide))
                        volumes.AppendLine(c.gameObject.scene.name + "/" + c.name + " trigger=" + c.isTrigger + " size=" + c.bounds.size + " scripts=" + string.Join(",", c.GetComponentsInParent<MonoBehaviour>().Select(b => b.GetType().Name)));
                }
                File.WriteAllText("Library/FractureLabyrinthValidation/DistrictVolumes.txt", volumes.ToString());
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
