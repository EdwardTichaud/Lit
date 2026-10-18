using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Clip-owned counter timing for an identified laboratory attack, not a second action clock.</summary>
public sealed class EnemyReactionTiming
{
    public readonly Animator Animator;
    public readonly AnimationClip Clip;
    public readonly int StateHash, ActionId;
    public readonly float ImpactSeconds;
    public readonly string ValidationError;
    public bool IsValid => ValidationError == null;

    private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>();
    private double sampledPhase, sampledGameTime;
    private int sampledFrame = -1;

    public EnemyReactionTiming(Animator animator, AnimationClip clip, string stateName, float impactSeconds, int actionId)
    {
        Animator = animator; Clip = clip; StateHash = UnityEngine.Animator.StringToHash(stateName ?? "");
        ImpactSeconds = impactSeconds; ActionId = actionId;
        if (animator == null || string.IsNullOrEmpty(stateName)) ValidationError = "Animator ou etat d'attaque absent";
        else if (!ValidateImpact(clip, impactSeconds, out string reason)) ValidationError = reason;
        Capture();
    }

    public static bool ValidateImpact(AnimationClip clip, float impactSeconds, out string reason)
    {
        reason = null;
        if (clip == null || float.IsNaN(impactSeconds) || float.IsInfinity(impactSeconds) || impactSeconds <= 0 || impactSeconds > clip.length)
        { reason = "clip ou instant d'impact invalide"; return false; }
        int count = 0;
        foreach (var marker in clip.events)
        {
            if (marker.functionName != "ResolveBrainsAttackImpact") continue;
            count++;
            if (Mathf.Abs(marker.time - impactSeconds) > .0001f)
            { reason = "instant du profil different de ResolveBrainsAttackImpact"; return false; }
        }
        if (count != 1) reason = "un seul ResolveBrainsAttackImpact requis";
        return reason == null;
    }

    public static bool Contains(double phase, double impact, double duration) =>
        !double.IsNaN(phase) && !double.IsInfinity(phase) && duration > 0 &&
        phase >= Math.Max(0, impact - duration) && phase < impact;

    /// <summary>Preserve the solo slow-motion allowance without slowing shared simulation.</summary>
    public static double CounterWindowSeconds(double nominalSeconds, double soloScale, double impactSeconds, bool online)
    {
        if (double.IsNaN(nominalSeconds) || double.IsInfinity(nominalSeconds) || nominalSeconds <= 0 ||
            double.IsNaN(impactSeconds) || double.IsInfinity(impactSeconds) || impactSeconds <= 0) return 0;
        if (!online) return nominalSeconds;
        if (double.IsNaN(soloScale) || double.IsInfinity(soloScale) || soloScale <= 0) return 0;
        return Math.Min(impactSeconds, nominalSeconds / Math.Min(1, soloScale));
    }

    public bool ContainsCounterPhase(double phase, double nominalSeconds, double soloScale) =>
        Contains(phase, ImpactSeconds, CounterWindowSeconds(nominalSeconds, soloScale, ImpactSeconds,
            TimeManager.IsNetworkSimulationActive));

    public static double ProjectPhase(double phase, double gameDelta, double animationRate, double inputAge, double globalScale) =>
        phase + Math.Max(0, gameDelta) * animationRate - Math.Max(0, inputAge) * globalScale * animationRate;

    private bool Read(out AnimatorStateInfo state, out double phase)
    {
        state = default; phase = 0;
        if (!IsValid || Clip == null || Animator == null || !Animator.isActiveAndEnabled) return false;
        bool next = Animator.IsInTransition(0);
        state = next ? Animator.GetNextAnimatorStateInfo(0) : Animator.GetCurrentAnimatorStateInfo(0);
        // The incoming state owns presentation. An outgoing attack cannot retain a counter window.
        if (state.shortNameHash != StateHash && state.fullPathHash != StateHash) return false;
        clips.Clear();
        if (next) Animator.GetNextAnimatorClipInfo(0, clips); else Animator.GetCurrentAnimatorClipInfo(0, clips);
        bool matched = false;
        foreach (var item in clips) if (item.clip == Clip) { matched = true; break; }
        if (!matched) return false;
        phase = state.normalizedTime * Clip.length;
        return !double.IsNaN(phase) && !double.IsInfinity(phase);
    }

    public void Capture()
    {
        if (!Read(out _, out double phase)) return;
        sampledPhase = phase;
        sampledGameTime = Time.timeAsDouble;
        sampledFrame = Time.frameCount;
    }

    public bool TryGetCurrentPhase(out double phase) => Read(out _, out phase);

    public bool TryGetPhase(double inputTimestamp, out double phase)
    {
        if (!Read(out var state, out phase)) return false;
        double now = InputState.currentTime;
        if (double.IsNaN(inputTimestamp) || inputTimestamp > now + .001) return false;
        double rate = Animator.speed * state.speed * state.speedMultiplier;
        // Dynamic input normally precedes Animator evaluation. Project only the
        // current frame from the last real sample; never integrate a parallel clock.
        double gameDelta = sampledFrame >= 0 && sampledFrame != Time.frameCount && Math.Abs(phase - sampledPhase) < .000001
            ? Time.timeAsDouble - sampledGameTime : 0;
        phase = ProjectPhase(phase, gameDelta, rate, now - inputTimestamp, Time.timeScale);
        return true;
    }
}
