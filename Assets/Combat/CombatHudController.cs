using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Role: affiche le HUD de combat et route les inputs simples d'action.
// Usage: appele par CombatSessionManager via snapshots locaux ou reseau.
// Responsibilities: afficher PV/tour/timer/message, montrer le bouton attaque, transmettre les actions joueur.
// Dependencies: CombatSessionManager, LocalInputRouter, TMP, Unity UI.
// Precautions: ce script peut utiliser une UI de scene ou une UI fallback; eviter de renommer les objets attendus.
/// <summary>
/// Controleur singleton du HUD de combat.
/// </summary>
public class CombatHudController : MonoBehaviour
{
    private const string DefaultBattlePanelName = "BattlePanel";
    private const string DefaultBaseAttackUiName = "BaseAttackUI";

    /// <summary>
    /// Etat de tour converti pour l'affichage local du HUD.
    /// </summary>
    public enum TurnState
    {
        /// <summary>Aucun combat actif.</summary>
        None = 0,
        /// <summary>Tour ennemi affiche.</summary>
        Enemy = 1,
        /// <summary>Tour joueur affiche.</summary>
        Player = 2,
        /// <summary>Combat termine.</summary>
        Finished = 3
    }

    /// <summary>
    /// Instance singleton du HUD de combat.
    /// </summary>
    public static CombatHudController Instance { get; private set; }

    /// <summary>
    /// Vrai si le HUD de combat detient actuellement le focus d'input local.
    /// </summary>
    public static bool HasCombatInputFocus => Instance != null && Instance.combatFocusPushed && InputFocusStack.HasFocus(Instance);

    [Header("Scene UI")]
    /// <summary>
    /// Autorise la creation d'une UI runtime si aucune UI de scene n'est trouvee.
    /// </summary>
    [SerializeField] private bool allowRuntimeFallback;
    /// <summary>
    /// CanvasGroup du panneau principal de combat dans la scene.
    /// </summary>
    [SerializeField] private CanvasGroup battlePanelCanvasGroup;
    /// <summary>
    /// CanvasGroup de l'action attaque de base.
    /// </summary>
    [SerializeField] private CanvasGroup baseAttackCanvasGroup;
    [SerializeField] private Image playerHpFillImage;
    [SerializeField] private Image enemyHpFillImage;
    [SerializeField] private Image timerFillImage;
    [SerializeField] private TextMeshProUGUI baseAttackText;

    private GameObject root;

    [Header("Scene Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI enemyHpText;
    [SerializeField] private TextMeshProUGUI prayerText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI actionsText;

    private TextMeshProUGUI runtimeTitleText;
    private TextMeshProUGUI runtimeTurnText;
    private TextMeshProUGUI runtimeTimerText;
    private TextMeshProUGUI runtimePlayerHpText;
    private TextMeshProUGUI runtimeEnemyHpText;
    private TextMeshProUGUI runtimePrayerText;
    private TextMeshProUGUI runtimeMessageText;
    private TextMeshProUGUI runtimeActionsText;
    private Image runtimePlayerHpFillImage;
    private Image runtimeEnemyHpFillImage;
    private Image runtimeTimerFillImage;

    private string activeSessionId;
    private TurnState currentTurn;
    private CombatSessionPhase currentPhase;
    private float timerEndsAt;
    private float timerDuration = 1f;
    private bool playerActionLocked;
    private bool visible;
    private bool combatFocusPushed;

    /// <summary>
    /// Retourne l'instance existante ou cree un HUD runtime minimal.
    /// </summary>
    public static CombatHudController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<CombatHudController>();
#else
        Instance = FindAnyObjectByType<CombatHudController>();
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

    /// <summary>
    /// Masque le HUD si la session active correspond.
    /// </summary>
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
        // Unity appelle Awake au chargement; on initialise le singleton et l'UI disponible.
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
        // Nettoyage des objets runtime et des panneaux de scene.
        if (Instance == this)
        {
            ReleaseCombatInputFocus();
            SetScenePanelVisibility(false, false);
        }

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
        // Les inputs sont attaches ici car le composant peut etre reactive entre scenes.
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Interact += OnInteract;
        LocalInputRouter.RightShoulder += OnRightShoulder;
        LocalInputRouter.Return += OnReturn;
    }

    private void OnDisable()
    {
        // Toujours detacher les inputs pour eviter les actions doublement envoyees.
        LocalInputRouter.Interact -= OnInteract;
        LocalInputRouter.RightShoulder -= OnRightShoulder;
        LocalInputRouter.Return -= OnReturn;
        ReleaseCombatInputFocus();
    }

    private void Update()
    {
        if (!visible)
        {
            return;
        }

        // Le timer est derive du temps local entre deux snapshots serveur.
        UpdateTimerText();
    }

    /// <summary>
    /// Applique un snapshot complet de combat au HUD.
    /// </summary>
    public void ShowSnapshot(
        string sessionId,
        TurnState turn,
        CombatSessionPhase phase,
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
        TurnState previousTurn = currentTurn;
        CombatSessionPhase previousPhase = currentPhase;
        bool previousActionLocked = playerActionLocked;
        activeSessionId = sessionId;
        currentTurn = turn;
        currentPhase = phase;
        float sanitizedTimer = Mathf.Max(0f, timerRemaining);
        if (turn != previousTurn || phase != previousPhase || actionLocked != previousActionLocked || sanitizedTimer > timerDuration)
        {
            // On remet la duree de reference quand le tour change ou que l'etat d'action evolue.
            timerDuration = Mathf.Max(1f, sanitizedTimer);
        }

        timerEndsAt = Time.unscaledTime + sanitizedTimer;
        playerActionLocked = actionLocked;
        visible = turn != TurnState.None && turn != TurnState.Finished;
        UpdateCombatInputFocus(visible);
        SetScenePanelVisibility(visible, CanShowCombatActionPrompt());
        ApplySnapshotToUi(
            turn,
            phase,
            playerHp,
            playerMaxHp,
            enemyName,
            enemyHp,
            enemyMaxHp,
            aliveEnemies,
            totalEnemies,
            prayerSupportCount,
            damageReduction,
            message);

        if (HasScenePanelUi())
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            return;
        }

        if (root != null)
        {
            root.SetActive(visible);
        }
    }

