using System;
using System.Reflection;
using Lit.Combat.Migration.Network;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatNetworkExecutionCompletionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase("Jump")]
    [TestCase("LocomotionMode")]
    [TestCase("TriggerMunin")]
    [TestCase("ToggleTorch")]
    public void RoutedNetworkCallbacksCannotShadowUnityInputValueMessages(string action)
    {
        var type = typeof(NetworkCharacterInput);
        Assert.That(type.GetMethod("On" + action, Private | BindingFlags.Public), Is.Null,
            "PlayerInput.SendMessages must not attempt to invoke a CallbackContext receiver with InputValue.");
        var callback = type.GetMethod("OnRouted" + action, Private);
        Assert.That(callback, Is.Not.Null);
        Assert.That(callback.GetParameters(), Has.Length.EqualTo(1));
        Assert.That(callback.GetParameters()[0].ParameterType, Is.EqualTo(typeof(UnityEngine.InputSystem.InputAction.CallbackContext)));
    }

    [TestCase("Lucian")]
    [TestCase("Aurell")]
    [TestCase("Vesta")]
    [TestCase("Soren")]
    public void ProductionActorKeepsTheAuthoredNoSlowdownReactionWindows(string character)
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/" + character + "/" + character + ".asset");
        Assert.That(data, Is.Not.Null);
        var runtime = data.ResolveWorldPrefab().GetComponent<CombatNetworkActorRuntime>();
        Assert.That(runtime, Is.Not.Null);
        var serialized = new SerializedObject(runtime);
        Assert.That(serialized.FindProperty("networkCounterPreImpactSeconds").floatValue, Is.EqualTo(.5f),
            "Without multiplayer slowdown the authored pre-impact interval is 0.5 animation seconds, not the old 0.2 default.");
        Assert.That(serialized.FindProperty("networkDodgeWindowSeconds").floatValue, Is.EqualTo(.5f));
        Assert.That(serialized.FindProperty("networkCounterRetrySeconds").floatValue, Is.EqualTo(.4f));
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static CombatCommandContext Context(CombatActionTicket ticket) => (CombatCommandContext)Activator.CreateInstance(
        typeof(CombatCommandContext), Private, null, new object[] { ticket, null, null, null, 1, Vector3.forward }, null);

    private static AnimationClip Clip()
    {
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
        AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = "HitEnemy", time = .5f } });
        return clip;
    }

    [TestCase("Assets/CombatRealTime/Skills/AirBasicSkill_1.asset", CombatNetworkSkillFamily.AirBasic)]
    [TestCase("Assets/Resources/CombatSupportSkills/Skill_Sommation.asset", CombatNetworkSkillFamily.Sommation)]
    [TestCase("Assets/Resources/CombatSupportSkills/Skill_JusteRetour.asset", CombatNetworkSkillFamily.Justice)]
    [TestCase("Assets/Resources/CombatControlSkills/Skill_PasDeRecul.asset", CombatNetworkSkillFamily.PasDeRecul)]
    public void RealAuthoredAssetsMeetTheConcreteExecutionContract(string path, CombatNetworkSkillFamily expected)
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(path);
        Assert.That(skill, Is.Not.Null, path);
        Assert.That(CombatNetworkSkillCatalog.Classify(skill, out string diagnostic), Is.EqualTo(expected), diagnostic);
    }

    [TestCase(AirborneBasicSkillLandingMode.StayAirborne)]
    [TestCase(AirborneBasicSkillLandingMode.LandOnApproach)]
    public void AirBasicRequiresNativeMarkerForBothMotorContracts(AirborneBasicSkillLandingMode mode)
    {
        var skill = ScriptableObject.CreateInstance<BasicSkillsSO>();
        var clip = Clip();
        try
        {
            Set(skill, "context", BasicSkillContext.Airborne);
            Set(skill, "airborneLandingMode", mode);
            skill.animationClip = clip; skill.animatorState = "Base Layer.AirBasic";
            skill.targetingMode = SkillTargetingMode.FreeAttack;
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out string diagnostic), Is.EqualTo(CombatNetworkSkillFamily.AirBasic), diagnostic);
            Assert.That(skill.HoldsAirborneDuringAnimation, Is.EqualTo(mode == AirborneBasicSkillLandingMode.StayAirborne));
            Assert.That(skill.RequestsLandingDuringAnimation, Is.EqualTo(mode == AirborneBasicSkillLandingMode.LandOnApproach));
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(clip); }
    }

    [Test]
    public void JusticeRequiresExplicitEnemyAndNativeImpactNotTimer()
    {
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        var clip = Clip();
        try
        {
            skill.protection.kind = SkillProtectionKind.JusteRetour;
            skill.targetingMode = SkillTargetingMode.RequiredTarget;
            skill.animationClip = clip; skill.animatorState = "Base Layer.Justice";
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out string diagnostic), Is.EqualTo(CombatNetworkSkillFamily.Justice), diagnostic);
            skill.targetingMode = SkillTargetingMode.LockedTarget;
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
            skill.targetingMode = SkillTargetingMode.RequiredTarget;
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(clip); }
    }

    [Test]
    public void TauntAndRetreatRejectMismatchedOrNonfiniteContracts()
    {
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        var clip = Clip();
        try
        {
            skill.protection.kind = SkillProtectionKind.Sommation; skill.targetingMode = SkillTargetingMode.CasterArea;
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Sommation));
            skill.targetingMode = SkillTargetingMode.RequiredTarget;
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
            skill.protection.kind = SkillProtectionKind.None; skill.control.kind = SkillControlKind.PasDeRecul;
            skill.targetingMode = SkillTargetingMode.FreeAttack; skill.animationClip = clip; skill.animatorState = "Base Layer.Retreat";
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.PasDeRecul));
            skill.control.distance = float.NaN;
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(clip); }
    }

    [TestCase(CombatActionEndReason.Completed, true)]
    [TestCase(CombatActionEndReason.Cancelled, false)]
    [TestCase(CombatActionEndReason.Death, false)]
    [TestCase(CombatActionEndReason.SceneChanged, false)]
    [TestCase(CombatActionEndReason.Expired, false)]
    [TestCase(CombatActionEndReason.OwnershipChanged, false)]
    [TestCase(CombatActionEndReason.Disconnect, false)]
    public void BufferDrainsOnceOnlyForMatchingCompletedSource(CombatActionEndReason reason, bool expected)
    {
        var root = new GameObject("Network buffer cleanup fixture");
        try
        {
            var runtime = root.AddComponent<CombatNetworkActorRuntime>();
            var ticket = new CombatActionTicket(17, 20, 3);
            var context = Context(ticket);
            Set(runtime, "active", context); Set(runtime, "hasAction", true);
            Set(runtime, "bufferedBasicAt", Time.unscaledTimeAsDouble);
            int calls = 0; bool dispatched = !expected;
            Set(runtime, "bufferedBasic", new Action<bool>(accepted => { calls++; dispatched = accepted; }));
            runtime.End(Context(new CombatActionTicket(17, 20, 2)), reason);
            Assert.That(calls, Is.Zero, "Stale generation cannot drain another session's buffer.");
            runtime.End(context, reason);
            runtime.End(context, reason);
            Assert.That(calls, Is.EqualTo(1)); Assert.That(dispatched, Is.EqualTo(expected));
            Assert.That(typeof(CombatNetworkActorRuntime).GetField("bufferedBasic", Private).GetValue(runtime), Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void CompletedSourceCannotDispatchAnExpiredEarlyPress()
    {
        var root = new GameObject("Network buffer expiry fixture");
        try
        {
            var runtime = root.AddComponent<CombatNetworkActorRuntime>();
            var context = Context(new CombatActionTicket(17, 20, 3));
            Set(runtime, "active", context); Set(runtime, "hasAction", true);
            Set(runtime, "bufferedBasicAt", Time.unscaledTimeAsDouble - 2);
            bool dispatched = true;
            Set(runtime, "bufferedBasic", new Action<bool>(accepted => dispatched = accepted));
            runtime.End(context, CombatActionEndReason.Completed);
            Assert.That(dispatched, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void OldCompletionCannotClearTheNextGenerationStartedByItsBuffer()
    {
        var root = new GameObject("Network generation handoff fixture");
        try
        {
            var runtime = root.AddComponent<CombatNetworkActorRuntime>();
            var before = Context(new CombatActionTicket(17, 20, 3));
            var after = Context(new CombatActionTicket(17, 20, 4));
            Set(runtime, "active", before); Set(runtime, "hasAction", true);
            Set(runtime, "bufferedBasicAt", Time.unscaledTimeAsDouble);
            Set(runtime, "bufferedBasic", new Action<bool>(accepted =>
            {
                Assert.That(accepted, Is.True);
                Set(runtime, "active", after); Set(runtime, "hasAction", true);
            }));
            runtime.End(before, CombatActionEndReason.Completed);
            runtime.End(before, CombatActionEndReason.Cancelled);
            Assert.That(typeof(CombatNetworkActorRuntime).GetField("hasAction", Private).GetValue(runtime), Is.True);
            var current = (CombatCommandContext)typeof(CombatNetworkActorRuntime).GetField("active", Private).GetValue(runtime);
            Assert.That(current.Ticket.Generation, Is.EqualTo(4));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void TauntCleanupCannotEraseReplacementGeneration()
    {
        var root = new GameObject("Network taunt ownership fixture");
        try
        {
            var taunt = root.AddComponent<EnemyTaunt>();
            // A fixture need not create UI to exercise the same owner/generation condition used by live taunts.
            typeof(EnemyTaunt).GetProperty("Generation").GetSetMethod(true).Invoke(taunt, new object[] { 9UL });
            taunt.ClearIfOwner(null, 8);
            Assert.That(taunt.Generation, Is.EqualTo(9));
            taunt.ClearIfOwner(null, 9);
            Assert.That(taunt.Generation, Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void UnspawnedActorCannotBufferOrConsumeFabricatedNativeEvent()
    {
        var root = new GameObject("Network source validation fixture");
        try
        {
            var runtime = root.AddComponent<CombatNetworkActorRuntime>();
            var buffer = typeof(CombatNetworkActorRuntime).GetMethod("TryBufferBasicCommand", Private);
            Assert.That(buffer.Invoke(runtime, new object[] { 0UL, 1UL, 1, Vector3.forward, new Action<bool>(_ => Assert.Fail("Untrusted buffer dispatched")) }), Is.False);
            typeof(CombatNetworkActorRuntime).GetMethod("OnAnimationImpact", Private).Invoke(runtime,
                new object[] { new AnimationEvent { functionName = "HitEnemy", time = .5f } });
            Assert.That(typeof(CombatNetworkActorRuntime).GetField("basicImpactResolved", Private).GetValue(runtime), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
