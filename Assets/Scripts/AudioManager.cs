using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gestionnaire audio global (musique de zones + ambiance + one-shots).
public class AudioManager : MonoBehaviour
{
    private const string MusicVolumePrefsKey = "settings.audio.music_volume";
    private const string SfxVolumePrefsKey = "settings.audio.sfx_volume";
    private const float DefaultChannelVolume = 1f;
    private const string DefaultActionAudioLibraryResourcePath = "Audio/ActionAudioLibrary_Default";

    private struct ManagedSfxSource
    {
        public AudioSource source;
        public float clipVolume;
        public float basePitch;
        public bool affectedByTimeScale;

        public ManagedSfxSource(AudioSource audioSource, float baseClipVolume, float sourceBasePitch, bool sourceAffectedByTimeScale)
        {
            source = audioSource;
            clipVolume = baseClipVolume;
            basePitch = sourceBasePitch;
            affectedByTimeScale = sourceAffectedByTimeScale;
        }
    }

    private struct MusicOverride
    {
        public int token;
        public AudioClipSO clip;
        public bool suppressesAmbience;

        public MusicOverride(int overrideToken, AudioClipSO overrideClip, bool overrideSuppressesAmbience)
        {
            token = overrideToken;
            clip = overrideClip;
            suppressesAmbience = overrideSuppressesAmbience;
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

    [Header("Ambience")]
    [Tooltip("Ambiance jouee lorsqu'aucune zone active n'en fournit une.")]
    public AudioClipSO defaultAmbienceClip;

    [Header("One Shot")]
    [Range(0f, 1f), Tooltip("Spatial blend des one-shots.")]
    public float oneShotSpatialBlend = 1f;
    [Tooltip("Distance min des one-shots.")]
    public float oneShotMinDistance = 1f;
    [Tooltip("Distance max des one-shots.")]
    public float oneShotMaxDistance = 25f;

    [Header("Action Audio")]
    [Tooltip("Librairie des sons d'actions gameplay/UI.")]
    public ActionAudioLibrarySO actionAudioLibrary;
    [Tooltip("Charge la librairie par defaut depuis Resources si aucune reference n'est assignee.")]
    public bool loadDefaultActionAudioLibrary = true;
    [Tooltip("Chemin Resources de la librairie audio d'actions par defaut.")]
    public string defaultActionAudioLibraryResourcePath = DefaultActionAudioLibraryResourcePath;

    [Header("Combat Audio")]
    [Tooltip("Librairie des sons de presentation combat.")]
    public CombatAudioLibrarySO combatAudioLibrary;

    private AudioSource primarySource;
    private AudioSource secondarySource;
    private AudioSource activeSource;
    private AudioSource inactiveSource;
    private AudioClipSO activeClip;
    private Coroutine fadeRoutine;
    private AudioSource primaryAmbienceSource;
    private AudioSource secondaryAmbienceSource;
    private AudioSource activeAmbienceSource;
    private AudioSource inactiveAmbienceSource;
    private AudioClipSO activeAmbienceClip;
    private Coroutine ambienceFadeRoutine;
    private readonly List<Zone> zoneStack = new List<Zone>();
    private readonly List<ManagedSfxSource> activeSfxSources = new List<ManagedSfxSource>();
    private readonly List<MusicOverride> musicOverrides = new List<MusicOverride>();
    private int musicDuckCount;
    private int ambienceDuckCount;
    private int nextMusicOverrideToken = 1;
    private float musicDuckMultiplier = 1f;
    private float ambienceDuckMultiplier = 1f;

    public float MusicVolume => Mathf.Clamp01(musicVolume);
    public float SfxVolume => Mathf.Clamp01(sfxVolume);

    public static AudioManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<AudioManager>();
#else
        Instance = FindAnyObjectByType<AudioManager>();
#endif
        if (Instance != null)
        {
            return Instance;
        }

        GameObject host = new GameObject("AudioManager");
        Instance = host.AddComponent<AudioManager>();
        return Instance;
    }

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
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

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

    private void Start()
    {
        UpdateZoneAudio();
    }

    private void LateUpdate()
    {
        RefreshTimeScaledPitches();
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
        UpdateZoneAudio();
    }

