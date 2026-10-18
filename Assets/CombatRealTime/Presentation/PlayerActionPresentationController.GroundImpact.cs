using System.Collections;
using UnityEngine;

public sealed partial class PlayerActionPresentationController
{
    private bool groundImpactResolved;
    private System.Action<SkillSO, bool, Vector3> externalGroundImpact;

    public bool BeginExternalGroundImpact(int generation, SkillSO skill, System.Action<SkillSO, bool, Vector3> contact)
    {
        if (!externallyDriven || !IsCurrentSession(generation) || activePresentedSkill != skill || contact == null ||
            !TryGroundImpactFloor(skill, out _) || !locomotionBridge.BeginScriptedPlanarMotion(this)) return false;
        groundImpactResolved = false;
        externalGroundImpact = contact;
        bool airborne = !locomotionBridge.Grounded || locomotionBridge.VerticalVelocity > .1f;
        RegisterActionCleanup(generation, () =>
        {
            externalGroundImpact = null;
            locomotionBridge.DriveScriptedPlanarMotion(this, Vector3.zero);
            locomotionBridge.EndScriptedPlanarMotion(this);
        });
        StartCoroutine(GroundImpactRoutine(skill, generation, airborne));
        return true;
    }
    public bool ConsumeGroundImpact(SkillSO skill)
    {
        if (!actionActive || groundImpactResolved || activePresentedSkill != skill) return false;
        groundImpactResolved = true;
        return true;
    }
    public bool CanStartGroundImpact(SkillSO skill, bool allowExternalPredecessor = false)
    {
        return skill != null && skill.IsGroundImpactSkill && CanPlayState(skill.AnimatorState) &&
            locomotionBridge != null && locomotionBridge.IsDriving && !deathAnimationLocked &&
            !locomotionBridge.IsScriptedTraversalActive && !locomotionBridge.IsCinematicMotionSessionActive &&
            (!actionActive || (!externallyDriven || allowExternalPredecessor) && skill.groundImpact.interruptibleSkill != null &&
                activePresentedSkill == skill.groundImpact.interruptibleSkill) &&
            locomotionBridge.HasOnlyExternalLocksOwnedBy(this, GetComponent<PlayerStateMotionController>()) &&
            TryGroundImpactFloor(skill, out _);
    }

    private bool TryGroundImpactFloor(SkillSO skill, out RaycastHit floor)
    {
        floor = default;
        float best = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * .35f, Vector3.down,
            skill.groundImpact.descentSpeed * skill.groundImpact.maximumDescentSeconds,
            skill.groundImpact.groundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<SquadCharacterController>() != null ||
                hit.collider.GetComponentInParent<EnemyController>() != null || hit.collider.GetComponentInParent<LitBrainsEnemy>() != null ||
                hit.normal.y < .5f || hit.distance >= best) continue;
            floor = hit; best = hit.distance;
        }
        return best < float.MaxValue;
    }

    public bool StartGroundImpact(SkillSO skill)
    {
        if (!CanStartGroundImpact(skill)) return false;
        bool airborne = !locomotionBridge.Grounded || locomotionBridge.VerticalVelocity > .1f;
        // Only the explicitly configured predecessor may be replaced, after full validation.
        if (actionActive) TerminateAction(activeToken, ActionEndReason.Interrupted, false);
        locomotionBridge.CancelActionImpulse(this);
        if (!TryPlaySkill(skill, Animator.StringToHash(skill.AnimatorState))) return false;
        groundImpactResolved = false;
        int generation = activeToken;
        if (!locomotionBridge.BeginScriptedPlanarMotion(this))
        { TerminateAction(generation, ActionEndReason.Failed); return false; }
        RegisterActionCleanup(generation, () =>
        {
            locomotionBridge.DriveScriptedPlanarMotion(this, Vector3.zero);
            locomotionBridge.EndScriptedPlanarMotion(this);
        });
        StartCoroutine(GroundImpactRoutine(skill, generation, airborne));
        return true;
    }

    private IEnumerator GroundImpactRoutine(SkillSO skill, int generation, bool airborne)
    {
        float elapsed = 0;
        var clock = GetComponent<CombatTimeDomain>();
        while (IsCurrentSession(generation) && elapsed < skill.groundImpact.maximumDescentSeconds)
        {
            locomotionBridge.DriveScriptedPlanarMotion(this, Vector3.zero);
            if (locomotionBridge.Grounded && elapsed >= .12f)
            {
                if (TryGroundImpactFloor(skill, out var floor) && floor.distance <= .65f)
                {
                    if (externalGroundImpact != null) externalGroundImpact(skill, airborne, floor.point);
                    else RealTimeCombatManager.Instance?.ResolveGroundImpact(skill, airborne, floor.point);
                }
                yield break;
            }
            if (airborne) locomotionBridge.DriveCombatDescent(skill.groundImpact.descentSpeed);
            yield return new WaitForFixedUpdate();
            elapsed += clock != null ? clock.FixedDeltaTime : Time.fixedDeltaTime;
        }
        if (IsCurrentSession(generation)) TerminateAction(generation, ActionEndReason.Failed);
    }
}
