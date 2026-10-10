using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>Unity-generated serialized artifacts for an unavailable open Editor. Never overwrites the source workspace.</summary>
public static class ActorReconstructionExport
{
    [Serializable] public sealed class FileRecord { public string path, beforeHash, afterHash, guid; }
    [Serializable] public sealed class Export { public List<FileRecord> files = new List<FileRecord>(); public string status; }
    public static string Hash(string path)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","");
    }
    public static void Generate()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Library/ActorAnimationSandbox/Assets"))
            throw new InvalidOperationException("Export generation requires the isolated validation project.");
        string main = Directory.GetParent(Application.dataPath).Parent.Parent.FullName;
        var report = new Export();
        var paths = Directory.GetFiles(Path.Combine(main,ActorAnimationAudit.EnemyFolder + "Animations"),"*.anim")
            .Select(p => p.Substring(main.Length+1).Replace('\\','/'))
            .Where(p => ActorAnimationAudit.EnemySource(Path.GetFileNameWithoutExtension(p).Replace("Juggernaut_v2_","")) != null)
            .Concat(new[] {JuggernautV2Setup.PrefabPath}).ToArray();
        foreach (string path in paths)
        {
            string original = Path.Combine(main,path);
            report.files.Add(new FileRecord {path=path,beforeHash=Hash(original)});
            File.Copy(original,path,true); File.Copy(original + ".meta",path + ".meta",true);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
        // Revalidate against the current workspace snapshots, not stale laboratory copies.
        ActorClipReconstruction.Prevalidate(); ActorClipReconstruction.Apply();
        var prefab = PrefabUtility.LoadPrefabContents(JuggernautV2Setup.PrefabPath);
        try
        {
            var brain = prefab.GetComponent<LitBrainsEnemy>();
            brain.reconcileNavigationGroundHeight = true;
            PrefabUtility.SaveAsPrefabAsset(prefab,JuggernautV2Setup.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (var record in report.files)
        {
            record.afterHash = Hash(record.path); record.guid = AssetDatabase.AssetPathToGUID(record.path);
            string output = ActorAnimationAudit.ReportFolder + "/rollout/" + record.path;
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.Copy(record.path,output,true);
        }
        report.status = "Source fidelity/events verified by Unity; original workspace SHA256 must still match beforeHash before transfer. Locomotion and action-handoff rollout remains gated separately.";
        File.WriteAllText(ActorAnimationAudit.ReportFolder + "/rollout.json",JsonUtility.ToJson(report,true));
        Debug.Log("[ActorReconstructionExport] " + report.files.Count + " Unity-generated artifacts; no source workspace modified.");
    }
}
