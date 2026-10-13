using UnityEngine;

public static class SkillProtection
{
    public static bool Activate(SkillSO skill, SquadCharacterController caster, SquadCharacterController ally)
    {
        if (!CombatProtectionDamage.HasAuthority || skill == null || !skill.IsProtectionSkill || !SkillHealing.CanCast(caster)) return false;
        var effects = PlayerProtectionEffects.Get(caster);
        switch (skill.protection.kind)
        {
            case SkillProtectionKind.Intercession: return effects.BeginLink(skill, ally);
            case SkillProtectionKind.Rempart: return effects.ToggleAura(skill);
            case SkillProtectionKind.Sommation:
                int count = 0;
                foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude))
                    if (enemy.TryReceiveTaunt(caster, skill.protection.durationSeconds, skill.protection.radius)) count++;
                foreach (var enemy in Object.FindObjectsByType<LitBrainsEnemy>(FindObjectsInactive.Exclude))
                    if (enemy.TryReceiveTaunt(caster, skill.protection.durationSeconds, skill.protection.radius)) count++;
                return count > 0;
            default: return false;
        }
    }
}