    public void RegisterZoneExit(Zone zone)
    {
        if (zone == null)
        {
            return;
        }

        // Retire la zone et recalcule la musique courante.
        zoneStack.Remove(zone);
        UpdateZoneAudio();
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

    public int PushMusicOverride(AudioClipSO clip)
    {
        return PushMusicOverride(clip, false);
    }

    public int PushCombatMusicOverride(AudioClipSO clip)
    {
        return PushMusicOverride(clip, true);
    }

    private int PushMusicOverride(AudioClipSO clip, bool suppressesAmbience)
    {
        if (clip == null || clip.audioClip == null)
        {
            return 0;
        }

        int token = nextMusicOverrideToken++;
        if (nextMusicOverrideToken <= 0)
        {
            nextMusicOverrideToken = 1;
        }

        musicOverrides.Add(new MusicOverride(token, clip, suppressesAmbience));
        UpdateZoneAudio();
        return token;
    }

    public void PopMusicOverride(int token)
    {
        if (token == 0 || musicOverrides.Count == 0)
        {
            return;
        }

        bool removed = false;
        for (int i = musicOverrides.Count - 1; i >= 0; i--)
        {
            if (musicOverrides[i].token == token)
            {
                musicOverrides.RemoveAt(i);
                removed = true;
                break;
            }
        }

        if (!removed)
        {
            return;
        }

        UpdateZoneAudio();
    }

    public void PlayAmbience(AudioClipSO clip)
    {
        if (HasAmbienceSuppressingMusicOverride())
        {
            StopAmbience();
            return;
        }

        SetAmbienceClip(ResolveAmbienceClip(clip));
    }

    private void StopAmbience()
    {
        SetAmbienceClip(null);
    }

    private void SetAmbienceClip(AudioClipSO clip)
    {
        EnsureAmbienceSources();

        if (clip == activeAmbienceClip &&
            activeAmbienceSource != null &&
            activeAmbienceSource.isPlaying)
        {
            return;
        }

        if (ambienceFadeRoutine != null)
        {
            StopCoroutine(ambienceFadeRoutine);
        }

        ambienceFadeRoutine = StartCoroutine(FadeAmbienceToClip(clip));
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

    /// <summary>Masque temporairement l'ambiance sans perdre la zone active.</summary>
    public void BeginAmbienceDucking(float multiplier)
    {
        multiplier = Mathf.Clamp01(multiplier);
        ambienceDuckCount++;
        if (ambienceDuckCount == 1)
        {
            ambienceDuckMultiplier = multiplier;
        }
        else
        {
            ambienceDuckMultiplier = Mathf.Min(ambienceDuckMultiplier, multiplier);
        }

        RefreshAmbienceVolume();
    }

    /// <summary>Retire un masquage d'ambiance temporaire.</summary>
    public void EndAmbienceDucking()
    {
        if (ambienceDuckCount <= 0)
        {
            return;
        }

        ambienceDuckCount = Mathf.Max(0, ambienceDuckCount - 1);
        if (ambienceDuckCount == 0)
        {
            ambienceDuckMultiplier = 1f;
        }

        RefreshAmbienceVolume();
    }

    /// <summary>
    /// Cree une voix reservee a une Timeline. Elle est volontairement
    /// independante des deux voix de zone afin que plusieurs clips puissent
    /// se chevaucher et etre fondus par Timeline.
    /// </summary>
    public AudioSource PlayTimelineClip(AudioClipSO clip, float startTime, bool loop)
    {
        if (clip == null || clip.audioClip == null)
        {
            return null;
        }

        AudioSource source = CreateSource("Timeline_" + clip.name);
        ConfigureOneShotSource(source);
        source.spatialBlend = 0f;
        source.clip = clip.audioClip;
        source.loop = loop;
        ApplyClipPitch(source, clip);
        source.volume = 0f;
        if (clip.audioClip.length > 0f)
        {
            source.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.audioClip.length - 0.001f));
        }

        source.Play();
        return source;
    }

    /// <summary>Arrete et libere une voix reservee a une Timeline.</summary>
    public void StopTimelineClip(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        Destroy(source.gameObject);
    }

    public float GetTimelineMusicVolume(AudioClipSO clip)
    {
        // La voix Timeline reste audible pendant que la musique de jeu est
        // masquee par BeginMusicDucking.
        return GetMusicSourceVolume(clip);
    }

