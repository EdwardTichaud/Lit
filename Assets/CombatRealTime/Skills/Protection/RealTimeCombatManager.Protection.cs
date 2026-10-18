using Unity.Netcode;
using UnityEngine;

public sealed partial class RealTimeCombatManager
{
    private bool PrepareProtectionSkill(SkillSO skill, PlayerAttackContext targeting = null)
    {
        ResolvePlayerReferences();
        if (IsCinematicSequenceActive || !SkillHealing.CanCast(playerController) || GetSkillCooldownRemaining(skill) > 0) return false;
        if (skill.protection.kind == SkillProtectionKind.JusteRetour)
            return (targeting != null ? targeting.TargetAlive : HasLockedCombatTarget) && Mathf.RoundToInt(PlayerProtectionEffects.Get(playerController).Reserve) > 0;
        return true;
    }
    private bool TryUseProtectionSkill(SkillSO skill, PlayerAttackContext targeting = null)
    {
        if (!PrepareProtectionSkill(skill, targeting)) return false;
        var ally = targeting != null ? targeting.Ally : supportTarget != null ? supportTarget : playerController;
        var enemy = targeting != null ? targeting.Target : LockedCombatTarget;
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            var relay = playerController.GetComponentInParent<NetworkCharacterInput>();
            return relay != null && relay.TryRequestProtectionSkill(skill, ally, enemy);
        }
        if (skill.protection.kind == SkillProtectionKind.JusteRetour)
        {
            var strike = playerController.GetComponent<PlayerJusticeStrike>();
            if (strike == null) strike = playerController.gameObject.AddComponent<PlayerJusticeStrike>();
            if (!strike.TryStart(skill, enemy, targeting: targeting)) return false;
        }
        else if (!SkillProtection.Activate(skill, playerController, ally)) return false;
        ConfirmHealingSkill(skill, playerRoot, skill.cooldownSeconds);
        return true;
    }
}
