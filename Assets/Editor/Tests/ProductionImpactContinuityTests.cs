#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Ultrabolt.BrainsAI;
using Object = UnityEngine.Object;

public sealed class ProductionImpactContinuityTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> owned = new List<Object>();
    private LitBrainsEnemy enemy;
    private SkillSO skill;

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private object Invoke(string method, params object[] arguments)
        => typeof(LitBrainsEnemy).GetMethod(method, Private).Invoke(enemy, arguments);

    [SetUp]
    public void SetUp()
    {
        var root = Own(new GameObject("Production impact fixture"));
        root.SetActive(false);
        root.transform.position = new Vector3(800, 800, 800);
        var fov = root.AddComponent<FOV>();
        enemy = root.AddComponent<LitBrainsEnemy>();
        enemy.fov = fov;
        var data = Own(ScriptableObject.CreateInstance<CharacterData>());
        var source = Own(ScriptableObject.CreateInstance<EnemyCombatProfileSO>());
        var profile = Own(ScriptableObject.CreateInstance<LitBrainsCombatProfile>());
        data.isEnemy = true; data.hp = 100; data.enemyCombatProfile = source;
        profile.productionSourceProfile = source; profile.executeProductionPatterns = true;
        enemy.combatProfile = profile;
        enemy.ConfigureCharacterData(data);
        skill = Own(ScriptableObject.CreateInstance<SkillSO>());
        skill.damages = 10.2f; skill.enemyDamageMultiplier = 1.5f;
        typeof(LitBrainsEnemy).GetField("activeAttack", Private).SetValue(enemy, new LitBrainsAttackProfile { skill = skill });
        typeof(LitBrainsEnemy).GetProperty(nameof(LitBrainsEnemy.ActionSequenceId)).GetSetMethod(true).Invoke(enemy, new object[] { 7 });
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [Test]
    public void NativeDamageIsCapturedAtLaunchAndTheNextAttackUsesTheEngagementMaximum()
    {
        Invoke("CaptureProductionAttackDamage", skill, false);
        Assert.That(enemy.AuthoredAttackDamage, Is.EqualTo(16));
        Invoke("RecordProductionAppliedDamage", 30);
        Invoke("RecordProductionAppliedDamage", 5);
        Assert.That(enemy.EngagementMaximumLightDamage, Is.EqualTo(30));
        skill.damages = 99; skill.enemyDamageMultiplier = 2;
        Assert.That(enemy.AuthoredAttackDamage, Is.EqualTo(16), "Late skill edits/hits cannot change an already committed impact.");
        Invoke("CaptureProductionAttackDamage", skill, false);
        Assert.That(enemy.AuthoredAttackDamage, Is.EqualTo(60));
        Invoke("ResetProductionEngagementDamage");
        Assert.That(enemy.AuthoredAttackDamage, Is.Zero);
        Assert.That(enemy.EngagementMaximumLightDamage, Is.Zero);
    }

    [Test]
    public void ThresholdRetaliationKeepsItsOwnAuthoredDamageAndStaleGenerationsCannotImpact()
    {
        Invoke("RecordProductionAppliedDamage", 80);
        skill.damages = 12.2f;
        Invoke("CaptureProductionAttackDamage", skill, true);
        Assert.That(enemy.AuthoredAttackDamage, Is.EqualTo(12));
        typeof(LitBrainsEnemy).GetProperty(nameof(LitBrainsEnemy.ActionSequenceId)).GetSetMethod(true).Invoke(enemy, new object[] { 8 });
        Assert.That(enemy.AuthoredAttackDamage, Is.Zero);
    }

    [Test]
    public void AuthoredVolumeCollectsBothPlayersOnceDespiteMultipleColliders()
    {
        skill.minimumHitDistance = 0; skill.maximumHitDistance = 3;
        skill.enemyImpact = new EnemySkillImpactShape {
            offset = new Vector3(0, 1, 1), radius = 1.5f, arcDegrees = 180,
            targetMask = ~0, requireGroundedTarget = false
        };
        var first = Victim(new Vector3(-.5f, 0, 1));
        var second = Victim(new Vector3(.5f, 0, 1));
        var behind = Victim(new Vector3(0, 0, -1));
        var extra = new GameObject("Additional contact collider");
        extra.transform.SetParent(first.transform, false);
        extra.AddComponent<BoxCollider>().center = Vector3.up;
        Physics.SyncTransforms();
        var victims = new List<SquadCharacterController>();
        enemy.CollectAuthoredImpactVictims(victims);
        Assert.That(victims, Is.EquivalentTo(new[] { first, second }));
        Assert.That(enemy.HasAuthoredImpactContact(behind), Is.False);
    }

    private SquadCharacterController Victim(Vector3 offset)
    {
        var root = Own(new GameObject("Impact victim"));
        root.transform.position = enemy.transform.position + offset;
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = Vector3.up; capsule.height = 2; capsule.radius = .2f;
        var actor = root.AddComponent<SquadCharacterController>();
        Assert.That(actor.CurrentHp, Is.GreaterThan(0));
        return actor;
    }
}
#endif
