using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RealTimeCombatLoadout : MonoBehaviour
{
    public const int SlotCount = 8;

    [SerializeField] private List<CombatAttackDefinition> equippedAttacks = new List<CombatAttackDefinition>(SlotCount);

    public event Action LoadoutChanged;
    public IReadOnlyList<CombatAttackDefinition> EquippedAttacks => equippedAttacks;

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
