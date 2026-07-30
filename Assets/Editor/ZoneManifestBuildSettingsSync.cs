using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Garantit que toute scene declaree par un ZoneManifest est disponible dans
/// les Build Settings. La verification s'effectue apres une recompilation et
/// reste accessible manuellement depuis le menu Lit.
/// </summary>
[InitializeOnLoad]
internal static class ZoneManifestBuildSettingsSync
{
    private const string ScenesRoot = "Assets/Scenes";

    static ZoneManifestBuildSettingsSync()
    {
        EditorApplication.delayCall += EnsureManifestScenesAreInBuildSettings;
    }

    [MenuItem("Lit/Scenes/Ajouter les scenes des manifests aux Build Settings")]
    private static void EnsureManifestScenesAreInBuildSettings()
    {
        HashSet<string> requiredSceneNames = CollectRequiredSceneNames();
        if (requiredSceneNames.Count == 0)
        {
            return;
        }

        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        HashSet<string> existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EditorBuildSettingsScene buildScene in buildScenes)
        {
            existingPaths.Add(buildScene.path);
        }

        int addedCount = 0;
        foreach (string sceneName in requiredSceneNames)
        {
            string scenePath = FindScenePath(sceneName);
            if (string.IsNullOrWhiteSpace(scenePath) || !existingPaths.Add(scenePath))
            {
                continue;
            }

            buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            addedCount++;
        }

        if (addedCount <= 0)
        {
            return;
        }

        EditorBuildSettings.scenes = buildScenes.ToArray();
        Debug.Log($"[ZoneManifest] {addedCount} scene(s) ajoutee(s) aux Build Settings.");
    }

    private static HashSet<string> CollectRequiredSceneNames()
    {
        HashSet<string> sceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] manifestGuids = AssetDatabase.FindAssets("t:ZoneManifest");
        foreach (string manifestGuid in manifestGuids)
        {
            ZoneManifest manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(AssetDatabase.GUIDToAssetPath(manifestGuid));
            if (manifest == null)
            {
                continue;
            }

            AddSceneName(sceneNames, manifest.PrimarySceneName);
            AddSceneNames(sceneNames, manifest.LoadingSceneNames);
            AddSceneNames(sceneNames, manifest.PostLoadingSceneNames);
        }

        AddProximitySceneNames(sceneNames);

        return sceneNames;
    }

    private static void AddProximitySceneNames(HashSet<string> destination)
    {
        foreach (string scenePath in Directory.GetFiles(ScenesRoot, "*.unity", SearchOption.AllDirectories))
        {
            string contents = File.ReadAllText(scenePath);
            MatchCollection matches = Regex.Matches(contents, @"(?m)^\s*proximitySceneName:\s*(.+?)\s*$");
            foreach (Match match in matches)
            {
                AddSceneName(destination, match.Groups[1].Value.Trim());
            }
        }
    }

    private static void AddSceneNames(HashSet<string> destination, IReadOnlyList<string> names)
    {
        for (int i = 0; i < names.Count; i++)
        {
            AddSceneName(destination, names[i]);
        }
    }

    private static void AddSceneName(HashSet<string> destination, string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            destination.Add(sceneName);
        }
    }

    private static string FindScenePath(string sceneName)
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot });
        foreach (string sceneGuid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        Debug.LogWarning($"[ZoneManifest] La scene '{sceneName}' est introuvable sous {ScenesRoot}.");
        return null;
    }
}
