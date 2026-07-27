using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class LitGameViewResolutionGuard
{
    private const int MaxDevelopmentWidth = 1920;
    private const int MaxDevelopmentHeight = 1080;
    private static bool warningLogged;

    static LitGameViewResolutionGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += CheckResolution;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            warningLogged = false;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            warningLogged = false;
        }
    }

    private static void CheckResolution()
    {
        if (!EditorApplication.isPlaying || warningLogged || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        if (Screen.width <= MaxDevelopmentWidth && Screen.height <= MaxDevelopmentHeight)
        {
            return;
        }

        Debug.LogWarning($"GameView de developpement en {Screen.width}x{Screen.height}. Utilisez 1920x1080 ou 1600x900 pour des tests Play Mode fluides.");
        warningLogged = true;
    }
}
