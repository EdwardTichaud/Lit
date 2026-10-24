using Opsive.UltimateCharacterController.Character;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Keeps UCC's physics/smoothing cache aligned with NGO's interpolated server
/// pose. Updating the Transform alone lets SimulationManager restore an old pose.
/// </summary>
public sealed class ServerUccNetworkTransform : NetworkTransform
{
    private CharacterLocomotion motor;
    private bool snapMotor;

    public override void OnNetworkSpawn()
    {
        motor = GetComponent<CharacterLocomotion>();
        snapMotor = true;
        base.OnNetworkSpawn();
    }

    protected override void OnNetworkTransformStateUpdated(ref NetworkTransformState oldState,
        ref NetworkTransformState newState)
    {
        snapMotor |= newState.IsTeleportingNextFrame;
        base.OnNetworkTransformStateUpdated(ref oldState, ref newState);
    }

    protected override void OnTransformUpdated()
    {
        base.OnTransformUpdated();
        // Never change solo locomotion or the authoritative server simulation.
        if (!IsSpawned || CanCommitToTransform) return;
        if (motor == null) motor = GetComponent<CharacterLocomotion>();
        if (motor == null || !motor.isActiveAndEnabled || motor.SimulationIndex < 0) return;

        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        if (snapMotor && motor is UltimateCharacterLocomotion character)
        {
            character.SetPositionAndRotation(position, rotation, snapAnimator: true, stopAllAbilities: true);
            snapMotor = false;
            return;
        }

        // Call the base motor API, not UltimateCharacterLocomotion's animator
        // teleport overload: ordinary interpolation must not snap the camera,
        // stop abilities or replay teleport events every frame.
        bool wasGrounded = motor.Grounded;
        motor.SetRotation(rotation);
        motor.SetPosition(position);
        // Let the next physics step emit the real Grounded/Land transition.
        // SetPosition checks the new floor silently, without sending those events.
        motor.Grounded = wasGrounded;
        snapMotor = false;
    }
}
