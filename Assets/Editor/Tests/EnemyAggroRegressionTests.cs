using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyAggroRegressionTests
{
    private GameObject container;
    private GameObject actor;
    private EnemyCombatBrain brain;

    [SetUp]
    public void SetUp()
    {
        container = new GameObject("Aggro regression fixture");
        container.SetActive(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab");
        actor = Object.Instantiate(prefab, container.transform);
        brain = actor.GetComponent<EnemyCombatBrain>();
        Set("home", actor.transform.position);
        Set("homeRotation", actor.transform.rotation);
        Set("locomotion", actor.GetComponent<CombatEnemyLocomotionController>());
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(container);

    [Test]
    public void IdleAtSpawnDoesNotPermanentlyDisableVision()
    {
        TickReturn();
        TickReturn();
        Assert.That(Get<bool>("returning"), Is.False);
        Assert.That(brain.Phase, Is.EqualTo(EnemyCombatBrain.CombatPhase.Idle));
    }

    [Test]
    public void CompletedReturnRearmsVisionAndClearsObservation()
    {
        Set("returning", true);
        Set("observeUntil", 10f);
        TickReturn();
        Assert.That(Get<bool>("returning"), Is.False);
        Assert.That(Get<float>("observeUntil"), Is.Zero);
    }

    [Test]
    public void SkillsResolveFromCharacterDataBeforeFirstReservation()
    {
        var skills = actor.GetComponent<EnemySkills>();
        var data = actor.GetComponent<CharacterInfo>().CharacterData;
        Assert.That(skills.Skills.Count, Is.EqualTo(data.combatSkills.Count));
        Assert.That(skills.SetActiveSkill(data.combatSkills[0]), Is.True);
    }

    private void TickReturn() => typeof(EnemyCombatBrain)
        .GetMethod("TickReturn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(brain, null);

    [TestCase(0f, 3f, 2.8f)]
    [TestCase(2.5f, 6f, 5.8f)]
    [TestCase(2f, 2.1f, 2.05f)]
    [TestCase(2f, 2f, 2f)]
    public void ApproachStaysInsideAuthoredRange(float minimum, float maximum, float expected)
    {
        Assert.That(EnemyCombatBrain.ResolveApproachDistance(minimum, maximum),
            Is.EqualTo(expected).Within(.0001f));
    }

    [Test]
    public void RunningUsesHysteresisBetweenSixAndEightMeters()
    {
        var locomotion = actor.GetComponent<CombatEnemyLocomotionController>();
        var type = typeof(CombatEnemyLocomotionController);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetField("navigationAgent", flags).SetValue(locomotion, actor.GetComponent<UnityEngine.AI.NavMeshAgent>());
        var pace = type.GetMethod("SetMovementPace", flags);
        var running = type.GetField("runPhase", flags);
        pace.Invoke(locomotion, new object[] { 15f });
        Assert.That(running.GetValue(locomotion), Is.True);
        pace.Invoke(locomotion, new object[] { 7f });
        Assert.That(running.GetValue(locomotion), Is.True);
        pace.Invoke(locomotion, new object[] { 5f });
        Assert.That(running.GetValue(locomotion), Is.False);
        pace.Invoke(locomotion, new object[] { 7f });
        Assert.That(running.GetValue(locomotion), Is.False);
    }

    private void Set(string name, object value) => typeof(EnemyCombatBrain)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(brain, value);

    private T Get<T>(string name) => (T)typeof(EnemyCombatBrain)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(brain);
}
