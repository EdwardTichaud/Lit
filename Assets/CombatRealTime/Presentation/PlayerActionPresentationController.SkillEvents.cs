using System.Collections.Generic;
using UnityEngine;

public sealed partial class PlayerActionPresentationController
{
    private EnemyController enemy;
    private EnemyController enemySkills;
    private SkillsManager skillsManager;
    private RealTimeCombatInput combatInput;
    private PlayerBow playerBow;
    private PlayerSword playerSword;
    private PlayerActionPresentationController playerActionPresentation;
    [Header("Input Prompt Animation Events")]
    [SerializeField] private Transform inputPromptAnchor;
    [SerializeField] private Vector3 inputPromptOffset = new Vector3(0f, 1.25f, 0f);

    public void HandleQTE(string input) => CombatHealthThresholdController.Instance?.OpenQte(input);
    private Coroutine hideSwordAfterComboRoutine;
    private CombatInputWorldPrompt activeInputPrompt;

    private void SkillEventsReset()
    {
        enemy = GetComponentInParent<EnemyController>();
        ResolvePlayerBow();
        ResolvePlayerSword();
    }

    private void SkillEventsAwake()
    {
        ResolvePlayerBow();
        ResolvePlayerSword();
        BindPlayerActionPresentation();
        HandleHideBow();
        HandleHideSword();

    }

    private void SkillEventsOnDisable()
    {
        UnbindPlayerActionPresentation();
        GetMobility()?.CancelAnimationDash(transform);

        if (hideSwordAfterComboRoutine != null)
        {
            StopCoroutine(hideSwordAfterComboRoutine);
            hideSwordAfterComboRoutine = null;
        }

        HandleHideBow();
        HandleHideSword();

    }

    private void OnEnable()
    {
        BindPlayerActionPresentation();
    }

    /// <summary>
    /// Animation Event ennemi : affiche le Sprite 2D de l'input a executer au-dessus de l'ennemi.
    /// </summary>


    /// <summary>
    /// Animation Event ennemi : masque le prompt d'input actif.
    /// </summary>




    /// <summary>Instantaneous authored enemy contact, including skill and result effects.</summary>


    /// <summary>Enemy Animation Event: locks the autonomous attack facing before its committed impact.</summary>


    /// <summary>
    /// Player animation event for a health-threshold QTE. Valid values are
    /// Y, B, A and X; the active threshold session owns validation and timing.
    /// </summary>








    /// <summary>Enemy Animation Event: begins the configured continuous homing rush.</summary>


    /// <summary>Enemy Animation Event: releases planar rush ownership before landing recovery.</summary>


    /// <summary>Enemy Animation Event: asks the physics motor to settle onto the ground.</summary>




    /// <summary>
    /// Player Animation Event: resolves the active LightSkill exactly on its authored contact frame.
    /// </summary>
    public void HandleResolveLightSkillImpact()
    {
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.GetComponent<LightSkillCombatController>()?.ResolveLightSkillImpact();
    }

    /// <summary>Player Animation Event: resolves the selected CounterSkill Timeline on its contact frame.</summary>
    public void HandleResolveCounterSkillImpact()
    {
        CounterSkillCombatController.Instance?.ResolveCounterSkillImpact();
    }

    /// <summary>
    /// Shared Timeline Animation Event for an optional BasicSkill, player Skill
    /// or EnemySkill cinematic. The active cinematic session owns the selected
    /// skill and rejects duplicate or late events.
    /// </summary>
    public void HandleResolveCinematicSkillImpact()
    {
        RealTimeCombatManager.Instance?.CombatSkillCinematicController?.ResolveCinematicSkillImpact();
    }

