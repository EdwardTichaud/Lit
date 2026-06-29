using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Centralise la logique d'IA des ennemis et applique les règles tactiques classiques
/// (priorité à la survie, buff si un lethal devient possible, focus sur une cible fragile...).
/// Cette version étend la logique précédente en prenant également en compte les items et
/// les actions multi-cibles.
/// </summary>
public static class BattleAIStrategy
{
    public readonly struct BattleAIDecision
    {
        private BattleAIDecision(MusicalMoveSO move, ItemData item, CharacterUnit target)
        {
            Move = move;
            Item = item;
            Target = target;
        }

        public MusicalMoveSO Move { get; }
        public ItemData Item { get; }
        public CharacterUnit Target { get; }
        public bool UsesItem => Item != null;
        public bool IsValid => (Move != null || Item != null) && Target != null;

        public static BattleAIDecision ForMove(MusicalMoveSO move, CharacterUnit target) =>
            new BattleAIDecision(move, null, target);

        public static BattleAIDecision ForItem(ItemData item, CharacterUnit target) =>
            new BattleAIDecision(null, item, target);

        public static BattleAIDecision None => new BattleAIDecision(null, null, null);
    }

    private sealed class BattleAIContext
    {
        public BattleAIContext(CharacterUnit self, IReadOnlyList<CharacterUnit> participants)
        {
            Self = self;
            Intelligence = self?.Data?.intelligenceLevel ?? EnemyIntelligenceLevel.Normal;
            Allies = new List<CharacterUnit>();
            Opponents = new List<CharacterUnit>();

            if (self != null && participants != null && self.Data != null)
            {
                foreach (var unit in participants)
                {
                    if (unit == null || unit.Data == null || unit.currentHP <= 0)
                        continue;

                    if (unit.IsAllyOf(self))
                        Allies.Add(unit);
                    else
                        Opponents.Add(unit);
                }
            }

            if (Self != null && Self.Data != null && Self.currentHP > 0 && !Allies.Contains(Self))
                Allies.Add(Self);

            AvailableMoves = BuildUsableMoves(self);
            AvailableItems = BuildUsableItems(self);
        }

        public CharacterUnit Self { get; }
        public EnemyIntelligenceLevel Intelligence { get; }
        public List<CharacterUnit> Allies { get; }
        public List<CharacterUnit> Opponents { get; }
        public List<MusicalMoveSO> AvailableMoves { get; }
        public List<ItemData> AvailableItems { get; }
        public float MaxHP => Self?.MaxHP ?? 0f;
    }

    private sealed class ScoredDecision
    {
        public BattleAIDecision Decision;
        public float Score;
    }

    public static BattleAIDecision Decide(CharacterUnit enemy, IReadOnlyList<CharacterUnit> participants)
    {
        if (enemy == null || enemy.Data == null)
            return BattleAIDecision.None;

        var context = new BattleAIContext(enemy, participants);
        BattleAIDecision decision = context.Intelligence switch
        {
            EnemyIntelligenceLevel.Beast => DecideBeast(context),
            EnemyIntelligenceLevel.Intelligent => DecideIntelligent(context),
            _ => DecideNormal(context)
        };

        if (!decision.IsValid)
            decision = BuildFallbackDecision(context);

        return decision;
    }

    private static BattleAIDecision DecideBeast(BattleAIContext context)
    {
        var lethal = TryFindLethalAction(context);
        if (lethal.IsValid)
            return lethal;

        return BuildOpportunisticAttack(context, preferRandom: true);
    }

    private static BattleAIDecision DecideNormal(BattleAIContext context)
    {
        var emergency = TryGetEmergencyAction(context);
        if (emergency.IsValid)
            return emergency;

        var lethal = TryFindLethalAction(context);
        if (lethal.IsValid)
            return lethal;

        return BuildOpportunisticAttack(context, preferRandom: false);
    }