    public float GetTimelineAmbienceVolume(AudioClipSO clip)
    {
        // La voix Timeline reste audible pendant que l'ambiance de zone est
        // masquee par BeginAmbienceDucking.
        return GetSfxSourceVolume(clip);
    }

    public float GetTimelineSfxVolume(AudioClipSO clip)
    {
        return GetSfxSourceVolume(clip);
    }

    public static AudioSource PlayClipAtPoint(AudioClipSO clip, Vector3 position)
    {
        AudioManager manager = Instance != null ? Instance : EnsureInstance();
        return manager != null ? manager.PlayClip(clip, position) : null;
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
        ApplyClipPitch(source, clip);
        source.volume = GetSfxSourceVolume(clip);
        source.Play();
        RegisterSfxSource(source, clip);

        if (!clip.loop)
        {
            StartCoroutine(DestroyAfterPlay(source));
        }

        return source;
    }

    /// <summary>Joue un AudioClipSO une seule fois, meme si son asset est configure en boucle.</summary>
    public AudioSource PlayOneShotClip(AudioClipSO clip, Vector3 position)
    {
        if (clip == null || clip.audioClip == null)
        {
            return null;
        }

        AudioSource source = CreateSource("OneShot_" + clip.name);
        ConfigureOneShotSource(source);
        source.transform.position = position;
        source.clip = clip.audioClip;
        source.loop = false;
        ApplyClipPitch(source, clip);
        source.volume = GetSfxSourceVolume(clip);
        source.Play();
        RegisterSfxSource(source, clip);
        StartCoroutine(DestroyAfterPlay(source));
        return source;
    }

    public AudioSource PlayClip(AudioClip clip, Vector3 position, float volume, float pitch = 1f)
    {
        if (clip == null)
        {
            return null;
        }

        AudioSource source = CreateSource("OneShot_" + clip.name);
        ConfigureOneShotSource(source);
        source.transform.position = position;
        source.clip = clip;
        source.loop = false;
        source.pitch = Mathf.Max(0.01f, pitch);
        source.volume = GetSfxSourceVolume(Mathf.Clamp01(volume));
        source.Play();
        RegisterSfxSource(source, Mathf.Clamp01(volume), source.pitch, false);
        StartCoroutine(DestroyAfterPlay(source));
        return source;
    }

    public AudioSource PlayUiClip(AudioClipSO clip)
    {
        if (clip == null || clip.audioClip == null)
        {
            return null;
        }

        AudioSource source = CreateSource("UiOneShot_" + clip.name);
        ConfigureOneShotSource(source);
        source.spatialBlend = 0f;
        source.clip = clip.audioClip;
        source.loop = clip.loop;
        ApplyClipPitch(source, clip);
        source.volume = GetSfxSourceVolume(clip);
        source.Play();
        RegisterSfxSource(source, clip);

        if (!clip.loop)
        {
            StartCoroutine(DestroyAfterPlay(source));
        }

        return source;
    }

    public AudioSource PlayActionCue(ActionAudioCue cue, Vector3 position)
    {
        AudioClipSO clip = ResolveActionAudioClip(cue);
        return PlayClip(clip, position);
    }

    public AudioSource PlayUiActionCue(ActionAudioCue cue)
    {
        AudioClipSO clip = ResolveActionAudioClip(cue);
        return PlayUiClip(clip);
    }

    public AudioClipSO ResolveActionAudioClip(ActionAudioCue cue)
    {
        if (cue == ActionAudioCue.None)
        {
            return null;
        }

        ActionAudioLibrarySO library = ResolveActionAudioLibrary();
        return library != null ? library.Resolve(cue) : null;
    }

    public AudioClipSO ResolveCombatAudioClip(CombatAudioCue cue)
    {
        if (cue == CombatAudioCue.None || combatAudioLibrary == null)
        {
            return null;
        }

        return combatAudioLibrary.Resolve(cue);
    }

    private ActionAudioLibrarySO ResolveActionAudioLibrary()
    {
        if (actionAudioLibrary != null || !loadDefaultActionAudioLibrary)
        {
            return actionAudioLibrary;
        }

        string resourcePath = string.IsNullOrWhiteSpace(defaultActionAudioLibraryResourcePath)
            ? DefaultActionAudioLibraryResourcePath
            : defaultActionAudioLibraryResourcePath.Trim();
        actionAudioLibrary = Resources.Load<ActionAudioLibrarySO>(resourcePath);
        return actionAudioLibrary;
    }

