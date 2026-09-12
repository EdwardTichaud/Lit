using System;
using UnityEngine;

public sealed partial class EnemyController
{
    private const string ActorLegacyHitAnimatorState = "Countered";
    private const string ActorDefaultHitAnimatorState = "Hit";
    private CharacterInfo ActorHealth;
    private CharacterAnimationController ActorAnimationContract;
    [SerializeField]
    private Animator ActorAnimator;
    private EnemyController ActorEnemySkills;
    private EnemyController ActorPhysicsMotor;
    private CombatLockOutline ActorLockOutline;
    private CombatLockIndicator ActorLockIndicator;
    private string ActorHitAnimatorState { get => Configuration.ActorHitAnimatorState; set => Configuration.ActorHitAnimatorState = value; }
    private float ActorHitAnimationTransitionSeconds { get => Configuration.ActorHitAnimationTransitionSeconds; set => Configuration.ActorHitAnimationTransitionSeconds = value; }
    private string ActorIdleAnimatorState { get => Configuration.ActorIdleAnimatorState; set => Configuration.ActorIdleAnimatorState = value; }
    private float ActorHitRecoveryNormalizedTime { get => Configuration.ActorHitRecoveryNormalizedTime; set => Configuration.ActorHitRecoveryNormalizedTime = value; }
    private float ActorHitRecoveryTransitionSeconds { get => Configuration.ActorHitRecoveryTransitionSeconds; set => Configuration.ActorHitRecoveryTransitionSeconds = value; }
    private string ActorDeathAnimatorState { get => Configuration.ActorDeathAnimatorState; set => Configuration.ActorDeathAnimatorState = value; }
    private float ActorDeathAnimationTransitionSeconds { get => Configuration.ActorDeathAnimationTransitionSeconds; set => Configuration.ActorDeathAnimationTransitionSeconds = value; }
    [SerializeField, Tooltip("Point vise par la camera de lock. L'enfant EnemyLockPoint est resolu automatiquement.")]
    private Transform ActorEnemyLockPoint;
    private float ActorRetaliationDelaySeconds { get => Configuration.ActorRetaliationDelaySeconds; set => Configuration.ActorRetaliationDelaySeconds = value; }
    private int ActorStoredMaximumLightDamage;
    // Persists for the whole encounter. The temporary stored value is consumed
    // when an attack starts; this value is what rearms the next attack.
    private int ActorEngagementMaximumLightDamage;
    private int ActorCommittedRetaliationDamage;
    private SkillSO ActorActiveSkill;
    private SkillSO ActorPlannedRetaliationSkill;
    private float ActorRetaliationReadyAt;
    private bool ActorDeathAnimationPlayed;
    private bool ActorDeathAnimationPendingGrounding;
    private Coroutine ActorHitRecoveryRoutine;
    private bool ActorRestoreRootMotionAfterHitPending;
    public event Action<int> LightAbsorbed;
    public event Action<SkillSO, int> RetaliationStarted;
    public CharacterInfo Health => ActorHealth != null ? ActorHealth : GetComponent<CharacterInfo>();
    public override Animator Animator => ActorAnimator != null ? ActorAnimator : GetComponent<Animator>();
    public override Transform LockPoint => ActorResolveLockPoint();
    public bool CanSeePlayer { get; private set; }

    public float PlayerVisibilityDistance { get; private set; }

    public float PlayerVisibilityAngle { get; private set; }

    public string PlayerVisibilityReason { get; private set; } = "non evalue";
    public int StoredMaximumLightDamage => ActorStoredMaximumLightDamage;
    public int EngagementMaximumLightDamage => ActorEngagementMaximumLightDamage;
    public bool IsHitRecovering => ActorHitRecoveryRoutine != null;
    public int CommittedRetaliationDamage => ActorCommittedRetaliationDamage;
    public SkillSO ActiveSkill => ActorActiveSkill;
    public int ActionSequenceId { get; private set; }

