using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public static class CounterHitSignalValidation
{
    sealed class Victim : ICombatTarget
    {
        public Transform Root { get; set; }
        public Transform LockPoint => Root;
        public Animator Animator { get; set; }
        public bool IsDead { get; set; }
        public bool IsAttackCommitted => false;
        public int ActionSequenceId => 0;
        public int ReceiveDamage(int amount) => amount;
        public void SetCinematicSuspended(bool value) { }
        public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => true;
    }
    static int assertions;
    static void Require(bool condition, string message)
    { assertions++; if (!condition) throw new InvalidOperationException("CounterHit validation: " + message); }
    public static void Run()
    {
        assertions = 0;
        Require(CounterHitSignal.RoundDamage(12.5f) == 12 && CounterHitSignal.RoundDamage(float.NaN) == 0 &&
            CounterHitSignal.RoundDamage(float.PositiveInfinity) == 0 && CounterHitSignal.RoundDamage(float.MaxValue) == int.MaxValue, "damage rounding/finite bounds");
        var timelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(CounterKnockoutSignalSetup.TimelinePath);
        var migrated = CombatCinematicRig.CounterHits(timelineAsset).OrderBy(m => m.time).ToArray();
        Require(migrated.Length == 4 && migrated.All(m => m.IsValid && m.vfx != null && m.sfx != null), "migrated contacts/effects");
        Require(timelineAsset.GetRootTracks().OfType<AnimationTrack>().Where(t => t.name == "Enemy.Animator").All(t => t.muted), "fixed enemy animation must be muted");
        var paths = new[] { "Assets/Characters/3_Enemy/Juggernaut/BrainsProduction/Production.controller", "Assets/Characters/3_Enemy/Juggernaut_v2/StoneGolem_Humanoid.controller", "Assets/Werewolf/Werewolf Animator Controller.controller" };
        foreach (var path in paths)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Require(controller.layers[0].stateMachine.states.Any(s => s.state.name == "Hit_1" && s.state.motion != null), "native Hit_1: " + path);
        }
        var root = new GameObject("CounterHit validation isolated"); root.hideFlags = HideFlags.HideAndDontSave;
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        var fallbackTimeline = ScriptableObject.CreateInstance<TimelineAsset>();
        var definition = ScriptableObject.CreateInstance<CounterSkillSO>();
        var controllerFixture = new AnimatorController(); controllerFixture.AddLayer("Base Layer");
        var clip = new AnimationClip();
        clip.SetCurve("body", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0, 0, 1, .1f));
        var machine = controllerFixture.layers[0].stateMachine;
        machine.AddState("Idle").motion = clip; machine.AddState("Hit_1").motion = clip; machine.AddState("Hit_2").motion = clip;
        try
        {
            var player = new GameObject("Player"); player.transform.SetParent(root.transform); new GameObject("body").transform.SetParent(player.transform);
            var enemy = new GameObject("Enemy"); enemy.transform.SetParent(root.transform); new GameObject("body").transform.SetParent(enemy.transform);
            var playerAnimator = player.AddComponent<Animator>(); playerAnimator.runtimeAnimatorController = controllerFixture; playerAnimator.Rebind(); playerAnimator.Update(0);
            var enemyAnimator = enemy.AddComponent<Animator>(); enemyAnimator.runtimeAnimatorController = controllerFixture; enemyAnimator.Rebind(); enemyAnimator.Update(0);
            bool originalEnabled = enemyAnimator.enabled, originalKeepState = enemyAnimator.keepAnimatorStateOnDisable;
            var victim = new Victim { Root = enemy.transform, Animator = enemyAnimator };
            var rigObject = new GameObject("Rig"); rigObject.transform.SetParent(root.transform);
            var rig = rigObject.AddComponent<CombatCinematicRig>();
            typeof(CombatCinematicRig).GetField("bakedTimeline", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(rig, fallbackTimeline);
            var director = rigObject.GetComponent<PlayableDirector>();
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("director").objectReferenceValue = director;
            serialized.FindProperty("signalReceiver").objectReferenceValue = rigObject.GetComponent<SignalReceiver>();
            serialized.FindProperty("logCameraDiagnostics").boolValue = false;
            serialized.FindProperty("logPlacementDiagnostics").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // EditMode-created MonoBehaviours do not receive the normal runtime lifecycle.
            var lifecycle = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CombatCinematicRig).GetMethod("OnDisable", lifecycle).Invoke(rig, null);
            typeof(CombatCinematicRig).GetMethod("Awake", lifecycle).Invoke(rig, null);
            typeof(CombatCinematicRig).GetMethod("OnEnable", lifecycle).Invoke(rig, null);
            var playerTrack = timeline.CreateTrack<AnimationTrack>(null, "Player.Animator");
            var animation = playerTrack.CreateClip<AnimationPlayableAsset>(); ((AnimationPlayableAsset)animation.asset).clip = clip; animation.duration = 1;
            var signals = timeline.CreateTrack<SignalTrack>(null, "Signals");
            var first = signals.CreateMarker<CounterHitSignal>(.2); first.damage = 7.25f;
            var second = signals.CreateMarker<CounterHitSignal>(.5); second.damage = 9; second.hitIndex = 2;
            var late = signals.CreateMarker<CounterHitSignal>(.8); late.damage = 4;
            int calls = 0, legacyCalls = 0; float total = 0;
            CombatCinematicContext Context(bool authority, Action<float> onDamage = null) => new CombatCinematicContext(null, definition,
                resolveImpact: () => legacyCalls++,
                allowGameplayCameraFallback: true, transferTimelineRootMotion: false, allowMissingTimelineBindings: true,
                explicitTarget: victim, explicitPlayer: player.transform, explicitPlayerAnimator: playerAnimator,
                controlsLocalCamera: false, appliesGameplayEffects: authority,
                resolveCounterHitDamage: amount => { calls++; total += amount; onDamage?.Invoke(amount); return CounterHitSignal.RoundDamage(amount); });
            Require(rig.ValidateSkillPlayback(Context(true), "Player.Animator", "Enemy.Animator", out string preflightError, timeline), "preflight explicit Timeline override: " + preflightError);
            Require(rig.TryPlay(Context(true), timeline, "Player.Animator", "Enemy.Animator", out string error), "start: " + error);
            Require(rig.UsesCounterHitSignals, "runtime Timeline override wins over pooled baked Timeline");
            var oldOrigin = director.playableGraph.GetRootPlayable(0);
            var output = Enumerable.Range(0, director.playableGraph.GetOutputCount()).Select(director.playableGraph.GetOutput)
                .First(o => o.IsPlayableOutputOfType<ScriptPlayableOutput>() && o.GetNotificationReceivers().Contains(rig));
            Require(output.GetNotificationReceivers().Contains(rig), "registered on real signal output");
            output.PushNotification(oldOrigin, first, null); rig.OnNotify(oldOrigin, first, null); rig.OnNotify(oldOrigin, second, null);
            Require(calls == 2 && Mathf.Approximately(total, 16.25f), "each float exactly once / multiple hits");
            Require(rig.TryResolveParticipantImpact() && legacyCalls == 0, "legacy clip impact consumed without extra damage");
            enemyAnimator.Update(.1f);
            Require(enemyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hit_2") || enemyAnimator.GetNextAnimatorStateInfo(0).IsName("Hit_2"), "victim controller Hit_2 selected");
            enemyAnimator.speed = 0;
            typeof(CombatCinematicRig).GetMethod("AdvanceCounterHitPresentation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(rig, new object[] { .75d });
            Require(enemyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > .2f && enemyAnimator.speed == 0 && !enemyAnimator.enabled,
                "Timeline clock drives paused Animator once and preserves temporal speed");
            rig.Stop(); rig.OnNotify(oldOrigin, late, null);
            Require(enemyAnimator.enabled == originalEnabled && enemyAnimator.keepAnimatorStateOnDisable == originalKeepState, "Animator ownership restored"); enemyAnimator.speed = 1;
            Require(calls == 2, "no late callback after interruption");
            Require(rig.TryPlay(Context(true), timeline, "Player.Animator", "Enemy.Animator", out error), "reuse");
            var newOrigin = director.playableGraph.GetRootPlayable(0);
            rig.OnNotify(oldOrigin, first, null); Require(calls == 2, "old graph cannot hit reused session");
            rig.OnNotify(newOrigin, first, null); Require(calls == 3, "dedup reset for next session");
            victim.IsDead = true; rig.OnNotify(newOrigin, second, null); Require(calls == 3, "dead target ignored"); victim.IsDead = false;
            rig.Stop();
            Require(rig.TryPlay(Context(false), timeline, "Player.Animator", "Enemy.Animator", out error), "observer start");
            rig.OnNotify(director.playableGraph.GetRootPlayable(0), first, null); Require(calls == 3, "observer never applies damage"); rig.Stop();
            second.hitIndex = 99;
            Require(!rig.TryPlay(Context(true), timeline, "Player.Animator", "Enemy.Animator", out error), "missing Hit_n rejected before effects");
            second.hitIndex = 2;
            Require(rig.TryPlay(Context(true, _ => rig.Stop()), timeline, "Player.Animator", "Enemy.Animator", out error), "reentrant interruption start");
            rig.OnNotify(director.playableGraph.GetRootPlayable(0), first, null);
            Require(calls == 4, "damage-triggered interruption safely releases feedback");
            if (director.playableGraph.IsValid()) director.Stop();
        }
        finally
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(timeline); Object.DestroyImmediate(fallbackTimeline); Object.DestroyImmediate(definition);
            Object.DestroyImmediate(controllerFixture); Object.DestroyImmediate(clip);
        }
        File.WriteAllText("Library/CounterHitSignals.validation.txt", "PASS " + assertions + " assertions; migrated assets, per-hit floats, indices, repeats, interruption, pool reuse, stale graph, death, observer and reentrant stop.");
    }
}
