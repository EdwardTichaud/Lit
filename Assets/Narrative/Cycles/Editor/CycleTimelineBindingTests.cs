using System.Collections.Generic;
using Lit.Editor;
using Lit.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public sealed class CycleTimelineBindingTests
{
    private GameObject owner;
    private PlayableDirector director;
    private TimelineAsset timeline;
    private TimelineBindingProfile profile;
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        owner = new GameObject("Timeline binding test");
        director = owner.AddComponent<PlayableDirector>();
        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        director.playableAsset = timeline;
        profile = ScriptableObject.CreateInstance<TimelineBindingProfile>();
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("timeline").objectReferenceValue = timeline;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(owner);
        Object.DestroyImmediate(profile);
        foreach (var value in created) if (value != null) Object.DestroyImmediate(value);
        created.Clear();
        Object.DestroyImmediate(timeline);
    }

    private ActivationTrack Activation()
    {
        var track = timeline.CreateTrack<ActivationTrack>(null, "Visible action");
        created.Add(track);
        var clip = track.CreateDefaultClip();
        clip.duration = 4;
        created.Add(clip.asset);
        return track;
    }

    [Test]
    public void UnboundActivationCannotPassAsAConfiguredSequence()
    {
        Activation();
        var issues = TimelineBindingsValidator.CollectBindingCoverageIssues(director, profile);
        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.That(issues[0], Does.Contain("Visible action"));
    }

    [Test]
    public void NativeDirectorBindingDoesNotRequireARedundantProfileEntry()
    {
        director.SetGenericBinding(Activation(), owner);
        Assert.That(TimelineBindingsValidator.CollectBindingCoverageIssues(director, profile), Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DeclaredStableBindingIsResolvedByTheManagerLater(bool required)
    {
        var track = Activation();
        var serialized = new SerializedObject(profile);
        var bindings = serialized.FindProperty("bindings");
        bindings.arraySize = 1;
        var binding = bindings.GetArrayElementAtIndex(0);
        binding.FindPropertyRelative("track").objectReferenceValue = track;
        binding.FindPropertyRelative("bindingId").stringValue = "test.visible";
        binding.FindPropertyRelative("required").boolValue = required;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(TimelineBindingsValidator.CollectBindingCoverageIssues(director, profile), Is.Empty);
    }

    [Test]
    public void MutedTrackDoesNotRequireATarget()
    {
        Activation().muted = true;
        Assert.That(TimelineBindingsValidator.CollectBindingCoverageIssues(director, profile), Is.Empty);
    }

    [Test]
    public void EmptyAudioPlaceholderDoesNotRequireATarget()
    {
        created.Add(timeline.CreateTrack<AudioTrack>(null, "Rupture - clip a assigner"));
        Assert.That(TimelineBindingsValidator.CollectBindingCoverageIssues(director, profile), Is.Empty);
    }

    [Test]
    public void IncompatibleNativeBindingIsRejected()
    {
        director.SetGenericBinding(Activation(), owner.transform);
        Assert.That(TimelineBindingsValidator.CollectBindingCoverageIssues(director, profile), Has.Count.EqualTo(1));
    }

    [Test]
    public void MissingProfileIsReportedWithoutThrowing()
    {
        Assert.That(TimelineBindingsValidator.CollectBindingCoverageIssues(director, null), Has.Count.EqualTo(1));
    }
}
