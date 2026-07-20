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
    [SerializeField, Min(0f)] private float groundedInputAcceleration = 6.2f;
    [SerializeField, Min(0f)] private float groundedSprintInputAcceleration = 5f;
    [SerializeField, Min(0f)] private float groundedInputDeceleration = 6.2f;
    [SerializeField, Min(0f)] private float groundedDirectionChangeAcceleration = 11.2f;
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
    [SerializeField, Min(0f)] private float groundedAnimatorSpeedRiseRate = 13.5f;
    [SerializeField, Min(0f)] private float groundedAnimatorSpeedFallRate = 5.4f;
    [SerializeField, Min(0f)] private float groundedAnimatorTurnRate = 5.4f;
    [SerializeField, Range(0f, 1f)] private float groundedTurnInPlaceThreshold = 0.55f;
    [SerializeField, Min(0f)] private float groundedTurnInPlaceMaxSpeed = 0.35f;
    [SerializeField, Min(0f)] private float groundedStopTriggerMinSpeed = 0.48f;
    [SerializeField, Min(0f), Tooltip("Keeps start/stop blend trees aimed after input or physical velocity drops to zero.")]
    private float groundedMoveTransitionDirectionHoldTime = 0.18f;
    [SerializeField, Min(0f), Tooltip("Minimum parameter radius used by directional start/stop blend trees while their direction is latched.")]
    private float groundedMoveTransitionParameterSpeed = 1.22f;
    [SerializeField, Tooltip("Keeps grounded locomotion on forward clips only; rotation turns the character instead of blending strafe/backward clips.")]
    private bool useForwardOnlyGroundedLocomotion = true;
    [SerializeField, Tooltip("Uses root-motion turn clips when movement starts from a sharp angle change.")]
    private bool enableRootMotionPivotTurns = true;
    [SerializeField, Range(45f, 180f)] private float groundedPivotMinAngle = 85f;
    [SerializeField, Range(90f, 180f)] private float groundedPivot180Angle = 135f;
    [SerializeField, Tooltip("Snaps starts from rest toward the requested direction instead of playing turn-in-place clips.")]
    private bool groundedSnapStationaryTurn = true;
    [SerializeField, Range(0f, 180f)] private float groundedSnapStationaryTurnMinAngle = 25f;
    [SerializeField, Min(0f)] private float groundedSnapStationaryTurnMaxSpeed = 0.22f;
    [SerializeField, Range(0f, 1f)] private float groundedSnapStationaryTurnMaxSmoothedInput = 0.08f;
    [SerializeField, Min(0f)] private float groundedPivotMaxSpeed = 0.45f;
    [SerializeField, Range(0f, 1f)] private float groundedPivotMaxSmoothedInput = 0.14f;
    [SerializeField, Min(0.05f)] private float groundedPivotHoldTime = 0.32f;
    [SerializeField, Min(0f)] private float groundedPivotCooldown = 0.34f;
    [SerializeField, Min(0f), Tooltip("Lets the first input frames settle before a moderate turn can trigger a root-motion pivot.")]
    private float groundedPivotStartGraceTime = 0.12f;
    [SerializeField, Range(45f, 180f), Tooltip("During the start grace window, only very decisive turns may enter a pivot clip.")]
    private float groundedPivotStartGraceMinAngle = 128f;
    [SerializeField, Range(0f, 1f), Tooltip("Normalized pivot progress before movement can blend back in.")]
    private float groundedPivotMovementReleaseStart = 0.38f;
    [SerializeField, Range(0f, 180f), Tooltip("Remaining angle under which movement can blend back in during a pivot.")]
    private float groundedPivotMovementReleaseMaxAngle = 72f;
    [SerializeField, Range(0f, 1f), Tooltip("Maximum movement input scale allowed while a pivot is finishing.")]
    private float groundedPivotMovementReleaseScale = 0.58f;
    [SerializeField, Tooltip("Commits the gameplay root rotation toward the authored turn target so turn clips cannot visually rotate and then snap back.")]
    private bool commitRootRotationDuringPivot = true;
    [SerializeField, Min(1f)] private float groundedPivotRotationCommitRate = 960f;

    private Vector2 desiredGroundedWorldMoveInput;
    private Vector2 smoothedGroundedWorldMoveInput;
    private bool groundedMoveIntent;
    private bool previousGroundedMoveIntent;
    private float groundedMoveIntentAge;
    private bool wasGroundedMoveIntentForAge;
    private float groundedPresentationSpeed;
    private float groundedPresentationTurn;
    private bool groundedPivotActive;
    private float groundedPivotHoldTimer;
    private float groundedPivotDuration;
    private float groundedPivotCooldownTimer;
    private float groundedPivotTurnValue;
    private Vector3 groundedPivotTargetDirection;
    private bool hasGroundedPivotTargetDirection;
    private Vector2 groundedMoveTransitionLocalDirection;
    private float groundedMoveTransitionDirectionTimer;

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
        float deltaTime = ResolveGroundedFeelDeltaTime();

        if (!enableCinematicGroundedFeel || IsFlightModeActive)
        {
            ResetGroundedPivotTurn();
            ResetGroundedMoveTransitionDirection();
            ResetGroundedMoveIntentAge();
            smoothedGroundedWorldMoveInput = targetWorldMoveInput;
            return targetWorldMoveInput;
        }

        TickGroundedMoveIntentAge(deltaTime);
        TickGroundedMoveTransitionDirection(deltaTime);
        if (locomotion == null || !locomotion.Grounded)
        {
            ResetGroundedPivotTurn();
        }
        else
        {
            TickGroundedPivotTurn(deltaTime);
        }

        if (TryStartGroundedPivotTurn(targetWorldMoveInput, targetMagnitude) || groundedPivotActive)
        {
            ResetGroundedMoveTransitionDirection();
            return ResolveGroundedPivotMoveInput(targetWorldMoveInput, targetMagnitude);
        }

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
        SetAnimatorFloat(speedParam, groundedPresentationSpeed);
        SetGroundedDirectionalAnimatorParameters(groundedPresentationSpeed, velocity);
        SetAnimatorBool(isMovingParam, shouldAnimateMoving);
        SetAnimatorFloat(locomotionTierParam, ResolveLocomotionTier(groundedPresentationSpeed));
        SetAnimatorFloat(turnParam, groundedPresentationTurn);
        SetAnimatorBool(turnInPlaceParam, ShouldGroundedTurnInPlace(speed, targetTurn));
        UpdateGroundedMoveTriggers(speed, velocity);
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
            float targetBlendTop = sprintPressed ? runPresentationSpeed : walkPresentationSpeed;
            inputBlendSpeed = inputMagnitude * targetBlendTop;
        }

        float maximumPresentationSpeed = sprintPressed ? runPresentationSpeed : walkPresentationSpeed;
        return Mathf.Clamp(Mathf.Max(scaledPhysicalSpeed, inputBlendSpeed), 0f, maximumPresentationSpeed);
    }

    private float ResolveGroundedPresentationTurn(Vector3 velocity)
    {
        if (groundedPivotActive)
        {
            return groundedPivotTurnValue;
        }

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
        if (groundedPivotActive)
        {
            return locomotion != null && locomotion.Grounded;
        }

        return locomotion != null &&
               locomotion.Grounded &&
               groundedMoveIntent &&
               speed <= groundedTurnInPlaceMaxSpeed &&
               currentWorldMoveInput.magnitude <= movementDeadZone * 1.5f &&
               Mathf.Abs(targetTurn) >= groundedTurnInPlaceThreshold;
    }

    private void TickGroundedPivotTurn(float deltaTime)
    {
        if (groundedPivotHoldTimer > 0f)
        {
            groundedPivotHoldTimer = Mathf.Max(0f, groundedPivotHoldTimer - deltaTime);
            groundedPivotActive = true;
            ApplyGroundedPivotRotationCommit(deltaTime, forceComplete: false);
            if (groundedPivotHoldTimer <= 0f)
            {
                ApplyGroundedPivotRotationCommit(deltaTime, forceComplete: true);
                CommitGroundedPivotTargetDirection();
                groundedPivotActive = false;
                groundedPivotDuration = 0f;
                groundedPivotCooldownTimer = Mathf.Max(groundedPivotCooldownTimer, groundedPivotCooldown);
            }
        }
        else
        {
            groundedPivotActive = false;
            groundedPivotDuration = 0f;
        }

        if (!groundedPivotActive && groundedPivotCooldownTimer > 0f)
        {
            groundedPivotCooldownTimer = Mathf.Max(0f, groundedPivotCooldownTimer - deltaTime);
        }
    }

    private bool TryStartGroundedPivotTurn(Vector2 targetWorldMoveInput, float targetMagnitude)
    {
        if (!enableRootMotionPivotTurns ||
            !IsRootMotionLocomotionEnabled() ||
            locomotion == null ||
            !locomotion.Grounded ||
            !groundedMoveIntent ||
            targetMagnitude <= movementDeadZone ||
            groundedPivotActive ||
            groundedPivotCooldownTimer > 0f)
        {
            return false;
        }

        Vector3 planarVelocity = locomotion.Velocity;
        planarVelocity.y = 0f;
        if (planarVelocity.magnitude > groundedPivotMaxSpeed ||
            smoothedGroundedWorldMoveInput.magnitude > groundedPivotMaxSmoothedInput)
        {
            return false;
        }

        Vector3 targetDirection = new Vector3(targetWorldMoveInput.x, 0f, targetWorldMoveInput.y);
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        targetDirection.Normalize();
        float signedAngle = Vector3.SignedAngle(transform.forward, targetDirection, Vector3.up);
        float absAngle = Mathf.Abs(signedAngle);
        if (ShouldSnapGroundedStationaryTurn(planarVelocity, absAngle))
        {
            SnapGroundedStationaryTurn(targetDirection);
            return false;
        }

        if (absAngle < groundedPivotMinAngle)
        {
            return false;
        }

        float startGraceTime = Mathf.Max(0f, groundedPivotStartGraceTime);
        float startGraceMinAngle = Mathf.Clamp(groundedPivotStartGraceMinAngle, groundedPivotMinAngle, 180f);
        if (startGraceTime > 0f &&
            groundedMoveIntentAge < startGraceTime &&
            absAngle < startGraceMinAngle)
        {
            return false;
        }

        float turnSign = Mathf.Sign(signedAngle);
        float signedTurn = turnSign * (absAngle >= groundedPivot180Angle ? 2f : 1f);

        groundedPivotTurnValue = signedTurn;
        groundedPresentationTurn = signedTurn;
        groundedPresentationSpeed = 0f;
        groundedPivotTargetDirection = targetDirection;
        hasGroundedPivotTargetDirection = true;
        CommitGroundedPivotTargetDirection();
        groundedPivotDuration = Mathf.Max(0.05f, groundedPivotHoldTime);
        groundedPivotHoldTimer = groundedPivotDuration;
        groundedPivotActive = true;
        return true;
    }

    private bool ShouldSnapGroundedStationaryTurn(Vector3 planarVelocity, float absAngle)
    {
        if (!groundedSnapStationaryTurn)
        {
            return false;
        }

        float minAngle = Mathf.Clamp(groundedSnapStationaryTurnMinAngle, 0f, 180f);
        if (absAngle < minAngle)
        {
            return false;
        }

        return planarVelocity.magnitude <= Mathf.Max(0f, groundedSnapStationaryTurnMaxSpeed) &&
               smoothedGroundedWorldMoveInput.magnitude <= Mathf.Clamp01(groundedSnapStationaryTurnMaxSmoothedInput);
    }

    private void SnapGroundedStationaryTurn(Vector3 targetDirection)
    {
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        targetDirection.Normalize();
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
        if (locomotion != null)
        {
            locomotion.SetPositionAndRotation(transform.position, targetRotation, false, false);
        }
        else
        {
            transform.rotation = targetRotation;
        }

        ResetGroundedPivotTurn();
        groundedPivotCooldownTimer = Mathf.Max(groundedPivotCooldownTimer, groundedPivotCooldown);
        groundedPivotTurnValue = 0f;
        groundedPresentationTurn = 0f;
        smoothedGroundedWorldMoveInput = Vector2.zero;
        lastPlanarDirection = targetDirection;
        ForceOrientationLookDirection(targetDirection);
    }

    private Vector2 ResolveGroundedPivotMoveInput(Vector2 targetWorldMoveInput, float targetMagnitude)
    {
        if (!groundedPivotActive ||
            !groundedMoveIntent ||
            targetMagnitude <= movementDeadZone ||
            targetWorldMoveInput.sqrMagnitude <= movementDeadZone * movementDeadZone)
        {
            smoothedGroundedWorldMoveInput = Vector2.zero;
            return Vector2.zero;
        }

        Vector3 targetDirection = new Vector3(targetWorldMoveInput.x, 0f, targetWorldMoveInput.y);
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            smoothedGroundedWorldMoveInput = Vector2.zero;
            return Vector2.zero;
        }

        targetDirection.Normalize();
        float duration = Mathf.Max(0.05f, groundedPivotDuration);
        float progress = Mathf.Clamp01(1f - groundedPivotHoldTimer / duration);
        float releaseStart = Mathf.Min(Mathf.Clamp01(groundedPivotMovementReleaseStart), 0.98f);
        float progressBlend = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(releaseStart, 1f, progress));
        float remainingAngle = Vector3.Angle(transform.forward, targetDirection);
        float releaseMaxAngle = Mathf.Max(8.01f, groundedPivotMovementReleaseMaxAngle);
        float angleBlend = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(releaseMaxAngle, 8f, remainingAngle));
        float releaseScale = Mathf.Min(progressBlend, angleBlend) * Mathf.Clamp01(groundedPivotMovementReleaseScale);
        if (releaseScale <= movementDeadZone)
        {
            smoothedGroundedWorldMoveInput = Vector2.zero;
            return Vector2.zero;
        }

        smoothedGroundedWorldMoveInput = Vector2.ClampMagnitude(
            targetWorldMoveInput.normalized * Mathf.Clamp01(targetMagnitude * releaseScale),
            1f);
        return smoothedGroundedWorldMoveInput;
    }

    private void ApplyGroundedPivotRotationCommit(float deltaTime, bool forceComplete)
    {
        if (!commitRootRotationDuringPivot ||
            locomotion == null ||
            !hasGroundedPivotTargetDirection ||
            groundedPivotTargetDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(groundedPivotTargetDirection.normalized, Vector3.up);
        float angle = Quaternion.Angle(transform.rotation, targetRotation);
        if (angle <= 0.05f)
        {
            return;
        }

        Quaternion nextRotation = forceComplete
            ? targetRotation
            : Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                Mathf.Max(1f, groundedPivotRotationCommitRate) * Mathf.Max(0.0001f, deltaTime));
        locomotion.SetPositionAndRotation(transform.position, nextRotation, false, false);
    }

    private void CommitGroundedPivotTargetDirection()
    {
        if (!hasGroundedPivotTargetDirection ||
            groundedPivotTargetDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 direction = groundedPivotTargetDirection.normalized;
        lastPlanarDirection = direction;
        ForceOrientationLookDirection(direction);
    }

    private void UpdateGroundedMoveTriggers(float speed, Vector3 velocity)
    {
        if (animator == null || IsFlightModeActive)
        {
            previousGroundedMoveIntent = groundedMoveIntent;
            return;
        }

        if (groundedPivotActive)
        {
            ResetAnimatorTrigger(moveStartTriggerParam);
            ResetAnimatorTrigger(moveStopTriggerParam);
            previousGroundedMoveIntent = false;
            return;
        }

        if (groundedMoveIntent && !previousGroundedMoveIntent)
        {
            LatchGroundedMoveTransitionDirection(ResolveGroundedMoveTransitionLocalDirection(velocity));
            ResetAnimatorTrigger(moveStopTriggerParam);
            SetAnimatorTrigger(moveStartTriggerParam);
        }
        else if (!groundedMoveIntent &&
                 previousGroundedMoveIntent &&
                 (Mathf.Max(speed, groundedPresentationSpeed) >= groundedStopTriggerMinSpeed ||
                  IsGroundedStartStateActive()))
        {
            LatchGroundedMoveTransitionDirection(ResolveGroundedMoveTransitionLocalDirection(velocity));
            ResetAnimatorTrigger(moveStartTriggerParam);
            SetAnimatorTrigger(moveStopTriggerParam);
        }

        previousGroundedMoveIntent = groundedMoveIntent;
    }

    private bool IsGroundedStartStateActive()
    {
        if (animator == null)
        {
            return false;
        }

        const int baseLayerIndex = 0;
        if (animator.IsInTransition(baseLayerIndex) &&
            IsGroundedStartState(animator.GetNextAnimatorStateInfo(baseLayerIndex)))
        {
            return true;
        }

        return IsGroundedStartState(animator.GetCurrentAnimatorStateInfo(baseLayerIndex));
    }

    private static bool IsGroundedStartState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName("Walk_Start") || stateInfo.IsName("Run_Start");
    }

    private void TickGroundedMoveIntentAge(float deltaTime)
    {
        if (groundedMoveIntent)
        {
            groundedMoveIntentAge = wasGroundedMoveIntentForAge
                ? groundedMoveIntentAge + Mathf.Max(0f, deltaTime)
                : 0f;
        }
        else
        {
            groundedMoveIntentAge = 0f;
        }

        wasGroundedMoveIntentForAge = groundedMoveIntent;
    }

    private void TickGroundedMoveTransitionDirection(float deltaTime)
    {
        if (groundedMoveTransitionDirectionTimer > 0f)
        {
            groundedMoveTransitionDirectionTimer = Mathf.Max(0f, groundedMoveTransitionDirectionTimer - deltaTime);
            if (groundedMoveTransitionDirectionTimer <= 0f)
            {
                groundedMoveTransitionLocalDirection = Vector2.zero;
            }
        }
    }

    private void LatchGroundedMoveTransitionDirection(Vector2 localDirection)
    {
        if (localDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        groundedMoveTransitionLocalDirection = localDirection.normalized;
        groundedMoveTransitionDirectionTimer = Mathf.Max(0f, groundedMoveTransitionDirectionHoldTime);
    }

    private bool TryGetGroundedMoveTransitionLocalDirection(out Vector2 localDirection, out float parameterSpeed)
    {
        if (groundedMoveTransitionDirectionTimer > 0f &&
            groundedMoveTransitionLocalDirection.sqrMagnitude > 0.0001f)
        {
            localDirection = groundedMoveTransitionLocalDirection.normalized;
            parameterSpeed = Mathf.Max(0f, groundedMoveTransitionParameterSpeed);
            return true;
        }

        localDirection = Vector2.zero;
        parameterSpeed = 0f;
        return false;
    }

    private Vector2 ResolveGroundedMoveTransitionLocalDirection(Vector3 fallbackVelocity)
    {
        if (useForwardOnlyGroundedLocomotion)
        {
            return Vector2.up;
        }

        Vector3 direction = Vector3.zero;
        if (desiredGroundedWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            direction = new Vector3(desiredGroundedWorldMoveInput.x, 0f, desiredGroundedWorldMoveInput.y);
        }
        else if (currentWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            direction = new Vector3(currentWorldMoveInput.x, 0f, currentWorldMoveInput.y);
        }
        else if (fallbackVelocity.sqrMagnitude > 0.0001f)
        {
            direction = fallbackVelocity;
        }
        else
        {
            direction = lastPlanarDirection;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        return ResolveLocalMoveInput(direction.normalized, 1f);
    }

    private void ResetGroundedFeelState()
    {
        desiredGroundedWorldMoveInput = Vector2.zero;
        smoothedGroundedWorldMoveInput = Vector2.zero;
        groundedMoveIntent = false;
        previousGroundedMoveIntent = false;
        ResetGroundedMoveIntentAge();
        groundedPresentationSpeed = 0f;
        groundedPresentationTurn = 0f;
        ResetGroundedPivotTurn();
        ResetGroundedMoveTransitionDirection();
    }

    private void ResetGroundedFeelInput()
    {
        desiredGroundedWorldMoveInput = Vector2.zero;
        smoothedGroundedWorldMoveInput = Vector2.zero;
        groundedMoveIntent = false;
        previousGroundedMoveIntent = false;
        ResetGroundedMoveIntentAge();
        ResetGroundedPivotTurn();
        ResetGroundedMoveTransitionDirection();
    }

    private void ResetGroundedMoveIntentAge()
    {
        groundedMoveIntentAge = 0f;
        wasGroundedMoveIntentForAge = false;
    }

    private void ResetGroundedPivotTurn()
    {
        groundedPivotActive = false;
        groundedPivotHoldTimer = 0f;
        groundedPivotDuration = 0f;
        groundedPivotCooldownTimer = 0f;
        groundedPivotTurnValue = 0f;
        groundedPivotTargetDirection = Vector3.zero;
        hasGroundedPivotTargetDirection = false;
    }

    private void ResetGroundedMoveTransitionDirection()
    {
        groundedMoveTransitionLocalDirection = Vector2.zero;
        groundedMoveTransitionDirectionTimer = 0f;
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
