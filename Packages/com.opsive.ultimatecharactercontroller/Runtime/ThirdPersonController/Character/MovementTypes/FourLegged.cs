/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.ThirdPersonController.Character.MovementTypes
{
    using Opsive.UltimateCharacterController.Character.MovementTypes;
    using Opsive.UltimateCharacterController.Utility;
    using UnityEngine;

    /// <summary>
    /// The FourLegged MovementType allows the character to move as if they are on four legs. The character cannot strafe and will rotate when trying to turn.
    /// </summary>
    [System.Serializable]
    public class FourLegged : MovementType
    {
        [Tooltip("Specifies if the character should instantly rotate to face the input direction relative to the look source.")]
        [SerializeField] protected bool m_RotateToFaceInputDirection;

        public bool RotateToFaceInputDirection { get => m_RotateToFaceInputDirection; set => m_RotateToFaceInputDirection = value; }
        public override bool FirstPersonPerspective { get { return false; } }

        /// <summary>
        /// Returns the delta yaw rotation of the character.
        /// </summary>
        /// <param name="characterHorizontalMovement">The character's horizontal movement.</param>
        /// <param name="characterForwardMovement">The character's forward movement.</param>
        /// <param name="cameraHorizontalMovement">The camera's horizontal movement.</param>
        /// <param name="cameraVerticalMovement">The camera's vertical movement.</param>
        /// <returns>The delta yaw rotation of the character.</returns>
        public override float GetDeltaYawRotation(float characterHorizontalMovement, float characterForwardMovement, float cameraHorizontalMovement, float cameraVerticalMovement)
        {
#if UNITY_EDITOR
            if (m_LookSource == null) {
                Debug.LogError($"Error: There is no look source attached to the character {m_GameObject.name}. Ensure the character has a look source attached. For player characters the look source is the Camera Controller, and AI agents use the Local Look Source.");
                return 0;
            }
#endif
            if (m_RotateToFaceInputDirection) {
                // Compute the full target angle to face the input direction (relative to the look source).
                if (characterHorizontalMovement != 0 || characterForwardMovement != 0) {
                    var inputDirection = new Vector3(characterHorizontalMovement, 0, characterForwardMovement).normalized;
                    var lookRotation = Quaternion.LookRotation(m_LookSource.Transform.rotation * inputDirection, m_CharacterLocomotion.Up);
                    return MathUtility.ClampInnerAngle(MathUtility.InverseTransformQuaternion(m_CharacterLocomotion.Rotation, lookRotation).eulerAngles.y);
                }
                return 0;
            }

            var localEuler = MathUtility.InverseTransformQuaternion(m_CharacterLocomotion.Rotation, Quaternion.LookRotation(m_CharacterLocomotion.Rotation * Vector3.forward, m_CharacterLocomotion.Up)).eulerAngles;
            localEuler.y += characterHorizontalMovement * (characterForwardMovement < 0 ? -1 : 1);
            // If there is no horizontal movement and some forward movement then the character should turn in the direction of the camera.
            if (characterHorizontalMovement == 0 && characterForwardMovement != 0) {
                var cameraAngle = MathUtility.InverseTransformQuaternion(m_CharacterLocomotion.Rotation, m_LookSource.Transform.rotation).eulerAngles;
                // The horizontal/forward movement input values will always be in a range between -1 and 1. Keep the camera angle within the same range
                // so the rotation doesn't move too quickly.
                localEuler.y += Mathf.Clamp(MathUtility.ClampInnerAngle(cameraAngle.y), -1, 1);
            }
            return localEuler.y;
        }

        /// <summary>
        /// Gets the controller's input vector relative to the movement type.
        /// </summary>
        /// <param name="inputVector">The current input vector.</param>
        /// <returns>The updated input vector.</returns>
        public override Vector2 GetInputVector(Vector2 inputVector)
        {
            if (m_RotateToFaceInputDirection) {
                var clampValue = Mathf.Max(Mathf.Abs(inputVector.x), Mathf.Max(Mathf.Abs(inputVector.y), 1));
                inputVector.y = Mathf.Clamp(inputVector.magnitude, -clampValue, clampValue);
                inputVector.x = 0;
            }
            return inputVector;
        }
    }
}