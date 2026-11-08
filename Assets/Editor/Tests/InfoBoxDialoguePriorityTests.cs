using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class InfoBoxDialoguePriorityTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private InfoBoxUI info;
    private DialoguePanelUI dialogue, previous;
    private CanvasGroup group;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Isolated toast/dialogue priority");
        root.SetActive(false);
        info = root.AddComponent<InfoBoxUI>();
        info.infoBoxRoot = new GameObject("Toast panel", typeof(RectTransform), typeof(CanvasGroup));
        info.infoBoxRoot.transform.SetParent(root.transform, false);
        group = info.infoBoxRoot.GetComponent<CanvasGroup>();
        group.alpha = .65f;
        typeof(InfoBoxUI).GetField("isShowing", Private).SetValue(info, true);
        dialogue = root.AddComponent<DialoguePanelUI>();
        previous = DialoguePanelUI.Instance;
        SetDialogueInstance(dialogue);
        SetDialogueShowing(true);
    }

    private static void SetDialogueInstance(DialoguePanelUI value) => typeof(DialoguePanelUI)
        .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private void SetDialogueShowing(bool value) => typeof(DialoguePanelUI).GetField("isShowing", Private).SetValue(dialogue, value);
    private IEnumerator Wait() => (IEnumerator)typeof(InfoBoxUI).GetMethod("WaitForDialogueIfToast", Private).Invoke(info, null);

    [TearDown]
    public void TearDown()
    {
        SetDialogueInstance(previous);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void PassiveToastHidesDuringReadingAndRestoresItsActualFadeAlpha()
    {
        var wait = Wait();
        Assert.That(wait.MoveNext(), Is.True);
        Assert.That(group.alpha, Is.Zero);
        Assert.That(wait.MoveNext(), Is.True);
        Assert.That(group.alpha, Is.Zero);
        SetDialogueShowing(false);
        Assert.That(wait.MoveNext(), Is.False);
        Assert.That(group.alpha, Is.EqualTo(.65f));
        Assert.That(group.blocksRaycasts, Is.False);
    }

    [Test]
    public void ModalInfoBoxRetainsItsExistingInputOwnershipAndPresentation()
    {
        typeof(InfoBoxUI).GetField("holdsInputFocus", Private).SetValue(info, true);
        Assert.That(Wait().MoveNext(), Is.False);
        Assert.That(group.alpha, Is.EqualTo(.65f));
    }

    [Test]
    public void CancelledToastIsNotResurrectedAfterTheDialogueEnds()
    {
        var wait = Wait();
        Assert.That(wait.MoveNext(), Is.True);
        typeof(InfoBoxUI).GetField("isShowing", Private).SetValue(info, false);
        SetDialogueShowing(false);
        Assert.That(wait.MoveNext(), Is.False);
        Assert.That(group.alpha, Is.Zero);
    }
}
