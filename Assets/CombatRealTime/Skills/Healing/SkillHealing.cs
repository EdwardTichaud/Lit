using System.Collections.Generic;
using UnityEngine;

public enum SkillHealingTarget { None, Self, PlayersInRadius, AllyRegeneration, EnemyLifeSteal }

public static class SkillHealing
{
    public static int CalculateAmount(int current, int maximum, float percent)
    {
        if (current <= 0 || maximum <= current || percent <= 0f || float.IsNaN(percent)) return 0;
        return Mathf.Min(maximum - current, Mathf.CeilToInt(maximum * Mathf.Clamp(percent, 0f, 100f) / 100f));
    }

    public static bool CanCast(SquadCharacterController caster)
    {
        if (Time.timeScale <= 0f || caster == null || !caster.isActiveAndEnabled || caster.CurrentHp <= 0 ||
            caster.CharacterData != null && caster.CharacterData.isEnemy) return false;
        var presentation = caster.GetComponent<PlayerActionPresentationController>();
        var bridge = caster.GetComponent<LitOpsiveLocomotionBridge>();
        return (presentation == null || !presentation.IsActionActive) &&
            (bridge == null || !bridge.IsExternalLockActive);
    }

    public static List<SquadCharacterController> Apply(SkillSO skill, SquadCharacterController caster)
    {
        var healed = new List<SquadCharacterController>();
        if (skill == null || !skill.IsHealingSkill || !CanCast(caster)) return healed;
        if (skill.healingTarget == SkillHealingTarget.Self) Heal(caster, skill, healed);
        else
        {
            float radius = Mathf.Max(0, skill.healingRadius);
            foreach (var candidate in Object.FindObjectsByType<SquadCharacterController>(FindObjectsInactive.Exclude))
            {
                if (!candidate.isActiveAndEnabled || candidate.CharacterData == null || candidate.CharacterData.isEnemy ||
                    (candidate.transform.position - caster.transform.position).sqrMagnitude > radius * radius) continue;
                Heal(candidate, skill, healed);
            }
        }
        return healed;
    }

    private static void Heal(SquadCharacterController target, SkillSO skill, List<SquadCharacterController> healed)
    {
        int amount = CalculateAmount(target.CurrentHp, target.MaxHp, skill.healingPercent);
        if (amount <= 0) return;
        int before = target.CurrentHp;
        target.SetCurrentHp(before + amount);
        if (target.CurrentHp > before) healed.Add(target);
    }
}
