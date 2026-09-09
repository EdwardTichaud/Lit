using UnityEngine;

public sealed partial class EnemyController
{
    [SerializeField] private Transform inputPromptAnchor;
    [SerializeField] private Vector3 inputPromptOffset = new Vector3(0f, 1.25f, 0f);
    private CombatInputWorldPrompt activeInputPrompt;
    public void ShowInput(Sprite inputSprite)
    {
        HideInput();

        Transform anchor = inputPromptAnchor;
        if (anchor == null)
        {
            EnemyController currentEnemy = this;
            anchor = currentEnemy != null ? currentEnemy.LockPoint : transform;
        }

        activeInputPrompt = CombatInputWorldPrompt.Show(anchor, inputSprite, inputPromptOffset);
    }

    public void HideInput()
    {
        if (activeInputPrompt != null)
        {
            activeInputPrompt.Hide();
            activeInputPrompt = null;
        }
    }

    public void EnemyAttack(SkillSO skill)
    {
        TraceEnemyEvent(nameof(EnemyAttack));
        this.ExecuteEnemyAttack(skill);
    }

    public void LockEnemyAttackDirection()
    {
        TraceEnemyEvent(nameof(LockEnemyAttackDirection));
        this.GetComponent<EnemyController>()?.LockAttackDirection();
    }

    public void QTE(string input)
    {
        EnemyController enemy = this;
        if (enemy != null) CombatHealthThresholdController.Instance?.OpenAttackQte(enemy, input);
        else CombatHealthThresholdController.Instance?.OpenQte(input);
    }

    public void OpenEnemyReactionOpportunity()
    {
        CombatHealthThresholdController.Instance?.OpenEnemyReactionOpportunity(this);
    }

    private int completedActionSequence = -1;
    public void EndEnemyAttack() => CompleteAction(ActionSequenceId, false);

    private void CompleteAction(int sequence, bool recovery)
    {
        if (!BrainAuthority || sequence != ActionSequenceId || ActiveSkill == null || (sequence == completedActionSequence && !recovery)) return;
        completedActionSequence = sequence;
        CombatHealthThresholdController.Instance?.EndEnemyReactionAction(this);
        if (IsAutonomousActionActive)
        {
            if (recovery) ResolveAttackSafetyTimeout();
            else ResolveAnimationAttackEnded();
            return;
        }
        if (CombatHealthThresholdController.Instance?.TryCompleteFailureRetaliation(this) == true) return;
        CompleteEnemyAttackWhenGrounded(() =>
        {
            if (sequence != ActionSequenceId || ActiveSkill == null) return;
            RealTimeCombatManager.Instance?.CompleteEnemyAttack(this);
            ReturnToIdle();
        });
    }

    public void BeginEnemyRush()
    {
        TraceEnemyEvent(nameof(BeginEnemyRush));
        this.BeginEnemyRush(ResolveEnemyDashTarget());
    }

    private void TraceEnemyEvent(string eventName)
    {
        EnemyController currentEnemy = this;
        currentEnemy?.GetComponent<EnemyController>()?.TraceAnimationEvent(eventName);
    }

    private Transform ResolveEnemyDashTarget()
    {
        EnemyController brain = this.GetComponent<EnemyController>();
        if (brain != null && brain.HasProfile) return brain.Target != null ? brain.Target.transform : null;
        Transform player = LocalPlayerContext.LocalCharacterRoot;
        if (player != null)
        {
            return player;
        }

        return RealTimeCombatManager.Instance != null
            ? RealTimeCombatManager.Instance.PlayerRoot
            : null;
    }
}
