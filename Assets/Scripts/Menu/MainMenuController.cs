using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Menu principal (BG3-style) : liste des parties/sauvegardes + details.
public class MainMenuController : MonoBehaviour
{
    public const string DefaultMenuSceneName = "MainMenu";

    [Header("Scenes")]
    [SerializeField] private string menuSceneName = DefaultMenuSceneName;
    [SerializeField] private string gameplaySceneName = "OutdoorsScene";

    [Header("Session Codes")]
    [SerializeField] private int codeLength = 6;
    [SerializeField] private ushort basePort = 7000;
    [SerializeField] private ushort portRange = 1000;
    [SerializeField] private string hostLoopbackAddress = "127.0.0.1";
    [SerializeField] private string listenAddress = "0.0.0.0";
    [SerializeField] private string clientAddressDefault = "127.0.0.1";

    [Header("Layout")]
    [SerializeField] private Vector2 leftPanelSize = new Vector2(520f, 860f);
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.98f);
    [SerializeField] private Color panelColor = new Color(0.12f, 0.12f, 0.16f, 0.95f);
    [SerializeField] private Color entryColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color entryHoverColor = new Color(0.6f, 0.8f, 1f, 0.18f);
    [SerializeField] private Color entrySelectedColor = new Color(0.6f, 0.8f, 1f, 0.32f);

    private Canvas canvas;
    private GameObject root;
    private RectTransform leftContentRoot;
    private Text detailsTitle;
    private Text detailsBody;
    private Text statusText;
    private InputField sessionNameInput;
    private InputField hostCodeInput;
    private InputField joinCodeInput;
    private InputField addressInput;
    private Font defaultFont;

    private SaveSlotInfo hoveredSave;
    private SaveSlotInfo selectedSave;
    private SaveEntryView selectedSaveView;
    private bool menuVisible;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        EnsureSaveManager();
        BuildUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetMenuVisible(IsMenuScene(SceneManager.GetActiveScene().name));
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isMenu = IsMenuScene(scene.name);
        SetMenuVisible(isMenu);
        if (isMenu)
        {
            RefreshSessions();
        }
    }

    private bool IsMenuScene(string sceneName)
    {
        return string.Equals(sceneName, menuSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureSaveManager()
    {
        if (SaveSessionManager.Instance != null)
        {
            SaveSessionManager.Instance.SetMenuSceneName(menuSceneName);
            return;
        }

        GameObject host = new GameObject("SaveSessionManager");
        SaveSessionManager manager = host.AddComponent<SaveSessionManager>();
        manager.SetMenuSceneName(menuSceneName);
    }

    private void BuildUI()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();

        root = new GameObject("MainMenuUI", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = backgroundColor;
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject layoutRoot = new GameObject("LayoutRoot", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        layoutRoot.transform.SetParent(canvasObject.transform, false);
        RectTransform layoutRect = layoutRoot.GetComponent<RectTransform>();
        layoutRect.anchorMin = new Vector2(0.5f, 0.5f);
        layoutRect.anchorMax = new Vector2(0.5f, 0.5f);
        layoutRect.pivot = new Vector2(0.5f, 0.5f);
        layoutRect.sizeDelta = new Vector2(1400f, 860f);

        HorizontalLayoutGroup hLayout = layoutRoot.GetComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 24f;
        hLayout.childForceExpandHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childControlWidth = true;

        GameObject leftPanel = CreatePanel(layoutRoot.transform, "SessionsPanel", leftPanelSize);
        leftContentRoot = BuildSessionsPanel(leftPanel.transform);

        GameObject rightPanel = CreatePanel(layoutRoot.transform, "DetailsPanel", new Vector2(0f, 860f));
        BuildDetailsPanel(rightPanel.transform);

        RefreshSessions();
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 fixedSize)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = panelColor;

        RectTransform rect = panel.GetComponent<RectTransform>();
        if (fixedSize.x > 0f)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fixedSize.x);
        }

        if (fixedSize.y > 0f)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fixedSize.y);
        }

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return panel;
    }

    private RectTransform BuildSessionsPanel(Transform parent)
    {
        CreateLabel(parent, "Parties", 22, FontStyle.Bold, TextAnchor.MiddleLeft);

        GameObject scrollRoot = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollRoot.transform.SetParent(parent, false);
        Image scrollImage = scrollRoot.GetComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0.2f);

        ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollRoot.transform, false);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.05f);
        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(0f, 0f);
        viewportRect.offsetMax = new Vector2(0f, 0f);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vLayout = content.GetComponent<VerticalLayoutGroup>();
        vLayout.spacing = 6f;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        LayoutElement scrollLayout = scrollRoot.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;

        return contentRect;
    }

    private void BuildDetailsPanel(Transform parent)
    {
        detailsTitle = CreateLabel(parent, "Details", 22, FontStyle.Bold, TextAnchor.MiddleLeft);
        detailsBody = CreateLabel(parent, "Survole une sauvegarde pour voir les details.", 14, FontStyle.Normal, TextAnchor.UpperLeft);
        detailsBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        detailsBody.verticalOverflow = VerticalWrapMode.Overflow;

        CreateLabel(parent, "Nom de partie", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
        sessionNameInput = CreateInputField(parent, "Nouvelle partie", 32);

        CreateLabel(parent, "Code Host", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
        GameObject hostRow = CreateRow(parent);
        hostCodeInput = CreateInputField(hostRow.transform, "CODE", codeLength);
        CreateButton(hostRow.transform, "Generer", OnGenerateCode, 100f);

        CreateLabel(parent, "Code rejoindre", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
        joinCodeInput = CreateInputField(parent, "CODE", codeLength);

        CreateLabel(parent, "Adresse", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
        addressInput = CreateInputField(parent, clientAddressDefault, 64);
        if (addressInput != null)
        {
            addressInput.text = clientAddressDefault;
        }

        GameObject actionsRow = CreateRow(parent);
        CreateButton(actionsRow.transform, "Host nouvelle", OnHostNew, 140f);
        CreateButton(actionsRow.transform, "Host selection", OnHostSelected, 160f);
        CreateButton(actionsRow.transform, "Rejoindre", OnJoin, 120f);

        GameObject bottomRow = CreateRow(parent);
        CreateButton(bottomRow.transform, "Rafraichir", OnRefresh, 120f);
        CreateButton(bottomRow.transform, "Quitter", OnQuit, 100f);

        statusText = CreateLabel(parent, "Etat: menu", 12, FontStyle.Italic, TextAnchor.MiddleLeft);
    }

    private void RefreshSessions()
    {
        if (SaveSessionManager.Instance == null)
        {
            return;
        }

        SaveSessionManager.Instance.ReloadSessions();
        if (leftContentRoot == null)
        {
            return;
        }

        for (int i = leftContentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(leftContentRoot.GetChild(i).gameObject);
        }

        IReadOnlyList<SaveSessionInfo> sessions = SaveSessionManager.Instance.Sessions;
        if (sessions == null || sessions.Count == 0)
        {
            CreateLabel(leftContentRoot, "Aucune partie pour le moment.", 12, FontStyle.Italic, TextAnchor.MiddleLeft);
            return;
        }

        for (int i = 0; i < sessions.Count; i++)
        {
            SaveSessionInfo session = sessions[i];
            if (session == null)
            {
                continue;
            }

            GameObject sessionGroup = new GameObject("SessionGroup", typeof(RectTransform), typeof(VerticalLayoutGroup));
            sessionGroup.transform.SetParent(leftContentRoot, false);
            VerticalLayoutGroup sessionLayout = sessionGroup.GetComponent<VerticalLayoutGroup>();
            sessionLayout.spacing = 2f;
            sessionLayout.childControlWidth = true;
            sessionLayout.childControlHeight = true;
            sessionLayout.childForceExpandWidth = true;
            sessionLayout.childForceExpandHeight = false;

            GameObject header = CreateEntry(sessionGroup.transform, session.sessionName, 16, FontStyle.Bold);
            SessionEntryView headerView = header.AddComponent<SessionEntryView>();
            headerView.Initialize(this, session);

            GameObject savesRoot = new GameObject("SavesRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
            savesRoot.transform.SetParent(sessionGroup.transform, false);
            VerticalLayoutGroup savesLayout = savesRoot.GetComponent<VerticalLayoutGroup>();
            savesLayout.spacing = 2f;
            savesLayout.padding = new RectOffset(18, 0, 0, 0);
            savesLayout.childControlWidth = true;
            savesLayout.childControlHeight = true;
            savesLayout.childForceExpandWidth = true;
            savesLayout.childForceExpandHeight = false;

            headerView.SetSavesRoot(savesRoot);

            if (session.saves == null || session.saves.Count == 0)
            {
                CreateLabel(savesRoot.transform, "Aucune sauvegarde.", 12, FontStyle.Italic, TextAnchor.MiddleLeft);
                continue;
            }

            for (int j = 0; j < session.saves.Count; j++)
            {
                SaveSlotInfo save = session.saves[j];
                if (save == null)
                {
                    continue;
                }

                GameObject entry = CreateEntry(savesRoot.transform, save.saveName, 13, FontStyle.Normal);
                SaveEntryView entryView = entry.AddComponent<SaveEntryView>();
                entryView.Initialize(this, save);
            }
        }
    }

    private void OnGenerateCode()
    {
        string code = NetcodeSessionCode.Generate(codeLength);
        if (hostCodeInput != null)
        {
            hostCodeInput.text = code;
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.text = code;
        }

        SetStatus($"Code genere: {code}");
    }

    private void OnHostNew()
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

    private void OnHostSelected()
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

    private void StartHostFlow()
    {
        if (!TryResolvePort(hostCodeInput, out string code, out ushort port))
        {
            SetStatus("Code host invalide.");
            return;
        }

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

        SetMenuVisible(false);
        SetStatus($"Host lance (code {code}).");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
    }

    private void OnJoin()
    {
        if (!TryResolvePort(joinCodeInput, out _, out ushort port))
        {
            SetStatus("Code rejoindre invalide.");
            return;
        }

        string address = ResolveAddress();
        NetcodeLauncher launcher = ResolveLauncher();
        if (launcher == null)
        {
            SetStatus("NetcodeLauncher manquant.");
            return;
        }

        bool started = launcher.StartClientWithConnection(address, port);
        if (!started)
        {
            SetStatus("Client deja actif.");
            return;
        }

        SetMenuVisible(false);
        SetStatus($"Connexion a {address}:{port}.");
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

    private void SelectSave(SaveSlotInfo save, SaveEntryView view)
    {
        selectedSave = save;
        if (selectedSaveView != null)
        {
            selectedSaveView.SetSelected(false);
        }

        selectedSaveView = view;
        if (selectedSaveView != null)
        {
            selectedSaveView.SetSelected(true);
        }

        ShowSaveDetails(save);
    }

    public void ShowSaveDetails(SaveSlotInfo save)
    {
        hoveredSave = save;
        if (detailsBody == null || save == null)
        {
            return;
        }

        DateTime savedAt = save.savedAtUtcTicks > 0
            ? new DateTime(save.savedAtUtcTicks, DateTimeKind.Utc).ToLocalTime()
            : DateTime.MinValue;

        TimeSpan playtime = TimeSpan.FromSeconds(Mathf.Max(0f, save.playTimeSeconds));
        string playtimeText = $"{(int)playtime.TotalHours:00}:{playtime.Minutes:00}:{playtime.Seconds:00}";

        detailsTitle.text = save.sessionName;
        detailsBody.text =
            $"Sauvegarde: {save.saveName}\n" +
            $"Date: {(savedAt == DateTime.MinValue ? "Inconnue" : savedAt.ToString("dd/MM/yyyy HH:mm"))}\n" +
            $"Temps de jeu: {playtimeText}\n" +
            $"Scene: {save.sceneName}";
    }

    private void SetMenuVisible(bool visible)
    {
        menuVisible = visible;
        if (root != null)
        {
            root.SetActive(visible);
        }

        if (visible)
        {
            InputFocusStack.Push(this);
        }
        else
        {
            InputFocusStack.Pop(this);
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

    private bool TryResolvePort(InputField field, out string code, out ushort port)
    {
        port = 0;
        code = field != null ? NetcodeSessionCode.Normalize(field.text) : string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            code = NetcodeSessionCode.Generate(codeLength);
            if (field != null)
            {
                field.text = code;
            }
        }

        if (!NetcodeSessionCode.TryGetPort(code, basePort, portRange, out ushort resolvedPort, out _))
        {
            return false;
        }

        port = resolvedPort;
        return true;
    }

    private string ResolveAddress()
    {
        string address = addressInput != null ? addressInput.text : clientAddressDefault;
        if (string.IsNullOrWhiteSpace(address))
        {
            return clientAddressDefault;
        }

        return address.Trim();
    }

    private void SetStatus(string message)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = $"Etat: {message}";
    }

    private void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<EventSystem>() != null)
#else
        if (FindObjectOfType<EventSystem>() != null)
#endif
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        eventSystem.transform.SetParent(transform, false);
    }

    private GameObject CreateEntry(Transform parent, string text, int size, FontStyle style)
    {
        GameObject entry = new GameObject("Entry", typeof(RectTransform), typeof(Image), typeof(Button));
        entry.transform.SetParent(parent, false);
        Image image = entry.GetComponent<Image>();
        image.color = entryColor;

        Button button = entry.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        Text label = CreateLabel(entry.transform, text, size, style, TextAnchor.MiddleLeft);
        label.raycastTarget = false;

        LayoutElement element = entry.AddComponent<LayoutElement>();
        element.minHeight = 34f;

        return entry;
    }

    private GameObject CreateRow(Transform parent)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return row;
    }

    private Text CreateLabel(Transform parent, string text, int fontSize, FontStyle style, TextAnchor alignment)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        Text label = labelObject.GetComponent<Text>();
        label.font = defaultFont;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = Color.white;
        label.alignment = alignment;

        LayoutElement element = labelObject.AddComponent<LayoutElement>();
        element.minHeight = Mathf.Max(24f, fontSize + 6f);
        element.flexibleWidth = 1f;

        return label;
    }

    private InputField CreateInputField(Transform parent, string placeholderText, int characterLimit)
    {
        GameObject fieldObject = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField));
        fieldObject.transform.SetParent(parent, false);

        Image background = fieldObject.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.08f);

        InputField field = fieldObject.GetComponent<InputField>();
        field.contentType = InputField.ContentType.Standard;
        field.lineType = InputField.LineType.SingleLine;
        field.characterLimit = characterLimit;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(fieldObject.transform, false);
        Text text = textObject.GetComponent<Text>();
        text.font = defaultFont;
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;

        GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        placeholderObject.transform.SetParent(fieldObject.transform, false);
        Text placeholder = placeholderObject.GetComponent<Text>();
        placeholder.font = defaultFont;
        placeholder.fontSize = 14;
        placeholder.color = new Color(1f, 1f, 1f, 0.4f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.text = placeholderText;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 6f);
        textRect.offsetMax = new Vector2(-10f, -6f);

        RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(10f, 6f);
        placeholderRect.offsetMax = new Vector2(-10f, -6f);

        field.textComponent = text;
        field.placeholder = placeholder;

        LayoutElement element = fieldObject.AddComponent<LayoutElement>();
        element.minHeight = 32f;
        element.flexibleWidth = 1f;

        return field;
    }

    private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction callback, float preferredWidth)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.12f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        if (callback != null)
        {
            button.onClick.AddListener(callback);
        }

        Text text = CreateLabel(buttonObject.transform, label, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.raycastTarget = false;

        LayoutElement element = buttonObject.AddComponent<LayoutElement>();
        if (preferredWidth > 0f)
        {
            element.preferredWidth = preferredWidth;
            element.minWidth = preferredWidth;
        }
        element.minHeight = 32f;

        return button;
    }

    private class SessionEntryView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        private MainMenuController owner;
        private SaveSessionInfo session;
        private GameObject savesRoot;
        private Image background;
        private bool expanded = true;

        public void Initialize(MainMenuController menu, SaveSessionInfo data)
        {
            owner = menu;
            session = data;
            background = GetComponent<Image>();
        }

        public void SetSavesRoot(GameObject root)
        {
            savesRoot = root;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Toggle();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != null && owner.detailsTitle != null)
            {
                owner.detailsTitle.text = session != null ? session.sessionName : "Details";
            }
        }

        private void Toggle()
        {
            expanded = !expanded;
            if (savesRoot != null)
            {
                savesRoot.SetActive(expanded);
            }
        }
    }

    private class SaveEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private MainMenuController owner;
        private SaveSlotInfo save;
        private Image background;

        public void Initialize(MainMenuController menu, SaveSlotInfo data)
        {
            owner = menu;
            save = data;
            background = GetComponent<Image>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != null && save != null)
            {
                owner.ShowSaveDetails(save);
            }

            SetHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHover(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner != null)
            {
                owner.SelectSave(save, this);
            }
        }

        public void SetSelected(bool selected)
        {
            if (background == null)
            {
                return;
            }

            background.color = selected ? owner.entrySelectedColor : owner.entryColor;
        }

        private void SetHover(bool hover)
        {
            if (background == null)
            {
                return;
            }

            if (owner != null && owner.selectedSaveView == this)
            {
                background.color = owner.entrySelectedColor;
            }
            else
            {
                background.color = hover ? owner.entryHoverColor : owner.entryColor;
            }
        }

        private void OnDisable()
        {
            SetHover(false);
        }
    }
}
