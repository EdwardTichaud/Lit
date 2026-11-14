using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
internal sealed class ExplorationAbilityValidationRunner : ICallbacks
{
    private const string Request = "Library/TraversalRemovalTests.request";
    private static TestRunnerApi runner;
    static ExplorationAbilityValidationRunner() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (runner != null || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        File.Delete(Request);
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new ExplorationAbilityValidationRunner());
        runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = new[] { "^ExplorationAbilityTests$", "^AbilitySystemTests$", "^LadderEndpointSelectionTests$" }
        }) { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor test) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, "Library/TraversalRemovalTests.xml");
        runner.UnregisterCallbacks(this);
        Object.DestroyImmediate(runner);
        runner = null;
    }
}
