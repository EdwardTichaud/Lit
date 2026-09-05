using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MainMenuBuildValidator : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (scene.name != MainMenuController.DefaultMenuSceneName) return;
        MainMenuController[] controllers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuController>(true)).ToArray();
        if (controllers.Length != 1) throw new BuildFailedException("MainMenu doit contenir exactement un MainMenuController.");
        foreach (MenuCursorAction action in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MenuCursorAction>(true)))
        {
            int value = new SerializedObject(action).FindProperty("action").intValue;
            if (!System.Enum.IsDefined(typeof(MenuCursorAction.MenuAction), value))
                throw new BuildFailedException($"MainMenu : action inconnue {value} sur {action.name}.");
        }
        SerializedObject data = new SerializedObject(controllers[0]);
        foreach (string reference in new[] { "titleCardGroup", "gameOptionsGroup", "soloOptionsGroup", "multiOptionsGroup", "loadMenuGroup", "statusText", "sessionsRoot", "savesRoot", "sessionEntryPrefab", "saveEntryPrefab", "newGamePanelGroup", "newGameNameInput", "joinPanelGroup", "joinCodeInput", "joinStatusText" })
            if (data.FindProperty(reference).objectReferenceValue == null)
                throw new BuildFailedException("MainMenu : référence obligatoire manquante : " + reference);
        if (data.FindProperty("loadingGroup").objectReferenceValue == data.FindProperty("loadMenuGroup").objectReferenceValue)
            throw new BuildFailedException("La liste des sauvegardes ne peut pas être l’overlay de chargement.");
        PrivateSessionRoster roster = Resources.Load<PrivateSessionRoster>("PrivateSessionRoster");
        if (roster == null || roster.characters == null || roster.characters.Length != 4 ||
            roster.characters.Any(c => c == null || string.IsNullOrWhiteSpace(c.characterId)) ||
            roster.characters.Select(c => c.characterId).Distinct().Count() != 4)
            throw new BuildFailedException("Le salon privé exige quatre personnages identifiés et distincts.");
        if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/Bootstrap.unity")))
            throw new BuildFailedException("Bootstrap doit être activée dans les scènes du build.");
    }
}
