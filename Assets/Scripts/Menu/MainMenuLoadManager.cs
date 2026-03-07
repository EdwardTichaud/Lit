using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// UI de gestion des sauvegardes (style BG3) integre au panel du menu principal.
public class MainMenuLoadManager : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string gameplaySceneName = "OutdoorsScene";

    [Header("Netcode")]
    [SerializeField] private int codeLength = 6;
    [SerializeField] private ushort basePort = 7000;
    [SerializeField] private ushort portRange = 1000;
    [SerializeField] private string hostLoopbackAddress = "127.0.0.1";
    [SerializeField] private string listenAddress = "0.0.0.0";

    [Header("Layout")]
    [SerializeField] private Vector2 leftPanelSize = new Vector2(520f, 0f);
    [SerializeField] private Color backgroundColor = new Color(0.06f, 0.06f, 0.08f, 0.96f);
    [SerializeField] private Color panelColor = new Color(0.12f, 0.12f, 0.16f, 0.95f);
    [SerializeField] private Color entryColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color entryHoverColor = new Color(0.6f, 0.8f, 1f, 0.18f);
    [SerializeField] private Color entrySelectedColor = new Color(0.6f, 0.8f, 1f, 0.32f);
    [SerializeField, Range(0.9f, 2f)] private float fontScale = 1.25f;
    [SerializeField, Range(0.75f, 2.5f)] private float layoutScale = 2f;

    [Header("Preview")]
    [SerializeField] private string screenshotFileName = "screenshot.png";

    private TMP_FontAsset defaultFont;
    private RectTransform leftContentRoot;
    private TMP_Text detailsTitle;
    private TMP_Text detailsBody;
    private RawImage previewImage;
    private AspectRatioFitter previewAspect;
    private Texture2D previewTexture;
    private TMP_Text statusText;
    private TMP_InputField sessionNameInput;
    private SaveSlotInfo selectedSave;
    private SaveEntryView selectedSaveView;
    private GameObject confirmRoot;
    private TMP_Text confirmText;
    private SaveSlotInfo pendingDelete;
    private bool built;

    private void Awake()
    {
        EnsureSaveManager();
        EnsureBuilt();
    }

    private void OnEnable()
    {
        EnsureBuilt();
        RefreshSessions();
    }

    private void OnDestroy()
    {
        ClearPreviewTexture();
    }

    private void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        built = true;
        defaultFont = TMP_Settings.defaultFontAsset;
        EnsureEventSystem();

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image background = GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }
        background.color = backgroundColor;

        GameObject stackRoot = new GameObject("StackRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
        stackRoot.transform.SetParent(transform, false);
        RectTransform stackRect = stackRoot.GetComponent<RectTransform>();
        stackRect.anchorMin = Vector2.zero;
        stackRect.anchorMax = Vector2.one;
        stackRect.offsetMin = new Vector2(ScaleLayout(36f), ScaleLayout(32f));
        stackRect.offsetMax = new Vector2(-ScaleLayout(36f), -ScaleLayout(32f));

        VerticalLayoutGroup stackLayout = stackRoot.GetComponent<VerticalLayoutGroup>();
        stackLayout.spacing = ScaleLayout(16f);
        stackLayout.childControlWidth = true;
        stackLayout.childControlHeight = true;
        stackLayout.childForceExpandWidth = true;
        stackLayout.childForceExpandHeight = false;

        CreateLabel(stackRoot.transform, "Charger une partie", 28, FontStyle.Bold, TextAnchor.MiddleLeft);

        GameObject contentRoot = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        contentRoot.transform.SetParent(stackRoot.transform, false);
        RectTransform contentRect = contentRoot.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        LayoutElement contentElement = contentRoot.AddComponent<LayoutElement>();
        contentElement.flexibleHeight = 1f;

        HorizontalLayoutGroup contentLayout = contentRoot.GetComponent<HorizontalLayoutGroup>();
        contentLayout.spacing = ScaleLayout(24f);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;

        GameObject leftPanel = CreatePanel(contentRoot.transform, "SessionsPanel", leftPanelSize);
        leftContentRoot = BuildSessionsPanel(leftPanel.transform);

        GameObject rightPanel = CreatePanel(contentRoot.transform, "DetailsPanel", new Vector2(0f, 0f));
        BuildDetailsPanel(rightPanel.transform);

        BuildFooter(stackRoot.transform);
        BuildConfirmationOverlay(transform);
    }

    private void BuildFooter(Transform parent)
    {
        CreateLabel(parent, "Nouvelle partie", 18, FontStyle.Bold, TextAnchor.MiddleLeft);

        GameObject nameRow = CreateRow(parent);
        CreateLabel(nameRow.transform, "Nom", 14, FontStyle.Normal, TextAnchor.MiddleLeft, ScaleLayout(80f));
        sessionNameInput = CreateInputField(nameRow.transform, "Nom de partie", 32);

        GameObject buttonsRow = CreateRow(parent);
        CreateButton(buttonsRow.transform, "Nouvelle", OnNewGame, ScaleLayout(120f));
        CreateButton(buttonsRow.transform, "Charger", OnLoadSelected, ScaleLayout(120f));
        CreateButton(buttonsRow.transform, "Supprimer", OnDeleteRequested, ScaleLayout(120f));
        CreateButton(buttonsRow.transform, "Rafraichir", OnRefresh, ScaleLayout(120f));
        CreateButton(buttonsRow.transform, "Quitter", OnQuit, ScaleLayout(100f));

        statusText = CreateLabel(parent, "Etat: menu", 12, FontStyle.Italic, TextAnchor.MiddleLeft);
    }

    private void BuildConfirmationOverlay(Transform parent)
    {
        confirmRoot = new GameObject("DeleteConfirm", typeof(RectTransform), typeof(Image));
        confirmRoot.transform.SetParent(parent, false);
        RectTransform overlayRect = confirmRoot.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = confirmRoot.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject dialog = new GameObject("Dialog", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        dialog.transform.SetParent(confirmRoot.transform, false);
        Image dialogImage = dialog.GetComponent<Image>();
        dialogImage.color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(ScaleLayout(520f), ScaleLayout(200f));

        VerticalLayoutGroup dialogLayout = dialog.GetComponent<VerticalLayoutGroup>();
        int dialogPad = ScaleLayoutInt(20);
        dialogLayout.padding = new RectOffset(dialogPad, dialogPad, dialogPad, dialogPad);
        dialogLayout.spacing = ScaleLayout(14f);
        dialogLayout.childControlWidth = true;
        dialogLayout.childControlHeight = true;
        dialogLayout.childForceExpandWidth = true;
        dialogLayout.childForceExpandHeight = false;

        confirmText = CreateLabel(dialog.transform, "Supprimer cette sauvegarde ?", 16, FontStyle.Bold, TextAnchor.MiddleCenter);
        if (confirmText != null)
        {
            confirmText.alignment = ToTMPAlignment(TextAnchor.MiddleCenter);
        }

        GameObject buttonsRow = CreateRow(dialog.transform);
        HorizontalLayoutGroup rowLayout = buttonsRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;

        Button confirmButton = CreateButton(buttonsRow.transform, "Supprimer", ConfirmDelete, 140f);
        if (confirmButton != null)
        {
            Image buttonImage = confirmButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = new Color(0.7f, 0.2f, 0.2f, 0.85f);
            }
        }

        CreateButton(buttonsRow.transform, "Annuler", CancelDelete, 140f);

        confirmRoot.SetActive(false);
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 fixedSize)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
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

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        int pad = ScaleLayoutInt(20);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.spacing = ScaleLayout(12f);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return panel;
    }

    private RectTransform BuildSessionsPanel(Transform parent)
    {
        CreateLabel(parent, "Parties", 20, FontStyle.Bold, TextAnchor.MiddleLeft);

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
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vLayout = content.GetComponent<VerticalLayoutGroup>();
        vLayout.spacing = ScaleLayout(6f);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        LayoutElement scrollElement = scrollRoot.AddComponent<LayoutElement>();
        scrollElement.flexibleHeight = 1f;

        return contentRect;
    }

    private void BuildDetailsPanel(Transform parent)
    {
        detailsTitle = CreateLabel(parent, "Details", 20, FontStyle.Bold, TextAnchor.MiddleLeft);

        GameObject preview = new GameObject("Preview", typeof(RectTransform), typeof(Image));
        preview.transform.SetParent(parent, false);
        Image previewBackground = preview.GetComponent<Image>();
        previewBackground.color = new Color(0f, 0f, 0f, 0.35f);
        RectTransform previewRect = preview.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0f, 1f);
        previewRect.anchorMax = new Vector2(1f, 1f);
        previewRect.pivot = new Vector2(0.5f, 1f);
        previewRect.sizeDelta = new Vector2(0f, ScaleLayout(180f));

        LayoutElement previewElement = preview.AddComponent<LayoutElement>();
        previewElement.minHeight = ScaleLayout(180f);

        GameObject previewTextureObject = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        previewTextureObject.transform.SetParent(preview.transform, false);
        RectTransform previewTextureRect = previewTextureObject.GetComponent<RectTransform>();
        previewTextureRect.anchorMin = Vector2.zero;
        previewTextureRect.anchorMax = Vector2.one;
        previewTextureRect.offsetMin = new Vector2(ScaleLayout(8f), ScaleLayout(8f));
        previewTextureRect.offsetMax = new Vector2(-ScaleLayout(8f), -ScaleLayout(8f));

        previewImage = previewTextureObject.GetComponent<RawImage>();
        previewImage.texture = null;
        previewImage.color = Color.white;
        previewImage.raycastTarget = false;

        previewAspect = previewTextureObject.GetComponent<AspectRatioFitter>();
        previewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        previewAspect.aspectRatio = 16f / 9f;

        detailsBody = CreateLabel(parent, "Selectionne une sauvegarde.", 14, FontStyle.Normal, TextAnchor.UpperLeft);
        if (detailsBody != null)
        {
            detailsBody.alignment = ToTMPAlignment(TextAnchor.UpperLeft);
        }
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

        selectedSave = null;
        if (selectedSaveView != null)
        {
            selectedSaveView.SetSelected(false);
            selectedSaveView = null;
        }

        ClearPreviewTexture();
        if (detailsTitle != null)
        {
            detailsTitle.text = "Details";
        }
        if (detailsBody != null)
        {
            detailsBody.text = "Selectionne une sauvegarde.";
        }
        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
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
            savesLayout.spacing = ScaleLayout(2f);
            savesLayout.padding = new RectOffset(ScaleLayoutInt(18), 0, 0, 0);
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
            Debug.LogWarning($"MainMenuLoadManager: echec chargement screenshot {path}. {ex.Message}");
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

    private void SetStatus(string message)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = $"Etat: {message}";
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

        TMP_Text label = CreateLabel(entry.transform, text, size, style, TextAnchor.MiddleLeft);
        label.raycastTarget = false;

        LayoutElement element = entry.AddComponent<LayoutElement>();
        element.minHeight = ScaleLayout(34f);

        return entry;
    }

    private GameObject CreateRow(Transform parent)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = ScaleLayout(8f);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return row;
    }

    private TMP_Text CreateLabel(Transform parent, string text, int fontSize, FontStyle style, TextAnchor alignment)
    {
        return CreateLabel(parent, text, fontSize, style, alignment, 0f);
    }

    private TMP_Text CreateLabel(Transform parent, string text, int fontSize, FontStyle style, TextAnchor alignment, float preferredWidth)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        if (defaultFont != null)
        {
            label.font = defaultFont;
        }
        label.text = text;
        label.fontSize = ScaleFont(fontSize);
        label.fontStyle = ToTMPFontStyle(style);
        label.color = Color.white;
        label.alignment = ToTMPAlignment(alignment);

        LayoutElement element = labelObject.AddComponent<LayoutElement>();
        element.minHeight = Mathf.Max(ScaleLayout(24f), ScaleFont(fontSize) + ScaleLayout(6f));
        element.flexibleWidth = 1f;
        if (preferredWidth > 0f)
        {
            element.preferredWidth = preferredWidth;
            element.minWidth = preferredWidth;
        }

        return label;
    }

    private TMP_InputField CreateInputField(Transform parent, string placeholderText, int characterLimit)
    {
        GameObject fieldObject = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        fieldObject.transform.SetParent(parent, false);

        Image background = fieldObject.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.08f);

        TMP_InputField field = fieldObject.GetComponent<TMP_InputField>();
        field.contentType = TMP_InputField.ContentType.Standard;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = characterLimit;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(fieldObject.transform, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (defaultFont != null)
        {
            text.font = defaultFont;
        }
        text.fontSize = ScaleFont(14);
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;

        GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderObject.transform.SetParent(fieldObject.transform, false);
        TMP_Text placeholder = placeholderObject.GetComponent<TMP_Text>();
        if (defaultFont != null)
        {
            placeholder.font = defaultFont;
        }
        placeholder.fontSize = ScaleFont(14);
        placeholder.color = new Color(1f, 1f, 1f, 0.4f);
        placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.text = placeholderText;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(ScaleLayout(10f), ScaleLayout(6f));
        textRect.offsetMax = new Vector2(-ScaleLayout(10f), -ScaleLayout(6f));

        RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(ScaleLayout(10f), ScaleLayout(6f));
        placeholderRect.offsetMax = new Vector2(-ScaleLayout(10f), -ScaleLayout(6f));

        field.textComponent = text;
        field.placeholder = placeholder;

        LayoutElement element = fieldObject.AddComponent<LayoutElement>();
        element.minHeight = ScaleLayout(32f);
        element.flexibleWidth = 1f;

        return field;
    }

    private int ScaleFont(int size)
    {
        return Mathf.Max(10, Mathf.RoundToInt(size * 2f * Mathf.Max(0.5f, fontScale)));
    }

    private float ScaleLayout(float value)
    {
        return value * Mathf.Max(0.5f, layoutScale);
    }

    private int ScaleLayoutInt(float value)
    {
        return Mathf.RoundToInt(ScaleLayout(value));
    }

    private static FontStyles ToTMPFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold:
                return FontStyles.Bold;
            case FontStyle.Italic:
                return FontStyles.Italic;
            case FontStyle.BoldAndItalic:
                return FontStyles.Bold | FontStyles.Italic;
            default:
                return FontStyles.Normal;
        }
    }

    private static TextAlignmentOptions ToTMPAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft:
                return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:
                return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:
                return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:
                return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter:
                return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight:
                return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft:
                return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:
                return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:
                return TextAlignmentOptions.BottomRight;
            default:
                return TextAlignmentOptions.Left;
        }
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

        TMP_Text text = CreateLabel(buttonObject.transform, label, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.raycastTarget = false;

        LayoutElement element = buttonObject.AddComponent<LayoutElement>();
        if (preferredWidth > 0f)
        {
            element.preferredWidth = preferredWidth;
            element.minWidth = preferredWidth;
        }
        element.minHeight = ScaleLayout(32f);

        return button;
    }

    private static void EnsureSaveManager()
    {
        if (SaveSessionManager.Instance != null)
        {
            SaveSessionManager.Instance.SetMenuSceneName(MainMenuController.DefaultMenuSceneName);
            return;
        }

        GameObject host = new GameObject("SaveSessionManager");
        SaveSessionManager manager = host.AddComponent<SaveSessionManager>();
        manager.SetMenuSceneName(MainMenuController.DefaultMenuSceneName);
    }

    private class SessionEntryView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        private MainMenuLoadManager owner;
        private SaveSessionInfo session;
        private GameObject savesRoot;

        public void Initialize(MainMenuLoadManager menu, SaveSessionInfo data)
        {
            owner = menu;
            session = data;
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
            if (savesRoot != null)
            {
                savesRoot.SetActive(!savesRoot.activeSelf);
            }
        }
    }

    private class SaveEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private MainMenuLoadManager owner;
        private SaveSlotInfo save;
        private Image background;

        public void Initialize(MainMenuLoadManager menu, SaveSlotInfo data)
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
            if (background == null || owner == null)
            {
                return;
            }

            background.color = selected ? owner.entrySelectedColor : owner.entryColor;
        }

        private void SetHover(bool hover)
        {
            if (background == null || owner == null)
            {
                return;
            }

            if (owner.selectedSaveView == this)
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