    public bool HasRetaliationPending => ActorActiveSkill != null;
    public bool IsAttackCommitted => ActorActiveSkill != null || GetComponent<EnemyController>()?.IsAutonomousActionActive == true;
    public bool HasStoredLightDamage => ActorStoredMaximumLightDamage > 0;
    public bool IsRetaliationReady => ActorActiveSkill == null && ActorStoredMaximumLightDamage > 0 && BrainNow >= ActorRetaliationReadyAt;


    private void ActorAwake()
    {
        ActorMigrateLegacyHitState();
        ActorAnimationContract = this;
        ActorLockIndicator = GetComponent<CombatLockIndicator>();
        if (ActorHealth == null)
        {
            ActorHealth = GetComponent<CharacterInfo>();
        }

        if (ActorEnemySkills == null)
        {
            ActorEnemySkills = GetComponent<EnemyController>();
        }

        if (ActorPhysicsMotor == null)
        {
            ActorPhysicsMotor = GetComponent<EnemyController>();
        }

        ActorAnimator = ActorResolveCombatAnimator();
        ActorResolveLockPoint();
        if (!TryGetComponent<EnemyDeathPresentation>(out _))
            gameObject.AddComponent<EnemyDeathPresentation>();
    }

    private void ActorOnDisable()
    {
        CombatHealthThresholdController.Instance?.EndEnemyReactionAction(this);
        CancelHitRecovery();
    }

#if UNITY_EDITOR


#endif


    /// <summary>
    /// Refreshes visibility on demand so the AI never makes a decision from
    /// the previous frame's value. This is intentionally kept here as the
    /// single visibility authority for both the modern and legacy executors.
    /// </summary>
    public bool RefreshPlayerVisibility()
    {
        Transform player = LocalPlayerContext.LocalCharacterRoot;
        if (player == null)
        {
            player = RealTimeCombatManager.Instance != null ? RealTimeCombatManager.Instance.PlayerRoot : null;
        }

        CanSeePlayer = TryEvaluate(player, out float distance, out float angle, out string reason);
        PlayerVisibilityDistance = distance;
        PlayerVisibilityAngle = angle;
        PlayerVisibilityReason = player == null ? "joueur absent" : reason;
        return CanSeePlayer;
    }

    public int ReceiveLightDamage(int amount, SquadCharacterController source = null)
    {
        return ReceiveDamage(amount, true, source);
    }

    public int ReceiveDamage(int amount, bool canPrepareRetaliation = false, SquadCharacterController source = null)
    {
        if (!CombatEnabled) return 0;
        EnemyController brain = GetComponent<EnemyController>();
        if (brain != null && brain.HasProfile)
            amount = brain.ResolveGuardDamage(amount);
        CombatHealthThresholdController thresholds = CombatHealthThresholdController.Instance;
        if (thresholds != null && thresholds.BlocksEnemyActions(this))
        {
            return 0;
        }

        int allowedDamage = amount;
        bool thresholdReached = thresholds != null && thresholds.TryPrepareDamage(this, amount, out allowedDamage);
        int applied = ActorHealth != null ? ActorHealth.ApplyDamage(thresholdReached ? allowedDamage : amount) : Mathf.Max(0, thresholdReached ? allowedDamage : amount);
        if (applied <= 0)
        {
            return 0;
        }

        CombatDamageWorldFeedback.Show(transform, applied, new Color(0.62f, 0.92f, 1f), 2.15f);
        if (ActorHealth != null && ActorHealth.IsDead)
        {
            ActorStoredMaximumLightDamage = 0;
            CompleteRetaliation();
            PlayDeathAnimation();
            return applied;
        }

        if (canPrepareRetaliation)
        {
            ActorEngagementMaximumLightDamage = Mathf.Max(ActorEngagementMaximumLightDamage, applied);
            brain?.RegisterThreat(source, applied);
        }

        if (thresholdReached)
        {
            thresholds.NotifyThresholdDamageApplied(this);
            return applied;
        }

        if (canPrepareRetaliation)
        {
            ActorEngagementMaximumLightDamage = Mathf.Max(ActorEngagementMaximumLightDamage, applied);
            if (ActorActiveSkill == null)
            {
                ActorStoreLightDamageForRetaliation(applied);
            }
        }

        // Une attaque deja engagee conserve sa priorite. Interrompre un clip
        // root (par exemple Assomoir) avec Hit coupe son root motion et peut
        // laisser l'ennemi suspendu dans sa pose de saut.
        if (ActorActiveSkill == null)
        {
            PlayHitAnimation();
        }

        return applied;
    }

