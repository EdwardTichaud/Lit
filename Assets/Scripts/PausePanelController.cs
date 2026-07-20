using System.Collections;
using TMPro;
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

    [Header("Save")]
    public string manualSaveNamePrefix = "Sauvegarde";
    public bool includeTimestampInSaveName = true;

    [Header("Quit")]
    public string mainMenuSceneName = MainMenuController.DefaultMenuSceneName;
    public bool shutdownNetworkOnQuit = true;

    [Header("Cursor")]
    public CursorController cursorController;
    public MenuCursorNavigator cursorNavigator;
    public bool enableCursorWhenOpen = true;
    public bool forceCursorFixedSize = true;

    [Header("Input")]
    public bool toggleOnStart = true;
    public bool closeOnReturn = true;
    public bool lockGameplayInput = true;

    [Header("Audio Options")]
    public bool createAudioOptions = true;
    [Range(0.01f, 0.5f)] public float audioOptionStep = 0.1f;
    public int audioOptionsInsertIndex = 1;
    public string audioOptionsButtonLabel = "Options audio";
    public string audioOptionsBackLabel = "Retour";
    public string musicOptionLabel = "Musique";
    public string sfxOptionLabel = "Sons";

    private CanvasGroup panelCanvasGroup;
    private Coroutine fadeRoutine;
    private bool isOpen;
    private bool hasInitialized;
    private bool gameplayInputLocked;
    private bool cachedMatchTargetSize;
    private bool cursorSizeCached;
    private RectTransform pauseOptionsRoot;
    private PauseCursorAction audioOptionsButton;
    private PauseCursorAction audioOptionsBackButton;
    private PauseAudioOption musicVolumeOption;
    private PauseAudioOption sfxVolumeOption;
    private readonly System.Collections.Generic.List<RectTransform> defaultPauseOptions = new System.Collections.Generic.List<RectTransform>();
    private bool defaultPauseOptionsCaptured;
    private bool audioOptionsOpen;

    public bool IsOpen => isOpen;

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
        EnsureRuntimeAudioOptions();
        ResolveCursor();
        SetAudioOptionsOpen(false, false);
        RefreshRuntimeAudioOptions();

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
            LockGameplayInput(true);
            ApplyCursorSizing(true);
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
        LockGameplayInput(false);
        ApplyCursorSizing(false);
        SetAudioOptionsOpen(false, false);
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

        if (!HasPauseInputFocus())
        {
            return;
        }

        if (audioOptionsOpen)
        {
            CloseAudioOptions();
            return;
        }

        ClosePanel();
    }

    private bool CanToggle()
    {
        if (isOpen)
        {
            return HasPauseInputFocus();
        }

        return true;
    }

    private bool HasPauseInputFocus()
    {
        if (!InputFocusStack.HasAnyFocus() || InputFocusStack.HasFocus(this))
        {
            return true;
        }

        return cursorNavigator != null && cursorNavigator.HasInputFocus();
    }

    public void OpenPanel()
    {
        if (isOpen)
        {
            return;
        }

        isOpen = true;
        InputFocusStack.Push(this);
        LockGameplayInput(true);
        ApplyCursorSizing(true);

        pausePanel.SetActive(true);
        EnsureRuntimeAudioOptions();
        SetAudioOptionsOpen(false, false);
        RefreshRuntimeAudioOptions();
        if (cursorController != null)
        {
            cursorController.Refresh();
        }

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
        LockGameplayInput(false);
        ApplyCursorSizing(false);
        SetAudioOptionsOpen(false, false);

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
        if (!EnsureSaveSlot())
        {
            InfoBoxUI.TryShowTopLeft("Sauvegarde impossible.");
            return;
        }

        CharacterStateStore store = ResolveCharacterStateStore();

        if (store != null)
        {
            store.Save();
            InfoBoxUI.TryShowTopLeft("Sauvegarde créée.");
        }
        else
        {
            Debug.LogWarning("PausePanelController: CharacterStateStore introuvable, sauvegarde impossible.");
            InfoBoxUI.TryShowTopLeft("Sauvegarde impossible.");
        }
    }

    private CharacterStateStore ResolveCharacterStateStore()
    {
        if (CharacterStateStore.Instance != null)
        {
            return CharacterStateStore.Instance;
        }

        CharacterStateStore store = FindAnyObjectByType<CharacterStateStore>();
        if (store != null)
        {
            return store;
        }

        store = FindAnyObjectByType<CharacterStateStore>();
        if (store != null)
        {
            return store;
        }

        CharacterStateStore[] candidates = Resources.FindObjectsOfTypeAll<CharacterStateStore>();
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            CharacterStateStore candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            Scene scene = candidate.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            return candidate;
        }

        return null;
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
            mainMenuSceneName = MainMenuController.DefaultMenuSceneName;
        }

        if (!GameFlowService.OpenMainMenu())
        {
            LoadingScreenService.LoadScene(mainMenuSceneName, "Retour au menu principal...", LoadSceneMode.Single);
        }
    }

    private bool EnsureSaveSlot()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("PausePanelController: sauvegarde refusee (client uniquement).");
            return false;
        }

        SaveSessionManager session = EnsureSaveSessionManager();
        if (session == null)
        {
            return true;
        }

        if (!session.HasActiveSave)
        {
            SaveSessionType sessionType = ResolveSessionType();
            string sessionName = string.IsNullOrWhiteSpace(session.CurrentSessionName) ? "Nouvelle partie" : session.CurrentSessionName;
            SaveSessionInfo info = session.CreateSession(sessionName, sessionType);
            if (info == null)
            {
                Debug.LogWarning("PausePanelController: creation de session impossible.");
                return false;
            }

            SaveSlotInfo save = session.CreateSave(info.sessionId, BuildManualSaveName());
            if (save == null)
            {
                Debug.LogWarning("PausePanelController: creation de sauvegarde impossible.");
                return false;
            }

            session.SetActiveSave(info.sessionId, save.saveId);
            return true;
        }

        SaveSlotInfo newSave = session.CreateSave(session.CurrentSessionId, BuildManualSaveName());
        if (newSave != null)
        {
            session.SetActiveSave(session.CurrentSessionId, newSave.saveId);
        }

        return true;
    }

    private SaveSessionManager EnsureSaveSessionManager()
    {
        if (SaveSessionManager.Instance != null)
        {
            return SaveSessionManager.Instance;
        }

        GameObject host = new GameObject("SaveSessionManager");
        SaveSessionManager manager = host.AddComponent<SaveSessionManager>();
        manager.SetMenuSceneName(MainMenuController.DefaultMenuSceneName);
        return manager;
    }

    private SaveSessionType ResolveSessionType()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return SaveSessionType.Multiplayer;
        }

        return SaveSessionType.Solo;
    }

    private string BuildManualSaveName()
    {
        string prefix = string.IsNullOrWhiteSpace(manualSaveNamePrefix) ? "Sauvegarde" : manualSaveNamePrefix.Trim();
        if (!includeTimestampInSaveName)
        {
            return prefix;
        }

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        return $"{prefix} {timestamp}";
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

    private void EnsureRuntimeAudioOptions()
    {
        if (!createAudioOptions || pausePanel == null)
        {
            return;
        }

        pauseOptionsRoot = FindInHierarchy(pausePanel.transform, "PauseOptions") as RectTransform;
        if (pauseOptionsRoot == null)
        {
            return;
        }

        CaptureDefaultPauseOptions(pauseOptionsRoot);

        RectTransform template = ResolveAudioOptionTemplate(pauseOptionsRoot);
        if (template == null)
        {
            return;
        }

        audioOptionsButton = EnsureActionOption(
            pauseOptionsRoot,
            template,
            "AudioOptionsButton",
            "AudioOptionsButton_Text",
            PauseCursorAction.PauseAction.AudioOptions,
            audioOptionsButtonLabel);
        musicVolumeOption = EnsureAudioOption(
            pauseOptionsRoot,
            template,
            "MusicVolumeOption",
            "MusicVolumeOption_Text",
            PauseAudioOption.VolumeChannel.Music,
            musicOptionLabel);
        sfxVolumeOption = EnsureAudioOption(
            pauseOptionsRoot,
            template,
            "SfxVolumeOption",
            "SfxVolumeOption_Text",
            PauseAudioOption.VolumeChannel.Sfx,
            sfxOptionLabel);
        audioOptionsBackButton = EnsureActionOption(
            pauseOptionsRoot,
            template,
            "AudioOptionsBackButton",
            "AudioOptionsBackButton_Text",
            PauseCursorAction.PauseAction.AudioOptionsBack,
            audioOptionsBackLabel);

        int insertIndex = Mathf.Max(0, audioOptionsInsertIndex);
        SetOptionSibling(audioOptionsButton, pauseOptionsRoot, insertIndex);
        SetOptionSibling(musicVolumeOption, pauseOptionsRoot, insertIndex);
        SetOptionSibling(sfxVolumeOption, pauseOptionsRoot, insertIndex + 1);
        SetOptionSibling(audioOptionsBackButton, pauseOptionsRoot, insertIndex + 2);
        ApplyAudioOptionsVisibility();
    }

    private void RefreshRuntimeAudioOptions()
    {
        SetActionOptionLabel(audioOptionsButton, audioOptionsButtonLabel);
        SetActionOptionLabel(audioOptionsBackButton, audioOptionsBackLabel);

        if (musicVolumeOption != null)
        {
            musicVolumeOption.RefreshLabel();
        }

        if (sfxVolumeOption != null)
        {
            sfxVolumeOption.RefreshLabel();
        }

        ApplyAudioOptionsVisibility();
    }

    public void UI_OpenAudioOptions()
    {
        OpenAudioOptions();
    }

    public void UI_CloseAudioOptions()
    {
        CloseAudioOptions();
    }

    public void OpenAudioOptions()
    {
        if (audioOptionsOpen)
        {
            return;
        }

        EnsureRuntimeAudioOptions();
        if (musicVolumeOption == null && sfxVolumeOption == null)
        {
            return;
        }

        SetAudioOptionsOpen(true, true);
    }

    public void CloseAudioOptions()
    {
        if (!audioOptionsOpen)
        {
            return;
        }

        SetAudioOptionsOpen(false, true);
    }

    private void SetAudioOptionsOpen(bool open, bool refreshCursorSelection)
    {
        audioOptionsOpen = open && createAudioOptions;
        ApplyAudioOptionsVisibility();

        if (!refreshCursorSelection || !isOpen || cursorController == null)
        {
            return;
        }

        RectTransform preferred = null;
        if (audioOptionsOpen)
        {
            preferred = musicVolumeOption != null
                ? musicVolumeOption.transform as RectTransform
                : sfxVolumeOption != null
                    ? sfxVolumeOption.transform as RectTransform
                    : audioOptionsBackButton != null
                        ? audioOptionsBackButton.transform as RectTransform
                        : null;
        }
        else if (audioOptionsButton != null)
        {
            preferred = audioOptionsButton.transform as RectTransform;
        }

        FocusPauseOption(preferred);
    }

    private PauseAudioOption EnsureAudioOption(
        RectTransform optionsRoot,
        RectTransform template,
        string optionObjectName,
        string textObjectName,
        PauseAudioOption.VolumeChannel channel,
        string optionLabel)
    {
        RectTransform optionRoot = optionsRoot.Find(optionObjectName) as RectTransform;
        if (optionRoot == null)
        {
            optionRoot = CreateAudioOption(optionsRoot, template, optionObjectName, textObjectName);
        }

        if (optionRoot == null)
        {
            return null;
        }

        PauseAudioOption option = optionRoot.GetComponent<PauseAudioOption>();
        if (option == null)
        {
            option = optionRoot.gameObject.AddComponent<PauseAudioOption>();
        }

        TMP_Text text = optionRoot.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
        {
            TMP_Text templateText = template.GetComponentInChildren<TMP_Text>(true);
            if (templateText != null)
            {
                GameObject clonedText = Instantiate(templateText.gameObject, optionRoot, false);
                clonedText.name = textObjectName;
                text = clonedText.GetComponent<TMP_Text>();
            }
        }
        else
        {
            text.gameObject.name = textObjectName;
        }

        optionRoot.gameObject.name = optionObjectName;
        option.Configure(this, channel, text, optionLabel, audioOptionStep);
        return option;
    }

    private PauseCursorAction EnsureActionOption(
        RectTransform optionsRoot,
        RectTransform template,
        string optionObjectName,
        string textObjectName,
        PauseCursorAction.PauseAction action,
        string optionLabel)
    {
        RectTransform optionRoot = optionsRoot.Find(optionObjectName) as RectTransform;
        if (optionRoot == null)
        {
            optionRoot = CreateMenuOption(optionsRoot, template, optionObjectName, textObjectName);
        }

        if (optionRoot == null)
        {
            return null;
        }

        PauseCursorAction option = optionRoot.GetComponent<PauseCursorAction>();
        if (option == null)
        {
            option = optionRoot.gameObject.AddComponent<PauseCursorAction>();
        }

        option.Configure(this, action);
        optionRoot.gameObject.name = optionObjectName;
        SetMenuOptionLabel(optionRoot, optionLabel, textObjectName);
        return option;
    }

    private RectTransform CreateAudioOption(RectTransform optionsRoot, RectTransform template, string optionObjectName, string textObjectName)
    {
        RectTransform optionRoot = CreateMenuOption(optionsRoot, template, optionObjectName, textObjectName);
        if (optionRoot == null)
        {
            return null;
        }

        if (optionRoot.GetComponent<PauseAudioOption>() == null)
        {
            optionRoot.gameObject.AddComponent<PauseAudioOption>();
        }

        return optionRoot;
    }

    private RectTransform CreateMenuOption(RectTransform optionsRoot, RectTransform template, string optionObjectName, string textObjectName)
    {
        GameObject optionObject = new GameObject(
            optionObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer));
        optionObject.layer = template.gameObject.layer;

        RectTransform optionRoot = optionObject.GetComponent<RectTransform>();
        optionRoot.SetParent(optionsRoot, false);
        CopyRectTransformLayout(template, optionRoot);

        TMP_Text templateText = template.GetComponentInChildren<TMP_Text>(true);
        if (templateText != null)
        {
            GameObject clonedText = Instantiate(templateText.gameObject, optionRoot, false);
            clonedText.name = textObjectName;
        }

        return optionRoot;
    }

    private static RectTransform ResolveAudioOptionTemplate(RectTransform optionsRoot)
    {
        if (optionsRoot == null)
        {
            return null;
        }

        string[] preferredNames = { "ResumeButton", "SaveButton", "QuitButton" };
        for (int i = 0; i < preferredNames.Length; i++)
        {
            RectTransform match = optionsRoot.Find(preferredNames[i]) as RectTransform;
            if (match != null)
            {
                return match;
            }
        }

        for (int i = 0; i < optionsRoot.childCount; i++)
        {
            RectTransform child = optionsRoot.GetChild(i) as RectTransform;
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static void CopyRectTransformLayout(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    private static void SetOptionSibling(PauseAudioOption option, RectTransform parent, int index)
    {
        if (option == null || parent == null)
        {
            return;
        }

        int maxIndex = Mathf.Max(0, parent.childCount - 1);
        option.transform.SetSiblingIndex(Mathf.Clamp(index, 0, maxIndex));
    }

    private static void SetOptionSibling(PauseCursorAction option, RectTransform parent, int index)
    {
        if (option == null || parent == null)
        {
            return;
        }

        int maxIndex = Mathf.Max(0, parent.childCount - 1);
        option.transform.SetSiblingIndex(Mathf.Clamp(index, 0, maxIndex));
    }

    private void CaptureDefaultPauseOptions(RectTransform optionsRoot)
    {
        if (defaultPauseOptionsCaptured || optionsRoot == null)
        {
            return;
        }

        defaultPauseOptions.Clear();
        for (int i = 0; i < optionsRoot.childCount; i++)
        {
            RectTransform child = optionsRoot.GetChild(i) as RectTransform;
            if (child == null || IsRuntimeAudioOptionName(child.name))
            {
                continue;
            }

            defaultPauseOptions.Add(child);
        }

        defaultPauseOptionsCaptured = true;
    }

    private void ApplyAudioOptionsVisibility()
    {
        bool showAudioOptions = createAudioOptions && audioOptionsOpen;

        for (int i = 0; i < defaultPauseOptions.Count; i++)
        {
            if (defaultPauseOptions[i] != null)
            {
                defaultPauseOptions[i].gameObject.SetActive(!showAudioOptions);
            }
        }

        SetOptionActive(audioOptionsButton, !showAudioOptions && createAudioOptions);
        SetOptionActive(musicVolumeOption, showAudioOptions);
        SetOptionActive(sfxVolumeOption, showAudioOptions);
        SetOptionActive(audioOptionsBackButton, showAudioOptions);
    }

    private void FocusPauseOption(RectTransform preferred)
    {
        if (cursorController == null)
        {
            return;
        }

        cursorController.Refresh();
        if (preferred != null && cursorController.TrySetCurrentItem(preferred, false))
        {
            return;
        }

        cursorController.SelectFirst();
    }

    private static void SetOptionActive(Component option, bool active)
    {
        if (option == null)
        {
            return;
        }

        option.gameObject.SetActive(active);
    }

    private static void SetActionOptionLabel(PauseCursorAction option, string label)
    {
        if (option == null)
        {
            return;
        }

        SetMenuOptionLabel(option.transform as RectTransform, label);
    }

    private static void SetMenuOptionLabel(RectTransform optionRoot, string label, string textObjectName = null)
    {
        if (optionRoot == null)
        {
            return;
        }

        TMP_Text text = optionRoot.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(textObjectName))
        {
            text.gameObject.name = textObjectName;
        }

        text.text = label;
    }

    private static bool IsRuntimeAudioOptionName(string optionName)
    {
        return optionName == "AudioOptionsButton"
            || optionName == "MusicVolumeOption"
            || optionName == "SfxVolumeOption"
            || optionName == "AudioOptionsBackButton";
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

        if (cursorNavigator == null && cursorController != null)
        {
            cursorNavigator = cursorController.GetComponent<MenuCursorNavigator>();
            if (cursorNavigator == null)
            {
                cursorNavigator = cursorController.gameObject.AddComponent<MenuCursorNavigator>();
            }
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

    private void LockGameplayInput(bool locked)
    {
        if (!lockGameplayInput)
        {
            return;
        }

        if (locked)
        {
            if (gameplayInputLocked)
            {
                return;
            }

            SquadManager manager = SquadManager.Instance;
            if (manager == null)
            {
                manager = FindAnyObjectByType<SquadManager>();
            }

            if (manager != null)
            {
                manager.SetInputLocked(true);
                gameplayInputLocked = true;
            }
        }
        else
        {
            if (!gameplayInputLocked)
            {
                return;
            }

            SquadManager manager = SquadManager.Instance;
            if (manager == null)
            {
                manager = FindAnyObjectByType<SquadManager>();
            }

            if (manager != null)
            {
                manager.SetInputLocked(false);
            }

            gameplayInputLocked = false;
        }
    }

    private void ApplyCursorSizing(bool opened)
    {
        if (!forceCursorFixedSize || cursorController == null)
        {
            return;
        }

        if (opened)
        {
            EnsureCursorLayout();
            if (!cursorSizeCached)
            {
                cachedMatchTargetSize = cursorController.matchTargetSize;
                cursorSizeCached = true;
            }

            cursorController.matchTargetSize = false;
        }
        else if (cursorSizeCached)
        {
            cursorController.matchTargetSize = cachedMatchTargetSize;
            cursorSizeCached = false;
        }
    }

    private void EnsureCursorLayout()
    {
        if (cursorController == null || cursorController.cursor == null)
        {
            return;
        }

        LayoutElement layout = cursorController.cursor.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = cursorController.cursor.gameObject.AddComponent<LayoutElement>();
        }

        layout.ignoreLayout = true;
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
