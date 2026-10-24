#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class ClairmontCycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Test] public void AuthoredScenesAndAbilitiesAreValid() => Assert.That(ClairmontCycleValidation.Audit(), Is.Empty);
    [TestCase(0, 1)] [TestCase(1, 0)]
    public void ProofsRemainParallelAndGateTheRegister(int first, int second)
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/ClairmontCycle.asset");
        var go = new GameObject("Clairmont isolated Dev test"); go.SetActive(false);
        try
        {
            var rules = go.AddComponent<WorldRulesStateManager>(); var cycle = go.AddComponent<CycleController>(); cycle.definition = definition;
            typeof(CycleController).GetField("rules", Private).SetValue(cycle, rules);
            typeof(CycleController).GetField("devStartEnabled", Private).SetValue(cycle, true);
            typeof(CycleController).GetField("devStartStepId", Private).SetValue(cycle, "relief_register_read");
            typeof(CycleController).GetMethod("InitializeDevSimulation", Private).Invoke(cycle, null);
            var active = typeof(CycleController).GetMethod("IsStepActive", Private);
            var complete = typeof(CycleController).GetMethod("DevCompleteStep", Private);
            var proofs = new[] { definition.FindStep("relief_register_read"), definition.FindStep("old_route_read") };
            Assert.That((bool)active.Invoke(cycle, new object[] { definition.FindStep("register_consulted") }), Is.False);
            foreach (int index in new[] { first, second })
            {
                Assert.That((bool)active.Invoke(cycle, new object[] { proofs[index] }), Is.True);
                complete.Invoke(cycle, new object[] { proofs[index] });
            }
            Assert.That((bool)active.Invoke(cycle, new object[] { definition.FindStep("register_consulted") }), Is.True);
            Assert.That((bool)active.Invoke(cycle, new object[] { definition.FindStep("agnes_spoken") }), Is.False);
            complete.Invoke(cycle, new object[] { definition.FindStep("register_consulted") });
            Assert.That((bool)active.Invoke(cycle, new object[] { definition.FindStep("agnes_spoken") }), Is.True);
            Assert.That(rules.CaptureVariables(), Is.Empty, "Dev must not write a real milestone or unlock.");
        }
        finally { Object.DestroyImmediate(go); }
    }
    [TestCase(ClairmontCycleValidation.Env)] [TestCase(ClairmontCycleValidation.Shortcuts)]
    public void BothEndpointsShareDiscoveryAndTeleportInBothDirections(string path)
    {
        var previous = SceneManager.GetActiveScene(); var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        var actor = new GameObject("Shortcut destination probe");
        TrouEtroit passage = null; bool previousDetected = false;
        try
        {
            passage = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TrouEtroit>(true)).Single();
            previousDetected = passage.IsDetected; passage.RestoreDetectedState(false);
            Physics.SyncTransforms();
            Assert.That(passage.IsDetected, Is.False);
            Assert.That(passage.IsInsideInteractionVolume(passage.teleportPointA.position), Is.True);
            Assert.That(passage.IsInsideInteractionVolume(passage.teleportPointB.position), Is.True);
            Assert.That(passage.IsInsideInteractionVolume((passage.teleportPointA.position + passage.teleportPointB.position) * .5f), Is.False);
            var method = typeof(TrouEtroit).GetMethod("TryGetTransformTeleportDestination", Private);
            foreach (bool forward in new[] { true, false })
            {
                actor.transform.position = forward ? passage.teleportPointA.position : passage.teleportPointB.position;
                var args = new object[] { actor.transform, Vector3.zero, Quaternion.identity };
                Assert.That((bool)method.Invoke(passage, args), Is.True);
                var target = forward ? passage.teleportPointB : passage.teleportPointA;
                Assert.That(Vector3.Distance((Vector3)args[1], target.position + passage.teleportOffset), Is.LessThan(.001f));
            }
            passage.Detect();
            Assert.That(passage.IsDetected, Is.True);
            var persistent = passage.GetComponent<PersistentSecretPassageState>();
            Assert.That(persistent, Is.Not.Null);
            Assert.That(passage.ServerTryPassage(null, true), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(actor);
            if (!opened && passage != null) passage.RestoreDetectedState(previousDetected);
            // Do not save presentation changes produced by the test.
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    [TestCase(ClairmontCycleValidation.Env)] [TestCase(ClairmontCycleValidation.Shortcuts)]
    public void ServerRequiresDiscoveryAndEquippedAbilityAtBothEnds(string path)
    {
        Assert.That(Application.isPlaying, Is.False, "This isolated contract test must not touch a live session.");
        Assert.That(AbilityService.Instance == null, Is.True, "Do not replace an existing player ability session.");
        var previous = SceneManager.GetActiveScene(); var oldPlayer = LocalPlayerContext.LocalCharacterRoot;
        var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        // Opening the authored home works even when NUnit's active scene is
        // untitled. Never create/save a scene or substitute a production asset.
        const string homePath = "Assets/Scenes/Maison/Maison_Core.unity";
        var home = SceneManager.GetSceneByPath(homePath); bool openedHome = !home.IsValid() || !home.isLoaded;
        if (openedHome) home = EditorSceneManager.OpenScene(homePath, OpenSceneMode.Additive);
        var actor = new GameObject("Ability traversal test actor"); actor.SetActive(false);
        actor.AddComponent<Rigidbody>(); actor.AddComponent<CapsuleCollider>(); actor.AddComponent<SquadCharacterController>();
        var serviceObject = new GameObject("Isolated ability service"); serviceObject.SetActive(false);
        var service = serviceObject.AddComponent<AbilityService>();
        TrouEtroit passage = null; bool oldDetected = false;
        try
        {
            typeof(AbilityService).GetMethod("Awake", Private).Invoke(service, null);
            LocalPlayerContext.SetLocalCharacter(actor.transform, "isolated ability test");
            passage = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TrouEtroit>(true)).Single();
            oldDetected = passage.IsDetected; passage.RestoreDetectedState(false);
            actor.transform.position = passage.teleportPointA.position; Physics.SyncTransforms();
            Assert.That(service.HasEquipped(actor, passage.observateurAbility), Is.True);
            Assert.That(service.IsUnlocked(passage.saufConduitAbility), Is.False);
            Assert.That(passage.ServerTryPassage(actor, true), Is.False, "Early encounter must not allow travel.");
            Assert.That(service.Unlock(passage.saufConduitAbility, false), Is.True);
            SceneManager.SetActiveScene(home);
            Assert.That(service.RequestEquip(passage.saufConduitAbility), Is.True);
            Assert.That(passage.ServerTryPassage(actor, true), Is.False, "Sauf-Conduit cannot bypass discovery.");
            Assert.That(service.RequestEquip(passage.observateurAbility), Is.True);
            Assert.That(passage.ServerTryPassage(actor, false), Is.True, "Observer discovers the shared passage.");
            Assert.That(passage.ServerTryPassage(actor, true), Is.False, "Observer does not also grant Sauf-Conduit.");
            Assert.That(service.RequestEquip(passage.saufConduitAbility), Is.True);
            SceneManager.SetActiveScene(scene);
            Assert.That(service.RequestEquip(passage.observateurAbility), Is.False, "Switching outside Maison is forbidden.");
            Assert.That(passage.ServerTryPassage(actor, true), Is.True);
            actor.transform.position = passage.teleportPointB.position;
            Assert.That(passage.ServerTryPassage(actor, true), Is.True, "Same permission at the second endpoint.");
            actor.transform.position += Vector3.up * 20;
            Assert.That(passage.ServerTryPassage(actor, true), Is.False, "Remote traversal claims must be rejected.");
        }
        finally
        {
            LocalPlayerContext.ClearIfMatch(actor.transform, "isolated ability test cleanup");
            if (oldPlayer != null) LocalPlayerContext.SetLocalCharacter(oldPlayer, "restored editor context");
            typeof(AbilityService).GetMethod("OnDestroy", Private).Invoke(service, null);
            Object.DestroyImmediate(serviceObject); Object.DestroyImmediate(actor);
            if (!opened && passage != null) passage.RestoreDetectedState(oldDetected);
            if (openedHome) EditorSceneManager.CloseScene(home, true);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
#endif
