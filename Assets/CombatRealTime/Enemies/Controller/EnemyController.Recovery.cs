using System.Collections;
using UnityEngine;

public sealed partial class EnemyController
{
    [SerializeField]
    private EnemyController RecoveryEnemy;
    [SerializeField]
    private EnemyController RecoveryPhysicsMotor;
    [SerializeField]
    private EnemyController RecoveryEnemySkills;
    private float RecoveryExtraRecoverySeconds { get => Configuration.RecoveryExtraRecoverySeconds; set => Configuration.RecoveryExtraRecoverySeconds = value; }
    private bool RecoveryLogDiagnostics { get => Configuration.RecoveryLogDiagnostics; set => Configuration.RecoveryLogDiagnostics = value; }
    private Coroutine RecoverySafetyRoutine;


    private void RecoveryAwake()
    {
        RecoveryResolveReferences();
    }

    private void RecoveryOnEnable()
    {
        RecoveryResolveReferences();
        if (RecoveryEnemy != null)
            { RecoveryEnemy.RetaliationStarted -= RecoveryOnRetaliationStarted; RecoveryEnemy.RetaliationStarted += RecoveryOnRetaliationStarted; }
    }

    private void RecoveryOnDisable()
    {
        if (RecoveryEnemy != null)
            RecoveryEnemy.RetaliationStarted -= RecoveryOnRetaliationStarted;
        RecoveryCancelSafety();
    }

    private void RecoveryOnRetaliationStarted(SkillSO skill, int _)
    {
        RecoveryCancelSafety();
        if (skill == null || skill.AnimationClip == null) return;
        RecoverySafetyRoutine = StartCoroutine(RecoveryWatchAttack(ActionSequenceId, skill.AnimationClip.length + RecoveryExtraRecoverySeconds));
    }

    private IEnumerator RecoveryWatchAttack(int sequence, float timeout)
    {
        float elapsed = 0f;
        while (elapsed < Mathf.Max(.25f, timeout))
        {
            if (sequence != ActionSequenceId || ActiveSkill == null || Health.IsDead) yield break;
            if (!IsSuspended && CombatEnabled) elapsed += TimeDomain != null ? TimeDomain.DeltaTime : Time.deltaTime;
            yield return null;
        }
        if (!BrainAuthority || sequence != ActionSequenceId || ActiveSkill == null) yield break;
        Debug.LogWarning("[EnemyController] Evenement de fin d'attaque manquant sur " + name + ". Recuperation apres retour au sol.", this);
        ForceCompleteEnemyAction(() => CompleteAction(sequence, true), "watchdog");
    }

    private void RecoveryCancelSafety()
    {
        if (RecoverySafetyRoutine != null)
        {
            StopCoroutine(RecoverySafetyRoutine);
            RecoverySafetyRoutine = null;
        }
    }

    private void RecoveryResolveReferences()
    {
        RecoveryEnemy ??= GetComponent<EnemyController>();
        RecoveryPhysicsMotor ??= GetComponent<EnemyController>();
        RecoveryEnemySkills ??= GetComponent<EnemyController>();
        if (RecoveryEnemy != null && RecoveryEnemy.Animator != null)
        {
            RecoveryEnemy.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }
}