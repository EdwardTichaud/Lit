using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Ultrabolt.BrainsAI;

public sealed class IluviliraeTests
{
    [Test]
    public void ExternalDetectionChangesTargetAndReportsLossOnlyOnce()
    {
        var owner = new GameObject("Perception test");
        var first = new GameObject("Player one");
        var second = new GameObject("Player two");
        try
        {
            var fov = owner.AddComponent<FOV>();
            fov.useExternalDetection = true;
            fov.onDetectTarget = new UnityEngine.Events.UnityEvent();
            fov.onLostTarget = new UnityEngine.Events.UnityEvent();
            int detects = 0, losses = 0;
            fov.onDetectTarget.AddListener(() => detects++);
            fov.onLostTarget.AddListener(() => losses++);
            fov.SetExternalTarget(first.transform);
            fov.SetExternalTarget(first.transform);
            Assert.That(detects, Is.EqualTo(1));
            fov.SetExternalTarget(second.transform);
            Assert.That(fov.CurrentTarget, Is.EqualTo(second.transform));
            Assert.That(detects, Is.EqualTo(2));
            fov.SetExternalTarget(null);
            fov.SetExternalTarget(null);
            Assert.That(losses, Is.EqualTo(1));
            Assert.That(fov.status, Is.EqualTo(FOV.Status.Investigate));
            Assert.That(fov.visibleTargets, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(owner); Object.DestroyImmediate(first); Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void DestroyedPlayerStillProducesOneLossAndPreservesLastKnownPosition()
    {
        var owner = new GameObject("Perception test");
        var target = new GameObject("Player");
        try
        {
            target.transform.position = new Vector3(3, 0, 5);
            var fov = owner.AddComponent<FOV>();
            fov.useExternalDetection = true;
            fov.SetExternalTarget(target.transform);
            Object.DestroyImmediate(target);
            fov.SetExternalTarget(null);
            Assert.That(fov.CurrentTarget, Is.Null);
            Assert.That(fov.visibleTargets, Is.Empty);
            Assert.That(fov.lastKnownPosition, Is.EqualTo(new Vector3(3, 0, 5)));
        }
        finally { Object.DestroyImmediate(owner); if (target != null) Object.DestroyImmediate(target); }
    }

    [Test]
    public void PrefabOwnsItsAnimatorAndHasNoLegacyBrain()
    {
        Assert.DoesNotThrow(IluviliraeSetup.Validate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IluviliraeSetup.PrefabPath);
        Assert.That(prefab.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, Is.False);
        var controller = (AnimatorController)prefab.GetComponent<Animator>().runtimeAnimatorController;
        Assert.That(AssetDatabase.GetAssetPath(controller), Is.EqualTo(IluviliraeSetup.ControllerPath));
        var states = controller.layers[0].stateMachine.states;
        Assert.That(states.Length, Is.EqualTo(4));
        foreach (var state in states)
            if (state.state.name != "Locomotion")
                Assert.That(AnimationUtility.GetAnimationClipSettings((AnimationClip)state.state.motion).loopTime, Is.False);
    }
}
