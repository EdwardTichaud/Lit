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
    private const float DefaultTitleCardIntroDelaySeconds = 3f;
    private const float DefaultTitleCardParticleLeadDelaySeconds = 2f;

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
    [SerializeField] private float titleCardIntroDelay = 3f;
    [SerializeField] private float titleCardParticleLeadDelay = 2f;
    [SerializeField, Min(0.01f)] private float titleCardIntroDuration = 3f;
    [SerializeField] private float titleCardInputEnableDelay = 2f;
    [SerializeField, Min(0f)] private float titleCardPointerCursorUnlockDelay = 1f;
    [SerializeField] private string titleCardParticleSortingLayerName = "UI";
    [SerializeField] private int titleCardParticleSortingOrder = 100;
    [SerializeField] private List<AudioClipSO> titleCardParticleSfx = new List<AudioClipSO>();
    public List<GameObject> titleCardParticleRoots = new List<GameObject>();

    [Header("Button Audio")]
    [SerializeField] private AudioClipSO menuButtonSfx;
    [SerializeField, Min(0f)] private float menuButtonSfxCooldown = 0.05f;

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
    [SerializeField] private string previewMissingLabelText = "Aucun aperÃÆ’Æ’§u";
    [SerializeField] private Color previewMissingLabelColor = new Color(1f, 1f, 1f, 0.7f);
    [SerializeField] private int previewMissingLabelFontSize = 36;

    [Header("Actions")]
    [SerializeField] private TMP_Text statusText;

    [Header("Game Options")]
    [Header("Shared Cursor")]
    [SerializeField] private CursorController sharedCursor;
    [SerializeField] private MainMenuPointerCursor pointerCursor;
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
    [SerializeField] private bool useSingleJoinCode = true;
    [SerializeField, Min(16)] private int joinCodeCharacterLimit = 160;
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
    private readonly MainMenuPreviewCache previewCache = new MainMenuPreviewCache();
    private bool waitingForInput;
    private MenuState currentMenu = MenuState.TitleCard;
    private Coroutine cursorSnapRoutine;
    private RectTransform currentCursorRoot;
    private bool hasInitializedState;
    private bool titleCardProceedTriggered;
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
    private Coroutine titleCardIntroRoutine;
    private Coroutine titleCardPointerCursorUnlockRoutine;
    private NetcodeSessionEndpoint activeJoinEndpoint;
    private bool titleCardIntroPlayed;
    private bool titleCardIntroInputLocked;
    private Transform titleCardParticleRuntimeRoot;
    private bool titleCardParticleRootsPrepared;
    private bool cachedTitleCardCursorAllowInput;
    private bool cachedTitleCardCursorNavigatorEnabled;
    private bool titleCardCursorStateCached;
    private float lastMenuButtonSfxTime = float.NegativeInfinity;

    private PrivateSessionService sessionService;
    private MenuState operationReturnState;
    private bool operationOwned;
    private static bool titleSeen;
    private string preparedSessionName;
    private SaveSlotInfo preparedSave;
    public bool OperationBusy => isLoading || (PrivateSessionService.Instance != null && PrivateSessionService.Instance.IsActive);
    public bool IsTitleActive => currentMenu == MenuState.TitleCard;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMenuStatics() { titleSeen = false; }

    private void Awake()
    {

        MainMenuDisplaySettings.ApplySavedModeIfNeeded();
        MainMenuInputSettings.ApplySavedModeIfNeeded();
        EnsureSaveManager();
        ResolveOptionalReferences();
        MainMenuPreferences.Apply();
        if (optionsGroup != null) optionsGroup.gameObject.SetActive(false);
        optionsGroup = MainMenuSettingsView.Create(this);
        optionsCursorRoot = optionsGroup.transform as RectTransform;
        ApplyInitialMenuVisibility();
        ConfigureNewGameActions();
        ConfigureJoinActions();
        ConfigureLoadConfirmActions();
        PrepareTitleCardParticleRoots();
        InitializeState();
        InitializeOverlays();
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        LocalInputRouter.Return += OnReturnPerformed;
        LocalInputRouter.TriggerMunin += OnTriggerMuninPerformed;

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
        LocalInputRouter.TriggerMunin -= OnTriggerMuninPerformed;
        if (sessionService != null) sessionService.Changed -= OnSessionChanged;
        sessionService = null;
        InputFocusStack.Pop(this);
        RegisterTextInput(false);
        ConfirmationManager.Dismiss(this);
        loadConfirmOpen = false;
        deleteConfirmOpen = false;

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
        CancelTitleCardIntro();
        SetTitleCardParticleRootsVisible(false);
    }

    private void OnDestroy()
    {
        ClearPreviewTexture();
        previewCache.Dispose();
    }

    private void Update()
    {
        if (sessionService == null && PrivateSessionService.Instance != null)
        {
            sessionService = PrivateSessionService.Instance;
            sessionService.Changed += OnSessionChanged;
            OnSessionChanged();
        }
        if (currentMenu == MenuState.TitleCard && AnyInputPressedThisFrame())
        {
            titleSeen = true;
            CancelTitleCardIntro();
            SetTitleCardIntroInputLock(false);
            ShowGameOptionsMenu();
            return;
        }
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
        if (MainMenuNavigation.Active) return;
        if (titleCardIntroInputLocked)
        {
            return;
        }

        if (InputFocusStack.HasAnyFocus() && !HasInputFocus())
        {
            return;
        }

        if (!CanProcessInteract())
        {
            return;
        }

        if (hoveredSessionEntry != null && IsCursorOnSessionsRoot())
        {
            LocalInputRouter.ConsumeInteract();
            OnSessionInteract(hoveredSessionEntry);
        }
    }

    private void OnReturnPerformed(InputAction.CallbackContext context)
    {
        if (MainMenuNavigation.Active) return;
        if (titleCardIntroInputLocked)
        {
            return;
        }

        if (TryCancelVirtualKeyboard())
        {
            return;
        }

        if (InputFocusStack.HasAnyFocus() && !HasInputFocus())
        {
            return;
        }

        HandleBackAction();
    }

    private void OnTriggerMuninPerformed(InputAction.CallbackContext context)
    {
        if (titleCardIntroInputLocked)
        {
            return;
        }

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
        if (waitForAnyInput && titleCardGroup != null && !titleSeen && !(PrivateSessionService.Instance?.IsActive ?? false))
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

        ConfigureJoinCodeInputField();
        SetJoinAddressFieldVisible(!useSingleJoinCode);

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
            joinAddressInput.interactable = !useSingleJoinCode;
            if (!useSingleJoinCode && string.IsNullOrWhiteSpace(joinAddressInput.text))
            {
                joinAddressInput.text = string.IsNullOrWhiteSpace(joinAddress) ? hostLoopbackAddress : joinAddress;
            }
        }

        RegisterTextInput(true);
        UpdateCursorTarget();
    }

    private void ConfigureJoinCodeInputField()
    {
        if (joinCodeInput == null)
        {
            return;
        }

        joinCodeInput.contentType = TMP_InputField.ContentType.Standard;
        joinCodeInput.characterValidation = TMP_InputField.CharacterValidation.None;
        joinCodeInput.characterLimit = Mathf.Max(joinCodeInput.characterLimit, joinCodeCharacterLimit);

        if (joinCodeInput.placeholder is TMP_Text placeholder)
        {
            placeholder.text = useSingleJoinCode ? "Code d'invitation" : "Code";
        }
    }

    private void SetJoinAddressFieldVisible(bool visible)
    {
        if (joinAddressInput == null)
        {
            return;
        }

        joinAddressInput.interactable = visible;

        Transform fieldRoot = joinAddressInput.transform;
        Transform rowRoot = fieldRoot.parent != null && fieldRoot.parent != joinPanelGroup.transform
            ? fieldRoot.parent
            : fieldRoot;
        rowRoot.gameObject.SetActive(visible);
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
                CancelTitleCardIntro();
                SetTitleCardIntroInputLock(false);
                CancelTitleCardPointerCursorUnlock();
                SetTitleCardPointerCursorLocked(false);
                SetTitleCardParticleRootsVisible(false);
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

        waitingForInput = waitForAnyInput && state == MenuState.TitleCard && titleCardIntroRoutine == null;

        SetSharedCursorChildrenActive(state != MenuState.TitleCard);

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

    private void SetSharedCursorChildrenActive(bool active)
    {
        if (sharedCursor == null)
        {
            return;
        }

        RectTransform cursorRoot = sharedCursor.cursor != null
            ? sharedCursor.cursor
            : sharedCursor.transform as RectTransform;
        if (cursorRoot == null)
        {
            return;
        }

        for (int i = 0; i < cursorRoot.childCount; i++)
        {
            Transform child = cursorRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            child.gameObject.SetActive(active);
        }
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
        RectTransform root = IsUsableCursorRoot(explicitRoot) ? explicitRoot : null;
        if (root == null && group != null)
        {
            RectTransform groupRoot = group.transform as RectTransform;
            root = IsUsableCursorRoot(groupRoot) ? groupRoot : null;
        }

        if (root == null)
        {
            root = explicitRoot != null
                ? explicitRoot
                : group != null
                    ? group.transform as RectTransform
                    : null;
        }

        if (root == null)
        {
            return null;
        }

        if (HasDirectCursorChildren(root))
        {
            return root;
        }

        if (FindFirstCursorItem(root) != null)
        {
            return root;
        }

        return root;
    }

    private static bool IsUsableCursorRoot(RectTransform root)
    {
        return root != null && FindFirstCursorItem(root) != null;
    }

    private static bool HasDirectCursorChildren(RectTransform root)
    {
        if (root == null)
        {
            return false;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform child = root.GetChild(i) as RectTransform;
            if (IsDirectCursorTarget(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDirectCursorTarget(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
        {
            return false;
        }

        MenuCursorAction action = rect.GetComponent<MenuCursorAction>();
        if (action != null && action.isActiveAndEnabled)
        {
            return true;
        }

        MenuCursorItem item = rect.GetComponent<MenuCursorItem>();
        if (item != null && item.isActiveAndEnabled)
        {
            return true;
        }

        MonoBehaviour[] behaviours = rect.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].isActiveAndEnabled && behaviours[i] is IMenuCursorHandler)
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

        if (fadeRoutines.TryGetValue(group, out Coroutine runningFade) && runningFade != null)
        {
            StopCoroutine(runningFade);
            fadeRoutines.Remove(group);
        }

        if (!titleCardIntroPlayed)
        {
            CancelTitleCardIntro();
            ApplyFadeImmediate(group, 0f, false);
            SetTitleCardParticleRootsVisible(false);
            SetTitleCardIntroInputLock(true);
            SetTitleCardPointerCursorLocked(true);
            waitingForInput = false;
            titleCardIntroRoutine = StartCoroutine(ShowTitleCardIntroRoutine(group));
            titleCardIntroPlayed = true;
        }
        else
        {
            SetTitleCardIntroInputLock(false);
            SetTitleCardPointerCursorLocked(false);
            SetTitleCardParticleRootsVisible(true);
            RestartTitleCardParticleSystems();
            PlayTitleCardParticleSfx();
            StartFade(group, 1f, true);
        }
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

    /// <summary>
    /// Retire toute l'interface propre au menu avant de laisser le service de
    /// chargement global prendre l'ecran. Celui-ci reste le seul affichage
    /// visible pendant l'ouverture d'une partie.
    /// </summary>
    private void CloseAllMenuPanelsForGameplayTransition()
    {
        CancelTitleCardIntro();
        newGamePromptOpen = false;
        loadConfirmOpen = false;
        deleteConfirmOpen = false;
        waitingForInput = false;

        ApplyFadeImmediate(titleCardGroup, 0f, false);
        ApplyFadeImmediate(gameOptionsGroup, 0f, false);
        ApplyFadeImmediate(soloOptionsGroup, 0f, false);
        ApplyFadeImmediate(multiOptionsGroup, 0f, false);
        ApplyFadeImmediate(optionsGroup, 0f, false);
        ApplyFadeImmediate(ResolveLoadMenuGroup(), 0f, false);
        ApplyFadeImmediate(newGamePanelGroup, 0f, false);
        ApplyFadeImmediate(joinPanelGroup, 0f, false);
        ApplyFadeImmediate(loadConfirmGroup, 0f, false);
        ApplyFadeImmediate(loadingGroup, 0f, false);

        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
        }

        HideVirtualKeyboard();
        SetTitleCardParticleRootsVisible(false);
        SetTitleCardIntroInputLock(true);
        SetMainMenuPointerCursorVisible(false);
        SetActiveMenuInteractable(false);
        SetSharedCursorChildrenActive(false);
    }

    private void CancelTitleCardIntro()
    {
        CancelTitleCardPointerCursorUnlock();
        if (titleCardIntroRoutine == null)
        {
            return;
        }

        StopCoroutine(titleCardIntroRoutine);
        titleCardIntroRoutine = null;
    }

    private void ApplyInitialMenuVisibility()
    {
        ApplyFadeImmediate(titleCardGroup, 0f, false);
        ApplyFadeImmediate(gameOptionsGroup, 0f, false);
        ApplyFadeImmediate(soloOptionsGroup, 0f, false);
        ApplyFadeImmediate(multiOptionsGroup, 0f, false);
        ApplyFadeImmediate(optionsGroup, 0f, false);
        ApplyFadeImmediate(joinPanelGroup, 0f, false);
        ApplyFadeImmediate(ResolveLoadMenuGroup(), 0f, false);
        SetTitleCardParticleRootsVisible(false);
        SetSharedCursorChildrenActive(false);
        SetTitleCardIntroInputLock(waitForAnyInput && titleCardGroup != null && !titleCardIntroPlayed);
        SetTitleCardPointerCursorLocked(titleCardGroup != null && !titleCardIntroPlayed);
    }

    private void SetTitleCardIntroInputLock(bool locked)
    {
        titleCardIntroInputLocked = locked;

        if (sharedCursor == null && sharedCursorNavigator == null)
        {
            return;
        }

        if (locked)
        {
            if (!titleCardCursorStateCached)
            {
                cachedTitleCardCursorAllowInput = sharedCursor != null && sharedCursor.allowInput;
                cachedTitleCardCursorNavigatorEnabled = sharedCursorNavigator != null && sharedCursorNavigator.enabled;
                titleCardCursorStateCached = true;
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

        if (!titleCardCursorStateCached)
        {
            return;
        }

        if (sharedCursor != null)
        {
            sharedCursor.allowInput = cachedTitleCardCursorAllowInput;
        }

        if (sharedCursorNavigator != null)
        {
            sharedCursorNavigator.enabled = cachedTitleCardCursorNavigatorEnabled;
        }

        titleCardCursorStateCached = false;
    }

    private void SetTitleCardPointerCursorLocked(bool locked)
    {
        ResolvePointerCursorReference();
        if (pointerCursor != null)
        {
            pointerCursor.SetInputLocked(locked);
        }
    }

    private void SetMainMenuPointerCursorVisible(bool visible)
    {
        ResolvePointerCursorReference();
        if (pointerCursor != null)
        {
            pointerCursor.SetCursorVisible(visible);
        }
    }

    private void ScheduleTitleCardPointerCursorUnlock(float delay)
    {
        CancelTitleCardPointerCursorUnlock();

        float resolvedDelay = Mathf.Max(0f, delay);
        if (resolvedDelay <= 0f)
        {
            SetTitleCardPointerCursorLocked(false);
            return;
        }

        titleCardPointerCursorUnlockRoutine = StartCoroutine(UnlockTitleCardPointerCursorAfterDelay(resolvedDelay));
    }

    private void CancelTitleCardPointerCursorUnlock()
    {
        if (titleCardPointerCursorUnlockRoutine == null)
        {
            return;
        }

        StopCoroutine(titleCardPointerCursorUnlockRoutine);
        titleCardPointerCursorUnlockRoutine = null;
    }

    private IEnumerator UnlockTitleCardPointerCursorAfterDelay(float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            elapsed += fadeUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        SetTitleCardPointerCursorLocked(false);
        titleCardPointerCursorUnlockRoutine = null;
    }

    private void ResolvePointerCursorReference()
    {
        if (pointerCursor != null)
        {
            return;
        }

        pointerCursor = GetComponentInChildren<MainMenuPointerCursor>(true);
        if (pointerCursor != null)
        {
            return;
        }

        MainMenuPointerCursor[] cursors = Resources.FindObjectsOfTypeAll<MainMenuPointerCursor>();
        for (int i = 0; i < cursors.Length; i++)
        {
            MainMenuPointerCursor candidate = cursors[i];
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.isLoaded)
            {
                pointerCursor = candidate;
                return;
            }
        }
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

    private bool IsVirtualKeyboardVisible()
    {
        if (virtualKeyboardRoot != null && virtualKeyboardRoot.gameObject.activeInHierarchy)
        {
            return true;
        }

        return virtualKeyboardGroup != null
            && virtualKeyboardGroup.gameObject.activeInHierarchy
            && virtualKeyboardGroup.alpha > 0.001f;
    }

    private bool TryCancelVirtualKeyboard()
    {
        if (!IsVirtualKeyboardVisible())
        {
            return false;
        }

        if (newGamePromptOpen)
        {
            CancelNewGame();
            return true;
        }

        if (currentMenu == MenuState.Join)
        {
            CancelJoin();
            return true;
        }

        HideVirtualKeyboard();
        UpdateCursorTarget();
        return true;
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
        if (group == null) return;
        float duration = MainMenuPreferences.ReducedMotion ? 0f : (durationOverride >= 0 ? durationOverride : panelFadeDuration);
        UIManager.TransitionCanvasGroup(this, group, show, duration, true);
    }

    private void ApplyFadeImmediate(CanvasGroup group, float targetAlpha, bool show)
    {
        if (group == null) return;
        UIManager.TransitionCanvasGroup(this, group, show && targetAlpha > .001f, 0f, true);
    }

    private void LateUpdate()
    {
        // Visibility animation never grants input to a screen covered by a modal/operation.
        foreach (CanvasGroup group in new[] { titleCardGroup, gameOptionsGroup, soloOptionsGroup, multiOptionsGroup,
            optionsGroup, loadMenuGroup, newGamePanelGroup, joinPanelGroup, virtualKeyboardGroup })
        {
            if (group == null || !group.gameObject.activeInHierarchy) continue;
            bool allowed = !OperationBusy && !deleteConfirmOpen && !loadConfirmOpen &&
                (!newGamePromptOpen || group == newGamePanelGroup || group == virtualKeyboardGroup);
            group.interactable = allowed && group.alpha > .01f;
            group.blocksRaycasts = group.interactable;
        }
    }

    private IEnumerator ShowTitleCardIntroRoutine(CanvasGroup group)
    {
        if (group == null)
        {
            yield break;
        }

        if (fadeRoutines.TryGetValue(group, out Coroutine runningFade) && runningFade != null)
        {
            StopCoroutine(runningFade);
            fadeRoutines.Remove(group);
        }

        ApplyFadeImmediate(group, 0f, false);
        SetTitleCardParticleRootsVisible(false);

        float introDelay = Mathf.Max(0f, titleCardIntroDelay);
        float particleLeadDelay = Mathf.Max(0f, titleCardParticleLeadDelay);
        float inputEnableDelay = Mathf.Max(0f, titleCardInputEnableDelay);
        float duration = Mathf.Max(0.01f, titleCardIntroDuration);
        bool hasParticles = false;
        if (titleCardParticleRoots != null)
        {
            for (int i = 0; i < titleCardParticleRoots.Count; i++)
            {
                if (titleCardParticleRoots[i] != null)
                {
                    hasParticles = true;
                    break;
                }
            }
        }

        float elapsed = 0f;
        bool particlesStarted = !hasParticles;
        bool fadeStarted = false;
        bool inputDelayReached = inputEnableDelay <= 0f;
        bool pointerCursorUnlockScheduled = false;
        while (true)
        {
            if (!particlesStarted && elapsed >= particleLeadDelay)
            {
                SetTitleCardParticleRootsVisible(true);
                RestartTitleCardParticleSystems();
                PlayTitleCardParticleSfx();
                particlesStarted = true;
            }

            if (!fadeStarted && elapsed >= introDelay)
            {
                group.gameObject.SetActive(true);
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                fadeStarted = true;
            }

            if (fadeStarted)
            {
                float fadeElapsed = Mathf.Clamp(elapsed - introDelay, 0f, duration);
                group.alpha = Mathf.Clamp01(fadeElapsed / duration);
            }

            if (!inputDelayReached && elapsed >= inputEnableDelay)
            {
                inputDelayReached = true;
            }

            bool fadeComplete = fadeStarted && elapsed >= introDelay + duration;
            if (!pointerCursorUnlockScheduled && fadeComplete)
            {
                pointerCursorUnlockScheduled = true;
                ScheduleTitleCardPointerCursorUnlock(titleCardPointerCursorUnlockDelay);
            }

            if (particlesStarted && fadeComplete && inputDelayReached)
            {
                break;
            }

            elapsed += fadeUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        group.gameObject.SetActive(true);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        if (!pointerCursorUnlockScheduled)
        {
            ScheduleTitleCardPointerCursorUnlock(titleCardPointerCursorUnlockDelay);
        }

        SetTitleCardIntroInputLock(false);
        titleCardIntroRoutine = null;
        waitingForInput = waitForAnyInput && currentMenu == MenuState.TitleCard;
    }

    private void HandleTitleCardProceed()
    {
        titleSeen = true;
        if (titleCardProceedTriggered)
        {
            return;
        }

        titleCardProceedTriggered = true;
        PlayTitleCardSfx();
        ShowGameOptionsMenu();
    }

    private void HandleBackAction()
    {
        if (isLoading)
        {
            sessionService?.Leave();
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
            if (waitForAnyInput && titleCardGroup != null && !titleSeen && !(PrivateSessionService.Instance?.IsActive ?? false))
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
            AudioManager.PlayClipAtPoint(titleCardProceedSfx, Vector3.zero);
        }
    }

    public void PlayMenuButtonSfx(MenuCursorAction.MenuAction action)
    {
        if (action == MenuCursorAction.MenuAction.None || (int)action > (int)MenuCursorAction.MenuAction.PasteJoinAddress)
        {
            return;
        }

        if (menuButtonSfx == null || menuButtonSfx.audioClip == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now - lastMenuButtonSfxTime < menuButtonSfxCooldown)
        {
            return;
        }

        lastMenuButtonSfxTime = now;
        AudioManager.EnsureInstance()?.PlayUiClip(menuButtonSfx);
    }

    private void PlayTitleCardParticleSfx()
    {
        if (titleCardParticleSfx == null || titleCardParticleSfx.Count == 0)
        {
            return;
        }

        for (int i = 0; i < titleCardParticleSfx.Count; i++)
        {
            AudioClipSO clip = titleCardParticleSfx[i];
            if (clip == null)
            {
                continue;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayClip(clip, Vector3.zero);
            }
            else if (clip.audioClip != null)
            {
                AudioManager.PlayClipAtPoint(clip, Vector3.zero);
            }
        }
    }

    private void RestartTitleCardParticleSystems()
    {
        PrepareTitleCardParticleRoots();

        if (titleCardParticleRoots == null)
        {
            return;
        }

        for (int i = 0; i < titleCardParticleRoots.Count; i++)
        {
            GameObject root = titleCardParticleRoots[i];
            if (root == null)
            {
                continue;
            }

            ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int particleIndex = 0; particleIndex < particleSystems.Length; particleIndex++)
            {
                ParticleSystem current = particleSystems[particleIndex];
                if (current == null)
                {
                    continue;
                }

                current.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
                current.Play(withChildren: true);
            }
        }
    }

    private void PrepareTitleCardParticleRoots()
    {
        if (titleCardParticleRootsPrepared || titleCardParticleRoots == null || titleCardParticleRoots.Count == 0)
        {
            return;
        }

        Transform runtimeRoot = EnsureTitleCardParticleRuntimeRoot();
        int sortingLayerId = SortingLayer.NameToID(titleCardParticleSortingLayerName);

        for (int i = 0; i < titleCardParticleRoots.Count; i++)
        {
            GameObject root = titleCardParticleRoots[i];
            if (root == null)
            {
                continue;
            }

            if (runtimeRoot != null)
            {
                root.transform.SetParent(runtimeRoot, true);
            }

            ConfigureTitleCardParticleRenderers(root, sortingLayerId);
            root.SetActive(false);
        }

        titleCardParticleRootsPrepared = true;
    }

    private Transform EnsureTitleCardParticleRuntimeRoot()
    {
        if (titleCardParticleRuntimeRoot != null)
        {
            return titleCardParticleRuntimeRoot;
        }

        Canvas canvas = GetComponent<Canvas>();
        Camera targetCamera = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        GameObject runtimeRoot = new GameObject("MainMenu_TitleCardParticles_Runtime");
        titleCardParticleRuntimeRoot = runtimeRoot.transform;
        if (targetCamera != null)
        {
            titleCardParticleRuntimeRoot.SetParent(targetCamera.transform, false);
        }

        return titleCardParticleRuntimeRoot;
    }

    private void ConfigureTitleCardParticleRenderers(GameObject root, int sortingLayerId)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer current = renderers[i];
            if (current == null)
            {
                continue;
            }

            current.sortingLayerID = sortingLayerId;
            current.sortingOrder = titleCardParticleSortingOrder;
        }
    }

    private void SetTitleCardParticleRootsVisible(bool visible)
    {
        PrepareTitleCardParticleRoots();

        if (titleCardParticleRoots == null)
        {
            return;
        }

        for (int i = 0; i < titleCardParticleRoots.Count; i++)
        {
            GameObject root = titleCardParticleRoots[i];
            if (root == null)
            {
                continue;
            }

            if (visible)
            {
                root.SetActive(true);
                continue;
            }

            ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int particleIndex = 0; particleIndex < particleSystems.Length; particleIndex++)
            {
                ParticleSystem current = particleSystems[particleIndex];
                if (current == null)
                {
                    continue;
                }

                current.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            root.SetActive(false);
        }
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

        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
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

        ResolvePointerCursorReference();

        ResolveLoadingReferences();
    }

    private CanvasGroup FindCanvasGroup(string name)
    {
        Transform found = FindInHierarchy(transform, name);
        return found != null ? found.GetComponent<CanvasGroup>() : null;
    }

    private void ResolveLoadingReferences()
    {
        CanvasGroup resolved = ResolveLoadingGroup();
        if (resolved != null)
        {
            loadingGroup = resolved;
        }

        if (loadingText != null && loadingGroup != null && !loadingText.transform.IsChildOf(loadingGroup.transform))
        {
            loadingText = null;
        }

        if (loadingText == null && loadingGroup != null)
        {
            loadingText = loadingGroup.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private CanvasGroup ResolveLoadingGroup()
    {
        if (IsValidLoadingGroup(loadingGroup))
        {
            return loadingGroup;
        }

        CanvasGroup candidate = FindCanvasGroup("MainMenu_Loading");
        if (IsValidLoadingGroup(candidate))
        {
            return candidate;
        }

        candidate = FindCanvasGroup("LoadingScreen");
        if (IsValidLoadingGroup(candidate))
        {
            return candidate;
        }

        LoadingScreenService service = GetComponentInChildren<LoadingScreenService>(true);
        if (service != null)
        {
            candidate = service.GetComponent<CanvasGroup>();
            if (IsValidLoadingGroup(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsValidLoadingGroup(CanvasGroup group)
    {
        if (group == null)
        {
            return false;
        }

        if (group == loadMenuGroup)
        {
            return false;
        }

        return !string.Equals(group.gameObject.name, "MainMenu_Load", StringComparison.Ordinal);
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

        IReadOnlyList<SaveSessionInfo> sessions = SaveSessionManager.Instance.GetSessionsByType(currentSessionType);
        if (sessions == null || sessions.Count == 0)
        {
            if (emptySessionsPlaceholder != null)
            {
                emptySessionsPlaceholder.SetActive(true);
            }
            selectedSession = null;
            SetStatus("Aucune partie enregistrée dans ce mode.");
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

        if (!HasDirectCursorChildren(targetRoot))
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

        if (!HasDirectCursorChildren(targetRoot))
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
        string selectedSaveId = selectedSave != null ? selectedSave.saveId : null;
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
            && save.savedAtUtcTicks <= DateTime.MaxValue.Ticks ? new DateTime(save.savedAtUtcTicks, DateTimeKind.Utc).ToLocalTime()
            : DateTime.MinValue;

        TimeSpan playtime = TimeSpan.FromSeconds(MainMenuSaveCatalog.SafePlaytime(save.playTimeSeconds));
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
                $"Lieu : {MainMenuSaveCatalog.SceneLabel(save.sceneName)}";
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
            Texture2D texture = previewCache.Get(path);
            if (texture == null) { ApplyMissingPreview(); return; }

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
            // Cache owns the texture lifetime.
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

        if (MainMenuNavigation.UsingGamepad) ShowVirtualKeyboard();
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
        if (OperationBusy) return;
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

    private void ConfirmJoin()
    {
        if (OperationBusy) return;
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

        StartJoinFlow(NetcodeRelayCode.Normalize(joinCodeInput.text));
    }

    private void CancelJoin()
    {
        if (sessionService != null && sessionService.IsActive) { sessionService.Leave(); return; }
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

        string normalized = NetcodeSessionCode.NormalizeJoinInput(clipboard);
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
        if (useSingleJoinCode)
        {
            PasteJoinCodeFromClipboard();
            return;
        }

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
        if (OperationBusy || SaveSessionManager.Instance == null) return;
        string name = (sessionName ?? string.Empty).Trim();
        if (preparedSave == null || preparedSessionName != name || preparedSave.sessionType != currentSessionType ||
            !Directory.Exists(preparedSave.directoryPath))
        {
            if (!SaveSessionManager.Instance.TryCreateNewGame(name, currentSessionType, defaultNewGameSaveName,
                out preparedSave, out string error))
            { SetStatus(error); return; }
            preparedSessionName = name;
        }
        SaveSessionManager.Instance.SetActiveSave(preparedSave.sessionId, preparedSave.saveId);
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
            if (!useSingleJoinCode && joinAddressInput != null && joinAddressInput.isFocused)
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
            text = NetcodeSessionCode.NormalizeJoinInput(text);
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

        string normalized = NetcodeRelayCode.Normalize(value);
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

        return NetcodeRelayCode.IsValid(joinCodeInput.text);
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

    public bool UI_TryCancelVirtualKeyboard()
    {
        return TryCancelVirtualKeyboard();
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
        if (OperationBusy) return;

        TMP_InputField input;
        MenuCursorAction confirm;
        if (newGamePromptOpen)
        {
            UpdateNewGameConfirmState();
            input = newGameNameInput;
            confirm = newGameConfirmAction;
        }
        else if (currentMenu == MenuState.Join)
        {
            UpdateJoinConfirmState();
            input = joinCodeInput;
            confirm = joinConfirmAction;
        }
        else return;

        // Finish editing without submitting the form. Invalid input stays editable.
        if (confirm == null || !confirm.enabled) return;
        if (input != null) input.DeactivateInputField();
        ApplyVirtualKeyboardImmediate(false);
        UpdateCursorTarget();
        FindAnyObjectByType<MainMenuNavigation>()?.Focus(confirm.gameObject);
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
        if (OperationBusy) return;
        if (SaveSessionManager.Instance == null)
        {
            SetActiveMenuInteractable(true);
            SetSharedCursorInputEnabled(true);
            return;
        }

        if (selectedSave == null)
        {
            SetStatus("Selectionne une sauvegarde.");
            SetActiveMenuInteractable(true);
            SetSharedCursorInputEnabled(true);
            return;
        }

        if (!selectedSave.validMetadata) { SetStatus("Métadonnées illisibles : cette sauvegarde ne peut pas être chargée."); return; }
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

        pendingLoad = save;
        loadConfirmOpen = true;
        SetActiveMenuInteractable(false);
        SetSharedCursorInputEnabled(false);

        string label = string.IsNullOrWhiteSpace(save.saveName) ? "sauvegarde" : save.saveName;
        string message = string.Format(loadConfirmMessageFormat, label);
        bool shown = ConfirmationManager.TryShow(
            new ConfirmationRequest(this, message, ConfirmLoad, CancelLoadConfirm)
            {
                Title = "Chargement",
                ConfirmLabel = "Charger",
                CancelLabel = "Annuler",
                DebugContext = "MainMenu.LoadConfirm"
            });

        if (shown)
        {
            return;
        }

        loadConfirmOpen = false;
        SetActiveMenuInteractable(true);
        SetSharedCursorInputEnabled(true);
        SetStatus("Confirmation indisponible.");
    }

    private void ConfirmLoad()
    {
        if (pendingLoad == null)
        {
            CancelLoadConfirm();
            return;
        }

        selectedSave = pendingLoad;
        pendingLoad = null;
        loadConfirmOpen = false;
        ConfirmationManager.Dismiss(this);
        SetSharedCursorInputEnabled(true);

        OnLoadSelected();
    }

    private void CancelLoadConfirm()
    {
        pendingLoad = null;
        loadConfirmOpen = false;
        ConfirmationManager.Dismiss(this);
        SetActiveMenuInteractable(true);
        SetSharedCursorInputEnabled(true);
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
        SetActiveMenuInteractable(false);
        SetSharedCursorInputEnabled(false);

        bool shown = ConfirmationManager.TryShow(
            new ConfirmationRequest(this, message, ConfirmDelete, CancelDelete)
            {
                Title = "Suppression",
                ConfirmLabel = "Supprimer",
                CancelLabel = "Annuler",
                PreferCancel = true,
                DebugContext = "MainMenu.DeleteConfirm"
            });

        if (shown)
        {
            return;
        }

        deleteConfirmOpen = false;
        SetActiveMenuInteractable(true);
        SetSharedCursorInputEnabled(true);
        SetStatus("Confirmation indisponible.");
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
        ConfirmationManager.Dismiss(this);
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
        previewCache.Dispose();
        SaveSessionManager.Instance?.ReloadSessions();
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
        BeginSessionOperation();
        try
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetcodeBootstrap.ShutdownActiveNetworkManager();
            if (!GameFlowService.StartOrLoadGame())
                RestoreAfterOperation("Impossible de démarrer le chargement de la partie.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            RestoreAfterOperation("Impossible de préparer la partie. Vérifiez la sauvegarde.");
        }
    }

    private void StartJoinFlow(string code)
    {
        if (OperationBusy) return;
        BeginSessionOperation();
        if (PrivateSessionService.Instance == null) { RestoreAfterOperation("Service multijoueur indisponible."); return; }
        PrivateSessionService.Instance.Join(code);
    }

    private void StartGameFlow()
    {
        if (OperationBusy) return;
        SaveSessionManager.Instance?.SetCurrentSessionType(currentSessionType);
        if (currentSessionType == SaveSessionType.Solo) { StartOfflineFlow(); return; }
        BeginSessionOperation();
        if (PrivateSessionService.Instance == null) { RestoreAfterOperation("Service multijoueur indisponible."); return; }
        PrivateSessionService.Instance.StartHost();
    }

    private void BeginSessionOperation()
    {
        operationReturnState = currentMenu;
        operationOwned = true;
        CloseAllMenuPanelsForGameplayTransition();
        isLoading = true;
    }

    private void OnSessionChanged()
    {
        if (sessionService == null) return;
        if (sessionService.Phase == PrivateSessionPhase.Failed)
        {
            RestoreAfterOperation(sessionService.Message);
            return;
        }
        if (sessionService.IsActive)
        {
            if (!operationOwned) { operationReturnState = MenuState.MultiOptions; operationOwned = true; }
            CloseAllMenuPanelsForGameplayTransition();
            isLoading = true;
        }
    }

    private void RestoreAfterOperation(string message)
    {
        isLoading = false;
        joinInProgress = false;
        SetTitleCardIntroInputLock(false);
        SetSharedCursorInputEnabled(true);
        LoadingScreenService.HideImmediately();
        SetMenuState(operationOwned ? operationReturnState : MenuState.MultiOptions);
        SetMainMenuPointerCursorVisible(true);
        operationOwned = false;
        SetStatus(message);
        SetJoinStatus(message);
    }

    public Transform NavigationModalRoot => IsVirtualKeyboardVisible() ? virtualKeyboardGroup.transform : null;
    public void UI_Back() { if (!TryCancelVirtualKeyboard()) HandleBackAction(); }
    public void UI_OpenKeyboard() { if (newGamePromptOpen || currentMenu == MenuState.Join) ShowVirtualKeyboard(); }
    private void ShowLoadingScreen(string overrideMessage = null)
    {
        isLoading = true;
        SetMainMenuPointerCursorVisible(false);
        string message = string.IsNullOrWhiteSpace(overrideMessage) ? loadingMessage : overrideMessage;
        ResolveLoadingReferences();

        LoadingScreenService.Show(message);

        // Le LoadingScreenService est l'unique overlay de transition. Ne pas
        // reactiver un CanvasGroup local ici : dans certains prefabs de menu,
        // cette reference pointe vers MainMenu_Load et affichait a tort la
        // liste des anciennes sauvegardes pendant la creation d'une partie.
        ApplyFadeImmediate(ResolveLoadMenuGroup(), 0f, false);
        ApplyFadeImmediate(newGamePanelGroup, 0f, false);
        ApplyFadeImmediate(joinPanelGroup, 0f, false);

        SetActiveMenuInteractable(false);
    }

    private void HideLoadingScreen()
    {
        isLoading = false;
        SetMainMenuPointerCursorVisible(true);
        ResolveLoadingReferences();
        LoadingScreenService.Hide();

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
        if (useSingleJoinCode)
        {
            return NetcodeSessionCode.NormalizeAddress(joinAddress, hostLoopbackAddress);
        }

        string address = joinAddressInput != null ? joinAddressInput.text : string.Empty;
        NetcodeLauncher launcher = ResolveLauncher();
        if (launcher != null)
        {
            return launcher.ResolveJoinAddress(address);
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            address = joinAddress;
        }

        return NetcodeSessionCode.NormalizeAddress(address, hostLoopbackAddress);
    }

    private bool TryResolveJoinEndpoint(out NetcodeSessionEndpoint endpoint)
    {
        string code = joinCodeInput != null ? joinCodeInput.text : string.Empty;
        string address = !useSingleJoinCode && joinAddressInput != null ? joinAddressInput.text : string.Empty;
        NetcodeLauncher launcher = ResolveLauncher();
        if (launcher != null)
        {
            return launcher.TryResolveJoinEndpoint(code, address, out endpoint, out _);
        }

        string normalizedAddress = ResolveJoinAddress();
        return NetcodeSessionCode.TryCreateEndpointFromJoinInput(code, normalizedAddress, basePort, portRange, out endpoint);
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
        launcher = FindAnyObjectByType<NetcodeLauncher>();
#else
        launcher = FindAnyObjectByType<NetcodeLauncher>();
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
