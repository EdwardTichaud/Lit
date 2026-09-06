using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatEnemy))]
public sealed class EnemySkills : MonoBehaviour
{
    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private CombatActorAnimationRoot animationContract;
    [SerializeField] private Animator animator;
    [SerializeField] private CombatTimeDomain timeDomain;
    [SerializeField, Tooltip("Point de depart des VFX ennemi. La racine est utilisee si vide.")]
    private Transform casterVfxPoint;
    private List<SkillSO> skills = new List<SkillSO>();

    private SkillSO activeSkill;
    private int resolvedAttackSequence = -1;
    private readonly HashSet<SquadCharacterController> contactVictims = new HashSet<SquadCharacterController>();

    public IReadOnlyList<SkillSO> Skills
    {
        get
        {
            ResolveReferences();
            return skills;
        }
    }
    public SkillSO ActiveSkill => activeSkill;
    public Animator Animator => animator;

    private void Reset()
    {
        enemy = GetComponent<RealTimeCombatEnemy>();
        animationContract = GetComponent<CombatActorAnimationRoot>();
        animator = animationContract != null ? animationContract.Animator : null;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    public bool SetActiveSkill(int skillIndex)
    {
        ResolveReferences();
        if (skillIndex < 0 || skillIndex >= skills.Count || skills[skillIndex] == null)
        {
            return false;
        }

        activeSkill = skills[skillIndex];
        return true;
    }

    public SkillSO ChooseRetaliationSkill(float meleePreference)
    {
        List<SkillSO> meleeCandidates = new List<SkillSO>();
        List<SkillSO> rangedCandidates = new List<SkillSO>();
        for (int i = 0; i < skills.Count; i++)
        {
            SkillSO skill = skills[i];
            if (skill == null)
            {
                continue;
            }

            (skill.EnemyRange == RealTimeCombatRange.Melee ? meleeCandidates : rangedCandidates).Add(skill);
        }

        if (meleeCandidates.Count == 0 && rangedCandidates.Count == 0)
        {
            return null;
        }

        bool preferMelee = Random.value < Mathf.Clamp01(meleePreference);
        List<SkillSO> candidates = preferMelee ? meleeCandidates : rangedCandidates;
        if (candidates.Count == 0)
        {
            candidates = preferMelee ? rangedCandidates : meleeCandidates;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    public bool SetActiveSkill(SkillSO skill)
    {
        ResolveReferences();
        if (skill == null || !skills.Contains(skill))
        {
            return false;
        }

        activeSkill = skill;
        return true;
    }

    public bool PlaySkill(int skillIndex)
    {
        if (!SetActiveSkill(skillIndex))
        {
            return false;
        }

        return PlayActiveSkill();
    }

    public bool PlayActiveSkill()
    {
        ResolveReferences();
        if (animator == null || activeSkill.AnimationClip == null)
        {
            return false;
        }

        enemy?.CancelHitRecovery();

        string stateName = string.IsNullOrWhiteSpace(activeSkill.AnimatorState)
            ? activeSkill.AnimationClip.name
            : activeSkill.AnimatorState;
        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash))
        {
            stateName = "Base Layer." + stateName;
            stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, stateHash))
            {
                Debug.LogWarning("[EnemySkills] Etat Animator introuvable pour le SkillSO '" + activeSkill.SkillName + "': " + stateName, this);
                return false;
            }
        }

