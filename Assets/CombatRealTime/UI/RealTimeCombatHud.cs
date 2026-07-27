using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RealTimeCombatHud : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject lockIndicator;
    [SerializeField] private TMP_Text lockedEnemyText;
    [SerializeField] private TMP_Text clarityText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text reactionPromptText;

    private RealTimeCombatManager manager;

    private void OnEnable()
    {
        manager = RealTimeCombatManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.LockChanged += OnLockChanged;
        manager.ClarityChanged += OnClarityChanged;
        manager.ReactionWindowChanged += OnReactionWindowChanged;
        manager.CombatStateChanged += OnCombatStateChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (manager == null)
        {
            return;
        }

        manager.LockChanged -= OnLockChanged;
        manager.ClarityChanged -= OnClarityChanged;
        manager.ReactionWindowChanged -= OnReactionWindowChanged;
        manager.CombatStateChanged -= OnCombatStateChanged;
        manager = null;
    }

    private void OnLockChanged(RealTimeCombatEnemy enemy)
    {
        if (lockIndicator != null) lockIndicator.SetActive(enemy != null);
        if (lockedEnemyText != null) lockedEnemyText.text = enemy != null ? enemy.name : string.Empty;
    }

    private void OnClarityChanged(float clarity, CombatClarityRank rank)
    {
        if (clarityText != null) clarityText.text = Mathf.RoundToInt(clarity).ToString();
        if (rankText != null) rankText.text = rank.ToString();
    }

    private void OnReactionWindowChanged(RealTimeCombatReactionWindow window)
    {
        if (reactionPromptText == null)
        {
            return;
        }

        if (!window.IsOpen || window.Skill == null)
        {
            reactionPromptText.text = string.Empty;
            return;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < window.Skill.AcceptedEnemyReactions.Count; i++)
        {
            if (i > 0) builder.Append(" + ");
            builder.Append(window.Skill.AcceptedEnemyReactions[i]);
        }

        reactionPromptText.text = builder.ToString();
    }

    private void OnCombatStateChanged(bool active)
    {
        if (root != null) root.SetActive(active);
    }

    private void Refresh()
    {
        if (root != null) root.SetActive(manager != null && manager.IsCombatActive);
        if (manager == null) return;
        OnLockChanged(manager.LockedEnemy);
        OnClarityChanged(manager.Clarity, manager.ClarityRank);
    }
}
