using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Imports externally produced receipts, not evidence manufactured by the migration.</summary>
public static class ProductionCombatEvidenceImport
{
    public const string EvidenceDirectory = ProductionCombatMigration.ReportDirectory + "/Evidence";

    [MenuItem("Lit/Combat/Production Migration/Import Evidence Receipt")]
    public static void ImportFromMenu()
    {
        string path = EditorUtility.OpenFilePanel("Import runner evidence receipt", "", "json");
        if (!string.IsNullOrEmpty(path)) Import(path);
    }

    // The receipt is a CombatReadinessEvidence.Receipt JSON. Artifact paths may be relative to its directory.
    // nunit3 artifacts require passed test properties named CombatReadinessCheck with RequiredChecks(gate) values.
    // Other runner artifacts must use combat-validation-v1 XML with matching gate/revision/fingerprint/runId
    // and <check id="..." result="Passed"/> children plus a hashed attachment with role=runner-log.
    // Network receipts also need distinct attachment artifacts with role=host-log/client-log.
    // Plain-text PASS claims alone are deliberately rejected. These are attestations, not signed provenance.
    public static void Import(string receiptPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Import evidence outside Play Mode/compilation.");
        string absolute = Path.GetFullPath(receiptPath);
        var receipt = JsonUtility.FromJson<CombatReadinessEvidence.Receipt>(File.ReadAllText(absolute));
        var snapshot = ProductionCombatAudit.CaptureSnapshot();
        string root = Path.GetDirectoryName(absolute);
        string error = CombatReadinessEvidence.Validate(receipt, snapshot, root, Array.Empty<string>());
        if (error != null)
        {
            // Rejected current input must not silently retain an older green receipt for the same gate.
            if (receipt != null && CombatReadinessEvidence.GateIds.Contains(receipt.gate))
            {
                receipt.passed = false;
                Directory.CreateDirectory(EvidenceDirectory);
                File.WriteAllText(Path.Combine(EvidenceDirectory, receipt.gate + ".json"), JsonUtility.ToJson(receipt, true));
            }
            throw new InvalidOperationException("Evidence rejected (previous evidence for this gate invalidated): " + error);
        }
        foreach (var artifact in receipt.artifacts)
            artifact.path = Path.GetFullPath(Path.IsPathRooted(artifact.path) ? artifact.path : Path.Combine(root, artifact.path));
        Directory.CreateDirectory(EvidenceDirectory);
        // One receipt per gate; a later failed run must not leave a prior green report as the current receipt.
        File.WriteAllText(Path.Combine(EvidenceDirectory, receipt.gate + ".json"), JsonUtility.ToJson(receipt, true));
        Debug.Log("[CombatReadiness] Imported " + receipt.gate + " runner attestation " + receipt.runId +
            ". Readiness is recalculated against complete inventory; import does not activate production.");
    }

    public static void Evaluate(ProductionCombatMigration.Report report, ProductionCombatAudit.Session audit = null)
    {
        report.ReadyForProduction = false;
        report.readiness = null;
        report.blockers.RemoveAll(entry => entry.category.StartsWith("readiness-gate:", StringComparison.Ordinal) ||
            entry.category == "invalid-evidence" || entry.category == "source-fingerprint" || entry.category == "readiness-unavailable");
        try
        {
            audit = audit ?? new ProductionCombatAudit.Session();
            var snapshot = audit.CaptureSnapshot();
            report.auditCost = audit.Costs();
            var receipts = new List<CombatReadinessEvidence.Receipt>();
            if (Directory.Exists(EvidenceDirectory))
                foreach (string path in Directory.GetFiles(EvidenceDirectory, "*.json").OrderBy(value => value, StringComparer.Ordinal))
                {
                    try { receipts.Add(JsonUtility.FromJson<CombatReadinessEvidence.Receipt>(File.ReadAllText(path))); }
                    catch (Exception exception) { report.blockers.Add(new ProductionCombatMigration.Entry("invalid-evidence", path, exception.Message)); }
                }
            var subjects = new Dictionary<string, string[]> {
                { "allEnemyMappings", report.enemySubjects.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() },
                { "zone", report.zoneSubjects.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() },
                { "save", report.saveSubjects.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() },
                { "network", report.networkSubjects.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() },
                { "inventoryCoverage", report.inventorySubjects.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() }
            };
            report.readiness = CombatReadinessEvidence.Evaluate(snapshot, receipts, ".", subjects,
                report.authoringPlanEvaluated && report.succeeded && report.errors.Count == 0 && report.changedAssets.Count == 0, report.blockers.Count != 0);
            report.ReadyForProduction = report.readiness.ReadyForProduction;
            foreach (var gate in report.readiness.gates.Where(gate => !gate.passed))
                report.blockers.Add(new ProductionCombatMigration.Entry("readiness-gate:" + gate.id, "", gate.status + ": " + gate.reason));
            foreach (string error in snapshot.errors) report.blockers.Add(new ProductionCombatMigration.Entry("source-fingerprint", "", error));
        }
        catch (Exception exception)
        {
            report.ReadyForProduction = false;
            report.blockers.Add(new ProductionCombatMigration.Entry("readiness-unavailable", "", exception.ToString()));
        }
    }
}
