using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LucianAnimationFolderUtility
{
    public const string Destination = "Assets/Characters/1_Squad/Lucian/Animation";
    private const string OldGenerated = "Assets/Characters/4_Animations/PlayerInPlace";
    private const string NewGenerated = Destination + "/PlayerInPlace";
    [Serializable] public sealed class Move { public string source, target, guid, hash; }
    [Serializable] public sealed class Moves { public Move[] assets; }

    [MenuItem("Lit/Animation/Organize Lucian Animation Folder")]
    public static void Organize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var clips = PlayerInPlaceAudit.Collect(new List<string>());
        // Threshold QTE clips are injected into the player Animator as well.
        var paths = clips
            .Select(pair => AssetDatabase.GetAssetPath(pair.Key)).Distinct().OrderBy(p => p).ToArray();
        var moves = new List<Move>();
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (path.StartsWith(Destination + "/", StringComparison.Ordinal)) continue;
            string guid = AssetDatabase.AssetPathToGUID(path);
            string target = path.StartsWith(OldGenerated + "/", StringComparison.Ordinal)
                ? NewGenerated + path.Substring(OldGenerated.Length) : Destination + "/" + Path.GetFileName(path);
            if (!targets.Add(target))
            {
                target = Destination + "/" + Path.GetFileNameWithoutExtension(path) + "_" + guid.Substring(0, 8) + Path.GetExtension(path);
                if (!targets.Add(target)) throw new InvalidOperationException("Ambiguous destination: " + path);
            }
            if (File.Exists(target)) throw new InvalidOperationException("Destination already exists: " + target);
            moves.Add(new Move { source = path, target = target, guid = guid, hash = PlayerInPlaceMigration.Hash(path) });
        }
        if (moves.Count == 0) { Debug.Log("[Lucian Animation] Already organized."); return; }
        var clipIds = clips.Keys.ToDictionary(c => c, c => {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string guid, out long id); return guid + ":" + id;
        });
        string oldManifest = OldGenerated + "/Editor/MigrationManifest.json";
        var manifest = JsonUtility.FromJson<PlayerInPlaceMigration.Manifest>(File.ReadAllText(oldManifest));
        string Remap(string path) => path.StartsWith(OldGenerated + "/", StringComparison.Ordinal) ? NewGenerated + path.Substring(OldGenerated.Length)
            : moves.FirstOrDefault(m => m.source == path)?.target ?? path;
        EnsureFolder(Destination);
        File.WriteAllText("Library/LucianAnimationMoves.json", JsonUtility.ToJson(new Moves { assets = moves.ToArray() }, true));
        var completed = new List<KeyValuePair<string, string>>();
        try
        {
            string error = AssetDatabase.MoveAsset(OldGenerated, NewGenerated);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            completed.Add(new KeyValuePair<string, string>(OldGenerated, NewGenerated));
            foreach (var move in moves.Where(m => !m.source.StartsWith(OldGenerated + "/", StringComparison.Ordinal)))
            {
                error = AssetDatabase.MoveAsset(move.source, move.target);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                completed.Add(new KeyValuePair<string, string>(move.source, move.target));
            }
            foreach (var move in moves)
                if (AssetDatabase.AssetPathToGUID(move.target) != move.guid || PlayerInPlaceMigration.Hash(move.target) != move.hash)
                    throw new InvalidOperationException("Resource changed while moving: " + move.source);
            foreach (var pair in clipIds)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Key, out string guid, out long id);
                if (pair.Value != guid + ":" + id) throw new InvalidOperationException("Clip identity changed: " + pair.Key.name);
            }
            foreach (var record in manifest.replacements) { record.sourcePath = Remap(record.sourcePath); record.targetPath = Remap(record.targetPath); }
            foreach (var file in manifest.protectedFiles) file.path = Remap(file.path);
            File.WriteAllText(NewGenerated + "/Editor/MigrationManifest.json", JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(NewGenerated + "/Editor/MigrationManifest.json");
            Debug.Log($"[Lucian Animation] Moved {moves.Count} animation resources. GUIDs, subclips and file contents unchanged.");
        }
        catch
        {
            foreach (var move in completed.AsEnumerable().Reverse())
            {
                string error = AssetDatabase.MoveAsset(move.Value, move.Key);
                if (!string.IsNullOrEmpty(error)) Debug.LogError("Animation move rollback: " + error);
            }
            throw;
        }
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
