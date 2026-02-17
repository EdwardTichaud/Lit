using UnityEngine;

[CreateAssetMenu(fileName = "CraftingSlotsPerLevelEffect", menuName = "Scriptable Objects/Effects/Crafting Slots Per Level")]
// Controle le nombre de slots de craft accessibles selon le niveau du building.
public class CraftingSlotsPerLevelEffect : Effect
{
    [Header("Slots")]
    [Tooltip("Nombre de slots disponibles au niveau 1.")]
    [SerializeField] private int baseSlots = 1;
    [Tooltip("Nombre de slots ajoutes par niveau apres le niveau 1.")]
    [SerializeField] private int slotsPerLevel = 1;
    [Tooltip("Limite maximale (0 = pas de limite).")]
    [SerializeField] private int maxSlots = 0;

    public int BaseSlots => Mathf.Max(0, baseSlots);
    public int SlotsPerLevel => Mathf.Max(0, slotsPerLevel);
    public int MaxSlots => Mathf.Max(0, maxSlots);

    public int GetSlotsForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        int slots = BaseSlots + (safeLevel - 1) * SlotsPerLevel;
        if (MaxSlots > 0)
        {
            slots = Mathf.Min(slots, MaxSlots);
        }

        return Mathf.Max(0, slots);
    }

    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return false;
    }

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

    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    public override string GetBonusDescriptionForLevel(int level)
    {
        int slots = GetSlotsForLevel(level);
        return $"Slots de craft: {slots}";
    }
}
