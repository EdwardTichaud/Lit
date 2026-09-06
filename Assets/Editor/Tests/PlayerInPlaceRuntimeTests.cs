using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Exercises the real player avatar and UCC collision pipeline in an isolated test area.</summary>
public sealed class PlayerInPlaceRuntimeTests
{
    private GameObject container;

    [UnityTest]
    public IEnumerator StateTrajectoryMovesThroughUccAndStopsAtWall()
    {
        bool reload = SessionState.GetBool("PlayerInPlace.Test.Reload", false);
        SessionState.SetBool("PlayerInPlace.Test.OptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt("PlayerInPlace.Test.Options", (int)EditorSettings.enterPlayModeOptions);
        SessionState.SetBool("PlayerInPlace.Test.RestoreOptions", true);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = reload ? EnterPlayModeOptions.None : EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        yield return new EnterPlayMode(reload);
        container = new GameObject("Player InPlace runtime test");
        container.SetActive(false);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerInPlaceAudit.LucianPath), container.transform);
        var keep = new HashSet<string> { "UltimateCharacterLocomotion", "UltimateCharacterLocomotionHandler", "LitOpsivePlayerInput",
            "LitOpsiveLocomotionBridge", "LitOpsiveLookSource", "AnimatorMonitor", "CharacterLayerManager", "CombatActorAnimationRoot",
            "CombatActorRootMotionRelay", "PlayerStateMotionController", "PlayerScriptedJumpController", "CombatTimeDomain",
            "CharacterAttributeManager", "CharacterHealth" };
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (component != null && !keep.Contains(component.GetType().Name)) Object.DestroyImmediate(component);
        var actor = root.GetComponent<CombatActorAnimationRoot>();
        var animator = actor.Animator;
        foreach (var other in root.GetComponentsInChildren<Animator>(true)) other.enabled = other == animator;
        animator.fireEvents = false;
        var origin = new Vector3(10000, 0, 10000);
        root.transform.SetPositionAndRotation(origin + Vector3.up * .02f, Quaternion.identity);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "InPlace test floor";
        floor.transform.SetParent(container.transform);
        floor.transform.position = origin - Vector3.up * .5f;
        floor.transform.localScale = new Vector3(20, 1, 20);
        container.SetActive(true);
        var bridge = root.GetComponent<LitOpsiveLocomotionBridge>();
        var motion = root.GetComponent<PlayerStateMotionController>();
        float timeout = Time.realtimeSinceStartup + 4;
        while (!bridge.Grounded && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(bridge.Grounded && bridge.IsDriving, Is.True, "UCC test actor must be grounded and driving");
        string state = "Base Layer.BasicSkill_1";
        int hash = Animator.StringToHash(state);
        var profile = motion.Library.Find(hash);
        Assert.That(profile, Is.Not.Null);
        var start = root.transform.position;
        animator.Play(hash, 0, 0);
        animator.Update(0);
        motion.SetActionPolicy(hash, PlayerActionMovementPolicy.StateTrajectory);
        timeout = Time.realtimeSinceStartup + 3;
        while (!motion.IsActive && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(motion.IsActive, Is.True, "State trajectory never acquired UCC motion");
        while (motion.IsActive && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(motion.IsActive || bridge.IsExternalLockActive, Is.False, "Motion slot leaked after completion");
        Vector3 freeDisplacement = Vector3.ProjectOnPlane(root.transform.position - start, Vector3.up);
        Vector3 expected = profile.Position(1) - profile.Position(0);
        Assert.That(Vector3.Distance(freeDisplacement, expected), Is.LessThanOrEqualTo(Mathf.Max(.05f, expected.magnitude * .05f)),
            "UCC displacement must match the migrated source trajectory");

        bridge.SetCinematicPositionAndRotation(start, Quaternion.identity, false, false);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(container.transform);
        Vector3 direction = expected.normalized;
        wall.transform.SetPositionAndRotation(start + direction * .65f + Vector3.up * 1.5f, Quaternion.LookRotation(direction));
        wall.transform.localScale = new Vector3(8, 3, .1f);
        Physics.SyncTransforms();
        animator.Play(hash, 0, 0);
        animator.Update(0);
        motion.SetActionPolicy(hash, PlayerActionMovementPolicy.StateTrajectory);
        timeout = Time.realtimeSinceStartup + 3;
        while (!motion.IsActive && Time.realtimeSinceStartup < timeout) yield return null;
        while (motion.IsActive && Time.realtimeSinceStartup < timeout) yield return null;
        float blockedDistance = Vector3.Dot(root.transform.position - start, direction);
        Assert.That(blockedDistance, Is.LessThan(.65f), "Trajectory crossed the wall");
        Assert.That(bridge.IsExternalLockActive, Is.False);
        actor.BeginCinematicMotion(42);
        Assert.That(animator.applyRootMotion, Is.True);
        actor.EndCinematicMotion(42);
        Assert.That(animator.applyRootMotion, Is.False);
        wall.SetActive(false);
        for (int interruption = 0; interruption < 3; interruption++)
        {
            bridge.SetCinematicPositionAndRotation(start, Quaternion.identity, false, false);
            animator.Play(hash, 0, 0);
            animator.Update(0);
            motion.SetActionPolicy(hash, PlayerActionMovementPolicy.StateTrajectory);
            timeout = Time.realtimeSinceStartup + 2;
            while (!motion.IsActive && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(motion.IsActive, Is.True, "Interrupted action must first acquire motion");
            if (interruption == 0) motion.Cancel();
            else if (interruption == 1) motion.enabled = false;
            else actor.BeginCinematicMotion(99);
            Assert.That(motion.IsActive || bridge.IsExternalLockActive, Is.False, "Interruption leaked the UCC motion slot");
            if (interruption == 1) motion.enabled = true;
            if (interruption == 2) actor.EndCinematicMotion(99);
        }
        var motor = root.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>();
        foreach (string landing in new[] { "Base Layer.Jump_End", "Base Layer.Landing_Hard" })
        {
            bridge.SetCinematicPositionAndRotation(start, Quaternion.identity, false, false);
            animator.Play(Animator.StringToHash(landing), 0, .2f);
            animator.Update(0);
            animator.speed = 0;
            Assert.That(motion.RefreshLandingLock(), Is.True, landing);
            var landingPosition = root.transform.position;
            var landingRotation = root.transform.rotation;
            motor.AddForce(Vector3.forward * 8, 1, false);
            float holdUntil = Time.realtimeSinceStartup + .3f;
            while (Time.realtimeSinceStartup < holdUntil)
            {
                bridge.SetMoveInput(Vector2.right, true);
                yield return null;
            }
            Assert.That(Vector3.ProjectOnPlane(root.transform.position - landingPosition, Vector3.up).magnitude, Is.LessThan(.01f), landing + " slid during landing");
            Assert.That(Quaternion.Angle(root.transform.rotation, landingRotation), Is.LessThan(.1f), landing + " turned during landing");
            animator.speed = 1;
            animator.Play(Animator.StringToHash("Base Layer.CombatIdle"), 0, 0);
            animator.Update(0);
            motion.RefreshLandingLock();
            Assert.That(bridge.IsExternalLockActive, Is.False, "Landing lock did not release");
            bridge.SetMoveInput(Vector2.right, true);
            Assert.That(bridge.CurrentWorldMoveInput.sqrMagnitude, Is.GreaterThan(0f), "Maintained input was not accepted after landing");
        }
        Debug.Log($"[Player InPlace Runtime] Free={freeDisplacement.magnitude:F4} expected={expected.magnitude:F4}; wall={blockedDistance:F4}; cinematic and landing locks OK");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (container != null) Object.DestroyImmediate(container);
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        if (SessionState.GetBool("PlayerInPlace.Test.RestoreOptions", false))
        {
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("PlayerInPlace.Test.OptionsEnabled", true);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt("PlayerInPlace.Test.Options", 3);
            SessionState.EraseBool("PlayerInPlace.Test.RestoreOptions");
        }
    }
}
