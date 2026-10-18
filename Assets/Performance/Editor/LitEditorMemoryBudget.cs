using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Bound import concurrency on memory-constrained development PCs; never changes Player settings.</summary>
[InitializeOnLoad]
internal static class LitEditorMemoryBudget
{
    private const string InteractionSnapshot = "Library/LitPerformance/interaction-mode-original.json";
    [Serializable] private sealed class InteractionSettings
    {
        public bool hadMode, hadIdle;
        public int mode, idle;
    }

    [MenuItem("Lit/Performance/Use Monitor Refresh Rate While Editing")]
    public static void UseMonitorRefreshRate()
    {
        if (Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess()) return;
        var refresh = InteractionRefreshMethod();
        var previous = new InteractionSettings {
            hadMode = EditorPrefs.HasKey("InteractionMode"), hadIdle = EditorPrefs.HasKey("ApplicationIdleTime"),
            mode = EditorPrefs.GetInt("InteractionMode", 0), idle = EditorPrefs.GetInt("ApplicationIdleTime", 4)
        };
        Directory.CreateDirectory("Library/LitPerformance");
        if (!File.Exists(InteractionSnapshot)) File.WriteAllText(InteractionSnapshot, JsonUtility.ToJson(previous, true));
        double hz = Screen.currentResolution.refreshRateRatio.value;
        int idle = hz > 0 ? Mathf.Clamp((int)(1000.0 / hz), 1, 1000) : 16;
        EditorPrefs.SetInt("ApplicationIdleTime", idle);
        EditorPrefs.SetInt("InteractionMode", 2); // Native Preferences: Monitor Refresh Rate.
        refresh.Invoke(null, null);
        Debug.Log("[LitPerformance] Editor interaction mode " + previous.mode + " (" + previous.idle +
            " ms) -> Monitor Refresh Rate (" + idle + " ms). Original preference saved in " + InteractionSnapshot);
    }

    [MenuItem("Lit/Performance/Restore Interaction Mode From Performance Snapshot")]
    private static void RestoreInteractionMode()
    {
        if (!File.Exists(InteractionSnapshot)) return;
        var refresh = InteractionRefreshMethod();
        var previous = JsonUtility.FromJson<InteractionSettings>(File.ReadAllText(InteractionSnapshot));
        if (previous.hadMode) EditorPrefs.SetInt("InteractionMode", previous.mode); else EditorPrefs.DeleteKey("InteractionMode");
        if (previous.hadIdle) EditorPrefs.SetInt("ApplicationIdleTime", previous.idle); else EditorPrefs.DeleteKey("ApplicationIdleTime");
        refresh.Invoke(null, null);
    }

    private static MethodInfo InteractionRefreshMethod() => typeof(EditorApplication).GetMethod(
        "UpdateInteractionModeSettings", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new NotSupportedException("This Unity version does not expose the interaction-mode refresh API.");

    private const string MenuPath = "Lit/Performance/Limit Import Workers On 16 GB Machines";
    private static string PreferenceKey => "Lit.Performance.ImportMemoryBudget." + Hash128.Compute(Application.dataPath);
    private static string PreviousWorkersKey => PreferenceKey + ".PreviousWorkers";
    private static string PreviousConfiguredWorkersKey => PreferenceKey + ".PreviousConfiguredWorkers";
    private static string PreviousStandbyKey => PreferenceKey + ".PreviousStandby";
    private static string PreviousIdleDelayKey => PreferenceKey + ".PreviousIdleDelay";

    static LitEditorMemoryBudget()
    {
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        if (Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess() || !EditorPrefs.GetBool(PreferenceKey, true) || SystemInfo.systemMemorySize <= 0 ||
            SystemInfo.systemMemorySize > 16384) return;
        bool changed = false;
        if (EditorUserSettings.desiredImportWorkerCount != 1)
        {
            Remember(PreviousConfiguredWorkersKey, EditorUserSettings.desiredImportWorkerCount);
            EditorUserSettings.desiredImportWorkerCount = 1;
            changed = true;
        }
        if (AssetDatabase.DesiredWorkerCount > 1)
        {
            Remember(PreviousWorkersKey, AssetDatabase.DesiredWorkerCount);
            AssetDatabase.DesiredWorkerCount = 1;
            changed = true;
        }
        if (EditorUserSettings.standbyImportWorkerCount != 1)
        {
            Remember(PreviousStandbyKey, EditorUserSettings.standbyImportWorkerCount);
            // Unity 6.6 enforces a minimum of one in its native pool config.
            EditorUserSettings.standbyImportWorkerCount = 1;
            changed = true;
        }
        if (EditorUserSettings.idleImportWorkerShutdownDelayMilliseconds > 30000)
        {
            Remember(PreviousIdleDelayKey, EditorUserSettings.idleImportWorkerShutdownDelayMilliseconds);
            EditorUserSettings.idleImportWorkerShutdownDelayMilliseconds = 30000;
            changed = true;
        }
        // Let Unity rebalance its pool at the next refresh. Forcing the pool here
        // would create an unnecessary worker if none is running while idle.
        if (changed)
        {
            AssetDatabase.RefreshSettings();
            Debug.Log("[LitPerformance] Import and standby worker counts limited to one on this 16 GB development machine. " +
                "Unity retains one standby worker. Restore previous settings through Lit/Performance/Limit Import Workers On 16 GB Machines.");
        }
    }

    private static void Remember(string key, int value)
    {
        if (!EditorPrefs.HasKey(key)) EditorPrefs.SetInt(key, value);
    }

    internal static void TrimIdleWorkersForCapture()
    {
        Apply();
        // Do not force the pool here: that would spawn a worker when no import
        // is pending. Unity releases idle workers through the standby policy.
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        bool enabled = !EditorPrefs.GetBool(PreferenceKey, true);
        EditorPrefs.SetBool(PreferenceKey, enabled);
        if (enabled) Apply();
        else
        {
            if (EditorPrefs.HasKey(PreviousConfiguredWorkersKey))
            {
                EditorUserSettings.desiredImportWorkerCount = Mathf.Max(1, EditorPrefs.GetInt(PreviousConfiguredWorkersKey));
                EditorPrefs.DeleteKey(PreviousConfiguredWorkersKey);
            }
            if (EditorPrefs.HasKey(PreviousWorkersKey))
            {
                AssetDatabase.DesiredWorkerCount = Mathf.Max(1, EditorPrefs.GetInt(PreviousWorkersKey));
                EditorPrefs.DeleteKey(PreviousWorkersKey);
            }
            if (EditorPrefs.HasKey(PreviousStandbyKey))
            {
                EditorUserSettings.standbyImportWorkerCount = EditorPrefs.GetInt(PreviousStandbyKey);
                EditorPrefs.DeleteKey(PreviousStandbyKey);
            }
            if (EditorPrefs.HasKey(PreviousIdleDelayKey))
            {
                EditorUserSettings.idleImportWorkerShutdownDelayMilliseconds = EditorPrefs.GetInt(PreviousIdleDelayKey);
                EditorPrefs.DeleteKey(PreviousIdleDelayKey);
            }
            AssetDatabase.RefreshSettings();
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PreferenceKey, true));
        return true;
    }
}
