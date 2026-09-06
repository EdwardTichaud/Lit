using UnityEngine;
using UnityEngine.Serialization;

public enum EnemyAttackReaction { Dodge, Counter }

public sealed partial class CombatHealthThresholdController
{
    [Header("Invisible enemy reactions")]
    [FormerlySerializedAs("attackQteDuration"), SerializeField, Min(.05f)] private float dodgeWindowSeconds = .5f;
    [SerializeField, Min(.01f)] private float counterWindowSeconds = .2f;
    [FormerlySerializedAs("attackQteTimeScale"), SerializeField, Range(.01f, 1f)] private float reactionTimeScale = .4f;
    [SerializeField] private bool logEnemyReactions;
    private bool attackQteActive, attackDodgeProtected;
    private bool dodgeAwaitingRelease, counterAwaitingRelease;
    private RealTimeCombatEnemy attackQteEnemy;
    private Transform attackReactionVictim;
    private SkillSO attackQteSkill;
    private int attackQteActionId;
    private double attackReactionOpenedAt, dodgeDeadline, counterDeadline;
    private TimeManager reactionTimeManager;
    private TimeManager.TimeRequestHandle reactionSlowMotionHandle;
    private readonly System.Collections.Generic.HashSet<AnimationClip> legacyReactionClips =
        new System.Collections.Generic.HashSet<AnimationClip>();

    private void OnValidate()
    {
        dodgeWindowSeconds = Mathf.Max(.05f, dodgeWindowSeconds);
        counterWindowSeconds = Mathf.Clamp(counterWindowSeconds, .01f, dodgeWindowSeconds);
        reactionTimeScale = Mathf.Clamp(reactionTimeScale, .01f, 1f);
    }

    public void OpenAttackQte(RealTimeCombatEnemy enemy, string input)
    {
        AnimationClip clip = enemy != null ? enemy.ActiveSkill?.AnimationClip : null;
        if (clip != null && legacyReactionClips.Add(clip))
            Debug.LogWarning("[EnemyReaction] Remplacer QTE(input) par OpenEnemyReactionOpportunity sur '" + clip.name + "'.", enemy);
        OpenEnemyReactionOpportunity(enemy);
    }

    public void OpenEnemyReactionOpportunity(RealTimeCombatEnemy enemy)
    {
        if (combatManager == null) combatManager = GetComponent<RealTimeCombatManager>();
        if (combatInput == null) combatInput = GetComponent<RealTimeCombatInput>();
        var network = Unity.Netcode.NetworkManager.Singleton;
        if (network != null && network.IsListening && !network.IsServer) return;
        if (!isActiveAndEnabled || combatManager == null || !combatManager.IsCombatActive ||
            combatManager.IsCinematicSequenceActive || enemy == null || enemy != combatManager.EngagedEnemy ||
            enemy.ActiveSkill == null || combatManager.PlayerRoot == null || qteOpen ||
            (state != SequenceState.Idle && state != SequenceState.Pending)) return;
        if (attackQteEnemy == enemy && attackQteActionId == enemy.ActionSequenceId && attackQteSkill == enemy.ActiveSkill)
        {
            TraceEnemyReaction("evenement duplique ignore");
            return;
        }
        ClearAttackReaction();
        attackQteEnemy = enemy;
        attackQteSkill = enemy.ActiveSkill;
        attackQteActionId = enemy.ActionSequenceId;
        attackReactionVictim = combatManager.PlayerRoot;
        attackReactionOpenedAt = Time.unscaledTimeAsDouble;
        dodgeDeadline = attackReactionOpenedAt + Mathf.Max(.05f, dodgeWindowSeconds);
        counterDeadline = attackReactionOpenedAt + Mathf.Clamp(counterWindowSeconds, .01f, Mathf.Max(.05f, dodgeWindowSeconds));
        dodgeAwaitingRelease = combatInput != null && combatInput.IsReactionButtonHeld(EnemyAttackReaction.Dodge);
        counterAwaitingRelease = combatInput != null && combatInput.IsReactionButtonHeld(EnemyAttackReaction.Counter);
        attackQteActive = true;
        if (reactionTimeScale < 1f)
        {
            reactionTimeManager = TimeManager.EnsureInstance();
            if (reactionTimeManager != null)
                reactionSlowMotionHandle = reactionTimeManager.AcquireGlobal(Mathf.Clamp(reactionTimeScale, .01f, 1f), this);
        }
        TraceEnemyReaction("ouverture B/Y sans UI");
    }

