using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gestionnaire audio global (musique de zones + one-shots).
public class AudioManager : MonoBehaviour
{
    private const string MusicVolumePrefsKey = "settings.audio.music_volume";
    private const string SfxVolumePrefsKey = "settings.audio.sfx_volume";
    private const float DefaultChannelVolume = 1f;

    private struct ManagedSfxSource
    {
        public AudioSource source;
        public float clipVolume;

        public ManagedSfxSource(AudioSource audioSource, float baseClipVolume)
        {
            source = audioSource;
            clipVolume = baseClipVolume;
        }
    }

    public static AudioManager Instance { get; private set; }

    [Header("Mix")]
    [Range(0f, 1f), Tooltip("Volume master applique a tout.")]
    public float masterVolume = 1f;
    [Range(0f, 1f), Tooltip("Volume dedie a la musique.")]
    public float musicVolume = DefaultChannelVolume;
    [Range(0f, 1f), Tooltip("Volume dedie aux sons du jeu.")]
    public float sfxVolume = DefaultChannelVolume;
    [Tooltip("Duree du crossfade de musique.")]
    public float fadeDuration = 1f;
    [Tooltip("Ne pas detruire au changement de scene.")]
    public bool dontDestroyOnLoad = true;

    [Header("One Shot")]
    [Range(0f, 1f), Tooltip("Spatial blend des one-shots.")]
    public float oneShotSpatialBlend = 1f;
    [Tooltip("Distance min des one-shots.")]
    public float oneShotMinDistance = 1f;
    [Tooltip("Distance max des one-shots.")]
    public float oneShotMaxDistance = 25f;

    private AudioSource primarySource;
    private AudioSource secondarySource;
    private AudioSource activeSource;
    private AudioSource inactiveSource;
    private AudioClipSO activeClip;
    private Coroutine fadeRoutine;
    private readonly List<Zone> zoneStack = new List<Zone>();
    private readonly List<ManagedSfxSource> activeSfxSources = new List<ManagedSfxSource>();
    private int musicDuckCount;
    private float musicDuckMultiplier = 1f;

    public float MusicVolume => Mathf.Clamp01(musicVolume);
    public float SfxVolume => Mathf.Clamp01(sfxVolume);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadSavedVolumes();
        ClampMixSettings();

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnValidate()
    {
        ClampMixSettings();
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshMusicVolume();
        RefreshSfxVolumes();
    }

    public static float GetSavedMusicVolume()
    {
        return LoadSavedVolume(MusicVolumePrefsKey, DefaultChannelVolume);
    }

    public static float GetSavedSfxVolume()
    {
        return LoadSavedVolume(SfxVolumePrefsKey, DefaultChannelVolume);
    }

    public static void SaveMusicVolumePreference(float value)
    {
        SaveVolume(MusicVolumePrefsKey, value);
    }

    public static void SaveSfxVolumePreference(float value)
    {
        SaveVolume(SfxVolumePrefsKey, value);
    }

    public void SetMusicVolume(float value, bool save = true)
    {
        float clamped = Mathf.Clamp01(value);
        musicVolume = clamped;
        if (save)
        {
            SaveMusicVolumePreference(clamped);
        }

        RefreshMusicVolume();
    }

    public void SetSfxVolume(float value, bool save = true)
    {
        float clamped = Mathf.Clamp01(value);
        sfxVolume = clamped;
        if (save)
        {
            SaveSfxVolumePreference(clamped);
        }

        RefreshSfxVolumes();
    }

    public void AdjustMusicVolume(float delta, bool save = true)
    {
        SetMusicVolume(MusicVolume + delta, save);
    }

    public void AdjustSfxVolume(float delta, bool save = true)
    {
        SetSfxVolume(SfxVolume + delta, save);
    }

    public void RegisterZoneEnter(Zone zone)
    {
        if (zone == null)
        {
            return;
        }

        // Ajoute la zone en haut de pile.
        zoneStack.Remove(zone);
        zoneStack.Add(zone);
        UpdateZoneMusic();
    }

    public void RegisterZoneExit(Zone zone)
    {
        if (zone == null)
        {
            return;
        }

        // Retire la zone et recalcule la musique courante.
        zoneStack.Remove(zone);
        UpdateZoneMusic();
    }

