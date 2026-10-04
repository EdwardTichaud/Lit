using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class LitPlayModeTimingLogger
{
    private const string EnabledPrefKey = "Lit.PlayModeTimingLogger.Enabled";
    private const string MenuPath = "Lit/Performance/Log Play Mode Timing";
    // Static fields are reset when domain reload is enabled. SessionState is
    // kept by the editor across that reload, so the measurement stays valid.
    private const string PlayStartKey = "Lit.PlayModeTimingLogger.PlayStart";
    private const string EditStartKey = "Lit.PlayModeTimingLogger.EditStart";

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
                SessionState.SetFloat(PlayStartKey, (float)EditorApplication.timeSinceStartup);
                break;
            case PlayModeStateChange.EnteredPlayMode:
                LogDuration("Enter Play Mode", PlayStartKey);
                break;
            case PlayModeStateChange.ExitingPlayMode:
                SessionState.SetFloat(EditStartKey, (float)EditorApplication.timeSinceStartup);
                break;
            case PlayModeStateChange.EnteredEditMode:
                LogDuration("Return Edit Mode", EditStartKey);
                break;
        }
    }

    private static void LogDuration(string label, string key)
    {
        float start = SessionState.GetFloat(key, -1f);
        double now = EditorApplication.timeSinceStartup;
        if (start >= 0f && now >= start)
        {
            Debug.Log($"[PlayModeTiming] {label}: {now - start:0.00}s");
        }
        else
        {
            Debug.Log($"[PlayModeTiming] {label}: mesure indisponible.");
        }

        SessionState.SetFloat(key, -1f);
    }
}
