using UnityEngine;

[CreateAssetMenu(fileName = "ToggleTorchEffect", menuName = "Scriptable Objects/Effects/Toggle Torch")]
public class ToggleTorchEffect : Effect
{
    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null || item == null)
        {
            return false;
        }

        if (!controller.HasTorchItem)
        {
            return false;
        }

        bool wasEquipped = controller.IsTorchEquipped;
        controller.ToggleTorch();
        return controller.IsTorchEquipped != wasEquipped;
    }

    public override string GetDescriptionForLevel(int level)
    {
        return "Allume/eteint la torche";
    }

    public override string GetBonusDescriptionForLevel(int level)
    {
        return "Allume/eteint la torche";
    }
}
