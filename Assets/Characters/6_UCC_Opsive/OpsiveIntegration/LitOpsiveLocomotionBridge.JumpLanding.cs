using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    [Header("Jump")]
    [SerializeField, Tooltip("Drives the Player_Model jump sequence from UCC grounded state.")]
    private bool driveJumpLandingAnimatorParameters = true;
    [SerializeField] private string jumpTriggerParam = "JumpTrigger";
    [SerializeField] private string isAirborneParam = "IsAirborne";
    [SerializeField, Tooltip("Reduces gravity only during the descending half of a player-initiated jump.")]
    private bool softenJumpDescent = true;
    [SerializeField, Min(0f)] private float jumpDescentGravityAmount = 0.56f;

    private bool jumpSequenceActive;
    private bool jumpSequenceWasAirborne;
    private Vector2 airborneInertiaWorldMoveInput;
    private bool jumpDescentGravityApplied;
    private float previousJumpGravityAmount;

    private void ValidateJumpLandingSettings()
    {
    }

    private void ResetJumpLandingState()
    {
        RestoreJumpDescentGravity();
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
            UpdateJumpDescentGravity();
            return;
        }

        RestoreJumpDescentGravity();
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

    private void UpdateJumpDescentGravity()
    {
        if (!jumpSequenceActive ||
            !softenJumpDescent ||
            jumpDescentGravityApplied ||
            Vector3.Dot(locomotion.Velocity, transform.up) > 0f)
        {
            return;
        }

        previousJumpGravityAmount = locomotion.GravityAmount;
        locomotion.GravityAmount = Mathf.Min(previousJumpGravityAmount, Mathf.Max(0f, jumpDescentGravityAmount));
        jumpDescentGravityApplied = true;
    }

    private void RestoreJumpDescentGravity()
    {
        if (!jumpDescentGravityApplied || locomotion == null)
        {
            jumpDescentGravityApplied = false;
            return;
        }

        locomotion.GravityAmount = previousJumpGravityAmount;
        jumpDescentGravityApplied = false;
    }
}
