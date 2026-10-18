using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit isolated authoring only. RunBatch defaults to dry-run; never runs on import.</summary>
public static class CombatDiagnosticCleanupBatch
{
    private const string DefaultPlan = "Library/ProductionCombatMigration/diagnostic-cleanup-plan.json";
    [Serializable] public sealed class FilePlan
    {
        public string path, beforeSha256, afterSha256;
        public string[] behaviourIds;
        public int references, orphans;
    }
    [Serializable] public sealed class Plan
    {
        public string schema = "combat-diagnostic-cleanup-v1", root, scriptGuid = CombatDiagnosticCleanupPlan.ScriptGuid,
            typeName = CombatDiagnosticCleanupPlan.TypeName, deletionCommit = CombatDiagnosticCleanupPlan.DeletionCommit;
        public int controllerCount, behaviourCount, referenceCount, orphanCount;
        public List<FilePlan> files = new List<FilePlan>();
    }

    public static Plan DryRun(string projectRoot)
    {
        string root = Path.GetFullPath(projectRoot);
        var plan = new Plan { root = root };
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.controller", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[] before = File.ReadAllBytes(path);
            string source = CombatDiagnosticCleanupPlan.Decode(before);
            if (source.IndexOf(CombatDiagnosticCleanupPlan.ScriptGuid, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var result = CombatDiagnosticCleanupPlan.Build(source);
            plan.files.Add(new FilePlan { path = path.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/'),
                beforeSha256 = CombatDiagnosticCleanupPlan.Hash(before), afterSha256 = CombatDiagnosticCleanupPlan.Hash(CombatDiagnosticCleanupPlan.EncodeLike(before, result.text)),
                behaviourIds = result.behaviourIds, references = result.removedReferences, orphans = result.orphanedBehaviours });
            plan.behaviourCount += result.behaviourIds.Length; plan.referenceCount += result.removedReferences; plan.orphanCount += result.orphanedBehaviours;
        }
        plan.controllerCount = plan.files.Count;
        return plan;
    }

    public static void ApplyApproved(string projectRoot, string parentRoot, string planPath, string approvedPlanSha256)
    {
        string root = Path.GetFullPath(projectRoot), parent = Path.GetFullPath(parentRoot);
        if (!Same(root, Path.GetFullPath(".")) || !Same(root, Path.GetDirectoryName(Application.dataPath)))
            throw new InvalidOperationException("Apply must execute inside the active isolated Unity project, not operate on another project's files.");
        if (HasGit(root) || !HasGit(parent) || Same(root, parent) || parent.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Apply requires an isolated project without Git metadata and an explicit distinct parent checkout. Main checkout Apply is forbidden.");
        if (!File.Exists(Path.Combine(root, "ProjectSettings/ProjectVersion.txt"))) throw new InvalidOperationException("Not a Unity project.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Wait for import/compilation and stop Play Mode before isolated authoring cleanup.");
        byte[] approvedBytes = File.ReadAllBytes(planPath);
        if (string.IsNullOrEmpty(approvedPlanSha256) || CombatDiagnosticCleanupPlan.Hash(approvedBytes) != approvedPlanSha256)
            throw new InvalidOperationException("Explicit approved plan SHA256 mismatch.");
        string approved = System.Text.Encoding.UTF8.GetString(approvedBytes).TrimStart('\uFEFF');
        var original = JsonUtility.FromJson<Plan>(approved);
        if (original == null || !Same(original.root, root)) throw new InvalidOperationException("Plan belongs to another project root.");
        RefuseRestoredDiagnostic();
        var current = DryRun(root);
        if (JsonUtility.ToJson(original) != JsonUtility.ToJson(current)) throw new InvalidOperationException("GUID/type/count/hash preflight differs from approved plan; rerun dry-run.");
        var originals = new Dictionary<string, byte[]>();
        var replacements = new Dictionary<string, byte[]>();
        foreach (var file in current.files)
        {
            string physical = Path.GetFullPath(Path.Combine(root, file.path));
            if (!physical.StartsWith(Path.Combine(root, "Assets") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Plan escaped Assets.");
            for (string directory = Path.GetDirectoryName(physical); directory != null; directory = Path.GetDirectoryName(directory))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Reparse-point isolation cannot be verified: " + directory);
                if (Same(directory, root)) break;
            }
            if ((File.GetAttributes(physical) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Linked controller refused.");
            byte[] before = File.ReadAllBytes(physical);
            if (CombatDiagnosticCleanupPlan.Hash(before) != file.beforeSha256) throw new InvalidOperationException("Controller changed during preflight: " + file.path);
            byte[] after = CombatDiagnosticCleanupPlan.EncodeLike(before, CombatDiagnosticCleanupPlan.Build(CombatDiagnosticCleanupPlan.Decode(before)).text);
            if (CombatDiagnosticCleanupPlan.Hash(after) != file.afterSha256) throw new InvalidOperationException("Output differs from approved plan: " + file.path);
            originals.Add(physical, before); replacements.Add(physical, after);
        }
        string backup = Path.Combine(root, "Library/ProductionCombatMigration/DiagnosticCleanupBackups", approvedPlanSha256);
        foreach (var file in current.files)
        {
            string destination = Path.Combine(backup, file.path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllBytes(destination, originals[Path.GetFullPath(Path.Combine(root, file.path))]);
        }
        var written = new List<string>();
        try
        {
            foreach (var pair in replacements)
            {
                if (CombatDiagnosticCleanupPlan.Hash(File.ReadAllBytes(pair.Key)) != CombatDiagnosticCleanupPlan.Hash(originals[pair.Key])) throw new InvalidOperationException("Concurrent controller edit: " + pair.Key);
                written.Add(pair.Key); File.WriteAllBytes(pair.Key, pair.Value);
                if (CombatDiagnosticCleanupPlan.Hash(File.ReadAllBytes(pair.Key)) != CombatDiagnosticCleanupPlan.Hash(pair.Value)) throw new IOException("Written controller hash mismatch.");
            }
        }
        catch { foreach (string path in written) File.WriteAllBytes(path, originals[path]); throw; }
        foreach (var file in current.files) AssetDatabase.ImportAsset(file.path, ImportAssetOptions.ForceUpdate);
        foreach (var file in current.files)
            if (CombatDiagnosticCleanupPlan.Hash(File.ReadAllBytes(Path.Combine(root, file.path))) != file.afterSha256)
                throw new IOException("Import changed planned controller bytes: " + file.path + "; pre-cleanup backups retained at " + backup);
        File.WriteAllText(Path.Combine(root, "Library/ProductionCombatMigration/diagnostic-cleanup-applied.json"), JsonUtility.ToJson(current, true));
    }

    public static void RunBatch()
    {
        try
        {
            string root = Path.GetFullPath("."), path = Path.GetFullPath(Argument("-combatDiagnosticCleanupPlan") ?? DefaultPlan);
            if (Environment.GetCommandLineArgs().Contains("-applyCombatDiagnosticCleanup"))
                ApplyApproved(root, Argument("-combatDiagnosticCleanupParentRoot") ?? throw new InvalidOperationException("Explicit parent root required."), path, Argument("-combatDiagnosticCleanupPlanSha256"));
            else
            {
                RefuseRestoredDiagnostic();
                if (!path.StartsWith(Path.Combine(root, "Library") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Write the dry-run plan under this project's Library only.");
                var plan = DryRun(root); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(plan, true));
                Debug.Log("Diagnostic cleanup DRY RUN: controllers=" + plan.controllerCount + "; behaviours=" + plan.behaviourCount + "; owner-list refs=" + plan.referenceCount + "; orphan documents=" + plan.orphanCount + "; plan=" + path + "; SHA256=" + CombatDiagnosticCleanupPlan.Hash(File.ReadAllBytes(path)));
            }
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    private static void RefuseRestoredDiagnostic()
    {
        if (AssetDatabase.GUIDToAssetPath(CombatDiagnosticCleanupPlan.ScriptGuid).Length != 0 || TypeCache.GetTypesDerivedFrom<StateMachineBehaviour>().Any(type => type.FullName == "LucianJumpStateTraceBehaviour"))
            throw new InvalidOperationException("Diagnostic script is no longer dangling; cleanup refused.");
    }
    private static bool HasGit(string root) => Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git"));
    private static bool Same(string first, string second) => string.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    private static string Argument(string name) { string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
}
