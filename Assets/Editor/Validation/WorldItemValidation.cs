using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
internal sealed class WorldItemValidation : ICallbacks
{
    private const string DirectoryPath = "Library/WorldItemAudit";
    private const string Request = DirectoryPath + "/Tests.request";
    private const string AuditRequest = DirectoryPath + "/Audit.request";
    private static TestRunnerApi runner;

    static WorldItemValidation() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (runner != null || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Request))
        {
            File.Delete(Request);
            Run();
        }
        else if (File.Exists(AuditRequest))
        {
            File.Delete(AuditRequest);
            WorldItemAudit.WriteReport(false);
        }
    }

    [MenuItem("Lit/Objets monde/Valider les raccords")]
    private static void Run()
    {
        if (runner != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new WorldItemValidation());
        runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = new[] { "^WorldItemAuthoringTests$", "^ScenePersistenceContinuityTests$" }
        }) { runSynchronously = true });
    }

    public void RunStarted(ITestAdaptor test) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        Directory.CreateDirectory(DirectoryPath);
        TestRunnerApi.SaveResultToFile(result, DirectoryPath + "/Tests.xml");
        runner.UnregisterCallbacks(this);
        Object.DestroyImmediate(runner);
        runner = null;
    }
}
