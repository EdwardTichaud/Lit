using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class ReveLimbesAuthoringSession
{
    private const string ZoneFolder = "Assets/Scenes/Reve";
    private static readonly Vector3 Origin = new(600, 120, 0);
    private static readonly string[] NewScenes = { "Reve_Limbes", "Reve_Limbes_Environment", "Reve_Limbes_Lighting", "Reve_Limbes_Interactives" };
    private static readonly Vector3[] Islands =
    {
        new(0,0,0), new(0,1.5f,27), new(-24,3,27), new(24,5,46), new(6,4,46), new(-12,7,62), new(4,9,84)
    };
    private static readonly string[] IslandNames = { "Seuil", "Archives_disloquees", "Chambre_renversee", "Balcon_sans_facade", "Carrefour_des_arches", "Belvedere_brise", "Rive_du_reveil" };
    private static readonly Vector3[] Stones =
    {
        new(0,.5f,10), new(0,1,16), new(0,1.2f,20),
        new(-10,2,27), new(-16,2.5f,27),
        new(10,2.5f,29), new(16,3.5f,33), new(22,4,37),
        new(0,2.5f,37), new(0,3.5f,42),
        new(16,4.5f,46), new(-4,5,50), new(-8,6,54)
    };
    private static void PollCreation()
    {
        string request = Output + "/CreateEnvironment.request";
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request); running = true;
        File.WriteAllText(Output + "/CreateEnvironment.result", "RUNNING pid=" + System.Diagnostics.Process.GetCurrentProcess().Id + "; utc=" + DateTime.UtcNow.ToString("O"));
        try { CreateEnvironment(); }
        catch (Exception error) { File.WriteAllText(Output + "/CreateEnvironment.result", "FAILED " + error); }
        finally { running = false; }
    }

    private static void CreateEnvironment()
    {
        foreach (string name in NewScenes)
            if (File.Exists(ZoneFolder + "/" + name + ".unity")) throw new InvalidOperationException("Never regenerate an existing scene: " + name);
        if (File.Exists(ZoneFolder + "/ZoneManifest_Reve_Limbes.asset")) throw new InvalidOperationException("Existing zone manifest must be edited, not regenerated.");
        EnsureFolder(ZoneFolder); EnsureFolder(ZoneFolder + "/Lighting"); EnsureFolder(ZoneFolder + "/Navigation");
        Scene original = SceneManager.GetActiveScene();
        var created = new List<Scene>();
        try
        {
            var core = NewSavedScene(NewScenes[0], created);
            var coreRoot = Root(core, "Reve_Limbes_Core", Origin);
            var recovery = coreRoot.AddComponent<LimbesFallRecovery>();
            recovery.voidHeight = Origin.y - 25;
            var participation = Child(coreRoot.transform, "Zone_des_participants", new Vector3(0, 25, 40));
            recovery.participationVolume = participation.AddComponent<BoxCollider>();
            recovery.participationVolume.isTrigger = true; recovery.participationVolume.size = new Vector3(220, 150, 240);
            recovery.landings = new LimbesFallRecovery.SafeLanding[Islands.Length];
            for (int i = 0; i < Islands.Length; i++)
            {
                var safe = Child(coreRoot.transform, "Palier_" + IslandNames[i], Islands[i] + Vector3.up * .1f);
                var box = safe.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.up; box.size = new Vector3(6, 3, 6);
                recovery.landings[i] = new LimbesFallRecovery.SafeLanding { id = "limbes." + i, arrival = safe.transform, volume = box };
            }
            for (int i = 0; i < 4; i++)
            {
                var spawn = Child(coreRoot.transform, "Arrivee_" + i, new Vector3((i - 1.5f) * 1.1f, .1f, -2));
                var point = spawn.AddComponent<ZoneSpawnPoint>();
                Set(point, "spawnId", "Default"); Set(point, "partySlot", i);
            }

            var environment = NewSavedScene(NewScenes[1], created);
            var environmentRoot = Root(environment, "Limbes_Architecture", Origin);
            var floors = Child(environmentRoot.transform, "01_Parcours_praticable", Vector3.zero);
            for (int i = 0; i < Islands.Length; i++)
            {
                var island = Child(floors.transform, "Ile_" + IslandNames[i], Islands[i]);
                Module("Castle_Floor", "Sol", island.transform, Vector3.zero, Vector3.zero, 1, true);
                for (int side = 0; side < 4; side++)
                {
                    Vector3 p = new Vector3((side % 2 == 0 ? -1 : 1) * 4.2f, -.8f, (side < 2 ? -1 : 1) * 4.2f);
                    Module("Pillar_01_4H Variant", "Pied_suspendu_" + side, island.transform, p, new Vector3(0, side * 90, 0), 1.25f, false);
                }
                Module("Doorway_Pillar_01 Variant", "Arche", island.transform, new Vector3(-3.8f, 4.4f, 3.2f), new Vector3(i == 2 ? 180 : 0, i * 29, i == 5 ? 16 : 0), 1.2f, false);
                Module("Railing_Stone_01 Variant", "Parapet_arriere", island.transform, new Vector3(0, 1.2f, -5.6f), Vector3.zero, 1, false);
            }
            var jumps = Child(floors.transform, "Sauts", Vector3.zero);
            for (int i = 0; i < Stones.Length; i++)
            {
                Module("Floor_Flagstone_01 Variant", "Saut_" + i.ToString("00"), jumps.transform, Stones[i], Vector3.zero, 1, true);
                Module("Pillar_01_2H Variant", "Fragment_sous_saut_" + i, jumps.transform, Stones[i] - Vector3.up * .35f, new Vector3(0, i * 33, 0), 1, false);
            }
            // Distinct silhouette for the upside-down optional room; walking
            // surface stays upright and separate from the impossible ceiling.
            Module("Ceiling_Arch_Brick_01_1Way_Sml Variant", "Voute_a_l_envers", environmentRoot.transform,
                Islands[2] + new Vector3(0, 5, 0), new Vector3(180, 90, 0), 2, false);
            var bridge = Child(environmentRoot.transform, "Passage_recompose", Vector3.zero);
            Vector3 a = Islands[5] + new Vector3(0, 0, 4), b = Islands[6] - new Vector3(0, 0, 4);
            int count = Mathf.CeilToInt(Vector3.Distance(a, b) / 3f);
            for (int i = 0; i <= count; i++) Module("Floor_Flagstone_01 Variant", "Dalle_" + i, bridge.transform, Vector3.Lerp(a, b, (float)i / count), Vector3.zero, 1, true);
            var link = bridge.AddComponent<NavMeshLink>(); link.startPoint = a + Vector3.up * .1f; link.endPoint = b + Vector3.up * .1f; link.width = 1.5f; link.bidirectional = true;
            bridge.SetActive(false);
            var gate = Child(environmentRoot.transform, "Activation_persistante_du_passage", Vector3.zero).AddComponent<CycleActivationId>();
            gate.activationId = "reve.limbes.seuil_recompose"; gate.target = bridge; gate.activeWhenSet = true;

            var background = Child(environmentRoot.transform, "02_Ruines_et_fractures_sans_collision", Vector3.zero);
            var random = new System.Random(66631);
            for (int i = 0; i < 46; i++)
            {
                float angle = i * 2.39996f, radius = 36 + (float)random.NextDouble() * 70;
                var p = new Vector3(Mathf.Cos(angle) * radius, -18 + (float)random.NextDouble() * 70, 44 + Mathf.Sin(angle) * radius);
                string model = i % 3 == 0 ? "Ceiling_Arch_Brick_01_1Way_Sml Variant" : "Pillar_01_4H Variant";
                Module(model, "Ruine_flottante_" + i.ToString("00"), background.transform, p,
                    new Vector3((float)random.NextDouble() * 170, i * 41, (float)random.NextDouble() * 70), 1.2f + (float)random.NextDouble() * 2.2f, false);
                if (i % 2 == 0) Module("BrickPile_02 Variant", "Debris_" + i, background.transform, p + new Vector3(3, -2, 2), new Vector3(i * 15, i * 31, i * 11), 3, false);
            }
            var vortex = Child(background.transform, "Tourbillon_d_architecture", Vector3.zero);
            for (int i = 0; i < 32; i++)
            {
                float angle = i * .59f, radius = 13 + i * .95f;
                Vector3 p = new(Mathf.Cos(angle) * radius, 35 + Mathf.Sin(angle) * radius, 69 + i * .32f);
                Module(i % 4 == 0 ? "Arch_Brick_Cross_4way_Sml Variant" : "Ceiling_Arch_Border_01 Variant",
                    "Arc_emporte_" + i.ToString("00"), vortex.transform, p,
                    new Vector3(20 + i * 7, 60 + i * 11, angle * Mathf.Rad2Deg + 90), 1.1f + (i % 3) * .3f, false);
            }

            var lighting = NewSavedScene(NewScenes[2], created);
            var lightingRoot = Root(lighting, "Limbes_Lumiere", Origin);
            CreateLighting(lightingRoot);
            for (int i = 0; i < Islands.Length; i++)
            {
                var flame = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Model_Flame_WallTorch.prefab"), lighting);
                flame.name = "Flame_" + IslandNames[i]; flame.transform.SetParent(lightingRoot.transform, false);
                flame.transform.localPosition = Islands[i] + new Vector3(-4.3f, .4f, 1.8f);
                flame.GetComponent<Flame>().ConfigureFromSceneMarker("reve.limbes.flame." + i, false, true, 2, 11, 0);
            }
            var interactions = NewSavedScene(NewScenes[3], created);
            Root(interactions, "Limbes_Interactions", Origin);

            foreach (var scene in created) Save(scene);
            var manifest = ScriptableObject.CreateInstance<ZoneManifest>();
            manifest.primaryScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ZoneFolder + "/Reve_Limbes.unity");
            manifest.loadingScenes = NewScenes.Skip(1).Select(name => AssetDatabase.LoadAssetAtPath<SceneAsset>(ZoneFolder + "/" + name + ".unity")).ToList();
            Set(manifest, "editorSceneReferencesMigrated", true); Set(manifest, "primarySceneName", NewScenes[0]);
            Set(manifest, "loadingSceneNames", NewScenes.Skip(1).ToArray()); Set(manifest, "loadingMessage", "Entrer dans les Limbes...");
            AssetDatabase.CreateAsset(manifest, ZoneFolder + "/ZoneManifest_Reve_Limbes.asset");
            var surface = environmentRoot.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms(); surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, ZoneFolder + "/Navigation/NavMesh_Reve_Limbes.asset");
                Set(manifest, "bakedNavMeshData", surface.navMeshData); Set(manifest, "bakedNavMeshAgentTypeId", 0); Set(manifest, "bakedNavMeshVersion", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            }
            EditorUtility.SetDirty(manifest); AssetDatabase.SaveAssetIfDirty(manifest); Save(environment);
            var builds = EditorBuildSettings.scenes.ToList();
            foreach (string name in NewScenes) if (!builds.Any(scene => scene.path == ZoneFolder + "/" + name + ".unity")) builds.Add(new EditorBuildSettingsScene(ZoneFolder + "/" + name + ".unity", true));
            EditorBuildSettings.scenes = builds.ToArray();
            File.WriteAllText(Output + "/CreateEnvironment.result", "FINISHED newScenes=4; modules instantiated; existing scenes not saved; access and cycles still to integrate.");
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            foreach (var scene in created) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Scene NewSavedScene(string name, List<Scene> owned)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = name; owned.Add(scene);
        // Unity refuses another additive scene while this new scene has no
        // path. Save only this owned, newly created scene before proceeding.
        Save(scene);
        return scene;
    }
    private static GameObject Root(Scene scene, string name, Vector3 position)
    {
        var root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = position; return root;
    }
    private static GameObject Child(Transform parent, string name, Vector3 position)
    {
        var child = new GameObject(name); child.transform.SetParent(parent, false); child.transform.localPosition = position; return child;
    }
    private static GameObject Module(string file, string name, Transform parent, Vector3 top, Vector3 rotation, float scale, bool walkable)
    {
        string path = Pack + "/" + file + ".prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new FileNotFoundException(path);
        var wrapper = Child(parent, name, top); wrapper.transform.localScale = Vector3.one * scale;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene); instance.transform.SetParent(wrapper.transform, false);
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        instance.transform.position += wrapper.transform.position - new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        if (walkable)
        {
            var box = wrapper.AddComponent<BoxCollider>(); box.center = new Vector3(0, -.2f, 0); box.size = new Vector3(bounds.size.x / scale, .4f, bounds.size.z / scale);
        }
        wrapper.transform.localRotation = Quaternion.Euler(rotation);
        return wrapper;
    }
    private static void Save(Scene scene)
    {
        string path = ZoneFolder + "/" + scene.name + ".unity";
        if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Scene save failed: " + scene.name);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }
    private static void Set(Object target, string name, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(name);
        if (p == null) throw new InvalidOperationException(target.GetType().Name + "." + name);
        switch (value)
        {
            case string s: p.stringValue = s; break;
            case bool b: p.boolValue = b; break;
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case Object o: p.objectReferenceValue = o; break;
            case string[] a: p.arraySize = a.Length; for (int i = 0; i < a.Length; i++) p.GetArrayElementAtIndex(i).stringValue = a[i]; break;
            default: throw new InvalidOperationException("Unsupported authoring field: " + name);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static void CreateLighting(GameObject root)
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ZoneFolder + "/Lighting/Volume_Limbes.asset");
        var visual = profile.Add<VisualEnvironment>(true); visual.skyType.Override((int)SkyType.HDRI); visual.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
        var sky = profile.Add<HDRISky>(true); sky.hdriSky.Override(AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/0 - UnityPackages/Fab/_WhiteCastle/__CubemapsShaders/sunset 1.hdr")); sky.rotation.Override(145);
        var exposure = profile.Add<Exposure>(true); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(7.5f);
        var fog = profile.Add<Fog>(true); fog.enabled.Override(true); fog.meanFreePath.Override(135); fog.baseHeight.Override(Origin.y - 30); fog.maximumHeight.Override(Origin.y + 65); fog.albedo.Override(new Color(.67f, .73f, .81f));
        var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.2f); bloom.threshold.Override(1);
        foreach (var component in profile.components) if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        var volume = root.AddComponent<Volume>(); volume.sharedProfile = profile; volume.isGlobal = false; volume.priority = 100; volume.blendDistance = 10;
        var area = root.AddComponent<BoxCollider>(); area.isTrigger = true; area.center = new Vector3(0, 25, 40); area.size = new Vector3(400, 250, 400);
        var sun = Child(root.transform, "Lumiere_du_vide", new Vector3(0, 60, 50)); sun.transform.rotation = Quaternion.Euler(38, -35, 0);
        sun.AddComponent<LitContrastLight>().Configure(LightType.Directional, new Color(.87f, .91f, 1), LightUnit.Lux, 600, 10, 55, true, .8f, .5f, .8f, false, "Diffuse dream illumination; independent of castle lighting.");
        var material = new Material(Shader.Find("HDRP/Lit")); material.name = "Limbes_Lueur"; material.SetColor("_BaseColor", Color.white); material.SetColor("_EmissiveColor", new Color(80, 65, 42));
        AssetDatabase.CreateAsset(material, ZoneFolder + "/Lighting/Limbes_Lueur.mat");
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere); core.name = "Coeur_du_tourbillon"; core.transform.SetParent(root.transform, false); core.transform.localPosition = new Vector3(0, 35, 68); core.transform.localScale = Vector3.one * 5;
        Object.DestroyImmediate(core.GetComponent<Collider>()); core.GetComponent<Renderer>().sharedMaterial = material;
    }
}