    private static BattleAIDecision DecideIntelligent(BattleAIContext context)
    {
        var emergency = TryGetEmergencyAction(context);
        if (emergency.IsValid)
            return emergency;

        var lethal = TryFindLethalAction(context);
        if (lethal.IsValid)
            return lethal;

        var selfBuff = TryGetSelfBuffForFutureKill(context);
        if (selfBuff.IsValid)
            return selfBuff;

        var allyBuff = TryGetAllyBuffForFutureKill(context);
        if (allyBuff.IsValid)
            return allyBuff;

        return BuildOpportunisticAttack(context, preferRandom: false);
    }

    private static BattleAIDecision TryGetEmergencyAction(BattleAIContext context)
    {
        if (!ShouldPanic(context))
            return BattleAIDecision.None;

        var options = new List<ScoredDecision>();
        EvaluateSelfPreservationMoves(context, options);
        EvaluateSelfPreservationItems(context, options);
        return PickBest(options);
    }

    private static BattleAIDecision TryFindLethalAction(BattleAIContext context)
    {
        var options = new List<ScoredDecision>();
        EvaluateDamageOptions(context, options, lethalOnly: true);
        return PickBest(options);
    }

    private static BattleAIDecision BuildOpportunisticAttack(BattleAIContext context, bool preferRandom)
    {
        var options = new List<ScoredDecision>();
        EvaluateDamageOptions(context, options, lethalOnly: false, preferRandom: preferRandom);
        return PickBest(options);
    }

    private static BattleAIDecision TryGetSelfBuffForFutureKill(BattleAIContext context)
    {
        var options = new List<ScoredDecision>();
        EvaluateOffensiveBuffs(context, options, selfOnly: true);
        return PickBest(options);
    }

    private static BattleAIDecision TryGetAllyBuffForFutureKill(BattleAIContext context)
    {
        var options = new List<ScoredDecision>();
        EvaluateOffensiveBuffs(context, options, selfOnly: false);
        return PickBest(options);
    }

    private static void EvaluateSelfPreservationMoves(BattleAIContext context, List<ScoredDecision> options)
    {
        foreach (var move in context.AvailableMoves)
        {
            if (move == null)
                continue;

            if (IsHealingMove(move) && TargetsSelf(move))
            {
                float amount = EstimateHealing(move, context.Self);
                TryRegisterOption(options, BattleAIDecision.ForMove(move, context.Self), amount);
                continue;
            }

            if (IsHealingMove(move) && TargetsAllies(move, true))
            {
                var ally = FindMostInjured(context.Allies);
                if (ally != null && IsMoveApplicableToTarget(context, move, ally))
                {
                    float amount = EstimateHealing(move, ally);
                    TryRegisterOption(options, BattleAIDecision.ForMove(move, ally), amount);
                }
                continue;
            }

            if (IsDefensiveMove(move) && TargetsSelf(move))
            {
                float value = EstimateDefensiveValue(move, context.Self);
                TryRegisterOption(options, BattleAIDecision.ForMove(move, context.Self), value);
            }
        }
    }

    private static void EvaluateSelfPreservationItems(BattleAIContext context, List<ScoredDecision> options)
    {
        foreach (var item in context.AvailableItems)
        {
            if (item == null)
                continue;

            if (IsHealingItem(item))
            {
                if (TargetsSelf(item))
                {
                    float amount = EstimateItemHealing(item, context.Self);
                    TryRegisterOption(options, BattleAIDecision.ForItem(item, context.Self), amount);
                }
                else
                {
                    var ally = FindMostInjured(context.Allies);
                    if (ally != null && IsItemApplicableToTarget(context, item, ally))
                    {
                        float amount = EstimateItemHealing(item, ally);
                        TryRegisterOption(options, BattleAIDecision.ForItem(item, ally), amount);
                    }
                }
                continue;
            }

            if (IsDefensiveItem(item) && TargetsSelf(item))
            {
                float value = EstimateItemDefensiveValue(item, context.Self);
                TryRegisterOption(options, BattleAIDecision.ForItem(item, context.Self), value);
            }
        }
    }

