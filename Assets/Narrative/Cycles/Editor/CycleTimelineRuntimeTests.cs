using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Lit.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;
using TimelineManager = Lit.Timeline.TimelineManager;

public sealed class CycleTimelineRuntimeTests
{
    private GameObject service, content;
    private Scene preview;
    private TimelineManager manager;
    private PlayableDirector director;
    private TimelineBindingProfile profile;
    private TimelineAsset timeline;
    private TimelinePlaybackHandle handle;
    private readonly List<Object> created = new();
    [SetUp]
    public void SetUp()
    {
        service = new GameObject("Isolated Timeline service"); service.AddComponent<ApplicationRoot>();
        manager = service.AddComponent<TimelineManager>();
        preview = EditorSceneManager.NewPreviewScene();
        content = new GameObject("Isolated Timeline content"); SceneManager.MoveGameObjectToScene(content, preview);
        director = content.AddComponent<PlayableDirector>();
        director.extrapolationMode = DirectorWrapMode.None;
        timeline = ScriptableObject.CreateInstance<TimelineAsset>(); director.playableAsset = timeline;
        profile = ScriptableObject.CreateInstance<TimelineBindingProfile>();
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("timeline").objectReferenceValue = timeline;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    [TearDown]
    public void TearDown()
    {
        if (handle != null && !handle.IsDone) handle.Stop();
        handle = null;
        Object.DestroyImmediate(service);
        EditorSceneManager.ClosePreviewScene(preview);
        foreach (var value in created) if (value != null) Object.DestroyImmediate(value);
        created.Clear();
        Object.DestroyImmediate(timeline); Object.DestroyImmediate(profile);
    }
    private Animator NativeAnimatedTarget()
    {
        var actor = new GameObject("Native bound prop"); actor.transform.SetParent(content.transform);
        actor.transform.localPosition = new Vector3(3, 2, 1);
        var animator = actor.AddComponent<Animator>(); actor.AddComponent<TimelineTransformRestore>();
        var track = timeline.CreateTrack<AnimationTrack>(null, "Prop pose"); created.Add(track);
        var clip = track.CreateClip<AnimationPlayableAsset>(); created.Add(clip.asset);
        var animation = new AnimationClip(); created.Add(animation);
        animation.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 3, 1, 4));
        ((AnimationPlayableAsset)clip.asset).clip = animation; clip.duration = 1;
        director.SetGenericBinding(track, animator);
        return animator;
    }
    [Test]
    public void SharedControllerCancellationDoesNotDereferenceTheClearedPlaybackField()
    {
        NativeAnimatedTarget();
        var cycle = content.AddComponent<CycleController>();
        cycle.director = director; cycle.bindingProfile = profile;
        var definition = ScriptableObject.CreateInstance<CycleDefinition>(); created.Add(definition);
        definition.cycleId = "test.timeline.cancellation"; cycle.definition = definition;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        // Edit Mode does not invoke the service's runtime Awake automatically.
        if (TimelineManager.Instance == null) typeof(TimelineManager).GetMethod("Awake", flags).Invoke(manager, null);
        Assert.That(TimelineManager.Instance, Is.SameAs(manager));
        var routine = (IEnumerator)typeof(CycleController).GetMethod("LocalCinematic", flags).Invoke(cycle, new object[] { 1 });
        Assert.That(routine.MoveNext(), Is.True, "The real native graph must be running before cancellation.");
        var owned = (TimelinePlaybackHandle)typeof(CycleController).GetField("playback", flags).GetValue(cycle);
        Assert.That(owned.State, Is.EqualTo(TimelinePlaybackState.Playing));
        typeof(CycleController).GetMethod("CancelPlayback", flags).Invoke(cycle, null);
        Assert.That(typeof(CycleController).GetField("playback", flags).GetValue(cycle), Is.Null);
        Assert.DoesNotThrow(() => Assert.That(routine.MoveNext(), Is.False));
        Assert.That(owned.State, Is.EqualTo(TimelinePlaybackState.Stopped));
    }
    [Test]
    public void ActiveUnboundTrackFailsInsteadOfCompletingAnInvisibleSequence()
    {
        var track = timeline.CreateTrack<ActivationTrack>(null, "Unbound action"); created.Add(track);
        var clip = track.CreateDefaultClip(); clip.duration = 1; created.Add(clip.asset);
        LogAssert.Expect(LogType.Error, new Regex("Timeline: .*sans cible compatible"));
        handle = manager.Play(director, profile);
        Assert.That(handle.State, Is.EqualTo(TimelinePlaybackState.Failed));
        Assert.That(director.state, Is.Not.EqualTo(PlayState.Playing));
    }
    [Test]
    public void NativeAnimatorBindingReceivesRestoreCallbacksOnStop()
    {
        var animator = NativeAnimatedTarget(); var original = animator.transform.localPosition;
        handle = manager.Play(director, profile);
        Assert.That(handle.State, Is.EqualTo(TimelinePlaybackState.Playing));
        animator.transform.localPosition = new Vector3(19, 18, 17);
        Assert.That(director.extrapolationMode, Is.EqualTo(DirectorWrapMode.Hold));
        handle.Stop();
        Assert.That(handle.State, Is.EqualTo(TimelinePlaybackState.Stopped));
        Assert.That(animator.transform.localPosition, Is.EqualTo(original));
        Assert.That(director.extrapolationMode, Is.EqualTo(DirectorWrapMode.None));
    }
    [TestCase("OnDisable")]
    [TestCase("OnDestroy")]
    public void LostServiceFailsPlaybackAndRestoresNativeParticipants(string callback)
    {
        var animator = NativeAnimatedTarget(); var original = animator.transform.localPosition;
        handle = manager.Play(director, profile);
        Assert.That(handle.State, Is.EqualTo(TimelinePlaybackState.Playing));
        animator.transform.localPosition = Vector3.one * 22;
        typeof(TimelineManager).GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
        Assert.That(handle.State, Is.EqualTo(TimelinePlaybackState.Failed));
        Assert.That(animator.transform.localPosition, Is.EqualTo(original));
    }
    [Test]
    public void DestroyedNativeTargetFailsPlaybackOnSceneUnload()
    {
        var animator = NativeAnimatedTarget();
        handle = manager.Play(director, profile);
        Assert.That(handle.State, Is.EqualTo(TimelinePlaybackState.Playing));
        Object.DestroyImmediate(animator.gameObject);
        typeof(TimelineManager).GetMethod("OnSceneUnloaded", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, new object[] { default(Scene) });
        Assert.That(handle.State, Is.EqualTo(TimelinePlaybackState.Failed));
    }
}
