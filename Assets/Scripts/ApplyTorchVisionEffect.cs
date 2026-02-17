using UnityEngine;

[CreateAssetMenu(fileName = "ApplyTorchVision", menuName = "Scriptable Objects/Effects/Apply Torch Vision")]
public class ApplyTorchVisionEffect : Effect
{
    [Header("Vision")]
    [SerializeField] private TorchVisionDefinition vision;
    [SerializeField] private float durationSeconds = 0f;
    [SerializeField] private bool requireTorchItem = true;
    [SerializeField] private bool requireTorchEquipped = false;

    public override bool Apply(SquadCharacterController controller, Item item)
    {
        if (controller == null || vision == null)
        {
            return false;
        }

        if (requireTorchItem && !controller.HasTorchItem)
        {
            return false;
        }

        if (requireTorchEquipped && !controller.IsTorchEquipped)
        {
            return false;
        }

        TorchVisionSystem.SetVision(vision, durationSeconds);
        return true;
    }

    public override string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription))
        {
            return effectDescription;
        }

        string label = vision != null && !string.IsNullOrWhiteSpace(vision.displayName)
            ? vision.displayName
            : (vision != null ? vision.name : "vision");
        return $"Torche: {label}";
    }

    public override string GetDescriptionForLevel(int level)
    {
        return GetDescription();
    }

    public override string GetBonusDescriptionForLevel(int level)
    {
        return GetDescription();
    }
}
