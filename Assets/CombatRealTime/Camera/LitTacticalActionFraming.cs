using System;
using UnityEngine;

[Serializable]
public sealed class LitTacticalActionFraming
{
    public bool enabled;
    [Range(.6f, 1)] public float basicDistanceRatio = .88f;
    [Range(.6f, 1)] public float skillDistanceRatio = .78f;
    [Range(.6f, 1)] public float enemyDistanceRatio = .92f;
    [Min(.01f)] public float entrySeconds = .25f;
    [Min(.01f)] public float returnSeconds = .6f;
    [Min(0)] public float manualZoomGraceSeconds = 1.5f;
    [Min(0)] public float maximumParticipantDistance = 6;

    public static float RequiredDistance(Vector3 cameraLocalPoint, float verticalFov, float aspect)
    {
        float tangent = Mathf.Tan(Mathf.Clamp(verticalFov, 20, 100) * .5f * Mathf.Deg2Rad) * .8f;
        return Mathf.Max(.1f - cameraLocalPoint.z, Mathf.Abs(cameraLocalPoint.y) / tangent - cameraLocalPoint.z,
            Mathf.Abs(cameraLocalPoint.x) / (tangent * Mathf.Max(.1f, aspect)) - cameraLocalPoint.z);
    }
}

public sealed partial class LitGameplayCameraModeController
{
    private GameObject actionFramingActor;
    private PlayerActionPresentationController actionFramingPresentation;

    public float ResolveActionFraming(out Transform participant)
    {
        participant = null;
        if (profile?.actionFraming == null || !profile.actionFraming.enabled || inspection || external ||
            ucc == null || ucc.Character == null || effectiveMode != GameplayCameraMode.Tactical) return 1;
        if (actionFramingActor != ucc.Character)
        {
            actionFramingActor = ucc.Character;
            actionFramingPresentation = actionFramingActor.GetComponent<PlayerActionPresentationController>();
        }
        var action = actionFramingPresentation;
        var manager = RealTimeCombatManager.Instance;
        if (action == null || action.IsChargingSkill || manager == null || manager.PlayerRoot != actionFramingActor.transform ||
            manager.IsCinematicSequenceActive) return 1;
        var context = action.AttackContext;
        float ratio = 1;
        if (action.IsActionActive && context?.Skill != null)
        {
            ratio = context.Skill is BasicSkillsSO ? profile.actionFraming.basicDistanceRatio : profile.actionFraming.skillDistanceRatio;
            if (context.TargetAlive) participant = context.TargetRoot;
        }
        var threat = manager.CombatTarget;
        if (threat != null && !threat.IsDead && threat.Root != null &&
            Vector3.Distance(threat.Root.position, actionFramingActor.transform.position) <= profile.actionFraming.maximumParticipantDistance)
        {
            var brain = threat.Root.GetComponent<LitBrainsEnemy>();
            if (brain != null && brain.CommittedAttackTarget != null &&
                brain.CommittedAttackTarget.IsChildOf(actionFramingActor.transform) &&
                (brain.AttackPhase == LitBrainsAttackPhase.Preparation || brain.AttackPhase == LitBrainsAttackPhase.Strike))
            {
                ratio = Mathf.Min(ratio, profile.actionFraming.enemyDistanceRatio);
                if (participant == null) participant = threat.Root;
            }
        }
        return Mathf.Clamp(ratio, .6f, 1);
    }
}
