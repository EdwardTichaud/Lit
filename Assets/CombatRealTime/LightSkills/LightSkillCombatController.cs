using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class LightSkillCombatController : MonoBehaviour
{
    [SerializeField] private RealTimeCombatManager combatManager;
    [SerializeField] private RealTimeCombatInput combatInput;
    [SerializeField] private PlayableDirector director;
    [SerializeField] private LightSkillSO lightSkill;
    [SerializeField] private CombatLockOnCameraController lockCamera;

    private float charge;
    private bool cinematicPlaying;
    private bool impactResolved;
    private bool playerLockHeld;
    private bool finishingCinematic;

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
        if (cinematicPlaying || !IsReady || combatManager == null || !combatManager.IsCombatActive ||
            combatManager.LockedEnemy == null || lightSkill == null || director == null)
        {
            return false;
        }

        if (lightSkill.Timeline == null)
        {
            Debug.LogWarning("[LightSkill] Aucune Timeline n'est assignee a '" + lightSkill.DisplayName + "'.", lightSkill);
            return false;
        }

        if (combatManager.LockedEnemy.Health != null && combatManager.LockedEnemy.Health.IsDead)
        {
            return false;
        }

        cinematicPlaying = true;
        impactResolved = false;
        charge = 0f;
        combatManager.CancelPlayerActionForCinematic();
        playerLockHeld = combatManager.TryLockPlayerForCinematic();
        combatInput?.SetInputActive(false);

        director.playableAsset = lightSkill.Timeline;
        BindTimelineTargets();
        lockCamera?.SetCinematicOverride(true);
        director.time = 0d;
        director.Evaluate();
        director.Play();
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

    private void OnPlayerLightDamageApplied(int damage)
    {
        if (!IsCombatActive || cinematicPlaying || lightSkill == null || damage <= 0)
        {
            return;
        }

        charge = Mathf.Min(RequiredCharge, charge + damage * lightSkill.ChargePerLightDamage);
        NotifyStateChanged();
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector != director || !cinematicPlaying)
        {
            return;
        }

        StopCinematic(resolveImpact: lightSkill != null && lightSkill.ResolveDamageWhenTimelineStops);
    }

    private void StopCinematic(bool resolveImpact)
    {
        if (!cinematicPlaying || finishingCinematic)
        {
            return;
        }

        finishingCinematic = true;
        if (resolveImpact && !impactResolved)
        {
            impactResolved = true;
            combatManager?.ApplyLightSkillDamage(lightSkill, resolveCombatOutcome: false);
        }

        cinematicPlaying = false;
        if (director != null && director.state == PlayState.Playing)
        {
            director.Stop();
        }

        if (playerLockHeld)
        {
            combatManager?.UnlockPlayerAfterCinematic();
            playerLockHeld = false;
        }

        lockCamera?.SetCinematicOverride(false);
        if (impactResolved && combatManager != null && combatManager.IsCombatActive)
        {
            combatManager.ResolveDeferredCombatOutcome();
        }

        if (combatManager != null && combatManager.IsCombatActive)
        {
            combatInput?.SetInputActive(true);
        }

        finishingCinematic = false;
        NotifyStateChanged();
    }

    private void Bind()
    {
        if (combatManager != null)
        {
            combatManager.CombatStateChanged -= OnCombatStateChanged;
            combatManager.CombatStateChanged += OnCombatStateChanged;
            combatManager.PlayerLightDamageApplied -= OnPlayerLightDamageApplied;
            combatManager.PlayerLightDamageApplied += OnPlayerLightDamageApplied;
        }

        if (director != null)
        {
            director.stopped -= OnDirectorStopped;
            director.stopped += OnDirectorStopped;
        }
    }

    private void Unbind()
    {
        if (combatManager != null)
        {
            combatManager.CombatStateChanged -= OnCombatStateChanged;
            combatManager.PlayerLightDamageApplied -= OnPlayerLightDamageApplied;
        }

        if (director != null)
        {
            director.stopped -= OnDirectorStopped;
        }
    }

    private void ResolveReferences()
    {
        if (combatManager == null) combatManager = GetComponent<RealTimeCombatManager>();
        if (combatInput == null) combatInput = GetComponent<RealTimeCombatInput>();
        if (director == null) director = GetComponent<PlayableDirector>();
        if (lockCamera == null) lockCamera = GetComponent<CombatLockOnCameraController>();
    }

    private void BindTimelineTargets()
    {
        if (director == null || lightSkill == null || lightSkill.Timeline == null || combatManager == null)
        {
            return;
        }

        foreach (PlayableBinding output in lightSkill.Timeline.outputs)
        {
            if (output.sourceObject == null)
            {
                continue;
            }

            if (output.streamName == lightSkill.PlayerAnimatorTrackName && combatManager.PlayerAnimator != null)
            {
                director.SetGenericBinding(output.sourceObject, combatManager.PlayerAnimator);
            }
            else if (output.streamName == lightSkill.EnemyAnimatorTrackName && combatManager.LockedEnemy != null)
            {
                director.SetGenericBinding(output.sourceObject, combatManager.LockedEnemy.Animator);
            }
            else if (output.streamName == lightSkill.CameraTrackName && lockCamera != null && lockCamera.ControlledCamera != null)
            {
                director.SetGenericBinding(output.sourceObject, lockCamera.ControlledCamera);
            }
        }
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
