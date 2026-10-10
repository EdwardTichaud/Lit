using System;
using UnityEngine;

/// <summary>Presentation only. Never sets physical speed, position or Animator.speed.</summary>
[CreateAssetMenu(menuName = "Lit/Animation/Locomotion Presentation Profile")]
public sealed class LocomotionPresentationProfile : ScriptableObject
{
    public const string PlaybackParameter = "LocomotionPlaybackRate";
    [Serializable] public sealed class Cycle
    {
        public AnimationClip clip;
        public Vector2 direction = Vector2.up;
        [Min(.001f)] public float nativeSpeed;
        [Range(0,1)] public float leftContactPhase, rightContactPhase;
    }
    public Avatar measuredAvatar;
    public Cycle[] walk = Array.Empty<Cycle>(), run = Array.Empty<Cycle>();
    public Cycle explorationWalk, explorationRun;
    public bool validated;
    [Tooltip("Separate release gate: rendered supports and transitions passed the measured acceptance criteria on this avatar.")]
    public bool supportValidated;
    [Min(0)] public float stopSpeed = .05f;
    [Min(.01f)] public float minimumCadence = .25f;
    [Min(.01f)] public float maximumCadence = 4f;
    [Tooltip("Existing controller's walk/run thresholds; these do not set physical speed.")]
    public Vector2 blendThresholds = new Vector2(1.35f, 3.25f);
    public Vector2 explorationBlendThresholds = new Vector2(1.35f, 3.25f);
    [Tooltip("For a NavMesh actor, reconcile the baked surface height with the collider through the Agent's base offset.")]
    public bool alignNavMeshGround;
    [Min(.01f)] public float maximumNavMeshGroundCorrection = .25f;

    public bool IsUsable(Avatar avatar) => validated && avatar == measuredAvatar &&
        avatar != null && avatar.isValid && Complete(walk) && Complete(run) &&
        Finite(minimumCadence) && Finite(maximumCadence) && minimumCadence > 0 && maximumCadence >= minimumCadence &&
        Finite(stopSpeed) && stopSpeed >= 0 && blendThresholds.x > 0 && blendThresholds.y > blendThresholds.x;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Complete(Cycle[] cycles)
    {
        if (cycles == null || cycles.Length == 0) return false;
        foreach (var cycle in cycles) if (cycle == null || cycle.clip == null || !Finite(cycle.nativeSpeed) || cycle.nativeSpeed <= 0 ||
            !Finite(cycle.direction.x) || !Finite(cycle.direction.y) || cycle.direction.sqrMagnitude < .001f) return false;
        return true;
    }
    public float NativeSpeed(bool running, Vector2 direction, bool exploration = false)
    {
        var forwardCycle = running ? explorationRun : explorationWalk;
        if (exploration && forwardCycle != null && forwardCycle.nativeSpeed > 0) return forwardCycle.nativeSpeed;
        var cycles = running ? run : walk;
        if (cycles == null || cycles.Length == 0) return 0;
        // Blend the neighbouring directional measurements by their angle.
        float speed = 0, weight = 0;
        direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.up;
        foreach (var cycle in cycles)
        {
            float angle = Vector2.Angle(direction, cycle.direction);
            float w = cycles.Length == 1 ? 1 : Mathf.Max(0, 1 - angle / 45.01f);
            speed += cycle.nativeSpeed * w; weight += w;
        }
        return weight > 0 ? speed / weight : cycles[0].nativeSpeed;
    }
    public void Evaluate(float physicalSpeed, bool running, Vector2 direction, out float blend, out float cadence, bool exploration = false)
    {
        if (physicalSpeed <= stopSpeed) { blend = 0; cadence = 1; return; }
        float native = Mathf.Max(.001f, NativeSpeed(running, direction, exploration));
        float requested = physicalSpeed / native;
        cadence = Mathf.Clamp(requested, minimumCadence, maximumCadence);
        // At very low speeds the Idle weight completes the remaining slowdown.
        var thresholds = exploration ? explorationBlendThresholds : blendThresholds;
        blend = (running ? thresholds.y : thresholds.x) * Mathf.Min(1, requested / minimumCadence);
    }
}
