// Role:
// ScriptableObject audio profile for a gameplay zone.
// Usage:
// Assigned to Zone components to choose music and ambience while the player is inside.
// Responsibilities:
// Store optional music and ambience clips. Empty values mean the zone can inherit or keep current audio.
// Dependencies:
// AudioClipSO and Zone/AudioManager runtime code.
// Precautions:
// Keep this data-only; playback rules belong in Zone and AudioManager.
using UnityEngine;

/// <summary>
/// Reusable music/ambience profile for a zone.
/// </summary>
[CreateAssetMenu(fileName = "ZoneAudioProfile", menuName = "Scriptable Objects/Audio/Zone Audio Profile")]
public class ZoneAudioProfileSO : ScriptableObject
{
    [Header("Music")]
    /// <summary>Music played while this zone is active, if assigned.</summary>
    [Tooltip("Musique jouee tant que cette zone est active. Laisse vide pour conserver celle d'une zone parente.")]
    public AudioClipSO music;

    [Header("Ambience")]
    /// <summary>Ambience played while this zone is active, if assigned.</summary>
    [Tooltip("Ambiance sonore jouee tant que cette zone est active. Laisse vide pour conserver celle d'une zone parente.")]
    public AudioClipSO ambience;
}