    private void Hide()
    {
        activeSessionId = null;
        currentTurn = TurnState.None;
        currentPhase = CombatSessionPhase.Finished;
        playerActionLocked = false;
        visible = false;
        ReleaseCombatInputFocus();
        SetScenePanelVisibility(false, false);
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (CanSendCounter())
        {
            LocalInputRouter.ConsumeInteract();
            CombatSessionManager.Instance?.RequestLocalCounter();
            return;
        }

        if (!CanSendPlayerAction())
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();
        CombatSessionManager.Instance?.RequestLocalPlayerAttack();
    }

    private void OnRightShoulder(InputAction.CallbackContext context)
    {
        if (CanSendCounter())
        {
            CombatSessionManager.Instance?.RequestLocalCounter();
            return;
        }

        if (!CanSendPlayerAction())
        {
            return;
        }

        CombatSessionManager.Instance?.RequestLocalPlayerAttack();
    }

    private void OnReturn(InputAction.CallbackContext context)
    {
        if (CanSendDefense())
        {
            CombatSessionManager.Instance?.RequestLocalDefense();
            return;
        }

        if (!CanSendPlayerAction())
        {
            return;
        }

        CombatSessionManager.Instance?.RequestLocalPlayerPass();
    }

    private bool CanSendPlayerAction()
    {
        return visible
            && currentTurn == TurnState.Player
            && currentPhase == CombatSessionPhase.TurnActive
            && !playerActionLocked
            && HasCombatInputAccess();
    }

    private bool CanSendCounter()
    {
        return visible
            && currentTurn == TurnState.Enemy
            && currentPhase == CombatSessionPhase.Decision
            && !playerActionLocked
            && HasCombatInputAccess();
    }

    private bool CanSendDefense()
    {
        return visible
            && currentTurn == TurnState.Enemy
            && currentPhase == CombatSessionPhase.Decision
            && !playerActionLocked
            && HasCombatInputAccess();
    }

    private bool HasCombatInputAccess()
    {
        return (!InputFocusStack.HasAnyFocus() || InputFocusStack.HasFocus(this))
            && CombatSessionManager.Instance != null;
    }

    private bool CanChoosePlayerAction()
    {
        return visible
            && currentTurn == TurnState.Player
            && currentPhase == CombatSessionPhase.TurnActive
            && !playerActionLocked;
    }

    private bool CanChooseEncounterReaction()
    {
        return visible
            && currentTurn == TurnState.Enemy
            && currentPhase == CombatSessionPhase.Decision
            && !playerActionLocked;
    }

    private bool CanShowCombatActionPrompt()
    {
        return CanChoosePlayerAction() || CanChooseEncounterReaction();
    }

    private void UpdateCombatInputFocus(bool shouldOwnFocus)
    {
        if (!shouldOwnFocus)
        {
            ReleaseCombatInputFocus();
            return;
        }

        if (!combatFocusPushed || !InputFocusStack.HasAnyFocus() || InputFocusStack.HasFocus(this))
        {
            InputFocusStack.Push(this);
            combatFocusPushed = true;
        }
    }

    private void ReleaseCombatInputFocus()
    {
        if (!combatFocusPushed)
        {
            return;
        }

        InputFocusStack.Pop(this);
        combatFocusPushed = false;
    }

