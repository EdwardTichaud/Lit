#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Decoupe la scene de lumieres de District_1 sans casser les prefabs ni les
/// references internes : le volume global reste disponible avant la reprise
/// du joueur, tandis que les lumieres decoratives sont chargees ensuite.
/// </summary>
internal static class District1LightingRebalancer
{
    private const string SourceScenePath = "Assets/Scenes/District_1/District_1_Loading_Lights.unity";
    private const string TargetScenePath = "Assets/Scenes/District_1/District_1_PostLoading_Lights.unity";
    private const string ManifestPath = "Assets/Scenes/Maison/ZoneManifest_District_1.asset";
    private const string SourceRootName = "03_Lights";
    private const string TargetRootName = "03_Lights_Decorative";

    // Ces familles sont visuelles. Elles ne sont pas necessaires au spawn,
    // aux collisions, a la camera ou a l'eclairage global de base.
    private static readonly string[] DecorativeRootNames =
    {
        "Candelabras",
        "ReflectionProbes",
        "Lanterns",
        "Chandeliers",
        "SpotLights"
    };

    [MenuItem("Tools/Lit/Scenes/Rebalance District 1 Lighting")]
    private static void Rebalance()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) != null)
        {
            EditorUtility.DisplayDialog(
                "District_1 deja reequilibre",
                "La sous-scene District_1_PostLoading_Lights existe deja. L'outil ne la remplace pas afin d'eviter toute duplication.",
                "OK");
            return;
        }

        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
        GameObject sourceRoot = FindRoot(sourceScene, SourceRootName);
        if (sourceRoot == null)
        {
            EditorUtility.DisplayDialog("Reequilibrage impossible", $"La racine '{SourceRootName}' est introuvable.", "OK");
            return;
        }

        List<Transform> rootsToMove = ResolveDecorativeRoots(sourceRoot.transform);
        if (rootsToMove.Count != DecorativeRootNames.Length)
        {
            EditorUtility.DisplayDialog(
                "Reequilibrage interrompu",
                "La structure de District_1_Loading_Lights a change. Aucune scene n'a ete modifiee.",
                "OK");
            return;
        }

        Scene targetScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        GameObject targetRoot = new GameObject(TargetRootName);
        SceneManager.MoveGameObjectToScene(targetRoot, targetScene);

        foreach (Transform decorativeRoot in rootsToMove)
        {
            // Un GameObject doit etre racine pour changer de scene. On conserve
            // ses coordonnees monde afin que le decor ne se decale pas.
            decorativeRoot.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(decorativeRoot.gameObject, targetScene);
            decorativeRoot.SetParent(targetRoot.transform, true);
        }

        EditorSceneManager.MarkSceneDirty(sourceScene);
        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(sourceScene);
        EditorSceneManager.SaveScene(targetScene, TargetScenePath);

        AddSceneToBuildSettings(TargetScenePath);
        UpdateManifest();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "District_1 reequilibre",
            "Le volume global est reste dans District_1_Loading_Lights. Lanternes, lustres, candelabres, sondes de reflexion et spots sont maintenant charges progressivement dans District_1_PostLoading_Lights.",
            "OK");
    }

    private static List<Transform> ResolveDecorativeRoots(Transform sourceRoot)
    {
        List<Transform> result = new List<Transform>(DecorativeRootNames.Length);
        foreach (string rootName in DecorativeRootNames)
        {
            Transform child = sourceRoot.Find(rootName);
            if (child == null)
            {
                Debug.LogError($"District1LightingRebalancer : racine '{rootName}' introuvable.");
                return new List<Transform>();
            }

            result.Add(child);
        }

        return result;
    }

    private static GameObject FindRoot(Scene scene, string rootName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, rootName, StringComparison.Ordinal))
            {
                return root;
            }
        }

        return null;
    }

    private static void UpdateManifest()
    {
        ZoneManifest manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
        SceneAsset targetScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath);
        if (manifest == null || targetScene == null)
        {
            Debug.LogError("District1LightingRebalancer : manifeste ou sous-scene introuvable.");
            return;
        }

        if (!manifest.postLoadingScenes.Contains(targetScene))
        {
            manifest.postLoadingScenes.Insert(0, targetScene);
        }

        SerializedObject serializedManifest = new SerializedObject(manifest);
        SerializedProperty postLoadingNames = serializedManifest.FindProperty("postLoadingSceneNames");
        if (postLoadingNames != null && !Contains(postLoadingNames, "District_1_PostLoading_Lights"))
        {
            postLoadingNames.InsertArrayElementAtIndex(0);
            postLoadingNames.GetArrayElementAtIndex(0).stringValue = "District_1_PostLoading_Lights";
        }

        serializedManifest.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manifest);
    }

    private static bool Contains(SerializedProperty property, string value)
    {
        for (int index = 0; index < property.arraySize; index++)
        {
            if (string.Equals(property.GetArrayElementAtIndex(index).stringValue, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
