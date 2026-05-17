// Role:
// ScriptableObject data for the older torch color-vision perception layer.
// Usage:
// Referenced by TorchVisionSystem, TorchVisionSensitive, ApplyTorchVisionEffect,
// TorchLightReceiver, and related assets in ScriptableObjects/TorchVisions.
// Responsibilities:
// Store vision identity and light appearance settings.
// Dependencies:
// Torch vision systems and Unity Light color data.
// Precautions:
// This is separate from TemporalTorch. Do not merge the two systems without migrating scenes.
using UnityEngine;

/// <summary>
/// Data asset describing one torch color vision.
/// </summary>
[CreateAssetMenu(fileName = "TorchVision", menuName = "Scriptable Objects/Torch Vision")]
public class TorchVisionDefinition : ScriptableObject
{
    // Perception layer for torch color readings. Temporal age systems live in
    // Assets/Scripts/Temporal.
    [Header("Identity")]
    /// <summary>Optional stable ID for saves, debugging, or future tooling.</summary>
    [Tooltip("Optional id for saves or debugging.")]
    public string visionId;
    /// <summary>Display name shown by UI and descriptions.</summary>
    [Tooltip("Optional display name for UI.")]
    public string displayName;

    [Header("Light")]
    /// <summary>If true, systems should restore default torch lighting for this vision.</summary>
    [Tooltip("If true, restores the torch default settings.")]
    public bool useDefaultLightSettings = false;
    /// <summary>Light color applied while this vision is active.</summary>
    [Tooltip("Light color applied to the torch.")]
    public Color lightColor = Color.white;
    /// <summary>If true, disables color temperature while this vision is active.</summary>
    [Tooltip("Disable color temperature when this vision is active.")]
    public bool disableColorTemperature = true;
}
