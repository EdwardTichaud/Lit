using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;

// Controle l'ouverture du panel pause et les actions principales (sauvegarde / quit).
[DisallowMultipleComponent]
public class PausePanelController : MonoBehaviour
{
    [Header("Panel")]
    public GameObject pausePanel;
    public bool deactivatePanelOnClose = true;
    public float fadeDuration = 0.4f;
    public bool useUnscaledTime = true;
    public bool startClosed = true;

    [Header("Buttons")]
    public Button saveButton;
    public Button quitButton;
    public UnityEvent onQuit;

    [Header("Quit")]
    public string mainMenuSceneName = MainMenuController.DefaultMenuSceneName;
    public bool shutdownNetworkOnQuit = true;

    [Header("Cursor")]
    public CursorController cursorController;
    public MenuCursorNavigator cursorNavigator;
    public bool enableCursorWhenOpen = true;

    [Header("Input")]
    public bool toggleOnStart = true;
    public bool closeOnReturn = true;

    private CanvasGroup panelCanvasGroup;
    private Coroutine fadeRoutine;
    private bool isOpen;
    private bool hasInitialized;

    private void Awake()
    {
        if (pausePanel == null)
        {
            pausePanel = gameObject;
        }

        if (!string.Equals(mainMenuSceneName, MainMenuController.DefaultMenuSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            mainMenuSceneName = MainMenuController.DefaultMenuSceneName;
        }

        panelCanvasGroup = pausePanel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = pausePanel.AddComponent<CanvasGroup>();
        }

        ResolveButtons();
        ResolveCursor();

        if (startClosed)
        {
            ApplyPanelImmediate(0f, false);
            isOpen = false;
            SetCursorState(false);
        }
        else
        {
            ApplyPanelImmediate(1f, true);
            isOpen = true;
            InputFocusStack.Push(this);
            SetCursorState(true);
        }

        hasInitialized = true;
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Start += OnStartPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
        BindButtons(true);
    }

    private void OnDisable()
    {
        LocalInputRouter.Start -= OnStartPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;
        BindButtons(false);
        InputFocusStack.Pop(this);
    }

    private void OnStartPerformed(InputAction.CallbackContext context)
    {
        if (!toggleOnStart)
        {
            return;
        }

        if (!CanToggle())
        {
            return;
        }

        if (isOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (!closeOnReturn || !isOpen)
        {
            return;
        }

        if (InputFocusStack.HasAnyFocus() && !InputFocusStack.HasFocus(this))
        {
            return;
        }

        ClosePanel();
    }

    private bool CanToggle()
    {
        if (isOpen)
        {
            return !InputFocusStack.HasAnyFocus() || InputFocusStack.HasFocus(this);
        }

        return true;
    }

    public void OpenPanel()
    {
        if (isOpen)
        {
            return;
        }

        isOpen = true;
        InputFocusStack.Push(this);

        pausePanel.SetActive(true);
        StartFade(1f, true);
        SetCursorState(true);

        if (saveButton != null)
        {
            saveButton.Select();
        }
    }

    public void ClosePanel()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        InputFocusStack.Pop(this);

        SetCursorState(false);
        StartFade(0f, false);
    }

    private void StartFade(float targetAlpha, bool show)
    {
        if (!hasInitialized)
        {
            ApplyPanelImmediate(targetAlpha, show);
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, show));
    }

    private void ApplyPanelImmediate(float targetAlpha, bool show)
    {
        if (pausePanel == null || panelCanvasGroup == null)
        {
            return;
        }

        if (show)
        {
            pausePanel.SetActive(true);
        }

        panelCanvasGroup.alpha = targetAlpha;
        bool visible = targetAlpha > 0.001f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;

        if (!visible && CanDeactivatePanel())
        {
            pausePanel.SetActive(false);
        }
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool show)
    {
        if (pausePanel == null || panelCanvasGroup == null)
        {
            yield break;
        }

        if (show)
        {
            pausePanel.SetActive(true);
        }

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        float duration = Mathf.Max(0.01f, fadeDuration);
        float startAlpha = panelCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        panelCanvasGroup.alpha = targetAlpha;
        bool visible = targetAlpha > 0.001f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;

        if (!visible && CanDeactivatePanel())
        {
            pausePanel.SetActive(false);
        }
    }

    private void BindButtons(bool enabled)
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(HandleSaveClicked);
            if (enabled)
            {
                saveButton.onClick.AddListener(HandleSaveClicked);
            }
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(HandleQuitClicked);
            if (enabled)
            {
                quitButton.onClick.AddListener(HandleQuitClicked);
            }
        }
    }

    private void HandleSaveClicked()
    {
        CharacterStateStore store = CharacterStateStore.Instance;
        if (store == null)
        {
            store = FindFirstObjectByType<CharacterStateStore>();
        }
        if (store == null)
        {
            store = FindObjectOfType<CharacterStateStore>();
        }

        if (store != null)
        {
            store.Save();
        }
        else
        {
            Debug.LogWarning("PausePanelController: CharacterStateStore introuvable, sauvegarde impossible.");
        }
    }

    public void UI_Save()
    {
        HandleSaveClicked();
    }

    private void HandleQuitClicked()
    {
        if (onQuit != null)
        {
            onQuit.Invoke();
        }

        ReturnToMainMenu();
    }

    public void UI_Quit()
    {
        HandleQuitClicked();
    }

    private void ReturnToMainMenu()
    {
        if (shutdownNetworkOnQuit && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning("PausePanelController: mainMenuSceneName vide, retour menu annule.");
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void ResolveButtons()
    {
        if (pausePanel == null)
        {
            return;
        }

        if (saveButton == null)
        {
            Transform found = FindInHierarchy(pausePanel.transform, "SaveButton");
            if (found != null)
            {
                saveButton = found.GetComponent<Button>();
            }
        }

        if (quitButton == null)
        {
            Transform found = FindInHierarchy(pausePanel.transform, "QuitButton");
            if (found != null)
            {
                quitButton = found.GetComponent<Button>();
            }
        }
    }

    private void ResolveCursor()
    {
        if (pausePanel == null)
        {
            return;
        }

        if (cursorController == null)
        {
            cursorController = pausePanel.GetComponentInChildren<CursorController>(true);
        }

        if (cursorNavigator == null)
        {
            cursorNavigator = pausePanel.GetComponentInChildren<MenuCursorNavigator>(true);
        }
    }

    private void SetCursorState(bool enabled)
    {
        if (!enableCursorWhenOpen)
        {
            return;
        }

        if (cursorController != null)
        {
            cursorController.enabled = enabled;
        }

        if (cursorNavigator != null)
        {
            cursorNavigator.enabled = enabled;
        }
    }

    private bool CanDeactivatePanel()
    {
        return deactivatePanelOnClose && pausePanel != gameObject;
    }

    private static Transform FindInHierarchy(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindInHierarchy(root.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
