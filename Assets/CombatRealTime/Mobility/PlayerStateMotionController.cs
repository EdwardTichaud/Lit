using UnityEngine;

/// <summary>Applies only migrated state trajectories, through the existing UCC motion slot.</summary>
[DisallowMultipleComponent]
public sealed class PlayerStateMotionController : MonoBehaviour
{
    [SerializeField] private PlayerStateMotionLibrary library;
    private LitOpsiveLocomotionBridge bridge;
    private CombatActorAnimationRoot actor;
    private Animator animator;
    private CombatTimeDomain timeDomain;
    private PlayerStateMotionLibrary.Profile active;
    private Quaternion originRotation;
    private float previousTime;
    private int observedState;
    private bool ownsMotion;
    private bool ownsLandingLock;
    private int policyState;
    private PlayerActionMovementPolicy policy;
    private float lastObservedNormalizedTime;

    public PlayerStateMotionLibrary Library { get => library; set => library = value; }
    public bool IsActive => ownsMotion;
    public bool IsLandingLocked => ownsLandingLock;

    public bool RefreshLandingLock()
    {
        bool landing = isActiveAndEnabled && bridge != null && bridge.IsDriving && bridge.Grounded &&
            !bridge.IsScriptedTraversalActive && !bridge.IsCinematicMotionSessionActive && animator != null &&
            (IsLandingState(animator.GetCurrentAnimatorStateInfo(0)) ||
             animator.IsInTransition(0) && IsLandingState(animator.GetNextAnimatorStateInfo(0)));
        if (!landing)
        {
            if (ownsLandingLock) Cancel();
            return false;
        }
        if (!ownsLandingLock)
        {
            Cancel();
            ownsMotion = bridge.BeginScriptedPlanarMotion();
            ownsLandingLock = ownsMotion;
        }
        return ownsLandingLock;
    }

    private static bool IsLandingState(AnimatorStateInfo state) =>
        state.IsName("Base Layer.Jump_End") || state.IsName("Base Layer.Landing") || state.IsName("Base Layer.Landing_Hard");
    public void SetActionPolicy(int stateHash, PlayerActionMovementPolicy movementPolicy)
    {
        Cancel();
        policyState = stateHash;
        policy = movementPolicy;
        observedState = 0;
    }

    private void Awake()
    {
        bridge = GetComponent<LitOpsiveLocomotionBridge>();
        actor = GetComponent<CombatActorAnimationRoot>();
        animator = actor != null ? actor.Animator : GetComponentInChildren<Animator>();
        timeDomain = GetComponent<CombatTimeDomain>();
    }

    public bool TryEvaluateMotion(float dt, out Vector3 displacement)
    {
        displacement = Vector3.zero;
        // Hold through the outgoing blend as long as the landing remains visible.
        // Contact is required: anticipating the animation must not shorten the jump arc.
        if (RefreshLandingLock()) return true;
        if (bridge == null || !bridge.IsDriving || animator == null || library == null ||
            actor != null && actor.IsCinematicMotionActive || bridge.IsScriptedTraversalActive)
        {
            Cancel();
            return false;
        }
        AnimatorStateInfo state = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        bool restarted = observedState == state.fullPathHash && state.normalizedTime + .05f < lastObservedNormalizedTime;
        lastObservedNormalizedTime = state.normalizedTime;
        if (observedState != state.fullPathHash || restarted)
        {
            Cancel();
            observedState = state.fullPathHash;
            var profile = library.Find(observedState);
            // An existing dodge, lunge, knockback or traversal always keeps its slot.
            if (profile != null && (policyState != observedState || policy == PlayerActionMovementPolicy.StateTrajectory) &&
                !bridge.IsExternalLockActive &&
                (bridge.Grounded || profile.allowAirborne) && bridge.BeginScriptedPlanarMotion())
            {
                active = profile;
                ownsMotion = true;
                originRotation = transform.rotation;
                previousTime = Mathf.Clamp01(state.normalizedTime);
                if (profile.initialUpwardSpeed > 0f)
                    bridge.ApplyStateMotionVerticalImpulse(profile.initialUpwardSpeed);
            }
        }
        if (!ownsMotion) return false;
        if (bridge.HasCompetingStateMotionLock || !bridge.Grounded && !active.allowAirborne)
        {
            Cancel();
            return false;
        }
        float localScale = timeDomain != null ? timeDomain.Scale : 1f;
        if (dt <= 0f || localScale <= 0f) return true;
        // Integrate at the physics cadence, including when Animator runs at a lower render rate.
        float next = Mathf.Clamp01(previousTime + dt * animator.speed * state.speed * state.speedMultiplier / active.duration);
        if (next <= previousTime) return true;
        // Animation time already includes local/global time and state speed. Do not scale twice.
        Vector3 delta = active.Position(next) - active.Position(previousTime);
        displacement = originRotation * delta;
        float turn = active.yaw.Evaluate(next);
        if (Mathf.Abs(turn) > 0.01f)
            bridge.SetActionFacingDirection(originRotation * Quaternion.Euler(0, turn, 0) * Vector3.forward);
        previousTime = next;
        if (next >= 1f) Cancel();
        return true;
    }

    public void Cancel()
    {
        if (ownsMotion && bridge != null && bridge.IsScriptedPlanarMotionActive)
        {
            bridge.EndScriptedPlanarMotion();
        }
        ownsMotion = false;
        ownsLandingLock = false;
        active = null;
    }

    private void OnDisable() { Cancel(); observedState = 0; }
}
