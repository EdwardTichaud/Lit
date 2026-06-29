using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralise la gestion des effets temporaires appliqués à une <see cref="CharacterUnit"/>.
/// L'objectif est de sortir cette responsabilité de l'InventoryManager afin de rendre
/// le code plus modulaire : tout système (Items, MusicalMoves, scripts narratifs...)
/// peut désormais appliquer un buff ou un débuff sans connaître la structure interne
/// de l'inventaire. Le composant se charge également de remettre l'unité dans son état
/// initial lorsque l'effet arrive à expiration ou que l'objet est désactivé.
/// Les durées sont exprimées en tours afin de rester cohérentes avec le gameplay.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterUnit))]
public class CharacterStatusEffectController : MonoBehaviour
{
    /// <summary>
    /// Représente un modificateur actif (buff ou débuff).
    /// Chaque entrée conserve la valeur appliquée ainsi que la durée restante.
    /// Lorsque le compteur arrive à zéro, la valeur est automatiquement retirée
    /// et l'entrée supprimée.
    /// </summary>
    private sealed class ActiveModifier
    {
        public BuffStatType stat;
        public float value;
        public int remainingTurns;
        public bool isInfinite;
    }

    private CharacterUnit owner;

    /// <summary>
    /// Liste des modificateurs actuellement appliqués à l'unité.
    /// L'utilisation d'une liste (plutôt qu'un simple dictionnaire par statistique)
    /// permet de conserver plusieurs buffs cumulés avec des durées différentes.
    /// </summary>
    private readonly List<ActiveModifier> activeModifiers = new();

    /// <summary>
    /// Fournit un accès paresseux au composant, en créant automatiquement
    /// une instance lorsque l'unité ciblée n'en possède pas encore.
    /// </summary>
    public static CharacterStatusEffectController GetOrCreate(CharacterUnit unit)
    {
        if (unit == null)
            return null;

        if (!unit.TryGetComponent(out CharacterStatusEffectController controller))
            controller = unit.gameObject.AddComponent<CharacterStatusEffectController>();

        return controller;
    }

    #region Cycle de vie

    private void Awake()
    {
        owner = GetComponent<CharacterUnit>();
    }

    private void OnDisable()
    {
        // Lorsqu'une unité sort de la scène, on réinitialise proprement les effets.
        ResetAllModifiers();
    }

    private void OnDestroy()
    {
        ResetAllModifiers();
    }

    #endregion

    #region API statique conviviale

    public static void ApplyBuff(CharacterUnit target, BuffStatType stat, int amount, float duration, bool isPercentage)
    {
        GetOrCreate(target)?.ApplyBuffInternal(stat, amount, duration, isPercentage);
    }

    public static void ApplyDebuff(CharacterUnit target, DebuffStatType stat, int amount, float duration, bool isPercentage)
    {
        GetOrCreate(target)?.ApplyDebuffInternal(stat, amount, duration, isPercentage);
    }

    public static void ApplyInterceptionImmunity(CharacterUnit target, int turns)
    {
        GetOrCreate(target)?.ApplyInterceptionImmunityInternal(turns);
    }

    public static void ExtendEffectDurations(CharacterUnit target, float additionalDuration)
    {
        GetOrCreate(target)?.ExtendEffectDurationsInternal(additionalDuration);
    }

    public static void ApplySleep(CharacterUnit target, int turns = -1)
    {
        GetOrCreate(target)?.ApplySleepInternal(turns);
    }

    public static void ApplyStun(CharacterUnit target, int turns = 1)
    {
        GetOrCreate(target)?.ApplyStunInternal(turns);
    }

    public static void RemoveSleep(CharacterUnit target)
    {
        GetOrCreate(target)?.RemoveSleepInternal();
    }

    public static void RemoveStun(CharacterUnit target)
    {
        GetOrCreate(target)?.RemoveStunInternal();
    }

    #endregion

    #region Méthodes d'instance

    private void ApplyBuffInternal(BuffStatType stat, int amount, float duration, bool isPercentage)
    {
        if (owner == null || stat == BuffStatType.None || amount == 0)
            return;

        float baseValue = GetBaseStat(stat);
        float delta = isPercentage ? baseValue * amount / 100f : amount;
        ApplyModifier(stat, delta, duration);

        DamagePopupManager.Instance?.ShowBuff(owner.transform, stat, amount, isPercentage);
    }

    private void ApplyDebuffInternal(DebuffStatType stat, int amount, float duration, bool isPercentage)
    {
        if (owner == null || stat == DebuffStatType.None || amount == 0)
            return;

        float baseValue = GetBaseStat((BuffStatType)stat);
        float delta = isPercentage ? baseValue * amount / 100f : amount;
        // Un débuff est simplement un buff négatif : on applique la valeur inverse.
        ApplyModifier((BuffStatType)stat, -delta, duration);

        DamagePopupManager.Instance?.ShowDebuff(owner.transform, stat, amount, isPercentage);
    }

    private void ApplyInterceptionImmunityInternal(int turns)
    {
        if (owner == null || turns <= 0)
            return;

        owner.isInterceptionImmune = true;
        owner.interceptionImmunityTurns = Mathf.Max(owner.interceptionImmunityTurns, turns);
    }