    /// <summary>Used only by a successful authored health-threshold QTE.</summary>
    public bool ForceDefeatFromThreshold()
    {
        if (ActorHealth == null)
        {
            return false;
        }

        if (!ActorHealth.IsDead)
        {
            ActorHealth.ForceDefeat();
        }

        ActorStoredMaximumLightDamage = 0;
        ActorEngagementMaximumLightDamage = 0;
        CompleteRetaliation();
        PlayDeathAnimation();
        return ActorHealth.IsDead;
    }

    public bool TryStartRetaliation(float meleePreference = 0.5f)
    {
        if (!CombatEnabled || !BrainAuthority) return false;
        if (CombatHealthThresholdController.Instance != null && CombatHealthThresholdController.Instance.BlocksEnemyActions(this))
        {
            return false;
        }

        if (!IsRetaliationReady)
        {
            return false;
        }

        EnemyController runtimeContract = GetComponent<EnemyController>();
        if (runtimeContract != null && !runtimeContract.CanRunCombat)
        {
            Debug.LogWarning("[RealTimeCombatEnemy] Riposte refusee pour '" + name + "' : contrat runtime ou moteur physique indisponible.", this);
            return false;
        }

        if (ActorPhysicsMotor == null || !ActorPhysicsMotor.IsOperational)
        {
            Debug.LogWarning("[RealTimeCombatEnemy] Riposte refusee pour '" + name + "' : CombatEnemyPhysicsMotor absent ou non operationnel.", this);
            return false;
        }

        ActorActiveSkill = PeekRetaliationSkill(meleePreference);
        ActionSequenceId++;
        if (ActorActiveSkill == null || ActorEnemySkills == null || !ActorEnemySkills.SetActiveSkill(ActorActiveSkill))
        {
            ActorActiveSkill = null;
            ActorStoredMaximumLightDamage = ActorEngagementMaximumLightDamage;
            ActorRetaliationReadyAt = BrainNow + ActorRetaliationDelaySeconds;
            return false;
        }

        ActorPlannedRetaliationSkill = null;
        ActorPhysicsMotor?.BeginEnemyAction(ActorActiveSkill);
        ActorCommittedRetaliationDamage = Mathf.CeilToInt(ActorEngagementMaximumLightDamage * Mathf.Max(0f, ActorActiveSkill.EnemyDamageMultiplier));
        ActorStoredMaximumLightDamage = 0;
        bool cinematicStarted = ActorActiveSkill.HasCombatCinematic && RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.TryPlayEnemySkillCinematic(this, ActorActiveSkill);
        if (!cinematicStarted && !ActorEnemySkills.PlayActiveSkill())
        {
            ActorActiveSkill = null;
            ActorCommittedRetaliationDamage = 0;
            ActorStoredMaximumLightDamage = ActorEngagementMaximumLightDamage;
            ActorRetaliationReadyAt = BrainNow + ActorRetaliationDelaySeconds;
            return false;
        }

        RetaliationStarted?.Invoke(ActorActiveSkill, ActorCommittedRetaliationDamage);
        return true;
    }

