using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the scene-authored combat panels from the real-time combat state.
/// No panel is created or resolved at runtime: all references are assigned by
/// the combat UI migration utility.
/// </summary>
[DisallowMultipleComponent]
public sealed class RealTimeCombatSceneUiController : MonoBehaviour
{
    public static RealTimeCombatSceneUiController Instance { get; private set; }

    [Header("Entry")]
    [SerializeField] private CanvasGroup combatEngagedPanel;
    [SerializeField] private Animator combatEngagedAnimator;
    [SerializeField] private string combatEngagedTrigger = "CombatEngagedPanel_Trigger";
    [SerializeField, Min(0f)] private float combatEngagedFallbackDuration = 1f;

    [Header("Combat HUD")]
    [SerializeField] private CanvasGroup combatScreenInfosPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI enemyHpText;
    [SerializeField] private Image playerHpFill;
    [SerializeField] private Image enemyHpFill;
    [SerializeField] private TextMeshProUGUI clarityText;
    [SerializeField] private TextMeshProUGUI combatLogText;
    [SerializeField, Min(1)] private int combatLogMaxLines = 6;

    [Header("Results")]
    [SerializeField] private CanvasGroup victoryPanel;
    [SerializeField] private Button victoryContinueButton;
    [SerializeField] private CanvasGroup defeatPanel;
    [SerializeField] private Button defeatReviveButton;
    [SerializeField] private Button defeatQuitButton;

