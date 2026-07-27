using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillVfxCue
{
    public string displayName;
    public GameObject prefab;
    public AudioClipSO audioClip;
    public SkillVfxDelivery delivery = SkillVfxDelivery.DirectOnTarget;
    [Min(0f)] public float holdAtCasterSeconds;
    [Min(0f)] public float travelDurationSeconds = 0.25f;
}

public enum SkillVfxDelivery
{
    DirectOnTarget,
    Projectile,
    PlayerHand
}

[CreateAssetMenu(fileName = "SkillSO", menuName = "Scriptable Objects/Combat/Skill SO")]
public class SkillSO : ScriptableObject
{
    [Header("Identity")]
    public string skillName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Combat")]
    public AnimationClip animationClip;
    [Tooltip("Chemin complet de la state Animator a jouer. Laisser vide pour utiliser le nom du clip.")]
    public string animatorState;
    [Min(0f)] public float damages;

    [Header("VFX")]
    public List<SkillVfxCue> vfxCues = new List<SkillVfxCue>();

    [Header("Enemy Retaliation")]
    public RealTimeCombatRange enemyRange = RealTimeCombatRange.Melee;
    [Min(0f)] public float enemyDamageMultiplier = 1f;
    public List<RealTimeCombatReaction> acceptedEnemyReactions = new List<RealTimeCombatReaction> { RealTimeCombatReaction.Dodge };
    public bool requireAllEnemyReactions;
    public AudioClipSO enemyAttackSfx;

    public string SkillName => string.IsNullOrWhiteSpace(skillName) ? name : skillName;
    public Sprite Icon => icon;
    public AnimationClip AnimationClip => animationClip;
    public string AnimatorState => animatorState;
    public float Damages => damages;
    public IReadOnlyList<SkillVfxCue> VfxCues => vfxCues;
    public RealTimeCombatRange EnemyRange => enemyRange;
    public float EnemyDamageMultiplier => enemyDamageMultiplier;
    public IReadOnlyList<RealTimeCombatReaction> AcceptedEnemyReactions => acceptedEnemyReactions;
    public bool RequireAllEnemyReactions => requireAllEnemyReactions;
    public AudioClipSO EnemyAttackSfx => enemyAttackSfx;

    public bool AcceptsEnemyReaction(RealTimeCombatReaction reaction)
    {
        return acceptedEnemyReactions != null && acceptedEnemyReactions.Contains(reaction);
    }
}
