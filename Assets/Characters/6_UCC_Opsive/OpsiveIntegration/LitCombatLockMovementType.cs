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
        if (Bridge != null && Bridge.TryGetCombatSprintSimulationIntent(out _, out var sprintFacing, out var rate))
        {
            return ResolveRateLimitedYaw(sprintFacing, rate);
        }
        if (Bridge != null && Bridge.TryGetTacticalSimulationIntent(out _, out var facing))
            return Bridge.UseRateLimitedCombatFacing
                ? ResolveRateLimitedYaw(facing, Bridge.CombatFacingTurnRate)
                : LitTacticalMotorMath.ResolveYaw(m_CharacterLocomotion.Rotation, facing);
        // Third-person keeps its existing target-facing bridge.
        return 0f;
    }

    private float ResolveRateLimitedYaw(Vector3 facing, float rate)
    {
        // The motor blends yaw once more. Compensate without exceeding its
        // shortest-arc limit, including when returning from directional evasion.
        float blend = Mathf.Clamp01(m_CharacterLocomotion.MotorRotationSpeed *
            m_CharacterLocomotion.TimeScale * Opsive.Shared.Utility.TimeUtility.TimeScale);
        if (blend <= .0001f) return 0f;
        float step = Mathf.Min(rate * Time.fixedDeltaTime, 179f * blend);
        return Mathf.Clamp(LitTacticalMotorMath.ResolveYaw(m_CharacterLocomotion.Rotation, facing), -step, step) / blend;
    }

    public override Vector2 GetInputVector(Vector2 inputVector)
    {
        if (Bridge != null && Bridge.TryGetCombatSprintSimulationIntent(out var sprintWorld, out _, out _))
            return LitTacticalMotorMath.ResolveInput(sprintWorld, inputVector.magnitude, m_CharacterLocomotion.Rotation,
                m_CharacterLocomotion.DeltaRotation.y, m_CharacterLocomotion.MotorRotationSpeed *
                m_CharacterLocomotion.TimeScale * Opsive.Shared.Utility.TimeUtility.TimeScale,
                m_CharacterLocomotion.PreviousAccelerationInfluence);
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
