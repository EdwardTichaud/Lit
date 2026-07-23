using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Recuperation ponctuelle des Canvas disparus avec l'ancienne scene Maison.
/// Le decor "World" historique n'est volontairement pas restaure : il est
/// maintenant reparti entre les sous-scenes de Maison.
/// </summary>
[InitializeOnLoad]
public static class LitLegacyMaisonUiRecovery
{
    private const string RecoveryKey = "Lit.LegacyMaisonUiRecovery.Completed";
    private const string LegacyCommit = "d154cfe53";
    private const string LegacyScenePath = "Assets/Scenes/Maison/Maison.unity";
    private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
    private const string GameplaySessionPrefabPath = "Assets/Core/System/GameplaySessionRoot.prefab";
    private const string TemporaryScenePath = "Assets/__LegacyRecovery/Maison_d154cfe53_Recovery.unity";

    static LitLegacyMaisonUiRecovery()
    {
        // La recuperation a ete demandee explicitement. Le marqueur EditorPrefs
        // evite qu'elle ne se relance aux compilations suivantes.
        if (!EditorPrefs.GetBool(RecoveryKey, false))
        {
            EditorApplication.delayCall += RestoreOnce;
        }
    }

    [MenuItem("Lit/Recovery/Restore legacy Maison UI")]
    private static void RestoreFromMenu()
    {
        EditorPrefs.DeleteKey(RecoveryKey);
        RestoreOnce();
    }

    private static void RestoreOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RestoreOnce;
            return;
        }

        // Marque avant toute mutation : en cas d'echec, le menu permet une
        // relance explicite sans risquer une boucle automatique.
        EditorPrefs.SetBool(RecoveryKey, true);

        try
        {
            WriteTemporaryLegacyScene();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Scene sourceScene = EditorSceneManager.OpenScene(TemporaryScenePath, OpenSceneMode.Additive);
            GameObject overlay = FindInScene(sourceScene, "UI_Overlay");
            GameObject worldSpace = FindInScene(sourceScene, "UI_WorldSpace");
            if (overlay == null && worldSpace == null)
            {
                throw new InvalidOperationException("Les objets UI_Overlay et UI_WorldSpace sont absents de la scene historique.");
            }

            Scene bootstrapScene = GetOrOpenScene(BootstrapScenePath);
            GameObject applicationRoot = FindInScene(bootstrapScene, "ApplicationRoot");
            if (applicationRoot == null)
            {
                throw new InvalidOperationException("ApplicationRoot est introuvable dans Bootstrap.");
            }

            if (overlay != null)
            {
                RestoreObjectIfMissing(overlay, "UI_Overlay", bootstrapScene, applicationRoot.transform);
            }

            // UI_WorldSpace est parfois deja enfant de UI_Overlay. Dans ce cas
            // la copie de l'overlay suffit et une seconde copie serait un doublon.
            if (worldSpace != null && (overlay == null || !worldSpace.transform.IsChildOf(overlay.transform)))
            {
                RestoreWorldSpaceIntoGameplaySession(worldSpace);
            }

            EditorSceneManager.SaveScene(bootstrapScene);
            EditorSceneManager.CloseScene(sourceScene, true);
            AssetDatabase.DeleteAsset("Assets/__LegacyRecovery");
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[Legacy UI Recovery] UI_Overlay et UI_WorldSpace ont ete recuperes depuis l'ancienne Maison.");
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"[Legacy UI Recovery] Echec de la recuperation : {exception.Message}");
        }
    }

    private static void RestoreWorldSpaceIntoGameplaySession(GameObject source)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(GameplaySessionPrefabPath);
        try
        {
            if (FindInHierarchy(prefabRoot.transform, "UI_WorldSpace") == null)
            {
                GameObject copy = UnityEngine.Object.Instantiate(source);
                copy.name = "UI_WorldSpace";
                copy.transform.SetParent(prefabRoot.transform, true);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, GameplaySessionPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void RestoreObjectIfMissing(GameObject source, string objectName, Scene targetScene, Transform parent)
    {
        if (FindInScene(targetScene, objectName) != null)
        {
            return;
        }

        GameObject copy = UnityEngine.Object.Instantiate(source);
        copy.name = objectName;
        SceneManager.MoveGameObjectToScene(copy, targetScene);
        copy.transform.SetParent(parent, true);
    }

    private static void WriteTemporaryLegacyScene()
    {
        string yaml = RunGitShow();
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new InvalidOperationException("Git n'a retourne aucun contenu pour l'ancienne scene Maison.");
        }

        string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, TemporaryScenePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllText(absolutePath, yaml);
    }

    private static string RunGitShow()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"show {LegacyCommit}:{LegacyScenePath}",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(info))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(error);
            }

            return output;
        }
    }

    private static Scene GetOrOpenScene(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        return scene.IsValid() && scene.isLoaded
            ? scene
            : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindInHierarchy(root.transform, objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindInHierarchy(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }
}
