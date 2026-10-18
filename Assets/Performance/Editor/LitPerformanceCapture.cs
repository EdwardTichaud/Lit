using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

/// <summary>Opt-in, short CPU captures in the current Edit/Play context. Deep Profile stays off.</summary>
[InitializeOnLoad]
public static class LitPerformanceCapture
{
    [Serializable] public sealed class Cost
    {
        public string marker;
        public long calls, allocatedBytes;
        public double inclusiveMs, selfMs;
    }
    [Serializable] public sealed class Report
    {
        public string utc, unity, mode, device, graphics, note, quality, profilerTarget, renderingThreadingMode;
        public string[] scenes, editorCallbacks;
        public int firstFrame, lastFrame, frames;
        public bool applicationFocused, runInBackground;
        public int targetFrameRate, vSyncCount, screenWidth, screenHeight;
        public int gpuFrames;
        public double gpuP50Ms = -1, gpuP95Ms = -1, gpuP99Ms = -1;
        public double mainThreadP50Ms, mainThreadP95Ms, mainThreadP99Ms;
        public double entrySetupSeconds, entryPreRollSeconds, entryPlaySetterSeconds, entryRequestToActiveSeconds;
        public List<Cost> cpu = new List<Cost>(), allocations = new List<Cost>();
    }
    private static bool capturing, wasEnabled, wasEditor, wasDeep, wasCpuEnabled, playing;
    private static bool wasGpuEnabled, wasMemoryEnabled;
    private static bool entryCapture;
    private static double enteredPlayAt, entryStartsAt, captureStoppedAt;
    private static double entryRequestedAt, entryProfilerReadyAt, entryRequestedPlayAt, entrySetterReturnedAt;
    private static bool forceEditLoop;
    private static int idleUpdates;
    private static readonly Dictionary<EditorWindow, bool> windowRecording = new Dictionary<EditorWindow, bool>();
    private static MethodInfo setWindowRecording;
    private static int firstFrame;
    private static double endTime;
    private static string[] callbacks, scenes;
    private static string exportedPath;
    private const string BatchKey = "Lit.Performance.BatchCapture";
    private const string BatchStartedKey = BatchKey + ".Started";
    private static double stableSince;
    private static double nextBatchStatus;
    private static int stableSceneCount = -1, batchStage;
    private static bool loadingProfileStarted;
    private static int firstLoadingFrame;
    private static double exportSettlesAt;
    private static int exportFrame;
    private const string RequestPath = "Library/LitPerformance/capture.request";

    static LitPerformanceCapture()
    {
        AssemblyReloadEvents.beforeAssemblyReload += StopWithoutExport;
        EditorApplication.quitting += StopWithoutExport;
        if (SessionState.GetBool(BatchKey, false)) EditorApplication.update += BatchTick;
        // A one-shot request is consumed only at reload; no idle file polling.
        if (File.Exists(RequestPath)) EditorApplication.delayCall += RunRequested;
    }

    private static void RunRequested()
    {
        if (!File.Exists(RequestPath)) return;
        string command = File.ReadAllText(RequestPath).Trim();
        File.Delete(RequestPath);
        if (command == "current") CaptureCurrentAfterRefresh();
        else if (command == "editor-loop")
        {
            idleUpdates = 0;
            EditorApplication.update -= StartEditorLoopWhenIdle;
            EditorApplication.update += StartEditorLoopWhenIdle;
        }
        else if (command == "entry") CapturePlayEntry();
        else if (command == "retained") Export(ProfilerDriver.firstFrameIndex, ProfilerDriver.lastFrameIndex - 1, retained: true);
        else if (command == "memory") LitLoadedAssetMemory.Capture();
        else if (command == "release-unused") LitLoadedAssetMemory.ScheduleUnusedEditorAssetRelease();
        else if (command == "finish-diagnostics")
        {
            StopDiagnosticRecording();
            LitLoadedAssetMemory.ArchiveProfilerHistory();
            LitLoadedAssetMemory.ScheduleUnusedEditorAssetRelease();
        }
        else if (command == "editor-idle-monitor")
        {
            StopDiagnosticRecording();
            LitLoadedAssetMemory.ArchiveProfilerHistory();
            LitEditorMemoryBudget.UseMonitorRefreshRate();
        }
        else if (command == "archive-profiler") LitLoadedAssetMemory.ArchiveProfilerHistory();
        else if (command == "bootstrap-light")
        {
            idleUpdates = 0;
            EditorApplication.update -= StartLightCaptureWhenIdle;
            EditorApplication.update += StartLightCaptureWhenIdle;
        }
        else if (command == "bootstrap")
        {
            LitEditorMemoryBudget.TrimIdleWorkersForCapture();
            CaptureBootstrapInOpenEditor();
        }
        else Debug.LogWarning("[LitPerformance] Unknown capture request.");
    }

