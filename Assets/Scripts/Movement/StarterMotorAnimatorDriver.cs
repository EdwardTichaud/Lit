using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(StarterInspiredThirdPersonMotor))]
[RequireComponent(typeof(Animator))]
public sealed class StarterMotorAnimatorDriver : MonoBehaviour
{
    private enum JumpPhase
    {
        Grounded = 0,
        Takeoff = 1,
        Airborne = 2,
        LandingRecovery = 3
    }

    private enum LandingType
    {
        None = 0,
        Recovery = 1
    }

    private enum ReconciliationFamily
    {
        None = 0,
        Airborne = 1,
        Landing = 2,
        GroundedMoving = 3,
        GroundedIdle = 4,
        WallSlide = 5
    }

    [Header("References")]
    [SerializeField] private StarterInspiredThirdPersonMotor motor;
    [SerializeField] private Animator animator;
    [SerializeField] private bool disableRootMotion = true;

    [Header("Locomotion")]
    [SerializeField, Min(0.01f)] private float motorFullSpeed = 3.25f;
    [SerializeField, Min(0.01f)] private float locomotionBlendMax = 3.25f;
    [SerializeField, Min(0f)] private float speedDampTime = 0.05f;
    [SerializeField, Min(0f)] private float motionSpeedDampTime = 0.05f;
    [SerializeField, Min(0f)] private float movingEnterSpeedThreshold = 0.32f;
    [SerializeField, Min(0f)] private float movingExitSpeedThreshold = 0.12f;
    [SerializeField, Range(0f, 1f)] private float walkTierThreshold = 0.33f;
    [SerializeField, Range(0f, 1f)] private float jogTierThreshold = 0.72f;

    [Header("Airborne")]
    [SerializeField, Min(0f)] private float landingVisualHoldTime = 0.34f;
    [SerializeField] private bool crossFadeJumpStates = true;
    [SerializeField, Min(0f)] private float jumpCrossFadeDuration = 0.08f;
    [SerializeField, Min(0f)] private float fallCrossFadeDuration = 0.1f;
    [SerializeField, Min(0f)] private float landingCrossFadeDuration = 0.08f;
    [SerializeField] private int animatorLayer = 0;

    [Header("State Names")]
    [SerializeField] private string jumpTakeoffStateName = "Jump_Takeoff";
    [SerializeField] private string jumpAirborneStateName = "Jump_Airborne";
    [SerializeField] private string freeFallStateName = "Falling";
    [SerializeField] private string landingStateName = "Landing";
    [SerializeField] private string jumpLandingStateName = "Jump_Land";
    [SerializeField] private string heavyLandingStateName = "Landing_Hard";
    [SerializeField] private string wallSlideRightStateName = "Slide_Wall_Right";
    [SerializeField] private string wallSlideLeftStateName = "Slide_Wall_Left";
    [SerializeField] private string locomotionStateName = "Locomotion";
    [SerializeField] private string walkStartStateName = "Walk_Start";
    [SerializeField] private string jogtrotStartStateName = "Jogtrot_Start";
    [SerializeField] private string runStartStateName = "Run_Start";
    [SerializeField] private string walkStopStateName = "Walk_Stop";
    [SerializeField] private string jogtrotStopStateName = "Jogtrot_Stop";
    [SerializeField] private string runStopStateName = "Run_Stop";

