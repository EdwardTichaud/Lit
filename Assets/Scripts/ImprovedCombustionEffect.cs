// Role:
// Squad effect that periodically restores flame time through ImprovedCombustionRuntime.
// Usage:
// Assigned to the ImprovedCombustion effect asset and applied when the squad/building gains levels.
// Responsibilities:
// Resolve per-level seconds/interval values and register them with the runtime service.
// Dependencies:
// Effect, ISquadEffect, ImprovedCombustionRuntime, SquadCharacterController.
// Precautions:
// Runtime behaviour is centralized in ImprovedCombustionRuntime; keep this asset as configuration.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configures periodic flame-time restoration for the squad.
/// </summary>
[CreateAssetMenu(fileName = "ImprovedCombustion", menuName = "Scriptable Objects/Effects/Improved Combustion")]
public class ImprovedCombustionEffect : Effect, ISquadEffect
{
    [Header("Timing")]
    /// <summary>Base seconds added at level 1.</summary>
    [Tooltip("Secondes ajoutees a chaque activation.")]
    [SerializeField] private int addedSeconds = 10;
    /// <summary>Linear seconds bonus per level after level 1.</summary>
    [Tooltip("Bonus de secondes par niveau (lineaire).")]
    [SerializeField] private int addedSecondsPerLevel = 0;
    /// <summary>Base interval between activations.</summary>
    [Tooltip("Intervalle entre deux activations.")]
    [SerializeField] private float intervalSeconds = 10f;
    /// <summary>Linear interval change per level. Negative means faster.</summary>
    [Tooltip("Variation d'intervalle par niveau (negatif = plus rapide).")]
    [SerializeField] private float intervalSecondsPerLevel = 0f;
    /// <summary>Lowest allowed interval after overrides and formulas.</summary>
    [Tooltip("Intervalle minimum autorise.")]
    [SerializeField] private float minimumIntervalSeconds = 0.5f;

    [Header("Per Level Overrides")]
    /// <summary>Per-level seconds override, index 0 is level 1.</summary>
    [Tooltip("Override par niveau des secondes ajoutees (index 0 = niveau 1).")]
    [SerializeField] private List<int> addedSecondsByLevel = new List<int>();
    /// <summary>Per-level interval override, index 0 is level 1.</summary>
    [Tooltip("Override par niveau de l'intervalle (index 0 = niveau 1).")]
    [SerializeField] private List<float> intervalSecondsByLevel = new List<float>();

    [Header("Rules")]
    /// <summary>If true, only characters with a flame item benefit from the runtime effect.</summary>
    [Tooltip("N'applique l'effet que si le perso possede une flamme.")]
    [SerializeField] private bool requireFlameItem = true;

    [System.NonSerialized] private int appliedLevels;

    /// <summary>
    /// Applies one level of this squad effect.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return ApplyToSquad(1);
    }

    /// <summary>
    /// Applies a positive level delta to the squad effect.
    /// </summary>
    public bool ApplyToSquad(int levelDelta)
    {
        if (levelDelta <= 0)
        {
            return false;
        }

        appliedLevels = Mathf.Max(0, appliedLevels + levelDelta);
        ApplyRuntimeConfig(Mathf.Max(1, appliedLevels));
        return true;
    }

    /// <summary>Returns a UI description for the requested level.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        int seconds = GetSecondsForLevel(safeLevel);
        float interval = GetIntervalForLevel(safeLevel);
        string intervalText = interval >= 1f ? interval.ToString("0.#") : interval.ToString("0.##");
        return $"+{seconds}s flamme / {intervalText}s (squad)";
    }

    /// <summary>Returns short bonus text for the requested level.</summary>
    public override string GetBonusDescriptionForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        int seconds = GetSecondsForLevel(safeLevel);
        float interval = GetIntervalForLevel(safeLevel);
        string intervalText = interval >= 1f ? interval.ToString("0.#") : interval.ToString("0.##");
        return $"+{seconds}s / {intervalText}s";
    }

    private void ApplyRuntimeConfig(int level)
    {
        int seconds = GetSecondsForLevel(level);
        float interval = GetIntervalForLevel(level);

        // Invalid or zero values mean this level should not register a runtime tick.
        if (seconds <= 0 || interval <= 0f)
        {
            return;
        }

        ImprovedCombustionRuntime runtime = ImprovedCombustionRuntime.GetOrCreate();
        runtime.Register(this, seconds, interval, requireFlameItem);
    }

    private int GetSecondsForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        if (addedSecondsByLevel != null && addedSecondsByLevel.Count >= safeLevel)
        {
            return Mathf.Max(0, addedSecondsByLevel[safeLevel - 1]);
        }

        int value = addedSeconds + addedSecondsPerLevel * Mathf.Max(0, safeLevel - 1);
        return Mathf.Max(0, value);
    }

    private float GetIntervalForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        if (intervalSecondsByLevel != null && intervalSecondsByLevel.Count >= safeLevel)
        {
            float value = intervalSecondsByLevel[safeLevel - 1];
            if (value > 0f)
            {
                return Mathf.Max(minimumIntervalSeconds, value);
            }
        }

        float linear = intervalSeconds + intervalSecondsPerLevel * Mathf.Max(0, safeLevel - 1);
        linear = Mathf.Max(0.01f, linear);
        return Mathf.Max(minimumIntervalSeconds, linear);
    }
}
