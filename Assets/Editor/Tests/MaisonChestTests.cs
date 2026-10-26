using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public sealed class MaisonChestTests
{
    private const string PrefabPath = "Assets/Environment/Castle/Maison_Chest.prefab";

    [Test]
    public void AuthoredChestIsAnAccessiblePermanentStorageContainer()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var container = prefab.GetComponent<InteractableItem>();
        Assert.That(container, Is.Not.Null);
        Assert.That(container.interactableCategory, Is.EqualTo(InteractableItem.InteractableCategory.Container));
        Assert.That(container.allowTake, Is.True);
        Assert.That(container.destroyWhenStorageEmpty || container.isLocked || container.isTrapped, Is.False);
        Assert.That(container.maxStoredQuantity, Is.EqualTo(100));
        Assert.That(prefab.CompareTag("MaisonChest"), Is.True);
        Assert.That(container.interactionTrigger, Is.TypeOf<BoxCollider>());
        Assert.That(container.interactionTrigger.isTrigger && container.interactionTrigger.enabled, Is.True);
        var properties = new SerializedObject(container);
        Assert.That(properties.FindProperty("requireLitInfluenceForInteraction").boolValue, Is.False);
        var item = container.representedItem;
        Assert.That(item, Is.Not.Null);
        Assert.That(item.isHomeChest && item.isContainer, Is.True);
        Assert.That(item.isBuilding || item.canPlace || item.canBreak, Is.False);
    }

    [Test]
    public void WorldInformationDoesNotHijackTheStorageInteraction()
    {
        var info = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<BuildingInfoInteractable>();
        Assert.That(info, Is.Not.Null);
        Assert.That(info.consumeInteractOnProximity || info.openCraftingPanelOnInteract, Is.False);
    }

    [Test]
    public void StoredQuantitiesSurviveTheExistingPersistenceRoundTrip()
    {
        var root = new GameObject("Maison chest persistence test");
        root.SetActive(false);
        try
        {
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<InteractableItem>();
            Item item = authored.storedItems[0].item;
            ItemRegistry.RegisterLoadedDefinition(item);
            ItemRegistry.RegisterLoadedDefinition(authored.representedItem);
            var container = root.AddComponent<InteractableItem>();
            container.showWorldInteractionUi = false;
            container.representedItem = authored.representedItem;
            container.maxStoredQuantity = authored.maxStoredQuantity;
            container.storedItems = new List<InteractableItem.LootItemEntry>
            {
                new InteractableItem.LootItemEntry { item = item, quantity = 7 }
            };
            var persistence = root.AddComponent<PersistentContainerState>();
            byte[] saved = persistence.CaptureState(null);
            container.storedItems.Clear();
            persistence.ApplyState(saved, PersistentApplyPhase.ApplyGameplayState, null);
            Assert.That(container.GetItemCount(item), Is.EqualTo(7));
            Assert.That(container.GetRemainingCapacity(), Is.EqualTo(93));
            Assert.That(container.representedItem, Is.SameAs(authored.representedItem));
            Assert.That(container.allowTake, Is.True);
            Assert.That(container.destroyWhenStorageEmpty, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }
}

[InitializeOnLoad]
public sealed class MaisonChestTestRunner : ICallbacks
{
    private const string Request = "Library/MaisonChestTests.request";
    private static TestRunnerApi runner;
    static MaisonChestTestRunner() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || runner != null || !File.Exists(Request)) return;
        File.Delete(Request);
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new MaisonChestTestRunner());
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^MaisonChestTests" } }) { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor test) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, "Library/MaisonChestTests.xml");
        runner.UnregisterCallbacks(this);
        Object.DestroyImmediate(runner);
        runner = null;
    }
}
