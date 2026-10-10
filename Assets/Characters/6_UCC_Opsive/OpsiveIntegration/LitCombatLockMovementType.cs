using Opsive.UltimateCharacterController.Character.MovementTypes;
using UnityEngine;

/// <summary>
/// UCC movement type used exclusively while a realtime-combat target is locked.
/// Tactical movement and facing are resolved in the UCC motor's simulation frame.
/// Third-person retains the bridge's existing target-relative movement and facing.
/// </summary>
[System.Serializable]
public sealed class LitCombatLockMovementType : MovementType
{
    private LitOpsiveLocomotionBridge bridge;
    private LitOpsiveLocomotionBridge Bridge => bridge != null ? bridge : bridge = m_GameObject.GetComponent<LitOpsiveLocomotionBridge>();
    public override bool FirstPersonPerspective => false;

    public override float GetDeltaYawRotation(
        float characterHorizontalMovement,
        float characterForwardMovement,
        float cameraHorizontalMovement,
        float cameraVerticalMovement)
    {
        if (Bridge != null && Bridge.TryGetTacticalSimulationIntent(out _, out var facing))
            return LitTacticalMotorMath.ResolveYaw(m_CharacterLocomotion.Rotation, facing);
        // Third-person keeps its existing target-facing bridge.
        return 0f;
    }

    public override Vector2 GetInputVector(Vector2 inputVector)
    {
        if (Bridge != null && Bridge.TryGetTacticalSimulationIntent(out var world, out _))
            return LitTacticalMotorMath.ResolveInput(world, inputVector.magnitude, m_CharacterLocomotion.Rotation,
                m_CharacterLocomotion.DeltaRotation.y, m_CharacterLocomotion.MotorRotationSpeed *
                m_CharacterLocomotion.TimeScale * Opsive.Shared.Utility.TimeUtility.TimeScale,
                m_CharacterLocomotion.PreviousAccelerationInfluence);
        // Preserve left, right, backward and diagonal axes exactly as injected
        // by the target-relative combat bridge.
        return inputVector;
    }
}
