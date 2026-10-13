using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private struct SkillContactSample
    {
        public Vector3 caster, target, facing;
        public float Distance => Vector3.ProjectOnPlane(target - caster, Vector3.up).magnitude;
    }
    private SkillContactSample? currentSkillContact;

    /// <summary>A swept, actually travelled contact uses the normal damage/defence pipeline.</summary>
    public int ApplySkillDamageAtContact(SkillSO skill, Transform contactedRoot, Vector3 casterPosition, Vector3 facing)
    {
        var target = LockedCombatTarget;
        if (skill == null || IsPlayerDead() || !skill.TargetLunge.resolveImpactAtContact || target == null || target.IsDead ||
            target.Root != contactedRoot || playerRoot == null || IsCinematicSequenceActive) return 0;
        var sample = new SkillContactSample { caster = casterPosition, target = contactedRoot.position, facing = facing };
        if (Mathf.Abs(sample.caster.y - sample.target.y) > 1.8f ||
            !skill.IsWithinHitRange(sample.Distance) ||
            Vector3.Angle(facing, Vector3.ProjectOnPlane(sample.target - sample.caster, Vector3.up)) > skill.enemyImpact.arcDegrees * .5f ||
            Physics.Linecast(sample.caster + Vector3.up, sample.target + Vector3.up,
                LayerMask.GetMask("Default", "Ground", "Obstacle", "CameraObstruction"), QueryTriggerInteraction.Ignore)) return 0;
        var previous = currentSkillContact;
        currentSkillContact = sample;
        try { return ApplySkillDamageToLockedEnemy(skill); }
        finally { currentSkillContact = previous; }
    }
}
