using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

/// <summary>Consolidates all fixed Flame authoring into the two gameplay model prefabs.</summary>
public static class FlamePrefabCentralization
{
    private const string WallTorchPath = "Assets/Prefabs/Model_Flame_WallTorch.prefab";
    private const string BrazierPath = "Assets/Prefabs/Model_Flame_Brazier.prefab";
    private const string LegacyCommonPath = "Assets/Interactive/Flame/Light_Flame_Fire_Common.prefab";
    private const string LegacyAncientLightPath = "Assets/Interactive/Flame/Light_Flame_Fire_Ancient.prefab";
    private const string LegacyAncientPath = "Assets/Interactive/Flame/Interactive_Flame_Ancient.prefab";
    private const string MaterialFolder = "Assets/Environment/Lighting/Materials";
    private const string RequestPath = "Library/FlamePrefabCentralization.request";
    private const string ResultPath = "Library/FlamePrefabCentralization.result";

    static FlamePrefabCentralization()
    {
        EditorApplication.delayCall += RunRequested;
        EditorApplication.update += RunRequested;
    }

    [MenuItem("Lit/Lighting/Centralize Flame Prefabs")]
    public static void Run()
    {
        Materials materials = LoadMaterials();
        ConfigureWallTorch(materials);
        ConfigureBrazier(materials);
        int migrated = MigrateLegacyAncientInstances();
        UpdateEditorTools();
        DeleteLegacyAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Lit Lighting] Centralized Flames into WallTorch and Brazier; migrated {migrated} legacy Ancient Flame instance(s).");
    }

    private static void RunRequested()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        try
        {
            File.Delete(RequestPath);
            Run();
            File.WriteAllText(ResultPath, "success " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "failure\n" + exception);
            Debug.LogException(exception);
        }
    }

    private static void ConfigureWallTorch(Materials materials)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WallTorchPath);
        try
        {
            Transform legacyLighting = FindDescendant(root.transform, "Light_Flame_Fire_Common");
            if (legacyLighting != null && PrefabUtility.IsPartOfPrefabInstance(legacyLighting.gameObject))
                PrefabUtility.UnpackPrefabInstance(legacyLighting.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Transform lighting = legacyLighting != null ? legacyLighting : EnsureChild(root.transform, "Lighting_Common");
            lighting.name = "Lighting_Common";
            Light warm = FindOrCreateLight(lighting, "Warm_Core");
            LitContrastLight profile = EnsureComponent<LitContrastLight>(warm.gameObject);
            profile.Configure(LightType.Point, new Color(1f, 0.31f, 0.08f, 1f), UnityEngine.Rendering.LightUnit.Lumen, 330f, 4.5f, 55f,
                true, 0.75f, 0.65f, 0.55f, true, "Warm practical source. Keep its range short so the arena background stays dark.");

            ConfigureFlamePresentation(root, warm, false, lighting.gameObject, materials);
            PrefabUtility.SaveAsPrefabAsset(root, WallTorchPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureBrazier(Materials materials)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BrazierPath);
        try
        {
            RemoveLegacyLighting(root);
            Transform lighting = EnsureChild(root.transform, "Lighting_Ancient");
            Light warm = FindOrCreateLight(EnsureChild(lighting, "Warm_Core"), "Point_Light");
            LitContrastLight warmProfile = EnsureComponent<LitContrastLight>(warm.gameObject);
            warmProfile.Configure(LightType.Point, new Color(1f, 0.38f, 0.12f, 1f), UnityEngine.Rendering.LightUnit.Lumen, 250f, 5f, 55f,
                true, 0.75f, 0.65f, 0.55f, true, "Warm core of the Ancient Flame. The cyan aura is a separate vertical beam.");

            Transform auraRoot = EnsureChild(lighting, "Cyan_Aura");
            Light aura = FindOrCreateLight(auraRoot, "Spot_Light");
            LitContrastLight auraProfile = EnsureComponent<LitContrastLight>(aura.gameObject);
            auraProfile.Configure(LightType.Spot, new Color(0.22f, 0.72f, 1f, 1f), UnityEngine.Rendering.LightUnit.Lumen, 250f, 5f, 65f,
                false, 0f, 0f, 0.75f, true, "Cyan vertical aura of the Ancient Flame. It remains supernatural rather than ambient fill.");
            aura.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            ConfigureFlamePresentation(root, warm, true, lighting.gameObject, materials);
            PrefabUtility.SaveAsPrefabAsset(root, BrazierPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureFlamePresentation(GameObject root, Light warmLight, bool ancient, GameObject lightingRoot, Materials materials)
    {
        Flame flame = root.GetComponentInChildren<Flame>(true);
        if (flame == null) throw new InvalidOperationException(root.name + " has no Flame component.");
        flame.ConfigureDirectInteraction();

        SerializedObject flameSerialized = new SerializedObject(flame);
        flameSerialized.FindProperty("ancientFlame").boolValue = ancient;
        flameSerialized.FindProperty("flameLight").objectReferenceValue = warmLight;
        flameSerialized.FindProperty("flameLightReceiver").objectReferenceValue = EnsureReceiver(lightingRoot, warmLight);
        flameSerialized.FindProperty("torchPointCostToLight").intValue = ancient ? 20 : 10;
        flameSerialized.FindProperty("chargeCostToLight").intValue = 0;
        flameSerialized.FindProperty("useInteractInput").boolValue = true;
        flameSerialized.FindProperty("useTriggerMuninInput").boolValue = false;
        flameSerialized.FindProperty("showStateDialogueOnInteract").boolValue = false;
        flameSerialized.ApplyModifiedPropertiesWithoutUndo();

        FlickeringLight flicker = EnsureComponent<FlickeringLight>(warmLight.gameObject);
        flicker.ConfigureForFlame(warmLight);

        LitAtmosphereParticles atmosphere = EnsureComponent<LitAtmosphereParticles>(lightingRoot);
        DestroyAtmosphereChildren(lightingRoot.transform);
        ParticleSystem embers = CreateParticleSystem(lightingRoot.transform, "AtmosphereParticles_Embers", new Vector3(0f, 0.08f, 0f));
        if (ancient)
        {
            ParticleSystem mist = CreateParticleSystem(lightingRoot.transform, "AtmosphereParticles_AncientMist", new Vector3(0f, 0.18f, 0f));
            atmosphere.ConfigureAuthoring(LitAtmosphereParticles.Profile.AncientFlameMix, new[] { embers, mist }, materials.Dust, materials.Embers, materials.AncientMist);
        }
        else atmosphere.ConfigureAuthoring(LitAtmosphereParticles.Profile.FlameEmbers, new[] { embers }, materials.Dust, materials.Embers, materials.AncientMist);
        atmosphere.ApplyVisibilityDefaults();
    }

    private static FlameLightReceiver EnsureReceiver(GameObject root, Light warmLight)
    {
        FlameLightReceiver receiver = root.GetComponent<FlameLightReceiver>();
        if (receiver == null) receiver = root.AddComponent<FlameLightReceiver>();
        receiver.ConfigureAsWorldRevealSource(warmLight, warmLight.color);
        return receiver;
    }

    private static void RemoveLegacyLighting(GameObject root)
    {
        foreach (FlickeringLight flicker in root.GetComponentsInChildren<FlickeringLight>(true))
            UnityEngine.Object.DestroyImmediate(flicker);

        foreach (LitContrastLight profile in root.GetComponentsInChildren<LitContrastLight>(true))
            UnityEngine.Object.DestroyImmediate(profile);

        foreach (Light light in root.GetComponentsInChildren<Light>(true))
        {
            if (light == null) continue;
            GameObject owner = light.gameObject;
            if (owner == root) { UnityEngine.Object.DestroyImmediate(light); continue; }
            if (owner.GetComponent<Renderer>() == null && owner.GetComponent<ParticleSystem>() == null)
                UnityEngine.Object.DestroyImmediate(owner);
            else UnityEngine.Object.DestroyImmediate(light);
        }
    }

    private static int MigrateLegacyAncientInstances()
    {
        int migrated = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var legacyRoots = new List<GameObject>();
                var seenRoots = new HashSet<int>();
                foreach (Flame flame in Resources.FindObjectsOfTypeAll<Flame>())
                {
                    if (flame.gameObject.scene != scene) continue;
                    GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(flame.gameObject);
                    bool isLegacyPrefabInstance = instanceRoot != null &&
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot) == LegacyAncientPath;

                    // District_5 contains unpacked copies of the former prefab. They no longer
                    // retain a prefab link, but are still identifiable by their ancient Flame
                    // authoring and root name. Treat them as part of the same migration.
                    bool isUnpackedLegacyRoot = instanceRoot == null &&
                        flame.gameObject.name == "Interactive_Flame_Ancient" &&
                        IsAncientFlame(flame);

                    GameObject legacyRoot = isLegacyPrefabInstance ? instanceRoot :
                        (isUnpackedLegacyRoot ? flame.gameObject : null);
                    if (legacyRoot != null && seenRoots.Add(legacyRoot.GetInstanceID()))
                        legacyRoots.Add(legacyRoot);
                }

                foreach (GameObject legacy in legacyRoots)
                {
                    ReplaceLegacyAncient(legacy);
                    migrated++;
                }
                if (legacyRoots.Count > 0) EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        return migrated;
    }

    private static void ReplaceLegacyAncient(GameObject legacy)
    {
        Flame legacyFlame = legacy.GetComponentInChildren<Flame>(true);
        FlameAuthoring authoring = FlameAuthoring.Capture(legacyFlame);
        Transform parent = legacy.transform.parent;
        int sibling = legacy.transform.GetSiblingIndex();
        bool active = legacy.activeSelf;
        string name = legacy.name;
        Vector3 position = legacy.transform.localPosition;
        Quaternion rotation = legacy.transform.localRotation;
        Vector3 scale = legacy.transform.localScale;

        GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BrazierPath), legacy.scene);
        replacement.name = name;
        replacement.transform.SetParent(parent, false);
        replacement.transform.localPosition = position;
        replacement.transform.localRotation = rotation;
        replacement.transform.localScale = scale;
        replacement.transform.SetSiblingIndex(sibling);
        Flame replacementFlame = replacement.GetComponentInChildren<Flame>(true);
        authoring.ApplyTo(replacementFlame, true);
        replacement.SetActive(active);
        UnityEngine.Object.DestroyImmediate(legacy);
    }

    private static bool IsAncientFlame(Flame flame)
    {
        SerializedObject serialized = new SerializedObject(flame);
        SerializedProperty ancient = serialized.FindProperty("ancientFlame");
        return ancient != null && ancient.boolValue;
    }

    private static void UpdateEditorTools()
    {
        ReplaceTextInFile("Assets/Editor/LitAtmosphereSetup.cs", LegacyCommonPath, WallTorchPath);
        ReplaceTextInFile("Assets/Editor/LitAtmosphereSetup.cs", LegacyAncientLightPath, BrazierPath);
        ReplaceTextInFile("Assets/Editor/LitFlameLightingMigration.cs", LegacyCommonPath, WallTorchPath);
        ReplaceTextInFile("Assets/Editor/LitFlameLightingMigration.cs", LegacyAncientLightPath, BrazierPath);
        ReplaceTextInFile("Assets/Editor/LitFlameLightingTests.cs", LegacyCommonPath, WallTorchPath);
        ReplaceTextInFile("Assets/Editor/LitFlameLightingTests.cs", LegacyAncientLightPath, BrazierPath);
        ReplaceTextInFile("Assets/Editor/MuninGameplayRemoval.cs", "        \"Assets/Interactive/Flame/Interactive_Flame_Ancient.prefab\",\n", string.Empty);
        ReplaceTextInFile("Assets/Editor/Tests/MuninGameplayRemovalTests.cs", "    [TestCase(\"Assets/Interactive/Flame/Interactive_Flame_Ancient.prefab\")]\n", string.Empty);
    }

    private static void DeleteLegacyAssets()
    {
        foreach (string path in new[] { LegacyCommonPath, LegacyAncientLightPath, LegacyAncientPath })
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null && !AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException("Unable to delete legacy Flame asset: " + path);
        }
    }

    private static void ReplaceTextInFile(string path, string oldValue, string newValue)
    {
        string fullPath = Path.GetFullPath(path);
        string text = File.ReadAllText(fullPath);
        string updated = text.Replace(oldValue, newValue);
        if (updated != text) File.WriteAllText(fullPath, updated);
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    private static Light FindOrCreateLight(Transform parent, string name)
    {
        Light existing = parent.GetComponentInChildren<Light>(true);
        if (existing != null) return existing;
        Transform lightRoot = EnsureChild(parent, name);
        return EnsureComponent<Light>(lightRoot.gameObject);
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void DestroyAtmosphereChildren(Transform root)
    {
        var targets = new List<GameObject>();
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && (child.name == "AtmosphereParticles_Embers" || child.name == "AtmosphereParticles_AncientMist"))
                targets.Add(child.gameObject);
        }
        foreach (GameObject target in targets) UnityEngine.Object.DestroyImmediate(target);
    }

    private static ParticleSystem CreateParticleSystem(Transform parent, string name, Vector3 position)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        return root.AddComponent<ParticleSystem>();
    }

    private static Materials LoadMaterials()
    {
        var materials = new Materials(
            AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_CastleIceDust.mat"),
            AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_FlameEmbers.mat"),
            AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_AncientFlameMist.mat"));
        if (materials.Dust == null || materials.Embers == null || materials.AncientMist == null)
            throw new InvalidOperationException("The central Flame prefabs require the three HDRP atmosphere materials.");
        return materials;
    }

    private readonly struct Materials
    {
        public readonly Material Dust;
        public readonly Material Embers;
        public readonly Material AncientMist;
        public Materials(Material dust, Material embers, Material mist) { Dust = dust; Embers = embers; AncientMist = mist; }
    }

    private struct FlameAuthoring
    {
        public bool IsLit;
        public string Id;
        public bool OverrideRange;
        public float Range;
        public float InteractionRadius;
        public Vector3 InteractionCenter;
        public float InteractionMaxDistance;
        public int TorchCost;

        public static FlameAuthoring Capture(Flame flame)
        {
            SerializedObject serialized = new SerializedObject(flame);
            return new FlameAuthoring
            {
                IsLit = serialized.FindProperty("isLit").boolValue,
                Id = serialized.FindProperty("flameId").stringValue,
                OverrideRange = serialized.FindProperty("overridePrimaryLightRange").boolValue,
                Range = serialized.FindProperty("primaryLightRange").floatValue,
                InteractionRadius = serialized.FindProperty("interactionRadius").floatValue,
                InteractionCenter = serialized.FindProperty("interactionCenter").vector3Value,
                InteractionMaxDistance = serialized.FindProperty("interactMaxDistance").floatValue,
                TorchCost = serialized.FindProperty("torchPointCostToLight").intValue
            };
        }

        public void ApplyTo(Flame flame, bool ancient)
        {
            SerializedObject serialized = new SerializedObject(flame);
            serialized.FindProperty("isLit").boolValue = IsLit;
            serialized.FindProperty("flameId").stringValue = Id;
            serialized.FindProperty("overridePrimaryLightRange").boolValue = OverrideRange;
            serialized.FindProperty("primaryLightRange").floatValue = Range;
            serialized.FindProperty("interactionRadius").floatValue = InteractionRadius;
            serialized.FindProperty("interactionCenter").vector3Value = InteractionCenter;
            serialized.FindProperty("interactMaxDistance").floatValue = InteractionMaxDistance;
            serialized.FindProperty("torchPointCostToLight").intValue = TorchCost > 0 ? TorchCost : 20;
            serialized.FindProperty("ancientFlame").boolValue = ancient;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            flame.ConfigureDirectInteraction();
        }
    }
}
