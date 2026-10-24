#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BelmontCycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Root = "Assets/Scenes/Cycles/Cycle_Belmont";

    [TestCase("Assets/Scenes/Cycles/Cycle_Belmont/Prefabs/BrokenAnchor.prefab", true)]
    [TestCase("Assets/Scenes/Cycles/Cycle_Etienne/Prefabs/Prefab_DeadWeight.prefab", false)]
    [TestCase("Assets/Scenes/Cycles/Cycle_Ardent/Prefabs/Prefab_FalseChoir.prefab", false)]
    public void AuthoredBossKeepsItsOwnDeathPresentationPolicy(string path, bool external)
    {
        var parent = new GameObject("Isolated death policy"); parent.SetActive(false);
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(source, Is.Not.Null);
            var actor = Object.Instantiate(source, parent.transform);
            var enemy = actor.GetComponent<EnemyController>();
            var boss = actor.GetComponent<BossEncounterBehaviour>();
            Assert.That(boss.Definition.UseExternalDeathPresentation, Is.EqualTo(external));
            typeof(EnemyController).GetMethod("ActorAwake", Private).Invoke(enemy, null);
            var presentation = actor.GetComponent<EnemyDeathPresentation>();
            typeof(EnemyDeathPresentation).GetMethod("Awake", Private).Invoke(presentation, null);
            Assert.That(presentation.UsesExternalPresentation, Is.EqualTo(external));
            if (external)
            {
                enemy.Health.SetHealth(0, 3);
                typeof(EnemyDeathPresentation).GetMethod("BeginIfDead", Private).Invoke(presentation, null);
                Assert.That(typeof(EnemyDeathPresentation).GetField("routine", Private).GetValue(presentation), Is.Null);
                Assert.That(actor.activeSelf, Is.True, "The cinematic must retain its target even at zero HP.");
                Assert.That(enemy.Health.IsDead, Is.True, "Visual ownership must not revive the boss.");
            }
        }
        finally { Object.DestroyImmediate(parent); }
    }

    [Test]
    public void ExternalDeathPresentationRemainsOptInForFutureBosses()
    {
        var definition = ScriptableObject.CreateInstance<BossDefinitionSO>();
        try { Assert.That(definition.UseExternalDeathPresentation, Is.False); }
        finally { Object.DestroyImmediate(definition); }
    }

    [TestCase(0, 1, 2)] [TestCase(0, 2, 1)] [TestCase(1, 0, 2)]
    [TestCase(1, 2, 0)] [TestCase(2, 0, 1)] [TestCase(2, 1, 0)]
    public void EachTorchRemovesExactlyOneHealthPointInEveryOrder(int a, int b, int c)
    {
        var root = new GameObject("Belmont isolated boss contract"); root.SetActive(false);
        try
        {
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(Root + "/Data/CharacterData_BrokenAnchor.asset");
            Assert.That(data, Is.Not.Null);
            var actor = Object.Instantiate(data.ResolveWorldPrefab(), root.transform);
            var enemy = actor.GetComponent<EnemyController>();
            var boss = actor.GetComponent<BrokenAnchorBoss>();
            Assert.That(enemy, Is.Not.Null); Assert.That(boss, Is.Not.Null);
            Assert.That(data.ResolveMaxHp(), Is.EqualTo(3));
            Assert.That(boss.Definition.ShowBossBar, Is.False);
            typeof(EnemyController).GetField("requiresFlameInfluence", Private).SetValue(enemy, false);
            enemy.Health.SetCharacterData(data);
            enemy.Health.InitializeMaxHealthFromCharacterData();
            var torches = Enumerable.Range(0, 3).Select(index =>
            {
                var target = new GameObject("Isolated torch " + index); target.transform.SetParent(root.transform);
                return target.AddComponent<BrokenAnchorTorch>();
            }).ToArray();
            typeof(BrokenAnchorBoss).GetMethod("Awake", Private).Invoke(boss, null);
            boss.Configure(actor.transform, torches, null, .75f, 3f, 12f, 8f, 15, 3.5f);
            actor.SetActive(true); root.SetActive(true);
            Assert.That(enemy.IsEncounterAvailable, Is.True);
            typeof(BossEncounterBehaviour).GetMethod("EngageAuthoritatively", Private).Invoke(boss, null);
            Assert.That(boss.IsBossEngaged, Is.True);
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(3));
            int remaining = 3;
            foreach (int index in new[] { a, b, c })
            {
                Assert.That(torches[index].TryLight(), Is.True);
                remaining--;
                Assert.That(boss.CurrentSegments, Is.EqualTo(remaining));
                Assert.That(enemy.Health.CurrentHp, Is.EqualTo(remaining));
                Assert.That(torches[index].TryLight(), Is.False);
                boss.NotifyTorchLit(torches[index]);
                Assert.That(boss.CurrentSegments, Is.EqualTo(remaining), "The projectile callback must not count the state event twice.");
            }
            Assert.That(boss.IsBossResolved, Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(0, 1, 2)] [TestCase(0, 2, 1)] [TestCase(1, 0, 2)]
    [TestCase(1, 2, 0)] [TestCase(2, 0, 1)] [TestCase(2, 1, 0)]
    public void DevTorchObjectivesStayParallelAndDoNotWriteProgress(int a, int b, int c)
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/BelmontCycle.asset");
        var root = new GameObject("Belmont isolated Dev contract"); root.SetActive(false);
        try
        {
            var rules = root.AddComponent<WorldRulesStateManager>();
            var cycle = root.AddComponent<CycleController>(); cycle.definition = definition;
            typeof(CycleController).GetField("rules", Private).SetValue(cycle, rules);
            typeof(CycleController).GetField("devStartEnabled", Private).SetValue(cycle, true);
            typeof(CycleController).GetField("devStartStepId", Private).SetValue(cycle, "lower_flame_one_lit");
            typeof(CycleController).GetMethod("InitializeDevSimulation", Private).Invoke(cycle, null);
            var active = typeof(CycleController).GetMethod("IsStepActive", Private);
            var complete = typeof(CycleController).GetMethod("DevCompleteStep", Private);
            var ids = new[] { "lower_flame_one_lit", "lower_flame_two_lit", "lower_flame_three_lit" };
            foreach (int index in new[] { a, b, c })
            {
                Assert.That((bool)active.Invoke(cycle, new object[] { definition.FindStep(ids[index]) }), Is.True);
                complete.Invoke(cycle, new object[] { definition.FindStep(ids[index]) });
            }
            Assert.That((bool)active.Invoke(cycle, new object[] { definition.FindStep("lower_flames_restored") }), Is.True);
            Assert.That((bool)active.Invoke(cycle, new object[] { definition.FindStep("first_ancient_flame_lit") }), Is.False);
            Assert.That(definition.steps.Single(step => step.terminal).id, Is.EqualTo("first_ancient_flame_lit"));
            Assert.That(rules.CaptureVariables(), Is.Empty);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
