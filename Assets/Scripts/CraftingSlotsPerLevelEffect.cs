// Role:
// Data effect that tells crafting UI how many slots are available per building level.
// Usage:
// Assigned to building/item effects used by crafting panels.
// Responsibilities:
// Calculate slot counts and provide short UI descriptions.
// Dependencies:
// Effect and Mathf.
// Precautions:
// Apply intentionally returns false because this effect is read as configuration.
using UnityEngine;

/// <summary>
/// Calculates crafting slots from building level.
/// </summary>
[CreateAssetMenu(fileName = "CraftingSlotsPerLevelEffect", menuName = "Scriptable Objects/Effects/Crafting Slots Per Level")]
public class CraftingSlotsPerLevelEffect : Effect
{
    [Header("Slots")]
    /// <summary>Slots available at level 1.</summary>
    [Tooltip("Nombre de slots disponibles au niveau 1.")]
    [SerializeField] private int baseSlots = 1;
    /// <summary>Additional slots per level after level 1.</summary>
    [Tooltip("Nombre de slots ajoutes par niveau apres le niveau 1.")]
    [SerializeField] private int slotsPerLevel = 1;
    /// <summary>Maximum slot cap. Zero means unlimited.</summary>
    [Tooltip("Limite maximale (0 = pas de limite).")]
    [SerializeField] private int maxSlots = 0;

    /// <summary>Clamped base slot count.</summary>
    public int BaseSlots => Mathf.Max(0, baseSlots);
    /// <summary>Clamped per-level slot increase.</summary>
    public int SlotsPerLevel => Mathf.Max(0, slotsPerLevel);
    /// <summary>Clamped maximum slot cap.</summary>
    public int MaxSlots => Mathf.Max(0, maxSlots);

    /// <summary>
    /// Returns the number of crafting slots available for a 1-based level.
    /// </summary>
    public int GetSlotsForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        int slots = BaseSlots + (safeLevel - 1) * SlotsPerLevel;
        if (MaxSlots > 0)
        {
            // A max of 0 intentionally means no cap.
            slots = Mathf.Min(slots, MaxSlots);
        }

        return Mathf.Max(0, slots);
    }

    /// <summary>
    /// Returns false because this effect is configuration read by other systems.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return false;
    }

    /// <summary>Returns custom or generated slot description.</summary>
    public override string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        if (SlotsPerLevel <= 0)
        {
            return "Slots de craft fixes.";
        }

        return $"Ajoute {SlotsPerLevel} slot(s) de craft par niveau.";
    }

    /// <summary>Returns the slot description for a level.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    /// <summary>Returns short slot count text for a level.</summary>
    public override string GetBonusDescriptionForLevel(int level)
    {
        int slots = GetSlotsForLevel(level);
        return $"Slots de craft: {slots}";
    }
}
