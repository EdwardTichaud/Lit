using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CycleProgressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private WorldRulesStateManager rules;
    private CycleProgressionService service;
    private readonly List<CycleDefinition> owned = new List<CycleDefinition>();
    [SetUp] public void Setup()
    {
        root = new GameObject("Cycle progression fixture");
        root.SetActive(false);
        rules = root.AddComponent<WorldRulesStateManager>();
        service = root.AddComponent<CycleProgressionService>();
        typeof(CycleProgressionService).GetField("rules", Private).SetValue(service, rules);
    }
    [TearDown] public void Cleanup()
    {
        Object.DestroyImmediate(root);
        foreach (var definition in owned) Object.DestroyImmediate(definition);
        owned.Clear();
    }
    private CycleDefinition Cycle(params CycleStep[] steps)
    {
        var definition = ScriptableObject.CreateInstance<CycleDefinition>();
        definition.cycleId = "test." + owned.Count;
        definition.enemyDefeatedFlags = 0;
        definition.steps = steps;
        owned.Add(definition);
        service.RegisterDefinition(definition);
        return definition;
    }
    private bool Report(CycleDefinition cycle, CycleStepKind kind, string source) =>
        (bool)typeof(CycleProgressionService).GetMethod("ApplyEvent", Private).Invoke(service, new object[] { cycle, kind, source });
    private static CycleRequirements Requires(params string[] ids) => new CycleRequirements
    { conditions = Array.ConvertAll(ids, id => new CycleRequirement { stepId = id }) };
    private static CycleStep Interaction(string id, bool terminal = false) =>
        new CycleStep { id = id, sourceId = id, kind = CycleStepKind.Interaction, terminal = terminal };

    [Test] public void ParallelCyclesAndStepsDoNotInterfere()
    {
        var first = Cycle(Interaction("left", true), Interaction("right", true));
        var second = Cycle(Interaction("last", true));
        Assert.That(service.GetStatus(first), Is.EqualTo(CycleStatus.Available));
        Assert.That(Report(first, CycleStepKind.Interaction, "right"), Is.True);
        Assert.That(service.GetStatus(first), Is.EqualTo(CycleStatus.InProgress));
        Assert.That(service.IsCompleted(second), Is.False);
        Report(first, CycleStepKind.Interaction, "left");
        Assert.That(service.IsCompleted(first), Is.True);
        Assert.That(service.GetStatus(second), Is.EqualTo(CycleStatus.Available));
    }
    [Test] public void EarlyEventsAreDiscardedButDefeatsArePersistentFacts()
    {
        var first = Interaction("first");
        var last = Interaction("last", true); last.prerequisites = Requires("first");
        var enemy = new CycleStep { id = "enemy", kind = CycleStepKind.EnemyDefeated, sourceId = "boss", prerequisites = Requires("first") };
        var cycle = Cycle(first, enemy, last);
        Assert.That(Report(cycle, CycleStepKind.Interaction, "last"), Is.False);
        Report(cycle, CycleStepKind.EnemyDefeated, "boss");
        Assert.That(service.IsStepCompleted(cycle, "enemy"), Is.False);
        Report(cycle, CycleStepKind.Interaction, "first");
        Assert.That(service.IsStepCompleted(cycle, "enemy"), Is.True);
        Assert.That(service.IsStepCompleted(cycle, "last"), Is.False);
        Report(cycle, CycleStepKind.Interaction, "last");
        Assert.That(service.IsCompleted(cycle), Is.True);
    }
    [Test] public void OneEventCannotTraverseTwoOrderedSteps()
    {
        var a = Interaction("a");
        var b = Interaction("b", true); b.sourceId = "a"; b.prerequisites = Requires("a");
        var cycle = Cycle(a, b);
        Report(cycle, CycleStepKind.Interaction, "a");
        Assert.That(service.IsStepCompleted(cycle, "a"), Is.True);
        Assert.That(service.IsStepCompleted(cycle, "b"), Is.False);
        Report(cycle, CycleStepKind.Interaction, "a");
        Assert.That(service.IsCompleted(cycle), Is.True);
    }
    [Test] public void CrossCycleRequirementsAndAnyConditionsWork()
    {
        var first = Cycle(Interaction("end", true));
        var next = Cycle(Interaction("end", true));
        next.prerequisites.conditions = new[] { new CycleRequirement { kind = CycleRequirementKind.CycleCompleted, cycle = first } };
        Assert.That(service.GetStatus(next), Is.EqualTo(CycleStatus.Unavailable));
        Assert.That(Report(next, CycleStepKind.Interaction, "end"), Is.False);
        Report(first, CycleStepKind.Interaction, "end");
        Assert.That(service.GetStatus(next), Is.EqualTo(CycleStatus.Available));
        var any = Requires("end", "missing"); any.mode = CycleRequirementMode.Any;
        Assert.That(service.Matches(first, any), Is.True);
        any.mode = CycleRequirementMode.All;
        Assert.That(service.Matches(first, any), Is.False);
    }
    [Test] public void DuplicateCompletionAndSnapshotRestoreDoNotReapplyReward()
    {
        var step = Interaction("end", true);
        var cycle = Cycle(step);
        Report(cycle, CycleStepKind.Interaction, "end");
        var snapshot = rules.CaptureVariables();
        int changes = 0;
        rules.VariablesChanged += () => changes++;
        Assert.That(Report(cycle, CycleStepKind.Interaction, "end"), Is.False);
        Assert.That(changes, Is.Zero);
        rules.ResetRuntimeState(); service.Refresh();
        Assert.That(service.IsCompleted(cycle), Is.False);
        rules.ApplyVariables(snapshot); service.Refresh();
        changes = 0;
        Assert.That(service.IsCompleted(cycle), Is.True);
        Report(cycle, CycleStepKind.Interaction, "end");
        Assert.That(changes, Is.Zero);
    }
    [TestCase(8)] [TestCase(31)]
    public void LegacyNinaRewardMigratesWithoutRewritingIdentity(int flags)
    {
        var nina = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        service.RegisterDefinition(nina);
        rules.SetInt(nina.StateKey, flags);
        service.Refresh();
        Assert.That(service.IsCompleted(nina), Is.True);
        Assert.That(service.IsStepCompleted(nina, "scar_reward"), Is.True);
        rules.TryGetInt(nina.StateKey, out int after);
        Assert.That(after, Is.EqualTo(flags));
        int changes = 0; rules.VariablesChanged += () => changes++;
        service.Refresh();
        Assert.That(changes, Is.Zero);
        Assert.That(service.IsCompletedScene(nina.cycleSceneName), Is.True);
    }
    [TestCase(4)] [TestCase(16)] [TestCase(20)]
    public void OldNinaVisitBitsRemainUnchanged(int flags)
    {
        var nina = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        service.RegisterDefinition(nina);
        rules.SetInt(nina.StateKey, flags);
        service.Refresh();
        Assert.That(service.IsStepCompleted(nina, "nina_spoken"), Is.True);
        rules.TryGetInt(nina.StateKey, out int after);
        Assert.That(after, Is.EqualTo(flags));
        Assert.That(service.IsCompleted(nina), Is.False);
    }
    [Test] public void CircularAndMissingDependenciesAreReported()
    {
        var a = Interaction("a"); var b = Interaction("b", true);
        a.prerequisites = Requires("b"); b.prerequisites = Requires("a");
        var cycle = Cycle(a,b);
        Assert.That(cycle.ValidateConfiguration(), Has.Some.Contains("circulaire"));
        b.prerequisites = Requires("missing");
        Assert.That(cycle.ValidateConfiguration(), Has.Some.Contains("introuvable"));
    }
    [Test] public void MultipleDialoguesAndSequencesUseIndependentSourceIds()
    {
        var a = new CycleStep { id = "a", sourceId = "intro", kind = CycleStepKind.SequenceCompleted };
        var b = new CycleStep { id = "b", sourceId = "end", kind = CycleStepKind.SequenceCompleted, terminal = true, prerequisites = Requires("a") };
        var cycle = Cycle(a,b);
        Report(cycle, CycleStepKind.SequenceCompleted, "intro");
        Assert.That(service.IsStepCompleted(cycle,"a"), Is.True);
        Assert.That(service.IsCompleted(cycle), Is.False);
        Report(cycle, CycleStepKind.SequenceCompleted, "end");
        Assert.That(service.IsCompleted(cycle), Is.True);
    }
}
