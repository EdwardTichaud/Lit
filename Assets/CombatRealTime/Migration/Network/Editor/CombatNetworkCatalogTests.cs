using System;
using System.Collections.Generic;
using Lit.Combat.Migration.Network;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatNetworkCatalogTests
{
    [Test]
    public void AuthoredGuidIsStableAndRejectsAssetNames()
    {
        const string guid = "0123456789abcdef0123456789abcdef";
        Assert.That(CombatSkillNetworkId.TryGet(guid, out int lower), Is.True);
        Assert.That(CombatSkillNetworkId.TryGet(guid.ToUpperInvariant(), out int upper), Is.True);
        Assert.That(upper, Is.EqualTo(lower));
        Assert.That(CombatSkillNetworkId.TryGet("BasicSkill_1", out _), Is.False);
        Assert.That(CombatSkillNetworkId.TryGet(new string('0', 32), out _), Is.False);
    }

    [Test]
    public void DuplicateGuidFailsWholeCatalogClosed()
    {
        var catalog = ScriptableObject.CreateInstance<CombatNetworkSkillCatalog>();
        var first = ScriptableObject.CreateInstance<SkillSO>();
        var second = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            // Unsaved fixture assets have no AssetDatabase GUID; supply authored fixture IDs directly.
            typeof(CombatNetworkSkillCatalog).GetField("entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(catalog, new List<CombatNetworkSkillCatalog.Entry>
                {
                    new CombatNetworkSkillCatalog.Entry { skill = first, assetGuid = "0123456789abcdef0123456789abcdef" },
                    new CombatNetworkSkillCatalog.Entry { skill = second, assetGuid = "0123456789abcdef0123456789abcdef" }
                });
            Assert.That(catalog.Rebuild(out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(catalog.TryGet(catalog.Entries[0].Id, out _), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(catalog); UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
    }

    [Test]
    public void RealClipRequiresExactlyOneImpactMarker()
    {
        var skill = ScriptableObject.CreateInstance<BasicSkillsSO>();
        var clip = new AnimationClip();
        try
        {
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
            skill.animationClip = clip; skill.animatorState = "Base Layer.Basic"; skill.targetingMode = SkillTargetingMode.FreeAttack;
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = "HitEnemy", time = .5f } });
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out string reason), Is.EqualTo(CombatNetworkSkillFamily.GroundBasic), reason);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out reason), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
            Assert.That(reason, Does.Contain("exactly one"));
            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { functionName = "HitEnemy", time = .4f },
                new AnimationEvent { functionName = "ResolveSkillImpact", time = .6f }
            });
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(clip); }
    }

    [Test]
    public void PersistentSupportSubsetsAndUnsupportedFamiliesAreExplicit()
    {
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        var charged = ScriptableObject.CreateInstance<ChargedSkillSO>();
        try
        {
            skill.healingTarget = SkillHealingTarget.AllyRegeneration; skill.targetingMode = SkillTargetingMode.SelectedAlly;
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.Regeneration));
            skill.healingTarget = SkillHealingTarget.EnemyLifeSteal; skill.targetingMode = SkillTargetingMode.RequiredTarget;
            Assert.That(CombatNetworkSkillCatalog.Classify(skill, out _), Is.EqualTo(CombatNetworkSkillFamily.LifeSteal));
            Assert.That(CombatNetworkSkillCatalog.Classify(charged, out string reason), Is.EqualTo(CombatNetworkSkillFamily.Unsupported));
            Assert.That(reason, Is.Not.Empty);
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(charged); }
    }

    [Test]
    public void StableContactOrderingPrefersAlignmentThenDistanceThenIdentity()
    {
        Assert.That(CombatNetworkTargetRanking.IsBetter(1, 10, 9, 20, 1, 1), Is.True);
        Assert.That(CombatNetworkTargetRanking.IsBetter(10, 2, 9, 10, 3, 1), Is.True);
        Assert.That(CombatNetworkTargetRanking.IsBetter(10, 2, 1, 10, 2, 9), Is.True);
    }

    [Test]
    public void EnemyHealthSnapshotAppliesZeroAndRejectsStaleResurrection()
    {
        var root = new GameObject("Network enemy health fixture");
        try
        {
            var health = root.AddComponent<CharacterInfo>(); health.SetHealth(8, 10);
            var runtime = root.AddComponent<CombatNetworkActorRuntime>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(CombatNetworkActorRuntime).GetMethod("ResolveReferences", flags).Invoke(runtime, null);
            int changes = 0; health.HealthChanged += _ => changes++;
            var apply = typeof(CombatNetworkActorRuntime).GetMethod("ApplyHealthSnapshot", flags);
            apply.Invoke(runtime, new object[] { new CombatNetworkHealthSnapshot { Revision = 2, Current = 0, Maximum = 10 } });
            Assert.That(health.CurrentHp, Is.Zero);
            Assert.That(changes, Is.EqualTo(1), "Existing health/death listeners must receive the lethal snapshot.");
            apply.Invoke(runtime, new object[] { new CombatNetworkHealthSnapshot { Revision = 1, Current = 10, Maximum = 10 } });
            Assert.That(health.CurrentHp, Is.Zero, "A stale heal snapshot must not resurrect this enemy.");
            Assert.That(changes, Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