    private static void EvaluateDamageOptions(BattleAIContext context, List<ScoredDecision> options, bool lethalOnly, bool preferRandom = false)
    {
        foreach (var move in context.AvailableMoves)
            EvaluateDamageOption(context, options, move, null, lethalOnly, preferRandom);

        foreach (var item in context.AvailableItems)
            EvaluateDamageOption(context, options, null, item, lethalOnly, preferRandom);
    }

    private static void EvaluateDamageOption(BattleAIContext context, List<ScoredDecision> options,
        MusicalMoveSO move, ItemData item, bool lethalOnly, bool preferRandom)
    {
        bool usesItem = item != null;
        if (!usesItem && (move == null || !IsDamageMove(move)))
            return;
        if (usesItem && !IsDamageItem(item))
            return;

        var candidates = context.Opponents;
        if (candidates.Count == 0)
            return;

        TargetType type = usesItem ? ResolveTargetType(item) : ResolveTargetType(move);
        bool multi = IsMultiTarget(type);

        if (multi)
        {
            var summary = BuildAreaDamageSummary(context, move, item, candidates);
            if (summary.TotalDamage <= 0f)
                return;

            bool hasKill = summary.KillCount > 0;
            if (lethalOnly && !hasKill)
                return;

            float score = hasKill ? 1000f + summary.TotalDamage : summary.TotalDamage;
            if (preferRandom)
                score += Random.Range(0f, 0.5f);

            var target = candidates.FirstOrDefault(u => u != null && u.currentHP > 0);
            if (target == null)
                return;

            var decision = usesItem
                ? BattleAIDecision.ForItem(item, target)
                : BattleAIDecision.ForMove(move, target);
            TryRegisterOption(options, decision, score);
            return;
        }

        foreach (var target in candidates)
        {
            if (target == null || target.currentHP <= 0)
                continue;

            bool valid = usesItem
                ? IsItemApplicableToTarget(context, item, target)
                : IsMoveApplicableToTarget(context, move, target);
            if (!valid)
                continue;

            float damage = usesItem
                ? EstimateItemDamage(item, context.Self, target)
                : EstimateDamage(move, context.Self, target);
            if (damage <= 0f)
                continue;

            bool kills = damage >= target.currentHP;
            if (lethalOnly && !kills)
                continue;

            float ratio = damage / Mathf.Max(1f, target.currentHP);
            float score = kills ? 1000f + damage : ratio;
            if (preferRandom)
                score += Random.Range(0f, 0.5f);

            var decision = usesItem
                ? BattleAIDecision.ForItem(item, target)
                : BattleAIDecision.ForMove(move, target);
            TryRegisterOption(options, decision, score);
        }
    }

    private static DamageSummary BuildAreaDamageSummary(BattleAIContext context, MusicalMoveSO move, ItemData item,
        IEnumerable<CharacterUnit> targets)
    {
        float total = 0f;
        int kills = 0;

        foreach (var target in targets)
        {
            if (target == null || target.currentHP <= 0)
                continue;

            float damage = move != null
                ? EstimateDamage(move, context.Self, target)
                : EstimateItemDamage(item, context.Self, target);

            if (damage <= 0f)
                continue;

            total += damage;
            if (damage >= target.currentHP)
                kills++;
        }

        return new DamageSummary(total, kills);
    }

    private static void EvaluateOffensiveBuffs(BattleAIContext context, List<ScoredDecision> options, bool selfOnly)
    {
        if (context.Self == null)
            return;

        IEnumerable<CharacterUnit> targets = selfOnly
            ? new[] { context.Self }
            : context.Allies.Where(u => u != null && u != context.Self && u.currentHP > 0);

        foreach (var target in targets)
        {
            foreach (var move in context.AvailableMoves)
            {
                if (move == null || !ProvidesOffensiveBuff(move))
                    continue;

                bool validTarget = selfOnly ? TargetsSelf(move) : TargetsAllies(move, includeSelf: false);
                if (!validTarget)
                    continue;

                if (!selfOnly && !IsMoveApplicableToTarget(context, move, target))
                    continue;

                float bonus = EstimateOffensiveBonus(move, target);
                if (bonus <= 0f)
                    continue;

                if (CanSecureFutureKill(context, target, bonus))
                    TryRegisterOption(options, BattleAIDecision.ForMove(move, target), bonus);
            }

            foreach (var item in context.AvailableItems)
            {
                if (item == null || !ProvidesOffensiveBuff(item))
                    continue;

                bool validTarget = selfOnly ? TargetsSelf(item) : TargetsAllies(item, includeSelf: false);
                if (!validTarget)
                    continue;

                if (!selfOnly && !IsItemApplicableToTarget(context, item, target))
                    continue;

                float bonus = EstimateItemOffensiveBonus(item, target);
                if (bonus <= 0f)
                    continue;

                if (CanSecureFutureKill(context, target, bonus, allyOverride: target != context.Self))
                    TryRegisterOption(options, BattleAIDecision.ForItem(item, target), bonus);
            }
        }
    }

