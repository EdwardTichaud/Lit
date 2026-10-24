using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class DistrictCycleRuntimeBuild
{
    public static void Build()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (Path.GetFileName(project) != "DistrictCycleRuntimeProject" || !File.Exists(Path.Combine(project, "CycleSmokeValidationProject.marker")))
            throw new InvalidOperationException("This build tool only runs in its marked disposable validation project.");
        UnityEditor.PlayerSettings.companyName = "LitValidation";
        UnityEditor.PlayerSettings.productName = "CycleProtocolSmoke";
        UnityEditor.PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.lit.validation.cycleprotocol");
        UnityEditor.PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        UnityEditor.PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
        GraphicsSettings.defaultRenderPipeline = null;
        QualitySettings.renderPipeline = null;
        Directory.CreateDirectory("Assets/Resources/Narrative");
        const string definitionPath = "Assets/Resources/Narrative/CycleSmokeDefinition.asset";
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(definitionPath);
        if (definition == null) definition = ScriptableObject.CreateInstance<CycleDefinition>();
        definition.cycleId = "test.auto.protocol"; definition.title = "Isolated automatic dialogue protocol";
        definition.cycleSceneName = "CycleSmokeScene";
        definition.steps = new[] { new CycleStep { id = "intro", sourceId = "spoken_intro", title = "Natural completion", kind = CycleStepKind.DialogueCompleted, terminal = true } };
        definition.dialogues = new[] { new CycleDialogue { id = "spoken_intro", line = "Smoke automatic conversation", durationSeconds = .5f } };
        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition))) AssetDatabase.CreateAsset(definition, definitionPath);
        else EditorUtility.SetDirty(definition);
        var root = new GameObject("Cycle smoke network prefab");
        var controller = root.AddComponent<CycleController>(); controller.definition = definition;
        controller.autoDialogues = new[] { new CycleAutoDialogueBinding { id = "intro", line = "Smoke automatic conversation", durationSeconds = .5f } };
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/CycleSmokePrefab.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        CreateTimelineFixture();
        CreateNetworkSequenceFixture();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/CycleSmokeScene.unity");
        AssetDatabase.SaveAssets();
        string output = Path.GetFullPath("../DistrictCycleValidation/Player/CycleSmoke.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/CycleSmokeScene.unity" }, locationPathName = output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        File.WriteAllText(Path.GetFullPath("../DistrictCycleValidation/RuntimeBuildResult.txt"), report.summary.result + " errors=" + report.summary.totalErrors);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Isolated protocol build failed: " + report.summary.result);
    }
}
