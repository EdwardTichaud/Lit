using System;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;
using Opsive.Shared.Game;
using Opsive.Shared.Utility;

/// <summary>Runs measured trajectories inside UCC's movement/collision pipeline, never after physics.</summary>
[Serializable]
public sealed class LitUccStateMotionAbility : Ability
{
    private PlayerStateMotionController controller;
    public override bool IsConcurrent => true;

    public LitUccStateMotionAbility()
    {
        m_StartType = AbilityStartType.Automatic;
        m_StopType = AbilityStopType.Manual;
        m_AbilityIndexParameter = -1;
    }

    public override void UpdateDesiredMovement()
    {
        controller ??= m_GameObject.GetComponent<PlayerStateMotionController>();
        if (controller == null || !controller.isActiveAndEnabled) return;
        if (!controller.TryEvaluateMotion(TimeUtility.DeltaTime, out Vector3 delta)) return;
        Vector3 vertical = Vector3.Project(m_CharacterLocomotion.DesiredMovement, m_CharacterLocomotion.Up);
        // Preserve gravity and let UCC resolve ground, walls, steps and platforms after this callback.
        m_CharacterLocomotion.DesiredMovement = vertical + Vector3.ProjectOnPlane(delta, m_CharacterLocomotion.Up);
    }

    public override void UpdateRotation()
    {
        controller ??= m_GameObject.GetComponent<PlayerStateMotionController>();
        if (controller != null && controller.isActiveAndEnabled && controller.RefreshLandingLock())
            m_CharacterLocomotion.DesiredRotation = Quaternion.identity;
    }
}