    private static bool CanSecureFutureKill(BattleAIContext context, CharacterUnit potentialCaster, float bonus, bool allyOverride = false)
    {
        if (potentialCaster == null)
            return false;

        var opponents = context.Opponents;
        if (opponents.Count == 0)
            return false;

        var moves = allyOverride && potentialCaster != context.Self
            ? BuildUsableMoves(potentialCaster)
            : context.AvailableMoves;

        foreach (var opponent in opponents)
        {
            var damagingMove = allyOverride && potentialCaster != context.Self
                ? FindBestDamageMoveFor(potentialCaster, opponent, moves)
                : FindBestDamageMove(context, opponent);

            if (damagingMove == null)
                continue;

            float damage = allyOverride && potentialCaster != context.Self
                ? EstimateDamage(damagingMove, potentialCaster, opponent, bonus)
                : EstimateDamage(damagingMove, context.Self, opponent, bonus);

            if (damage >= opponent.currentHP)
                return true;
        }

        return false;
    }

    private static BattleAIDecision BuildFallbackDecision(BattleAIContext context)
    {
        foreach (var move in context.AvailableMoves)
        {
            if (move == null)
                continue;

            var target = FindFallbackTargetForMove(context, move);
            if (target != null)
                return BattleAIDecision.ForMove(move, target);
        }

        foreach (var item in context.AvailableItems)
        {
            if (item == null)
                continue;

            var target = FindFallbackTargetForItem(context, item);
            if (target != null)
                return BattleAIDecision.ForItem(item, target);
        }

        return BattleAIDecision.None;
    }

    private static CharacterUnit FindFallbackTargetForMove(BattleAIContext context, MusicalMoveSO move)
    {
        if (move == null)
            return null;

        TargetType type = ResolveTargetType(move);
        if (TargetsSelf(move))
            return context.Self;

        if (TargetsOpponents(move) && context.Opponents.Count > 0)
            return context.Opponents.FirstOrDefault(u => u != null && u.currentHP > 0);

        if (TargetsAllies(move, true) && context.Allies.Count > 0)
            return context.Allies.FirstOrDefault(u => u != null && u.currentHP > 0);

        return null;
    }

    private static CharacterUnit FindFallbackTargetForItem(BattleAIContext context, ItemData item)
    {
        if (item == null)
            return null;

        if (TargetsSelf(item))
            return context.Self;

        var type = ResolveTargetType(item);
        if (TargetsOpponents(type) && context.Opponents.Count > 0)
            return context.Opponents.FirstOrDefault(u => u != null && u.currentHP > 0);

        if (TargetsAllies(type) && context.Allies.Count > 0)
            return context.Allies.FirstOrDefault(u => u != null && u.currentHP > 0);

        return null;
    }

    private static bool ShouldPanic(BattleAIContext context)
    {
        if (context.Self == null)
            return false;

        float ratio = context.MaxHP > 0f ? context.Self.currentHP / context.MaxHP : 0f;
        if (ratio <= 0.25f)
            return true;

        float incoming = EstimateIncomingThreat(context);
        return incoming >= context.Self.currentHP;
    }

