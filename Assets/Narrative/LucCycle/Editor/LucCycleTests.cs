#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class LucCycleTests
{
    private const string DefinitionPath = "Assets/Scenes/Cycles/Cycle_Luc/LucCycle.asset";
    private const string ScenePath = "Assets/Scenes/Cycles/Cycle_Luc/District_1_Cycle_Luc.unity";

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

    [Test]
    public void LucIsEnabledAndTheGraveHasAuthoredInteractionAndVisibleOutline()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var cycle = objects.Select(t => t.GetComponent<CycleController>()).Single(c => c != null);
            Assert.That(cycle.isActiveAndEnabled, Is.True, "An authored disabled root cannot run its cycle.");
            var grave = objects.Single(t => t.name == "Petite tombe de Jon").GetComponent<InteractableItem>();
            var serialized = new SerializedObject(grave);
            var collider = serialized.FindProperty("interactionTrigger").objectReferenceValue as Collider;
            Assert.That(collider, Is.Not.Null, "Do not depend on the Play Mode fallback for the grave.");
            Assert.That(collider.enabled, Is.True);
            Assert.That(collider.transform.IsChildOf(grave.transform) || collider.transform == grave.transform, Is.True);
            Assert.That(grave.interactionMaxDistance, Is.EqualTo(2));
            Assert.That(grave.representedItem, Is.Not.Null);
            Assert.That(grave.representedItem.IsReadable(), Is.True);
            var model = grave.GetComponentsInChildren<MeshRenderer>(true).Single();
            Assert.That(model.GetComponent<RuntimeOutlineTarget>(), Is.Not.Null, "Outline must live on the visible renderer, not only an empty parent.");
            Assert.That(Vector3.Distance(model.transform.position, grave.transform.position), Is.LessThan(.1f), "Remove the old world-sized prefab-child offset on this scene instance.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void AllLucInteractionsHavePotentialLightFromAPermanentSceneWithoutBelmont()
    {
        var quest = EditorSceneManager.OpenPreviewScene(ScenePath);
        var lighting = EditorSceneManager.OpenPreviewScene("Assets/Scenes/District_1/District_1_Rooms_Flammes.unity");
        try
        {
            // Both ordinary and Ancient Flames provide influence in the runtime
            // contract. Independence concerns scene lifetime, not Flame subtype.
            var flames = lighting.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Flame>(true)).ToArray();
            Assert.That(flames, Is.Not.Empty);
            var ghost = quest.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GhostController>(true)).Single();
            Assert.That(flames.Any(flame => CycleZoneFlameScope.Covers(flame, ghost.transform.position + Vector3.up, false)), Is.True, "Luc must remain usable after Belmont unloads.");
            var items = quest.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InteractableItem>(true)).ToArray();
            Assert.That(items.Length, Is.EqualTo(2));
            foreach (var item in items)
            {
                var collider = item.GetInteractionDetectionCollider();
                Vector3 point = collider is BoxCollider box ? box.transform.TransformPoint(box.center) : item.transform.position;
                Assert.That(flames.Any(flame => CycleZoneFlameScope.Covers(flame, point, false)), Is.True, item.name + " needs permanent light coverage.");
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(lighting);
            EditorSceneManager.ClosePreviewScene(quest);
        }
    }
}
#endif
