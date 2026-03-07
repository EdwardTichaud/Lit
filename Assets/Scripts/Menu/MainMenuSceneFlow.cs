using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Gere l'entree "Press any button" et l'activation des panels du menu principal.
public class MainMenuSceneFlow : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string menuSceneName = MainMenuController.DefaultMenuSceneName;
    [SerializeField] private string titleCardName = "MainMenu_TitleCard";
    [SerializeField] private string panelName = "MainMenu_Panel";

    [Header("Behaviour")]
    [SerializeField] private bool hideTitleCardOnProceed = true;

    private CanvasGroup titleCardGroup;
    private CanvasGroup panelGroup;
    private bool waitingForInput;
    private bool panelShown;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        EnsureSaveManager(menuSceneName);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (!waitingForInput || panelShown)
        {
            return;
        }

        if (AnyInputPressedThisFrame())
        {
            ShowMainMenuPanel();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsMenuScene(scene.name))
        {
            waitingForInput = false;
            panelShown = false;
            titleCardGroup = null;
            panelGroup = null;
            return;
        }

        BindScene(scene);
        InitializeMenuState();
    }

    private bool IsMenuScene(string sceneName)
    {
        return string.Equals(sceneName, menuSceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void BindScene(Scene scene)
    {
        titleCardGroup = FindCanvasGroup(scene, titleCardName, "TitleCard");
        panelGroup = FindCanvasGroup(scene, panelName, "Panel");

        if (panelGroup != null)
        {
            EnsureLoadManager(panelGroup.transform);
        }
    }

    private void InitializeMenuState()
    {
        panelShown = false;

        if (titleCardGroup != null)
        {
            ShowTitleCard(titleCardGroup);
        }

        if (panelGroup != null)
        {
            if (titleCardGroup == null)
            {
                ShowPanel(panelGroup);
                panelShown = true;
            }
            else
            {
                HidePanel(panelGroup);
            }
        }

        waitingForInput = !panelShown && titleCardGroup != null && panelGroup != null;
    }

    private void ShowMainMenuPanel()
    {
        panelShown = true;
        waitingForInput = false;

        if (panelGroup != null)
        {
            ShowPanel(panelGroup);
        }

        if (hideTitleCardOnProceed && titleCardGroup != null)
        {
            HidePanel(titleCardGroup);
        }
    }

    private void ShowTitleCard(CanvasGroup group)
    {
        if (group == null)
        {
            return;
        }

        group.gameObject.SetActive(true);
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void ShowPanel(CanvasGroup group)
    {
        if (group == null)
        {
            return;
        }

        group.gameObject.SetActive(true);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private void HidePanel(CanvasGroup group)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }

    private void EnsureLoadManager(Transform panelTransform)
    {
        if (panelTransform == null)
        {
            return;
        }

        Transform existing = panelTransform.Find("LoadManager");
        GameObject host = existing != null ? existing.gameObject : null;
        if (host == null)
        {
            host = new GameObject("LoadManager", typeof(RectTransform));
            host.transform.SetParent(panelTransform, false);
        }

        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (host.GetComponent<MainMenuLoadManager>() == null)
        {
            host.AddComponent<MainMenuLoadManager>();
        }

        host.transform.SetAsLastSibling();
    }

    private CanvasGroup FindCanvasGroup(Scene scene, string objectName, string label)
    {
        GameObject target = FindInScene(scene, objectName);
        if (target == null)
        {
            Debug.LogWarning($"MainMenuSceneFlow: {label} '{objectName}' introuvable dans la scene {scene.name}.");
            return null;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
            Debug.LogWarning($"MainMenuSceneFlow: CanvasGroup ajoute sur {objectName}.");
        }

        return group;
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            Transform match = FindInHierarchy(root.transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindInHierarchy(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform match = FindInHierarchy(child, objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void EnsureSaveManager(string sceneName)
    {
        if (SaveSessionManager.Instance != null)
        {
            SaveSessionManager.Instance.SetMenuSceneName(sceneName);
            return;
        }

        GameObject host = new GameObject("SaveSessionManager");
        SaveSessionManager manager = host.AddComponent<SaveSessionManager>();
        manager.SetMenuSceneName(sceneName);
    }

    private static bool AnyInputPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame ||
                Mouse.current.rightButton.wasPressedThisFrame ||
                Mouse.current.middleButton.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad == null)
            {
                continue;
            }

            if (pad.buttonSouth.wasPressedThisFrame ||
                pad.buttonNorth.wasPressedThisFrame ||
                pad.buttonWest.wasPressedThisFrame ||
                pad.buttonEast.wasPressedThisFrame ||
                pad.startButton.wasPressedThisFrame ||
                pad.selectButton.wasPressedThisFrame ||
                pad.leftShoulder.wasPressedThisFrame ||
                pad.rightShoulder.wasPressedThisFrame ||
                pad.leftStickButton.wasPressedThisFrame ||
                pad.rightStickButton.wasPressedThisFrame ||
                pad.dpad.up.wasPressedThisFrame ||
                pad.dpad.down.wasPressedThisFrame ||
                pad.dpad.left.wasPressedThisFrame ||
                pad.dpad.right.wasPressedThisFrame ||
                pad.leftTrigger.wasPressedThisFrame ||
                pad.rightTrigger.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
#else
        return Input.anyKeyDown ||
               Input.GetMouseButtonDown(0) ||
               Input.GetMouseButtonDown(1) ||
               Input.GetMouseButtonDown(2);
#endif
    }
}