    private static float EstimateIncomingThreat(BattleAIContext context)
    {
        float highest = 0f;
        if (context.Self == null)
            return highest;

        foreach (var opponent in context.Opponents)
        {
            if (opponent == null)
                continue;

            var moves = BuildUsableMoves(opponent);
            foreach (var move in moves)
            {
                if (move == null || !IsDamageMove(move))
                    continue;

                if (!IsMoveApplicableToTarget(opponent, context.Self, move))
                    continue;

                float damage = EstimateDamage(move, opponent, context.Self);
                if (damage > highest)
                    highest = damage;
            }
        }

        return highest;
    }

    private static void TryRegisterOption(List<ScoredDecision> options, BattleAIDecision decision, float score)
    {
        if (!decision.IsValid || score <= 0f)
            return;

        options.Add(new ScoredDecision { Decision = decision, Score = score });
    }

    private static BattleAIDecision PickBest(List<ScoredDecision> options)
    {
        ScoredDecision best = null;
        foreach (var candidate in options)
        {
            if (candidate == null || !candidate.Decision.IsValid)
                continue;

            if (best == null || candidate.Score > best.Score)
                best = candidate;
        }

        return best != null ? best.Decision : BattleAIDecision.None;
    }

    private static List<MusicalMoveSO> BuildUsableMoves(CharacterUnit caster)
    {
        var result = new List<MusicalMoveSO>();
        if (caster == null || caster.Data == null)
            return result;

        var unique = new HashSet<MusicalMoveSO>();

        void TryAdd(MusicalMoveSO move)
        {
            if (move == null || unique.Contains(move))
                return;

            if (!IsMoveUsable(caster, move))
                return;

            unique.Add(move);
            result.Add(move);
        }

        if (caster.Data.musicalAttacks != null)
        {
            foreach (var move in caster.Data.musicalAttacks)
                TryAdd(move);
        }

        if (caster.Data.basicAttack != null)
        {
            foreach (var move in caster.Data.basicAttack)
                TryAdd(move);
        }

        TryAdd(caster.Data.specialMusicalMove);
        return result;
    }

    private static List<ItemData> BuildUsableItems(CharacterUnit caster)
    {
        var result = new List<ItemData>();
        if (caster?.Data?.itemSets == null)
            return result;

        var unique = new HashSet<ItemData>();
        foreach (var set in caster.Data.itemSets)
        {
            if (set?.prioritizedItems == null)
                continue;

            foreach (var item in set.prioritizedItems)
            {
                if (item == null || unique.Contains(item))
                    continue;
                if (!item.isUsableInBattle)
                    continue;

                unique.Add(item);
                result.Add(item);
            }
        }

        return result;
    }

    private static bool IsMoveUsable(CharacterUnit caster, MusicalMoveSO move)
    {
        if (caster == null || caster.Data == null || move == null)
            return false;

        if (move.onlyAwake && !caster.IsAwake)
            return false;

        if (move.enterAwake && caster.IsAwake)
            return false;

        if (move.enterAwake && caster.GetHarmonicCount(caster.Data.harmonicType) < caster.Data.awakeHarmonicThreshold)
            return false;

        if (caster.GetAvailableHarmonicsForCost(move.consumedHarmonicType) < move.harmonicCost)
            return false;

        if (caster.IsMoveOnCooldown(move))
            return false;

        return caster.CanUseMove(move);
    }

    private static bool TargetsSelf(MusicalMoveSO move)
        => ResolveTargetType(move) == TargetType.Self || ResolveTargetType(move) == TargetType.SpawnPosition;
    private static bool TargetsSelf(ItemData item) => ResolveTargetType(item) == TargetType.Self;

    private static bool TargetsAllies(MusicalMoveSO move, bool includeSelf)
    {
        var type = ResolveTargetType(move);
        if (includeSelf && (type == TargetType.Self || type == TargetType.SpawnPosition))
            return true;

        return type == TargetType.SingleAlly
               || type == TargetType.AllAllies
               || type == TargetType.SingleAllyOrEnemy
               || type == TargetType.All;
    }

