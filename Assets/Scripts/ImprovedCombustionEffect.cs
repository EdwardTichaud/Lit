using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ImprovedCombustion", menuName = "Scriptable Objects/Effects/Improved Combustion")]
// Ajoute periodiquement du temps de torche a la squad.
public class ImprovedCombustionEffect : Effect, ISquadEffect
{
    [Header("Timing")]
    [Tooltip("Secondes ajoutees a chaque activation.")]
    [SerializeField] private int addedSeconds = 10;
    [Tooltip("Bonus de secondes par niveau (lineaire).")]
    [SerializeField] private int addedSecondsPerLevel = 0;
    [Tooltip("Intervalle entre deux activations.")]
    [SerializeField] private float intervalSeconds = 10f;
    [Tooltip("Variation d'intervalle par niveau (negatif = plus rapide).")]
    [SerializeField] private float intervalSecondsPerLevel = 0f;
    [Tooltip("Intervalle minimum autorise.")]
    [SerializeField] private float minimumIntervalSeconds = 0.5f;

    [Header("Per Level Overrides")]
    [Tooltip("Override par niveau des secondes ajoutees (index 0 = niveau 1).")]
    [SerializeField] private List<int> addedSecondsByLevel = new List<int>();
    [Tooltip("Override par niveau de l'intervalle (index 0 = niveau 1).")]
    [SerializeField] private List<float> intervalSecondsByLevel = new List<float>();

    [Header("Rules")]
    [Tooltip("N'applique l'effet que si le perso possede une torche.")]
    [SerializeField] private bool requireTorchItem = true;

    [System.NonSerialized] private int appliedLevels;

    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return ApplyToSquad(1);
    }

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

    public override string GetDescriptionForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        int seconds = GetSecondsForLevel(safeLevel);
        float interval = GetIntervalForLevel(safeLevel);
        string intervalText = interval >= 1f ? interval.ToString("0.#") : interval.ToString("0.##");
        return $"+{seconds}s torche / {intervalText}s (squad)";
    }

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

        if (seconds <= 0 || interval <= 0f)
        {
            return;
        }

        ImprovedCombustionRuntime runtime = ImprovedCombustionRuntime.GetOrCreate();
        runtime.Register(this, seconds, interval, requireTorchItem);
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
