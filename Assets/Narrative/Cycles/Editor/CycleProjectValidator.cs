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
        var runtimeCycles = new HashSet<CycleDefinition>(CycleCatalog.LoadDefinitions());
        var cycles = AssetDatabase.FindAssets("t:CycleDefinition").Select(guid =>
            AssetDatabase.LoadAssetAtPath<CycleDefinition>(AssetDatabase.GUIDToAssetPath(guid))).Where(cycle => cycle != null).ToArray();
        var ids = new HashSet<string>();
        var buildScenes = new HashSet<string>(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path)));
        foreach (var cycle in cycles)
        {
            if (cycle == null) continue;
            if (!runtimeCycles.Contains(cycle)) issues.Add(cycle.name + " : definition introuvable au demarrage ; placer sous Resources/Narrative ou referencer dans un CycleCatalog de ce dossier.");
            if (!ids.Add(cycle.cycleId ?? string.Empty)) issues.Add("Cycle ID duplique : " + cycle.cycleId);
            foreach (var issue in cycle.ValidateConfiguration()) issues.Add(cycle.name + " : " + issue);
            if (!string.IsNullOrWhiteSpace(cycle.cycleSceneName) && !buildScenes.Contains(cycle.cycleSceneName))
                issues.Add(cycle.name + " : scene absente des Build Settings : " + cycle.cycleSceneName);
            foreach (var step in cycle.steps ?? System.Array.Empty<CycleStep>())
                if (step != null) foreach (var reward in step.rewards ?? System.Array.Empty<CycleReward>())
                    if (reward != null && reward.kind == CycleRewardKind.Item && reward.item == null) issues.Add(cycle.name + " : item de recompense absent : " + step.id);
        }
        // Inspect serialized component references, not every prefab's imported
        // hierarchy. Loading the entire asset library here also loads its meshes,
        // materials and editor callbacks, unrelated to this authoring gate.
        string maisonScriptGuid = AssetDatabase.FindAssets("Maison t:MonoScript")
            .FirstOrDefault(guid => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid))?.GetClass() == typeof(Maison));
        bool HasMaisonScript(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return maisonScriptGuid != null && System.IO.File.Exists(path) && System.IO.File.ReadLines(path)
                .Any(line => line.Contains("m_Script:") && line.Contains("guid: " + maisonScriptGuid + ","));
        }
        bool hasMaison = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Any(HasMaisonScript) ||
            AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Any(HasMaisonScript);
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
