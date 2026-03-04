using UnityEngine;

// Bootstrap runtime pour garantir la creation du menu principal.
public class MainMenuBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRuntime()
    {
#if UNITY_2023_1_OR_NEWER
        MainMenuController existing = Object.FindFirstObjectByType<MainMenuController>();
#else
        MainMenuController existing = Object.FindObjectOfType<MainMenuController>();
#endif
        if (existing != null)
        {
            return;
        }

        GameObject host = new GameObject("MainMenuController");
        host.AddComponent<MainMenuController>();
    }
}
