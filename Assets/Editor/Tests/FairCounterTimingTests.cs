#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

public sealed class FairCounterTimingTests
{
    [TestCase(.79999, false)]
    [TestCase(.8, true)]
    [TestCase(.99999, true)]
    [TestCase(1, false)]
    [TestCase(1.00001, false)]
    public void WindowIsInclusiveAtOpeningExclusiveAtContact(double phase, bool expected) =>
        Assert.That(EnemyReactionTiming.Contains(phase, 1, .2), Is.EqualTo(expected));

    [TestCase(30, 1)] [TestCase(60, 1)] [TestCase(120, 1)]
    [TestCase(30, .4)] [TestCase(60, .4)] [TestCase(120, .4)]
    public void InputProjectionUsesScaledAnimationNotARealtimeDeadline(int fps, double scale)
    {
        double frame = 1d / fps;
        double phase = EnemyReactionTiming.ProjectPhase(.9 - frame * scale, frame * scale, 1, .005, scale);
        Assert.That(phase, Is.EqualTo(.9 - .005 * scale).Within(.000001));
        Assert.That(EnemyReactionTiming.Contains(phase, 1, .2), Is.True);
        Assert.That(.2 / scale, Is.EqualTo(scale == 1 ? .2 : .5).Within(.000001));
        Assert.That(EnemyReactionTiming.Contains(EnemyReactionTiming.ProjectPhase(1, 0, 1, 0, scale), 1, .2), Is.False,
            "Processing contact before a later press never grants a retrospective counter.");
    }

    [Test]
    public void PauseAndActorSpeedAreAppliedOnce()
    {
        Assert.That(EnemyReactionTiming.ProjectPhase(.9, 0, 1, 0, 0), Is.EqualTo(.9));
        Assert.That(EnemyReactionTiming.ProjectPhase(.8, .1, .5, 0, 1), Is.EqualTo(.85).Within(.000001));
        Assert.That(EnemyReactionTiming.Contains(double.NaN, 1, .2), Is.False);
    }

    [Test]
    public void AuthoredProfilesKeepOriginalImpactsAndArmBeforeCounterWindow()
    {
        EnemyCounterTimingValidation.Validate(AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(EnemyCounterTimingValidation.LabProfilePath));
        EnemyCounterTimingValidation.Validate(AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(LucianBrainsCombatSetup.FluidProfilePath));
    }

    [Test]
    public void InvalidOrDuplicateImpactMarkersAreRejected()
    {
        var clip = MakeClip();
        try
        {
            Assert.That(EnemyReactionTiming.ValidateImpact(clip, 1, out _), Is.True);
            Assert.That(EnemyReactionTiming.ValidateImpact(clip, .9f, out _), Is.False);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            Assert.That(EnemyReactionTiming.ValidateImpact(clip, 1, out _), Is.False);
            AnimationUtility.SetAnimationEvents(clip, new[] { Impact(), Impact() });
            Assert.That(EnemyReactionTiming.ValidateImpact(clip, 1, out _), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(clip); }
    }

    [Test]
    public void IncomingAttackOwnsWindowAndOutgoingAttackLosesIt()
    {
        var root = new GameObject("Fair counter animator fixture");
        var clip = MakeClip();
        var idle = new AnimationClip { name = "Idle fixture" };
        idle.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 2, 0));
        var controller = new AnimatorController();
        controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        machine.AddState("Idle").motion = idle;
        machine.AddState("Attack").motion = clip;
        try
        {
            var animator = root.AddComponent<Animator>();
            animator.fireEvents = false;
            animator.runtimeAnimatorController = controller;
            animator.Rebind(); animator.Update(0);
            animator.CrossFadeInFixedTime("Attack", .2f, 0, .85f);
            animator.Update(.01f);
            var timing = new EnemyReactionTiming(animator, clip, "Attack", 1, 7);
            Assert.That(timing.IsValid, Is.True);
            Assert.That(timing.TryGetPhase(InputState.currentTime, out double phase), Is.True);
            Assert.That(EnemyReactionTiming.Contains(phase, 1, .2), Is.True);
            animator.CrossFadeInFixedTime("Idle", .2f, 0, 0);
            animator.Update(.01f);
            Assert.That(timing.TryGetPhase(InputState.currentTime, out _), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(controller);
            UnityEngine.Object.DestroyImmediate(clip);
            UnityEngine.Object.DestroyImmediate(idle);
        }
    }