    [Header("Parameters")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string motionSpeedParam = "MotionSpeed";
    [SerializeField] private string groundedParam = "Grounded";
    [SerializeField] private string jumpBoolParam = "Jump";
    [SerializeField] private string freeFallParam = "FreeFall";
    [SerializeField] private string verticalSpeedParam = "VerticalSpeed";
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveStartTriggerParam = "MoveStartTrigger";
    [SerializeField] private string moveStopTriggerParam = "MoveStopTrigger";
    [SerializeField] private string locomotionTierParam = "LocomotionTier";
    [SerializeField] private string jumpTriggerParam = "JumpTrigger";
    [SerializeField] private string isAirborneParam = "IsAirborne";
    [SerializeField] private string landingTriggerParam = "LandTrigger";
    [SerializeField] private string landingTriggerFallbackParam = "LandingTrigger";
    [SerializeField] private string landingBoolParam = "Landing";
    [SerializeField] private string landingTypeParam = "LandingType";
    [SerializeField] private string jumpFromMovementParam = "JumpFromMovement";
    [SerializeField] private string jumpPhaseParam = "JumpPhase";

    [Header("State Reconciliation")]
    [SerializeField] private bool reconcileAnimatorState = true;
    [SerializeField, Min(0f)] private float airborneResyncDelay = 0.16f;
    [SerializeField, Min(0f)] private float landingResyncDelay = 0.12f;
    [SerializeField, Min(0f)] private float locomotionResyncDelay = 0.18f;
    [SerializeField, Min(0f)] private float idleResyncDelay = 0.28f;
    [SerializeField, Min(0f)] private float resyncCrossFadeDuration = 0.05f;
    [SerializeField, Min(1f)] private float oneShotCompatibleNormalizedTime = 1.15f;
    [SerializeField] private bool interruptStopStatesOnMoveResume = true;
    [SerializeField] private bool interruptStartStatesOnMoveStop = true;
    [SerializeField, Min(0f)] private float stopInterruptCrossFadeDuration = 0.04f;
    [SerializeField] private bool forceAnimatorStateFromMotor = true;
    [SerializeField] private bool suppressGroundedStartStopTriggersWhenAuthoritative = true;
    [SerializeField, Min(0f)] private float forcedStateCrossFadeDuration = 0.03f;

    [Header("Debug")]
    [SerializeField] private bool showDebugValues = true;
    [SerializeField] private float debugAnimatorSpeed;
    [SerializeField] private float debugMotionSpeed;
    [SerializeField] private bool debugGrounded;
    [SerializeField] private bool debugIsMoving;
    [SerializeField] private bool debugAirborne;
    [SerializeField] private bool debugFreeFall;
    [SerializeField] private bool debugWallSliding;
    [SerializeField] private int debugWallSlideSide;
    [SerializeField] private bool debugJumpTriggered;
    [SerializeField] private bool debugLandingTriggered;
    [SerializeField] private StarterInspiredThirdPersonMotor.LandingSeverity debugLandingSeverity;
    [SerializeField] private float debugLocomotionTier;
    [SerializeField] private int debugJumpPhase;
    [SerializeField] private int debugLandingType;
    [SerializeField] private int debugCurrentStateShortHash;
    [SerializeField] private bool debugRootMotionDisabled;

    private readonly Dictionary<int, AnimatorControllerParameterType> parameterTypes = new();
    private float landingVisualTimer;
    private bool wasMoving;
    private bool wasFreeFalling;
    private bool wasWallSliding;
    private int lastWallSlideSide;
    private bool jumpSequenceActive;
    private bool lastJumpFromMovement;
    private float lastMovingLocomotionTier = 1f;
    private ReconciliationFamily activeReconciliationFamily = ReconciliationFamily.None;
    private float stateMismatchTimer;

    private int speedHash;
    private int motionSpeedHash;
    private int groundedHash;
    private int jumpBoolHash;
    private int freeFallHash;
    private int verticalSpeedHash;
    private int isMovingHash;
    private int moveStartTriggerHash;
    private int moveStopTriggerHash;
    private int locomotionTierHash;
    private int jumpTriggerHash;
    private int isAirborneHash;
    private int landingTriggerHash;
    private int landingTriggerFallbackHash;
    private int landingBoolHash;
    private int landingTypeHash;
    private int jumpFromMovementHash;
    private int jumpPhaseHash;

    public float DebugAnimatorSpeed => debugAnimatorSpeed;
    public float DebugMotionSpeed => debugMotionSpeed;
    public bool DebugGrounded => debugGrounded;
    public bool DebugIsMoving => debugIsMoving;
    public bool DebugAirborne => debugAirborne;
    public bool DebugFreeFall => debugFreeFall;
    public bool DebugWallSliding => debugWallSliding;
    public int DebugWallSlideSide => debugWallSlideSide;
    public bool DebugJumpTriggered => debugJumpTriggered;
    public bool DebugLandingTriggered => debugLandingTriggered;
    public StarterInspiredThirdPersonMotor.LandingSeverity DebugLandingSeverity => debugLandingSeverity;
    public float DebugLocomotionTier => debugLocomotionTier;
    public int DebugJumpPhase => debugJumpPhase;
    public int DebugLandingType => debugLandingType;

    private void Reset()
    {
        motor = GetComponent<StarterInspiredThirdPersonMotor>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<StarterInspiredThirdPersonMotor>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        RefreshHashes();
        CacheAnimatorParameters();
        ConfigureAnimator();
        Tick(0f);
    }

    private void OnValidate()
    {
        motorFullSpeed = Mathf.Max(0.01f, motorFullSpeed);
        locomotionBlendMax = Mathf.Max(0.01f, locomotionBlendMax);
        speedDampTime = Mathf.Max(0f, speedDampTime);
        motionSpeedDampTime = Mathf.Max(0f, motionSpeedDampTime);
        movingEnterSpeedThreshold = Mathf.Max(0f, movingEnterSpeedThreshold);
        movingExitSpeedThreshold = Mathf.Clamp(movingExitSpeedThreshold, 0f, movingEnterSpeedThreshold);
        walkTierThreshold = Mathf.Clamp01(walkTierThreshold);
        jogTierThreshold = Mathf.Clamp(jogTierThreshold, walkTierThreshold, 1f);
        landingVisualHoldTime = Mathf.Max(0f, landingVisualHoldTime);
        jumpCrossFadeDuration = Mathf.Max(0f, jumpCrossFadeDuration);
        fallCrossFadeDuration = Mathf.Max(0f, fallCrossFadeDuration);
        landingCrossFadeDuration = Mathf.Max(0f, landingCrossFadeDuration);
        animatorLayer = Mathf.Max(0, animatorLayer);
        airborneResyncDelay = Mathf.Max(0f, airborneResyncDelay);
        landingResyncDelay = Mathf.Max(0f, landingResyncDelay);
        locomotionResyncDelay = Mathf.Max(0f, locomotionResyncDelay);
        idleResyncDelay = Mathf.Max(0f, idleResyncDelay);
        resyncCrossFadeDuration = Mathf.Max(0f, resyncCrossFadeDuration);
        oneShotCompatibleNormalizedTime = Mathf.Max(1f, oneShotCompatibleNormalizedTime);
        stopInterruptCrossFadeDuration = Mathf.Max(0f, stopInterruptCrossFadeDuration);
        forcedStateCrossFadeDuration = Mathf.Max(0f, forcedStateCrossFadeDuration);
        RefreshHashes();
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    public void ResetAnimationState()
    {
        landingVisualTimer = 0f;
        wasMoving = false;
        wasFreeFalling = false;
        wasWallSliding = false;
        lastWallSlideSide = 0;
        jumpSequenceActive = false;
        lastJumpFromMovement = false;
        lastMovingLocomotionTier = 1f;
        ResetStateReconciliation();
        debugWallSliding = false;
        debugWallSlideSide = 0;
        debugJumpTriggered = false;
        debugLandingTriggered = false;
        debugLandingSeverity = StarterInspiredThirdPersonMotor.LandingSeverity.None;
        debugLocomotionTier = 1f;

        if (animator == null)
        {
            return;
        }

        ConfigureAnimator();
        if (parameterTypes.Count == 0 && animator.runtimeAnimatorController != null)
        {
            CacheAnimatorParameters();
        }

        SetFloat(speedHash, 0f, 0f, 0f);
        SetFloat(motionSpeedHash, 0f, 0f, 0f);
        SetFloat(verticalSpeedHash, 0f, 0f, 0f);
        SetFloat(locomotionTierHash, 1f, 0f, 0f);
        SetBool(isMovingHash, false);
        SetBool(isAirborneHash, false);
        SetBool(freeFallHash, false);
        SetBool(jumpBoolHash, false);
        SetBool(landingBoolHash, false);
        SetBool(jumpFromMovementHash, false);
        SetBool(groundedHash, motor == null || motor.StableGrounded);
        SetInt(jumpPhaseHash, (int)JumpPhase.Grounded);
        SetInt(landingTypeHash, (int)LandingType.None);
    }

    private void Tick(float deltaTime)
    {
        debugJumpTriggered = false;
        debugLandingTriggered = false;

        if (motor == null || animator == null)
        {
            return;
        }

        ConfigureAnimator();

        if (parameterTypes.Count == 0 && animator.runtimeAnimatorController != null)
        {
            CacheAnimatorParameters();
        }

        if (motor.IsLadderTraversalActive)
        {
            HoldNeutralForLadder();
            return;
        }

        UpdateLandingTimer(deltaTime);
        if (motor.LandingTriggered)
        {
            landingVisualTimer = landingVisualHoldTime;
        }

        float actualSpeed = motor.ActualSpeed;
        float normalizedSpeed = Mathf.Clamp01(actualSpeed / motorFullSpeed);
        float animatorSpeed = normalizedSpeed * locomotionBlendMax;
        float motionSpeed = normalizedSpeed;
        bool landingActive = landingVisualTimer > 0f;
        bool moving = ResolveMovingState(actualSpeed, motor.StableGrounded, landingActive);
        float locomotionTier = ResolveDrivenLocomotionTier(actualSpeed, moving);
        bool airborne = motor.Airborne || !motor.StableGrounded;
        bool freeFall = motor.FreeFall;
        bool wallSliding = motor.WallSliding && airborne && !landingActive;
        int wallSlideSide = NormalizeWallSlideSide(motor.WallSlideSide);

        if (motor.JumpStarted)
        {
            jumpSequenceActive = true;
            lastJumpFromMovement = actualSpeed > movingExitSpeedThreshold;
        }

        DriveLocomotion(animatorSpeed, motionSpeed, locomotionTier, moving, deltaTime);
        DriveAirborne(airborne, freeFall, landingActive);
        bool suppressGroundedOneShots = ShouldSuppressGroundedOneShotTransitions(airborne, landingActive, wallSliding);
        DriveTransitions(moving, freeFall, wallSliding, wallSlideSide, suppressGroundedOneShots);
        bool authoritativeStateApplied = ForceExpectedAnimatorState(
            moving,
            airborne,
            freeFall,
            landingActive,
            wallSliding,
            wallSlideSide);

        if (!authoritativeStateApplied)
        {
            InterruptStopStateIfMovementResumed(moving, airborne, landingActive, wallSliding);
            InterruptStartStateIfMovementStopped(moving, airborne, landingActive, wallSliding);
            ReconcileAnimatorState(moving, airborne, freeFall, landingActive, wallSliding, wallSlideSide, deltaTime);
        }

        wasMoving = moving;
        wasFreeFalling = freeFall && !wallSliding;
        wasWallSliding = wallSliding;
        lastWallSlideSide = wallSlideSide;

        UpdateDebugValues(animatorSpeed, motionSpeed, locomotionTier, moving, airborne, freeFall, landingActive, wallSliding, wallSlideSide);
    }

    private void HoldNeutralForLadder()
    {
        landingVisualTimer = 0f;
        wasMoving = false;
        wasFreeFalling = false;
        wasWallSliding = false;
        lastWallSlideSide = 0;
        jumpSequenceActive = false;
        lastJumpFromMovement = false;
        ResetStateReconciliation();

        SetFloat(speedHash, 0f, 0f, 0f);
        SetFloat(motionSpeedHash, 0f, 0f, 0f);
        SetFloat(verticalSpeedHash, 0f, 0f, 0f);
        SetFloat(locomotionTierHash, 0f, 0f, 0f);
        SetBool(groundedHash, false);
        SetBool(isMovingHash, false);
        SetBool(isAirborneHash, false);
        SetBool(freeFallHash, false);
        SetBool(jumpBoolHash, false);
        SetBool(landingBoolHash, false);
        SetBool(jumpFromMovementHash, false);
        SetInt(jumpPhaseHash, (int)JumpPhase.Grounded);
        SetInt(landingTypeHash, (int)LandingType.None);
        ResetTrigger(moveStartTriggerHash);
        ResetTrigger(moveStopTriggerHash);
        ResetTrigger(jumpTriggerHash);
        ResetTrigger(landingTriggerHash);
        ResetTrigger(landingTriggerFallbackHash);

        if (!showDebugValues)
        {
            return;
        }

        debugAnimatorSpeed = 0f;
        debugMotionSpeed = 0f;
        debugGrounded = false;
        debugIsMoving = false;
        debugAirborne = false;
        debugFreeFall = false;
        debugWallSliding = false;
        debugWallSlideSide = 0;
        debugLandingTriggered = false;
        debugLandingSeverity = StarterInspiredThirdPersonMotor.LandingSeverity.None;
        debugLocomotionTier = 0f;
        debugJumpPhase = (int)JumpPhase.Grounded;
        debugLandingType = (int)LandingType.None;
        debugRootMotionDisabled = animator != null && !animator.applyRootMotion;
        debugCurrentStateShortHash = animator != null && animator.layerCount > animatorLayer
            ? animator.GetCurrentAnimatorStateInfo(animatorLayer).shortNameHash
            : 0;
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            return;
        }

        if (disableRootMotion)
        {
            animator.applyRootMotion = false;
        }
    }

    private void UpdateLandingTimer(float deltaTime)
    {
        if (landingVisualTimer > 0f)
        {
            landingVisualTimer = Mathf.Max(0f, landingVisualTimer - deltaTime);
            if (landingVisualTimer <= 0f)
            {
                jumpSequenceActive = false;
            }
        }
    }

    private bool ResolveMovingState(float actualSpeed, bool grounded, bool landingActive)
    {
        if (!grounded || landingActive)
        {
            return false;
        }

        if (actualSpeed >= movingEnterSpeedThreshold)
        {
            return true;
        }

        if (actualSpeed <= movingExitSpeedThreshold)
        {
            return false;
        }

        return wasMoving;
    }

    private float ResolveDrivenLocomotionTier(float actualSpeed, bool moving)
    {
        if (moving)
        {
            float tierSelectionSpeed = Mathf.Max(actualSpeed, motor.DesiredSpeed);
            lastMovingLocomotionTier = ResolveLocomotionTier(Mathf.Clamp01(tierSelectionSpeed / motorFullSpeed));
            return lastMovingLocomotionTier;
        }

        return lastMovingLocomotionTier;
    }

    private void DriveLocomotion(
        float animatorSpeed,
        float motionSpeed,
        float locomotionTier,
        bool moving,
        float deltaTime)
    {
        SetFloat(speedHash, animatorSpeed, speedDampTime, deltaTime);
        SetFloat(motionSpeedHash, motionSpeed, motionSpeedDampTime, deltaTime);
        SetBool(groundedHash, motor.StableGrounded);
        SetBool(isMovingHash, moving);
        SetFloat(locomotionTierHash, locomotionTier, 0f, deltaTime);
        SetFloat(verticalSpeedHash, motor.VerticalVelocity, 0f, deltaTime);
    }

    private void DriveAirborne(bool airborne, bool freeFall, bool landingActive)
    {
        JumpPhase phase = ResolveJumpPhase(airborne, landingActive);
        LandingType landingType = landingActive ? LandingType.Recovery : LandingType.None;

        SetBool(isAirborneHash, airborne && !landingActive);
        SetBool(freeFallHash, freeFall);
        SetBool(jumpBoolHash, jumpSequenceActive && !landingActive);
        SetBool(landingBoolHash, landingActive);
        SetBool(jumpFromMovementHash, lastJumpFromMovement);
        SetInt(jumpPhaseHash, (int)phase);
        SetInt(landingTypeHash, (int)landingType);
    }

    private void DriveTransitions(
        bool moving,
        bool freeFall,
        bool wallSliding,
        int wallSlideSide,
        bool suppressGroundedOneShots)
    {
        ClearOpposingMovementTrigger(moving);

        if (suppressGroundedOneShots)
        {
            ResetTrigger(moveStartTriggerHash);
            ResetTrigger(moveStopTriggerHash);
        }
        else if (moving && !wasMoving)
        {
            SetTrigger(moveStartTriggerHash);
        }
        else if (!moving && wasMoving && motor.StableGrounded)
        {
            SetTrigger(moveStopTriggerHash);
        }

        if (motor.JumpStarted)
        {
            SetTrigger(jumpTriggerHash);
            debugJumpTriggered = true;
            CrossFadeState(jumpTakeoffStateName, jumpCrossFadeDuration);
            return;
        }

        if (motor.LandingTriggered)
        {
            SetTrigger(landingTriggerHash);
            SetTrigger(landingTriggerFallbackHash);
            debugLandingTriggered = true;
            CrossFadeState(ResolveLandingStateName(), landingCrossFadeDuration);
            return;
        }

        if (wallSliding && (!wasWallSliding || wallSlideSide != lastWallSlideSide))
        {
            CrossFadeState(ResolveWallSlideStateName(wallSlideSide), fallCrossFadeDuration);
            return;
        }

        if (freeFall && !wasFreeFalling && !wallSliding)
        {
            CrossFadeState(jumpSequenceActive ? jumpAirborneStateName : freeFallStateName, fallCrossFadeDuration);
        }
    }

    private bool ShouldSuppressGroundedOneShotTransitions(bool airborne, bool landingActive, bool wallSliding)
    {
        return forceAnimatorStateFromMotor &&
               suppressGroundedStartStopTriggersWhenAuthoritative &&
               motor.StableGrounded &&
               !airborne &&
               !landingActive &&
               !wallSliding;
    }

    private void ClearOpposingMovementTrigger(bool moving)
    {
        if (moving)
        {
            ResetTrigger(moveStopTriggerHash);
        }
        else
        {
            ResetTrigger(moveStartTriggerHash);
        }
    }

    private void InterruptStopStateIfMovementResumed(
        bool moving,
        bool airborne,
        bool landingActive,
        bool wallSliding)
    {
        if (!interruptStopStatesOnMoveResume ||
            animator == null ||
            !moving ||
            airborne ||
            landingActive ||
            wallSliding ||
            !motor.StableGrounded ||
            !IsInGroundedStopState())
        {
            return;
        }

        ResetTrigger(moveStopTriggerHash);
        ResetTrigger(moveStartTriggerHash);
        ResetStateReconciliation();
        CrossFadeState(locomotionStateName, stopInterruptCrossFadeDuration, true);
    }

    private void InterruptStartStateIfMovementStopped(
        bool moving,
        bool airborne,
        bool landingActive,
        bool wallSliding)
    {
        if (!interruptStartStatesOnMoveStop ||
            animator == null ||
            moving ||
            airborne ||
            landingActive ||
            wallSliding ||
            !motor.StableGrounded ||
            !IsInGroundedStartState())
        {
            return;
        }

        ResetTrigger(moveStartTriggerHash);
        ResetTrigger(moveStopTriggerHash);
        ResetStateReconciliation();
        CrossFadeState(ResolveStopStateName(lastMovingLocomotionTier), stopInterruptCrossFadeDuration, true);
    }

    private bool ForceExpectedAnimatorState(
        bool moving,
        bool airborne,
        bool freeFall,
        bool landingActive,
        bool wallSliding,
        int wallSlideSide)
    {
        if (!forceAnimatorStateFromMotor || animator == null)
        {
            return false;
        }

        if (landingActive)
        {
            ForceAnimatorStateIfNeeded(
                ResolveLandingStateName(),
                landingCrossFadeDuration,
                IsAnimatorResolvedToAny(landingStateName, jumpLandingStateName, heavyLandingStateName));
            return true;
        }

        if (wallSliding)
        {
            string wallSlideStateName = ResolveWallSlideStateName(wallSlideSide);
            ForceAnimatorStateIfNeeded(
                wallSlideStateName,
                forcedStateCrossFadeDuration,
                IsAnimatorResolvedToAny(wallSlideStateName));
            return true;
        }

        if (motor.JumpStarted)
        {
            ForceAnimatorStateIfNeeded(
                jumpTakeoffStateName,
                jumpCrossFadeDuration,
                IsAnimatorResolvedToAny(jumpTakeoffStateName));
            return true;
        }

        if (airborne)
        {
            string airborneStateName = ResolveAirborneStateName(freeFall);
            bool airborneResolved = IsAnimatorResolvedToAny(airborneStateName);
            if (!airborneResolved && jumpSequenceActive)
            {
                airborneResolved = MatchesCurrentOrNextState(jumpTakeoffStateName, oneShotCompatibleNormalizedTime);
            }

            ForceAnimatorStateIfNeeded(airborneStateName, forcedStateCrossFadeDuration, airborneResolved);
            return true;
        }

        ResetTrigger(moveStartTriggerHash);
        ResetTrigger(moveStopTriggerHash);
        ForceAnimatorStateIfNeeded(
            locomotionStateName,
            forcedStateCrossFadeDuration,
            IsAnimatorResolvedToAny(locomotionStateName));
        return true;
    }

    private void ForceAnimatorStateIfNeeded(string stateName, float transitionDuration, bool alreadyResolved)
    {
        if (alreadyResolved || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        ResetStateReconciliation();
        CrossFadeState(stateName, transitionDuration, true);
    }

    private void ReconcileAnimatorState(
        bool moving,
        bool airborne,
        bool freeFall,
        bool landingActive,
        bool wallSliding,
        int wallSlideSide,
        float deltaTime)
    {
        if (!reconcileAnimatorState || animator == null)
        {
            ResetStateReconciliation();
            return;
        }

        if (landingActive)
        {
            ReconcileLandingState(deltaTime);
            return;
        }

        if (wallSliding)
        {
            ReconcileWallSlideState(wallSlideSide, deltaTime);
            return;
        }

        if (airborne)
        {
            ReconcileAirborneState(freeFall, deltaTime);
            return;
        }

        if (moving)
        {
            ReconcileGroundedMovingState(deltaTime);
            return;
        }

        ReconcileGroundedIdleState(deltaTime);
    }

    private void ReconcileAirborneState(bool freeFall, float deltaTime)
    {
        if (!jumpSequenceActive && !freeFall)
        {
            ResetStateReconciliation();
            return;
        }

        string targetStateName = ResolveAirborneStateName(freeFall);
        if (string.IsNullOrWhiteSpace(targetStateName) || IsInAirborneCompatibleState(freeFall))
        {
            ResetStateReconciliation();
            return;
        }

        TryResyncAnimatorState(
            ReconciliationFamily.Airborne,
            targetStateName,
            airborneResyncDelay,
            resyncCrossFadeDuration,
            deltaTime);
    }

    private void ReconcileLandingState(float deltaTime)
    {
        string targetStateName = ResolveLandingStateName();
        if (string.IsNullOrWhiteSpace(targetStateName) || IsInLandingCompatibleState())
        {
            ResetStateReconciliation();
            return;
        }

        TryResyncAnimatorState(
            ReconciliationFamily.Landing,
            targetStateName,
            landingResyncDelay,
            landingCrossFadeDuration,
            deltaTime);
    }

    private void ReconcileWallSlideState(int wallSlideSide, float deltaTime)
    {
        string targetStateName = ResolveWallSlideStateName(wallSlideSide);
        if (string.IsNullOrWhiteSpace(targetStateName) || IsInWallSlideCompatibleState(wallSlideSide))
        {
            ResetStateReconciliation();
            return;
        }

        TryResyncAnimatorState(
            ReconciliationFamily.WallSlide,
            targetStateName,
            airborneResyncDelay,
            resyncCrossFadeDuration,
            deltaTime);
    }

    private void ReconcileGroundedMovingState(float deltaTime)
    {
        if (IsInGroundedMovingCompatibleState())
        {
            ResetStateReconciliation();
            return;
        }

        TryResyncAnimatorState(
            ReconciliationFamily.GroundedMoving,
            locomotionStateName,
            locomotionResyncDelay,
            resyncCrossFadeDuration,
            deltaTime);
    }

    private void ReconcileGroundedIdleState(float deltaTime)
    {
        if (IsInGroundedIdleCompatibleState())
        {
            ResetStateReconciliation();
            return;
        }

        TryResyncAnimatorState(
            ReconciliationFamily.GroundedIdle,
            locomotionStateName,
            idleResyncDelay,
            resyncCrossFadeDuration,
            deltaTime);
    }

    private void TryResyncAnimatorState(
        ReconciliationFamily family,
        string targetStateName,
        float delay,
        float transitionDuration,
        float deltaTime)
    {
        if (!HasAnimatorState(targetStateName))
        {
            ResetStateReconciliation();
            return;
        }

        if (activeReconciliationFamily != family)
        {
            activeReconciliationFamily = family;
            stateMismatchTimer = 0f;
        }

        stateMismatchTimer += Mathf.Max(0f, deltaTime);
        if (stateMismatchTimer < delay)
        {
            return;
        }

        ApplyResyncTriggers(family);
        CrossFadeState(targetStateName, transitionDuration, true);
        ResetStateReconciliation();
    }

    private void ApplyResyncTriggers(ReconciliationFamily family)
    {
        switch (family)
        {
            case ReconciliationFamily.Landing:
                SetTrigger(landingTriggerHash);
                SetTrigger(landingTriggerFallbackHash);
                break;
            case ReconciliationFamily.GroundedMoving:
                SetTrigger(moveStartTriggerHash);
                break;
            case ReconciliationFamily.GroundedIdle:
                SetTrigger(moveStopTriggerHash);
                break;
        }
    }

    private void ResetStateReconciliation()
    {
        activeReconciliationFamily = ReconciliationFamily.None;
        stateMismatchTimer = 0f;
    }

    private string ResolveAirborneStateName(bool freeFall)
    {
        if (jumpSequenceActive)
        {
            return jumpAirborneStateName;
        }

        return freeFall ? freeFallStateName : jumpAirborneStateName;
    }

    private string ResolveWallSlideStateName(int wallSlideSide)
    {
        string preferredStateName = wallSlideSide < 0 ? wallSlideLeftStateName : wallSlideRightStateName;
        if (HasAnimatorState(preferredStateName))
        {
            return preferredStateName;
        }

        string fallbackStateName = wallSlideSide < 0 ? wallSlideRightStateName : wallSlideLeftStateName;
        return HasAnimatorState(fallbackStateName) ? fallbackStateName : preferredStateName;
    }

    private bool IsInAirborneCompatibleState(bool freeFall)
    {
        if (jumpSequenceActive)
        {
            return MatchesCurrentOrNextState(jumpAirborneStateName) ||
                   MatchesCurrentOrNextState(jumpTakeoffStateName, oneShotCompatibleNormalizedTime);
        }

        return freeFall && MatchesCurrentOrNextState(freeFallStateName);
    }

    private bool IsInLandingCompatibleState()
    {
        return MatchesCurrentOrNextState(landingStateName) ||
               MatchesCurrentOrNextState(jumpLandingStateName) ||
               MatchesCurrentOrNextState(heavyLandingStateName);
    }

    private bool IsInWallSlideCompatibleState(int wallSlideSide)
    {
        return MatchesCurrentOrNextState(ResolveWallSlideStateName(wallSlideSide));
    }

    private bool IsInGroundedStopState()
    {
        return MatchesCurrentOrNextState(walkStopStateName) ||
               MatchesCurrentOrNextState(jogtrotStopStateName) ||
               MatchesCurrentOrNextState(runStopStateName);
    }

    private bool IsInGroundedStartState()
    {
        return MatchesCurrentOrNextState(walkStartStateName) ||
               MatchesCurrentOrNextState(jogtrotStartStateName) ||
               MatchesCurrentOrNextState(runStartStateName);
    }

    private bool IsInGroundedMovingCompatibleState()
    {
        return IsAnimatorResolvedToAny(locomotionStateName);
    }

    private bool IsInGroundedIdleCompatibleState()
    {
        return IsAnimatorResolvedToAny(locomotionStateName);
    }

    private JumpPhase ResolveJumpPhase(bool airborne, bool landingActive)
    {
        if (landingActive)
        {
            return JumpPhase.LandingRecovery;
        }

        if (motor.JumpStarted)
        {
            return JumpPhase.Takeoff;
        }

        if (airborne)
        {
            return JumpPhase.Airborne;
        }

        return JumpPhase.Grounded;
    }

    private string ResolveLandingStateName()
    {
        if (motor.LastLandingSeverity == StarterInspiredThirdPersonMotor.LandingSeverity.Heavy &&
            HasAnimatorState(heavyLandingStateName))
        {
            return heavyLandingStateName;
        }

        if (jumpSequenceActive && HasAnimatorState(jumpLandingStateName))
        {
            return jumpLandingStateName;
        }

        return landingStateName;
    }

    private float ResolveLocomotionTier(float normalizedSpeed)
    {
        if (normalizedSpeed >= jogTierThreshold)
        {
            return 3f;
        }

        if (normalizedSpeed >= walkTierThreshold)
        {
            return 2f;
        }

        return 1f;
    }

    private string ResolveStopStateName(float locomotionTier)
    {
        if (locomotionTier >= 2.5f)
        {
            return runStopStateName;
        }

        if (locomotionTier >= 1.5f)
        {
            return jogtrotStopStateName;
        }

        return walkStopStateName;
    }

    private static int NormalizeWallSlideSide(int wallSlideSide)
    {
        return wallSlideSide < 0 ? -1 : 1;
    }

    private void CrossFadeState(string stateName, float duration)
    {
        CrossFadeState(stateName, duration, crossFadeJumpStates);
    }

    private void CrossFadeState(string stateName, float duration, bool enabled)
    {
        if (!enabled || string.IsNullOrWhiteSpace(stateName) || animator == null)
        {
            return;
        }

        if (!TryResolveStateHash(stateName, out int stateHash))
        {
            return;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        if (MatchesState(current, stateHash) &&
            (!animator.IsInTransition(animatorLayer) ||
             MatchesState(animator.GetNextAnimatorStateInfo(animatorLayer), stateHash)))
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, duration, animatorLayer);
    }

    private bool MatchesCurrentOrNextState(
        string stateName,
        float maxCurrentNormalizedTime = float.PositiveInfinity)
    {
        if (!TryResolveStateHash(stateName, out int stateHash))
        {
            return false;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        if (MatchesState(current, stateHash))
        {
            return float.IsPositiveInfinity(maxCurrentNormalizedTime) ||
                   current.normalizedTime <= maxCurrentNormalizedTime;
        }

        if (!animator.IsInTransition(animatorLayer))
        {
            return false;
        }

        return MatchesState(animator.GetNextAnimatorStateInfo(animatorLayer), stateHash);
    }

    private bool IsAnimatorResolvedToAny(params string[] stateNames)
    {
        if (animator == null || animator.layerCount <= animatorLayer)
        {
            return false;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        if (!animator.IsInTransition(animatorLayer))
        {
            return MatchesAnyState(current, stateNames);
        }

        return MatchesAnyState(animator.GetNextAnimatorStateInfo(animatorLayer), stateNames);
    }

    private bool MatchesAnyState(AnimatorStateInfo stateInfo, params string[] stateNames)
    {
        for (int i = 0; i < stateNames.Length; i++)
        {
            if (TryResolveStateHash(stateNames[i], out int stateHash) &&
                MatchesState(stateInfo, stateHash))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesState(AnimatorStateInfo stateInfo, int stateHash)
    {
        return stateInfo.shortNameHash == stateHash || stateInfo.fullPathHash == stateHash;
    }

    private bool HasAnimatorState(string stateName)
    {
        return TryResolveStateHash(stateName, out _);
    }

    private bool TryResolveStateHash(string stateName, out int stateHash)
    {
        stateHash = 0;
        if (animator == null || string.IsNullOrWhiteSpace(stateName) || animator.layerCount <= animatorLayer)
        {
            return false;
        }

        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(animatorLayer, shortHash))
        {
            stateHash = shortHash;
            return true;
        }

        string layerName = animator.GetLayerName(animatorLayer);
        int fullHash = Animator.StringToHash($"{layerName}.{stateName}");
        if (animator.HasState(animatorLayer, fullHash))
        {
            stateHash = fullHash;
            return true;
        }

        return false;
    }

    private void RefreshHashes()
    {
        speedHash = Animator.StringToHash(speedParam);
        motionSpeedHash = Animator.StringToHash(motionSpeedParam);
        groundedHash = Animator.StringToHash(groundedParam);
        jumpBoolHash = Animator.StringToHash(jumpBoolParam);
        freeFallHash = Animator.StringToHash(freeFallParam);
        verticalSpeedHash = Animator.StringToHash(verticalSpeedParam);
        isMovingHash = Animator.StringToHash(isMovingParam);
        moveStartTriggerHash = Animator.StringToHash(moveStartTriggerParam);
        moveStopTriggerHash = Animator.StringToHash(moveStopTriggerParam);
        locomotionTierHash = Animator.StringToHash(locomotionTierParam);
        jumpTriggerHash = Animator.StringToHash(jumpTriggerParam);
        isAirborneHash = Animator.StringToHash(isAirborneParam);
        landingTriggerHash = Animator.StringToHash(landingTriggerParam);
        landingTriggerFallbackHash = Animator.StringToHash(landingTriggerFallbackParam);
        landingBoolHash = Animator.StringToHash(landingBoolParam);
        landingTypeHash = Animator.StringToHash(landingTypeParam);
        jumpFromMovementHash = Animator.StringToHash(jumpFromMovementParam);
        jumpPhaseHash = Animator.StringToHash(jumpPhaseParam);
    }

    private void CacheAnimatorParameters()
    {
        parameterTypes.Clear();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (!parameterTypes.ContainsKey(parameter.nameHash))
            {
                parameterTypes.Add(parameter.nameHash, parameter.type);
            }
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        return parameterTypes.TryGetValue(hash, out AnimatorControllerParameterType registeredType) &&
               registeredType == type;
    }

    private void SetFloat(int hash, float value, float dampTime, float deltaTime)
    {
        if (!HasParameter(hash, AnimatorControllerParameterType.Float))
        {
            return;
        }

        if (dampTime > 0f && deltaTime > 0f)
        {
            animator.SetFloat(hash, value, dampTime, deltaTime);
            return;
        }

        animator.SetFloat(hash, value);
    }

    private void SetInt(int hash, int value)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(hash, value);
        }
    }

    private void SetBool(int hash, bool value)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(hash, value);
        }
    }

    private void SetTrigger(int hash)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(hash);
            animator.SetTrigger(hash);
        }
    }

    private void ResetTrigger(int hash)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(hash);
        }
    }

    private void UpdateDebugValues(
        float animatorSpeed,
        float motionSpeed,
        float locomotionTier,
        bool moving,
        bool airborne,
        bool freeFall,
        bool landingActive,
        bool wallSliding,
        int wallSlideSide)
    {
        if (!showDebugValues)
        {
            return;
        }

        debugAnimatorSpeed = animatorSpeed;
        debugMotionSpeed = motionSpeed;
        debugLocomotionTier = locomotionTier;
        debugGrounded = motor.StableGrounded;
        debugIsMoving = moving;
        debugAirborne = airborne;
        debugFreeFall = freeFall;
        debugWallSliding = wallSliding;
        debugWallSlideSide = wallSlideSide;
        debugLandingTriggered |= motor.LandingTriggered;
        debugLandingSeverity = motor.LastLandingSeverity;
        debugJumpPhase = (int)ResolveJumpPhase(airborne, landingActive);
        debugLandingType = landingActive ? (int)LandingType.Recovery : (int)LandingType.None;
        debugRootMotionDisabled = animator != null && !animator.applyRootMotion;
        debugCurrentStateShortHash = animator != null && animator.layerCount > animatorLayer
            ? animator.GetCurrentAnimatorStateInfo(animatorLayer).shortNameHash
            : 0;
    }
}
