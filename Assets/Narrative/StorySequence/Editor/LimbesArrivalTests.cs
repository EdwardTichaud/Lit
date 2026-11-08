using System.IO;
using System.Linq;
using Lit.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public sealed class LimbesArrivalTests
{
    private const string ScenePath = "Assets/Scenes/Reve/Reve_Limbes.unity";
    private const string SequencePath = "Assets/Narrative/StorySequence/LimbesArrival/Limbes_First_Arrival.asset";

    [Test] public void FirstArrivalUsesTheExistingSaveAwareSequenceAndExactRequestedLines()
    {
        var sequence = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(SequencePath);
        Assert.That(sequence, Is.Not.Null);
        Assert.That(sequence.TryValidate(out string issue), Is.True, issue);
        Assert.That(sequence.sequenceId, Is.EqualTo("limbes.first_arrival"));
        Assert.That(sequence.playOnce && sequence.lockPlayerControl && sequence.useUnscaledTime, Is.True);
        Assert.That(sequence.startFromBlack || sequence.fadeToBlackBeforeGameplay, Is.False);
        var dialogues = sequence.steps.Where(s => s.type == StorySequenceStepType.Dialogue).ToArray();
        CollectionAssert.AreEqual(new[] { "C'est la première fois que je vois ça...",
            "Ce château ne tourne vraiment pas rond... C'est plus grave que ce que je pensais." },
            dialogues.Select(s => s.voiceLine.voiceLineText).ToArray());
        Assert.That(dialogues.All(s => s.actorId == "Lucian" && !s.focusCameraOnSpeaker && s.skippable &&
            s.dialogueMaxDisplayDuration == 0), Is.True);
    }

    [Test] public void ThreePanoramasFinishBeforeTheNormalCameraAndDialogue()
    {
        var sequence = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(SequencePath);
        Assert.That(sequence.steps.Count, Is.EqualTo(7));
        var shots = sequence.steps.Take(3).ToArray();
        Assert.That(shots.All(s => s.type == StorySequenceStepType.CameraShot && !s.skippable &&
            s.cameraHoldDuration >= 3 && s.cameraHoldDuration > s.cameraTransitionDuration), Is.True);
        Assert.That(shots.Select(s => s.cameraPointId).Distinct().Count(), Is.EqualTo(3));
        Assert.That(shots.Sum(s => s.cameraHoldDuration), Is.EqualTo(10.5f).Within(.01f));
        Assert.That(sequence.steps[3].type, Is.EqualTo(StorySequenceStepType.SceneEvent));
        Assert.That(sequence.steps[3].eventId, Is.EqualTo("limbes.camera.gameplay"));
        Assert.That(sequence.steps[4].type, Is.EqualTo(StorySequenceStepType.Wait));
        Assert.That(sequence.steps[4].duration, Is.GreaterThan(0));
        Assert.That(sequence.steps.Skip(5).All(s => s.type == StorySequenceStepType.Dialogue && !s.focusCameraOnSpeaker), Is.True);
    }

    [Test] public void SceneHasOneArrivalGateThreeElevatedPointsAndAnExplicitCameraReturn()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var arrivals = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StorySequenceOnZoneArrival>(true)).ToArray();
            Assert.That(arrivals.Length, Is.EqualTo(1));
            var root = arrivals[0].gameObject;
            var runner = root.GetComponent<StorySequenceRunner>();
            Assert.That(runner.Sequence, Is.SameAs(AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(SequencePath)));
            Assert.That(new SerializedObject(runner).FindProperty("playOnStart").boolValue, Is.False);
            var arrival = new SerializedObject(arrivals[0]);
            Assert.That(arrival.FindProperty("runner").objectReferenceValue, Is.SameAs(runner));
            Assert.That(arrival.FindProperty("requiredSceneNames").arraySize, Is.EqualTo(2));
            var bindings = root.GetComponent<StorySequenceSceneBindings>();
            foreach (var step in runner.Sequence.steps.Take(3))
            {
                var point = bindings.ResolveCameraPoint(step.cameraPointId);
                Assert.That(point, Is.Not.Null);
                Assert.That(point.transform.position.y, Is.GreaterThan(25));
                Assert.That(point.LookTarget, Is.Not.Null);
                Assert.That(point.LookTarget.position.y, Is.LessThan(point.transform.position.y));
            }
            var serialized = new SerializedObject(bindings);
            var entry = serialized.FindProperty("events").GetArrayElementAtIndex(0);
            Assert.That(entry.FindPropertyRelative("eventId").stringValue, Is.EqualTo("limbes.camera.gameplay"));
            var call = entry.FindPropertyRelative("callback.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0);
            Assert.That(call.FindPropertyRelative("m_Target").objectReferenceValue, Is.SameAs(root.GetComponent<StorySequenceCameraDriver>()));
            Assert.That(call.FindPropertyRelative("m_MethodName").stringValue, Is.EqualTo("EndCinematic"));
            Assert.That(call.FindPropertyRelative("m_Mode").enumValueIndex, Is.EqualTo(6));
            Assert.That(call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue, Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test] public void ACompletedArrivalExitsWithoutStartingOrWritingItsCompletionAgain()
    {
        var root = new GameObject("Completed arrival fixture");
        root.SetActive(false);
        var oldCompleted = StorySequenceCompletionStore.IsCompletedOverride;
        var oldMark = StorySequenceCompletionStore.MarkCompletedOverride;
        try
        {
            var runner = root.AddComponent<StorySequenceRunner>();
            var runnerData = new SerializedObject(runner);
            runnerData.FindProperty("sequence").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(SequencePath);
            runnerData.ApplyModifiedPropertiesWithoutUndo();
            var arrival = root.AddComponent<StorySequenceOnZoneArrival>();
            var data = new SerializedObject(arrival);
            data.FindProperty("runner").objectReferenceValue = runner;
            data.ApplyModifiedPropertiesWithoutUndo();
            bool marked = false;
            StorySequenceCompletionStore.IsCompletedOverride = _ => true;
            StorySequenceCompletionStore.MarkCompletedOverride = _ => marked = true;
            var routine = (System.Collections.IEnumerator)typeof(StorySequenceOnZoneArrival).GetMethod("AwaitArrival",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(arrival, null);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(runner.IsPlaying || marked, Is.False);
        }
        finally
        {
            StorySequenceCompletionStore.IsCompletedOverride = oldCompleted;
            StorySequenceCompletionStore.MarkCompletedOverride = oldMark;
            Object.DestroyImmediate(root);
        }
    }
}

[InitializeOnLoad]
public sealed class LimbesArrivalTestRunner : ICallbacks
{
    private const string Request = "Library/LimbesArrival/Tests.request";
    private static TestRunnerApi api;
    static LimbesArrivalTestRunner() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            api != null || !File.Exists(Request)) return;
        File.Delete(Request);
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new LimbesArrivalTestRunner());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^LimbesArrivalTests" } }) { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor test) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, "Library/LimbesArrival/Tests.xml");
        api.UnregisterCallbacks(this);
        Object.DestroyImmediate(api);
        api = null;
    }
}
