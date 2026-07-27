using UnityEngine;

[DisallowMultipleComponent]
public sealed class RealTimeCombatAnimationEvents : MonoBehaviour
{
    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private EnemySkills enemySkills;
    [SerializeField] private SkillsManager skillsManager;
    [SerializeField] private RealTimeCombatInput combatInput;
    [SerializeField] private PlayerBow playerBow;
    [SerializeField] private PlayerSword playerSword;

    [Header("Dash Animation Events")]
    [SerializeField, Min(0f)] private float dashOvershootDistance = 1.25f;
    [SerializeField, Min(0f)] private float dashImpulsePerMeter = 8f;
    [SerializeField, Min(0f)] private float minimumDashImpulse = 14f;
    [SerializeField, Min(0f)] private float maximumDashImpulse = 32f;
    [SerializeField, Min(0f)] private float dashInputLockSeconds = 0.1f;
    [SerializeField, Min(0.01f)] private float stopDashDuration = 0.18f;
    [SerializeField, Min(0f)] private float stopDashDeceleration = 65f;

    private Vector3 lastDashDirection;
    private Coroutine stopDashRoutine;

    private void Reset()
    {
        enemy = GetComponentInParent<RealTimeCombatEnemy>();
        ResolvePlayerBow();
        ResolvePlayerSword();
    }

    private void Awake()
    {
        ResolvePlayerBow();
        ResolvePlayerSword();
        HideBow();
        HideSword();
    }

    private void OnDisable()
    {
        if (stopDashRoutine != null)
        {
            StopCoroutine(stopDashRoutine);
            stopDashRoutine = null;
        }

        HideBow();
        HideSword();
    }

    public void ShowReactionPrompt()
    {
        RealTimeCombatManager.Instance?.BeginEnemyAttackWindow(ResolveEnemy());
    }

    public void OpenReactionWindow()
    {
        RealTimeCombatManager.Instance?.BeginEnemyAttackWindow(ResolveEnemy());
    }

    public void ResolveEnemyAttackImpact()
    {
        RealTimeCombatManager.Instance?.ResolveEnemyAttackImpact(ResolveEnemy());
    }

    public void EndEnemyAttack()
    {
        RealTimeCombatEnemy currentEnemy = ResolveEnemy();
        RealTimeCombatManager.Instance?.CompleteEnemyAttack(currentEnemy);
        ResolveEnemySkills()?.ReturnToIdle();
    }

    /// <summary>
    /// Animation Event joueur : joue tous les VFX de la competence selectionnee.
    /// </summary>
    public void InstantiateSkillVFX()
    {
        SkillSO skill = ResolveSelectedSkill();
        RealTimeCombatEnemy target = RealTimeCombatManager.Instance != null
            ? RealTimeCombatManager.Instance.LockedEnemy
            : null;
        if (skill == null || skill.VfxCues == null)
        {
            return;
        }

        bool canReachTarget = RealTimeCombatManager.Instance != null &&
            RealTimeCombatManager.Instance.IsLockedEnemyWithinSkillHitRange(skill);

        for (int i = 0; i < skill.VfxCues.Count; i++)
        {
            SkillVfxCue cue = skill.VfxCues[i];
            if (cue != null && cue.delivery != SkillVfxDelivery.PlayerHand && !canReachTarget)
            {
                continue;
            }

            PlaySkillVfxCue(cue, target);
        }
    }

    /// <summary>
    /// Animation Event joueur : joue un seul VFX par son index dans SkillSO.VfxCues.
    /// </summary>
    public void InstantiateSkillVFXAtIndex(int cueIndex)
    {
        SkillSO skill = ResolveSelectedSkill();
        RealTimeCombatEnemy target = RealTimeCombatManager.Instance != null
            ? RealTimeCombatManager.Instance.LockedEnemy
            : null;
        if (skill == null || skill.VfxCues == null || cueIndex < 0 || cueIndex >= skill.VfxCues.Count)
        {
            return;
        }

        SkillVfxCue cue = skill.VfxCues[cueIndex];
        bool canReachTarget = RealTimeCombatManager.Instance != null &&
            RealTimeCombatManager.Instance.IsLockedEnemyWithinSkillHitRange(skill);
        if (cue == null || (cue.delivery != SkillVfxDelivery.PlayerHand && !canReachTarget))
        {
            return;
        }

        PlaySkillVfxCue(cue, target);
    }

    /// <summary>
    /// Animation Event optionnel : affiche l'arc pour la competence active.
    /// </summary>
    public void ShowBow()
    {
        ResolvePlayerBow();
        playerBow?.Show();
    }

    /// <summary>
    /// Animation Event optionnel : masque l'arc de la competence active.
    /// </summary>
    public void HideBow()
    {
        ResolvePlayerBow();
        playerBow?.Hide();
    }