    public void PlayMusic(AudioClipSO clip)
    {
        EnsureMusicSources();

        // Ignore si deja en cours avec le meme clip.
        if (clip == activeClip && activeSource != null && activeSource.isPlaying)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeToClip(clip));
    }

    public void BeginMusicDucking(float multiplier)
    {
        multiplier = Mathf.Clamp01(multiplier);
        musicDuckCount++;
        if (musicDuckCount == 1)
        {
            musicDuckMultiplier = multiplier;
        }
        else
        {
            musicDuckMultiplier = Mathf.Min(musicDuckMultiplier, multiplier);
        }

        RefreshMusicVolume();
    }

    public void EndMusicDucking()
    {
        if (musicDuckCount <= 0)
        {
            return;
        }

        musicDuckCount = Mathf.Max(0, musicDuckCount - 1);
        if (musicDuckCount == 0)
        {
            musicDuckMultiplier = 1f;
        }

        RefreshMusicVolume();
    }

    public AudioSource PlayClip(AudioClipSO clip, Vector3 position)
    {
        if (clip == null || clip.audioClip == null)
        {
            return null;
        }

        // Joue un one-shot spatialise.
        AudioSource source = CreateSource("OneShot_" + clip.name);
        ConfigureOneShotSource(source);
        source.transform.position = position;
        source.clip = clip.audioClip;
        source.loop = clip.loop;
        source.volume = GetSfxSourceVolume(clip);
        source.Play();
        RegisterSfxSource(source, clip);

        if (!clip.loop)
        {
            StartCoroutine(DestroyAfterPlay(source, clip.audioClip.length));
        }

        return source;
    }

    private void UpdateZoneMusic()
    {
        // Choisit la derniere zone valide de la pile.
        CleanupNullZones();

        AudioClipSO nextClip = null;
        for (int i = zoneStack.Count - 1; i >= 0; i--)
        {
            Zone zone = zoneStack[i];
            if (zone == null)
            {
                continue;
            }

            if (!zone.playZoneMusic || zone.zoneMusic == null)
            {
                continue;
            }

            nextClip = zone.zoneMusic;
            break;
        }

        PlayMusic(nextClip);
    }

    private void CleanupNullZones()
    {
        for (int i = zoneStack.Count - 1; i >= 0; i--)
        {
            if (zoneStack[i] == null)
            {
                zoneStack.RemoveAt(i);
            }
        }
    }

    private IEnumerator FadeToClip(AudioClipSO clip)
    {
        EnsureMusicSources();

        float duration = Mathf.Max(0.01f, fadeDuration);
        AudioSource from = activeSource;
        AudioSource to = inactiveSource;
        AudioClipSO previousClip = activeClip;

        if (clip == null || clip.audioClip == null)
        {
            // Fade vers silence si aucun clip valide.
            if (from != null && from.isPlaying)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    float t = elapsed / duration;
                    float multiplier = GetMusicMultiplier();
                    from.volume = Mathf.Lerp(GetMusicSourceVolume(previousClip) * multiplier, 0f, t);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                from.volume = 0f;
                from.Stop();
            }

            activeClip = null;
            yield break;
        }

        if (to == null)
        {
            to = from;
        }

        if (to != null)
        {
            // Demarre la nouvelle musique en volume 0.
            to.clip = clip.audioClip;
            to.loop = clip.loop;
            to.volume = 0f;
            to.Play();
        }

        float time = 0f;
        while (time < duration)
        {
            float t = time / duration;
            float multiplier = GetMusicMultiplier();
            if (to != null)
            {
                to.volume = Mathf.Lerp(0f, GetMusicSourceVolume(clip) * multiplier, t);
            }

            if (from != null)
            {
                from.volume = Mathf.Lerp(GetMusicSourceVolume(previousClip) * multiplier, 0f, t);
            }

            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (to != null)
        {
            to.volume = GetMusicSourceVolume(clip) * GetMusicMultiplier();
        }

        if (from != null)
        {
            from.volume = 0f;
            if (from.isPlaying)
            {
                from.Stop();
            }
        }

        activeSource = to;
        inactiveSource = from;
        activeClip = clip;
    }

    private void LoadSavedVolumes()
    {
        musicVolume = GetSavedMusicVolume();
        sfxVolume = GetSavedSfxVolume();
    }

    private void ClampMixSettings()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        oneShotSpatialBlend = Mathf.Clamp01(oneShotSpatialBlend);
        oneShotMinDistance = Mathf.Max(0f, oneShotMinDistance);
        oneShotMaxDistance = Mathf.Max(oneShotMinDistance, oneShotMaxDistance);
    }

    private static float LoadSavedVolume(string key, float fallback)
    {
        return PlayerPrefs.HasKey(key) ? Mathf.Clamp01(PlayerPrefs.GetFloat(key)) : fallback;
    }

    private static void SaveVolume(string key, float value)
    {
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    private float GetMusicMultiplier()
    {
        return musicDuckCount > 0 ? Mathf.Clamp01(musicDuckMultiplier) : 1f;
    }

    private float GetMusicSourceVolume(AudioClipSO clip)
    {
        if (clip == null)
        {
            return 0f;
        }

        return Mathf.Clamp01(clip.volume) * Mathf.Clamp01(masterVolume) * MusicVolume;
    }

    private float GetSfxSourceVolume(AudioClipSO clip)
    {
        if (clip == null)
        {
            return 0f;
        }

        return GetSfxSourceVolume(Mathf.Clamp01(clip.volume));
    }

    private float GetSfxSourceVolume(float clipVolume)
    {
        return clipVolume * Mathf.Clamp01(masterVolume) * SfxVolume;
    }

    private void RefreshMusicVolume()
    {
        if (activeSource == null || activeClip == null || activeClip.audioClip == null)
        {
            return;
        }

        activeSource.volume = GetMusicSourceVolume(activeClip) * GetMusicMultiplier();
    }

    private void RefreshSfxVolumes()
    {
        CleanupTrackedSfxSources();
        for (int i = 0; i < activeSfxSources.Count; i++)
        {
            ManagedSfxSource entry = activeSfxSources[i];
            if (entry.source == null)
            {
                continue;
            }

            entry.source.volume = GetSfxSourceVolume(entry.clipVolume);
        }
    }

    private void RegisterSfxSource(AudioSource source, AudioClipSO clip)
    {
        if (source == null)
        {
            return;
        }

        float clipVolume = clip != null ? Mathf.Clamp01(clip.volume) : 1f;
        CleanupTrackedSfxSources();

        for (int i = 0; i < activeSfxSources.Count; i++)
        {
            if (activeSfxSources[i].source == source)
            {
                activeSfxSources[i] = new ManagedSfxSource(source, clipVolume);
                return;
            }
        }

        activeSfxSources.Add(new ManagedSfxSource(source, clipVolume));
    }

    private void CleanupTrackedSfxSources()
    {
        for (int i = activeSfxSources.Count - 1; i >= 0; i--)
        {
            if (activeSfxSources[i].source == null)
            {
                activeSfxSources.RemoveAt(i);
            }
        }
    }

    private void UnregisterSfxSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = activeSfxSources.Count - 1; i >= 0; i--)
        {
            if (activeSfxSources[i].source == source)
            {
                activeSfxSources.RemoveAt(i);
            }
        }
    }

    private IEnumerator DestroyAfterPlay(AudioSource source, float duration)
    {
        if (source == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(duration);
        if (source != null)
        {
            UnregisterSfxSource(source);
            Destroy(source.gameObject);
        }
    }

    private void EnsureMusicSources()
    {
        if (primarySource == null)
        {
            primarySource = CreateSource("Music_A");
        }

        if (secondarySource == null)
        {
            secondarySource = CreateSource("Music_B");
        }

        ConfigureMusicSource(primarySource);
        ConfigureMusicSource(secondarySource);

        if (activeSource == null || inactiveSource == null)
        {
            activeSource = primarySource;
            inactiveSource = secondarySource;
        }
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        return child.AddComponent<AudioSource>();
    }

    private void ConfigureMusicSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    private void ConfigureOneShotSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.spatialBlend = Mathf.Clamp01(oneShotSpatialBlend);
        source.minDistance = oneShotMinDistance;
        source.maxDistance = oneShotMaxDistance;
    }
}
