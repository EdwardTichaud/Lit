using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    private LocomotionPresentationResponse measuredCombatResponse;
    private LocomotionPresentationProfile measuredResponseProfile;
#if UNITY_EDITOR
    private LocomotionPresentationResponse explorationBrakeProbeResponse;
#endif
    public bool PreserveWholeBodyLocomotion => ModuleSettings.preserveWholeBodyLocomotion;
    public LocomotionPresentationProfile ActiveMeasuredPresentationProfile => IsMeasuredLocomotionPresentationActive ? ModuleSettings.presentationProfile : null;
    public bool IsMeasuredLocomotionPresentationActive => isActiveAndEnabled && IsDriving && driveLitLocomotionAnimatorParameters &&
        animator != null && locomotion != null && locomotion.Grounded && !IsFlightModeActive && !IsCinematicMotionSessionActive &&
        animatorMonitor is LitPresentationAnimatorMonitor && ModuleSettings.presentationProfile != null && ModuleSettings.presentationProfile.IsUsable(animator.avatar);
    private bool TryUpdateMeasuredLocomotionPresentation(Vector3 velocity, float speed)
    {
        var profile = ModuleSettings.presentationProfile;
        if (profile == null || !profile.IsUsable(animator.avatar) || !(animatorMonitor is LitPresentationAnimatorMonitor) || locomotion == null || !locomotion.Grounded)
        {
            measuredCombatResponse.Reset();
            return false;
        }
        if (IsCinematicMotionSessionActive) return true;
        float clockRate = timeDomain != null ? timeDomain.BaseAnimatorSpeed * timeDomain.Scale : animator.speed;
        if (clockRate <= 0) return true; // Keep the paused presentation unchanged.
        var localVelocity = transform.InverseTransformDirection(velocity);
        var direction = new Vector2(localVelocity.x, localVelocity.z).normalized;
        float actorSpeed = speed / clockRate;
        profile.Evaluate(actorSpeed, sprintPressed, combatLockActive && !IsCombatFreeSprint ? direction : Vector2.up,
            out float presentationSpeed, out float cadence, !combatLockActive);
        if(measuredResponseProfile!=profile){measuredCombatResponse.Reset();measuredResponseProfile=profile;}
        bool moving = actorSpeed > profile.stopSpeed;
        var state=animator.GetCurrentAnimatorStateInfo(0);
        if(moving&&combatLockActive&&!IsCombatFreeSprint&&!combatDirectionalEvasionFacing&&
            IsGroundPresentationState(state)&&(!animator.IsInTransition(0)||IsGroundPresentationState(animator.GetNextAnimatorStateInfo(0))))
        {
            float delta=timeDomain!=null?timeDomain.DeltaTime:Time.deltaTime;
            measuredCombatResponse.Evaluate(Time.frameCount,delta,direction,presentationSpeed,profile.directionDegreesPerSecond,
                profile.gaitBlendSeconds>0?(profile.blendThresholds.y-profile.blendThresholds.x)/profile.gaitBlendSeconds:0,
                out direction,out presentationSpeed);
            cadence=profile.CadenceForPresentedBlend(actorSpeed,direction,presentationSpeed);
        }
        else measuredCombatResponse.Reset();
        SetAnimatorFloat(LocomotionPresentationProfile.PlaybackParameter, cadence);
        if (combatLockActive && !combatDirectionalEvasionFacing)
        {
            // Continue the physical deceleration, even after input release.
            SetAnimatorFloat(speedParam, speed);
            bool keepOutgoingGait = false;
#if UNITY_EDITOR
            keepOutgoingGait = !moving && state.fullPathHash == CombatLocomotionStateHash &&
                GetComponent<LitPresentationCharacterIK>()?.KeepCombatDirectionForProbe == true;
#endif
            if (!keepOutgoingGait)
            {
                SetAnimatorFloat(horizontalMovementParam, moving && !IsCombatFreeSprint ? direction.x : 0);
                SetAnimatorFloat(forwardMovementParam, moving ? (IsCombatFreeSprint ? 1f : direction.y) : 0);
                SetAnimatorFloat(locomotionTierParam, presentationSpeed);
            }
            SetAnimatorFloat(combatMoveMagnitudeParam, moving ? 1 : 0);
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
        var stateBeforeUpdate = groundedPresentationState;
        if (ownsGroundState) UpdateGroundedPresentationState(speed, velocity, presentationDelta);
        else
        {
            ResetAnimatorTrigger(moveStartTriggerParam);
            ResetAnimatorTrigger(moveStopTriggerParam);
            groundedPresentationState = LocomotionPresentationState.Idle;
        }
        bool runStopEnabled = profile.runStop?.IsConfigured == true;
        var runStopSolver = runStopEnabled ? GetComponent<LitPresentationCharacterIK>() : null;
        if (runStopSolver != null && stateBeforeUpdate != LocomotionPresentationState.Stopping &&
            groundedPresentationState == LocomotionPresentationState.Stopping)
            runStopSolver.PrepareMeasuredRunStop(profile,current,animator.GetFloat(speedParam));

        float visualPresentationSpeed = presentationSpeed;
        if (runStopSolver != null && ownsGroundState && !groundedMoveIntent &&
            current.IsName("Base Layer.Locomotion"))
        {
            float previousBlend = animator.GetFloat(speedParam);
            if (Mathf.Abs(previousBlend - profile.explorationBlendThresholds.y) < .001f)
            {
                visualPresentationSpeed = previousBlend;
                SetAnimatorFloat(LocomotionPresentationProfile.PlaybackParameter, moving
                    ? profile.CadenceForPresentedBlend(actorSpeed,Vector2.up,previousBlend,exploration:true)
                    : profile.minimumCadence);
            }
        }
#if UNITY_EDITOR
        if (ownsGroundState && GetComponent<LitPresentationCharacterIK>()?.SmoothExplorationForProbe == true)
        {
            // The state machine still receives the obtained speed and original triggers.
            // Only the exploration tree's pose selection uses the eased value.
            float delta = timeDomain != null ? timeDomain.DeltaTime : Time.deltaTime;
            explorationBrakeProbeResponse.Evaluate(Time.frameCount, delta, Vector2.up, presentationSpeed,
                0, profile.explorationBlendThresholds.x / .14f, out _, out visualPresentationSpeed);
            float visualCadence = !moving && visualPresentationSpeed > .001f ? profile.minimumCadence :
                profile.CadenceForPresentedBlend(actorSpeed, Vector2.up, visualPresentationSpeed, exploration: true);
            SetAnimatorFloat(LocomotionPresentationProfile.PlaybackParameter, visualCadence);
        }
        else explorationBrakeProbeResponse.Reset();
#endif
        // Keep the outgoing exploration pose while it fades into a start clip.
        // Reweighting its blend tree with the new speed would animate two starts.
        bool leavingLocomotionForStart = current.IsName("Base Layer.Locomotion") &&
            groundedPresentationState == LocomotionPresentationState.Starting &&
            (!animator.IsInTransition(0) || IsGroundedStartState(animator.GetNextAnimatorStateInfo(0)));
        if (!leavingLocomotionForStart)
            SetAnimatorFloat(speedParam, visualPresentationSpeed);
        SetGroundedDirectionalAnimatorParameters(presentationSpeed, velocity);
        SetAnimatorBool(isMovingParam, moving || groundedPresentationState != LocomotionPresentationState.Idle);
        float tier = moving ? ResolveGroundedLocomotionTier() : 0;
        // While starting, the authored WalkStartToRun transition follows the
        // dominant measured gait even when the sprint button is released.
        if (moving && ownsGroundState && IsGroundedStartStateActive() &&
            presentationSpeed >= (profile.explorationBlendThresholds.x + profile.explorationBlendThresholds.y) * .5f)
            tier = runPresentationSpeed;
        SetAnimatorFloat(locomotionTierParam, tier);
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
