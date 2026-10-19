#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Ultrabolt.BrainsAI;

public sealed class LitBrainsProductionPolicyTests
{
    [Test]
    public void ProductionJuggernautImportsItsAuthoredVisionInsteadOfDefaultSettings()
    {
        const string path = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset";
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        Assert.IsNotNull(data);
        Assert.AreEqual("c54e94b14c7826c4482529b6d4d739ec", data.UniqueId);
        Assert.AreEqual(30f, data.vision.maximumDistance, "Validate the imported asset, not only its YAML text.");
        Assert.AreEqual(100f, data.vision.fieldOfViewDegrees);
        Assert.AreEqual(1.5f, data.vision.eyeHeight);
    }

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

    [TestCase(LitBrainsAttackPhase.Preparation)]
    [TestCase(LitBrainsAttackPhase.Strike)]
    [TestCase(LitBrainsAttackPhase.Recovery)]
    public void LeavingPursuitBoundsPreservesTheCommittedActionUntilItsCompletion(LitBrainsAttackPhase phase)
    {
        var data = ScriptableObject.CreateInstance<CharacterData>();
        var source = ScriptableObject.CreateInstance<EnemyCombatProfileSO>();
        var profile = ScriptableObject.CreateInstance<LitBrainsCombatProfile>();
        var target = new GameObject("Escaped living target");
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            source.pursuitRadius = 35;
            source.disengagePauseSeconds = 1;
            data.enemyCombatProfile = source;
            profile.productionSourceProfile = source; profile.executeProductionPatterns = true;
            enemy.combatProfile = profile;
            enemy.ConfigureCharacterData(data);
            target.transform.position = new Vector3(100, 0, 0);
            enemy.fov.useExternalDetection = true;
            enemy.fov.SetExternalTarget(target.transform);
            typeof(LitBrainsEnemy).GetField("productionEngaged", fields).SetValue(enemy, true);
            typeof(LitBrainsEnemy).GetProperty("AttackPhase").SetValue(enemy, phase);
            typeof(LitBrainsEnemy).GetProperty("CommittedAttackTarget").SetValue(enemy, target.transform);
            typeof(LitBrainsEnemy).GetProperty("ActionSequenceId").SetValue(enemy, 17);
            var tick = typeof(LitBrainsEnemy).GetMethod("TickProductionEncounter", fields);
            Assert.IsFalse((bool)tick.Invoke(enemy, null));
            Assert.AreEqual(phase, enemy.AttackPhase);
            Assert.AreSame(target.transform, enemy.CommittedAttackTarget);
            Assert.AreEqual(17, enemy.ActionSequenceId);
            Assert.IsFalse((bool)typeof(LitBrainsEnemy).GetField("productionReturning", fields).GetValue(enemy));
            typeof(LitBrainsEnemy).GetProperty("AttackPhase").SetValue(enemy, LitBrainsAttackPhase.None);
            Assert.IsTrue((bool)tick.Invoke(enemy, null));
            Assert.IsTrue((bool)typeof(LitBrainsEnemy).GetField("productionReturning", fields).GetValue(enemy));
            Assert.IsNull(enemy.CommittedAttackTarget);
        }
        finally
        {
            Object.DestroyImmediate(target); Object.DestroyImmediate(profile);
            Object.DestroyImmediate(source); Object.DestroyImmediate(data);
        }
    }

    [TestCase(2.5f, 0f, 1f, true)]
    [TestCase(2.5f, 0f, 2.5f, false)]
    [TestCase(2.5f, 0f, 5f, false)]
    [TestCase(0f, 3f, 2f, true)]
    public void RetreatRequiresBeingInsideEveryEquippedPatternMinimum(float patternMinimum, float skillMinimum, float distance, bool expected)
    {
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            skill.minimumHitDistance = skillMinimum;
            var pattern = new EnemyCombatPattern { minimumStartDistance = patternMinimum,
                skills = new System.Collections.Generic.List<SkillSO> { skill } };
            var method = typeof(LitBrainsEnemy).GetMethod("ProductionNeedsRetreat", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual(expected, method.Invoke(null, new object[] { new[] { pattern }, distance }));
            var melee = new EnemyCombatPattern { skills = new System.Collections.Generic.List<SkillSO> { skill } };
            skill.minimumHitDistance = 0;
            Assert.IsFalse((bool)method.Invoke(null, new object[] { new[] { pattern, melee }, distance }),
                "A usable melee band must not be discarded just because Assomoir is too close.");
        }
        finally { Object.DestroyImmediate(skill); }
    }

    [TestCase(0f, 2.5f, 6f, 2.7f)]
    [TestCase(10f, 2.5f, 6f, 5.8f)]
    [TestCase(0f, 2f, 2.1f, 2.05f)]
    public void ApproachUsesTheInteriorOfTheAuthoredRangeWithoutExceedingItsBounds(float preferred, float minimum, float maximum, float expected)
    {
        var source = ScriptableObject.CreateInstance<EnemyCombatProfileSO>();
        var profile = ScriptableObject.CreateInstance<LitBrainsCombatProfile>();
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            source.preferredCombatDistance = preferred;
            profile.productionSourceProfile = source; profile.executeProductionPatterns = true;
            enemy.combatProfile = profile;
            skill.maximumHitDistance = maximum;
            var pattern = new EnemyCombatPattern { minimumStartDistance = minimum,
                skills = new System.Collections.Generic.List<SkillSO> { skill } };
            float actual = (float)typeof(LitBrainsEnemy).GetMethod("ProductionApproachDistance", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(enemy, new object[] { pattern });
            Assert.AreEqual(expected, actual, .0001f);
            Assert.That(actual, Is.InRange(minimum, maximum));
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(profile); Object.DestroyImmediate(skill); }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void MeleePreferenceNeverDiscardsAnAirborneOnlyLoadout(bool preferMelee, bool includesMelee)
    {
        var source = ScriptableObject.CreateInstance<EnemyCombatProfileSO>();
        var profile = ScriptableObject.CreateInstance<LitBrainsCombatProfile>();
        var airborne = ScriptableObject.CreateInstance<SkillSO>();
        var melee = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            source.preferMeleeApproach = preferMelee;
            profile.productionSourceProfile = source;
            profile.executeProductionPatterns = true;
            enemy.combatProfile = profile;
            airborne.enemyActionMotion.movementMode = EnemyActionMovementMode.Airborne;
            var airPattern = new EnemyCombatPattern { skills = new System.Collections.Generic.List<SkillSO> { airborne } };
            var meleePattern = new EnemyCombatPattern { skills = new System.Collections.Generic.List<SkillSO> { melee } };
            var equipped = includesMelee ? new[] { airPattern, meleePattern } : new[] { airPattern };
            var filter = typeof(LitBrainsEnemy).GetMethod("FilterProductionApproachCandidates", BindingFlags.Instance | BindingFlags.NonPublic);
            var candidates = (EnemyCombatPattern[])filter.Invoke(enemy, new object[] { equipped });
            if (preferMelee && includesMelee)
            {
                CollectionAssert.AreEqual(new[] { meleePattern }, candidates);
                typeof(LitBrainsEnemy).GetField("productionAirborneChoice", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, airPattern);
                candidates = (EnemyCombatPattern[])filter.Invoke(enemy, new object[] { equipped });
                CollectionAssert.AreEqual(equipped, candidates, "A chosen airborne alternative remains eligible.");
            }
            else CollectionAssert.AreEqual(equipped, candidates, "Airborne-only actors must still approach and attack.");
        }
        finally
        {
            Object.DestroyImmediate(source); Object.DestroyImmediate(profile);
            Object.DestroyImmediate(airborne); Object.DestroyImmediate(melee);
        }
    }

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
