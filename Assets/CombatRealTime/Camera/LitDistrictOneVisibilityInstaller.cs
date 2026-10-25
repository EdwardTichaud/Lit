using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local presentation, including additive and standalone District_1 scenes.</summary>
public static class LitDistrictOneVisibilityInstaller
{
    private static LitMaisonVisibilityZone zone;

    public static bool IsDistrictScene(string path, string name)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(name)) return false;
        if (path.StartsWith("Assets/Scenes/District_1/", StringComparison.Ordinal)) return true;
        bool districtName = name == "District_1" || name.StartsWith("District_1_", StringComparison.Ordinal);
        return districtName && (path.StartsWith("Assets/Scenes/Cycles/", StringComparison.Ordinal) ||
            (path.StartsWith("Assets/", StringComparison.Ordinal) && path.LastIndexOf('/') == 6));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < SceneManager.sceneCount; i++) Ensure(SceneManager.GetSceneAt(i));
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure(scene);
    private static void Ensure(Scene scene)
    {
        if (!scene.isLoaded || !IsDistrictScene(scene.path, scene.name) || zone != null) return;
        // Reuse a persistent zone after an in-Play domain reload rather than
        // installing a second owner when the next additive scene arrives.
        foreach (var existing in UnityEngine.Object.FindObjectsByType<LitMaisonVisibilityZone>())
            if (existing.IsDistrictOne) { zone = existing; return; }
        var root = new GameObject("District_1_TacticalVisibility");
        root.SetActive(false);
        zone = root.AddComponent<LitMaisonVisibilityZone>();
        zone.ConfigureForDistrictOne();
        UnityEngine.Object.DontDestroyOnLoad(root);
        root.SetActive(true);
    }
}
