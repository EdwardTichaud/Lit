using System;
using Opsive.Shared.Utility;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

// Like UCC Jump, takeoff intent is submitted before the collision/ground solver.
[Serializable]
public sealed class LitUccActionTakeoffAbility : Ability
{
    private float speed, remaining;
    private bool scriptedRise;
    public bool IsAirborneHold { get; private set; }
    public override bool IsConcurrent => true;
    public LitUccActionTakeoffAbility()
    {
        m_StartType = AbilityStartType.Manual; m_StopType = AbilityStopType.Manual;
        m_AbilityIndexParameter = -1;
    }
    public void Configure(float upwardSpeed, float duration = .08f, bool scripted = false)
    { speed = Mathf.Max(0, upwardSpeed); remaining = Mathf.Max(.01f, duration); scriptedRise = scripted; IsAirborneHold = false; }
    public void ConfigureHold() { IsAirborneHold = true; scriptedRise = false; }
    public override void UpdatePosition()
    {
        if (IsAirborneHold || scriptedRise) return;
        if (remaining <= 0) { m_CharacterLocomotion.TryStopAbility(this); return; }
        float dt = TimeUtility.DeltaTime;
        if (dt <= 0) return;
        var movement = m_CharacterLocomotion.LocalDesiredMovement;
        movement.y = Mathf.Max(movement.y, speed * dt);
        m_CharacterLocomotion.LocalDesiredMovement = movement;
        remaining -= dt;
    }
    public override void UpdateDesiredMovement()
    {
        if (!IsAirborneHold && !scriptedRise) return;
        if (scriptedRise && remaining <= 0) { m_CharacterLocomotion.TryStopAbility(this); return; }
        float dt = TimeUtility.DeltaTime;
        if (dt <= 0) return;
        var movement = m_CharacterLocomotion.LocalDesiredMovement;
        movement.y = IsAirborneHold ? 0 : speed * Mathf.Min(dt, remaining);
        m_CharacterLocomotion.LocalDesiredMovement = movement;
        if (scriptedRise) remaining = Mathf.Max(0, remaining - dt);
    }
}
