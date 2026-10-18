#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using Ultrabolt.BrainsAI;

public sealed class LitBrainsEnemyRuntimeTests
{
    private GameObject actor;
    private CharacterData data;
    private LitBrainsEnemy enemy;

    [SetUp]
    public void SetUp()
    {
        actor = new GameObject("Brains runtime fixture");
        actor.SetActive(false);
        var perception = actor.AddComponent<FOV>();
        enemy = actor.AddComponent<LitBrainsEnemy>();
        enemy.fov = perception;
        data = ScriptableObject.CreateInstance<CharacterData>();
        data.hp = 123;
        data.isEnemy = true;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(actor);
        Object.DestroyImmediate(data);
    }

    [Test]
    public void ConfigurationUsesCharacterInfoAndAuthoredMaxHealth()
    {
        enemy.ConfigureCharacterData(data);
        Assert.AreSame(actor.GetComponent<CharacterInfo>(), enemy.Health);
        Assert.AreEqual(data.ResolveMaxHp(), enemy.CurrentHealth);
        Assert.AreEqual(data.ResolveMaxHp(), enemy.Health.MaxHp);
        Assert.IsNull(actor.GetComponent<EnemyController>());
        Assert.AreEqual(1, actor.GetComponents<Brain>().Length);
    }

    [TestCase(37)]
    [TestCase(0)]
    public void ReconfigurationPreservesRestoredHealthIncludingDefeat(int restored)
    {
        enemy.ConfigureCharacterData(data);
        enemy.Health.SetHealth(restored, 200);
        enemy.ConfigureCharacterData(data);
        Assert.AreEqual(restored, enemy.CurrentHealth);
        Assert.AreEqual(200, enemy.Health.MaxHp);
        Assert.AreEqual(restored == 0, enemy.IsDead);
        Assert.AreEqual(123, data.hp);
    }

    [Test]
    public void DirectPersistentHealthChangesRemainVisibleToBrain()
    {
        enemy.ConfigureCharacterData(data);
        enemy.Health.ApplyDamage(13);
        Assert.AreEqual(110, enemy.CurrentHealth);
        enemy.Health.ForceDefeat();
        Assert.IsTrue(enemy.IsDead);
        enemy.Health.RestoreToMax();
        Assert.IsFalse(enemy.IsDead);
        Assert.AreEqual(123, enemy.CurrentHealth);
    }

    [Test]
    public void PendingThresholdBlocksOnlyItsOwnActor()
    {
        var other = new GameObject("Other threshold actor");
        var host = new GameObject("Threshold controller fixture");
        other.SetActive(false);
        host.SetActive(false);
        var otherEnemy = other.AddComponent<LitBrainsEnemy>();
        var controller = host.AddComponent<CombatHealthThresholdController>();
        var type = typeof(CombatHealthThresholdController);
        var singleton = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var active = type.GetField("activeEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
        var state = type.GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
        var previous = singleton.GetValue(null);
        try
        {
            var targetType = type.Assembly.GetType("CombatHealthThresholdTarget");
            var factory = targetType.GetMethod("From", BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(LitBrainsEnemy) }, null);
            active.SetValue(controller, factory.Invoke(null, new object[] { enemy }));
            state.SetValue(controller, System.Enum.Parse(state.FieldType, "Pending"));
            singleton.SetValue(null, controller);
            var blocks = typeof(LitBrainsEnemy).GetProperty("ThresholdStageBlocksActions", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsTrue((bool)blocks.GetValue(enemy));
            Assert.IsFalse((bool)blocks.GetValue(otherEnemy));
            state.SetValue(controller, System.Enum.Parse(state.FieldType, "Idle"));
            Assert.IsFalse((bool)blocks.GetValue(enemy));
        }
        finally
        {
            active.SetValue(controller, null);
            state.SetValue(controller, System.Enum.Parse(state.FieldType, "Idle"));
            singleton.SetValue(null, previous);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(other);
        }
    }

    [Test]
    public void ThresholdInterfaceDoesNotInventDormancyOrRetaliation()
    {
        enemy.ConfigureCharacterData(data);
        ICombatHealthThresholdActor thresholdActor = enemy;
        Assert.IsFalse(thresholdActor.IsFlameDormant);
        Assert.IsFalse(thresholdActor.TryStartThresholdFailureRetaliation(null));
        Assert.IsNull(enemy.ActiveAttack);
        Assert.AreEqual(LitBrainsAttackPhase.None, enemy.AttackPhase);
    }
}
#endif
