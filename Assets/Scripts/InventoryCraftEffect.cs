using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryCraftEffect", menuName = "Scriptable Objects/Effects/Inventory Craft")]
// Craft simple directement depuis l'inventaire lors de l'utilisation d'un item.
public class InventoryCraftEffect : Effect
{
    [System.Serializable]
    public class Ingredient
    {
        [Tooltip("Item requis.")]
        public Item item;
        [Min(1)]
        [Tooltip("Quantite requise.")]
        public int quantity = 1;
    }

    [Header("Input")]
    [Tooltip("Ingredients requis pour le craft.")]
    public List<Ingredient> ingredients = new List<Ingredient>();

    [Header("Output")]
    [Tooltip("Item cree.")]
    public Item outputItem;
    [Min(1)]
    [Tooltip("Quantite creee.")]
    public int outputQuantity = 1;

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

    public override string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        return BuildRecipeDescription();
    }

    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

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
