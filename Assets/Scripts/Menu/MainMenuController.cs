using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Controleur du menu principal base sur une UI creee dans la scene.
public class MainMenuController : MonoBehaviour
{
    public const string DefaultMenuSceneName = "MainMenu";

    private enum MenuState
    {
        TitleCard,
        GameOptions,
        LoadMenu
    }

    [Header("Scene")]
    [SerializeField] private CanvasGroup titleCardGroup;
    [SerializeField] private CanvasGroup gameOptionsGroup;
    [SerializeField] private CanvasGroup loadMenuGroup;
    [SerializeField] private CanvasGroup mainMenuGroup;
    [SerializeField] private bool hideTitleCardOnProceed = true;
    [SerializeField] private bool waitForAnyInput = true;
    [SerializeField] private float panelFadeDuration = 1.5f;
    [SerializeField] private float panelHideDuration = 1f;
    [SerializeField] private bool fadeUseUnscaledTime = true;

    [Header("Title Card FX")]
    [SerializeField] private AudioClipSO titleCardProceedSfx;
    [SerializeField] private GameObject titleCardFlamesPrefab;
    [SerializeField] private Transform titleCardFlamesParent;
    [SerializeField] private bool spawnFlamesOnce = true;

    [Header("Save List")]
    [SerializeField] private Transform sessionsRoot;
    [SerializeField] private Transform savesRoot;
    [SerializeField] private MainMenuSessionEntryUI sessionEntryPrefab;
    [SerializeField] private MainMenuSaveEntryUI saveEntryPrefab;
    [SerializeField] private GameObject emptySessionsPlaceholder;

    [Header("Details")]
    [SerializeField] private TMP_Text detailsTitle;
    [SerializeField] private TMP_Text detailsBody;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private AspectRatioFitter previewAspect;
    [SerializeField] private string screenshotFileName = "screenshot.png";

    [Header("Actions")]
    [SerializeField] private TMP_InputField sessionNameInput;
    [SerializeField] private TMP_Text statusText;

    [Header("Game Options")]
    [Header("Shared Cursor")]
    [SerializeField] private CursorController sharedCursor;
    [SerializeField] private RectTransform gameOptionsCursorRoot;
    [SerializeField] private RectTransform loadMenuCursorRoot;

    [Header("Confirm Delete")]
    [SerializeField] private GameObject confirmRoot;
    [SerializeField] private TMP_Text confirmText;

    [Header("New Game Prompt")]
    [SerializeField] private CanvasGroup newGamePanelGroup;
    [SerializeField] private TMP_InputField newGameNameInput;
    [SerializeField] private MenuCursorAction newGameConfirmAction;
    [SerializeField] private MenuCursorAction newGameCancelAction;
    [SerializeField] private RectTransform newGameCursorRoot;
    [SerializeField] private string defaultNewGameSaveName = "Depart";
    [SerializeField] private bool requireNewGameName = true;
    [SerializeField] private bool autoFocusNewGameInput = true;
    [SerializeField, Range(0.1f, 1f)] private float newGameConfirmDisabledAlpha = 0.4f;
    [SerializeField] private bool useManualTextInputFallback = true;

    [Header("Virtual Keyboard")]
    [SerializeField] private CanvasGroup virtualKeyboardGroup;
    [SerializeField] private RectTransform virtualKeyboardRoot;

    [Header("Loading Screen")]
    [SerializeField] private CanvasGroup loadingGroup;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string loadingMessage = "Chargement...";

    [Header("Netcode")]
    [SerializeField] private string gameplaySceneName = "OutdoorsScene";
    [SerializeField] private int codeLength = 6;
    [SerializeField] private ushort basePort = 7000;
    [SerializeField] private ushort portRange = 1000;
    [SerializeField] private string hostLoopbackAddress = "127.0.0.1";
    [SerializeField] private string listenAddress = "0.0.0.0";

    [Header("Entry Colors")]
    [SerializeField] private Color entryColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color entryHoverColor = new Color(0.6f, 0.8f, 1f, 0.18f);
    [SerializeField] private Color entrySelectedColor = new Color(0.6f, 0.8f, 1f, 0.32f);

    private MainMenuSessionEntryUI hoveredSessionEntry;
    private SaveSessionInfo selectedSession;
    private SaveSlotInfo selectedSave;
    private MainMenuSaveEntryUI selectedSaveView;
    private SaveSlotInfo pendingDelete;
    private Texture2D previewTexture;
    private bool waitingForInput;
    private MenuState currentMenu = MenuState.TitleCard;
    private Coroutine cursorSnapRoutine;
    private RectTransform currentCursorRoot;
    private bool hasInitializedState;
    private bool titleCardProceedTriggered;
    private GameObject titleCardFlamesInstance;
    private readonly Dictionary<CanvasGroup, Coroutine> fadeRoutines = new Dictionary<CanvasGroup, Coroutine>();
    private bool newGamePromptOpen;
    private bool isLoading;
    private readonly Dictionary<Graphic, float> newGameConfirmGraphicAlphas = new Dictionary<Graphic, float>();
    private bool textInputSubscribed;
    private Keyboard subscribedKeyboard;
    private bool warnedMissingNewGameInputText;

    private void Awake()
    {
        EnsureSaveManager();
        ResolveOptionalReferences();
        ConfigureNewGameActions();
        InitializeState();
        InitializeOverlays();
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
        RefreshSessions();

        if (currentMenu == MenuState.GameOptions || currentMenu == MenuState.LoadMenu)
        {
            InputFocusStack.Push(this);
        }
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;
        InputFocusStack.Pop(this);
        RegisterTextInput(false);
    }

    private void OnDestroy()
    {
        ClearPreviewTexture();
    }

