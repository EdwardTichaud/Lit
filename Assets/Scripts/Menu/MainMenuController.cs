using System;
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
    [SerializeField] private Button newButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Game Options")]
    [SerializeField] private Button newGameOptionButton;
    [SerializeField] private Button loadMenuButton;
    [SerializeField] private Button multiplayerButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitOptionButton;

    [Header("Shared Cursor")]
    [SerializeField] private CursorController sharedCursor;
    [SerializeField] private RectTransform gameOptionsCursorRoot;
    [SerializeField] private RectTransform loadMenuCursorRoot;

    [Header("Confirm Delete")]
    [SerializeField] private GameObject confirmRoot;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

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

    private void Awake()
    {
        EnsureSaveManager();
        InitializeState();
        BindButtons();
    }

    private void OnEnable()
    {
        BindButtons();
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteractPerformed;
        RefreshSessions();

        if (currentMenu == MenuState.GameOptions || currentMenu == MenuState.LoadMenu)
        {
            InputFocusStack.Push(this);
        }
    }

    private void OnDisable()
    {
        UnbindButtons();
        LocalInputRouter.Interact -= OnInteractPerformed;
        InputFocusStack.Pop(this);
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
            ShowGameOptionsMenu();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!CanProcessInteract())
        {
            return;
        }

        if (hoveredSessionEntry != null)
        {
            OnSessionInteract(hoveredSessionEntry);
        }
    }

    private bool HasInputFocus()
    {
        return InputFocusStack.HasFocus(this);
    }

    private bool CanProcessInteract()
    {
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
    }

    private void UpdateCursorTarget()
    {
        if (sharedCursor == null)
        {
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

    private void BindButtons()
    {
        if (newGameOptionButton != null)
        {
            newGameOptionButton.onClick.RemoveListener(OnNewGame);
            newGameOptionButton.onClick.AddListener(OnNewGame);
        }

        if (loadMenuButton != null)
        {
            loadMenuButton.onClick.RemoveListener(OnLoadMenuRequested);
            loadMenuButton.onClick.AddListener(OnLoadMenuRequested);
        }

        if (multiplayerButton != null)
        {
            multiplayerButton.onClick.RemoveListener(OnMultiplayerRequested);
            multiplayerButton.onClick.AddListener(OnMultiplayerRequested);
        }

        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveListener(OnOptionsRequested);
            optionsButton.onClick.AddListener(OnOptionsRequested);
        }

        if (quitOptionButton != null)
        {
            quitOptionButton.onClick.RemoveListener(OnQuit);
            quitOptionButton.onClick.AddListener(OnQuit);
        }

        if (newButton != null)
        {
            newButton.onClick.RemoveListener(OnNewGame);
            newButton.onClick.AddListener(OnNewGame);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(OnLoadSelected);
            loadButton.onClick.AddListener(OnLoadSelected);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(OnDeleteRequested);
            deleteButton.onClick.AddListener(OnDeleteRequested);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(OnRefresh);
            refreshButton.onClick.AddListener(OnRefresh);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuit);
            quitButton.onClick.AddListener(OnQuit);
        }

        if (confirmYesButton != null)
        {
            confirmYesButton.onClick.RemoveListener(ConfirmDelete);
            confirmYesButton.onClick.AddListener(ConfirmDelete);
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.onClick.RemoveListener(CancelDelete);
            confirmNoButton.onClick.AddListener(CancelDelete);
        }
    }

    private void UnbindButtons()
    {
        if (newGameOptionButton != null)
        {
            newGameOptionButton.onClick.RemoveListener(OnNewGame);
        }

        if (loadMenuButton != null)
        {
            loadMenuButton.onClick.RemoveListener(OnLoadMenuRequested);
        }

        if (multiplayerButton != null)
        {
            multiplayerButton.onClick.RemoveListener(OnMultiplayerRequested);
        }

        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveListener(OnOptionsRequested);
        }

        if (quitOptionButton != null)
        {
            quitOptionButton.onClick.RemoveListener(OnQuit);
        }

        if (newButton != null)
        {
            newButton.onClick.RemoveListener(OnNewGame);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(OnLoadSelected);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(OnDeleteRequested);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(OnRefresh);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuit);
        }

        if (confirmYesButton != null)
        {
            confirmYesButton.onClick.RemoveListener(ConfirmDelete);
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.onClick.RemoveListener(CancelDelete);
        }
    }

    private void RefreshSessions()
    {
        if (SaveSessionManager.Instance == null)
        {
            return;
        }

        SaveSessionManager.Instance.ReloadSessions();
        ClearSessionsUI();

        IReadOnlyList<SaveSessionInfo> sessions = SaveSessionManager.Instance.Sessions;
        if (sessions == null || sessions.Count == 0)
        {
            if (emptySessionsPlaceholder != null)
            {
                emptySessionsPlaceholder.SetActive(true);
            }
            selectedSession = null;
            return;
        }

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
            SelectSession(entryToSelect);
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

        SelectSession(entry);
    }

    private void SelectSession(MainMenuSessionEntryUI entry)
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

        for (int j = 0; j < session.saves.Count; j++)
        {
            SaveSlotInfo save = session.saves[j];
            if (save == null)
            {
                continue;
            }

            MainMenuSaveEntryUI saveEntry = Instantiate(saveEntryPrefab, savesRoot);
            saveEntry.Initialize(this, save, entryColor, entryHoverColor, entrySelectedColor);
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
        if (detailsBody == null || save == null)
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

        detailsBody.text =
            $"Sauvegarde: {save.saveName}\n" +
            $"Date: {(savedAt == DateTime.MinValue ? "Inconnue" : savedAt.ToString("dd/MM/yyyy HH:mm"))}\n" +
            $"Temps de jeu: {playtimeText}\n" +
            $"Scene: {save.sceneName}";

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
        if (SaveSessionManager.Instance == null)
        {
            return;
        }

        string sessionName = sessionNameInput != null ? sessionNameInput.text : string.Empty;
        SaveSessionInfo session = SaveSessionManager.Instance.CreateSession(sessionName);
        SaveSlotInfo save = SaveSessionManager.Instance.CreateSave(session.sessionId, "Depart");
        if (save == null)
        {
            SetStatus("Impossible de creer la sauvegarde.");
            return;
        }

        SaveSessionManager.Instance.SetActiveSave(session.sessionId, save.saveId);
        StartHostFlow();
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

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(gameplaySceneName);
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
