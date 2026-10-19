using System;
using UnityEngine;

public enum ContactDashMotion { Approach, Burst }

/// <summary>Explicit session configuration shared by gameplay and standalone laboratories.</summary>
[Serializable]
public sealed class FreeAttackProfile
{
    public bool enabled = true;
    public bool diagnostics;
    [Range(0, 180)] public float acquisitionCone = 60f;
    [Range(0, 90)] public float maximumCorrection = 25f;
    [Min(1)] public float turnDegreesPerSecond = 1440f;
    [Min(0)] public float freeDashDistance = 10f;
    public ContactDashMotion contactDashMotion;
    [Min(0), Tooltip("Freinage de l'elan de recul apres atterrissage, pendant sa session. 0 conserve le comportement existant.")]
    public float retreatLandingDeceleration;
    public Vector3 contactCenter = new Vector3(0, 1f, 1.15f);
    [Min(.01f)] public float contactRadius = 1.05f;
    [Min(0)] public float heightTolerance = 1.8f;
    public LayerMask obstructionMask = ~0;

    public static bool InCone(Vector3 intent, Vector3 delta, float cone, float correction)
    {
        delta.y = intent.y = 0;
        return delta.sqrMagnitude < .000001f || Vector3.Angle(intent, delta) <= Mathf.Min(cone * .5f, correction);
    }

    public static Vector3 ResolveDirection(Vector2 input, Vector3 cameraForward, Vector3 facing)
    {
        facing = Vector3.ProjectOnPlane(facing, Vector3.up);
        if (facing.sqrMagnitude < .0001f) facing = Vector3.forward;
        if (input.sqrMagnitude <= .01f) return facing.normalized;
        var forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
        if (forward.sqrMagnitude < .0001f) forward = facing;
        forward.Normalize();
        return (Vector3.Cross(Vector3.up, forward) * input.x + forward * input.y).normalized;
    }
}

public sealed class PlayerAttackContext
{
    public readonly int Generation;
    public readonly SkillSO Skill;
    public readonly SkillTargetingMode Mode;
    public readonly SquadCharacterController Ally;
    public readonly Vector3 ZoneOrigin;
    public readonly ICombatTarget Target;
    public readonly Vector3 Origin, TargetOrigin, Direction;
    public readonly bool WasLocked;
    public bool ImpactConsumed { get; private set; }
    public Transform TargetRoot => TargetAlive ? Target.Root : null;
    public Transform TargetPoint => TargetAlive ? Target.LockPoint : null;
    public bool TargetAlive => Target != null && (!(Target is UnityEngine.Object obj) || obj != null) &&
        Target.Root != null && Target.Root.gameObject.activeInHierarchy && !Target.IsDead;

    public PlayerAttackContext(int generation, SkillSO skill, Vector3 origin, Vector3 direction, ICombatTarget target, bool locked,
        SquadCharacterController ally = null, Vector3? zoneOrigin = null)
    {
        Generation = generation; Skill = skill; Origin = origin; Direction = direction;
        Mode = skill != null ? skill.targetingMode : SkillTargetingMode.Unconfigured;
        Ally = ally;
        Target = target; WasLocked = locked;
        TargetOrigin = TargetAlive ? target.Root.position : origin;
        ZoneOrigin = zoneOrigin ?? origin;
    }

    private PlayerAttackContext(PlayerAttackContext source, int generation)
    {
        Generation = generation; Skill = source.Skill; Mode = source.Mode; Ally = source.Ally;
        Origin = source.Origin; Direction = source.Direction; Target = source.Target;
        WasLocked = source.WasLocked; TargetOrigin = source.TargetOrigin; ZoneOrigin = source.ZoneOrigin;
    }
    public PlayerAttackContext WithGeneration(int generation) => new PlayerAttackContext(this, generation);

    public bool TryConsume(int generation)
    {
        if (Generation != generation || ImpactConsumed) return false;
        ImpactConsumed = true;
        return true;
    }
}
