using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    public bool UsesSimpleWholeBodyPresentation => ModuleSettings.presentationProfile != null;
    public bool IsMeasuredLocomotionPresentationActive => isActiveAndEnabled && IsDriving && driveLitLocomotionAnimatorParameters &&
        animator != null && locomotion != null && locomotion.Grounded && !IsFlightModeActive && !IsCinematicMotionSessionActive &&
        animatorMonitor is LitPresentationAnimatorMonitor && ModuleSettings.presentationProfile != null && ModuleSettings.presentationProfile.IsUsable(animator.avatar);
    private static bool IsGroundPresentationState(AnimatorStateInfo state)
    {
        foreach (string name in GroundPresentationStates)
            if (state.shortNameHash == Animator.StringToHash(name)) return true;
        return false;
    }
    private static readonly string[] GroundPresentationStates =
        { "Locomotion", "CombatLocomotion", "CombatIdle", "Walk_Start", "Run_Start", "Walk_Stop", "Run_Stop", "WalkStartToRun" };
}
