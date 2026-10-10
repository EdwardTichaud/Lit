#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class AnimationEventsTests
{
    [Test]
    public void EditorSignalsOnlyRecordPreviewAndNeverDeactivateTheirTarget()
    {
        var go = new GameObject("Preview", typeof(AnimationEvents));
        try
        {
            var events = go.GetComponent<AnimationEvents>();
            events.SetActive(false);
            events.KnockedOut();
            events.ResolveDamage();
            Assert.IsTrue(go.activeSelf);
            Assert.AreEqual("ResolveDamage", events.LastSignal);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void EventReceiverRetainsOriginalPlayerScriptGuid()
    {
        var path = AssetDatabase.GUIDToAssetPath("95818c943a004c47a64b9f42e568f020");
        Assert.That(path, Does.EndWith("/AnimationEvents.cs"));
        Assert.AreEqual(typeof(AnimationEvents), AssetDatabase.LoadAssetAtPath<MonoScript>(path).GetClass());
    }

    [Test]
    public void NativeCounterSignalsTargetTheCentralReceiver()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CounterKnockoutSignalSetup.RigPath);
        var signals = prefab.GetComponent<SignalReceiver>();
        foreach (var signal in signals.GetRegisteredSignals())
        {
            var reaction = signals.GetReaction(signal);
            Assert.IsTrue(reaction.GetPersistentEventCount() > 0);
            for (int i = 0; i < reaction.GetPersistentEventCount(); i++)
                Assert.IsInstanceOf<AnimationEvents>(reaction.GetPersistentTarget(i));
        }
    }

    [Test]
    public void MigratedServicesCannotReceiveClipCallbacksTwice()
    {
        Assert.IsFalse(typeof(EnemyController).GetMethods().Any(m => m.Name == "EnemyAttack" || m.Name == "QTE"));
        Assert.IsFalse(typeof(LitBrainsEnemy).GetMethods().Any(m => m.Name == "ResolveBrainsAttackImpact" || m.Name == "KnockedOut"));
        Assert.IsFalse(typeof(SpiritBondAnimationActions).GetMethods().Any(m => m.Name == "PlayEffect_CharacterEffect"));
        Assert.IsFalse(typeof(LocomotionAnimationEvent).GetMethods().Any(m => m.Name == "PlayFootstepLeft"));
    }

    [Test]
    public void EnsureReceiverIsIdempotentAndDirectorsAreValidated()
    {
        var go = new GameObject("Director", typeof(PlayableDirector));
        try
        {
            var first = AnimationEvents.EnsureOn(go);
            Assert.AreSame(first, AnimationEvents.EnsureOn(go));
            var errors = new List<string>();
            AnimationEventsAuthoring.ValidateHierarchy(go, "Test", errors);
            Assert.IsEmpty(errors);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void CharacterAndCinematicPrefabsHaveCompleteReceiverBindings()
    {
        var paths = new[] {
            "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab",
            "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab",
            "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.prefab",
            "Assets/Characters/9_Ghosts/Luc/Enemy_Model_MadScientist.prefab",
            "Assets/Scenes/Cycles/Cycle_Belmont/Prefabs/ShadowGuardian.prefab",
            IluviliraeSetup.PrefabPath, JuggernautV2Setup.PrefabPath, CounterKnockoutSignalSetup.RigPath,
            "Assets/CombatRealTime/LightSkills/AnimationLab.prefab",
            "Assets/CombatRealTime/LightSkills/1_Devastation/LightSkill_1_Devastation_CinematicRig.prefab",
            "Assets/CombatRealTime/LightSkills/2_Furie/LightSkill_2_Furie_CinematicRig.prefab",
            "Assets/CombatRealTime/LightSkills/3_Enchainement/LightSkill_3_Enchainement_CinematicRig.prefab" };
        var errors = new List<string>();
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            AnimationEventsAuthoring.ValidateHierarchy(prefab, path, errors);
        }
        Assert.IsEmpty(errors, string.Join("\n", errors));
    }
}
#endif
