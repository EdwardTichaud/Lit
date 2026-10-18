using System;
using UnityEngine;

/// <summary>Actor-owned threshold operations; implementations must not add another enemy brain.</summary>
public interface ICombatHealthThresholdActor
{
    bool IsFlameDormant { get; }
    bool ForceDefeatFromThreshold();
    bool TryStartThresholdFailureRetaliation(SkillSO skill);
    void CompleteThresholdFailureRetaliation(Action onCompleted);
}

/// <summary>Executes authored retaliation against the captured threshold participant, never perception fallback.</summary>
public interface ICombatHealthThresholdParticipantActor
{
    bool TryStartThresholdFailureRetaliation(SkillSO skill, SquadCharacterController participant);
}

/// <summary>Adapts enemy implementations without duplicating the threshold sequence state machine.</summary>
internal sealed class CombatHealthThresholdTarget
{
    public ICombatTarget Target { get; }
    private readonly EnemyController legacy;
    private ICombatHealthThresholdActor Actor => Target as ICombatHealthThresholdActor;
    public Transform Root => Target is UnityEngine.Object owner && owner == null ? null : Target.Root;
    public string Name => Root != null ? Root.name : "None";
    public bool IsAvailable => Root != null && Root.gameObject.activeInHierarchy;
    public bool IsDead => !IsAvailable || Target.IsDead || Health != null && Health.IsDead;
    public bool IsFlameDormant => legacy != null ? legacy.IsFlameDormant : Actor?.IsFlameDormant == true;
    public bool IsAttackCommitted => IsAvailable && Target.IsAttackCommitted;
    public CharacterInfo Health => legacy != null ? legacy.Health : Root != null
        ? Root.GetComponent<CharacterInfo>() ?? Root.GetComponentInChildren<CharacterInfo>(true) : null;
    public CharacterData Data => Health != null ? Health.CharacterData : null;

    private CombatHealthThresholdTarget(ICombatTarget target, EnemyController legacy = null)
    {
        Target = target;
        this.legacy = legacy;
    }

    public static CombatHealthThresholdTarget From(EnemyController enemy) =>
        enemy != null ? new CombatHealthThresholdTarget(new LegacyCombatTarget(enemy), enemy) : null;

    public static CombatHealthThresholdTarget From(LitBrainsEnemy enemy) =>
        enemy != null ? new CombatHealthThresholdTarget(enemy) : null;

    public bool Matches(Component enemy) => enemy != null && Root == enemy.transform;
    public void SetCinematicSuspended(bool suspended)
    {
        if (Root != null) Target.SetCinematicSuspended(suspended);
    }

    public bool PlaceForCinematic(Vector3 position, Quaternion rotation) =>
        IsAvailable && Target.PlaceForCinematic(position, rotation);

    public bool ForceDefeat()
    {
        if (!IsAvailable || IsFlameDormant || !CombatProtectionDamage.HasAuthority) return false;
        if (legacy != null) return legacy.ForceDefeatFromThreshold();
        if (Actor != null) return Actor.ForceDefeatFromThreshold();
        // CharacterInfo owns health; its normal death notification drives the Brains actor cleanup.
        var health = Health;
        if (health == null) return false;
        if (!health.IsDead) health.ForceDefeat();
        return health.IsDead;
    }

    public bool TryStartThresholdFailureRetaliation(SkillSO skill)
    {
        if (!IsAvailable || IsDead || IsFlameDormant) return false;
        if (legacy != null) return legacy.TryStartThresholdFailureRetaliation(skill);
        if (Actor != null) return Actor.TryStartThresholdFailureRetaliation(skill);
        Debug.LogError("[CombatThreshold] '" + Name + "' must implement ICombatHealthThresholdActor to execute its authored failure retaliation.", Root);
        return false;
    }

    public bool TryStartThresholdFailureRetaliation(SkillSO skill, SquadCharacterController participant)
    {
        if (participant == null || !participant.isActiveAndEnabled || participant.CurrentHp <= 0 ||
            !IsAvailable || IsDead || IsFlameDormant) return false;
        if (legacy != null) return legacy.TryStartThresholdFailureRetaliation(skill, participant);
        if (Target is ICombatHealthThresholdParticipantActor actor)
            return actor.TryStartThresholdFailureRetaliation(skill, participant);
        Debug.LogWarning("[CombatThreshold] '" + Name + "' requires ICombatHealthThresholdParticipantActor for source-bound retaliation; no perception-target fallback is allowed.", Root);
        return false;
    }

    public void CompleteFailureRetaliation(RealTimeCombatManager manager, Action completed)
    {
        if (!IsAvailable) { completed?.Invoke(); return; }
        if (legacy != null)
        {
            legacy.CompleteEnemyAttackWhenGrounded(() =>
            {
                if (legacy != null)
                {
                    manager?.CompleteEnemyAttack(legacy);
                    legacy.ReturnToIdleAnimation();
                    legacy.NotifyAttackCompleted();
                }
                completed?.Invoke();
            });
            return;
        }
        if (Actor != null) { Actor.CompleteThresholdFailureRetaliation(completed); return; }
        Debug.LogError("[CombatThreshold] Missing ICombatHealthThresholdActor retaliation completion on '" + Name + "'.", Root);
        completed?.Invoke();
    }
}
