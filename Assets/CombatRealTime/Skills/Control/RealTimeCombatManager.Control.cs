using Unity.Netcode;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private bool TryUseControlSkill(SkillSO skill, PlayerAttackContext targeting = null)
    {
        ResolvePlayerReferences();
        if (IsCinematicSequenceActive || !SkillHealing.CanCast(playerController) || GetSkillCooldownRemaining(skill) > 0) return false;
        var effects = PlayerControlSkills.Get(playerController);
        var target = targeting != null ? targeting.Target : LockedCombatTarget;
        Vector3 direction = targeting != null ? targeting.Direction : effects.ResolveDirection(target?.Root);
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            var relay = playerController.GetComponentInParent<NetworkCharacterInput>();
            return relay != null && relay.TryRequestControlSkill(skill, target, direction);
        }
        if (!effects.TryActivate(skill, target, direction, targeting?.ZoneOrigin)) return false;
        ConfirmHealingSkill(skill, playerRoot, skill.cooldownSeconds); return true;
    }
}
