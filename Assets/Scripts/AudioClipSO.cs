// Role:
// ScriptableObject wrapper around a Unity AudioClip and basic playback metadata.
// Usage:
// Referenced by AudioManager, action audio libraries, voice lines, zones, and effects.
// Responsibilities:
// Store a reusable clip, display metadata, volume, and loop flag.
// Dependencies:
// Unity AudioClip.
// Precautions:
// Do not move assets loaded through Resources without checking Resources.Load paths.
using UnityEngine;

/// <summary>
/// Reusable audio clip data asset.
/// </summary>
[CreateAssetMenu(fileName = "AudioClip", menuName = "Scriptable Objects/Audio/Audio Clip")]
public class AudioClipSO : ScriptableObject
{
    [Header("Identity")]
    /// <summary>Display title for this clip.</summary>
    [Tooltip("Titre affiche pour ce clip.")]
    public string title;
    /// <summary>Optional composer/author credit.</summary>
    [Tooltip("Nom du compositeur (optionnel).")]
    public string composer;

    [Header("Audio")]
    /// <summary>Unity audio clip reference.</summary>
    [Tooltip("Clip Unity reference.")]
    public AudioClip audioClip;
    /// <summary>Default volume multiplier for playback.</summary>
    [Range(0f, 1f), Tooltip("Volume applique au clip.")]
    public float volume = 0.8f;
    /// <summary>Whether this clip should loop by default.</summary>
    [Tooltip("Lecture en boucle.")]
    public bool loop = true;
}