    private void UpdateTimerText()
    {
        TextMeshProUGUI timer = ActiveTimerText;
        if (timer == null)
        {
            return;
        }

        float remaining = Mathf.Max(0f, timerEndsAt - Time.unscaledTime);
        timer.text = $"Temps: {Mathf.CeilToInt(remaining)} s";
        SetFill(ActiveTimerFillImage, remaining, timerDuration);
    }

    private void BuildUi()
    {
        ResolveScenePanelsIfNeeded();
        if (HasScenePanelUi())
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            return;
        }

        if (!allowRuntimeFallback)
        {
            return;
        }

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

        runtimeTitleText = CreateText(panel.transform, "Title", 24, FontStyles.Bold);
        runtimeTurnText = CreateText(panel.transform, "Turn", 18, FontStyles.Bold);
        runtimeTimerText = CreateText(panel.transform, "Timer", 18, FontStyles.Normal);
        runtimePlayerHpText = CreateText(panel.transform, "PlayerHp", 17, FontStyles.Normal);
        runtimeEnemyHpText = CreateText(panel.transform, "EnemyHp", 17, FontStyles.Normal);
        runtimePrayerText = CreateText(panel.transform, "Prayer", 16, FontStyles.Normal);
        runtimeMessageText = CreateText(panel.transform, "Message", 16, FontStyles.Italic);
        runtimeActionsText = CreateText(panel.transform, "Actions", 15, FontStyles.Normal);

