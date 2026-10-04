using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LitFlameLightingMigration
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/District_1/District_1_Corridor_Flammes.unity",
        "Assets/Scenes/District_1/District_1_Rooms_Flammes.unity",
        "Assets/Scenes/District_1/District_1_PuitsDeLaReleve_Environment.unity",
        "Assets/Scenes/District_5.unity"
    };

    private static readonly string[] PrefabPaths =
    {
        "Assets/Interactive/Flame/Light_Flame_Fire_Common.prefab",
        "Assets/Interactive/Flame/Light_Flame_Fire_Ancient.prefab",
        "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab"
    };

    [MenuItem("Lit/Lighting/Migrate Flame Contrast Lights")]
    public static void Migrate()
    {
        foreach (string prefabPath in PrefabPaths)
            ApplyProfilesToPrefab(prefabPath);

        int reverted = 0;
        for (int i = 0; i < ScenePaths.Length; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Additive);
            foreach (LitContrastLight profile in Object.FindObjectsByType<LitContrastLight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (profile.gameObject.scene != scene) continue;
                reverted += RevertLegacyLightOverrides(profile);
            }
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Lit Lighting] Updated {PrefabPaths.Length} prefabs and removed {reverted} inherited light overrides.");
    }

    [MenuItem("Lit/Lighting/Validate Flame Contrast Lights")]
    public static void Validate()
    {
        var failures = new List<string>();
        ValidatePrefab(PrefabPaths[0], 1, failures);
        ValidatePrefab(PrefabPaths[1], 2, failures);
        ValidatePrefab(PrefabPaths[2], 1, failures);
        if (failures.Count > 0)
        {
            Debug.LogError("[Lit Lighting] Validation failed:\n" + string.Join("\n", failures));
            return;
        }
        Debug.Log("[Lit Lighting] Flame contrast-light prefabs are valid.");
    }

    private static void ApplyProfilesToPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (LitContrastLight profile in root.GetComponentsInChildren<LitContrastLight>(true))
                profile.ApplySettings();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int RevertLegacyLightOverrides(LitContrastLight profile)
    {
        int reverted = 0;
        Light light = profile.GetComponent<Light>();
        if (light != null)
            reverted += RemoveVisualOverrides(light, IsLegacyUnityLightProperty);
        var hdLight = profile.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        if (hdLight != null)
            reverted += RemoveVisualOverrides(hdLight, propertyPath => propertyPath != "m_Enabled");
        return reverted;
    }

    private static int RemoveVisualOverrides(Object component, System.Predicate<string> shouldRemove)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(component)) return 0;
        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(component);
        if (modifications == null || modifications.Length == 0) return 0;

        var kept = new List<PropertyModification>(modifications.Length);
        int removed = 0;
        foreach (PropertyModification modification in modifications)
        {
            if (shouldRemove(modification.propertyPath)) removed++;
            else kept.Add(modification);
        }
        if (removed > 0) PrefabUtility.SetPropertyModifications(component, kept.ToArray());
        return removed;
    }

    private static bool IsLegacyUnityLightProperty(string propertyPath)
    {
        return propertyPath == "m_Range"
            || propertyPath == "m_Intensity"
            || propertyPath == "m_Type"
            || propertyPath == "m_InnerSpotAngle"
            || propertyPath.StartsWith("m_Color")
            || propertyPath.StartsWith("m_Shadows");
    }

    private static void ValidatePrefab(string path, int expectedMinimum, List<string> failures)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (root == null) { failures.Add("Missing prefab: " + path); return; }
        LitContrastLight[] profiles = root.GetComponentsInChildren<LitContrastLight>(true);
        if (profiles.Length < expectedMinimum) failures.Add($"{path}: expected at least {expectedMinimum} LitContrastLight components, got {profiles.Length}.");
        foreach (LitContrastLight light in profiles)
            if (light.GetComponent<Light>() == null) failures.Add($"{path}: {light.name} has no Unity Light.");
    }
}
