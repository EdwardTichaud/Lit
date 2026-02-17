using UnityEngine;

[CreateAssetMenu(fileName = "CatalyseurOrbCraftEffect", menuName = "Scriptable Objects/Effects/Catalyseur Orb Craft")]
// Permet de convertir un item d'entree en orbe lors de l'interaction avec le batiment.
public class CatalyseurOrbCraftEffect : Effect, IBuildingInteractEffect
{
    [Header("Input")]
    [Tooltip("Item requis pour la conversion.")]
    [SerializeField] private Item inputItem;
    [Tooltip("Quantite requise pour une conversion.")]
    [SerializeField] private int inputQuantity = 5;

    [Header("Output")]
    [Tooltip("Item cree apres conversion.")]
    [SerializeField] private Item outputItem;
    [Tooltip("Quantite creee apres conversion.")]
    [SerializeField] private int outputQuantity = 1;

    public Item InputItem => inputItem;
    public int InputQuantity => inputQuantity;
    public Item OutputItem => outputItem;
    public int OutputQuantity => outputQuantity;

    public override bool Apply(SquadCharacterController controller, Item item)
    {
        return TryCraft(controller);
    }

    public bool ApplyOnInteract(SquadCharacterController controller, Item building, int currentLevel)
    {
        return TryCraft(controller);
    }

    public override string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        return BuildDescription();
    }

    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    public override string GetBonusDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    private bool TryCraft(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return false;
        }

        if (inputItem == null || outputItem == null)
        {
            return false;
        }

        int inputCount = Mathf.Max(1, inputQuantity);
        int outputCount = Mathf.Max(1, outputQuantity);
        if (!controller.TryRemoveItemQuantity(inputItem, inputCount))
        {
            return false;
        }
        controller.AddItem(outputItem, outputCount);
        return true;
    }

    private string BuildDescription()
    {
        if (inputItem == null || outputItem == null)
        {
            return "Conversion d'orbe lumineuse";
        }

        string inputName = !string.IsNullOrWhiteSpace(inputItem.itemName) ? inputItem.itemName : inputItem.name;
        string outputName = !string.IsNullOrWhiteSpace(outputItem.itemName) ? outputItem.itemName : outputItem.name;
        int inputCount = Mathf.Max(1, inputQuantity);
        int outputCount = Mathf.Max(1, outputQuantity);
        return $"{inputCount} {inputName} -> {outputCount} {outputName}";
    }

}
