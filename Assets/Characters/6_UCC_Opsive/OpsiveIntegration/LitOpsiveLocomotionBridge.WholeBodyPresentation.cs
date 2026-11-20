using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    private bool UpdateSimpleWholeBodyPresentation(Vector3 velocity, float speed)
    {
        var profile = ModuleSettings.presentationProfile;
        if (animator == null || profile == null || !profile.IsUsable(animator.avatar)) return true;
        if (IsCinematicMotionSessionActive || !Grounded) return true;
        float rate = timeDomain != null ? timeDomain.BaseAnimatorSpeed * timeDomain.Scale : animator.speed;
        if (rate <= .0001f) return true;
        float actorSpeed = speed / rate;
        bool strafe = combatLockActive && !IsCombatFreeSprint;
        Vector3 local = transform.InverseTransformDirection(velocity);
        Vector2 direction = new Vector2(local.x, local.z).normalized;
        profile.Evaluate(actorSpeed, sprintPressed, strafe ? direction : Vector2.up,
            out float blend, out float cadence, !strafe);
        SetAnimatorFloat(speedParam, blend);
        SetAnimatorFloat(locomotionTierParam, blend);
        SetAnimatorFloat(horizontalMovementParam, strafe ? direction.x : 0);
        SetAnimatorFloat(forwardMovementParam, strafe ? direction.y : 1);
        SetAnimatorFloat(combatMoveMagnitudeParam, actorSpeed > profile.stopSpeed ? 1 : 0);
        SetAnimatorFloat(LocomotionPresentationProfile.PlaybackParameter, cadence);
        SetAnimatorBool(isMovingParam, actorSpeed > profile.stopSpeed);
        SetAnimatorBool(turnInPlaceParam, false);
        SetAnimatorFloat(turnParam, 0);
        ResetAnimatorTrigger(moveStartTriggerParam);
        ResetAnimatorTrigger(moveStopTriggerParam);
        groundedPresentationState = actorSpeed > profile.stopSpeed ? LocomotionPresentationState.Moving : LocomotionPresentationState.Idle;

        // Only ground locomotion may select its next state. Actions, interactions
        // and dialogue keep their owner until that owner releases presentation.
        var current = animator.GetCurrentAnimatorStateInfo(0);
        if (!IsGroundPresentationState(current) || animator.IsInTransition(0) ||
            GetComponent<PlayerActionPresentationController>()?.HasDialoguePresentation == true ||
            GetComponent<PlayerActionPresentationController>()?.IsActionActive == true) return true;
        int destination = strafe ? (actorSpeed > profile.stopSpeed ? CombatLocomotionStateHash : CombatIdleStateHash) : Animator.StringToHash("Base Layer.Locomotion");
        if (current.fullPathHash != destination && animator.HasState(0, destination))
            animator.CrossFadeInFixedTime(destination, .18f, 0);
        return true;
    }
}
