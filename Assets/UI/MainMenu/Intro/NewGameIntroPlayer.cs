using System.Collections;
using UnityEngine;

// New-game orchestration stays in the menu; all presentation uses Bootstrap's shared panel.
public sealed class NewGameIntroPlayer : MonoBehaviour
{
    public const string IntroResourcePath = "Videos/Lit_Intro";
    public const float MinimumPreRollLoadingSeconds = 2.5f;
    public const double HouseMusicCueSeconds = 19d;
    private BootstrapVideoPanel panel;
    private VideoTimeCue musicCue;
    private float minimumLoadingUntil;

    public IEnumerator Play()
    {
        yield return LoadingScreenService.ShowAndWaitForPresentation("Preparation de l'introduction...");
        minimumLoadingUntil = Time.realtimeSinceStartup + MinimumPreRollLoadingSeconds;
        panel = FindAnyObjectByType<BootstrapVideoPanel>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogWarning("[Intro] VideoPanel de Bootstrap absent. Poursuite du chargement de la Maison.", this);
            yield break;
        }
        musicCue = new VideoTimeCue(HouseMusicCueSeconds, StartHouseMusic);
        yield return panel.PlayResource(IntroResourcePath, this, PreparePlayback, musicCue.Evaluate);
    }

    private IEnumerator PreparePlayback()
    {
        // Both decoders must be ready before the 28-second film begins; slow disks
        // extend the loading screen, never move the cue relative to the film.
        var music = GameFlowService.Instance != null ? GameFlowService.Instance.HubMusic : null;
        var clip = music != null ? music.audioClip : null;
        if (clip != null)
        {
            bool requested = clip.loadState != AudioDataLoadState.Unloaded || clip.LoadAudioData();
            while (requested && panel != null && panel.IsOwnedBy(this) &&
                   (clip.loadState == AudioDataLoadState.Loading || clip.loadState == AudioDataLoadState.Unloaded))
                yield return null;
            if (clip.loadState != AudioDataLoadState.Loaded)
                Debug.LogWarning("[Intro] La musique de Maison n'a pas pu etre prechargee.", this);
        }
        while (panel != null && panel.IsOwnedBy(this) && Time.realtimeSinceStartup < minimumLoadingUntil)
            yield return null;
        // Playback is revealed by the shared panel in the same frame, after this callback.
        if (panel != null && panel.IsOwnedBy(this)) LoadingScreenService.HideImmediately();
    }

    private void StartHouseMusic()
    {
        var flow = GameFlowService.Instance;
        if (flow == null || !flow.BeginHubIntroMusic())
        {
            Debug.LogWarning("[Intro] Musique de Maison indisponible au repere de 19 secondes.", this);
            return;
        }
        panel.AllowMusic(this, AudioManager.Instance);
    }

    private void OnDisable()
    {
        if (panel != null) panel.StopAndHide(this);
    }
}

// One-shot cue driven exclusively by the media clock (not wall time or scene progress).
public sealed class VideoTimeCue
{
    private readonly double seconds;
    private readonly System.Action callback;
    public bool Fired { get; private set; }
    public VideoTimeCue(double seconds, System.Action callback)
    {
        this.seconds = seconds;
        this.callback = callback;
    }
    public void Evaluate(double videoTime)
    {
        if (Fired || double.IsNaN(videoTime) || double.IsInfinity(videoTime) || videoTime < seconds) return;
        Fired = true;
        callback?.Invoke();
    }
}
