#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Ultrabolt.BrainsAI;

public sealed class LucianFluidCombatTests
{
    [Test]
    public void LocomotionHookIsVirtualAndAdapterDoesNotWriteInLateUpdate()
    {
        var hook = typeof(Brain).GetMethod("UpdateAnimator", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsTrue(hook.IsVirtual);
        Assert.AreEqual(typeof(LitBrainsEnemy), typeof(LitBrainsEnemy).GetMethod("UpdateAnimator", BindingFlags.NonPublic | BindingFlags.Instance).DeclaringType);
        Assert.IsNull(typeof(LitBrainsEnemy).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance));
    }

    [Test]
    public void AttackMarkersAndHitShapesAreOrderedAndBoundToIndependentClips()
    {
        var profile = AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(LucianBrainsCombatSetup.FluidProfilePath);
        Assert.IsNotNull(profile);
        Assert.AreEqual(3, profile.attacks.Length);
        foreach (var attack in profile.attacks)
        {
            Assert.That(attack.reactionSeconds, Is.LessThan(attack.strikeSeconds));
            Assert.That(attack.strikeSeconds, Is.LessThan(attack.impactSeconds));
            Assert.That(attack.impactSeconds, Is.LessThan(attack.recoverySeconds));
            Assert.That(attack.recoverySeconds, Is.LessThan(attack.clip.length));
            Assert.That(attack.reach + attack.preparationAdvanceDistance + attack.advanceDistance,
                Is.GreaterThanOrEqualTo(profile.attackStartDistance), "Engagement must be reachable by the authored advance.");
            Assert.That(AssetDatabase.GetAssetPath(attack.clip), Does.StartWith(JuggernautV2Setup.Folder));
            var events = AnimationUtility.GetAnimationEvents(attack.clip);
            Assert.AreEqual(1, events.Count(e => e.functionName == "ResolveBrainsAttackImpact"));
            Assert.AreEqual(attack.impactSeconds, events.Single(e => e.functionName == "ResolveBrainsAttackImpact").time);
        }
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(JuggernautV2Setup.PrefabPath).GetComponent<LitBrainsEnemy>().combatProfile);
    }

    [Test]
    public void LaboratoryCopiesKeepProductionSkillsUntouched()
    {
        for (int i = 1; i <= 3; i++)
        {
            var source = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>("Assets/CombatRealTime/Skills/BasicSkill_" + i + ".asset");
            var copy = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(IluviliraeSetup.Folder + "/CombatLab_BasicSkill_" + i + ".asset");
            Assert.IsNotNull(copy);
            Assert.AreNotSame(source, copy);
            Assert.AreEqual(source.AnimationClip, copy.AnimationClip);
            Assert.AreEqual(i == 1 ? 20 : i == 2 ? 30 : 50, copy.interruptionForce);
            Assert.That(copy.maximumHitDistance, Is.LessThan(source.maximumHitDistance));
        }
    }

    [Test]
    public void BasicComboWindowsFollowEachAuthoredContact()
    {
        for (int i = 1; i <= 3; i++)
        {
            var skill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(IluviliraeSetup.Folder + "/CombatLab_BasicSkill_" + i + ".asset");
            var impacts = AnimationUtility.GetAnimationEvents(skill.AnimationClip)
                .Where(e => e.functionName == "ResolveSkillImpact").ToArray();
            Assert.AreEqual(1, impacts.Length, "One authored contact per basic clip.");
            float contact = impacts[0].time / skill.AnimationClip.length;
            Assert.That(skill.presentation.chainNormalizedTime, Is.GreaterThan(contact));
            Assert.That(skill.presentation.chainTransitionNormalizedTime, Is.GreaterThan(contact));
            Assert.That(skill.presentation.mobilityCancelNormalizedTime, Is.GreaterThan(contact));
        }
    }

    [Test]
    public void ContactRejectsTargetsAboveTheAuthoredHeight()
    {
        var attacker = new GameObject("attacker"); var target = new GameObject("target");
        try
        {
            attacker.transform.position = new Vector3(10000, 0, 10000);
            target.transform.position = attacker.transform.position + new Vector3(0, 3, 1);
            Assert.IsFalse(LucianBrainsCombatArena.HasContact(attacker.transform, target.transform, 2.6f, 110, 1.8f));
            target.transform.position -= Vector3.up * 3;
            Assert.IsTrue(LucianBrainsCombatArena.HasContact(attacker.transform, target.transform, 2.6f, 110, 1.8f));
            target.transform.position = attacker.transform.position + Vector3.right;
            Assert.IsFalse(LucianBrainsCombatArena.HasContact(attacker.transform, target.transform, 2.6f, 110, 1.8f));
        }
        finally { Object.DestroyImmediate(attacker); Object.DestroyImmediate(target); }
    }

    [Test]
    public void CounterStageHasRelativeTracksAndContactAlignedReaction()
    {
        var skill = AssetDatabase.LoadAssetAtPath<CounterSkillSO>(IluviliraeSetup.Folder + "/CombatLab_CounterSkill.asset");
        Assert.IsTrue(skill.CombatCinematicRigPrefab.HasAuthoringStageLayout);
        var timeline = (UnityEngine.Timeline.TimelineAsset)skill.Timeline;
        var tracks = timeline.GetOutputTracks().OfType<UnityEngine.Timeline.AnimationTrack>().ToArray();
        var playerClip = tracks.Single(t => t.name == skill.PlayerAnimatorTrackName).GetClips().Single();
        var enemyClip = tracks.Single(t => t.name == skill.EnemyAnimatorTrackName).GetClips().Single();
        var animation = (UnityEngine.Timeline.AnimationPlayableAsset)playerClip.asset;
        float impact = AnimationUtility.GetAnimationEvents(animation.clip).Single(e => e.functionName == "ResolveCounterSkillImpact").time;
        Assert.That(enemyClip.start, Is.EqualTo(playerClip.start + (impact - playerClip.clipIn) / playerClip.timeScale).Within(.001));
        foreach (var track in tracks)
            Assert.AreEqual(UnityEngine.Timeline.TrackOffset.ApplySceneOffsets, track.trackOffset);
    }

    [Test]
    public void CounterStunStateAndReplaceableVfxAreAuthored()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(JuggernautV2Setup.ControllerPath);
        var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Knocked Out").state;
        Assert.AreEqual(AssetDatabase.LoadAssetAtPath<AnimationClip>(CounterKnockoutSignalSetup.AnimationPath), state.motion);
        Assert.IsEmpty(state.transitions, "The stun timer must own recovery.");
        Assert.IsTrue(typeof(LitBrainsEnemy).GetField("VFX_KnockedOut").IsPublic);
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(JuggernautV2Setup.PrefabPath).GetComponent<LitBrainsEnemy>();
        Assert.AreEqual(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX_KnockedOut.prefab"), enemy.VFX_KnockedOut);
    }

    [Test]
    public void CounterTwoSignalTargetsTheCurrentEnemyAtThreePointFiveSeconds()
    {
        var timeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(CounterKnockoutSignalSetup.TimelinePath);
        var signal = AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.SignalAsset>(CounterKnockoutSignalSetup.SignalPath);
        var markers = timeline.GetRootTracks().OfType<UnityEngine.Timeline.SignalTrack>()
            .SelectMany(t => t.GetMarkers()).OfType<UnityEngine.Timeline.SignalEmitter>().Where(m => m.asset == signal).ToArray();
        Assert.AreEqual(1, markers.Length);
        Assert.AreEqual(3.5, markers[0].time);
        Assert.IsTrue(markers[0].emitOnce);
        Assert.IsFalse(markers[0].retroactive);
        Assert.Greater(timeline.duration, 3.5);
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>(CounterKnockoutSignalSetup.RigPath);
        var reaction = rig.GetComponent<UnityEngine.Timeline.SignalReceiver>().GetReaction(signal);
        Assert.IsNotNull(reaction);
        Assert.AreEqual(1, Enumerable.Range(0, reaction.GetPersistentEventCount()).Count(i =>
            reaction.GetPersistentTarget(i) == rig.GetComponent<AnimationEvents>() && reaction.GetPersistentMethodName(i) == "KnockedOut"));
        Assert.IsTrue(typeof(ICombatKnockoutReceiver).IsAssignableFrom(typeof(LitBrainsEnemy)));
    }

    [Test]
    public void CounterTwoCameraShakeSignalsPreserveAuthoredTimesAndBindToRig()
    {
        var timeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(CounterKnockoutSignalSetup.TimelinePath);
        var signal = AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.SignalAsset>(CounterKnockoutSignalSetup.CameraShakeSignalPath);
        Assert.IsNotNull(signal);
        var markers = timeline.GetRootTracks().OfType<UnityEngine.Timeline.SignalTrack>()
            .SelectMany(t => t.GetMarkers()).OfType<UnityEngine.Timeline.SignalEmitter>().Where(m => m.asset == signal).OrderBy(m => m.time).ToArray();
        Assert.AreEqual(4, markers.Length);
        double[] times = { 22 / 60d, 46 / 60d, 77 / 60d, 111 / 60d };
        for (int i = 0; i < markers.Length; i++)
        {
            Assert.That(markers[i].time, Is.EqualTo(times[i]).Within(.00001));
            Assert.IsTrue(markers[i].emitOnce);
            Assert.IsFalse(markers[i].retroactive);
        }
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>(CounterKnockoutSignalSetup.RigPath);
        var reaction = rig.GetComponent<UnityEngine.Timeline.SignalReceiver>().GetReaction(signal);
        Assert.IsNotNull(reaction);
        Assert.AreEqual(1, Enumerable.Range(0, reaction.GetPersistentEventCount()).Count(i =>
            reaction.GetPersistentTarget(i) == rig.GetComponent<AnimationEvents>() && reaction.GetPersistentMethodName(i) == "CameraShake"));
        foreach (var camera in rig.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true))
            Assert.IsNotNull(camera.GetComponent<CombatCinematicCameraShake>());
        foreach (var shot in timeline.GetRootTracks().OfType<Unity.Cinemachine.CinemachineTrack>()
            .SelectMany(t => t.GetClips()).Select(c => c.asset).OfType<Unity.Cinemachine.CinemachineShot>())
            Assert.IsTrue(rig.GetComponent<CombatCinematicRig>().HasCameraBinding(shot.VirtualCamera.exposedName.ToString()));
    }

    [Test]
    public void CinematicShakeIsBoundedAndEndsWithoutPositionDrift()
    {
        Assert.AreEqual(Vector3.zero, CombatCinematicCameraShake.EvaluateOffset(-.01f, .06f, .14f, 28));
        Assert.AreEqual(Vector3.zero, CombatCinematicCameraShake.EvaluateOffset(.14f, .06f, .14f, 28));
        Assert.AreEqual(Vector3.zero, CombatCinematicCameraShake.EvaluateOffset(10, .06f, .14f, 28));
        for (float t = 0; t < .14f; t += .005f)
        {
            var offset = CombatCinematicCameraShake.EvaluateOffset(t, .06f, .14f, 28);
            Assert.That(offset.magnitude, Is.LessThanOrEqualTo(.072f));
            Assert.AreEqual(0, offset.z);
        }
    }

    [Test]
    public void CounterCameraDoesNotFollowActorDriftOrFrameDamping()
    {
        var camera = new GameObject("counter camera", typeof(Unity.Cinemachine.CinemachineCamera), typeof(CounterSkillCameraRig));
        var player = new GameObject("player"); var enemy = new GameObject("enemy");
        try
        {
            player.transform.SetPositionAndRotation(new Vector3(20, 0, 30), Quaternion.Euler(0, 90, 0));
            enemy.transform.position = player.transform.position + Vector3.right * 1.7f;
            var rig = camera.GetComponent<CounterSkillCameraRig>();
            typeof(CounterSkillCameraRig).GetField("fixedStageFraming", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rig, true);
            rig.Begin(player.transform, enemy.transform);
            var shot = typeof(CounterSkillCameraRig).GetMethod("GetShotPosition", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector3 before = (Vector3)shot.Invoke(rig, new object[] { .5f });
            player.transform.position += Vector3.forward * 10;
            enemy.transform.position += Vector3.left * 10;
            Vector3 after = (Vector3)shot.Invoke(rig, new object[] { .5f });
            Assert.AreEqual(before, after);
            Assert.AreNotEqual(before, (Vector3)shot.Invoke(rig, new object[] { .8f }));
        }
        finally { Object.DestroyImmediate(camera); Object.DestroyImmediate(player); Object.DestroyImmediate(enemy); }
    }

    [Test]
    public void CounterCameraCannotRemainInsideActorClearance()
    {
        Vector3 actor = new Vector3(5, 0, 2);
        Vector3 center = actor + Vector3.up * 1.1f;
        Assert.That(Vector3.Distance(center, CounterSkillCameraRig.KeepOutsideActor(center, actor, 1.8f)), Is.EqualTo(1.8f).Within(.001f));
        Vector3 outside = center + Vector3.back * 4;
        Assert.AreEqual(outside, CounterSkillCameraRig.KeepOutsideActor(outside, actor, 1.8f));
    }

    [Test]
    public void AuthoringUpgradePreservesAdjustedProfileAndLaboratorySkill()
    {
        var profile = AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(LucianBrainsCombatSetup.FluidProfilePath);
        var skill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(IluviliraeSetup.Folder + "/CombatLab_BasicSkill_1.asset");
        float advance = profile.attacks[0].advanceDistance, force = skill.interruptionForce;
        try
        {
            profile.attacks[0].advanceDistance = .37f;
            skill.interruptionForce = 23f;
            EditorUtility.SetDirty(profile); EditorUtility.SetDirty(skill); AssetDatabase.SaveAssets();
            LucianBrainsCombatSetup.Upgrade();
            Assert.AreEqual(.37f, profile.attacks[0].advanceDistance);
            Assert.AreEqual(23f, skill.interruptionForce);
        }
        finally
        {
            profile.attacks[0].advanceDistance = advance; skill.interruptionForce = force;
            EditorUtility.SetDirty(profile); EditorUtility.SetDirty(skill); AssetDatabase.SaveAssets();
        }
    }
}
#endif
