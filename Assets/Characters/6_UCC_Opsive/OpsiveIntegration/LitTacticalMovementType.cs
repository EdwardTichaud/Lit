using Opsive.UltimateCharacterController.Character.MovementTypes;
using UnityEngine;

/// <summary>The bridge owns facing; UCC must not collapse screen-relative lateral/backward intent into forward.</summary>
[System.Serializable]
public sealed class LitTacticalMovementType : MovementType
{
    public override bool FirstPersonPerspective => false;
    public override Vector2 GetInputVector(Vector2 inputVector) => inputVector;
    public override float GetDeltaYawRotation(float horizontal, float forward, float cameraHorizontal, float cameraVertical) => 0;
}
