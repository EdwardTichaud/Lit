using UnityEditor;
using UnityEngine;

public static class LitDomainResetTools
{
    private const string MenuPath = "Lit/Settings/Reset Domain";
    private static bool reloadAfterLeavingPlayMode;

    [MenuItem(MenuPath, false, 100)]
    private static void ResetDomain()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorUtility.DisplayDialog(
                "Reset Domain",
                "Unity compile ou importe actuellement des assets. Attendez la fin de l'operation puis recommencez.",
                "OK");
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Reset Domain",
                "Unity doit quitter le Play Mode avant de recharger le domaine C#. Continuer ?",
                "Quitter et recharger",
                "Annuler");
            if (!confirmed)
            {
                return;
            }

            reloadAfterLeavingPlayMode = true;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.isPlaying = false;
            return;
        }

        RequestDomainReload();
    }

    [MenuItem(MenuPath, true)]
    private static bool CanResetDomain()
    {
        return !EditorApplication.isCompiling && !EditorApplication.isUpdating;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!reloadAfterLeavingPlayMode || state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        reloadAfterLeavingPlayMode = false;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.delayCall += RequestDomainReload;
    }

    private static void RequestDomainReload()
    {
        AssetDatabase.SaveAssets();
        Debug.Log(
            "Lit/Settings/Reset Domain: rechargement du domaine C# demande. " +
            "Les etats statiques seront reinitialises; les PlayerPrefs et sauvegardes sont conserves.");
        EditorUtility.RequestScriptReload();
    }
}
