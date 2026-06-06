using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// UI runtime simple pour host/join via un code de session.
public class NetcodeLobbyUI : MonoBehaviour
{
    public static NetcodeLobbyUI Instance { get; private set; }

    [Header("Auto UI")]
    [SerializeField] private bool autoCreateUI = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Session Code")]
    [SerializeField] private int codeLength = 6;
    [SerializeField] private ushort basePort = 7000;
    [SerializeField] private ushort portRange = 1000;

    [Header("Address")]
    [SerializeField] private string hostLoopbackAddress = "127.0.0.1";
    [SerializeField] private string listenAddress = "0.0.0.0";
    [SerializeField] private bool fetchPublicIp = true;
    [SerializeField] private string publicIpServiceUrl = "https://api.ipify.org";
    [SerializeField] private string publicIpLabelFormat = "IP publique: {0}";

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(420f, 520f);
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color fieldColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color buttonColor = new Color(1f, 1f, 1f, 0.18f);

    private InputField hostCodeInput;
    private Text statusText;
    private Text portText;
    private Text publicIpText;
    private string publicIpValue;
    private GameObject panelRoot;
    private GameObject canvasRoot;
    private Font defaultFont;
    private NetcodeLauncher launcher;
    private string currentHostCode;
    private string lastStatus;
    private bool uiVisible = true;
    private bool inputLocked;
    private bool publicIpRequested;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        launcher = GetComponent<NetcodeLauncher>();
        if (autoCreateUI)
        {
            BuildUI();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (uiVisible)
        {
            SetUIVisible(false);
        }

        if (canvasRoot != null)
        {
            Destroy(canvasRoot);
        }
        else if (panelRoot != null)
        {
            Destroy(panelRoot);
        }
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Multi += OnMultiPerformed;
        RegisterTransportFailureCallback(true);
    }

    private void OnDisable()
    {
        LocalInputRouter.Multi -= OnMultiPerformed;
        RegisterTransportFailureCallback(false);
    }

    private void Update()
    {
        UpdateStatus();
        EnsureInputLock();
    }

    private void BuildUI()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();

        canvasRoot = new GameObject("LobbyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panelRoot = new GameObject("LobbyPanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(canvasRoot.transform, false);
        Image panelImage = panelRoot.GetComponent<Image>();
        panelImage.color = panelColor;

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;

        VerticalLayoutGroup vertical = panelRoot.AddComponent<VerticalLayoutGroup>();
        vertical.childControlHeight = true;
        vertical.childControlWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.childForceExpandWidth = true;
        vertical.spacing = 10f;
        vertical.padding = new RectOffset(16, 16, 16, 16);

        ContentSizeFitter fitter = panelRoot.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateLabel(panelRoot.transform, "Multijoueur", 20, FontStyle.Bold, TextAnchor.MiddleCenter);

        GameObject hostRow = CreateRow(panelRoot.transform);
        CreateLabel(hostRow.transform, "Code host", 14, FontStyle.Normal, TextAnchor.MiddleLeft, 110f);
        hostCodeInput = CreateInputField(hostRow.transform, "CODE", codeLength, InputField.ContentType.Alphanumeric);
        CreateButton(hostRow.transform, "Generer", OnGenerateClicked, 90f);
        CreateButton(hostRow.transform, "Copier", OnCopyClicked, 70f);
        SetupCodeField(hostCodeInput);
        currentHostCode = ResolveOrGenerateHostCode();
        hostCodeInput.text = currentHostCode;
        UpdatePortDisplay(currentHostCode);

        GameObject buttonsRow = CreateRow(panelRoot.transform);
        CreateButton(buttonsRow.transform, "Host", OnHostClicked, 100f);

        portText = CreateLabel(panelRoot.transform, "Port: -", 12, FontStyle.Italic, TextAnchor.MiddleLeft);

        GameObject ipRow = CreateRow(panelRoot.transform);
        publicIpText = CreateLabel(ipRow.transform, string.Format(publicIpLabelFormat, "..."), 12, FontStyle.Normal, TextAnchor.MiddleLeft);
        LayoutElement ipLayout = publicIpText.GetComponent<LayoutElement>();
        if (ipLayout != null)
        {
            ipLayout.flexibleWidth = 1f;
        }
        CreateButton(ipRow.transform, "Copier code", OnCopyPublicIpClicked, 95f);

        statusText = CreateLabel(panelRoot.transform, "Etat: offline", 12, FontStyle.Normal, TextAnchor.MiddleLeft);

        StartPublicIpFetch();
        SetUIVisible(true);
    }

    private void StartPublicIpFetch()
    {
        if (!fetchPublicIp || publicIpRequested)
        {
            return;
        }

        publicIpRequested = true;
        StartCoroutine(FetchPublicIp());
    }

