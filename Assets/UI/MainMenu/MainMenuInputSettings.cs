using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Gere le mode d'entree choisi dans les options du menu principal.
public static class MainMenuInputSettings
{
    public enum InputMode
    {
        Automatic = -1,
        KeyboardMouse = 0,
        Gamepad = 1
    }

    private const string InputModePrefKey = "Lit.InputMode.v2";
    private const string LegacyInputModePrefKey = "Lit.InputMode";
    private static bool initialized;
    private static InputMode currentMode;

    public static event Action<InputMode> ModeChanged;

    public static void ApplySavedModeIfNeeded()
    {
        EnsureInitialized();
    }

    public static InputMode GetCurrentMode()
    {
        EnsureInitialized();
        return currentMode;
    }

    public static InputMode GetSavedMode()
    {
        int value = PlayerPrefs.GetInt(InputModePrefKey, (int)InputMode.Automatic);
        return Enum.IsDefined(typeof(InputMode), value) ? (InputMode)value : InputMode.Automatic;
    }

    public static bool SetMode(InputMode mode)
    {
        EnsureInitialized();
        if (!Enum.IsDefined(typeof(InputMode), mode) || currentMode == mode)
        {
            return false;
        }

        currentMode = mode;
        SaveCurrentMode();
        ModeChanged?.Invoke(currentMode);
        return true;
    }

    public static bool IsActionAllowed(InputAction.CallbackContext context)
    {
        return context.control != null && IsDeviceAllowed(context.control.device);
    }

    public static bool IsDeviceAllowed(InputDevice device)
    {
        InputMode mode = GetCurrentMode();
        if (mode == InputMode.Automatic)
        {
            return true;
        }

        if (device is Gamepad)
        {
            return mode == InputMode.Gamepad;
        }

        if (device is Keyboard || device is Mouse)
        {
            return mode == InputMode.KeyboardMouse;
        }

        return false;
    }

    public static bool AllowsKeyboardMouse()
    {
        return GetCurrentMode() != InputMode.Gamepad;
    }

    public static bool AllowsGamepad()
    {
        return GetCurrentMode() != InputMode.KeyboardMouse;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        initialized = false;
        ModeChanged = null;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        currentMode = GetSavedMode();
        initialized = true;
    }

    private static void SaveCurrentMode()
    {
        if (currentMode == InputMode.Automatic)
        {
            PlayerPrefs.DeleteKey(InputModePrefKey);
        }
        else
        {
            PlayerPrefs.SetInt(InputModePrefKey, (int)currentMode);
        }

        PlayerPrefs.Save();
    }
}
