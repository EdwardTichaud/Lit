using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private ICombatTarget externalTarget;
    private bool externalTargetLocked;
    public bool UsesExternalTarget => externalTarget != null;
    public ICombatTarget CombatTarget => externalTarget ?? (engagedEnemy != null || lockedEnemy != null ? new LegacyCombatTarget(engagedEnemy ?? lockedEnemy) : null);
    public ICombatTarget LockedCombatTarget => externalTarget != null
        ? (externalTargetLocked ? externalTarget : null)
        : (lockedEnemy != null ? new LegacyCombatTarget(lockedEnemy) : null);
    public Transform LockedTargetPoint => externalTarget != null
        ? (externalTargetLocked && !externalTarget.IsDead ? externalTarget.LockPoint : null)
        : (lockedEnemy != null && (lockedEnemy.Health == null || !lockedEnemy.Health.IsDead) ? lockedEnemy.LockPoint : null);
    public bool HasLockedCombatTarget => LockedTargetPoint != null;

    /// <summary>Standalone encounter: shares player services, without attaching a legacy enemy brain.</summary>
    public void BeginExternalEncounter(Transform player, ICombatTarget target)
    {
        EndCombat();
        externalTarget = target;
        externalTargetLocked = true;
        playerRoot = player;
        playerLoadout = null; playerHealth = null; playerController = null;
        playerAnimator = null; playerActionPresentation = null; playerLocomotionBridge = null;
        ResolvePlayerReferences();
        clarity = 0;
        ClarityChanged?.Invoke(clarity, ClarityRank);
        combatActive = playerRoot != null && target != null && target.Root != null && !target.IsDead;
        combatInput?.SetInputActive(combatActive);
        RefreshLockedEnemyStrafeBinding();
        CombatStateChanged?.Invoke(combatActive);
    }

    private bool ToggleExternalLock()
    {
        if (!combatActive || externalTarget == null || externalTarget.IsDead) return false;
        externalTargetLocked = !externalTargetLocked;
        RefreshLockedEnemyStrafeBinding();
        return true;
    }

    public void SuspendCombatTarget(bool suspended) => CombatTarget?.SetCinematicSuspended(suspended);

    private int ApplyExternalSkillDamage(SkillSO skill)
    {
        if (!combatActive || IsPlayerDead() || skill == null || !HasLockedCombatTarget || externalTarget.IsDead) return 0;
        if (!TryGetLockedEnemyHitDistance(out float distance) || !skill.IsWithinHitRange(distance)) return 0;
        Vector3 delta = Vector3.ProjectOnPlane(externalTarget.Root.position - playerRoot.position, Vector3.up);
        if (Mathf.Abs(externalTarget.Root.position.y - playerRoot.position.y) > 1.8f ||
            Vector3.Angle(playerRoot.forward, delta) > skill.enemyImpact.arcDegrees * .5f || Physics.Linecast(playerRoot.position + Vector3.up,
            externalTarget.Root.position + Vector3.up, LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction"), QueryTriggerInteraction.Ignore)) return 0;
        int applied = DeliverExternalImpact(new CombatImpact(Mathf.RoundToInt(skill.Damages), skill.interruptionForce, CombatImpactOrigin.Basic));
        if (applied <= 0) return 0;
        CombatDamageWorldFeedback.Show(externalTarget.Root, applied, Color.white, 2);
        AddClarity(skill.ClarityGainOnHit);
        PlayerLightDamageApplied?.Invoke(applied);
        PlayerSkillImpactApplied?.Invoke(skill, applied);
        EvaluateCombatOutcome();
        return applied;
    }

    private int DeliverExternalImpact(CombatImpact impact) => externalTarget is ICombatImpactReceiver receiver
        ? receiver.ReceiveImpact(impact) : externalTarget.ReceiveDamage(impact.Damage);
}
