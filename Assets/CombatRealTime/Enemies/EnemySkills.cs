using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatEnemy))]
public sealed class EnemySkills : MonoBehaviour
{
    [SerializeField] private RealTimeCombatEnemy enemy;
    [SerializeField] private Animator animator;
    [SerializeField, Tooltip("Point de depart des VFX ennemi. La racine est utilisee si vide.")]
    private Transform casterVfxPoint;
    [SerializeField] private List<SkillSO> skills = new List<SkillSO>();

    private SkillSO activeSkill;

    public IReadOnlyList<SkillSO> Skills => skills;
    public SkillSO ActiveSkill => activeSkill;

    private void Reset()
    {
        enemy = GetComponent<RealTimeCombatEnemy>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    public bool SetActiveSkill(int skillIndex)
    {
        if (skillIndex < 0 || skillIndex >= skills.Count || skills[skillIndex] == null)
        {
            return false;
        }

        activeSkill = skills[skillIndex];
        return true;
    }

    /// <summary>
    /// Selectionne le SkillSO utilise par les Animation Events du clip courant,
    /// sans interrompre ce clip.
    /// </summary>
    public void SetSkillForAnimationEvents(int skillIndex)
    {
        SetActiveSkill(skillIndex);
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

        animator.CrossFade(stateHash, 0.08f, 0);
        return true;
    }

    public void ReturnToIdle()
    {
        ResolveReferences();
        enemy?.ReturnToIdleAnimation();
    }

    public void InstantiateSkillVFX()
    {
        if (activeSkill == null || activeSkill.VfxCues == null)
        {
            return;
        }

        for (int i = 0; i < activeSkill.VfxCues.Count; i++)
        {
            PlayVfxCue(activeSkill.VfxCues[i]);
        }
    }

    public void InstantiateSkillVFXAtIndex(int cueIndex)
    {
        if (activeSkill == null || activeSkill.VfxCues == null || cueIndex < 0 || cueIndex >= activeSkill.VfxCues.Count)
        {
            return;
        }

        PlayVfxCue(activeSkill.VfxCues[cueIndex]);
    }

    public void HitPlayer()
    {
        RealTimeCombatManager.Instance?.ResolveEnemyAttackImpact(enemy);
    }

    private void PlayVfxCue(SkillVfxCue cue)
    {
        if (cue == null)
        {
            return;
        }

        Transform caster = casterVfxPoint != null ? casterVfxPoint : transform;
        Transform target = LocalPlayerContext.LocalCharacterRoot;
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
            elapsed += Time.deltaTime;
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
        if (enemy == null)
        {
            enemy = GetComponent<RealTimeCombatEnemy>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
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
