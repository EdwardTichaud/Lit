using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RealTimeCombatEnemy : MonoBehaviour
{
    private const string LegacyHitAnimatorState = "Countered";
    private const string DefaultHitAnimatorState = "Hit";

    [SerializeField] private CombatHealth health;
    [SerializeField] private Animator animator;
    [SerializeField] private VisionField visionField;
    [SerializeField] private EnemySkills enemySkills;
    [SerializeField] private CombatLockOutline lockOutline;
    [SerializeField] private CombatLockIndicator lockIndicator;
    [SerializeField] private string hitAnimatorState = "Hit";
    [SerializeField, Min(0f)] private float hitAnimationTransitionSeconds = 0.06f;
    [SerializeField] private string idleAnimatorState = "Idle";
    [SerializeField, Range(0.5f, 1f), Tooltip("Part de l'animation Hit jouee avant le retour fluide vers Idle.")]
    private float hitRecoveryNormalizedTime = 0.9f;
    [SerializeField, Min(0f)] private float hitRecoveryTransitionSeconds = 0.1f;
    [SerializeField] private string deathAnimatorState = "Death";
    [SerializeField, Min(0f)] private float deathAnimationTransitionSeconds = 0.08f;
    [SerializeField, Tooltip("Point vise par la camera de lock. L'enfant EnemyLockPoint est resolu automatiquement.")]
    private Transform enemyLockPoint;
    [SerializeField, Min(0f)] private float retaliationDelaySeconds = 0.15f;

    private int storedMaximumLightDamage;
    private int committedRetaliationDamage;
    private SkillSO activeSkill;
    private SkillSO plannedRetaliationSkill;
    private float retaliationReadyAt;
    private bool deathAnimationPlayed;
    private Coroutine hitRecoveryRoutine;
    private bool restoreRootMotionAfterHit;

    public event Action<int> LightAbsorbed;
    public event Action<SkillSO, int> RetaliationStarted;
    public CombatHealth Health => health;
    public Animator Animator => animator;
    public VisionField VisionField
    {
        get
        {
            if (visionField == null)
            {
                visionField = GetComponent<VisionField>();
            }

            return visionField;
        }
    }
    public Transform LockPoint => ResolveLockPoint();
    public bool CanSeePlayer { get; private set; }
    public int StoredMaximumLightDamage => storedMaximumLightDamage;
    public int CommittedRetaliationDamage => committedRetaliationDamage;
    public SkillSO ActiveSkill => activeSkill;
    public bool HasRetaliationPending => activeSkill != null;
    public bool HasStoredLightDamage => storedMaximumLightDamage > 0;
    public bool IsRetaliationReady => activeSkill == null && storedMaximumLightDamage > 0 && Time.time >= retaliationReadyAt;

    private void Reset()
    {
        health = GetComponent<CombatHealth>();
        animator = GetComponentInChildren<Animator>();
        enemySkills = GetComponent<EnemySkills>();
    }

    private void Awake()
    {
        MigrateLegacyHitState();

        if (health == null)
        {
            health = GetComponent<CombatHealth>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (enemySkills == null)
        {
            enemySkills = GetComponent<EnemySkills>();
        }

        ResolveLockPoint();
    }

    private void OnDisable()
    {
        CancelHitRecovery();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MigrateLegacyHitState();
    }
#endif

    private void Update()
    {
        if (visionField == null)
        {
            visionField = GetComponent<VisionField>();
        }

        Transform player = LocalPlayerContext.LocalCharacterRoot;
        CanSeePlayer = visionField != null && player != null && visionField.CanSee(player);
    }

    public int ReceiveLightDamage(int amount)
    {
        return ReceiveDamage(amount, true);
    }

    public int ReceiveDamage(int amount, bool canPrepareRetaliation = false)
    {
        int applied = health != null ? health.ApplyDamage(amount) : Mathf.Max(0, amount);
        if (applied <= 0)
        {
            return 0;
        }

        CombatDamageWorldFeedback.Show(transform, applied, new Color(0.62f, 0.92f, 1f), 2.15f);

        if (health != null && health.IsDead)
        {
            storedMaximumLightDamage = 0;
            CompleteRetaliation();
            PlayDeathAnimation();
            return applied;
        }

        if (canPrepareRetaliation)
        {
            storedMaximumLightDamage = Mathf.Max(storedMaximumLightDamage, applied);
            retaliationReadyAt = Time.time + retaliationDelaySeconds;
            LightAbsorbed?.Invoke(applied);
        }

        // Une attaque deja engagee conserve sa priorite. Interrompre un clip
        // root (par exemple Assomoir) avec Hit coupe son root motion et peut
        // laisser l'ennemi suspendu dans sa pose de saut.
        if (activeSkill == null)
        {
            PlayHitAnimation();
        }

        return applied;
    }

    public bool TryStartRetaliation(float meleePreference = 0.5f)
    {
        if (!IsRetaliationReady)
        {
            return false;
        }

        activeSkill = PeekRetaliationSkill(meleePreference);
        if (activeSkill == null || enemySkills == null || !enemySkills.SetActiveSkill(activeSkill) || !enemySkills.PlayActiveSkill())
        {
            activeSkill = null;
            return false;
        }

        plannedRetaliationSkill = null;
        committedRetaliationDamage = Mathf.CeilToInt(storedMaximumLightDamage * Mathf.Max(0f, activeSkill.EnemyDamageMultiplier));
        storedMaximumLightDamage = 0;

        RetaliationStarted?.Invoke(activeSkill, committedRetaliationDamage);
        return true;
    }

    public SkillSO PeekRetaliationSkill(float meleePreference = 0.5f)
    {
        if (!IsRetaliationReady)
        {
            return null;
        }

        if (plannedRetaliationSkill != null)
        {
            return plannedRetaliationSkill;
        }

        plannedRetaliationSkill = enemySkills != null
            ? enemySkills.ChooseRetaliationSkill(meleePreference)
            : null;
        return plannedRetaliationSkill;
    }

    public void CompleteRetaliation()
    {
        activeSkill = null;
        plannedRetaliationSkill = null;
        committedRetaliationDamage = 0;
    }

    public void SetLockPresentation(bool locked, bool playSound)
    {
        if (lockOutline == null)
        {
            lockOutline = GetComponent<CombatLockOutline>();
            if (lockOutline == null)
            {
                lockOutline = gameObject.AddComponent<CombatLockOutline>();
            }
        }

        lockOutline.SetLocked(locked);
        lockIndicator?.SetLocked(false, false);
        if (locked && playSound)
        {
            lockIndicator?.PlayLockSound();
        }
    }

    public void PlayHitAnimation()
    {
        if (deathAnimationPlayed ||
            (health != null && health.IsDead) ||
            animator == null ||
            string.IsNullOrWhiteSpace(hitAnimatorState))
        {
            return;
        }

        if (hitRecoveryRoutine != null)
        {
            StopCoroutine(hitRecoveryRoutine);
        }

        if (!restoreRootMotionAfterHit)
        {
            restoreRootMotionAfterHit = animator.applyRootMotion;
            animator.applyRootMotion = false;
        }

        animator.CrossFade(hitAnimatorState, hitAnimationTransitionSeconds, 0);
        hitRecoveryRoutine = StartCoroutine(RecoverFromHit());
    }

    public void CancelHitRecovery()
    {
        if (hitRecoveryRoutine != null)
        {
            StopCoroutine(hitRecoveryRoutine);
            hitRecoveryRoutine = null;
        }

        RestoreRootMotionAfterHit();
    }

    public void PlayDeathAnimation()
    {
        if (deathAnimationPlayed || animator == null || string.IsNullOrWhiteSpace(deathAnimatorState))
        {
            return;
        }

        deathAnimationPlayed = true;
        CancelHitRecovery();
        animator.CrossFade(deathAnimatorState, deathAnimationTransitionSeconds, 0);
    }

    public void ReturnToIdleAnimation()
    {
        if (deathAnimationPlayed || (health != null && health.IsDead) || animator == null || string.IsNullOrWhiteSpace(idleAnimatorState))
        {
            return;
        }

        animator.CrossFade(idleAnimatorState, hitRecoveryTransitionSeconds, 0);
    }

    private System.Collections.IEnumerator RecoverFromHit()
    {
        yield return null;

        if (animator == null || deathAnimationPlayed)
        {
            RestoreRootMotionAfterHit();
            hitRecoveryRoutine = null;
            yield break;
        }

        AnimatorStateInfo hitState = animator.GetCurrentAnimatorStateInfo(0);
        int hitStateHash = Animator.StringToHash(hitAnimatorState);
        if (hitState.shortNameHash != hitStateHash)
        {
            RestoreRootMotionAfterHit();
            hitRecoveryRoutine = null;
            yield break;
        }

        float animationSpeed = Mathf.Max(0.01f, Mathf.Abs(animator.speed * hitState.speed));
        float waitSeconds = Mathf.Max(0.01f, hitState.length * hitRecoveryNormalizedTime / animationSpeed);
        yield return new WaitForSeconds(waitSeconds);

        if (animator != null && !deathAnimationPlayed &&
            animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hitStateHash &&
            !string.IsNullOrWhiteSpace(idleAnimatorState))
        {
            ReturnToIdleAnimation();
        }

        yield return null;
        RestoreRootMotionAfterHit();
        hitRecoveryRoutine = null;
    }

    private void RestoreRootMotionAfterHit()
    {
        if (!restoreRootMotionAfterHit || animator == null)
        {
            return;
        }

        animator.applyRootMotion = true;
        restoreRootMotionAfterHit = false;
    }

    private Transform ResolveLockPoint()
    {
        if (enemyLockPoint != null)
        {
            return enemyLockPoint;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && (candidate.name == "EnemyLockPoint" || candidate.name == "EnemyPointLock"))
            {
                enemyLockPoint = candidate;
                break;
            }
        }

        return enemyLockPoint != null ? enemyLockPoint : transform;
    }

    private void MigrateLegacyHitState()
    {
        if (hitAnimatorState == LegacyHitAnimatorState)
        {
            hitAnimatorState = DefaultHitAnimatorState;
        }
    }
}
