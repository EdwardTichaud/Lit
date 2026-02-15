using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gestionnaire audio global (musique de zones + one-shots).
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mix")]
    [Range(0f, 1f), Tooltip("Volume master applique a tout.")]
    public float masterVolume = 1f;
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
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
        source.volume = Mathf.Clamp01(clip.volume) * Mathf.Clamp01(masterVolume);
        source.Play();

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

        if (clip == null || clip.audioClip == null)
        {
            // Fade vers silence si aucun clip valide.
            if (from != null && from.isPlaying)
            {
                float startVolume = from.volume;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    float t = elapsed / duration;
                    from.volume = Mathf.Lerp(startVolume, 0f, t);
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

        float targetVolume = Mathf.Clamp01(clip.volume) * Mathf.Clamp01(masterVolume);
        float fromStartVolume = from != null ? from.volume : 0f;
        float time = 0f;
        while (time < duration)
        {
            float t = time / duration;
            if (to != null)
            {
                to.volume = Mathf.Lerp(0f, targetVolume, t);
            }

            if (from != null)
            {
                from.volume = Mathf.Lerp(fromStartVolume, 0f, t);
            }

            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (to != null)
        {
            to.volume = targetVolume;
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

    private IEnumerator DestroyAfterPlay(AudioSource source, float duration)
    {
        if (source == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(duration);
        if (source != null)
        {
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
