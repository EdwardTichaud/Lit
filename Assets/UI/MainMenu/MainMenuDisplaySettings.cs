using UnityEngine;

// Gere la persistance simple du mode d'affichage du menu principal.
public static class MainMenuDisplaySettings
{
    public enum DisplayMode
    {
        Windowed = 0,
        Fullscreen = 1
    }

    private const string DisplayModePrefKey = "Lit.DisplayMode";
    private static bool appliedSavedMode;

    public static void ApplySavedModeIfNeeded()
    {
        if (appliedSavedMode)
        {
            return;
        }

        ApplyMode(GetSavedMode(), false);
        appliedSavedMode = true;
    }

    public static DisplayMode GetCurrentMode()
    {
        FullScreenMode mode = Screen.fullScreenMode;
        return mode == FullScreenMode.Windowed ? DisplayMode.Windowed : DisplayMode.Fullscreen;
    }

    public static DisplayMode GetSavedMode()
    {
        if (!PlayerPrefs.HasKey(DisplayModePrefKey))
        {
            return DisplayMode.Windowed;
        }

        int savedValue = PlayerPrefs.GetInt(DisplayModePrefKey, (int)DisplayMode.Windowed);
        return savedValue == (int)DisplayMode.Fullscreen ? DisplayMode.Fullscreen : DisplayMode.Windowed;
    }

    public static bool SetMode(DisplayMode mode)
    {
        return ApplyMode(mode, true);
    }

    private static bool ApplyMode(DisplayMode mode, bool savePreference)
    {
        DisplayMode currentMode = GetCurrentMode();
        if (currentMode == mode)
        {
            if (savePreference)
            {
                PlayerPrefs.SetInt(DisplayModePrefKey, (int)mode);
                PlayerPrefs.Save();
            }

            return false;
        }

        if (mode == DisplayMode.Fullscreen)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }

        if (!savePreference)
        {
            return true;
        }

        PlayerPrefs.SetInt(DisplayModePrefKey, (int)mode);
        PlayerPrefs.Save();
        return true;
    }
}
