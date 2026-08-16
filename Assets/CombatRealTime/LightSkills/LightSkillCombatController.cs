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

    private float charge;
    private bool cinematicPlaying;
    private bool impactResolved;
    private bool playerLockHeld;
    private bool finishingCinematic;
    private SpiritBondController activeLightSkillBond;
    private bool usingPooledRig;
    private float chargeBeforeCinematic;

    public event System.Action StateChanged;

    public LightSkillSO LightSkill => lightSkill;
    public float Charge => charge;
    public float RequiredCharge => lightSkill != null ? lightSkill.RequiredCharge : 1f;
    public bool IsReady => lightSkill != null && charge >= RequiredCharge;
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
        StopCinematic(resolveImpact: false);
    }

    public bool TryUseLightSkill()
    {
        ResolveReferences();
        Trace("Tentative | charge=" + charge + "/" + RequiredCharge +
              " | combat=" + IsCombatActive + " | verrou=" + (combatManager != null && combatManager.LockedEnemy != null) +
              " | rig=" + (lightSkill != null && lightSkill.CombatCinematicRigPrefab != null ? lightSkill.CombatCinematicRigPrefab.name : "None") + ".");
        if (cinematicPlaying) return Reject("LightSkill deja en cours.");
        if (lightSkill == null) return Reject("Aucune LightSkill n'est assignee.");
        if (!IsReady) return Reject("Charge de lumiere insuffisante.");
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
        if (player == null || target == null || HorizontalDistance(player.position, target.position) > lightSkill.MaximumCinematicStartDistance)
        {
            return Reject("La cible est trop loin pour cette LightSkill.");
        }

        CombatCinematicContext context = new CombatCinematicContext(combatManager, lightSkill, ResolveLightSkillImpact);
        if (!CombatCinematicPlacementResolver.TryResolve(
                lightSkill.CombatCinematicRigPrefab,
                context,
                lightSkill.CinematicClearance,
                out CombatCinematicPlacement placement,
                out string placementError))
        {
            return Reject(placementError);
        }
        Trace("Placement calcule | rig=" + placement.RigPosition + " rot=" + placement.RigRotation.eulerAngles +
              " | player=" + placement.PlayerPosition + " rot=" + placement.PlayerRotation.eulerAngles +
              " | enemy=" + placement.EnemyPosition + " rot=" + placement.EnemyRotation.eulerAngles + ".");

        activeLightSkillBond = SpiritBondController.FindForCharacter(combatManager.PlayerRoot.gameObject);
        activeLightSkillBond?.BeginLightSkillFusion();
        cinematicPlaying = true;
        impactResolved = false;
        chargeBeforeCinematic = charge;
        charge = 0f;
        combatManager.CancelPlayerActionForCinematic();
        playerLockHeld = combatManager.TryLockPlayerForCinematic();
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
            return AbortStart(error, restoreCharge: true);
        }

        usingPooledRig = true;
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
            charge = 0f;
            StopCinematic(resolveImpact: false);
        }
        else
        {
            charge = 0f;
        }

        NotifyStateChanged();
    }

    private void OnPlayerSkillImpactApplied(SkillSO skill, int damage)
    {
        if (!IsCombatActive || cinematicPlaying || lightSkill == null || skill == null || damage <= 0)
        {
            return;
        }

        charge = Mathf.Min(RequiredCharge, charge + skill.LightChargeOnHit);
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
        if (playerLockHeld)
        {
            combatManager?.UnlockPlayerAfterCinematic();
            playerLockHeld = false;
        }

        activeLightSkillBond?.EndLightSkillFusion();
        activeLightSkillBond = null;
        if (impactResolved && combatManager != null && combatManager.IsCombatActive)
        {
            combatManager.ResolveDeferredCombatOutcome();
        }

        if (combatManager != null && combatManager.IsCombatActive)
        {
            combatInput?.SetInputActive(true);
        }

        finishingCinematic = false;
        usingPooledRig = false;
        chargeBeforeCinematic = 0f;
        NotifyStateChanged();
    }

    private void Bind()
    {
        if (combatManager != null)
        {
            combatManager.CombatStateChanged -= OnCombatStateChanged;
            combatManager.CombatStateChanged += OnCombatStateChanged;
            combatManager.PlayerSkillImpactApplied -= OnPlayerSkillImpactApplied;
            combatManager.PlayerSkillImpactApplied += OnPlayerSkillImpactApplied;
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
            combatManager.PlayerSkillImpactApplied -= OnPlayerSkillImpactApplied;
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

    private bool AbortStart(string reason, bool restoreCharge = false)
    {
        Trace("Echec lancement | raison='" + reason + "' | restoreCharge=" + restoreCharge + ".");
        cinematicPlaying = false;
        if (restoreCharge)
        {
            charge = chargeBeforeCinematic;
        }
        chargeBeforeCinematic = 0f;
        if (playerLockHeld)
        {
            combatManager?.UnlockPlayerAfterCinematic();
            playerLockHeld = false;
        }

        combatInput?.SetInputActive(true);
        activeLightSkillBond?.EndLightSkillFusion();
        activeLightSkillBond = null;
        usingPooledRig = false;
        return Reject(reason);
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
