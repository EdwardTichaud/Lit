using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public sealed class GrappleTestRunner : ICallbacks
{
    private static TestRunnerApi api;
    static GrappleTestRunner() => EditorApplication.update += Poll;
    private static void Poll()
    {
        string request = GrappleAuthoring.Root + "/Tests.request";
        if (api != null || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request); api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new GrappleTestRunner());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^GrappleTests", "^AbilitySystemTests" } }) { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor tests) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, GrappleAuthoring.Root + "/Tests.xml");
        api.UnregisterCallbacks(this); Object.DestroyImmediate(api); api = null;
    }
}
