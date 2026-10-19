#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using Lit.Combat.Migration.Network;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CombatNetworkBasicTailChargeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private PlayerActionPresentationController presentation;
    private ChargedSkillSO charged;
    private BasicSkillsSO basic;

    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Network BasicTail session");
        presentation = root.AddComponent<PlayerActionPresentationController>();
        charged = ScriptableObject.CreateInstance<ChargedSkillSO>();
        charged.preparationMode = ChargedPreparationMode.BasicTail;
        basic = ScriptableObject.CreateInstance<BasicSkillsSO>();
        Set(presentation, "activeToken", 42);
        Set(presentation, "actionActive", true);
        Set(presentation, "externallyDriven", true);
        Set(presentation, "activeActionIsBasic", true);
        Set(presentation, "activePresentedSkill", basic);
        Set(presentation, "chargeTailGeneration", 42);
        Set(presentation, "chargeTailSkill", charged);
        Set(presentation, "chargeTailEntered", false);
        presentation.RegisterActionCleanup(42, () => typeof(PlayerActionPresentationController)
            .GetMethod("ResetChargedBasicTail", Private).Invoke(presentation, new object[] { 42 }));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(charged);
        Object.DestroyImmediate(basic);
    }

    [TestCase(100d, 101.999d, 2f, false)]
    [TestCase(100d, 102d, 2f, true)]
    [TestCase(100d, 103d, 2f, true)]
    [TestCase(100d, 99d, 2f, false)]
    [TestCase(double.NaN, 102d, 2f, false)]
    [TestCase(100d, double.PositiveInfinity, 2f, false)]
    [TestCase(100d, 102d, float.NaN, false)]
    [TestCase(100d, 102d, 0f, false)]
    public void ChargeUsesRealClockAndIncludesExactTwoSecondBoundary(double pressed, double now, float seconds, bool expected)
    {
        var method = typeof(CombatNetworkActorRuntime).GetMethod("ChargeDurationReached", BindingFlags.Static | BindingFlags.NonPublic);
        float previous = Time.timeScale;
        try
        {
            Time.timeScale = .1f;
            Assert.That(method.Invoke(null, new object[] { pressed, now, seconds }), Is.EqualTo(expected));
        }
        finally { Time.timeScale = previous; }
    }

    [Test]
    public void ReplicatedTailCannotAttachToAnotherEpochOrActionGeneration()
    {
        var snapshot = new CombatNetworkChargeTailSnapshot { Epoch = 12, Generation = 7, SkillId = 4, NormalizedTime = 1f };
        Assert.That(snapshot.Matches(12, 7), Is.True);
        Assert.That(snapshot.Matches(11, 7), Is.False);
        Assert.That(snapshot.Matches(12, 8), Is.False);
        snapshot.SkillId = 0;
        Assert.That(snapshot.Matches(12, 7), Is.False);
        Assert.That(default(CombatNetworkChargeTailSnapshot).Matches(0, 0), Is.False);
    }

    [Test]
    public void AuthoritativeArmedTailProtectsBasicBeforeSlowdownStarts()
    {
        Assert.That(presentation.IsPreparingBasicTail, Is.True);
        Assert.That(presentation.IsChargingSkill, Is.True);
        Assert.That(presentation.IsDamageReactionSuppressed, Is.True);
        Assert.That(presentation.CanCancelToMobility, Is.False);
        Assert.That(presentation.CanStartAction, Is.False);
        presentation.InterruptBasicSkillForDamage();
        Assert.That(presentation.IsCurrentSession(42), Is.True);
        Assert.That(presentation.ActiveBasicForCharge, Is.SameAs(basic));
    }

    [Test]
    public void ReleaseRestoresResourcesWithoutEndingOrReplacingTheRealBasic()
    {
        int movement = 0, camera = 0;
        Set(presentation, "chargeTailMovementRelease", (Action)(() => movement++));
        Set(presentation, "chargeTailCameraRelease", (Action)(() => camera++));
        Set(presentation, "chargeTailRate", .1f);
        presentation.ReleaseChargedBasicTail(41);
        Assert.That(presentation.IsChargingSkill, Is.True);
        presentation.ReleaseChargedBasicTail(42);
        presentation.ReleaseChargedBasicTail(42);
        Assert.That(movement, Is.EqualTo(1)); Assert.That(camera, Is.EqualTo(1));
        Assert.That(presentation.IsCurrentSession(42), Is.True);
        Assert.That(presentation.ActiveBasicForCharge, Is.SameAs(basic));
        Assert.That(presentation.IsChargingSkill, Is.False);
        Assert.That(Get<bool>(presentation, "chargeTailReleased"), Is.True);
        Assert.That(Get<float>(presentation, "chargeTailRate"), Is.EqualTo(.1f), "The shared tick blends back, rather than snapping the rate.");
    }

    [Test]
    public void ExternalChargedHandoffCleansTheGenerationWithoutLocomotionHandoff()
    {
        int ended = 0, cleaned = 0;
        presentation.ActionEnded += () => ended++;
        presentation.RegisterActionCleanup(42, () => cleaned++);
        presentation.EndExternalChargedBasicTail(41);
        Assert.That(presentation.IsCurrentSession(42), Is.True);
        presentation.EndExternalChargedBasicTail(42);
        presentation.EndExternalChargedBasicTail(42);
        Assert.That(presentation.IsActionActive, Is.False);
        Assert.That(cleaned, Is.EqualTo(1));
        Assert.That(ended, Is.Zero);
        Assert.That(Get<float>(presentation, "chargeTailRate"), Is.EqualTo(1f));
        Assert.That(presentation.IsChargingSkill, Is.False);
    }

    [TestCase(CombatActionEndReason.Death)]
    [TestCase(CombatActionEndReason.Cancelled)]
    [TestCase(CombatActionEndReason.SceneChanged)]
    public void PriorityOrForcedCancellationReleasesTailAndRequiresNewPress(CombatActionEndReason reason)
    {
        var runtime = root.AddComponent<CombatNetworkActorRuntime>();
        Set(runtime, "presentation", presentation);
        Set(runtime, "presentationSession", 42);
        Set(runtime, "chargePreparation", 42);
        Set(runtime, "chargeHeld", true);
        Set(runtime, "hasAction", true);
        Set(runtime, "activeFamily", CombatNetworkSkillFamily.GroundBasic);
        var ticket = new CombatActionTicket(12, 9, 7);
        var context = (CombatCommandContext)Activator.CreateInstance(typeof(CombatCommandContext), Private, null,
            new object[] { ticket, null, null, basic, 1, Vector3.forward }, null);
        Set(runtime, "active", context);
        runtime.End(context, reason);
        Assert.That(Get<bool>(runtime, "chargeHeld"), Is.False);
        Assert.That(Get<bool>(runtime, "chargeRequiresRelease"), Is.True);
        Assert.That(presentation.IsActionActive, Is.False);
        Assert.That(presentation.IsChargingSkill, Is.False);
        Assert.That(Get<float>(presentation, "chargeTailRate"), Is.EqualTo(1f));
    }

    [Test]
    public void ExternalTickRejectsAStaleOrNonExternalSession()
    {
        Assert.That(presentation.UpdateExternalChargedBasicTail(41), Is.False);
        Set(presentation, "externallyDriven", false);
        Assert.That(presentation.UpdateExternalChargedBasicTail(42), Is.False);
        Assert.That(presentation.IsCurrentSession(42), Is.True);
    }
}
#endif
