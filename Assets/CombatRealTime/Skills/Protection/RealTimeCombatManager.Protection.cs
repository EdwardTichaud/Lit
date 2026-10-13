using Unity.Netcode;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private bool PrepareProtectionSkill(SkillSO skill)
    {
        ResolvePlayerReferences();
        if (IsCinematicSequenceActive || !SkillHealing.CanCast(playerController) || GetSkillCooldownRemaining(skill) > 0) return false;
        if (skill.protection.kind == SkillProtectionKind.JusteRetour)
            return HasLockedCombatTarget && Mathf.RoundToInt(PlayerProtectionEffects.Get(playerController).Reserve) > 0;
        return true;
    }
    private bool TryUseProtectionSkill(SkillSO skill)
    {
        if (!PrepareProtectionSkill(skill)) return false;
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            var relay = playerController.GetComponentInParent<NetworkCharacterInput>();
            return relay != null && relay.TryRequestProtectionSkill(skill, supportTarget != null ? supportTarget : playerController, LockedCombatTarget);
        }
        if (skill.protection.kind == SkillProtectionKind.JusteRetour)
        {
            var strike = playerController.GetComponent<PlayerJusticeStrike>();
            if (strike == null) strike = playerController.gameObject.AddComponent<PlayerJusticeStrike>();
            if (!strike.TryStart(skill, LockedCombatTarget)) return false;
        }
        else if (!SkillProtection.Activate(skill, playerController, supportTarget != null ? supportTarget : playerController)) return false;
        ConfirmHealingSkill(skill, playerRoot, skill.cooldownSeconds);
        return true;
    }
}