    /// <summary>
    /// Animation Event optionnel : affiche l'epee de la competence active.
    /// </summary>
    public void ShowSword()
    {
        ResolvePlayerSword();
        playerSword?.Show();
    }

    /// <summary>
    /// Animation Event optionnel : masque l'epee de la competence active.
    /// </summary>
    public void HideSword()
    {
        ResolvePlayerSword();
        playerSword?.Hide();
    }

    /// <summary>
    /// Animation Event : propulse le joueur vers l'ennemi verrouille et vise un point
    /// situe derriere lui pour permettre une attaque qui traverse la cible.
    /// </summary>
    public void Dash()
    {
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        Transform caster = manager != null ? manager.PlayerRoot : null;
        RealTimeCombatEnemy target = manager != null ? manager.LockedEnemy : null;
        if (caster == null || target == null)
        {
            return;
        }

        Vector3 direction = target.LockPoint.position - caster.position;
        direction.y = 0f;
        float distanceToTarget = direction.magnitude;
        if (distanceToTarget <= 0.001f)
        {
            return;
        }

        lastDashDirection = direction / distanceToTarget;
        float intendedTravelDistance = distanceToTarget + dashOvershootDistance;
        float impulse = Mathf.Clamp(
            intendedTravelDistance * dashImpulsePerMeter,
            minimumDashImpulse,
            maximumDashImpulse);

        LitOpsiveLocomotionBridge bridge = caster.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        if (bridge == null || !bridge.AddExternalImpulse(lastDashDirection * impulse, ForceMode.VelocityChange, dashInputLockSeconds))
        {
            lastDashDirection = Vector3.zero;
        }

        if (stopDashRoutine != null)
        {
            StopCoroutine(stopDashRoutine);
            stopDashRoutine = null;
        }
    }

