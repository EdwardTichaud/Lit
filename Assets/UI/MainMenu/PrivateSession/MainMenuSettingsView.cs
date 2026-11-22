using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuSettingsView : MonoBehaviour
{
    public enum Setting { Display, Input, ReducedMotion, MasterVolume, Music, Sfx, UiScale, Back }

    [Serializable]
    private sealed class Binding
    {
        public Setting setting;
        public Button button;
        public TMP_Text label;
    }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform navigationRoot;
    [SerializeField] private Binding[] bindings;

    private MainMenuController owner;
    private UnityAction[] listeners;
    public CanvasGroup Group => group;
    public RectTransform NavigationRoot => navigationRoot;

    public bool IsConfigured
    {
        get
        {
            if (group == null || navigationRoot == null || bindings == null || bindings.Length != 8) return false;
            var settings = new HashSet<Setting>();
            foreach (var binding in bindings)
                if (binding == null || binding.button == null || binding.label == null ||
                    !Enum.IsDefined(typeof(Setting), binding.setting) || !settings.Add(binding.setting)) return false;
            return true;
        }
    }

    public bool Bind(MainMenuController controller)
    {
        Unbind();
        if (controller == null || !IsConfigured)
        {
            Debug.LogError("[MainMenu] MenuSettingsCanvas: references de reglages incompletes.", this);
            return false;
        }
        owner = controller;
        listeners = new UnityAction[bindings.Length];
        for (int i = 0; i < bindings.Length; i++)
        {
            Setting setting = bindings[i].setting;
            listeners[i] = () => Apply(setting);
            bindings[i].button.onClick.AddListener(listeners[i]);
        }
        RefreshLabels();
        return true;
    }

    public void RefreshLabels()
    {
        if (bindings == null) return;
        foreach (var binding in bindings)
            if (binding != null && binding.label != null) binding.label.text = Label(binding.setting);
    }

    private static string Label(Setting setting)
    {
        switch (setting)
        {
            case Setting.Display: return "Affichage : " + (MainMenuDisplaySettings.GetCurrentMode() == MainMenuDisplaySettings.DisplayMode.Fullscreen ? "Plein écran" : "Fenêtre");
            case Setting.Input: return "Contrôles : " + (MainMenuInputSettings.GetCurrentMode() == MainMenuInputSettings.InputMode.Automatic ? "Automatiques" : MainMenuInputSettings.GetCurrentMode() == MainMenuInputSettings.InputMode.Gamepad ? "Manette" : "Clavier / souris");
            case Setting.ReducedMotion: return "Animations réduites : " + (MainMenuPreferences.ReducedMotion ? "Oui" : "Non");
            case Setting.MasterVolume: return $"Volume principal : {MainMenuPreferences.MasterVolume:P0}";
            case Setting.Music: return $"Musique : {AudioManager.EnsureInstance().MusicVolume:P0}";
            case Setting.Sfx: return $"Effets : {AudioManager.EnsureInstance().SfxVolume:P0}";
            case Setting.UiScale: return $"Taille de l’interface : {MainMenuPreferences.UiScale:P0}";
            default: return "Retour";
        }
    }

    private void Apply(Setting setting)
    {
        if (owner == null) return;
        switch (setting)
        {
            case Setting.Display:
                MainMenuDisplaySettings.SetMode(MainMenuDisplaySettings.GetCurrentMode() == MainMenuDisplaySettings.DisplayMode.Fullscreen ? MainMenuDisplaySettings.DisplayMode.Windowed : MainMenuDisplaySettings.DisplayMode.Fullscreen);
                break;
            case Setting.Input:
                MainMenuInputSettings.SetMode(MainMenuInputSettings.GetCurrentMode() == MainMenuInputSettings.InputMode.Automatic ? MainMenuInputSettings.InputMode.KeyboardMouse : MainMenuInputSettings.GetCurrentMode() == MainMenuInputSettings.InputMode.KeyboardMouse ? MainMenuInputSettings.InputMode.Gamepad : MainMenuInputSettings.InputMode.Automatic);
                break;
            case Setting.ReducedMotion: MainMenuPreferences.Toggle("ReducedMotion"); break;
            case Setting.MasterVolume: MainMenuPreferences.Set("MasterVolume", Next(MainMenuPreferences.MasterVolume, 0, 1, .1f)); break;
            case Setting.Music:
                var music = AudioManager.EnsureInstance();
                music.SetMusicVolume(Next(music.MusicVolume, 0, 1, .1f));
                break;
            case Setting.Sfx:
                var sfx = AudioManager.EnsureInstance();
                sfx.SetSfxVolume(Next(sfx.SfxVolume, 0, 1, .1f));
                break;
            case Setting.UiScale: MainMenuPreferences.Set("UiScale", Next(MainMenuPreferences.UiScale, .8f, 1.3f, .1f)); break;
            case Setting.Back: owner.UI_ShowGameOptions(); break;
        }
        RefreshLabels();
    }

    private static float Next(float value, float min, float max, float step) => value + step > max + .01f ? min : Mathf.Min(max, value + step);

    private void Unbind()
    {
        if (listeners != null && bindings != null)
            for (int i = 0; i < listeners.Length && i < bindings.Length; i++)
                if (bindings[i]?.button != null) bindings[i].button.onClick.RemoveListener(listeners[i]);
        listeners = null;
        owner = null;
    }

    private void OnDestroy() => Unbind();
}
