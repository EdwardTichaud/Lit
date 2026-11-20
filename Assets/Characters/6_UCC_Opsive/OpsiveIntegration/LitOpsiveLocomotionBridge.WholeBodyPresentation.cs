using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    private bool UpdateSimpleWholeBodyPresentation(Vector3 velocity, float speed)
    {
        var profile = ModuleSettings.presentationProfile;
        if (animator == null || profile == null || !profile.IsUsable(animator.avatar)) return true;
        if (IsCinematicMotionSessionActive || !Grounded) return true;
        bool strafe = combatLockActive && !IsCombatFreeSprint;
        Vector3 local = transform.InverseTransformDirection(velocity);
        Vector2 direction = new Vector2(local.x, local.z).normalized;
        // Parameters report physical motion. State Speed and Blend Tree thresholds
        // are authored manually in the Animator; no cadence correction is applied.
        SetAnimatorFloat(speedParam, speed);
        SetAnimatorFloat(locomotionTierParam, speed);
        SetAnimatorFloat(horizontalMovementParam, strafe ? direction.x : 0);
        SetAnimatorFloat(forwardMovementParam, strafe ? direction.y : 1);
        SetAnimatorFloat(combatMoveMagnitudeParam, speed > profile.stopSpeed ? 1 : 0);
        SetAnimatorBool(isMovingParam, speed > profile.stopSpeed);
        SetAnimatorBool(turnInPlaceParam, false);
        SetAnimatorFloat(turnParam, 0);
        ResetAnimatorTrigger(moveStartTriggerParam);
        ResetAnimatorTrigger(moveStopTriggerParam);
        groundedPresentationState = speed > profile.stopSpeed ? LocomotionPresentationState.Moving : LocomotionPresentationState.Idle;

        // Only ground locomotion may select its next state. Actions, interactions
        // and dialogue keep their owner until that owner releases presentation.
        var current = animator.GetCurrentAnimatorStateInfo(0);
        if (!IsGroundPresentationState(current) || animator.IsInTransition(0) ||
            GetComponent<PlayerActionPresentationController>()?.HasDialoguePresentation == true ||
            GetComponent<PlayerActionPresentationController>()?.IsActionActive == true) return true;
        int destination = strafe ? (speed > profile.stopSpeed ? CombatLocomotionStateHash : CombatIdleStateHash) : Animator.StringToHash("Base Layer.Locomotion");
        if (current.fullPathHash != destination && animator.HasState(0, destination))
            animator.CrossFadeInFixedTime(destination, .18f, 0);
        return true;
    }
}
