#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ArdentCycleTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static CycleDefinition Definition=>AssetDatabase.LoadAssetAtPath<CycleDefinition>(ArdentCycleValidation.DefinitionPath);
    [Test] public void AuthoredScenesPassAudit()=>Assert.That(ArdentCycleValidation.Audit(),Is.Empty);
    [Test] public void DormantBossCannotBypassGatheringThroughManualCombatOrAggro()
    {
        var root=new GameObject("Boss gathering guard test");root.SetActive(false);
        try
        {
            var actor=new GameObject("Boss");actor.transform.SetParent(root.transform);var enemy=actor.AddComponent<EnemyController>();actor.AddComponent<FalseChoirBoss>();
            var manager=root.AddComponent<RealTimeCombatManager>();
            Assert.That(manager.BeginCombat(root.transform,enemy),Is.False);
            Assert.That(manager.BeginEnemyAggro(root.transform,enemy),Is.False);
            Assert.That(manager.IsCombatActive,Is.False);
        }
        finally{Object.DestroyImmediate(root);}
    }
    [Test] public void MemoryIsAnEditableFourShotTimeline()
    {
        var timeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(ArdentCycleValidation.Root+"/Timeline/LaVoixQuiReste.playable");
        Assert.That(timeline.duration,Is.EqualTo(24));
        var tracks=timeline.GetOutputTracks().ToArray();var shots=tracks.OfType<Unity.Cinemachine.CinemachineTrack>().Single().GetClips().OrderBy(c=>c.start).ToArray();
        Assert.That(shots.Select(c=>c.start),Is.EqualTo(new double[]{0,5,11,17}));Assert.That(shots.Select(c=>c.duration),Is.EqualTo(new double[]{5,6,6,7}));
        Assert.That(tracks.OfType<UnityEngine.Timeline.AnimationTrack>().Count(),Is.EqualTo(2));
        Assert.That(tracks.OfType<UnityEngine.Timeline.ActivationTrack>().All(t=>t.postPlaybackState==UnityEngine.Timeline.ActivationTrack.PostPlaybackState.Inactive),Is.True);
    }
    [TestCase(0,1,2)] [TestCase(0,2,1)] [TestCase(1,0,2)]
    [TestCase(1,2,0)] [TestCase(2,0,1)] [TestCase(2,1,0)]
    public void EvidenceAndRelayPermutationsRemainParallel(int a,int b,int c)
    {
        WithDev("transfer_register_read",cycle=>
        {
            string[][] groups={new[]{"transfer_register_read","conduit_instruction_read","iris_ribbon_found"},new[]{"echo_relay_one_tuned","echo_relay_two_tuned","echo_relay_three_tuned"}};
            var active=typeof(CycleController).GetMethod("IsStepActive",Private);var complete=typeof(CycleController).GetMethod("DevCompleteStep",Private);
            foreach(var group in groups)foreach(int i in new[]{a,b,c}) {var step=Definition.FindStep(group[i]);Assert.That((bool)active.Invoke(cycle,new object[]{step}),Is.True);complete.Invoke(cycle,new object[]{step});}
            Assert.That((bool)active.Invoke(cycle,new object[]{Definition.FindStep("false_choir_defeated")}),Is.True);
            Assert.That((bool)active.Invoke(cycle,new object[]{Definition.FindStep("nora_spoken")}),Is.False);
        });
    }
    [TestCase("echo_relay_one_tuned")] [TestCase("false_choir_defeated")]
    [TestCase("voice_departure_seen")] [TestCase("nora_spoken")]
    public void DevStartIsMemoryOnly(string start)=>WithDev(start,cycle=>Assert.That(cycle.IsSceneStepCompleted(start),Is.False));
    static void WithDev(string start,Action<CycleController> test)
    {
        var go=new GameObject("Ardent isolated test");go.SetActive(false);
        try
        {
            var rules=go.AddComponent<WorldRulesStateManager>();var cycle=go.AddComponent<CycleController>();cycle.definition=Definition;
            typeof(CycleController).GetField("rules",Private).SetValue(cycle,rules);
            typeof(CycleController).GetField("devStartEnabled",Private).SetValue(cycle,true);
            typeof(CycleController).GetField("devStartStepId",Private).SetValue(cycle,start);
            typeof(CycleController).GetMethod("InitializeDevSimulation",Private).Invoke(cycle,null);
            int before=rules.CaptureVariables().Count;test(cycle);Assert.That(rules.CaptureVariables().Count,Is.EqualTo(before));
        }
        finally{Object.DestroyImmediate(go);}
    }
    [Test] public void FalseChoirOnlyTakesDamageDuringRevealAndRejectsDuplicateOrWrongRelay()
    {
        var go=new GameObject("False choir isolated test");go.SetActive(false);
        try
        {
            var enemy=go.AddComponent<EnemyController>();enemy.GetComponent<CharacterInfo>().SetCharacterData(AssetDatabase.LoadAssetAtPath<CharacterData>(ArdentCycleValidation.Root+"/Data/CharacterData_FalseChoir.asset"));
            var boss=go.AddComponent<FalseChoirBoss>();typeof(BossEncounterBehaviour).GetField("definition",Private).SetValue(boss,AssetDatabase.LoadAssetAtPath<BossDefinitionSO>(ArdentCycleValidation.Root+"/Data/BossDefinition_FalseChoir.asset"));typeof(BossEncounterBehaviour).GetField("enemy",Private).SetValue(boss,enemy);
            typeof(FalseChoirBoss).GetMethod("Awake",Private).Invoke(boss,null);
            Assert.That(boss.TryReveal(0),Is.False);
            typeof(BossEncounterBehaviour).GetMethod("EngageAuthoritatively",Private).Invoke(boss,null);
            Assert.That(boss.FilterIncomingDamage(25,null,out var damage),Is.False);Assert.That(damage,Is.Zero);
            Assert.That(boss.TryReveal(1),Is.False);Assert.That(boss.CooldownRemaining,Is.Zero);
            Assert.That(boss.TryReveal(0),Is.True);Assert.That(boss.TryReveal(0),Is.False);
            Assert.That(boss.FilterIncomingDamage(25,null,out damage),Is.True);Assert.That(damage,Is.EqualTo(25));Assert.That(enemy.Health.CurrentHp,Is.EqualTo(300),"A relay never inflicts automatic damage.");
            Assert.That(boss.CooldownRemaining,Is.InRange(11.5,12.1));
            typeof(FalseChoirBoss).GetField("offlineExposed",Private).SetValue(boss,Time.unscaledTimeAsDouble-.01);
            typeof(FalseChoirBoss).GetMethod("Update",Private).Invoke(boss,null);
            Assert.That(boss.ActiveRelay,Is.EqualTo(1));Assert.That(boss.IsExposed,Is.False);Assert.That(boss.TryReveal(1),Is.False);
            enemy.Health.ForceDefeat();Assert.That(boss.IsBossResolved,Is.True);Assert.That(boss.CurrentSegments,Is.Zero);Assert.That(boss.TryReveal(1),Is.False);
        }
        finally{Object.DestroyImmediate(go);}
    }
}
#endif