    /// <summary>
    /// Starts the authored retaliation used after a failed health-threshold
    /// QTE. It intentionally preserves the encounter ledger so the regular AI
    /// can rearm after this one-off response has completed.
    /// </summary>
    public bool TryStartThresholdFailureRetaliation(SkillSO skill)
    {
        if (skill == null || ActorActiveSkill != null || ActorEnemySkills == null || !ActorEnemySkills.SetActiveSkill(skill))
        {
            return false;
        }

        if (ActorPhysicsMotor != null && !ActorPhysicsMotor.IsOperational)
        {
            return false;
        }

        ActorActiveSkill = skill;
        ActionSequenceId++;
        ActorPlannedRetaliationSkill = null;
        ActorCommittedRetaliationDamage = Mathf.Max(0, Mathf.RoundToInt(skill.Damages));
        ActorPhysicsMotor?.BeginEnemyAction(skill);
        if (!ActorEnemySkills.PlayActiveSkill())
        {
            ActorActiveSkill = null;
            ActorCommittedRetaliationDamage = 0;
            return false;
        }

        RetaliationStarted?.Invoke(ActorActiveSkill, ActorCommittedRetaliationDamage);
        return true;
    }

    /// <summary>Starts an autonomous attack chosen by EnemyCombatBrain. It deliberately bypasses the legacy retaliation ledger.</summary>
    public bool TryStartAutonomousAttack(SkillSO skill)
    {
        if (skill == null || ActorActiveSkill != null || ActorEnemySkills == null || !ActorEnemySkills.SetActiveSkill(skill) || ActorHealth != null && ActorHealth.IsDead)
        {
            return false;
        }

        if (ActorPhysicsMotor == null || !ActorPhysicsMotor.IsOperational || ActorPhysicsMotor.State != CombatEnemyPhysicsState.Navigation)
        {
            return false;
        }

        ActorActiveSkill = skill;
        ActionSequenceId++;
        ActorPlannedRetaliationSkill = null;
        // A visible player is enough to start an encounter. Before the first
        // player hit there is no light-damage ledger, so use the skill's
        // authored damage as the neutral attack basis. Once a hit is received,
        // engagementMaximumLightDamage takes over and remains persistent.
        float damageBasis = ActorEngagementMaximumLightDamage > 0 ? ActorEngagementMaximumLightDamage : Mathf.Max(0f, skill.Damages);
        ActorCommittedRetaliationDamage = Mathf.Max(1, Mathf.CeilToInt(damageBasis * Mathf.Max(0f, skill.EnemyDamageMultiplier)));
        ActorPhysicsMotor?.BeginEnemyAction(skill);
        if (!ActorEnemySkills.PlayActiveSkill())
        {
            ActorActiveSkill = null;
            ActorCommittedRetaliationDamage = 0;
            ActorPhysicsMotor.InterruptEnemyAction(null);
            return false;
        }

        RetaliationStarted?.Invoke(skill, ActorCommittedRetaliationDamage);
        return true;
    }

    /// <summary>Completes an autonomous action without rearming legacy retaliation damage.</summary>
    public void CompleteAutonomousAttack()
    {
        ActorActiveSkill = null;
        ActorPlannedRetaliationSkill = null;
        ActorCommittedRetaliationDamage = 0;
    }

    public SkillSO PeekRetaliationSkill(float meleePreference = 0.5f)
    {
        if (!IsRetaliationReady)
        {
            return null;
        }

        if (ActorPlannedRetaliationSkill != null)
        {
            return ActorPlannedRetaliationSkill;
        }

        ActorPlannedRetaliationSkill = ActorEnemySkills != null ? ActorEnemySkills.ChooseRetaliationSkill(meleePreference) : null;
        return ActorPlannedRetaliationSkill;
    }

    public void CompleteRetaliation()
    {
        ActorActiveSkill = null;
        ActorPlannedRetaliationSkill = null;
        ActorCommittedRetaliationDamage = 0;
        ActorStoredMaximumLightDamage = 0;
        ActorEngagementMaximumLightDamage = 0;
        ActorRetaliationReadyAt = 0f;
    }

    /// <summary>
    /// Ends the active attack and rearms the next one with the strongest light
    /// damage received during the current engagement. Called from the attack-end
    /// Animation Event only.
    /// </summary>
    public void CompleteRetaliationAndPrepareNext()
    {
        ActorActiveSkill = null;
        ActorPlannedRetaliationSkill = null;
        ActorCommittedRetaliationDamage = 0;
        if (ActorEngagementMaximumLightDamage <= 0)
        {
            return;
        }

        ActorStoredMaximumLightDamage = ActorEngagementMaximumLightDamage;
        ActorRetaliationReadyAt = BrainNow + ActorRetaliationDelaySeconds;
    }

