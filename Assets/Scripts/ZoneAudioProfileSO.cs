using UnityEngine;

[CreateAssetMenu(fileName = "ZoneAudioProfile", menuName = "Scriptable Objects/Audio/Zone Audio Profile")]
// Profil audio reutilisable pour une zone: musique + ambiance.
public class ZoneAudioProfileSO : ScriptableObject
{
    [Header("Music")]
    [Tooltip("Musique jouee tant que cette zone est active. Laisse vide pour conserver celle d'une zone parente.")]
    public AudioClipSO music;

    [Header("Ambience")]
    [Tooltip("Ambiance sonore jouee tant que cette zone est active. Laisse vide pour conserver celle d'une zone parente.")]
    public AudioClipSO ambience;
}