        root.SetActive(false);
    }

    private bool HasScenePanelUi()
    {
        return battlePanelCanvasGroup != null;
    }

    private void ResolveScenePanelsIfNeeded()
    {
        if (battlePanelCanvasGroup == null)
        {
            battlePanelCanvasGroup = FindCanvasGroupByName(DefaultBattlePanelName);
        }

        if (baseAttackCanvasGroup == null)
        {
            baseAttackCanvasGroup = FindCanvasGroupByName(DefaultBaseAttackUiName);
        }

        ResolveSceneTextIfNeeded(ref titleText, "CombatTitleText");
        ResolveSceneTextIfNeeded(ref turnText, "CombatTurnText");
        ResolveSceneTextIfNeeded(ref timerText, "CombatTimerText");
        ResolveSceneTextIfNeeded(ref playerHpText, "CombatPlayerHpText");
        ResolveSceneTextIfNeeded(ref enemyHpText, "CombatEnemyHpText");
        ResolveSceneTextIfNeeded(ref prayerText, "CombatPrayerText");
        ResolveSceneTextIfNeeded(ref messageText, "CombatMessageText");
        ResolveSceneTextIfNeeded(ref actionsText, "CombatActionsText");
        ResolveSceneTextIfNeeded(ref baseAttackText, "BaseAttack_Text");

        ResolveSceneImageIfNeeded(ref playerHpFillImage, "CombatPlayerHpFill");
        ResolveSceneImageIfNeeded(ref enemyHpFillImage, "CombatEnemyHpFill");
        ResolveSceneImageIfNeeded(ref timerFillImage, "CombatTimerFill");
    }

    private void SetScenePanelVisibility(bool battleVisible, bool baseAttackVisible)
    {
        ResolveScenePanelsIfNeeded();
        SetCanvasGroupVisible(battlePanelCanvasGroup, battleVisible, blocksRaycasts: false);
        SetCanvasGroupVisible(baseAttackCanvasGroup, battleVisible && baseAttackVisible, blocksRaycasts: false);
    }

    private static void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool visible, bool blocksRaycasts)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible && blocksRaycasts;
        canvasGroup.blocksRaycasts = visible && blocksRaycasts;
    }

    private static CanvasGroup FindCanvasGroupByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject found = GameObject.Find(objectName);
        if (found != null)
        {
            return found.GetComponent<CanvasGroup>();
        }

        return null;
    }

    private void ApplySnapshotToUi(
        TurnState turn,
        CombatSessionPhase phase,
        int playerHp,
        int playerMaxHp,
        string enemyName,
        int enemyHp,
        int enemyMaxHp,
        int aliveEnemies,
        int totalEnemies,
        int prayerSupportCount,
        float damageReduction,
        string message)
    {
        if (!visible)
        {
            return;
        }

        SetText(ActiveTitleText, "Combat");
        SetText(ActiveTurnText, ResolveTurnLabel(turn, phase));
        SetText(ActivePlayerHpText, $"Joueur\n{Mathf.Max(0, playerHp)}/{Mathf.Max(1, playerMaxHp)} PV");
        SetText(
            ActiveEnemyHpText,
            $"{(string.IsNullOrWhiteSpace(enemyName) ? "Ennemi" : enemyName)}\n{Mathf.Max(0, enemyHp)}/{Mathf.Max(1, enemyMaxHp)} PV\nCibles: {Mathf.Max(0, aliveEnemies)}/{Mathf.Max(1, totalEnemies)}");
        SetText(
            ActivePrayerText,
            prayerSupportCount > 0
                ? $"Soutien: {prayerSupportCount} priere(s), -{Mathf.RoundToInt(damageReduction * 100f)}% degats"
                : "Soutien: aucune priere active");
        SetText(ActiveMessageText, string.IsNullOrWhiteSpace(message) ? "Combat en cours." : message);
        SetText(ActiveActionsText, ResolveActionsLabel(turn, phase));
        SetText(baseAttackText, ResolvePrimaryActionLabel(turn, phase));
        SetFill(ActivePlayerHpFillImage, playerHp, playerMaxHp);
        SetFill(ActiveEnemyHpFillImage, enemyHp, enemyMaxHp);
        UpdateTimerText();
    }

    private string ResolveTurnLabel(TurnState turn, CombatSessionPhase phase)
    {
        if (turn == TurnState.Player)
        {
            if (phase == CombatSessionPhase.Decision)
            {
                return "Fenetre d'attaque - preparation";
            }

            return playerActionLocked ? "Attaque en cours" : "Fenetre d'attaque";
        }

        if (turn == TurnState.Enemy)
        {
            if (phase == CombatSessionPhase.Decision)
            {
                return "Fenetre de reaction";
            }

            return phase == CombatSessionPhase.EnemyAction ? "Attaque ennemie" : "Rencontre ennemie";
        }

        return "Resolution";
    }

    private string ResolveActionsLabel(TurnState turn, CombatSessionPhase phase)
    {
        if (turn != TurnState.Player)
        {
            if (phase == CombatSessionPhase.Decision)
            {
                return "Interagir/RB: contre | Retour: defense | aucune reaction: mort.";
            }

            return phase == CombatSessionPhase.EnemyAction
                ? "L'ennemi agit."
                : "L'ennemi attaque en premier.";
        }

        if (phase == CombatSessionPhase.Decision)
        {
            return "Choix disponibles dans un instant.";
        }

        if (playerActionLocked)
        {
            return "Attaque en cours.";
        }

        return "Interagir/RB: attaque decisive | Retour: abandonner";
    }

    private string ResolvePrimaryActionLabel(TurnState turn, CombatSessionPhase phase)
    {
        if (turn == TurnState.Enemy && phase == CombatSessionPhase.Decision)
        {
            return "Contrer / Defendre";
        }

        if (turn == TurnState.Player && phase == CombatSessionPhase.TurnActive)
        {
            return playerActionLocked ? "En cours" : "Attaquer";
        }

        return playerActionLocked ? "En cours" : string.Empty;
    }

    private TextMeshProUGUI ActiveTitleText => titleText != null ? titleText : runtimeTitleText;
    private TextMeshProUGUI ActiveTurnText => turnText != null ? turnText : runtimeTurnText;
    private TextMeshProUGUI ActiveTimerText => timerText != null ? timerText : runtimeTimerText;
    private TextMeshProUGUI ActivePlayerHpText => playerHpText != null ? playerHpText : runtimePlayerHpText;
    private TextMeshProUGUI ActiveEnemyHpText => enemyHpText != null ? enemyHpText : runtimeEnemyHpText;
    private TextMeshProUGUI ActivePrayerText => prayerText != null ? prayerText : runtimePrayerText;
    private TextMeshProUGUI ActiveMessageText => messageText != null ? messageText : runtimeMessageText;
    private TextMeshProUGUI ActiveActionsText => actionsText != null ? actionsText : runtimeActionsText;
    private Image ActivePlayerHpFillImage => playerHpFillImage != null ? playerHpFillImage : runtimePlayerHpFillImage;
    private Image ActiveEnemyHpFillImage => enemyHpFillImage != null ? enemyHpFillImage : runtimeEnemyHpFillImage;
    private Image ActiveTimerFillImage => timerFillImage != null ? timerFillImage : runtimeTimerFillImage;

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private static void SetFill(Image image, float current, float maximum)
    {
        if (image != null)
        {
            image.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        }
    }

    private static void ResolveSceneTextIfNeeded(ref TextMeshProUGUI text, string objectName)
    {
        if (text != null)
        {
            return;
        }

        GameObject found = GameObject.Find(objectName);
        if (found != null)
        {
            text = found.GetComponent<TextMeshProUGUI>();
        }
    }

    private static void ResolveSceneImageIfNeeded(ref Image image, string objectName)
    {
        if (image != null)
        {
            return;
        }

        GameObject found = GameObject.Find(objectName);
        if (found != null)
        {
            image = found.GetComponent<Image>();
        }
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
