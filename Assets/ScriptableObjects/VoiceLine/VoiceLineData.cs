// Role:
// ScriptableObject data for one voiced line and optional timed text cues.
// Usage:
// Referenced by LocalVoiceLineController and voice line assets.
// Responsibilities:
// Store audio, fallback text, and optional cue timing for subtitles or floating text.
// Dependencies:
// AudioClipSO and UI systems that display voice text.
// Precautions:
// Keep cue times aligned with the audio clip when editing localized text.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data asset for one voice line.
/// </summary>
[CreateAssetMenu(fileName = "VoiceLine", menuName = "Scriptable Objects/Audio/Voice Line")]
public class VoiceLineData : ScriptableObject
{
    /// <summary>
    /// Timed text cue displayed at a specific time in the audio.
    /// </summary>
    [System.Serializable]
    public class VoiceLineTextCue
    {
        /// <summary>Seconds since the start of the audio clip.</summary>
        [Tooltip("Temps (secondes) depuis le debut de l'audio.")]
        public float time;
        /// <summary>Text displayed at this cue time.</summary>
        [TextArea]
        [Tooltip("Texte affiche a ce moment.")]
        public string text;
    }

    [Header("Identity")]
    /// <summary>Index used by systems that look up a voice line numerically.</summary>
    [Tooltip("Index utilise pour retrouver la ligne.")]
    public int voiceLineIndex;

    [Header("Audio")]
    /// <summary>Audio clip data for this voice line.</summary>
    [Tooltip("Clip audio associe a la ligne.")]
    public AudioClipSO voiceLineAudioClip;

    [Header("Text")]
    /// <summary>Fallback text displayed above the character.</summary>
    [TextArea]
    [Tooltip("Texte affiche au dessus du personnage.")]
    public string voiceLineText;

    [Header("Text Cues")]
    /// <summary>Optional sequence of timed text cues synchronized with audio.</summary>
    [Tooltip("Sequence de textes synchronises sur l'audio (optionnel).")]
    public List<VoiceLineTextCue> voiceLineCues = new List<VoiceLineTextCue>();
}