    [Test]
    public void CounterCooldownStillDoesNotExtendAndDoesNotConsumeDodge()
    {
        new EnemyAggroRegressionTests().CounterRetryDeadlineIsNotExtendedBySpamOrNewOpportunity();
        new EnemyAggroRegressionTests().CounterSpamConsumesPressBeforeGuardFallbackButLeavesDodgeAvailable();
    }

    [Test]
    public void ExpiredDodgeDoesNotCloseAnimationCounterAndNewGenerationClearsIt()
    {
        var actor = new GameObject("Counter timing owner");
        var services = new GameObject("Inactive reaction fixture"); services.SetActive(false);
        var victim = new GameObject("Counter victim");
        var clip = MakeClip();
        var controller = new AnimatorController(); controller.AddLayer("Base Layer");
        controller.layers[0].stateMachine.AddState("Attack").motion = clip;
        try
        {
            var animator = actor.AddComponent<Animator>(); animator.fireEvents = false;
            animator.runtimeAnimatorController = controller;
            animator.Rebind(); animator.Play("Attack", 0, .45f); animator.Update(0);
            var timing = new EnemyReactionTiming(animator, clip, "Attack", 1, 7);
            var target = new Target(actor.transform, animator);
            var manager = services.AddComponent<RealTimeCombatManager>();
            var reactions = services.AddComponent<CombatHealthThresholdController>();
            Set(manager, "externalTarget", target); Set(manager, "combatActive", true); Set(manager, "playerRoot", victim.transform);
            Set(reactions, "combatManager", manager); Set(reactions, "externalReactionTarget", target);
            Set(reactions, "attackReactionVictim", victim.transform); Set(reactions, "externalCounterTiming", timing);
            Set(reactions, "attackQteActionId", 7); Set(reactions, "attackQteActive", true);
            Set(reactions, "animationCounterActive", true); Set(reactions, "dodgeDeadline", -1d);
            Set(reactions, "reactionTimeScale", 1f);
            Invoke(reactions, "Update");
            Assert.That(Get(reactions, "attackQteActive"), Is.False);
            Assert.That(Get(reactions, "animationCounterActive"), Is.True);
            reactions.CloseExternalReactionAtContact(target);
            Assert.That(Get(reactions, "animationCounterActive"), Is.False);
            Set(reactions, "animationCounterActive", true);
            target.ActionSequenceId++;
            Invoke(reactions, "Update");
            Assert.That(Get(reactions, "externalReactionTarget"), Is.Null);
            Assert.That(Get(reactions, "animationCounterActive"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(services); UnityEngine.Object.DestroyImmediate(victim);
            UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(controller); UnityEngine.Object.DestroyImmediate(clip);
        }
    }

    private sealed class Target : ICombatTarget
    {
        public Transform Root { get; }
        public Transform LockPoint => Root;
        public Animator Animator { get; }
        public bool IsDead => false;
        public bool IsAttackCommitted => true;
        public int ActionSequenceId { get; set; } = 7;
        public Target(Transform root, Animator animator) { Root = root; Animator = animator; }
        public int ReceiveDamage(int damage) => damage;
        public void SetCinematicSuspended(bool suspended) { }
        public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => false;
    }
    private const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
    private static object Get(object owner, string name) => owner.GetType().GetField(name, Flags).GetValue(owner);
    private static void Invoke(object owner, string name) => owner.GetType().GetMethod(name, Flags).Invoke(owner, null);

    private static AnimationEvent Impact() => new AnimationEvent { functionName = "ResolveBrainsAttackImpact", time = 1 };
    private static AnimationClip MakeClip()
    {
        var clip = new AnimationClip { name = "Counter timing fixture" };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 2, 0));
        AnimationUtility.SetAnimationEvents(clip, new[] { Impact() });
        return clip;
    }
}
#endif
