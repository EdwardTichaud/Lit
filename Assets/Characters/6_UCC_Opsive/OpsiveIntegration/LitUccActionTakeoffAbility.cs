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
    private ChargedSkillSO chargedRise;
    private float chargedElapsed;
    private Vector3 chargedOrigin;
    public bool IsAirborneHold { get; private set; }
    public bool HasPendingChargedRise => scriptedRise && chargedRise != null && (remaining > 0 ||
        chargedRise.launchTrajectory == ChargedLaunchTrajectory.Impulse && remaining > -.15f &&
        Vector3.Dot(m_CharacterLocomotion.Position - chargedOrigin, m_CharacterLocomotion.Up) < chargedRise.launchHeight - .02f);
    public override bool IsConcurrent => true;
    public LitUccActionTakeoffAbility()
    {
        m_StartType = AbilityStartType.Manual; m_StopType = AbilityStopType.Manual;
        m_AbilityIndexParameter = -1;
    }
    public void Configure(float upwardSpeed, float duration = .08f, bool scripted = false)
    { speed = Mathf.Max(0, upwardSpeed); remaining = Mathf.Max(.01f, duration); scriptedRise = scripted; IsAirborneHold = false; chargedRise = null; }
    public void ConfigureChargedRise(ChargedSkillSO skill)
    {
        Configure(0, skill.ascentSeconds, true);
        chargedRise = skill;
        chargedElapsed = 0;
        chargedOrigin = m_CharacterLocomotion.Position;
    }
    public void ConfigureHold() { IsAirborneHold = true; scriptedRise = false; chargedRise = null; }
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
        if (scriptedRise && remaining <= 0 && !HasPendingChargedRise) { m_CharacterLocomotion.TryStopAbility(this); return; }
        float dt = TimeUtility.DeltaTime;
        if (dt <= 0) return;
        var movement = m_CharacterLocomotion.LocalDesiredMovement;
        movement.y = IsAirborneHold ? 0 : speed * Mathf.Min(dt, remaining);
        if (scriptedRise && chargedRise != null)
        {
            movement.y = chargedRise.EvaluateAscentHeight(chargedElapsed + dt) - chargedRise.EvaluateAscentHeight(chargedElapsed);
            if (chargedRise.launchTrajectory == ChargedLaunchTrajectory.Impulse)
            {
                float risen = Vector3.Dot(m_CharacterLocomotion.Position - chargedOrigin, m_CharacterLocomotion.Up);
                float desiredHeight = chargedRise.EvaluateAscentHeight(chargedElapsed + dt);
                float maximumSpeed = 2f * chargedRise.launchHeight / Mathf.Max(.1f, chargedRise.ascentSeconds);
                movement.y = Mathf.Clamp(desiredHeight - risen, 0, maximumSpeed * dt);
            }
            chargedElapsed += dt;
            remaining = chargedRise.ascentSeconds - chargedElapsed;
        }
        m_CharacterLocomotion.LocalDesiredMovement = movement;
        if (scriptedRise && chargedRise == null) remaining = Mathf.Max(0, remaining - dt);
    }
}
