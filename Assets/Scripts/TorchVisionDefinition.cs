using UnityEngine;

[CreateAssetMenu(fileName = "TorchVision", menuName = "Scriptable Objects/Torch Vision")]
public class TorchVisionDefinition : ScriptableObject
{
    // Perception layer for torch color readings. Temporal age systems live in
    // Assets/Scripts/Temporal.
    [Header("Identity")]
    [Tooltip("Optional id for saves or debugging.")]
    public string visionId;
    [Tooltip("Optional display name for UI.")]
    public string displayName;

    [Header("Light")]
    [Tooltip("If true, restores the torch default settings.")]
    public bool useDefaultLightSettings = false;
    [Tooltip("Light color applied to the torch.")]
    public Color lightColor = Color.white;
    [Tooltip("Disable color temperature when this vision is active.")]
    public bool disableColorTemperature = true;
}