    public void CompleteEnemyAttackWhenGrounded(Action onGrounded)
    {
        if (ActorPhysicsMotor == null)
        {
            onGrounded?.Invoke();
            return;
        }

        ActorPhysicsMotor.CompleteEnemyAction(onGrounded);
    }

    public void BeginEnemyAirborne()
    {
        ActorPhysicsMotor?.PhysicsBeginEnemyAirborne();
    }

    public void RequestEnemyLanding()
    {
        ActorPhysicsMotor?.PhysicsRequestEnemyLanding();
    }

    public void BeginEnemyRush(Transform target)
    {
        ActorPhysicsMotor?.PhysicsBeginEnemyRush(target);
    }

    public void EndEnemyRush()
    {
        ActorPhysicsMotor?.PhysicsEndEnemyRush();
    }

    /// <summary>
    /// Moves an active enemy action without bypassing its physics motor. Vertical
    /// position remains exclusively controlled by the motor.
    /// </summary>
    public void SetActionPlanarPosition(Vector3 position)
    {
        if (ActorPhysicsMotor == null)
        {
            ActorPhysicsMotor = GetComponent<EnemyController>();
        }

        if (ActorPhysicsMotor != null)
        {
            ActorPhysicsMotor.PhysicsSetActionPlanarPosition(position);
            return;
        }

        Vector3 currentPosition = transform.position;
        currentPosition.x = position.x;
        currentPosition.z = position.z;
        transform.position = currentPosition;
    }

    public void SetLockPresentation(bool locked, bool playSound)
    {
        if (ActorLockOutline == null)
        {
            ActorLockOutline = GetComponent<CombatLockOutline>();
            if (ActorLockOutline == null)
            {
                ActorLockOutline = gameObject.AddComponent<CombatLockOutline>();
            }
        }

        ActorLockOutline.SetLocked(locked);
        ActorLockIndicator?.SetLocked(false, false);
        if (locked && playSound)
        {
            ActorLockIndicator?.PlayLockSound();
        }
    }

    public void PlayHitAnimation()
    {
        if (IsAttackCommitted || ActorDeathAnimationPlayed || (ActorHealth != null && ActorHealth.IsDead) || ActorAnimator == null || string.IsNullOrWhiteSpace(ActorHitAnimatorState))
        {
            return;
        }

        if (ActorHitRecoveryRoutine != null)
        {
            StopCoroutine(ActorHitRecoveryRoutine);
        }

        ActorPhysicsMotor?.AuditPose("Hit:avant CrossFade");
        if (!ActorRestoreRootMotionAfterHitPending)
        {
            ActorRestoreRootMotionAfterHitPending = ActorAnimator.applyRootMotion;
            ActorAnimator.applyRootMotion = false;
        }

        ActorAnimator.CrossFade(ActorHitAnimatorState, ActorHitAnimationTransitionSeconds, 0);
        ActorPhysicsMotor?.AuditPose("Hit:apres CrossFade");
        ActorHitRecoveryRoutine = StartCoroutine(ActorRecoverFromHit());
    }

    public void CancelHitRecovery()
    {
        if (ActorHitRecoveryRoutine != null)
        {
            StopCoroutine(ActorHitRecoveryRoutine);
            ActorHitRecoveryRoutine = null;
        }

        ActorRestoreRootMotionAfterHit();
    }

    public void PlayDeathAnimation()
    {
        if (ActorDeathAnimationPlayed || ActorAnimator == null || string.IsNullOrWhiteSpace(ActorDeathAnimatorState))
        {
            return;
        }

        if (ActorPhysicsMotor != null && ActorPhysicsMotor.IsDrivingActionRootMotion)
        {
            if (ActorDeathAnimationPendingGrounding)
            {
                return;
            }

            ActorDeathAnimationPendingGrounding = true;
            ActorPhysicsMotor.InterruptEnemyAction(ActorPlayDeathAnimationAfterGrounding);
            return;
        }

        ActorPlayDeathAnimationAfterGrounding();
    }

