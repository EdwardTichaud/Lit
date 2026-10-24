using Lit.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

public static partial class DistrictCycleRuntimeBuild
{
    private static void CreateNetworkSequenceFixture()
    {
        const string path = "Assets/Resources/Narrative/CycleSequenceSmokeDefinition.asset";
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(path);
        if (definition == null) { definition = ScriptableObject.CreateInstance<CycleDefinition>(); AssetDatabase.CreateAsset(definition, path); }
        definition.cycleId = "test.sequence.protocol";
        definition.title = "Native network sequence barrier";
        definition.cycleSceneName = "CycleSmokeScene";
        definition.deathDelay = 0;
        definition.steps = new[]
        {
            new CycleStep { id = "memory_seen", sourceId = "memory", kind = CycleStepKind.SequenceCompleted },
            // Keep the scene loaded after the memory so final state can be inspected.
            new CycleStep { id = "leave", sourceId = "leave", kind = CycleStepKind.Interaction, terminal = true,
                prerequisites = new CycleRequirements { conditions = new[] { new CycleRequirement { stepId = "memory_seen" } } } }
        };
        var root = new GameObject("Native network sequence fixture");
        var controller = root.AddComponent<CycleController>(); controller.definition = definition;
        var presentation = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/CycleTimelineFixture.prefab"));
        presentation.transform.SetParent(root.transform, false);
        controller.sequences = new[] { new CycleSequenceBinding { id = "memory",
            director = presentation.GetComponent<PlayableDirector>(),
            profile = AssetDatabase.LoadAssetAtPath<TimelineBindingProfile>("Assets/Resources/CycleTimelineFixtureProfile.asset") } };
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/CycleSequenceSmokePrefab.prefab");
        Object.DestroyImmediate(root);
        EditorUtility.SetDirty(definition);
    }
}