    private static void CaptureCurrentAfterRefresh()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CaptureCurrentAfterRefresh;
            return;
        }
        OpenProfilerAndStart();
    }

    private static void StartEditorLoopWhenIdle()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleUpdates = 0; return; }
        if (++idleUpdates < 3) return;
        EditorApplication.update -= StartEditorLoopWhenIdle;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        OpenProfilerAndStart();
        if (!capturing) return;
        forceEditLoop = true;
        endTime = EditorApplication.timeSinceStartup + 5;
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private static void StartLightCaptureWhenIdle()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleUpdates = 0; return; }
        if (++idleUpdates < 3) return;
        EditorApplication.update -= StartLightCaptureWhenIdle;
        LitLightweightPlayCapture.Start();
    }

    private static void OpenProfilerAndStart()
    {
        bool originallyRecording = ProfilerDriver.enabled;
        EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
        Start();
        if (!capturing) return;
        // Opening a new Profiler window can itself enable recording. Restore
        // the effective state from before this tool opened/focused that window.
        wasEnabled = originallyRecording;
        foreach (var window in windowRecording.Keys.ToArray()) windowRecording[window] = originallyRecording;
    }

    private static void StopDiagnosticRecording()
    {
        if (capturing) throw new InvalidOperationException("Wait for the owned capture to finish before closing diagnostics.");
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow");
        var setter = type?.GetMethod("SetRecordingEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(bool) }, null);
        if (setter == null) throw new InvalidOperationException("Cannot synchronize the Profiler recording window in this Unity version.");
        foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(type)) setter.Invoke(window, new object[] { false });
        ProfilerDriver.enabled = false;
    }

    [MenuItem("Lit/Performance/Capture Bootstrap Play Entry")]
    private static void CapturePlayEntry()
    {
        // This short observation must survive the transition without a domain reload.
        // Do not change the user's reload settings to make a diagnostic possible.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += CapturePlayEntry; return; }
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            SceneManager.GetActiveScene().path != "Assets/Scenes/Bootstrap.unity" ||
            !EditorSettings.enterPlayModeOptionsEnabled ||
            (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0)
        {
            Debug.LogWarning("[LitPerformance] Entry capture requires Bootstrap in Edit Mode and the existing Disable Domain Reload setting.");
            return;
        }
        entryRequestedAt = EditorApplication.timeSinceStartup;
        OpenProfilerAndStart();
        if (!capturing) return;
        entryProfilerReadyAt = EditorApplication.timeSinceStartup;
        entryCapture = true;
        enteredPlayAt = 0;
        firstFrame = ProfilerDriver.lastFrameIndex;
        entryStartsAt = EditorApplication.timeSinceStartup + 2;
        endTime = EditorApplication.timeSinceStartup + 90;
        // Let native profiling activate before the synchronous Play transition.
        EditorApplication.QueuePlayerLoopUpdate();
    }

    [MenuItem("Lit/Performance/Capture Bootstrap In Current Editor")]
    public static void CaptureBootstrapInOpenEditor()
    {
        if (ProfilerDriver.deepProfiling)
        {
            Debug.LogWarning("[LitPerformance] Disable Deep Profile before the Bootstrap capture.");
            return;
        }
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            SceneManager.GetActiveScene().path != "Assets/Scenes/Bootstrap.unity")
        {
            Debug.LogWarning("[LitPerformance] Open Bootstrap in Edit Mode before this capture.");
            return;
        }
        SessionState.SetBool(BatchKey + ".Live", true);
        SessionState.SetBool(BatchKey, true);
        SessionState.SetFloat(BatchStartedKey, (float)EditorApplication.timeSinceStartup);
        EditorApplication.update -= BatchTick;
        EditorApplication.update += BatchTick;
        batchStage = 0; stableSceneCount = -1; stableSince = 0; nextBatchStatus = 0; loadingProfileStarted = false;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Lit/Performance/Capture Current Context (15 Seconds)")]
    public static void Start()
    {
        if (capturing || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        playing = EditorApplication.isPlaying;
        entryCapture = false;
        forceEditLoop = false;
        captureStoppedAt = 0;
        exportedPath = null;
        scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path).ToArray();
        callbacks = EditorApplication.update?.GetInvocationList()
            .Select(d => d.Method.DeclaringType?.FullName + "." + d.Method.Name).ToArray();
        wasEnabled = ProfilerDriver.enabled;
        wasCpuEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU);
        wasGpuEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.GPU);
        wasMemoryEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.Memory);
        wasEditor = ProfilerDriver.profileEditor;
        wasDeep = ProfilerDriver.deepProfiling;
        // Changing Deep Profile can require a domain reload. Do not disturb an existing session.
        if (wasDeep)
        {
            Debug.LogWarning("[LitPerformance] Disable Deep Profile before this capture.");
            return;
        }
        // The Profiler window reapplies its own recording state on focus.
        // Keep it in sync with the driver and restore it after this opt-in capture.
        if (!BeginWindowRecording()) return;
        ProfilerDriver.profileEditor = !playing;
        ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, true);
        ProfilerDriver.SetAreaEnabled(ProfilerArea.GPU, playing);
        ProfilerDriver.SetAreaEnabled(ProfilerArea.Memory, false);
        ProfilerDriver.enabled = true;
        firstFrame = ProfilerDriver.lastFrameIndex + 2;
        capturing = true;
        endTime = EditorApplication.timeSinceStartup + 15;
        EditorApplication.update += Tick;
        Debug.Log("[LitPerformance] CPU capture started in " + (playing ? "Play" : "Edit") + ". Keep the same view and workload for comparisons.");
        Debug.Log("[LitPerformance] Profiler target: " + ProfilerDriver.GetConnectionIdentifier(ProfilerDriver.connectedProfiler));
    }

    private static void Tick()
    {
        if ((!entryCapture && EditorApplication.isPlaying != playing) || EditorApplication.isCompiling)
        {
            StopWithoutExport();
            Debug.LogWarning("[LitPerformance] Capture cancelled: context changed.");
            return;
        }
        if (forceEditLoop && captureStoppedAt == 0) EditorApplication.QueuePlayerLoopUpdate();
        if (entryCapture && entryStartsAt > 0)
        {
            if (EditorApplication.timeSinceStartup < entryStartsAt) return;
            entryStartsAt = 0;
            Debug.Log("[LitPerformance] Entry pre-roll completed: range=" + ProfilerDriver.firstFrameIndex + ".." + ProfilerDriver.lastFrameIndex);
            entryRequestedPlayAt = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
            entrySetterReturnedAt = EditorApplication.timeSinceStartup;
            return;
        }
        if (entryCapture && EditorApplication.isPlaying)
        {
            if (enteredPlayAt == 0) enteredPlayAt = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - enteredPlayAt < 2) return;
        }
        else if (EditorApplication.timeSinceStartup < endTime) return;
        if (captureStoppedAt == 0)
        {
            ProfilerDriver.enabled = false;
            captureStoppedAt = EditorApplication.timeSinceStartup;
            return;
        }
        // The Editor connection can deliver the final frame after recording stops.
        if (EditorApplication.timeSinceStartup - captureStoppedAt < .5) return;
        int last = ProfilerDriver.lastFrameIndex - (entryCapture ? 0 : 1);
        int first = Math.Max(firstFrame, ProfilerDriver.firstFrameIndex);
        Debug.Log("[LitPerformance] Capture end: recording=" + ProfilerDriver.enabled +
            " editor=" + ProfilerDriver.profileEditor + " CPU=" + ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU) +
            " range=" + ProfilerDriver.firstFrameIndex + ".." + ProfilerDriver.lastFrameIndex);
        // Freeze recording, but keep the captured context/areas until its data
        // is read. Restoring profileEditor first can invalidate Edit-mode frames.
        ProfilerDriver.enabled = false;
        try { Export(first, last); }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            bool exitOwnedPlay = entryCapture;
            StopWithoutExport();
            if (exitOwnedPlay && EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        }
    }

    private static void StopWithoutExport()
    {
        if (!capturing) return;
        EditorApplication.update -= Tick;
        foreach (var state in windowRecording)
            if (state.Key) setWindowRecording.Invoke(state.Key, new object[] { state.Value });
        windowRecording.Clear();
        ProfilerDriver.enabled = wasEnabled;
        ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, wasCpuEnabled);
        ProfilerDriver.SetAreaEnabled(ProfilerArea.GPU, wasGpuEnabled);
        ProfilerDriver.SetAreaEnabled(ProfilerArea.Memory, wasMemoryEnabled);
        ProfilerDriver.profileEditor = wasEditor;
        capturing = false;
        entryCapture = false;
    }

    private static bool BeginWindowRecording()
    {
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        setWindowRecording = type?.GetMethod("SetRecordingEnabled", flags, null, new[] { typeof(bool) }, null);
        var readRecording = type?.GetMethod("IsSetToRecord", flags, null, Type.EmptyTypes, null);
        if (setWindowRecording == null || readRecording == null)
        {
            Debug.LogWarning("[LitPerformance] This Unity version does not expose the Profiler window recording controls. Capture not started.");
            return false;
        }
        windowRecording.Clear();
        foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(type))
            windowRecording.Add(window, (bool)readRecording.Invoke(window, null));
        foreach (var state in windowRecording)
            setWindowRecording.Invoke(state.Key, new object[] { true });
        return true;
    }

    private static void Export(int first, int last, bool loading = false, bool retained = false)
    {
        var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
            mode = retained ? "Retained mixed Profiler history (diagnostic only)" : entryCapture ? "Enter Play Mode" : loading ? "Play loading in Editor" : playing ? "Play in Editor" : "Edit", device = SystemInfo.processorType,
            graphics = SystemInfo.graphicsDeviceName, scenes = scenes, editorCallbacks = callbacks,
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
            profilerTarget = ProfilerDriver.GetConnectionIdentifier(ProfilerDriver.connectedProfiler),
            renderingThreadingMode = SystemInfo.renderingThreadingMode.ToString(),
            firstFrame = first, lastFrame = last,
            applicationFocused = Application.isFocused, runInBackground = Application.runInBackground,
            targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount,
            screenWidth = Screen.width, screenHeight = Screen.height,
            entrySetupSeconds = entryCapture ? entryProfilerReadyAt - entryRequestedAt : 0,
            entryPreRollSeconds = entryCapture ? entryRequestedPlayAt - entryProfilerReadyAt : 0,
            entryPlaySetterSeconds = entryCapture ? entrySetterReturnedAt - entryRequestedPlayAt : 0,
            entryRequestToActiveSeconds = entryCapture ? enteredPlayAt - entryRequestedPlayAt : 0,
            note = retained ? "Mixed retained history, including partial frames and earlier contexts. Diagnostic attribution only; not a timing baseline. Inclusive costs overlap." :
                forceEditLoop ? "Explicit Editor loop attribution with QueuePlayerLoopUpdate during capture. Forces Edit-mode updates/rendering, so not an idle CPU/FPS baseline. Inclusive costs overlap." :
                entryCapture ? "Entry capture attempt. Synchronous transition work can be absent from the native frames: compare coverage with PlayModeTiming before attributing entry cost. Not a timing baseline." :
                "Main-thread elapsed frame time includes waits. Profiler enabled, Deep Profile off, Memory module off. GPU timing requested only in Play; -1 means unavailable. Inclusive costs overlap. Retained Profiler ring buffer only; not a standalone Player baseline." };
        var cpu = new Dictionary<string, Cost>();
        var allocations = new Dictionary<string, Cost>();
        var markerNames = new Dictionary<int, string>();
        var times = new List<double>();
        var gpuTimes = new List<double>();
        for (int frame = first; frame <= last; frame++)
        {
            using (var data = ProfilerDriver.GetRawFrameDataView(frame, 0))
            {
                if (!data.valid || data.sampleCount == 0) continue;
                report.frames++;
                times.Add(data.frameTimeMs);
                if (data.frameGpuTimeMs > 0 && !float.IsNaN(data.frameGpuTimeMs) && !float.IsInfinity(data.frameGpuTimeMs))
                    gpuTimes.Add(data.frameGpuTimeMs);
                var parents = new Stack<int>();
                for (int i = 0; i < data.sampleCount; i++)
                {
                    while (parents.Count > 0 && i > parents.Peek() + data.GetSampleChildrenCountRecursive(parents.Peek())) parents.Pop();
                    string name = SampleName(data, i, markerNames);
                    double total = data.GetSampleTimeMs(i), self = total;
                    int end = i + data.GetSampleChildrenCountRecursive(i);
                    for (int child = i + 1; child <= end; child += 1 + data.GetSampleChildrenCountRecursive(child))
                        self -= data.GetSampleTimeMs(child);
                    if (!cpu.TryGetValue(name, out Cost cost)) cpu[name] = cost = new Cost { marker = name };
                    cost.calls++; cost.inclusiveMs += total; cost.selfMs += Math.Max(0, self);
                    if (name == "GC.Alloc" && data.GetSampleMetadataCount(i) > 0)
                    {
                        string parent = parents.Count > 0 ? SampleName(data, parents.Peek(), markerNames) : "Unattributed";
                        if (!allocations.TryGetValue(parent, out Cost alloc)) allocations[parent] = alloc = new Cost { marker = parent };
                        alloc.calls++; alloc.allocatedBytes += data.GetSampleMetadataAsLong(i, 0);
                    }
                    if (end > i) parents.Push(i);
                }
            }
        }
        times.Sort();
        if (times.Count == 0) throw new InvalidOperationException("No completed CPU frames available (requested " + first + ".." + last +
            ", available " + ProfilerDriver.firstFrameIndex + ".." + ProfilerDriver.lastFrameIndex + "). No report claimed.");
        report.mainThreadP50Ms = Percentile(times, .5);
        report.mainThreadP95Ms = Percentile(times, .95);
        report.mainThreadP99Ms = Percentile(times, .99);
        gpuTimes.Sort();
        report.gpuFrames = gpuTimes.Count;
        if (gpuTimes.Count > 0)
        {
            report.gpuP50Ms = Percentile(gpuTimes, .5);
            report.gpuP95Ms = Percentile(gpuTimes, .95);
            report.gpuP99Ms = Percentile(gpuTimes, .99);
        }
        report.cpu = cpu.Values.OrderByDescending(c => c.selfMs).Take(100).ToList();
        report.allocations = allocations.Values.OrderByDescending(c => c.allocatedBytes).Take(60).ToList();
        Directory.CreateDirectory("Library/LitPerformance");
        string path = "Library/LitPerformance/" + (retained ? "retained-" : entryCapture ? "entry-" : loading ? "loading-" : playing ? "play-" : "edit-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json";
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        exportedPath = path;
        Debug.Log("[LitPerformance] " + report.frames + " CPU frames exported to " + path);
    }

    private static double Percentile(List<double> sorted, double percentile) => sorted[Math.Min(sorted.Count - 1, Math.Max(0, (int)Math.Ceiling(sorted.Count * percentile) - 1))];

    private static string SampleName(RawFrameDataView data, int sample, Dictionary<int, string> names)
    {
        int id = data.GetSampleMarkerId(sample);
        if (!names.TryGetValue(id, out string name)) names[id] = name = data.GetSampleName(sample);
        return name;
    }

    /// <summary>Runs the real Bootstrap flow only in the existing disposable validation project, with graphics.</summary>
    public static void RunBatchPlay()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/Library/ActorAnimationSandbox/"))
            throw new InvalidOperationException("Use the isolated ActorAnimationSandbox project for batch gameplay capture.");
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("Gameplay capture requires graphics; omit -nographics.");
        if (ProfilerDriver.deepProfiling) throw new InvalidOperationException("Disable Deep Profile before the Bootstrap capture.");
        SessionState.SetBool(BatchKey, true);
        SessionState.SetBool(BatchKey + ".Live", false);
        SessionState.SetFloat(BatchStartedKey, (float)EditorApplication.timeSinceStartup);
        EditorApplication.update -= BatchTick;
        EditorApplication.update += BatchTick;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
        foreach (var root in scene.GetRootGameObjects())
        foreach (var saves in root.GetComponentsInChildren<SaveSessionManager>(true))
        {
            var serialized = new SerializedObject(saves);
            serialized.FindProperty("savesRootFolder").stringValue = Path.GetFullPath("Library/LitPerformance/TemporarySaves");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        batchStage = 0; stableSceneCount = -1; stableSince = 0; nextBatchStatus = 0; loadingProfileStarted = false;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    private static void BatchTick()
    {
        if (!SessionState.GetBool(BatchKey, false)) return;
        // Play Mode already advances the player loop. Requeuing it every
        // editor tick rebuilds scheduling state and heavily perturbs GC/CPU.
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(BatchStartedKey, 0) > 240)
        {
            FinishBatch(false, "Gameplay capture timed out before producing a valid report.");
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (batchStage == 0)
        {
            if (!loadingProfileStarted)
            {
                playing = true;
                wasEnabled = ProfilerDriver.enabled;
                wasCpuEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU);
                wasGpuEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.GPU);
                wasMemoryEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.Memory);
                wasEditor = ProfilerDriver.profileEditor;
                ProfilerDriver.profileEditor = false;
                ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, true);
                ProfilerDriver.SetAreaEnabled(ProfilerArea.GPU, true);
                ProfilerDriver.SetAreaEnabled(ProfilerArea.Memory, false);
                firstLoadingFrame = ProfilerDriver.lastFrameIndex + 2;
                ProfilerDriver.enabled = true;
                capturing = true;
                loadingProfileStarted = true;
            }
            if (EditorApplication.timeSinceStartup >= nextBatchStatus)
            {
                nextBatchStatus = EditorApplication.timeSinceStartup + 15;
                Debug.Log("[LitPerformance] Loading status | frame=" + Time.frameCount +
                    " | scenes=" + string.Join(", ", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name)) +
                    " | flow=" + (GameFlowService.Instance != null) +
                    " | transitioning=" + (GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning) +
                    " | navigation=" + (NavMeshWorldService.Instance != null ? NavMeshWorldService.Instance.State.ToString() : "absent"));
            }
            if (!SessionState.GetBool(BatchKey + ".Live", false))
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }
            if (GameFlowService.Instance == null || GameFlowService.Instance.IsTransitioning ||
                GameFlowService.IsPreparingGameplayScene || LocalPlayerUtils.GetControlledCharacter() == null)
            { stableSince = EditorApplication.timeSinceStartup; return; }
            if (stableSceneCount != SceneManager.sceneCount)
            { stableSceneCount = SceneManager.sceneCount; stableSince = EditorApplication.timeSinceStartup; }
            if (EditorApplication.timeSinceStartup - stableSince < 10) return;
            scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path).ToArray();
            callbacks = EditorApplication.update?.GetInvocationList().Select(d => d.Method.DeclaringType?.FullName + "." + d.Method.Name).ToArray();
            try { Export(Math.Max(firstLoadingFrame, ProfilerDriver.firstFrameIndex), ProfilerDriver.lastFrameIndex - 1, true); }
            catch (Exception e) { Debug.LogException(e); }
            StopWithoutExport();
            exportSettlesAt = EditorApplication.timeSinceStartup + 2;
            exportFrame = Time.frameCount;
            batchStage = 1;
        }
        else if (batchStage == 1)
        {
            if (EditorApplication.timeSinceStartup < exportSettlesAt || Time.frameCount < exportFrame + 10) return;
            Start();
            if (!capturing) { FinishBatch(false, "CPU capture did not start."); return; }
            batchStage = 2;
        }
        else if (!capturing) FinishBatch(!string.IsNullOrEmpty(exportedPath), exportedPath ?? "Capture interrupted.");
    }

    private static void FinishBatch(bool success, string message)
    {
        StopWithoutExport();
        SessionState.SetBool(BatchKey, false);
        EditorApplication.update -= BatchTick;
        if (success)
        {
            try { LitLoadedAssetMemory.Capture(); }
            catch (Exception e) { Debug.LogException(e); }
        }
        Debug.Log("[LitPerformance] " + message);
        if (SessionState.GetBool(BatchKey + ".Live", false))
        {
            SessionState.SetBool(BatchKey + ".Live", false);
            if (success)
            {
                EditorApplication.playModeStateChanged -= CaptureReturnedEditorMemory;
                EditorApplication.playModeStateChanged += CaptureReturnedEditorMemory;
            }
            EditorApplication.isPlaying = false;
        }
        else EditorApplication.Exit(success ? 0 : 1);
    }

    private static void CaptureReturnedEditorMemory(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= CaptureReturnedEditorMemory;
        EditorApplication.delayCall += LitLoadedAssetMemory.Capture;
    }
}
