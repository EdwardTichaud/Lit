using System.IO;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
internal sealed class AurellReunionValidation : ICallbacks
{
    private const string Output = "Library/AurellFirstEncounter";
    private static TestRunnerApi runner;
    static AurellReunionValidation() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (runner != null || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/Tests.request")) return;
        File.Delete(Output + "/Tests.request");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                File.WriteAllText(Output + "/Tests.result", "BLOCKED: save or discard the modified scene explicitly before running tests; no save dialog opened.");
                return;
            }
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new AurellReunionValidation());
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^AurellReunionTests$", "^SoloTeamProgressionTests$", "^MaisonCompanionReunionTests$",
                "^CycleProgressionTests$", "^CycleAutoDialogueTests$", "^CycleAutoDialogueControllerTests$", "^CycleRewardSkillPaletteTests$" } })
            { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor test) => File.WriteAllText(Output + "/Progress.txt", "Started " + test.FullName);
    public void TestStarted(ITestAdaptor test) => File.AppendAllText(Output + "/Progress.txt", "\n" + test.FullName);
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, Output + "/Tests.xml");
        File.WriteAllText(Output + "/Tests.result", $"{result.ResultState}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
        runner.UnregisterCallbacks(this);
        Object.DestroyImmediate(runner);
        runner = null;
    }
}
