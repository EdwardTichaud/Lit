using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HiddenRoomSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/Maison.unity";
    private const string HiddenRoomName = "HiddenRoom";
    private const string RootName = "Root";

    [MenuItem("Tools/Hidden Room/Refresh Existing Setup In Maison")]
    public static void InstallMaison()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException($"Impossible d'ouvrir la scene '{ScenePath}'.");
        }

        GameObject hiddenRoom = FindInScene(scene, HiddenRoomName);
        if (hiddenRoom == null)
        {
            throw new InvalidOperationException($"Objet '{HiddenRoomName}' introuvable dans '{ScenePath}'. Cree-le manuellement avant d'executer l'outil.");
        }

        Transform rootTransform = hiddenRoom.transform.Find(RootName);
        if (rootTransform == null)
        {
            throw new InvalidOperationException($"Objet '{HiddenRoomName}/{RootName}' introuvable dans '{ScenePath}'. Cree-le manuellement avant d'executer l'outil.");
        }

        HiddenRoomBootstrap bootstrap = rootTransform.GetComponent<HiddenRoomBootstrap>();
        if (bootstrap == null)
        {
            throw new InvalidOperationException($"Composant '{nameof(HiddenRoomBootstrap)}' manquant sur '{HiddenRoomName}/{RootName}'. Ajoute-le manuellement avant d'executer l'outil.");
        }

        bootstrap.EnsureSceneSetup();
        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException($"Echec de sauvegarde de la scene '{ScenePath}'.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("HiddenRoom: references existantes actualisees dans Maison.");
    }

    public static void InstallMaisonFromCommandLine()
    {
        InstallMaison();
    }

    private static GameObject FindInScene(Scene scene, string targetName)
    {
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform match = FindInHierarchy(roots[i].transform, targetName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindInHierarchy(Transform current, string targetName)
    {
        if (current == null)
        {
            return null;
        }

        if (string.Equals(current.name, targetName, StringComparison.Ordinal))
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform match = FindInHierarchy(current.GetChild(i), targetName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
