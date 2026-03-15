using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
        SoloOptions,
        MultiOptions,
        Options,
        Join,
        LoadMenu
    }

    [Header("Scene")]
    [SerializeField] private CanvasGroup titleCardGroup;
    [SerializeField] private CanvasGroup gameOptionsGroup;
    [SerializeField] private CanvasGroup soloOptionsGroup;
    [SerializeField] private CanvasGroup multiOptionsGroup;
    [SerializeField] private CanvasGroup optionsGroup;
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
    [SerializeField] private Texture2D previewMissingTexture;
    [SerializeField] private TMP_Text previewMissingLabel;
    [SerializeField] private string previewMissingLabelText = "Aucun aperçu";
    [SerializeField] private Color previewMissingLabelColor = new Color(1f, 1f, 1f, 0.7f);
    [SerializeField] private int previewMissingLabelFontSize = 36;

    [Header("Actions")]
    [SerializeField] private TMP_Text statusText;

    [Header("Game Options")]
    [Header("Shared Cursor")]
    [SerializeField] private CursorController sharedCursor;
    [SerializeField] private RectTransform gameOptionsCursorRoot;
    [SerializeField] private RectTransform soloOptionsCursorRoot;
    [SerializeField] private RectTransform multiOptionsCursorRoot;
    [SerializeField] private RectTransform optionsCursorRoot;
    [SerializeField] private RectTransform loadMenuCursorRoot;

    [Header("Confirm Delete")]
    [SerializeField] private GameObject confirmRoot;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private string deleteSaveConfirmFormat = "Supprimer '{0}' ?";
    [SerializeField] private string deleteSessionConfirmFormat = "Supprimer la session '{0}' ?";

    [Header("Confirm Load")]
    [SerializeField] private CanvasGroup loadConfirmGroup;
    [SerializeField] private TMP_Text loadConfirmText;
    [SerializeField] private MenuCursorAction loadConfirmYesAction;
    [SerializeField] private MenuCursorAction loadConfirmNoAction;
    [SerializeField] private RectTransform loadConfirmCursorRoot;
    [SerializeField] private string loadConfirmMessageFormat = "Charger '{0}' ?";

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

    [Header("Join Prompt")]
    [SerializeField] private CanvasGroup joinPanelGroup;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_InputField joinAddressInput;
    [SerializeField] private MenuCursorAction joinConfirmAction;
    [SerializeField] private MenuCursorAction joinCancelAction;
    [SerializeField] private RectTransform joinCursorRoot;
    [SerializeField] private bool autoFocusJoinInput = true;
    [SerializeField, Range(0.1f, 1f)] private float joinConfirmDisabledAlpha = 0.4f;
    [SerializeField] private string joinInvalidMessage = "Code invalide.";
    [SerializeField] private string joinNoSessionMessage = "Aucune session pour ce code.";
    [SerializeField] private float joinTimeoutSeconds = 6f;
    [SerializeField] private TMP_Text joinStatusText;
    [SerializeField] private string joinConnectingMessage = "Connexion...";

    [Header("Virtual Keyboard")]
    [SerializeField] private CanvasGroup virtualKeyboardGroup;
    [SerializeField] private RectTransform virtualKeyboardRoot;
    [SerializeField] private VirtualKeyboardCursorController virtualKeyboardCursor;

    [Header("Loading Screen")]
    [SerializeField] private CanvasGroup loadingGroup;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string loadingMessage = "Chargement...";

    [Header("Netcode")]
    [SerializeField] private string gameplaySceneName = "OutdoorsScene";
    [SerializeField] private ushort basePort = 7000;
    [SerializeField] private ushort portRange = 1000;
    [SerializeField] private string hostLoopbackAddress = "127.0.0.1";
    [SerializeField] private string joinAddress = "127.0.0.1";

    [Header("Entry Colors")]
    [SerializeField] private Color entryColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color entryHoverColor = new Color(0.6f, 0.8f, 1f, 0.18f);
    [SerializeField] private Color entrySelectedColor = new Color(0.6f, 0.8f, 1f, 0.32f);

    private MainMenuSessionEntryUI hoveredSessionEntry;
    private SaveSessionInfo selectedSession;
    private SaveSlotInfo selectedSave;
    private MainMenuSaveEntryUI selectedSaveView;
    private SaveSlotInfo pendingDelete;
    private SaveSessionInfo pendingDeleteSession;
    private SaveSlotInfo hoveredSave;
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
    private readonly Dictionary<Graphic, float> joinConfirmGraphicAlphas = new Dictionary<Graphic, float>();
    private bool warnedMissingNewGameInputText;
    private bool warnedMissingJoinInputText;
    private bool warnedMissingJoinAddressInputText;
    private bool loadConfirmOpen;
    private bool deleteConfirmOpen;
    private SaveSlotInfo pendingLoad;
    private MenuState loadMenuReturnState = MenuState.GameOptions;
    private SaveSessionType currentSessionType = SaveSessionType.Solo;
    private MenuCursorNavigator sharedCursorNavigator;
    private bool cachedSharedCursorAllowInput;
    private bool cachedSharedCursorNavigatorEnabled;
    private bool cachedSharedCursorState;
    private CursorController.LayoutFallback sharedCursorFallbackDefault = CursorController.LayoutFallback.None;
    private bool sharedCursorFallbackInitialized;
    private bool joinInProgress;
    private Coroutine joinTimeoutRoutine;
    private Coroutine joinSceneSyncRoutine;
    private NetcodeSessionEndpoint activeJoinEndpoint;

    private void Awake()
    {
        MainMenuDisplaySettings.ApplySavedModeIfNeeded();
        EnsureSaveManager();
        ResolveOptionalReferences();
        ConfigureNewGameActions();
        ConfigureJoinActions();
        ConfigureLoadConfirmActions();
        InitializeState();
        InitializeOverlays();
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
        LocalInputRouter.ToggleTorch += OnToggleTorchPerformed;
        RegisterJoinTransportFailureCallback(true);
        RefreshSessions();

        if (currentMenu == MenuState.GameOptions || currentMenu == MenuState.SoloOptions || currentMenu == MenuState.MultiOptions || currentMenu == MenuState.Options || currentMenu == MenuState.LoadMenu)
        {
            InputFocusStack.Push(this);
        }
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.Return -= OnReturnPerformed;
        LocalInputRouter.ToggleTorch -= OnToggleTorchPerformed;
        RegisterJoinTransportFailureCallback(false);
        InputFocusStack.Pop(this);
        RegisterTextInput(false);
        RegisterJoinCallbacks(false);
        if (joinTimeoutRoutine != null)
        {
            StopCoroutine(joinTimeoutRoutine);
            joinTimeoutRoutine = null;
        }
        if (joinSceneSyncRoutine != null)
        {
            StopCoroutine(joinSceneSyncRoutine);
            joinSceneSyncRoutine = null;
        }
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

        if (deleteConfirmOpen)
        {
            ConfirmDelete();
            return;
        }

        if (!CanProcessInteract())
        {
            return;
        }

        if (hoveredSessionEntry != null && IsCursorOnSessionsRoot())
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

    private void OnToggleTorchPerformed(InputAction.CallbackContext context)
    {
        if (InputFocusStack.HasAnyFocus() && !HasInputFocus())
        {
            return;
        }

        if (!IsLoadMenuActive())
        {
            return;
        }

        if (newGamePromptOpen || loadConfirmOpen || isLoading || deleteConfirmOpen)
        {
            return;
        }

        if (IsCursorOnSavesRoot())
        {
            RequestDeleteSave();
            return;
        }

        if (IsCursorOnSessionsRoot())
        {
            RequestDeleteSession();
        }
    }

    private bool HasInputFocus()
    {
        return InputFocusStack.HasFocus(this);
    }

    private bool CanProcessInteract()
    {
        if (newGamePromptOpen || loadConfirmOpen || isLoading)
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

    private bool IsLoadMenuActive()
    {
        CanvasGroup loadGroup = ResolveLoadMenuGroup();
        if (loadGroup == null)
        {
            return false;
        }

        return loadGroup.gameObject.activeInHierarchy && loadGroup.interactable;
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

    private void ShowSoloOptionsMenu()
    {
        if (soloOptionsGroup == null)
        {
            SetStatus("Menu solo manquant.");
            return;
        }

        currentSessionType = SaveSessionType.Solo;
        loadMenuReturnState = MenuState.SoloOptions;
        SetMenuState(MenuState.SoloOptions);
    }

    private void ShowMultiOptionsMenu()
    {
        if (multiOptionsGroup == null)
        {
            SetStatus("Menu multijoueur manquant.");
            return;
        }

        currentSessionType = SaveSessionType.Multiplayer;
        loadMenuReturnState = MenuState.MultiOptions;
        SetMenuState(MenuState.MultiOptions);
    }

    private void ShowOptionsMenu()
    {
        if (optionsGroup == null)
        {
            SetStatus("Menu options manquant.");
            return;
        }

        SetMenuState(MenuState.Options);
    }

    private void ShowJoinMenu()
    {
        if (joinPanelGroup == null)
        {
            SetStatus("Menu rejoindre manquant.");
            return;
        }

        currentSessionType = SaveSessionType.Multiplayer;
        if (SaveSessionManager.Instance != null)
        {
            SaveSessionManager.Instance.SetCurrentSessionType(SaveSessionType.Multiplayer);
        }

        SetMenuState(MenuState.Join);
        activeJoinEndpoint = default;

        if (joinPanelGroup != null)
        {
            joinPanelGroup.transform.SetAsLastSibling();
        }

        if (joinStatusText != null)
        {
            joinStatusText.text = string.Empty;
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.interactable = true;
            joinCodeInput.text = string.Empty;
            RegisterJoinCodeListener(true);
            UpdateJoinConfirmState();
            if (autoFocusJoinInput)
            {
                joinCodeInput.Select();
                joinCodeInput.ActivateInputField();
            }
        }

        if (joinAddressInput != null)
        {
            joinAddressInput.interactable = true;
            if (string.IsNullOrWhiteSpace(joinAddressInput.text))
            {
                joinAddressInput.text = string.IsNullOrWhiteSpace(joinAddress) ? hostLoopbackAddress : joinAddress;
            }
        }

        RegisterTextInput(true);
        UpdateCursorTarget();
    }

    private void ShowLoadMenu()
    {
        SetMenuState(MenuState.LoadMenu);
    }

    private void EnsureSessionTypeFromMenu()
    {
        if (currentMenu == MenuState.MultiOptions)
        {
            currentSessionType = SaveSessionType.Multiplayer;
            loadMenuReturnState = MenuState.MultiOptions;
            return;
        }

        if (currentMenu == MenuState.Join)
        {
            currentSessionType = SaveSessionType.Multiplayer;
            loadMenuReturnState = MenuState.MultiOptions;
            return;
        }

        if (currentMenu == MenuState.SoloOptions)
        {
            currentSessionType = SaveSessionType.Solo;
            loadMenuReturnState = MenuState.SoloOptions;
            return;
        }

        currentSessionType = SaveSessionType.Solo;
        loadMenuReturnState = MenuState.GameOptions;
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

        if (soloOptionsGroup != null)
        {
            if (state == MenuState.SoloOptions)
            {
                ShowPanel(soloOptionsGroup);
            }
            else
            {
                HidePanel(soloOptionsGroup);
            }
        }

        if (multiOptionsGroup != null)
        {
            if (state == MenuState.MultiOptions)
            {
                ShowPanel(multiOptionsGroup);
            }
            else
            {
                HidePanel(multiOptionsGroup);
            }
        }

        if (optionsGroup != null)
        {
            if (state == MenuState.Options)
            {
                ShowPanel(optionsGroup);
            }
            else
            {
                HidePanel(optionsGroup);
            }
        }

        if (joinPanelGroup != null)
        {
            if (state == MenuState.Join)
            {
                ShowPanel(joinPanelGroup);
            }
            else
            {
                HidePanel(joinPanelGroup);
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

        if (state == MenuState.GameOptions || state == MenuState.SoloOptions || state == MenuState.MultiOptions || state == MenuState.Options || state == MenuState.Join || state == MenuState.LoadMenu)
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

        if (!sharedCursorFallbackInitialized)
        {
            sharedCursorFallbackDefault = sharedCursor.fallbackLayout;
            sharedCursorFallbackInitialized = true;
        }

        sharedCursor.fallbackLayout = currentMenu == MenuState.Join
            ? CursorController.LayoutFallback.Vertical
            : sharedCursorFallbackDefault;

        if (newGamePromptOpen)
        {
            UpdateCursorTargetForPrompt();
            return;
        }

        if (loadConfirmOpen)
        {
            UpdateCursorTargetForLoadConfirm();
            return;
        }

        RectTransform targetRoot = null;
        if (currentMenu == MenuState.GameOptions)
        {
            targetRoot = ResolveCursorRoot(gameOptionsCursorRoot, gameOptionsGroup);
        }
        else if (currentMenu == MenuState.SoloOptions)
        {
            targetRoot = ResolveCursorRoot(soloOptionsCursorRoot, soloOptionsGroup);
        }
        else if (currentMenu == MenuState.MultiOptions)
        {
            targetRoot = ResolveCursorRoot(multiOptionsCursorRoot, multiOptionsGroup);
        }
        else if (currentMenu == MenuState.Options)
        {
            targetRoot = ResolveCursorRoot(optionsCursorRoot, optionsGroup);
        }
        else if (currentMenu == MenuState.Join)
        {
            targetRoot = ResolveCursorRoot(joinCursorRoot, joinPanelGroup);
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

    private void UpdateCursorTargetForLoadConfirm()
    {
        if (sharedCursor == null)
        {
            return;
        }

        RectTransform targetRoot = ResolveCursorRoot(loadConfirmCursorRoot, loadConfirmGroup);
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
        }

        if (virtualKeyboardRoot != null)
        {
            virtualKeyboardRoot.gameObject.SetActive(true);
        }

        if (virtualKeyboardCursor != null)
        {
            virtualKeyboardCursor.Activate();
        }
    }

    private void HideVirtualKeyboard()
    {
        if (virtualKeyboardCursor != null)
        {
            virtualKeyboardCursor.Deactivate();
        }

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
            if (virtualKeyboardCursor != null)
            {
                if (show)
                {
                    virtualKeyboardCursor.Activate();
                }
                else
                {
                    virtualKeyboardCursor.Deactivate();
                }
            }
            return;
        }

        if (virtualKeyboardRoot != null)
        {
            virtualKeyboardRoot.gameObject.SetActive(show);
            if (virtualKeyboardCursor != null)
            {
                if (show)
                {
                    virtualKeyboardCursor.Activate();
                }
                else
                {
                    virtualKeyboardCursor.Deactivate();
                }
            }
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

        if (deleteConfirmOpen)
        {
            CancelDelete();
            return;
        }

        if (newGamePromptOpen)
        {
            CancelNewGame();
            return;
        }

        if (loadConfirmOpen)
        {
            CancelLoadConfirm();
            return;
        }

        if (confirmRoot != null && confirmRoot.activeSelf)
        {
            CancelDelete();
            return;
        }

        if (currentMenu == MenuState.Join)
        {
            CancelJoin();
            return;
        }

        if (currentMenu == MenuState.LoadMenu)
        {
            if (IsCursorOnSavesRoot())
            {
                FocusSessionsRoot();
                return;
            }

            switch (loadMenuReturnState)
            {
                case MenuState.SoloOptions:
                    ShowSoloOptionsMenu();
                    break;
                case MenuState.MultiOptions:
                    ShowMultiOptionsMenu();
                    break;
                default:
                    ShowGameOptionsMenu();
                    break;
            }
            return;
        }

        if (currentMenu == MenuState.SoloOptions || currentMenu == MenuState.MultiOptions || currentMenu == MenuState.Options)
        {
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

        if (joinPanelGroup != null)
        {
            ApplyFadeImmediate(joinPanelGroup, 0f, false);
        }

        if (loadConfirmGroup != null)
        {
            ApplyFadeImmediate(loadConfirmGroup, 0f, false);
        }

        ApplyVirtualKeyboardImmediate(false);

        if (loadingGroup != null)
        {
            ApplyFadeImmediate(loadingGroup, 0f, false);
        }
    }

    private void ResolveOptionalReferences()
    {
        if (gameOptionsGroup == null)
        {
            gameOptionsGroup = FindCanvasGroup("MainMenu_GameOptions");
        }

        if (soloOptionsGroup == null)
        {
            soloOptionsGroup = FindCanvasGroup("MainMenu_Solo_GameOptions");
            if (soloOptionsGroup == null)
            {
                soloOptionsGroup = FindCanvasGroup("MainMenu_Solo");
            }
        }

        if (multiOptionsGroup == null)
        {
            multiOptionsGroup = FindCanvasGroup("MainMenu_Multi_GameOptions");
            if (multiOptionsGroup == null)
            {
                multiOptionsGroup = FindCanvasGroup("MainMenu_Multi");
            }
        }

        if (optionsGroup == null)
        {
            optionsGroup = FindCanvasGroup("MainMenu_Options");
            if (optionsGroup == null)
            {
                optionsGroup = BuildOptionsPanel();
            }
        }

        if (soloOptionsCursorRoot == null && soloOptionsGroup != null)
        {
            soloOptionsCursorRoot = soloOptionsGroup.transform as RectTransform;
        }

        if (multiOptionsCursorRoot == null && multiOptionsGroup != null)
        {
            multiOptionsCursorRoot = multiOptionsGroup.transform as RectTransform;
        }

        if (optionsGroup != null)
        {
            RectTransform detectedOptionsCursorRoot = FindOptionsCursorRoot(optionsGroup.transform);
            if (detectedOptionsCursorRoot != null)
            {
                optionsCursorRoot = detectedOptionsCursorRoot;
            }
            else if (optionsCursorRoot == null)
            {
                optionsCursorRoot = optionsGroup.transform as RectTransform;
            }
        }

        if (multiOptionsGroup != null)
        {
            MenuCursorAction joinAction = FindMenuCursorActionByName(multiOptionsGroup.transform, "Join");
            if (joinAction == null)
            {
                joinAction = FindMenuCursorActionByName(multiOptionsGroup.transform, "Multi_Join");
            }
            if (joinAction != null)
            {
                joinAction.Configure(this, MenuCursorAction.MenuAction.Join);
            }
        }

        if (gameOptionsGroup != null)
        {
            MenuCursorAction optionsAction = FindMenuCursorActionByName(gameOptionsGroup.transform, "Options");
            if (optionsAction == null)
            {
                Transform optionsButton = FindInHierarchy(gameOptionsGroup.transform, "Options");
                if (optionsButton != null)
                {
                    optionsAction = optionsButton.GetComponent<MenuCursorAction>();
                    if (optionsAction == null)
                    {
                        optionsAction = optionsButton.gameObject.AddComponent<MenuCursorAction>();
                    }
                }
            }

            if (optionsAction != null)
            {
                optionsAction.Configure(this, MenuCursorAction.MenuAction.Options);
            }
        }

        if (loadMenuGroup == null)
        {
            loadMenuGroup = FindCanvasGroup("MainMenu_Load");
        }

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

        if (joinPanelGroup == null)
        {
            joinPanelGroup = FindCanvasGroup("MainMenu_Join");
            if (joinPanelGroup == null)
            {
                joinPanelGroup = FindCanvasGroup("MainMenu_Multi_Join");
            }
        }

        if (joinPanelGroup != null)
        {
            if (joinCodeInput == null)
            {
                joinCodeInput = FindInputByExactNames(joinPanelGroup.transform,
                    "MainMenu_Multi_Join_SessionField_Input",
                    "MainMenu_Join_SessionField_Input",
                    "JoinCodeInput",
                    "Join_Code_Input",
                    "JoinCode",
                    "SessionCodeInput");
            }

            if (joinAddressInput == null)
            {
                joinAddressInput = FindInputByExactNames(joinPanelGroup.transform,
                    "MainMenu_Multi_Join_AddressField_Input",
                    "MainMenu_Join_AddressField_Input",
                    "JoinAddressInput",
                    "Join_Address_Input",
                    "JoinAddress",
                    "AddressInput",
                    "JoinIPInput",
                    "IPInput");
            }

            TMP_InputField[] joinInputs = joinPanelGroup.GetComponentsInChildren<TMP_InputField>(true);
            if (joinCodeInput == null)
            {
                joinCodeInput = FindInputByName(joinInputs, "Code", "Session");
            }

            if (joinAddressInput == null)
            {
                joinAddressInput = FindInputByName(joinInputs, "Address", "Adresse", "IP");
            }

            if (joinCodeInput == null && joinInputs.Length > 0)
            {
                joinCodeInput = joinInputs[0];
            }

            if (joinAddressInput == null && joinInputs.Length > 0)
            {
                for (int i = 0; i < joinInputs.Length; i++)
                {
                    TMP_InputField input = joinInputs[i];
                    if (input != null && input != joinCodeInput)
                    {
                        joinAddressInput = input;
                        break;
                    }
                }
            }
        }

        if (joinConfirmAction == null && joinPanelGroup != null)
        {
            joinConfirmAction = FindMenuCursorActionByName(joinPanelGroup.transform, "Confirm");
            if (joinConfirmAction == null)
            {
                joinConfirmAction = FindMenuCursorActionByName(joinPanelGroup.transform, "Join");
            }
        }

        if (joinConfirmAction == null && joinPanelGroup != null)
        {
            Transform joinButton = FindInHierarchy(joinPanelGroup.transform, "Join");
            if (joinButton != null)
            {
                MenuCursorAction action = joinButton.GetComponent<MenuCursorAction>();
                if (action == null)
                {
                    action = joinButton.gameObject.AddComponent<MenuCursorAction>();
                }
                joinConfirmAction = action;
            }
        }

        if (joinCancelAction == null && joinPanelGroup != null)
        {
            joinCancelAction = FindMenuCursorActionByName(joinPanelGroup.transform, "Cancel");
            if (joinCancelAction == null)
            {
                joinCancelAction = FindMenuCursorActionByName(joinPanelGroup.transform, "Back");
            }
        }

        if (joinPanelGroup != null && joinCursorRoot == null)
        {
            Transform confirm = FindInHierarchy(joinPanelGroup.transform, "Confirm");
            Transform cancel = FindInHierarchy(joinPanelGroup.transform, "Cancel");
            if (confirm != null && cancel != null && confirm.parent == cancel.parent)
            {
                joinCursorRoot = confirm.parent as RectTransform;
            }
            else
            {
                Transform background = FindInHierarchy(joinPanelGroup.transform, "Background");
                if (background != null)
                {
                    joinCursorRoot = background as RectTransform;
                }
                else
                {
                    joinCursorRoot = joinPanelGroup.transform as RectTransform;
                }
            }
        }

        if (joinStatusText == null && joinPanelGroup != null)
        {
            joinStatusText = FindTextByExactNames(joinPanelGroup.transform,
                "JoinStatusText",
                "Join_Status",
                "MainMenu_Join_Status",
                "MainMenu_Multi_Join_Status",
                "JoinStatus",
                "JoinMessage",
                "JoinMessageText",
                "Join_Status_Text");

            if (joinStatusText == null)
            {
                joinStatusText = FindTextByName(joinPanelGroup.transform, "Status", "Etat", "Message");
            }
        }

        if (loadConfirmGroup == null)
        {
            loadConfirmGroup = FindCanvasGroup("MainMenu_LoadConfirm");
            if (loadConfirmGroup == null)
            {
                loadConfirmGroup = FindCanvasGroup("LoadConfirm");
            }
        }

        if (loadConfirmText == null && loadConfirmGroup != null)
        {
            loadConfirmText = loadConfirmGroup.GetComponentInChildren<TMP_Text>(true);
        }

        if (loadConfirmYesAction == null && loadConfirmGroup != null)
        {
            loadConfirmYesAction = FindMenuCursorActionByName(loadConfirmGroup.transform, "Confirm");
        }

        if (loadConfirmNoAction == null && loadConfirmGroup != null)
        {
            loadConfirmNoAction = FindMenuCursorActionByName(loadConfirmGroup.transform, "Cancel");
        }

        if (loadConfirmCursorRoot == null && loadConfirmGroup != null)
        {
            Transform confirm = FindInHierarchy(loadConfirmGroup.transform, "Confirm");
            Transform cancel = FindInHierarchy(loadConfirmGroup.transform, "Cancel");
            if (confirm != null && cancel != null && confirm.parent == cancel.parent)
            {
                loadConfirmCursorRoot = confirm.parent as RectTransform;
            }
            else
            {
                loadConfirmCursorRoot = loadConfirmGroup.transform as RectTransform;
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

        if (confirmRoot == null)
        {
            Transform confirm = FindInHierarchy(transform, "MainMenu_DeleteConfirm");
            if (confirm == null)
            {
                confirm = FindInHierarchy(transform, "DeleteConfirm");
            }
            if (confirm == null)
            {
                confirm = FindInHierarchy(transform, "ConfirmDelete");
            }
            if (confirm != null)
            {
                confirmRoot = confirm.gameObject;
            }
        }

        if (confirmText == null && confirmRoot != null)
        {
            confirmText = confirmRoot.GetComponentInChildren<TMP_Text>(true);
        }

        if (virtualKeyboardCursor == null)
        {
            Transform vkRoot = virtualKeyboardRoot != null
                ? virtualKeyboardRoot
                : virtualKeyboardGroup != null
                    ? virtualKeyboardGroup.transform
                    : null;

            if (vkRoot != null)
            {
                virtualKeyboardCursor = vkRoot.GetComponentInChildren<VirtualKeyboardCursorController>(true);
            }
        }

        if (sharedCursorNavigator == null && sharedCursor != null)
        {
            sharedCursorNavigator = sharedCursor.GetComponent<MenuCursorNavigator>();
            if (sharedCursorNavigator == null)
            {
                sharedCursorNavigator = sharedCursor.GetComponentInParent<MenuCursorNavigator>();
            }
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

    private CanvasGroup BuildOptionsPanel()
    {
        if (gameOptionsGroup == null)
        {
            return null;
        }

        Transform template = FindInHierarchy(gameOptionsGroup.transform, "Options");
        if (template == null)
        {
            return null;
        }

        Transform parent = gameOptionsGroup.transform.parent;
        if (parent == null)
        {
            return null;
        }

        GameObject root = new GameObject("MainMenu_Options",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(VerticalLayoutGroup),
            typeof(CanvasGroup));
        root.layer = gameOptionsGroup.gameObject.layer;
        root.transform.SetParent(parent, false);
        root.transform.SetSiblingIndex(gameOptionsGroup.transform.GetSiblingIndex() + 1);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        CopyRectTransform(gameOptionsGroup.transform as RectTransform, rootRect);

        VerticalLayoutGroup sourceLayout = gameOptionsGroup.GetComponent<VerticalLayoutGroup>();
        VerticalLayoutGroup targetLayout = root.GetComponent<VerticalLayoutGroup>();
        CopyVerticalLayoutGroup(sourceLayout, targetLayout);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        CreateOptionsHeaderRow(template, root.transform);
        Transform optionsRoot = CreateOptionsItemsRoot(root.transform);
        CreateDisplayModeRow(template, optionsRoot, "Fenetre", MainMenuDisplayModeAction.DisplayModeOption.Windowed);
        CreateDisplayModeRow(template, optionsRoot, "PleinEcran", MainMenuDisplayModeAction.DisplayModeOption.Fullscreen);
        CreateOptionsBackRow(template, optionsRoot);

        return group;
    }

    private Transform CreateOptionsItemsRoot(Transform parent)
    {
        GameObject root = new GameObject("Options_Root",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup));
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);

        RectTransform rectTransform = root.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = new Vector2(0f, -114.63916f);
        rectTransform.sizeDelta = new Vector2(0f, -229.2784f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset();
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childScaleWidth = false;
        layout.childScaleHeight = false;
        layout.reverseArrangement = false;

        return root.transform;
    }

    private Transform CreateOptionsHeaderRow(Transform template, Transform parent)
    {
        GameObject row = Instantiate(template.gameObject, parent, false);
        row.name = "Affichage";
        DisableNestedMenuCursorHandlers(row.transform);

        Image image = row.GetComponent<Image>();
        if (image != null)
        {
            Color color = image.color;
            color.a = 0.65f;
            image.color = color;
            image.raycastTarget = false;
        }

        TMP_Text text = row.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = "Affichage";
            text.raycastTarget = false;
        }

        return row.transform;
    }

    private Transform CreateDisplayModeRow(Transform template, Transform parent, string rowName, MainMenuDisplayModeAction.DisplayModeOption mode)
    {
        GameObject row = Instantiate(template.gameObject, parent, false);
        row.name = rowName;
        DisableNestedMenuCursorHandlers(row.transform);

        TMP_Text text = row.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.name = $"{rowName}_Text";
            text.raycastTarget = false;
        }

        MainMenuDisplayModeAction action = row.GetComponent<MainMenuDisplayModeAction>();
        if (action == null)
        {
            action = row.AddComponent<MainMenuDisplayModeAction>();
        }

        action.Configure(mode, text);
        return row.transform;
    }

    private Transform CreateOptionsBackRow(Transform template, Transform parent)
    {
        GameObject row = Instantiate(template.gameObject, parent, false);
        row.name = "Back";
        DisableNestedMenuCursorHandlers(row.transform);

        TMP_Text text = row.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.name = "Back_Text";
            text.text = "Retour";
            text.raycastTarget = false;
        }

        MenuCursorAction action = row.GetComponent<MenuCursorAction>();
        if (action == null)
        {
            action = row.AddComponent<MenuCursorAction>();
        }

        action.Configure(this, MenuCursorAction.MenuAction.BackToGameOptions);
        return row.transform;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
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
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    private static void CopyVerticalLayoutGroup(VerticalLayoutGroup source, VerticalLayoutGroup target)
    {
        if (target == null)
        {
            return;
        }

        if (source == null)
        {
            return;
        }

        target.padding = source.padding;
        target.spacing = source.spacing;
        target.childAlignment = source.childAlignment;
        target.childForceExpandWidth = source.childForceExpandWidth;
        target.childForceExpandHeight = source.childForceExpandHeight;
        target.childControlWidth = source.childControlWidth;
        target.childControlHeight = source.childControlHeight;
        target.childScaleWidth = source.childScaleWidth;
        target.childScaleHeight = source.childScaleHeight;
        target.reverseArrangement = source.reverseArrangement;
    }

    private static void DisableNestedMenuCursorHandlers(Transform root)
    {
        if (root == null)
        {
            return;
        }

        MenuCursorAction[] actions = root.GetComponentsInChildren<MenuCursorAction>(true);
        for (int i = 0; i < actions.Length; i++)
        {
            if (actions[i] != null && actions[i].transform != root)
            {
                actions[i].enabled = false;
            }
        }

        MainMenuDisplayModeAction[] displayActions = root.GetComponentsInChildren<MainMenuDisplayModeAction>(true);
        for (int i = 0; i < displayActions.Length; i++)
        {
            if (displayActions[i] != null && displayActions[i].transform != root)
            {
                displayActions[i].enabled = false;
            }
        }
    }

    private static RectTransform FindOptionsCursorRoot(Transform optionsRoot)
    {
        if (optionsRoot == null)
        {
            return null;
        }

        Transform explicitRoot = FindInHierarchy(optionsRoot, "Options_Root");
        if (explicitRoot is RectTransform explicitRect)
        {
            return explicitRect;
        }

        RectTransform firstItem = FindFirstCursorItem(optionsRoot);
        if (firstItem != null && firstItem.parent is RectTransform parent)
        {
            return parent;
        }

        return null;
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

    private static TMP_InputField FindInputByName(TMP_InputField[] inputs, params string[] tokens)
    {
        if (inputs == null || tokens == null || tokens.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < inputs.Length; i++)
        {
            TMP_InputField input = inputs[i];
            if (input == null)
            {
                continue;
            }

            string name = input.name;
            for (int t = 0; t < tokens.Length; t++)
            {
                string token = tokens[t];
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return input;
                }
            }
        }

        return null;
    }

    private static TMP_InputField FindInputByExactNames(Transform root, params string[] names)
    {
        if (root == null || names == null || names.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            Transform found = FindInHierarchy(root, name);
            if (found == null)
            {
                continue;
            }

            TMP_InputField input = found.GetComponent<TMP_InputField>();
            if (input == null)
            {
                input = found.GetComponentInChildren<TMP_InputField>(true);
            }

            if (input != null)
            {
                return input;
            }
        }

        return null;
    }

    private static TMP_Text FindTextByName(Transform root, params string[] tokens)
    {
        if (root == null || tokens == null || tokens.Length == 0)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (text.GetComponentInParent<TMP_InputField>() != null)
            {
                continue;
            }

            string name = text.name;
            for (int t = 0; t < tokens.Length; t++)
            {
                string token = tokens[t];
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static TMP_Text FindTextByExactNames(Transform root, params string[] names)
    {
        if (root == null || names == null || names.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            Transform found = FindInHierarchy(root, name);
            if (found == null)
            {
                continue;
            }

            TMP_Text text = found.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = found.GetComponentInChildren<TMP_Text>(true);
            }

            if (text != null)
            {
                return text;
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
        IReadOnlyList<SaveSessionInfo> sessions = SaveSessionManager.Instance.GetSessionsByType(currentSessionType);
        if (sessions == null || sessions.Count == 0)
        {
            if (emptySessionsPlaceholder != null)
            {
                emptySessionsPlaceholder.SetActive(true);
            }
            selectedSession = null;
            ClearSavesUI();
            ClearSessionsUI();
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
        selectedSession = null;
        selectedSave = null;
        selectedSaveView = null;
        pendingDelete = null;
        pendingDeleteSession = null;
        hoveredSave = null;
        pendingLoad = null;
        hoveredSessionEntry = null;

        CloseDeleteConfirm();

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
        if (loadConfirmOpen)
        {
            return;
        }
        if (entry != null && entry.Session != null)
        {
            RebuildSavesList(entry.Session, false);
        }
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
        RebuildSavesList(session, true);
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

    private bool IsCursorOnSessionsRoot()
    {
        if (sessionsRoot == null)
        {
            return false;
        }

        if (currentCursorRoot == sessionsRoot)
        {
            return true;
        }

        RectTransform current = sharedCursor != null ? sharedCursor.itemsParent : null;
        if (current == null)
        {
            return false;
        }

        return current == sessionsRoot;
    }

    private void RebuildSavesList(SaveSessionInfo session, bool autoSelectSave)
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
        if (autoSelectSave && entryToSelect != null && saveToSelect != null)
        {
            OnSaveSelected(saveToSelect, entryToSelect, false);
        }
    }

    private void ClearSavesUI()
    {
        selectedSave = null;
        selectedSaveView = null;
        pendingDelete = null;
        hoveredSave = null;

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
        hoveredSave = save;
        ShowSaveDetails(save);
    }

    internal void OnSaveSelected(SaveSlotInfo save, MainMenuSaveEntryUI view, bool requestLoad)
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

        if (requestLoad)
        {
            RequestLoadConfirm(save);
        }
    }

    private void ShowSaveDetails(SaveSlotInfo save)
    {
        if (save == null)
        {
            return;
        }

        EnsurePreviewReferences();

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
        EnsurePreviewReferences();
        ClearPreviewTexture();

        if (previewImage == null || save == null)
        {
            return;
        }

        string path = GetScreenshotPath(save);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ApplyMissingPreview();
            return;
        }

        try
        {
            byte[] data = File.ReadAllBytes(path);
            if (data == null || data.Length == 0)
            {
                ApplyMissingPreview();
                return;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(data))
            {
                Destroy(texture);
                ApplyMissingPreview();
                return;
            }

            previewTexture = texture;
            previewImage.texture = previewTexture;
            previewImage.enabled = true;
            SetPreviewPlaceholderVisible(false);

            if (previewAspect != null && previewTexture.height > 0)
            {
                previewAspect.aspectRatio = (float)previewTexture.width / previewTexture.height;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MainMenuController: echec chargement screenshot {path}. {ex.Message}");
            ApplyMissingPreview();
        }
    }

    private void EnsurePreviewReferences()
    {
        if (previewImage == null)
        {
            Transform root = ResolveLoadMenuGroup() != null ? ResolveLoadMenuGroup().transform : transform;
            if (root != null)
            {
                Transform found = FindInHierarchy(root, "ScreenView");
                if (found != null)
                {
                    previewImage = found.GetComponent<RawImage>();
                }
            }
        }

        if (previewAspect == null && previewImage != null)
        {
            previewAspect = previewImage.GetComponent<AspectRatioFitter>();
        }

        if (previewMissingLabel == null && previewImage != null)
        {
            Transform found = previewImage.transform.Find("PreviewPlaceholder");
            if (found != null)
            {
                previewMissingLabel = found.GetComponent<TMP_Text>();
            }
        }

        if (previewMissingLabel == null && previewImage != null)
        {
            GameObject labelObject = new GameObject("PreviewPlaceholder", typeof(RectTransform));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(previewImage.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            previewMissingLabel = labelObject.AddComponent<TextMeshProUGUI>();
            previewMissingLabel.alignment = TextAlignmentOptions.Center;
            previewMissingLabel.fontSize = previewMissingLabelFontSize;
            previewMissingLabel.color = previewMissingLabelColor;
            previewMissingLabel.text = previewMissingLabelText;
            previewMissingLabel.raycastTarget = false;
            if (previewMissingLabel.font == null && TMP_Settings.defaultFontAsset != null)
            {
                previewMissingLabel.font = TMP_Settings.defaultFontAsset;
            }
            previewMissingLabel.gameObject.SetActive(false);
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

    private void ApplyMissingPreview()
    {
        if (previewImage == null)
        {
            return;
        }

        if (previewMissingTexture == null)
        {
            previewMissingTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            previewMissingTexture.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.08f, 1f));
            previewMissingTexture.Apply();
        }

        previewImage.texture = previewMissingTexture;
        previewImage.enabled = true;
        if (previewAspect != null)
        {
            previewAspect.aspectRatio = 16f / 9f;
        }

        SetPreviewPlaceholderVisible(true);
    }

    private void SetPreviewPlaceholderVisible(bool visible)
    {
        if (previewMissingLabel == null)
        {
            return;
        }

        previewMissingLabel.text = previewMissingLabelText;
        previewMissingLabel.color = previewMissingLabelColor;
        previewMissingLabel.fontSize = previewMissingLabelFontSize;
        if (previewMissingLabel.font == null && TMP_Settings.defaultFontAsset != null)
        {
            previewMissingLabel.font = TMP_Settings.defaultFontAsset;
        }
        previewMissingLabel.gameObject.SetActive(visible);
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

    private void HideJoinMenu()
    {
        if (currentMenu != MenuState.Join)
        {
            return;
        }

        RegisterJoinCodeListener(false);
        RegisterTextInput(false);
        if (joinStatusText != null)
        {
            joinStatusText.text = string.Empty;
        }
        activeJoinEndpoint = default;
        ShowMultiOptionsMenu();
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
        if (!isLoading)
        {
            ShowLoadingScreen(loadingMessage);
        }
        HideNewGamePrompt();
        CreateNewGameAndStart(sessionName);
    }

    private void CancelNewGame()
    {
        HideNewGamePrompt();
    }

    private void ConfirmJoin()
    {
        if (!IsJoinCodeValid())
        {
            SetStatus(joinInvalidMessage);
            SetJoinStatus(joinInvalidMessage);
            if (joinCodeInput != null)
            {
                joinCodeInput.ActivateInputField();
            }
            return;
        }

        if (!TryResolveJoinEndpoint(out NetcodeSessionEndpoint endpoint))
        {
            SetStatus(joinInvalidMessage);
            SetJoinStatus(joinInvalidMessage);
            return;
        }

        StartJoinFlow(endpoint);
    }

    private void CancelJoin()
    {
        if (joinInProgress)
        {
            return;
        }

        HideJoinMenu();
    }

    private void PasteJoinCodeFromClipboard()
    {
        if (currentMenu != MenuState.Join)
        {
            return;
        }

        if (joinCodeInput == null)
        {
            return;
        }

        string clipboard = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clipboard))
        {
            return;
        }

        string normalized = NetcodeSessionCode.Normalize(clipboard);
        if (!EnsureJoinInputFieldReady())
        {
            return;
        }

        joinCodeInput.SetTextWithoutNotify(normalized);
        joinCodeInput.Select();
        joinCodeInput.ActivateInputField();
        int caret = normalized.Length;
        joinCodeInput.caretPosition = caret;
        joinCodeInput.selectionAnchorPosition = caret;
        joinCodeInput.selectionFocusPosition = caret;
        joinCodeInput.ForceLabelUpdate();
        UpdateJoinConfirmState();
    }

    private void PasteJoinAddressFromClipboard()
    {
        if (currentMenu != MenuState.Join)
        {
            return;
        }

        if (joinAddressInput == null)
        {
            return;
        }

        string clipboard = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clipboard))
        {
            return;
        }

        string trimmed = clipboard.Trim();
        if (!EnsureJoinAddressInputFieldReady())
        {
            return;
        }

        joinAddressInput.SetTextWithoutNotify(trimmed);
        joinAddressInput.Select();
        joinAddressInput.ActivateInputField();
        int caret = trimmed.Length;
        joinAddressInput.caretPosition = caret;
        joinAddressInput.selectionAnchorPosition = caret;
        joinAddressInput.selectionFocusPosition = caret;
        joinAddressInput.ForceLabelUpdate();
    }

    private void CreateNewGameAndStart(string sessionName)
    {
        if (!isLoading)
        {
            ShowLoadingScreen(loadingMessage);
        }

        if (SaveSessionManager.Instance == null)
        {
            HideLoadingScreen();
            return;
        }

        SaveSessionInfo session = SaveSessionManager.Instance.CreateSession(sessionName, currentSessionType);
        string initialSaveName = string.IsNullOrWhiteSpace(defaultNewGameSaveName) ? "Depart" : defaultNewGameSaveName;
        SaveSlotInfo save = SaveSessionManager.Instance.CreateSave(session.sessionId, initialSaveName);
        if (save == null)
        {
            HideLoadingScreen();
            SetStatus("Impossible de creer la sauvegarde.");
            return;
        }

        SaveSessionManager.Instance.SetActiveSave(session.sessionId, save.saveId);
        StartGameFlow();
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

    private void RegisterJoinCodeListener(bool enabled)
    {
        if (joinCodeInput == null)
        {
            return;
        }

        joinCodeInput.onValueChanged.RemoveListener(OnJoinCodeChanged);
        if (enabled)
        {
            joinCodeInput.onValueChanged.AddListener(OnJoinCodeChanged);
        }
    }

    private void RegisterTextInput(bool enabled)
    {
        if (!enabled || !useManualTextInputFallback)
        {
            return;
        }

        // TMP_InputField gere deja le clavier physique. Rejouer Keyboard.onTextInput
        // ici dupliquait chaque caractere. Le clavier virtuel continue de passer
        // explicitement par UI_VirtualKey -> OnTextInput.
    }

    private void OnTextInput(char character)
    {
        TMP_InputField field = null;
        bool isNewGame = false;
        bool isJoinCode = false;
        if (newGamePromptOpen)
        {
            field = newGameNameInput;
            isNewGame = true;
        }
        else if (currentMenu == MenuState.Join)
        {
            if (joinAddressInput != null && joinAddressInput.isFocused)
            {
                field = joinAddressInput;
            }
            else
            {
                field = joinCodeInput;
                isJoinCode = true;
            }
        }

        if (field == null || !field.interactable)
        {
            return;
        }

        if (isNewGame)
        {
            if (!EnsureNewGameInputFieldReady())
            {
                return;
            }
        }
        else
        {
            if (isJoinCode)
            {
                if (!EnsureJoinInputFieldReady())
                {
                    return;
                }
            }
            else
            {
                if (!EnsureJoinAddressInputFieldReady())
                {
                    return;
                }
            }
        }

        if (!field.isFocused)
        {
            if (isNewGame ? autoFocusNewGameInput : autoFocusJoinInput)
            {
                field.Select();
                field.ActivateInputField();
            }
            else
            {
                return;
            }
        }

        string text = field.text ?? string.Empty;
        int anchor = field.selectionAnchorPosition;
        int focus = field.selectionFocusPosition;
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
            if (field.characterLimit > 0 && text.Length >= field.characterLimit)
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

        if (!isNewGame && isJoinCode)
        {
            text = NetcodeSessionCode.Normalize(text);
            caret = Mathf.Min(caret, text.Length);
        }

        field.SetTextWithoutNotify(text);
        field.caretPosition = caret;
        field.selectionAnchorPosition = caret;
        field.selectionFocusPosition = caret;
        field.ForceLabelUpdate();

        if (isNewGame)
        {
            UpdateNewGameConfirmState();
        }
        else if (isJoinCode)
        {
            UpdateJoinConfirmState();
        }
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

    private bool EnsureJoinInputFieldReady()
    {
        if (joinCodeInput == null)
        {
            return false;
        }

        if (joinCodeInput.textComponent == null)
        {
            TMP_Text text = FindPreferredInputText(joinCodeInput);
            if (text != null)
            {
                joinCodeInput.textComponent = text;
            }
        }

        if (joinCodeInput.textComponent == null)
        {
            if (!warnedMissingJoinInputText)
            {
                warnedMissingJoinInputText = true;
                Debug.LogWarning("MainMenuController: TMP_InputField textComponent manquant sur le champ de code multijoueur. Assigne un TextMeshProUGUI dans l'inspecteur.");
            }
            return false;
        }

        return true;
    }

    private bool EnsureJoinAddressInputFieldReady()
    {
        if (joinAddressInput == null)
        {
            return false;
        }

        if (joinAddressInput.textComponent == null)
        {
            TMP_Text text = FindPreferredInputText(joinAddressInput);
            if (text != null)
            {
                joinAddressInput.textComponent = text;
            }
        }

        if (joinAddressInput.textComponent == null)
        {
            if (!warnedMissingJoinAddressInputText)
            {
                warnedMissingJoinAddressInputText = true;
                Debug.LogWarning("MainMenuController: TMP_InputField textComponent manquant sur le champ d'adresse multijoueur. Assigne un TextMeshProUGUI dans l'inspecteur.");
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

    private void OnJoinCodeChanged(string value)
    {
        if (joinCodeInput == null)
        {
            return;
        }

        string normalized = NetcodeSessionCode.Normalize(value);
        if (!string.Equals(joinCodeInput.text, normalized, StringComparison.Ordinal))
        {
            joinCodeInput.SetTextWithoutNotify(normalized);
        }

        UpdateJoinConfirmState();
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

    private bool IsJoinCodeValid()
    {
        if (joinCodeInput == null)
        {
            return false;
        }

        string text = NetcodeSessionCode.Normalize(joinCodeInput.text);
        return !string.IsNullOrWhiteSpace(text);
    }

    private void UpdateJoinConfirmState()
    {
        SetJoinConfirmEnabled(IsJoinCodeValid());
    }

    private void SetJoinConfirmEnabled(bool enabled)
    {
        if (joinConfirmAction == null)
        {
            return;
        }

        joinConfirmAction.enabled = enabled;

        CanvasGroup group = joinConfirmAction.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = enabled ? 1f : Mathf.Clamp01(joinConfirmDisabledAlpha);
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }
        else
        {
            ApplyJoinConfirmGraphicsAlpha(enabled);
        }

        if (currentMenu == MenuState.Join && sharedCursor != null)
        {
            sharedCursor.Refresh();
            StartCursorSnap();
        }
    }

    private void ApplyJoinConfirmGraphicsAlpha(bool enabled)
    {
        if (joinConfirmAction == null)
        {
            return;
        }

        Graphic[] graphics = joinConfirmAction.GetComponentsInChildren<Graphic>(true);
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

            if (!joinConfirmGraphicAlphas.ContainsKey(graphic))
            {
                joinConfirmGraphicAlphas[graphic] = graphic.color.a;
            }

            float baseAlpha = joinConfirmGraphicAlphas[graphic];
            Color color = graphic.color;
            color.a = enabled ? baseAlpha : baseAlpha * Mathf.Clamp01(joinConfirmDisabledAlpha);
            graphic.color = color;
        }
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

    private void ConfigureJoinActions()
    {
        if (joinConfirmAction != null)
        {
            joinConfirmAction.Configure(this, MenuCursorAction.MenuAction.ConfirmJoin);
        }

        if (joinCancelAction != null)
        {
            joinCancelAction.Configure(this, MenuCursorAction.MenuAction.CancelJoin);
        }
    }

    private void ConfigureLoadConfirmActions()
    {
        if (loadConfirmYesAction != null)
        {
            loadConfirmYesAction.Configure(this, MenuCursorAction.MenuAction.ConfirmLoad);
        }

        if (loadConfirmNoAction != null)
        {
            loadConfirmNoAction.Configure(this, MenuCursorAction.MenuAction.CancelLoad);
        }
    }

    private void OnLoadMenuRequested()
    {
        ShowLoadMenu();
        RefreshSessions();
    }

    private void OnMultiplayerRequested()
    {
        ShowMultiOptionsMenu();
    }

    private void OnOptionsRequested()
    {
        ShowOptionsMenu();
    }

    public void UI_NewGame()
    {
        EnsureSessionTypeFromMenu();
        OnNewGame();
    }

    public void UI_Solo()
    {
        ShowSoloOptionsMenu();
    }

    public void UI_ConfirmNewGame()
    {
        ConfirmNewGame();
    }

    public void UI_CancelNewGame()
    {
        CancelNewGame();
    }

    public void UI_ConfirmLoad()
    {
        ConfirmLoad();
    }

    public void UI_CancelLoad()
    {
        CancelLoadConfirm();
    }

    public void UI_VirtualKey(char character)
    {
        OnTextInput(character);
    }

    public void UI_VirtualValidate()
    {
        if (newGamePromptOpen)
        {
            ConfirmNewGame();
            return;
        }

        if (currentMenu == MenuState.Join)
        {
            ConfirmJoin();
        }
    }

    public void UI_ShowLoadMenu()
    {
        EnsureSessionTypeFromMenu();
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

    public void UI_Join()
    {
        ShowJoinMenu();
    }

    public void UI_ConfirmJoin()
    {
        ConfirmJoin();
    }

    public void UI_CancelJoin()
    {
        CancelJoin();
    }

    public void UI_PasteJoinCode()
    {
        PasteJoinCodeFromClipboard();
    }

    public void UI_PasteJoinAddress()
    {
        PasteJoinAddressFromClipboard();
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
        if (!isLoading)
        {
            ShowLoadingScreen(loadingMessage);
        }

        if (SaveSessionManager.Instance == null)
        {
            HideLoadingScreen();
            return;
        }

        if (selectedSave == null)
        {
            HideLoadingScreen();
            SetStatus("Selectionne une sauvegarde.");
            return;
        }

        currentSessionType = selectedSave.sessionType;
        SaveSessionManager.Instance.SetActiveSave(selectedSave.sessionId, selectedSave.saveId);
        StartGameFlow();
    }

    private void RequestLoadConfirm(SaveSlotInfo save)
    {
        if (save == null)
        {
            return;
        }

        if (loadConfirmOpen)
        {
            pendingLoad = save;
            if (loadConfirmText != null)
            {
                string label = string.IsNullOrWhiteSpace(save.saveName) ? "sauvegarde" : save.saveName;
                loadConfirmText.text = string.Format(loadConfirmMessageFormat, label);
            }
            return;
        }

        pendingLoad = save;

        if (loadConfirmText != null)
        {
            string label = string.IsNullOrWhiteSpace(save.saveName) ? "sauvegarde" : save.saveName;
            loadConfirmText.text = string.Format(loadConfirmMessageFormat, label);
        }

        if (loadConfirmGroup == null)
        {
            OnLoadSelected();
            return;
        }

        loadConfirmOpen = true;
        StartFade(loadConfirmGroup, 1f, true);
        SetActiveMenuInteractable(false);
        UpdateCursorTargetForLoadConfirm();
    }

    private void ConfirmLoad()
    {
        if (pendingLoad == null)
        {
            CancelLoadConfirm();
            return;
        }

        if (!isLoading)
        {
            ShowLoadingScreen(loadingMessage);
        }
        selectedSave = pendingLoad;
        pendingLoad = null;
        loadConfirmOpen = false;

        if (loadConfirmGroup != null)
        {
            StartFade(loadConfirmGroup, 0f, false);
        }

        OnLoadSelected();
    }

    private void CancelLoadConfirm()
    {
        pendingLoad = null;
        loadConfirmOpen = false;
        if (loadConfirmGroup != null)
        {
            StartFade(loadConfirmGroup, 0f, false);
        }
        SetActiveMenuInteractable(true);
        FocusSavesRoot();
    }

    private void OnDeleteRequested()
    {
        if (!IsLoadMenuActive())
        {
            return;
        }

        if (IsCursorOnSavesRoot())
        {
            RequestDeleteSave();
            return;
        }

        if (IsCursorOnSessionsRoot())
        {
            RequestDeleteSession();
        }
    }

    private void RequestDeleteSave()
    {
        SaveSlotInfo save = hoveredSave ?? selectedSave;
        if (save == null)
        {
            SetStatus("Selectionne une sauvegarde.");
            return;
        }

        pendingDelete = save;
        pendingDeleteSession = null;
        string label = string.IsNullOrWhiteSpace(save.saveName) ? "sauvegarde" : save.saveName;
        OpenDeleteConfirm(string.Format(deleteSaveConfirmFormat, label));
    }

    private void RequestDeleteSession()
    {
        SaveSessionInfo session = hoveredSessionEntry != null ? hoveredSessionEntry.Session : null;
        if (session == null)
        {
            session = selectedSession;
        }

        if (session == null)
        {
            SetStatus("Selectionne une session.");
            return;
        }

        pendingDeleteSession = session;
        pendingDelete = null;
        string label = string.IsNullOrWhiteSpace(session.sessionName) ? "session" : session.sessionName;
        OpenDeleteConfirm(string.Format(deleteSessionConfirmFormat, label));
    }

    private void OpenDeleteConfirm(string message)
    {
        deleteConfirmOpen = true;

        if (confirmText != null)
        {
            confirmText.text = message;
        }

        if (confirmRoot != null)
        {
            confirmRoot.SetActive(true);
            confirmRoot.transform.SetAsLastSibling();
        }
        else
        {
            InfoBoxUI.TryShowTopLeft($"{message} (Interact = confirmer / Retour = annuler)");
        }

        SetActiveMenuInteractable(false);
        SetSharedCursorInputEnabled(false);
    }

    private void ConfirmDelete()
    {
        if (!deleteConfirmOpen)
        {
            return;
        }

        CloseDeleteConfirm();

        if (SaveSessionManager.Instance == null)
        {
            pendingDelete = null;
            pendingDeleteSession = null;
            return;
        }

        bool deleted = false;
        if (pendingDeleteSession != null)
        {
            deleted = SaveSessionManager.Instance.DeleteSession(pendingDeleteSession.sessionId);
            SetStatus(deleted ? "Session supprimee." : "Echec suppression session.");
        }
        else if (pendingDelete != null)
        {
            deleted = SaveSessionManager.Instance.DeleteSave(pendingDelete.sessionId, pendingDelete.saveId, true);
            SetStatus(deleted ? "Sauvegarde supprimee." : "Echec suppression.");
        }

        pendingDelete = null;
        pendingDeleteSession = null;
        RefreshSessions();
    }

    private void CancelDelete()
    {
        pendingDelete = null;
        pendingDeleteSession = null;
        CloseDeleteConfirm();
    }

    private void CloseDeleteConfirm()
    {
        if (!deleteConfirmOpen)
        {
            return;
        }

        deleteConfirmOpen = false;
        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
        }
        SetActiveMenuInteractable(true);
        SetSharedCursorInputEnabled(true);
    }

    private void SetSharedCursorInputEnabled(bool enabled)
    {
        if (sharedCursor == null && sharedCursorNavigator == null)
        {
            return;
        }

        if (!enabled)
        {
            if (!cachedSharedCursorState)
            {
                cachedSharedCursorAllowInput = sharedCursor != null && sharedCursor.allowInput;
                cachedSharedCursorNavigatorEnabled = sharedCursorNavigator != null && sharedCursorNavigator.enabled;
                cachedSharedCursorState = true;
            }

            if (sharedCursor != null)
            {
                sharedCursor.allowInput = false;
            }

            if (sharedCursorNavigator != null)
            {
                sharedCursorNavigator.enabled = false;
            }
            return;
        }

        if (!cachedSharedCursorState)
        {
            return;
        }

        if (sharedCursor != null)
        {
            sharedCursor.allowInput = cachedSharedCursorAllowInput;
        }

        if (sharedCursorNavigator != null)
        {
            sharedCursorNavigator.enabled = cachedSharedCursorNavigatorEnabled;
        }

        cachedSharedCursorState = false;
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

    private void StartOfflineFlow()
    {
        if (!isLoading)
        {
            ShowLoadingScreen(loadingMessage);
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (!LoadingScreenService.LoadScene(gameplaySceneName, loadingMessage, LoadSceneMode.Single))
        {
            HideLoadingScreen();
        }
    }

    private void StartJoinFlow(NetcodeSessionEndpoint endpoint)
    {
        if (!isLoading)
        {
            ShowLoadingScreen(joinConnectingMessage);
        }

        NetcodeLauncher launcher = ResolveLauncher();
        if (launcher == null)
        {
            HideLoadingScreen();
            SetStatus("NetcodeLauncher manquant.");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            HideLoadingScreen();
            SetStatus("Connexion deja active.");
            return;
        }

        if (SaveSessionManager.Instance != null)
        {
            SaveSessionManager.Instance.SetCurrentSessionType(SaveSessionType.Multiplayer);
        }

        bool started = launcher.StartClientWithConnection(endpoint.Address, endpoint.Port);
        if (!started)
        {
            HideLoadingScreen();
            SetStatus("Client deja actif.");
            return;
        }

        activeJoinEndpoint = endpoint;
        joinInProgress = true;
        RegisterJoinCallbacks(true);
        SetJoinStatus($"{joinConnectingMessage} {endpoint.EndpointLabel}");
        SetStatus($"Connexion vers {endpoint.EndpointLabel} (code {endpoint.Code})...");

        if (joinTimeoutRoutine != null)
        {
            StopCoroutine(joinTimeoutRoutine);
        }
        joinTimeoutRoutine = StartCoroutine(JoinTimeoutRoutine());
    }

    private void StartGameFlow()
    {
        if (SaveSessionManager.Instance != null)
        {
            SaveSessionManager.Instance.SetCurrentSessionType(currentSessionType);
        }

        StartOfflineFlow();
    }

    private System.Collections.IEnumerator JoinTimeoutRoutine()
    {
        float timeout = Mathf.Max(10f, joinTimeoutSeconds);
        float endTime = Time.unscaledTime + timeout;
        while (Time.unscaledTime < endTime)
        {
            if (!joinInProgress)
            {
                yield break;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsConnectedClient)
            {
                yield break;
            }

            yield return null;
        }

        if (joinInProgress)
        {
            HandleJoinFailure(BuildJoinFailureMessage(true));
        }
    }

    private void RegisterJoinTransportFailureCallback(bool enabled)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            return;
        }

        manager.OnTransportFailure -= OnJoinTransportFailure;
        if (enabled)
        {
            manager.OnTransportFailure += OnJoinTransportFailure;
        }
    }

    private void OnJoinTransportFailure()
    {
        if (!joinInProgress)
        {
            return;
        }

        HandleJoinFailure(BuildJoinFailureMessage(false));
    }

    private void RegisterJoinCallbacks(bool enabled)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            return;
        }

        manager.OnClientConnectedCallback -= OnJoinClientConnected;
        manager.OnClientDisconnectCallback -= OnJoinClientDisconnected;
        if (enabled)
        {
            manager.OnClientConnectedCallback += OnJoinClientConnected;
            manager.OnClientDisconnectCallback += OnJoinClientDisconnected;
        }
    }

    private void OnJoinClientConnected(ulong clientId)
    {
        if (!joinInProgress)
        {
            return;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || clientId != manager.LocalClientId)
        {
            return;
        }

        if (joinTimeoutRoutine != null)
        {
            StopCoroutine(joinTimeoutRoutine);
            joinTimeoutRoutine = null;
        }

        if (joinSceneSyncRoutine != null)
        {
            StopCoroutine(joinSceneSyncRoutine);
        }

        joinSceneSyncRoutine = StartCoroutine(JoinSceneSyncRoutine());
    }

    private void OnJoinClientDisconnected(ulong clientId)
    {
        if (!joinInProgress)
        {
            return;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || clientId != manager.LocalClientId)
        {
            return;
        }

        HandleJoinFailure(BuildJoinFailureMessage(false));
    }

    private void HandleJoinFailure(string message)
    {
        joinInProgress = false;
        RegisterJoinCallbacks(false);
        if (joinTimeoutRoutine != null)
        {
            StopCoroutine(joinTimeoutRoutine);
            joinTimeoutRoutine = null;
        }
        if (joinSceneSyncRoutine != null)
        {
            StopCoroutine(joinSceneSyncRoutine);
            joinSceneSyncRoutine = null;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        HideLoadingScreen();
        SetJoinStatus(message);
        SetStatus(message);
        activeJoinEndpoint = default;
    }

    private IEnumerator JoinSceneSyncRoutine()
    {
        string targetSceneName = string.Empty;
        float timeout = Time.unscaledTime + Mathf.Max(5f, joinTimeoutSeconds);
        while (Time.unscaledTime < timeout)
        {
            if (!joinInProgress)
            {
                joinSceneSyncRoutine = null;
                yield break;
            }

            WorldInteractionService service = WorldInteractionService.Instance;
            if (service != null && !string.IsNullOrWhiteSpace(service.ActiveSceneName))
            {
                targetSceneName = service.ActiveSceneName;
                break;
            }

            yield return null;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            targetSceneName = gameplaySceneName;
        }

        joinInProgress = false;
        RegisterJoinCallbacks(false);
        activeJoinEndpoint = default;
        joinSceneSyncRoutine = null;

        Scene activeScene = SceneManager.GetActiveScene();
        if (string.Equals(activeScene.name, targetSceneName, StringComparison.OrdinalIgnoreCase))
        {
            HideLoadingScreen();
            SetJoinStatus("Connexion et synchronisation terminees.");
            SetStatus("Connexion et synchronisation terminees.");
            yield break;
        }

        string loadingMessageText = $"Synchronisation de la scene {targetSceneName}...";
        ShowLoadingScreen(loadingMessageText);
        SetJoinStatus(loadingMessageText);
        SetStatus(loadingMessageText);
        if (!LoadingScreenService.LoadScene(targetSceneName, loadingMessageText, LoadSceneMode.Single))
        {
            HideLoadingScreen();
            SetJoinStatus("Echec du chargement de scene.");
            SetStatus("Echec du chargement de scene.");
        }
    }

    private void ShowLoadingScreen(string overrideMessage = null)
    {
        isLoading = true;
        string message = string.IsNullOrWhiteSpace(overrideMessage) ? loadingMessage : overrideMessage;

        LoadingScreenService.Show(message);

        if (loadingText != null)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                loadingText.text = message;
            }
        }

        if (loadingGroup != null)
        {
            loadingGroup.gameObject.SetActive(true);
            loadingGroup.alpha = 1f;
            loadingGroup.interactable = false;
            loadingGroup.blocksRaycasts = true;
        }

        SetActiveMenuInteractable(false);
    }

    private void HideLoadingScreen()
    {
        isLoading = false;
        LoadingScreenService.Hide();

        if (loadingGroup != null)
        {
            loadingGroup.alpha = 0f;
            loadingGroup.interactable = false;
            loadingGroup.blocksRaycasts = false;
            loadingGroup.gameObject.SetActive(false);
        }

        SetActiveMenuInteractable(true);
    }

    private void SetJoinStatus(string message)
    {
        if (joinStatusText == null)
        {
            return;
        }

        joinStatusText.text = message ?? string.Empty;
    }

    private string ResolveJoinAddress()
    {
        string address = joinAddressInput != null ? joinAddressInput.text : string.Empty;
        if (string.IsNullOrWhiteSpace(address))
        {
            address = joinAddress;
        }

        return NetcodeSessionCode.NormalizeAddress(address, hostLoopbackAddress);
    }

    private bool TryResolveJoinEndpoint(out NetcodeSessionEndpoint endpoint)
    {
        string code = joinCodeInput != null ? joinCodeInput.text : string.Empty;
        string address = ResolveJoinAddress();
        return NetcodeSessionCode.TryCreateEndpoint(code, address, basePort, portRange, out endpoint);
    }

    private string BuildJoinFailureMessage(bool timedOut)
    {
        if (!activeJoinEndpoint.IsValid)
        {
            return joinNoSessionMessage;
        }

        NetworkManager manager = NetworkManager.Singleton;
        string disconnectReason = manager != null ? manager.DisconnectReason : string.Empty;
        if (!string.IsNullOrWhiteSpace(disconnectReason))
        {
            return $"Connexion refusee par l'hote ({activeJoinEndpoint.EndpointLabel}, code {activeJoinEndpoint.Code}) : {disconnectReason}";
        }

        string reason = timedOut
            ? "Aucune reponse de l'hote dans le delai imparti."
            : "La connexion a ete interrompue avant la validation du join.";

        string message = $"Connexion impossible a {activeJoinEndpoint.EndpointLabel} pour le code {activeJoinEndpoint.Code}. {reason} Verifie que l'hote est lance avec ce meme code.";
        if (ShouldSuggestLoopback(activeJoinEndpoint.Address))
        {
            message += " Pour un test sur le meme PC, utilise 127.0.0.1 au lieu de l'IP publique.";
        }

        return message;
    }

    private static bool ShouldSuggestLoopback(string address)
    {
        string normalized = NetcodeSessionCode.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IPAddress.TryParse(normalized, out IPAddress parsed) || IPAddress.IsLoopback(parsed))
        {
            return false;
        }

        if (parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        byte[] bytes = parsed.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        if (bytes[0] == 10)
        {
            return false;
        }

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return false;
        }

        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return false;
        }

        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return false;
        }

        return true;
    }

    private void SetActiveMenuInteractable(bool enabled)
    {
        if (currentMenu == MenuState.GameOptions && gameOptionsGroup != null)
        {
            gameOptionsGroup.interactable = enabled;
            gameOptionsGroup.blocksRaycasts = enabled;
            return;
        }

        if (currentMenu == MenuState.SoloOptions && soloOptionsGroup != null)
        {
            soloOptionsGroup.interactable = enabled;
            soloOptionsGroup.blocksRaycasts = enabled;
            return;
        }

        if (currentMenu == MenuState.MultiOptions && multiOptionsGroup != null)
        {
            multiOptionsGroup.interactable = enabled;
            multiOptionsGroup.blocksRaycasts = enabled;
            return;
        }

        if (currentMenu == MenuState.Options && optionsGroup != null)
        {
            optionsGroup.interactable = enabled;
            optionsGroup.blocksRaycasts = enabled;
            return;
        }

        if (currentMenu == MenuState.Join && joinPanelGroup != null)
        {
            joinPanelGroup.interactable = enabled;
            joinPanelGroup.blocksRaycasts = enabled;
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
