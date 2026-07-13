using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    private const int JumpPhaseGrounded = 0;
    private const int JumpPhaseTakeoff = 1;
    private const int JumpPhaseAirborne = 2;
    private const int JumpPhaseLanding = 3;
    private const int JumpPhaseRoll = 4;

    [Header("Jump & Landing Feel")]
    [SerializeField, Tooltip("Drives the custom Player_Model jump and landing parameters from UCC grounded/velocity state.")]
    private bool driveJumpLandingAnimatorParameters = true;
    [SerializeField] private string jumpTriggerParam = "JumpTrigger";
    [SerializeField] private string isAirborneParam = "IsAirborne";
    [SerializeField] private string landingTypeParam = "LandingType";
    [SerializeField] private string jumpFromMovementParam = "JumpFromMovement";
    [SerializeField] private string rollTriggerParam = "RollTrigger";
    [SerializeField] private string jumpPhaseParam = "JumpPhase";
    [SerializeField] private string normalLandingStateName = "Jump_Land";
    [SerializeField] private string hardLandingStateName = "Landing_Hard";
    [SerializeField] private string rollLandingStateName = "Jump_Roll";
    [SerializeField, Min(0f)] private float takeoffPhaseMaxGroundedTime = 0.24f;
    [SerializeField, Min(0f)] private float airbornePhaseMinTime = 0.08f;
    [SerializeField, Min(0f)] private float normalLandingMinFallSpeed = 1.35f;
    [SerializeField, Min(0f)] private float hardLandingMinFallSpeed = 5.75f;
    [SerializeField, Min(0f)] private float rollLandingMinFallSpeed = 8.25f;
    [SerializeField, Min(0f)] private float rollLandingMinPlanarSpeed = 2.2f;
    [SerializeField, Min(0f)] private float normalLandingRecoveryTime = 0.18f;
    [SerializeField, Min(0f)] private float hardLandingRecoveryTime = 0.34f;
    [SerializeField, Min(0f)] private float rollLandingRecoveryTime = 0.28f;
    [SerializeField, Range(0f, 1f)] private float normalLandingInputScale = 0.42f;
    [SerializeField, Range(0f, 1f)] private float hardLandingInputScale = 0.1f;
    [SerializeField, Range(0f, 1f)] private float rollLandingInputScale = 0.32f;
    [SerializeField, Tooltip("Directly cross-fades into the landing state when the controller contains the target state.")]
    private bool crossFadeLandingStates = true;
    [SerializeField, Min(0f)] private float landingCrossFadeDuration = 0.055f;

    private bool jumpLandingWasGrounded;
    private bool jumpLandingWasAirborne;
    private bool jumpLandingPresentationActive;
    private float jumpLandingTakeoffGroundedTimer;
    private float jumpLandingAirborneTimer;
    private float jumpLandingMaxFallSpeed;
    private float landingRecoveryTimer;
    private float landingRecoveryDuration;
    private float landingRecoveryInputStartScale = 1f;

    private void ValidateJumpLandingSettings()
    {
        airbornePhaseMinTime = Mathf.Max(0f, airbornePhaseMinTime);
        takeoffPhaseMaxGroundedTime = Mathf.Max(0f, takeoffPhaseMaxGroundedTime);
        normalLandingMinFallSpeed = Mathf.Max(0f, normalLandingMinFallSpeed);
        hardLandingMinFallSpeed = Mathf.Max(normalLandingMinFallSpeed, hardLandingMinFallSpeed);
        rollLandingMinFallSpeed = Mathf.Max(hardLandingMinFallSpeed, rollLandingMinFallSpeed);
        rollLandingMinPlanarSpeed = Mathf.Max(0f, rollLandingMinPlanarSpeed);
        normalLandingRecoveryTime = Mathf.Max(0f, normalLandingRecoveryTime);
        hardLandingRecoveryTime = Mathf.Max(normalLandingRecoveryTime, hardLandingRecoveryTime);
        rollLandingRecoveryTime = Mathf.Max(0f, rollLandingRecoveryTime);
        normalLandingInputScale = Mathf.Clamp01(normalLandingInputScale);
        hardLandingInputScale = Mathf.Clamp01(hardLandingInputScale);
        rollLandingInputScale = Mathf.Clamp01(rollLandingInputScale);
        landingCrossFadeDuration = Mathf.Max(0f, landingCrossFadeDuration);
    }

    private void ResetJumpLandingState()
    {
        bool grounded = locomotion == null || locomotion.Grounded;
        jumpLandingWasGrounded = grounded;
        jumpLandingWasAirborne = false;
        jumpLandingPresentationActive = false;
        jumpLandingTakeoffGroundedTimer = 0f;
        jumpLandingAirborneTimer = 0f;
        jumpLandingMaxFallSpeed = 0f;
        landingRecoveryTimer = 0f;
        landingRecoveryDuration = 0f;
        landingRecoveryInputStartScale = 1f;

        if (animator != null && driveJumpLandingAnimatorParameters)
        {
            SetAnimatorBool(isAirborneParam, false);
            SetAnimatorInteger(jumpPhaseParam, JumpPhaseGrounded);
            SetAnimatorInteger(landingTypeParam, 0);
            SetAnimatorBool(jumpFromMovementParam, false);
        }
    }

    private void NotifyJumpStarted()
    {
        if (!driveJumpLandingAnimatorParameters || animator == null)
        {
            return;
        }

        jumpLandingPresentationActive = true;
        jumpLandingWasAirborne = false;
        jumpLandingTakeoffGroundedTimer = 0f;
        jumpLandingAirborneTimer = 0f;
        jumpLandingMaxFallSpeed = 0f;
        landingRecoveryTimer = 0f;
        landingRecoveryDuration = 0f;
        landingRecoveryInputStartScale = 1f;

        bool fromMovement = currentWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone;
        SetAnimatorBool(jumpFromMovementParam, fromMovement);
        SetAnimatorBool(isAirborneParam, false);
        SetAnimatorInteger(landingTypeParam, 0);
        SetAnimatorInteger(jumpPhaseParam, JumpPhaseTakeoff);
        ResetAnimatorTrigger(rollTriggerParam);
        ResetAnimatorTrigger(jumpTriggerParam);
        SetAnimatorTrigger(jumpTriggerParam);
    }

    private void UpdateJumpLandingAnimatorParameters()
    {
        if (!driveJumpLandingAnimatorParameters || animator == null || locomotion == null || IsFlightModeActive)
        {
            ResetJumpLandingState();
            return;
        }

        float deltaTime = ResolveJumpLandingDeltaTime();
        bool grounded = locomotion.Grounded;
        float verticalSpeed = Vector3.Dot(locomotion.Velocity, transform.up);
        float fallSpeed = Mathf.Max(0f, -verticalSpeed);
        float planarSpeed = Vector3.ProjectOnPlane(locomotion.Velocity, transform.up).magnitude;

        if (!grounded)
        {
            jumpLandingTakeoffGroundedTimer = 0f;
            jumpLandingWasAirborne = true;
            jumpLandingAirborneTimer += deltaTime;
            jumpLandingMaxFallSpeed = Mathf.Max(jumpLandingMaxFallSpeed, fallSpeed);
            landingRecoveryTimer = 0f;

            if (jumpLandingPresentationActive || jumpLandingAirborneTimer >= airbornePhaseMinTime)
            {
                SetAnimatorBool(isAirborneParam, true);
                SetAnimatorInteger(jumpPhaseParam, JumpPhaseAirborne);
            }

            jumpLandingWasGrounded = false;
            return;
        }

        if (jumpLandingPresentationActive && !jumpLandingWasAirborne)
        {
            jumpLandingTakeoffGroundedTimer += deltaTime;
            if (jumpLandingTakeoffGroundedTimer <= takeoffPhaseMaxGroundedTime)
            {
                SetAnimatorBool(isAirborneParam, false);
                SetAnimatorInteger(jumpPhaseParam, JumpPhaseTakeoff);
                jumpLandingWasGrounded = true;
                return;
            }

            jumpLandingPresentationActive = false;
        }

        if (!jumpLandingWasGrounded && jumpLandingWasAirborne)
        {
            BeginJumpLandingPresentation(jumpLandingMaxFallSpeed, planarSpeed);
        }
        else
        {
            TickJumpLandingRecovery(deltaTime);
        }

        jumpLandingWasGrounded = true;
    }

    private void BeginJumpLandingPresentation(float fallSpeed, float planarSpeed)
    {
        jumpLandingWasAirborne = false;
        jumpLandingPresentationActive = false;
        jumpLandingTakeoffGroundedTimer = 0f;
        jumpLandingAirborneTimer = 0f;
        jumpLandingMaxFallSpeed = 0f;
        SetAnimatorBool(isAirborneParam, false);

        if (fallSpeed < normalLandingMinFallSpeed)
        {
            SetAnimatorInteger(jumpPhaseParam, JumpPhaseGrounded);
            SetAnimatorInteger(landingTypeParam, 0);
            return;
        }

        bool wantsRoll = fallSpeed >= rollLandingMinFallSpeed &&
                         planarSpeed >= rollLandingMinPlanarSpeed &&
                         currentWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone;
        bool hardLanding = !wantsRoll && fallSpeed >= hardLandingMinFallSpeed;

        int landingPhase = wantsRoll ? JumpPhaseRoll : JumpPhaseLanding;
        int landingType = wantsRoll ? 2 : hardLanding ? 1 : 0;
        string stateName = wantsRoll
            ? rollLandingStateName
            : hardLanding ? hardLandingStateName : normalLandingStateName;

        SetAnimatorInteger(landingTypeParam, landingType);
        SetAnimatorInteger(jumpPhaseParam, landingPhase);
        if (wantsRoll)
        {
            ResetAnimatorTrigger(rollTriggerParam);
            SetAnimatorTrigger(rollTriggerParam);
        }

        CrossFadeLandingState(stateName);
        StartJumpLandingRecovery(wantsRoll, hardLanding);
    }

    private void StartJumpLandingRecovery(bool wantsRoll, bool hardLanding)
    {
        if (wantsRoll)
        {
            landingRecoveryDuration = rollLandingRecoveryTime;
            landingRecoveryInputStartScale = rollLandingInputScale;
        }
        else if (hardLanding)
        {
            landingRecoveryDuration = hardLandingRecoveryTime;
            landingRecoveryInputStartScale = hardLandingInputScale;
        }
        else
        {
            landingRecoveryDuration = normalLandingRecoveryTime;
            landingRecoveryInputStartScale = normalLandingInputScale;
        }

        landingRecoveryTimer = landingRecoveryDuration;
    }

    private void TickJumpLandingRecovery(float deltaTime)
    {
        if (landingRecoveryTimer <= 0f)
        {
            SetAnimatorInteger(jumpPhaseParam, JumpPhaseGrounded);
            SetAnimatorInteger(landingTypeParam, 0);
            return;
        }

        landingRecoveryTimer = Mathf.Max(0f, landingRecoveryTimer - deltaTime);
        if (landingRecoveryTimer <= 0f)
        {
            SetAnimatorInteger(jumpPhaseParam, JumpPhaseGrounded);
            SetAnimatorInteger(landingTypeParam, 0);
        }
    }

    private Vector2 ResolveJumpLandingWorldMoveInput(Vector2 targetWorldMoveInput)
    {
        if (!driveJumpLandingAnimatorParameters || landingRecoveryTimer <= 0f || landingRecoveryDuration <= 0f)
        {
            return targetWorldMoveInput;
        }

        float recoveryProgress = 1f - Mathf.Clamp01(landingRecoveryTimer / landingRecoveryDuration);
        float inputScale = Mathf.Lerp(landingRecoveryInputStartScale, 1f, recoveryProgress);
        return targetWorldMoveInput * inputScale;
    }

    private void CrossFadeLandingState(string stateName)
    {
        if (!crossFadeLandingStates || animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        const int baseLayer = 0;
        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(baseLayer, stateHash))
        {
            stateHash = Animator.StringToHash($"Base Layer.{stateName}");
            if (!animator.HasState(baseLayer, stateHash))
            {
                return;
            }
        }

        animator.CrossFadeInFixedTime(stateHash, landingCrossFadeDuration, baseLayer);
    }

    private float ResolveJumpLandingDeltaTime()
    {
        float deltaTime = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;
        return Mathf.Max(deltaTime, 0.0001f);
    }
}
