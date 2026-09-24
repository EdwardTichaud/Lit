using UnityEngine;

/// <summary>
/// Contexte runtime local a une zone. Il vit dans la scene Core et applique la
/// presentation audio du ZoneManifest aux services persistants.
/// </summary>
[DisallowMultipleComponent]
public sealed class ZoneRuntimeContext : MonoBehaviour
{
    private int audioPresentationToken;
    private bool configured;

    public void Configure(ZoneManifest manifest)
    {
        if (configured || manifest == null)
        {
            return;
        }

        configured = true;

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioPresentationToken = audioManager.PushZonePresentation(
                manifest.Music,
                manifest.Ambience,
                manifest.PresentationAudioFadeDuration);
        }
        else
        {
            Debug.LogWarning("[ZonePresentation] AudioManager introuvable : presentation audio non appliquee.", this);
        }

    }

    /// <summary>
    /// Libere, sauf transition directe vers une autre zone, la presentation
    /// audio. Conserver l'audio permet a
    /// AudioManager de comparer la piste courante avec celle de destination.
    /// </summary>
    public void ReleasePresentation(bool preserveAudio = false)
    {
        if (!configured)
        {
            return;
        }

        if (!preserveAudio && audioPresentationToken != 0 && AudioManager.Instance != null)
        {
            AudioManager.Instance.PopZonePresentation(audioPresentationToken);
        }

        // Le prochain PushZonePresentation remplacera atomiquement ce token.
        // L'ancien contexte ne doit jamais le retirer pendant son OnDestroy.
        audioPresentationToken = 0;
        configured = false;
    }

    private void OnDestroy()
    {
        ReleasePresentation();
    }
}