    private readonly List<string> combatLogLines = new List<string>();
    private RealTimeCombatManager manager;
    private Coroutine entryRoutine;
    private bool resultVisible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        SetVisible(combatEngagedPanel, false, false);
        SetVisible(combatScreenInfosPanel, false, false);
        SetVisible(victoryPanel, false, false);
        SetVisible(defeatPanel, false, false);
    }

    private void Start()
    {
        BindManager(RealTimeCombatManager.Instance);
        if (manager != null && manager.IsCombatActive)
        {
            BeginCombatUi();
        }
    }

    private void OnDestroy()
    {
        BindManager(null);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (manager == null && RealTimeCombatManager.Instance != null)
        {
            BindManager(RealTimeCombatManager.Instance);
            if (manager.IsCombatActive && entryRoutine == null && !resultVisible)
            {
                BeginCombatUi();
            }
        }

        if (manager != null && manager.IsCombatActive && !resultVisible)
        {
            RefreshHud();
        }
    }

    public void Configure(
        CanvasGroup engaged,
        Animator engagedAnimator,
        CanvasGroup screenInfos,
        CanvasGroup victory,
        CanvasGroup defeat)
    {
        combatEngagedPanel = engaged;
        combatEngagedAnimator = engagedAnimator;
        combatScreenInfosPanel = screenInfos;
        victoryPanel = victory;
        defeatPanel = defeat;
    }

    public void ShowDefeat()
    {
        CloseCombatPanels();
        resultVisible = true;
        SetVisible(defeatPanel, true, true);
        InputModeCoordinator.Enter(this, InputMode.UserInterface);
        if (defeatReviveButton != null)
        {
            defeatReviveButton.Select();
        }
    }

    private void BindManager(RealTimeCombatManager newManager)
    {
        if (manager == newManager)
        {
            return;
        }

        if (manager != null)
        {
            manager.CombatStateChanged -= OnCombatStateChanged;
            manager.LockChanged -= OnLockChanged;
            manager.ClarityChanged -= OnClarityChanged;
            manager.ReactionWindowChanged -= OnReactionWindowChanged;
            manager.PlayerSkillImpactApplied -= OnPlayerSkillImpactApplied;
            manager.EnemyAttackStarted -= OnEnemyAttackStarted;
            manager.ReactionImpactResolved -= OnReactionImpactResolved;
            manager.PlayerDamaged -= OnPlayerDamaged;
            manager.CombatResolved -= OnCombatResolved;
        }

        manager = newManager;
        if (manager == null)
        {
            return;
        }

        manager.CombatStateChanged += OnCombatStateChanged;
        manager.LockChanged += OnLockChanged;
        manager.ClarityChanged += OnClarityChanged;
        manager.ReactionWindowChanged += OnReactionWindowChanged;
        manager.PlayerSkillImpactApplied += OnPlayerSkillImpactApplied;
        manager.EnemyAttackStarted += OnEnemyAttackStarted;
        manager.ReactionImpactResolved += OnReactionImpactResolved;
        manager.PlayerDamaged += OnPlayerDamaged;
        manager.CombatResolved += OnCombatResolved;
    }

    private void OnCombatStateChanged(bool active)
    {
        if (active)
        {
            BeginCombatUi();
            return;
        }

        if (!resultVisible)
        {
            CloseCombatPanels();
        }
    }

    private void BeginCombatUi()
    {
        resultVisible = false;
        SetVisible(victoryPanel, false, false);
        SetVisible(defeatPanel, false, false);
        ClearCombatLog();
        if (entryRoutine != null)
        {
            StopCoroutine(entryRoutine);
            entryRoutine = null;
        }

        // "EN GARDE !" is a threat cue, not a generic combat-entry banner.
        // CombatThreatPanelController owns it during an authored warning.
        SetVisible(combatEngagedPanel, false, false);
        SetVisible(combatScreenInfosPanel, true, false);
        RefreshHud();
        AppendCombatLog("Combat engage.");
    }

    private IEnumerator PlayEntryRoutine()
    {
        SetVisible(combatScreenInfosPanel, false, false);
        SetVisible(combatEngagedPanel, true, false);
        if (combatEngagedAnimator != null && !string.IsNullOrWhiteSpace(combatEngagedTrigger))
        {
            combatEngagedAnimator.ResetTrigger(combatEngagedTrigger);
            combatEngagedAnimator.SetTrigger(combatEngagedTrigger);
        }

        yield return null;
        float duration = ResolveEntryDuration();
        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(duration);
        }

        SetVisible(combatEngagedPanel, false, false);
        if (manager != null && manager.IsCombatActive && !resultVisible)
        {
            SetVisible(combatScreenInfosPanel, true, false);
            RefreshHud();
            AppendCombatLog("Combat engage.");
        }

        entryRoutine = null;
    }

    private float ResolveEntryDuration()
    {
        if (combatEngagedAnimator == null)
        {
            return combatEngagedFallbackDuration;
        }

        AnimatorClipInfo[] clips = combatEngagedAnimator.GetCurrentAnimatorClipInfo(0);
        float duration = 0f;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].clip != null)
            {
                duration = Mathf.Max(duration, clips[i].clip.length / Mathf.Max(0.01f, combatEngagedAnimator.speed));
            }
        }

        return duration > 0f ? duration : combatEngagedFallbackDuration;
    }

    private void OnLockChanged(RealTimeCombatEnemy enemy)
    {
        if (enemy != null)
        {
            AppendCombatLog("Cible verrouillee : " + ResolveEnemyName(enemy) + ".");
        }

        RefreshHud();
    }

    private void OnClarityChanged(float clarity, CombatClarityRank rank)
    {
        if (clarityText != null)
        {
            clarityText.text = "Clarte " + Mathf.RoundToInt(clarity) + " | " + rank;
        }
    }

    private void OnReactionWindowChanged(RealTimeCombatReactionWindow window)
    {
        if (window.IsOpen && window.Skill != null)
        {
            SetText(stateText, "Reaction : " + string.Join(" / ", window.Skill.AcceptedEnemyReactions));
            AppendCombatLog("Fenetre de reaction ouverte.");
        }
        else
        {
            SetText(stateText, "Combat en cours");
        }
    }

    private void OnPlayerSkillImpactApplied(SkillSO skill, int damage)
    {
        AppendCombatLog("Lucian inflige " + damage + " degats" + (skill != null ? " avec " + skill.SkillName : string.Empty) + ".");
        RefreshHud();
    }

    private void OnEnemyAttackStarted(SkillSO skill, int damage)
    {
        AppendCombatLog(ResolveEnemyName(manager != null ? manager.EngagedEnemy : null) + " prepare " + (skill != null ? skill.SkillName : "une attaque") + ".");
    }

    private void OnReactionImpactResolved(SkillSO skill, bool succeeded)
    {
        AppendCombatLog(succeeded ? "Reaction reussie." : "Reaction echouee.");
    }

    private void OnPlayerDamaged(int damage)
    {
        AppendCombatLog("Lucian subit " + damage + " degats.");
        RefreshHud();
    }

    private void OnCombatResolved(bool playerVictory)
    {
        if (!playerVictory)
        {
            return;
        }

        CloseCombatPanels();
        resultVisible = true;
        SetVisible(victoryPanel, true, true);
        InputModeCoordinator.Enter(this, InputMode.UserInterface);
        victoryContinueButton?.Select();
    }

    private void RefreshHud()
    {
        if (manager == null)
        {
            return;
        }

        SquadCharacterController player = manager.PlayerRoot != null
            ? manager.PlayerRoot.GetComponentInChildren<SquadCharacterController>(true)
            : null;
        CombatHealth playerHealth = manager.PlayerRoot != null
            ? manager.PlayerRoot.GetComponentInChildren<CombatHealth>(true)
            : null;
        RealTimeCombatEnemy enemy = manager.EngagedEnemy;
        CombatHealth enemyHealth = enemy != null ? enemy.Health : null;

        int playerCurrent = player != null ? player.CurrentHp : playerHealth != null ? playerHealth.CurrentHp : 0;
        int playerMaximum = player != null ? player.MaxHp : playerHealth != null ? playerHealth.MaxHp : 1;
        int enemyCurrent = enemyHealth != null ? enemyHealth.CurrentHp : 0;
        int enemyMaximum = enemyHealth != null ? enemyHealth.MaxHp : 1;

        SetText(titleText, enemy != null ? ResolveEnemyName(enemy) : "Combat");
        SetText(playerHpText, "Lucian\n" + Mathf.Max(0, playerCurrent) + "/" + Mathf.Max(1, playerMaximum) + " PV");
        SetText(enemyHpText, (enemy != null ? ResolveEnemyName(enemy) : "Ennemi") + "\n" + Mathf.Max(0, enemyCurrent) + "/" + Mathf.Max(1, enemyMaximum) + " PV");
        SetFill(playerHpFill, playerCurrent, playerMaximum);
        SetFill(enemyHpFill, enemyCurrent, enemyMaximum);
        OnClarityChanged(manager.Clarity, manager.ClarityRank);
        if (stateText != null && string.IsNullOrWhiteSpace(stateText.text))
        {
            stateText.text = "Combat en cours";
        }
    }

    private void CloseCombatPanels()
    {
        if (entryRoutine != null)
        {
            StopCoroutine(entryRoutine);
            entryRoutine = null;
        }

        SetVisible(combatEngagedPanel, false, false);
        SetVisible(combatScreenInfosPanel, false, false);
        ClearCombatLog();
    }

    private void AppendCombatLog(string message)
    {
        if (combatLogText == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        combatLogLines.Add(message.Trim());
        while (combatLogLines.Count > Mathf.Max(1, combatLogMaxLines))
        {
            combatLogLines.RemoveAt(0);
        }

        combatLogText.text = string.Join("\n", combatLogLines);
    }

    private void ClearCombatLog()
    {
        combatLogLines.Clear();
        SetText(combatLogText, string.Empty);
    }

    private void OnVictoryContinue()
    {
        CloseResultPanels();
    }

    private void OnDefeatRevive()
    {
        if (defeatReviveButton != null) defeatReviveButton.interactable = false;
        if (defeatQuitButton != null) defeatQuitButton.interactable = false;
        RealTimeCombatManager.Instance?.ReviveFromDefeat();
    }

    private void OnDefeatQuit()
    {
        if (defeatReviveButton != null) defeatReviveButton.interactable = false;
        if (defeatQuitButton != null) defeatQuitButton.interactable = false;
        RealTimeCombatManager.QuitFromDefeat();
    }

    private void CloseResultPanels()
    {
        SetVisible(victoryPanel, false, false);
        SetVisible(defeatPanel, false, false);
        resultVisible = false;
        InputModeCoordinator.Exit(this);
    }

    private void OnEnable()
    {
        if (victoryContinueButton != null) victoryContinueButton.onClick.AddListener(OnVictoryContinue);
        if (defeatReviveButton != null) defeatReviveButton.onClick.AddListener(OnDefeatRevive);
        if (defeatQuitButton != null) defeatQuitButton.onClick.AddListener(OnDefeatQuit);
    }

    private void OnDisable()
    {
        if (victoryContinueButton != null) victoryContinueButton.onClick.RemoveListener(OnVictoryContinue);
        if (defeatReviveButton != null) defeatReviveButton.onClick.RemoveListener(OnDefeatRevive);
        if (defeatQuitButton != null) defeatQuitButton.onClick.RemoveListener(OnDefeatQuit);
        InputModeCoordinator.Exit(this);
    }

    private static string ResolveEnemyName(RealTimeCombatEnemy enemy)
    {
        CharacterInfo info = enemy != null ? enemy.GetComponentInChildren<CharacterInfo>(true) : null;
        return info != null && info.CharacterData != null
            ? info.CharacterData.ResolveDisplayName()
            : enemy != null ? enemy.name : "Ennemi";
    }

    private static void SetVisible(CanvasGroup group, bool visible, bool blocksRaycasts)
    {
        if (group == null)
        {
            return;
        }

        group.gameObject.SetActive(visible);
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible && blocksRaycasts;
        group.blocksRaycasts = visible && blocksRaycasts;
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private static void SetFill(Image image, int current, int maximum)
    {
        if (image != null)
        {
            image.fillAmount = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
        }
    }
}
