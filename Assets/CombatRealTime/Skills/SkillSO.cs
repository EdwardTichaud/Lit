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
    [Header("Airborne Inertia")]
    [Tooltip("Duree durant laquelle l'impulsion maintient progressivement son elan horizontal apres le recul.")]
    [Min(0f)] public float airborneInertiaSeconds;
    [Tooltip("Part de la vitesse horizontale initiale conservee jusqu'a l'atterrissage apres la deceleration aerienne.")]
    [Range(0f, 1f)] public float airborneInertiaEndSpeedMultiplier = 0.3f;
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

[Serializable]
public sealed class CombatReactionTelegraphProfile
{
    public bool enabled = true;
    public GameObject alertPrefab;
    public Color threatColor = new Color(0.85f, 0.08f, 0.4f, 1f);
    public Color perfectWindowColor = new Color(0.72f, 0.94f, 1f, 1f);
    [Min(0f)] public float heightOffset = 1.1f;
    [Min(0.01f)] public float fadeSeconds = 0.12f;
    public AudioClipSO anticipationAudio;
    public AudioClipSO perfectWindowAudio;
    public AudioClipSO successfulReactionAudio;
    public bool usePerfectWindowSlowMotion = true;
    [Range(0.1f, 1f)] public float perfectWindowTimeScale = 0.85f;
    [Min(0f)] public float perfectWindowSlowMotionSeconds = 0.15f;
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

public enum EnemyActionMovementMode
{
    Grounded,
    Airborne
}

[Serializable]
public sealed class EnemyActionMotionProfile
{
    public EnemyActionMovementMode movementMode = EnemyActionMovementMode.Grounded;
    [Min(0f), Tooltip("Vitesse verticale initiale appliquee par BeginEnemyAirborne.")]
    public float initialUpwardSpeed = 12f;
    [Min(0.1f)] public float gravity = 32f;
    [Min(0.1f)] public float maximumFallSpeed = 28f;
    [Min(0.01f), Tooltip("Vitesse descendante minimale lorsqu'un atterrissage est demande.")]
    public float minimumLandingSpeed = 4f;
    [Min(0.1f), Tooltip("Securite : l'ennemi force son retour au sol apres cette duree.")]
    public float maximumAirborneSeconds = 2.5f;

    public bool IsAirborne => movementMode == EnemyActionMovementMode.Airborne;

    public static EnemyActionMotionProfile GroundedDefault => new EnemyActionMotionProfile();
}

[Serializable]
public sealed class PlayerActionPresentationProfile
{
    [Range(0f, 0.25f)] public float entryBlendSeconds = 0.06f;
    [Range(0.05f, 1f)] public float chainNormalizedTime = 0.7f;
    [Range(0.05f, 1f), Tooltip("Instant ou une BasicSkill bufferisee interrompt le clip courant. Il est automatiquement borne entre l'ouverture de chaine et la recuperation.")]
    public float chainTransitionNormalizedTime = 0.78f;
    [Range(0.05f, 1f), Tooltip("Instant a partir duquel une esquive, un dash ou un saut peut interrompre cette action.")]
    public float mobilityCancelNormalizedTime = 0.7f;
    [Range(0.05f, 1f)] public float recoveryNormalizedTime = 0.88f;
    [Range(0f, 0.25f)] public float exitBlendSeconds = 0.1f;
    [Header("Root Motion")]
    [Tooltip("InPlace: aucun deplacement racine. AuthoredRootMotion: le clip deplace UCC. ScriptedDash: le script pilote le dash.")]
    public PlayerActionRootMotionMode rootMotionMode = PlayerActionRootMotionMode.AuthoredRootMotion;
    [Header("Facing And Inertia")]
    [Tooltip("UccBody : la capsule UCC conserve l'orientation vers la cible apres l'action. VisualOnly ne tourne que le rig et convient aux poses non dirigees.")]
    public PlayerActionFacingMode facingMode = PlayerActionFacingMode.UccBody;
    public bool allowMoveAfterRecovery = true;
    public bool allowDodgeAfterRecovery = true;
    [Tooltip("Autorise les actions de mobilite a interrompre ce skill a partir de Mobility Cancel Normalized Time.")]
    public bool allowMobilityCancel = true;

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
    [Header("Light Skill Charge")]
    [Tooltip("Charge ajoutee a la jauge LightSkill lorsqu'un impact de cette competence touche reellement la cible.")]
    [Min(0f)] public float lightChargeOnHit = 1f;

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
    [Header("Enemy Motion")]
    [Tooltip("Grounded ignore le root motion vertical. Airborne utilise BeginEnemyAirborne et une chute controlee.")]
    public EnemyActionMotionProfile enemyActionMotion = new EnemyActionMotionProfile();
    [Header("Reaction Telegraph")]
    public CombatReactionTelegraphProfile reactionTelegraph = new CombatReactionTelegraphProfile();

    public string SkillName => string.IsNullOrWhiteSpace(skillName) ? name : skillName;
    public Sprite Icon => icon;
    public AnimationClip AnimationClip => animationClip;
    public string AnimatorState => animatorState;
    public PlayerActionPresentationProfile Presentation => presentation ?? (presentation = PlayerActionPresentationProfile.CreateDefault());
    public float Damages => damages;
    public float LightChargeOnHit => lightChargeOnHit;
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
    public EnemyActionMotionProfile EnemyActionMotion => enemyActionMotion ?? (enemyActionMotion = EnemyActionMotionProfile.GroundedDefault);
    public CombatReactionTelegraphProfile ReactionTelegraph => reactionTelegraph ?? (reactionTelegraph = new CombatReactionTelegraphProfile());

    public bool AcceptsEnemyReaction(RealTimeCombatReaction reaction)
    {
        return acceptedEnemyReactions != null && acceptedEnemyReactions.Contains(reaction);
    }

    public bool IsWithinHitRange(float horizontalDistance)
    {
        return horizontalDistance >= MinimumHitDistance && horizontalDistance <= MaximumHitDistance;
    }
}
