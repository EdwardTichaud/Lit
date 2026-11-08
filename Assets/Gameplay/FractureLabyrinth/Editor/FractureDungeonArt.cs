using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.Fractures.Editor
{
    /// <summary>Scene-local visual dressing. Authored gameplay colliders remain the authority.</summary>
    public static class FractureDungeonArt
    {
        public const string Pack = "Assets/0 - UnityPackages/Fab/ModularDungeonCollection/";
        public const string Marker = "ModularDungeonCollection_Visual";
        private static readonly string[] Sources = {
            "Prefabs/Floor_Flagstone_01 Variant.prefab", "Prefabs/Wall_Stone_01_Sml Variant.prefab",
            "Prefabs/Door_02 Variant.prefab", "Meshes/SM_Shelf_01.fbx",
            "Prefabs/Pillar_01_2H Variant.prefab", "Meshes/SM_Lever_Base_01.fbx", "Meshes/SM_Lever_Handle_01.fbx"
        };

        public static void Inspect()
        {
            var lines = Sources.Select(path =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + path);
                if (asset == null) throw new InvalidOperationException("Asset manquant : " + path);
                var clone = UnityEngine.Object.Instantiate(asset);
                try
                {
                    clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var bounds = BoundsOf(clone);
                    return path + " | size=" + bounds.size + " center=" + bounds.center + " | " +
                        string.Join(", ", clone.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                            .Select(m => m == null ? "MISSING" : m.name + ":" + m.shader.name).Distinct());
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            });
            File.WriteAllLines("Library/FractureLabyrinthValidation/DungeonArt-assets.txt", lines);
        }

        [MenuItem("Lit/Fractures/Install Modular Dungeon Art")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(FractureGrayboxAuthoring.ScenePath);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (loaded && scene.isDirty) throw new InvalidOperationException("La scène contient des modifications non sauvegardées.");
            if (!loaded) scene = EditorSceneManager.OpenScene(FractureGrayboxAuthoring.ScenePath, OpenSceneMode.Additive);
            try
            {
                var lab = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FractureGraybox>()).Single();
                Dress(lab);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!loaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        public static void Dress(FractureGraybox lab)
        {
            // Snapshot only original primitive renderers, never newly installed meshes.
            var originals = lab.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null && !AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith("Assets/", StringComparison.Ordinal)).ToArray();
            foreach (var filter in originals)
            {
                var host = filter.gameObject;
                var station = host.GetComponent<FractureStation>();
                bool shelf = station != null && station.kind == FractureStation.Kind.Bookshelf;
                bool door = host == lab.startGate || host == lab.galleryGate || host == lab.thresholdGate || host == lab.networkGate;
                bool wall = station == null && !door && !host.name.StartsWith("Floor_") &&
                    !(host.transform.parent != null && host.transform.parent.GetComponent<FractureStation>() != null);
                var installed = host.transform.Find(Marker);
                if (installed != null)
                {
                    if (wall ? installed.Find("Mur massif") != null :
                        !shelf || installed.childCount == 0 || Mathf.Abs(Mathf.DeltaAngle(installed.GetChild(0).eulerAngles.y, 180)) < 1) continue;
                    UnityEngine.Object.DestroyImmediate(installed.gameObject);
                }
                if (station == null && host.transform.parent != null && host.transform.parent.GetComponent<FractureStation>() != null)
                    continue; // Temporary books/boards are hidden together with their shelf.
                var collider = host.GetComponent<BoxCollider>();
                if (collider == null) continue;
                string path = shelf ? Sources[3] : station != null ? Sources[4] : door ? Sources[2] : host.name.StartsWith("Floor_") ? Sources[0] : Sources[1];
                Bounds target = collider.bounds;
                // Inactive gates still need authored bounds rather than Unity's empty collider.bounds.
                if (target.size.sqrMagnitude < .001f)
                    target = new Bounds(host.transform.TransformPoint(collider.center), Vector3.Scale(collider.size, host.transform.lossyScale));
                if (shelf)
                {
                    foreach (var renderer in host.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                }
                else if (host.GetComponent<Renderer>() != null) host.GetComponent<Renderer>().enabled = false;
                var root = new GameObject(Marker);
                root.transform.SetParent(host.transform, false);
                float yaw = shelf ? 180 : door ? 90 : 0;
                if (wall)
                {
                    var solid = new GameObject("Mur massif", typeof(MeshFilter), typeof(MeshRenderer));
                    solid.transform.position = target.center;
                    solid.transform.SetParent(root.transform, true);
                    solid.GetComponent<MeshFilter>().sharedMesh = SolidWallMesh(target.size);
                    solid.GetComponent<MeshRenderer>().sharedMaterial = SolidWallMaterial();
                }
                else Fit(path, root.transform, target, yaw);
                if (station != null && !shelf)
                {
                    // Each active desk is now a stone pedestal carrying the collection's lever.
                    var leverBounds = new Bounds(target.center + Vector3.up * (target.extents.y + .08f), new Vector3(.38f, .16f, .38f));
                    Fit(Sources[5], root.transform, leverBounds, 0);
                    Fit(Sources[6], root.transform, new Bounds(leverBounds.center + Vector3.up * .16f, new Vector3(.12f, .32f, .12f)), 0);
                }
            }
            AssetDatabase.SaveAssets();
        }

        public static Mesh SolidWallMesh(Vector3 size)
        {
            string key = string.Join("_", new[] { size.x, size.y, size.z }.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', 'p')));
            string path = FractureGrayboxAuthoring.Folder + "/SolidWall_" + key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var mesh = new Mesh { name = "SolidWall_" + key };
            Vector3 h = size * .5f;
            var vertices = new[] {
                new Vector3(-h.x,-h.y,h.z), new Vector3(h.x,-h.y,h.z), new Vector3(h.x,h.y,h.z), new Vector3(-h.x,h.y,h.z),
                new Vector3(h.x,-h.y,-h.z), new Vector3(-h.x,-h.y,-h.z), new Vector3(-h.x,h.y,-h.z), new Vector3(h.x,h.y,-h.z),
                new Vector3(h.x,-h.y,h.z), new Vector3(h.x,-h.y,-h.z), new Vector3(h.x,h.y,-h.z), new Vector3(h.x,h.y,h.z),
                new Vector3(-h.x,-h.y,-h.z), new Vector3(-h.x,-h.y,h.z), new Vector3(-h.x,h.y,h.z), new Vector3(-h.x,h.y,-h.z),
                new Vector3(-h.x,h.y,h.z), new Vector3(h.x,h.y,h.z), new Vector3(h.x,h.y,-h.z), new Vector3(-h.x,h.y,-h.z),
                new Vector3(-h.x,-h.y,-h.z), new Vector3(h.x,-h.y,-h.z), new Vector3(h.x,-h.y,h.z), new Vector3(-h.x,-h.y,h.z)
            };
            var uv = new Vector2[24];
            var triangles = new int[36];
            for (int face = 0; face < 6; face++)
            {
                int i = face * 4;
                Vector3 u = (vertices[i + 1] - vertices[i]).normalized;
                Vector3 v = (vertices[i + 3] - vertices[i]).normalized;
                for (int j = 0; j < 4; j++)
                    uv[i + j] = new Vector2(Vector3.Dot(vertices[i + j], u), Vector3.Dot(vertices[i + j], v)) / 4f;
                int t = face * 6;
                triangles[t] = i; triangles[t + 1] = i + 1; triangles[t + 2] = i + 2;
                triangles[t + 3] = i; triangles[t + 4] = i + 2; triangles[t + 5] = i + 3;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material SolidWallMaterial()
        {
            string path = FractureGrayboxAuthoring.Folder + "/SolidWall_Stone.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/M_Stone_Wall_01_Master.mat");
            if (source == null) throw new InvalidOperationException("Matériau pierre du pack manquant.");
            var material = new Material(source) { name = "Fracture_SolidWall_Stone" };
            // Retain the pack's color/normal/AO/roughness maps. UV density lives in the mesh.
            foreach (string property in material.GetTexturePropertyNames())
            {
                material.SetTextureScale(property, Vector2.one);
                material.SetTextureOffset(property, Vector2.zero);
            }
            if (material.HasProperty("_UvTiling")) material.SetVector("_UvTiling", new Vector4(1, 1, 0, 0));
            if (material.HasProperty("_UvOffset")) material.SetVector("_UvOffset", Vector4.zero);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static GameObject Fit(string path, Transform parent, Bounds target, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + path);
            if (asset == null) throw new InvalidOperationException("Asset manquant : " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, yaw, 0));
            var bounds = BoundsOf(instance);
            var scale = instance.transform.localScale;
            instance.transform.localScale = Vector3.Scale(scale, new Vector3(Ratio(target.size.x, bounds.size.x), Ratio(target.size.y, bounds.size.y), Ratio(target.size.z, bounds.size.z)));
            bounds = BoundsOf(instance);
            instance.transform.position += target.center - bounds.center;
            instance.transform.SetParent(parent, true);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return instance;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Aucun mesh : " + root.name);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        private static float Ratio(float target, float size) => size > .001f ? target / size : 1f;
    }
}
