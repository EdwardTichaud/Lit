using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Service dédié à la manipulation des piles d'objets de l'inventaire.
/// Il encapsule les structures de données afin d'éviter que le <see cref="InventoryManager"/>
/// ne mélange logique métier, UI et input.
/// </summary>
public sealed class InventoryDataService
{
    private readonly List<InventoryItemStack> itemStacks;
    private readonly Dictionary<ItemData, InventoryItemStack> stackLookup = new();
    private readonly List<ItemData> flatInventoryCache = new();

    public InventoryDataService(List<InventoryItemStack> sourceStacks)
    {
        itemStacks = sourceStacks ?? new List<InventoryItemStack>();
        RebuildLookup();
        SortStacks();
    }

    /// <summary>
    /// Liste des piles actuellement connues (utilisée par l'UI et la persistance).
    /// </summary>
    public IReadOnlyList<InventoryItemStack> ItemStacks => itemStacks;

    /// <summary>
    /// Produit un instantané des items possédés afin d'alimenter les listes UI.
    /// </summary>
    public IReadOnlyList<ItemData> BuildInventorySnapshot()
    {
        flatInventoryCache.Clear();

        foreach (var stack in itemStacks)
        {
            if (stack?.item == null)
                continue;

            if (stack.item.consumeOnUse && stack.quantity <= 0)
                continue;

            int copies = stack.item.consumeOnUse
                ? stack.quantity
                : Mathf.Max(1, stack.quantity);
            for (int i = 0; i < copies; i++)
                flatInventoryCache.Add(stack.item);
        }

        return flatInventoryCache;
    }

    /// <summary>
    /// Construit la liste des items réellement utilisables en combat.
    /// </summary>
    public List<ItemData> BuildUsableItems()
    {
        List<ItemData> usable = new();

        foreach (var stack in itemStacks)
        {
            if (!IsStackUsable(stack))
                continue;

            int copies = stack.item.consumeOnUse
                ? stack.quantity
                : Mathf.Max(1, stack.quantity);
            for (int i = 0; i < copies; i++)
                usable.Add(stack.item);
        }

        return usable;
    }

    /// <summary>
    /// Ajoute (ou incrémente) une pile. Retourne faux si l'entrée est invalide.
    /// </summary>
    public bool AddItem(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0)
            return false;

        if (!stackLookup.TryGetValue(item, out var stack))
        {
            stack = new InventoryItemStack(item, 0);
            itemStacks.Add(stack);
            stackLookup[item] = stack;
        }

        stack.AddQuantity(quantity);
        SortStacks();
        return true;
    }

    public bool CanUseItem(ItemData item)
    {
        if (item == null)
            return false;

        return stackLookup.TryGetValue(item, out var stack) && IsStackUsable(stack);
    }

    public void RegisterItemUse(ItemData item)
    {
        if (item == null)
            return;

        if (!stackLookup.TryGetValue(item, out var stack))
            return;

        bool consumed = stack.RegisterUse(item.consumeOnUse);
        if (!consumed && item.consumeOnUse)
            return;

        if (item.consumeOnUse && stack.quantity <= 0)
        {
            itemStacks.Remove(stack);
            stackLookup.Remove(item);
        }
    }

    public void ResetTurnUsage()
    {
        foreach (var stack in itemStacks)
            stack?.ResetTurnUsage();
    }

    public void ResetBattleUsage()
    {
        foreach (var stack in itemStacks)
            stack?.ResetBattleUsage();
    }

    private bool IsStackUsable(InventoryItemStack stack)
    {
        if (stack == null || stack.item == null)
            return false;

        if (!stack.item.isUsableInBattle)
            return false;

        bool hasStock = stack.item.consumeOnUse ? stack.quantity > 0 : true;
        if (!hasStock)
            return false;

        if (stack.item.maxUsesPerTurn > 0 && stack.usesThisTurn >= stack.item.maxUsesPerTurn)
            return false;

        if (stack.item.maxUsesPerBattle > 0 && stack.usesThisBattle >= stack.item.maxUsesPerBattle)
            return false;

        return true;
    }

    private void RebuildLookup()
    {
        stackLookup.Clear();
        itemStacks.RemoveAll(stack => stack == null || stack.item == null);

        foreach (var stack in itemStacks)
            stackLookup[stack.item] = stack;
    }

    private void SortStacks()
    {
        itemStacks.Sort((a, b) =>
        {
            string aName = a?.item != null ? a.item.itemName : string.Empty;
            string bName = b?.item != null ? b.item.itemName : string.Empty;
            return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        });
    }
}
