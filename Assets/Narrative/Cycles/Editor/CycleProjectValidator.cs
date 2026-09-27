using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Project-wide authoring gate. It is also run by Unity before a build.</summary>
public sealed class CycleProjectValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    [MenuItem("Lit/Narrative/Validate All Cycles")]
    public static void ValidateMenu()
    {
        var issues = CollectIssues();
        if (issues.Count == 0) Debug.Log("[Cycle] Validation globale reussie.");
        else Debug.LogError("[Cycle] Validation globale :\n- " + string.Join("\n- ", issues));
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        var issues = CollectIssues();
        if (issues.Count > 0) throw new BuildFailedException("Cycle validation failed:\n- " + string.Join("\n- ", issues));
    }

    public static List<string> CollectIssues()
    {
        var issues = new List<string>();
        var cycles = Resources.LoadAll<CycleDefinition>("Narrative");
        var ids = new HashSet<string>();
        var buildScenes = new HashSet<string>(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path)));
        foreach (var cycle in cycles)
        {
            if (cycle == null) continue;
            if (!ids.Add(cycle.cycleId ?? string.Empty)) issues.Add("Cycle ID duplique : " + cycle.cycleId);
            foreach (var issue in cycle.ValidateConfiguration()) issues.Add(cycle.name + " : " + issue);
            if (!string.IsNullOrWhiteSpace(cycle.cycleSceneName) && !buildScenes.Contains(cycle.cycleSceneName))
                issues.Add(cycle.name + " : scene absente des Build Settings : " + cycle.cycleSceneName);
            foreach (var step in cycle.steps ?? System.Array.Empty<CycleStep>())
                if (step != null) foreach (var reward in step.rewards ?? System.Array.Empty<CycleReward>())
                    if (reward != null && reward.kind == CycleRewardKind.Item && reward.item == null) issues.Add(cycle.name + " : item de recompense absent : " + step.id);
        }
        bool hasMaison = AssetDatabase.FindAssets("t:Prefab").Any(guid =>
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            return prefab != null && prefab.GetComponentInChildren<Maison>(true) != null;
        });
        if (!hasMaison) hasMaison = AssetDatabase.FindAssets("t:Scene").Any(guid =>
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrWhiteSpace(path) && System.IO.File.ReadAllText(path).Contains("Assembly-CSharp::Maison");
        });
        if (!hasMaison)
            issues.Add("Aucun composant Maison trouve pour le coffre de recompenses.");
        return issues;
    }
}

public static class CycleAuthoringWizard
{
    [MenuItem("Lit/Narrative/New Cycle")]
    private static void NewCycle()
    {
        string path = EditorUtility.SaveFilePanelInProject("New Cycle", "NewCycle", "asset", "Choose a Resources/Narrative location.", "Assets/Resources/Narrative");
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!path.Contains("/Resources/Narrative/")) { EditorUtility.DisplayDialog("Cycle", "The definition must be saved under Resources/Narrative.", "OK"); return; }
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        definition.cycleId = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Replace(' ', '_');
        definition.title = System.IO.Path.GetFileNameWithoutExtension(path);
        definition.steps = new[]
        {
            new CycleStep { id = "conclusion", title = "Interaction finale", kind = CycleStepKind.Interaction, sourceId = "conclusion", terminal = true }
        };
        AssetDatabase.CreateAsset(definition, path);
        const string sceneFolder = "Assets/Scenes/Cycles";
        if (!AssetDatabase.IsValidFolder(sceneFolder)) AssetDatabase.CreateFolder("Assets/Scenes", "Cycles");
        string scenePath = AssetDatabase.GenerateUniqueAssetPath(sceneFolder + "/" + definition.cycleId + ".unity");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var root = new GameObject("Cycle_" + definition.cycleId);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<NetworkObject>();
        root.AddComponent<CycleController>().definition = definition;
        definition.cycleSceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        EditorUtility.SetDirty(definition);
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorSceneManager.CloseScene(scene, true);
        var buildScenes = EditorBuildSettings.scenes.ToList();
        if (!buildScenes.Any(item => item.path == scenePath))
        {
            buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }
        AssetDatabase.SaveAssets();
        Selection.activeObject = definition;
        EditorGUIUtility.PingObject(definition);
    }
}
