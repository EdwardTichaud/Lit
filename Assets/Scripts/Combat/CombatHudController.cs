using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CombatHudController : MonoBehaviour
{
    public enum TurnState
    {
        None = 0,
        Enemy = 1,
        Player = 2,
        Finished = 3
    }

    public static CombatHudController Instance { get; private set; }

    private GameObject root;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI playerHpText;
    private TextMeshProUGUI enemyHpText;
    private TextMeshProUGUI prayerText;
    private TextMeshProUGUI messageText;
    private TextMeshProUGUI actionsText;

    private string activeSessionId;
    private TurnState currentTurn;
    private float timerEndsAt;
    private bool playerActionLocked;
    private bool visible;

    public static CombatHudController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindFirstObjectByType<CombatHudController>();
#else
        Instance = FindObjectOfType<CombatHudController>();
#endif
        if (Instance != null)
        {
            return Instance;
        }

        GameObject host = new GameObject("CombatHudController");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<CombatHudController>();
        return Instance;
    }

    public static void HideActive(string sessionId)
    {
        if (Instance == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sessionId) && Instance.activeSessionId != sessionId)
        {
            return;
        }

        Instance.Hide();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (gameObject.name == "CombatHudController")
        {
            DontDestroyOnLoad(gameObject);
        }

        BuildUi();
    }

    private void OnDestroy()
    {
        if (root != null)
        {
            Destroy(root);
            root = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteract;
        LocalInputRouter.RightShoulder += OnRightShoulder;
        LocalInputRouter.Return += OnReturn;
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteract;
        LocalInputRouter.RightShoulder -= OnRightShoulder;
        LocalInputRouter.Return -= OnReturn;
    }

    private void Update()
    {
        if (!visible)
        {
            return;
        }

        UpdateTimerText();
    }

    public void ShowSnapshot(
        string sessionId,
        TurnState turn,
        float timerRemaining,
        int playerHp,
        int playerMaxHp,
        string enemyName,
        int enemyHp,
        int enemyMaxHp,
        int aliveEnemies,
        int totalEnemies,
        int prayerSupportCount,
        float damageReduction,
        bool actionLocked,
        string message)
    {
        BuildUi();
        activeSessionId = sessionId;
        currentTurn = turn;
        timerEndsAt = Time.unscaledTime + Mathf.Max(0f, timerRemaining);
        playerActionLocked = actionLocked;
        visible = turn != TurnState.None && turn != TurnState.Finished;

        if (root != null)
        {
            root.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        titleText.text = "Combat";
        turnText.text = turn == TurnState.Player ? "Tour joueur" : "Tour ennemi";
        playerHpText.text = $"Joueur: {playerHp}/{Mathf.Max(1, playerMaxHp)} PV";
        enemyHpText.text = $"{enemyName}: {enemyHp}/{Mathf.Max(1, enemyMaxHp)} PV ({aliveEnemies}/{Mathf.Max(1, totalEnemies)})";
        prayerText.text = prayerSupportCount > 0
            ? $"Prieres de soutien: {prayerSupportCount} (-{Mathf.RoundToInt(damageReduction * 100f)}% degats)"
            : "Prieres de soutien: 0";
        messageText.text = message ?? string.Empty;
        actionsText.text = turn == TurnState.Player
            ? playerActionLocked
                ? "Attaque de base en cours..."
                : "RightShoulder: attaque de base | Inventaire: utiliser un item | Retour: passer"
            : "Attente de l'action ennemie";
        UpdateTimerText();
    }

    private void Hide()
    {
        activeSessionId = null;
        currentTurn = TurnState.None;
        playerActionLocked = false;
        visible = false;
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!CanSendPlayerAction())
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();
        CombatSessionManager.EnsureInstance()?.RequestLocalPlayerAttack();
    }

    private void OnRightShoulder(InputAction.CallbackContext context)
    {
        if (!CanSendPlayerAction())
        {
            return;
        }

        CombatSessionManager.EnsureInstance()?.RequestLocalPlayerAttack();
    }

    private void OnReturn(InputAction.CallbackContext context)
    {
        if (!CanSendPlayerAction())
        {
            return;
        }

        CombatSessionManager.EnsureInstance()?.RequestLocalPlayerPass();
    }

    private bool CanSendPlayerAction()
    {
        return visible
            && currentTurn == TurnState.Player
            && !playerActionLocked
            && !InputFocusStack.HasAnyFocus()
            && CombatSessionManager.EnsureInstance() != null;
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
        {
            return;
        }

        float remaining = Mathf.Max(0f, timerEndsAt - Time.unscaledTime);
        timerText.text = $"Timer: {Mathf.CeilToInt(remaining)}s";
    }

    private void BuildUi()
    {
        if (root != null)
        {
            return;
        }

        root = new GameObject("CombatHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(root);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        panelRect.sizeDelta = new Vector2(430f, 250f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.03f, 0.035f, 0.04f, 0.88f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 5f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        titleText = CreateText(panel.transform, "Title", 24, FontStyles.Bold);
        turnText = CreateText(panel.transform, "Turn", 18, FontStyles.Bold);
        timerText = CreateText(panel.transform, "Timer", 18, FontStyles.Normal);
        playerHpText = CreateText(panel.transform, "PlayerHp", 17, FontStyles.Normal);
        enemyHpText = CreateText(panel.transform, "EnemyHp", 17, FontStyles.Normal);
        prayerText = CreateText(panel.transform, "Prayer", 16, FontStyles.Normal);
        messageText = CreateText(panel.transform, "Message", 16, FontStyles.Italic);
        actionsText = CreateText(panel.transform, "Actions", 15, FontStyles.Normal);

        root.SetActive(false);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, float size, FontStyles style)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Left;
        return text;
    }
}
