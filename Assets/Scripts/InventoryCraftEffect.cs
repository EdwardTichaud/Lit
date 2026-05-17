// Role:
// Simple inventory recipe effect that consumes ingredients and grants an output item.
// Usage:
// Assigned to an item that should craft directly when used from inventory.
// Responsibilities:
// Validate all ingredients first, remove them, then add the output item.
// Dependencies:
// Effect, SquadCharacterController inventory methods, Item.
// Precautions:
// The recipe removes ingredients sequentially after validation. If inventory logic changes,
// keep this two-step pattern to avoid partial craft failures.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Crafts an output item directly from inventory ingredients.
/// </summary>
[CreateAssetMenu(fileName = "InventoryCraftEffect", menuName = "Scriptable Objects/Effects/Inventory Craft")]
public class InventoryCraftEffect : Effect
{
    /// <summary>
    /// One required ingredient for an inventory craft recipe.
    /// </summary>
    [System.Serializable]
    public class Ingredient
    {
        /// <summary>Required item type.</summary>
        [Tooltip("Item requis.")]
        public Item item;
        /// <summary>Required quantity of this item.</summary>
        [Min(1)]
        [Tooltip("Quantite requise.")]
        public int quantity = 1;
    }

    [Header("Input")]
    /// <summary>Ingredients consumed by this recipe.</summary>
    [Tooltip("Ingredients requis pour le craft.")]
    public List<Ingredient> ingredients = new List<Ingredient>();

    [Header("Output")]
    /// <summary>Item granted after a successful craft.</summary>
    [Tooltip("Item cree.")]
    public Item outputItem;
    /// <summary>Quantity granted after a successful craft.</summary>
    [Min(1)]
    [Tooltip("Quantite creee.")]
    public int outputQuantity = 1;

    /// <summary>
    /// Applies the craft recipe to the controller inventory.
    /// </summary>
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null || outputItem == null || ingredients == null || ingredients.Count == 0)
        {
            return false;
        }

        if (!HasIngredients(controller))
        {
            return false;
        }

        // Remove only after HasIngredients succeeds so the recipe cannot partially consume inputs.
        for (int i = 0; i < ingredients.Count; i++)
        {
            Ingredient ingredient = ingredients[i];
            if (ingredient == null || ingredient.item == null)
            {
                continue;
            }

            int quantity = Mathf.Max(1, ingredient.quantity);
            if (!controller.TryRemoveItemQuantity(ingredient.item, quantity))
            {
                return false;
            }
        }

        controller.AddItem(outputItem, Mathf.Max(1, outputQuantity));
        return true;
    }

    /// <summary>
    /// Returns custom description or a generated recipe text.
    /// </summary>
    public override string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        return BuildRecipeDescription();
    }

    /// <summary>Returns the same description because this recipe does not scale by level.</summary>
    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    /// <summary>Returns the same bonus text because this recipe does not scale by level.</summary>
    public override string GetBonusDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    private bool HasIngredients(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return false;
        }

        for (int i = 0; i < ingredients.Count; i++)
        {
            Ingredient ingredient = ingredients[i];
            if (ingredient == null || ingredient.item == null)
            {
                return false;
            }

            int quantity = Mathf.Max(1, ingredient.quantity);
            if (controller.CountItem(ingredient.item) < quantity)
            {
                return false;
            }
        }

        return true;
    }

    private string BuildRecipeDescription()
    {
        if (ingredients == null || ingredients.Count == 0 || outputItem == null)
        {
            return "Craft d'inventaire";
        }

        List<string> inputParts = new List<string>();
        for (int i = 0; i < ingredients.Count; i++)
        {
            Ingredient ingredient = ingredients[i];
            if (ingredient == null || ingredient.item == null)
            {
                continue;
            }

            string itemName = !string.IsNullOrWhiteSpace(ingredient.item.itemName)
                ? ingredient.item.itemName
                : ingredient.item.name;
            inputParts.Add($"{Mathf.Max(1, ingredient.quantity)} {itemName}");
        }

        string outputName = !string.IsNullOrWhiteSpace(outputItem.itemName)
            ? outputItem.itemName
            : outputItem.name;

        if (inputParts.Count == 0)
        {
            return outputName;
        }

        return $"{string.Join(" + ", inputParts)} -> {Mathf.Max(1, outputQuantity)} {outputName}";
    }
}
