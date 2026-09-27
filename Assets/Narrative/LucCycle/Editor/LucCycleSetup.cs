using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Connects the published Luc content to its named-step cycle without rebuilding its authored scene.</summary>
[InitializeOnLoad]
public static class LucCycleSetup
{
    public const string ScenePath = "Assets/Scenes/District_1/District_1_Cycle_Luc.unity";
    private const string DefinitionPath = "Assets/Resources/Narrative/LucCycle.asset";

    static LucCycleSetup() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Library/LucCycle.request")) return;
        File.Delete("Library/LucCycle.request");
        try
        {
            Apply();
            File.WriteAllText("Library/LucCycle.result", "Configured and validated " + ScenePath);
        }
        catch (Exception exception)
        {
            File.WriteAllText("Library/LucCycle.result", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Lit/Narrative/Configure Luc Cycle")]
    public static void Apply()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(DefinitionPath);
        if (definition == null) throw new InvalidOperationException("Definition LucCycle introuvable.");
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (sceneAsset == null) throw new InvalidOperationException("Scene Luc introuvable : " + ScenePath);

        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var ghost = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GhostController>(true))
                .FirstOrDefault(item => item.name == "Ghost_Luc");
            if (ghost == null) throw new InvalidOperationException("Ghost_Luc introuvable dans la scene Luc.");

            var root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "LucCycle") ?? new GameObject("LucCycle");
            if (root.scene != scene) SceneManager.MoveGameObjectToScene(root, scene);
            var cycle = root.GetComponent<CycleController>() ?? root.AddComponent<CycleController>();
            cycle.definition = definition;
            cycle.encounterMarker = null;
            cycle.encounterEnemy = null;
            cycle.encounters = Array.Empty<CycleEncounterBinding>();
            cycle.sequences = Array.Empty<CycleSequenceBinding>();
            cycle.activations = Array.Empty<CycleActivationBinding>();
            cycle.poses = Array.Empty<CyclePoseBinding>();
            cycle.cinematicParticipants = Array.Empty<EnemyController>();

            var interaction = ghost.GetComponent<CycleInteraction>() ?? ghost.gameObject.AddComponent<CycleInteraction>();
            interaction.cycle = cycle;
            interaction.dialogueId = "luc";
            cycle.interactions = new[] { interaction };

            EditorUtility.SetDirty(interaction);
            EditorUtility.SetDirty(cycle);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ConfigureManifest(sceneAsset);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static void ConfigureManifest(SceneAsset sceneAsset)
    {
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
        if (manifest == null) throw new InvalidOperationException("ZoneManifest District 1 introuvable.");
        var serialized = new SerializedObject(manifest);
        var scenes = serialized.FindProperty("loadingScenes");
        var names = serialized.FindProperty("loadingSceneNames");
        bool containsScene = Enumerable.Range(0, scenes.arraySize).Any(index => scenes.GetArrayElementAtIndex(index).objectReferenceValue == sceneAsset);
        if (!containsScene)
        {
            scenes.InsertArrayElementAtIndex(scenes.arraySize);
            scenes.GetArrayElementAtIndex(scenes.arraySize - 1).objectReferenceValue = sceneAsset;
        }
        for (int index = 0; index < names.arraySize; index++)
            if (names.GetArrayElementAtIndex(index).stringValue == "District_1_Enigme_Luc")
                names.GetArrayElementAtIndex(index).stringValue = "District_1_Cycle_Luc";
        bool containsName = Enumerable.Range(0, names.arraySize).Any(index => names.GetArrayElementAtIndex(index).stringValue == "District_1_Cycle_Luc");
        if (!containsName)
        {
            names.InsertArrayElementAtIndex(names.arraySize);
            names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = "District_1_Cycle_Luc";
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manifest);
    }
}
