using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Inspects saved District_1 cycle scenes in isolated preview scenes.
/// Never saves content or replaces the developer's open scene setup.</summary>
public static class CycleTimelineSceneAudit
{
    [MenuItem("Lit/Narrative/Audit District_1 Timeline Bindings")]
    public static void AuditMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Cycle] L'audit des scenes enregistrees est disponible hors Play Mode.");
            return;
        }
        var issues = CollectIssues();
        const string report = "Library/DistrictCycleTimelineAudit.txt";
        File.WriteAllLines(report, new[] { "Saved-scene Timeline binding coverage audit " + DateTime.UtcNow.ToString("O"),
            "Does not verify the rendered cinematic, runtime target availability or playback cleanup.",
            "Issues=" + issues.Count }.Concat(issues));
        if (issues.Count == 0) Debug.Log("[Cycle] Liaisons Timeline des scenes District_1 : couverture valide.");
        else Debug.LogWarning("[Cycle] Liaisons Timeline incompletes :\n- " + string.Join("\n- ", issues));
    }

    public static List<string> CollectIssues()
    {
        var issues = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileName(path).StartsWith("District_1_Cycle_", StringComparison.Ordinal)) continue;
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                foreach (var cycle in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CycleController>(true)))
                {
                    if (cycle.director != null)
                        foreach (var issue in TimelineBindingsValidator.CollectBindingCoverageIssues(cycle.director, cycle.bindingProfile))
                            issues.Add(path + " : " + issue);
                    foreach (var sequence in cycle.sequences ?? Array.Empty<CycleSequenceBinding>())
                        if (sequence != null)
                            foreach (var issue in TimelineBindingsValidator.CollectBindingCoverageIssues(sequence.director, sequence.profile))
                                issues.Add(path + " [" + sequence.id + "] : " + issue);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        return issues;
    }
}
