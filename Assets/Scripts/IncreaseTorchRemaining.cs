using UnityEngine;

[CreateAssetMenu(fileName = "IncreaseTorchRemaining", menuName = "Scriptable Objects/Effects/Increase Torch Remaining")]
public class IncreaseTorchRemaining : Effect
{
    [Header("Settings")]
    [Tooltip("Secondes ajoutees par application de l'effet.")]
    [SerializeField] private int addedSeconds = 60;

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

        if (!controller.TryRemoveItem(item, 1))
        {
            return false;
        }

        controller.AddTorchSeconds(addedSeconds);
        return true;
    }

    public override string GetDescriptionForLevel(int level)
    {
        int value = Mathf.Max(1, addedSeconds);
        return $"+{value}s torche";
    }

    public override string GetBonusDescriptionForLevel(int level)
    {
        int value = Mathf.Max(1, addedSeconds);
        return $"+{value}s";
    }
}
