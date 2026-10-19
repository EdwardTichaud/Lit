using System;
using System.Reflection;
using Lit.Combat.Migration.Network;
using NUnit.Framework;
using Opsive.UltimateCharacterController.Character;
using UnityEditor;
using UnityEngine;

public sealed class CombatNetworkContactDashTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [TestCase(ContactDashMotion.Approach, false)]
    [TestCase(ContactDashMotion.Burst, true)]
    public void ExternalDashUsesItsCapturedMotionAndCannotLeakItIntoNextSession(ContactDashMotion motion, bool burst)
    {
        var root = new GameObject("External dash motion fixture");
        try
        {
            var action = root.AddComponent<PlayerActionPresentationController>();
            var type = typeof(PlayerActionPresentationController);
            var resolve = type.GetMethod("ResolveContactBurstMotion", Private);
            int first = action.BeginExternalPresentation(action);
            type.GetField("externalContactMotion", Private).SetValue(action, motion);
            type.GetField("externalContactResolver", Private).SetValue(action,
                new Func<Transform, Vector3, Vector3, bool>((_, __, ___) => true));
            Assert.That(resolve.Invoke(action, null), Is.EqualTo(burst));
            action.EndExternalPresentation(first);
            Assert.That(type.GetField("externalContactResolver", Private).GetValue(action), Is.Null);
            int second = action.BeginExternalPresentation(action);
            Assert.That(resolve.Invoke(action, null), Is.False, "No local manager or previous session may supply an external dash mode.");
            type.GetField("externalContactMotion", Private).SetValue(action, ContactDashMotion.Burst);
            type.GetField("externalContactResolver", Private).SetValue(action,
                new Func<Transform, Vector3, Vector3, bool>((_, __, ___) => true));
            action.EndExternalPresentation(first);
            Assert.That(resolve.Invoke(action, null), Is.True, "A stale end cannot clear the new dash.");
            action.EndExternalPresentation(second);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void ExternalDashRejectsUnknownMotionAndStaleGenerationBeforeAcquiringMotor()
    {
        var root = new GameObject("External dash parameter fixture");
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            var action = root.AddComponent<PlayerActionPresentationController>();
            int session = action.BeginExternalPresentation(action);
            Assert.That(action.ConfigureExternalSkill(session, skill), Is.True);
            Assert.That(action.SetExternalAttackContext(session,
                new PlayerAttackContext(session, skill, Vector3.zero, Vector3.forward, null, false)), Is.True);
            Func<Transform, Vector3, Vector3, bool> contact = (_, __, ___) => true;
            Assert.That(action.TryBeginExternalContactLunge(session, skill, contact, 10, (ContactDashMotion)99), Is.False);
            Assert.That(action.TryBeginExternalContactLunge(session - 1, skill, contact, 10, ContactDashMotion.Burst), Is.False);
            Assert.That(action.IsTargetLungeActive, Is.False);
            Assert.That(typeof(PlayerActionPresentationController).GetField("externalContactResolver", Private).GetValue(action), Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(skill); }
    }

    [TestCase(0f)]
    [TestCase(60f)]
    public void ExternalRetreatBrakingIsCapturedAndCleanedByItsOwnGeneration(float deceleration)
    {
        var root = new GameObject("External retreat braking fixture");
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        skill.RetreatImpulse.enabled = true;
        try
        {
            var action = root.AddComponent<PlayerActionPresentationController>();
            var type = typeof(PlayerActionPresentationController);
            var resolve = type.GetMethod("ResolveRetreatLandingDeceleration", Private);
            var profile = new FreeAttackProfile { retreatLandingDeceleration = deceleration };
            int first = action.BeginExternalPresentation(action);
            Assert.That(action.ConfigureExternalSkill(first, skill), Is.True);
            Assert.That(action.ConfigureExternalRetreatBraking(first, profile.retreatLandingDeceleration), Is.True);
            profile.retreatLandingDeceleration = 123;
            Assert.That(resolve.Invoke(action, null), Is.EqualTo(deceleration), "The action must retain its server session value.");
            action.EndExternalPresentation(first);
            Assert.That(type.GetField("externalRetreatBrakingGeneration", Private).GetValue(action), Is.EqualTo(-1));
            Assert.That(type.GetField("externalRetreatLandingDeceleration", Private).GetValue(action), Is.EqualTo(0f));
            int second = action.BeginExternalPresentation(action);
            Assert.That(resolve.Invoke(action, null), Is.EqualTo(0f));
            Assert.That(action.ConfigureExternalSkill(second, skill), Is.True);
            Assert.That(action.ConfigureExternalRetreatBraking(first, 99), Is.False);
            Assert.That(action.ConfigureExternalRetreatBraking(second, 17), Is.True);
            action.EndExternalPresentation(first);
            Assert.That(resolve.Invoke(action, null), Is.EqualTo(17f));
            action.EndExternalPresentation(second);
            Assert.That(type.GetField("externalRetreatLandingDeceleration", Private).GetValue(action), Is.EqualTo(0f));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(skill); }
    }

    [Test]
    public void ExternalRetreatBrakingRejectsInvalidValuesAndNonRetreatSessions()
    {
        var root = new GameObject("External retreat braking validation fixture");
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            var action = root.AddComponent<PlayerActionPresentationController>();
            Assert.That(action.ConfigureExternalRetreatBraking(action.ActionGeneration, 60), Is.False);
            int session = action.BeginExternalPresentation(action);
            Assert.That(action.ConfigureExternalSkill(session, skill), Is.True);
            Assert.That(action.ConfigureExternalRetreatBraking(session, 60), Is.False);
            skill.RetreatImpulse.enabled = true;
            Assert.That(action.ConfigureExternalRetreatBraking(session, 23), Is.True);
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.That(action.ConfigureExternalRetreatBraking(session, invalid), Is.False);
            var resolve = typeof(PlayerActionPresentationController).GetMethod("ResolveRetreatLandingDeceleration", Private);
            Assert.That(resolve.Invoke(action, null), Is.EqualTo(23f), "Rejected configuration cannot erase valid braking.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(skill); }
    }

    [Test]
    public void FreeDashDistanceUsesTheSharedAuthoredProfileAndNeverInventsAFallback()
    {
        var resolve = typeof(CombatNetworkActorRuntime).GetMethod("TryResolveContactDashDistance", BindingFlags.NonPublic | BindingFlags.Static);
        var profile = new FreeAttackProfile();
        var arguments = new object[] { profile, 0f };
        Assert.That(resolve.Invoke(null, arguments), Is.True);
        Assert.That(arguments[1], Is.EqualTo(10f), "The common profile owns the default, not a network constant.");
        profile.freeDashDistance = 7.5f;
        Assert.That(resolve.Invoke(null, arguments), Is.True); Assert.That(arguments[1], Is.EqualTo(7.5f));
        profile.enabled = false;
        Assert.That(resolve.Invoke(null, arguments), Is.False);
        profile.enabled = true;
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, 129f })
        {
            profile.freeDashDistance = invalid;
            Assert.That(resolve.Invoke(null, arguments), Is.False);
            Assert.That(arguments[1], Is.EqualTo(0f));
        }
        arguments[0] = null;
        Assert.That(resolve.Invoke(null, arguments), Is.False);
    }

    [TestCase("Assets/CombatRealTime/Skills/Skill_1_Eclair.asset", true, 0f, 10f)]
    [TestCase("Assets/CombatRealTime/Skills/Skill_BondOffensif.asset", false, 2f, 30f)]
    public void RealDashAssetsRetainNativeContactAndTheirDistinctLaunchRanges(string path, bool passThrough, float minimum, float maximum)
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(path);
        Assert.That(skill, Is.Not.Null);
        Assert.That(CombatNetworkSkillCatalog.Classify(skill, out string diagnostic), Is.EqualTo(CombatNetworkSkillFamily.ContactDash), diagnostic);
        Assert.That(skill.TargetLunge.passThroughTarget, Is.EqualTo(passThrough));
        Assert.That(skill.IsWithinLaunchRange(minimum), Is.True);
        Assert.That(skill.IsWithinLaunchRange(maximum), Is.True);
        Assert.That(skill.IsWithinLaunchRange(maximum + .01f), Is.False);
        if (minimum > 0) Assert.That(skill.IsWithinLaunchRange(minimum - .01f), Is.False);
        Assert.That(CombatNetworkSkillCatalog.TryGetContactWindow(skill, out float time, out diagnostic), Is.True, diagnostic);
        Assert.That(time, Is.EqualTo(.08f).Within(.001f));
    }

    [Test]
    public void MissingContactMarkerCannotBecomeTimerDamage()
    {
        var original = AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/CombatRealTime/Skills/Skill_1_Eclair.asset");
        var skill = UnityEngine.Object.Instantiate(original);
        var clip = UnityEngine.Object.Instantiate(skill.AnimationClip);
        try
        {
            skill.animationClip = clip;
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = "OpenSkillContactWindow", time = .08f },
                new AnimationEvent { functionName = "HitEnemy", time = .5f } });
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(clip); }
    }

    [Test]
    public void FabricatedContactWindowCannotArmAnUnspawnedSource()
    {
        var root = new GameObject("Network contact source fixture");
        try
        {
            var runtime = root.AddComponent<CombatNetworkActorRuntime>();
            typeof(CombatNetworkActorRuntime).GetMethod("OnAnimationContactWindow", Private).Invoke(runtime,
                new object[] { new AnimationEvent { functionName = "OpenSkillContactWindow", time = .08f } });
            Assert.That(typeof(CombatNetworkActorRuntime).GetField("dashWindowArmed", Private).GetValue(runtime), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static LitOpsiveLocomotionBridge Bridge(GameObject root, out UltimateCharacterLocomotion locomotion, out CapsuleCollider body)
    {
        body = root.AddComponent<CapsuleCollider>(); body.center = Vector3.up; body.height = 2; body.radius = .4f;
        var bridge = root.AddComponent<LitOpsiveLocomotionBridge>();
        locomotion = root.GetComponent<UltimateCharacterLocomotion>();
        // Supply the same native collider stores normally populated by UCC Awake, without running a gameplay scene in Edit Mode.
        typeof(CharacterLocomotion).GetField("m_Colliders", Private).SetValue(locomotion, new Collider[] { body });
        typeof(CharacterLocomotion).GetField("m_IgnoredColliders", Private).SetValue(locomotion, Array.Empty<Collider>());
        typeof(CharacterLocomotion).GetField("m_IgnoredColliderLayers", Private).SetValue(locomotion, Array.Empty<int>());
        typeof(CharacterLocomotion).GetField("m_IgnoredColliderGameObjects", Private).SetValue(locomotion, Array.Empty<GameObject>());
        typeof(CharacterLocomotion).GetField("m_IgnoredColliderCount", Private).SetValue(locomotion, 0);
        typeof(CharacterLocomotion).GetField("m_CollisionLayerEnabled", Private).SetValue(locomotion, true);
        typeof(LitOpsiveLocomotionBridge).GetField("locomotion", Private).SetValue(bridge, locomotion);
        return bridge;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CapturedTargetBypassRestoresOnlyItsPairsAndPreservesPreexistingIgnores(bool alreadyIgnored)
    {
        var root = new GameObject("Network target collision fixture");
        var first = new GameObject("Captured target"); var second = new GameObject("Other target"); var wall = new GameObject("Wall");
        LitOpsiveLocomotionBridge.TargetCollisionHandle firstScope = null, secondScope = null;
        try
        {
            var bridge = Bridge(root, out var locomotion, out var body);
            var a = first.AddComponent<BoxCollider>(); var b = second.AddComponent<BoxCollider>(); var obstacle = wall.AddComponent<BoxCollider>();
            Physics.IgnoreCollision(body, obstacle, true);
            if (alreadyIgnored) { Physics.IgnoreCollision(body, a, true); locomotion.AddIgnoredCollider(a); }
            Assert.That(bridge.TryAcquireTargetCollisionBypass(this, first.transform, out firstScope), Is.True);
            Assert.That(Physics.GetIgnoreCollision(body, a), Is.True);
            Assert.That(Physics.GetIgnoreCollision(body, b), Is.False);
            Assert.That(bridge.TryAcquireTargetCollisionBypass(this, second.transform, out secondScope), Is.True);
            firstScope.Dispose(); firstScope.Dispose();
            Assert.That(Physics.GetIgnoreCollision(body, a), Is.EqualTo(alreadyIgnored));
            Assert.That(Physics.GetIgnoreCollision(body, b), Is.True, "Another active handle must remain intact.");
            Assert.That(Physics.GetIgnoreCollision(body, obstacle), Is.True, "Unrelated preexisting pairs must remain intact.");
            secondScope.Dispose();
            Assert.That(Physics.GetIgnoreCollision(body, b), Is.False);
            Assert.That(locomotion.IgnoredColliderCount, Is.EqualTo(alreadyIgnored ? 1 : 0));
            Assert.That(bridge.TargetCollisionBypassCount, Is.Zero);
        }
        finally
        {
            firstScope?.Dispose(); secondScope?.Dispose();
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second); UnityEngine.Object.DestroyImmediate(wall);
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void SharedFreePathIsTenMetresAndStopsAtWallOrVoid(bool blocked, bool edge)
    {
        var root = new GameObject("Network safe route fixture");
        var floor = new GameObject("Route ground"); var wall = new GameObject("Route wall");
        try
        {
            root.transform.position = new Vector3(1000, 0, 1000);
            Bridge(root, out _, out _);
            var ground = floor.AddComponent<BoxCollider>();
            floor.transform.position = root.transform.position + new Vector3(0, -.5f, edge ? 0 : 5);
            ground.size = new Vector3(20, 1, edge ? 6 : 30);
            var obstacle = wall.AddComponent<BoxCollider>();
            wall.transform.position = root.transform.position + Vector3.forward * 4 + Vector3.up;
            obstacle.size = new Vector3(5, 2, 1); obstacle.enabled = blocked;
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            typeof(PlayerActionPresentationController).GetField("locomotionBridge", Private).SetValue(presentation, root.GetComponent<LitOpsiveLocomotionBridge>());
            Physics.SyncTransforms();
            float travel = presentation.GetSafePlanarTravel(Vector3.forward, 10);
            Assert.That(travel, Is.GreaterThan(0));
            Assert.That(travel, blocked || edge ? Is.LessThan(3.5f) : Is.EqualTo(10).Within(.001f));
            Assert.That(presentation.GetSafePlanarTravel(Vector3.forward, float.NaN), Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(floor); UnityEngine.Object.DestroyImmediate(wall); }
    }

    [Test]
    public void ExternalTerminationReleasesItsCapturedHandleButAStaleEndCannotReleaseTheNextSession()
    {
        var root = new GameObject("Network contact termination fixture");
        var target = new GameObject("Captured termination target");
        LitOpsiveLocomotionBridge.TargetCollisionHandle previous = null, next = null;
        try
        {
            var bridge = Bridge(root, out _, out _); target.AddComponent<BoxCollider>();
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            int first = presentation.BeginExternalPresentation(presentation);
            Assert.That(bridge.TryAcquireTargetCollisionBypass(presentation, target.transform, out previous), Is.True);
            typeof(PlayerActionPresentationController).GetField("contactCollisionHandle", Private).SetValue(presentation, previous);
            typeof(PlayerActionPresentationController).GetField("externalContactResolver", Private).SetValue(presentation,
                new Func<Transform, Vector3, Vector3, bool>((_, __, ___) => true));
            presentation.EndExternalPresentation(first);
            Assert.That(previous.IsReleased, Is.True);
            Assert.That(typeof(PlayerActionPresentationController).GetField("externalContactResolver", Private).GetValue(presentation), Is.Null);
            int second = presentation.BeginExternalPresentation(presentation);
            Assert.That(bridge.TryAcquireTargetCollisionBypass(presentation, target.transform, out next), Is.True);
            typeof(PlayerActionPresentationController).GetField("contactCollisionHandle", Private).SetValue(presentation, next);
            presentation.EndExternalPresentation(first);
            Assert.That(next.IsReleased, Is.False);
            presentation.EndExternalPresentation(second);
            Assert.That(next.IsReleased, Is.True);
            Assert.That(bridge.TargetCollisionBypassCount, Is.Zero);
        }
        finally { previous?.Dispose(); next?.Dispose(); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(target); }
    }
}
