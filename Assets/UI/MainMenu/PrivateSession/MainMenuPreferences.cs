using UnityEngine;

public static class MainMenuPreferences
{
    public static bool ReducedMotion => PlayerPrefs.GetInt("Lit.Menu.ReducedMotion", 0) != 0;
    public static float PointerSensitivity => Mathf.Clamp(PlayerPrefs.GetFloat("Lit.Menu.PointerSensitivity", 1f), .25f, 2f);
    public static float UiScale => Mathf.Clamp(PlayerPrefs.GetFloat("Lit.Menu.UiScale", 1f), .8f, 1.3f);
    public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat("Lit.Menu.MasterVolume", 1f));
    public static void Set(string key, float value) { PlayerPrefs.SetFloat("Lit.Menu." + key, value); PlayerPrefs.Save(); Apply(); }
    public static void Toggle(string key) { string full = "Lit.Menu." + key; PlayerPrefs.SetInt(full, 1 - PlayerPrefs.GetInt(full, 0)); PlayerPrefs.Save(); Apply(); }
    public static void Apply()
    {
        AudioManager audio = AudioManager.EnsureInstance();
        if (audio != null) audio.masterVolume = MasterVolume;
    }
}
