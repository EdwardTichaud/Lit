using UnityEngine;

public sealed partial class EnemyController : ICombatChargedTarget
{
    private bool chargedReaction;
    private float chargedRiseLeft, chargedRiseSpeed, chargedFloatLeft;
    private string chargedHitState;
    public Transform ChargedRoot => this != null ? transform : null;
    public bool ChargedAlive => this != null && Health != null && !Health.IsDead && isActiveAndEnabled;
    public bool ChargedProtected => this == null || IsCinematicSuspended;
    public int ChargedGeneration => ActionSequenceId;
    public bool IsChargedAirborne => chargedReaction;
    public bool CanReceiveChargedLaunch => BrainAuthority && CombatEnabled && ChargedAlive && !IsCinematicSuspended &&
        !chargedReaction && !IsFlameDormant && (State == CombatEnemyPhysicsState.Navigation || State == CombatEnemyPhysicsState.GroundedAction) && IsOperational &&
        PhysicsNavigationAgent != null && PhysicsNavigationAgent.enabled && PhysicsNavigationAgent.isOnNavMesh && !PhysicsNavigationAgent.isOnOffMeshLink;

    public bool BeginChargedLaunch(ChargedSkillSO skill)
    {
        if (skill == null || !CanReceiveChargedLaunch) return false;
        CancelAction("projection chargee");
        ActionSequenceId++;
        BeginEnemyAction(skill);
        PhysicsActiveMotionProfile = new EnemyActionMotionProfile
        {
            movementMode = EnemyActionMovementMode.Airborne,
            initialUpwardSpeed = skill.launchHeight / Mathf.Max(.1f, skill.ascentSeconds),
            maximumAirborneSeconds = Mathf.Max(4, skill.ascentSeconds + skill.airborneHoldSeconds + 2)
        };
        chargedRiseLeft = skill.ascentSeconds;
        chargedRiseSpeed = PhysicsActiveMotionProfile.initialUpwardSpeed;
        chargedFloatLeft = skill.airborneHoldSeconds;
        chargedHitState = skill.enemyFloatState;
        chargedReaction = true;
        PhysicsVerticalVelocity = chargedRiseSpeed;
        PhysicsAirborneStartedAt = PhysicsLocalTime;
        PhysicsSetState(CombatEnemyPhysicsState.AirborneAction, "projection chargee");
        PlayChargedReaction();
        return true;
    }

    public void BeginChargedFloat(float seconds)
    { if (chargedReaction) chargedFloatLeft = Mathf.Max(0, seconds); }

    public void ReleaseChargedHold()
    { chargedRiseLeft = chargedFloatLeft = 0; }

    private bool TryChargedReactionDelta(Vector3 position, out Vector3 delta)
    {
        delta = Vector3.zero;
        if (!chargedReaction) return false;
        if (!ChargedAlive || IsCinematicSuspended || PhysicsLandingRequested) ReleaseChargedHold();
        if (chargedRiseLeft > 0)
        {
            float step = chargedRiseSpeed * Mathf.Min(PhysicsLocalFixedDeltaTime, chargedRiseLeft);
            chargedRiseLeft = Mathf.Max(0, chargedRiseLeft - PhysicsLocalFixedDeltaTime);
            if (PhysicsTryClampMotionToObstacle(position, Vector3.up, step, out float allowed))
            { step = allowed; chargedRiseLeft = 0; }
            PhysicsVerticalVelocity = chargedRiseSpeed;
            delta = Vector3.up * step;
            PlayChargedReaction();
            return true;
        }
        if (chargedFloatLeft <= 0) return false;
        chargedFloatLeft = Mathf.Max(0, chargedFloatLeft - PhysicsLocalFixedDeltaTime);
        PhysicsVerticalVelocity = 0;
        PlayChargedReaction();
        return true;
    }

    private void PlayChargedReaction()
    {
        var animator = Animator;
        if (!ChargedAlive || animator == null || !animator.isActiveAndEnabled || IsCinematicSuspended) return;
        string state = chargedHitState;
        if (string.IsNullOrWhiteSpace(state) || !animator.HasState(0, Animator.StringToHash(state)))
            state = Configuration.ActorHitAnimatorState;
        if (!animator.HasState(0, Animator.StringToHash(state))) return;
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(state) &&
            (!animator.IsInTransition(0) || !animator.GetNextAnimatorStateInfo(0).IsName(state)))
            animator.CrossFadeInFixedTime(state, .08f, 0, 0);
    }
}
