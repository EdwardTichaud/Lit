using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Shared cinematic video surface authored in Bootstrap/UI_Overlay/VideoPanel.
// A caller owns the panel through playback and the handoff to its next screen.
[DisallowMultipleComponent]
public sealed class BootstrapVideoPanel : MonoBehaviour
{
    [SerializeField] private VideoPlayer player;
    [SerializeField] private RawImage picture;
    [SerializeField] private AspectRatioFitter aspect;
    [SerializeField] private UiPanel panel;
    [SerializeField, Min(1f)] private float timeoutSeconds = 20f;
    private readonly Dictionary<AudioSource, bool> mutedSources = new Dictionary<AudioSource, bool>();
    private object owner;
    private int generation;
    private bool ended;
    private string playbackError;

    public VideoPlayer Player => player;
    public bool IsBusy => owner != null;
    public bool IsOwnedBy(object requester) => owner != null && ReferenceEquals(owner, requester) && isActiveAndEnabled;
    public bool IsConfigured => player != null && picture != null && aspect != null && panel != null;

    private void Awake()
    {
        if (player != null) player.playOnAwake = false;
        if (picture != null) { picture.texture = null; picture.color = Color.clear; }
        panel?.Hide(true);
    }

    public IEnumerator PlayResource(string resourcePath, object requester,
        System.Func<IEnumerator> beforePlayback = null, System.Action<double> onPlaybackTime = null)
    {
        if (!TryBegin(requester, beforePlayback == null)) yield break;
        int lease = generation;
        var request = Resources.LoadAsync<VideoClip>(resourcePath);
        yield return request;
        if (!Owns(requester, lease)) yield break;
        var clip = request.asset as VideoClip;
        if (clip == null)
        {
            Debug.LogWarning("[VideoPanel] Video Resources absente : " + resourcePath, this);
            yield break;
        }
        yield return PlayAcquiredClip(clip, requester, lease, beforePlayback, onPlaybackTime);
    }

    public IEnumerator Play(VideoClip clip, object requester,
        System.Func<IEnumerator> beforePlayback = null, System.Action<double> onPlaybackTime = null)
    {
        if (clip == null || !TryBegin(requester, beforePlayback == null)) yield break;
        yield return PlayAcquiredClip(clip, requester, generation, beforePlayback, onPlaybackTime);
    }

    private bool TryBegin(object requester, bool revealPanel)
    {
        if (requester == null || owner != null || !isActiveAndEnabled || !IsConfigured)
        {
            Debug.LogWarning("[VideoPanel] Lecture refusee : panneau absent/inactif, incomplet ou deja utilise.", this);
            return false;
        }
        owner = requester;
        generation++;
        ended = false;
        playbackError = null;
        foreach (var source in FindObjectsByType<AudioSource>())
        {
            mutedSources[source] = source.mute;
            source.mute = true;
        }
        picture.texture = null;
        picture.color = Color.clear;
        transform.SetAsLastSibling();
        if (revealPanel) panel.Show(true); else panel.Hide(true);
        UIManager.PushFocus(this, InputMode.Cinematic);
        return true;
    }

    private IEnumerator PlayAcquiredClip(VideoClip clip, object requester, int lease,
        System.Func<IEnumerator> beforePlayback, System.Action<double> onPlaybackTime)
    {
        player.Stop();
        player.source = VideoSource.VideoClip;
        player.clip = clip;
        player.playOnAwake = false;
        player.isLooping = false;
        player.waitForFirstFrame = true;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.controlledAudioTrackCount = clip.audioTrackCount;
        for (ushort track = 0; track < clip.audioTrackCount; track++)
        {
            player.EnableAudioTrack(track, true);
            player.SetDirectAudioMute(track, false);
            player.SetDirectAudioVolume(track, AudioManager.Instance != null
                ? Mathf.Clamp01(AudioManager.Instance.masterVolume) : 1f);
        }
        player.loopPointReached += OnEnded;
        player.errorReceived += OnError;
        player.Prepare();
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Owns(requester, lease) && !player.isPrepared && playbackError == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!Owns(requester, lease)) yield break;
        if (!player.isPrepared || playbackError != null)
        {
            Debug.LogWarning("[VideoPanel] Preparation impossible : " + (playbackError ?? "delai depasse"), this);
            ClearPlayback();
            yield break;
        }
        aspect.aspectRatio = player.height > 0 ? (float)player.width / player.height : 16f / 9f;
        if (beforePlayback != null) yield return beforePlayback();
        if (!Owns(requester, lease)) yield break;
        panel.Show(true);
        player.Play();
        long previousFrame = -1;
        deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Owns(requester, lease) && !ended && playbackError == null && Time.realtimeSinceStartup < deadline)
        {
            if (player.frame >= 0) onPlaybackTime?.Invoke(player.time);
            if (player.frame != previousFrame)
            {
                previousFrame = player.frame;
                deadline = Time.realtimeSinceStartup + timeoutSeconds;
            }
            if (player.texture != null)
            {
                picture.texture = player.texture;
                picture.color = Color.white;
            }
            yield return null;
        }
        if (!Owns(requester, lease)) yield break;
        if (ended) onPlaybackTime?.Invoke(clip.length);
        if (!ended) Debug.LogWarning("[VideoPanel] Lecture interrompue : " + (playbackError ?? "video bloquee"), this);
        // Keep the authored black backdrop until the caller has presented the next screen.
        ClearPlayback();
    }

    private bool Owns(object requester, int lease) => ReferenceEquals(owner, requester) && generation == lease && isActiveAndEnabled;
    private void OnEnded(VideoPlayer source) => ended = true;
    private void OnError(VideoPlayer source, string message) => playbackError = message;

    private void ClearPlayback()
    {
        if (picture != null) { picture.texture = null; picture.color = Color.clear; }
        if (player == null) return;
        player.loopPointReached -= OnEnded;
        player.errorReceived -= OnError;
        player.Stop();
    }

    // Music can intentionally enter at a cinematic cue while ambience/UI sources stay muted.
    public void AllowMusic(object requester, AudioManager manager)
    {
        if (!ReferenceEquals(owner, requester) || manager == null) return;
        var restored = new List<AudioSource>();
        foreach (var pair in mutedSources)
        {
            if (pair.Key == null || !manager.IsMusicSource(pair.Key)) continue;
            pair.Key.mute = pair.Value;
            restored.Add(pair.Key);
        }
        foreach (var source in restored) mutedSources.Remove(source);
    }

    public void StopAndHide(object requester)
    {
        if (!ReferenceEquals(owner, requester) || owner == null) return;
        Release();
    }

    private void Release()
    {
        generation++;
        ClearPlayback();
        panel?.Hide(true);
        foreach (var pair in mutedSources)
            if (pair.Key != null) pair.Key.mute = pair.Value;
        mutedSources.Clear();
        UIManager.PopFocus(this);
        owner = null;
    }

    private void OnDisable() { if (owner != null) Release(); }
}
