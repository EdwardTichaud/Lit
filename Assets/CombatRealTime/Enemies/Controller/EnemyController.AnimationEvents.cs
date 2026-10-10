using UnityEngine;

public sealed partial class EnemyController
{
    [SerializeField] private Transform inputPromptAnchor;
    [SerializeField] private Vector3 inputPromptOffset = new Vector3(0f, 1.25f, 0f);
    private CombatInputWorldPrompt activeInputPrompt;
    public void HandleShowInput(Sprite inputSprite)
    {
        HandleHideInput();

        Transform anchor = inputPromptAnchor;
        if (anchor == null)
        {
            EnemyController currentEnemy = this;
            anchor = currentEnemy != null ? currentEnemy.LockPoint : transform;
        }

        activeInputPrompt = CombatInputWorldPrompt.Show(anchor, inputSprite, inputPromptOffset);
    }

    public void HandleHideInput()
    {
        if (activeInputPrompt != null)
        {
            activeInputPrompt.Hide();
            activeInputPrompt = null;
        }
    }

    public void HandleEnemyAttack(SkillSO skill)
    {
        if (IsFlameDormant) return;
        TraceEnemyEvent(nameof(HandleEnemyAttack));
        this.ExecuteEnemyAttack(skill);
    }

    public void HandleLockEnemyAttackDirection()
    {
        TraceEnemyEvent(nameof(HandleLockEnemyAttackDirection));
        this.GetComponent<EnemyController>()?.LockAttackDirection();
    }

    public void HandleQTE(string input)
    {
        if (IsFlameDormant) return;
        EnemyController enemy = this;
        if (enemy != null) CombatHealthThresholdController.Instance?.OpenAttackQte(enemy, input);
        else CombatHealthThresholdController.Instance?.OpenQte(input);
    }

    public void HandleOpenEnemyReactionOpportunity()
    {
        if (IsFlameDormant) return;
        CombatHealthThresholdController.Instance?.OpenEnemyReactionOpportunity(this);
    }

    private int completedActionSequence = -1;
    public void HandleEndEnemyAttack() => CompleteAction(ActionSequenceId, false);

    private void CompleteAction(int sequence, bool recovery)
    {
        if (IsFlameDormant || !BrainAuthority || sequence != ActionSequenceId || ActiveSkill == null || (sequence == completedActionSequence && !recovery)) return;
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

    public void HandleBeginEnemyRush()
    {
        TraceEnemyEvent(nameof(HandleBeginEnemyRush));
        this.HandleBeginEnemyRush(ResolveEnemyDashTarget());
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
