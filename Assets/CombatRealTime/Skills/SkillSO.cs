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

[Serializable]
public sealed class SkillScreenWaveCue
{
    public bool enabled;
    public ScreenWaveController.ScreenWaveSettings settings = ScreenWaveController.ScreenWaveSettings.Default;
}

[Serializable]
public sealed class SkillRetreatImpulse
{
    public bool enabled;
    [Min(0f)] public float horizontalImpulse = 24f;
    [Min(0f)] public float verticalImpulse = 9f;
    [Tooltip("Plafond de securite applique au recul, pour que la capsule UCC conserve une collision fiable avec les murs.")]
    [Min(0f)] public float maximumHorizontalImpulse = 24f;
    [Tooltip("Plafond de securite applique a la poussee verticale, pour eviter de depasser le sol a l'atterrissage.")]
    [Min(0f)] public float maximumVerticalImpulse = 8f;
    [Min(0f)] public float minimumInputLockSeconds = 0.12f;
    [Min(0.1f)] public float maximumInputLockSeconds = 3.5f;
}

[Serializable]
public sealed class CombatCameraImpactProfile
{
    [Tooltip("Offset ajoute au cadrage UCC de lock. Il est amorti et ne deplace jamais directement la Main Camera.")]
    public Vector3 lookOffsetKick = new Vector3(0f, 0.03f, 0.2f);
    [Tooltip("Variation temporaire de FOV. Une valeur negative rapproche visuellement la camera.")]
    [Range(-15f, 15f)] public float fieldOfViewKick = -1f;
    [Min(0.1f)] public float recoverySharpness = 18f;
}

[Serializable]
public sealed class CombatImpactFeedbackProfile
{
    public bool enabled = true;

    [Header("Global Hit Stop")]
    public bool useHitStop = true;
    [Range(0f, 1f)] public float hitStopTimeScale = 0.05f;
    [Min(0f)] public float hitStopSeconds = 0.055f;

    [Header("Lock Camera")]
    public CombatCameraImpactProfile camera = new CombatCameraImpactProfile();

    [Header("Screen Wave")]
    public SkillScreenWaveCue screenWave = new SkillScreenWaveCue();

    [Header("Optional Impact Cue")]
    public GameObject additionalImpactVfx;
    public AudioClipSO additionalImpactAudio;
}

public enum SkillVfxDelivery
{
    DirectOnTarget,
    Projectile,
    PlayerHand,
    PlayerSword,
    ProjectileFromPlayerHand
}

public enum PlayerActionRootMotionMode
{
    InPlace,
    AuthoredRootMotion,
    ScriptedDash
}

public enum PlayerActionFacingMode
{
    // Le rig regarde la cible, sans modifier la direction physique UCC.
    VisualOnly,
    // La capsule UCC tourne aussi vers la cible. Reserve aux actions dirigees.
    UccBody
}

[Serializable]
public sealed class PlayerActionPresentationProfile
{
    [Range(0f, 0.25f)] public float entryBlendSeconds = 0.06f;
    [Range(0.05f, 1f)] public float chainNormalizedTime = 0.7f;
    [Range(0.05f, 1f), Tooltip("Instant ou une BasicSkill bufferisee interrompt le clip courant. Il est automatiquement borne entre l'ouverture de chaine et la recuperation.")]
    public float chainTransitionNormalizedTime = 0.78f;
    [Range(0.05f, 1f)] public float recoveryNormalizedTime = 0.88f;
    [Range(0f, 0.25f)] public float exitBlendSeconds = 0.1f;
    [Header("Root Motion")]
    [Tooltip("InPlace: aucun deplacement racine. AuthoredRootMotion: le clip deplace UCC. ScriptedDash: le script pilote le dash.")]
    public PlayerActionRootMotionMode rootMotionMode = PlayerActionRootMotionMode.AuthoredRootMotion;
    [Header("Facing And Inertia")]
    [Tooltip("VisualOnly (recommande) : Lucian regarde la cible mais conserve exactement son inertie UCC. UccBody : la capsule UCC tourne aussi, a reserver aux actions dirigees.")]
    public PlayerActionFacingMode facingMode = PlayerActionFacingMode.VisualOnly;
    public bool allowMoveAfterRecovery = true;
    public bool allowDodgeAfterRecovery = true;

    public static PlayerActionPresentationProfile CreateDefault()
    {
        return new PlayerActionPresentationProfile();
    }
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

    [Header("Player Presentation")]
    public PlayerActionPresentationProfile presentation = new PlayerActionPresentationProfile();
    [Header("Hit Range")]
    [Min(0f), Tooltip("Distance horizontale minimale entre le joueur et EnemyLockPoint pour appliquer HitEnemy.")]
    public float minimumHitDistance;
    [Min(0.01f), Tooltip("Distance horizontale maximale entre le joueur et EnemyLockPoint pour appliquer HitEnemy.")]
    public float maximumHitDistance = 2.5f;
    [Tooltip("Si active, le skill ne demarre pas hors de sa portee de hit.")]
    public bool requireValidRangeToStart;

    [Header("VFX")]
    public List<SkillVfxCue> vfxCues = new List<SkillVfxCue>();

    [Header("Impact Presentation")]
    public CombatImpactFeedbackProfile impactFeedback = new CombatImpactFeedbackProfile();
    public SkillRetreatImpulse retreatImpulse = new SkillRetreatImpulse();

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
    public PlayerActionPresentationProfile Presentation => presentation ?? (presentation = PlayerActionPresentationProfile.CreateDefault());
    public float Damages => damages;
    public float MinimumHitDistance => minimumHitDistance;
    public float MaximumHitDistance => Mathf.Max(minimumHitDistance, maximumHitDistance);
    public bool RequireValidRangeToStart => requireValidRangeToStart;
    public IReadOnlyList<SkillVfxCue> VfxCues => vfxCues;
    public CombatImpactFeedbackProfile ImpactFeedback => impactFeedback ?? (impactFeedback = new CombatImpactFeedbackProfile());
    public SkillRetreatImpulse RetreatImpulse => retreatImpulse ?? (retreatImpulse = new SkillRetreatImpulse());
    public RealTimeCombatRange EnemyRange => enemyRange;
    public float EnemyDamageMultiplier => enemyDamageMultiplier;
    public IReadOnlyList<RealTimeCombatReaction> AcceptedEnemyReactions => acceptedEnemyReactions;
    public bool RequireAllEnemyReactions => requireAllEnemyReactions;
    public AudioClipSO EnemyAttackSfx => enemyAttackSfx;

    public bool AcceptsEnemyReaction(RealTimeCombatReaction reaction)
    {
        return acceptedEnemyReactions != null && acceptedEnemyReactions.Contains(reaction);
    }

    public bool IsWithinHitRange(float horizontalDistance)
    {
        return horizontalDistance >= MinimumHitDistance && horizontalDistance <= MaximumHitDistance;
    }
}
