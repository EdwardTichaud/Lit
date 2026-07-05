// Role:
// Central ScriptableObject mapping combat presentation audio cues to reusable AudioClipSO assets.
// Usage:
// Assigned on AudioManager so combat systems can resolve transition music/SFX without Resources.Load.
// Responsibilities:
// Keep combat audio references in one serialized place.
// Dependencies:
// AudioClipSO.
using UnityEngine;

/// <summary>
/// Identifiers for combat presentation audio.
/// </summary>
public enum CombatAudioCue
{
    None = 0,
    CombatMusic = 1,
    EnterTransition = 2,
    ExitTransition = 3,
    Accent = 4,
    GameOverMusic = 5,
}

/// <summary>
/// ScriptableObject library resolving combat audio cues to AudioClipSO assets.
/// </summary>
[CreateAssetMenu(fileName = "CombatAudioLibrary", menuName = "Scriptable Objects/Audio/Combat Audio Library")]
public class CombatAudioLibrarySO : ScriptableObject
{
    [Header("Music")]
    /// <summary>Music override used while combat is active or nearby.</summary>
    public AudioClipSO combatMusic;
    /// <summary>Music override used during defeat resolution and Game Over.</summary>
    public AudioClipSO gameOverMusic;

    [Header("Transition SFX")]
    /// <summary>Clip played when entering combat.</summary>
    public AudioClipSO enterTransition;
    /// <summary>Clip played when leaving combat.</summary>
    public AudioClipSO exitTransition;
    /// <summary>Short accent layered on top of combat entry.</summary>
    public AudioClipSO accent;

    /// <summary>
    /// Returns the clip configured for the given combat cue, or null if none is configured.
    /// </summary>
    public AudioClipSO Resolve(CombatAudioCue cue)
    {
        switch (cue)
        {
            case CombatAudioCue.CombatMusic: return combatMusic;
            case CombatAudioCue.GameOverMusic: return gameOverMusic;
            case CombatAudioCue.EnterTransition: return enterTransition;
            case CombatAudioCue.ExitTransition: return exitTransition;
            case CombatAudioCue.Accent: return accent;
            default: return null;
        }
    }
}
