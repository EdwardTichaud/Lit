using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class BootstrapVideoPanelTests
{
    private Scene preview;
    private BootstrapVideoPanel panel;

    [OneTimeSetUp] public void OpenAuthoredBootstrap()
    {
        preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Bootstrap.unity");
        panel = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BootstrapVideoPanel>(true)).Single();
    }
    [OneTimeTearDown] public void ClosePreview()
    {
        if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
    }

    [Test] public void HouseMusicCueUsesTheVideoClockAndFiresOnlyOnce()
    {
        int starts = 0;
        var cue = new VideoTimeCue(NewGameIntroPlayer.HouseMusicCueSeconds, () => starts++);
        foreach (double time in new[] { 0d, 8d, 18.99d, 18.99d, 18.99d }) cue.Evaluate(time);
        Assert.That(starts, Is.Zero, "Buffering/pause before 19s must not advance the cue.");
        cue.Evaluate(19d);
        Assert.That(starts, Is.EqualTo(1));
        foreach (double time in new[] { 19.1d, 28d, 0d, 19d }) cue.Evaluate(time);
        Assert.That(starts, Is.EqualTo(1), "Later frames and a backwards seek must not restart the track.");
    }

    [Test] public void DroppedFramesStillCrossTheCueAndANewGameGetsANewCue()
    {
        int starts = 0;
        var firstRun = new VideoTimeCue(19d, () => starts++);
        firstRun.Evaluate(18.8d);
        firstRun.Evaluate(19.4d);
        firstRun.Evaluate(20d);
        Assert.That(starts, Is.EqualTo(1));
        var nextRun = new VideoTimeCue(19d, () => starts++);
        nextRun.Evaluate(0d);
        Assert.That(starts, Is.EqualTo(1));
        nextRun.Evaluate(19d);
        Assert.That(starts, Is.EqualTo(2));
    }

    [Test] public void UnpreparedOrInvalidVideoTimesCannotTriggerTheCue()
    {
        var cue = new VideoTimeCue(19d, () => Assert.Fail("Invalid media timestamp triggered music."));
        foreach (double time in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, 0d })
            cue.Evaluate(time);
        Assert.That(cue.Fired, Is.False);
    }

    [Test] public void MaisonTrackCanContinueThroughLongLoadingAndTimeScaleChanges()
    {
        var flow = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameFlowService>(true)).Single();
        var music = flow.HubMusic;
        Assert.That(music, Is.Not.Null);
        Assert.That(music.audioClip, Is.Not.Null);
        Assert.That(music.loop, Is.True, "Long loading must not exhaust the music.");
        Assert.That(music.affectedByTimeScale, Is.False);
        Assert.That(Resources.Load<VideoClip>(NewGameIntroPlayer.IntroResourcePath).length,
            Is.GreaterThan(NewGameIntroPlayer.HouseMusicCueSeconds));
    }

    [Test] public void CueUnmutesOnlyMusicAndVideoReleaseDoesNotRemuteIt()
    {
        var ownerRoot = new GameObject("Video audio lease test");
        ownerRoot.SetActive(false);
        SceneManager.MoveGameObjectToScene(ownerRoot, preview);
        try
        {
            var manager = ownerRoot.AddComponent<AudioManager>();
            var video = ownerRoot.AddComponent<BootstrapVideoPanel>();
            var musicA = ownerRoot.AddComponent<AudioSource>();
            var musicB = ownerRoot.AddComponent<AudioSource>();
            var ambience = ownerRoot.AddComponent<AudioSource>();
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(AudioManager).GetField("primarySource", fields).SetValue(manager, musicA);
            typeof(AudioManager).GetField("secondarySource", fields).SetValue(manager, musicB);
            var owner = new object();
            typeof(BootstrapVideoPanel).GetField("owner", fields).SetValue(video, owner);
            var muted = (System.Collections.Generic.Dictionary<AudioSource, bool>)typeof(BootstrapVideoPanel)
                .GetField("mutedSources", fields).GetValue(video);
            musicA.mute = musicB.mute = ambience.mute = true;
            muted[musicA] = false;
            muted[musicB] = true;
            muted[ambience] = false;
            video.AllowMusic(new object(), manager);
            Assert.That(musicA.mute, Is.True, "Another caller cannot release this video's mute.");
            video.AllowMusic(owner, manager);
            Assert.That(musicA.mute, Is.False);
            Assert.That(musicB.mute, Is.True, "An authored music mute must be preserved.");
            Assert.That(ambience.mute, Is.True, "Ambience remains muted during the film.");
            video.StopAndHide(owner);
            Assert.That(musicA.mute, Is.False, "The handoff must not remute Maison music.");
            Assert.That(ambience.mute, Is.False);
            Assert.That(video.IsBusy, Is.False);
        }
        finally { Object.DestroyImmediate(ownerRoot); }
    }

    [Test] public void SharedPlayerIsTheExactAuthoredOverlayPlayer()
    {
        Assert.That(panel.IsConfigured, Is.True);
        Assert.That(panel.name, Is.EqualTo("VideoPanel"));
        Assert.That(panel.transform.parent.name, Is.EqualTo("UI_Overlay"));
        Assert.That(panel.Player.name, Is.EqualTo("VideoPanel_Root"));
        Assert.That(panel.Player.transform.parent, Is.EqualTo(panel.transform));
        Assert.That(panel.GetComponentInParent<ApplicationRoot>(), Is.Not.Null);
        Assert.That(panel.GetComponentsInChildren<VideoPlayer>(true).Length, Is.EqualTo(1));
    }

    [Test] public void IntroIsTheImportedResourcesVideoWithAudio()
    {
        var clip = Resources.Load<VideoClip>(NewGameIntroPlayer.IntroResourcePath);
        Assert.That(clip, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo("Assets/Resources/Videos/Lit_Intro.mp4"));
        Assert.That(panel.Player.clip, Is.EqualTo(clip));
        Assert.That(clip.width, Is.GreaterThan(0));
        Assert.That(clip.frameCount, Is.GreaterThan(0));
        Assert.That(clip.audioTrackCount, Is.GreaterThan(0));
        Assert.That(panel.Player.playOnAwake, Is.False);
        Assert.That(panel.Player.isLooping, Is.False);
        Assert.That(panel.Player.source, Is.EqualTo(VideoSource.VideoClip));
    }

    [Test] public void AuthoredSurfaceIsHiddenBlackAndKeepsTheVideoAspectRatio()
    {
        var serialized = new SerializedObject(panel);
        Assert.That(serialized.FindProperty("player").objectReferenceValue, Is.EqualTo(panel.Player));
        var image = (RawImage)serialized.FindProperty("picture").objectReferenceValue;
        var aspect = (AspectRatioFitter)serialized.FindProperty("aspect").objectReferenceValue;
        var ui = (UiPanel)serialized.FindProperty("panel").objectReferenceValue;
        Assert.That(image.gameObject, Is.EqualTo(panel.Player.gameObject));
        Assert.That(aspect.gameObject, Is.EqualTo(panel.Player.gameObject));
        Assert.That(aspect.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.FitInParent));
        Assert.That(panel.Player.renderMode, Is.EqualTo(VideoRenderMode.APIOnly));
        Assert.That(ui.VisibleAtStartup, Is.False);
        Assert.That(ui.CanvasGroup.alpha, Is.Zero);
        Assert.That(ui.CanvasGroup.blocksRaycasts, Is.False);
        Assert.That(panel.GetComponent<Image>().color, Is.EqualTo(Color.black));
    }
}
