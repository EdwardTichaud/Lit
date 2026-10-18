#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Ultrabolt.BrainsAI;

public sealed class LitBrainsProductionPolicyTests
{
    private GameObject root;
    private LitBrainsEnemy enemy;
    private LitBrainsEncounterGate gate;

    [SetUp]
    public void Setup()
    {
        root = new GameObject("Production gate fixture");
        root.SetActive(false);
        var fov = root.AddComponent<FOV>();
        enemy = root.AddComponent<LitBrainsEnemy>();
        enemy.fov = fov;
        gate = root.AddComponent<LitBrainsEncounterGate>();
        enemy.RefreshEncounterPolicies();
    }

    [TearDown]
    public void Cleanup() => Object.DestroyImmediate(root);

    [Test]
    public void LockPointPreservesExplicitNestedAnchorAndFallsBackToRoot()
    {
        Assert.AreSame(root.transform, enemy.LockPoint);
        var parent = new GameObject("AuthoredRig").transform;
        parent.SetParent(root.transform, false);
        var anchor = new GameObject("EnemyLockPoint").transform;
        anchor.SetParent(parent, false);
        anchor.localPosition = new Vector3(0, 1.7f, 0);
        enemy.lockPoint = anchor;
        Assert.AreSame(anchor, enemy.LockPoint);
        Assert.AreSame(parent, anchor.parent);
        Assert.AreEqual(new Vector3(0, 1.7f, 0), anchor.localPosition);
        enemy.lockPoint = null;
        Assert.AreSame(root.transform, enemy.LockPoint);
    }

    [TestCase("BeginEnemyAdvance")]
    [TestCase("EndEnemyAdvance")]
    [TestCase("BeginEnemyAirborne")]
    [TestCase("RequestEnemyLanding")]
    [TestCase("BeginEnemyRush")]
    [TestCase("EndEnemyRush")]
    public void ConvertedMotionEventsHaveExactlyOneReceiverOnAnimator(string original)
    {
        string mapped = ProductionBrainsEnemyConversion.MapEvent(original);
        Assert.IsNotNull(enemy.Animator.GetComponent<LitBrainsAnimationEvents>());
        int count = 0;
        foreach (var receiver in enemy.Animator.GetComponents<MonoBehaviour>())
            if (receiver != null && receiver.GetType().GetMethod(mapped, new[] { typeof(AnimationEvent) }) != null) count++;
        Assert.AreEqual(1, count, mapped);
        Assert.IsNull(typeof(LitBrainsEnemy).GetMethod(mapped), "The actor must not receive the same Unity event twice.");
    }

    private bool Gate(string name) => (bool)typeof(LitBrainsEnemy)
        .GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);

    [Test]
    public void EncounterCanDisableDecisionsWithoutInventingDamageImmunity()
    {
        gate.decisionsEnabled = false;
        Assert.IsFalse(Gate("EncounterAllowsDecisions"));
        Assert.IsTrue(Gate("EncounterAllowsDamage"));
        gate.damageEnabled = false;
        Assert.IsFalse(Gate("EncounterAllowsDamage"));
    }

    [Test]
    public void ReturningAndRealDormancyAreNotEngagedPursuit()
    {
        gate.returning = true;
        Assert.IsTrue(Gate("EncounterIsReturning"));
        Assert.IsFalse(Gate("EncounterAllowsDecisions"));
        gate.returning = false;
        gate.flameDormant = true;
        Assert.IsTrue(enemy.IsFlameDormant);
        Assert.IsFalse(Gate("EncounterAllowsDecisions"));
        Assert.IsFalse(Gate("EncounterAllowsDamage"));
    }

    [Test]
    public void CharacterInfoDataChangeIsTheRuntimeSourceOfTruthWithoutResettingRestoredHealth()
    {
        var original = ScriptableObject.CreateInstance<CharacterData>();
        var replacement = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            original.hp = 300;
            replacement.hp = 500;
            enemy.ConfigureCharacterData(original);
            enemy.Health.SetHealth(37, 300);
            enemy.Health.SetCharacterData(replacement);
            Assert.AreSame(replacement, enemy.characterData);
            Assert.AreSame(replacement, enemy.Health.SourceData);
            Assert.AreEqual(37, enemy.CurrentHealth);
            Assert.AreEqual(300, enemy.Health.MaxHp);
        }
        finally { Object.DestroyImmediate(original); Object.DestroyImmediate(replacement); }
    }

    [Test]
    public void EncounterFilteringPreservesTheSuppliedSourceIncludingExplicitNull()
    {
        var probe = root.AddComponent<LitBrainsSourcePolicyProbe>();
        Assert.IsNotNull(probe);
        var sourceRoot = new GameObject("Remote source fixture");
        sourceRoot.SetActive(false);
        try
        {
            var source = sourceRoot.AddComponent<SquadCharacterController>();
            enemy.RefreshEncounterPolicies();
            var filter = typeof(LitBrainsEnemy).GetMethod("FilterEncounterDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            object[] arguments = { 12, source, 0 };
            Assert.IsTrue((bool)filter.Invoke(enemy, arguments));
            Assert.AreSame(source, probe.receivedSource);
            Assert.AreEqual(12, arguments[2]);
            arguments = new object[] { 12, null, 0 };
            Assert.IsTrue((bool)filter.Invoke(enemy, arguments));
            Assert.IsNull(probe.receivedSource);
        }
        finally { Object.DestroyImmediate(sourceRoot); }
    }

    [Test]
    public void PursuitBoundsUseAuthoredHomeRadiusNotVisionOrCurrentEnemyPosition()
    {
        var data = ScriptableObject.CreateInstance<CharacterData>();
        var source = ScriptableObject.CreateInstance<EnemyCombatProfileSO>();
        var profile = ScriptableObject.CreateInstance<LitBrainsCombatProfile>();
        var target = new GameObject("Participant");
        try
        {
            source.pursuitRadius = 35;
            data.enemyCombatProfile = source;
            data.vision.maximumDistance = 5;
            profile.productionSourceProfile = source;
            profile.executeProductionPatterns = true;
            enemy.combatProfile = profile;
            enemy.ConfigureCharacterData(data);
            target.transform.position = new Vector3(35, 0, 0);
            Assert.IsTrue(enemy.IsWithinPursuitBounds(target.transform));
            root.transform.position = new Vector3(30, 0, 0);
            target.transform.position = new Vector3(35.01f, 0, 0);
            Assert.IsFalse(enemy.IsWithinPursuitBounds(target.transform));
        }
        finally { Object.DestroyImmediate(target); Object.DestroyImmediate(profile); Object.DestroyImmediate(source); Object.DestroyImmediate(data); }
    }
}
#endif
