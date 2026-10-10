using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    public bool IsMeasuredLocomotionPresentationActive => isActiveAndEnabled && IsDriving && driveLitLocomotionAnimatorParameters &&
        animator != null && locomotion != null && locomotion.Grounded && !IsFlightModeActive && !IsCinematicMotionSessionActive &&
        animatorMonitor is LitPresentationAnimatorMonitor && ModuleSettings.presentationProfile != null && ModuleSettings.presentationProfile.IsUsable(animator.avatar);
    private bool TryUpdateMeasuredLocomotionPresentation(Vector3 velocity, float speed)
    {
        var profile = ModuleSettings.presentationProfile;
        if (profile == null || !profile.IsUsable(animator.avatar) || !(animatorMonitor is LitPresentationAnimatorMonitor) || locomotion == null || !locomotion.Grounded)
            return false;
        if (IsCinematicMotionSessionActive) return true;
        float clockRate = timeDomain != null ? timeDomain.BaseAnimatorSpeed * timeDomain.Scale : animator.speed;
        if (clockRate <= 0) return true; // Keep the paused presentation unchanged.
        var localVelocity = transform.InverseTransformDirection(velocity);
        var direction = new Vector2(localVelocity.x, localVelocity.z).normalized;
        float actorSpeed = speed / clockRate;
        profile.Evaluate(actorSpeed, sprintPressed, combatLockActive && !IsCombatFreeSprint ? direction : Vector2.up,
            out float presentationSpeed, out float cadence, !combatLockActive);
        SetAnimatorFloat(LocomotionPresentationProfile.PlaybackParameter, cadence);
        bool moving = actorSpeed > profile.stopSpeed;
        if (combatLockActive && !combatDirectionalEvasionFacing)
        {
            // Continue the physical deceleration, even after input release.
            SetAnimatorFloat(speedParam, speed);
            SetAnimatorFloat(horizontalMovementParam, moving && !IsCombatFreeSprint ? direction.x : 0);
            SetAnimatorFloat(forwardMovementParam, moving ? (IsCombatFreeSprint ? 1f : direction.y) : 0);
            SetAnimatorFloat(combatMoveMagnitudeParam, moving ? 1 : 0);
            SetAnimatorFloat(locomotionTierParam, presentationSpeed);
            SetAnimatorBool(isMovingParam, moving);
            SetAnimatorFloat(turnParam, 0);
            if (IsGroundPresentationState(animator.GetCurrentAnimatorStateInfo(0)) &&
                (!animator.IsInTransition(0) || IsGroundPresentationState(animator.GetNextAnimatorStateInfo(0))))
            {
                if (!moving) EnterCombatIdleFromLocomotion();
                else ExitCombatIdleForMovement();
            }
            return true;
        }
        groundedPresentationSpeed = presentationSpeed;
        var current = animator.GetCurrentAnimatorStateInfo(0);
        bool ownsGroundState = IsGroundPresentationState(current) &&
            (!animator.IsInTransition(0) || IsGroundPresentationState(animator.GetNextAnimatorStateInfo(0)));
        float presentationDelta = timeDomain != null ? (Time.inFixedTimeStep ? timeDomain.FixedDeltaTime : timeDomain.DeltaTime) : ResolveGroundedFeelDeltaTime();
        if (ownsGroundState) UpdateGroundedPresentationState(speed, velocity, presentationDelta);
        else
        {
            ResetAnimatorTrigger(moveStartTriggerParam);
            ResetAnimatorTrigger(moveStopTriggerParam);
            groundedPresentationState = LocomotionPresentationState.Idle;
        }
        SetAnimatorFloat(speedParam, presentationSpeed);
        SetGroundedDirectionalAnimatorParameters(presentationSpeed, velocity);
        SetAnimatorBool(isMovingParam, moving || groundedPresentationState != LocomotionPresentationState.Idle);
        SetAnimatorFloat(locomotionTierParam, moving ? ResolveGroundedLocomotionTier() : 0);
        SetAnimatorFloat(turnParam, ResolveGroundedPresentationTurn(velocity));
        RecordLocomotionSample(speed);
        return true;
    }
    private static bool IsGroundPresentationState(AnimatorStateInfo state)
    {
        foreach (string name in GroundPresentationStates)
            if (state.shortNameHash == Animator.StringToHash(name)) return true;
        return false;
    }
    private static readonly string[] GroundPresentationStates =
        { "Locomotion", "CombatLocomotion", "CombatIdle", "Walk_Start", "Run_Start", "Walk_Stop", "Run_Stop", "WalkStartToRun" };
}