    private static bool TargetsAllies(ItemData item, bool includeSelf)
    {
        var type = ResolveTargetType(item);
        if (includeSelf && type == TargetType.Self)
            return true;

        return type == TargetType.SingleAlly
               || type == TargetType.AllAllies
               || type == TargetType.All;
    }

    private static bool TargetsOpponents(MusicalMoveSO move) => TargetsOpponents(ResolveTargetType(move));
    private static bool TargetsOpponents(ItemData item) => TargetsOpponents(ResolveTargetType(item));

    private static bool TargetsOpponents(TargetType type)
    {
        return type == TargetType.SingleEnemy
               || type == TargetType.AllEnemies
               || type == TargetType.SingleAllyOrEnemy
               || type == TargetType.All;
    }

    private static bool TargetsAllies(TargetType type)
    {
        return type == TargetType.SingleAlly
               || type == TargetType.AllAllies
               || type == TargetType.SingleAllyOrEnemy
               || type == TargetType.All
               || type == TargetType.Self
               || type == TargetType.SpawnPosition;
    }

    private static TargetType ResolveTargetType(MusicalMoveSO move)
    {
        if (move == null)
            return TargetType.SingleEnemy;

        return move.targetType;
    }

    private static TargetType ResolveTargetType(ItemData item)
    {
        if (item == null)
            return TargetType.SingleAlly;

        TargetType type = item.defaultTargetType;
        if (item.targetTypes != null && item.targetTypes.Count > 0 && !item.targetTypes.Contains(type))
            type = item.targetTypes[0];

        return type;
    }

    private static bool IsMultiTarget(TargetType type)
    {
        return type == TargetType.AllEnemies
               || type == TargetType.AllAllies
               || type == TargetType.All;
    }

    private static bool IsHealingMove(MusicalMoveSO move) => move != null && move.HasEffect(MusicalEffectType.Heal);
    private static bool IsDamageMove(MusicalMoveSO move) => move != null && move.HasEffect(MusicalEffectType.Damage);
    private static bool ProvidesOffensiveBuff(MusicalMoveSO move) => move != null && move.HasEffect(MusicalEffectType.IncreaseDamage);

    private static bool IsHealingItem(ItemData item) => item != null && item.HasEffect(ItemEffectType.Heal);
    private static bool IsDamageItem(ItemData item) => item != null && item.HasEffect(ItemEffectType.Damage);
    private static bool ProvidesOffensiveBuff(ItemData item) => item != null && item.HasEffect(ItemEffectType.Buff) && item.buffStat == BuffStatType.Strength;

    private static bool IsDefensiveMove(MusicalMoveSO move)
    {
        if (move == null)
            return false;

        if (move.HasEffect(MusicalEffectType.IncreaseDefense)
            || move.HasEffect(MusicalEffectType.IncreaseMaxHP)
            || move.HasEffect(MusicalEffectType.DecreaseDamage)
            || move.HasEffect(MusicalEffectType.DecreaseDefense))
            return true;

        return move.HasEffect(MusicalEffectType.Sleep);
    }

    private static bool IsDefensiveItem(ItemData item)
    {
        if (item == null)
            return false;

        if (item.HasEffect(ItemEffectType.Buff) && item.buffStat == BuffStatType.Defense)
            return true;

        if (item.HasEffect(ItemEffectType.Debuff) && item.debuffStat == DebuffStatType.Strength)
            return true;

        return item.HasEffect(ItemEffectType.Sleep);
    }

    private static float EstimateDamage(MusicalMoveSO move, CharacterUnit caster, CharacterUnit target, float powerBonus = 0f)
    {
        if (move == null || caster == null || target == null)
            return 0f;

        float power = caster.currentPower + powerBonus;
        float value = move.GetEffectValue(MusicalEffectType.Damage, move.PrimaryEffectValue) + power;
        value *= caster.GetAttackMultiplier();
        value = caster.ApplyDamageModifiers(value);
        return Mathf.Max(0f, value);
    }

