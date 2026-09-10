using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;

/// <summary>
/// Presentation-only jump state machine. UCC owns collision and position; this
/// component only supplies the vertical impulse and the temporary gravity
/// profile. The jump clips must therefore remain strictly in-place.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerScriptedJumpController : MonoBehaviour
{
    private readonly PlayerModuleConfiguration<PlayerJumpSettings> moduleConfiguration = new PlayerModuleConfiguration<PlayerJumpSettings>();
    private PlayerJumpSettings ModuleSettings => moduleConfiguration.Resolve(this, data => data.jump);

    private const string GroundedEvent = "OnCharacterGrounded";
    private const string LandEvent = "OnCharacterLand";

    private enum Phase { Grounded, Takeoff, Ascending, Falling, Landing }

    [Header("References")]
    private UltimateCharacterLocomotion locomotion;
    private LitOpsiveLocomotionBridge locomotionBridge;
    [SerializeField] private Animator animator;

    private float jumpHeight { get => ModuleSettings.jumpHeight; set => ModuleSettings.jumpHeight = value; }
    public float jumpStartTakeoffNormalizedTime { get => ModuleSettings.jumpStartTakeoffNormalizedTime; set => ModuleSettings.jumpStartTakeoffNormalizedTime = value; }
    public float jumpStartPlanarSlowdown { get => ModuleSettings.jumpStartPlanarSlowdown; set => ModuleSettings.jumpStartPlanarSlowdown = value; }
    private float apexGravityEntryVelocity { get => ModuleSettings.apexGravityEntryVelocity; set => ModuleSettings.apexGravityEntryVelocity = value; }
    private float apexGravityMultiplier { get => ModuleSettings.apexGravityMultiplier; set => ModuleSettings.apexGravityMultiplier = value; }
    private float descentGravityMultiplier { get => ModuleSettings.descentGravityMultiplier; set => ModuleSettings.descentGravityMultiplier = value; }
    private float fallingAnimationEntryVelocity { get => ModuleSettings.fallingAnimationEntryVelocity; set => ModuleSettings.fallingAnimationEntryVelocity = value; }
    private float hardLandingHeight { get => ModuleSettings.hardLandingHeight; set => ModuleSettings.hardLandingHeight = value; }

    private MotionHandoffProfile landingHandoff { get => ModuleSettings.landingHandoff; set => ModuleSettings.landingHandoff = value; }

    private bool jumpActive;
    private bool leftGround;
    private bool landingRequested;
    private bool takeoffImpulseApplied;
    private bool gravityOverridden;
    private float baseGravity;
    private float landingContactStartedAt = -1f;
    private Phase phase;

    public bool IsActive => jumpActive;
    public float TargetJumpHeight => jumpHeight;

    public void SetTargetJumpHeight(float value)
    {
        jumpHeight = Mathf.Max(0.1f, value);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        EventHandler.RegisterEvent<bool>(gameObject, GroundedEvent, OnGroundedChanged);
        EventHandler.RegisterEvent<float>(gameObject, LandEvent, OnLanded);
        ResetPresentation();
    }

    private void OnDisable()
    {
        EventHandler.UnregisterEvent<bool>(gameObject, GroundedEvent, OnGroundedChanged);
        EventHandler.UnregisterEvent<float>(gameObject, LandEvent, OnLanded);
        RestoreGravity();
    }

    /// <summary>Starts the only supported player jump arc. Input is accepted
    /// for contract symmetry with the bridge; planar inertia is left to UCC.</summary>
    public bool TryStartJump(Vector2 worldInput, bool hasWorldInput)
    {
        ResolveReferences();
        if (!isActiveAndEnabled || jumpActive || locomotion == null || !locomotion.Grounded || locomotionBridge == null ||
            !locomotionBridge.IsDriving || locomotionBridge.IsInputSuppressedByUcc ||
            locomotionBridge.IsFlightActive || locomotionBridge.IsScriptedTraversalActive)
        {
            return false;
        }

        jumpActive = true;
        leftGround = false;
        landingRequested = false;
        takeoffImpulseApplied = false;
        landingContactStartedAt = -1f;
        baseGravity = locomotion.GravityAmount;
        gravityOverridden = false;
        SetBool("JumpPresentationActive", true);
        SetBool("IsAirborne", false);
        SetInteger("LandingType", 0);
        SetPhase(Phase.Takeoff);
        ResetTrigger("JumpStartTrigger");
        SetTrigger("JumpStartTrigger");

        return true;
    }

    private void Update()
    {
        if (!jumpActive || locomotion == null) return;

        if (!takeoffImpulseApplied)
        {
            if (HasReachedJumpStartTakeoffTime()) ApplyTakeoffImpulse();
            return;
        }

        float verticalSpeed = Vector3.Dot(locomotion.Velocity, transform.up);
        if (!landingRequested)
        {
            SetPhase(verticalSpeed < fallingAnimationEntryVelocity ? Phase.Falling : Phase.Ascending);
            if (verticalSpeed < -0.01f && locomotionBridge != null && locomotionBridge.ShouldBeginMotionHandoff(landingHandoff))
            {
                RequestLanding(0f);
            }
            return;
        }

        if (IsLandingComplete()) FinishLanding();
    }

    private void FixedUpdate()
    {
        if (!jumpActive || landingRequested || locomotion == null || locomotion.Grounded) return;
        if (!gravityOverridden)
        {
            baseGravity = locomotion.GravityAmount;
            gravityOverridden = true;
        }

        float verticalSpeed = Vector3.Dot(locomotion.Velocity, transform.up);
        float multiplier = verticalSpeed > apexGravityEntryVelocity ? 1f :
            verticalSpeed >= -apexGravityEntryVelocity ? apexGravityMultiplier : descentGravityMultiplier;
        locomotion.GravityAmount = baseGravity * multiplier;
    }

    private void OnGroundedChanged(bool grounded)
    {
        if (!jumpActive) return;
        if (!grounded)
        {
            leftGround = true;
            SetBool("IsAirborne", true);
            return;
        }

        RestoreGravity();
        if (landingRequested)
        {
            if (landingContactStartedAt < 0f) landingContactStartedAt = Time.unscaledTime;
        }
        else if (leftGround)
        {
            RequestLanding(0f);
        }
    }

    private void OnLanded(float fallHeight)
    {
        if (!jumpActive && !leftGround) return;
        RestoreGravity();
        if (!landingRequested) RequestLanding(fallHeight);
        else if (landingContactStartedAt < 0f) landingContactStartedAt = Time.unscaledTime;
    }

    private void RequestLanding(float fallHeight)
    {
        if (landingRequested) return;
        landingRequested = true;
        leftGround = false;
        landingContactStartedAt = locomotion != null && locomotion.Grounded ? Time.unscaledTime : -1f;
        SetBool("IsAirborne", false);
        SetInteger("LandingType", fallHeight >= hardLandingHeight ? 1 : 0);
        SetPhase(Phase.Landing);
        ResetTrigger("LandingTrigger");
        SetTrigger("LandingTrigger");
    }

    private bool IsLandingComplete()
    {
        if (locomotionBridge == null || !locomotionBridge.Grounded) return false;
        if (landingContactStartedAt < 0f) landingContactStartedAt = Time.unscaledTime;
        float elapsed = Time.unscaledTime - landingContactStartedAt;
        if (elapsed < landingHandoff.minimumContactSeconds) return false;
        return (HasLandingAnimationReachedExit() && locomotionBridge.IsMotionHandoffSettled(landingHandoff)) ||
               elapsed >= landingHandoff.maximumSettleSeconds;
    }

    private bool HasLandingAnimationReachedExit()
    {
        if (animator == null) return true;
        return IsLandingExitStateReady(animator.GetCurrentAnimatorStateInfo(0)) ||
               (animator.IsInTransition(0) && IsLandingExitStateReady(animator.GetNextAnimatorStateInfo(0)));
    }

    private bool IsLandingExitStateReady(AnimatorStateInfo state)
    {
        return (state.shortNameHash == Animator.StringToHash("Jump_End") ||
                state.shortNameHash == Animator.StringToHash("Landing_Hard")) &&
               state.normalizedTime >= landingHandoff.animationExitNormalizedTime;
    }

    private void FinishLanding()
    {
        RestoreGravity();
        jumpActive = false;
        landingRequested = false;
        takeoffImpulseApplied = false;
        landingContactStartedAt = -1f;
        SetBool("JumpPresentationActive", false);
        SetBool("IsAirborne", false);
        SetInteger("LandingType", 0);
        SetPhase(Phase.Grounded);
    }

    private void ResetPresentation()
    {
        RestoreGravity();
        jumpActive = false;
        leftGround = false;
        landingRequested = false;
        takeoffImpulseApplied = false;
        landingContactStartedAt = -1f;
        SetBool("JumpPresentationActive", false);
        SetBool("IsAirborne", false);
        SetInteger("LandingType", 0);
        SetPhase(Phase.Grounded);
    }

    private void RestoreGravity()
    {
        if (gravityOverridden && locomotion != null) locomotion.GravityAmount = baseGravity;
        gravityOverridden = false;
    }

    private float CalculateTakeoffSpeed(float gravity)
    {
        // UCC's GravityAmount is a multiplier, not an acceleration expressed
        // in world units. Convert it to the actual acceleration applied by
        // the motor before deriving a takeoff velocity from a target height.
        float acceleration = Mathf.Max(0.01f, Mathf.Abs(gravity) * Physics.gravity.magnitude);
        float apexSpeed = Mathf.Max(0f, apexGravityEntryVelocity);
        float apexMultiplier = Mathf.Max(0.01f, apexGravityMultiplier);
        float apexPhaseHeight = apexSpeed * apexSpeed / (2f * acceleration * apexMultiplier);
        if (jumpHeight <= apexPhaseHeight)
        {
            return Mathf.Sqrt(2f * acceleration * apexMultiplier * jumpHeight);
        }

        // The first part of the rise uses normal gravity; the final part
        // uses the lighter apex gravity, matching FixedUpdate exactly.
        float speedSquared = apexSpeed * apexSpeed +
                             2f * acceleration * (jumpHeight - apexPhaseHeight);
        return Mathf.Sqrt(Mathf.Max(0f, speedSquared));
    }

    private bool HasReachedJumpStartTakeoffTime()
    {
        if (animator == null) return true;
        int jumpStartHash = Animator.StringToHash("Jump_Start");
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
        return HasReachedTakeoffTime(current, jumpStartHash) || HasReachedTakeoffTime(next, jumpStartHash);
    }

    private bool HasReachedTakeoffTime(AnimatorStateInfo state, int jumpStartHash)
    {
        return state.shortNameHash == jumpStartHash &&
               state.normalizedTime >= Mathf.Clamp01(jumpStartTakeoffNormalizedTime);
    }

    private void ApplyTakeoffImpulse()
    {
        if (locomotion == null) return;
        float currentVerticalSpeed = Vector3.Dot(locomotion.Velocity, transform.up);
        float requiredImpulse = Mathf.Max(0f, CalculateTakeoffSpeed(locomotion.GravityAmount) - currentVerticalSpeed);
        if (requiredImpulse <= 0f)
        {
            ResetPresentation();
            return;
        }

        Vector3 planarVelocity = Vector3.ProjectOnPlane(locomotion.Velocity, transform.up);
        Vector3 slowdownImpulse = -planarVelocity * Mathf.Clamp01(jumpStartPlanarSlowdown);
        // AddForce is consumed by UCC's motor. No Transform is ever written.
        // A zero slowdown is intentionally a no-op and preserves the approved feel.
        locomotion.AddForce(transform.up * requiredImpulse + slowdownImpulse, 1, false);
        takeoffImpulseApplied = true;
    }

    private void ResolveReferences()
    {
        if (locomotion == null) locomotion = GetComponent<UltimateCharacterLocomotion>();
        if (locomotionBridge == null) locomotionBridge = GetComponent<LitOpsiveLocomotionBridge>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void SetPhase(Phase value)
    {
        phase = value;
        SetInteger("JumpPhase", (int)value);
    }

    private bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == name && parameter.type == type) return true;
        return false;
    }

    private void SetBool(string name, bool value) { if (HasParameter(name, AnimatorControllerParameterType.Bool)) animator.SetBool(name, value); }
    private void SetInteger(string name, int value) { if (HasParameter(name, AnimatorControllerParameterType.Int)) animator.SetInteger(name, value); }
    private void SetTrigger(string name) { if (HasParameter(name, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(name); }
    private void ResetTrigger(string name) { if (HasParameter(name, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(name); }
}
