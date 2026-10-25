using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BrokenAnchorRecoveryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root, player;
    private CycleController cycle;
    private CycleDefinition definition;
    private WorldRulesStateManager rules;
    private BrokenAnchorTorch torch;
    private SquadCharacterController character;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Isolated Belmont recovery"); root.SetActive(false);
        rules = root.AddComponent<WorldRulesStateManager>();
        cycle = root.AddComponent<CycleController>();
        definition = ScriptableObject.CreateInstance<CycleDefinition>();
        definition.cycleId = "test.belmont.recovery";
        definition.steps = new[] {
            new CycleStep { id = "release", kind = CycleStepKind.SequenceCompleted, sourceId = "anchor_release" },
            new CycleStep { id = "end", kind = CycleStepKind.Interaction, sourceId = "end", terminal = true }
        };
        cycle.definition = definition;
        typeof(CycleController).GetField("devStartEnabled", Private).SetValue(cycle, true);
        typeof(CycleController).GetField("devStartStepId", Private).SetValue(cycle, "release");
        var torchRoot = new GameObject("Existing puzzle torch"); torchRoot.transform.SetParent(root.transform);
        torch = torchRoot.AddComponent<BrokenAnchorTorch>();
        cycle.flames = new[] { new CycleFlameBinding { id = "lower_one", bossTorch = torch } };
        root.SetActive(true);
        torch.TryLight();
        player = new GameObject("Living nearby player"); player.SetActive(false);
        character = player.AddComponent<SquadCharacterController>();
        character.SetHealth(10, 10); player.SetActive(true);
        Physics.SyncTransforms();
        // Edit Mode does not run every runtime lifecycle callback. Initialize
        // the virtual state through the public query before simulating a save.
        Assert.That(cycle.IsSceneStepCompleted("release"), Is.False);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(player); Object.DestroyImmediate(root); Object.DestroyImmediate(definition);
    }

    private HashSet<string> Attempts => (HashSet<string>)typeof(CycleController).GetField("attemptedSequences", Private).GetValue(cycle);
    private bool Retry() => (bool)typeof(BrokenAnchorTorch).GetMethod("TryRetryRelease", Private).Invoke(torch, new object[] { player.transform });

    [Test]
    public void NearbyLivingPlayerCanRearmExactlyOneFailedReleaseWithoutGrantingProgress()
    {
        Attempts.Add("anchor_release");
        Assert.That(Retry(), Is.True);
        Assert.That(Retry(), Is.False, "Simultaneous/repeated requests must not rearm an attempt twice.");
        Assert.That(torch.IsLit, Is.True);
        Assert.That(cycle.IsSceneStepCompleted("release"), Is.False);
        Assert.That(rules.CaptureVariables(), Is.Empty, "Recovery in Dev must not modify the real save.");
    }

    [Test]
    public void TorchWithoutAFailedAttemptCannotLaunchAnExtraRelease()
    {
        Assert.That(Retry(), Is.False);
        Assert.That(cycle.IsSceneStepCompleted("release"), Is.False);
    }

    [Test]
    public void NormalTorchInteractionUsesTheRecoveryPath()
    {
        Attempts.Add("anchor_release");
        torch.SetDetectedCharacter(player);
        Assert.That(torch.TryHandleLocalInteract(), Is.True);
        Assert.That(Attempts, Does.Not.Contain("anchor_release"));
        Assert.That(cycle.IsSceneStepCompleted("release"), Is.False);
    }

    [TestCase("dead")]
    [TestCase("far")]
    [TestCase("disabled_player")]
    [TestCase("disabled_torch")]
    [TestCase("disabled_collider")]
    [TestCase("unlit")]
    [TestCase("busy")]
    [TestCase("unbound")]
    [TestCase("completed_sequence")]
    [TestCase("wrong_sequence")]
    public void InvalidRecoveryRequestCannotChangeTheAttemptOrProgress(string reason)
    {
        Attempts.Add("anchor_release");
        if (reason == "dead") character.SetHealth(0, 10);
        if (reason == "far") player.transform.position = Vector3.right * 20;
        if (reason == "disabled_player") character.enabled = false;
        if (reason == "disabled_torch") torch.enabled = false;
        if (reason == "disabled_collider") torch.GetComponent<Collider>().enabled = false;
        if (reason == "unlit") typeof(BrokenAnchorTorch).GetField("isLit", Private).SetValue(torch, false);
        if (reason == "busy") typeof(CycleController).GetField("cinematicRunning", Private).SetValue(cycle, true);
        if (reason == "unbound") cycle.flames = System.Array.Empty<CycleFlameBinding>();
        if (reason == "completed_sequence") ((HashSet<string>)typeof(CycleController).GetField("devCompletedSteps", Private).GetValue(cycle)).Add("release");
        if (reason == "wrong_sequence") definition.steps[0].sourceId = "last_relief";
        Physics.SyncTransforms();
        Assert.That(Retry(), Is.False);
        Assert.That(Attempts, Does.Contain("anchor_release"));
        Assert.That(cycle.IsSceneStepCompleted("release"), Is.EqualTo(reason == "completed_sequence"));
        Assert.That(rules.CaptureVariables(), Is.Empty);
    }
}