    private void Update()
    {
        if (!waitForAnyInput || !waitingForInput || currentMenu != MenuState.TitleCard)
        {
            return;
        }

        if (AnyInputPressedThisFrame())
        {
            HandleTitleCardProceed();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (InputFocusStack.HasAnyFocus() && !HasInputFocus())
        {
            return;
        }

        if (!CanProcessInteract())
        {
            return;
        }

        if (hoveredSessionEntry != null)
        {
            OnSessionInteract(hoveredSessionEntry);
        }
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (InputFocusStack.HasAnyFocus() && !HasInputFocus())
        {
            return;
        }

        HandleBackAction();
    }

    private bool HasInputFocus()
    {
        return InputFocusStack.HasFocus(this);
    }

    private bool CanProcessInteract()
    {
        if (newGamePromptOpen || isLoading)
        {
            return false;
        }

        if (InputFocusStack.HasAnyFocus() && !HasInputFocus())
        {
            return false;
        }

        CanvasGroup loadGroup = ResolveLoadMenuGroup();
        if (currentMenu != MenuState.LoadMenu || loadGroup == null || !loadGroup.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (HasInputFocus())
        {
            return true;
        }

        return !InputFocusStack.HasAnyFocus();
    }

    private void InitializeState()
    {
        if (waitForAnyInput && titleCardGroup != null)
        {
            SetMenuState(MenuState.TitleCard);
        }
        else
        {
            ShowGameOptionsMenu();
        }
    }

    private CanvasGroup ResolveLoadMenuGroup()
    {
        return loadMenuGroup != null ? loadMenuGroup : mainMenuGroup;
    }

    private void ShowGameOptionsMenu()
    {
        if (gameOptionsGroup == null && ResolveLoadMenuGroup() != null)
        {
            SetMenuState(MenuState.LoadMenu);
            return;
        }

        SetMenuState(MenuState.GameOptions);
    }

    private void ShowLoadMenu()
    {
        SetMenuState(MenuState.LoadMenu);
    }

    private void SetMenuState(MenuState state)
    {
        currentMenu = state;

        if (titleCardGroup != null)
        {
            if (state == MenuState.TitleCard)
            {
                ShowTitleCard(titleCardGroup);
                titleCardProceedTriggered = false;
            }
            else
            {
                HidePanel(titleCardGroup);
            }
        }

        if (gameOptionsGroup != null)
        {
            if (state == MenuState.GameOptions)
            {
                ShowPanel(gameOptionsGroup);
            }
            else
            {
                HidePanel(gameOptionsGroup);
            }
        }

        CanvasGroup loadGroup = ResolveLoadMenuGroup();
        if (loadGroup != null)
        {
            if (state == MenuState.LoadMenu)
            {
                ShowPanel(loadGroup);
            }
            else
            {
                HidePanel(loadGroup);
            }
        }

        waitingForInput = waitForAnyInput && state == MenuState.TitleCard;

        if (state == MenuState.GameOptions || state == MenuState.LoadMenu)
        {
            InputFocusStack.Push(this);
        }
        else
        {
            InputFocusStack.Pop(this);
        }

        UpdateCursorTarget();
        hasInitializedState = true;
    }

    private void UpdateCursorTarget()
    {
        if (sharedCursor == null)
        {
            return;
        }

        if (newGamePromptOpen)
        {
            UpdateCursorTargetForPrompt();
            return;
        }

        RectTransform targetRoot = null;
        if (currentMenu == MenuState.GameOptions)
        {
            targetRoot = ResolveCursorRoot(gameOptionsCursorRoot, gameOptionsGroup);
        }
        else if (currentMenu == MenuState.LoadMenu)
        {
            targetRoot = ResolveCursorRoot(loadMenuCursorRoot, ResolveLoadMenuGroup());
        }

        currentCursorRoot = targetRoot;
        sharedCursor.itemsParent = targetRoot;
        sharedCursor.layoutGroup = targetRoot != null ? targetRoot.GetComponent<LayoutGroup>() : null;
        sharedCursor.Refresh();
        StartCursorSnap();
    }

    private void UpdateCursorTargetForPrompt()
    {
        if (sharedCursor == null)
        {
            return;
        }

        RectTransform targetRoot = ResolveCursorRoot(newGameCursorRoot, newGamePanelGroup);
        currentCursorRoot = targetRoot;
        sharedCursor.itemsParent = targetRoot;
        sharedCursor.layoutGroup = targetRoot != null ? targetRoot.GetComponent<LayoutGroup>() : null;
        sharedCursor.Refresh();
        StartCursorSnap();
    }

    private void StartCursorSnap()
    {
        if (cursorSnapRoutine != null)
        {
            StopCoroutine(cursorSnapRoutine);
        }
        cursorSnapRoutine = StartCoroutine(SnapCursorNextFrame());
    }

    private System.Collections.IEnumerator SnapCursorNextFrame()
    {
        yield return null;
        if (sharedCursor == null)
        {
            cursorSnapRoutine = null;
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        sharedCursor.Refresh();
        if (!sharedCursor.SelectFirst())
        {
            RectTransform fallback = FindFirstCursorItem(currentCursorRoot);
            if (fallback != null)
            {
                sharedCursor.TrySetCurrentItem(fallback, true);
            }
        }
        cursorSnapRoutine = null;
    }

    private RectTransform ResolveCursorRoot(RectTransform explicitRoot, CanvasGroup group)
    {
        RectTransform root = explicitRoot;
        if (root == null && group != null)
        {
            root = group.transform as RectTransform;
        }

        if (root == null)
        {
            return null;
        }

        if (HasDirectActiveChildren(root))
        {
            return root;
        }

        RectTransform fallback = FindFirstCursorItem(root);
        if (fallback != null && fallback.parent is RectTransform parent)
        {
            return parent;
        }

        return root;
    }

    private static bool HasDirectActiveChildren(RectTransform root)
    {
        if (root == null)
        {
            return false;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private static RectTransform FindFirstCursorItem(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        MenuCursorAction action = root.GetComponentInChildren<MenuCursorAction>(true);
        if (action != null)
        {
            return action.transform as RectTransform;
        }

        MenuCursorItem item = root.GetComponentInChildren<MenuCursorItem>(true);
        if (item != null)
        {
            return item.transform as RectTransform;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMenuCursorHandler)
            {
                return behaviours[i].transform as RectTransform;
            }
        }

        return null;
    }

    private void ShowTitleCard(CanvasGroup group)
    {
        if (group == null)
        {
            return;
        }

        StartFade(group, 1f, true);
    }

    private void ShowPanel(CanvasGroup group)
    {
        if (group == null)
        {
            return;
        }

        StartFade(group, 1f, true);
    }

    private void HidePanel(CanvasGroup group)
    {
        if (group == null)
        {
            return;
        }

        StartFade(group, 0f, false, panelHideDuration);
    }

    private void ShowVirtualKeyboard()
    {
        if (virtualKeyboardGroup != null)
        {
            StartFade(virtualKeyboardGroup, 1f, true);
            return;
        }

        if (virtualKeyboardRoot != null)
        {
            virtualKeyboardRoot.gameObject.SetActive(true);
        }
    }

    private void HideVirtualKeyboard()
    {
        if (virtualKeyboardGroup != null)
        {
            StartFade(virtualKeyboardGroup, 0f, false);
            return;
        }

        if (virtualKeyboardRoot != null)
        {
            virtualKeyboardRoot.gameObject.SetActive(false);
        }
    }

    private void ApplyVirtualKeyboardImmediate(bool show)
    {
        if (virtualKeyboardGroup != null)
        {
            ApplyFadeImmediate(virtualKeyboardGroup, show ? 1f : 0f, show);
            return;
        }

        if (virtualKeyboardRoot != null)
        {
            virtualKeyboardRoot.gameObject.SetActive(show);
        }
    }

    private void StartFade(CanvasGroup group, float targetAlpha, bool show, float durationOverride = -1f)
    {
        if (group == null)
        {
            return;
        }

        if (!hasInitializedState)
        {
            ApplyFadeImmediate(group, targetAlpha, show);
            return;
        }

        if (fadeRoutines.TryGetValue(group, out Coroutine routine) && routine != null)
        {
            StopCoroutine(routine);
        }

        float duration = durationOverride >= 0f ? durationOverride : panelFadeDuration;
        fadeRoutines[group] = StartCoroutine(FadeRoutine(group, targetAlpha, show, duration));
    }

    private void ApplyFadeImmediate(CanvasGroup group, float targetAlpha, bool show)
    {
        if (group == null)
        {
            return;
        }

        if (show)
        {
            group.gameObject.SetActive(true);
        }

        group.alpha = targetAlpha;
        bool visible = targetAlpha > 0.001f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        if (!visible)
        {
            group.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeRoutine(CanvasGroup group, float targetAlpha, bool show, float durationOverride)
    {
        if (group == null)
        {
            yield break;
        }

        if (show)
        {
            group.gameObject.SetActive(true);
        }

        group.interactable = false;
        group.blocksRaycasts = false;

        float duration = Mathf.Max(0.01f, durationOverride);
        float startAlpha = group.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            elapsed += fadeUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        group.alpha = targetAlpha;
        bool visible = targetAlpha > 0.001f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        if (!visible)
        {
            group.gameObject.SetActive(false);
        }
    }

    private void HandleTitleCardProceed()
    {
        if (titleCardProceedTriggered)
        {
            return;
        }

        titleCardProceedTriggered = true;
        PlayTitleCardSfx();
        SpawnTitleCardFlames();
        ShowGameOptionsMenu();
    }

    private void HandleBackAction()
    {
        if (isLoading)
        {
            return;
        }

        if (newGamePromptOpen)
        {
            CancelNewGame();
            return;
        }

        if (confirmRoot != null && confirmRoot.activeSelf)
        {
            CancelDelete();
            return;
        }

        if (currentMenu == MenuState.LoadMenu)
        {
            if (IsCursorOnSavesRoot())
            {
                FocusSessionsRoot();
                return;
            }

            ShowGameOptionsMenu();
            return;
        }

        if (currentMenu == MenuState.GameOptions)
        {
            if (waitForAnyInput && titleCardGroup != null)
            {
                SetMenuState(MenuState.TitleCard);
            }
            return;
        }
    }

    private void PlayTitleCardSfx()
    {
        if (titleCardProceedSfx == null)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClip(titleCardProceedSfx, Vector3.zero);
        }
        else if (titleCardProceedSfx.audioClip != null)
        {
            AudioSource.PlayClipAtPoint(titleCardProceedSfx.audioClip, Vector3.zero, Mathf.Clamp01(titleCardProceedSfx.volume));
        }
    }

    private void SpawnTitleCardFlames()
    {
        if (titleCardFlamesPrefab == null)
        {
            return;
        }

        if (spawnFlamesOnce && titleCardFlamesInstance != null)
        {
            return;
        }

        Transform parent = titleCardFlamesParent;
        if (parent == null && mainMenuGroup != null)
        {
            parent = mainMenuGroup.transform;
        }
        if (parent == null && titleCardGroup != null)
        {
            parent = titleCardGroup.transform;
        }
        if (parent == null)
        {
            parent = transform;
        }

        titleCardFlamesInstance = Instantiate(titleCardFlamesPrefab, parent);
    }

    private void InitializeOverlays()
    {
        if (newGamePanelGroup != null)
        {
            ApplyFadeImmediate(newGamePanelGroup, 0f, false);
        }

        ApplyVirtualKeyboardImmediate(false);

        if (loadingGroup != null)
        {
            ApplyFadeImmediate(loadingGroup, 0f, false);
        }
    }

    private void ResolveOptionalReferences()
    {
        if (newGamePanelGroup == null)
        {
            newGamePanelGroup = FindCanvasGroup("MainMenu_NewGame");
        }

        if (newGameNameInput == null && newGamePanelGroup != null)
        {
            newGameNameInput = newGamePanelGroup.GetComponentInChildren<TMP_InputField>(true);
        }

        if (newGameConfirmAction == null && newGamePanelGroup != null)
        {
            newGameConfirmAction = FindMenuCursorActionByName(newGamePanelGroup.transform, "Confirm");
        }

        if (newGameCancelAction == null && newGamePanelGroup != null)
        {
            newGameCancelAction = FindMenuCursorActionByName(newGamePanelGroup.transform, "Cancel");
        }

        if (newGameCursorRoot == null && newGamePanelGroup != null)
        {
            Transform confirm = FindInHierarchy(newGamePanelGroup.transform, "Confirm");
            Transform cancel = FindInHierarchy(newGamePanelGroup.transform, "Cancel");
            if (confirm != null && cancel != null && confirm.parent == cancel.parent)
            {
                newGameCursorRoot = confirm.parent as RectTransform;
            }
            else
            {
                newGameCursorRoot = newGamePanelGroup.transform as RectTransform;
            }
        }

        if (virtualKeyboardGroup == null)
        {
            virtualKeyboardGroup = FindCanvasGroup("MainMenu_VirtualKeyboard");
        }
        if (virtualKeyboardRoot == null && virtualKeyboardGroup != null)
        {
            virtualKeyboardRoot = virtualKeyboardGroup.transform as RectTransform;
        }
        if (virtualKeyboardRoot == null)
        {
            Transform found = FindInHierarchy(transform, "MainMenu_VirtualKeyboard");
            if (found != null)
            {
                virtualKeyboardRoot = found as RectTransform;
                if (virtualKeyboardGroup == null)
                {
                    virtualKeyboardGroup = virtualKeyboardRoot.GetComponent<CanvasGroup>();
                }
            }
        }
        if (virtualKeyboardGroup == null && virtualKeyboardRoot != null)
        {
            virtualKeyboardGroup = virtualKeyboardRoot.gameObject.AddComponent<CanvasGroup>();
        }


        if (loadingGroup == null)
        {
            loadingGroup = FindCanvasGroup("MainMenu_Loading");
        }

        if (loadingText == null && loadingGroup != null)
        {
            loadingText = loadingGroup.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private CanvasGroup FindCanvasGroup(string name)
    {
        Transform found = FindInHierarchy(transform, name);
        return found != null ? found.GetComponent<CanvasGroup>() : null;
    }

    private static MenuCursorAction FindMenuCursorActionByName(Transform root, string name)
    {
        Transform found = FindInHierarchy(root, name);
        if (found == null)
        {
            return null;
        }

        MenuCursorAction action = found.GetComponent<MenuCursorAction>();
        if (action == null)
        {
            action = found.gameObject.AddComponent<MenuCursorAction>();
        }
        return action;
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
            Transform child = root.GetChild(i);
            Transform match = FindInHierarchy(child, name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private void RefreshSessions()
    {
        if (SaveSessionManager.Instance == null)
        {
            return;
        }

        SaveSessionManager.Instance.ReloadSessions();
        IReadOnlyList<SaveSessionInfo> sessions = SaveSessionManager.Instance.Sessions;
        if (sessions == null || sessions.Count == 0)
        {
            bool hasExistingEntries = sessionsRoot != null && sessionsRoot.childCount > 0;
            if (emptySessionsPlaceholder != null)
            {
                emptySessionsPlaceholder.SetActive(!hasExistingEntries);
            }
            selectedSession = null;
            ClearSavesUI();
            if (!hasExistingEntries)
            {
                ClearSessionsUI();
            }
            return;
        }

        ClearSessionsUI();

        if (emptySessionsPlaceholder != null)
        {
            emptySessionsPlaceholder.SetActive(false);
        }

        if (sessionsRoot == null || sessionEntryPrefab == null)
        {
            SetStatus("References UI manquantes.");
            return;
        }

        MainMenuSessionEntryUI firstEntry = null;
        MainMenuSessionEntryUI matchedEntry = null;
        string selectedSessionId = selectedSession != null ? selectedSession.sessionId : null;
        for (int i = 0; i < sessions.Count; i++)
        {
            SaveSessionInfo session = sessions[i];
            if (session == null)
            {
                continue;
            }

            MainMenuSessionEntryUI sessionEntry = Instantiate(sessionEntryPrefab, sessionsRoot);
            sessionEntry.Initialize(this, session, false);
            if (firstEntry == null)
            {
                firstEntry = sessionEntry;
            }
            if (!string.IsNullOrEmpty(selectedSessionId) && session.sessionId == selectedSessionId)
            {
                matchedEntry = sessionEntry;
            }
        }

        MainMenuSessionEntryUI entryToSelect = matchedEntry != null ? matchedEntry : firstEntry;
        if (entryToSelect != null)
        {
            SelectSession(entryToSelect, false);
        }
        else
        {
            ClearSavesUI();
        }
    }

    private void ClearSessionsUI()
    {
        selectedSave = null;
        selectedSaveView = null;
        pendingDelete = null;
        hoveredSessionEntry = null;

        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
        }

        ClearSavesUI();

        if (sessionsRoot == null)
        {
            return;
        }

        for (int i = sessionsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(sessionsRoot.GetChild(i).gameObject);
        }
    }

    internal void OnSessionHovered(MainMenuSessionEntryUI entry)
    {
        hoveredSessionEntry = entry;
    }

    internal void OnSessionUnhovered(MainMenuSessionEntryUI entry)
    {
        if (hoveredSessionEntry == entry)
        {
            hoveredSessionEntry = null;
        }
    }

    internal void OnSessionInteract(MainMenuSessionEntryUI entry)
    {
        if (entry == null)
        {
            return;
        }

        SelectSession(entry, true);
    }

    private void SelectSession(MainMenuSessionEntryUI entry, bool focusSaves)
    {
        if (entry == null)
        {
            return;
        }

        SaveSessionInfo session = entry.Session;
        if (session == null)
        {
            return;
        }

        selectedSession = session;
        RebuildSavesList(session);
        if (focusSaves)
        {
            FocusSavesRoot();
        }
    }

    private void FocusSavesRoot()
    {
        if (sharedCursor == null || savesRoot == null)
        {
            return;
        }

        RectTransform targetRoot = savesRoot as RectTransform;
        if (targetRoot == null)
        {
            return;
        }

        if (!HasDirectActiveChildren(targetRoot))
        {
            RectTransform fallback = FindFirstCursorItem(targetRoot);
            if (fallback == null)
            {
                return;
            }
        }

        currentCursorRoot = targetRoot;
        sharedCursor.itemsParent = targetRoot;
        sharedCursor.layoutGroup = targetRoot.GetComponent<LayoutGroup>();
        sharedCursor.Refresh();
        StartCursorSnap();
    }

    private void FocusSessionsRoot()
    {
        if (sharedCursor == null || sessionsRoot == null)
        {
            return;
        }

        RectTransform targetRoot = sessionsRoot as RectTransform;
        if (targetRoot == null)
        {
            return;
        }

        if (!HasDirectActiveChildren(targetRoot))
        {
            RectTransform fallback = FindFirstCursorItem(targetRoot);
            if (fallback == null)
            {
                return;
            }
        }

        currentCursorRoot = targetRoot;
        sharedCursor.itemsParent = targetRoot;
        sharedCursor.layoutGroup = targetRoot.GetComponent<LayoutGroup>();
        sharedCursor.Refresh();
        StartCursorSnap();
    }

    private bool IsCursorOnSavesRoot()
    {
        if (savesRoot == null)
        {
            return false;
        }

        if (currentCursorRoot == savesRoot)
        {
            return true;
        }

        RectTransform current = sharedCursor != null ? sharedCursor.itemsParent : null;
        if (current == null)
        {
            return false;
        }

        return current == savesRoot;
    }

    private void RebuildSavesList(SaveSessionInfo session)
    {
        ClearSavesUI();

        if (savesRoot == null || saveEntryPrefab == null)
        {
            SetStatus("References UI manquantes.");
            return;
        }

        if (session == null || session.saves == null || session.saves.Count == 0)
        {
            return;
        }

        MainMenuSaveEntryUI firstEntry = null;
        SaveSlotInfo firstSave = null;
        MainMenuSaveEntryUI matchedEntry = null;
        SaveSlotInfo matchedSave = null;
        string selectedSaveId = selectedSave != null ? selectedSave.saveId : null;
        for (int j = 0; j < session.saves.Count; j++)
        {
            SaveSlotInfo save = session.saves[j];
            if (save == null)
            {
                continue;
            }

            MainMenuSaveEntryUI saveEntry = Instantiate(saveEntryPrefab, savesRoot);
            saveEntry.Initialize(this, save, entryColor, entryHoverColor, entrySelectedColor);
            if (firstEntry == null)
            {
                firstEntry = saveEntry;
                firstSave = save;
            }
            if (!string.IsNullOrEmpty(selectedSaveId) && save.saveId == selectedSaveId)
            {
                matchedEntry = saveEntry;
                matchedSave = save;
            }
        }

        MainMenuSaveEntryUI entryToSelect = matchedEntry != null ? matchedEntry : firstEntry;
        SaveSlotInfo saveToSelect = matchedSave != null ? matchedSave : firstSave;
        if (entryToSelect != null && saveToSelect != null)
        {
            OnSaveSelected(saveToSelect, entryToSelect);
        }
    }

    private void ClearSavesUI()
    {
        selectedSave = null;
        selectedSaveView = null;
        pendingDelete = null;

        ClearPreviewTexture();
        if (detailsTitle != null)
        {
            detailsTitle.text = "Details";
        }
        if (detailsBody != null)
        {
            detailsBody.text = "Selectionne une sauvegarde.";
        }

        if (savesRoot == null)
        {
            return;
        }

        for (int i = savesRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(savesRoot.GetChild(i).gameObject);
        }
    }

    internal void OnSaveHovered(SaveSlotInfo save)
    {
        ShowSaveDetails(save);
    }

    internal void OnSaveSelected(SaveSlotInfo save, MainMenuSaveEntryUI view)
    {
        if (selectedSaveView != null)
        {
            selectedSaveView.SetSelected(false);
        }

        selectedSave = save;
        selectedSaveView = view;
        if (selectedSaveView != null)
        {
            selectedSaveView.SetSelected(true);
        }

        ShowSaveDetails(save);
    }

    private void ShowSaveDetails(SaveSlotInfo save)
    {
        if (save == null)
        {
            return;
        }

        DateTime savedAt = save.savedAtUtcTicks > 0
            ? new DateTime(save.savedAtUtcTicks, DateTimeKind.Utc).ToLocalTime()
            : DateTime.MinValue;

        TimeSpan playtime = TimeSpan.FromSeconds(Mathf.Max(0f, save.playTimeSeconds));
        string playtimeText = $"{(int)playtime.TotalHours:00}:{playtime.Minutes:00}:{playtime.Seconds:00}";

        if (detailsTitle != null)
        {
            detailsTitle.text = save.sessionName;
        }

        if (detailsBody != null)
        {
            detailsBody.text =
                $"Sauvegarde: {save.saveName}\n" +
                $"Date: {(savedAt == DateTime.MinValue ? "Inconnue" : savedAt.ToString("dd/MM/yyyy HH:mm"))}\n" +
                $"Temps de jeu: {playtimeText}\n" +
                $"Scene: {save.sceneName}";
        }

        UpdatePreview(save);
    }

    private void UpdatePreview(SaveSlotInfo save)
    {
        ClearPreviewTexture();

        if (previewImage == null || save == null)
        {
            return;
        }

        string path = GetScreenshotPath(save);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            previewImage.texture = null;
            return;
        }

        try
        {
            byte[] data = File.ReadAllBytes(path);
            if (data == null || data.Length == 0)
            {
                previewImage.texture = null;
                return;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(data))
            {
                Destroy(texture);
                previewImage.texture = null;
                return;
            }

            previewTexture = texture;
            previewImage.texture = previewTexture;

            if (previewAspect != null && previewTexture.height > 0)
            {
                previewAspect.aspectRatio = (float)previewTexture.width / previewTexture.height;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MainMenuController: echec chargement screenshot {path}. {ex.Message}");
            previewImage.texture = null;
        }
    }

    private void ClearPreviewTexture()
    {
        if (previewImage != null)
        {
            previewImage.texture = null;
        }

        if (previewTexture != null)
        {
            Destroy(previewTexture);
            previewTexture = null;
        }
    }

    private string GetScreenshotPath(SaveSlotInfo save)
    {
        if (save == null || string.IsNullOrWhiteSpace(save.directoryPath) || string.IsNullOrWhiteSpace(screenshotFileName))
        {
            return null;
        }

        return Path.Combine(save.directoryPath, screenshotFileName);
    }

    private void OnNewGame()
    {
        if (newGamePanelGroup == null)
        {
            CreateNewGameAndStart(string.Empty);
            return;
        }

        ShowNewGamePrompt();
    }

    private void ShowNewGamePrompt()
    {
        if (newGamePromptOpen)
        {
            return;
        }

        newGamePromptOpen = true;

        if (newGamePanelGroup != null)
        {
            StartFade(newGamePanelGroup, 1f, true);
        }

        ShowVirtualKeyboard();
        if (newGamePanelGroup != null)
        {
            newGamePanelGroup.transform.SetAsLastSibling();
        }

        SetActiveMenuInteractable(false);

        if (newGameNameInput != null)
        {
            newGameNameInput.interactable = true;
            newGameNameInput.text = string.Empty;
            RegisterNewGameNameListener(true);
            UpdateNewGameConfirmState();
            if (autoFocusNewGameInput)
            {
                newGameNameInput.Select();
                newGameNameInput.ActivateInputField();
            }
        }

        RegisterTextInput(true);
        UpdateCursorTargetForPrompt();
    }

    private void HideNewGamePrompt()
    {
        if (!newGamePromptOpen)
        {
            return;
        }

        newGamePromptOpen = false;

        if (newGamePanelGroup != null)
        {
            StartFade(newGamePanelGroup, 0f, false);
        }

        HideVirtualKeyboard();

        RegisterNewGameNameListener(false);
        RegisterTextInput(false);
        SetActiveMenuInteractable(true);
        UpdateCursorTarget();
    }

    private void ConfirmNewGame()
    {
        if (requireNewGameName && !IsNewGameNameValid())
        {
            SetStatus("Nom de partie requis.");
            if (newGameNameInput != null)
            {
                newGameNameInput.ActivateInputField();
            }
            return;
        }

        string sessionName = newGameNameInput != null ? newGameNameInput.text : string.Empty;
        HideNewGamePrompt();
        CreateNewGameAndStart(sessionName);
    }

    private void CancelNewGame()
    {
        HideNewGamePrompt();
    }

    private void CreateNewGameAndStart(string sessionName)
    {
        if (SaveSessionManager.Instance == null)
        {
            return;
        }

        SaveSessionInfo session = SaveSessionManager.Instance.CreateSession(sessionName);
        string initialSaveName = string.IsNullOrWhiteSpace(defaultNewGameSaveName) ? "Depart" : defaultNewGameSaveName;
        SaveSlotInfo save = SaveSessionManager.Instance.CreateSave(session.sessionId, initialSaveName);
        if (save == null)
        {
            SetStatus("Impossible de creer la sauvegarde.");
            return;
        }

        SaveSessionManager.Instance.SetActiveSave(session.sessionId, save.saveId);
        StartHostFlow();
    }

    private void RegisterNewGameNameListener(bool enabled)
    {
        if (newGameNameInput == null)
        {
            return;
        }

        newGameNameInput.onValueChanged.RemoveListener(OnNewGameNameChanged);
        if (enabled)
        {
            newGameNameInput.onValueChanged.AddListener(OnNewGameNameChanged);
        }
    }

    private void RegisterTextInput(bool enabled)
    {
        if (!useManualTextInputFallback)
        {
            return;
        }

        if (enabled)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (textInputSubscribed && subscribedKeyboard == keyboard)
            {
                return;
            }

            if (subscribedKeyboard != null)
            {
                subscribedKeyboard.onTextInput -= OnTextInput;
            }

            keyboard.onTextInput += OnTextInput;
            subscribedKeyboard = keyboard;
            textInputSubscribed = true;
            return;
        }

        if (subscribedKeyboard != null)
        {
            subscribedKeyboard.onTextInput -= OnTextInput;
        }
        subscribedKeyboard = null;
        textInputSubscribed = false;
    }

    private void OnTextInput(char character)
    {
        if (!newGamePromptOpen || newGameNameInput == null || !newGameNameInput.interactable)
        {
            return;
        }

        if (!EnsureNewGameInputFieldReady())
        {
            return;
        }

        if (!newGameNameInput.isFocused)
        {
            if (autoFocusNewGameInput)
            {
                newGameNameInput.Select();
                newGameNameInput.ActivateInputField();
            }
            else
            {
                return;
            }
        }

        string text = newGameNameInput.text ?? string.Empty;
        int anchor = newGameNameInput.selectionAnchorPosition;
        int focus = newGameNameInput.selectionFocusPosition;
        if (anchor < 0 || anchor > text.Length)
        {
            anchor = text.Length;
        }
        if (focus < 0 || focus > text.Length)
        {
            focus = text.Length;
        }

        int min = Mathf.Min(anchor, focus);
        int max = Mathf.Max(anchor, focus);
        int caret = focus;

        if (character == '\b')
        {
            if (min != max)
            {
                text = text.Remove(min, max - min);
                caret = min;
            }
            else if (caret > 0)
            {
                text = text.Remove(caret - 1, 1);
                caret -= 1;
            }
        }
        else if (character == '\r' || character == '\n')
        {
            return;
        }
        else
        {
            if (newGameNameInput.characterLimit > 0 && text.Length >= newGameNameInput.characterLimit)
            {
                return;
            }

            if (min != max)
            {
                text = text.Remove(min, max - min);
                caret = min;
            }

            text = text.Insert(caret, character.ToString());
            caret += 1;
        }

        newGameNameInput.SetTextWithoutNotify(text);
        newGameNameInput.caretPosition = caret;
        newGameNameInput.selectionAnchorPosition = caret;
        newGameNameInput.selectionFocusPosition = caret;
        newGameNameInput.ForceLabelUpdate();
        UpdateNewGameConfirmState();
    }

    private bool EnsureNewGameInputFieldReady()
    {
        if (newGameNameInput == null)
        {
            return false;
        }

        if (newGameNameInput.textComponent == null)
        {
            TMP_Text text = FindPreferredInputText(newGameNameInput);
            if (text != null)
            {
                newGameNameInput.textComponent = text;
            }
        }

        if (newGameNameInput.textComponent == null)
        {
            if (!warnedMissingNewGameInputText)
            {
                warnedMissingNewGameInputText = true;
                Debug.LogWarning("MainMenuController: TMP_InputField textComponent manquant sur le champ de nom. Assigne un TextMeshProUGUI dans l'inspecteur.");
            }
            return false;
        }

        return true;
    }

    private static TMP_Text FindPreferredInputText(TMP_InputField field)
    {
        if (field == null)
        {
            return null;
        }

        Transform direct = field.transform.Find("Text");
        if (direct != null)
        {
            TMP_Text directText = direct.GetComponent<TMP_Text>();
            if (directText != null)
            {
                return directText;
            }
        }

        TMP_Text placeholderText = field.placeholder as TMP_Text;
        TMP_Text[] candidates = field.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            TMP_Text candidate = candidates[i];
            if (candidate == null || candidate == placeholderText)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private void OnNewGameNameChanged(string value)
    {
        UpdateNewGameConfirmState();
    }

    private bool IsNewGameNameValid()
    {
        if (newGameNameInput == null)
        {
            return !requireNewGameName;
        }

        return !string.IsNullOrWhiteSpace(newGameNameInput.text);
    }

    private void UpdateNewGameConfirmState()
    {
        bool valid = !requireNewGameName || IsNewGameNameValid();
        SetNewGameConfirmEnabled(valid);
    }

    private void SetNewGameConfirmEnabled(bool enabled)
    {
        if (newGameConfirmAction == null)
        {
            return;
        }

        newGameConfirmAction.enabled = enabled;

        CanvasGroup group = newGameConfirmAction.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = enabled ? 1f : Mathf.Clamp01(newGameConfirmDisabledAlpha);
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }
        else
        {
            ApplyConfirmGraphicsAlpha(enabled);
        }

        if (newGamePromptOpen && sharedCursor != null)
        {
            sharedCursor.Refresh();
            StartCursorSnap();
        }
    }

    private void ApplyConfirmGraphicsAlpha(bool enabled)
    {
        if (newGameConfirmAction == null)
        {
            return;
        }

        Graphic[] graphics = newGameConfirmAction.GetComponentsInChildren<Graphic>(true);
        if (graphics == null || graphics.Length == 0)
        {
            return;
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            if (!newGameConfirmGraphicAlphas.ContainsKey(graphic))
            {
                newGameConfirmGraphicAlphas[graphic] = graphic.color.a;
            }

            float baseAlpha = newGameConfirmGraphicAlphas[graphic];
            Color color = graphic.color;
            color.a = enabled ? baseAlpha : baseAlpha * Mathf.Clamp01(newGameConfirmDisabledAlpha);
            graphic.color = color;
        }
    }

    private void ConfigureNewGameActions()
    {
        if (newGameConfirmAction != null)
        {
            newGameConfirmAction.Configure(this, MenuCursorAction.MenuAction.ConfirmNewGame);
        }

        if (newGameCancelAction != null)
        {
            newGameCancelAction.Configure(this, MenuCursorAction.MenuAction.CancelNewGame);
        }
    }

    private void OnLoadMenuRequested()
    {
        ShowLoadMenu();
        RefreshSessions();
    }

    private void OnMultiplayerRequested()
    {
        SetStatus("Multijoueur non configure.");
    }

    private void OnOptionsRequested()
    {
        SetStatus("Options non configurees.");
    }

    public void UI_NewGame()
    {
        OnNewGame();
    }

    public void UI_ConfirmNewGame()
    {
        ConfirmNewGame();
    }

    public void UI_CancelNewGame()
    {
        CancelNewGame();
    }

    public void UI_VirtualKey(char character)
    {
        OnTextInput(character);
    }

    public void UI_VirtualValidate()
    {
        if (!newGamePromptOpen)
        {
            return;
        }

        ConfirmNewGame();
    }

    public void UI_ShowLoadMenu()
    {
        OnLoadMenuRequested();
    }

    public void UI_LoadSelected()
    {
        OnLoadSelected();
    }

    public void UI_DeleteSelected()
    {
        OnDeleteRequested();
    }

    public void UI_Refresh()
    {
        OnRefresh();
    }

    public void UI_ShowGameOptions()
    {
        ShowGameOptionsMenu();
    }

    public void UI_Multiplayer()
    {
        OnMultiplayerRequested();
    }

    public void UI_Options()
    {
        OnOptionsRequested();
    }

    public void UI_Quit()
    {
        OnQuit();
    }

    private void OnLoadSelected()
    {
        if (SaveSessionManager.Instance == null)
        {
            return;
        }

        if (selectedSave == null)
        {
            SetStatus("Selectionne une sauvegarde.");
            return;
        }

        SaveSessionManager.Instance.SetActiveSave(selectedSave.sessionId, selectedSave.saveId);
        StartHostFlow();
    }

    private void OnDeleteRequested()
    {
        if (selectedSave == null)
        {
            SetStatus("Selectionne une sauvegarde.");
            return;
        }

        pendingDelete = selectedSave;
        if (confirmText != null)
        {
            confirmText.text = $"Supprimer '{selectedSave.saveName}' ?";
        }

        if (confirmRoot != null)
        {
            confirmRoot.SetActive(true);
        }
        else
        {
            ConfirmDelete();
        }
    }

    private void ConfirmDelete()
    {
        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
        }

        if (pendingDelete == null || SaveSessionManager.Instance == null)
        {
            pendingDelete = null;
            return;
        }

        bool deleted = SaveSessionManager.Instance.DeleteSave(pendingDelete.sessionId, pendingDelete.saveId, true);
        SetStatus(deleted ? "Sauvegarde supprimee." : "Echec suppression.");
        pendingDelete = null;
        RefreshSessions();
    }

    private void CancelDelete()
    {
        pendingDelete = null;
        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
        }
    }

    private void OnRefresh()
    {
        RefreshSessions();
        SetStatus("Liste rafraichie.");
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void StartHostFlow()
    {
        if (isLoading)
        {
            return;
        }

        ushort port = ResolvePort();
        NetcodeLauncher launcher = ResolveLauncher();
        if (launcher == null)
        {
            SetStatus("NetcodeLauncher manquant.");
            return;
        }

        bool started = launcher.StartHostWithConnection(hostLoopbackAddress, port, listenAddress);
        if (!started)
        {
            SetStatus("Host deja actif.");
            return;
        }

        ShowLoadingScreen();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
    }

    private void ShowLoadingScreen()
    {
        if (loadingGroup == null)
        {
            return;
        }

        isLoading = true;
        if (loadingText != null && !string.IsNullOrWhiteSpace(loadingMessage))
        {
            loadingText.text = loadingMessage;
        }

        loadingGroup.gameObject.SetActive(true);
        loadingGroup.alpha = 1f;
        loadingGroup.interactable = false;
        loadingGroup.blocksRaycasts = true;

        SetActiveMenuInteractable(false);
    }

    private void HideLoadingScreen()
    {
        if (loadingGroup == null)
        {
            return;
        }

        isLoading = false;
        loadingGroup.alpha = 0f;
        loadingGroup.interactable = false;
        loadingGroup.blocksRaycasts = false;
        loadingGroup.gameObject.SetActive(false);

        SetActiveMenuInteractable(true);
    }

    private void SetActiveMenuInteractable(bool enabled)
    {
        if (currentMenu == MenuState.GameOptions && gameOptionsGroup != null)
        {
            gameOptionsGroup.interactable = enabled;
            gameOptionsGroup.blocksRaycasts = enabled;
            return;
        }

        if (currentMenu == MenuState.LoadMenu)
        {
            CanvasGroup loadGroup = ResolveLoadMenuGroup();
            if (loadGroup != null)
            {
                loadGroup.interactable = enabled;
                loadGroup.blocksRaycasts = enabled;
            }
        }
    }

    private ushort ResolvePort()
    {
        string code = NetcodeSessionCode.Generate(codeLength);
        if (!NetcodeSessionCode.TryGetPort(code, basePort, portRange, out ushort port, out _))
        {
            return basePort;
        }

        return port;
    }

    private NetcodeLauncher ResolveLauncher()
    {
        NetcodeLauncher launcher = null;
#if UNITY_2023_1_OR_NEWER
        launcher = FindFirstObjectByType<NetcodeLauncher>();
#else
        launcher = FindObjectOfType<NetcodeLauncher>();
#endif
        return launcher;
    }

    private void SetStatus(string message)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = $"Etat: {message}";
    }

    private static void EnsureSaveManager()
    {
        if (SaveSessionManager.Instance != null)
        {
            SaveSessionManager.Instance.SetMenuSceneName(DefaultMenuSceneName);
            return;
        }

        GameObject host = new GameObject("SaveSessionManager");
        SaveSessionManager manager = host.AddComponent<SaveSessionManager>();
        manager.SetMenuSceneName(DefaultMenuSceneName);
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