    private System.Collections.IEnumerator FetchPublicIp()
    {
        if (string.IsNullOrWhiteSpace(publicIpServiceUrl))
        {
            SetPublicIpLabel("indisponible");
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(publicIpServiceUrl))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                string ip = request.downloadHandler.text != null ? request.downloadHandler.text.Trim() : string.Empty;
                SetPublicIpLabel(ip);
            }
            else
            {
                SetPublicIpLabel("indisponible");
            }
        }
    }

    private void SetPublicIpLabel(string ip)
    {
        if (publicIpText == null)
        {
            return;
        }

        string value = string.IsNullOrWhiteSpace(ip) ? "indisponible" : ip;
        publicIpValue = value == "indisponible" ? string.Empty : value;
        publicIpText.text = string.Format(publicIpLabelFormat, value);
    }

    private void OnGenerateClicked()
    {
        string code = NetcodeSessionCode.Generate(codeLength);
        if (hostCodeInput != null)
        {
            hostCodeInput.SetTextWithoutNotify(code);
        }

        currentHostCode = code;
        UpdatePortDisplay(code);
    }

    private void OnCopyClicked()
    {
        if (!TryBuildCurrentJoinCode(out string joinCode, out string sessionCode, out string address))
        {
            sessionCode = ResolveOrGenerateHostCode();
            if (hostCodeInput != null)
            {
                hostCodeInput.SetTextWithoutNotify(sessionCode);
            }

            joinCode = NetcodeSessionCode.CreateJoinCode(sessionCode, ResolveAdvertisedAddress());
            address = ResolveAdvertisedAddress();
        }

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            SetStatus("Code d'invitation indisponible.");
            return;
        }

        GUIUtility.systemCopyBuffer = joinCode;
        currentHostCode = sessionCode;
        UpdatePortDisplay(sessionCode);
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsHost)
        {
            SetStatus($"Code d'invitation copie: {joinCode}");
            return;
        }

        SetStatus($"Code d'invitation copie pour {address}. Clique sur Host pour ouvrir la session.");
    }

    private void OnHostClicked()
    {
        if (!TryResolveHostEndpoint(out NetcodeSessionEndpoint endpoint))
        {
            SetStatus("Code host invalide.");
            return;
        }

        NetcodeLauncher resolved = ResolveLauncher();
        if (resolved == null)
        {
            SetStatus("NetcodeLauncher manquant.");
            return;
        }

        currentHostCode = endpoint.Code;
        UpdatePortDisplay(endpoint.Code);
        bool started = resolved.StartHostWithSessionEndpoint(endpoint);
        string listenLabel = $"{resolved.SessionListenAddress}:{endpoint.Port}";
        string joinCode = NetcodeSessionCode.CreateJoinCode(endpoint.Code, ResolveAdvertisedAddress());
        if (started && !string.IsNullOrWhiteSpace(joinCode))
        {
            GUIUtility.systemCopyBuffer = joinCode;
        }

        SetStatus(started
            ? $"Host lance: code d'invitation copie {joinCode}. Ecoute {listenLabel}."
            : "Host deja actif.");
        if (started)
        {
            SetUIVisible(false);
        }
    }

    private void RegisterTransportFailureCallback(bool enabled)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            return;
        }

        manager.OnTransportFailure -= OnTransportFailure;
        if (enabled)
        {
            manager.OnTransportFailure += OnTransportFailure;
        }
    }

    private void OnTransportFailure()
    {
        NetworkManager manager = NetworkManager.Singleton;
        string reason = manager != null ? manager.DisconnectReason : string.Empty;
        if (string.IsNullOrWhiteSpace(reason))
        {
            NetcodeLauncher resolved = ResolveLauncher();
            if (resolved != null && resolved.TryGetLastConnectionAttempt(out NetcodeConnectionAttemptInfo attempt))
            {
                reason = $"Le transport reseau s'est arrete sur {attempt.ListenLabel}. Verifie que le port est libre et que le pare-feu n'a pas bloque l'application.";
            }
            else
            {
                reason = "Le transport reseau s'est arrete. Verifie que le port est libre et que le pare-feu n'a pas bloque l'application.";
            }
        }

        SetStatus(reason);
        if (uiVisible)
        {
            return;
        }

        SetUIVisible(true);
    }

    private void OnCopyPublicIpClicked()
    {
        if (!TryBuildCurrentJoinCode(out string joinCode, out _, out string address))
        {
            SetStatus("Code d'invitation indisponible.");
            return;
        }

        GUIUtility.systemCopyBuffer = joinCode;
        SetStatus($"Code d'invitation copie pour {address}: {joinCode}");
    }

    private void OnMultiPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        ToggleUI();
    }

    private void ToggleUI()
    {
        SetUIVisible(!uiVisible);
    }

    private void SetupCodeField(InputField field)
    {
        if (field == null)
        {
            return;
        }

        field.onValueChanged.AddListener(value =>
        {
            string normalized = NetcodeSessionCode.Normalize(value);
            field.SetTextWithoutNotify(normalized);
            UpdatePortDisplay(normalized);
        });
    }

    private bool TryResolveHostEndpoint(out NetcodeSessionEndpoint endpoint)
    {
        string code = ResolveCodeFromField(hostCodeInput);
        NetcodeLauncher resolved = ResolveLauncher();
        if (resolved != null)
        {
            return resolved.TryResolveHostEndpoint(code, out endpoint, out _);
        }

        string address = NetcodeSessionCode.NormalizeAddress(hostLoopbackAddress, "127.0.0.1");
        return NetcodeSessionCode.TryCreateEndpoint(code, address, basePort, portRange, out endpoint);
    }

    private string ResolveCodeFromField(InputField field)
    {
        if (field == null)
        {
            return string.Empty;
        }

        return NetcodeSessionCode.Normalize(field.text);
    }

    private bool TryBuildCurrentJoinCode(out string joinCode, out string sessionCode, out string address)
    {
        joinCode = string.Empty;
        sessionCode = ResolveCodeFromField(hostCodeInput);
        address = ResolveAdvertisedAddress();
        if (string.IsNullOrWhiteSpace(sessionCode) || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        joinCode = NetcodeSessionCode.CreateJoinCode(sessionCode, address);
        return !string.IsNullOrWhiteSpace(joinCode);
    }

    private string ResolveAdvertisedAddress()
    {
        if (!string.IsNullOrWhiteSpace(publicIpValue))
        {
            return publicIpValue;
        }

        NetcodeLauncher resolved = ResolveLauncher();
        if (resolved != null)
        {
            return resolved.SessionDefaultJoinAddress;
        }

        return NetcodeSessionCode.NormalizeAddress(hostLoopbackAddress, "127.0.0.1");
    }

    private string ResolveOrGenerateHostCode()
    {
        string code = NetcodeSessionCode.Normalize(currentHostCode);
        if (!string.IsNullOrEmpty(code))
        {
            return code;
        }

        currentHostCode = NetcodeSessionCode.Generate(codeLength);
        return currentHostCode;
    }

    private NetcodeLauncher ResolveLauncher()
    {
        if (launcher != null)
        {
            return launcher;
        }

#if UNITY_2023_1_OR_NEWER
        launcher = FindAnyObjectByType<NetcodeLauncher>();
#else
        launcher = FindAnyObjectByType<NetcodeLauncher>();
#endif
        return launcher;
    }

    private void UpdatePortDisplay(string code)
    {
        if (portText == null)
        {
            return;
        }

        NetcodeLauncher resolved = ResolveLauncher();
        ushort port;
        bool gotPort = resolved != null
            ? resolved.TryResolveSessionPort(code, out port, out _)
            : NetcodeSessionCode.TryGetPort(code, basePort, portRange, out port, out _);
        if (!gotPort)
        {
            portText.text = "Port: -";
            return;
        }

        portText.text = $"Port: {port}";
    }

    private void UpdateStatus()
    {
        if (statusText == null)
        {
            return;
        }

        string status;
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
        {
            status = "offline";
            if (!uiVisible)
            {
                SetUIVisible(true);
            }
        }
        else if (manager.IsHost)
        {
            status = "host";
        }
        else if (manager.IsServer)
        {
            status = "serveur";
        }
        else
        {
            status = "client";
        }

        if (status != lastStatus)
        {
            lastStatus = status;
            statusText.text = $"Etat: {status}";
        }
    }

    private void SetStatus(string message)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = $"Etat: {message}";
        lastStatus = message;
    }

    private void SetUIVisible(bool visible)
    {
        uiVisible = visible;
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }

        if (visible)
        {
            InputFocusStack.Push(this);
            LocalInputRouter.ResetMove();
            EnsureInputLock();
        }
        else
        {
            InputFocusStack.Pop(this);
            if (inputLocked && SquadManager.Instance != null)
            {
                SquadManager.Instance.SetInputLocked(false);
                inputLocked = false;
            }
        }
    }

    private void EnsureInputLock()
    {
        if (!uiVisible || inputLocked)
        {
            return;
        }

        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.SetInputLocked(true);
            inputLocked = true;
        }
    }

    private void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindAnyObjectByType<EventSystem>() != null)
