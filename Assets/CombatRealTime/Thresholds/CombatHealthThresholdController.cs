using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns optional authored enemy-health breakpoints. Threshold sequences are
/// Animator-driven and deliberately independent from combat Timelines.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatHealthThresholdController : MonoBehaviour
{
    private enum SequenceState { Idle, Pending, PlayingSequence, SuccessPresentation, FailureRetaliation }

    private struct StageCapsule
    {
        public Vector3 localCenter;
        public float radius;
        public float height;
        public string source;
    }

    [SerializeField] private RealTimeCombatManager combatManager;
    [SerializeField] private RealTimeCombatInput combatInput;
    [SerializeField] private QTEPanelController qtePanel;
    [SerializeField] private CombatLocalTimeField qteLocalTimeField;
    [SerializeField, Min(0.25f)] private float failureRetaliationGraceSeconds = 1.5f;
    [Header("Threshold Stage Pose")]
    [SerializeField, Min(0.1f)] private float stageDistance = 2f;
    [SerializeField] private LayerMask stageGroundMask = Physics.DefaultRaycastLayers;
    [SerializeField] private LayerMask stageBlockingMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.01f)] private float stageRetrySeconds = 0.15f;
    [SerializeField, Min(0f)] private float stageClearance = 0.03f;
    [SerializeField] private bool logDiagnostics = true;

    private readonly Dictionary<RealTimeCombatEnemy, HashSet<CombatHealthThresholdStage>> resolvedStages =
        new Dictionary<RealTimeCombatEnemy, HashSet<CombatHealthThresholdStage>>();
    private readonly List<RealTimeCombatEnemyBehaviour> suspendedEnemies = new List<RealTimeCombatEnemyBehaviour>();
    private readonly HashSet<CharacterData> invalidDataReported = new HashSet<CharacterData>();

    [Header("QTE")]
    [Min(0.01f), Tooltip("Duree reelle disponible pour chaque QTE, independamment du ralentissement local.")]
    public float qteDurationSeconds = 0.5f;
    [SerializeField, Min(0.1f), Tooltip("Securite auteur : temps reel maximal avant que l'etat QTE emette son Animation Event QTE(input).")]
    private float qteEventTimeoutSeconds = 3f;
    private static readonly float[] StageAngles = { 0f, 15f, -15f, 30f, -30f, 45f, -45f };

    private InputActionMap qteMap;
    private InputAction qteYAction;
    private InputAction qteBAction;
    private InputAction qteAAction;
    private InputAction qteXAction;
    private RealTimeCombatEnemy activeEnemy;
    private CombatHealthThresholdStage activeStage;
    private ThresholdSequence activeSequence;
    private SequenceState state;
    private bool qteOpen;
    private int expectedQteCount;
    private int openedQteCount;
    private int completedQteCount;
    private bool fallbackStageCapsuleLogged;
    private CombatThresholdQteInput expectedQteInput;
    private bool waitingForExpectedRelease;
    private bool playerLockHeld;
    private bool failureCompletionRequested;
    private bool successResultResolved;
    private bool thresholdKillApplied;
    private int sessionToken;
    private Coroutine pendingRoutine;
    private Coroutine qteRoutine;
    private Coroutine successRoutine;
    private Coroutine qteEventWatchdogRoutine;
    private Animator boundPlayerAnimator;
    private RuntimeAnimatorController playerRuntimeControllerBeforeSequence;
    private AnimatorOverrideController playerThresholdOverrideController;
    private AnimationClip boundQteClip;

    public static CombatHealthThresholdController Instance { get; private set; }
    public bool IsSequenceActive => state == SequenceState.Pending ||
                                    state == SequenceState.PlayingSequence ||
                                    state == SequenceState.SuccessPresentation;

    /// <summary>Prevents a second hit or a new retaliation from racing an armed stage.</summary>
    public bool BlocksEnemyActions(RealTimeCombatEnemy enemy)
    {
        return enemy != null && enemy == activeEnemy &&
               (state == SequenceState.Pending || state == SequenceState.PlayingSequence ||
                state == SequenceState.SuccessPresentation);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();
        ResolveQteInput();
    }

    private void OnDisable()
    {
        AbortActiveSequence("desactivation");
        SetQteInputEnabled(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SetQteInputEnabled(false);
        ReleaseQteSlowMotion();
    }

    /// <summary>
    /// Called before CombatHealth receives damage. Returns the amount that may
    /// pass through; an armed stage clamps exactly to its threshold.
    /// </summary>
    public bool TryPrepareDamage(RealTimeCombatEnemy enemy, int requestedDamage, out int allowedDamage)
    {
        allowedDamage = Mathf.Max(0, requestedDamage);
        if (enemy == null || requestedDamage <= 0 || state != SequenceState.Idle)
        {
            return false;
        }

        CombatHealth health = enemy.Health;
        CharacterData data = ResolveCharacterData(enemy);
        if (health == null || data == null || !data.isEnemy || !data.enableCombatHealthThresholds || health.IsDead)
        {
            return false;
        }

        CombatHealthThresholdStage next = GetNextValidStage(enemy, data);
        if (next == null)
        {
            return false;
        }

        int thresholdHp = Mathf.Clamp(Mathf.CeilToInt(health.MaxHp * (next.healthPercent / 100f)), 1, health.MaxHp - 1);
        if (health.CurrentHp <= thresholdHp || health.CurrentHp - requestedDamage > thresholdHp)
        {
            return false;
        }

        activeEnemy = enemy;
        activeStage = next;
        state = SequenceState.Pending;
        allowedDamage = health.CurrentHp - thresholdHp;
        Trace("Palier arme | enemy='" + enemy.name + "' | hp=" + health.CurrentHp + " -> " + thresholdHp +
              " | stage=" + next.healthPercent + "%.");
        return true;
    }

    /// <summary>Called only after the clamped health change has been committed.</summary>
    public void NotifyThresholdDamageApplied(RealTimeCombatEnemy enemy)
    {
        if (state != SequenceState.Pending || enemy == null || enemy != activeEnemy || activeStage == null)
        {
            return;
        }

        if (pendingRoutine != null) StopCoroutine(pendingRoutine);
        pendingRoutine = StartCoroutine(BeginWhenCombatIsAvailable(++sessionToken));
    }

    /// <summary>Animation Event placed on the configured failure-retaliation skill.</summary>
    public bool TryResolveThresholdFailureImpact(RealTimeCombatEnemy enemy)
    {
        if (state != SequenceState.FailureRetaliation || enemy == null || enemy != activeEnemy || activeStage == null)
        {
            return false;
        }

        int applied = combatManager != null
            ? combatManager.ApplyEnemySkillDamageToPlayer(enemy, activeSequence.failureRetaliationSkill)
            : 0;
        if (applied > 0)
        {
            combatManager?.ApplyThresholdFailureKnockback(enemy, 3f);
        }
        Trace("Impact de riposte palier | enemy='" + enemy.name + "' | damage=" + applied + ".");
        return true;
    }

    public void ResolveThresholdFailureImpact(RealTimeCombatEnemy enemy)
    {
        TryResolveThresholdFailureImpact(enemy);
    }

    /// <summary>Allows the existing EndEnemyAttack event to finish a failed QTE attack.</summary>
    public bool TryCompleteFailureRetaliation(RealTimeCombatEnemy enemy)
    {
        if (state != SequenceState.FailureRetaliation || enemy == null || enemy != activeEnemy)
        {
            return false;
        }

        RequestFailureRetaliationCompletion(enemy);
        return true;
    }

    public void AbortActiveSequence(string reason)
    {
        if (state == SequenceState.Idle)
        {
            return;
        }

        sessionToken++;
        if (pendingRoutine != null) StopCoroutine(pendingRoutine);
        if (qteRoutine != null) StopCoroutine(qteRoutine);
        if (successRoutine != null) StopCoroutine(successRoutine);
        if (qteEventWatchdogRoutine != null) StopCoroutine(qteEventWatchdogRoutine);
        pendingRoutine = null;
        qteRoutine = null;
        successRoutine = null;
        qteEventWatchdogRoutine = null;
        SetQteInputEnabled(false);
        ReleaseQteSlowMotion();
        qteOpen = false;
        qtePanel?.HideImmediate();

        state = SequenceState.Idle;

        ClearPlayerThresholdVisualState();
        RestoreCombatOwnership();
        Trace("Session annulee | reason=" + reason + ".");
        ClearActive();
    }

    private IEnumerator BeginWhenCombatIsAvailable(int token)
    {
        yield return null;
        while (token == sessionToken && state == SequenceState.Pending)
        {
            ResolveReferences();
            if (combatManager == null || activeEnemy == null || activeStage == null ||
                !combatManager.IsCombatActive || combatManager.EngagedEnemy != activeEnemy ||
                (activeEnemy.Health != null && activeEnemy.Health.IsDead))
            {
                AbortActiveSequence("combat ou cible invalide");
                yield break;
            }

            // Let an already-authored enemy action complete, but do not allow
            // the AI to arm another one while the stage is pending.
            if (!activeEnemy.HasRetaliationPending && !combatManager.IsCinematicSequenceActive)
            {
                if (TryResolveStagePose(out Vector3 playerPosition, out Quaternion playerRotation, out Quaternion enemyRotation, out string poseIssue))
                {
                    if (StartSequence(token, playerPosition, playerRotation, enemyRotation))
                    {
                        yield break;
                    }
                }

                Trace("Palier en attente de pose face-a-face | " + poseIssue);
            }

            yield return new WaitForSecondsRealtime(stageRetrySeconds);
        }
    }

    private bool StartSequence(int token, Vector3 playerPosition, Quaternion playerRotation, Quaternion enemyRotation)
    {
        activeSequence = activeStage != null ? activeStage.sequence : null;
        if (token != sessionToken || activeSequence == null || !activeSequence.IsComplete)
        {
            AbortActiveSequence("ThresholdSequence invalide");
            return true;
        }

        combatManager.CancelPlayerActionForCinematic();
        SuspendEncounter();
        combatManager.SetCinematicSequenceActive(true);
        // A threshold is in-place and owns no virtual camera. Keep UCC's look
        // input alive while its movement lock still rejects locomotion.
        playerLockHeld = combatManager.TryLockPlayerForCinematic(disableGameplayInput: false);
        if (!ApplyStagePose(playerPosition, playerRotation, enemyRotation, out string placementError))
        {
            RestoreCombatOwnership();
            state = SequenceState.Pending;
            Trace("Pose de palier differee | " + placementError);
            return false;
        }

        Animator playerAnimator = combatManager.PlayerAnimator;
        string qteStateName = activeSequence.PlayerQteStateName;
        int qteStateHash = Animator.StringToHash(qteStateName);
        string successStateName = activeSequence.SuccessPlayerStateName;
        int successStateHash = Animator.StringToHash(successStateName);
        if (playerAnimator == null || !playerAnimator.HasState(0, qteStateHash) ||
            !playerAnimator.HasState(0, successStateHash))
        {
            AbortActiveSequence("etat generique de palier Lucian introuvable (QTE='" + qteStateName +
                                "', success='" + successStateName + "')");
            return true;
        }

        if (!BindSequenceAnimationClips(playerAnimator, out string bindingIssue))
        {
            AbortActiveSequence("binding clips de palier impossible : " + bindingIssue);
            return true;
        }

        expectedQteCount = CountQteEvents(boundQteClip);
        if (expectedQteCount == 0)
        {
            AbortActiveSequence("le clip QTE lie ne contient aucun Animation Event QTE(input)");
            return true;
        }

        state = SequenceState.PlayingSequence;
        openedQteCount = 0;
        completedQteCount = 0;
        InputModeCoordinator.Enter(this, InputMode.ThresholdSequence);
        combatInput?.SetCinematicInputSuspended(true);
        PreparePlayerThresholdVisualState();
        playerAnimator.CrossFade(qteStateHash, activeSequence.playerQteEntryBlendSeconds, 0, 0f);
        qteEventWatchdogRoutine = StartCoroutine(WatchForQteEvent(token, 1));
        Trace("Sequence palier lancee | enemy='" + activeEnemy.name + "' | stage=" + activeStage.healthPercent + "% | state='" + qteStateName + "' | qtes=" + expectedQteCount + ".");
        return true;
    }

    private IEnumerator ExpireQte(int token, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (token != sessionToken || !qteOpen || state != SequenceState.PlayingSequence)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            qtePanel?.SetProgress(elapsed / duration);
            yield return null;
        }

        if (token == sessionToken && qteOpen && state == SequenceState.PlayingSequence)
        {
            FailQte("expiration");
        }
    }

    private IEnumerator WatchForQteEvent(int token, int expectedEventIndex)
    {
        yield return new WaitForSecondsRealtime(qteEventTimeoutSeconds);
        if (token == sessionToken && state == SequenceState.PlayingSequence && !qteOpen &&
            openedQteCount < expectedEventIndex)
        {
            AbortActiveSequence("Animation Event QTE(input) " + expectedEventIndex + "/" + expectedQteCount +
                                " absent apres " + qteEventTimeoutSeconds.ToString("F2") + "s");
        }
    }

    /// <summary>Called by the authored player Animation Event QTE(string input).</summary>
    public void OpenQte(string input)
    {
        if (state != SequenceState.PlayingSequence)
        {
            return;
        }

        if (!TryParseQteInput(input, out CombatThresholdQteInput parsed))
        {
            FailQte("input QTE auteur invalide '" + input + "'");
            return;
        }

        if (qteOpen)
        {
            FailQte("QTE auteur chevauchant");
            return;
        }

        if (openedQteCount >= expectedQteCount)
        {
            FailQte("Animation Event QTE supplementaire non declare");
            return;
        }

        ResolveQteInput();
        if (qteMap == null)
        {
            FailQte("ActionMap CombatQTE indisponible");
            return;
        }

        expectedQteInput = parsed;
        openedQteCount++;
        qteOpen = true;
        if (qteEventWatchdogRoutine != null) StopCoroutine(qteEventWatchdogRoutine);
        qteEventWatchdogRoutine = null;
        InputModeCoordinator.Enter(this, InputMode.CombatQTE);
        SetQteInputEnabled(true);
        InputAction expected = GetQteAction(parsed);
        waitingForExpectedRelease = expected != null && expected.ReadValue<float>() > 0.5f;
        ResolveReferences();
        if (qtePanel == null)
        {
            Debug.LogWarning("[CombatThreshold] QTE ouvert sans QTEPanelController actif : le QTE reste jouable, mais aucun overlay ne peut etre affiche.", this);
        }
        AcquireQteSlowMotion();
        qtePanel?.Show(parsed);
        if (qteRoutine != null) StopCoroutine(qteRoutine);
        float duration = Mathf.Max(0.01f, qteDurationSeconds);
        qteRoutine = StartCoroutine(ExpireQte(sessionToken, duration));
        Trace("QTE ouvert | step=" + openedQteCount + "/" + expectedQteCount + " | input=" + parsed +
              " | duration=" + duration.ToString("F2") + "s | releaseRequired=" + waitingForExpectedRelease + ".");
    }

    private void FailQte(string reason)
    {
        if (state != SequenceState.PlayingSequence || activeEnemy == null || activeSequence == null)
        {
            return;
        }

        qteOpen = false;
        if (qteRoutine != null) StopCoroutine(qteRoutine);
        qteRoutine = null;
        if (qteEventWatchdogRoutine != null) StopCoroutine(qteEventWatchdogRoutine);
        qteEventWatchdogRoutine = null;
        SetQteInputEnabled(false);
        ReleaseQteSlowMotion();
        qtePanel?.ResolveFailure();
        state = SequenceState.FailureRetaliation;
        MarkActiveStageResolved();
        ClearPlayerThresholdVisualState();
        RestorePlayerThresholdAnimationBindings();
        TransitionPlayerToThresholdIdle();
        RestoreCombatOwnership();

        if (activeSequence.failureResult == ThresholdSequenceFailureResult.ResumeCombat)
        {
            Trace("QTE echoue | reason=" + reason + " | reprise immediate.");
            FinishFailureRetaliation();
            return;
        }

        SkillSO skill = activeSequence.failureRetaliationSkill;
        bool started = skill != null && activeEnemy.TryStartThresholdFailureRetaliation(skill);
        if (!started)
        {
            Debug.LogWarning("[CombatThreshold] Riposte de palier indisponible pour '" + activeEnemy.name +
                             "' (skill='" + (skill != null ? skill.name : "None") + "). Reprise du combat.", this);
            FinishFailureRetaliation();
            return;
        }

        Trace("QTE echoue | reason=" + reason + " | riposte='" + skill.SkillName + "'.");
        if (successRoutine != null) StopCoroutine(successRoutine);
        successRoutine = StartCoroutine(WatchFailureRetaliation(sessionToken, skill.AnimationClip));
    }

    private IEnumerator WatchFailureRetaliation(int token, AnimationClip clip)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(1f, clip != null ? clip.length : 0f) + failureRetaliationGraceSeconds);
        if (token == sessionToken && state == SequenceState.FailureRetaliation && activeEnemy != null)
        {
            Trace("Fin de riposte absente : recuperation forcee.");
            RequestFailureRetaliationCompletion(activeEnemy);
        }
    }

    private void RequestFailureRetaliationCompletion(RealTimeCombatEnemy enemy)
    {
        if (failureCompletionRequested || enemy == null)
        {
            return;
        }

        failureCompletionRequested = true;
        enemy.CompleteEnemyAttackWhenGrounded(() =>
        {
            combatManager?.CompleteEnemyAttack(enemy);
            enemy.ReturnToIdleAnimation();
            enemy.GetComponent<RealTimeCombatEnemyBehaviour>()?.NotifyAttackCompleted();
            FinishFailureRetaliation();
        });
    }

    private void FinishFailureRetaliation()
    {
        ClearPlayerThresholdVisualState();
        combatManager?.ResumePlayerLocomotionAfterCinematic(false, false);
        LocalPlayerInput.RequestHeldLocomotionReconciliation("riposte de palier terminee");
        ClearActive();
    }

    private void BeginSuccessPresentation()
    {
        if (activeSequence == null || combatManager == null || activeEnemy == null)
        {
            AbortActiveSequence("sequence de reussite invalide");
            return;
        }

        Animator animator = combatManager.PlayerAnimator;
        string successStateName = activeSequence.SuccessPlayerStateName;
        int stateHash = Animator.StringToHash(successStateName);
        if (animator == null || !animator.HasState(0, stateHash))
        {
            // A missing authored state must never turn a successful QTE into an
            // instant kill. Keep the player in the current in-place pose for the
            // configured payoff duration, then resolve the configured result.
            Debug.LogError("[CombatThreshold] Etat de reussite Lucian introuvable '" +
                           successStateName + "'. La sequence conserve son delai de reussite, mais aucun clip " +
                           "de succes ne peut etre joue. Ajoutez un etat Player_Model.controller portant ce nom.", activeSequence);
            StartSuccessResolutionDelay();
            return;
        }

        state = SequenceState.SuccessPresentation;
        animator.CrossFade(stateHash, activeSequence.successEntryBlendSeconds, 0, 0f);
        StartSuccessResolutionDelay();
        Trace("QTE reussi | animation='" + successStateName + "' | resolution=" +
              activeSequence.successResolutionDelaySeconds.ToString("F2") + "s.");
    }

    private void StartSuccessResolutionDelay()
    {
        state = SequenceState.SuccessPresentation;
        if (successRoutine != null) StopCoroutine(successRoutine);
        successRoutine = StartCoroutine(CompleteSuccessAfterDelay(sessionToken, activeSequence.successResolutionDelaySeconds));
    }

    private IEnumerator CompleteSuccessAfterDelay(int token, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (token == sessionToken && state == SequenceState.SuccessPresentation)
        {
            ResolveSuccessResultAtDelay();

            float clipLength = activeSequence != null && activeSequence.successPlayerAnimationClip != null
                ? activeSequence.successPlayerAnimationClip.length
                : 0f;
            float remainingVisualSeconds = Mathf.Max(0f, clipLength - Mathf.Max(0f, delay));
            if (remainingVisualSeconds > 0f) yield return new WaitForSeconds(remainingVisualSeconds);

            if (token == sessionToken && state == SequenceState.SuccessPresentation)
            {
                yield return BlendOutSuccessPresentation(token);
            }

            if (token == sessionToken && state == SequenceState.SuccessPresentation)
            {
                FinishSuccessPresentation();
            }
        }
    }

    private IEnumerator BlendOutSuccessPresentation(int token)
    {
        Animator animator = boundPlayerAnimator != null
            ? boundPlayerAnimator
            : (combatManager != null ? combatManager.PlayerAnimator : null);
        int combatIdleHash = Animator.StringToHash("CombatIdle");
        float blend = activeSequence != null ? activeSequence.successExitBlendSeconds : 0.16f;
        if (animator == null || !animator.HasState(0, combatIdleHash))
        {
            yield break;
        }

        animator.CrossFade(combatIdleHash, blend, 0, 0f);
        Trace("Sortie animation de succes | destination=CombatIdle | blend=" + blend.ToString("F2") + "s.");
        if (blend > 0f) yield return new WaitForSeconds(blend);
    }

    private void ResolveSuccessResultAtDelay()
    {
        if (successResultResolved || activeSequence == null) return;

        successResultResolved = true;
        MarkActiveStageResolved();
        if (activeSequence.successResult != CombatHealthThresholdSuccessResult.KillEnemy)
        {
            return;
        }

        RealTimeCombatEnemy enemy = activeEnemy;
        thresholdKillApplied = enemy != null && combatManager != null &&
                              combatManager.CompleteThresholdKill(enemy, endCombatImmediately: false);
        if (!thresholdKillApplied)
        {
            Debug.LogWarning("[CombatThreshold] KillEnemy n'a pas pu mettre fin aux PV de '" +
                             (enemy != null ? enemy.name : "None") + "'. Le combat reprendra a la fin de l'animation.", this);
        }
        else
        {
            Trace("KillEnemy applique apres le delai de reussite. Animation de succes conservee jusqu'a sa fin.");
        }
    }

    private void FinishSuccessPresentation()
    {
        if ((state != SequenceState.SuccessPresentation && state != SequenceState.PlayingSequence) ||
            activeSequence == null)
        {
            return;
        }

        ResolveSuccessResultAtDelay();
        CombatHealthThresholdSuccessResult result = activeSequence.successResult;
        RealTimeCombatEnemy enemy = activeEnemy;
        bool killApplied = thresholdKillApplied;
        ClearPlayerThresholdVisualState();
        RestoreCombatOwnership();
        ClearActive();

        if (result == CombatHealthThresholdSuccessResult.KillEnemy && killApplied && enemy != null)
        {
            combatManager?.FinishThresholdKillPresentation(enemy);
            return;
        }

        combatManager?.ResumePlayerLocomotionAfterCinematic(false, false);
        LocalPlayerInput.RequestHeldLocomotionReconciliation("QTE palier reussi");
    }

    private void RestoreCombatOwnership()
    {
        RestoreEncounter();
        combatManager?.SetCinematicSequenceActive(false);
        if (playerLockHeld)
        {
            combatManager?.UnlockPlayerAfterCinematic();
            playerLockHeld = false;
        }
        InputModeCoordinator.Exit(this);
        if (combatManager != null && combatManager.IsCombatActive) combatInput?.SetCinematicInputSuspended(false);
        else combatInput?.SetInputActive(false);
    }

    private void SuspendEncounter()
    {
        suspendedEnemies.Clear();
        RealTimeCombatEnemyBehaviour[] behaviours = FindObjectsByType<RealTimeCombatEnemyBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue;
            behaviours[i].SetCinematicSuspended(true);
            suspendedEnemies.Add(behaviours[i]);
        }
    }

    private void RestoreEncounter()
    {
        for (int i = 0; i < suspendedEnemies.Count; i++)
        {
            if (suspendedEnemies[i] != null) suspendedEnemies[i].SetCinematicSuspended(false);
        }
        suspendedEnemies.Clear();
    }

    private CombatHealthThresholdStage GetNextValidStage(RealTimeCombatEnemy enemy, CharacterData data)
    {
        if (data.combatHealthThresholdStages == null || data.combatHealthThresholdStages.Count == 0) return null;
        HashSet<CombatHealthThresholdStage> resolved = GetResolvedStages(enemy);
        CombatHealthThresholdStage candidate = null;
        int previousPercent = 100;
        bool valid = true;
        for (int i = 0; i < data.combatHealthThresholdStages.Count; i++)
        {
            CombatHealthThresholdStage stage = data.combatHealthThresholdStages[i];
            if (stage == null || !stage.IsComplete || stage.healthPercent < 1 || stage.healthPercent > 99 ||
                stage.healthPercent >= previousPercent)
            {
                valid = false;
                continue;
            }
            previousPercent = stage.healthPercent;
            if (!resolved.Contains(stage) && candidate == null) candidate = stage;
        }

        if (!valid && invalidDataReported.Add(data))
        {
            Debug.LogWarning("[CombatThreshold] Certains paliers de '" + data.name +
                             "' sont incomplets ou mal ordonnes : ils sont ignores.", data);
        }
        return candidate;
    }

    private HashSet<CombatHealthThresholdStage> GetResolvedStages(RealTimeCombatEnemy enemy)
    {
        if (!resolvedStages.TryGetValue(enemy, out HashSet<CombatHealthThresholdStage> resolved))
        {
            resolved = new HashSet<CombatHealthThresholdStage>();
            resolvedStages.Add(enemy, resolved);
        }
        return resolved;
    }

    private void MarkActiveStageResolved()
    {
        if (activeEnemy != null && activeStage != null) GetResolvedStages(activeEnemy).Add(activeStage);
    }

    private static CharacterData ResolveCharacterData(RealTimeCombatEnemy enemy)
    {
        CharacterInfo info = enemy != null
            ? enemy.GetComponent<CharacterInfo>() ?? enemy.GetComponentInChildren<CharacterInfo>(true)
            : null;
        return info != null ? info.CharacterData : null;
    }

    private void ClearActive()
    {
        RestorePlayerThresholdAnimationBindings();
        pendingRoutine = null;
        qteRoutine = null;
        successRoutine = null;
        qteEventWatchdogRoutine = null;
        qteOpen = false;
        expectedQteCount = 0;
        openedQteCount = 0;
        completedQteCount = 0;
        waitingForExpectedRelease = false;
        failureCompletionRequested = false;
        successResultResolved = false;
        thresholdKillApplied = false;
        activeEnemy = null;
        activeStage = null;
        activeSequence = null;
        state = SequenceState.Idle;
    }

    private void ResolveQteInput()
    {
        InputActionMap sharedMap = LocalPlayerInput.FindSharedActionMap("CombatQTE");
        if (ReferenceEquals(qteMap, sharedMap)) return;

        UnbindQteInput();
        qteMap = sharedMap;
        qteYAction = qteMap != null ? qteMap.FindAction("Y", false) : null;
        qteBAction = qteMap != null ? qteMap.FindAction("B", false) : null;
        qteAAction = qteMap != null ? qteMap.FindAction("A", false) : null;
        qteXAction = qteMap != null ? qteMap.FindAction("X", false) : null;
        BindQteAction(qteYAction, OnQteY, OnQteYReleased);
        BindQteAction(qteBAction, OnQteB, OnQteBReleased);
        BindQteAction(qteAAction, OnQteA, OnQteAReleased);
        BindQteAction(qteXAction, OnQteX, OnQteXReleased);
    }

    private static void BindQteAction(InputAction action, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
    {
        if (action == null) return;
        action.performed += performed;
        action.canceled += canceled;
    }

    private void UnbindQteInput()
    {
        // The runtime PlayerInputs instance owns this map and disposes it.
        // This controller never disposes or recreates shared input actions.
        UnbindQteAction(qteYAction, OnQteY, OnQteYReleased);
        UnbindQteAction(qteBAction, OnQteB, OnQteBReleased);
        UnbindQteAction(qteAAction, OnQteA, OnQteAReleased);
        UnbindQteAction(qteXAction, OnQteX, OnQteXReleased);
        if (qteMap != null) qteMap.Disable();
        qteMap = null;
        qteYAction = qteBAction = qteAAction = qteXAction = null;
    }

    private static void UnbindQteAction(InputAction action, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
    {
        if (action == null) return;
        action.performed -= performed;
        action.canceled -= canceled;
    }

    private void SetQteInputEnabled(bool enabled)
    {
        if (qteMap == null) return;
        if (!enabled) qteMap.Disable();
        // Enable is intentionally left to InputModeCoordinator: it is the
        // single owner of map exclusivity during a live QTE.
    }

    private bool TryResolveStagePose(
        out Vector3 playerPosition,
        out Quaternion playerRotation,
        out Quaternion enemyRotation,
        out string issue)
    {
        playerPosition = Vector3.zero;
        playerRotation = Quaternion.identity;
        enemyRotation = Quaternion.identity;
        issue = "references acteur manquantes";
        Transform playerRoot = combatManager != null ? combatManager.PlayerRoot : null;
        if (playerRoot == null || activeEnemy == null) return false;

        Vector3 enemyPosition = activeEnemy.transform.position;
        Vector3 forward = Vector3.ProjectOnPlane(activeEnemy.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(playerRoot.position - enemyPosition, Vector3.up);
        }
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        if (!TryResolveStageCapsule(playerRoot, out StageCapsule capsule))
        {
            issue = "volume de collision joueur introuvable";
            return false;
        }

        for (int i = 0; i < StageAngles.Length; i++)
        {
            Vector3 direction = Quaternion.AngleAxis(StageAngles[i], Vector3.up) * forward;
            Vector3 horizontalCandidate = enemyPosition + direction * stageDistance;
            if (!TryFindGroundedCapsulePose(playerRoot, capsule, horizontalCandidate, out Vector3 candidate, out string candidateIssue))
            {
                issue = candidateIssue;
                continue;
            }

            Vector3 playerToEnemy = Vector3.ProjectOnPlane(enemyPosition - candidate, Vector3.up);
            if (playerToEnemy.sqrMagnitude < 0.0001f) continue;
            playerRotation = Quaternion.LookRotation(playerToEnemy.normalized, Vector3.up);
            enemyRotation = Quaternion.LookRotation(-playerToEnemy.normalized, Vector3.up);
            playerPosition = candidate;
            issue = null;
            return true;
        }

        issue = string.IsNullOrEmpty(issue) ? "aucun candidat libre a 2 m" : issue;
        return false;
    }

    private bool TryResolveStageCapsule(Transform playerRoot, out StageCapsule shape)
    {
        CapsuleCollider[] capsuleColliders = playerRoot.GetComponentsInChildren<CapsuleCollider>(true);
        CapsuleCollider selectedCapsule = null;
        for (int i = 0; i < capsuleColliders.Length; i++)
        {
            CapsuleCollider candidate = capsuleColliders[i];
            if (candidate == null || candidate.isTrigger || !candidate.enabled) continue;
            if (selectedCapsule == null ||
                (candidate.transform.position - playerRoot.position).sqrMagnitude <
                (selectedCapsule.transform.position - playerRoot.position).sqrMagnitude)
            {
                selectedCapsule = candidate;
            }
        }

        if (selectedCapsule != null)
        {
            fallbackStageCapsuleLogged = false;
            shape = CreateStageCapsule(playerRoot, selectedCapsule.transform, selectedCapsule.center,
                selectedCapsule.radius, selectedCapsule.height, "CapsuleCollider");
            return true;
        }

        CharacterController[] characterControllers = playerRoot.GetComponentsInChildren<CharacterController>(true);
        CharacterController selectedController = null;
        for (int i = 0; i < characterControllers.Length; i++)
        {
            CharacterController candidate = characterControllers[i];
            if (candidate == null || !candidate.enabled) continue;
            if (selectedController == null ||
                (candidate.transform.position - playerRoot.position).sqrMagnitude <
                (selectedController.transform.position - playerRoot.position).sqrMagnitude)
            {
                selectedController = candidate;
            }
        }

        if (selectedController != null)
        {
            fallbackStageCapsuleLogged = false;
            shape = CreateStageCapsule(playerRoot, selectedController.transform, selectedController.center,
                selectedController.radius, selectedController.height, "CharacterController");
            return true;
        }

        // UCC characters occasionally defer their physical collider while a
        // character is being initialized. Keep the QTE recoverable: the final
        // overlap test still rejects an unsafe staging point.
        shape = new StageCapsule
        {
            localCenter = Vector3.up * 0.9f,
            radius = 0.35f,
            height = 1.8f,
            source = "fallback UCC"
        };
        if (!fallbackStageCapsuleLogged)
        {
            fallbackStageCapsuleLogged = true;
            Trace("Capsule UCC absente : volume de secours utilise pour la pose du palier.");
        }
        return true;
    }

    private static StageCapsule CreateStageCapsule(
        Transform playerRoot, Transform colliderTransform, Vector3 colliderCenter,
        float colliderRadius, float colliderHeight, string source)
    {
        Vector3 worldCenter = colliderTransform.TransformPoint(colliderCenter);
        float horizontalScale = Mathf.Max(Mathf.Abs(colliderTransform.lossyScale.x), Mathf.Abs(colliderTransform.lossyScale.z));
        return new StageCapsule
        {
            localCenter = Quaternion.Inverse(playerRoot.rotation) * (worldCenter - playerRoot.position),
            radius = Mathf.Max(0.01f, colliderRadius * horizontalScale),
            height = Mathf.Max(0.02f, colliderHeight * Mathf.Abs(colliderTransform.lossyScale.y)),
            source = source
        };
    }

    private bool TryFindGroundedCapsulePose(
        Transform playerRoot,
        StageCapsule capsule,
        Vector3 horizontalCandidate,
        out Vector3 rootPosition,
        out string issue)
    {
        rootPosition = default;
        issue = "sol introuvable";
        float rayStart = Mathf.Max(activeEnemy.transform.position.y, playerRoot.position.y) + 4f;
        if (!Physics.Raycast(new Vector3(horizontalCandidate.x, rayStart, horizontalCandidate.z), Vector3.down,
                out RaycastHit groundHit, 12f, stageGroundMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        float bottomFromRoot = capsule.localCenter.y - (capsule.height * 0.5f);
        rootPosition = groundHit.point - Vector3.up * bottomFromRoot + Vector3.up * stageClearance;
        float radius = Mathf.Max(0.01f, capsule.radius - stageClearance);
        float height = Mathf.Max(radius * 2f, capsule.height - stageClearance * 2f);
        Vector3 center = rootPosition + playerRoot.rotation * capsule.localCenter;
        Vector3 halfAxis = Vector3.up * Mathf.Max(0f, height * 0.5f - radius);
        Collider[] blockers = Physics.OverlapCapsule(center - halfAxis, center + halfAxis, radius, stageBlockingMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < blockers.Length; i++)
        {
            Collider blocker = blockers[i];
            if (blocker == null || blocker.isTrigger || IsStageActorCollider(blocker, playerRoot)) continue;
            issue = "obstacle '" + blocker.name + "'";
            return false;
        }

        return true;
    }

    private bool IsStageActorCollider(Collider collider, Transform playerRoot)
    {
        Transform candidate = collider.transform;
        return candidate == playerRoot || candidate.IsChildOf(playerRoot) ||
               candidate == activeEnemy.transform || candidate.IsChildOf(activeEnemy.transform);
    }

    private bool ApplyStagePose(Vector3 playerPosition, Quaternion playerRotation, Quaternion enemyRotation, out string error)
    {
        error = null;
        Transform playerRoot = combatManager != null ? combatManager.PlayerRoot : null;
        if (playerRoot == null || activeEnemy == null)
        {
            error = "acteurs absents";
            return false;
        }

        CombatActorAnimationRoot playerContract = playerRoot.GetComponent<CombatActorAnimationRoot>();
        bool playerPlaced = playerContract != null
            ? playerContract.SetActorPose(playerPosition, playerRotation)
            : playerRoot.TryGetComponent(out LitOpsiveLocomotionBridge bridge) && bridge.SetCinematicPositionAndRotation(playerPosition, playerRotation, true, false);
        if (!playerPlaced)
        {
            error = "placement UCC de Lucian refuse";
            return false;
        }

        CombatActorAnimationRoot enemyContract = activeEnemy.GetComponent<CombatActorAnimationRoot>();
        bool enemyPlaced = enemyContract != null
            ? enemyContract.SetActorPose(activeEnemy.transform.position, enemyRotation)
            : activeEnemy.TryGetComponent(out RealTimeCombatEnemyBehaviour behaviour) && behaviour.PlaceForCinematic(activeEnemy.transform.position, enemyRotation);
        if (!enemyPlaced)
        {
            error = "rotation cinematographique de l'ennemi refusee";
            return false;
        }

        Physics.SyncTransforms();
        Trace("Pose palier appliquee | player=" + playerPosition + " | enemy=" + activeEnemy.transform.position + " | distance=" +
              Vector3.Distance(new Vector3(playerPosition.x, 0f, playerPosition.z), new Vector3(activeEnemy.transform.position.x, 0f, activeEnemy.transform.position.z)).ToString("F3") + ".");
        return true;
    }

    private void OnQteButton(CombatThresholdQteInput input, InputAction.CallbackContext context)
    {
        if (context.phase != InputActionPhase.Performed || !qteOpen || state != SequenceState.PlayingSequence) return;
        if (waitingForExpectedRelease) return;
        if (input != expectedQteInput)
        {
            FailQte("mauvais input " + input + ", attendu " + expectedQteInput);
            return;
        }

        qteOpen = false;
        if (qteRoutine != null) StopCoroutine(qteRoutine);
        qteRoutine = null;
        if (qteEventWatchdogRoutine != null) StopCoroutine(qteEventWatchdogRoutine);
        qteEventWatchdogRoutine = null;
        SetQteInputEnabled(false);
        ReleaseQteSlowMotion();
        qtePanel?.ResolveSuccess();
        InputModeCoordinator.Enter(this, InputMode.ThresholdSequence);
        completedQteCount++;
        if (completedQteCount < expectedQteCount)
        {
            qteEventWatchdogRoutine = StartCoroutine(WatchForQteEvent(sessionToken, openedQteCount + 1));
            Trace("QTE reussi | step=" + completedQteCount + "/" + expectedQteCount +
                  " | animation QTE continue vers l'etape suivante.");
            return;
        }

        BeginSuccessPresentation();
    }

    private void OnQteButtonReleased(CombatThresholdQteInput input, InputAction.CallbackContext context)
    {
        if (qteOpen && input == expectedQteInput) waitingForExpectedRelease = false;
    }

    private void OnQteY(InputAction.CallbackContext context) => OnQteButton(CombatThresholdQteInput.Y, context);
    private void OnQteB(InputAction.CallbackContext context) => OnQteButton(CombatThresholdQteInput.B, context);
    private void OnQteA(InputAction.CallbackContext context) => OnQteButton(CombatThresholdQteInput.A, context);
    private void OnQteX(InputAction.CallbackContext context) => OnQteButton(CombatThresholdQteInput.X, context);
    private void OnQteYReleased(InputAction.CallbackContext context) => OnQteButtonReleased(CombatThresholdQteInput.Y, context);
    private void OnQteBReleased(InputAction.CallbackContext context) => OnQteButtonReleased(CombatThresholdQteInput.B, context);
    private void OnQteAReleased(InputAction.CallbackContext context) => OnQteButtonReleased(CombatThresholdQteInput.A, context);
    private void OnQteXReleased(InputAction.CallbackContext context) => OnQteButtonReleased(CombatThresholdQteInput.X, context);

    private InputAction GetQteAction(CombatThresholdQteInput input)
    {
        switch (input)
        {
            case CombatThresholdQteInput.Y: return qteYAction;
            case CombatThresholdQteInput.B: return qteBAction;
            case CombatThresholdQteInput.A: return qteAAction;
            default: return qteXAction;
        }
    }

    private static bool TryParseQteInput(string value, out CombatThresholdQteInput input)
    {
        switch ((value ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "Y": input = CombatThresholdQteInput.Y; return true;
            case "B": input = CombatThresholdQteInput.B; return true;
            case "A": input = CombatThresholdQteInput.A; return true;
            case "X": input = CombatThresholdQteInput.X; return true;
            default: input = default; return false;
        }
    }

    private void ResolveReferences()
    {
        if (combatManager == null) combatManager = GetComponent<RealTimeCombatManager>();
        if (combatInput == null) combatInput = GetComponent<RealTimeCombatInput>();
        if (qtePanel == null) qtePanel = QTEPanelController.Instance;
        if (qtePanel == null) qtePanel = UnityEngine.Object.FindFirstObjectByType<QTEPanelController>(FindObjectsInactive.Include);
        if (qtePanel == null)
        {
            QTEPanelController[] allPanels = Resources.FindObjectsOfTypeAll<QTEPanelController>();
            for (int i = 0; i < allPanels.Length; i++)
            {
                if (allPanels[i] != null && allPanels[i].gameObject.scene.IsValid())
                {
                    qtePanel = allPanels[i];
                    break;
                }
            }
        }
    }

    private void AcquireQteSlowMotion()
    {
        ResolveReferences();
        if (qteLocalTimeField == null)
        {
            qteLocalTimeField = GetComponent<CombatLocalTimeField>();
            if (qteLocalTimeField == null) qteLocalTimeField = gameObject.AddComponent<CombatLocalTimeField>();
        }

        Transform player = combatManager != null ? combatManager.PlayerRoot : null;
        qteLocalTimeField.Begin(player, null, 0.4f);
        Trace("Ralentissement QTE local acquis | centre='" + (player != null ? player.name : "None") + "' | radius=10.0 | scale=0.40 | actors=" + qteLocalTimeField.AffectedActorCount + ".");
    }

    private void ReleaseQteSlowMotion()
    {
        if (qteLocalTimeField == null || !qteLocalTimeField.IsActive) return;

        qteLocalTimeField.End();
        Trace("Ralentissement QTE local libere.");
    }

    private void PreparePlayerThresholdVisualState()
    {
        Transform playerRoot = combatManager != null ? combatManager.PlayerRoot : null;
        LitOpsiveLocomotionBridge bridge = playerRoot != null
            ? playerRoot.GetComponentInChildren<LitOpsiveLocomotionBridge>(true)
            : null;
        bridge?.SetPlayerActionRootMotionMode(PlayerActionRootMotionMode.InPlace, false, false);
    }

    private void ClearPlayerThresholdVisualState()
    {
        Transform playerRoot = combatManager != null ? combatManager.PlayerRoot : null;
        LitOpsiveLocomotionBridge bridge = playerRoot != null
            ? playerRoot.GetComponentInChildren<LitOpsiveLocomotionBridge>(true)
            : null;
        bridge?.ClearPlayerActionRootMotionMode();
    }

    private bool BindSequenceAnimationClips(Animator animator, out string issue)
    {
        issue = null;
        if (animator == null || animator.runtimeAnimatorController == null || activeSequence == null)
        {
            issue = "Animator ou ThresholdSequence absent";
            return false;
        }

        RuntimeAnimatorController sourceController = animator.runtimeAnimatorController;
        AnimationClip qtePlaceholder = FindControllerClip(sourceController, ThresholdSequence.DefaultQteAnimatorState);
        AnimationClip successPlaceholder = FindControllerClip(sourceController, "Threshold_Succes_Placeholder");
        if (qtePlaceholder == null || successPlaceholder == null)
        {
            issue = "clips placeholder manquants (QTE='" + ThresholdSequence.DefaultQteAnimatorState +
                    "', success='Threshold_Succes_Placeholder')";
            return false;
        }

        AnimatorOverrideController overrideController = new AnimatorOverrideController(sourceController);
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
            new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);

        if (!TrySetClipOverride(overrides, qtePlaceholder, activeSequence.playerQteAnimationClip ?? qtePlaceholder) ||
            !TrySetClipOverride(overrides, successPlaceholder, activeSequence.successPlayerAnimationClip))
        {
            issue = "placeholder absent de la table AnimatorOverrideController";
            Destroy(overrideController);
            return false;
        }

        overrideController.ApplyOverrides(overrides);
        boundPlayerAnimator = animator;
        playerRuntimeControllerBeforeSequence = sourceController;
        playerThresholdOverrideController = overrideController;
        boundQteClip = activeSequence.playerQteAnimationClip ?? qtePlaceholder;
        animator.runtimeAnimatorController = overrideController;
        Trace("Clips de palier lies | qte='" + (activeSequence.playerQteAnimationClip != null
                  ? activeSequence.playerQteAnimationClip.name : qtePlaceholder.name) + "' | success='" +
              activeSequence.successPlayerAnimationClip.name + "'.");
        return true;
    }

    private static int CountQteEvents(AnimationClip clip)
    {
        if (clip == null) return 0;

        AnimationEvent[] events = clip.events;
        int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            if (events[i].functionName == "QTE") count++;
        }

        return count;
    }

    private static AnimationClip FindControllerClip(RuntimeAnimatorController controller, string clipName)
    {
        if (controller == null || string.IsNullOrWhiteSpace(clipName)) return null;

        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == clipName) return clips[i];
        }

        return null;
    }

    private static bool TrySetClipOverride(List<KeyValuePair<AnimationClip, AnimationClip>> overrides,
                                           AnimationClip placeholder, AnimationClip replacement)
    {
        if (placeholder == null || replacement == null) return false;
        for (int i = 0; i < overrides.Count; i++)
        {
            if (overrides[i].Key != placeholder) continue;
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(placeholder, replacement);
            return true;
        }

        return false;
    }

    private void RestorePlayerThresholdAnimationBindings()
    {
        if (playerThresholdOverrideController == null) return;

        if (boundPlayerAnimator != null &&
            boundPlayerAnimator.runtimeAnimatorController == playerThresholdOverrideController)
        {
            boundPlayerAnimator.runtimeAnimatorController = playerRuntimeControllerBeforeSequence;
        }

        Destroy(playerThresholdOverrideController);
        playerThresholdOverrideController = null;
        playerRuntimeControllerBeforeSequence = null;
        boundPlayerAnimator = null;
        boundQteClip = null;
    }

    private void TransitionPlayerToThresholdIdle()
    {
        Animator animator = combatManager != null ? combatManager.PlayerAnimator : null;
        int combatIdleHash = Animator.StringToHash("CombatIdle");
        if (animator == null || !animator.HasState(0, combatIdleHash))
        {
            Trace("Retour Idle de palier ignore : etat CombatIdle introuvable.");
            return;
        }

        float blend = activeSequence != null ? activeSequence.failureIdleBlendSeconds : 0.10f;
        animator.CrossFade(combatIdleHash, blend, 0, 0f);
        Trace("QTE echoue | transition fluide vers CombatIdle | blend=" + blend.ToString("F2") + "s.");
    }

    private void Trace(string message)
    {
        if (logDiagnostics) Debug.Log("[CombatThreshold] " + message, this);
    }
}
