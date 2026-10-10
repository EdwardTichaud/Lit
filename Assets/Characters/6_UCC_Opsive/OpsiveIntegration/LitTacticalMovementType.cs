using Opsive.UltimateCharacterController.Character.MovementTypes;
using UnityEngine;

/// <summary>UCC owns the simulated pose; the bridge supplies world-space movement and facing intent.</summary>
[System.Serializable]
public sealed class LitTacticalMovementType : MovementType
{
    public override bool FirstPersonPerspective => false;
    private LitOpsiveLocomotionBridge bridge;
    private LitOpsiveLocomotionBridge Bridge => bridge != null ? bridge : bridge = m_GameObject.GetComponent<LitOpsiveLocomotionBridge>();
    public override Vector2 GetInputVector(Vector2 inputVector) => Bridge != null &&
        Bridge.TryGetTacticalSimulationIntent(out var world, out _) ?
        LitTacticalMotorMath.ResolveInput(world, inputVector.magnitude, m_CharacterLocomotion.Rotation,
            m_CharacterLocomotion.DeltaRotation.y, m_CharacterLocomotion.MotorRotationSpeed *
            m_CharacterLocomotion.TimeScale * Opsive.Shared.Utility.TimeUtility.TimeScale,
            m_CharacterLocomotion.PreviousAccelerationInfluence) : inputVector;
    public override float GetDeltaYawRotation(float horizontal, float forward, float cameraHorizontal, float cameraVertical)
        => Bridge != null && Bridge.TryGetTacticalSimulationIntent(out _, out var facing)
            ? LitTacticalMotorMath.ResolveYaw(m_CharacterLocomotion.Rotation, facing) : 0;
}
