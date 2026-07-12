using System.Collections.Generic;
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
    private const string DefaultCombatEngagedPanelName = "CombatEngagedPanel";
    private const string DefaultCombatScreenInfosPanelName = "CombatScreenInfosPanel";
    private const string DefaultCombatLogName = "CombatLog";
    private const string DefaultVictoryPanelName = "VictoryPanel";
    private const string DefaultDefeatPanelName = "DefeatPanel";
    private const string CombatEngagedAnimationName = "CombatEngagedPanel_Trigger";
    private static readonly string[] DefeatMainMenuButtonHints = { "mainmenu", "main menu", "menuprincipal", "menu principal", "menu" };
    private static readonly string[] DefeatRetryButtonHints = { "retry", "reessayer", "réessayer", "recommencer", "relancer" };
    private static readonly string[] DefeatCheckpointButtonHints = { "checkpoint", "derniercheckpoint", "dernier checkpoint" };
    private const string EnemyAttackAlertText = "Attention l’ennemi attaque:";

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

    public static void SetCombatDefensePanelVisibleFromAnimationEvent(bool shouldBeVisible)
    {
        CombatHudController controller = Instance;
        if (controller == null && shouldBeVisible)
        {
            controller = EnsureInstance();
        }

        if (controller != null)
        {
            controller.SetCombatDefensePanelRequested(shouldBeVisible);
            return;
        }

        if (!shouldBeVisible)
        {
            CombatDefensePanelController.HideActive();
        }
    }

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
    [SerializeField] private CanvasGroup combatEngagedCanvasGroup;
    [SerializeField] private CanvasGroup combatScreenInfosCanvasGroup;
    [SerializeField] private CanvasGroup victoryPanelCanvasGroup;
    [SerializeField] private CanvasGroup defeatPanelCanvasGroup;
    [SerializeField] private TextMeshProUGUI victoryResultMessageText;
    [SerializeField] private TextMeshProUGUI defeatResultMessageText;
    [SerializeField] private Button victoryContinueButton;
    [SerializeField] private Button defeatContinueButton;
    [SerializeField] private Button defeatMainMenuButton;
    [SerializeField] private Button defeatRetryButton;
    [SerializeField] private Button defeatCheckpointButton;
    [SerializeField] private Animator combatEngagedAnimator;
    [SerializeField, Min(0.1f)] private float combatEngagedFallbackDuration = 1.2f;
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
    [SerializeField] private TextMeshProUGUI combatLogText;
    [SerializeField, Min(1)] private int combatLogMaxLines = 6;

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
    private string combatEngagedSessionId;
    private bool combatEngagedIntroActive;
    private float combatEngagedIntroEndsAt;
    private bool combatEngagedAnimationObserved;
    private bool combatDefensePanelRequested;
    private TextMeshProUGUI resultMessageText;
    private Button resultContinueButton;
    private CanvasGroup activeResultPanelCanvasGroup;
    private bool combatResultVisible;
    private bool combatResultPlayerVictory;
    private string combatResultSessionId;
    private readonly List<string> combatLogLines = new List<string>(6);

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

    public static void AppendCombatLog(string message)
    {
        CombatHudController controller = EnsureInstance();
        if (controller != null)
        {
            controller.AppendCombatLogLine(message);
        }
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

    public void BeginCombatSessionIntro(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        BuildUi();
        HideCombatResult();
        activeSessionId = sessionId;
        LocalPlayerInput.SetCombatInputActive(true);
        UpdateCombatInputFocus(true);
        if (!string.Equals(combatEngagedSessionId, sessionId, System.StringComparison.Ordinal))
        {
            StartCombatEngagedIntro(sessionId);
        }
    }

    public void ShowCombatResult(string sessionId, bool playerVictory, string message)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        BuildResultUi();
        activeSessionId = sessionId;
        combatResultSessionId = sessionId;
        combatResultVisible = true;
        combatResultPlayerVictory = playerVictory;
        visible = false;
        playerActionLocked = true;
        combatDefensePanelRequested = false;
        combatEngagedIntroActive = false;

        resultMessageText = playerVictory ? victoryResultMessageText : null;
        resultContinueButton = playerVictory ? victoryContinueButton : null;
        if (playerVictory)
        {
            SetText(resultMessageText, string.IsNullOrWhiteSpace(message) ? "Combat remporte." : message);
        }

        if (resultContinueButton != null)
        {
            resultContinueButton.interactable = true;
        }

        SetDefeatChoiceButtonsInteractable(!playerVictory);

        SetScenePanelVisibility(false, false);
        CombatDefensePanelController.HideActive();
        SetCombatEngagedVisible(false);
        if (root != null)
        {
            root.SetActive(false);
        }

        SetCanvasGroupVisible(victoryPanelCanvasGroup, false, blocksRaycasts: false);
        SetCanvasGroupVisible(defeatPanelCanvasGroup, false, blocksRaycasts: false);

        CanvasGroup targetResultPanel = playerVictory ? victoryPanelCanvasGroup : defeatPanelCanvasGroup;
        activeResultPanelCanvasGroup = targetResultPanel;
        if (targetResultPanel != null)
        {
            if (IsAncestorOf(battlePanelCanvasGroup, targetResultPanel))
            {
                SetCanvasGroupVisible(battlePanelCanvasGroup, true, blocksRaycasts: true);
            }

            SetCanvasGroupVisible(targetResultPanel, true, blocksRaycasts: true);
        }
        else
        {
            Debug.LogWarning(playerVictory
                ? "[CombatHud] VictoryPanel introuvable dans la scene; aucun panel de victoire runtime ne sera cree."
                : "[CombatHud] DefeatPanel introuvable dans la scene; aucun panel de defaite runtime ne sera cree.");
        }

        LocalPlayerInput.SetCombatInputActive(true);
        UpdateCombatInputFocus(true);
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
        HideCombatResult();
    }

    private void OnDestroy()
    {
        // Nettoyage des objets runtime et des panneaux de scene.
        if (Instance == this)
        {
            ReleaseCombatInputFocus();
            LocalPlayerInput.SetCombatInputActive(false);
            SetScenePanelVisibility(false, false);
            SetCombatEngagedVisible(false);
            CombatDefensePanelController.HideActive();
            HideCombatResult();
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
        LocalInputRouter.CombatUseItem += OnCombatUseItem;
        HideCombatResult();
        RefreshCombatPanelVisibility();
    }

    private void OnDisable()
    {
        // Toujours detacher les inputs pour eviter les actions doublement envoyees.
        LocalInputRouter.Interact -= OnInteract;
        LocalInputRouter.RightShoulder -= OnRightShoulder;
        LocalInputRouter.Return -= OnReturn;
        LocalInputRouter.CombatUseItem -= OnCombatUseItem;
        combatDefensePanelRequested = false;
        HideCombatResult();
        ReleaseCombatInputFocus();
        LocalPlayerInput.SetCombatInputActive(false);
    }

    private void Update()
    {
        if (combatEngagedIntroActive)
        {
            UpdateCombatEngagedIntro();
        }

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
        bool resolving = phase == CombatSessionPhase.Resolving;
        visible = turn != TurnState.None && turn != TurnState.Finished;
        if (visible && !string.IsNullOrWhiteSpace(sessionId) && !string.Equals(combatEngagedSessionId, sessionId, System.StringComparison.Ordinal))
        {
            StartCombatEngagedIntro(sessionId);
        }

        bool combatInputVisible = visible || resolving || combatEngagedIntroActive || combatResultVisible;
        LocalPlayerInput.SetCombatInputActive(combatInputVisible);
        UpdateCombatInputFocus(combatInputVisible);
        RefreshCombatPanelVisibility();
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
        combatEngagedSessionId = null;
        currentTurn = TurnState.None;
        currentPhase = CombatSessionPhase.Finished;
        playerActionLocked = false;
        visible = false;
        combatDefensePanelRequested = false;
        HideCombatResult();
        ReleaseCombatInputFocus();
        LocalPlayerInput.SetCombatInputActive(false);
        SetScenePanelVisibility(false, false);
        SetCombatEngagedVisible(false);
        ClearCombatLog();
        CombatDefensePanelController.HideActive();
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (combatResultVisible)
        {
            LocalInputRouter.ConsumeInteract();
            if (combatResultPlayerVictory)
            {
                RequestCombatResultContinue();
            }

            return;
        }

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
        if (combatResultVisible)
        {
            if (combatResultPlayerVictory)
            {
                RequestCombatResultContinue();
            }

            return;
        }

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

    private void OnCombatUseItem(int slotIndex)
    {
        if (!combatResultVisible)
        {
            return;
        }

        if (combatResultPlayerVictory)
        {
            RequestCombatResultContinue();
        }
    }

    private void RequestCombatResultContinue()
    {
        if (!combatResultVisible || !combatResultPlayerVictory || string.IsNullOrWhiteSpace(combatResultSessionId))
        {
            return;
        }

        if (resultContinueButton != null)
        {
            resultContinueButton.interactable = false;
        }

        CombatSessionManager.Instance?.RequestLocalCombatResultContinue(combatResultSessionId);
    }

    private void RequestCombatResultReturnToMainMenu()
    {
        if (!CanRequestDefeatChoice())
        {
            return;
        }

        SetDefeatChoiceButtonsInteractable(false);
        CombatSessionManager.Instance?.RequestLocalCombatResultReturnToMainMenu(combatResultSessionId);
    }

    private void RequestCombatResultRetry()
    {
        if (!CanRequestDefeatChoice())
        {
            return;
        }

        SetDefeatChoiceButtonsInteractable(false);
        CombatSessionManager.Instance?.RequestLocalCombatResultRetry(combatResultSessionId);
    }

    private void RequestCombatResultLastCheckpoint()
    {
        if (!CanRequestDefeatChoice())
        {
            return;
        }

        SetDefeatChoiceButtonsInteractable(false);
        CombatSessionManager.Instance?.RequestLocalCombatResultLastCheckpoint(combatResultSessionId);
    }

    private bool CanRequestDefeatChoice()
    {
        return combatResultVisible && !combatResultPlayerVictory && !string.IsNullOrWhiteSpace(combatResultSessionId);
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

        if (!InputFocusStack.HasFocus(this))
        {
            InventoryPanelController.CloseAllOpenForCombat();
            InputFocusStack.PushExclusive(this);
        }

        combatFocusPushed = true;
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

    private void StartCombatEngagedIntro(string sessionId)
    {
        ResolveScenePanelsIfNeeded();
        combatEngagedSessionId = sessionId;
        if (combatEngagedCanvasGroup == null && combatEngagedAnimator == null)
        {
            combatEngagedIntroActive = false;
            RefreshCombatPanelVisibility();
            return;
        }

        combatEngagedIntroActive = true;
        combatEngagedIntroEndsAt = Time.unscaledTime + ResolveCombatEngagedDuration();
        combatEngagedAnimationObserved = false;
        combatDefensePanelRequested = false;
        ClearCombatLog();

        LocalPlayerInput.SetCombatInputActive(true);
        UpdateCombatInputFocus(true);
        SetScenePanelVisibility(false, false);
        CombatDefensePanelController.HideActive();
        SetCombatEngagedVisible(true);
        PlayCombatEngagedAnimation();
    }

    private void UpdateCombatEngagedIntro()
    {
        if (!combatEngagedIntroActive)
        {
            return;
        }

        if (IsCombatEngagedAnimationStillPlaying())
        {
            return;
        }

        combatEngagedIntroActive = false;
        SetCombatEngagedVisible(false);
        RefreshCombatPanelVisibility();
    }

    private bool IsCombatEngagedAnimationStillPlaying()
    {
        if (combatEngagedAnimator == null || !combatEngagedAnimator.isActiveAndEnabled)
        {
            return Time.unscaledTime < combatEngagedIntroEndsAt;
        }

        AnimatorStateInfo currentState = combatEngagedAnimator.GetCurrentAnimatorStateInfo(0);
        if (AnimatorStateOrClipMatches(combatEngagedAnimator, currentState, false))
        {
            combatEngagedAnimationObserved = true;
            return combatEngagedAnimator.IsInTransition(0) || currentState.normalizedTime < 1f;
        }

        if (combatEngagedAnimator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = combatEngagedAnimator.GetNextAnimatorStateInfo(0);
            if (AnimatorStateOrClipMatches(combatEngagedAnimator, nextState, true))
            {
                combatEngagedAnimationObserved = true;
                return nextState.normalizedTime < 1f;
            }
        }

        if (combatEngagedAnimationObserved)
        {
            return false;
        }

        return Time.unscaledTime < combatEngagedIntroEndsAt;
    }

    private void RefreshCombatPanelVisibility()
    {
        if (combatResultVisible)
        {
            SetScenePanelVisibility(false, false);
            CombatDefensePanelController.HideActive();
            SetCombatEngagedVisible(false);
            RefreshActiveResultPanelVisibility();
            return;
        }

        bool hasDefenseRequestContext = combatDefensePanelRequested && HasLocalCombatContext();
        if (!visible && !hasDefenseRequestContext && !combatEngagedIntroActive)
        {
            combatDefensePanelRequested = false;
            SetScenePanelVisibility(false, false);
            CombatDefensePanelController.HideActive();
            SetCombatEngagedVisible(false);
            return;
        }

        if (combatDefensePanelRequested && combatEngagedIntroActive)
        {
            SetCombatEngagedVisible(false);
        }

        bool defenseVisible = combatDefensePanelRequested && (visible || hasDefenseRequestContext);
        bool infosVisible = visible && !combatEngagedIntroActive && !defenseVisible;
        SetScenePanelVisibility(infosVisible, infosVisible && CanChoosePlayerAction());
        CombatDefensePanelController.SetAnimationEventVisible(defenseVisible);
    }

    private void SetCombatDefensePanelRequested(bool shouldBeVisible)
    {
        combatDefensePanelRequested = shouldBeVisible;
        if (shouldBeVisible)
        {
            LocalPlayerInput.SetCombatInputActive(true);
            UpdateCombatInputFocus(true);
        }

        if (shouldBeVisible && combatEngagedIntroActive)
        {
            SetCombatEngagedVisible(false);
        }

        RefreshCombatPanelVisibility();
    }

    private static bool HasLocalCombatContext()
    {
        CombatSessionManager manager = CombatSessionManager.Instance;
        if (manager == null)
        {
            return false;
        }

        return manager.TryGetLocalCombatCameraContext(
                out Transform player,
                out Transform enemy,
                out _,
                out CombatSessionPhase phase)
            && phase != CombatSessionPhase.Created
            && phase != CombatSessionPhase.Finished
            && (player != null || enemy != null);
    }

    private void SetCombatEngagedVisible(bool shouldBeVisible)
    {
        ResolveScenePanelsIfNeeded();
        if (shouldBeVisible && IsAncestorOf(battlePanelCanvasGroup, combatEngagedCanvasGroup))
        {
            SetCanvasGroupVisible(battlePanelCanvasGroup, true, blocksRaycasts: false);
        }

        SetCanvasGroupVisible(combatEngagedCanvasGroup, shouldBeVisible, blocksRaycasts: false);
        if (!shouldBeVisible)
        {
            combatEngagedIntroActive = false;
        }
    }

    private void PlayCombatEngagedAnimation()
    {
        if (combatEngagedAnimator == null)
        {
            return;
        }

        if (HasAnimatorTrigger(combatEngagedAnimator, CombatEngagedAnimationName))
        {
            combatEngagedAnimator.ResetTrigger(CombatEngagedAnimationName);
            combatEngagedAnimator.SetTrigger(CombatEngagedAnimationName);
            return;
        }

        combatEngagedAnimator.Play(CombatEngagedAnimationName, 0, 0f);
    }

    private float ResolveCombatEngagedDuration()
    {
        float fallback = Mathf.Max(0.1f, combatEngagedFallbackDuration);
        if (combatEngagedAnimator == null || combatEngagedAnimator.runtimeAnimatorController == null)
        {
            return fallback;
        }

        AnimationClip[] clips = combatEngagedAnimator.runtimeAnimatorController.animationClips;
        if (clips == null)
        {
            return fallback;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || !string.Equals(clip.name, CombatEngagedAnimationName, System.StringComparison.Ordinal))
            {
                continue;
            }

            return Mathf.Max(0.1f, clip.length);
        }

        return fallback;
    }

    private static bool HasAnimatorTrigger(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger
                && string.Equals(parameter.name, triggerName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnimatorStateOrClipMatches(Animator animator, AnimatorStateInfo stateInfo, bool next)
    {
        if (stateInfo.IsName(CombatEngagedAnimationName))
        {
            return true;
        }

        AnimatorClipInfo[] clips = next
            ? animator.GetNextAnimatorClipInfo(0)
            : animator.GetCurrentAnimatorClipInfo(0);
        if (clips == null)
        {
            return false;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i].clip;
            if (clip != null && string.Equals(clip.name, CombatEngagedAnimationName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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

    private void BuildResultUi()
    {
        ResolveSceneResultPanelsIfNeeded();
        SetCanvasGroupVisible(victoryPanelCanvasGroup, false, blocksRaycasts: false);
        SetCanvasGroupVisible(defeatPanelCanvasGroup, false, blocksRaycasts: false);
    }

    private void HideCombatResult()
    {
        combatResultVisible = false;
        combatResultPlayerVictory = false;
        combatResultSessionId = null;
        if (resultContinueButton != null)
        {
            resultContinueButton.interactable = true;
        }

        SetDefeatChoiceButtonsInteractable(true);
        ResolveSceneResultPanelsIfNeeded();
        SetCanvasGroupVisible(activeResultPanelCanvasGroup, false, blocksRaycasts: false);
        SetCanvasGroupVisible(victoryPanelCanvasGroup, false, blocksRaycasts: false);
        SetCanvasGroupVisible(defeatPanelCanvasGroup, false, blocksRaycasts: false);
        activeResultPanelCanvasGroup = null;
        resultMessageText = null;
        resultContinueButton = null;
    }

    private bool HasScenePanelUi()
    {
        return combatScreenInfosCanvasGroup != null || battlePanelCanvasGroup != null;
    }

    private void ResolveScenePanelsIfNeeded()
    {
        if (combatScreenInfosCanvasGroup == null)
        {
            combatScreenInfosCanvasGroup = FindCanvasGroupByName(DefaultCombatScreenInfosPanelName);
        }

        if (battlePanelCanvasGroup == null)
        {
            battlePanelCanvasGroup = FindCanvasGroupByName(DefaultBattlePanelName);
        }

        if (baseAttackCanvasGroup == null)
        {
            baseAttackCanvasGroup = FindCanvasGroupByName(DefaultBaseAttackUiName);
        }

        if (combatEngagedCanvasGroup == null)
        {
            combatEngagedCanvasGroup = FindCanvasGroupByName(DefaultCombatEngagedPanelName);
        }

        if (combatEngagedAnimator == null && combatEngagedCanvasGroup != null)
        {
            combatEngagedAnimator = combatEngagedCanvasGroup.GetComponentInChildren<Animator>(true);
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
        ResolveCombatLogTextIfNeeded();

        ResolveSceneImageIfNeeded(ref playerHpFillImage, "CombatPlayerHpFill");
        ResolveSceneImageIfNeeded(ref enemyHpFillImage, "CombatEnemyHpFill");
        ResolveSceneImageIfNeeded(ref timerFillImage, "CombatTimerFill");
    }

    private void ResolveSceneResultPanelsIfNeeded()
    {
        if (victoryPanelCanvasGroup == null)
        {
            victoryPanelCanvasGroup = FindCanvasGroupByName(DefaultVictoryPanelName);
        }

        if (defeatPanelCanvasGroup == null)
        {
            defeatPanelCanvasGroup = FindCanvasGroupByName(DefaultDefeatPanelName);
        }

        if (victoryResultMessageText == null)
        {
            victoryResultMessageText = FindResultMessageText(victoryPanelCanvasGroup, DefaultVictoryPanelName);
        }

        if (defeatResultMessageText == null)
        {
            defeatResultMessageText = FindResultMessageText(defeatPanelCanvasGroup, DefaultDefeatPanelName);
        }

        if (victoryContinueButton == null)
        {
            victoryContinueButton = FindResultContinueButton(victoryPanelCanvasGroup);
        }

        if (defeatContinueButton == null)
        {
            defeatContinueButton = FindResultContinueButton(defeatPanelCanvasGroup);
        }

        ResolveDefeatChoiceButtonsIfNeeded();
        RegisterResultContinueButton(victoryContinueButton);
        UnregisterResultContinueButton(defeatContinueButton);
        RegisterButton(defeatMainMenuButton, RequestCombatResultReturnToMainMenu);
        RegisterButton(defeatRetryButton, RequestCombatResultRetry);
        RegisterButton(defeatCheckpointButton, RequestCombatResultLastCheckpoint);
    }

    private void RefreshActiveResultPanelVisibility()
    {
        if (activeResultPanelCanvasGroup == null)
        {
            return;
        }

        if (IsAncestorOf(battlePanelCanvasGroup, activeResultPanelCanvasGroup))
        {
            SetCanvasGroupVisible(battlePanelCanvasGroup, true, blocksRaycasts: true);
        }

        SetCanvasGroupVisible(activeResultPanelCanvasGroup, true, blocksRaycasts: true);
    }

    private static TextMeshProUGUI FindResultMessageText(CanvasGroup panel, string panelName)
    {
        if (panel == null)
        {
            return null;
        }

        TextMeshProUGUI[] texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null)
        {
            return null;
        }

        string panelMessageName = panelName + "_Message";
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (string.Equals(text.name, "CombatResultMessageText", System.StringComparison.Ordinal)
                || string.Equals(text.name, "ResultMessageText", System.StringComparison.Ordinal)
                || string.Equals(text.name, panelMessageName, System.StringComparison.Ordinal))
            {
                return text;
            }
        }

        return null;
    }

    private static Button FindResultContinueButton(CanvasGroup panel)
    {
        return panel != null ? panel.GetComponentInChildren<Button>(true) : null;
    }

    private void ResolveDefeatChoiceButtonsIfNeeded()
    {
        if (defeatPanelCanvasGroup == null)
        {
            return;
        }

        Button[] buttons = defeatPanelCanvasGroup.GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
        {
            return;
        }

        if (defeatMainMenuButton == null)
        {
            defeatMainMenuButton = FindButtonByHints(buttons, DefeatMainMenuButtonHints);
        }

        if (defeatRetryButton == null)
        {
            defeatRetryButton = FindButtonByHints(buttons, DefeatRetryButtonHints);
        }

        if (defeatCheckpointButton == null)
        {
            defeatCheckpointButton = FindButtonByHints(buttons, DefeatCheckpointButtonHints);
        }

        AssignMissingDefeatButtonsByOrder(buttons);
    }

    private void AssignMissingDefeatButtonsByOrder(Button[] buttons)
    {
        int nextIndex = 0;
        if (defeatMainMenuButton == null)
        {
            defeatMainMenuButton = FindNextUnassignedButton(buttons, ref nextIndex);
        }

        if (defeatRetryButton == null)
        {
            defeatRetryButton = FindNextUnassignedButton(buttons, ref nextIndex);
        }

        if (defeatCheckpointButton == null)
        {
            defeatCheckpointButton = FindNextUnassignedButton(buttons, ref nextIndex);
        }
    }

    private Button FindNextUnassignedButton(Button[] buttons, ref int startIndex)
    {
        if (buttons == null)
        {
            return null;
        }

        for (int i = Mathf.Max(0, startIndex); i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null ||
                button == defeatMainMenuButton ||
                button == defeatRetryButton ||
                button == defeatCheckpointButton)
            {
                continue;
            }

            startIndex = i + 1;
            return button;
        }

        return null;
    }

    private static Button FindButtonByHints(Button[] buttons, string[] hints)
    {
        if (buttons == null || hints == null)
        {
            return null;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (ButtonMatchesHints(button, hints))
            {
                return button;
            }
        }

        return null;
    }

    private static bool ButtonMatchesHints(Button button, string[] hints)
    {
        if (button == null || hints == null)
        {
            return false;
        }

        if (TextMatchesHints(button.name, hints))
        {
            return true;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        return label != null && TextMatchesHints(label.text, hints);
    }

    private static bool TextMatchesHints(string value, string[] hints)
    {
        if (string.IsNullOrWhiteSpace(value) || hints == null)
        {
            return false;
        }

        for (int i = 0; i < hints.Length; i++)
        {
            string hint = hints[i];
            if (!string.IsNullOrWhiteSpace(hint) &&
                value.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void RegisterResultContinueButton(Button button)
    {
        RegisterButton(button, RequestCombatResultContinue);
    }

    private void UnregisterResultContinueButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(RequestCombatResultContinue);
    }

    private static void RegisterButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null || callback == null)
        {
            return;
        }

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    private void SetDefeatChoiceButtonsInteractable(bool interactable)
    {
        ResolveDefeatChoiceButtonsIfNeeded();
        SetButtonInteractable(defeatMainMenuButton, interactable);
        SetButtonInteractable(defeatRetryButton, interactable);
        SetButtonInteractable(defeatCheckpointButton, interactable);
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void SetScenePanelVisibility(bool battleVisible, bool baseAttackVisible)
    {
        ResolveScenePanelsIfNeeded();
        if (combatScreenInfosCanvasGroup != null)
        {
            if (IsAncestorOf(battlePanelCanvasGroup, combatScreenInfosCanvasGroup)
                || IsAncestorOf(battlePanelCanvasGroup, combatEngagedCanvasGroup))
            {
                SetCanvasGroupVisible(battlePanelCanvasGroup, true, blocksRaycasts: false);
            }
            else
            {
                SetCanvasGroupVisible(battlePanelCanvasGroup, false, blocksRaycasts: false);
            }

            SetCanvasGroupVisible(combatScreenInfosCanvasGroup, battleVisible, blocksRaycasts: false);
        }
        else
        {
            SetCanvasGroupVisible(battlePanelCanvasGroup, battleVisible, blocksRaycasts: false);
        }

        SetCanvasGroupVisible(baseAttackCanvasGroup, battleVisible && baseAttackVisible, blocksRaycasts: false);
    }

    private static void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool visible, bool blocksRaycasts)
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (visible)
        {
            EnsureActiveHierarchy(canvasGroup.transform);
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible && blocksRaycasts;
        canvasGroup.blocksRaycasts = visible && blocksRaycasts;
    }

    private static bool IsAncestorOf(CanvasGroup maybeAncestor, CanvasGroup maybeDescendant)
    {
        if (maybeAncestor == null || maybeDescendant == null || maybeAncestor == maybeDescendant)
        {
            return false;
        }

        return maybeDescendant.transform.IsChildOf(maybeAncestor.transform);
    }

    private static void EnsureActiveHierarchy(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Transform parent = target.parent;
        if (parent != null && parent.gameObject.scene.IsValid())
        {
            EnsureActiveHierarchy(parent);
        }

        if (!target.gameObject.activeSelf)
        {
            target.gameObject.SetActive(true);
        }

        if (target.localScale.sqrMagnitude <= 0.0001f)
        {
            target.localScale = Vector3.one;
        }
    }

    private static CanvasGroup FindCanvasGroupByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject found = FindSceneGameObjectByName(objectName);
        if (found != null)
        {
            CanvasGroup canvasGroup = found.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = found.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }

        return null;
    }

    private static GameObject FindSceneGameObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        if (transforms == null)
        {
            return null;
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
            {
                return candidate.gameObject;
            }
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

        bool enemyReactionWindow = IsEnemyReactionWindow(turn, phase);
        SetText(ActiveTitleText, enemyReactionWindow ? EnemyAttackAlertText : "Combat");
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
        SetText(ActiveMessageText, ResolveMessageLabel(turn, phase, message));
        SetText(ActiveActionsText, ResolveActionsLabel(turn, phase));
        SetText(baseAttackText, ResolvePrimaryActionLabel(turn, phase));
        SetFill(ActivePlayerHpFillImage, playerHp, playerMaxHp);
        SetFill(ActiveEnemyHpFillImage, enemyHp, enemyMaxHp);
        UpdateTimerText();
    }

    private static bool IsEnemyReactionWindow(TurnState turn, CombatSessionPhase phase)
    {
        return turn == TurnState.Enemy && phase == CombatSessionPhase.Decision;
    }

    private static string ResolveMessageLabel(TurnState turn, CombatSessionPhase phase, string message)
    {
        if (IsEnemyReactionWindow(turn, phase) && string.IsNullOrWhiteSpace(message))
        {
            return EnemyAttackAlertText;
        }

        return string.IsNullOrWhiteSpace(message) ? "Combat en cours." : message;
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
                return "Fenetre de contre";
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
                return "Interagir/RB: contre | Retour: defense | aucune reaction: defaite.";
            }

            return phase == CombatSessionPhase.EnemyAction
                ? "Impact ennemi imminent."
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
            return "Contrer";
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

    private void AppendCombatLogLine(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ResolveCombatLogTextIfNeeded();
        if (combatLogText == null)
        {
            return;
        }

        combatLogLines.Add(message.Trim());
        int maxLines = Mathf.Max(1, combatLogMaxLines);
        while (combatLogLines.Count > maxLines)
        {
            combatLogLines.RemoveAt(0);
        }

        combatLogText.text = string.Join("\n", combatLogLines);
    }

    private void ClearCombatLog()
    {
        combatLogLines.Clear();
        ResolveCombatLogTextIfNeeded();
        if (combatLogText != null)
        {
            combatLogText.text = string.Empty;
        }
    }

    private void ResolveCombatLogTextIfNeeded()
    {
        if (combatLogText != null)
        {
            return;
        }

        GameObject found = FindSceneGameObjectByName(DefaultCombatLogName);
        if (found == null)
        {
            return;
        }

        combatLogText = found.GetComponent<TextMeshProUGUI>();
        if (combatLogText == null)
        {
            combatLogText = found.GetComponentInChildren<TextMeshProUGUI>(true);
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

        GameObject found = FindSceneGameObjectByName(objectName);
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

        GameObject found = FindSceneGameObjectByName(objectName);
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
