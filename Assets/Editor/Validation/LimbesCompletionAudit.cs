using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Inspects saved zone content, never saves or rebuilds a scene.</summary>
public static class LimbesCompletionAudit
{
    private const string ManifestPath = "Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset";
    private const string ReportPath = "Library/ReveLimbesCompletion/audit.json";

    [Serializable] private sealed class Report
    {
        public string utc;
        public List<string> scenes = new();
        public List<string> cycles = new();
        public List<string> storySequences = new();
        public List<string> timelineSequences = new();
        public List<string> enemies = new();
        public List<string> issues = new();
        public string qualification = "Saved-content inspection only. Does not prove traversal, combat, presentation, persistence or multiplayer playability.";
    }

    [MenuItem("Tools/Lit/Limbes/Audit saved zone completion")]
    public static void Run()
    {
        var report = new Report { utc = DateTime.UtcNow.ToString("O") };
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(ManifestPath);
        if (manifest == null) throw new InvalidOperationException("Missing Limbes manifest.");
        var paths = new[] { manifest.primaryScene }.Concat(manifest.loadingScenes)
            .Concat(manifest.postLoadingScenes).Select(AssetDatabase.GetAssetPath).ToArray();
        var built = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        var cycles = new HashSet<string>(StringComparer.Ordinal);
        var stories = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path)) { report.issues.Add("Manifest contains a missing scene."); continue; }
            if (report.scenes.Contains(path)) { report.issues.Add("Duplicate scene: " + path); continue; }
            report.scenes.Add(path);
            if (!built.Contains(path)) report.issues.Add("Scene not enabled in Build Settings: " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var node in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0)
                            report.issues.Add("Missing script: " + path + "/" + node.name);
                    foreach (var controller in root.GetComponentsInChildren<CycleController>(true))
                    {
                        var definition = controller.definition;
                        if (definition == null) { report.issues.Add("Missing cycle definition in " + path); continue; }
                        if (!cycles.Add(definition.cycleId)) report.issues.Add("Duplicate bound cycle ID: " + definition.cycleId);
                        else report.cycles.Add(definition.cycleId);
                        foreach (string issue in definition.ValidateConfiguration())
                            report.issues.Add(definition.cycleId + ": " + issue);
                        foreach (var binding in controller.sequences ?? Array.Empty<CycleSequenceBinding>())
                            if (binding != null && binding.director != null && binding.director.playableAsset != null && binding.profile != null)
                                report.timelineSequences.Add(definition.cycleId + ":" + binding.id);
                            else report.issues.Add("Incomplete Timeline sequence binding: " + definition.cycleId);
                    }
                    foreach (var runner in root.GetComponentsInChildren<StorySequenceRunner>(true))
                    {
                        var sequence = runner.Sequence;
                        if (sequence == null) { report.issues.Add("Story runner has no sequence: " + path); continue; }
                        if (stories.Add(sequence.sequenceId)) report.storySequences.Add(sequence.sequenceId);
                        if (!sequence.TryValidate(out string issue)) report.issues.Add(sequence.sequenceId + ": " + issue);
                    }
                    foreach (var enemy in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (enemy is IEnemyEncounterActor) report.enemies.Add(path + ":" + enemy.name);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        if (report.cycles.Count < 5) report.issues.Add("At least five bound cycles required; found " + report.cycles.Count);
        if (report.storySequences.Count + report.timelineSequences.Count < 6)
            report.issues.Add("At least six bound sequences required; found " + (report.storySequences.Count + report.timelineSequences.Count));
        if (report.enemies.Count == 0) report.issues.Add("No EnemyController placed in the manifested zone; inspect markers before concluding boss absence.");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Debug.Log("Limbes saved-content audit: " + report.cycles.Count + " cycles, " + report.storySequences.Count +
            " story sequences, " + report.timelineSequences.Count + " Timeline bindings. Issues: " + report.issues.Count +
            ". Report: " + ReportPath + ". Runtime qualification is still required.");
    }
}
