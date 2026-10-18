using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// Explicit, short observation without the native Profiler's frame-history buffer.
[InitializeOnLoad]
public static class LitLightweightPlayCapture
{
    const string Key = "Lit.Performance.LightCapture";
    [Serializable] public sealed class Sample
    {
        public int frame;
        public float frameMs;
        public long mainNs, renderNs, gcBytes;
        public bool focused;
    }
    [Serializable] public sealed class Report
    {
        public string utc, outcome, note, unity, quality, graphics, device;
        public double requestToReadySeconds, sampleSeconds;
        public int skippedFrames, vSync, targetFps, width, height;
        public float timeScale;
        public long allocatedBefore, allocatedAfter;
        public bool mainAvailable, renderAvailable, gcAvailable;
        public string gpu = "Not measured: this capture does not enable the GPU Profiler.";
        public string[] scenes;
        public List<Sample> samples = new List<Sample>(4096);
    }
    static ProfilerRecorder main, render, gc;
    static Report report;
    static double stableSince, sampleStarted;
    static int sceneCount = -1, lastFrame = -1;

    static LitLightweightPlayCapture()
    {
        EditorApplication.playModeStateChanged += StateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
        if (!EditorApplication.isPlayingOrWillChangePlaymode) RestoreProfiler();
    }

    [MenuItem("Lit/Performance/Capture Bootstrap Without Profiler History")]
    public static void Start()
    {
        if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
            SceneManager.GetActiveScene().path != "Assets/Scenes/Bootstrap.unity" || ProfilerDriver.deepProfiling)
        { Debug.LogWarning("[LitPerformance] Lightweight capture requires Bootstrap in Edit Mode, with Deep Profile off."); return; }
        SessionState.SetBool(Key + ".Profiler", ProfilerDriver.enabled);
        SessionState.SetBool(Key + ".Restore", true);
        SessionState.SetFloat(Key + ".Started", (float)EditorApplication.timeSinceStartup);
        SessionState.SetBool(Key, true);
        ProfilerDriver.enabled = false;
        stableSince = 0; sceneCount = -1; report = null;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
        Debug.Log("[LitPerformance] Lightweight Bootstrap observation started (15 seconds after readiness).");
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - SessionState.GetFloat(Key + ".Started", 0) > 300)
        { Finish("timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (ProfilerDriver.enabled) { Finish("interrupted: Profiler enabled during observation"); return; }
        if (report == null)
        {
            bool ready = GameFlowService.Instance != null && !GameFlowService.Instance.IsTransitioning &&
                !GameFlowService.IsPreparingGameplayScene && LocalPlayerUtils.GetControlledCharacter() != null;
            if (!ready || sceneCount != SceneManager.sceneCount)
            { sceneCount = SceneManager.sceneCount; stableSince = now; return; }
            if (stableSince == 0) stableSince = now;
            if (now - stableSince < 10) return;
            main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            report = new Report {
                utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                requestToReadySeconds = now - SessionState.GetFloat(Key + ".Started", 0) - 10,
                vSync = QualitySettings.vSyncCount, targetFps = Application.targetFrameRate,
                width = Screen.width, height = Screen.height, timeScale = Time.timeScale,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                graphics = SystemInfo.graphicsDeviceType.ToString(), device = SystemInfo.graphicsDeviceName,
                allocatedBefore = Profiler.GetTotalAllocatedMemoryLong(),
                mainAvailable = main.Valid, renderAvailable = render.Valid, gcAvailable = gc.Valid,
                scenes = new string[SceneManager.sceneCount],
                note = "Editor update samples at most once per observed Play frame. Skipped frames are not reconstructed. Focus is recorded per sample; this is not a standalone Player baseline. Request-to-ready includes Play entry and excludes the final 10-second settling window."
            };
            for (int i = 0; i < report.scenes.Length; i++) report.scenes[i] = SceneManager.GetSceneAt(i).path;
            sampleStarted = now; lastFrame = Time.frameCount;
            Debug.Log("[LitPerformance] Lightweight steady-state sampling started.");
            return;
        }
        if (Time.frameCount != lastFrame)
        {
            report.skippedFrames += Math.Max(0, Time.frameCount - lastFrame - 1);
            lastFrame = Time.frameCount;
            report.samples.Add(new Sample {
                frame = lastFrame, frameMs = Time.unscaledDeltaTime * 1000,
                mainNs = main.Valid && main.Count > 0 ? main.LastValue : -1,
                renderNs = render.Valid && render.Count > 0 ? render.LastValue : -1,
                gcBytes = gc.Valid && gc.Count > 0 ? gc.LastValue : -1,
                focused = Application.isFocused
            });
        }
        if (QualitySettings.vSyncCount != report.vSync || Application.targetFrameRate != report.targetFps ||
            QualitySettings.names[QualitySettings.GetQualityLevel()] != report.quality ||
            Screen.width != report.width || Screen.height != report.height || Time.timeScale != report.timeScale ||
            SceneManager.sceneCount != report.scenes.Length)
        { Finish("interrupted: observation settings changed"); return; }
        if (now - sampleStarted >= 15) Finish("observed");
    }

    static void Finish(string outcome)
    {
        if (!SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        DisposeRecorders();
        if (report == null) report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion };
        else report.sampleSeconds = EditorApplication.timeSinceStartup - sampleStarted;
        report.outcome = outcome;
        report.allocatedAfter = Profiler.GetTotalAllocatedMemoryLong();
        string path = "Library/LitPerformance/light-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json";
        Directory.CreateDirectory("Library/LitPerformance");
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log("[LitPerformance] Lightweight capture: " + outcome + " | " + Path.GetFullPath(path));
        // Inventory after all timing samples, so its traversal/export cannot enter their distribution.
        if (outcome == "observed")
        {
            SessionState.SetBool(Key + ".MemoryAfterEdit", true);
            try { LitLoadedAssetMemory.Capture(); }
            catch (Exception e) { Debug.LogException(e); }
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else RestoreProfiler();
    }

    static void StateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Key, false)) Finish("interrupted: Play exited");
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            RestoreProfiler();
            if (SessionState.GetBool(Key + ".MemoryAfterEdit", false))
            {
                SessionState.SetBool(Key + ".MemoryAfterEdit", false);
                EditorApplication.delayCall += LitLoadedAssetMemory.Capture;
            }
            report = null;
        }
    }
    static void BeforeReload()
    {
        if (report != null && SessionState.GetBool(Key, false)) Finish("interrupted: assembly reload");
        DisposeRecorders();
    }
    static void DisposeRecorders() { main.Dispose(); render.Dispose(); gc.Dispose(); }
    static void RestoreProfiler()
    {
        if (!SessionState.GetBool(Key + ".Restore", false)) return;
        ProfilerDriver.enabled = SessionState.GetBool(Key + ".Profiler", false);
        SessionState.SetBool(Key + ".Restore", false);
    }
}
