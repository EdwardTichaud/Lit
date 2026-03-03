using System.Collections.Generic;
using UnityEngine;

// Donnees d'une ligne de voix (audio + texte).
[CreateAssetMenu(fileName = "VoiceLine", menuName = "Scriptable Objects/Audio/Voice Line")]
public class VoiceLineData : ScriptableObject
{
    [System.Serializable]
    public class VoiceLineTextCue
    {
        [Tooltip("Temps (secondes) depuis le debut de l'audio.")]
        public float time;
        [TextArea]
        [Tooltip("Texte affiche a ce moment.")]
        public string text;
    }

    [Header("Identity")]
    [Tooltip("Index utilise pour retrouver la ligne.")]
    public int voiceLineIndex;

    [Header("Audio")]
    [Tooltip("Clip audio associe a la ligne.")]
    public AudioClipSO voiceLineAudioClip;

    [Header("Text")]
    [TextArea]
    [Tooltip("Texte affiche au dessus du personnage.")]
    public string voiceLineText;

    [Header("Text Cues")]
    [Tooltip("Sequence de textes synchronises sur l'audio (optionnel).")]
    public List<VoiceLineTextCue> voiceLineCues = new List<VoiceLineTextCue>();
}
