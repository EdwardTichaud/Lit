#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public static class NetcodePrefabPreparationTool
{
    [MenuItem("Tools/Lit/Netcode/Scan Runtime Prefabs")]
    private static void ScanRuntimePrefabs()
    {
        RuntimePrefabPreparationReport report = BuildReport(prepareMissingNetworkObjects: false);
        Debug.Log(report.ToLogString("scan"));
    }

    [MenuItem("Tools/Lit/Netcode/Prepare Runtime Prefabs")]
    private static void PrepareRuntimePrefabs()
    {
        RuntimePrefabPreparationReport report = BuildReport(prepareMissingNetworkObjects: true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(report.ToLogString("prepare"));
    }

    private static RuntimePrefabPreparationReport BuildReport(bool prepareMissingNetworkObjects)
    {
        Dictionary<string, RuntimePrefabReference> prefabs = CollectRuntimePrefabs();
        RuntimePrefabPreparationReport report = new RuntimePrefabPreparationReport();

        foreach (KeyValuePair<string, RuntimePrefabReference> entry in prefabs)
        {
            string path = entry.Key;
            RuntimePrefabReference reference = entry.Value;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                report.missingPrefabs.Add($"{path} ({FormatSources(reference)})");
                continue;
            }

            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.PrefabIdHash != 0u)
            {
                report.readyPrefabs.Add($"{path} ({FormatSources(reference)})");
                continue;
            }

            if (!prepareMissingNetworkObjects)
            {
                report.legacyFallbackPrefabs.Add($"{path} ({FormatSources(reference)})");
                continue;
            }

            bool prepared = EnsureNetworkObjectOnPrefab(path);
            if (prepared)
            {
                report.preparedPrefabs.Add($"{path} ({FormatSources(reference)})");
            }
            else
            {
                report.failedPrefabs.Add($"{path} ({FormatSources(reference)})");
            }
        }

        return report;
    }

    private static Dictionary<string, RuntimePrefabReference> CollectRuntimePrefabs()
    {
        Dictionary<string, RuntimePrefabReference> results = new Dictionary<string, RuntimePrefabReference>();

        string[] characterGuids = AssetDatabase.FindAssets("t:CharacterData");
        for (int i = 0; i < characterGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(characterGuids[i]);
            CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
            if (character == null || character.model == null)
            {
                continue;
            }

            AddReference(results, character.model, $"CharacterData:{character.name}");
        }

        string[] itemGuids = AssetDatabase.FindAssets("t:Item");
        for (int i = 0; i < itemGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
            Item item = AssetDatabase.LoadAssetAtPath<Item>(assetPath);
            if (item == null)
            {
                continue;
            }

            GameObject prefab = item.ResolveWorldPrefab();
            if (prefab == null)
            {
                continue;
            }

            AddReference(results, prefab, $"Item:{item.name}");
        }

        return results;
    }

    private static void AddReference(Dictionary<string, RuntimePrefabReference> results, GameObject prefab, string source)
    {
        if (prefab == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!results.TryGetValue(path, out RuntimePrefabReference reference))
        {
            reference = new RuntimePrefabReference();
            results.Add(path, reference);
        }

        reference.sources.Add(source);
    }

    private static bool EnsureNetworkObjectOnPrefab(string path)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
        if (prefabRoot == null)
        {
            return false;
        }

        try
        {
            NetworkObject networkObject = prefabRoot.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = prefabRoot.AddComponent<NetworkObject>();
            }

            EditorUtility.SetDirty(prefabRoot);
            EditorUtility.SetDirty(networkObject);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (savedPrefab == null)
        {
            return false;
        }

        NetworkObject savedNetworkObject = savedPrefab.GetComponent<NetworkObject>();
        return savedNetworkObject != null && savedNetworkObject.PrefabIdHash != 0u;
    }

    private static string FormatSources(RuntimePrefabReference reference)
    {
        if (reference == null || reference.sources == null || reference.sources.Count == 0)
        {
            return "source inconnue";
        }

        return string.Join(", ", reference.sources);
    }

    private sealed class RuntimePrefabReference
    {
        public readonly HashSet<string> sources = new HashSet<string>();
    }

    private sealed class RuntimePrefabPreparationReport
    {
        public readonly List<string> readyPrefabs = new List<string>();
        public readonly List<string> preparedPrefabs = new List<string>();
        public readonly List<string> legacyFallbackPrefabs = new List<string>();
        public readonly List<string> missingPrefabs = new List<string>();
        public readonly List<string> failedPrefabs = new List<string>();

        public string ToLogString(string mode)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("NetcodePrefabPreparationTool: mode=").Append(mode);
            builder.Append(" ready=").Append(readyPrefabs.Count);
            builder.Append(" prepared=").Append(preparedPrefabs.Count);
            builder.Append(" legacyFallback=").Append(legacyFallbackPrefabs.Count);
            builder.Append(" missing=").Append(missingPrefabs.Count);
            builder.Append(" failed=").Append(failedPrefabs.Count);

            AppendSection(builder, "ready", readyPrefabs);
            AppendSection(builder, "prepared", preparedPrefabs);
            AppendSection(builder, "legacyFallback", legacyFallbackPrefabs);
            AppendSection(builder, "missing", missingPrefabs);
            AppendSection(builder, "failed", failedPrefabs);
            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string label, List<string> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.Append(label).Append(':');
            for (int i = 0; i < entries.Count; i++)
            {
                builder.AppendLine();
                builder.Append(" - ").Append(entries[i]);
            }
        }
    }
}
#endif
