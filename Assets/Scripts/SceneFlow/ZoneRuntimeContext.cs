using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Contexte runtime local a une zone. Il vit dans la scene Core et applique les
/// donnees de presentation du ZoneManifest aux services persistants.
/// </summary>
[DisallowMultipleComponent]
public sealed class ZoneRuntimeContext : MonoBehaviour
{
    private int audioPresentationToken;
    private int volumeProfileToken;
    private Volume runtimeVolume;
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

        if (manifest.VolumeProfile == null)
        {
            return;
        }

        EnvironmentManager environmentManager = FindAnyObjectByType<EnvironmentManager>();
        if (environmentManager != null)
        {
            volumeProfileToken = environmentManager.PushBaseProfileOverride(manifest.VolumeProfile);
        }
        else
        {
            GameObject volumeHost = new GameObject("ZonePresentationVolume");
            volumeHost.transform.SetParent(transform, false);
            runtimeVolume = volumeHost.AddComponent<Volume>();
            runtimeVolume.isGlobal = true;
            runtimeVolume.priority = 0f;
            runtimeVolume.weight = 1f;
            runtimeVolume.sharedProfile = manifest.VolumeProfile;
        }
    }

    /// <summary>
    /// Libere la presentation visuelle et, sauf transition directe vers une
    /// autre zone, la presentation audio. Conserver l'audio permet a
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

        if (volumeProfileToken != 0)
        {
            EnvironmentManager environmentManager = FindAnyObjectByType<EnvironmentManager>();
            if (environmentManager != null)
            {
                environmentManager.PopBaseProfileOverride(volumeProfileToken);
            }
        }

        if (runtimeVolume != null)
        {
            Destroy(runtimeVolume.gameObject);
            runtimeVolume = null;
        }

        // Le prochain PushZonePresentation remplacera atomiquement ce token.
        // L'ancien contexte ne doit jamais le retirer pendant son OnDestroy.
        audioPresentationToken = 0;
        volumeProfileToken = 0;
        configured = false;
    }

    private void OnDestroy()
    {
        ReleasePresentation();
    }
}
