// Role:
// Squad effect that defines the maximum torch duration granted by a torch building/item.
// Usage:
// Assigned to torch-related ScriptableObject effect assets.
// Responsibilities:
// Provide per-level torch max duration text and values for other systems.
// Dependencies:
// Effect, ISquadEffect, SquadCharacterController.
// Precautions:
// This effect reports data; the actual torch reset logic lives elsewhere.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines max torch seconds for a squad-level torch effect.
/// </summary>
[CreateAssetMenu(fileName = "TorchEffect", menuName = "Scriptable Objects/Effects/Torch")]
public class TorchEffect : Effect, ISquadEffect
{
    [Header("Torch Max")]
    /// <summary>Maximum torch seconds at level 1.</summary>
    [Tooltip("Secondes max de torche au niveau 1.")]
    [SerializeField] private int maxSeconds = 300;
    /// <summary>Linear maximum-seconds bonus per level after level 1.</summary>
    [Tooltip("Bonus de secondes max par niveau.")]
    [SerializeField] private int maxSecondsPerLevel = 0;
    /// <summary>Optional per-level overrides, where index 0 is level 1.</summary>
    [Tooltip("Override par niveau (index 0 = niveau 1).")]
    [SerializeField] private List<int> maxSecondsByLevel = new List<int>();

    /// <summary>
    /// This effect is passive/data-only when applied to one controller.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return true;
    }

    /// <summary>
    /// Returns true when the squad effect is upgraded by at least one level.
    /// </summary>
    public bool ApplyToSquad(int levelDelta)
    {
        return levelDelta > 0;
    }

    /// <summary>
    /// Resolves max torch seconds for a 1-based level.
    /// </summary>
    public int GetMaxSecondsForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        // Per-level override wins over the linear formula when configured.
        if (maxSecondsByLevel != null && maxSecondsByLevel.Count >= safeLevel)
        {
            return Mathf.Max(0, maxSecondsByLevel[safeLevel - 1]);
        }

        int value = maxSeconds + maxSecondsPerLevel * Mathf.Max(0, safeLevel - 1);
        return Mathf.Max(0, value);
    }

    /// <summary>Returns a UI description for the given level.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        int value = GetMaxSecondsForLevel(level);
        return $"Torche max: {value}s (reset maison)";
    }

    /// <summary>Returns a short bonus text for the given level.</summary>
    public override string GetBonusDescriptionForLevel(int level)
    {
        int value = GetMaxSecondsForLevel(level);
        return $"Torche max: {value}s";
    }
}