    private void UpdateZoneAudio()
    {
        // Choisit la derniere zone valide de la pile pour chaque canal.
        CleanupNullZones();

        AudioClipSO nextMusicClip = null;
        AudioClipSO nextAmbienceClip = null;
        for (int i = zoneStack.Count - 1; i >= 0; i--)
        {
            Zone zone = zoneStack[i];
            if (zone == null)
            {
                continue;
            }

            if (nextMusicClip == null)
            {
                nextMusicClip = zone.GetZoneMusicClip();
            }

            if (nextAmbienceClip == null)
            {
                nextAmbienceClip = zone.GetZoneAmbienceClip();
            }

            if (nextMusicClip != null && nextAmbienceClip != null)
            {
                break;
            }
        }

        if (musicOverrides.Count > 0)
        {
            PlayMusic(musicOverrides[musicOverrides.Count - 1].clip);
        }
        else
        {
            PlayMusic(nextMusicClip);
        }

        if (HasAmbienceSuppressingMusicOverride())
        {
            StopAmbience();
            return;
        }

        PlayAmbience(nextAmbienceClip);
    }

    private AudioClipSO ResolveAmbienceClip(AudioClipSO clip)
    {
        return clip != null && clip.audioClip != null ? clip : defaultAmbienceClip;
    }

    private bool HasAmbienceSuppressingMusicOverride()
    {
        for (int i = 0; i < musicOverrides.Count; i++)
        {
            if (musicOverrides[i].suppressesAmbience)
            {
                return true;
            }
        }

        return false;
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
                    ApplyClipPitch(from, previousClip);
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
            ApplyClipPitch(to, clip);
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
                ApplyClipPitch(to, clip);
                to.volume = Mathf.Lerp(0f, GetMusicSourceVolume(clip) * multiplier, t);
            }

