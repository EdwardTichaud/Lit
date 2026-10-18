using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Parent-invoked rehearsal against actual identities, exclusively in a disposable project copy.</summary>
public static class ProductionBrainsEnemyConversionValidation
{
    [Serializable] private sealed class Evidence
    {
        public bool passed;
        public string characterGuid, prefabGuid, reason;
        public List<string> checks = new List<string>();
    }

    public static void RunIsolatedApplyBatch()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!Application.isBatchMode || Directory.Exists(Path.Combine(project, ".git")) || File.Exists(Path.Combine(project, ".git")) ||
            !Environment.GetCommandLineArgs().Contains("-applyIsolatedProductionBrains"))
            throw new InvalidOperationException("Requires an isolated batch project without .git and explicit -applyIsolatedProductionBrains.");
        var result = new Evidence();
        try
        {
            const string characterPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(characterPath);
            Require(data != null, "Actual production CharacterData resolves", result);
            string prefabPath = AssetDatabase.GetAssetPath(data.ResolveWorldPrefab());
            string identity = data.UniqueId;
            result.characterGuid = AssetDatabase.AssetPathToGUID(characterPath);
            result.prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            // Only the original prefab and newly generated assets may change on the first Apply.
            var immutablePaths = AssetDatabase.GetDependencies(characterPath, true)
                .Where(path => File.Exists(path) && path != prefabPath && !path.Contains("/BrainsProduction/")).ToArray();
            var immutable = immutablePaths.ToDictionary(path => path, File.ReadAllBytes);
            var request = ProductionBrainsEnemyConversion.GenerateReviewedAssets(data);
            ProductionBrainsEnemyConversion.Apply(request);
            var converted = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(converted.GetComponent<EnemyController>() == null && converted.GetComponent<LitBrainsEnemy>() != null,
                "One Brains executor replaces the legacy controller", result);
            Require(ProductionBrainsEnemyConversion.Analyze(data).CanApply,
                "Default post-migration audit resolves the persisted original controller", result);
            Require(converted.GetComponent<ProductionBrainsFlamePolicy>()?.requiresFlameInfluence == request.requiresFlameInfluence &&
                converted.GetComponent<ProductionBrainsDeathLifecycle>() != null && converted.GetComponent<EnemyDeathPresentation>() != null,
                "Authored flame requirement and retained death/dissolve lifecycle installed", result);
            Require(converted.GetComponent<CharacterInfo>().SourceData == data && data.UniqueId == identity &&
                AssetDatabase.AssetPathToGUID(characterPath) == result.characterGuid && AssetDatabase.AssetPathToGUID(prefabPath) == result.prefabGuid,
                "Original author identity, prefab reference and GUIDs preserved", result);
            foreach (var entry in immutable)
                Require(entry.Value.SequenceEqual(File.ReadAllBytes(entry.Key)), "Source unchanged: " + entry.Key, result);
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(request)).Replace('\\', '/');
            var generated = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith(folder + "/", StringComparison.Ordinal) && File.Exists(path));
            var bytes = generated.Append(prefabPath).ToDictionary(path => path, File.ReadAllBytes);
            var guids = bytes.Keys.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);
            var repeated = ProductionBrainsEnemyConversion.GenerateReviewedAssets(data);
            ProductionBrainsEnemyConversion.Apply(repeated);
            Require(ProductionBrainsEnemyConversion.Analyze(data, repeated.sourceController).CanApply, "Post-Apply audit remains eligible", result);
            foreach (var entry in bytes)
                Require(entry.Value.SequenceEqual(File.ReadAllBytes(entry.Key)) && AssetDatabase.AssetPathToGUID(entry.Key) == guids[entry.Key],
                    "Second pass byte/GUID stable: " + entry.Key, result);
            result.passed = true;
            result.reason = "Authoring rehearsal only. Play, persistence reload and host/client remain separate parent validations.";
        }
        catch (Exception exception) { result.reason = exception.ToString(); Debug.LogException(exception); }
        string output = Path.Combine(project, "Library/ProductionCombatMigration");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "brains-isolated-apply.json"), JsonUtility.ToJson(result, true));
        Debug.Log("[ProductionBrainsConversion] " + (result.passed ? "PASS" : "FAIL") + ": " + result.reason);
        if (result.passed && Environment.GetCommandLineArgs().Contains("-combatMigrationValidateAfterConversion"))
        {
            CombatMigrationValidation.RunEditBatch();
            return;
        }
        EditorApplication.Exit(result.passed ? 0 : 1);
    }

    private static void Require(bool condition, string check, Evidence result)
    {
        if (!condition) throw new InvalidOperationException(check);
        result.checks.Add(check);
    }
}
