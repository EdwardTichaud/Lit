using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class InventoryReadableLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
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
