#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MuninMemoryChargeSetup
{
    private const string PrefabFolder = "Assets/Prefabs/Munin";
    private const string MaterialFolder = PrefabFolder + "/Materials";
    private const string MaisonScenePath = "Assets/Scenes/Maison.unity";
    private const string HubAltarName = "VigilAltar_Hub_Safeguard";

    [MenuItem("Lit/Munin/Create Memory Charge Sources")]
    public static void CreateMemoryChargeSources()
    {
        EnsureFolder("Assets/Prefabs", "Munin");
        EnsureFolder(PrefabFolder, "Materials");

        Material shardMaterial = EnsureMaterial(
            MaterialFolder + "/MemoryShard.mat",
            new Color(0.42f, 0.76f, 1f, 0.82f),
            emission: new Color(0.12f, 0.42f, 0.8f));
        Material altarMaterial = EnsureMaterial(
            MaterialFolder + "/VigilAltar.mat",
            new Color(0.18f, 0.2f, 0.28f, 1f),
            emission: new Color(0.16f, 0.3f, 0.55f));

        EnsureMemoryShardPrefab(shardMaterial);
        EnsurePacifiedMemoryRewardPrefab();
        GameObject altarPrefab = EnsureVigilAltarPrefab(altarMaterial);
        PlaceHubSafeguard(altarPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Munin memory charge sources created and hub safeguard placed.");
    }

    [MenuItem("Lit/Munin/Validate Memory Charge System")]
    public static void ValidateMemoryChargeSystem()
    {
        ValidateChargeTransitions();
        ValidateRewardAmounts();
        ValidateAuthoredContent();
        Debug.Log("Munin memory charge validation passed.");
    }

    private static void EnsureMemoryShardPrefab(Material material)
    {
        string path = PrefabFolder + "/MemoryShard.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            return;
        }

        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "MemoryShard";
        root.transform.localScale = Vector3.one * 0.35f;
        Renderer renderer = root.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        SphereCollider collider = root.GetComponent<SphereCollider>();
        collider.radius = 0.65f;
        root.AddComponent<MemoryShard>();
        AddSoftLight(root, new Color(0.35f, 0.7f, 1f), 1.3f, 1.6f);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void ValidateChargeTransitions()
    {
        GameObject flameObject = new GameObject("FlameValidation");
        Flame flame = flameObject.AddComponent<Flame>();
        Assert(flame.GetChargeCostForTargetState(true) == 1, "A standard Flame must cost 1 charge to light.");
        Assert(flame.GetChargeCostForTargetState(false) == 0, "Extinguishing a Flame must cost/refund 0 charges.");

        SerializedObject serializedFlame = new SerializedObject(flame);
        serializedFlame.FindProperty("ancientFlame").boolValue = true;
        serializedFlame.ApplyModifiedPropertiesWithoutUndo();
        Assert(flame.GetChargeCostForTargetState(true) == 2, "An AncientFlame must cost at least 2 charges to light.");
        Object.DestroyImmediate(flameObject);
    }

    private static void ValidateRewardAmounts()
    {
        GameObject character = new GameObject("MuninRewardValidationCharacter");
        GameObject muninObject = new GameObject("Munin");
        muninObject.transform.SetParent(character.transform, false);
        MuninController munin = muninObject.AddComponent<MuninController>();
        munin.SetMaxCharges(10, true);

        Assert(munin.TryConsumeCharge(1), "Munin should consume one charge.");
        Assert(munin.ChargesRemaining == 9, "A one-charge light must leave Munin at 9/10.");
        Assert(munin.GrantChargeReward(1, false, "Memory shard") == 1, "A MemoryShard must add 1 charge.");
        Assert(munin.ChargesRemaining == 10, "MemoryShard validation must restore 10/10.");

        munin.SetCharges(0);
        MemoryShard shard = CreateValidationReward<MemoryShard>("MemoryShardValidation");
        Assert(shard.TryGrantToCharacter(character), "MemoryShard should grant its reward.");
        Assert(munin.ChargesRemaining == 1, "MemoryShard must grant exactly +1.");
        Assert(!shard.TryGrantToCharacter(character), "A consumed MemoryShard must not grant twice.");

        munin.SetCharges(0);
        PacifiedMemoryReward memory = CreateValidationReward<PacifiedMemoryReward>("PacifiedMemoryValidation");
        Assert(memory.TryGrantToCharacter(character), "PacifiedMemoryReward should grant its reward.");
        Assert(munin.ChargesRemaining == 3, "PacifiedMemoryReward must grant exactly +3.");

        munin.SetCharges(2);
        VigilAltar altar = CreateValidationReward<VigilAltar>("VigilAltarValidation");
        Assert(altar.TryGrantToCharacter(character), "VigilAltar should grant its reward.");
        Assert(munin.ChargesRemaining == 10, "VigilAltar must refill Munin to maximum.");

        Object.DestroyImmediate(shard.gameObject);
        Object.DestroyImmediate(memory.gameObject);
        Object.DestroyImmediate(altar.gameObject);
        Object.DestroyImmediate(character);
    }

    private static T CreateValidationReward<T>(string objectName) where T : MuninChargeReward
    {
        GameObject rewardObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rewardObject.name = objectName;
        T reward = rewardObject.AddComponent<T>();
        SerializedObject serializedReward = new SerializedObject(reward);
        serializedReward.FindProperty("requireControlledCharacter").boolValue = false;
        serializedReward.FindProperty("requireLitInfluenceForInteraction").boolValue = false;
        serializedReward.FindProperty("showFeedback").boolValue = false;
        serializedReward.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    private static void ValidateAuthoredContent()
    {
        Assert(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/MemoryShard.prefab") != null,
            "MemoryShard prefab is missing.");
        Assert(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/PacifiedMemoryReward.prefab") != null,
            "PacifiedMemoryReward prefab is missing.");
        Assert(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/VigilAltar.prefab") != null,
            "VigilAltar prefab is missing.");

        Scene scene = EditorSceneManager.OpenScene(MaisonScenePath, OpenSceneMode.Single);
        Assert(FindTransformByName(scene, HubAltarName) != null, "The hub VigilAltar safeguard is missing.");

        int ghostCount = 0;
        int rewardedGhostCount = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GhostController[] ghosts = roots[i].GetComponentsInChildren<GhostController>(true);
            ghostCount += ghosts.Length;
            for (int j = 0; j < ghosts.Length; j++)
            {
                if (ghosts[j] != null && ghosts[j].GetComponent<PacifiedMemoryReward>() != null)
                {
                    rewardedGhostCount++;
                }
            }
        }

        Assert(ghostCount > 0 && rewardedGhostCount == ghostCount,
            "Every authored ghost in Maison must have a PacifiedMemoryReward.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsurePacifiedMemoryRewardPrefab()
    {
        string path = PrefabFolder + "/PacifiedMemoryReward.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            return;
        }

        GameObject root = new GameObject("PacifiedMemoryReward");
        root.AddComponent<PacifiedMemoryReward>();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static GameObject EnsureVigilAltarPrefab(Material material)
    {
        string path = PrefabFolder + "/VigilAltar.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = "VigilAltar";
        root.transform.localScale = new Vector3(0.85f, 0.55f, 0.85f);
        Collider primitiveCollider = root.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            Object.DestroyImmediate(primitiveCollider);
        }

        SphereCollider interaction = root.AddComponent<SphereCollider>();
        interaction.radius = 1.4f;
        interaction.center = new Vector3(0f, 0.45f, 0f);
        Renderer renderer = root.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        root.AddComponent<VigilAltar>();
        AddSoftLight(root, new Color(0.35f, 0.58f, 1f), 2.2f, 3.2f);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void PlaceHubSafeguard(GameObject altarPrefab)
    {
        if (altarPrefab == null || !File.Exists(MaisonScenePath))
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(MaisonScenePath, OpenSceneMode.Single);
        bool sceneChanged = AttachPacifiedRewardsToGhosts(scene);
        Transform existing = FindTransformByName(scene, HubAltarName);
        if (existing == null)
        {
            Transform spawn = FindTransformByName(scene, "00_SoloSpawnPoint");
            if (spawn == null)
            {
                spawn = FindTransformByName(scene, "00_SoloFirstSpawnPoint");
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(altarPrefab, scene) as GameObject;
            if (instance != null)
            {
                instance.name = HubAltarName;
                Vector3 origin = spawn != null ? spawn.position : Vector3.zero;
                Vector3 lateral = spawn != null ? spawn.right * 2.25f : Vector3.right * 2.25f;
                instance.transform.SetPositionAndRotation(origin + lateral + Vector3.up * 0.55f, Quaternion.identity);
                sceneChanged = true;
            }
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static bool AttachPacifiedRewardsToGhosts(Scene scene)
    {
        bool changed = false;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GhostController[] ghosts = roots[i].GetComponentsInChildren<GhostController>(true);
            for (int j = 0; j < ghosts.Length; j++)
            {
                GhostController ghost = ghosts[j];
                if (ghost == null || ghost.GetComponent<PacifiedMemoryReward>() != null)
                {
                    continue;
                }

                PacifiedMemoryReward reward = Undo.AddComponent<PacifiedMemoryReward>(ghost.gameObject);
                SerializedObject serializedReward = new SerializedObject(reward);
                serializedReward.FindProperty("optionalGhostRequirement").objectReferenceValue = ghost;
                serializedReward.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
        }

        return changed;
    }

    private static Transform FindTransformByName(Scene scene, string targetName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j] != null && transforms[j].name == targetName)
                {
                    return transforms[j];
                }
            }
        }

        return null;
    }

    private static Material EnsureMaterial(string path, Color color, Color emission)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            color = color
        };
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void AddSoftLight(GameObject root, Color color, float intensity, float range)
    {
        GameObject lightObject = new GameObject("MemoryLight");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = Vector3.up * 0.8f;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
