using UnityEngine;

// Donnees d'une ligne de voix (audio + texte).
[CreateAssetMenu(fileName = "VoiceLine", menuName = "Scriptable Objects/Audio/Voice Line")]
public class VoiceLineData : ScriptableObject
{
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
}
