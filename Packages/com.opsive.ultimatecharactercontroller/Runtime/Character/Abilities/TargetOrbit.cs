/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Character.Abilities
{
    using Opsive.UltimateCharacterController.Utility;
    using UnityEngine;

    /// <summary>
    /// Orbits around the target when the character moves.
    /// </summary>
    [DefaultStartType(AbilityStartType.Automatic)]
    [DefaultStopType(AbilityStopType.Automatic)]
    [System.Serializable]
    public class TargetOrbit : Ability
    {
        private const float c_InputEpsilon = 0.0001f;
        private const float c_OrbitDistanceEpsilon = 0.0001f;

        [Tooltip("Should the ability use the assist aim target?")]
        [SerializeField] protected bool m_UseAssistAimTarget;
        [Tooltip("Specifies the target transform if the aim assist target is not used.")]
        [SerializeField] protected Transform m_Target;
        [Tooltip("Should the character rotate to always look at the target while orbiting?")]
        [SerializeField] protected bool m_RotateTowardsTarget;

        public bool UseAssistAimTarget { get { return m_UseAssistAimTarget; } set { m_UseAssistAimTarget = value; } }
        public bool RotateTowardsTarget { get { return m_RotateTowardsTarget; } set { m_RotateTowardsTarget = value; } }

        private AssistAim m_AssistAim;

        public override bool IsConcurrent { get { return true; } }

        private Transform Target => m_UseAssistAimTarget && m_AssistAim != null && m_AssistAim.Target != null ? m_AssistAim.Target : m_Target;
        private bool IsAIAgent => m_CharacterLocomotion.LookSource is LocalLookSource;
        
        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_AssistAim = m_CharacterLocomotion.GetAbility<AssistAim>();
        }
        
        /// <summary>
        /// Called when the ablity is tried to be started. If false is returned then the ability will not be started.
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            return base.CanStartAbility() && Target != null;
        }

        /// <summary>
        /// Stops the ability if the target is null.
        /// </summary>
        public override void Update()
        {
            base.Update();

            if (Target == null) {
                StopAbility();
            }
        }

        /// <summary>
        /// Updates the rotation to face the target if specified.
        /// </summary>
        public override void UpdateRotation()
        {
            if (!m_RotateTowardsTarget || !TryGetOrbitDirection(out var orbitDirection, out _)) {
                return;
            }

            m_CharacterLocomotion.DesiredRotation = GetLookRotationDelta(m_CharacterLocomotion.Rotation, orbitDirection, m_CharacterLocomotion.Up, m_CharacterLocomotion.MotorRotationSpeed);
        }

        /// <summary>
        /// Returns the direction from the character to the target in the orbit plane.
        /// </summary>
        /// <param name="orbitDirection">The direction from the character to the target in the orbit plane.</param>
        /// <param name="orbitDistance">The distance to the target in the orbit plane.</param>
        /// <returns>True if the orbit direction is valid.</returns>
        private bool TryGetOrbitDirection(out Vector3 orbitDirection, out float orbitDistance)
        {
            orbitDirection = Vector3.zero;
            orbitDistance = 0;

            var target = Target;
            if (target == null) {
                return false;
            }

            orbitDirection = Vector3.ProjectOnPlane(target.position - m_Transform.position, m_CharacterLocomotion.Up);
            orbitDistance = orbitDirection.magnitude;
            return orbitDistance > c_OrbitDistanceEpsilon;
        }

        /// <summary>
        /// Returns the rotation delta required to look at the target.
        /// </summary>
        /// <param name="currentRotation">The current character rotation.</param>
        /// <param name="lookDirection">The direction that the character should face.</param>
        /// <param name="up">The character up direction.</param>
        /// <param name="rotationSpeed">The maximum rotation delta.</param>
        /// <returns>The rotation delta required to look at the target.</returns>
        private static Quaternion GetLookRotationDelta(Quaternion currentRotation, Vector3 lookDirection, Vector3 up, float rotationSpeed)
        {
            var targetRotation = Quaternion.LookRotation(lookDirection, up);
            targetRotation = Quaternion.Slerp(currentRotation, targetRotation, rotationSpeed);
            return Quaternion.Inverse(currentRotation) * targetRotation;
        }

        /// <summary>
        /// Returns the local horizontal input relative to the orbit target.
        /// </summary>
        /// <param name="rotation">The orbit rotation.</param>
        /// <param name="localInputX">The input direction in orbit local space.</param>
        /// <returns>True if the local input direction is valid.</returns>
        private bool TryGetOrbitInputX(Quaternion rotation, out float localInputX)
        {
            localInputX = 0;

            var orbitInput = IsAIAgent ? m_CharacterLocomotion.InputVector : m_CharacterLocomotion.RawInputVector;
            if (orbitInput.sqrMagnitude == 0) {
                return false;
            }

            var up = m_CharacterLocomotion.Up;
            Vector3 inputDirection;
            if (IsAIAgent) {
                inputDirection = Vector3.ProjectOnPlane(m_CharacterLocomotion.DesiredMovement, up);
                if (inputDirection.sqrMagnitude <= c_OrbitDistanceEpsilon) {
                    inputDirection = Vector3.ProjectOnPlane(m_Transform.TransformDirection(new Vector3(orbitInput.x, 0, orbitInput.y)), up);
                }
            } else {
                var inputRotation = m_CharacterLocomotion.LookSource != null ? m_CharacterLocomotion.LookSource.Transform.rotation : m_CharacterLocomotion.Rotation;
                inputDirection = Vector3.ProjectOnPlane(inputRotation * new Vector3(orbitInput.x, 0, orbitInput.y), up);
            }

            if (inputDirection.sqrMagnitude <= c_OrbitDistanceEpsilon) {
                return false;
            }

            localInputX = MathUtility.InverseTransformDirection(inputDirection.normalized, rotation).x;
            return Mathf.Abs(localInputX) > c_InputEpsilon;
        }

        /// <summary>
        /// Verify the position values. Called immediately before the position is applied.
        /// </summary>
        public override void ApplyPosition()
        {
            if (!TryGetOrbitDirection(out var orbitDirection, out var orbitDistance)) {
                return;
            }

            var rotation = Quaternion.LookRotation(orbitDirection / orbitDistance, m_CharacterLocomotion.Up);
            if (!TryGetOrbitInputX(rotation, out var localInputX)) {
                return;
            }

            var desiredMovementMagnitude = Vector3.ProjectOnPlane(m_CharacterLocomotion.DesiredMovement, m_CharacterLocomotion.Up).magnitude;
            if (desiredMovementMagnitude <= c_InputEpsilon) {
                return;
            }

            // Keep the lateral movement while adding the inward correction required to stay on the same orbit radius.
            var localDesiredMovement = MathUtility.InverseTransformDirection(m_CharacterLocomotion.DesiredMovement, rotation);
            localDesiredMovement.x = localInputX * desiredMovementMagnitude;
            m_CharacterLocomotion.DesiredMovement = MathUtility.TransformDirection(GetLocalOrbitMovement(localDesiredMovement, orbitDistance), rotation);
        }

        /// <summary>
        /// Returns the desired movement in target local space while keeping the same orbit radius.
        /// </summary>
        /// <param name="localDesiredMovement">The desired movement relative to the target.</param>
        /// <param name="orbitDistance">The distance to the target in the orbit plane.</param>
        /// <returns>The local desired movement which stays on the same orbit radius.</returns>
        private static Vector3 GetLocalOrbitMovement(Vector3 localDesiredMovement, float orbitDistance)
        {
            localDesiredMovement.z = 0;
            if (Mathf.Abs(localDesiredMovement.x) <= c_InputEpsilon || orbitDistance <= c_OrbitDistanceEpsilon) {
                return localDesiredMovement;
            }

            localDesiredMovement.x = Mathf.Clamp(localDesiredMovement.x, -orbitDistance, orbitDistance);
            var orbitDistanceSqr = orbitDistance * orbitDistance;
            var tangentDistanceSqr = localDesiredMovement.x * localDesiredMovement.x;
            localDesiredMovement.z = orbitDistance - Mathf.Sqrt(Mathf.Max(orbitDistanceSqr - tangentDistanceSqr, 0));
            return localDesiredMovement;
        }
    }
}
