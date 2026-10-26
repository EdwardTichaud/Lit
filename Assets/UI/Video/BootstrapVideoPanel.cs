using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

// Shared cinematic video surface authored in Bootstrap/UI_Overlay/VideoPanel.
// A caller owns the panel through playback and the handoff to its next screen.
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class BootstrapVideoPanel : MonoBehaviour
{
    [SerializeField] private VideoPlayer player;
    [SerializeField] private RawImage picture;
    [SerializeField] private AspectRatioFitter aspect;
    [SerializeField] private UiPanel panel;
    [SerializeField, Min(1f)] private float timeoutSeconds = 20f;
    private readonly Dictionary<AudioSource, bool> mutedSources = new Dictionary<AudioSource, bool>();
    private readonly List<BaseInputModule> blockedUiModules = new List<BaseInputModule>();
    private readonly VideoSkipConfirmation skip = new VideoSkipConfirmation();
    private InputAction skipStart;
    private InputAction skipConfirm;
    private TextMeshProUGUI skipPrompt;
    private bool acceptingSkip;
    private bool cursorWasVisible;
    private CursorLockMode previousCursorLock;
    private object owner;
    private int generation;
    private bool ended;
    private string playbackError;

    public bool WasSkipped { get; private set; }
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
        WasSkipped = false;
        skip.Reset();
        cursorWasVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        Cursor.visible = false;
        BlockUiModules();
        GamepadInputContextStack.Push(this, GamepadInputContext.Cinematic);
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
        LocalPlayerInput.EnsureInstance();
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
        // Private copies preserve the configured bindings without enabling Player/UI maps.
        var actions = LocalPlayerInput.FindSharedActionMap("Player");
        skipStart = actions?.FindAction("Start", false)?.Clone();
        skipConfirm = actions?.FindAction("Interact", false)?.Clone();
        skipStart?.Enable();
        skipConfirm?.Enable();
        acceptingSkip = true;
        player.Play();
        long previousFrame = -1;
        deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Owns(requester, lease) && !ended && !WasSkipped && playbackError == null && Time.realtimeSinceStartup < deadline)
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
        if (!ended && !WasSkipped) Debug.LogWarning("[VideoPanel] Lecture interrompue : " + (playbackError ?? "video bloquee"), this);
        // Keep the authored black backdrop until the caller has presented the next screen.
        ClearPlayback();
    }

    private bool Owns(object requester, int lease) => ReferenceEquals(owner, requester) && generation == lease && isActiveAndEnabled;
    private void OnEnded(VideoPlayer source) => ended = true;
    private void OnError(VideoPlayer source, string message) => playbackError = message;

    private void ClearPlayback()
    {
        acceptingSkip = false;
        skipStart?.Dispose(); skipStart = null;
        skipConfirm?.Dispose(); skipConfirm = null;
        skip.Reset();
        if (skipPrompt != null) skipPrompt.transform.parent.gameObject.SetActive(false);
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
        foreach (var module in blockedUiModules)
            if (module != null) module.enabled = true;
        blockedUiModules.Clear();
        Cursor.lockState = previousCursorLock;
        Cursor.visible = cursorWasVisible;
        GamepadInputContextStack.Pop(this);
        UIManager.PopFocus(this);
        owner = null;
    }

    private void BlockUiModules()
    {
        foreach (var module in FindObjectsByType<BaseInputModule>())
        {
            if (!module.enabled) continue;
            blockedUiModules.Add(module);
            module.enabled = false;
        }
    }

    private void Update()
    {
        if (owner == null) return;
        // Also catch an EventSystem created by a scene loaded during this lease.
        BlockUiModules();
        Cursor.visible = false;
        if (!acceptingSkip || !UIManager.HasFocus(this)) return;
        bool wasVisible = skip.IsVisible;
        if (skip.Tick(skipStart != null && skipStart.WasPressedThisFrame(),
            skipConfirm != null && skipConfirm.WasPressedThisFrame(),
            skipConfirm != null && skipConfirm.IsPressed()))
            WasSkipped = true;
        if (wasVisible != skip.IsVisible) ShowSkipPrompt(skip.IsVisible);
    }

    private void ShowSkipPrompt(bool show)
    {
        if (show && skipPrompt == null)
        {
            var root = new GameObject("VideoSkipConfirmation", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(.15f, .05f);
            rect.anchorMax = new Vector2(.85f, .2f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0, 0, 0, .85f);
            var label = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            var textRect = (RectTransform)label.transform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 8); textRect.offsetMax = new Vector2(-16, -8);
            skipPrompt = label.GetComponent<TextMeshProUGUI>();
            skipPrompt.text = "Passer la cin\u00e9matique ?\nSouthButton : confirmer     Start : continuer";
            skipPrompt.fontSize = 28;
            skipPrompt.alignment = TextAlignmentOptions.Center;
            skipPrompt.color = Color.white;
            skipPrompt.raycastTarget = false;
        }
        if (skipPrompt != null) skipPrompt.transform.parent.gameObject.SetActive(show);
    }

    private void OnDisable() { if (owner != null) Release(); }
}

// Confirmation must be a fresh South press after Start, never a held/simultaneous input.
public sealed class VideoSkipConfirmation
{
    public bool IsVisible { get; private set; }
    private bool confirmReleased;
    public void Reset() { IsVisible = false; confirmReleased = false; }
    public bool Tick(bool startPressed, bool southPressed, bool southHeld)
    {
        if (startPressed)
        {
            IsVisible = !IsVisible;
            confirmReleased = !southHeld && !southPressed;
            return false;
        }
        if (!IsVisible) return false;
        if (!southHeld && !southPressed) confirmReleased = true;
        if (!confirmReleased || !southPressed) return false;
        Reset();
        return true;
    }
}
