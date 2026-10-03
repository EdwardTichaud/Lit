#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public sealed class EtienneCycleTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static CycleDefinition Definition => AssetDatabase.LoadAssetAtPath<CycleDefinition>(EtienneCycleValidation.DefinitionPath);

    [Test] public void AuthoredScenesPassReadOnlyAudit() => Assert.That(EtienneCycleValidation.Audit(), Is.Empty);

    [Test] public void DefinitionIsIndependentAndOnlyFinalLineTerminates()
    {
        var d = Definition;
        Assert.That(d.ValidateConfiguration(), Is.Empty);
        Assert.That(d.prerequisites.conditions, Is.Empty);
        Assert.That(d.FindStep("puits_entered").kind, Is.EqualTo(CycleStepKind.ZoneEntered));
        Assert.That(d.FindStep("puits_entered").prerequisites.conditions, Is.Empty);
        Assert.That(d.steps.Single(s => s.terminal).id, Is.EqualTo("lucian_after_etienne"));
        var exit = d.FindStep("etienne_spoken").rewards.Single(r => r.kind == CycleRewardKind.Activation);
        Assert.That(exit.key, Is.EqualTo("district1.etienne.flooded_conduits_access"));
        Assert.That(exit.boolValue, Is.True);
        Assert.That(d.FindStep("last_relief_seen").kind, Is.EqualTo(CycleStepKind.SequenceCompleted));
    }

    [Test] public void ExitActivationIsIncludedInNetworkSnapshotWithoutUnrelatedWorldKeys()
    {
        var go = new GameObject("Étienne snapshot test"); go.SetActive(false);
        try
        {
            var rules = go.AddComponent<WorldRulesStateManager>();
            var service = go.AddComponent<CycleProgressionService>();
            typeof(CycleProgressionService).GetField("rules", Private).SetValue(service, rules);
            service.RegisterDefinition(Definition);
            var accepts = typeof(CycleProgressionService).GetMethod("IsSynchronizedKey", Private);
            Assert.That((bool)accepts.Invoke(service, new object[]{"district1.etienne.flooded_conduits_access"}), Is.True);
            Assert.That((bool)accepts.Invoke(service, new object[]{"narrative.district1.etienne.step.etienne_spoken"}), Is.True);
            Assert.That((bool)accepts.Invoke(service, new object[]{"unrelated.world.variable"}), Is.False);
        }
        finally {Object.DestroyImmediate(go);}
    }

    [TestCase(0,1,2)] [TestCase(0,2,1)] [TestCase(1,0,2)]
    [TestCase(1,2,0)] [TestCase(2,0,1)] [TestCase(2,1,0)]
    public void AllEvidencePermutationsUnlockBrakeButNotGhost(int a, int b, int c)
    {
        WithDev("relief_register_read", cycle =>
        {
            var d = cycle.definition;
            string[] ids = {"relief_register_read", "conduit_route_read", "emergency_brake_read"};
            var complete = typeof(CycleController).GetMethod("DevCompleteStep", Private);
            var active = typeof(CycleController).GetMethod("IsStepActive", Private);
            foreach (int i in new[] {a,b,c})
            {
                Assert.That((bool)active.Invoke(cycle, new object[]{d.FindStep(ids[i])}), Is.True);
                complete.Invoke(cycle, new object[]{d.FindStep(ids[i])});
            }
            Assert.That((bool)active.Invoke(cycle, new object[]{d.FindStep("brake_released")}), Is.True);
            Assert.That((bool)active.Invoke(cycle, new object[]{d.FindStep("etienne_spoken")}), Is.False);
        });
    }

    [TestCase("brake_released", "brake_released")]
    [TestCase("dead_weight_defeated", "dead_weight_defeated")]
    [TestCase("last_relief_seen", "last_relief_seen")]
    [TestCase("etienne_spoken", "etienne_spoken")]
    public void DevStartsNeverWriteWorldState(string start, string activeId)
    {
        WithDev(start, cycle =>
        {
            var active = typeof(CycleController).GetMethod("IsStepActive", Private);
            Assert.That((bool)active.Invoke(cycle, new object[]{cycle.definition.FindStep(activeId)}), Is.True);
            Assert.That(cycle.IsSceneStepCompleted(activeId), Is.False);
            if (start == "etienne_spoken")
                Assert.That(CycleController.IsVirtuallyKnownForDevSimulation(cycle.definition.FindStep("last_relief_seen").rewards[0].knowledge), Is.True);
        });
    }

    static void WithDev(string start, Action<CycleController> test)
    {
        var go = new GameObject("Étienne isolated test"); go.SetActive(false);
        try
        {
            var rules = go.AddComponent<WorldRulesStateManager>();
            var cycle = go.AddComponent<CycleController>(); cycle.definition = Definition;
            typeof(CycleController).GetField("rules", Private).SetValue(cycle, rules);
            typeof(CycleController).GetField("devStartEnabled", Private).SetValue(cycle, true);
            typeof(CycleController).GetField("devStartStepId", Private).SetValue(cycle, start);
            typeof(CycleController).GetMethod("InitializeDevSimulation", Private).Invoke(cycle, null);
            int count = rules.CaptureVariables().Count;
            test(cycle);
            Assert.That(rules.CaptureVariables().Count, Is.EqualTo(count), "Dev state must remain memory-only.");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test] public void PoidsMortHasIndependentDataAndOrdinaryDamage()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EtienneCycleValidation.Root + "/Prefabs/Prefab_DeadWeight.prefab");
        var b = prefab.GetComponent<DeadWeightBoss>(); var d = prefab.GetComponent<CharacterInfo>().SourceData;
        Assert.That(d.hp, Is.EqualTo(300)); Assert.That(d.worldPrefab, Is.EqualTo(prefab));
        Assert.That(b.SuppressDefaultEnemyBrain, Is.False); Assert.That(b.Definition.ShowBossBar, Is.False);
        Assert.That(b.Definition.DamagePolicy, Is.EqualTo(BossDamagePolicy.Normal));
        var anchor = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/Cycles/Cycle_Belmont/Prefabs/BrokenAnchor.prefab").GetComponent<BrokenAnchorBoss>();
        Assert.That(anchor.SuppressDefaultEnemyBrain, Is.True, "BrokenAnchor must retain its custom brain.");
        Assert.That(anchor.Definition.DamagePolicy, Is.EqualTo(BossDamagePolicy.Invulnerable));
    }

    [Test] public void BrakeStunsFourSecondsWithSharedCooldownAndNoDamage()
    {
        var go = new GameObject("Poids mort isolated test");go.SetActive(false);
        try
        {
            var enemy = go.AddComponent<EnemyController>();
            enemy.GetComponent<CharacterInfo>().SetCharacterData(AssetDatabase.LoadAssetAtPath<CharacterData>(EtienneCycleValidation.Root + "/Data/CharacterData_DeadWeight.asset"));
            var boss = go.AddComponent<DeadWeightBoss>();
            typeof(BossEncounterBehaviour).GetField("definition", Private).SetValue(boss, AssetDatabase.LoadAssetAtPath<BossDefinitionSO>(EtienneCycleValidation.Root + "/Data/BossDefinition_DeadWeight.asset"));
            typeof(BossEncounterBehaviour).GetField("enemy", Private).SetValue(boss, enemy);
            typeof(DeadWeightBoss).GetMethod("Awake", Private).Invoke(boss, null);
            Assert.That(boss.TryApplyEmergencyBrake(), Is.False);
            typeof(BossEncounterBehaviour).GetMethod("EngageAuthoritatively", Private).Invoke(boss, null);
            int hp = enemy.Health.CurrentHp;
            Assert.That(boss.TryApplyEmergencyBrake(), Is.True);
            Assert.That(boss.TryApplyEmergencyBrake(), Is.False);
            Assert.That(boss.IsBrakeHolding, Is.True);
            Assert.That(boss.BrakeCooldownRemaining, Is.InRange(11.5,12.1));
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(hp));
            typeof(DeadWeightBoss).GetField("offlineStunUntil", Private).SetValue(boss, Time.timeAsDouble - 1);
            Assert.That(boss.IsBrakeHolding, Is.False);
            Assert.That(boss.TryApplyEmergencyBrake(), Is.False, "Ending the stun does not end the cooldown.");
            Assert.That(boss.FilterIncomingDamage(20, null, out int damage), Is.True);Assert.That(damage, Is.EqualTo(20));
            enemy.Health.ForceDefeat();Assert.That(boss.IsBossResolved, Is.True);Assert.That(boss.CurrentSegments, Is.Zero);
        }
        finally {Object.DestroyImmediate(go);}
    }

    [Test] public void InterruptedMemoryRestoresAnimatedTransform()
    {
        var go = new GameObject("Étienne memory isolated test");
        try
        {
            go.transform.localPosition = new Vector3(1,2,3);
            go.transform.localRotation = Quaternion.Euler(10,20,30);
            go.transform.localScale = new Vector3(2,3,4);
            var restore = go.AddComponent<Lit.Timeline.TimelineTransformRestore>();
            var director = go.AddComponent<PlayableDirector>();
            var initial = go.transform.localPosition; var rotation = go.transform.localRotation; var scale = go.transform.localScale;
            restore.OnTimelinePlaybackStarted(director);
            go.transform.localPosition += Vector3.up * 2;
            go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            restore.OnTimelinePlaybackFinished(director);
            restore.OnTimelinePlaybackFinished(director);
            Assert.That(go.transform.localPosition, Is.EqualTo(initial));
            Assert.That(Quaternion.Angle(go.transform.localRotation, rotation), Is.LessThan(.001f));
            Assert.That(go.transform.localScale, Is.EqualTo(scale));
        }
        finally {Object.DestroyImmediate(go);}
    }
    [Test] public void MemoryUsesEditableTracksInsteadOfScriptedClock()
    {
        var timeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(EtienneCycleValidation.Root + "/Timeline/TenirJusquAuDernier.playable");
        Assert.That(timeline.GetRootTracks().Select(t=>t.name), Is.EquivalentTo(new[]{"Caméras","Personnages","Contrepoids","Son"}));
        var tracks = timeline.GetOutputTracks().ToArray();
        var shots = tracks.OfType<Unity.Cinemachine.CinemachineTrack>().Single().GetClips().OrderBy(c=>c.start).ToArray();
        Assert.That(shots.Select(c=>c.start), Is.EqualTo(new double[]{0,6,14,24}));
        Assert.That(shots.Select(c=>c.duration), Is.EqualTo(new double[]{6,8,10,6}));
        Assert.That(tracks.OfType<UnityEngine.Timeline.AnimationTrack>().Count(), Is.EqualTo(3));
        var activations = tracks.OfType<UnityEngine.Timeline.ActivationTrack>().ToArray();
        Assert.That(activations.Length, Is.EqualTo(3));
        Assert.That(activations.All(t=>t.postPlaybackState==UnityEngine.Timeline.ActivationTrack.PostPlaybackState.Inactive), Is.True);
        Assert.That(activations.SelectMany(t=>t.GetClips()).All(c=>c.start==6&&(c.end==22||c.end==23)), Is.True);
    }
}
#endif
