using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class LitPlayModeTimingLogger
{
    private const string EnabledPrefKey = "Lit.PlayModeTimingLogger.Enabled";
    private const string MenuPath = "Lit/Performance/Log Play Mode Timing";
    private static double playStartTime;
    private static double editStartTime;

    static LitPlayModeTimingLogger()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem(MenuPath)]
    private static void ToggleEnabled()
    {
        EditorPrefs.SetBool(EnabledPrefKey, !IsEnabled);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggleEnabled()
    {
        Menu.SetChecked(MenuPath, IsEnabled);
        return true;
    }

    private static bool IsEnabled => EditorPrefs.GetBool(EnabledPrefKey, true);

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!IsEnabled)
        {
            return;
        }

        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                playStartTime = EditorApplication.timeSinceStartup;
                break;
            case PlayModeStateChange.EnteredPlayMode:
                Debug.Log($"[PlayModeTiming] Enter Play Mode: {EditorApplication.timeSinceStartup - playStartTime:0.00}s");
                break;
            case PlayModeStateChange.ExitingPlayMode:
                editStartTime = EditorApplication.timeSinceStartup;
                break;
            case PlayModeStateChange.EnteredEditMode:
                Debug.Log($"[PlayModeTiming] Return Edit Mode: {EditorApplication.timeSinceStartup - editStartTime:0.00}s");
                break;
        }
    }
}
