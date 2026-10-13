using Unity.Netcode;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private bool TryUseControlSkill(SkillSO skill)
    {
        ResolvePlayerReferences();
        if (IsCinematicSequenceActive || !SkillHealing.CanCast(playerController) || GetSkillCooldownRemaining(skill) > 0) return false;
        var effects = PlayerControlSkills.Get(playerController);
        Vector3 direction = effects.ResolveDirection(LockedCombatTarget?.Root);
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            var relay = playerController.GetComponentInParent<NetworkCharacterInput>();
            return relay != null && relay.TryRequestControlSkill(skill, LockedCombatTarget, direction);
        }
        if (!effects.TryActivate(skill, LockedCombatTarget, direction)) return false;
        ConfirmHealingSkill(skill, playerRoot, skill.cooldownSeconds); return true;
    }
}