    /// <summary>
    /// Prolonge la durée de tous les effets temporaires appliqués à l'unité.
    /// Le paramètre est volontairement en <see cref="float"/> afin de gérer
    /// aussi bien les durées exprimées en secondes que celles assimilées à un
    /// nombre de tours. Les effets infinis restent inchangés.
    /// </summary>
    private void ExtendEffectDurationsInternal(float additionalDuration)
    {
        int additionalTurns = NormalizeTurns(additionalDuration);
        if (additionalTurns <= 0)
            return;

        foreach (var modifier in activeModifiers)
        {
            if (!modifier.isInfinite)
                modifier.remainingTurns += additionalTurns;
        }

        if (owner != null && owner.interceptionImmunityTurns > 0)
            owner.interceptionImmunityTurns += additionalTurns;
    }

    private void ApplySleepInternal(int turns)
    {
        if (owner == null)
            return;

        var sleep = owner.GetComponent<SleepStatus>();
        if (sleep == null)
            sleep = owner.gameObject.AddComponent<SleepStatus>();
        sleep.Sleep(turns);

        DamagePopupManager.Instance?.ShowStatus(owner.transform, "Sommeil", false);
    }

    private void ApplyStunInternal(int turns)
    {
        if (owner == null)
            return;

        var stunned = owner.GetComponent<StunnedStatus>();
        if (stunned == null)
            stunned = owner.gameObject.AddComponent<StunnedStatus>();
        stunned.Stun(Mathf.Max(1, turns));

        DamagePopupManager.Instance?.ShowStatus(owner.transform, "Etourdi", false);
    }

    private void RemoveSleepInternal()
    {
        if (owner == null)
            return;

        var sleep = owner.GetComponent<SleepStatus>();
        if (sleep != null)
            sleep.WakeUp();
    }

    private void RemoveStunInternal()
    {
        if (owner == null)
            return;

        var stunned = owner.GetComponent<StunnedStatus>();
        stunned?.Recover();
    }

    #endregion

    #region Gestion interne des modificateurs

    /// <summary>
    /// Ajoute un modificateur et applique immédiatement sa valeur. Le retrait
    /// est assuré soit par la coroutine dédiée, soit par <see cref="ResetAllModifiers"/>.
    /// </summary>
    private void ApplyModifier(BuffStatType stat, float value, float duration)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        int durationTurns = NormalizeTurns(duration);
        var modifier = new ActiveModifier
        {
            stat = stat,
            value = value,
            remainingTurns = durationTurns,
            isInfinite = durationTurns <= 0
        };

        activeModifiers.Add(modifier);
        ModifyStat(stat, value);
    }

    private void FinalizeModifier(ActiveModifier modifier)
    {
        ModifyStat(modifier.stat, -modifier.value);

        activeModifiers.Remove(modifier);
    }

    private void ResetAllModifiers()
    {
        for (int i = activeModifiers.Count - 1; i >= 0; i--)
        {
            var modifier = activeModifiers[i];

            ModifyStat(modifier.stat, -modifier.value);
            activeModifiers.RemoveAt(i);
        }
    }

    /// <summary>
    /// Décrémentation en fin de tour des buffs/débuffs actifs.
    /// Les durées sont exprimées en tours pour rester cohérentes avec le gameplay.
    /// </summary>
    public void TickTurn()
    {
        if (activeModifiers.Count == 0)
            return;

        for (int i = activeModifiers.Count - 1; i >= 0; i--)
        {
            var modifier = activeModifiers[i];
            if (modifier.isInfinite)
                continue;

            modifier.remainingTurns = Mathf.Max(0, modifier.remainingTurns - 1);
            if (modifier.remainingTurns <= 0)
                FinalizeModifier(modifier);
        }
    }

    private void ModifyStat(BuffStatType stat, float delta)
    {
        if (owner == null)
            return;

        switch (stat)
        {
            case BuffStatType.Strength:
                owner.currentStrength += delta;
                break;
            case BuffStatType.Defense:
                owner.currentDefense += delta;
                break;
            case BuffStatType.Initiative:
                owner.currentInitiative += delta;
                break;
            case BuffStatType.MaxHP:
                owner.currentVitality += delta;
                owner.RefreshHealthDisplay(refreshMax: true);
                break;
            case BuffStatType.Power:
                owner.AddBasePowerBonus(delta);
                break;
            case BuffStatType.CriticalRate:
                owner.AddCriticalRateBonus(delta);
                break;
        }
    }

    private float GetBaseStat(BuffStatType stat)
    {
        if (owner == null || owner.Data == null)
            return 0f;

        return stat switch
        {
            BuffStatType.Strength => owner.Data.baseStrength,
            BuffStatType.Defense => owner.Data.baseDefense,
            BuffStatType.Initiative => owner.Data.baseInitiative,
            BuffStatType.MaxHP => owner.Data.baseHP + owner.currentVitality,
            BuffStatType.Power => owner.BasePower,
            BuffStatType.CriticalRate => owner.CriticalRate,
            _ => 0f,
        };
    }

    /// <summary>
    /// Normalise une durée saisie dans l'éditeur en un nombre de tours.
    /// Les valeurs fractionnaires sont arrondies à l'entier supérieur pour éviter
    /// des effets qui expirent plus tôt que prévu. Une durée <= 0 signifie "infini".
    /// </summary>
    private static int NormalizeTurns(float duration)
    {
        if (duration <= 0f)
            return 0;

        return Mathf.Max(1, Mathf.CeilToInt(duration));
    }

    #endregion
}
