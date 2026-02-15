using UnityEngine;

[CreateAssetMenu(fileName = "AudioClip", menuName = "Scriptable Objects/Audio/Audio Clip")]
// Donnees audio reutilisables (clip + meta + volume).
public class AudioClipSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Titre affiche pour ce clip.")]
    public string title;
    [Tooltip("Nom du compositeur (optionnel).")]
    public string composer;

    [Header("Audio")]
    [Tooltip("Clip Unity reference.")]
    public AudioClip audioClip;
    [Range(0f, 1f), Tooltip("Volume applique au clip.")]
    public float volume = 0.8f;
    [Tooltip("Lecture en boucle.")]
    public bool loop = true;
}
