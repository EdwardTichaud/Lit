using UnityEngine;

/// <summary>
/// Pipeline unique de résolution des dégâts et soins.
/// Centralise les règles pour éviter les divergences entre MusicalMoves, Items et IA.
/// </summary>
public static class CombatPipeline
{
    public struct DamageOptions
    {
        public bool includePower;
        public bool applyAttackMultiplier;
        public bool applyModifiers;
        public bool clampToBaseValue;
        public bool registerDamage;
        public bool allowRedirect;
        public float valueMultiplier;
    }

    public struct HealOptions
    {
        public bool includePower;
        public bool useSagacity;
        public bool applyAttackMultiplier;
        public bool applyModifiers;
        public bool clampToBaseValue;
        public float valueMultiplier;
    }

    public static float ApplyDamage(CharacterUnit caster, CharacterUnit target, float baseValue, DamageOptions options)
    {
        if (target == null)
            return 0f;

        float finalValue = ResolveDamageValue(caster, baseValue, options);
        target.TakeDamage(finalValue, caster != null ? caster.transform : null, options.allowRedirect);

        if (options.registerDamage)
            NewBattleManager.Instance?.RegisterDamage(caster, finalValue);

        return finalValue;
    }

    public static float ApplyHealing(CharacterUnit caster, CharacterUnit target, float baseValue, HealOptions options)
    {
        if (target == null)
            return 0f;

        float finalValue = ResolveHealingValue(caster, baseValue, options);
        target.Heal(finalValue);
        return finalValue;
    }

    public static float ResolveDamageValue(CharacterUnit caster, float baseValue, DamageOptions options)
    {
        float value = baseValue;

        if (caster != null && options.includePower)
            value += caster.currentPower;

        if (caster != null && options.applyAttackMultiplier)
            value *= caster.GetAttackMultiplier();

        if (caster != null && options.applyModifiers)
            value = caster.ApplyDamageModifiers(value);

        if (!Mathf.Approximately(options.valueMultiplier, 1f))
            value *= options.valueMultiplier;

        if (options.clampToBaseValue)
            value = Mathf.Max(baseValue, value);

        return Mathf.Max(0f, value);
    }

    public static float ResolveHealingValue(CharacterUnit caster, float baseValue, HealOptions options)
    {
        float value = baseValue;

        if (caster != null && options.includePower)
        {
            float power = options.useSagacity
                ? Mathf.Max(caster.currentSagacity, caster.currentPower)
                : caster.currentPower;
            value += power;
        }

        if (caster != null && options.applyAttackMultiplier)
            value *= caster.GetAttackMultiplier();

        if (caster != null && options.applyModifiers)
            value = caster.ApplyHealingModifiers(value);

        if (!Mathf.Approximately(options.valueMultiplier, 1f))
            value *= options.valueMultiplier;

        if (options.clampToBaseValue)
            value = Mathf.Max(baseValue, value);

        return Mathf.Max(0f, value);
    }
}
