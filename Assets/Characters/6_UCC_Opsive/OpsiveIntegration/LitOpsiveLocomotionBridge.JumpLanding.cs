using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    // The dedicated LucianJumpPresentationController owns all jump presentation.
    // These hooks remain because the input bridge still calls them as part of its lifecycle.
    private void ValidateJumpLandingSettings() { }

    private void ResetJumpLandingState() { }

    private void NotifyJumpStarted() { }

    private void UpdateJumpLandingAnimatorParameters() { }

    private Vector2 ResolveJumpLandingWorldMoveInput(Vector2 targetWorldMoveInput)
    {
        return targetWorldMoveInput;
    }

    private bool IsLandingPresentationLocked()
    {
        return jumpPresentationController != null && jumpPresentationController.PresentationLocked;
    }
}
