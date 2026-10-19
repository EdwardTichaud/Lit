using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Explicit read-only audit. Removal is performed separately after verification.
public static class LitDownloadedAssetAudit
{
    private const string PackRoot = "Assets/0 - UnityPackages/Fab/";
    private const string Report = "Library/LitDownloadedAssetAudit.json";

    [Serializable]
    private sealed class ScopeConfiguration { public string[] roots; }

    [Serializable]
    public sealed class CodeReferences
    {
        public string path;
        public string[] tokens;
    }

    [Serializable]
    public sealed class Entry
    {
        public string path;
        public string guid;
        public long bytes;
    }

    [Serializable]
    public sealed class Audit
    {
        public string project;
        public string utc;
        public string unityVersion;
        public string scope;
        public string[] scopes;
        public int rootCount;
        public string[] preserved;
        public Entry[] candidates;
        public CodeReferences[] codeReferences;
    }

    [MenuItem("Tools/Lit/Assets/Audit Downloaded Packs (Read Only)")]
    public static void Run()
    {
        try
        {
            string[] scopes = JsonUtility.FromJson<ScopeConfiguration>(File.ReadAllText("Tools/AssetCleanup/scopes.json")).roots;
            if (scopes == null || scopes.Length == 0 || scopes.Any(path => !path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith("/", StringComparison.Ordinal) || path.Contains("..")))
                throw new InvalidDataException("Invalid downloaded asset scope configuration.");
            if (File.Exists(Report + ".error.txt")) File.Delete(Report + ".error.txt");
            string[] paths = AssetDatabase.GetAllAssetPaths();
            string[] roots = paths.Where(path => !AssetDatabase.IsValidFolder(path) &&
                (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal)) &&
                (!InScope(path, scopes) || IsProtected(path))).ToArray();
            var keep = new HashSet<string>(roots, StringComparer.Ordinal);
            var codeReferences = new List<CodeReferences>();
            foreach (string settings in Directory.GetFiles("ProjectSettings"))
            {
                if (!settings.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (Match match in Regex.Matches(File.ReadAllText(settings), "guid:\\s*([a-fA-F0-9]{32})"))
                {
                    string referenced = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                    if (!string.IsNullOrEmpty(referenced)) keep.Add(referenced);
                }
            }
            // Authoring tools also refer to source assets by path, outside the
            // serialized Unity dependency graph.
            foreach (string root in roots)
            {
                string extension = Path.GetExtension(root).ToLowerInvariant();
                if ((extension != ".cs" && extension != ".json" && extension != ".xml" && extension != ".csv") || !File.Exists(root)) continue;
                string text = File.ReadAllText(root);
                if (extension == ".cs")
                {
                    codeReferences.Add(new CodeReferences { path = root,
                        tokens = Regex.Matches(text, "\"(Assets/[^\"\\r\\n]+)\"|[a-fA-F0-9]{32}")
                            .Cast<Match>().Select(match => match.Value).Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray() });
                }
                foreach (Match match in Regex.Matches(text, "\"(Assets/[^\"\\r\\n]+)\""))
                {
                    string referenced = match.Groups[1].Value;
                    if (File.Exists(referenced)) keep.Add(referenced);
                }
            }
            roots = keep.ToArray();
            keep.UnionWith(AssetDatabase.GetDependencies(roots, true));
            string[] keptFolders = keep.Where(path => InScope(path, scopes) && AssetDatabase.IsValidFolder(path)).ToArray();
            string[] folderChildren = paths.Where(path => keptFolders.Any(folder => path.StartsWith(folder + "/", StringComparison.Ordinal))).ToArray();
            if (folderChildren.Length > 0) keep.UnionWith(AssetDatabase.GetDependencies(folderChildren, true));
            var candidates = new List<Entry>();
            foreach (string path in paths)
            {
                if (!InScope(path, scopes) || AssetDatabase.IsValidFolder(path) || keep.Contains(path)) continue;
                if (!File.Exists(path) || !File.Exists(path + ".meta")) continue;
                candidates.Add(new Entry { path = path, guid = AssetDatabase.AssetPathToGUID(path), bytes = new FileInfo(path).Length });
            }
            var audit = new Audit
            {
                project = Path.GetFullPath("."), utc = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion, scope = "downloaded-packs-v2", scopes = scopes, rootCount = roots.Length,
                preserved = keep.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                codeReferences = codeReferences.ToArray(),
                candidates = candidates.OrderBy(entry => entry.path, StringComparer.Ordinal).ToArray()
            };
            File.WriteAllText(Report, JsonUtility.ToJson(audit, true));
            Debug.Log("[DownloadedAssetAudit] Read-only audit complete: " + candidates.Count +
                " candidates, " + candidates.Sum(entry => entry.bytes) + " bytes. Report: " + Report);
        }
        catch (Exception exception)
        {
            File.WriteAllText(Report + ".error.txt", exception.ToString());
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static bool InScope(string path, string[] scopes)
    {
        return scopes.Any(root => path.StartsWith(root, StringComparison.Ordinal));
    }

    private static bool IsProtected(string path)
    {
        // Animation installers select files by constructed names in these two
        // packs. Keep the complete libraries until those selectors are audited.
        if (path.StartsWith(PackRoot + "TwinBladesBundle/", StringComparison.Ordinal) ||
            path.StartsWith(PackRoot + "Mobility_Starter/", StringComparison.Ordinal)) return true;
        if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("/StreamingAssets/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".cs": case ".dll": case ".asmdef": case ".asmref":
            case ".shader": case ".shadergraph": case ".shadersubgraph":
            case ".compute": case ".hlsl": case ".cginc": case ".vfx": case ".raytrace":
            case ".uxml": case ".uss": case ".glsl": case ".cg":
            case ".json": case ".xml": case ".txt": case ".csv":
                return true;
            default: return false;
        }
    }
}
