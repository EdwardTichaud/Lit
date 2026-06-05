using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LitPerformanceAuditUtility
{
    private const string MenuRoot = "Tools/Lit/Performance/";

    private static readonly string[] HeavyAssetRoots =
    {
        "Assets/0 - UnityPackages",
        "Assets/0 - UnityPackages/Fab",
        "Assets/0 - UnityPackages/UnityAssets/StarterAssets",
        "Assets/0 - UnityPackages/UnityAssets/GalaxyBox2",
        "Assets/CharacterControllerLegacy",
        "Assets/WorldMaterialsFree",
        "Assets/Audio",
        "Assets/Lucian_CC5_Embed"
    };

    [MenuItem(MenuRoot + "Print Build Dependency Audit")]
    public static void PrintBuildDependencyAudit()
    {
        string[] scenePaths = GetEnabledBuildScenePaths();
        if (scenePaths.Length == 0)
        {
            Debug.LogWarning("[PerformanceAudit] No enabled build scenes found.");
            return;
        }

        HashSet<string> dependencies = new HashSet<string>(
            AssetDatabase.GetDependencies(scenePaths, recursive: true),
            StringComparer.Ordinal);

        List<string> lines = new List<string>
        {
            "[PerformanceAudit] Build dependency audit",
            "Scenes:",
        };

        for (int i = 0; i < scenePaths.Length; i++)
        {
            lines.Add($"- {scenePaths[i]}");
        }

        lines.Add("Heavy roots:");
        for (int i = 0; i < HeavyAssetRoots.Length; i++)
        {
            AppendRootAudit(lines, HeavyAssetRoots[i], dependencies);
        }

        Debug.Log(string.Join("\n", lines));
    }

    private static string[] GetEnabledBuildScenePaths()
    {
        List<string> scenePaths = new List<string>();
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            EditorBuildSettingsScene scene = scenes[i];
            if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
            {
                scenePaths.Add(scene.path);
            }
        }

        return scenePaths.ToArray();
    }

    private static void AppendRootAudit(List<string> lines, string root, HashSet<string> dependencies)
    {
        if (!AssetDatabase.IsValidFolder(root))
        {
            lines.Add($"- {root}: missing");
            return;
        }

        string fullPath = Path.GetFullPath(root);
        long totalBytes = GetDirectorySize(fullPath);
        int fileCount = 0;
        int dependencyCount = 0;
        long dependencyBytes = 0;

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { root });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                continue;
            }

            fileCount++;
            if (!dependencies.Contains(path))
            {
                continue;
            }

            dependencyCount++;
            string assetFullPath = Path.GetFullPath(path);
            if (File.Exists(assetFullPath))
            {
                dependencyBytes += new FileInfo(assetFullPath).Length;
            }
        }

        string status = dependencyCount == 0 ? "candidate for removal after manual review" : "referenced by build scenes";
        lines.Add(
            $"- {root}: {FormatBytes(totalBytes)}, files={fileCount}, buildDeps={dependencyCount}, buildDepSize={FormatBytes(dependencyBytes)}; {status}");
    }

    private static long GetDirectorySize(string fullPath)
    {
        if (!Directory.Exists(fullPath))
        {
            return 0L;
        }

        long bytes = 0L;
        string[] files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            bytes += new FileInfo(files[i]).Length;
        }

        return bytes;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
