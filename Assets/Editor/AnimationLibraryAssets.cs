using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Resolves paths assembled by existing animation authoring tools after library organization.</summary>
public static class AnimationLibraryAssets
{
    private const string MappingPath = "Assets/Editor/AnimationLibraryPaths.json";
    [Serializable] private sealed class Entry { public string source, guid; }
    [Serializable] private sealed class Mapping { public Entry[] assets; }
    private static Dictionary<string, string> movedGuids;

    private static Dictionary<string, string> MovedGuids
    {
        get
        {
            if (movedGuids == null)
            {
                var mapping = JsonUtility.FromJson<Mapping>(File.ReadAllText(MappingPath));
                movedGuids = mapping.assets.ToDictionary(entry => entry.source, entry => entry.guid,
                    StringComparer.OrdinalIgnoreCase);
            }
            return movedGuids;
        }
    }

    public static string Resolve(string path)
    {
        if (string.IsNullOrEmpty(path) || !MovedGuids.TryGetValue(path.Replace('\\', '/'), out string guid))
            return path;

        string currentPath = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(currentPath) ? path : currentPath;
    }

    public static bool IsWithin(string path, string originalFolder)
    {
        string prefix = originalFolder.TrimEnd('/') + "/";
        if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;
        string guid = AssetDatabase.AssetPathToGUID(path);
        return MovedGuids.Any(pair => pair.Value == guid &&
            pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    [MenuItem("Lit/Animation/Validate Organized Library")]
    public static void ValidateOrganization()
    {
        foreach (var pair in MovedGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(pair.Value);
            if (!path.StartsWith("Assets/Animations/", StringComparison.Ordinal))
                throw new InvalidOperationException("Animation is outside the organized library: " + pair.Key);
        }
        Debug.Log($"Animation library contains {MovedGuids.Count} relocated resources with their original GUIDs.");
    }

    public static T LoadAssetAtPath<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(Resolve(path));

    public static Object LoadAssetAtPath(string path, Type type) =>
        AssetDatabase.LoadAssetAtPath(Resolve(path), type);

    public static Object[] LoadAllAssetsAtPath(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(Resolve(path));

    public static AssetImporter GetImporterAtPath(string path) => AssetImporter.GetAtPath(Resolve(path));

    public static void CreateAsset(Object asset, string path) => AssetDatabase.CreateAsset(asset, Resolve(path));

    public static string[] FindAssets(string filter) => AssetDatabase.FindAssets(filter);

    public static string[] FindAssets(string filter, string[] folders)
    {
        if (folders == null)
            return AssetDatabase.FindAssets(filter);

        var relocated = MovedGuids.Where(pair => folders.Any(folder =>
            pair.Key.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))
            .Select(pair => pair.Value).ToHashSet();
        var result = new HashSet<string>();
        string[] existing = folders.Where(AssetDatabase.IsValidFolder).ToArray();
        if (existing.Length > 0)
            result.UnionWith(AssetDatabase.FindAssets(filter, existing));
        string[] destinations = relocated.Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => Path.GetDirectoryName(path).Replace('\\', '/')).Distinct().ToArray();
        if (destinations.Length > 0)
            result.UnionWith(AssetDatabase.FindAssets(filter, destinations).Where(relocated.Contains));
        return result.OrderBy(guid => guid, StringComparer.Ordinal).ToArray();
    }
}
