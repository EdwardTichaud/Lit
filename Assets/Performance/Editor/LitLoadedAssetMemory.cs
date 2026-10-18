using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

/// <summary>Explicit loaded-resource inventory. Does not load assets or change import settings.</summary>
internal static class LitLoadedAssetMemory
{
    private static int releaseIdleUpdates;

    public static void ScheduleUnusedEditorAssetRelease()
    {
        releaseIdleUpdates = 0;
        EditorApplication.update -= ReleaseWhenIdle;
        EditorApplication.update += ReleaseWhenIdle;
    }

    private static void ReleaseWhenIdle()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { releaseIdleUpdates = 0; return; }
        if (++releaseIdleUpdates < 3) return;
        EditorApplication.update -= ReleaseWhenIdle;
        ReleaseUnusedEditorAssets();
    }
    [Serializable] private sealed class ActorState
    {
        public string name;
        public bool driving, grounded;
        public int simulationIndex;
        public Vector3 position;
    }
    [Serializable] private sealed class AssetCost
    {
        public string name, path, type;
        public long bytes;
        public int width, height;
    }
    [Serializable] private sealed class Report
    {
        public string utc, mode;
        public long allocatedBytes, reservedBytes, managedBytes;
        public long profilerUsedBytes, profilerReservedBytes, graphicsUsedBytes;
        public ulong currentTextureBytes, nonStreamingTextureBytes;
        public bool streamingEnabled;
        public int graphicsMemoryMB;
        public long loadedTextureBytes, loadedMeshBytes, loadedAudioBytes, loadedAnimationBytes;
        public int textureCount, meshCount, audioCount, animationCount;
        public List<AssetCost> largestAssets;
        public List<ActorState> actors = new List<ActorState>();
        public string note = "Loaded objects only, including Editor assets and render targets. Runtime size estimates are not process RAM or GPU residency. No assets are loaded, unloaded or reimported by this inventory.";
    }

    [MenuItem("Lit/Performance/Capture Loaded Asset Memory")]
    public static void Capture()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var report = new Report {
            utc = DateTime.UtcNow.ToString("O"), mode = Application.isPlaying ? "Play in Editor" : "Edit",
            allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(), reservedBytes = Profiler.GetTotalReservedMemoryLong(),
            managedBytes = Profiler.GetMonoUsedSizeLong(), currentTextureBytes = Texture.currentTextureMemory,
            profilerUsedBytes = ReadCounter("Profiler Used Memory"),
            profilerReservedBytes = ReadCounter("Profiler Reserved Memory"),
            graphicsUsedBytes = ReadCounter("Gfx Used Memory"),
            nonStreamingTextureBytes = Texture.nonStreamingTextureMemory, streamingEnabled = QualitySettings.streamingMipmapsActive,
            graphicsMemoryMB = SystemInfo.graphicsMemorySize
        };
        var costs = new List<AssetCost>();
        if (Application.isPlaying)
        foreach (var bridge in UnityEngine.Object.FindObjectsByType<LitOpsiveLocomotionBridge>())
        {
            var motor = bridge.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>();
            report.actors.Add(new ActorState { name = bridge.name, driving = bridge.IsDriving, grounded = bridge.Grounded,
                simulationIndex = motor != null ? motor.SimulationIndex : -1, position = bridge.transform.position });
        }
        foreach (var texture in Resources.FindObjectsOfTypeAll<Texture>())
        {
            var cost = Describe(texture); cost.width = texture.width; cost.height = texture.height;
            report.loadedTextureBytes += cost.bytes; report.textureCount++; costs.Add(cost);
        }
        foreach (var mesh in Resources.FindObjectsOfTypeAll<Mesh>())
        {
            var cost = Describe(mesh); report.loadedMeshBytes += cost.bytes; report.meshCount++; costs.Add(cost);
        }
        foreach (var audio in Resources.FindObjectsOfTypeAll<AudioClip>())
        {
            var cost = Describe(audio); report.loadedAudioBytes += cost.bytes; report.audioCount++; costs.Add(cost);
        }
        foreach (var clip in Resources.FindObjectsOfTypeAll<AnimationClip>())
        {
            var cost = Describe(clip); report.loadedAnimationBytes += cost.bytes; report.animationCount++; costs.Add(cost);
        }
        report.largestAssets = costs.OrderByDescending(cost => cost.bytes).Take(100).ToList();
        Directory.CreateDirectory("Library/LitPerformance");
        string path = "Library/LitPerformance/memory-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json";
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log("[LitPerformance] Loaded asset memory inventory exported to " + path);
    }

    private static AssetCost Describe(UnityEngine.Object asset) => new AssetCost {
        name = asset.name, path = AssetDatabase.GetAssetPath(asset), type = asset.GetType().Name,
        bytes = Profiler.GetRuntimeMemorySizeLong(asset)
    };

    private static long ReadCounter(string name)
    {
        // CurrentValue needs no running recorder or per-frame sample storage.
        using (var recorder = new ProfilerRecorder(ProfilerCategory.Memory, name, 0))
            return recorder.Valid ? recorder.CurrentValue : -1;
    }

    [MenuItem("Lit/Performance/Release Unused Editor Assets")]
    public static void ReleaseUnusedEditorAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || UnityEditorInternal.ProfilerDriver.enabled)
        {
            Debug.LogWarning("[LitPerformance] Unused-asset release refused: playing=" + EditorApplication.isPlayingOrWillChangePlaymode +
                " compiling=" + EditorApplication.isCompiling + " updating=" + EditorApplication.isUpdating +
                " recording=" + UnityEditorInternal.ProfilerDriver.enabled);
            return;
        }
        Capture();
        long before = Profiler.GetTotalAllocatedMemoryLong();
        double started = EditorApplication.timeSinceStartup;
        GC.Collect();
        // Preserve assets referenced by managed properties and static caches,
        // as well as the open scenes and unsaved authoring objects.
        EditorUtility.UnloadUnusedAssetsImmediate(includeMonoReferencesAsRoots: true);
        Debug.Log("[LitPerformance] Unused Editor assets released in " +
            (EditorApplication.timeSinceStartup - started).ToString("F3") + " s; allocated bytes " +
            before + " -> " + Profiler.GetTotalAllocatedMemoryLong() + ".");
        EditorApplication.delayCall += Capture;
    }

    [MenuItem("Lit/Performance/Archive And Clear Profiler History")]
    public static void ArchiveProfilerHistory()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEditorInternal.ProfilerDriver.enabled)
        {
            Debug.LogWarning("[LitPerformance] Stop Play and Profiler recording before archiving history.");
            return;
        }
        Directory.CreateDirectory("Library/LitPerformance");
        string path = "Library/LitPerformance/history-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".data";
        UnityEditorInternal.ProfilerDriver.SaveProfile(path);
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new IOException("Profiler history archive was not written; history has not been cleared.");
        UnityEditorInternal.ProfilerDriver.ClearAllFrames();
        Debug.Log("[LitPerformance] Profiler history archived to " + path + " and released from memory.");
        EditorApplication.delayCall += Capture;
    }
}
