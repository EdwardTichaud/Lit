using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed partial class CombatCinematicRig : INotificationReceiver
{
    private readonly HashSet<CounterHitSignal> consumedCounterHits = new HashSet<CounterHitSignal>();
    private Animator counterHitAnimator;
    private bool counterHitAnimatorEnabled, counterHitKeepState, counterHitRootMotion;
    private double counterHitPoseTime;
    private PlayableAsset CounterHitTimeline => sessionActive && director != null ? director.playableAsset : RuntimeTimeline;
    public bool UsesCounterHitSignals => HasCounterHitSignals(CounterHitTimeline);

    public static IEnumerable<CounterHitSignal> CounterHits(PlayableAsset asset)
    {
        if (asset is not TimelineAsset timeline) yield break;
        foreach (var track in timeline.GetOutputTracks())
            foreach (var hit in TrackHits(track)) yield return hit;
        if (timeline.markerTrack != null)
            foreach (var hit in timeline.markerTrack.GetMarkers().OfType<CounterHitSignal>()) yield return hit;
    }
    private static IEnumerable<CounterHitSignal> TrackHits(TrackAsset track)
    {
        if (track.mutedInHierarchy) yield break;
        foreach (var hit in track.GetMarkers().OfType<CounterHitSignal>()) yield return hit;
        foreach (var child in track.GetChildTracks()) foreach (var hit in TrackHits(child)) yield return hit;
    }
    public static bool HasCounterHitSignals(PlayableAsset asset) => CounterHits(asset).Any();
    public static bool ValidateCounterHitStates(PlayableAsset asset, Animator target, out string error)
    {
        foreach (var hit in CounterHits(asset))
        {
            string state = "Base Layer.Hit_" + hit.hitIndex;
            if (!hit.IsValid || target == null || !target.HasState(0, Animator.StringToHash(state)))
            { error = "CounterHit invalide ou etat manquant sur la victime : " + state; return false; }
        }
        error = null; return true;
    }
    private void RegisterCounterHitNotifications()
    {
        if (!UsesCounterHitSignals || !director.playableGraph.IsValid()) return;
        var graph = director.playableGraph;
        for (int i = 0; i < graph.GetOutputCount(); i++)
        {
            var output = graph.GetOutput(i);
            if (!output.IsPlayableOutputOfType<ScriptPlayableOutput>()) continue;
            if (output.GetNotificationReceivers().Contains(this)) continue;
            output.AddNotificationReceiver(this);
        }
    }
    public void OnNotify(Playable origin, INotification notification, object userContext)
    {
        if (notification is not CounterHitSignal hit || !sessionActive || context == null || !hit.IsValid ||
            !origin.IsValid() || !director.playableGraph.Equals(origin.GetGraph()) ||
            !CounterHits(CounterHitTimeline).Contains(hit) || !consumedCounterHits.Add(hit)) return;
        AdvanceCounterHitPresentation(hit.time);
        CombatLab_CounterHit_Damage_Animation_VFX_SFX(hit.damage, hit.hitIndex, hit.vfx, hit.sfx, hit.blendSeconds);
    }
    private void CombatLab_CounterHit_Damage_Animation_VFX_SFX(float damage, int hitIndex, GameObject vfx, AudioClipSO sfx, float blend)
    {
        if (!sessionActive || context?.Target?.Root == null || context.Target.IsDead) return;
        int hash = Animator.StringToHash("Base Layer.Hit_" + hitIndex);
        if (context.TargetAnimator == null || !context.TargetAnimator.HasState(0, hash)) return;
        int token = sessionToken;
        // Start the pose before damage: lethal hits retain the normal death authority.
        context.TargetAnimator.CrossFadeInFixedTime(hash, Mathf.Clamp(blend, 0, .25f), 0, 0);
        AdvanceCounterHitPresentation(counterHitPoseTime);
        if (context.AppliesGameplayEffects) context.ResolveCounterHitDamage?.Invoke(damage);
        if (!sessionActive || sessionToken != token || context?.TargetRoot == null) return;
        SpawnHitVfx(vfx);
        if (sfx != null) AudioManager.PlayClipAtPoint(sfx, context.TargetLockPoint != null ? context.TargetLockPoint.position : context.TargetRoot.position);
    }
    private void BeginCounterHitPresentation(PlayableAsset timeline)
    {
        if (!HasCounterHitSignals(timeline) || context?.TargetAnimator == null) return;
        counterHitAnimator = context.TargetAnimator;
        counterHitAnimatorEnabled = counterHitAnimator.enabled;
        counterHitKeepState = counterHitAnimator.keepAnimatorStateOnDisable;
        counterHitRootMotion = counterHitAnimator.applyRootMotion;
        counterHitAnimator.keepAnimatorStateOnDisable = true;
        counterHitAnimator.applyRootMotion = false;
        counterHitAnimator.enabled = false;
        counterHitPoseTime = 0;
    }
    private void AdvanceCounterHitPresentation(double time)
    {
        if (counterHitAnimator == null || !sessionActive || time < counterHitPoseTime) return;
        float delta = (float)(time - counterHitPoseTime); counterHitPoseTime = time;
        var animator = counterHitAnimator; float speed = animator.speed;
        try { animator.speed = 1; animator.Update(delta); }
        finally { if (animator != null) animator.speed = speed; }
    }
    private void EndCounterHitPresentation()
    {
        if (counterHitAnimator == null) return;
        counterHitAnimator.applyRootMotion = counterHitRootMotion;
        counterHitAnimator.enabled = counterHitAnimatorEnabled;
        counterHitAnimator.keepAnimatorStateOnDisable = counterHitKeepState;
        counterHitAnimator = null;
    }
}
