using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RealTimeCombatLoadout : MonoBehaviour
{
    private readonly PlayerModuleConfiguration<PlayerEquipmentSettings> moduleConfiguration = new PlayerModuleConfiguration<PlayerEquipmentSettings>();
    private PlayerEquipmentSettings ModuleSettings => moduleConfiguration.Resolve(this, data => data.equipment);

    public const int SlotCount = 8;

    private List<CombatAttackDefinition> equippedAttacks { get => ModuleSettings.equippedAttacks; set => ModuleSettings.equippedAttacks = value; }
    private LightSkillSO equippedLightSkill { get => ModuleSettings.equippedLightSkill; set => ModuleSettings.equippedLightSkill = value; }

    public event Action LoadoutChanged;
    public IReadOnlyList<CombatAttackDefinition> EquippedAttacks => equippedAttacks;
    public LightSkillSO EquippedLightSkill => equippedLightSkill;

    private void OnValidate()
    {
        NormalizeSlots();
    }

    public CombatAttackDefinition GetAttack(int index)
    {
        return index >= 0 && index < equippedAttacks.Count ? equippedAttacks[index] : null;
    }

    public bool SetAttack(int index, CombatAttackDefinition attack)
    {
        if (index < 0 || index >= SlotCount)
        {
            return false;
        }

        NormalizeSlots();
        if (equippedAttacks[index] == attack)
        {
            return true;
        }

        equippedAttacks[index] = attack;
        LoadoutChanged?.Invoke();
        return true;
    }

    public bool SetLightSkill(LightSkillSO lightSkill)
    {
        if (equippedLightSkill == lightSkill)
        {
            return true;
        }

        equippedLightSkill = lightSkill;
        LoadoutChanged?.Invoke();
        return true;
    }

    private void NormalizeSlots()
    {
        if (equippedAttacks == null)
        {
            equippedAttacks = new List<CombatAttackDefinition>(SlotCount);
        }

        while (equippedAttacks.Count < SlotCount)
        {
            equippedAttacks.Add(null);
        }

        if (equippedAttacks.Count > SlotCount)
        {
            equippedAttacks.RemoveRange(SlotCount, equippedAttacks.Count - SlotCount);
        }
    }
}
