using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightSkillCombatController : MonoBehaviour
{
    [SerializeField] private RealTimeCombatManager combatManager;
    [SerializeField] private RealTimeCombatInput combatInput;
    [SerializeField] private CombatCinematicPlaybackService cinematicPlayback;
    [SerializeField] private LightSkillSO lightSkill;
    [SerializeField, Tooltip("Journalise chaque etape du lancement et de l'arret d'une LightSkill.")]
    private bool logLightSkillDiagnostics = true;
    [SerializeField, Min(0.25f), Tooltip("Delai de securite ajoute a la duree de la Timeline avant de restituer le controle si son rappel de fin n'arrive pas.")]
    private float cinematicCompletionGraceSeconds = 1.5f;

    private bool cinematicPlaying;
    private bool impactResolved;
    private bool playerLockHeld;
    private bool finishingCinematic;
    private SpiritBondController activeLightSkillBond;
    private bool usingPooledRig;
    private float claritySpentForCinematic;
    private Coroutine cinematicCompletionWatchdog;
    private Coroutine locomotionHandoffRoutine;
    private int cinematicSessionToken;

    public event System.Action StateChanged;

    public LightSkillSO LightSkill => lightSkill;
    public float Clarity => combatManager != null ? combatManager.Clarity : 0f;
    public float RequiredClarity => lightSkill != null ? lightSkill.RequiredClarity : 1f;
    public bool IsReady => lightSkill != null && combatManager != null && Clarity >= RequiredClarity;
    public bool IsCombatActive => combatManager != null && combatManager.IsCombatActive;
    public bool IsCinematicPlaying => cinematicPlaying;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Bind();
    }

    private void Start()
    {
        ResolveReferences();
        Bind();
        NotifyStateChanged();
    }

    private void OnDisable()
    {
        Unbind();
        if (locomotionHandoffRoutine != null)
        {
            StopCoroutine(locomotionHandoffRoutine);
            locomotionHandoffRoutine = null;
        }
        StopCinematic(resolveImpact: false);
        if (cinematicPlaying)
        {
            // A disabled Director may not emit its stopped callback. Do not
            // leave this controller holding a stale cinematic session.
            usingPooledRig = false;
            StopCinematic(resolveImpact: false);
        }
        RestorePlayerControl("LightSkill component disabled");
    }

    public bool TryUseLightSkill()
    {
        ResolveReferences();
        Trace("Tentative | clarte=" + Clarity + "/" + RequiredClarity +
              " | combat=" + IsCombatActive + " | verrou=" + (combatManager != null && combatManager.LockedEnemy != null) +
              " | rig=" + (lightSkill != null && lightSkill.CombatCinematicRigPrefab != null ? lightSkill.CombatCinematicRigPrefab.name : "None") + ".");
        if (cinematicPlaying) return Reject("LightSkill deja en cours.");
        if (lightSkill == null) return Reject("Aucune LightSkill n'est assignee.");
        if (!IsReady) return Reject("Clarte insuffisante.");
        if (combatManager == null || !combatManager.IsCombatActive) return Reject("Combat non actif.");
        if (combatManager.LockedEnemy == null) return Reject("Aucun ennemi verrouille.");
        if (lightSkill.CombatCinematicRigPrefab == null)
        {
            return Reject("Prefab cinematographique manquant sur la LightSkill.");
        }

        if (lightSkill.Timeline == null)
        {
            return Reject("Aucune Timeline n'est assignee a '" + lightSkill.DisplayName + "'.");
        }

        if (combatManager.LockedEnemy.Health != null && combatManager.LockedEnemy.Health.IsDead)
        {
            return Reject("La cible verrouillee est deja vaincue.");
        }

        Transform player = combatManager.PlayerRoot;
        Transform target = combatManager.LockedEnemy.LockPoint != null
            ? combatManager.LockedEnemy.LockPoint
            : combatManager.LockedEnemy.transform;
        if (player == null || target == null)
        {
            return Reject("La position de depart de cette LightSkill est introuvable.");
        }

        float startDistance = HorizontalDistance(player.position, target.position);
        if (!lightSkill.IsWithinCinematicStartRange(startDistance))
        {
            string reason = startDistance < lightSkill.MinimumCinematicStartDistance
                ? "La cible est trop proche pour cette LightSkill."
                : "La cible est trop loin pour cette LightSkill.";
            return Reject(reason);
        }

        CombatCinematicContext context = new CombatCinematicContext(combatManager, lightSkill, ResolveLightSkillImpact);
        if (!lightSkill.CombatCinematicRigPrefab.TryGetMidpointPlacement(
                context,
                out CombatCinematicPlacement placement,
                out string placementError))
        {
            return Reject(placementError);
        }
        Trace("Placement calcule | rig=" + placement.RigPosition + " rot=" + placement.RigRotation.eulerAngles +
              " | player=" + placement.PlayerPosition + " rot=" + placement.PlayerRotation.eulerAngles +
              " | enemy=" + placement.EnemyPosition + " rot=" + placement.EnemyRotation.eulerAngles + ".");

        if (!combatManager.TrySpendClarity(RequiredClarity))
        {
            return Reject("Clarte insuffisante.");
        }

        activeLightSkillBond = SpiritBondController.FindForCharacter(combatManager.PlayerRoot.gameObject);
        activeLightSkillBond?.BeginLightSkillFusion();
        cinematicPlaying = true;
        impactResolved = false;
        claritySpentForCinematic = RequiredClarity;
        combatManager.CancelPlayerActionForCinematic();
        playerLockHeld = combatManager.TryLockPlayerForCinematic();
        InputModeCoordinator.Enter(this, InputMode.Cinematic);
        combatInput?.SetInputActive(false);
        Trace("Verrous appliques | playerLock=" + playerLockHeld + " | inputCombat=false.");
        if (cinematicPlayback == null) cinematicPlayback = GetComponent<CombatCinematicPlaybackService>();
        string error = "CombatCinematicPlaybackService manquant.";
        if (cinematicPlayback == null || !cinematicPlayback.TryPlay(
                lightSkill.CombatCinematicRigPrefab,
                context,
                null,
                lightSkill.PlayerAnimatorTrackName,
                lightSkill.EnemyAnimatorTrackName,
                placement,
                OnRuntimeRigCompleted,
                out error))
        {
            Debug.LogWarning("[LightSkill] Impossible de lancer le rig : " + error, this);
            return AbortStart(error, restoreClarity: true);
        }

        usingPooledRig = true;
        StartCinematicCompletionWatchdog();
        Trace("Rig demarre | runtime=" + (cinematicPlayback.ActiveRig != null ? cinematicPlayback.ActiveRig.name : "None") + ".");
        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Timeline Signal entry point. Place it on the exact fatality impact frame.
    /// </summary>
    public void ResolveLightSkillImpact()
    {
        if (!cinematicPlaying || impactResolved || combatManager == null || lightSkill == null)
        {
            return;
        }

        impactResolved = true;
        combatManager.ApplyLightSkillDamage(lightSkill, resolveCombatOutcome: false);
    }

    private void OnCombatStateChanged(bool active)
    {
        if (!active)
        {
            StopCinematic(resolveImpact: false);
        }

        NotifyStateChanged();
    }

    private void OnClarityChanged(float clarity, CombatClarityRank rank)
    {
        NotifyStateChanged();
    }

    private void StopCinematic(bool resolveImpact)
    {
        if (!cinematicPlaying || finishingCinematic)
        {
            return;
        }

        finishingCinematic = true;
        Trace("Arret cinematic | resolveImpact=" + resolveImpact + " | impactResolved=" + impactResolved +
              " | pooledRig=" + usingPooledRig + ".");
        if (usingPooledRig && cinematicPlayback != null && cinematicPlayback.IsPlaying)
        {
            finishingCinematic = false;
            cinematicPlayback.StopActive();
            return;
        }
        if (resolveImpact && !impactResolved)
        {
            impactResolved = true;
            combatManager?.ApplyLightSkillDamage(lightSkill, resolveCombatOutcome: false);
        }

        cinematicPlaying = false;
        StopCinematicCompletionWatchdog();

        activeLightSkillBond?.EndLightSkillFusion();
        activeLightSkillBond = null;
        if (impactResolved && combatManager != null && combatManager.IsCombatActive)
        {
            combatManager.ResolveDeferredCombatOutcome();
        }

        RestorePlayerControl("LightSkill cinematic completed");

        finishingCinematic = false;
        usingPooledRig = false;
        claritySpentForCinematic = 0f;
        NotifyStateChanged();
    }

    private void Bind()
    {
        if (combatManager != null)
        {
            combatManager.CombatStateChanged -= OnCombatStateChanged;
            combatManager.CombatStateChanged += OnCombatStateChanged;
            combatManager.ClarityChanged -= OnClarityChanged;
            combatManager.ClarityChanged += OnClarityChanged;
        }

    }

    private void OnRuntimeRigCompleted(CombatCinematicRig rig)
    {
        if (!cinematicPlaying) return;
        Trace("Rig termine | runtime=" + (rig != null ? rig.name : "None") + ".");
        StopCinematic(resolveImpact: lightSkill != null && lightSkill.ResolveDamageWhenTimelineStops);
    }

    private void Unbind()
    {
        if (combatManager != null)
        {
            combatManager.CombatStateChanged -= OnCombatStateChanged;
            combatManager.ClarityChanged -= OnClarityChanged;
        }

    }

    private void ResolveReferences()
    {
        if (combatManager == null) combatManager = GetComponent<RealTimeCombatManager>();
        if (combatInput == null) combatInput = GetComponent<RealTimeCombatInput>();
        if (cinematicPlayback == null) cinematicPlayback = GetComponent<CombatCinematicPlaybackService>();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private bool AbortStart(string reason, bool restoreClarity = false)
    {
        Trace("Echec lancement | raison='" + reason + "' | restoreClarity=" + restoreClarity + ".");
        cinematicPlaying = false;
        if (restoreClarity && claritySpentForCinematic > 0f)
        {
            combatManager?.RefundClarity(claritySpentForCinematic);
        }
        claritySpentForCinematic = 0f;
        if (playerLockHeld)
        {
            combatManager?.UnlockPlayerAfterCinematic();
            playerLockHeld = false;
        }

        StopCinematicCompletionWatchdog();
        RestorePlayerControl("LightSkill cinematic failed");
        activeLightSkillBond?.EndLightSkillFusion();
        activeLightSkillBond = null;
        usingPooledRig = false;
        return Reject(reason);
    }

    private void StartCinematicCompletionWatchdog()
    {
        StopCinematicCompletionWatchdog();
        cinematicSessionToken++;
        float timelineDuration = lightSkill != null && lightSkill.Timeline != null
            ? (float)lightSkill.Timeline.duration
            : 0f;
        float timeout = Mathf.Max(1f, timelineDuration) + cinematicCompletionGraceSeconds;
        cinematicCompletionWatchdog = StartCoroutine(WatchCinematicCompletion(cinematicSessionToken, timeout));
    }

    private void StopCinematicCompletionWatchdog()
    {
        cinematicSessionToken++;
        if (cinematicCompletionWatchdog != null)
        {
            StopCoroutine(cinematicCompletionWatchdog);
            cinematicCompletionWatchdog = null;
        }
    }

    private System.Collections.IEnumerator WatchCinematicCompletion(int sessionToken, float timeout)
    {
        yield return new WaitForSecondsRealtime(timeout);
        if (sessionToken != cinematicSessionToken || !cinematicPlaying)
        {
            yield break;
        }

        cinematicCompletionWatchdog = null;
        Trace("Fin de Timeline non recue apres " + timeout.ToString("F2") + "s : restitution de secours.");
        cinematicPlayback?.StopActive();
        yield return null;

        if (sessionToken == cinematicSessionToken && cinematicPlaying)
        {
            // StopActive normally invokes the completion callback. If a broken
            // Timeline did not, bypass the pooled-rig wait and close the lease.
            usingPooledRig = false;
            StopCinematic(resolveImpact: lightSkill != null && lightSkill.ResolveDamageWhenTimelineStops);
        }
    }

    private void RestorePlayerControl(string reason)
    {
        if (playerLockHeld)
        {
            combatManager?.UnlockPlayerAfterCinematic();
            playerLockHeld = false;
        }

        InputModeCoordinator.Exit(this);
        bool combatStillActive = combatManager != null && combatManager.IsCombatActive;
        combatInput?.SetInputActive(combatStillActive);
        LocalPlayerInput.RequestHeldLocomotionReconciliation(reason);
        ScheduleLocomotionHandoff(reason);
    }

    private void ScheduleLocomotionHandoff(string reason)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (locomotionHandoffRoutine != null)
        {
            StopCoroutine(locomotionHandoffRoutine);
        }

        locomotionHandoffRoutine = StartCoroutine(CompleteLocomotionHandoff(reason));
    }

    private System.Collections.IEnumerator CompleteLocomotionHandoff(string reason)
    {
        // Input maps and the UCC external lock both settle on the following frame.
        // Retry briefly so held controls are restored before choosing the state.
        for (int frame = 0; frame < 4; frame++)
        {
            yield return null;
            SquadManager.Instance?.ReapplyHeldLocomotionIntent();
            bool hasMovementInput = LocalInputRouter.MoveValue.sqrMagnitude > 0.0001f;
            // The first sampled frame can still be the zero imposed by UCC's
            // lock. Keep one extra frame for LocalPlayerInput to read controls.
            if (hasMovementInput || frame >= 1)
            {
                break;
            }
        }

        bool movementHeld = LocalInputRouter.MoveValue.sqrMagnitude > 0.0001f;
        bool sprintHeld = movementHeld && LocalInputRouter.RightShoulderPressed;
        combatManager?.ResumePlayerLocomotionAfterCinematic(movementHeld, sprintHeld);
        Trace("Handoff locomotion | reason=" + reason + " | move=" + movementHeld + " | sprint=" + sprintHeld + ".");
        locomotionHandoffRoutine = null;
    }

    private bool Reject(string reason)
    {
        Debug.LogWarning("[LightSkill] " + reason, this);
        Transform feedbackTarget = combatManager != null && combatManager.LockedEnemy != null
            ? combatManager.LockedEnemy.transform
            : combatManager != null ? combatManager.PlayerRoot : null;
        CombatDamageWorldFeedback.ShowMessage(
            feedbackTarget,
            reason,
            new Color(1f, 0.82f, 0.38f),
            2.25f);
        return false;
    }

    private void Trace(string message)
    {
        if (logLightSkillDiagnostics)
        {
            Debug.Log("[LightSkill Debug] " + message, this);
        }
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }
}
