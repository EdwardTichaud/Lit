using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private ICombatTarget externalTarget;
    private bool externalTargetLocked;
    private CombatLockIndicator externalLockIndicator;
    // Laboratory-only arbitration; null preserves immediate production impacts.
    public System.Func<SkillSO, System.Action, bool> ExternalImpactArbitrator { get; set; }

    private void RefreshExternalLockIndicator()
    {
        if (externalLockIndicator != null) externalLockIndicator.SetLocked(false, false);
        externalLockIndicator = null;
        var target = LockedCombatTarget;
        if (!SkillTargetingContract.Alive(target)) return;
        externalLockIndicator = target.Root.GetComponent<CombatLockIndicator>();
        if (externalLockIndicator == null)
            externalLockIndicator = target.Root.gameObject.AddComponent<CombatLockIndicator>();
        externalLockIndicator.SetLocked(true, false);
    }
    public bool UsesExternalTarget => externalTarget != null || worldLockedTarget != null;
    public ICombatTarget CombatTarget => cinematicTarget ?? worldLockedTarget ?? externalTarget ?? (engagedEnemy != null || lockedEnemy != null ? new LegacyCombatTarget(engagedEnemy ?? lockedEnemy) : null);
    public ICombatTarget LockedCombatTarget => worldLockedTarget ?? (externalTarget != null
        ? (externalTargetLocked ? externalTarget : null)
        : (lockedEnemy != null ? new LegacyCombatTarget(lockedEnemy) : null));
    public Transform LockedTargetPoint => worldLockedTarget != null
        ? (SkillTargetingContract.Alive(worldLockedTarget) ? worldLockedTarget.LockPoint : null) : externalTarget != null
        ? (externalTargetLocked && !externalTarget.IsDead ? externalTarget.LockPoint : null)
        : (lockedEnemy != null && (lockedEnemy.Health == null || !lockedEnemy.Health.IsDead) ? lockedEnemy.LockPoint : null);
    public bool HasLockedCombatTarget => LockedTargetPoint != null;

    /// <summary>Standalone encounter: shares player services, without attaching a legacy enemy brain.</summary>
    public void BeginExternalEncounter(Transform player, ICombatTarget target, bool startEngaged = true)
    {
        EndCombat();
        externalTarget = target;
        externalTargetLocked = startEngaged;
        playerRoot = player;
        playerLoadout = null; playerHealth = null; playerController = null;
        playerAnimator = null; playerActionPresentation = null; playerLocomotionBridge = null;
        ResolvePlayerReferences();
        clarity = 0;
        ClarityChanged?.Invoke(clarity, ClarityRank);
        combatActive = startEngaged && playerRoot != null && target != null && target.Root != null && !target.IsDead;
        RefreshCombatSpiritFusion(combatActive);
        combatInput?.SetInputActive(combatActive);
        RefreshExternalLockIndicator();
        RefreshLockedEnemyStrafeBinding();
        CombatStateChanged?.Invoke(combatActive);
    }

    private bool ToggleExternalLock()
    {
        if (!combatActive || externalTarget == null || externalTarget.IsDead) return false;
        externalTargetLocked = !externalTargetLocked;
        RefreshExternalLockIndicator();
        RefreshLockedEnemyStrafeBinding();
        return true;
    }

    public bool TryAdvanceManualLock()
    {
        if (AllowsFreeAttacks) return ToggleWorldLock(true);
        if (externalTarget == null)
            return HasLockedCombatTarget ? TrySwitchEnemyLock() : TryToggleManualLock();
        if (!combatActive || externalTarget.IsDead) return false;
        externalTargetLocked = true;
        RefreshExternalLockIndicator();
        RefreshLockedEnemyStrafeBinding();
        return true;
    }

    public bool TryReleaseManualLock()
    {
        if (AllowsFreeAttacks) return HasLockedCombatTarget && ToggleWorldLock(false);
        if (externalTarget == null) return lockedEnemy != null && TryToggleManualLock();
        if (!externalTargetLocked) return false;
        externalTargetLocked = false;
        RefreshExternalLockIndicator();
        RefreshLockedEnemyStrafeBinding();
        return true;
    }

    public void SuspendCombatTarget(bool suspended) => CombatTarget?.SetCinematicSuspended(suspended);

    private int ApplyExternalSkillDamage(SkillSO skill)
    {
        if (!combatActive || IsPlayerDead() || skill == null || !HasLockedCombatTarget || externalTarget.IsDead) return 0;
        if (!currentSkillContact.HasValue)
        {
            if (!TryGetLockedEnemyHitDistance(out float distance) || !skill.IsWithinHitRange(distance)) return 0;
            Vector3 delta = Vector3.ProjectOnPlane(externalTarget.Root.position - playerRoot.position, Vector3.up);
            if (Mathf.Abs(externalTarget.Root.position.y - playerRoot.position.y) > 1.8f ||
                Vector3.Angle(playerRoot.forward, delta) > skill.enemyImpact.arcDegrees * .5f || Physics.Linecast(playerRoot.position + Vector3.up,
                externalTarget.Root.position + Vector3.up, LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction"), QueryTriggerInteraction.Ignore)) return 0;
        }
        if (ExternalImpactArbitrator != null && ExternalImpactArbitrator(skill, () => CompleteExternalSkillImpact(skill, true))) return 0;
        return CompleteExternalSkillImpact(skill);
    }

    private int CompleteExternalSkillImpact(SkillSO skill, bool deferred = false)
    {
        if (!combatActive || externalTarget == null || externalTarget.IsDead || IsCinematicSequenceActive) return 0;
        int applied = DeliverExternalImpact(new CombatImpact(Mathf.RoundToInt(skill.Damages), skill.interruptionForce, CombatImpactOrigin.Basic));
        if (applied <= 0) return 0;
        if (skill.stunSeconds > 0f && externalTarget is ICombatStunReceiver stunReceiver)
            stunReceiver.TryApplySkillStun(skill.stunSeconds);
        CombatDamageWorldFeedback.Show(externalTarget.Root, applied, Color.white, 2);
        AddClarity(skill.ClarityGainOnHit);
        PlayerLightDamageApplied?.Invoke(applied);
        PlayerSkillImpactApplied?.Invoke(skill, applied);
        if (deferred)
        {
            playerActionPresentation?.HandleConfirmedExternalImpact(skill, externalTarget.LockPoint);
            playerActionPresentation?.HandleConfirmedSkillRetreat(skill, externalTarget.LockPoint);
        }
        EvaluateCombatOutcome();
        return applied;
    }

    private int DeliverExternalImpact(CombatImpact impact)
    {
        return DeliverExternalImpact(CombatTarget, impact);
    }

    private int DeliverExternalImpact(ICombatTarget target, CombatImpact impact)
    {
        if (target == null || target.IsDead) return 0;
        if (!(target is LegacyCombatTarget) && impact.Origin != CombatImpactOrigin.Passive && !impact.DamageModifiersApplied)
            impact = new CombatImpact(EnemyRevelation.ModifyDamage(target.Root, impact.Damage), impact.Interruption, impact.Origin, true,
                impact.Source != null ? impact.Source : playerController);
        else if (impact.Source == null)
            impact = new CombatImpact(impact.Damage, impact.Interruption, impact.Origin, impact.DamageModifiersApplied, playerController);
        return target is ICombatImpactReceiver receiver ? receiver.ReceiveImpact(impact) : target.ReceiveDamage(impact.Damage);
    }
}
