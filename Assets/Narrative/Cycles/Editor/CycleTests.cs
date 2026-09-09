using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CycleTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void DirectEnemyDefeatIsRecordedWithoutSceneMarker(bool alreadyDefeated)
    {
        var root = new GameObject("Direct cycle encounter");
        root.SetActive(false);
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        try
        {
            definition.cycleId = "test.direct-encounter";
            var rules = root.AddComponent<WorldRulesStateManager>();
            var enemy = root.AddComponent<EnemyController>();
            enemy.Health.SetHealth(alreadyDefeated ? 0 : 10, 10);
            var cycle = root.AddComponent<CycleController>();
            cycle.definition = definition;
            cycle.encounterEnemy = enemy;
            typeof(CycleController).GetField("rules", Private).SetValue(cycle, rules);
            typeof(CycleController).GetMethod("BindEncounter", Private).Invoke(cycle, null);
            if (!alreadyDefeated) enemy.Health.ForceDefeat();
            Assert.That(rules.TryGetInt(definition.StateKey, out int state), Is.True);
            Assert.That(state & definition.enemyDefeatedFlags, Is.EqualTo(definition.enemyDefeatedFlags));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
    }

    [Test]
    public void EncounterUsesRuntimeInstanceInsteadOfBakedCopy()
    {
        var root = new GameObject("Cycle marker fixture");
        root.SetActive(false);
        var baked = new GameObject("Baked copy");
        var spawned = new GameObject("Spawned encounter");
        baked.transform.SetParent(root.transform);
        spawned.transform.SetParent(root.transform);
        try
        {
            baked.AddComponent<CharacterInfo>();
            var runtimeHealth = spawned.AddComponent<CharacterInfo>();
            var marker = root.AddComponent<SceneMarker>();
            marker.SetBakedCharacterInstance(baked);
            typeof(SceneMarker).GetField("runtimeInstance", Private).SetValue(marker, spawned);
            var cycle = root.AddComponent<CycleController>();
            cycle.encounterMarker = marker;
            Assert.That(typeof(CycleController).GetMethod("ResolveEncounterHealth", Private).Invoke(cycle, null), Is.SameAs(runtimeHealth));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void NinaPoseSetsDeadParameterAndRestoresItAfterAnimatorReset()
    {
        var root = new GameObject("Nina animator fixture");
        root.SetActive(false);
        try
        {
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Sci-Fi Dog/Nina_Controller.controller");
            root.SetActive(true);
            animator.Rebind();
            animator.Update(0f);
            var binding = new CyclePoseBinding { animator = animator, conditionBoolParameter = "isDead", crossFade = 0f };
            binding.Apply(false);
            Assert.That(animator.GetBool("isDead"), Is.False);
            binding.Apply(true);
            animator.Update(0f);
            Assert.That(animator.GetBool("isDead"), Is.True);
            Assert.That(binding.previousState, Is.EqualTo("Dead"));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Dead"), Is.True);
            animator.Rebind();
            binding.Apply(true);
            Assert.That(animator.GetBool("isDead"), Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void NinaSceneBindsItsScientistAndDeathParameter()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(NinaCycleSetup.ScenePath);
        try
        {
            CycleController cycle = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cycle = root.GetComponentInChildren<CycleController>(true);
                if (cycle != null) break;
            }
            Assert.That(cycle, Is.Not.Null);
            Assert.That(cycle.encounterEnemy, Is.Not.Null);
            Assert.That(cycle.encounterEnemy.GetComponent<ScientistEncounterController>(), Is.Not.Null);
            Assert.That(cycle.poses[0].conditionBoolParameter, Is.EqualTo("isDead"));
            Assert.That(cycle.poses[0].condition.knowledge.Length, Is.EqualTo(2));
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void NinaConfigurationUsesGenericDialoguesAndKeepsPersistentKey()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        Assert.That(definition.ValidateConfiguration(), Is.Empty);
        Assert.That(definition.StateKey, Is.EqualTo("narrative.district1.nina"));
        Assert.That(definition.FindDialogue("nina").openedFlags, Is.EqualTo(20));
        Assert.That(definition.FindDialogue("scar").rewardFlag, Is.EqualTo(8));
    }

    [Test]
    public void ConditionsCombineAllFlagsAnyFlagsAndKnowledge()
    {
        var knowledge = ScriptableObject.CreateInstance<KnowledgeSO>();
        try
        {
            var condition = new CycleCondition { allFlags = 3, anyFlags = 12, knowledge = new[] { knowledge } };
            Assert.IsFalse(condition.Matches(3, _ => true));
            Assert.IsFalse(condition.Matches(4, _ => true));
            Assert.IsFalse(condition.Matches(7, _ => false));
            Assert.IsTrue(condition.Matches(7, _ => true));
            Assert.IsTrue(condition.Matches(11, _ => true));
        }
        finally { Object.DestroyImmediate(knowledge); }
    }

    [Test]
    public void IndependentCyclesSupportSeveralDialoguesWithoutEncounter()
    {
        var root = new GameObject("Independent cycle test");
        root.SetActive(false);
        var first = ScriptableObject.CreateInstance<CycleDefinition>();
        var second = ScriptableObject.CreateInstance<CycleDefinition>();
        try
        {
            first.cycleId = "test.library";
            second.cycleId = "test.harbour";
            first.enemyDefeatedFlags = second.enemyDefeatedFlags = 0;
            first.dialogues = new[] { new CycleDialogue { id = "arrival", line = "Hello", openedFlags = 1 },
                new CycleDialogue { id = "question", line = "Why?", completedFlags = 2 },
                new CycleDialogue { id = "answer", line = "Because.", completedFlags = 4 } };
            Assert.That(first.ValidateConfiguration(), Is.Empty);
            Assert.That(first.FindDialogue("answer"), Is.SameAs(first.dialogues[2]));
            var rules = root.AddComponent<WorldRulesStateManager>();
            var cycle = root.AddComponent<CycleController>();
            cycle.definition = first;
            typeof(CycleController).GetField("rules", Private).SetValue(cycle, rules);
            typeof(CycleController).GetMethod("ApplyDialogueEffects", Private).Invoke(cycle, new object[] { first.dialogues[2], true });
            Assert.IsTrue(rules.TryGetInt(first.StateKey, out int value));
            Assert.AreEqual(4, value);
            Assert.IsFalse(rules.TryGetInt(second.StateKey, out _));
            var snapshot = rules.CaptureVariables();
            rules.ResetRuntimeState();
            rules.ApplyVariables(snapshot);
            Assert.IsTrue(rules.TryGetInt(first.StateKey, out value));
            Assert.AreEqual(4, value);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
    }

    [TestCase(false, -1d, 1)]
    [TestCase(true, 100d, 1)]
    [TestCase(true, -1d, 2)]
    public void CancellationEarlyCompletionAndStaleTokenDoNotGrant(bool completed, double delay, int token)
    {
        var root = new GameObject("Pending dialogue test");
        root.SetActive(false);
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        try
        {
            definition.cycleId = "test.pending";
            var rules = root.AddComponent<WorldRulesStateManager>();
            var cycle = root.AddComponent<CycleController>();
            cycle.definition = definition;
            typeof(CycleController).GetField("rules", Private).SetValue(cycle, rules);
            var pending = (IDictionary)typeof(CycleController).GetField("pendingDialogues", Private).GetValue(cycle);
            var pendingType = typeof(CycleController).GetNestedType("PendingDialogue", BindingFlags.NonPublic);
            var entry = Activator.CreateInstance(pendingType);
            pendingType.GetField("id").SetValue(entry, "reward");
            pendingType.GetField("token").SetValue(entry, 1);
            pendingType.GetField("earliest").SetValue(entry, Time.realtimeSinceStartupAsDouble + delay);
            pending.Add(0UL, entry);
            typeof(CycleController).GetMethod("CompleteDialogue", Private).Invoke(cycle, new object[] { 0UL, "reward", token, completed, null });
            Assert.IsFalse(rules.TryGetInt(definition.StateKey, out _));
            Assert.AreEqual(token == 1 ? 0 : 1, pending.Count);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
    }

    [Test]
    public void DefinitionRejectsDuplicateIdsAndRewardTransitionCollision()
    {
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            definition.cycleId = "test.invalid";
            definition.dialogues = new[] { new CycleDialogue { id = "same", line = "One", rewardFlag = 8, rewardSkill = skill },
                new CycleDialogue { id = "same", line = "Two", openedFlags = 8 } };
            Assert.That(definition.ValidateConfiguration(), Has.Some.Contains("duplicate"));
            Assert.That(definition.ValidateConfiguration(), Has.Some.Contains("other transitions"));
        }
        finally { Object.DestroyImmediate(definition); Object.DestroyImmediate(skill); }
    }
}