    /// <summary>
    /// Animation Event joueur : joue tous les VFX de la competence selectionnee.
    /// </summary>
    public void HandleInstantiateSkillVFX()
    {
        SkillSO skill = ResolveSelectedSkill();
        EnemyController target = RealTimeCombatManager.Instance != null
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
    public void HandleInstantiateSkillVFXAtIndex(int cueIndex)
    {
        SkillSO skill = ResolveSelectedSkill();
        EnemyController target = RealTimeCombatManager.Instance != null
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
    public void HandleShowBow()
    {
        BindPlayerActionPresentation();
        ResolvePlayerBow();
        playerBow?.Show();
    }

    /// <summary>
    /// Animation Event optionnel : masque l'arc de la competence active.
    /// </summary>
    public void HandleHideBow()
    {
        ResolvePlayerBow();
        playerBow?.Hide();
    }

    /// <summary>
    /// Animation Event optionnel : affiche l'epee de la competence active.
    /// </summary>
    public void HandleShowSword()
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
    public void HandleHideSword()
    {
        ResolvePlayerSword();
        playerSword?.Hide();
    }

    /// <summary>
    /// BasicSkill event: the sword remains visible while a buffered combo is
    /// still active, then is hidden once the final action has recovered.
    /// </summary>
    public void HandleHideSwordWhenComboEnds()
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
    private CombatMobilityController GetMobility() => RealTimeCombatManager.Instance != null
        ? RealTimeCombatManager.Instance.GetComponent<CombatMobilityController>() : null;

    public void HandleDash() => GetMobility()?.HandleDash();

    /// <summary>
    /// Animation Event : freine progressivement la derniere impulsion de dash.
    /// </summary>
    public void HandleStopDash() => GetMobility()?.HandleStopDash();


    private void PlaySkillVfxCue(SkillVfxCue cue, EnemyController target)
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
            Transform targetPoint = target.LockPoint != null ? target.LockPoint : target.transform;
            Vector3 impactPosition = CombatImpactFeedbackController.ResolvePlayerImpactPosition(targetPoint, transform);
            PlaySkillVfxCueAudio(cue, impactPosition);
            if (cue.prefab != null)
            {
                Instantiate(cue.prefab, impactPosition, targetPoint.rotation, targetPoint);
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

    private System.Collections.IEnumerator PlayProjectileSkillVfx(SkillVfxCue cue, Transform caster, EnemyController target)
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
        Transform targetPoint = target.LockPoint != null ? target.LockPoint : target.transform;
        Vector3 startPosition = projectile.transform.position;
        float duration = Mathf.Max(0f, cue.travelDurationSeconds);
        if (duration <= 0f)
        {
            projectile.transform.position = CombatImpactFeedbackController.ResolvePlayerImpactPosition(targetPoint, transform);
            projectile.transform.rotation = targetPoint.rotation;
            projectile.transform.SetParent(targetPoint, true);
            yield break;
        }

        float elapsed = 0f;
        while (projectile != null && target != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 destination = CombatImpactFeedbackController.ResolvePlayerImpactPosition(targetPoint, transform);
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
            projectile.transform.position = CombatImpactFeedbackController.ResolvePlayerImpactPosition(targetPoint, transform);
            projectile.transform.rotation = targetPoint.rotation;
            projectile.transform.SetParent(targetPoint, true);
        }
    }

    /// <summary>
    /// Animation Event joueur : applique les degats de la competence selectionnee
    /// et joue le hit de l'ennemi verrouille.
    /// </summary>
    public void HandleHitEnemy()
    {
        HandleResolveSkillImpact();
    }

    /// <summary>
    /// Generic player impact event. Damage is confirmed first, then every
    /// configurable feedback cue is played exactly once.
    /// </summary>
    public void HandleResolveSkillImpact()
    {
        if (!TryResolveSelectedSkillImpact(out SkillSO skill, out EnemyController target))
        {
            return;
        }

        CombatImpactFeedbackController.EnsureInstance()?.PlayImpact(skill, target);
    }

    /// <summary>
    /// Animation Event generique d'impact avec recul. La portee est reevaluee au
    /// contact : aucun VFX, onde ou mouvement n'est joue si la cible s'est echappee.
    /// </summary>
    public void HandleResolveSkillImpactAndRetreat()
    {
        SkillSO skill = ResolveSelectedSkill();
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        EnemyController target = manager != null ? manager.LockedEnemy : null;
        Transform caster = manager != null ? manager.PlayerRoot : null;
        if (skill == null || manager == null || target == null || caster == null)
        {
            return;
        }

        if (!TryResolveSelectedSkillImpact(out skill, out target))
        {
            return;
        }

        HandleInstantiateSkillVFX();
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


    private EnemyController ResolveEnemy()
    {
        if (enemy == null)
        {
            enemy = GetComponentInParent<EnemyController>();
        }

        return enemy;
    }




    private EnemyController ResolveEnemySkills()
    {
        if (enemySkills == null)
        {
            enemySkills = GetComponentInParent<EnemyController>();
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
        playerActionPresentation = this;
        ActionEnded -= HideEquippedWeapons;
        ActionEnded += HideEquippedWeapons;
    }
    private void UnbindPlayerActionPresentation()
    {
        ActionEnded -= HideEquippedWeapons;
        playerActionPresentation = null;
    }

    private void HideEquippedWeapons()
    {
        HandleHideBow();
        HandleHideSword();
    }

    private bool TryResolveSelectedSkillImpact(out SkillSO skill, out EnemyController target)
    {
        skill = ResolveSelectedSkill();
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        target = manager != null ? manager.LockedEnemy : null;
        return skill != null && target != null && manager != null
            && !(skill is BasicSkillsSO && basicSkillInterruptedByDamage)
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

        HandleHideSword();
        hideSwordAfterComboRoutine = null;
    }


}
