using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuSettingsView
{
    public static CanvasGroup Create(MainMenuController owner)
    {
        GameObject canvasObject = new GameObject("MenuSettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(owner.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        GameObject root = MenuViewFactory.Column(canvasObject.transform, "MainMenuSettings");
        root.AddComponent<Image>().color = new Color(.035f, .045f, .055f, .98f);
        root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(32, 32, 24, 24);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(920, 990);
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        MenuViewFactory.Label(root.transform, "Réglages", 40);
        Add(root.transform, () => "Affichage : " + (MainMenuDisplaySettings.GetCurrentMode() == MainMenuDisplaySettings.DisplayMode.Fullscreen ? "Plein écran" : "Fenêtre"), () =>
            MainMenuDisplaySettings.SetMode(MainMenuDisplaySettings.GetCurrentMode() == MainMenuDisplaySettings.DisplayMode.Fullscreen ? MainMenuDisplaySettings.DisplayMode.Windowed : MainMenuDisplaySettings.DisplayMode.Fullscreen));
        Add(root.transform, () => "Contrôles : " + (MainMenuInputSettings.GetCurrentMode() == MainMenuInputSettings.InputMode.Automatic ? "Automatiques" : MainMenuInputSettings.GetCurrentMode() == MainMenuInputSettings.InputMode.Gamepad ? "Manette" : "Clavier / souris"), () =>
            MainMenuInputSettings.SetMode(MainMenuInputSettings.GetCurrentMode() == MainMenuInputSettings.InputMode.Automatic ? MainMenuInputSettings.InputMode.KeyboardMouse : MainMenuInputSettings.GetCurrentMode() == MainMenuInputSettings.InputMode.KeyboardMouse ? MainMenuInputSettings.InputMode.Gamepad : MainMenuInputSettings.InputMode.Automatic));
        MenuViewFactory.Label(root.transform, "Manette : croix / stick pour sélectionner, A pour valider, B pour revenir.", 64);
        Add(root.transform, () => "Animations réduites : " + (MainMenuPreferences.ReducedMotion ? "Oui" : "Non"), () => MainMenuPreferences.Toggle("ReducedMotion"));
        Add(root.transform, () => $"Volume principal : {MainMenuPreferences.MasterVolume:P0}", () => MainMenuPreferences.Set("MasterVolume", Next(MainMenuPreferences.MasterVolume, 0, 1, .1f)));
        Add(root.transform, () => $"Musique : {AudioManager.EnsureInstance().MusicVolume:P0}", () => { AudioManager audio = AudioManager.EnsureInstance(); audio.SetMusicVolume(Next(audio.MusicVolume, 0, 1, .1f)); });
        Add(root.transform, () => $"Effets : {AudioManager.EnsureInstance().SfxVolume:P0}", () => { AudioManager audio = AudioManager.EnsureInstance(); audio.SetSfxVolume(Next(audio.SfxVolume, 0, 1, .1f)); });
        Add(root.transform, () => $"Taille de l’interface : {MainMenuPreferences.UiScale:P0}", () => MainMenuPreferences.Set("UiScale", Next(MainMenuPreferences.UiScale, .8f, 1.3f, .1f)));
        MenuViewFactory.Label(root.transform, "Valider : réglage suivant · Y : clavier virtuel\nRetour / Échap : fermer", 70);
        MenuViewFactory.Button(root.transform, "Retour", owner.UI_ShowGameOptions);
        MenuViewFactory.MakeScrollable(root);
        root.SetActive(false);
        return group;
    }
    private static float Next(float value, float min, float max, float step) => value + step > max + .01f ? min : Mathf.Min(max, value + step);
    private static void Add(Transform parent, System.Func<string> label, System.Action change)
    {
        Button button = null;
        button = MenuViewFactory.Button(parent, label(), () => { change(); button.GetComponentInChildren<TMP_Text>().text = label(); });
    }
}