    /// <summary>
    /// Animation Event : freine progressivement la derniere impulsion de dash.
    /// </summary>
    public void StopDash()
    {
        if (lastDashDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (stopDashRoutine != null)
        {
            StopCoroutine(stopDashRoutine);
        }

        stopDashRoutine = StartCoroutine(StopDashRoutine(lastDashDirection));
    }

    private void PlaySkillVfxCue(SkillVfxCue cue, RealTimeCombatEnemy target)
    {
        if (cue == null)
        {
            return;
        }

        if (cue.delivery == SkillVfxDelivery.PlayerHand)
        {
            ResolvePlayerBow();
            Transform handPoint = playerBow != null
                ? playerBow.transform
                : RealTimeCombatManager.Instance?.PlayerRoot;
            if (handPoint != null)
            {
                PlaySkillVfxCueAudio(cue, handPoint.position);
                if (cue.prefab != null)
                {
                    Instantiate(cue.prefab, handPoint.position, handPoint.rotation, handPoint);
                }
            }

            return;
        }

        if (target == null)
        {
            return;
        }

        if (cue.delivery == SkillVfxDelivery.DirectOnTarget)
        {
            Transform targetPoint = target.LockPoint;
            PlaySkillVfxCueAudio(cue, targetPoint.position);
            if (cue.prefab != null)
            {
                Instantiate(cue.prefab, targetPoint.position, targetPoint.rotation, targetPoint);
            }

            return;
        }

        Transform caster = RealTimeCombatManager.Instance != null
            ? RealTimeCombatManager.Instance.PlayerRoot
            : null;
        if (caster != null)
        {
            PlaySkillVfxCueAudio(cue, caster.position);
            if (cue.prefab != null)
            {
                StartCoroutine(PlayProjectileSkillVfx(cue, caster, target));
            }
        }
    }

    private static void PlaySkillVfxCueAudio(SkillVfxCue cue, Vector3 position)
    {
        if (cue.audioClip != null)
        {
            AudioManager.PlayClipAtPoint(cue.audioClip, position);
        }
    }

    private System.Collections.IEnumerator PlayProjectileSkillVfx(SkillVfxCue cue, Transform caster, RealTimeCombatEnemy target)
    {
        GameObject projectile = Instantiate(cue.prefab, caster.position, caster.rotation, caster);
        if (cue.holdAtCasterSeconds > 0f)
        {
            yield return new WaitForSeconds(cue.holdAtCasterSeconds);
        }

        if (projectile == null || target == null)
        {
            if (projectile != null)
            {
                Destroy(projectile);
            }

            yield break;
        }

        projectile.transform.SetParent(null, true);
        Transform targetPoint = target.LockPoint;
        Vector3 startPosition = projectile.transform.position;
        float duration = Mathf.Max(0f, cue.travelDurationSeconds);
        if (duration <= 0f)
        {
            projectile.transform.position = targetPoint.position;
            projectile.transform.rotation = targetPoint.rotation;
            projectile.transform.SetParent(targetPoint, true);
            yield break;
        }

        float elapsed = 0f;
        while (projectile != null && target != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 destination = targetPoint.position;
            Vector3 direction = destination - projectile.transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                projectile.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            projectile.transform.position = Vector3.Lerp(startPosition, destination, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        if (projectile != null && target != null)
        {
            projectile.transform.position = targetPoint.position;
            projectile.transform.rotation = targetPoint.rotation;
            projectile.transform.SetParent(targetPoint, true);
        }
    }

    /// <summary>
    /// Animation Event joueur : applique les degats de la competence selectionnee
    /// et joue le hit de l'ennemi verrouille.
    /// </summary>
    public void HitEnemy()
    {
        SkillSO skill = ResolveSelectedSkill();
        if (skill == null)
        {
            return;
        }

        RealTimeCombatManager.Instance?.ApplySkillDamageToLockedEnemy(skill);
    }

    /// <summary>
    /// Animation Event ennemi : selectionne puis joue un SkillSO de EnemySkills.
    /// </summary>
    public void PlayEnemySkill(int skillIndex)
    {
        ResolveEnemySkills()?.PlaySkill(skillIndex);
    }

    /// <summary>
    /// Animation Event ennemi : choisit le SkillSO du clip courant sans le relancer.
    /// </summary>
    public void SetEnemySkill(int skillIndex)
    {
        ResolveEnemySkills()?.SetSkillForAnimationEvents(skillIndex);
    }

    /// <summary>
    /// Animation Event ennemi : joue tous les VFX du SkillSO ennemi actif.
    /// </summary>
    public void InstantiateEnemySkillVFX()
    {
        ResolveEnemySkills()?.InstantiateSkillVFX();
    }

    /// <summary>
    /// Animation Event ennemi : joue un VFX du SkillSO ennemi actif.
    /// </summary>
    public void InstantiateEnemySkillVFXAtIndex(int cueIndex)
    {
        ResolveEnemySkills()?.InstantiateSkillVFXAtIndex(cueIndex);
    }

    /// <summary>
    /// Animation Event ennemi : applique les degats du SkillSO ennemi actif a Lucian.
    /// </summary>
    public void HitPlayer()
    {
        ResolveEnemySkills()?.HitPlayer();
    }

    private RealTimeCombatEnemy ResolveEnemy()
    {
        if (enemy == null)
        {
            enemy = GetComponentInParent<RealTimeCombatEnemy>();
        }

        return enemy;
    }

    private EnemySkills ResolveEnemySkills()
    {
        if (enemySkills == null)
        {
            enemySkills = GetComponentInParent<EnemySkills>();
        }

        return enemySkills;
    }

    private SkillSO ResolveSelectedSkill()
    {
        if (skillsManager == null)
        {
            skillsManager = FindAnyObjectByType<SkillsManager>(FindObjectsInactive.Include);
        }

        if (combatInput == null)
        {
            combatInput = FindAnyObjectByType<RealTimeCombatInput>(FindObjectsInactive.Include);
        }

        if (skillsManager != null && skillsManager.AnimationEventSkill != null)
        {
            return skillsManager.AnimationEventSkill;
        }

        return skillsManager != null && combatInput != null
            ? skillsManager.GetEquippedSkill(combatInput.SelectedSlot)
            : null;
    }

    private void ResolvePlayerBow()
    {
        playerBow ??= GetComponentInChildren<PlayerBow>(true);
    }

    private void ResolvePlayerSword()
    {
        playerSword ??= GetComponentInChildren<PlayerSword>(true);
    }

    private System.Collections.IEnumerator StopDashRoutine(Vector3 dashDirection)
    {
        Transform caster = RealTimeCombatManager.Instance != null
            ? RealTimeCombatManager.Instance.PlayerRoot
            : null;
        LitOpsiveLocomotionBridge bridge = caster != null
            ? caster.GetComponentInChildren<LitOpsiveLocomotionBridge>(true)
            : null;
        if (bridge == null)
        {
            lastDashDirection = Vector3.zero;
            stopDashRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < stopDashDuration)
        {
            Vector3 velocity = bridge.PlanarVelocity;
            float forwardSpeed = Vector3.Dot(velocity, dashDirection);
            if (forwardSpeed <= 0.01f)
            {
                break;
            }

            float brakingStep = Mathf.Min(forwardSpeed, stopDashDeceleration * Time.fixedDeltaTime);
            bridge.AddExternalImpulse(-dashDirection * brakingStep, ForceMode.VelocityChange, 0f);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        lastDashDirection = Vector3.zero;
        stopDashRoutine = null;
    }
}
