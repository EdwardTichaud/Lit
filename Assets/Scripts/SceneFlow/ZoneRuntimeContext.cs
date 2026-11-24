using UnityEngine;

/// <summary>
/// Contexte runtime local a une zone. Il vit dans la scene Core et applique la
/// presentation audio du ZoneManifest aux services persistants.
/// </summary>
[DisallowMultipleComponent]
public sealed class ZoneRuntimeContext : MonoBehaviour
{
    [SerializeField] private ZoneAudioProfileSO audioProfile;
    [SerializeField] private bool startOnEnable = true;
    [SerializeField, Min(.01f)] private float fadeDuration = 1;
    [Header("Presentation cues")]
    public AudioClipSO titleCardProceedSfx;
    public AudioClipSO menuButtonSfx;
    public System.Collections.Generic.List<AudioClipSO> titleCardParticleSfx = new();
    [Min(0)] public float menuButtonSfxCooldown = .05f;
    private float lastButtonSound = float.NegativeInfinity;
    private int audioPresentationToken;
    private bool configured;

    private void OnEnable() { if (startOnEnable) ActivatePresentation(); }
    public void ActivatePresentation()
    {
        if (audioProfile == null) return;
        if (AudioManager.Instance != null && AudioManager.Instance.OwnsZonePresentation(audioPresentationToken)) return;
        configured = true;
        audioPresentationToken = AudioManager.EnsureInstance().PushZonePresentationLayers(
            audioProfile.music, audioProfile.Ambiences, fadeDuration);
    }

    public void Configure(ZoneManifest manifest)
    {
        if (configured || manifest == null)
        {
            return;
        }

        configured = true;

        AudioManager audioManager = AudioManager.EnsureInstance();
        if (audioManager != null)
        {
            audioPresentationToken = audioManager.PushZonePresentationLayers(
                manifest.Music,
                manifest.Ambiences,
                manifest.PresentationAudioFadeDuration);
        }
        else
        {
            Debug.LogWarning("[ZonePresentation] AudioManager introuvable : presentation audio non appliquee.", this);
        }

    }

    public void PlayTitleConfirm() => PlayCue(titleCardProceedSfx);
    public void PlayMenuButton()
    {
        if (Time.unscaledTime - lastButtonSound < menuButtonSfxCooldown) return;
        lastButtonSound = Time.unscaledTime;
        PlayCue(menuButtonSfx);
    }
    public void PlayTitleParticles()
    {
        foreach (var clip in titleCardParticleSfx) PlayCue(clip);
    }
    private void PlayCue(AudioClipSO clip)
    {
        if (isActiveAndEnabled && clip != null) AudioManager.EnsureInstance().PlayUiClip(clip);
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
    private void OnDisable() => ReleasePresentation();
}
