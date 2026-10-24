using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercises the real controller enumerators and UI ownership without saving or entering Play Mode.</summary>
public sealed class CycleAutoDialogueControllerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private CycleController cycle;
    private CycleDefinition definition;
    private WorldRulesStateManager rules;
    private CycleAutoDialogueBinding binding;
    private DialoguePanelUI previousUi, fakeUi;

    [SetUp] public void SetUp()
    {
        root = new GameObject("Isolated automatic dialogue contract"); root.SetActive(false);
        rules = root.AddComponent<WorldRulesStateManager>();
        cycle = root.AddComponent<CycleController>();
        definition = ScriptableObject.CreateInstance<CycleDefinition>();
        definition.cycleId = "test.auto-dialogue";
        definition.steps = new[] { new CycleStep { id = "intro", sourceId = "spoken_intro", kind = CycleStepKind.DialogueCompleted, terminal = true } };
        cycle.definition = definition;
        binding = new CycleAutoDialogueBinding { id = "intro", line = "Lucian : Une trace subsiste.", durationSeconds = 5f };
        cycle.autoDialogues = new[] { binding };
        Set("rules", rules); Set("devStartEnabled", true); Set("devStartStepId", "intro");
        Invoke("InitializeDevSimulation");
        previousUi = DialoguePanelUI.Instance;
    }

    [TearDown] public void TearDown()
    {
        if (fakeUi != null) SetUiInstance(previousUi);
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(definition);
    }

    private void Set(string name, object value) => typeof(CycleController).GetField(name, Private).SetValue(cycle, value);
    private T Get<T>(string name) => (T)typeof(CycleController).GetField(name, Private).GetValue(cycle);
    private object Invoke(string name, params object[] args) => typeof(CycleController).GetMethod(name, Private).Invoke(cycle, args);
    private static void SetUiInstance(DialoguePanelUI value) => typeof(DialoguePanelUI)
        .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private CycleAutoDialogueSession PendingSession()
    {
        var session = new CycleAutoDialogueSession(7, "spoken_intro", new[] { 0UL }, Time.realtimeSinceStartupAsDouble - 10d, 5d, 30d);
        Set("autoDialogueSession", session);
        Get<HashSet<CycleAutoDialogueBinding>>("playedAutoDialogues").Add(binding);
        return session;
    }

    [Test] public void NaturalCompletionNotElapsedTimeCommitsTheDevStep()
    {
        var session = PendingSession();
        var wait = (IEnumerator)Invoke("WaitForAutoDialogue", session, binding);
        Assert.That(wait.MoveNext(), Is.True);
        Assert.That(cycle.IsSceneStepCompleted("intro"), Is.False);
        Assert.That((bool)Invoke("CompletionPresentationReady"), Is.False);
        Assert.That(session.Acknowledge(0, 7, true, Time.realtimeSinceStartupAsDouble), Is.True);
        Assert.That(wait.MoveNext(), Is.False);
        Assert.That(cycle.IsSceneStepCompleted("intro"), Is.True);
        Assert.That((bool)Invoke("CompletionPresentationReady"), Is.True);
        Assert.That(rules.CaptureVariables(), Is.Empty, "The actual completion path must preserve Dev isolation.");
    }

    [Test] public void FailedSessionLeavesTheStepUnchangedAndRearmsTheBinding()
    {
        var session = PendingSession(); session.Cancel();
        var wait = (IEnumerator)Invoke("WaitForAutoDialogue", session, binding);
        Assert.That(wait.MoveNext(), Is.False);
        Assert.That(cycle.IsSceneStepCompleted("intro"), Is.False);
        Assert.That(Get<HashSet<CycleAutoDialogueBinding>>("playedAutoDialogues").Contains(binding), Is.False);
        Assert.That(Get<float>("nextAutoDialogueAttempt"), Is.GreaterThan(Time.unscaledTime));
        Assert.That(rules.CaptureVariables(), Is.Empty);
    }

    [Test] public void LocalPresenterWaitsWithoutReplacingAnotherOwnersDialogue()
    {
        var uiRoot = new GameObject("Other owner's UI"); uiRoot.SetActive(false); uiRoot.transform.SetParent(root.transform);
        fakeUi = uiRoot.AddComponent<DialoguePanelUI>();
        var otherOwner = new object();
        typeof(DialoguePanelUI).GetField("isShowing", Private).SetValue(fakeUi, true);
        typeof(DialoguePanelUI).GetField("timedConversationOwner", Private).SetValue(fakeUi, otherOwner);
        SetUiInstance(fakeUi);
        var session = PendingSession();
        Set("localAutoDialogueToken", 7); Set("autoDialogueGeneration", 1); Set("localAutoDialoguePending", true);
        root.SetActive(true);
        var local = (IEnumerator)Invoke("LocalAutoDialogue", 7, "intro", 1);
        Assert.That(local.MoveNext(), Is.True);
        Assert.That(fakeUi.IsShowing, Is.True);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Waiting));
        Assert.That(cycle.IsSceneStepCompleted("intro"), Is.False);
        Invoke("CancelAutoDialogues");
        Assert.That(local.MoveNext(), Is.False);
        Assert.That(fakeUi.IsShowing, Is.True, "Cancellation must only close our own timed conversation.");
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Failed));
        Assert.That(rules.CaptureVariables(), Is.Empty);
    }

    [Test] public void EmptyLocalPresentationReportsFailureInsteadOfProgress()
    {
        var session = PendingSession(); binding.line = string.Empty;
        Set("localAutoDialogueToken", 7); Set("autoDialogueGeneration", 1); Set("localAutoDialoguePending", true);
        root.SetActive(true);
        var local = (IEnumerator)Invoke("LocalAutoDialogue", 7, "intro", 1);
        Assert.That(local.MoveNext(), Is.False);
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Failed));
        Assert.That(Get<bool>("localAutoDialoguePending"), Is.False);
        Assert.That(cycle.IsSceneStepCompleted("intro"), Is.False);
        Assert.That(rules.CaptureVariables(), Is.Empty);
    }

    [Test] public void OldStopCannotCancelANewerPresentation()
    {
        Set("localAutoDialogueToken", 9); Set("autoDialogueGeneration", 5); Set("localAutoDialoguePending", true);
        Invoke("StopLocalAutoDialogue", 7);
        Assert.That(Get<bool>("localAutoDialoguePending"), Is.True);
        Assert.That(Get<int>("autoDialogueGeneration"), Is.EqualTo(5));
    }

    [Test] public void DisableCancelsTheBarrierAndClearsOnlyTransientState()
    {
        var session = PendingSession();
        Set("localAutoDialoguePending", true);
        Invoke("OnDisable");
        Assert.That(session.Status, Is.EqualTo(CycleAutoDialogueStatus.Failed));
        Assert.That(Get<CycleAutoDialogueSession>("autoDialogueSession"), Is.Null);
        Assert.That(Get<bool>("localAutoDialoguePending"), Is.False);
        Assert.That(Get<HashSet<CycleAutoDialogueBinding>>("playedAutoDialogues"), Is.Empty);
        Assert.That(cycle.IsSceneStepCompleted("intro"), Is.False);
        Assert.That(rules.CaptureVariables(), Is.Empty);
    }
}
