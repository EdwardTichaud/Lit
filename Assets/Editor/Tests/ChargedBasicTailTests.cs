#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class ChargedBasicTailTests
{
    [Test]
    public void ChargingCannotBeInterruptedByDamageOrDirectAnimationRequests()
    {
        var root = new GameObject("Protected charging fixture");
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(GroundImpactSkillSetup.ControllerPath);
        var action = root.AddComponent<PlayerActionPresentationController>();
        action.ResolveReferences(animator, null);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(PlayerActionPresentationController);
        System.Action<string, object> set = (name, value) => type.GetField(name, flags).SetValue(action, value);
        var basic = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_0.asset");
        try
        {
            set("activeToken", 10); set("actionActive", true); set("activeActionIsBasic", true);
            set("activePresentedSkill", basic); set("chargeTailGeneration", 10); set("chargeTailEntered", true);
            set("chargeTailSkill", AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SkillPath));
            Assert.That(action.CanPlayState(basic.AnimatorState), Is.True, "The requested animation must exist.");
            Assert.That(action.IsChargingSkill, Is.True);
            action.InterruptBasicSkillForDamage();
            Assert.That(action.IsCurrentSession(10), Is.True, "Direct damage interruption must not cancel charging.");
            Assert.That(action.TryPlaySkill(basic, Animator.StringToHash(basic.AnimatorState)), Is.False);
            Assert.That(action.LastRequestWasBuffered, Is.False);
            Assert.That(action.PendingBasicForCharge, Is.Null);
            set("recoveryOpen", true);
            Assert.That(action.TryPlayCombatState(basic.AnimatorState, basic.Presentation, "Ordinary replacement"), Is.False);
            Assert.That(action.TryReplaceWithDamageReaction(basic.AnimatorState, basic.Presentation), Is.False);
            Assert.That(action.CancelActionForMobility(), Is.False);
            Assert.That(action.IsCurrentSession(10), Is.True);
            action.ReleaseChargedBasicTail(10);
            Assert.That(action.IsChargingSkill, Is.False);
            action.InterruptBasicSkillForDamage();
            Assert.That(action.IsCurrentSession(10), Is.False, "Normal interruption is restored after release.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void TailStartsAfterEveryGameplayEventAndKeepsUsefulAnimation(int index)
    {
        var skill = AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SkillPath);
        var basic = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_" + index + ".asset");
        Assert.That(PlayerActionPresentationController.TryResolveChargeTailStart(basic, skill, out float start), Is.True);
        Assert.That(start, Is.GreaterThanOrEqualTo(.7f).And.LessThanOrEqualTo(.98f));
        foreach (var e in basic.AnimationClip.events.Where(e => e.functionName != "ShowSword" && e.functionName != "HideSwordWhenComboEnds"))
            Assert.That(start * basic.AnimationClip.length, Is.GreaterThan(e.time));
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GroundImpactSkillSetup.ControllerPath);
        var state = controller.layers[0].stateMachine.states.Single(s => "Base Layer." + s.state.name == basic.AnimatorState).state;
        Assert.That(state.speedParameterActive, Is.True);
        Assert.That(state.speedParameter, Is.EqualTo(PlayerActionPresentationController.ChargeTailSpeedParameter));
        Assert.That(controller.parameters.Single(p => p.name == state.speedParameter).defaultFloat, Is.EqualTo(1));
        Assert.That(controller.layers[0].stateMachine.states.Where(s => s.state.name.StartsWith("AirBasicSkill_") || s.state.name == "ChargedAscension")
            .Any(s => s.state.speedParameterActive && s.state.speedParameter == state.speedParameter), Is.False);
    }

    [Test]
    public void LateGameplayEventRejectsChargeButCosmeticSwordHideDoesNot()
    {
        var basic = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_0.asset"));
        var clip = Object.Instantiate(basic.AnimationClip);
        var skill = ScriptableObject.CreateInstance<ChargedSkillSO>(); basic.animationClip = clip;
        try
        {
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = clip.length * .995f, functionName = "ResolveSkillImpact" } });
            Assert.That(PlayerActionPresentationController.TryResolveChargeTailStart(basic, skill, out _), Is.False);
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = clip.length * .995f, functionName = "HideSwordWhenComboEnds" } });
            Assert.That(PlayerActionPresentationController.TryResolveChargeTailStart(basic, skill, out _), Is.True);
        }
        finally { Object.DestroyImmediate(basic); Object.DestroyImmediate(clip); Object.DestroyImmediate(skill); }
    }

    [Test]
    public void ValidatedTailIsPromotedWithoutChangingDefaultsAndLabReinstallIsIdempotent()
    {
        var lab = AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SkillPath);
        var production = AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SourceSkillPath);
        Assert.That(lab.preparationMode, Is.EqualTo(ChargedPreparationMode.BasicTail));
        Assert.That(production.preparationMode, Is.EqualTo(ChargedPreparationMode.BasicTail));
        Assert.That(production, Is.Not.SameAs(lab));
        var defaults = ScriptableObject.CreateInstance<ChargedSkillSO>();
        try { Assert.That(defaults.preparationMode, Is.EqualTo(ChargedPreparationMode.HeldPose)); }
        finally { Object.DestroyImmediate(defaults); }
        Assert.That(lab.chargeTailPlaybackRate, Is.EqualTo(.1f));
        Assert.That(lab.chargeTailBlendSeconds, Is.EqualTo(.08f));
        var paths = new[] { ChargedSkillSetup.SkillPath, GroundImpactSkillSetup.ControllerPath, ChargedSkillSetup.SourceSkillPath };
        var before = paths.Select(System.IO.File.ReadAllBytes).ToArray();
        ChargedSkillSetup.InstallBasicTailPrototype();
        for (int i = 0; i < paths.Length; i++) Assert.That(System.IO.File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i]);
    }

    [Test]
    public void CleanupIsIdempotentAndOldGenerationCannotReleaseNewCharge()
    {
        var root = new GameObject("Charge tail cleanup fixture"); root.SetActive(false);
        var action = root.AddComponent<PlayerActionPresentationController>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(PlayerActionPresentationController);
        int releases = 0;
        System.Action<int> setGeneration = value => {
            type.GetField("activeToken", flags).SetValue(action, value);
            type.GetField("actionActive", flags).SetValue(action, true);
            type.GetField("chargeTailGeneration", flags).SetValue(action, value);
            type.GetField("chargeTailRate", flags).SetValue(action, .1f);
            type.GetField("chargeTailCameraRelease", flags).SetValue(action, (System.Action)(() => releases++));
        };
        var reset = type.GetMethod("ResetChargedBasicTail", flags);
        try
        {
            setGeneration(21);
            action.RegisterActionCleanup(21, () => reset.Invoke(action, new object[] { 21 }));
            action.CancelAction(); action.CancelAction();
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(type.GetField("chargeTailRate", flags).GetValue(action), Is.EqualTo(1f));
            setGeneration(23);
            reset.Invoke(action, new object[] { 21 });
            action.ReleaseChargedBasicTail(21);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(type.GetField("chargeTailRate", flags).GetValue(action), Is.EqualTo(.1f));
            Assert.That(action.IsCurrentSession(23), Is.True);
            reset.Invoke(action, new object[] { 23 });
            reset.Invoke(action, new object[] { 23 });
            Assert.That(releases, Is.EqualTo(2));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void BufferedHoldBelongsToDestinationBasicAndNotOutgoingGeneration()
    {
        var root = new GameObject("Buffered charge owner");
        var inputRoot = new GameObject("Buffered charge input"); inputRoot.SetActive(false);
        var managerRoot = new GameObject("Buffered charge manager"); managerRoot.SetActive(false);
        var manager = managerRoot.AddComponent<RealTimeCombatManager>();
        var owner = root.AddComponent<PlayerActionPresentationController>();
        var input = inputRoot.AddComponent<RealTimeCombatInput>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        System.Action<object, string, object> set = (target, name, value) => target.GetType().GetField(name, flags).SetValue(target, value);
        var first = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_0.asset");
        var next = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_1.asset");
        var singleton = typeof(RealTimeCombatManager).GetField("<Instance>k__BackingField", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var previousManager = singleton.GetValue(null);
        try
        {
            set(owner, "actionActive", true); set(owner, "activeActionIsBasic", true);
            set(owner, "activeToken", 20); set(owner, "activePresentedSkill", first);
            set(owner, "hasBufferedAction", true); set(owner, "bufferedBasicSkill", next);
            set(input, "chargePresentation", owner); set(input, "basicAttackHeld", true);
            set(input, "heldChargedSkill", AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SkillPath));
            set(input, "pendingChargeBasic", next); set(input, "basicChargeGeneration", 20);
            Assert.That(input.IsChargingSkillFor(owner), Is.True);
            singleton.SetValue(null, manager); set(manager, "playerRoot", root.transform);
            input.CancelBufferedBasicSkills(preserveChargedHold: true);
            Assert.That(owner.PendingBasicForCharge, Is.SameAs(next), "A refused defense or damage must not remove the Basic that owns the charge.");
            Assert.That(input.IsChargingSkillFor(owner), Is.True);
            set(owner, "hasBufferedAction", false);
            Assert.That(input.IsChargingSkillFor(owner), Is.False, "The outgoing Basic cannot inherit a removed buffer.");
            set(owner, "activePresentedSkill", next); set(owner, "activeToken", 22);
            Assert.That(input.IsChargingSkillFor(owner), Is.True, "A new generation of the actual buffered Basic takes over.");
            set(owner, "activeToken", 20);
            Assert.That(input.IsChargingSkillFor(owner), Is.False, "The original generation is not the buffered action.");
        }
        finally
        {
            singleton.SetValue(null, previousManager);
            set(owner, "actionActive", false);
            Object.DestroyImmediate(root); Object.DestroyImmediate(inputRoot); Object.DestroyImmediate(managerRoot);
        }
    }
}
#endif