    private static float EstimateItemDamage(ItemData item, CharacterUnit caster, CharacterUnit target)
    {
        if (item == null || target == null)
            return 0f;

        float value = item.GetTotalEffectValue(ItemEffectType.Damage);
        if (caster != null)
            value *= caster.GetAttackMultiplier();
        return Mathf.Max(0f, value);
    }

    private static float EstimateHealing(MusicalMoveSO move, CharacterUnit target)
    {
        if (move == null || target == null)
            return 0f;

        float baseValue = move.GetEffectValue(MusicalEffectType.Heal, move.PrimaryEffectValue)
                          + Mathf.Max(target.currentSagacity, target.currentPower);
        return Mathf.Max(0f, target.ApplyHealingModifiers(baseValue));
    }

    private static float EstimateItemHealing(ItemData item, CharacterUnit target)
    {
        if (item == null || target == null)
            return 0f;

        float maxHP = target.MaxHP;
        return item.healIsPercentage ? maxHP * item.healAmount / 100f : item.healAmount;
    }

    private static float EstimateDefensiveValue(MusicalMoveSO move, CharacterUnit target)
    {
        if (move == null || target == null)
            return 0f;

        float total = 0f;

        if (move.HasEffect(MusicalEffectType.IncreaseDefense))
        {
            float amount = move.GetEffectValue(MusicalEffectType.IncreaseDefense);
            total += move.buffIsPercentage ? target.currentDefense * amount / 100f : amount;
        }

        if (move.HasEffect(MusicalEffectType.IncreaseMaxHP))
        {
            float amount = move.GetEffectValue(MusicalEffectType.IncreaseMaxHP);
            total += move.buffIsPercentage ? target.MaxHP * amount / 100f : amount;
        }

        if (move.HasEffect(MusicalEffectType.DecreaseDamage))
        {
            float amount = move.GetEffectValue(MusicalEffectType.DecreaseDamage);
            total += move.debuffIsPercentage ? amount : Mathf.Abs(amount);
        }

        if (move.HasEffect(MusicalEffectType.DecreaseDefense))
        {
            float amount = move.GetEffectValue(MusicalEffectType.DecreaseDefense);
            total += move.debuffIsPercentage ? amount : Mathf.Abs(amount);
        }

        if (move.HasEffect(MusicalEffectType.Sleep))
            total += 10f;

        return total;
    }

    private static float EstimateItemDefensiveValue(ItemData item, CharacterUnit target)
    {
        if (item == null || target == null)
            return 0f;

        float total = 0f;

        if (item.HasEffect(ItemEffectType.Buff) && item.buffStat == BuffStatType.Defense)
        {
            total += item.buffIsPercentage ? target.currentDefense * item.buffAmount / 100f : item.buffAmount;
        }

        if (item.HasEffect(ItemEffectType.Debuff) && item.debuffStat == DebuffStatType.Strength)
        {
            total += item.debuffIsPercentage ? item.debuffAmount : Mathf.Abs(item.debuffAmount);
        }

        if (item.HasEffect(ItemEffectType.Sleep))
            total += 10f;

        return total;
    }

    private static float EstimateOffensiveBonus(MusicalMoveSO move, CharacterUnit target)
    {
        if (move == null || target == null || !move.HasEffect(MusicalEffectType.IncreaseDamage))
            return 0f;

        float amount = move.GetEffectValue(MusicalEffectType.IncreaseDamage);
        if (move.buffIsPercentage)
            return target.currentPower * amount / 100f;

        return amount;
    }

    private static float EstimateItemOffensiveBonus(ItemData item, CharacterUnit target)
    {
        if (item == null || target == null || item.buffStat != BuffStatType.Strength || !item.HasEffect(ItemEffectType.Buff))
            return 0f;

        if (item.buffIsPercentage)
            return target.currentPower * item.buffAmount / 100f;

        return item.buffAmount;
    }

    private static bool IsMoveApplicableToTarget(BattleAIContext context, MusicalMoveSO move, CharacterUnit target) =>
        IsMoveApplicableToTarget(context.Self, target, move);

