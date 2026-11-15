using System;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

namespace Lit.Traversal
{
    /// <summary>Submits the held pose before UCC resolves movement and camera interpolation.</summary>
    [Serializable]
    internal sealed class ClingCarryAbility : Ability
    {
        internal delegate bool PoseSource(Vector3 position, Quaternion rotation, out Pose pose);
        internal PoseSource Source { get; set; }
        private Pose target;
        private bool hasTarget;

        public override bool IsConcurrent => true;

        public ClingCarryAbility()
        {
            m_StartType = AbilityStartType.Manual;
            m_StopType = AbilityStopType.Manual;
            m_AbilityIndexParameter = -1;
        }

        public override void ApplyRotation()
        {
            var motor = m_CharacterLocomotion;
            hasTarget = Source != null && Source(motor.Position, motor.Rotation, out target);
            motor.DesiredRotation = hasTarget ? Quaternion.Inverse(motor.Rotation) * target.rotation : Quaternion.identity;
        }

        public override void UpdateDesiredMovement()
        {
            var motor = m_CharacterLocomotion;
            motor.DesiredMovement = hasTarget ? target.position - motor.Position : Vector3.zero;
        }
    }
}
