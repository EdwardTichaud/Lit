using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatReactionTelegraphController : MonoBehaviour
{
    public static CombatReactionTelegraphController Instance { get; private set; }

    [SerializeField] private RealTimeCombatManager combatManager;
    [SerializeField] private RealTimeCombatReactionPrompt prompt;
    [SerializeField] private CombatImpactFeedbackController impactFeedback;

    private RealTimeCombatEnemy activeEnemy;
    private SkillSO activeSkill;
    private readonly List<GameObject> activeAlerts = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (combatManager != null)
        {
            combatManager.ReactionWindowChanged += OnReactionWindowChanged;
            combatManager.ReactionImpactResolved += OnReactionImpactResolved;
            combatManager.CombatStateChanged += OnCombatStateChanged;
            combatManager.LockChanged += OnLockChanged;
        }
    }

    private void OnDisable()
    {
        if (combatManager != null)
        {
            combatManager.ReactionWindowChanged -= OnReactionWindowChanged;
            combatManager.ReactionImpactResolved -= OnReactionImpactResolved;
            combatManager.CombatStateChanged -= OnCombatStateChanged;
            combatManager.LockChanged -= OnLockChanged;
        }
        Clear();
    }

    private void OnDestroy()
    {
        Clear();
        if (Instance == this) Instance = null;
    }

    public void BeginTelegraph(RealTimeCombatEnemy enemy)
    {
        if (enemy == null || enemy.ActiveSkill == null) return;
        Clear();
        activeEnemy = enemy;
        activeSkill = enemy.ActiveSkill;
        CombatReactionTelegraphProfile profile = activeSkill.ReactionTelegraph;
        if (!profile.enabled) return;

        ResolveReferences();
        prompt?.BeginTelegraph(enemy.LockPoint, activeSkill);
        PlayAlert(profile.threatColor, profile);
        if (profile.anticipationAudio != null) AudioManager.PlayClipAtPoint(profile.anticipationAudio, enemy.LockPoint.position);
    }

    public void Clear()
    {
        prompt?.Clear();
        for (int i = activeAlerts.Count - 1; i >= 0; i--)
        {
            if (activeAlerts[i] != null) Destroy(activeAlerts[i]);
        }
        activeAlerts.Clear();
        activeEnemy = null;
        activeSkill = null;
    }

    private void OnReactionWindowChanged(RealTimeCombatReactionWindow window)
    {
        if (!window.IsOpen)
        {
            prompt?.Clear();
            return;
        }

        if (window.Enemy == null || window.Skill == null) return;
        activeEnemy = window.Enemy.GetComponent<RealTimeCombatEnemy>();
        activeSkill = window.Skill;
        CombatReactionTelegraphProfile profile = activeSkill.ReactionTelegraph;
        if (!profile.enabled) return;

        ResolveReferences();
        prompt?.OpenPerfectWindow(window.Enemy, activeSkill);
        PlayAlert(profile.perfectWindowColor, profile);
        if (profile.perfectWindowAudio != null) AudioManager.PlayClipAtPoint(profile.perfectWindowAudio, window.Enemy.position);
        if (profile.usePerfectWindowSlowMotion)
        {
            impactFeedback?.PlayReactionSlowMotion(profile.perfectWindowTimeScale, profile.perfectWindowSlowMotionSeconds);
        }
    }

    private void OnReactionImpactResolved(SkillSO skill, bool succeeded)
    {
        if (activeSkill != skill) return;
        CombatReactionTelegraphProfile profile = activeSkill.ReactionTelegraph;
        prompt?.Resolve(succeeded);
        if (succeeded && profile.successfulReactionAudio != null && activeEnemy != null)
        {
            AudioManager.PlayClipAtPoint(profile.successfulReactionAudio, activeEnemy.LockPoint.position);
        }
        activeEnemy = null;
        activeSkill = null;
    }

    private void OnCombatStateChanged(bool active)
    {
        if (!active) Clear();
    }

    private void OnLockChanged(RealTimeCombatEnemy enemy)
    {
        if (enemy == null || enemy != activeEnemy) Clear();
    }

    private void PlayAlert(Color color, CombatReactionTelegraphProfile profile)
    {
        if (activeEnemy == null || profile.alertPrefab == null) return;
        Vector3 position = activeEnemy.LockPoint.position + Vector3.up * profile.heightOffset;
        GameObject alert = Instantiate(profile.alertPrefab, position, Quaternion.identity);
        activeAlerts.Add(alert);
        alert.GetComponent<AttackLightAlert>()?.Configure(color, profile.fadeSeconds, Camera.main);
    }

    private void ResolveReferences()
    {
        if (combatManager == null) combatManager = GetComponent<RealTimeCombatManager>();
        if (impactFeedback == null) impactFeedback = GetComponent<CombatImpactFeedbackController>();
        if (prompt == null) prompt = FindAnyObjectByType<RealTimeCombatReactionPrompt>(FindObjectsInactive.Include);
    }
}