    private static bool IsMoveApplicableToTarget(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        if (caster == null || caster.Data == null || target == null || target.Data == null || move == null)
            return false;

        bool targetIsEnemy = caster.IsEnemyOf(target);
        var targetType = ResolveTargetType(move);

        bool isValid = targetType switch
        {
            TargetType.Self => target == caster,
            TargetType.SpawnPosition => target == caster,
            TargetType.SingleEnemy or TargetType.AllEnemies or TargetType.All => targetIsEnemy,
            TargetType.SingleAlly or TargetType.AllAllies => !targetIsEnemy,
            TargetType.SingleAllyOrEnemy => true,
            _ => false
        };

        if (!isValid)
            return false;

        return CheckSpatialConstraints(caster, target, move);
    }

    private static bool IsItemApplicableToTarget(BattleAIContext context, ItemData item, CharacterUnit target)
    {
        if (context.Self == null || target == null || item == null)
            return false;

        bool targetIsEnemy = context.Self.IsEnemyOf(target);
        var type = ResolveTargetType(item);

        bool isValid = type switch
        {
            TargetType.Self => target == context.Self,
            TargetType.SingleEnemy or TargetType.AllEnemies or TargetType.All => targetIsEnemy,
            TargetType.SingleAlly or TargetType.AllAllies => !targetIsEnemy,
            _ => false
        };

        if (!isValid)
            return false;

        return CheckItemSpatialConstraints(context.Self, target, item);
    }

    private static bool CheckSpatialConstraints(CharacterUnit caster, CharacterUnit target, MusicalMoveSO move)
    {
        var manager = NewBattleManager.Instance;
        if (manager == null || move == null || caster == null || target == null)
            return true;

        if (!manager.IsTargetInRange(caster, target, move))
            return false;

        if (!manager.HasSpaceForMove(caster, target, move))
            return false;

        return manager.IsTargetAltitudeValid(target, move);
    }

    private static bool CheckItemSpatialConstraints(CharacterUnit caster, CharacterUnit target, ItemData item)
    {
        var manager = NewBattleManager.Instance;
        if (manager == null || item == null || caster == null || target == null)
            return true;

        return manager.IsTargetInRange(caster, target, item);
    }

    private static CharacterUnit FindMostInjured(IEnumerable<CharacterUnit> units)
    {
        CharacterUnit best = null;
        float bestRatio = float.MaxValue;

        foreach (var unit in units)
        {
            if (unit == null || unit.currentHP <= 0)
                continue;

            float maxHP = unit.MaxHP;
            float ratio = maxHP > 0f ? unit.currentHP / maxHP : 1f;
            if (ratio < bestRatio)
            {
                bestRatio = ratio;
                best = unit;
            }
        }

        return best;
    }

    private static MusicalMoveSO FindBestDamageMove(BattleAIContext context, CharacterUnit target)
    {
        MusicalMoveSO best = null;
        float bestDamage = 0f;

        foreach (var move in context.AvailableMoves)
        {
            if (move == null || !IsDamageMove(move))
                continue;

            if (!IsMoveApplicableToTarget(context, move, target))
                continue;

            float damage = EstimateDamage(move, context.Self, target);
            if (damage > bestDamage)
            {
                bestDamage = damage;
                best = move;
            }
        }

        return best;
    }

    private static MusicalMoveSO FindBestDamageMoveFor(CharacterUnit attacker, CharacterUnit target, List<MusicalMoveSO> moves)
    {
        MusicalMoveSO best = null;
        float bestDamage = 0f;

        foreach (var move in moves)
        {
            if (move == null || !IsDamageMove(move))
                continue;

            if (!IsMoveApplicableToTarget(attacker, target, move))
                continue;

            float damage = EstimateDamage(move, attacker, target);
            if (damage > bestDamage)
            {
                bestDamage = damage;
                best = move;
            }
        }

        return best;
    }

    private readonly struct DamageSummary
    {
        public DamageSummary(float totalDamage, int killCount)
        {
            TotalDamage = totalDamage;
            KillCount = killCount;
        }

        public float TotalDamage { get; }
        public int KillCount { get; }
    }
}
