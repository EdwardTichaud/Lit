#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EnemyUnificationTests
{
    [Test]
    public void RestoredZeroHealthSurvivesInitializationAndReactivation()
    {
        var root = new GameObject("Restored character");
        root.SetActive(false);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.hp = 60;
        try
        {
            var info = root.AddComponent<CharacterInfo>();
            info.SetHealth(0, 60);
            info.SetCharacterData(data);
            info.InitializeMaxHealthFromCharacterData();
            root.SetActive(true);
            root.SetActive(false);
            root.SetActive(true);
            Assert.That(info.CurrentHp, Is.Zero);
            Assert.That(info.MaxHp, Is.EqualTo(60));
            Assert.That(info.IsDead, Is.True);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
    }

    [Test]
    public void HealthWithoutCharacterDataPreservesRestoredValuesAndReportsActualDamage()
    {
        var root = new GameObject("Health only object");
        root.SetActive(false);
        try
        {
            var info = root.AddComponent<CharacterInfo>();
            info.SetHealth(4, 12);
            int changes = 0;
            info.HealthChanged += _ => changes++;
            Assert.That(info.ApplyDamage(9), Is.EqualTo(4));
            Assert.That(info.ApplyDamage(1), Is.Zero);
            Assert.That(changes, Is.EqualTo(1));
            info.RestoreToMax();
            Assert.That(info.CurrentHp, Is.EqualTo(12));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab")]
    [TestCase("Assets/Characters/9_Ghosts/Luc/Enemy_Model_MadScientist.prefab")]
    [TestCase("Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.prefab")]
    public void EnemyPrefabUsesOneControllerAndNoPlayerAnimationReceiver(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponents<EnemyController>().Length, Is.EqualTo(1));
        Assert.That(prefab.GetComponents<CharacterInfo>().Length, Is.EqualTo(1));
        Assert.That(prefab.GetComponent<PlayerCombatAnimationEvents>(), Is.Null);
        Assert.That(prefab.GetComponent<PlayerRootMotionRelay>(), Is.Null);
        Assert.That(prefab.GetComponent<Animator>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<CharacterInfo>().SourceData, Is.Not.Null);
        foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true)) Assert.That(component, Is.Not.Null, "Missing script in " + path);
    }

    [Test]
    public void OldActionCompletionCannotFinishCurrentAction()
    {
        var root = new GameObject("Action sequence fixture");
        root.SetActive(false);
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            var enemy = root.AddComponent<EnemyController>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(EnemyController).GetField("ActorActiveSkill", flags).SetValue(enemy, skill);
            typeof(EnemyController).GetField("<ActionSequenceId>k__BackingField", flags).SetValue(enemy, 7);
            typeof(EnemyController).GetMethod("CompleteAction", flags).Invoke(enemy, new object[] { 6, true });
            Assert.That(enemy.ActiveSkill, Is.SameAs(skill));
            Assert.That(enemy.ActionSequenceId, Is.EqualTo(7));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(skill); }
    }

    [UnityTest]
    public IEnumerator InstancesDoNotShareMutableRuntimeData()
    {
        yield return new EnterPlayMode();
        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.hp = 20;
        data.enemyCombatProfile = ScriptableObject.CreateInstance<EnemyCombatProfileSO>();
        var first = new GameObject("First character");
        var second = new GameObject("Second character");
        try
        {
            var a = first.AddComponent<CharacterInfo>();
            var b = second.AddComponent<CharacterInfo>();
            a.SetCharacterData(data);
            b.SetCharacterData(data);
            a.CharacterData.stats.strength = 99;
            a.CharacterData.enemySettings.PhysicsGroundSkin = .2f;
            a.CharacterData.enemyCombatProfile.pursuitRadius = 99;
            a.ApplyDamage(5);
            Assert.That(b.CharacterData.stats.strength, Is.EqualTo(data.stats.strength));
            Assert.That(b.CharacterData.enemySettings.PhysicsGroundSkin, Is.EqualTo(data.enemySettings.PhysicsGroundSkin));
            Assert.That(b.CharacterData.enemyCombatProfile.pursuitRadius, Is.EqualTo(data.enemyCombatProfile.pursuitRadius));
            Assert.That(b.CurrentHp, Is.EqualTo(20));
            Assert.That(a.CharacterData.UniqueId, Is.EqualTo(data.UniqueId));
            Assert.That(a.SourceData, Is.SameAs(b.SourceData));
        }
        finally
        {
            Object.Destroy(first); Object.Destroy(second);
            Object.Destroy(data.enemyCombatProfile); Object.Destroy(data);
        }
        yield return null;
        yield return new ExitPlayMode();
    }
}
#endif
