using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Story;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit one-time authoring of the approved Courts. Never runs on startup or in Play.</summary>
public static partial class CourtsAuthoring
{
    internal const string Folder = "Assets/Scenes/District_2/Courts";
    internal const string CorePath = Folder + "/District_2_Courts.unity";
    internal static readonly Vector3 Origin = new(1200, 0, 0);
    private static Material stone, glow;
    private static readonly List<Scene> Owned = new();
    private static readonly List<Item> Items = new();

    [MenuItem("Tools/Lit/Zone 2/Install approved Courts")]
    public static void Install()
    {
        if (File.Exists(CorePath)) throw new InvalidOperationException("Courts already authored; edit the existing scenes, do not regenerate.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scenes.");
        if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene("Assets/Scenes/Reve/Reve_Limbes.unity", OpenSceneMode.Single);
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Owned.Clear(); Items.Clear();
        EnsureFolder(Folder + "/Data"); EnsureFolder(Folder + "/Sequences"); EnsureFolder(Folder + "/Prefabs");
        stone = Load<Material>("Assets/Scenes/Reve/Completion/Data/LimbesExpansionStone.mat");
        glow = Load<Material>("Assets/Scenes/Reve/Lighting/Limbes_Lueur.mat");
        try
        {
            Scene core = NewScene(CorePath);
            var root = new GameObject("Les_Cours_interieures"); SceneManager.MoveGameObjectToScene(root, core); root.transform.position = Origin;
            root.AddComponent<ZoneRuntimeContext>();
            for (int i = 0; i < 4; i++)
            {
                var spawn = Child(root.transform, "Arrivee_" + i, new Vector3((i % 2) * 2 - 1, .1f, (i / 2) * 2));
                var point = spawn.AddComponent<ZoneSpawnPoint>(); Set(point, "spawnId", "Default"); Set(point, "partySlot", i);
            }
            Scene world = NewScene(Folder + "/District_2_Courts_Environment.unity");
            var environment = new GameObject("Architecture_reemployee"); SceneManager.MoveGameObjectToScene(environment, world); environment.transform.position = Origin;
            BuildEnvironment(environment.transform);
            var family = BuildFamily(root.transform);
            var visits = BuildVisits(root.transform);
            BuildReserve(root.transform); BuildMargins(root.transform);
            var finale = BuildFinale(root.transform);
            var progression = root.AddComponent<CourtsProgression>();
            progression.family = family; progression.visits = visits; progression.finale = finale;
            progression.commonAccessBarrier = Block(root.transform, "Acces_commun_a_retablir", new Vector3(0, 7.4f, 56), new Vector3(8, 2.8f, .8f));
            progression.visitsBarrier = Block(root.transform, "Retour_des_visites_a_ouvrir", new Vector3(-9, 1.4f, 24), new Vector3(.8f, 2.8f, 6));
            Story(root.transform, "Arrival", "District 2 — Stillhaven", Vector3.zero, new[] {
                "Stillhaven. Des maisons, des réserves, des galeries… Les chemins se croisent au-dessus de nous.",
                "Quelqu'un a laissé des indications dans ces cours. Cherchons ce qu'elles relient."
            }, null, null);
            var manifest = ScriptableObject.CreateInstance<ZoneManifest>();
            Set(manifest, "loadingMessage", "Chargement de Stillhaven...");
            AssetDatabase.CreateAsset(manifest, Folder + "/ZoneManifest_District_2_Courts.asset");
            manifest.primaryScene = Load<SceneAsset>(CorePath);
            manifest.loadingScenes = Owned.Where(s => s != core).Select(s => Load<SceneAsset>(s.path)).ToList();
            Set(manifest, "editorSceneReferencesMigrated", true);
            typeof(ZoneManifest).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manifest, null);
            // Return is always available; onward access is the finale reward, never the global notes cycle.
            Portal(root.transform, "Retour_District_1", new Vector3(0, 0, -5), FindManifest("District_1_Core"));
            progression.nextZonePortal = Portal(root.transform, "Vers_les_Limbes", new Vector3(0, 0, 88),
                Load<ZoneManifest>("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset"));
            progression.nextZonePortal.SetActive(false);
            foreach (Scene scene in Owned) EditorSceneManager.SaveScene(scene);
            BakeNavigation(manifest, world);
            RegisterItems();
            var build = EditorBuildSettings.scenes.ToList();
            foreach (var scene in Owned) if (!build.Any(s => s.path == scene.path)) build.Add(new EditorBuildSettingsScene(scene.path, true));
            EditorBuildSettings.scenes = build.ToArray();
            AssetDatabase.SaveAssets();
            var bootstrap = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Additive);
            var flow = bootstrap.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameFlowService>(true)).Single();
            var flowData = new SerializedObject(flow);
            var zones = flowData.FindProperty("zoneManifests");
            if (!Enumerable.Range(0, zones.arraySize).Any(i => zones.GetArrayElementAtIndex(i).objectReferenceValue == manifest))
            {
                zones.InsertArrayElementAtIndex(zones.arraySize);
                zones.GetArrayElementAtIndex(zones.arraySize - 1).objectReferenceValue = manifest;
                flowData.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(bootstrap);
            }
            Debug.Log("[CourtsAuthoring] Authored zone, five cycles, six presentations. Runtime qualification pending.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); Owned.Clear(); Items.Clear(); }
        FinalizeSafety();
        InstallLighting();
    }

    private static void BuildEnvironment(Transform root)
    {
        Room(root, "Cour_arrivee", new Vector3(0, 0, 5), new Vector2(24, 24));
        Room(root, "Maison_Mirage", new Vector3(-20, 0, 27), new Vector2(16, 20));
        Room(root, "Atelier_et_reserve", new Vector3(20, 0, 27), new Vector2(16, 20));
        Deck(root, "Jonction_maison", new Vector3(-13, 0, 14), new Vector3(14, .4f, 6));
        Deck(root, "Jonction_atelier", new Vector3(13, 0, 14), new Vector3(14, .4f, 6));
        Deck(root, "Traversante_des_visites", new Vector3(0, 0, 24), new Vector3(26, .4f, 6));
        Deck(root, "Galerie_haute", new Vector3(0, 6, 49), new Vector3(44, .5f, 12));
        Ramp(root, "Montee_maison", new Vector3(-20, 0, 35), new Vector3(-20, 6, 45), 5);
        Ramp(root, "Montee_atelier", new Vector3(20, 0, 35), new Vector3(20, 6, 45), 5);
        Ramp(root, "Acces_commun_large", new Vector3(0, 6, 54), new Vector3(0, 0, 72), 7);
        Room(root, "Cour_finale", new Vector3(0, 0, 81), new Vector2(28, 24));
        // Closed perimeters, with deliberately placed doors; neither roof nor camera-obscuring facade.
        Wall(root, "Maison_ouest", new Vector3(-28, 1.5f, 27), new Vector3(.5f, 3, 20));
        Wall(root, "Reserve_est", new Vector3(28, 1.5f, 27), new Vector3(.5f, 3, 20));
        Wall(root, "Galerie_arriere_gauche", new Vector3(-14, 7, 55), new Vector3(16, 2, .5f));
        Wall(root, "Galerie_arriere_droite", new Vector3(14, 7, 55), new Vector3(16, 2, .5f));
        string[] props = { "WoodTrunk_01 Variant", "Pillar_01_4H Variant", "Doorway_Pillar_01 Variant" };
        foreach (string name in props)
        {
            string path = "Assets/0 - UnityPackages/Fab/ModularDungeonCollection/Prefabs/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            for (int i = 0; i < 2; i++) Module(root, path, new Vector3(i == 0 ? -24 : 24, 0, 20 + Array.IndexOf(props, name) * 6), false);
        }
        foreach (Vector3 point in new[] { new Vector3(-8, .6f, 5), new Vector3(-25, .6f, 29), new Vector3(25, .6f, 29), new Vector3(-17, 6.6f, 51), new Vector3(17, 6.6f, 51), new Vector3(-10, .6f, 80) })
        {
            var flame = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Prefabs/Model_Flame_WallTorch.prefab"), root.gameObject.scene);
            flame.transform.SetParent(root, false); flame.transform.localPosition = point; flame.name = "Flame_Cours_" + point;
            flame.GetComponent<Flame>().ConfigureFromSceneMarker("district2.courts.flame." + point, false, true, 2, 22, 0);
        }
        var sun = Child(root, "Lumiere_des_cours", new Vector3(0, 12, 20)).AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f;
        sun.transform.rotation = Quaternion.Euler(55, -35, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.35f, .38f, .45f);
    }
    private static void Room(Transform root, string name, Vector3 center, Vector2 size)
    {
        Deck(root, name, center, new Vector3(size.x, .5f, size.y));
        // Reuse native floor modules as the visible surface; collision is a continuous, explicit slab.
        string path = "Assets/0 - UnityPackages/Fab/ModularDungeonCollection/Prefabs/Castle_Floor.prefab";
        var go = Module(root, path, center + Vector3.up * .015f, true);
        var renderers = go.GetComponentsInChildren<Renderer>(true); Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.localScale = new Vector3(size.x / bounds.size.x, .3f / bounds.size.y, size.y / bounds.size.z);
        bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.position += Origin + center - new Vector3(bounds.center.x, bounds.max.y - .015f, bounds.center.z);
        go.name = name + "_Sol_reemploye";
    }
    private static GameObject Module(Transform root, string path, Vector3 position, bool stripColliders)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(path), root.gameObject.scene);
        go.transform.SetParent(root, false); go.transform.localPosition = position;
        if (stripColliders) foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = r.sharedMaterials.Select(_ => stone).ToArray();
        return go;
    }
    private static void Ramp(Transform root, string name, Vector3 a, Vector3 b, float width)
    {
        Vector3 delta = b - a;
        var go = Block(root, name, (a + b) * .5f - Vector3.up * .25f, new Vector3(width, .5f, delta.magnitude + .6f));
        go.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        for (int i = -1; i <= 1; i += 2)
        {
            var rail = Block(root, name + "_Garde_corps_" + i, (a + b) * .5f + Vector3.right * i * (width * .5f + .2f) + Vector3.up * .8f,
                new Vector3(.25f, 1.2f, delta.magnitude)); rail.transform.localRotation = go.transform.localRotation;
        }
    }
    private static void Deck(Transform root, string name, Vector3 top, Vector3 size) => Block(root, name, top - Vector3.up * size.y * .5f, size);
    private static void Wall(Transform root, string name, Vector3 point, Vector3 size) => Block(root, name, point, size);
    private static GameObject Block(Transform root, string name, Vector3 point, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(root, false);
        go.transform.localPosition = point; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = stone; return go;
    }
    private static GameObject Child(Transform root, string name, Vector3 point)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.localPosition = point; return go;
    }
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Required source missing: " + path);
    private static Scene NewScene(string path)
    {
        if (File.Exists(path)) throw new InvalidOperationException("Refusing overwrite: " + path);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); EditorSceneManager.SaveScene(scene, path); Owned.Add(scene); return scene;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static void Set(Object target, string name, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(name) ?? throw new InvalidOperationException(name);
        if (value is Object asset) p.objectReferenceValue = asset;
        else if (value is string s) p.stringValue = s;
        else if (value is bool b) p.boolValue = b;
        else if (value is int i) { if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i; else p.intValue = i; }
        else throw new InvalidOperationException("Unsupported serialized value " + name);
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
    private static ZoneManifest FindManifest(string primary) => AssetDatabase.FindAssets("t:ZoneManifest").Select(g => Load<ZoneManifest>(AssetDatabase.GUIDToAssetPath(g))).First(m => m.PrimarySceneName == primary);
    private static GameObject Portal(Transform root, string name, Vector3 point, ZoneManifest destination)
    {
        var go = Child(root, name, point); var visual = Block(go.transform, "Arche_interactive", new Vector3(0, 1.5f, 0), new Vector3(2, 3, .4f));
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        var trigger = go.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.center = Vector3.up; trigger.radius = 1.5f;
        var portal = go.AddComponent<PortalController>(); Set(portal, "destinationMode", 1); Set(portal, "destinationZone", destination); Set(portal, "interactionCollider", trigger);
        go.AddComponent<RuntimeOutlineTarget>(); return go;
    }
    private static void RegisterItems()
    {
        const string path = "Assets/Core/System/GameplaySessionRoot.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var registry in root.GetComponentsInChildren<ItemRegistry>(true)) foreach (var item in Items)
                if (!registry.items.Any(i => i != null && i.itemId == item.itemId)) registry.items.Add(item);
            foreach (var store in root.GetComponentsInChildren<CharacterStateStore>(true)) foreach (var item in Items)
                if (!store.allItems.Any(i => i != null && i.itemId == item.itemId)) store.allItems.Add(item);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void BakeNavigation(ZoneManifest manifest, Scene world)
    {
        var host = new GameObject("__Courts_NavigationBake"); SceneManager.MoveGameObjectToScene(host, world);
        try
        {
            var surface = host.AddComponent<NavMeshSurface>(); surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.Volume; surface.center = Origin + new Vector3(0, 5, 44); surface.size = new Vector3(80, 40, 120);
            surface.BuildNavMesh(); if (surface.navMeshData == null) throw new InvalidOperationException("Navigation build failed.");
            var data = Object.Instantiate(surface.navMeshData); AssetDatabase.CreateAsset(data, Folder + "/Data/CourtsNavigation.asset");
            Set(manifest, "bakedNavMeshData", data); Set(manifest, "bakedNavMeshAgentTypeId", surface.agentTypeID); Set(manifest, "bakedNavMeshVersion", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        }
        finally { Object.DestroyImmediate(host); }
    }
}