        animator.CrossFadeInFixedTime(stateHash, 0.08f, 0, 0f);
        return true;
    }

    public void ReturnToIdle()
    {
        ResolveReferences();
        enemy?.ReturnToIdleAnimation();
    }

    /// <summary>One instantaneous, authority-owned impact per authored enemy action.</summary>
    public void ExecuteEnemyAttack(SkillSO authoredSkill)
    {
        ResolveReferences();
        var manager = RealTimeCombatManager.Instance;
        var network = Unity.Netcode.NetworkManager.Singleton;
        if (network != null && network.IsListening && !network.IsServer) return;
        SkillSO skill = authoredSkill != null ? authoredSkill : enemy != null ? enemy.ActiveSkill : null;
        if (!isActiveAndEnabled || manager == null || manager.IsCinematicSequenceActive ||
            enemy == null || !enemy.isActiveAndEnabled || enemy.Health != null && enemy.Health.IsDead ||
            skill == null || enemy.ActiveSkill != skill || !skills.Contains(skill)) return;
        if (resolvedAttackSequence == enemy.ActionSequenceId) return;
        resolvedAttackSequence = enemy.ActionSequenceId;
        int action = enemy.ActionSequenceId;
        manager.GetComponent<CombatHealthThresholdController>()?.CancelAttackQte(enemy);
        contactVictims.Clear();
        var brain = enemy.GetComponent<EnemyCombatBrain>();
        Transform target = brain != null && brain.Target != null ? brain.Target.transform : manager.PlayerRoot;
        foreach (var cue in skill.VfxCues)
            if (cue != null && cue.delivery != SkillVfxDelivery.DirectOnTarget) PlayVfxCue(cue, target);

        EnemySkillImpactShape shape = skill.enemyImpact ?? new EnemySkillImpactShape();
        Vector3 center = transform.TransformPoint(shape.offset);
        foreach (Collider collider in Physics.OverlapSphere(center, Mathf.Max(.05f, shape.radius),
                     shape.targetMask, QueryTriggerInteraction.Ignore))
        {
            var victim = collider.GetComponentInParent<SquadCharacterController>();
            if (victim == null || victim.CurrentHp <= 0 || !contactVictims.Add(victim)) continue;
            Vector3 delta = Vector3.ProjectOnPlane(victim.transform.position - transform.position, Vector3.up);
            if (!skill.IsWithinHitRange(delta.magnitude) ||
                Vector3.Angle(transform.forward, delta) > shape.arcDegrees * .5f ||
                shape.requireGroundedTarget && !victim.IsGrounded) continue;
            int applied = manager.ResolveEnemyAttackContact(enemy, victim, skill, action, out var outcome);
            if (outcome == EnemyAttackOutcome.Damaged || outcome == EnemyAttackOutcome.Guarded)
            {
                foreach (var cue in skill.VfxCues)
                    if (cue != null && cue.delivery == SkillVfxDelivery.DirectOnTarget) PlayVfxCue(cue, victim.transform);
                PlayOutcomeFeedback(skill, victim.transform, outcome);
            }
            if (manager.PlayerRoot != null &&
                (victim.transform == manager.PlayerRoot || victim.transform.IsChildOf(manager.PlayerRoot)))
                manager.GetComponent<CombatHealthThresholdController>()?.NotifyFailureRetaliationImpact(enemy, skill, applied);
        }
    }

    public static void PlayOutcomeFeedback(SkillSO skill, Transform target, EnemyAttackOutcome outcome)
    {
        if (skill == null || target == null || outcome == EnemyAttackOutcome.Miss ||
            outcome == EnemyAttackOutcome.Avoided) return;
        var profile = skill.enemyAttackFeedback ?? new EnemyAttackFeedbackProfile();
        GameObject prefab = outcome == EnemyAttackOutcome.Countered ? profile.counterVfx :
            outcome == EnemyAttackOutcome.Guarded ? profile.guardVfx : profile.damageVfx;
        AudioClipSO audio = outcome == EnemyAttackOutcome.Countered ? profile.counterAudio :
            outcome == EnemyAttackOutcome.Guarded ? profile.guardAudio : profile.damageAudio;
        if (outcome == EnemyAttackOutcome.Damaged && skill.ImpactFeedback.enabled)
        {
            if (prefab == null) prefab = skill.ImpactFeedback.additionalImpactVfx;
            if (audio == null) audio = skill.ImpactFeedback.additionalImpactAudio;
        }
        Vector3 position = target.position + Vector3.up * profile.height;
        if (prefab != null)
        {
            var effect = Object.Instantiate(prefab, position, target.rotation, target);
            Object.Destroy(effect, Mathf.Max(.1f, profile.lifetimeSeconds));
        }
        if (audio != null) AudioManager.PlayClipAtPoint(audio, position);
    }

    private void PlayVfxCue(SkillVfxCue cue, Transform target)
    {
        if (cue == null)
        {
            return;
        }

        Transform caster = casterVfxPoint != null ? casterVfxPoint : transform;
        if (cue.delivery == SkillVfxDelivery.PlayerHand)
        {
            PlayCueAudio(cue, caster.position);
            if (cue.prefab != null)
            {
                Instantiate(cue.prefab, caster.position, caster.rotation, caster);
            }

            return;
        }

        if (target == null)
        {
            return;
        }

        if (cue.delivery == SkillVfxDelivery.DirectOnTarget)
        {
            PlayCueAudio(cue, target.position);
            if (cue.prefab != null)
            {
                Instantiate(cue.prefab, target.position, target.rotation, target);
            }

            return;
        }

        PlayCueAudio(cue, caster.position);
        if (cue.prefab != null)
        {
            StartCoroutine(PlayProjectileCue(cue, caster, target));
        }
    }

    private IEnumerator PlayProjectileCue(SkillVfxCue cue, Transform caster, Transform target)
    {
        GameObject projectile = Instantiate(cue.prefab, caster.position, caster.rotation, caster);
        if (cue.holdAtCasterSeconds > 0f)
        {
            if (timeDomain != null) yield return timeDomain.WaitForLocalSeconds(cue.holdAtCasterSeconds);
            else yield return new WaitForSeconds(cue.holdAtCasterSeconds);
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
        Vector3 startPosition = projectile.transform.position;
        float duration = Mathf.Max(0f, cue.travelDurationSeconds);
        if (duration <= 0f)
        {
            projectile.transform.position = target.position;
            projectile.transform.rotation = target.rotation;
            projectile.transform.SetParent(target, true);
            yield break;
        }

        float elapsed = 0f;
        while (projectile != null && target != null && elapsed < duration)
        {
            elapsed += timeDomain != null ? timeDomain.DeltaTime : Time.deltaTime;
            Vector3 destination = target.position;
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
            projectile.transform.position = target.position;
            projectile.transform.rotation = target.rotation;
            projectile.transform.SetParent(target, true);
        }
    }

    private void ResolveReferences()
    {
        CharacterData data = GetComponent<CharacterInfo>()?.CharacterData;
        if (data != null) skills = data.combatSkills;
        if (enemy == null)
        {
            enemy = GetComponent<RealTimeCombatEnemy>();
        }

        if (animationContract == null)
        {
            animationContract = GetComponent<CombatActorAnimationRoot>();
        }

        timeDomain ??= GetComponent<CombatTimeDomain>();

        if (animationContract != null && animationContract.ValidateContract(out _))
        {
            animator = animationContract.Animator;
        }
        else
        {
            animator = null;
        }
    }

    private static void PlayCueAudio(SkillVfxCue cue, Vector3 position)
    {
        if (cue.audioClip != null)
        {
            AudioManager.PlayClipAtPoint(cue.audioClip, position);
        }
    }
}
