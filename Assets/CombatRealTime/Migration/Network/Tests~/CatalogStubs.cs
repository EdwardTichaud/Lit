// Managed catalog fixtures only. Native Animator execution is not tested by these doubles.
using UnityEngine;

public enum SkillTargetingMode { Unconfigured = -1, FreeAttack, OptionalTarget, RequiredTarget, CasterArea, SelectedAlly, Self, AllyArea, LockedTarget }
public enum BasicSkillContext { Grounded, Airborne }
public enum PlayerActionMovementPolicy { VisualOnly, StateTrajectory, ExistingScripted }
public enum SkillHealingTarget { None, Self, PlayersInRadius, AllyRegeneration, EnemyLifeSteal }
public enum SkillProtectionKind { None, Intercession, Rempart, Sommation, JusteRetour }
public enum SkillControlKind { None, Revelation, Entrave, PasDeRecul }
public sealed class PresentationProfile { public PlayerActionMovementPolicy movementPolicy; }
public sealed class LungeProfile
{
    public bool enabled, resolveImpactAtContact, passThroughTarget;
    public AnimationClip contactAnimationSlot;
    public float passThroughDistance = 4, stoppingDistance = .85f, contactTolerance = .18f,
        approachDurationSeconds = .25f, maximumApproachSpeed = 65, reboundHorizontalImpulse, reboundVerticalImpulse;
}
public sealed class RetreatProfile
{
    public bool enabled;
    public float horizontalImpulse = 5, verticalImpulse = 4, maximumHorizontalImpulse = 10,
        maximumVerticalImpulse = 8, minimumInputLockSeconds = .1f, maximumInputLockSeconds = .4f,
        airborneInertiaSeconds = .2f, airborneInertiaEndSpeedMultiplier = .5f;
}
public sealed class GroundImpactProfile
{
    public float radius = 5, descentSpeed = 15, maximumDescentSeconds = 2,
        airborneDamageMultiplier = 2, groundPushDistance = 1, airbornePushDistance = 3;
}
public sealed class CombatCinematicRig : Object { public Object RuntimeTimeline; }
public sealed class CinematicProfile
{
    public CombatCinematicRig CombatCinematicRigPrefab;
    public string PlayerAnimatorTrackName = "Player.Animator", EnemyAnimatorTrackName = "Enemy.Animator";
}
public sealed class CounterSkillSO : ScriptableObject { }
public sealed class LightSkillSO : ScriptableObject { }
public sealed class ProtectionProfile
{
    public SkillProtectionKind kind;
    public float radius = 5, maximumLinkDistance = 20, durationSeconds = 4, reduction = .3f, transferFraction = .75f, reserveMaxHealthFraction = .5f;
}
public sealed class ControlProfile
{
    public SkillControlKind kind;
    public float range = 25, radius = 4, durationSeconds = 6, strength = .2f, distance = 5;
}
public class SkillSO : ScriptableObject
{
    public bool HasCombatCinematic, IsGroundImpactSkill;
    public CinematicProfile Cinematic = new CinematicProfile();
    public GroundImpactProfile groundImpact = new GroundImpactProfile();
    public LungeProfile TargetLunge = new LungeProfile();
    public RetreatProfile RetreatImpulse = new RetreatProfile();
    public PresentationProfile Presentation = new PresentationProfile();
    public SkillTargetingMode targetingMode;
    public AnimationClip AnimationClip;
    public string AnimatorState;
    public bool useLaunchRange;
    public float minimumLaunchDistance, maximumLaunchDistance = 10, minimumHitDistance, maximumHitDistance = 10;
    public SkillHealingTarget healingTarget;
    public float healingPercent, healingRadius = 6, regenerationTickSeconds = 1;
    public int regenerationHealthPerTick = 1;
    public ProtectionProfile protection = new ProtectionProfile();
    public ControlProfile control = new ControlProfile();
    public bool IsHealingSkill => healingTarget == SkillHealingTarget.AllyRegeneration || healingTarget == SkillHealingTarget.EnemyLifeSteal || healingTarget != SkillHealingTarget.None && healingPercent > 0;
    public bool IsProtectionSkill => protection.kind != SkillProtectionKind.None;
    public bool IsControlSkill => control.kind != SkillControlKind.None;
}
public sealed class BasicSkillsSO : SkillSO { public BasicSkillContext Context; }
public sealed class ChargedSkillSO : SkillSO
{
    public AnimationClip preparationClip;
    public string preparationState;
    public float chargeSeconds = 2, launchHeight = 3, ascentSeconds = .3f, airborneHoldSeconds = 1.2f;
    public float descentInitialSpeed = 2.5f, descentAcceleration = 42, maximumDescentSpeed = 18;
}
