using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scoped authoring evidence; import fixtures run only in the isolated project.</summary>
public static class CombatMigrationAuthoringDiagnostics
{
    [Serializable] private sealed class VisionImportRecord
    {
        public string label, path, guid, characterId, vision;
    }
    [Serializable] private sealed class VisionImportReport
    {
        public string sourceSha256;
        public bool sourceBytesUnchanged;
        public List<VisionImportRecord> records = new List<VisionImportRecord>();
    }

    public static void RunVisionImportBatch()
    {
        const string source = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset";
        const string temporary = "Assets/__CombatVisionImportDiagnostic";
        bool created = false;
        try
        {
            if (!Application.isBatchMode || Directory.Exists(".git") || File.Exists(".git"))
                throw new InvalidOperationException("Vision import diagnosis requires an isolated batch project.");
            if (Directory.Exists(temporary)) throw new InvalidOperationException("Diagnostic fixture directory already exists.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            byte[] original = File.ReadAllBytes(source);
            string Hash(byte[] bytes)
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            }
            var report = new VisionImportReport { sourceSha256 = Hash(original) };
            void Record(string label, string path)
            {
                var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (data == null) throw new InvalidOperationException("CharacterData import failed: " + path);
                report.records.Add(new VisionImportRecord { label = label, path = AssetDatabase.GetAssetPath(data),
                    guid = AssetDatabase.AssetPathToGUID(path), characterId = data.UniqueId, vision = JsonUtility.ToJson(data.vision) });
                Debug.Log("[CombatVisionImport] " + label + " " + path + " " + JsonUtility.ToJson(data.vision));
            }
            Record("canonical-before", source);
            AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Record("canonical-force-import", source);
            AssetDatabase.CreateFolder("Assets", "__CombatVisionImportDiagnostic");
            created = true;
            string compact = string.Join("\n", System.Text.Encoding.UTF8.GetString(original).Replace("\r\n", "\n")
                .Split('\n').Where(line => !string.IsNullOrWhiteSpace(line))) + "\n";
            if (!compact.Contains("\n  vision:")) throw new InvalidOperationException("Expected authored vision section is absent.");
            File.WriteAllText(temporary + "/WithBlankLines.asset", compact.Replace("\n  vision:", "\n\n  vision:"));
            File.WriteAllText(temporary + "/WithoutBlankLines.asset", compact);
            foreach (string name in new[] { "WithBlankLines", "WithoutBlankLines" })
            {
                string path = temporary + "/" + name + ".asset";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Record(name, path);
            }
            report.sourceBytesUnchanged = report.sourceSha256 == Hash(File.ReadAllBytes(source));
            Directory.CreateDirectory("Library/ProductionCombatMigration/VisionImport");
            File.WriteAllText("Library/ProductionCombatMigration/VisionImport/diagnostic.json", JsonUtility.ToJson(report, true));
            if (!report.sourceBytesUnchanged) throw new InvalidOperationException("The canonical source bytes unexpectedly changed during import.");
            AssetDatabase.DeleteAsset(temporary);
            created = false;
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (created) AssetDatabase.DeleteAsset(temporary);
            EditorApplication.Exit(1);
        }
    }

    [Serializable] private sealed class FlameRecord
    {
        public string path, prefab, id;
        public bool lit, ancient, influenceEnabled, hasLight;
        public Vector3 position, center;
        public float radius;
    }
    [Serializable] private sealed class TargetRecord
    {
        public string path, closestFlame;
        public Vector3 position;
        public float distance, radius;
    }
    [Serializable] private sealed class ZoneRecord
    {
        public string environment, cycle;
        public List<FlameRecord> flames = new List<FlameRecord>();
        public List<TargetRecord> targets = new List<TargetRecord>();
    }
    [Serializable] private sealed class Report
    {
        public List<ZoneRecord> zones = new List<ZoneRecord>();
        public string etienneWorldCoveringFlame;
        public int activeWorldFlames;
    }

