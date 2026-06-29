using UnityEngine;

public static class MusicalMoveExecutor
{
    public static void ApplyEffect(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target)
    {
        ApplyEffect(move, caster, target, false, ignoreFatigue: false, skipDamageRegistration: false);
    }

    public static void ApplyEffect(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target, bool isCritical)
    {
        ApplyEffect(move, caster, target, isCritical, ignoreFatigue: false, skipDamageRegistration: false);
    }

    public static void ApplyEffect(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target, bool isCritical,
        bool ignoreFatigue, bool skipDamageRegistration)
    {
        if (move == null)
            return;

        var effects = move.GetEffects();
        bool shouldDoubleBaseEffects = isCritical && !move.useCriticalVariant;
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect == null)
                continue;

            bool applyFatigue = i == 0;
            ApplySingleEffect(move, effect.type, caster, target, effect.value, move.fatigueCost,
                shouldDoubleBaseEffects, ignoreFatigue, skipDamageRegistration, applyFatigue);
        }

        if (isCritical && move.useCriticalVariant)
        {
            ApplySingleEffect(move, move.criticalEffectType, caster, target, move.criticalEffectValue,
                move.criticalFatigueCost, false, ignoreFatigue, skipDamageRegistration, true);
        }
    }

    private static void ApplySingleEffect(MusicalMoveSO move, MusicalEffectType typeToUse, CharacterUnit caster,
        CharacterUnit target, int baseValue, float fatigueToApply, bool doubleValue, bool ignoreFatigue,
        bool skipDamageRegistration, bool applyFatigue)
    {
        if (typeToUse == MusicalEffectType.Damage)
        {
            float multiplier = doubleValue ? 2f : 1f;
            CombatPipeline.ApplyDamage(caster, target, baseValue, new CombatPipeline.DamageOptions
            {
                includePower = true,
                applyAttackMultiplier = true,
                applyModifiers = true,
                clampToBaseValue = true,
                registerDamage = !skipDamageRegistration,
                allowRedirect = true,
                valueMultiplier = multiplier
            });
        }
        else if (typeToUse == MusicalEffectType.Heal)
        {
            float multiplier = doubleValue ? 2f : 1f;
            CombatPipeline.ApplyHealing(caster, target, baseValue, new CombatPipeline.HealOptions
            {
                includePower = true,
                useSagacity = true,
                applyAttackMultiplier = true,
                applyModifiers = true,
                clampToBaseValue = true,
                valueMultiplier = multiplier
            });
        }
        else if (typeToUse == MusicalEffectType.Sleep)
        {
            CharacterStatusEffectController.ApplySleep(target, Mathf.Max(1, baseValue));
        }
        else if (typeToUse == MusicalEffectType.WakeUpAll)
        {
            foreach (var unit in NewBattleManager.Instance.activeCharacterUnits)
            {
                CharacterStatusEffectController.RemoveSleep(unit);
            }
        }
        else if (typeToUse == MusicalEffectType.LoyaltyMark)
        {
            var mark = target.GetComponent<LoyaltyMark>();
            if (mark == null)
                mark = target.gameObject.AddComponent<LoyaltyMark>();
            mark.SetProtector(caster, move.passiveEffectPrefab, move.passiveEffectVerticalOffset,
                Mathf.Max(1, baseValue));
        }
        else if (typeToUse == MusicalEffectType.LinkMark)
        {
            var mark = target.GetComponent<LinkMark>();
            if (mark == null)
                mark = target.gameObject.AddComponent<LinkMark>();
            mark.ApplyDuration(Mathf.Max(1, baseValue));
        }
        else if (typeToUse == MusicalEffectType.AnchorGround)
        {
            int turns = Mathf.Max(1, baseValue);
            target.EnsureAltitudeOverrideStatus().AnchorToGround(turns);
        }
        else if (typeToUse == MusicalEffectType.SuspendAir)
        {
            int turns = Mathf.Max(1, baseValue);
            target.EnsureAltitudeOverrideStatus().SuspendInAir(turns);
        }
        else if (typeToUse == MusicalEffectType.PrestoForcedAttack)
        {
            int turns = Mathf.Max(1, baseValue);
            PrestoForcedAttackSystem.ApplyStatus(target, caster, move.passiveEffectPrefab,
                move.passiveEffectVerticalOffset, turns);
        }
        else if (typeToUse == MusicalEffectType.Stun)
        {
            CharacterStatusEffectController.ApplyStun(target, Mathf.Max(1, baseValue));
        }
        else if (typeToUse == MusicalEffectType.FillATB)
        {
            if (target != null)
            {
                target.currentATB = target.ATBMax;
                NewBattleManager.Instance?.ForceNextUnit(target);
            }
        }

        if (applyFatigue && !ignoreFatigue && caster != null && caster.Data.gameplayType == GameplayType.Fatigue)
        {
            caster.GetComponent<FatigueSystem>()?.OnActionPerformed(fatigueToApply);
        }

        if (typeToUse == MusicalEffectType.IncreaseDamage)
        {
            CharacterStatusEffectController.ApplyBuff(target, BuffStatType.Strength, baseValue, move.buffDuration,
                move.buffIsPercentage);
        }
        else if (typeToUse == MusicalEffectType.DecreaseDamage)
        {
            CharacterStatusEffectController.ApplyDebuff(target, DebuffStatType.Strength, baseValue, move.debuffDuration,
                move.debuffIsPercentage);
        }
        else if (typeToUse == MusicalEffectType.IncreaseDefense)
        {
            CharacterStatusEffectController.ApplyBuff(target, BuffStatType.Defense, baseValue, move.buffDuration,
                move.buffIsPercentage);
        }
        else if (typeToUse == MusicalEffectType.DecreaseDefense)
        {
            CharacterStatusEffectController.ApplyDebuff(target, DebuffStatType.Defense, baseValue, move.debuffDuration,
                move.debuffIsPercentage);
        }
        else if (typeToUse == MusicalEffectType.IncreaseInitiative)
        {
            CharacterStatusEffectController.ApplyBuff(target, BuffStatType.Initiative, baseValue, move.buffDuration,
                move.buffIsPercentage);
        }
        else if (typeToUse == MusicalEffectType.DecreaseInitiative)
        {
            CharacterStatusEffectController.ApplyDebuff(target, DebuffStatType.Initiative, baseValue, move.debuffDuration,
                move.debuffIsPercentage);
        }
        else if (typeToUse == MusicalEffectType.IncreaseMaxHP)
        {
            CharacterStatusEffectController.ApplyBuff(target, BuffStatType.MaxHP, baseValue, move.buffDuration,
                move.buffIsPercentage);
        }
        else if (typeToUse == MusicalEffectType.DecreaseMaxHP)
        {
            CharacterStatusEffectController.ApplyDebuff(target, DebuffStatType.MaxHP, baseValue, move.debuffDuration,
                move.debuffIsPercentage);
        }

        if (move.buffStat != BuffStatType.None)
        {
            CharacterStatusEffectController.ApplyBuff(target, move.buffStat, move.buffAmount, move.buffDuration,
                move.buffIsPercentage);
        }

        if (move.debuffStat != DebuffStatType.None)
        {
            CharacterStatusEffectController.ApplyDebuff(target, move.debuffStat, move.debuffAmount,
                move.debuffDuration, move.debuffIsPercentage);
        }
    }
}
