using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    [Header("Jump")]
    [SerializeField, Tooltip("Drives the Player_Model jump sequence from UCC grounded state.")]
    private bool driveJumpLandingAnimatorParameters = true;
    [SerializeField] private string jumpTriggerParam = "JumpTrigger";
    [SerializeField] private string isAirborneParam = "IsAirborne";

    private bool jumpSequenceActive;
    private bool jumpSequenceWasAirborne;
    private Vector2 airborneInertiaWorldMoveInput;

    private void ValidateJumpLandingSettings()
    {
    }

    private void ResetJumpLandingState()
    {
        jumpSequenceActive = false;
        jumpSequenceWasAirborne = false;
        airborneInertiaWorldMoveInput = Vector2.zero;

        if (animator != null && driveJumpLandingAnimatorParameters)
        {
            SetAnimatorBool(isAirborneParam, false);
        }
    }

    private void NotifyJumpStarted()
    {
        if (!driveJumpLandingAnimatorParameters || animator == null)
        {
            return;
        }

        jumpSequenceActive = true;
        jumpSequenceWasAirborne = false;
        airborneInertiaWorldMoveInput = currentWorldMoveInput;
        SetAnimatorBool(isAirborneParam, false);
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

        if (!locomotion.Grounded)
        {
            jumpSequenceWasAirborne = true;
            SetAnimatorBool(isAirborneParam, true);
            return;
        }

        if (jumpSequenceActive && jumpSequenceWasAirborne)
        {
            // The controller transitions Jump_Loop -> Jump_End when this becomes false.
            SetAnimatorBool(isAirborneParam, false);
            jumpSequenceActive = false;
            jumpSequenceWasAirborne = false;
            airborneInertiaWorldMoveInput = Vector2.zero;
        }
    }

    private Vector2 ResolveJumpLandingWorldMoveInput(Vector2 targetWorldMoveInput)
    {
        if (!jumpSequenceActive || locomotion == null || locomotion.Grounded)
        {
            return targetWorldMoveInput;
        }

        // Do not cancel the takeoff momentum just because the player released the stick
        // in mid-air. Fresh input still replaces the carried direction.
        if (targetWorldMoveInput.sqrMagnitude > movementDeadZone * movementDeadZone)
        {
            airborneInertiaWorldMoveInput = targetWorldMoveInput;
        }

        return airborneInertiaWorldMoveInput;
    }
}
