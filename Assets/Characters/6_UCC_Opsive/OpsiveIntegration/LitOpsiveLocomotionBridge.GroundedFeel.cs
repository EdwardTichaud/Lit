using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    [Header("Grounded Feel")]
    [SerializeField, Tooltip("Adds a heavier, cinematic movement filter for grounded third-person locomotion.")]
    private bool enableCinematicGroundedFeel = true;
    [SerializeField, Tooltip("Temporarily tunes UCC ground physics while this bridge is active.")]
    private bool tuneGroundedUccPhysics = true;
    [SerializeField] private Vector3 groundedMotorAcceleration = new Vector3(4.6f, 0f, 4.6f);
    [SerializeField, Min(0f)] private float groundedMotorDamping = 5.7f;
    [SerializeField, Range(0f, 1f)] private float groundedPreviousAccelerationInfluence = 0.88f;
    [SerializeField, Range(0f, 1f)] private float groundedBackwardsMultiplier = 0.6f;
    [SerializeField, Min(0f)] private float groundedGravityAmount = 0.65f;
    [SerializeField, Min(0f)] private float groundedStickToGroundDistance = 0.72f;
    [SerializeField, Range(0f, 89f)] private float groundedSlopeLimit = 60f;
    [SerializeField, Min(0f)] private float groundedMaxStepHeight = 0.5f;
    [SerializeField, Min(0f)] private float groundedMovingPlatformSeparationVelocity = 7f;
    [SerializeField, Range(0f, 1f)] private float groundedMovingPlatformDisconnectMultiplier = 0.65f;
    [SerializeField, Min(0f)] private float groundedMovingPlatformForceDamping = 0.18f;

    [Header("Grounded Input Feel")]
    [SerializeField, Min(0f)] private float groundedInputAcceleration = 6.5f;
    [SerializeField, Min(0f)] private float groundedSprintInputAcceleration = 4.8f;
    [SerializeField, Min(0f)] private float groundedInputDeceleration = 9.5f;
    [SerializeField, Min(0f)] private float groundedDirectionChangeAcceleration = 13f;
    [SerializeField, Range(-1f, 1f)] private float groundedDirectionChangeDot = 0.2f;

    [Header("Grounded Sprint Feel")]
    [SerializeField, Tooltip("Reduces the binary UCC SpeedChange kick into a more measured exploration sprint.")]
    private bool tuneGroundedSprintSpeedChange = true;
    [SerializeField, Range(1f, 2.4f)] private float groundedSprintSpeedMultiplier = 1.65f;
    [SerializeField, Min(0f)] private float groundedSprintSpeedParameterValue = 2.6f;

    [Header("Grounded Animation Feel")]
    [SerializeField] private string moveStartTriggerParam = "MoveStartTrigger";
    [SerializeField] private string moveStopTriggerParam = "MoveStopTrigger";
    [SerializeField] private string turnInPlaceParam = "TurnInPlace";
    [SerializeField, Min(0.01f)] private float groundedAnimationSpeedToBlend = 0.48f;
    [SerializeField, Min(0.01f), Tooltip("Lower physical-speed feedback while root motion drives displacement, keeping animation selection input-led.")]
    private float groundedRootMotionSpeedToBlend = 0.22f;
    [SerializeField, Min(0f)] private float groundedAnimatorSpeedRiseRate = 18f;
    [SerializeField, Min(0f)] private float groundedAnimatorSpeedFallRate = 11f;
    [SerializeField, Min(0f)] private float groundedAnimatorTurnRate = 9f;
    [SerializeField, Range(0f, 1f)] private float groundedTurnInPlaceThreshold = 0.55f;
    [SerializeField, Min(0f)] private float groundedTurnInPlaceMaxSpeed = 0.35f;
    [SerializeField, Min(0f)] private float groundedStopTriggerMinSpeed = 0.35f;

    private Vector2 desiredGroundedWorldMoveInput;
    private Vector2 smoothedGroundedWorldMoveInput;
    private bool groundedMoveIntent;
    private bool previousGroundedMoveIntent;
    private float groundedPresentationSpeed;
    private float groundedPresentationTurn;

    private bool groundedFeelProfileApplied;
    private Vector3 previousGroundedMotorAcceleration;
    private float previousGroundedMotorDamping;
    private float previousGroundedAccelerationInfluence;
    private float previousGroundedBackwardsMultiplier;
    private float previousGroundedGravityAmount;
    private bool previousGroundedStickToGround;
    private float previousGroundedStickToGroundDistance;
    private float previousGroundedSlopeLimit;
    private float previousGroundedMaxStepHeight;
    private float previousGroundedMovingPlatformSeparationVelocity;
    private float previousGroundedMovingPlatformDisconnectMultiplier;
    private float previousGroundedMovingPlatformForceDamping;

    private SpeedChange groundedFeelSpeedChange;
    private bool groundedFeelSpeedChangeApplied;
    private float previousGroundedSpeedChangeMultiplier;
    private float previousGroundedSpeedChangeMinValue;
    private float previousGroundedSpeedChangeMaxValue;
    private float previousGroundedSpeedChangeParameter;

    private void ConfigureGroundedFeelProfile()
    {
        if (!enableCinematicGroundedFeel || locomotion == null)
        {
            ConfigureGroundedSprintSpeedChange();
            return;
        }

        if (IsRootMotionLocomotionEnabled() || !tuneGroundedUccPhysics || groundedFeelProfileApplied)
        {
            ConfigureGroundedSprintSpeedChange();
            return;
        }

        previousGroundedMotorAcceleration = locomotion.MotorAcceleration;
        previousGroundedMotorDamping = locomotion.MotorDamping;
        previousGroundedAccelerationInfluence = locomotion.PreviousAccelerationInfluence;
        previousGroundedBackwardsMultiplier = locomotion.MotorBackwardsMultiplier;
        previousGroundedGravityAmount = locomotion.GravityAmount;
        previousGroundedStickToGround = locomotion.StickToGround;
        previousGroundedStickToGroundDistance = locomotion.StickToGroundDistance;
        previousGroundedSlopeLimit = locomotion.SlopeLimit;
        previousGroundedMaxStepHeight = locomotion.MaxStepHeight;
        previousGroundedMovingPlatformSeparationVelocity = locomotion.MovingPlatformSeperationVelocity;
        previousGroundedMovingPlatformDisconnectMultiplier = locomotion.MovingPlatformDisconnectMovementMultiplier;
        previousGroundedMovingPlatformForceDamping = locomotion.MovingPlatformForceDamping;

        locomotion.MotorAcceleration = new Vector3(
            Mathf.Max(0f, groundedMotorAcceleration.x),
            groundedMotorAcceleration.y,
            Mathf.Max(0f, groundedMotorAcceleration.z));
        locomotion.MotorDamping = Mathf.Max(0f, groundedMotorDamping);
        locomotion.PreviousAccelerationInfluence = Mathf.Clamp01(groundedPreviousAccelerationInfluence);
        locomotion.MotorBackwardsMultiplier = Mathf.Clamp01(groundedBackwardsMultiplier);
        locomotion.GravityAmount = Mathf.Max(locomotion.GravityAmount, groundedGravityAmount);
        locomotion.StickToGround = true;
        locomotion.StickToGroundDistance = Mathf.Max(locomotion.StickToGroundDistance, groundedStickToGroundDistance);
        locomotion.SlopeLimit = Mathf.Max(locomotion.SlopeLimit, Mathf.Clamp(groundedSlopeLimit, 0f, 89f));
        locomotion.MaxStepHeight = Mathf.Max(locomotion.MaxStepHeight, groundedMaxStepHeight);
        locomotion.MovingPlatformSeperationVelocity = Mathf.Max(
            locomotion.MovingPlatformSeperationVelocity,
            groundedMovingPlatformSeparationVelocity);
        locomotion.MovingPlatformDisconnectMovementMultiplier = Mathf.Clamp01(groundedMovingPlatformDisconnectMultiplier);
        locomotion.MovingPlatformForceDamping = Mathf.Max(
            locomotion.MovingPlatformForceDamping,
            groundedMovingPlatformForceDamping);

        groundedFeelProfileApplied = true;
        ConfigureGroundedSprintSpeedChange();
    }

    private void RestoreGroundedFeelProfile()
    {
        RestoreGroundedSprintSpeedChange();

        if (!groundedFeelProfileApplied || locomotion == null)
        {
            groundedFeelProfileApplied = false;
            return;
        }

        locomotion.MotorAcceleration = previousGroundedMotorAcceleration;
        locomotion.MotorDamping = previousGroundedMotorDamping;
        locomotion.PreviousAccelerationInfluence = previousGroundedAccelerationInfluence;
        locomotion.MotorBackwardsMultiplier = previousGroundedBackwardsMultiplier;
        locomotion.GravityAmount = previousGroundedGravityAmount;
        locomotion.StickToGround = previousGroundedStickToGround;
        locomotion.StickToGroundDistance = previousGroundedStickToGroundDistance;
        locomotion.SlopeLimit = previousGroundedSlopeLimit;
        locomotion.MaxStepHeight = previousGroundedMaxStepHeight;
        locomotion.MovingPlatformSeperationVelocity = previousGroundedMovingPlatformSeparationVelocity;
        locomotion.MovingPlatformDisconnectMovementMultiplier = previousGroundedMovingPlatformDisconnectMultiplier;
        locomotion.MovingPlatformForceDamping = previousGroundedMovingPlatformForceDamping;
        groundedFeelProfileApplied = false;
    }

    private void ConfigureGroundedSprintSpeedChange()
    {
        if (!enableCinematicGroundedFeel ||
            !tuneGroundedSprintSpeedChange ||
            locomotion == null ||
            groundedFeelSpeedChangeApplied)
        {
            return;
        }

        groundedFeelSpeedChange = locomotion.GetAbility<SpeedChange>();
        if (groundedFeelSpeedChange == null)
        {
            return;
        }

        previousGroundedSpeedChangeMultiplier = groundedFeelSpeedChange.SpeedChangeMultiplier;
        previousGroundedSpeedChangeMinValue = groundedFeelSpeedChange.MinSpeedChangeValue;
        previousGroundedSpeedChangeMaxValue = groundedFeelSpeedChange.MaxSpeedChangeValue;
        previousGroundedSpeedChangeParameter = groundedFeelSpeedChange.SpeedParameter;

        float sprintMultiplier = Mathf.Max(1f, groundedSprintSpeedMultiplier);
        groundedFeelSpeedChange.SpeedChangeMultiplier = sprintMultiplier;
        groundedFeelSpeedChange.MinSpeedChangeValue = -sprintMultiplier;
        groundedFeelSpeedChange.MaxSpeedChangeValue = sprintMultiplier;
        groundedFeelSpeedChange.SpeedParameter = Mathf.Max(0f, groundedSprintSpeedParameterValue);
        groundedFeelSpeedChangeApplied = true;
    }

    private void RestoreGroundedSprintSpeedChange()
    {
        if (!groundedFeelSpeedChangeApplied || groundedFeelSpeedChange == null)
        {
            groundedFeelSpeedChangeApplied = false;
            groundedFeelSpeedChange = null;
            return;
        }

        groundedFeelSpeedChange.SpeedChangeMultiplier = previousGroundedSpeedChangeMultiplier;
        groundedFeelSpeedChange.MinSpeedChangeValue = previousGroundedSpeedChangeMinValue;
        groundedFeelSpeedChange.MaxSpeedChangeValue = previousGroundedSpeedChangeMaxValue;
        groundedFeelSpeedChange.SpeedParameter = previousGroundedSpeedChangeParameter;
        groundedFeelSpeedChangeApplied = false;
        groundedFeelSpeedChange = null;
    }

    private Vector2 ResolveGroundedFeelWorldMoveInput(Vector2 targetWorldMoveInput, float targetMagnitude)
    {
        desiredGroundedWorldMoveInput = targetWorldMoveInput;
        groundedMoveIntent = targetMagnitude > movementDeadZone;

        if (!enableCinematicGroundedFeel || IsFlightModeActive)
        {
            smoothedGroundedWorldMoveInput = targetWorldMoveInput;
            return targetWorldMoveInput;
        }

        float deltaTime = ResolveGroundedFeelDeltaTime();
        float rate = ResolveGroundedInputRate(targetWorldMoveInput, targetMagnitude);
        smoothedGroundedWorldMoveInput = Vector2.MoveTowards(
            smoothedGroundedWorldMoveInput,
            targetWorldMoveInput,
            rate * deltaTime);

        if (targetMagnitude <= 0f && smoothedGroundedWorldMoveInput.magnitude <= movementDeadZone * 0.35f)
        {
            smoothedGroundedWorldMoveInput = Vector2.zero;
        }

        return smoothedGroundedWorldMoveInput;
    }

    private float ResolveGroundedInputRate(Vector2 targetWorldMoveInput, float targetMagnitude)
    {
        if (targetMagnitude <= 0f)
        {
            return Mathf.Max(0f, groundedInputDeceleration);
        }

        if (smoothedGroundedWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            float directionDot = Vector2.Dot(smoothedGroundedWorldMoveInput.normalized, targetWorldMoveInput.normalized);
            if (directionDot <= groundedDirectionChangeDot)
            {
                return Mathf.Max(0f, groundedDirectionChangeAcceleration);
            }
        }

        return sprintPressed
            ? Mathf.Max(0f, groundedSprintInputAcceleration)
            : Mathf.Max(0f, groundedInputAcceleration);
    }

    private bool TryUpdateGroundedFeelAnimatorParameters(Vector3 velocity, float speed, bool moving)
    {
        if (!enableCinematicGroundedFeel)
        {
            return false;
        }

        float deltaTime = ResolveGroundedFeelDeltaTime();
        float targetSpeed = ResolveGroundedPresentationSpeed(speed);
        float speedRate = targetSpeed > groundedPresentationSpeed
            ? groundedAnimatorSpeedRiseRate
            : groundedAnimatorSpeedFallRate;
        groundedPresentationSpeed = Mathf.MoveTowards(
            groundedPresentationSpeed,
            targetSpeed,
            Mathf.Max(0f, speedRate) * deltaTime);

        float targetTurn = ResolveGroundedPresentationTurn(velocity);
        groundedPresentationTurn = Mathf.MoveTowards(
            groundedPresentationTurn,
            targetTurn,
            Mathf.Max(0f, groundedAnimatorTurnRate) * deltaTime);

        bool shouldAnimateMoving = moving || groundedMoveIntent || groundedPresentationSpeed > 0.05f;
        UpdateGroundedMoveTriggers(speed);

        SetAnimatorFloat(speedParam, groundedPresentationSpeed);
        SetGroundedDirectionalAnimatorParameters(groundedPresentationSpeed, velocity);
        SetAnimatorBool(isMovingParam, shouldAnimateMoving);
        SetAnimatorFloat(locomotionTierParam, ResolveLocomotionTier(groundedPresentationSpeed));
        SetAnimatorFloat(turnParam, groundedPresentationTurn);
        SetAnimatorBool(turnInPlaceParam, ShouldGroundedTurnInPlace(speed, targetTurn));
        return true;
    }

    private float ResolveGroundedPresentationSpeed(float physicalSpeed)
    {
        float physicalSpeedToBlend = IsRootMotionLocomotionEnabled()
            ? groundedRootMotionSpeedToBlend
            : groundedAnimationSpeedToBlend;
        float scaledPhysicalSpeed = physicalSpeed * Mathf.Max(0.01f, physicalSpeedToBlend);
        float inputBlendSpeed = 0f;
        float inputMagnitude = currentWorldMoveInput.magnitude;
        if (inputMagnitude > 0f)
        {
            float targetBlendTop = sprintPressed
                ? runPresentationSpeed
                : Mathf.Lerp(walkPresentationSpeed, runPresentationSpeed, 0.55f);
            inputBlendSpeed = inputMagnitude * targetBlendTop;
        }

        return Mathf.Clamp(Mathf.Max(scaledPhysicalSpeed, inputBlendSpeed), 0f, runPresentationSpeed);
    }

    private float ResolveGroundedPresentationTurn(Vector3 velocity)
    {
        Vector3 direction = Vector3.zero;
        if (desiredGroundedWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            direction = new Vector3(desiredGroundedWorldMoveInput.x, 0f, desiredGroundedWorldMoveInput.y);
        }
        else if (velocity.sqrMagnitude > 0.0001f)
        {
            direction = velocity;
        }
        else
        {
            direction = lastPlanarDirection;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Clamp(Vector3.SignedAngle(transform.forward, direction.normalized, Vector3.up) / 90f, -1f, 1f);
    }

    private bool ShouldGroundedTurnInPlace(float speed, float targetTurn)
    {
        return locomotion != null &&
               locomotion.Grounded &&
               groundedMoveIntent &&
               speed <= groundedTurnInPlaceMaxSpeed &&
               currentWorldMoveInput.magnitude <= movementDeadZone * 1.5f &&
               Mathf.Abs(targetTurn) >= groundedTurnInPlaceThreshold;
    }

    private void UpdateGroundedMoveTriggers(float speed)
    {
        if (animator == null || IsFlightModeActive)
        {
            previousGroundedMoveIntent = groundedMoveIntent;
            return;
        }

        if (groundedMoveIntent && !previousGroundedMoveIntent)
        {
            ResetAnimatorTrigger(moveStopTriggerParam);
            SetAnimatorTrigger(moveStartTriggerParam);
        }
        else if (!groundedMoveIntent && previousGroundedMoveIntent && speed >= groundedStopTriggerMinSpeed)
        {
            ResetAnimatorTrigger(moveStartTriggerParam);
            SetAnimatorTrigger(moveStopTriggerParam);
        }

        previousGroundedMoveIntent = groundedMoveIntent;
    }

    private void ResetGroundedFeelState()
    {
        desiredGroundedWorldMoveInput = Vector2.zero;
        smoothedGroundedWorldMoveInput = Vector2.zero;
        groundedMoveIntent = false;
        previousGroundedMoveIntent = false;
        groundedPresentationSpeed = 0f;
        groundedPresentationTurn = 0f;
    }

    private void ResetGroundedFeelInput()
    {
        desiredGroundedWorldMoveInput = Vector2.zero;
        smoothedGroundedWorldMoveInput = Vector2.zero;
        groundedMoveIntent = false;
        previousGroundedMoveIntent = false;
    }

    private float ResolveGroundedFeelDeltaTime()
    {
        float deltaTime = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;
        return Mathf.Max(deltaTime, 0.0001f);
    }

    private void ResetAnimatorTrigger(string parameter)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(parameter);
        }
    }

    private void SetLitAnimatorSpeedParameterOverride(bool active)
    {
        if (animatorMonitor != null)
        {
            animatorMonitor.SpeedParameterOverride = active;
        }
    }
}