    public static void RunBatch()
    {
        try
        {
            WriteReport();
            string folder = "Library/ProductionCombatMigration";
            var cleanup = CombatDiagnosticCleanupBatch.DryRun(Path.GetFullPath("."));
            File.WriteAllText(folder + "/diagnostic-cleanup-plan.json", JsonUtility.ToJson(cleanup, true));
            Debug.Log("[CombatMigration] Read-only spatial diagnostics saved; cleanup plan controllers=" + cleanup.controllerCount);
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void WriteReport()
    {
        var report = new Report();
        report.zones.Add(Inspect(EtienneCycleValidation.EnvironmentPath, EtienneCycleValidation.CyclePath));
        report.zones.Add(Inspect(ArdentCycleValidation.EnvironmentPath, ArdentCycleValidation.CyclePath));
        using (var world = new CycleZoneFlameScope(AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset")))
        {
            report.activeWorldFlames = world.Flames.Length;
            var ghost = SceneManager.GetSceneByPath(EtienneCycleValidation.CyclePath).GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GhostController>(true)).Single();
            var covering = world.Flames.FirstOrDefault(flame => CycleZoneFlameScope.Covers(flame, ghost.transform.position, false));
            report.etienneWorldCoveringFlame = covering != null ? covering.gameObject.scene.path + ":" + Hierarchy(covering.transform) : "NONE";
        }
        const string folder = "Library/ProductionCombatMigration";
        Directory.CreateDirectory(folder);
        File.WriteAllText(folder + "/authoring-spatial-diagnostics.json", JsonUtility.ToJson(report, true));
    }

    private static ZoneRecord Inspect(string environment, string cycle)
    {
        var opened = new List<Scene>();
        Scene previous = SceneManager.GetActiveScene();
        try
        {
            Scene Load(string path)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded)
                { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened.Add(scene); }
                return scene;
            }
            var env = Load(environment);
            var quest = Load(cycle);
            var result = new ZoneRecord { environment = environment, cycle = cycle };
            foreach (var flame in env.GetRootGameObjects().Concat(quest.GetRootGameObjects())
                .SelectMany(root => root.GetComponentsInChildren<Flame>(true)))
            {
                var serialized = new SerializedObject(flame);
                var influence = serialized.FindProperty("litInfluence");
                result.flames.Add(new FlameRecord {
                    path = Hierarchy(flame.transform), prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(flame),
                    id = flame.FlameId, lit = flame.IsLit, ancient = flame.IsAncientFlame,
                    hasLight = flame.flameLight != null, position = flame.transform.position,
                    influenceEnabled = influence.FindPropertyRelative("enabled").boolValue,
                    center = flame.transform.TransformPoint(influence.FindPropertyRelative("center").vector3Value),
                    radius = influence.FindPropertyRelative("radius").floatValue
                });
            }
            foreach (var root in quest.GetRootGameObjects())
            foreach (var target in root.GetComponentsInChildren<Transform>(true).Where(target =>
                target.GetComponent<InteractableItem>() != null || target.GetComponent<GhostController>() != null ||
                target.GetComponent<EtienneEmergencyBrake>() != null || target.GetComponent<ArdentEchoRelay>() != null))
            {
                var nearest = result.flames.OrderBy(flame => Vector3.Distance(target.position, flame.center)).FirstOrDefault();
                result.targets.Add(new TargetRecord { path = Hierarchy(target), position = target.position,
                    closestFlame = nearest?.path, distance = nearest != null ? Vector3.Distance(target.position, nearest.center) : -1,
                    radius = nearest?.radius ?? 0 });
            }
            return result;
        }
        finally
        {
            foreach (var scene in opened.AsEnumerable().Reverse()) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static string Hierarchy(Transform item) => item.parent == null ? item.name : Hierarchy(item.parent) + "/" + item.name;
}
