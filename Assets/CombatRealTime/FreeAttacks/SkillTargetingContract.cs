using UnityEngine;

/// <summary>Checks the explicit contract against the existing executor, never infers a runtime mode.</summary>
public static class SkillTargetingContract
{
    public static bool Validate(SkillSO skill, out string reason)
    {
        reason = null;
        if (skill == null) { reason = "Competence absente."; return false; }
        var mode = skill.targetingMode;
        if (mode == SkillTargetingMode.Unconfigured || !System.Enum.IsDefined(typeof(SkillTargetingMode), mode))
        { reason = "Contrat de ciblage non configure : " + skill.name; return false; }
        int executors = (skill.IsHealingSkill ? 1 : 0) + (skill.IsControlSkill ? 1 : 0) +
            (skill.IsProtectionSkill ? 1 : 0) + (skill.IsGroundImpactSkill ? 1 : 0) + (skill.HasCombatCinematic ? 1 : 0);
        bool valid = executors <= 1;
        if (skill.IsHealingSkill)
            valid &= skill.healingTarget switch {
                SkillHealingTarget.Self => mode == SkillTargetingMode.Self,
                SkillHealingTarget.PlayersInRadius => mode == SkillTargetingMode.AllyArea,
                SkillHealingTarget.AllyRegeneration => mode == SkillTargetingMode.SelectedAlly,
                SkillHealingTarget.EnemyLifeSteal => mode == SkillTargetingMode.RequiredTarget,
                _ => false };
        else if (skill.IsProtectionSkill)
            valid &= skill.protection.kind switch {
                SkillProtectionKind.Intercession => mode == SkillTargetingMode.SelectedAlly,
                SkillProtectionKind.Rempart => mode == SkillTargetingMode.AllyArea,
                SkillProtectionKind.Sommation => mode == SkillTargetingMode.CasterArea,
                SkillProtectionKind.JusteRetour => mode == SkillTargetingMode.RequiredTarget,
                _ => false };
        else if (skill.IsControlSkill)
            valid &= mode == (skill.control.kind == SkillControlKind.PasDeRecul ? SkillTargetingMode.FreeAttack : SkillTargetingMode.RequiredTarget);
        else if (skill.IsGroundImpactSkill) valid &= mode == SkillTargetingMode.CasterArea;
        else if (skill.HasCombatCinematic) valid &= mode == SkillTargetingMode.RequiredTarget;
        else if (skill is BasicSkillsSO || skill is ChargedSkillSO) valid &= mode == SkillTargetingMode.FreeAttack;
        else valid &= mode == SkillTargetingMode.FreeAttack || mode == SkillTargetingMode.OptionalTarget ||
            mode == SkillTargetingMode.RequiredTarget || mode == SkillTargetingMode.LockedTarget;
        if (mode == SkillTargetingMode.OptionalTarget)
            valid &= skill.TargetLunge.enabled && skill.TargetLunge.resolveImpactAtContact;
        if (!valid) reason = "Contrat de ciblage incompatible avec l'executeur : " + skill.name;
        return valid;
    }

    public static void EnemyRange(SkillSO skill, out float minimum, out float maximum)
    {
        minimum = skill.useLaunchRange ? skill.minimumLaunchDistance : skill.MinimumHitDistance;
        maximum = skill.useLaunchRange ? skill.maximumLaunchDistance : skill.MaximumHitDistance;
        if (skill.IsControlSkill) { minimum = 0; maximum = skill.control.range; }
        else if (skill.IsHealingSkill && skill.healingTarget == SkillHealingTarget.EnemyLifeSteal)
        { minimum = 0; maximum = skill.healingRadius; }
    }

    public static bool Exists(ICombatTarget target) => target != null &&
        (!(target is Object obj) || obj != null) && target.Root != null;

    public static bool Alive(ICombatTarget target) => Exists(target) && target.Root.gameObject.activeInHierarchy && !target.IsDead;
}
