using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    private bool enableCinematicGroundedFeel { get => ModuleSettings.enableCinematicGroundedFeel; set => ModuleSettings.enableCinematicGroundedFeel = value; }
    private bool tuneGroundedUccPhysics { get => ModuleSettings.tuneGroundedUccPhysics; set => ModuleSettings.tuneGroundedUccPhysics = value; }
    private Vector3 groundedMotorAcceleration { get => ModuleSettings.groundedMotorAcceleration; set => ModuleSettings.groundedMotorAcceleration = value; }
    private float groundedMotorDamping { get => ModuleSettings.groundedMotorDamping; set => ModuleSettings.groundedMotorDamping = value; }
    private float groundedPreviousAccelerationInfluence { get => ModuleSettings.groundedPreviousAccelerationInfluence; set => ModuleSettings.groundedPreviousAccelerationInfluence = value; }
    private float groundedBackwardsMultiplier { get => ModuleSettings.groundedBackwardsMultiplier; set => ModuleSettings.groundedBackwardsMultiplier = value; }
    private float groundedGravityAmount { get => ModuleSettings.groundedGravityAmount; set => ModuleSettings.groundedGravityAmount = value; }
    private float groundedStickToGroundDistance { get => ModuleSettings.groundedStickToGroundDistance; set => ModuleSettings.groundedStickToGroundDistance = value; }
    private float groundedSlopeLimit { get => ModuleSettings.groundedSlopeLimit; set => ModuleSettings.groundedSlopeLimit = value; }
    private float groundedMaxStepHeight { get => ModuleSettings.groundedMaxStepHeight; set => ModuleSettings.groundedMaxStepHeight = value; }
    private float groundedMovingPlatformSeparationVelocity { get => ModuleSettings.groundedMovingPlatformSeparationVelocity; set => ModuleSettings.groundedMovingPlatformSeparationVelocity = value; }
    private float groundedMovingPlatformDisconnectMultiplier { get => ModuleSettings.groundedMovingPlatformDisconnectMultiplier; set => ModuleSettings.groundedMovingPlatformDisconnectMultiplier = value; }
    private float groundedMovingPlatformForceDamping { get => ModuleSettings.groundedMovingPlatformForceDamping; set => ModuleSettings.groundedMovingPlatformForceDamping = value; }

    private float groundedInputAcceleration { get => ModuleSettings.groundedInputAcceleration; set => ModuleSettings.groundedInputAcceleration = value; }
    private float groundedSprintInputAcceleration { get => ModuleSettings.groundedSprintInputAcceleration; set => ModuleSettings.groundedSprintInputAcceleration = value; }
    private float groundedInputDeceleration { get => ModuleSettings.groundedInputDeceleration; set => ModuleSettings.groundedInputDeceleration = value; }
    private float groundedDirectionChangeAcceleration { get => ModuleSettings.groundedDirectionChangeAcceleration; set => ModuleSettings.groundedDirectionChangeAcceleration = value; }
    private float groundedDirectionChangeDot { get => ModuleSettings.groundedDirectionChangeDot; set => ModuleSettings.groundedDirectionChangeDot = value; }

    private bool tuneGroundedSprintSpeedChange { get => ModuleSettings.tuneGroundedSprintSpeedChange; set => ModuleSettings.tuneGroundedSprintSpeedChange = value; }
    private float groundedSprintSpeedMultiplier { get => ModuleSettings.groundedSprintSpeedMultiplier; set => ModuleSettings.groundedSprintSpeedMultiplier = value; }
    private float groundedSprintSpeedParameterValue { get => ModuleSettings.groundedSprintSpeedParameterValue; set => ModuleSettings.groundedSprintSpeedParameterValue = value; }
    private bool suppressSpeedChangeAnimatorParameter { get => ModuleSettings.suppressSpeedChangeAnimatorParameter; set => ModuleSettings.suppressSpeedChangeAnimatorParameter = value; }

    private string moveStartTriggerParam { get => ModuleSettings.moveStartTriggerParam; set => ModuleSettings.moveStartTriggerParam = value; }
    private string moveStopTriggerParam { get => ModuleSettings.moveStopTriggerParam; set => ModuleSettings.moveStopTriggerParam = value; }
    private string turnInPlaceParam { get => ModuleSettings.turnInPlaceParam; set => ModuleSettings.turnInPlaceParam = value; }
    private float groundedTurnInPlaceThreshold { get => ModuleSettings.groundedTurnInPlaceThreshold; set => ModuleSettings.groundedTurnInPlaceThreshold = value; }
    private float groundedTurnInPlaceMaxSpeed { get => ModuleSettings.groundedTurnInPlaceMaxSpeed; set => ModuleSettings.groundedTurnInPlaceMaxSpeed = value; }
    private bool enableGroundedTurnInPlaceClips { get => ModuleSettings.enableGroundedTurnInPlaceClips; set => ModuleSettings.enableGroundedTurnInPlaceClips = value; }
    private float groundedStopRequestDelay { get => ModuleSettings.groundedStopRequestDelay; set => ModuleSettings.groundedStopRequestDelay = value; }
    private float groundedStopSettledSpeed { get => ModuleSettings.groundedStopSettledSpeed; set => ModuleSettings.groundedStopSettledSpeed = value; }
    private float groundedStopSettledDuration { get => ModuleSettings.groundedStopSettledDuration; set => ModuleSettings.groundedStopSettledDuration = value; }
    private float groundedStopExitNormalizedTime { get => ModuleSettings.groundedStopExitNormalizedTime; set => ModuleSettings.groundedStopExitNormalizedTime = value; }
    private float groundedMoveTransitionDirectionHoldTime { get => ModuleSettings.groundedMoveTransitionDirectionHoldTime; set => ModuleSettings.groundedMoveTransitionDirectionHoldTime = value; }
    private float groundedMoveTransitionParameterSpeed { get => ModuleSettings.groundedMoveTransitionParameterSpeed; set => ModuleSettings.groundedMoveTransitionParameterSpeed = value; }
    private bool useForwardOnlyGroundedLocomotion { get => ModuleSettings.useForwardOnlyGroundedLocomotion; set => ModuleSettings.useForwardOnlyGroundedLocomotion = value; }
    private bool enableScriptedPivotTurns { get => ModuleSettings.enableScriptedPivotTurns; set => ModuleSettings.enableScriptedPivotTurns = value; }
    private float groundedPivotMinAngle { get => ModuleSettings.groundedPivotMinAngle; set => ModuleSettings.groundedPivotMinAngle = value; }
    private float groundedPivot180Angle { get => ModuleSettings.groundedPivot180Angle; set => ModuleSettings.groundedPivot180Angle = value; }
    private float groundedPivotMaxSpeed { get => ModuleSettings.groundedPivotMaxSpeed; set => ModuleSettings.groundedPivotMaxSpeed = value; }
    private float groundedPivotMaxSmoothedInput { get => ModuleSettings.groundedPivotMaxSmoothedInput; set => ModuleSettings.groundedPivotMaxSmoothedInput = value; }
    private float groundedPivotHoldTime { get => ModuleSettings.groundedPivotHoldTime; set => ModuleSettings.groundedPivotHoldTime = value; }
    private float groundedPivotCooldown { get => ModuleSettings.groundedPivotCooldown; set => ModuleSettings.groundedPivotCooldown = value; }
    private float groundedPivotStartGraceTime { get => ModuleSettings.groundedPivotStartGraceTime; set => ModuleSettings.groundedPivotStartGraceTime = value; }
    private float groundedPivotStartGraceMinAngle { get => ModuleSettings.groundedPivotStartGraceMinAngle; set => ModuleSettings.groundedPivotStartGraceMinAngle = value; }
    private float groundedPivotMovementReleaseStart { get => ModuleSettings.groundedPivotMovementReleaseStart; set => ModuleSettings.groundedPivotMovementReleaseStart = value; }
    private float groundedPivotMovementReleaseMaxAngle { get => ModuleSettings.groundedPivotMovementReleaseMaxAngle; set => ModuleSettings.groundedPivotMovementReleaseMaxAngle = value; }
    private float groundedPivotMovementReleaseScale { get => ModuleSettings.groundedPivotMovementReleaseScale; set => ModuleSettings.groundedPivotMovementReleaseScale = value; }
    private bool commitBodyRotationDuringPivot { get => ModuleSettings.commitBodyRotationDuringPivot; set => ModuleSettings.commitBodyRotationDuringPivot = value; }
    private float groundedPivotRotationCommitRate { get => ModuleSettings.groundedPivotRotationCommitRate; set => ModuleSettings.groundedPivotRotationCommitRate = value; }

    private Vector2 desiredGroundedWorldMoveInput;
    private Vector2 smoothedGroundedWorldMoveInput;
    private bool groundedMoveIntent;
    private float groundedMoveIntentAge;
    private bool wasGroundedMoveIntentForAge;
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
    private LocomotionPresentationState groundedPresentationState;

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

        if (!tuneGroundedUccPhysics || groundedFeelProfileApplied)
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
        groundedFeelSpeedChange.SpeedParameter = suppressSpeedChangeAnimatorParameter
            ? -1f
            : Mathf.Max(0f, groundedSprintSpeedParameterValue);
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

        // The combat strafe blend tree is InPlace. It must have a real neutral
        // input on release: carrying the exploration input smoothing into this
        // state keeps a walk/strafe pose alive after the stick is neutral.
        // Scripted actions retain their own motion path and never reach here as
        // a player move input, so this does not cancel dashes or skill inertia.
        if (combatLockActive && !groundedMoveIntent)
        {
            smoothedGroundedWorldMoveInput = Vector2.zero;
            return Vector2.zero;
        }

        if (combatLockActive)
        {
            // The free-movement pivot system writes root yaw and can only
            // reason about camera-relative direction. It is incompatible with
            // face-to-face combat, where the locked target owns yaw.
            ResetGroundedPivotTurn();
            ResetGroundedMoveTransitionDirection();
            smoothedGroundedWorldMoveInput = targetWorldMoveInput;
            return targetWorldMoveInput;
        }

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
        if (!enableScriptedPivotTurns ||
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
        groundedPivotTargetDirection = targetDirection;
        hasGroundedPivotTargetDirection = true;
        CommitGroundedPivotTargetDirection();
        groundedPivotDuration = Mathf.Max(0.05f, groundedPivotHoldTime);
        groundedPivotHoldTimer = groundedPivotDuration;
        groundedPivotActive = true;
        return true;
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
        if (!commitBodyRotationDuringPivot ||
            locomotion == null ||
            !hasGroundedPivotTargetDirection ||
            groundedPivotTargetDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(groundedPivotTargetDirection.normalized, Vector3.up);
        if (IsTacticalMovementActive)
        {
            tacticalFacingDirection = groundedPivotTargetDirection.normalized;
            ForceOrientationLookDirection(tacticalFacingDirection);
            return;
        }
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

    private void ResetGroundedFeelState()
    {
        desiredGroundedWorldMoveInput = Vector2.zero;
        smoothedGroundedWorldMoveInput = Vector2.zero;
        groundedMoveIntent = false;

        ResetGroundedMoveIntentAge();
        groundedPresentationTurn = 0f;
        groundedPresentationState = LocomotionPresentationState.Idle;
        ResetGroundedPivotTurn();
        ResetGroundedMoveTransitionDirection();
    }

    private void ResetGroundedFeelInput()
    {
        desiredGroundedWorldMoveInput = Vector2.zero;
        smoothedGroundedWorldMoveInput = Vector2.zero;
        groundedMoveIntent = false;

        groundedPresentationState = LocomotionPresentationState.Idle;
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
        if (animator != null && timeDomain != null && ModuleSettings.presentationProfile != null &&
            ModuleSettings.presentationProfile.IsUsable(animator.avatar))
            return Time.inFixedTimeStep ? timeDomain.FixedDeltaTime : timeDomain.DeltaTime;
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
