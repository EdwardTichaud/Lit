using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public sealed class CycleTestRunner : ICallbacks
{
    private static TestRunnerApi runner;
    static CycleTestRunner() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (runner != null || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Library/CycleTests.request")) return;
        File.Delete("Library/CycleTests.request");
        Run();
    }
    [MenuItem("Lit/Narrative/Executer les tests des cycles")]
    public static void Run()
    {
        if (runner != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Never open a save/discard prompt from an unattended test request.
        // TestRunner changes scenes; preserve all manual edits first.
        var modifiedScenes = new System.Collections.Generic.List<string>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.isDirty) modifiedScenes.Add(string.IsNullOrEmpty(scene.path) ? scene.name + " (non enregistree)" : scene.path);
        }
        if (modifiedScenes.Count > 0)
        {
            string sceneNames = string.Join("\n- ", modifiedScenes);
            File.WriteAllText("Library/CycleTests.result", "NOT RUN: save modified scenes before running cycle tests.\n- " + sceneNames);
            Debug.LogWarning("[Cycle] Tests non lances : enregistrer les scenes modifiees puis relancer.\n- " + sceneNames);
            return;
        }
        File.WriteAllText("Library/CycleTests.result", "RUNNING " + System.DateTime.UtcNow.ToString("O"));
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new CycleTestRunner());
        try
        {
            runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                groupNames = new[] { "^CycleProgressionTests", "^CycleTests", "^CycleAutoDialogueTests", "^CycleAutoDialogueControllerTests", "^CycleTimelineBindingTests", "^CycleTimelinePresentationTests", "^CycleGroupSequenceGateTests", "^NinaCycleTests", "^LucCycleTests",
                    "^CycleTimelineRuntimeTests", "^CycleZoneTriggerTests", "^BelmontCycleTests", "^BelmontReleaseSceneTests", "^EtienneCycleTests", "^ArdentCycleTests", "^ClairmontCycleTests" } }) { runSynchronously = true });
        }
        catch (System.Exception exception)
        {
            File.WriteAllText("Library/CycleTests.result", "ERROR: " + exception);
            if (runner != null) Object.DestroyImmediate(runner);
            runner = null;
            throw;
        }
    }
    public void RunStarted(ITestAdaptor test) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, "Library/CycleTests.xml");
        File.WriteAllText("Library/CycleTests.result", "Passed=" + result.PassCount + "; Failed=" + result.FailCount + "; Skipped=" + result.SkipCount);
        runner.UnregisterCallbacks(this);
        Object.DestroyImmediate(runner);
        runner = null;
    }
}
