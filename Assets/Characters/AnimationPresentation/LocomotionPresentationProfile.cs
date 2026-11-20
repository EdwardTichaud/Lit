using System;
using UnityEngine;

/// <summary>Presentation only. Never sets physical speed, position or Animator.speed.</summary>
[CreateAssetMenu(menuName = "Lit/Animation/Locomotion Presentation Profile")]
public sealed class LocomotionPresentationProfile : ScriptableObject
{
    [Serializable] public sealed class Cycle
    {
        public AnimationClip clip;
        public Vector2 direction = Vector2.up;
        [Min(.001f)] public float nativeSpeed;
        [Range(0,1)] public float leftContactPhase, rightContactPhase;
        [Range(0,1)] public float cycleOffset;
    }
    public Avatar measuredAvatar;
    public Cycle[] walk = Array.Empty<Cycle>(), run = Array.Empty<Cycle>();
    public Cycle explorationWalk, explorationRun;
    public bool validated;
    [Min(0)] public float stopSpeed = .05f;
    [Tooltip("Existing controller's walk/run thresholds; these do not set physical speed.")]
    public Vector2 blendThresholds = new Vector2(1.35f, 3.25f);
    public Vector2 explorationBlendThresholds = new Vector2(1.35f, 3.25f);
    [Tooltip("For a NavMesh actor, reconcile the baked surface height with the collider through the Agent's base offset.")]
    public bool alignNavMeshGround;
    [Min(.01f)] public float maximumNavMeshGroundCorrection = .25f;

    public bool IsUsable(Avatar avatar) => validated && avatar == measuredAvatar &&
        avatar != null && avatar.isValid && Complete(walk) && Complete(run) &&
        Finite(stopSpeed) && stopSpeed >= 0 && blendThresholds.x > 0 && blendThresholds.y > blendThresholds.x;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Complete(Cycle[] cycles)
    {
        if (cycles == null || cycles.Length == 0) return false;
        foreach (var cycle in cycles) if (cycle == null || cycle.clip == null || !Finite(cycle.nativeSpeed) || cycle.nativeSpeed <= 0 ||
            !Finite(cycle.direction.x) || !Finite(cycle.direction.y) || cycle.direction.sqrMagnitude < .001f) return false;
        return true;
    }
}
