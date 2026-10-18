using UnityEngine;

public sealed partial class EnemyController : ICombatHealthThresholdParticipantActor
{
    public bool TryStartThresholdFailureRetaliation(SkillSO skill, SquadCharacterController participant)
    {
        if (participant == null || !participant.isActiveAndEnabled || participant.CurrentHp <= 0 ||
            !CombatProtectionDamage.HasAuthority) return false;
        var previous = BrainTarget;
        BrainTarget = participant;
        if (TryStartThresholdFailureRetaliation(skill)) return true;
        BrainTarget = previous;
        return false;
    }
}

public partial class LitBrainsEnemy : ICombatHealthThresholdParticipantActor
{
    public bool TryStartThresholdFailureRetaliation(SkillSO skill, SquadCharacterController participant)
    {
        if (participant == null || !participant.isActiveAndEnabled || participant.CurrentHp <= 0 ||
            !HasSimulationAuthority || fov == null || !IsLivingTarget(participant.transform)) return false;
        var previous = fov.CurrentTarget;
        fov.SetExternalTarget(participant.transform);
        if (TryStartThresholdFailureRetaliation(skill)) return true;
        fov.SetExternalTarget(previous);
        return false;
    }
}
