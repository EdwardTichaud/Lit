#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EnolaKnifeTests
{
    [Test]
    public void EightBeatsPermitOneScoringPressEach()
    {
        var rhythm = new EnolaKnifeRhythm(10);
        for (int beat = 0; beat < EnolaKnifeRhythm.BeatCount; beat++)
        {
            double time = 10 + (beat + rhythm.Window(beat).x) * EnolaKnifeRhythm.BeatDuration;
            Assert.That(rhythm.TryPress(time, out var hit) && hit, Is.True);
            Assert.That(rhythm.TryPress(time + .01, out _), Is.False);
        }
        Assert.That(rhythm.Score, Is.EqualTo(8));
        Assert.That(rhythm.Complete(19) && !rhythm.TryPress(19, out _), Is.True);
    }
    [Test]
    public void EarlyPressConsumesBeatAndLateStartDoesNotScore()
    {
        var rhythm = new EnolaKnifeRhythm(5);
        Assert.That(rhythm.TryPress(4.99, out _), Is.False);
        Assert.That(rhythm.TryPress(5.05, out var hit), Is.True); Assert.That(hit, Is.False);
        Assert.That(rhythm.TryPress(5.69, out _), Is.False);
        Assert.That(rhythm.Score, Is.Zero);
    }
    [Test]
    public void MissedBeatsCannotBeRecoveredBySpamming()
    {
        var rhythm = new EnolaKnifeRhythm(0);
        for (int beat = 0; beat < 5; beat++)
            rhythm.TryPress((beat + rhythm.Window(beat).x) * EnolaKnifeRhythm.BeatDuration, out _);
        Assert.That(rhythm.Complete(8.81), Is.True);
        Assert.That(rhythm.Score, Is.EqualTo(5));
    }
    [Test]
    public void WindowMovesAndResizesEachPassAndAcrossRetries()
    {
        var random = new System.Random(213);
        var rhythm = new EnolaKnifeRhythm(0, random);
        Vector2 previous = rhythm.Window(0);
        for (int beat = 0; beat < EnolaKnifeRhythm.BeatCount; beat++)
        {
            Vector2 window = rhythm.Window(beat);
            double left = window.x - window.y * .5f, right = window.x + window.y * .5f;
            Assert.That(left, Is.GreaterThan(.1));
            Assert.That(right, Is.LessThan(.95));
            if (beat > 0)
            {
                Assert.That(window.x, Is.Not.EqualTo(previous.x));
                Assert.That(window.y, Is.Not.EqualTo(previous.y));
            }
            Assert.That(rhythm.InWindow((beat + left - .001) * EnolaKnifeRhythm.BeatDuration), Is.False);
            Assert.That(rhythm.InWindow((beat + left + .001) * EnolaKnifeRhythm.BeatDuration), Is.True);
            Assert.That(rhythm.InWindow((beat + right - .001) * EnolaKnifeRhythm.BeatDuration), Is.True);
            Assert.That(rhythm.InWindow((beat + right + .001) * EnolaKnifeRhythm.BeatDuration), Is.False);
            previous = window;
        }
        var retry = new EnolaKnifeRhythm(0, random, previousWindow: previous);
        Assert.That(retry.Window(0).x, Is.Not.EqualTo(previous.x));
        Assert.That(retry.Window(0).y, Is.Not.EqualTo(previous.y));
    }
    [Test]
    public void CameraStartsAheadOfTheFaceAndAimsAtTheHands()
    {
        var eyes = new Vector3(3f, 1.55f, 4f);
        var workArea = eyes + new Vector3(-.55f, -.52f, .1f);
        Pose shot = EnolaKnifeGame.ResolveKnifeCamera(eyes, Vector3.left, workArea);
        Assert.That(Vector3.Dot(shot.position - eyes, Vector3.left), Is.GreaterThan(.1f));
        Assert.That(Vector3.Angle(shot.rotation * Vector3.forward, workArea - shot.position), Is.LessThan(.1f));
    }
    [Test]
    public void AuthoredStoolUsesTheRealPointAndOneReward()
    {
        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(EnolaKnifeAuthoring.ScenePath); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(EnolaKnifeAuthoring.ScenePath, OpenSceneMode.Additive);
        try
        {
            var game = scene.GetRootGameObjects().Select(go => go.GetComponent<EnolaKnifeGame>()).Single(g => g != null);
            var action = game.GetComponent<WorldActionInteractable>();
            Assert.That(game.stoolPoint, Is.EqualTo(game.transform.Find("StoolPoint")));
            Assert.That(game.activityAnimator, Is.Not.Null);
            Assert.That(game.activityAnimator.animationClips.Select(clip => clip.humanMotion), Is.All.True,
                "The mannequin's transform-bound clips do not animate Lucian's skeleton.");
            Assert.That(action.soloOnly && !action.RequiresFlame && action.GetInteractionDetectionCollider().isTrigger, Is.True);
            Assert.That(action.onInteract.GetPersistentMethodName(0), Is.EqualTo(nameof(EnolaKnifeGame.Begin)));
            Assert.That(game.GetComponentsInChildren<RuntimeOutlineTarget>().Length, Is.GreaterThan(0));
            Assert.That(game.reward, Is.EqualTo(Resources.Load<KnowledgeSO>("Knowledges/" + EnolaKnifeGame.RewardId)));
            Assert.That(scene.GetRootGameObjects().Any(go => go.name == EnolaKnifeAuthoring.ExitName), Is.True);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == EnolaKnifeAuthoring.ScenePath), Is.True);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (original.isLoaded) SceneManager.SetActiveScene(original); }
    }
}

[InitializeOnLoad]
public sealed class EnolaKnifeTestRunner : ICallbacks
{
    private static TestRunnerApi api;
    static EnolaKnifeTestRunner() => EditorApplication.update += Poll;
    private static void Poll()
    {
        string path = EnolaKnifeAuthoring.Root + "/Tests.request";
        if (api != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(path)) return;
        File.Delete(path); api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new EnolaKnifeTestRunner());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, groupNames = new[] { "^EnolaKnifeTests", "^CharacterInteractionDetectionTests" } }) { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor tests) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, EnolaKnifeAuthoring.Root + "/Tests.xml");
        api.UnregisterCallbacks(this); UnityEngine.Object.DestroyImmediate(api); api = null;
    }
}
#endif