    private void ActorPlayDeathAnimationAfterGrounding()
    {
        ActorDeathAnimationPendingGrounding = false;
        if (ActorDeathAnimationPlayed || ActorAnimator == null || string.IsNullOrWhiteSpace(ActorDeathAnimatorState))
        {
            return;
        }

        ActorDeathAnimationPlayed = true;
        CancelHitRecovery();
        ActorAnimator.CrossFade(ActorDeathAnimatorState, ActorDeathAnimationTransitionSeconds, 0);
    }

    public void ReturnToIdleAnimation()
    {
        if (ActorDeathAnimationPlayed || (ActorHealth != null && ActorHealth.IsDead) || ActorAnimator == null || string.IsNullOrWhiteSpace(ActorIdleAnimatorState))
        {
            return;
        }

        ActorAnimator.CrossFade(ActorIdleAnimatorState, ActorHitRecoveryTransitionSeconds, 0);
    }

    private System.Collections.IEnumerator ActorRecoverFromHit()
    {
        yield return null;
        if (ActorAnimator == null || ActorDeathAnimationPlayed)
        {
            ActorRestoreRootMotionAfterHit();
            ActorHitRecoveryRoutine = null;
            yield break;
        }

        AnimatorStateInfo hitState = ActorAnimator.GetCurrentAnimatorStateInfo(0);
        int hitStateHash = Animator.StringToHash(ActorHitAnimatorState);
        if (hitState.shortNameHash != hitStateHash)
        {
            ActorRestoreRootMotionAfterHit();
            ActorHitRecoveryRoutine = null;
            yield break;
        }

        float animationSpeed = Mathf.Max(0.01f, Mathf.Abs(ActorAnimator.speed * hitState.speed));
        float waitSeconds = Mathf.Max(0.01f, hitState.length * ActorHitRecoveryNormalizedTime / animationSpeed);
        yield return new WaitForSeconds(waitSeconds);
        if (ActorAnimator != null && !ActorDeathAnimationPlayed && ActorAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == hitStateHash && !string.IsNullOrWhiteSpace(ActorIdleAnimatorState))
        {
            ReturnToIdleAnimation();
        }

        yield return null;
        ActorRestoreRootMotionAfterHit();
        ActorPhysicsMotor?.AuditPose("Hit:fin");
        ActorHitRecoveryRoutine = null;
    }

    private void ActorRestoreRootMotionAfterHit()
    {
        if (!ActorRestoreRootMotionAfterHitPending || ActorAnimator == null)
        {
            return;
        }

        ActorAnimator.applyRootMotion = true;
        ActorRestoreRootMotionAfterHitPending = false;
    }

    private void ActorStoreLightDamageForRetaliation(int damage)
    {
        ActorStoredMaximumLightDamage = Mathf.Max(ActorStoredMaximumLightDamage, ActorEngagementMaximumLightDamage, damage);
        ActorRetaliationReadyAt = BrainNow + ActorRetaliationDelaySeconds;
        LightAbsorbed?.Invoke(damage);
    }

    private Animator ActorResolveCombatAnimator() => GetComponent<Animator>();

    private Transform ActorResolveLockPoint()
    {
        if (ActorEnemyLockPoint != null)
        {
            return ActorEnemyLockPoint;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && (candidate.name == "EnemyLockPoint" || candidate.name == "EnemyPointLock"))
            {
                ActorEnemyLockPoint = candidate;
                break;
            }
        }

        return ActorEnemyLockPoint != null ? ActorEnemyLockPoint : transform;
    }

    private void ActorMigrateLegacyHitState()
    {
        if (ActorHitAnimatorState == ActorLegacyHitAnimatorState)
        {
            ActorHitAnimatorState = ActorDefaultHitAnimatorState;
        }
    }
}
