using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit source-project authoring installation, separate from production qualification.</summary>
[InitializeOnLoad]
public static class CombatMigrationImplementationInstall
{
    private const string RequestPath = "Library/ProductionCombatMigration/install-authoring.request";
    private static double nextRequestPoll;

    static CombatMigrationImplementationInstall() => EditorApplication.update += RunRequested;

    private static void RunRequested()
    {
        if (Application.isBatchMode || EditorApplication.timeSinceStartup < nextRequestPoll) return;
        nextRequestPoll = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string claimed = RequestPath + ".claimed-" + Guid.NewGuid().ToString("N");
        try
        {
            if (File.ReadAllText(RequestPath).Trim() != "install-production-combat-implementation-v2") return;
            File.Move(RequestPath, claimed);
            Install();
        }
        catch (Exception exception)
        {
            File.WriteAllText(ProductionCombatMigration.ReportDirectory + "/implementation-install-error.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }
    [Serializable] public sealed class FileRecord
    {
        public string path, before, after, guidBefore, guidAfter;
    }

    [Serializable] public sealed class Report
    {
        public string runId = Guid.NewGuid().ToString("N"), reason, backup, stage;
        public bool authoringInstalled, idempotent, readyForProduction;
        public List<FileRecord> files = new List<FileRecord>();
        public List<string> unresolvedImplementation = new List<string>();
        public string validation = "Compilation/authoring only. No Play Mode, save roundtrip or multiplayer acceptance executed.";
        public string scope = "Approved roster/session/Juggernaut/seven specialized encounter manifest; full reachability and readiness gates are not evaluated by this installer.";
    }

    [MenuItem("Lit/Combat/Production Migration/Install Common Runtime And Production Juggernaut")]
    public static void InstallMenu() => Install();

    public static void RunBatch()
    {
        try
        {
            if (!Environment.GetCommandLineArgs().Contains("-installCombatAuthoring"))
                throw new InvalidOperationException("Explicit -installCombatAuthoring is required.");
            var report = Install();
            EditorApplication.Exit(report.authoringInstalled && report.idempotent ? 0 : 1);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static Report Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Install in Edit Mode after compilation/import. No Play Mode is started or stopped by this installer.");
        var result = new Report();
        result.backup = ProductionCombatMigration.ReportDirectory + "/InstallBackups/" + result.runId;
        string juggernautPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset";
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(juggernautPath);
        var conversion = ProductionBrainsEnemyConversion.Analyze(data);
        if (!conversion.CanApply)
            throw new InvalidOperationException("Production Juggernaut preflight failed before any installation:\n" +
                string.Join("\n", conversion.blockers.Concat(conversion.conversionBlockers)));
        var primaryPlan = ProductionCombatMigration.RunAuthoringInstall(false);
        var networkPlan = CombatNetworkSetup.Run(false, commonAuthoringPlanned: true, approvedManifestOnly: true);
        var encounterPlan = ProductionEncounterMigration.Run(false, includeSceneConsumers: false);
        if (!networkPlan.succeeded || !encounterPlan.succeeded)
            throw new InvalidOperationException("Installation preflight failed before authoring writes:\n" +
                string.Join("\n", networkPlan.blockers.Concat(networkPlan.errors).Concat(encounterPlan.errors)));
        string generated = Path.GetDirectoryName(conversion.prefabPath).Replace('\\', '/') + "/BrainsProduction";
        var watched = new HashSet<string>(primaryPlan.changedAssets.Concat(networkPlan.changedAssets).Concat(encounterPlan.changedAssets).Concat(encounterPlan.plannedFiles), StringComparer.Ordinal)
        { conversion.prefabPath };
        if (Directory.Exists(generated))
            foreach (string path in Directory.GetFiles(generated, "*", SearchOption.AllDirectories)) watched.Add(path.Replace('\\', '/'));
        foreach (string path in watched.ToArray()) watched.Add(path + ".meta");
        foreach (string path in watched.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains(".."))
                throw new InvalidOperationException("Authoring plan escaped Assets: " + path);
            var loaded = AssetDatabase.LoadMainAssetAtPath(path);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (loaded != null && EditorUtility.IsDirty(loaded) || stage != null && stage.assetPath == path && stage.scene.isDirty)
                throw new InvalidOperationException("Save the modified authoring asset before installation: " + path);
            var record = new FileRecord { path = path, before = Hash(path), guidBefore = AssetDatabase.AssetPathToGUID(path) };
            result.files.Add(record);
            if (!File.Exists(path)) continue;
            string backup = Path.Combine(result.backup, path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(path, backup, false);
        }
        Directory.CreateDirectory(ProductionCombatMigration.ReportDirectory);
        Write(result);
        try
        {
            result.stage = "common-runtime"; Write(result);
            ProductionCombatMigration.RunAuthoringInstall(true);
            result.stage = "production-juggernaut"; Write(result);
            ProductionBrainsEnemyConversion.ConvertJuggernaut();
            result.stage = "network-authoring"; Write(result);
            CombatNetworkSetup.Run(true, approvedManifestOnly: true);
            result.stage = "specialized-encounters"; Write(result);
            var encounters = ProductionEncounterMigration.Run(true, includeSceneConsumers: false);
            if (!encounters.succeeded) throw new InvalidOperationException("Encounter authoring incomplete:\n" + string.Join("\n", encounters.errors));
            if (Directory.Exists(generated))
                foreach (string path in Directory.GetFiles(generated, "*", SearchOption.AllDirectories))
                {
                    string normalized = path.Replace('\\', '/');
                    if (result.files.All(item => item.path != normalized)) result.files.Add(new FileRecord { path = normalized });
                }
            if (File.Exists(generated + ".meta") && result.files.All(item => item.path != generated + ".meta"))
                result.files.Add(new FileRecord { path = generated + ".meta" });
            foreach (var item in result.files)
            {
                item.after = Hash(item.path); item.guidAfter = AssetDatabase.AssetPathToGUID(item.path);
                if (!string.IsNullOrEmpty(item.guidBefore) && item.guidBefore != item.guidAfter)
                    throw new InvalidOperationException("Existing asset GUID changed: " + item.path);
            }
            result.stage = "repeat-and-audit"; Write(result);
            var repeatedPrimary = ProductionCombatMigration.RunAuthoringInstall(true);
            ProductionBrainsEnemyConversion.ConvertJuggernaut();
            var repeatedNetwork = CombatNetworkSetup.Run(true, approvedManifestOnly: true);
            var repeatedEncounters = ProductionEncounterMigration.Run(true, includeSceneConsumers: false);
            var primary = ProductionCombatMigration.RunAuthoringInstall(false);
            var network = CombatNetworkSetup.Run(false, approvedManifestOnly: true);
            var encounterAudit = ProductionEncounterMigration.Run(false, includeSceneConsumers: false);
            result.idempotent = repeatedPrimary.changedAssets.Count == 0 && repeatedNetwork.savedAssets.Count == 0 &&
                repeatedEncounters.succeeded && repeatedEncounters.savedAssets.Count == 0 && encounterAudit.succeeded && encounterAudit.changedAssets.Count == 0 &&
                primary.changedAssets.Count == 0 && network.changedAssets.Count == 0 && result.files.All(item => Hash(item.path) == item.after);
            if (!result.idempotent) throw new InvalidOperationException("Repeated installation is not byte-stable; inspect the manifest and retained backups.");
            result.unresolvedImplementation.AddRange(network.unsupportedSkills);
            result.unresolvedImplementation.AddRange(primary.blockers.Where(item => item.category == "unsupported-enemy" || item.category == "incomplete-enemy-mapping")
                .Select(item => item.path + ": " + item.detail));
            result.authoringInstalled = true;
            result.stage = "installed";
            result.reason = "Common session, four player kits and specialized production encounters installed from current authoring sources; this is not gameplay qualification.";
        }
        catch (Exception exception)
        {
            result.reason = exception.ToString();
            result.stage = "failed";
            Debug.LogException(exception);
        }
        finally { Write(result); }
        Debug.Log("[CombatImplementationInstall] Installed=" + result.authoringInstalled + "; idempotent=" + result.idempotent +
            "; ReadyForProduction=false; report=" + ProductionCombatMigration.ReportDirectory + "/implementation-install.json");
        return result;
    }

    private static string Hash(string path) => File.Exists(path) ? CombatReadinessEvidence.HashFile(path) : null;
    private static void Write(Report report) => File.WriteAllText(ProductionCombatMigration.ReportDirectory + "/implementation-install.json", JsonUtility.ToJson(report, true));
}
