using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class InventoryReadableLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(false)]
    [TestCase(true)]
    public void WorldNoteCanBeTakenWhetherItStoresItselfOrNot(bool stored)
    {
        var root = new GameObject("World note");
        root.SetActive(false);
        var note = ScriptableObject.CreateInstance<Item>();
        var unrelated = ScriptableObject.CreateInstance<Item>();
        try
        {
            note.readableKind = Item.ReadableKind.Parchment;
            note.allowTakeFromContainers = true;
            var interactable = root.AddComponent<InteractableItem>();
            interactable.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
            interactable.representedItem = note;
            if (stored)
                interactable.storedItems.Add(new InteractableItem.LootItemEntry { item = note });
            var canTake = typeof(InteractableItem).GetMethod("CanTakeWorldReadableAfterReading", Private);
            bool CanTake() => (bool)canTake.Invoke(interactable, null);
            Assert.That(CanTake(), Is.True);
            interactable.allowTake = false;
            Assert.That(CanTake(), Is.False);
            interactable.allowTake = true;
            interactable.interactableCategory = InteractableItem.InteractableCategory.ReadableItem;
            Assert.That(CanTake(), Is.False, "Fixed documents must stay in the world.");
            interactable.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
            interactable.storedItems.Add(new InteractableItem.LootItemEntry { item = unrelated });
            Assert.That(CanTake(), Is.False, "Reading must not collect unrelated loot.");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(note); Object.DestroyImmediate(unrelated); }
    }

    [TestCase("CloseReadablePanel", 0)]
    [TestCase("TakeReadableWorldItemAndClose", 1)]
    public void CloseLeavesNoteAndTakeInvokesPickupOnlyOnce(string method, int expected)
    {
        var root = new GameObject("Reading fixture");
        root.SetActive(false);
        try
        {
            var controller = root.AddComponent<InventoryPanelController>();
            var type = typeof(InventoryPanelController);
            int pickups = 0;
            type.GetField("readablePanelTakeCallback", Private).SetValue(controller, (System.Action)(() => pickups++));
            type.GetMethod(method, Private).Invoke(controller, null);
            type.GetMethod(method, Private).Invoke(controller, null);
            Assert.That(pickups, Is.EqualTo(expected));
        }
        finally { Object.DestroyImmediate(root); }
    }
    [Test] public void ClosingWithoutCachedInputsDoesNotDiscoverAnotherPanel()
    {
        var root = new GameObject("Disabled inventory fixture");
        root.SetActive(false);
        var unrelated = new GameObject("ReadableActionInputs");
        try
        {
            var controller = root.AddComponent<InventoryPanelController>();
            CloseInputs(controller);
            Assert.That(unrelated.activeSelf, Is.True, "Closing must not discover and hide scene objects during teardown.");
            Assert.That(typeof(InventoryPanelController).GetField("readableActionInputs", Private).GetValue(controller), Is.Null);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(unrelated); }
    }
    [Test] public void DisabledInventoryHidesCachedInputsWithoutReparenting()
    {
        var root = new GameObject("Disabled inventory fixture");
        root.SetActive(false);
        var originalParent = new GameObject("Parchment");
        var currentParent = new GameObject("Book");
        var inputs = new GameObject("Cached inputs");
        inputs.transform.SetParent(currentParent.transform);
        try
        {
            var controller = root.AddComponent<InventoryPanelController>();
            typeof(InventoryPanelController).GetField("readableActionInputs", Private).SetValue(controller, inputs);
            typeof(InventoryPanelController).GetField("readableActionInputsInitialParent", Private).SetValue(controller, originalParent.transform);
            CloseInputs(controller);
            CloseInputs(controller);
            Assert.That(inputs.activeSelf, Is.False);
            Assert.That(inputs.transform.parent, Is.SameAs(currentParent.transform));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(originalParent); Object.DestroyImmediate(currentParent); }
    }
    [Test] public void DestroyedCachedInputsDoNotTriggerRediscovery()
    {
        var root = new GameObject("Disabled inventory fixture");
        root.SetActive(false);
        var inputs = new GameObject("Old inputs");
        var unrelated = new GameObject("ReadableActionInputs");
        try
        {
            var controller = root.AddComponent<InventoryPanelController>();
            typeof(InventoryPanelController).GetField("readableActionInputs", Private).SetValue(controller, inputs);
            Object.DestroyImmediate(inputs);
            Assert.DoesNotThrow(() => CloseInputs(controller));
            Assert.That(unrelated.activeSelf, Is.True);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(unrelated); if (inputs != null) Object.DestroyImmediate(inputs); }
    }
    private static void CloseInputs(InventoryPanelController controller) =>
        typeof(InventoryPanelController).GetMethod("SetReadableActionInputsVisible", Private).Invoke(controller, new object[] { false });
}
