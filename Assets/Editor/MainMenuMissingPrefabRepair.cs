using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Supprime le residu d'un prefab VFX retire du projet mais encore reference par MainMenu.</summary>
[InitializeOnLoad]
internal static class MainMenuMissingPrefabRepair
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu/MainMenu.unity";

    static MainMenuMissingPrefabRepair()
    {
        EditorApplication.delayCall += RepairMissingPrefab;
    }

    [MenuItem("Lit/Scenes/Repair MainMenu Missing Prefab")]
    private static void RepairMissingPrefab()
    {
        Scene scene = EditorSceneManager.GetSceneByPath(MainMenuScenePath);
        bool openedForRepair = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
            openedForRepair = true;
        }

        List<GameObject> missingRoots = new List<GameObject>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                GameObject candidate = transforms[j].gameObject;
                if (PrefabUtility.GetPrefabAssetType(candidate) == PrefabAssetType.MissingAsset)
                {
                    missingRoots.Add(candidate);
                }
            }
        }

        for (int i = 0; i < missingRoots.Count; i++)
        {
            GameObject candidate = missingRoots[i];
            if (candidate != null)
            {
                Object.DestroyImmediate(candidate);
            }
        }

        if (missingRoots.Count > 0)
        {
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MainMenuMissingPrefabRepair] {missingRoots.Count} prefab(s) manquant(s) supprime(s) de MainMenu.");
        }

        if (openedForRepair)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
