using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public sealed class PlayerInPlaceTestRunner : IErrorCallbacks
{
    private static TestRunnerApi runner;
    private static string resultPath;
    static PlayerInPlaceTestRunner()
    {
        EditorApplication.update += Poll;
        if (SessionState.GetBool("PlayerInPlace.TestsRunning", false))
        {
            SuppressUnrelatedSceneRepair();
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new PlayerInPlaceTestRunner());
        }
    }
    private static void SuppressUnrelatedSceneRepair()
    {
            // This unrelated repair opens an Editor scene unconditionally after every reload,
            // including Play Mode. Keep the physics fixture isolated from that Editor side effect.
            var repair = typeof(PlayerInPlaceTestRunner).Assembly.GetType("MainMenuMissingPrefabRepair");
            if (repair != null)
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(repair.TypeHandle);
                var method = repair.GetMethod("RepairMissingPrefab", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (method != null) EditorApplication.delayCall -= (EditorApplication.CallbackFunction)System.Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), method);
            }
    }
    private static void Poll()
    {
        if (SessionState.GetBool("PlayerInPlace.TestsRunning", false) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Library/PlayerInPlaceTests.request")) return;
        string command = File.ReadAllText("Library/PlayerInPlaceTests.request").Trim();
        bool runtime = command.StartsWith("runtime");
        SessionState.SetBool("PlayerInPlace.Test.Reload", command == "runtime-reload");
        File.Delete("Library/PlayerInPlaceTests.request");
        resultPath = runtime ? (command == "runtime-reload" ? "Library/PlayerInPlaceRuntimeReloadTests.xml" : "Library/PlayerInPlaceRuntimeTests.xml") : "Library/PlayerInPlaceTests.xml";
        SessionState.SetString("PlayerInPlace.TestResult", resultPath);
        SessionState.SetBool("PlayerInPlace.TestsRunning", true);
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new PlayerInPlaceTestRunner());
        SuppressUnrelatedSceneRepair();
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { runtime ? "^PlayerInPlaceRuntimeTests" : "^PlayerInPlaceTests" } }) { runSynchronously = !runtime });
    }
    public void RunStarted(ITestAdaptor test) { }
    public void OnError(string message)
    {
        SessionState.SetBool("PlayerInPlace.TestsRunning", false);
        File.WriteAllText("Library/PlayerInPlaceTestFailure.txt", message);
        EditorApplication.isPlaying = false;
    }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, SessionState.GetString("PlayerInPlace.TestResult", "Library/PlayerInPlaceRuntimeTests.xml"));
        SessionState.SetBool("PlayerInPlace.TestsRunning", false);
        if (runner != null) { runner.UnregisterCallbacks(this); Object.DestroyImmediate(runner); }
        runner = null;
    }
}
