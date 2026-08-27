using System.Collections;
using UnityEngine;

/// <summary>
/// Final safety net for an authored enemy attack. Animation Events remain the
/// normal completion authority; this component only repairs a clip that missed
/// EndEnemyAttack so an encounter cannot deadlock after one action.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatEnemy))]
public sealed class EnemyAttackRecoverySafety : MonoBehaviour
{
    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private CombatEnemyPhysicsMotor physicsMotor;
    [SerializeField] private EnemySkills enemySkills;
    [SerializeField, Min(.1f)] private float extraRecoverySeconds = 1f;
    [SerializeField] private bool logDiagnostics;

    private Coroutine safetyRoutine;

    private void Reset()
    {
        enemy = GetComponent<RealTimeCombatEnemy>();
        physicsMotor = GetComponent<CombatEnemyPhysicsMotor>();
        enemySkills = GetComponent<EnemySkills>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (enemy != null) enemy.RetaliationStarted += OnRetaliationStarted;
    }

    private void OnDisable()
    {
        if (enemy != null) enemy.RetaliationStarted -= OnRetaliationStarted;
        CancelSafety();
    }

    private void OnRetaliationStarted(SkillSO skill, int _)
    {
        CancelSafety();
        if (skill == null || skill.AnimationClip == null)
        {
            return;
        }

        safetyRoutine = StartCoroutine(WatchAttack(skill, skill.AnimationClip.length + extraRecoverySeconds));
        if (skill.EnemyActionMotion != null && skill.EnemyActionMotion.IsAirborne)
        {
            StartCoroutine(ReportAirborneCheckpoint(skill));
        }
    }

    private IEnumerator ReportAirborneCheckpoint(SkillSO skill)
    {
        yield return new WaitForSeconds(.22f);
        if (enemy == null || enemy.ActiveSkill != skill || physicsMotor == null)
        {
            yield break;
        }

        if (!physicsMotor.IsAirborne)
        {
            Debug.LogWarning("[EnemyAttackSafety] '" + skill.SkillName + "' n'est pas aerien apres son evenement de decollage sur " + name +
                             ". Verifier BeginEnemyAirborne et l'Animator en Always Animate.", this);
        }
    }

    private IEnumerator WatchAttack(SkillSO skill, float timeout)
    {
        yield return new WaitForSeconds(Mathf.Max(.25f, timeout));
        if (enemy == null || enemy.ActiveSkill != skill || (enemy.Health != null && enemy.Health.IsDead))
        {
            yield break;
        }

        Debug.LogWarning("[EnemyAttackSafety] Fin d'attaque manquante pour '" + skill.SkillName + "' sur " + name + ". Recuperation forcee.", this);
        enemy.CompleteEnemyAttackWhenGrounded(CompleteFallback);
    }

    private void CompleteFallback()
    {
        if (enemy == null || enemy.ActiveSkill == null)
        {
            return;
        }

        RealTimeCombatManager.Instance?.CompleteEnemyAttack(enemy);
        GetComponent<RealTimeCombatEnemyBehaviour>()?.NotifyAttackCompleted();
        enemySkills?.ReturnToIdle();
        if (logDiagnostics)
        {
            Debug.Log("[EnemyAttackSafety] Recuperation terminee pour " + name + ".", this);
        }
    }

    private void CancelSafety()
    {
        if (safetyRoutine != null)
        {
            StopCoroutine(safetyRoutine);
            safetyRoutine = null;
        }
    }

    private void ResolveReferences()
    {
        enemy ??= GetComponent<RealTimeCombatEnemy>();
        physicsMotor ??= GetComponent<CombatEnemyPhysicsMotor>();
        enemySkills ??= GetComponent<EnemySkills>();
        if (enemy != null && enemy.Animator != null)
        {
            enemy.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }
}
