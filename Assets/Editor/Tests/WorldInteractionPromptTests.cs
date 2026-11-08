using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class WorldInteractionPromptTests
{
    private GameObject root, cameraRoot;
    private LadderController target;
    private WorldInteractionPrompt prompt;
    private static readonly MethodInfo Tick = typeof(WorldInteractionPrompt).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        RuntimeOutlineSelectionManager.Clear();
        root = new GameObject("Selected world label regression");
        root.SetActive(false);
        root.transform.position = Vector3.one * 16000;
        target = root.AddComponent<LadderController>();
        target.leafs1 = new GameObject("Leafs_1").transform;
        target.leafs1.SetParent(root.transform, false);
        target.leafs2 = new GameObject("Leafs_2").transform;
        target.leafs2.SetParent(root.transform, false);
        target.leafs2.localPosition = Vector3.up * 4;
        prompt = root.AddComponent<WorldInteractionPrompt>();
        prompt.target = target;
        prompt.label = "Rejoindre Stillhaven";
        root.SetActive(true);
        cameraRoot = new GameObject("Prompt regression camera", typeof(Camera));
        cameraRoot.tag = "MainCamera";
        cameraRoot.transform.position = root.transform.position + Vector3.back * 10;
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeOutlineSelectionManager.PopSuspension(root);
        RuntimeOutlineSelectionManager.Clear();
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraRoot);
    }

    [Test]
    public void OnlySelectedOwnerDisplaysLabelAndReselectionReusesItsInstance()
    {
        Tick.Invoke(prompt, null);
        Assert.That(root.GetComponentInChildren<TMP_Text>(true), Is.Null);
        RuntimeOutlineSelectionManager.SetActiveInteractable(target);
        Tick.Invoke(prompt, null);
        Assert.That(prompt.IsVisible, Is.True);
        var text = root.GetComponentInChildren<TMP_Text>(true);
        Assert.That(text.text, Is.EqualTo("Rejoindre Stillhaven"));
        RuntimeOutlineSelectionManager.Clear();
        Tick.Invoke(prompt, null);
        Assert.That(prompt.IsVisible, Is.False);
        RuntimeOutlineSelectionManager.SetActiveInteractable(target);
        Tick.Invoke(prompt, null);
        Assert.That(root.GetComponentInChildren<TMP_Text>(true), Is.SameAs(text));
        Assert.That(root.GetComponentsInChildren<TMP_Text>(true).Length, Is.EqualTo(1));
    }

    [Test]
    public void CombatOrUiOutlineSuspensionHidesPromptUntilReleased()
    {
        RuntimeOutlineSelectionManager.SetActiveInteractable(target);
        Tick.Invoke(prompt, null);
        Assert.That(prompt.IsVisible, Is.True);
        RuntimeOutlineSelectionManager.PushSuspension(root);
        Tick.Invoke(prompt, null);
        Assert.That(prompt.IsVisible, Is.False);
        RuntimeOutlineSelectionManager.PopSuspension(root);
        Tick.Invoke(prompt, null);
        Assert.That(prompt.IsVisible, Is.True);
    }

    [Test]
    public void DestroyedTargetHidesPromptAndDisablingOwnerDestroysItsUi()
    {
        RuntimeOutlineSelectionManager.SetActiveInteractable(target);
        Tick.Invoke(prompt, null);
        var text = root.GetComponentInChildren<TMP_Text>(true);
        Object.DestroyImmediate(target);
        Assert.DoesNotThrow(() => Tick.Invoke(prompt, null));
        Assert.That(prompt.IsVisible, Is.False);
        typeof(WorldInteractionPrompt).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(prompt, null);
        Assert.That(text == null, Is.True);
        Assert.That(prompt.IsVisible, Is.False);
    }
}
