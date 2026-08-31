using Opsive.UltimateCharacterController.Character.MovementTypes;
using UnityEngine;

/// <summary>
/// UCC movement type used exclusively while a realtime-combat target is locked.
/// Position axes stay untouched and body yaw is intentionally owned by
/// LitOpsiveLocomotionBridge, which keeps the actor facing EnemyLockPoint.
/// </summary>
[System.Serializable]
public sealed class LitCombatLockMovementType : MovementType
{
    public override bool FirstPersonPerspective => false;

    public override float GetDeltaYawRotation(
        float characterHorizontalMovement,
        float characterForwardMovement,
        float cameraHorizontalMovement,
        float cameraVerticalMovement)
    {
        // A camera-relative yaw here would fight the target-facing lock.
        return 0f;
    }

    public override Vector2 GetInputVector(Vector2 inputVector)
    {
        // Preserve left, right, backward and diagonal axes exactly as injected
        // by the target-relative combat bridge.
        return inputVector;
    }
}