#else
        if (FindAnyObjectByType<EventSystem>() != null)
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

    private GameObject CreateRow(Transform parent)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 8f;

        LayoutElement element = row.AddComponent<LayoutElement>();
        element.minHeight = 36f;

        return row;
    }

    private Text CreateLabel(Transform parent, string text, int fontSize, FontStyle style, TextAnchor alignment, float preferredWidth = -1f)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        Text label = labelObject.GetComponent<Text>();
        label.text = text;
        label.font = defaultFont;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = Color.white;
        label.alignment = alignment;

        LayoutElement element = labelObject.AddComponent<LayoutElement>();
        if (preferredWidth > 0f)
        {
            element.preferredWidth = preferredWidth;
            element.minWidth = preferredWidth;
        }

        element.minHeight = Mathf.Max(24f, fontSize + 6f);

        return label;
    }

    private InputField CreateInputField(Transform parent, string placeholderText, int characterLimit, InputField.ContentType contentType)
    {
        GameObject fieldObject = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField));
        fieldObject.transform.SetParent(parent, false);

        Image background = fieldObject.GetComponent<Image>();
        background.color = fieldColor;

        InputField field = fieldObject.GetComponent<InputField>();
        field.contentType = contentType;
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
        element.preferredHeight = 32f;
        element.flexibleWidth = 1f;

        return field;
    }

    private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction callback, float preferredWidth)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = buttonColor;

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
}
