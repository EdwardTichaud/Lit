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
        AssetDatabase.ImportAsset("Assets/Narrative/NinaCycle/Data/Enemy_ScientifiqueFou.asset", ImportAssetOptions.ForceUpdate);
        var scientist = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Narrative/NinaCycle/Data/Enemy_ScientifiqueFou.asset");
        File.WriteAllText("Library/CycleScientistImported.json", JsonUtility.ToJson(scientist, true));
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new CycleTestRunner());
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^CycleProgressionTests", "^CycleTests", "^NinaCycleTests" } }) { runSynchronously = true });
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
    }
}
