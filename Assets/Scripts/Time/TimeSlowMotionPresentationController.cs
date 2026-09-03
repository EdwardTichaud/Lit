using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Presentation-only observer for TimeManager global slow motion. It never
/// changes Unity time: TimeManager calls SetGlobalScale whenever its effective
/// global scale changes.
/// </summary>
[DisallowMultipleComponent]
public sealed class TimeSlowMotionPresentationController : MonoBehaviour
{
    private GameObject runtimeVolumeObject;
    private Volume runtimeVolume;
    private VolumeProfile runtimeProfile;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private TimeSlowMotionPresentationSettings settings;
    private bool initialized;
    private bool slowMotionActive;
    private float targetWeight;
    private float currentWeight;

    public void SetGlobalScale(float globalScale, TimeSlowMotionPresentationSettings nextSettings, bool emitTransitions)
    {
        settings = nextSettings;
        bool shouldPresent = settings != null && settings.enabled && globalScale > 0f && globalScale < 0.999f;
        float nextCameraWeight = shouldPresent && settings.cameraEffectsEnabled ? 1f : 0f;

        if (!initialized)
        {
            initialized = true;
            slowMotionActive = shouldPresent;
            targetWeight = nextCameraWeight;
            currentWeight = targetWeight;
            ApplyCameraEffect();
            return;
        }

        bool presentationChanged = slowMotionActive != shouldPresent;
        bool cameraChanged = !Mathf.Approximately(targetWeight, nextCameraWeight);
        slowMotionActive = shouldPresent;
        targetWeight = nextCameraWeight;
        if (!presentationChanged && !cameraChanged)
        {
            return;
        }

        if (!emitTransitions)
        {
            currentWeight = targetWeight;
            ApplyCameraEffect();
            return;
        }

        if (!presentationChanged)
        {
            return;
        }

        if (shouldPresent)
        {
            PlayEnterFeedback();
        }
        else
        {
            PlayExitFeedback();
        }
    }

    private void Update()
    {
        if (settings == null || Mathf.Approximately(currentWeight, targetWeight))
        {
            return;
        }

        float blendSeconds = Mathf.Max(0.01f, targetWeight > currentWeight
            ? settings.cameraEnterBlendSeconds
            : settings.cameraExitBlendSeconds);
        currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, Time.unscaledDeltaTime / blendSeconds);
        ApplyCameraEffect();
    }

    public void ClearImmediate()
    {
        initialized = false;
        slowMotionActive = false;
        targetWeight = 0f;
        currentWeight = 0f;
        if (runtimeVolume != null)
        {
            runtimeVolume.weight = 0f;
        }
    }

    private void PlayEnterFeedback()
    {
        if (settings.cameraEffectsEnabled)
        {
            ScreenWaveController wave = settings.screenWave != null
                ? settings.screenWave
                : ScreenWaveController.EnsureInstance();
            wave?.PlayScreenWavePhase(settings.entryScreenWave);
        }
        AudioManager.EnsureInstance()?.PlayUiOneShotClip(settings.enterSfx);
    }

    private void PlayExitFeedback()
    {
        AudioManager.EnsureInstance()?.PlayUiOneShotClip(settings.exitSfx);
    }

    private void ApplyCameraEffect()
    {
        if (settings == null || currentWeight <= 0f)
        {
            if (runtimeVolume != null) runtimeVolume.weight = 0f;
            return;
        }

        EnsureRuntimeVolume();
        runtimeVolume.weight = currentWeight;
        vignette.intensity.value = Mathf.Clamp01(settings.vignetteIntensity);
        vignette.smoothness.value = Mathf.Clamp01(settings.vignetteSmoothness);
        chromaticAberration.intensity.value = Mathf.Clamp01(settings.chromaticAberrationIntensity);
    }

    private void EnsureRuntimeVolume()
    {
        if (runtimeVolume != null)
        {
            return;
        }

        runtimeVolumeObject = new GameObject("TimeManager Slow Motion Camera Volume");
        runtimeVolumeObject.transform.SetParent(transform, false);
        runtimeVolumeObject.hideFlags = HideFlags.HideAndDontSave;
        runtimeVolume = runtimeVolumeObject.AddComponent<Volume>();
        runtimeVolume.isGlobal = true;
        runtimeVolume.priority = 1000f;
        runtimeVolume.weight = 0f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
        runtimeVolume.sharedProfile = runtimeProfile;

        vignette = runtimeProfile.Add<Vignette>(true);
        vignette.mode.overrideState = true;
        vignette.mode.value = VignetteMode.Procedural;
        vignette.color.overrideState = true;
        vignette.color.value = Color.black;
        vignette.center.overrideState = true;
        vignette.center.value = new Vector2(0.5f, 0.5f);
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        vignette.roundness.overrideState = true;
        vignette.roundness.value = 1f;
        vignette.rounded.overrideState = true;
        vignette.rounded.value = false;

        chromaticAberration = runtimeProfile.Add<ChromaticAberration>(true);
        chromaticAberration.intensity.overrideState = true;
        chromaticAberration.maxSamples = 6;
    }

    private void OnDestroy()
    {
        if (runtimeProfile != null)
        {
            Destroy(runtimeProfile);
        }
    }
}

[Serializable]
public sealed class TimeSlowMotionPresentationSettings
{
    public bool enabled = true;
    [Tooltip("Desactive temporairement onde, vignette et aberration du ralenti sans couper ses SFX.")]
    public bool cameraEffectsEnabled;
    [Tooltip("Optionnel : utilise le ScreenWaveController actif si vide.")]
    public ScreenWaveController screenWave;
    [Header("Slow Motion Distortion")]
    [Tooltip("Deformation lente et large. Les impacts de combat gardent leurs ondes rapides et localisees.")]
    public ScreenWaveController.ScreenWaveSettings entryScreenWave = SlowMotionDeformation;
    [Min(0.01f)] public float cameraEnterBlendSeconds = 0.08f;
    [Min(0.01f)] public float cameraExitBlendSeconds = 0.14f;
    [Range(0f, 1f)] public float vignetteIntensity = 0.2f;
    [Range(0f, 1f)] public float vignetteSmoothness = 0.7f;
    [Range(0f, 1f)] public float chromaticAberrationIntensity = 0.18f;
    [Tooltip("SFX 2D joue une seule fois au passage en ralenti.")]
    public AudioClipSO enterSfx;
    [Tooltip("SFX 2D joue une seule fois au retour a la vitesse normale.")]
    public AudioClipSO exitSfx;

    private static ScreenWaveController.ScreenWaveSettings SlowMotionDeformation => new ScreenWaveController.ScreenWaveSettings
    {
        origin = new Vector2(0.5f, 0.5f),
        direction = Vector2.zero,
        reverse = false,
        // One broad, image-warping swell rather than the tight ripples used by impacts.
        frequency = 0.8f,
        propagationSpeed = 0.3f,
        amplitude = 0.25f,
        duration = 1.35f,
        falloff = 0.45f,
        fadeOutDuration = 0.5f,
        highlightIntensity = 0f,
        edgeContrast = 1f,
        highlightColor = Color.white
    };
}
