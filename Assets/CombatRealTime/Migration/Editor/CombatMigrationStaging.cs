using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Isolated authoring rehearsal. Its success never enables the user's production game.</summary>
public static class CombatMigrationStaging
{
    [Serializable] private sealed class Result
    {
        public bool passed, productionReady;
        public string runId = Guid.NewGuid().ToString("N"), reason;
        public List<string> primaryWrites = new List<string>();
        public List<string> networkWrites = new List<string>();
        public List<string> unsupportedSkills = new List<string>();
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || Directory.Exists(".git") || File.Exists(".git") ||
            !Environment.GetCommandLineArgs().Contains("-applyStagedCombatAssets"))
            throw new InvalidOperationException("Explicit -applyStagedCombatAssets in an isolated batch project is required.");
        var result = new Result();
        try
        {
            var primary = ProductionCombatMigration.Run(true);
            result.primaryWrites.AddRange(primary.changedAssets);
            var network = CombatNetworkSetup.Run(true);
            result.networkWrites.AddRange(network.savedAssets);
            result.unsupportedSkills.AddRange(network.unsupportedSkills);
            if (!primary.succeeded || !network.succeeded) throw new InvalidOperationException("Authoring pass failed.");
            var repeatedPrimary = ProductionCombatMigration.Run(true);
            var repeatedNetwork = CombatNetworkSetup.Run(true);
            if (repeatedPrimary.changedAssets.Count != 0 || repeatedNetwork.savedAssets.Count != 0)
                throw new InvalidOperationException("Second Apply is not idempotent.");
            var dryPrimary = ProductionCombatMigration.Run(false);
            var dryNetwork = CombatNetworkSetup.Run(false);
            if (dryPrimary.changedAssets.Count != 0 || dryNetwork.changedAssets.Count != 0)
                throw new InvalidOperationException("Post-Apply dry-run still proposes writes.");
            result.passed = true;
            result.productionReady = dryPrimary.ReadyForProduction && dryNetwork.unsupportedSkills.Count == 0;
            result.reason = "Isolated authoring and repeated Apply/dry-run completed. Runtime, world, save and network delivery gates remain separate.";
        }
        catch (Exception exception) { result.reason = exception.ToString(); Debug.LogException(exception); }
        Directory.CreateDirectory(ProductionCombatMigration.ReportDirectory);
        File.WriteAllText(ProductionCombatMigration.ReportDirectory + "/staging.json", JsonUtility.ToJson(result, true));
        Debug.Log("[CombatMigrationStaging] " + (result.passed ? "PASS" : "FAIL") + "; productionReady=" + result.productionReady +
            "; primaryWrites=" + result.primaryWrites.Count + "; networkWrites=" + result.networkWrites.Count);
        EditorApplication.Exit(result.passed ? 0 : 1);
    }
}