            if (from != null)
            {
                ApplyClipPitch(from, previousClip);
                from.volume = Mathf.Lerp(GetMusicSourceVolume(previousClip) * multiplier, 0f, t);
            }

            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (to != null)
        {
            ApplyClipPitch(to, clip);
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

    private IEnumerator FadeAmbienceToClip(AudioClipSO clip)
    {
        EnsureAmbienceSources();

        float duration = Mathf.Max(0.01f, fadeDuration);
        AudioSource from = activeAmbienceSource;
        AudioSource to = inactiveAmbienceSource;
        AudioClipSO previousClip = activeAmbienceClip;

        if (clip == null || clip.audioClip == null)
        {
            if (from != null && from.isPlaying)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    float t = elapsed / duration;
                    ApplyClipPitch(from, previousClip);
                    from.volume = Mathf.Lerp(GetAmbienceSourceVolume(previousClip), 0f, t);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                from.volume = 0f;
                from.Stop();
            }

            activeAmbienceClip = null;
            yield break;
        }

        if (to == null)
        {
            to = from;
        }

        if (to != null)
        {
            to.clip = clip.audioClip;
            to.loop = clip.loop;
            ApplyClipPitch(to, clip);
            to.volume = 0f;
            to.Play();
        }

        float time = 0f;
        while (time < duration)
        {
            float t = time / duration;
            if (to != null)
            {
                ApplyClipPitch(to, clip);
                to.volume = Mathf.Lerp(0f, GetAmbienceSourceVolume(clip), t);
            }

            if (from != null)
            {
                ApplyClipPitch(from, previousClip);
                from.volume = Mathf.Lerp(GetAmbienceSourceVolume(previousClip), 0f, t);
            }

            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (to != null)
        {
            ApplyClipPitch(to, clip);
            to.volume = GetAmbienceSourceVolume(clip);
        }

        if (from != null)
        {
            from.volume = 0f;
            if (from.isPlaying)
            {
                from.Stop();
            }
        }

        activeAmbienceSource = to;
        inactiveAmbienceSource = from;
        activeAmbienceClip = clip;
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

    private float GetAmbienceMultiplier()
    {
        return ambienceDuckCount > 0 ? Mathf.Clamp01(ambienceDuckMultiplier) : 1f;
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

    private float GetAmbienceSourceVolume(AudioClipSO clip)
    {
        if (clip == null)
        {
            return 0f;
        }

        return GetSfxSourceVolume(Mathf.Clamp01(clip.volume)) * GetAmbienceMultiplier();
    }

    public static float GetClipPitch(AudioClipSO clip, float basePitch = 1f)
    {
        float safeBasePitch = Mathf.Max(0f, basePitch);
        if (clip == null || !clip.affectedByTimeScale)
        {
            return safeBasePitch;
        }

        return Mathf.Clamp(safeBasePitch * TimeManager.GetAudioTimeScale(), 0f, 3f);
    }

    public static void ApplyClipPitch(AudioSource source, AudioClipSO clip, float basePitch = 1f)
    {
        if (source == null)
        {
            return;
        }

        source.pitch = GetClipPitch(clip, basePitch);
    }

    private static float GetManagedSourcePitch(ManagedSfxSource entry)
    {
        float basePitch = Mathf.Max(0f, entry.basePitch);
        if (!entry.affectedByTimeScale)
        {
            return basePitch;
        }

        return Mathf.Clamp(basePitch * TimeManager.GetAudioTimeScale(), 0f, 3f);
    }

    private void RefreshTimeScaledPitches()
    {
        ApplyClipPitch(activeSource, activeClip);
        ApplyClipPitch(activeAmbienceSource, activeAmbienceClip);
        CleanupTrackedSfxSources();
        for (int i = 0; i < activeSfxSources.Count; i++)
        {
            ManagedSfxSource entry = activeSfxSources[i];
            if (entry.source != null)
            {
                entry.source.pitch = GetManagedSourcePitch(entry);
            }
        }
    }

    private void RefreshMusicVolume()
    {
        if (activeSource == null || activeClip == null || activeClip.audioClip == null)
        {
            return;
        }

        activeSource.volume = GetMusicSourceVolume(activeClip) * GetMusicMultiplier();
    }

    private void RefreshAmbienceVolume()
    {
        if (activeAmbienceSource == null || activeAmbienceClip == null || activeAmbienceClip.audioClip == null)
        {
            return;
        }

        activeAmbienceSource.volume = GetAmbienceSourceVolume(activeAmbienceClip);
    }

    private void RefreshSfxVolumes()
    {
        RefreshAmbienceVolume();
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
        RegisterSfxSource(
            source,
            clip != null ? Mathf.Clamp01(clip.volume) : 1f,
            1f,
            clip != null && clip.affectedByTimeScale);
    }

    private void RegisterSfxSource(AudioSource source, float clipVolume, float basePitch, bool affectedByTimeScale)
    {
        if (source == null)
        {
            return;
        }

        clipVolume = Mathf.Clamp01(clipVolume);
        basePitch = Mathf.Max(0f, basePitch);
        CleanupTrackedSfxSources();

        for (int i = 0; i < activeSfxSources.Count; i++)
        {
            if (activeSfxSources[i].source == source)
            {
                activeSfxSources[i] = new ManagedSfxSource(source, clipVolume, basePitch, affectedByTimeScale);
                return;
            }
        }

        activeSfxSources.Add(new ManagedSfxSource(source, clipVolume, basePitch, affectedByTimeScale));
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

    private IEnumerator DestroyAfterPlay(AudioSource source)
    {
        if (source == null)
        {
            yield break;
        }

        while (source != null && source.isPlaying)
        {
            yield return null;
        }

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

    private void EnsureAmbienceSources()
    {
        if (primaryAmbienceSource == null)
        {
            primaryAmbienceSource = CreateSource("Ambience_A");
        }

        if (secondaryAmbienceSource == null)
        {
            secondaryAmbienceSource = CreateSource("Ambience_B");
        }

        ConfigureAmbienceSource(primaryAmbienceSource);
        ConfigureAmbienceSource(secondaryAmbienceSource);

        if (activeAmbienceSource == null || inactiveAmbienceSource == null)
        {
            activeAmbienceSource = primaryAmbienceSource;
            inactiveAmbienceSource = secondaryAmbienceSource;
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

    private void ConfigureAmbienceSource(AudioSource source)
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
