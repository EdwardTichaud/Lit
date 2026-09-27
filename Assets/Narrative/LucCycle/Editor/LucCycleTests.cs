#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class LucCycleTests
{
    private const string DefinitionPath = "Assets/Resources/Narrative/LucCycle.asset";
    private const string ScenePath = "Assets/Scenes/District_1/District_1_Cycle_Luc.unity";

    [Test]
    public void LucUsesNamedStepsAndARewardOwnedByTheFinalStep()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(DefinitionPath);
        Assert.NotNull(definition);
        Assert.AreEqual("district1.luc", definition.cycleId);
        Assert.AreEqual(0, definition.completionFlags);
        Assert.AreEqual(0, definition.legacyMigrationVersion);
        Assert.IsEmpty(definition.legacyMigrations);
        Assert.AreEqual("District_1_Cycle_Luc", definition.cycleSceneName);

        var collar = definition.FindStep("jon_collar_collected");
        var grave = definition.FindStep("jon_grave_read");
        var spoken = definition.FindStep("luc_spoken");
        var farewell = definition.FindStep("luc_farewell");
        Assert.NotNull(collar);
        Assert.NotNull(grave);
        Assert.NotNull(spoken);
        Assert.NotNull(farewell);
        Assert.AreEqual(CycleStepKind.Knowledge, collar.kind);
        Assert.AreEqual(CycleStepKind.Knowledge, grave.kind);
        Assert.AreEqual(CycleStepKind.Interaction, spoken.kind);
        Assert.AreEqual(CycleStepKind.DialogueCompleted, farewell.kind);
        Assert.IsTrue(farewell.terminal);
        Assert.AreEqual(1, farewell.rewards.Length);
        Assert.AreEqual(CycleRewardKind.Knowledge, farewell.rewards[0].kind);
        Assert.AreEqual("jonLocation", farewell.rewards[0].knowledge.knowledgeId);
        Assert.IsEmpty(definition.ValidateConfiguration());
    }

    [Test]
    public void LucSceneBindsTheGhostToTheCycleController()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CycleController>(true)).Single();
            var interaction = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CycleInteraction>(true)).Single();
            var ghost = interaction.GetComponent<GhostController>();
            Assert.NotNull(controller.definition);
            Assert.AreEqual("district1.luc", controller.definition.cycleId);
            Assert.AreEqual(controller, interaction.cycle);
            Assert.AreEqual("luc", interaction.dialogueId);
            Assert.NotNull(ghost);
            Assert.AreEqual("Ghost_Luc", ghost.name);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
#endif
