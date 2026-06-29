using UnityEngine;

public static class ItemEffectExecutor
{
    public static void ApplyEffect(ItemData item, CharacterUnit caster, CharacterUnit target)
    {
        ApplyEffect(item, caster, target, false);
    }

    public static void ApplyEffect(ItemData item, CharacterUnit caster, CharacterUnit target, bool isCritical)
    {
        if (item == null)
            return;

        var effects = item.GetEffects();
        foreach (var effect in effects)
        {
            if (effect == null)
                continue;

            ApplySingleEffect(item, effect.type, caster, target, effect.value);
        }

        if (isCritical && item.useCriticalVariant)
        {
            ApplySingleEffect(item, item.criticalEffectType, caster, target, item.criticalEffectValue);
        }
    }

    private static void ApplySingleEffect(ItemData item, ItemEffectType type, CharacterUnit caster, CharacterUnit target,
        float value = 0f)
    {
        switch (type)
        {
            case ItemEffectType.Heal:
                ApplyHeal(item, target);
                break;
            case ItemEffectType.Revive:
                ApplyRevive(item, target);
                break;
            case ItemEffectType.Buff:
                ApplyBuff(item, target);
                break;
            case ItemEffectType.Debuff:
                ApplyDebuff(item, target);
                break;
            case ItemEffectType.BoostTiming:
                Debug.Log("[ItemData] Effet BoostTiming non implemente.");
                break;
            case ItemEffectType.Damage:
                ApplyDamage(item, caster, target, value);
                break;
            case ItemEffectType.IncreaseRange:
                ApplyIncreaseRange(target, value);
                break;
            case ItemEffectType.PreventInterception:
                ApplyInterceptionImmunity(item, target);
                break;
            case ItemEffectType.ExtendEffects:
                ApplyExtendEffects(item, target);
                break;
            case ItemEffectType.Sleep:
                ApplySleep(target, value);
                break;
            case ItemEffectType.Stun:
                ApplyStun(target, value);
                break;
            case ItemEffectType.WakeUp:
                ApplyWakeUp(target);
                break;
            default:
                Debug.LogWarning($"[ItemData] Type d'effet inconnu : {item.effectType}");
                break;
        }
    }

    private static void ApplyHeal(ItemData item, CharacterUnit target)
    {
        if (target == null)
            return;

        float amount = item.healIsPercentage
            ? (target.Data.baseHP + target.currentVitality) * item.healAmount / 100f
            : item.healAmount;

        CombatPipeline.ApplyHealing(null, target, amount, new CombatPipeline.HealOptions
        {
            includePower = false,
            useSagacity = false,
            applyAttackMultiplier = false,
            applyModifiers = false,
            clampToBaseValue = false,
            valueMultiplier = 1f
        });
    }

    private static void ApplyRevive(ItemData item, CharacterUnit target)
    {
        if (target == null || target.currentHP > 0 || target.IsPermanentlyDead)
            return;

        float maxHP = target.Data.baseHP + target.currentVitality;
        float amount = maxHP * item.revivePercentage / 100f;
        target.currentHP = Mathf.Clamp(amount, 0f, maxHP);
        if (target.hpBar != null)
            target.hpBar.SetValue(target.currentHP);

        DamagePopupManager.Instance?.ShowHeal(target.transform, Mathf.RoundToInt(target.currentHP));
        target.PlayResurection();
    }

    private static void ApplyBuff(ItemData item, CharacterUnit target)
    {
        CharacterStatusEffectController.ApplyBuff(target, item.buffStat, item.buffAmount, item.buffDuration,
            item.buffIsPercentage);
    }

    private static void ApplyDebuff(ItemData item, CharacterUnit target)
    {
        CharacterStatusEffectController.ApplyDebuff(target, item.debuffStat, item.debuffAmount, item.debuffDuration,
            item.debuffIsPercentage);
    }

    private static void ApplyDamage(ItemData item, CharacterUnit caster, CharacterUnit target, float value)
    {
        if (target == null)
            return;

        CombatPipeline.ApplyDamage(caster, target, value, new CombatPipeline.DamageOptions
        {
            includePower = false,
            applyAttackMultiplier = caster != null,
            applyModifiers = false,
            clampToBaseValue = false,
            registerDamage = false,
            allowRedirect = true,
            valueMultiplier = 1f
        });
    }

    private static void ApplyIncreaseRange(CharacterUnit target, float value)
    {
        if (target != null)
            target.currentRange += value;
    }

    private static void ApplyInterceptionImmunity(ItemData item, CharacterUnit target)
    {
        CharacterStatusEffectController.ApplyInterceptionImmunity(target, Mathf.RoundToInt(item.buffDuration));
    }

    private static void ApplyExtendEffects(ItemData item, CharacterUnit target)
    {
        CharacterStatusEffectController.ExtendEffectDurations(target, item.buffDuration);
    }

    private static void ApplySleep(CharacterUnit target, float value)
    {
        CharacterStatusEffectController.ApplySleep(target, Mathf.Max(1, Mathf.RoundToInt(value)));
    }

    private static void ApplyStun(CharacterUnit target, float value)
    {
        CharacterStatusEffectController.ApplyStun(target, Mathf.Max(1, Mathf.RoundToInt(value)));
    }

    private static void ApplyWakeUp(CharacterUnit target)
    {
        CharacterStatusEffectController.RemoveSleep(target);
    }
}
