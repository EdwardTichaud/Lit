using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{

    // Règles sonores par défaut imposées par la documentation de production.
    private const float DefaultVolume = 0.8f;
    private const bool DefaultLoop = false;

    /// <summary>
    /// Récupère le clip Unity encapsulé dans le ScriptableObject.
    /// </summary>
    private static AudioClip ResolveClip(AudioClipSO clipAsset) => clipAsset != null ? clipAsset.Clip : null;

    /// <summary>
    /// Calcule un volume effectif en combinant la valeur du ScriptableObject et un éventuel fallback.
    /// </summary>
    private static float ResolveVolume(AudioClipSO clipAsset, float fallback = DefaultVolume)
    {
        return clipAsset != null ? clipAsset.Volume : Mathf.Clamp01(fallback);
    }

    /// <summary>
    /// Indique si le clip doit être lu en boucle selon les données configurées.
    /// </summary>
    private static bool ResolveLoop(AudioClipSO clipAsset, bool fallback = DefaultLoop)
    {
        return clipAsset != null ? clipAsset.Loop : fallback;
    }

    /// <summary>
    /// Delai avant lecture d'un clip (ignore si la lecture est en boucle).
    /// </summary>
    private static float ResolveDelay(AudioClipSO clipAsset)
    {
        if (clipAsset == null || clipAsset.Loop)
            return 0f;

        return Mathf.Max(0f, clipAsset.StartDelay);
    }

    [Header("Audio Sources")]
    // Sources utilisées comme gabarits (mixer group, spatialisation, etc.).
    public AudioSource[] musicSources = new AudioSource[3];
    public AudioSource[] sfxSources = new AudioSource[3];
    // Source modèle facultative dédiée aux sons d'avertissement.
    // ⚠️ Aucun son ne doit être joué directement via cette référence :
    // elle sert uniquement de gabarit pour copier les réglages (mixer group,
    // spatialisation, etc.) vers une AudioSource éphémère créée à la volée.
    public AudioSource warningClipTemplate;

    [Header("Warning Clip Settings")]
    [Tooltip("Pourcentage d'atténuation appliqué aux autres sources lors de la lecture d'un warning clip")]
    [Range(0f, 1f)]
    public float warningAttenuation = 0.8f; // 80 % d'atténuation par défaut

    [Header("Fade Settings")]
    public float fadeDuration = 2f;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float voiceVolume = 1f;

    [Header("Normalisation automatique")]
    [Tooltip("Calcule un facteur pour que tous les clips aient un volume perçu similaire")]
    public bool autoNormalize = true;
    [Range(0f, 1f)] public float targetRms = 0.1f;

    public static AudioManager Instance { get; private set; }

    private AudioSource CurrentMusicSource => currentMusicHandle != null ? currentMusicHandle.Source : null;

    public bool TryGetActiveMusic(out AudioClipSO clipAsset, out float playbackTime)
    {
        clipAsset = null;
        playbackTime = 0f;

        if (musicOverrideSource != null && musicOverrideSource.isPlaying)
        {
            clipAsset = overrideMusicAsset;
            playbackTime = musicOverrideSource.time;
            return clipAsset != null;
        }

        if (currentMusicHandle != null && currentMusicHandle.Source != null && currentMusicHandle.Source.isPlaying)
        {
            clipAsset = currentMusicHandle.ClipAsset ?? currentMusicAsset;
            playbackTime = currentMusicHandle.Source.time;
            return clipAsset != null;
        }

        if (CurrentMusicSource != null)
            playbackTime = CurrentMusicSource.time;

        clipAsset = currentMusicAsset;
        return clipAsset != null;
    }

    private Coroutine crossfadeRoutine;
    private enum FadingHandleAction
    {
        None,
        Pause,
        Destroy
    }
    // Coroutine gérant l'atténuation temporaire lors de la lecture d'un warning
    private Coroutine warningRoutine;
    private AudioClipSO lastExplorationClip;
    private float lastExplorationTime;

    private bool isInCombat = false;

    // 📈 Indique si une timeline est en cours.
    // Permet de suspendre la musique actuelle et de la reprendre à la fin de la cinématique.
    private bool isInTimeline = false;

    // 📊 Sauvegarde l'état avant l'entrée en timeline.
    private bool wasInCombatBeforeTimeline = false; // → vrai si l'on était en combat avant la cinématique
    private AudioClipSO clipBeforeTimeline;         // → musique en cours avant la timeline
    private float timeBeforeTimeline;               // → position de lecture de cette musique

    private readonly Dictionary<AudioClipSO, float> explorationPlaybackPositions = new Dictionary<AudioClipSO, float>();
    private AudioClipSO currentMusicAsset;
    private DynamicAudioHandle handleBeforeTimeline;
    private DynamicAudioHandle timelineMusicHandle;
    private Dictionary<AudioClip, float> normalizationCache = new Dictionary<AudioClip, float>();

    // Gestion des musiques déclenchées à la volée (timelines, scripts, etc.).
    private AudioSource musicOverrideSource;
    private AudioClipSO overrideMusicAsset;
    private float? overrideCustomVolume;
    private Coroutine musicOverrideWatcher;
    private List<AudioSource> pausedMusicSources;

    /// <summary>
    /// Classe interne dédiée au suivi des AudioSource créées dynamiquement pour les SFX/voix.
    /// Elle nous permet de recalculer facilement les volumes lorsque les réglages globaux changent
    /// (volume SFX/voix, atténuation lors d'un avertissement, etc.).
    /// </summary>
    private class DynamicAudioHandle
    {
        public AudioSource Source;
        public float BaseFactor;
        public AudioClipSO ClipAsset;
    }

    // Listes des instances dynamiques actuellement en cours de lecture.
    private readonly List<DynamicAudioHandle> activeSfxHandles = new List<DynamicAudioHandle>();
    private readonly List<DynamicAudioHandle> activeVoiceHandles = new List<DynamicAudioHandle>();
    private readonly List<DynamicAudioHandle> activeMusicHandles = new List<DynamicAudioHandle>();

    private DynamicAudioHandle currentMusicHandle;
    private DynamicAudioHandle fadingMusicHandle;
    private FadingHandleAction fadingHandleAction = FadingHandleAction.None;

    // Compteur pour nommer clairement les AudioSource engendrées dynamiquement.
    private int dynamicSourceCounter = 0;

    // Volume de base utilisé pour les avertissements (dépend du volume musique).
    private float warningBaseVolume = 1f;

    // Facteur appliqué aux SFX/voix lorsque la lecture d'un avertissement est en cours.
    private float currentWarningAttenuation = 1f;

    // Référence vers la source actuellement utilisée pour jouer un avertissement.
    private AudioSource currentWarningSource;

    // Sauvegardes temporaires des volumes pendant la lecture d'un avertissement.
    private float[] cachedWarningMusicVolumes;
    private float[] cachedWarningSfxVolumes;

    /// <summary>
    /// Met à jour le volume de la source d'avertissement en fonction de la musique.
    /// </summary>
    private void UpdateWarningVolume()
    {
        // Volume deux fois plus élevé que celui des sources de musique
        // → utilisé au moment de générer la prochaine source d'avertissement éphémère.
        warningBaseVolume = musicVolume * 2f;
    }

    private void PrepareTemplateSources(AudioSource[] sources)
    {
        if (sources == null)
            return;

        foreach (var source in sources)
        {
            if (source == null)
                continue;

            if (!source.transform.IsChildOf(transform))
                continue;

            source.playOnAwake = false;
            if (source.isPlaying)
                source.Stop();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (!Instance.isActiveAndEnabled && gameObject.activeInHierarchy && enabled)
            {
                Destroy(Instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        if (gameObject.activeInHierarchy && enabled)
            RegisterInstance();
    }

    private void OnEnable()
    {
        if (Instance == null || !Instance.isActiveAndEnabled)
        {
            if (Instance != null && Instance != this)
                Destroy(Instance.gameObject);

            RegisterInstance();
        }
    }

    private void OnTransformParentChanged()
    {
        if (Instance == this && transform.parent != null)
            transform.SetParent(null, true);
    }

    private void RegisterInstance()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (transform.parent != null)
            transform.SetParent(null, true);
    }

    private bool EnsureActive(string context)
    {
        if (gameObject.activeInHierarchy && enabled)
            return true;

        if (transform.parent != null)
            transform.SetParent(null, true);

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (!enabled)
            enabled = true;

        if (!gameObject.activeInHierarchy || !enabled)
        {
            Debug.LogWarning($"[AudioManager] AudioManager inactif ({context}), lecture annulee.");
            return false;
        }

        return true;
    }

    private void Start()
    {
        PrepareTemplateSources(musicSources);
        PrepareTemplateSources(sfxSources);
        // Les sources vocales utilisent désormais le gabarit SFX.

        // Source dédiée aux avertissements
        if (warningClipTemplate != null)
        {
            // Sécurise l'éventuel gabarit renseigné dans l'inspecteur pour éviter
            // toute lecture intempestive lors de l'initialisation.
            warningClipTemplate.playOnAwake = false;
            if (warningClipTemplate.isPlaying)
                warningClipTemplate.Stop();
        }

        SetMusicVolume(musicVolume);
        SetSfxVolume(sfxVolume);
        SetVoiceVolume(voiceVolume);
        UpdateWarningVolume();
    }

    private float GetNormalizationFactor(AudioClip clip)
    {
        if (!autoNormalize || clip == null)
            return 1f;

        if (normalizationCache.TryGetValue(clip, out float factor))
            return factor;

        try
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            double sumSq = 0;
            for (int i = 0; i < samples.Length; i++)
                sumSq += samples[i] * samples[i];

            double rms = Mathf.Sqrt((float)(sumSq / samples.Length));
            factor = rms > 0 ? targetRms / (float)rms : 1f;
        }
        catch
        {
            factor = 1f;
        }

        normalizationCache[clip] = factor;
        return factor;
    }

    #region \U0001F4E3 Volume

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        RefreshMusicVolumes();
        UpdateWarningVolume();

        if (musicOverrideSource != null && musicOverrideSource.isPlaying && musicOverrideSource.clip != null)
        {
            float normalization = GetNormalizationFactor(musicOverrideSource.clip);
            float volumeFactor = overrideCustomVolume ?? (overrideMusicAsset != null ? ResolveVolume(overrideMusicAsset, DefaultVolume) : DefaultVolume);
            musicOverrideSource.volume = musicVolume * normalization * volumeFactor;
        }
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        RefreshDynamicVolumes(activeSfxHandles, sfxVolume);
    }

    public void SetVoiceVolume(float value)
    {
        voiceVolume = Mathf.Clamp01(value);
        RefreshDynamicVolumes(activeVoiceHandles, voiceVolume);
    }

    #endregion

    #region 🎼 Gestion des musiques instantanées

    private void PauseMainMusic()
    {
        if (pausedMusicSources != null)
            return;

        pausedMusicSources = new List<AudioSource>();
        for (int i = activeMusicHandles.Count - 1; i >= 0; i--)
        {
            var source = activeMusicHandles[i]?.Source;
            if (source != null && source.isPlaying)
            {
                source.Pause();
                pausedMusicSources.Add(source);
            }
        }
    }

    private void ResumeMainMusic()
    {
        if (pausedMusicSources == null)
            return;

        foreach (var source in pausedMusicSources)
        {
            if (source != null)
                source.UnPause();
        }

        pausedMusicSources = null;
    }

    private void StopMusicOverride(bool resumeMainMusic = true)
    {
        if (musicOverrideWatcher != null)
        {
            StopCoroutine(musicOverrideWatcher);
            musicOverrideWatcher = null;
        }

        if (musicOverrideSource != null)
        {
            musicOverrideSource.Stop();
            Destroy(musicOverrideSource.gameObject);
            musicOverrideSource = null;
        }

        overrideMusicAsset = null;
        overrideCustomVolume = null;

        if (resumeMainMusic)
            ResumeMainMusic();
    }

    private IEnumerator WaitForOverrideEnd()
    {
        while (musicOverrideSource != null && musicOverrideSource.isPlaying)
            yield return null;

        StopMusicOverride();
    }

    public void PlayMusicOverride(AudioClipSO clipAsset, float? customVolume = null)
    {
        if (!EnsureActive(nameof(PlayMusicOverride)))
            return;

        if (clipAsset == null)
            return;

        AudioClip clip = ResolveClip(clipAsset);
        if (clip == null)
            return;

        StopMusicOverride(resumeMainMusic: false);
        PauseMainMusic();
        AudioSource template = GetTemplateSource(musicSources);
        musicOverrideSource = CreateManagedSource("Music_Override", template, ResolveLoop(clipAsset));

        musicOverrideSource.clip = clip;
        ApplyClipRadius(musicOverrideSource, clipAsset);

        float normalization = GetNormalizationFactor(clip);
        float volumeFactor = customVolume.HasValue
            ? Mathf.Clamp01(customVolume.Value)
            : ResolveVolume(clipAsset, DefaultVolume);

        musicOverrideSource.volume = musicVolume * normalization * volumeFactor;
        musicOverrideSource.time = 0f;
        musicOverrideSource.Play();

        overrideMusicAsset = clipAsset;
        overrideCustomVolume = customVolume;

        if (!clipAsset.Loop)
            musicOverrideWatcher = StartCoroutine(WaitForOverrideEnd());

        AnnounceMusic(clipAsset);
    }

    #endregion

    #region 🎵 Musique : Transitions

    public void PlayExplorationMusic(AudioClipSO newExplorationClip)
    {
        if (!EnsureActive(nameof(PlayExplorationMusic)))
            return;

        if (newExplorationClip == null)
            return;

        if (isInCombat || isInTimeline)
            return;

        if (currentMusicAsset == newExplorationClip && CurrentMusicSource != null && CurrentMusicSource.isPlaying)
            return;

        float resumeTime = explorationPlaybackPositions.TryGetValue(newExplorationClip, out float savedTime)
            ? savedTime
            : 0f;

        DynamicAudioHandle toHandle = GetOrCreateExplorationHandle(newExplorationClip, resumeTime);
        if (toHandle == null)
            return;

        if (currentMusicHandle != null && currentMusicHandle.Source != null && currentMusicHandle.ClipAsset != null)
            explorationPlaybackPositions[currentMusicHandle.ClipAsset] = currentMusicHandle.Source.time;

        lastExplorationClip = newExplorationClip;

        currentMusicAsset = newExplorationClip;
        AnnounceMusic(newExplorationClip);

        if (currentMusicHandle == null)
            StartCrossfade(null, toHandle, FadingHandleAction.None);
        else
            StartCrossfade(currentMusicHandle, toHandle, FadingHandleAction.Destroy);

        isInCombat = false;
    }

    public void TransitionToNewExplorationZone(AudioClipSO newExplorationClip)
    {
        if (!EnsureActive(nameof(TransitionToNewExplorationZone)))
            return;

        if (newExplorationClip == null)
            return;

        if (isInCombat || isInTimeline)
            return;

        if (currentMusicAsset == newExplorationClip && CurrentMusicSource != null && CurrentMusicSource.isPlaying)
            return;

        float resumeTime = explorationPlaybackPositions.TryGetValue(newExplorationClip, out float savedTime)
            ? savedTime
            : 0f;

        DynamicAudioHandle toHandle = GetOrCreateExplorationHandle(newExplorationClip, resumeTime);
        if (toHandle == null)
            return;

        if (currentMusicHandle != null && currentMusicHandle.Source != null && currentMusicHandle.ClipAsset != null)
            explorationPlaybackPositions[currentMusicHandle.ClipAsset] = currentMusicHandle.Source.time;

        lastExplorationClip = newExplorationClip;

        currentMusicAsset = newExplorationClip;
        AnnounceMusic(newExplorationClip);

        if (currentMusicHandle == null)
            StartCrossfade(null, toHandle, FadingHandleAction.None);
        else
            StartCrossfade(currentMusicHandle, toHandle, FadingHandleAction.Destroy);
    }

    public void TransitionToCombat(AudioClipSO combatClip)
    {
        if (!EnsureActive(nameof(TransitionToCombat)))
            return;

        if (ResolveClip(combatClip) == null)
            return;

        if (isInCombat)
            return;

        if (isInTimeline)
        {
            isInTimeline = false;

            if (!wasInCombatBeforeTimeline)
            {
                lastExplorationClip = clipBeforeTimeline;
                lastExplorationTime = timeBeforeTimeline;
            }

            timelineMusicHandle = null;
        }
        else if (currentMusicAsset != null)
        {
            lastExplorationClip = currentMusicAsset;
            lastExplorationTime = CurrentMusicSource != null ? CurrentMusicSource.time : 0f;
        }

        if (currentMusicHandle != null && currentMusicHandle.Source != null && currentMusicHandle.ClipAsset != null)
            explorationPlaybackPositions[currentMusicHandle.ClipAsset] = currentMusicHandle.Source.time;

        isInCombat = true;
        SwitchImmediately(combatClip);
    }

    public void ReturnFromBattle()
    {
        if (!EnsureActive(nameof(ReturnFromBattle)))
            return;

        AudioClipSO resumeClip = lastExplorationClip;
        if (isInCombat && resumeClip != null && resumeClip == currentMusicAsset)
            resumeClip = null;

        if (resumeClip == null && ZoneManager.Instance != null)
        {
            resumeClip = ZoneManager.Instance.currentZone != null
                ? ZoneManager.Instance.currentZone.zoneMusic
                : null;
        }

        if (resumeClip == null)
        {
            if (currentMusicHandle != null)
                StartCrossfade(currentMusicHandle, null, FadingHandleAction.Destroy);

            isInCombat = false;
            return;
        }

        if (!isInCombat && currentMusicHandle != null && currentMusicHandle.Source != null
            && currentMusicHandle.Source.isPlaying && currentMusicHandle.ClipAsset == resumeClip)
        {
            isInCombat = false;
            return;
        }

        float resumeTime = 0f;
        if (explorationPlaybackPositions.TryGetValue(resumeClip, out float savedTime))
        {
            resumeTime = savedTime;
        }
        else if (resumeClip == lastExplorationClip)
        {
            resumeTime = lastExplorationTime;
        }

        DynamicAudioHandle toHandle = GetOrCreateExplorationHandle(resumeClip, resumeTime);

        currentMusicAsset = resumeClip;
        AnnounceMusic(resumeClip);

        if (toHandle != null)
            StartCrossfade(currentMusicHandle, toHandle, FadingHandleAction.Destroy);
        else
            StartCrossfade(currentMusicHandle, null, FadingHandleAction.Destroy);

        lastExplorationClip = resumeClip;
        isInCombat = false;
    }

    /// <summary>
    /// Lance la musique d'une timeline en sauvegardant l'état précédent
    /// (exploration ou combat).
    /// </summary>
    /// <param name="timelineClip">Musique à jouer durant la cinématique.</param>
    public void TransitionToTimeline(AudioClipSO timelineClip)
    {
        if (!EnsureActive(nameof(TransitionToTimeline)))
            return;

        if (isInTimeline)
            return;

        wasInCombatBeforeTimeline = isInCombat;
        clipBeforeTimeline = currentMusicAsset;
        timeBeforeTimeline = CurrentMusicSource != null ? CurrentMusicSource.time : 0f;
        handleBeforeTimeline = currentMusicHandle;

        if (handleBeforeTimeline != null && handleBeforeTimeline.Source != null && handleBeforeTimeline.ClipAsset != null)
            explorationPlaybackPositions[handleBeforeTimeline.ClipAsset] = handleBeforeTimeline.Source.time;

        isInTimeline = true;
        isInCombat = false;

        AudioClip clip = ResolveClip(timelineClip);
        if (clip != null)
        {
            DynamicAudioHandle toHandle = CreateMusicHandle(clip, timelineClip, 0f, ResolveLoop(timelineClip));
            if (toHandle == null)
                return;

            timelineMusicHandle = toHandle;
            currentMusicAsset = timelineClip;
            AnnounceMusic(timelineClip);

            StartCrossfade(handleBeforeTimeline, toHandle, FadingHandleAction.Destroy);
        }
        else
        {
            timelineMusicHandle = null;
            StartCrossfade(handleBeforeTimeline, null, FadingHandleAction.Destroy);
        }

        handleBeforeTimeline = null;
    }

    /// <summary>
    /// Restaure la musique précédente après une timeline.
    /// </summary>
    public void ReturnFromTimeline()
    {
        if (!EnsureActive(nameof(ReturnFromTimeline)))
            return;

        if (!isInTimeline)
            return;

        isInCombat = wasInCombatBeforeTimeline;
        isInTimeline = false;

        DynamicAudioHandle resumeHandle = null;
        AudioClipSO resumeClip = clipBeforeTimeline;

        if (resumeHandle == null && resumeClip != null)
        {
            float resumeTime = timeBeforeTimeline;
            if (!wasInCombatBeforeTimeline)
            {
                if (explorationPlaybackPositions.TryGetValue(resumeClip, out float savedTime))
                    resumeTime = savedTime;

                resumeHandle = GetOrCreateExplorationHandle(resumeClip, resumeTime);
            }
            else
            {
                AudioClip combatClip = ResolveClip(resumeClip);
                if (combatClip != null)
                    resumeHandle = CreateMusicHandle(combatClip, resumeClip, resumeTime, ResolveLoop(resumeClip));
            }
        }

        currentMusicAsset = resumeHandle != null ? (resumeHandle.ClipAsset ?? resumeClip) : resumeClip;
        if (currentMusicAsset != null)
            AnnounceMusic(currentMusicAsset);

        if (timelineMusicHandle != null)
        {
            StartCrossfade(timelineMusicHandle, resumeHandle, FadingHandleAction.Destroy);
            timelineMusicHandle = null;
        }
        else if (resumeHandle != null)
        {
            StartCrossfade(null, resumeHandle, FadingHandleAction.None);
        }

        handleBeforeTimeline = null;
    }

    /// <summary>
    /// Réduit progressivement le volume de la musique actuelle puis la met en pause.
    /// Utilisé lors d'une timeline sans musique dédiée.
    /// </summary>
    private IEnumerator FadeOutCurrentMusic()
    {
        AudioSource source = CurrentMusicSource;
        if (source == null)
        {
            yield break;
        }

        float startVolume = source.volume;
        float t = 0f;

        // ⏱️ Utilise le temps non-scalé afin que le fondu reste fonctionnel
        // même lorsque la Timeline met le jeu en pause via un timeScale nul.
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, t / fadeDuration);
            yield return null;
        }

        source.volume = 0f;
        source.Pause();
    }

    /// <summary>
    /// Restaure progressivement le volume de la musique après une timeline.
    /// </summary>
    private IEnumerator FadeInCurrentMusic()
    {
        AudioSource source = CurrentMusicSource;
        if (source == null || currentMusicHandle == null)
        {
            yield break;
        }

        float targetVolume = Mathf.Clamp01(musicVolume * currentMusicHandle.BaseFactor);
        source.volume = 0f;
        float t = 0f;

        // ⏱️ Utilise Time.unscaledDeltaTime pour que le fondu soit insensible
        // aux ralentis ou arrêts complets provoqués par les timelines.
        while (t < fadeDuration)
        {
            if (source == null || currentMusicHandle == null || currentMusicHandle.Source == null)
            {
                yield break;
            }

            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, t / fadeDuration);
            yield return null;
        }

        if (source != null && currentMusicHandle != null && currentMusicHandle.Source != null)
            source.volume = targetVolume;
    }

    private DynamicAudioHandle CreateMusicHandle(AudioClip clip, AudioClipSO clipAsset, float startTime, bool loop)
    {
        if (clip == null)
            return null;

        AudioSource template = GetTemplateSource(musicSources);
        AudioSource source = CreateManagedSource("Music", template, loop);
        source.clip = clip;
        float clampedStart = Mathf.Clamp(startTime, 0f, clip.length > 0f ? clip.length : 0f);
        source.time = clampedStart;
        ApplyClipRadius(source, clipAsset);

        DynamicAudioHandle handle = new DynamicAudioHandle
        {
            Source = source,
            BaseFactor = GetNormalizationFactor(clip) * ResolveVolume(clipAsset, DefaultVolume),
            ClipAsset = clipAsset
        };

        activeMusicHandles.Add(handle);
        return handle;
    }

    private DynamicAudioHandle GetOrCreateExplorationHandle(AudioClipSO clipAsset, float startTime)
    {
        if (clipAsset == null)
            return null;

        AudioClip clip = ResolveClip(clipAsset);
        if (clip == null)
            return null;

        return CreateMusicHandle(clip, clipAsset, startTime, ResolveLoop(clipAsset));
    }

    private void FinalizeFadingHandle(DynamicAudioHandle handle, FadingHandleAction action)
    {
        if (handle == null)
            return;

        if (action == FadingHandleAction.None)
        {
            if (handle.Source == null)
            {
                activeMusicHandles.Remove(handle);
            }
            return;
        }

        if (handle.Source == null)
        {
            activeMusicHandles.Remove(handle);
            return;
        }

        handle.Source.volume = 0f;

        if (action == FadingHandleAction.Pause || action == FadingHandleAction.Destroy)
            DestroyMusicHandle(handle);
    }

    private void DestroyMusicHandle(DynamicAudioHandle handle)
    {
        if (handle == null)
            return;

        activeMusicHandles.Remove(handle);

        if (handle.Source == null)
            return;

        if (handle.Source.isPlaying)
            handle.Source.Stop();
        Destroy(handle.Source.gameObject);
    }

    private void StopCrossfadeRoutine()
    {
        if (crossfadeRoutine != null)
        {
            StopCoroutine(crossfadeRoutine);
            crossfadeRoutine = null;
        }

        if (fadingMusicHandle != null)
        {
            FinalizeFadingHandle(fadingMusicHandle, fadingHandleAction);
            fadingMusicHandle = null;
            fadingHandleAction = FadingHandleAction.None;
        }
    }

    private void StartCrossfade(DynamicAudioHandle fromHandle, DynamicAudioHandle toHandle, FadingHandleAction actionAfterFade)
    {
        StopMusicOverride();
        StopCrossfadeRoutine();

        if (fromHandle == null && toHandle == null)
            return;

        if (fromHandle == null)
            actionAfterFade = FadingHandleAction.None;

        crossfadeRoutine = StartCoroutine(CrossfadeMusic(fromHandle, toHandle, actionAfterFade));
    }

    private void SwitchImmediately(AudioClipSO newClip)
    {
        StopMusicOverride();
        currentMusicAsset = newClip;
        AnnounceMusic(newClip);

        StopCrossfadeRoutine();

        if (currentMusicHandle != null)
        {
            DestroyMusicHandle(currentMusicHandle);
        }

        currentMusicHandle = null;

        AudioClip clip = ResolveClip(newClip);

        if (clip == null)
            return;

        DynamicAudioHandle handle = CreateMusicHandle(clip, newClip, 0f, ResolveLoop(newClip));
        ApplyMusicVolume(handle);
        handle.Source.Play();
        currentMusicHandle = handle;
    }

    /// <summary>
    /// Met à jour la boîte d'information musicale en bas de l'écran.
    /// </summary>
    private void AnnounceMusic(AudioClipSO clip)
    {
        if (clip == null)
            return;

        MusicInfoBoxUI.Instance.Show(clip);
    }

    private IEnumerator CrossfadeMusic(DynamicAudioHandle fromHandle, DynamicAudioHandle toHandle, FadingHandleAction actionAfterFade)
    {
        AudioSource fromSource = fromHandle != null ? fromHandle.Source : null;
        AudioSource toSource = toHandle != null ? toHandle.Source : null;
        float fromInitialVolume = fromSource != null ? fromSource.volume : 0f;
        float targetVolume = toHandle != null ? Mathf.Clamp01(musicVolume * toHandle.BaseFactor) : 0f;

        if (toSource != null)
        {
            toSource.volume = 0f;
            toSource.UnPause();
            if (!toSource.isPlaying)
                toSource.Play();
        }

        if (toHandle != null)
        {
            currentMusicHandle = toHandle;
        }
        else if (actionAfterFade == FadingHandleAction.Pause)
        {
            currentMusicHandle = fromHandle;
        }
        else if (actionAfterFade == FadingHandleAction.Destroy)
        {
            currentMusicHandle = null;
        }

        if (fromHandle != null)
        {
            fadingMusicHandle = fromHandle;
            fadingHandleAction = actionAfterFade;
        }
        else
        {
            fadingMusicHandle = null;
            fadingHandleAction = FadingHandleAction.None;
        }

        float duration = Mathf.Max(0f, fadeDuration);
        if (duration > 0f)
        {
            float tCrossfade = 0f;

            // 🎚️ Crossfade piloté par le temps non-scalé afin de rester fluide
            // pendant les cinématiques qui manipulent le timeScale global.
            while (tCrossfade < duration)
            {
                tCrossfade += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(tCrossfade / duration);

                if (toSource != null)
                    toSource.volume = Mathf.Lerp(0f, targetVolume, progress);
                if (fromSource != null)
                    fromSource.volume = Mathf.Lerp(fromInitialVolume, 0f, progress);

                if ((toHandle != null && toHandle.Source == null) || (fromHandle != null && fromHandle.Source == null))
                    break;

                yield return null;
            }
        }

        if (toSource != null)
            toSource.volume = targetVolume;

        if (fromHandle != null)
            FinalizeFadingHandle(fromHandle, actionAfterFade);

        fadingMusicHandle = null;
        fadingHandleAction = FadingHandleAction.None;

        crossfadeRoutine = null;
    }

    #endregion

    #region 🔊 Effets

    /// <summary>
    /// Récupère une AudioSource modèle dans le tableau fourni. On privilégie la première
    /// non nulle afin de conserver les réglages renseignés dans l'inspecteur (mixer group, etc.).
    /// </summary>
    private AudioSource GetTemplateSource(AudioSource[] sources)
    {
        if (sources == null)
            return null;

        foreach (var src in sources)
        {
            if (src != null)
                return src;
        }

        return null;
    }

    /// <summary>
    /// Copie les paramètres pertinents d'une AudioSource modèle vers une nouvelle instance.
    /// On évite volontairement de recopier "volume", "loop" ou "playOnAwake" qui sont
    /// définis dynamiquement pour chaque son joué.
    /// </summary>
    private static void CopySourceSettings(AudioSource template, AudioSource target)
    {
        if (template == null || target == null)
            return;

        target.outputAudioMixerGroup = template.outputAudioMixerGroup;
        target.priority = template.priority;
        target.pitch = template.pitch;
        target.panStereo = template.panStereo;
        target.spatialBlend = template.spatialBlend;
        target.reverbZoneMix = template.reverbZoneMix;
        target.dopplerLevel = template.dopplerLevel;
        target.spread = template.spread;
        target.rolloffMode = template.rolloffMode;
        target.minDistance = template.minDistance;
        target.maxDistance = template.maxDistance;
        target.velocityUpdateMode = template.velocityUpdateMode;
        target.ignoreListenerVolume = template.ignoreListenerVolume;
        target.ignoreListenerPause = template.ignoreListenerPause;
    }

    /// <summary>
    /// Applique le rayon d'action defini par un AudioClipSO.
    /// 0 = infini (on conserve les reglages du gabarit).
    /// </summary>
    private static void ApplyClipRadius(AudioSource source, AudioClipSO clipAsset)
    {
        if (source == null || clipAsset == null)
            return;

        float radius = clipAsset.Radius;
        if (radius <= 0f)
            return;

        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.spatialBlend = 1f;
        source.maxDistance = radius;
        if (source.minDistance > radius)
            source.minDistance = radius;
    }

    /// <summary>
    /// Crée une nouvelle AudioSource enfant de l'AudioManager, entièrement gérée par ce dernier.
    /// Note : cette méthode sert de colonne vertébrale à toutes les sources éphémères
    /// (warnings, SFX/voix temporaires, etc.).
    /// </summary>
    private AudioSource CreateManagedSource(string category, AudioSource template, bool loop = false)
    {
        GameObject go = new GameObject($"AudioSource_{category}_{++dynamicSourceCounter}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;

        CopySourceSettings(template, source);

        return source;
    }

    /// <summary>
    /// Applique un volume cohérent à une source gérée dynamiquement.
    /// </summary>
    private void ApplyVolume(DynamicAudioHandle handle, float globalVolume)
    {
        if (handle?.Source == null)
            return;

        float finalVolume = Mathf.Clamp01(globalVolume * handle.BaseFactor * currentWarningAttenuation);
        handle.Source.volume = finalVolume;
    }

    /// <summary>
    /// Applique le volume global sur une source musicale dynamique.
    /// </summary>
    private void ApplyMusicVolume(DynamicAudioHandle handle)
    {
        if (handle?.Source == null)
            return;

        handle.Source.volume = Mathf.Clamp01(musicVolume * handle.BaseFactor);
    }

    /// <summary>
    /// Réactualise le volume de toutes les sources musicales actives.
    /// </summary>
    private void RefreshMusicVolumes()
    {
        for (int i = activeMusicHandles.Count - 1; i >= 0; i--)
        {
            DynamicAudioHandle handle = activeMusicHandles[i];
            if (handle?.Source == null)
            {
                activeMusicHandles.RemoveAt(i);
                continue;
            }

            ApplyMusicVolume(handle);
        }
    }

    /// <summary>
    /// Réactualise le volume de toutes les sources dynamiques d'une catégorie.
    /// </summary>
    private void RefreshDynamicVolumes(List<DynamicAudioHandle> handles, float globalVolume)
    {
        for (int i = handles.Count - 1; i >= 0; i--)
        {
            DynamicAudioHandle handle = handles[i];
            if (handle?.Source == null)
            {
                // Nettoyage défensif : si la source a été détruite ailleurs, on évite les références mortes.
                handles.RemoveAt(i);
                continue;
            }

            ApplyVolume(handle, globalVolume);
        }
    }

    private void ReleaseManagedSource(AudioSource source, List<DynamicAudioHandle> registry)
    {
        if (source == null)
            return;

        if (registry != null)
        {
            for (int i = registry.Count - 1; i >= 0; i--)
            {
                if (registry[i]?.Source == source)
                {
                    registry.RemoveAt(i);
                    break;
                }
            }
        }

        if (source.isPlaying)
            source.Stop();

        Destroy(source.gameObject);
    }

    /// <summary>
    /// Détruit une AudioSource une fois le clip terminé (pour les sons non bouclés).
    /// </summary>
    private IEnumerator DestroySourceWhenFinished(AudioSource source, List<DynamicAudioHandle> registry = null, DynamicAudioHandle handle = null)
    {
        if (source == null || source.loop)
            yield break;

        // Attente minimale d'une frame pour s'assurer que la lecture a bien démarré.
        yield return null;

        // Patiente jusqu'à la fin effective du clip afin de respecter la durée
        // complète avant destruction de l'AudioSource temporaire.
        while (source != null && source.isPlaying)
        {
            yield return null;
        }

        if (registry != null && handle != null)
            registry.Remove(handle);

        if (source != null)
            Destroy(source.gameObject);
    }

    /// <summary>
    /// Interrompt et détruit la source utilisée pour un avertissement si elle est encore active.
    /// </summary>
    private void CleanupWarningSource()
    {
        if (currentWarningSource == null)
            return;

        // Sécurise la destruction même si le clip est toujours en cours de lecture
        // (cas d'un nouvel avertissement qui arrive avant la fin du précédent).
        if (currentWarningSource.isPlaying)
            currentWarningSource.Stop();

        Destroy(currentWarningSource.gameObject);
        currentWarningSource = null;
    }

    /// <summary>
    /// Restaure les volumes sauvegardés lors d'un avertissement et remet à zéro
    /// les multiplicateurs appliqués sur les sources dynamiques.
    /// </summary>
    private void RestoreWarningVolumes()
    {
        if (cachedWarningMusicVolumes != null)
        {
            for (int i = 0; i < musicSources.Length && i < cachedWarningMusicVolumes.Length; i++)
            {
                if (musicSources[i] != null)
                    musicSources[i].volume = cachedWarningMusicVolumes[i];
            }
        }

        if (cachedWarningSfxVolumes != null)
        {
            for (int i = 0; i < sfxSources.Length && i < cachedWarningSfxVolumes.Length; i++)
            {
                if (sfxSources[i] != null)
                    sfxSources[i].volume = cachedWarningSfxVolumes[i];
            }
        }

        cachedWarningMusicVolumes = null;
        cachedWarningSfxVolumes = null;

        currentWarningAttenuation = 1f;
        RefreshMusicVolumes();
        RefreshDynamicVolumes(activeSfxHandles, sfxVolume);
        RefreshDynamicVolumes(activeVoiceHandles, voiceVolume);
    }

    /// <summary>
    /// Lecture simplifiée d'un effet sonore configuré via un <see cref="AudioClipSO"/>.
    /// </summary>
    public void PlaySfx(AudioClipSO clipAsset)
    {
        if (clipAsset == null)
            return;

        if (clipAsset.type == AudioClipSO.AudioClipType.Music)
        {
            PlayMusicOverride(clipAsset);
            return;
        }

        float delaySeconds = ResolveDelay(clipAsset);
        PlaySfxInternal(ResolveClip(clipAsset), ResolveVolume(clipAsset), ResolveLoop(clipAsset), delaySeconds, clipAsset);
    }

    /// <summary>
    /// Joue un effet sonore arbitraire en réutilisant les paramètres globaux du gestionnaire.
    /// Cette surcharge simplifie l'utilisation d'effets dédiés (menus, dash, etc.) sans
    /// dépendre d'une liste globale de clips.
    /// </summary>
    /// <param name="clip">Clip à jouer immédiatement.</param>
    /// <param name="volume">Facteur multiplicatif optionnel (1 = volume par défaut).</param>
    /// <param name="loop">Lecture en boucle ?</param>
    public void PlaySfx(AudioClip clip, float volume = DefaultVolume, bool loop = DefaultLoop, float delaySeconds = 0f)
    {
        PlaySfxInternal(clip, volume, loop, delaySeconds, null);
    }

    private void PlaySfxInternal(AudioClip clip, float volume, bool loop, float delaySeconds, AudioClipSO clipAsset)
    {
        if (!EnsureActive(nameof(PlaySfx)))
            return;

        if (clip == null)
            return; // Rien à jouer : on quitte proprement.

        AudioSource template = GetTemplateSource(sfxSources);
        AudioSource src = CreateManagedSource("SFX", template, loop);
        ApplyClipRadius(src, clipAsset);

        DynamicAudioHandle handle = new DynamicAudioHandle
        {
            Source = src,
            BaseFactor = Mathf.Clamp01(volume) * GetNormalizationFactor(clip)
        };
        activeSfxHandles.Add(handle);

        src.clip = clip;
        ApplyVolume(handle, sfxVolume);
        delaySeconds = loop ? 0f : Mathf.Max(0f, delaySeconds);
        if (!loop && delaySeconds > 0f)
        {
            StartCoroutine(PlayDelayedOneShot(src, delaySeconds, activeSfxHandles, handle));
        }
        else
        {
            src.Play();
            if (!loop)
                StartCoroutine(DestroySourceWhenFinished(src, activeSfxHandles, handle));
        }
    }

    /// <summary>
    /// Joue un effet sonore en boucle et renvoie la source pour un arrêt manuel.
    /// </summary>
    public AudioSource PlayLoopingSfx(AudioClipSO clipAsset, bool forceLoop = true)
    {
        if (!EnsureActive(nameof(PlayLoopingSfx)))
            return null;

        if (clipAsset == null)
            return null;

        if (clipAsset.type == AudioClipSO.AudioClipType.Music)
        {
            PlayMusicOverride(clipAsset);
            return null;
        }

        AudioClip clip = ResolveClip(clipAsset);
        if (clip == null)
            return null;

        bool loop = forceLoop || ResolveLoop(clipAsset);
        AudioSource template = GetTemplateSource(sfxSources);
        AudioSource src = CreateManagedSource("SFX_Loop", template, loop);
        ApplyClipRadius(src, clipAsset);

        DynamicAudioHandle handle = new DynamicAudioHandle
        {
            Source = src,
            BaseFactor = Mathf.Clamp01(ResolveVolume(clipAsset, DefaultVolume)) * GetNormalizationFactor(clip)
        };
        activeSfxHandles.Add(handle);

        src.clip = clip;
        ApplyVolume(handle, sfxVolume);
        src.Play();

        return src;
    }

    /// <summary>
    /// Stoppe et détruit une AudioSource issue des SFX gérés.
    /// </summary>
    public void StopLoopingSfx(AudioSource source)
    {
        ReleaseManagedSource(source, activeSfxHandles);
    }

    /// <summary>
    /// Lecture d'une voix via un <see cref="AudioClipSO"/>.
    /// </summary>
    public void PlayVoice(AudioClipSO clipAsset, float volumeMultiplier = 1f)
    {
        PlayVoice(clipAsset, null, volumeMultiplier);
    }

    public void PlayVoice(AudioClipSO clipAsset, string speakerName, float volumeMultiplier = 1f)
    {
        if (clipAsset == null)
            return;

        AudioClip clip = ResolveClip(clipAsset);
        if (clip == null)
            return;

        float baseVolume = ResolveVolume(clipAsset);
        float finalVolume = Mathf.Clamp01(baseVolume * Mathf.Clamp01(volumeMultiplier));

        TryShowVoiceOverSubtitles(clipAsset, speakerName);
        float delaySeconds = ResolveDelay(clipAsset);
        PlayVoiceInternal(clip, finalVolume, ResolveLoop(clipAsset), delaySeconds, clipAsset);
    }

    public void PlayVoice(AudioClip clip, float volume = DefaultVolume, bool loop = DefaultLoop, float delaySeconds = 0f)
    {
        PlayVoiceInternal(clip, volume, loop, delaySeconds, null);
    }

    private void PlayVoiceInternal(AudioClip clip, float volume, bool loop, float delaySeconds, AudioClipSO clipAsset)
    {
        if (!EnsureActive(nameof(PlayVoice)))
            return;

        if (clip == null) return;

        AudioSource template = GetTemplateSource(sfxSources);
        AudioSource src = CreateManagedSource("Voice", template, loop);
        ApplyClipRadius(src, clipAsset);

        DynamicAudioHandle handle = new DynamicAudioHandle
        {
            Source = src,
            BaseFactor = Mathf.Clamp01(volume) * GetNormalizationFactor(clip)
        };
        activeVoiceHandles.Add(handle);

        src.clip = clip;
        ApplyVolume(handle, voiceVolume);
        delaySeconds = loop ? 0f : Mathf.Max(0f, delaySeconds);
        if (!loop && delaySeconds > 0f)
        {
            StartCoroutine(PlayDelayedOneShot(src, delaySeconds, activeVoiceHandles, handle));
        }
        else
        {
            src.Play();
            if (!loop)
                StartCoroutine(DestroySourceWhenFinished(src, activeVoiceHandles, handle));
        }
    }

    private void TryShowVoiceOverSubtitles(AudioClipSO clipAsset, string speakerName)
    {
        if (clipAsset == null)
            return;

        if (clipAsset.type != AudioClipSO.AudioClipType.VoiceOver)
            return;

        if (DialogueManager.Instance == null)
            return;

        if (string.IsNullOrWhiteSpace(clipAsset.subtitles))
        {
            DialogueManager.Instance.CloseSubtitleIfActive();
            return;
        }

        float duration = clipAsset.Length;
        if (duration <= 0f)
            duration = Mathf.Clamp(clipAsset.subtitles.Length * 0.04f, 0.5f, 4f);

        DialogueManager.Instance.ShowSubtitle(speakerName, clipAsset.subtitles, duration);
    }

    /// <summary>
    /// Joue un clip d'avertissement en utilisant la source dédiée.
    /// </summary>
    public void PlayWarningClip(AudioClipSO clipAsset)
    {
        if (!EnsureActive(nameof(PlayWarningClip)))
            return;

        AudioClip clip = ResolveClip(clipAsset);
        if (clip == null) return;

        float normalization = GetNormalizationFactor(clip);
        float clipVolume = ResolveVolume(clipAsset, DefaultVolume);

        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            RestoreWarningVolumes();
            // Comme la source d'avertissement est désormais éphémère, on la supprime
            // immédiatement pour éviter qu'elle ne persiste en parallèle du nouveau clip.
            CleanupWarningSource();
            warningRoutine = null;
        }

        warningRoutine = StartCoroutine(WarningCoroutine(clipAsset, clip, normalization, clipVolume));
    }

    /// <summary>
    /// Coroutine qui réduit temporairement le volume des autres sources pendant
    /// la lecture d'un warning clip, puis restaure les volumes initiaux.
    /// </summary>
    private IEnumerator WarningCoroutine(AudioClipSO clipAsset, AudioClip clip, float normalizationFactor, float volumeFactor)
    {
        // Lecture du warning clip via une AudioSource dédiée créée pour l'occasion.
        // On privilégie le gabarit dédié s'il a été assigné, sinon on recycle
        // les réglages d'un effet sonore classique pour rester cohérent.
        AudioSource warningTemplate = warningClipTemplate != null ? warningClipTemplate : GetTemplateSource(sfxSources);

        // Conformément aux directives de production, la source servant réellement à la
        // lecture du warning est créée dynamiquement et ne vit que le temps du clip.
        // Les warnings étant par nature ponctuels, on force l'absence de boucle pour
        // garantir la destruction automatique de la source éphémère.
        AudioSource warningSource = CreateManagedSource("Warning", warningTemplate, false);
        warningSource.clip = clip;
        ApplyClipRadius(warningSource, clipAsset);
        warningSource.volume = Mathf.Clamp01(warningBaseVolume * normalizationFactor * volumeFactor);
        currentWarningSource = warningSource;

        float delaySeconds = ResolveDelay(clipAsset);
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        if (warningSource == null)
            yield break;

        warningSource.Play();

        // Attente de la fin du clip puis destruction automatique de la source,
        // garantissant que la source n'existe que pendant la lecture du warning.
        yield return DestroySourceWhenFinished(warningSource);
        currentWarningSource = null;

        warningRoutine = null;
    }

    public void PlaySound(AudioClip clip)
    {
        PlaySfx(clip);
    }

    public void PlaySound(AudioClipSO clipAsset)
    {
        PlaySfx(clipAsset);
    }

    public void PlayTempSfx(AudioClip clip)
    {
        PlaySfx(clip);
    }

    public void PlayTempSfx(AudioClipSO clipAsset)
    {
        PlaySfx(clipAsset);
    }

    private IEnumerator PlayDelayedOneShot(AudioSource source, float delaySeconds, List<DynamicAudioHandle> registry, DynamicAudioHandle handle)
    {
        if (source == null || source.loop)
            yield break;

        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        if (source == null)
            yield break;

        source.Play();
        yield return DestroySourceWhenFinished(source, registry, handle);
    }

    #endregion
}
