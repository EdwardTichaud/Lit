using Opsive.UltimateCharacterController.Character;
using UnityEngine;

/// <summary>Stops the demonstrated competing stick writes when the bridge owns measured ground presentation.</summary>
public sealed class LitPresentationAnimatorMonitor : AnimatorMonitor
{
    private LitOpsiveLocomotionBridge presentationBridge;
    private bool BridgeOwnsDirection
    {
        get
        {
            if (!isActiveAndEnabled) return false;
            if (presentationBridge == null) presentationBridge = GetComponentInParent<LitOpsiveLocomotionBridge>();
            return presentationBridge != null && presentationBridge.IsMeasuredLocomotionPresentationActive;
        }
    }
    public override bool SetHorizontalMovementParameter(float value,float timeScale,float dampingTime)
        => BridgeOwnsDirection ? false : base.SetHorizontalMovementParameter(value,timeScale,dampingTime);
    public override bool SetForwardMovementParameter(float value,float timeScale,float dampingTime)
        => BridgeOwnsDirection ? false : base.SetForwardMovementParameter(value,timeScale,dampingTime);
}
