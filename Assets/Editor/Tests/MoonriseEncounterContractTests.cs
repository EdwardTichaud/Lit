using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class MoonriseEncounterContractTests
{
    private static CharacterInfo HudHealth(ICombatTarget target) => (CharacterInfo)typeof(RealTimeCombatSceneUiController)
        .GetMethod("ResolveHudEnemyHealth", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new object[] { target, null });
    private sealed class Target : ICombatTarget
    {
        public Transform Root { get; set; }
        public Transform LockPoint => Root;
        public Animator Animator => null;
        public bool IsDead { get; set; }
        public bool IsAttackCommitted => false;
        public int ActionSequenceId => 0;
        public int ReceiveDamage(int amount) => 0;
        public void SetCinematicSuspended(bool value) { }
        public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => false;
    }

    [Test]
    public void HudReadsLiveHealthFromCommonTargetWithoutLegacyController()
    {
        var root = new GameObject("Common combat HUD target");
        try
        {
            var health = root.AddComponent<CharacterInfo>();
            health.SetHealth(300, 300);
            var target = new Target { Root = root.transform };
            Assert.That(HudHealth(target), Is.SameAs(health));
            health.ApplyDamage(37);
            Assert.That(HudHealth(target).CurrentHp, Is.EqualTo(263));
            health.ForceDefeat();
            Assert.That(HudHealth(target).CurrentHp, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void HudReleasesDestroyedCommonTargetRoot()
    {
        var root = new GameObject("Released HUD target");
        var target = new Target { Root = root.transform };
        Object.DestroyImmediate(root);
        Assert.That(HudHealth(target), Is.Null);
    }

    [Test]
    public void TacticalMaskAndNearClipProtectCommonTargetBodiesWithoutLegacyBrain()
    {
        var root = new GameObject("Common camera body target");
        var target = new Target { Root = root.transform };
        var resolve = typeof(LitGameplayCameraModeController).GetMethod("ResolveProtectedCombatRoot", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            Assert.That(root.GetComponent<EnemyController>(), Is.Null);
            Assert.That(resolve.Invoke(null, new object[] { target }), Is.SameAs(root.transform));
            target.IsDead = true;
            Assert.That(resolve.Invoke(null, new object[] { target }), Is.Null);
            target.IsDead = false;
            root.SetActive(false);
            Assert.That(resolve.Invoke(null, new object[] { target }), Is.Null);
            Object.DestroyImmediate(root);
            Assert.That(resolve.Invoke(null, new object[] { target }), Is.Null);
        }
        finally { if (root != null) Object.DestroyImmediate(root); }
    }

    [Test]
    public void EquippedProductionKitCanAnswerCloseRangeWithItsReviewedPatterns()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset");
        var brain = data.ResolveWorldPrefab().GetComponent<LitBrainsEnemy>();
        var authored = data.enemyCombatProfile.patterns.SelectMany(pattern => pattern.skills).Distinct().ToArray();
        Assert.That(authored.Where(skill => !data.combatSkills.Contains(skill)), Is.Empty,
            "Reviewed melee patterns must actually be equipped; a jump-only kit has no attack when retreat is obstructed.");
        Assert.That(data.combatSkills.Any(skill => skill.IsWithinHitRange(1)), Is.True);
        Assert.That(authored.All(skill => brain.combatProfile.attacks.Any(attack => attack.skill == skill && attack.clip != null)), Is.True);
    }

    [Test]
    public void TacticalVisibilityProtectsUnlockedEncounterWithoutChangingLockPriority()
    {
        var lockedRoot = new GameObject("Manual lock visibility");
        var engagedRoot = new GameObject("Unlocked encounter visibility");
        var locked = new Target { Root = lockedRoot.transform };
        var engaged = new Target { Root = engagedRoot.transform };
        var resolve = typeof(LitGameplayCameraModeController).GetMethod("ResolveVisibilityTargetRoot", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            Assert.That(resolve.Invoke(null, new object[] { null, engaged, true }), Is.SameAs(engagedRoot.transform));
            Assert.That(resolve.Invoke(null, new object[] { null, engaged, false }), Is.Null, "A stale encounter must not retain an exploration opening.");
            Assert.That(resolve.Invoke(null, new object[] { locked, engaged, true }), Is.SameAs(lockedRoot.transform));
            locked.IsDead = true;
            Assert.That(resolve.Invoke(null, new object[] { locked, engaged, true }), Is.SameAs(engagedRoot.transform));
            engaged.IsDead = true;
            Assert.That(resolve.Invoke(null, new object[] { null, engaged, true }), Is.Null);
        }
        finally { Object.DestroyImmediate(lockedRoot); Object.DestroyImmediate(engagedRoot); }
    }

    [Test]
    public void ProductionJuggernautHasItsRealKnockoutPresentation()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset");
        var prefab = data.ResolveWorldPrefab();
        var brain = prefab.GetComponent<LitBrainsEnemy>();
        Assert.That(brain, Is.Not.Null);
        Assert.That(brain.VFX_KnockedOut, Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX_KnockedOut.prefab")));
        Assert.That(brain.combatProfile.knockoutState, Is.EqualTo("Base Layer.Knocked Out"));
        Assert.That(brain.combatProfile.knockoutState, Is.Not.EqualTo(brain.combatProfile.hurtState));
        var controller = (AnimatorController)prefab.GetComponent<Animator>().runtimeAnimatorController;
        var state = controller.layers[0].stateMachine.states.Single(entry => entry.state.name == "Knocked Out").state;
        Assert.That(state.motion, Is.EqualTo(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/4_Animations/Mixamo_KnockedOut.anim")));
        Assert.That(state.transitions, Is.Empty, "The stun owner releases this state; an automatic exit must not override its vulnerability window.");
        Assert.That(state.speed, Is.GreaterThan(0));
        Assert.That(prefab.GetComponent<ProductionBrainsDeathLifecycle>(), Is.Not.Null);
    }
}
