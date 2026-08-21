using System.Collections.Generic;
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
    private PlayerActionPresentationController playerActionPresentation;

    [Header("Input Prompt Animation Events")]
    [SerializeField] private Transform inputPromptAnchor;
    [SerializeField] private Vector3 inputPromptOffset = new Vector3(0f, 1.25f, 0f);

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
    private Coroutine hideSwordAfterComboRoutine;
    private CombatInputWorldPrompt activeInputPrompt;
    private readonly HashSet<string> warnedUnknownPlayerHitConditions = new HashSet<string>();

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
        BindPlayerActionPresentation();
        HideBow();
        HideSword();
        HideInput();
    }

    private void OnDisable()
    {
        UnbindPlayerActionPresentation();
        if (stopDashRoutine != null)
        {
            StopCoroutine(stopDashRoutine);
            stopDashRoutine = null;
        }

        if (hideSwordAfterComboRoutine != null)
        {
            StopCoroutine(hideSwordAfterComboRoutine);
            hideSwordAfterComboRoutine = null;
        }

        HideBow();
        HideSword();
        HideInput();
    }

    private void OnEnable()
    {
        BindPlayerActionPresentation();
    }

    /// <summary>
    /// Animation Event ennemi : affiche le Sprite 2D de l'input a executer au-dessus de l'ennemi.
    /// </summary>
    public void ShowInput(Sprite inputSprite)
    {
        HideInput();

        Transform anchor = inputPromptAnchor;
        if (anchor == null)
        {
            RealTimeCombatEnemy currentEnemy = ResolveEnemy();
            anchor = currentEnemy != null ? currentEnemy.LockPoint : transform;
        }

        activeInputPrompt = CombatInputWorldPrompt.Show(anchor, inputSprite, inputPromptOffset);
    }

    /// <summary>
    /// Animation Event ennemi : masque le prompt d'input actif.
    /// </summary>
    public void HideInput()
    {
        if (activeInputPrompt != null)
        {
            activeInputPrompt.Hide();
            activeInputPrompt = null;
        }
    }

    public void ShowReactionPrompt()
    {
        RealTimeCombatManager.Instance?.BeginEnemyAttackWindow(ResolveEnemy());
    }

    /// <summary>Enemy Animation Event: starts presentation only; it never opens the logical reaction window.</summary>
    public void BeginReactionTelegraph()
    {
        CombatReactionTelegraphController.Instance?.BeginTelegraph(ResolveEnemy());
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
        if (currentEnemy == null)
        {
            return;
        }

        currentEnemy.CompleteEnemyAttackWhenGrounded(() =>
        {
            RealTimeCombatManager.Instance?.CompleteEnemyAttack(currentEnemy);
            ResolveEnemySkills()?.ReturnToIdle();
        });
    }

    /// <summary>Enemy Animation Event: starts the authored ballistic phase.</summary>
    public void BeginEnemyAirborne()
    {
        ResolveEnemy()?.BeginEnemyAirborne();
    }

    /// <summary>Enemy Animation Event: asks the physics motor to settle onto the ground.</summary>
    public void RequestEnemyLanding()
    {
        ResolveEnemy()?.RequestEnemyLanding();
    }

    /// <summary>
    /// Player Animation Event: resolves the active LightSkill exactly on its authored contact frame.
    /// </summary>
    public void ResolveLightSkillImpact()
    {
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.GetComponent<LightSkillCombatController>()?.ResolveLightSkillImpact();
    }

    /// <summary>Player Animation Event: resolves the selected CounterSkill Timeline on its contact frame.</summary>
    public void ResolveCounterSkillImpact()
    {
        CounterSkillCombatController.Instance?.ResolveCounterSkillImpact();
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
        BindPlayerActionPresentation();
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
        BindPlayerActionPresentation();
        if (hideSwordAfterComboRoutine != null)
        {
            StopCoroutine(hideSwordAfterComboRoutine);
            hideSwordAfterComboRoutine = null;
        }

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
    /// BasicSkill event: the sword remains visible while a buffered combo is
    /// still active, then is hidden once the final action has recovered.
    /// </summary>
    public void HideSwordWhenComboEnds()
    {
        if (hideSwordAfterComboRoutine != null)
        {
            StopCoroutine(hideSwordAfterComboRoutine);
        }

        hideSwordAfterComboRoutine = StartCoroutine(HideSwordAfterComboEnds());
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

        if (cue.delivery == SkillVfxDelivery.PlayerHand || cue.delivery == SkillVfxDelivery.PlayerSword)
        {
            Transform handPoint;
            if (cue.delivery == SkillVfxDelivery.PlayerSword)
            {
                ResolvePlayerSword();
                handPoint = playerSword != null ? playerSword.transform : RealTimeCombatManager.Instance?.PlayerRoot;
            }
            else
            {
                ResolvePlayerBow();
                handPoint = playerBow != null ? playerBow.transform : RealTimeCombatManager.Instance?.PlayerRoot;
            }
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

        Transform caster = RealTimeCombatManager.Instance != null ? RealTimeCombatManager.Instance.PlayerRoot : null;
        if (cue.delivery == SkillVfxDelivery.ProjectileFromPlayerHand)
        {
            ResolvePlayerBow();
            caster = playerBow != null ? playerBow.transform : caster;
        }
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
        ResolveSkillImpact();
    }

    /// <summary>
    /// Generic player impact event. Damage is confirmed first, then every
    /// configurable feedback cue is played exactly once.
    /// </summary>
    public void ResolveSkillImpact()
    {
        if (!TryResolveSelectedSkillImpact(out SkillSO skill, out RealTimeCombatEnemy target))
        {
            return;
        }

        CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(skill, target);
    }

    /// <summary>
    /// Animation Event generique d'impact avec recul. La portee est reevaluee au
    /// contact : aucun VFX, onde ou mouvement n'est joue si la cible s'est echappee.
    /// </summary>
    public void ResolveSkillImpactAndRetreat()
    {
        SkillSO skill = ResolveSelectedSkill();
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        RealTimeCombatEnemy target = manager != null ? manager.LockedEnemy : null;
        Transform caster = manager != null ? manager.PlayerRoot : null;
        if (skill == null || manager == null || target == null || caster == null)
        {
            return;
        }

        if (!TryResolveSelectedSkillImpact(out skill, out target))
        {
            return;
        }

        InstantiateSkillVFX();
        CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(skill, target);

        SkillRetreatImpulse retreat = skill.RetreatImpulse;
        if (!retreat.enabled)
        {
            return;
        }

        Vector3 direction = caster.position - target.LockPoint.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = -caster.forward;
            direction.y = 0f;
        }

        LitOpsiveLocomotionBridge bridge = caster.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        if (bridge != null)
        {
            // Les forces trop elevees peuvent faire franchir a la capsule UCC
            // un obstacle entre deux mises a jour de simulation. Chaque skill
            // conserve son recul auteurise, mais dans une limite sure.
            float horizontalImpulse = Mathf.Min(retreat.horizontalImpulse, retreat.maximumHorizontalImpulse);
            float verticalImpulse = Mathf.Min(retreat.verticalImpulse, retreat.maximumVerticalImpulse);
            Vector3 impulse = direction.normalized * horizontalImpulse + Vector3.up * verticalImpulse;
            bridge.AddExternalImpulseUntilGrounded(
                impulse,
                ForceMode.VelocityChange,
                retreat.minimumInputLockSeconds,
                retreat.maximumInputLockSeconds,
                retreat.airborneInertiaSeconds,
                retreat.airborneInertiaEndSpeedMultiplier);
        }
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

    /// <summary>
    /// Animation Event ennemi : applique les degats seulement si la condition cible est valide.
    /// Les conditions sont exprimees par nom pour pouvoir en ajouter sans modifier les clips existants.
    /// </summary>
    public void HitPlayerIf(string conditionName)
    {
        if (!MeetsPlayerHitCondition(conditionName))
        {
            return;
        }

        HitPlayer();
    }

    private bool MeetsPlayerHitCondition(string conditionName)
    {
        string normalizedCondition = string.IsNullOrWhiteSpace(conditionName)
            ? "Always"
            : conditionName.Trim();
        if (string.Equals(normalizedCondition, "Always", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(normalizedCondition, "Grounded", System.StringComparison.OrdinalIgnoreCase))
        {
            RealTimeCombatManager manager = RealTimeCombatManager.Instance;
            SquadCharacterController player = manager != null && manager.PlayerRoot != null
                ? manager.PlayerRoot.GetComponentInChildren<SquadCharacterController>(true)
                : null;
            return player != null && player.IsGrounded;
        }

        if (warnedUnknownPlayerHitConditions.Add(normalizedCondition))
        {
            Debug.LogWarning("[RealTimeCombatAnimationEvents] Condition HitPlayerIf inconnue : '" + normalizedCondition + "'. L'impact est ignore.", this);
        }

        return false;
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

    private void BindPlayerActionPresentation()
    {
        if (playerActionPresentation != null)
        {
            return;
        }

        Transform playerRoot = RealTimeCombatManager.Instance != null
            ? RealTimeCombatManager.Instance.PlayerRoot
            : transform.root;
        playerActionPresentation = playerRoot != null
            ? playerRoot.GetComponentInChildren<PlayerActionPresentationController>(true)
            : null;
        if (playerActionPresentation != null)
        {
            playerActionPresentation.ActionEnded += HideEquippedWeapons;
        }
    }

    private void UnbindPlayerActionPresentation()
    {
        if (playerActionPresentation != null)
        {
            playerActionPresentation.ActionEnded -= HideEquippedWeapons;
            playerActionPresentation = null;
        }
    }

    private void HideEquippedWeapons()
    {
        HideBow();
        HideSword();
    }

    private bool TryResolveSelectedSkillImpact(out SkillSO skill, out RealTimeCombatEnemy target)
    {
        skill = ResolveSelectedSkill();
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        target = manager != null ? manager.LockedEnemy : null;
        return skill != null && target != null && manager != null
            && manager.ApplySkillDamageToLockedEnemy(skill) > 0;
    }

    private System.Collections.IEnumerator HideSwordAfterComboEnds()
    {
        PlayerActionPresentationController presentation = RealTimeCombatManager.Instance != null
            ? RealTimeCombatManager.Instance.PlayerRoot?.GetComponentInChildren<PlayerActionPresentationController>(true)
            : null;
        while (presentation != null && presentation.IsActionActive)
        {
            yield return null;
        }

        HideSword();
        hideSwordAfterComboRoutine = null;
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
