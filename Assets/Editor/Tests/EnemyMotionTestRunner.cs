using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public sealed class EnemyMotionTestRunner : ICallbacks
{
    private static TestRunnerApi runner;
    static EnemyMotionTestRunner() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Library/EnemyMotionTests.request")) return;
        File.Delete("Library/EnemyMotionTests.request");
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new EnemyMotionTestRunner());
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^EnemyAggroRegressionTests" } }) { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor test) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, "Library/EnemyMotionTests.xml");
        runner.UnregisterCallbacks(this);
        Object.DestroyImmediate(runner);
    }
}
