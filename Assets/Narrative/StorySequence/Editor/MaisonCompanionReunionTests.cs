using System;
using System.IO;
using System.Linq;
using Lit.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class MaisonCompanionReunionTests
{
    [TestCase(0f, 0f, true)]
    [TestCase(3f, 0f, true)]
    [TestCase(3.1f, 0f, false)]
    [TestCase(1f, 2f, false)]
    public void ProximityRejectsDistanceAndOtherFloors(float x, float y, bool expected) =>
        Assert.That(MaisonCompanionReunion.IsWithinApproach(new Vector3(x, y, 0f), Vector3.zero, 3f, 1.5f), Is.EqualTo(expected));

    [TestCase("lucian", "", true)]
    [TestCase("", "Lucian", true)]
    [TestCase("Vesta", "Vesta", false)]
    public void OnlyLucianTriggersTheFirstConversation(string id, string name, bool expected) =>
        Assert.That(MaisonCompanionReunion.IsLucian(id, name), Is.EqualTo(expected));

    [Test] public void RuntimeBindingReusesTheIdInsteadOfCreatingDuplicateSpeakers()
    {
        var root = new GameObject("Reunion bindings test");
        try
        {
            var bindings = root.AddComponent<StorySequenceSceneBindings>();
            var actor = root.AddComponent<StorySequenceActor>();
            bindings.BindRuntimeActor("Vesta", actor);
            bindings.BindRuntimeActor(" vEsTa ", actor);
            Assert.That(bindings.ResolveActor("VESTA"), Is.SameAs(actor));
            Assert.That(new SerializedObject(bindings).FindProperty("actors").arraySize, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("Vesta")]
    [TestCase("Soren")]
    [TestCase("Aurell")]
    public void CompanionPresentationHasVisualsButNoGameplayOrNetworkAuthority(string name)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(MaisonCompanionReunionAuthoring.Folder +
            "/Presentations/" + name + "_Maison_Presentation.prefab");
        Assert.That(root, Is.Not.Null);
        Assert.That(root.GetComponent<StorySequenceActor>().ActorId, Is.EqualTo(name));
        Assert.That(root.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
        Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true).All(b => b is StorySequenceActor), Is.True);
        Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        Assert.That(root.GetComponentsInChildren<Animator>(true).Any(a => a.runtimeAnimatorController != null), Is.True);
        Assert.That(root.GetComponentsInChildren<Animator>(true).All(a => !a.applyRootMotion), Is.True);
        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Assert.That(skin.bones.Where(b => b != null).All(b => b.IsChildOf(root.transform)), Is.True);
    }

    [Test] public void AuthoredConversationIsPlayOnceManualAdvanceAndUsesAllFourSpeakers()
    {
        var sequence = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(MaisonCompanionReunionAuthoring.SequencePath);
        Assert.That(sequence, Is.Not.Null);
        Assert.That(sequence.TryValidate(out var issue), Is.True, issue);
        Assert.That(sequence.playOnce && sequence.lockPlayerControl, Is.True);
        Assert.That(sequence.startFromBlack, Is.False);
        CollectionAssert.AreEquivalent(new[] { "Lucian", "Vesta", "Soren", "Aurell" }, sequence.steps.Select(s => s.actorId).Distinct());
        var dialogues = sequence.steps.Where(s => s.type == StorySequenceStepType.Dialogue).ToArray();
        Assert.That(dialogues.Length, Is.EqualTo(17));
        Assert.That(dialogues.All(s => s.voiceLine != null && s.skippable && s.dialogueMaxDisplayDuration == 0), Is.True);
        string text = string.Join(" ", dialogues.Select(s => s.voiceLine.voiceLineText));
        StringAssert.Contains("[Munin]", text);
        StringAssert.Contains("gamins", text);
        StringAssert.Contains("trois jours", text);
        StringAssert.Contains("hier", text);
        StringAssert.Contains("deux nuits", text);
        StringAssert.Contains("pourquoi", text);
    }

    [Test] public void CinematographyHasDeliberateWideBeatsAndHeldReactionShots()
    {
        var sequence = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(MaisonCompanionReunionAuthoring.SequencePath);
        var wides = sequence.steps.Where(s => s.type == StorySequenceStepType.CameraShot).ToArray();
        Assert.That(wides.Length, Is.EqualTo(3));
        Assert.That(sequence.steps.First().type, Is.EqualTo(StorySequenceStepType.CameraShot));
        Assert.That(sequence.steps.Last().type, Is.EqualTo(StorySequenceStepType.CameraShot));
        Assert.That(wides.All(s => s.cameraProfile != null && !s.skippable &&
            s.cameraHoldDuration > s.cameraTransitionDuration), Is.True);
        var dialogues = sequence.steps.Where(s => s.type == StorySequenceStepType.Dialogue).ToArray();
        Assert.That(dialogues.Where(s => s.focusCameraOnSpeaker).All(s => s.dialogueCameraProfile != null), Is.True);
        Assert.That(dialogues.Count(s => !s.focusCameraOnSpeaker), Is.EqualTo(3));
        Assert.That(dialogues.Select(s => s.dialogueCameraProfile).Distinct().Count(), Is.GreaterThanOrEqualTo(6));
        Assert.That(dialogues.All(s => s.cameraTransitionDuration >= .7f && s.cameraTransitionDuration <= 1.2f), Is.True);
    }

    [Test] public void LegacyCameraProfilesRetainTheirAuthoredRotationAndScale()
    {
        var speaker = new GameObject("Legacy shot");
        var listener = new GameObject("Listener");
        var profile = ScriptableObject.CreateInstance<StorySequenceCameraProfile>();
        try
        {
            speaker.transform.SetPositionAndRotation(new Vector3(3, 2, 1), Quaternion.Euler(0, 72, 0));
            speaker.transform.localScale = Vector3.one * .8f;
            var offset = new Vector3(.8f, 1.6f, 2.4f);
            Assert.That(Vector3.Distance(StorySequenceCameraDriver.ResolveActorCameraPosition(
                speaker.transform, listener.transform, profile, offset), speaker.transform.TransformPoint(offset)), Is.LessThan(.0001f));
        }
        finally { Object.DestroyImmediate(speaker); Object.DestroyImmediate(listener); Object.DestroyImmediate(profile); }
    }

    [Test] public void ConversationAxisFollowsActualPlayerPositionAndReverseKeepsTheSameSide()
    {
        var speaker = new GameObject("Companion");
        var listener = new GameObject("Lucian");
        var profile = ScriptableObject.CreateInstance<StorySequenceCameraProfile>();
        try
        {
            profile.useConversationAxis = true;
            listener.transform.position = new Vector3(0, 0, 4);
            speaker.transform.rotation = Quaternion.Euler(0, 147, 0); // Actor's idle facing must not determine this shot.
            Vector3 forwardShot = StorySequenceCameraDriver.ResolveActorCameraPosition(
                speaker.transform, listener.transform, profile, new Vector3(1, 1.6f, 2));
            Assert.That(Vector3.Distance(forwardShot, new Vector3(1, 1.6f, 2)), Is.LessThan(.0001f));
            Vector3 reverse = StorySequenceCameraDriver.ResolveActorCameraPosition(
                listener.transform, speaker.transform, profile, new Vector3(-1, 1.6f, 2));
            Assert.That(reverse.x, Is.EqualTo(forwardShot.x).Within(.0001f));
            profile.centerCameraBetweenSubjects = true;
            Vector3 centered = StorySequenceCameraDriver.ResolveActorCameraPosition(
                speaker.transform, listener.transform, profile, new Vector3(0, 2, 3));
            Assert.That(Vector3.Distance(centered, new Vector3(0, 2, 5)), Is.LessThan(.0001f));
            listener.transform.position = speaker.transform.position;
            Assert.That(float.IsNaN(StorySequenceCameraDriver.ResolveActorCameraPosition(
                speaker.transform, listener.transform, profile, Vector3.one).x), Is.False);
        }
        finally { Object.DestroyImmediate(speaker); Object.DestroyImmediate(listener); Object.DestroyImmediate(profile); }
    }

    [Test] public void MaisonHasOneExplicitlyConfiguredGroupWithThreeDistinctWaitingPoints()
    {
        var scene = EditorSceneManager.OpenPreviewScene(MaisonCompanionReunionAuthoring.ScenePath);
        var previousMark = StorySequenceCompletionStore.MarkCompletedOverride;
        bool marked = false;
        StorySequenceCompletionStore.MarkCompletedOverride = _ => marked = true;
        try
        {
            var groups = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MaisonCompanionReunion>(true)).ToArray();
            Assert.That(groups.Length, Is.EqualTo(1));
            var reunion = groups[0];
            Assert.That(reunion.Companions.Length, Is.EqualTo(3));
            Assert.That(reunion.Companions.All(c => c.presentationPrefab != null && c.waitingPoint != null), Is.True);
            Assert.That(reunion.Companions.Select(c => c.waitingPoint).Distinct().Count(), Is.EqualTo(3));
            Assert.That(new SerializedObject(reunion.GetComponent<StorySequenceRunner>()).FindProperty("playOnStart").boolValue, Is.False);
            reunion.EnsurePresentations();
            var initial = reunion.Companions.Select(c => c.instance).ToArray();
            reunion.EnsurePresentations();
            CollectionAssert.AreEqual(initial, reunion.Companions.Select(c => c.instance).ToArray());
            Assert.That(marked, Is.False, "Spawning the actors must not complete their conversation.");
            for (int i = 0; i < initial.Length; i++)
            {
                Assert.That(initial[i].gameObject.scene, Is.EqualTo(scene));
                Assert.That(Vector3.Distance(initial[i].transform.position, reunion.Companions[i].waitingPoint.position), Is.LessThan(.001f));
            }
        }
        finally
        {
            StorySequenceCompletionStore.MarkCompletedOverride = previousMark;
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}

[InitializeOnLoad]
public sealed class MaisonCompanionReunionTestRunner : ICallbacks
{
    private const string Request = "Library/MaisonCompanionReunion/Tests.request";
    private static TestRunnerApi api;
    static MaisonCompanionReunionTestRunner() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            api != null || !File.Exists(Request)) return;
        File.Delete(Request);
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new MaisonCompanionReunionTestRunner());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^MaisonCompanionReunionTests", "^StorySequenceAuthoringTests", "^SoloTeamProgressionTests" } })
            { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor test) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, "Library/MaisonCompanionReunion/Tests.xml");
        api.UnregisterCallbacks(this);
        Object.DestroyImmediate(api);
        api = null;
    }
}
