using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Audit leger des scenes declarees dans les ZoneManifest. Il lit les scenes
/// sans les ouvrir afin de ne jamais modifier l'espace de travail courant.
/// </summary>
public static class ZoneManifestPhaseAudit
{
    [MenuItem("Lit/Performance/Auditer les phases de scenes")]
    private static void AuditAllManifests()
    {
        string[] manifestGuids = AssetDatabase.FindAssets("t:ZoneManifest");
        if (manifestGuids.Length == 0)
        {
            Debug.Log("[ScenePhaseAudit] Aucun ZoneManifest trouve.");
            return;
        }

        Debug.Log("[ScenePhaseAudit] Debut de l'audit. Une phase lourde doit etre decoupee avant d'etre chargee pendant le jeu.");
        for (int manifestIndex = 0; manifestIndex < manifestGuids.Length; manifestIndex++)
        {
            string path = AssetDatabase.GUIDToAssetPath(manifestGuids[manifestIndex]);
            ZoneManifest manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(path);
            if (manifest == null || !manifest.IsValid)
            {
                continue;
            }

            AuditScene(manifest.name, "Critical", manifest.PrimarySceneName);
            for (int i = 0; i < manifest.LoadingSceneNames.Count; i++)
            {
                AuditScene(manifest.name, "Loading", manifest.LoadingSceneNames[i]);
            }

            for (int i = 0; i < manifest.PostLoadingSceneNames.Count; i++)
            {
                AuditScene(manifest.name, "PostLoading", manifest.PostLoadingSceneNames[i]);
            }
        }
    }

    private static void AuditScene(string manifestName, string phase, string sceneName)
    {
        string path = FindScenePath(sceneName);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Debug.LogWarning($"[ScenePhaseAudit] {manifestName} / {phase} : scene introuvable '{sceneName}'.");
            return;
        }

        string contents = File.ReadAllText(path);
        int gameObjects = Count(contents, @"(?m)^--- !u!1 &");
        int behaviours = Count(contents, @"(?m)^--- !u!114 &");
        int renderers = Count(contents, @"(?m)^--- !u!23 &");
        int lights = Count(contents, @"(?m)^--- !u!108 &");
        float megabytes = new FileInfo(path).Length / (1024f * 1024f);
        string warning = IsPotentiallyHeavy(phase, gameObjects, behaviours, renderers, lights)
            ? "  <-- a decouper si un pic est observe"
            : string.Empty;

        Debug.Log(
            $"[ScenePhaseAudit] {manifestName} / {phase} / {sceneName} | " +
            $"{megabytes:0.00} MB | {gameObjects} objets | {renderers} renderers | " +
            $"{lights} lights | {behaviours} composants{warning}");
    }

    private static bool IsPotentiallyHeavy(string phase, int gameObjects, int behaviours, int renderers, int lights)
    {
        if (phase == "Critical")
        {
            return gameObjects > 150 || behaviours > 100 || renderers > 100 || lights > 20;
        }

        return gameObjects > 400 || behaviours > 200 || renderers > 250 || lights > 80;
    }

    private static int Count(string contents, string pattern)
    {
        return Regex.Matches(contents, pattern).Count;
    }

    private static string FindScenePath(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return null;
        }

        string[] guids = AssetDatabase.FindAssets($"t:Scene {sceneName}");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                return path;
            }
        }

        return null;
    }
}