    private bool AttackReactionStillValid() => combatManager != null && combatManager.IsCombatActive &&
        !combatManager.IsCinematicSequenceActive && attackQteEnemy != null && attackQteEnemy.isActiveAndEnabled &&
        attackReactionVictim != null && attackReactionVictim.gameObject.activeInHierarchy &&
        combatManager.PlayerRoot == attackReactionVictim && combatManager.EngagedEnemy == attackQteEnemy &&
        attackQteEnemy.ActiveSkill == attackQteSkill && attackQteEnemy.ActionSequenceId == attackQteActionId &&
        (attackQteEnemy.Health == null || !attackQteEnemy.Health.IsDead);

    private void Update()
    {
        if (attackQteEnemy == null && !attackQteActive && !attackDodgeProtected) return;
        if (!AttackReactionStillValid()) { ClearAttackReaction(); return; }
        if (combatInput != null && !combatInput.IsInputActive) CloseAttackQte();
        if (attackQteActive && Time.unscaledTimeAsDouble >= dodgeDeadline) CloseAttackQte();
    }

    public void ReleaseEnemyReactionButton(EnemyAttackReaction reaction)
    {
        if (reaction == EnemyAttackReaction.Dodge) dodgeAwaitingRelease = false;
        else counterAwaitingRelease = false;
    }

    // True means the press was handled, not that a defence necessarily succeeded.
    public bool TryHandleEnemyReaction(EnemyAttackReaction reaction)
    {
        if (!IsReactionPressEligible(reaction, Time.unscaledTimeAsDouble)) return false;
        var enemy = attackQteEnemy;
        var skill = attackQteSkill;
        if (reaction == EnemyAttackReaction.Dodge)
        {
            var mobility = combatManager.GetComponent<CombatMobilityController>();
            if (mobility != null && mobility.TryDodgeImmediate())
            {
                attackDodgeProtected = true;
                TraceEnemyReaction("roulade acceptee : protection de ce coup uniquement");
                CloseAttackQte();
            }
            else TraceEnemyReaction("roulade refusee par la mobilite, aucune protection");
            return true; // Never buffer a failed timed dodge into a later action.
        }
        var counter = CounterSkillCombatController.Instance;
        if (counter == null || !counter.TryStartFromSuccessfulQte(enemy, skill)) return false;
        TraceEnemyReaction("contre cinematique demarre");
        CloseAttackQte();
        return true;
    }

    private bool IsReactionPressEligible(EnemyAttackReaction reaction, double now) =>
        attackQteActive && AttackReactionStillValid() && now >= attackReactionOpenedAt &&
        now < (reaction == EnemyAttackReaction.Dodge ? dodgeDeadline : counterDeadline) &&
        !(reaction == EnemyAttackReaction.Dodge ? dodgeAwaitingRelease : counterAwaitingRelease);

    public bool IsAttackDodged(RealTimeCombatEnemy enemy, Transform victim, SkillSO skill)
    {
        return attackDodgeProtected && AttackReactionStillValid() && enemy == attackQteEnemy &&
            skill == attackQteSkill && victim != null &&
            (victim == attackReactionVictim || victim.IsChildOf(attackReactionVictim));
    }

    // Impact closes eligibility, but protection survives all windows of this action.
    public void CancelAttackQte(RealTimeCombatEnemy enemy)
    {
        if (enemy == attackQteEnemy) CloseAttackQte();
    }

    public void EndEnemyReactionAction(RealTimeCombatEnemy enemy)
    {
        if (enemy != attackQteEnemy) return;
        CloseAttackQte();
        attackDodgeProtected = false;
        // Keep the identity until the skill ends: late events cannot reopen it
        // while the physical motor is still waiting for grounded recovery.
    }

    private void CloseAttackQte()
    {
        attackQteActive = false;
        if (reactionTimeManager != null) reactionTimeManager.Release(reactionSlowMotionHandle);
        reactionSlowMotionHandle = default;
        reactionTimeManager = null;
    }

    private void ClearAttackReaction()
    {
        CloseAttackQte();
        attackDodgeProtected = false;
        attackQteEnemy = null;
        attackQteSkill = null;
        attackReactionVictim = null;
    }

    private void TraceEnemyReaction(string message)
    {
        if (logEnemyReactions) Debug.Log("[EnemyReaction] " + message + " | action=" + attackQteActionId, this);
    }
}
