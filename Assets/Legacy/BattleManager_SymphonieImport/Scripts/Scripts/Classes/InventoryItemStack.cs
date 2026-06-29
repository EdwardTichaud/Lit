using System;
using UnityEngine;

/// <summary>
/// Représente une pile d'un même <see cref="ItemData"/> dans l'inventaire.
/// Cette classe est partagée entre le gestionnaire runtime et l'éditeur pour
/// exposer clairement les quantités disponibles sans dupliquer les références
/// de ScriptableObjects.
/// </summary>
[Serializable]
public class InventoryItemStack
{
    [Tooltip("Référence du ScriptableObject décrivant l'objet.")]
    public ItemData item;

    [Min(0), Tooltip("Nombre d'exemplaires actuellement disponibles.")]
    public int quantity = 1;

    [HideInInspector]
    public int usesThisTurn = 0;

    [HideInInspector]
    public int usesThisBattle = 0;

    public InventoryItemStack()
    {
    }

    public InventoryItemStack(ItemData item, int quantity)
    {
        this.item = item;
        this.quantity = Mathf.Max(0, quantity);
    }

    /// <summary>
    /// Ajoute un certain nombre d'exemplaires à la pile.
    /// </summary>
    public void AddQuantity(int amount)
    {
        if (amount <= 0)
            return;

        quantity = Mathf.Max(0, quantity + amount);
    }

    /// <summary>
    /// Consomme un exemplaire et enregistre l'utilisation.
    /// </summary>
    /// <returns>Vrai si un exemplaire a bien été retiré.</returns>
    public bool RegisterUse(bool consumeItem)
    {
        if (consumeItem)
        {
            if (quantity <= 0)
                return false;

            quantity = Mathf.Max(0, quantity - 1);
        }

        usesThisTurn++;
        usesThisBattle++;
        return true;
    }

    /// <summary>
    /// Réinitialise les compteurs d'utilisation liés au tour en cours.
    /// </summary>
    public void ResetTurnUsage()
    {
        usesThisTurn = 0;
    }

    /// <summary>
    /// Réinitialise l'ensemble des compteurs d'utilisation.
    /// </summary>
    public void ResetBattleUsage()
    {
        usesThisTurn = 0;
        usesThisBattle = 0;
    }
}