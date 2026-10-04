using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Removes the current playable Munin implementation while retaining its source assets for lore work.</summary>
[InitializeOnLoad]
public static class MuninGameplayRemoval
{
    private static readonly string[] PlayerPrefabPaths =
    {
        "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab",
        "Assets/Characters/1_Squad/Link/Player_Model_Link.prefab",
        "Assets/Characters/1_Squad/Mia/Player_Model_Mia.prefab",
        "Assets/Characters/1_Squad/Luna/Player_Model_Luna.prefab"
    };

    private static readonly string[] FlamePrefabPaths =
    {
        "Assets/Prefabs/Model_Flame_WallTorch.prefab",
        "Assets/Prefabs/Model_Flame_Brazier.prefab",
        "Assets/Interactive/Flame/Interactive_Flame_Ancient.prefab",
        "Assets/Environment/Castle/Balconies/Balcony_1.prefab"
    };

    private static readonly string[] FlameScenePaths =
    {
        "Assets/Scenes/District_1/District_1_Corridor_Flammes.unity",
        "Assets/Scenes/District_1/District_1_Rooms_Flammes.unity",
        "Assets/Scenes/District_1/District_1_PuitsDeLaReleve_Environment.unity",
        "Assets/Scenes/District_1/District_1_ConduitsNoyés_Environement.unity",
        "Assets/Scenes/District_5.unity",
        "Assets/Scenes/Cycles/Cycle_Belmont/District_1_Cycle_Belmont.unity"
    };

    private static readonly string[] MuninUiScenePaths =
    {
        "Assets/Scenes/Bootstrap.unity",
        "Assets/Scenes/Arena.unity"
    };

    private const string RequestPath = "Library/MuninGameplayRemoval.request";
    private const string ResultPath = "Library/MuninGameplayRemoval.result";

    static MuninGameplayRemoval()
    {
        EditorApplication.delayCall += ProcessRequestedRemoval;
        EditorApplication.update += ProcessRequestedRemoval;
    }

    [MenuItem("Lit/Gameplay/Remove Munin Gameplay")]
    public static void RemoveMuninGameplay()
    {
        foreach (string path in PlayerPrefabPaths) RemoveMuninFromPlayerPrefab(path);
        foreach (string path in FlamePrefabPaths) ConfigureFlamePrefab(path);
        foreach (string path in FlameScenePaths) ConfigureFlamesInScene(path);
        foreach (string path in MuninUiScenePaths) RemoveMuninUiFromScene(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Lit Gameplay] Munin gameplay removed; Flames now use nearby direct interaction.");
    }

    private static void ProcessRequestedRemoval()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            File.Delete(RequestPath);
            RemoveMuninGameplay();
            File.WriteAllText(ResultPath, "success " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "failure\n" + exception);
            Debug.LogException(exception);
        }
    }

    private static void RemoveMuninFromPlayerPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var rootsToDestroy = new HashSet<GameObject>();
            foreach (MuninController controller in root.GetComponentsInChildren<MuninController>(true))
                if (controller != null) rootsToDestroy.Add(controller.gameObject);
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform != root.transform && transform.name == "Munin") rootsToDestroy.Add(transform.gameObject);

            foreach (GameObject candidate in rootsToDestroy)
            {
                if (candidate == null) continue;
                bool hasParentMarkedForRemoval = false;
                for (Transform parent = candidate.transform.parent; parent != null; parent = parent.parent)
                {
                    if (rootsToDestroy.Contains(parent.gameObject)) { hasParentMarkedForRemoval = true; break; }
                }
                if (!hasParentMarkedForRemoval) UnityEngine.Object.DestroyImmediate(candidate);
            }

            foreach (SpiritBondAnimationEvents events in root.GetComponentsInChildren<SpiritBondAnimationEvents>(true))
                UnityEngine.Object.DestroyImmediate(events);
            foreach (SpiritBondController bond in root.GetComponentsInChildren<SpiritBondController>(true))
                UnityEngine.Object.DestroyImmediate(bond);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureFlamePrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (Flame flame in root.GetComponentsInChildren<Flame>(true))
                flame.ConfigureDirectInteraction();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureFlamesInScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            foreach (Flame flame in Resources.FindObjectsOfTypeAll<Flame>())
                if (flame.gameObject.scene == scene) flame.ConfigureDirectInteraction();
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void RemoveMuninUiFromScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            foreach (MuninUI ui in Resources.FindObjectsOfTypeAll<MuninUI>())
                if (ui.gameObject.scene == scene) UnityEngine.Object.DestroyImmediate(ui.gameObject);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
