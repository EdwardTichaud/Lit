using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CycleTests
{
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
