using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using Unity.Profiling;
using Object = UnityEngine.Object;

/// <summary>Repeatable CPU/GC checks of production influence discovery, without touching authored scenes.</summary>
public static class LitPerformanceHotPathChecks
{
    [Serializable] public sealed class Measurement
    {
        public string name;
        public int iterations;
        public double milliseconds;
        public long allocationEvents;
    }
    [Serializable] public sealed class Report
    {
        public string unityVersion, utc, device, scope;
        public int rendererCount, receiverCount;
        public bool allocationCounterAvailable;
        public List<Measurement> measurements = new List<Measurement>();
    }

    [MenuItem("Lit/Performance/Check Influence CPU And Allocations")]
    public static void Run()
    {
        var report = new Report { unityVersion = Application.unityVersion,
            utc = DateTime.UtcNow.ToString("O"), device = SystemInfo.processorType,
            scope = "Synthetic discovery microbenchmark in a preview scene. Not a game FPS baseline." };
        using (var calibration = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 32,
            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            GC.KeepAlive(new byte[4096]);
            calibration.Stop();
            report.allocationCounterAvailable = calibration.Valid && calibration.Count > 0;
        }
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Influence performance fixture");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var parentRenderer = root.AddComponent<MeshRenderer>();
            var receiver = root.AddComponent<LitPerformanceProbeReceiver>();
            receiver.enabled = false;
            var child = new GameObject("Collider");
            child.transform.SetParent(root.transform);
            var collider = child.AddComponent<BoxCollider>();
            for (int i = 0; i < 64; i++)
            {
                var leaf = new GameObject("Leaf " + i);
                leaf.transform.SetParent(child.transform);
                leaf.AddComponent<MeshRenderer>();
                leaf.AddComponent<LitPerformanceProbeReceiver>();
                if (i % 2 == 0) leaf.SetActive(false);
            }
            var source = new LitInfluenceSource();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var discoverReceivers = (Action<Collider>)Delegate.CreateDelegate(typeof(Action<Collider>), source,
                typeof(LitInfluenceSource).GetMethod("AddReceivers", flags, null, new[] {typeof(Collider)}, null));
            var discoverRenderers = (Action<Collider>)Delegate.CreateDelegate(typeof(Action<Collider>), source,
                typeof(LitInfluenceSource).GetMethod("AddMaterialRenderers", flags, null, new[] {typeof(Collider)}, null));
            var receivers = (HashSet<ILitInfluenceReceiver>)typeof(LitInfluenceSource).GetField("scannedReceivers", flags).GetValue(source);
            var renderers = (HashSet<Renderer>)typeof(LitInfluenceSource).GetField("scannedMaterialRenderers", flags).GetValue(source);
            Action discover = () => { receivers.Clear(); renderers.Clear(); discoverReceivers(collider); discoverRenderers(collider); };
            discover();
            var expectedReceivers = new HashSet<ILitInfluenceReceiver>();
            foreach (var item in collider.GetComponentsInParent<MonoBehaviour>(true))
                if (item is ILitInfluenceReceiver influenceReceiver) expectedReceivers.Add(influenceReceiver);
            foreach (var item in collider.GetComponentsInChildren<MonoBehaviour>(true))
                if (item is ILitInfluenceReceiver influenceReceiver) expectedReceivers.Add(influenceReceiver);
            var expectedRenderers = new HashSet<Renderer>(collider.GetComponentsInParent<Renderer>(true));
            expectedRenderers.UnionWith(collider.GetComponentsInChildren<Renderer>(true));
            if (!receivers.SetEquals(expectedReceivers) || !renderers.SetEquals(expectedRenderers) ||
                !receivers.Contains(receiver) || !renderers.Contains(parentRenderer))
                throw new InvalidOperationException("Influence discovery lost a parent, inactive descendant or disabled receiver.");
            report.receiverCount = receivers.Count; report.rendererCount = renderers.Count;
            for (int pass = 0; pass < 3; pass++)
                report.measurements.Add(Measure("Production influence discovery " + pass, discover, 2000, report.allocationCounterAvailable));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        BenchmarkAnimatorLookup(report);
        string label = GetArgument("-litPerformanceLabel", "current");
        string folder = "Library/LitPerformance";
        Directory.CreateDirectory(folder);
        string path = folder + "/influence-" + label + ".json";
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        UnityEngine.Debug.Log("[LitPerformance] Checks passed: " + path);
    }

    private static Measurement Measure(string name, Action action, int iterations, bool counterAvailable)
    {
        for (int i = 0; i < 32; i++) action();
        var timer = new Stopwatch();
        using (var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 16384,
            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            timer.Start();
            for (int i = 0; i < iterations; i++) action();
            timer.Stop();
            allocations.Stop();
            long events = 0;
            for (int i = 0; i < allocations.Count; i++) events += (long)allocations.GetSample(i).Count;
            return new Measurement { name = name, iterations = iterations,
                milliseconds = timer.Elapsed.TotalMilliseconds,
                allocationEvents = counterAvailable && allocations.Count < allocations.Capacity ? events : -1 };
        }
    }

    private static void BenchmarkAnimatorLookup(Report report)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Animator lookup performance fixture");
        root.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var controller = new AnimatorController();
        try
        {
            controller.AddLayer("Base Layer");
            controller.layers[0].stateMachine.AddState("Idle");
            for (int i = 0; i < 80; i++) controller.AddParameter("Parameter" + i, AnimatorControllerParameterType.Float);
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind(); animator.Update(0);
            var lookup = new AnimatorParameterLookup();
            Action original = () =>
            {
                bool found = false;
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == "Parameter79")
                    { found = true; break; }
                if (!found) throw new InvalidOperationException("Reference Animator parameter missing.");
            };
            Action optimized = () =>
            {
                if (!lookup.Has(animator, "Parameter79", AnimatorControllerParameterType.Float))
                    throw new InvalidOperationException("Cached Animator parameter missing.");
            };
            // 100 iterations keep the reference's many allocation events below recorder capacity.
            for (int pass = 0; pass < 3; pass++)
            {
                report.measurements.Add(Measure("Animator array lookup reference " + pass, original, 100, report.allocationCounterAvailable));
                report.measurements.Add(Measure("Animator cached lookup " + pass, optimized, 100, report.allocationCounterAvailable));
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(controller); }
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch entry point only.");
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception e) { UnityEngine.Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static string GetArgument(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }
}

public sealed class LitPerformanceProbeReceiver : MonoBehaviour, ILitInfluenceReceiver
{
    public void OnLitInfluenceEnter(LitInfluenceInfo info) { }
    public void OnLitInfluenceStay(LitInfluenceInfo info) { }
    public void OnLitInfluenceExit(LitInfluenceInfo info) { }
}
