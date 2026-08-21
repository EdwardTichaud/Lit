using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Etat mutable d'un personnage pendant une partie. Cet objet n'est jamais
/// serialize dans CharacterData : la sauvegarde en est la representation durable.
/// </summary>
[Serializable]
public sealed class CharacterRuntimeState
{
    public List<Item> inventoryItems = new List<Item>();
    public List<Item> equippedInteractionItems = new List<Item>();
    public List<Item> enabledCombatItems = new List<Item>();
    public List<CombatDefenseItemHitPointData> combatDefenseItemHitPoints = new List<CombatDefenseItemHitPointData>();
    public int flameSecondsRemaining;
    public bool flameEquipped;
    public bool inventoryInitialized;
    public int muninChargesRemaining;
    public int muninMaxCharges = 10;
    public bool muninChargesInitialized;

    public void EnsureCollections()
    {
        inventoryItems ??= new List<Item>();
        equippedInteractionItems ??= new List<Item>();
        enabledCombatItems ??= new List<Item>();
        combatDefenseItemHitPoints ??= new List<CombatDefenseItemHitPointData>();
    }

    public void ApplyInventory(
        List<Item> items,
        int flameSeconds,
        bool equipped,
        bool initialized,
        List<Item> equippedItems,
        List<Item> combatItems,
        List<CombatDefenseItemHitPointData> defenseHitPoints)
    {
        EnsureCollections();
        inventoryItems.Clear();
        if (items != null) inventoryItems.AddRange(items);

        equippedInteractionItems.Clear();
        if (equippedItems != null) equippedInteractionItems.AddRange(equippedItems);

        enabledCombatItems.Clear();
        if (combatItems != null) enabledCombatItems.AddRange(combatItems);

        combatDefenseItemHitPoints.Clear();
        if (defenseHitPoints != null)
        {
            for (int i = 0; i < defenseHitPoints.Count; i++)
            {
                CombatDefenseItemHitPointData entry = defenseHitPoints[i];
                if (entry == null) continue;
                combatDefenseItemHitPoints.Add(new CombatDefenseItemHitPointData
                {
                    itemId = entry.itemId,
                    hitPoints = entry.hitPoints,
                    quantity = entry.quantity
                });
            }
        }

        flameSecondsRemaining = Mathf.Max(0, flameSeconds);
        flameEquipped = equipped;
        inventoryInitialized = initialized;
    }
}
