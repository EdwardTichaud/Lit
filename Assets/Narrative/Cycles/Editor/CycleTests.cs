using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CycleTests
{
    [TestCase(0, 0, false)]
    [TestCase(8, 7, false)]
    [TestCase(8, 8, true)]
    [TestCase(8, 31, true)]
    [TestCase(12, 8, false)]
    [TestCase(12, 12, true)]
    public void CompletionRequiresAllConfiguredSharedMilestones(int flags, int state, bool expected)
    {
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        try
        {
            definition.completionFlags = flags;
            Assert.That(definition.IsCompleted(state), Is.EqualTo(expected));
        }
        finally { Object.DestroyImmediate(definition); }
    }

    [Test]
    public void ScarDisappearanceUsesItsExistingRewardMilestone()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        var dialogue = definition.FindDialogue("scar");
        Assert.That(dialogue.disappearAfterCompletion, Is.True);
        Assert.That(dialogue.disappearanceDelay, Is.Zero);
        Assert.That(definition.ResolveDialogueSeconds(dialogue), Is.EqualTo(2f));
        Assert.That(definition.ResolveDialogueSeconds(definition.FindDialogue("nina")), Is.EqualTo(4f));
        Assert.That(dialogue.HasCompleted(7), Is.False);
        Assert.That(dialogue.HasCompleted(8), Is.True);
        Assert.That(definition.IsCompleted(8), Is.True);
        Assert.That(definition.IsCompleted(7), Is.False);
        Assert.That(definition.cycleSceneName, Is.EqualTo("District_1_Enigme_Ghost_Nina"));
        Assert.That(definition.FindDialogue("nina").disappearAfterCompletion, Is.False);
    }

    [Test]
    public void DialogueDurationOverrideDoesNotChangeCycleDefault()
    {
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        try
        {
            definition.dialogueSeconds = 5f;
            Assert.That(definition.ResolveDialogueSeconds(new CycleDialogue()), Is.EqualTo(5f));
            Assert.That(definition.ResolveDialogueSeconds(new CycleDialogue { durationSeconds = 2f }), Is.EqualTo(2f));
            Assert.That(definition.dialogueSeconds, Is.EqualTo(5f));
        }
        finally { Object.DestroyImmediate(definition); }
    }

    [Test]
    public void ZeroDialogueDelayStartsTheFadeWithoutSkippingItsFrames()
    {
        var root = new GameObject("Ghost dialogue fade");
        root.SetActive(false);
        try
        {
            var ghost = root.AddComponent<GhostController>();
            typeof(GhostController).GetField("enableProximityDissolve", Private).SetValue(ghost, true);
            typeof(GhostController).GetField("ghostDisappearanceRendererDelay", Private).SetValue(ghost, 1f);
            var fade = (IEnumerator)typeof(GhostController).GetMethod("DisappearAfterDialogue", Private).Invoke(ghost, new object[] { 0f });
            Assert.That(fade.MoveNext(), Is.True, "Zero waiting time must still play the visual fade.");
            Assert.That(fade.Current, Is.Null, "The first step must be a fade frame, not an additional delay or final cleanup.");
            Assert.That(ghost.IsDialogueDisappearanceComplete, Is.False);
            (fade as IDisposable)?.Dispose();
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void RestoredDialogueGhostStaysHiddenDespiteActivationBinding()
    {
        var root = new GameObject("Restored cycle");
        root.SetActive(false);
        var actor = new GameObject("Restored Ghost");
        actor.transform.SetParent(root.transform);
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        try
        {
            definition.cycleId = "test.ghost-cleanup";
            definition.dialogues = new[] { new CycleDialogue { id = "ghost", completedFlags = 8, disappearAfterCompletion = true } };
            var cycle = root.AddComponent<CycleController>();
            var rules = root.AddComponent<WorldRulesStateManager>();
            cycle.definition = definition;
            typeof(CycleController).GetField("rules", Private).SetValue(cycle, rules);
            actor.AddComponent<GhostController>();
            var interaction = actor.AddComponent<CycleInteraction>();
            interaction.cycle = cycle;
            interaction.dialogueId = "ghost";
            cycle.interactions = new[] { interaction };
            cycle.activations = new[] { new CycleActivationBinding { target = actor } };
            rules.SetInt(definition.StateKey, 8);
            var ready = typeof(CycleController).GetMethod("CompletionPresentationReady", Private);
            Assert.That(ready.Invoke(cycle, null), Is.False, "Scene must remain until the Ghost has disappeared.");
            var present = typeof(CycleController).GetMethod("ApplyPresentation", Private);
            present.Invoke(cycle, null);
            present.Invoke(cycle, null);
            Assert.That(actor.activeSelf, Is.False);
            Assert.That(interaction.Ghost.IsDialogueDisappearanceComplete, Is.True);
            Assert.That(ready.Invoke(cycle, null), Is.True);
            typeof(CycleController).GetField("localDialogue", Private).SetValue(cycle, true);
            Assert.That(ready.Invoke(cycle, null), Is.False, "An open local dialogue must finish before unloading.");
            typeof(CycleController).GetField("localDialogue", Private).SetValue(cycle, false);
            rules.SetInt(definition.StateKey, 0);
            present.Invoke(cycle, null);
            Assert.That(actor.activeSelf, Is.True, "Loading an earlier milestone restores the author activation rule.");
            Assert.That(interaction.Ghost.IsDialogueDisappearanceComplete, Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
    }

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
            Assert.That(cycle.encounterEnemy.StartsAsGhost, Is.True);
            Assert.That(cycle.encounterEnemy, Is.InstanceOf<ICycleCinematicBlocker>());
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
