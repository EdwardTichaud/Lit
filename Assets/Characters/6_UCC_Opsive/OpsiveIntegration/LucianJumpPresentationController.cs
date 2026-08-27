using System.Collections.Generic;
using System.Text;
using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LucianJumpPresentationController : MonoBehaviour
{
    private const string AbilityActiveEvent = "OnCharacterAbilityActive";
    private const string GroundedEvent = "OnCharacterGrounded";
    private const string LandEvent = "OnCharacterLand";

    private enum PresentationPhase { Grounded, Takeoff, Ascending, Falling, Landing }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private struct HandoffDiagnosticSample
    {
        public int frame;
        public float time;
        public float planarSpeed;
        public float verticalSpeed;
        public float rootMotionMagnitude;
        public float normalizedTime;
        public int stateHash;
        public PresentationPhase phase;
        public bool grounded;
        public bool approaching;
        public bool landing;
    }
#endif

    [Header("References")]
    [SerializeField] private UltimateCharacterLocomotion locomotion;
    [SerializeField] private Animator animator;
    [SerializeField] private LitOpsiveLocomotionBridge locomotionBridge;

    [Header("Animator")]
    [SerializeField] private string jumpStartTrigger = "JumpStartTrigger";
    [SerializeField] private string landingTrigger = "LandingTrigger";
    [SerializeField] private string jumpRollTrigger = "JumpRollTrigger";
    [SerializeField] private string activeParam = "JumpPresentationActive";
    [SerializeField] private string airborneParam = "IsAirborne";
    [SerializeField] private string phaseParam = "JumpPhase";
    [SerializeField] private string landingTypeParam = "LandingType";
    [SerializeField] private string jumpEndStateName = "Jump_End";
    [SerializeField] private string hardLandingStateName = "Landing_Hard";
    [SerializeField] private string jumpRollStateName = "Jump_Roll";

    [Header("Feel")]
    [SerializeField] private MotionHandoffProfile landingHandoff = new MotionHandoffProfile {
        minimumContactSeconds = 0.15f,
        animationExitNormalizedTime = 0.82f,
        planarSettledSpeed = 0.12f,
        verticalSettledSpeed = 0.2f,
        planarDampingPerSecond = 7f,
        maximumSettleSeconds = 0.55f,
        locomotionBlendSeconds = 0.12f,
        preLandingProbeDistance = 1.2f,
        preLandingLeadSeconds = 0.14f
    };
    [SerializeField, Min(0f)] private float hardLandingHeight = 3f;
    [SerializeField, Range(0f, 1f), Tooltip("Minimum magnitude of the last explicit move input required to roll on landing. The direction itself is preserved in world space.")]
    private float rollForwardInputThreshold = 0.55f;
    [SerializeField, Min(0f), Tooltip("How long a released move direction remains eligible for a landing roll.")]
    private float landingRollInputMemorySeconds = 0.2f;
    [SerializeField, Min(0f)] private float apexVelocityThreshold = 0.7f;
    [SerializeField, Range(0.1f, 1f)] private float apexGravityMultiplier = 0.35f;
    [SerializeField, Min(0f), Tooltip("Short weighted suspension at the top of a jump. It only changes gravity; UCC remains the owner of position and collision.")]
    private float apexHangDuration = 0.14f;
    [SerializeField, Range(0.05f, 1f), Tooltip("Gravity multiplier at the start of the apex suspension. Lower values create more float without freezing the character.")]
    private float apexHangGravityMultiplier = 0.16f;
    [SerializeField, Range(0.1f, 1f), Tooltip("The physical jump keeps normal UCC gravity during descent; the landing contract, not a reduced gravity timer, creates the weight.")]
    private float descentGravityMultiplier = 1f;

    [Header("Development")]
    [SerializeField] private bool debugTrace;

    private readonly Queue<string> trace = new Queue<string>(48);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private readonly HandoffDiagnosticSample[] handoffDiagnostics = new HandoffDiagnosticSample[96];
    private int handoffDiagnosticWriteIndex;
    private int handoffDiagnosticCount;
    private float nextHandoffDiagnosticTime;
#endif
    private bool jumpActive;
    private bool leftGround;
    private bool landingRequested;
    private bool rollingLanding;
    private bool gravityOverridden;
    private float baseGravity;
    private bool apexHangConsumed;
    private float apexHangStartedAt = -1f;
    // -1 means that the visual landing approach started before the capsule
    // touched the ground. The recovery timer only starts on real contact.
    private float landingContactStartedAt = -1f;
    private bool approachingLanding;
    private PresentationPhase phase;

    public bool PresentationLocked => landingRequested;
    public bool IsApproachingLanding => approachingLanding;

    private void Awake()
    {
        if (locomotion == null) locomotion = GetComponent<UltimateCharacterLocomotion>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (locomotionBridge == null) locomotionBridge = GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
    }

    private void OnEnable()
    {
        EventHandler.RegisterEvent<Ability, bool>(gameObject, AbilityActiveEvent, OnAbilityActive);
        EventHandler.RegisterEvent<bool>(gameObject, GroundedEvent, OnGroundedChanged);
        EventHandler.RegisterEvent<float>(gameObject, LandEvent, OnLanded);
        ResetPresentation();
    }

    private void OnDisable()
    {
        EventHandler.UnregisterEvent<Ability, bool>(gameObject, AbilityActiveEvent, OnAbilityActive);
        EventHandler.UnregisterEvent<bool>(gameObject, GroundedEvent, OnGroundedChanged);
        EventHandler.UnregisterEvent<float>(gameObject, LandEvent, OnLanded);
        locomotionBridge?.EndDirectionalEvasionFacing();
        RestoreGravity();
    }

    private void Update()
    {
        if (!jumpActive || locomotion == null)
        {
            return;
        }

        float verticalVelocity = Vector3.Dot(locomotion.Velocity, transform.up);
        SetPhase(verticalVelocity > apexVelocityThreshold ? PresentationPhase.Ascending : PresentationPhase.Falling);

        if (!landingRequested)
        {
            approachingLanding = IsApproachingGround(verticalVelocity);
            if (approachingLanding)
            {
                RequestLanding(0f, "Ground approach");
                // RequestLanding resets transient input state. Keep this flag
                // until the UCC contact event so diagnostics distinguish the
                // pre-contact approach from the recovery on the ground.
                approachingLanding = locomotionBridge != null && !locomotionBridge.Grounded;
            }
            RecordHandoffDiagnostic();
            return;
        }

        locomotionBridge?.ApplyPlanarHandoffDamping(landingHandoff != null ? landingHandoff.planarDampingPerSecond : 0f);
        RecordHandoffDiagnostic();
        if (IsLandingHandoffComplete())
        {
            FinishLandingPresentation();
        }
    }

    private void FixedUpdate()
    {
        if (!jumpActive || landingRequested || locomotion == null)
        {
            return;
        }

        UpdateGravity(Vector3.Dot(locomotion.Velocity, transform.up));
    }

    private void OnAbilityActive(Ability ability, bool active)
    {
        if (ability == null || ability.GetType().Name != "Jump")
        {
            return;
        }

        Trace("UCC Jump " + (active ? "started" : "stopped") + ".");
        if (!active)
        {
            return;
        }

        jumpActive = true;
        leftGround = false;
        landingRequested = false;
        rollingLanding = false;
        landingContactStartedAt = -1f;
        approachingLanding = false;
        apexHangConsumed = false;
        apexHangStartedAt = -1f;
        baseGravity = locomotion != null ? locomotion.GravityAmount : 0f;
        SetAnimatorBool(activeParam, true);
        SetAnimatorBool(airborneParam, false);
        SetAnimatorInteger(landingTypeParam, 0);
        SetPhase(PresentationPhase.Takeoff);
        ResetAnimatorTrigger(jumpStartTrigger);
        SetAnimatorTrigger(jumpStartTrigger);
    }

    private void OnGroundedChanged(bool grounded)
    {
        Trace("UCC grounded=" + grounded + ".");
        if (!jumpActive)
        {
            return;
        }

        if (!grounded)
        {
            leftGround = true;
            SetAnimatorBool(airborneParam, true);
            return;
        }

        if (landingRequested && landingContactStartedAt < 0f)
        {
            landingContactStartedAt = Time.unscaledTime;
            approachingLanding = false;
            Trace("Landing contact confirmed by OnCharacterGrounded.");
            return;
        }

        if (leftGround)
        {
            RequestLanding(0f, "OnCharacterGrounded");
        }
    }

    private void OnLanded(float fallHeight)
    {
        Trace("UCC OnLand height=" + fallHeight.ToString("F2") + ".");
        if (landingRequested && landingContactStartedAt < 0f)
        {
            landingContactStartedAt = Time.unscaledTime;
            approachingLanding = false;
            if (fallHeight >= hardLandingHeight && !rollingLanding)
            {
                SetAnimatorInteger(landingTypeParam, 1);
                ResetAnimatorTrigger(landingTrigger);
                SetAnimatorTrigger(landingTrigger);
                Trace("Hard landing promoted from OnCharacterLand.");
            }
            Trace("Landing contact confirmed by OnCharacterLand.");
            return;
        }

        if (jumpActive || leftGround)
        {
            RequestLanding(fallHeight, "OnCharacterLand");
        }
    }

    private void RequestLanding(float fallHeight, string source)
    {
        if (landingRequested)
        {
            return;
        }

        landingRequested = true;
        leftGround = false;
        bool hardLanding = fallHeight >= hardLandingHeight;
        Vector2 rollInput = locomotionBridge != null
            ? locomotionBridge.LastExplicitWorldMoveInput
            : locomotion != null ? locomotion.InputVector : Vector2.zero;
        bool hasRecentRollInput = locomotionBridge == null ||
                                  locomotionBridge.HasRecentExplicitWorldMoveInput(landingRollInputMemorySeconds);
        rollingLanding = !hardLanding && hasRecentRollInput &&
                         rollInput.sqrMagnitude >= rollForwardInputThreshold * rollForwardInputThreshold;
        landingContactStartedAt = locomotion != null && locomotion.Grounded ? Time.unscaledTime : -1f;
        approachingLanding = false;
        SetAnimatorBool(airborneParam, false);
        SetAnimatorInteger(landingTypeParam, hardLanding ? 1 : 0);
        SetPhase(PresentationPhase.Landing);
        if (rollingLanding)
        {
            Vector3 rollDirection = new Vector3(rollInput.x, 0f, rollInput.y);
            locomotionBridge?.BeginDirectionalEvasionFacing(rollDirection);
            ResetAnimatorTrigger(jumpRollTrigger);
            SetAnimatorTrigger(jumpRollTrigger);
            Trace("Directional landing roll requested by " + source + ".");
        }
        else
        {
            ResetAnimatorTrigger(landingTrigger);
            SetAnimatorTrigger(landingTrigger);
            Trace("Landing requested by " + source + ".");
        }
    }

    private void UpdateGravity(float verticalVelocity)
    {
        if (locomotion == null || landingRequested)
        {
            return;
        }

        if (!gravityOverridden)
        {
            baseGravity = locomotion.GravityAmount;
            gravityOverridden = true;
        }

        if (!apexHangConsumed && verticalVelocity <= apexVelocityThreshold)
        {
            apexHangConsumed = true;
            apexHangStartedAt = Time.unscaledTime;
            Trace("Apex hang started.");
        }

        float multiplier;
        if (apexHangStartedAt >= 0f)
        {
            float duration = Mathf.Max(0.0001f, apexHangDuration);
            float progress = Mathf.Clamp01((Time.unscaledTime - apexHangStartedAt) / duration);
            // Ease out of the lightest point rather than switching straight
            // from ascent gravity to descent gravity at zero vertical speed.
            multiplier = Mathf.Lerp(apexHangGravityMultiplier, apexGravityMultiplier, progress * progress);
            if (progress >= 1f)
            {
                apexHangStartedAt = -1f;
                Trace("Apex hang completed.");
            }
        }
        else
        {
            multiplier = verticalVelocity > apexVelocityThreshold ? 1f :
                verticalVelocity >= -apexVelocityThreshold ? apexGravityMultiplier : descentGravityMultiplier;
        }

        locomotion.GravityAmount = baseGravity * multiplier;
    }

    private void RestoreGravity()
    {
        if (!gravityOverridden || locomotion == null)
        {
            gravityOverridden = false;
            return;
        }

        locomotion.GravityAmount = baseGravity;
        gravityOverridden = false;
    }

    private void ResetPresentation()
    {
        jumpActive = false;
        leftGround = false;
        landingRequested = false;
        rollingLanding = false;
        landingContactStartedAt = -1f;
        approachingLanding = false;
        apexHangConsumed = false;
        apexHangStartedAt = -1f;
        locomotionBridge?.EndDirectionalEvasionFacing();
        SetAnimatorBool(activeParam, false);
        SetAnimatorBool(airborneParam, false);
        SetAnimatorInteger(landingTypeParam, 0);
        SetPhase(PresentationPhase.Grounded);
    }

    private void SetPhase(PresentationPhase value)
    {
        if (phase == value)
        {
            return;
        }

        phase = value;
        SetAnimatorInteger(phaseParam, (int)value);
        Trace("Phase=" + value + ".");
    }

    private bool IsApproachingGround(float verticalVelocity)
    {
        return verticalVelocity < -0.01f && locomotionBridge != null &&
               locomotionBridge.ShouldBeginMotionHandoff(landingHandoff);
    }

    private bool IsLandingHandoffComplete()
    {
        if (landingHandoff == null)
        {
            return locomotionBridge != null && locomotionBridge.Grounded;
        }

        if (locomotionBridge == null || !locomotionBridge.Grounded)
        {
            return false;
        }

        if (landingContactStartedAt < 0f)
        {
            landingContactStartedAt = Time.unscaledTime;
            approachingLanding = false;
        }

        float elapsed = Time.unscaledTime - landingContactStartedAt;
        if (elapsed < landingHandoff.minimumContactSeconds)
        {
            return false;
        }

        bool animationReady = HasLandingAnimationReachedExit();
        bool physicsReady = locomotionBridge != null && locomotionBridge.IsMotionHandoffSettled(landingHandoff);
        return (animationReady && physicsReady) || elapsed >= landingHandoff.maximumSettleSeconds;
    }

    private bool HasLandingAnimationReachedExit()
    {
        if (animator == null || landingHandoff == null)
        {
            return true;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
        return IsLandingExitStateReady(current) || IsLandingExitStateReady(next);
    }

    private bool IsLandingExitStateReady(AnimatorStateInfo state)
    {
        if (state.fullPathHash == 0)
        {
            return false;
        }

        int jumpEndHash = Animator.StringToHash(jumpEndStateName);
        int hardLandingHash = Animator.StringToHash(hardLandingStateName);
        int rollHash = Animator.StringToHash(jumpRollStateName);
        return (state.shortNameHash == jumpEndHash || state.shortNameHash == hardLandingHash || state.shortNameHash == rollHash) &&
               state.normalizedTime >= landingHandoff.animationExitNormalizedTime;
    }

    private void FinishLandingPresentation()
    {
        SetAnimatorBool(activeParam, false);
        jumpActive = false;
        landingRequested = false;
        rollingLanding = false;
        approachingLanding = false;
        locomotionBridge?.EndDirectionalEvasionFacing();
        SetPhase(PresentationPhase.Grounded);
        RestoreGravity();
        Trace("Landing presentation released after physical/animation handoff.");
        DumpTrace();
    }

    public void TraceAnimatorState(string eventName, AnimatorStateInfo state)
    {
        Trace(eventName + " state=" + state.fullPathHash + " t=" + state.normalizedTime.ToString("F2") + ".");
    }

    private void Trace(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!debugTrace)
        {
            return;
        }

        if (trace.Count == 48) trace.Dequeue();
        string animatorSnapshot = "none";
        if (animator != null)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            animatorSnapshot = "current=" + current.fullPathHash + " next=" + next.fullPathHash;
        }
        trace.Enqueue("f=" + Time.frameCount + " " + message + " " + animatorSnapshot);
#endif
    }

    private void DumpTrace()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugTrace && trace.Count > 0)
        {
            Debug.Log("[Lucian Jump Trace]\n" + string.Join("\n", trace), this);
        }
        trace.Clear();
        DumpHandoffDiagnostics();
#endif
    }

    private void RecordHandoffDiagnostic()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!debugTrace || Time.unscaledTime < nextHandoffDiagnosticTime)
        {
            return;
        }

        nextHandoffDiagnosticTime = Time.unscaledTime + 0.05f;
        AnimatorStateInfo state = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
        Vector3 velocity = locomotion != null ? locomotion.Velocity : Vector3.zero;
        HandoffDiagnosticSample sample = new HandoffDiagnosticSample {
            frame = Time.frameCount,
            time = Time.unscaledTime,
            planarSpeed = locomotionBridge != null ? locomotionBridge.PlanarVelocity.magnitude : new Vector3(velocity.x, 0f, velocity.z).magnitude,
            verticalSpeed = Vector3.Dot(velocity, transform.up),
            rootMotionMagnitude = animator != null ? animator.deltaPosition.magnitude : 0f,
            normalizedTime = state.normalizedTime,
            stateHash = state.fullPathHash,
            phase = phase,
            grounded = locomotion != null && locomotion.Grounded,
            approaching = approachingLanding,
            landing = landingRequested
        };
        handoffDiagnostics[handoffDiagnosticWriteIndex] = sample;
        handoffDiagnosticWriteIndex = (handoffDiagnosticWriteIndex + 1) % handoffDiagnostics.Length;
        handoffDiagnosticCount = Mathf.Min(handoffDiagnosticCount + 1, handoffDiagnostics.Length);
#endif
    }

    [ContextMenu("Dump Jump Handoff Diagnostics")]
    private void DumpHandoffDiagnostics()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!debugTrace || handoffDiagnosticCount == 0)
        {
            return;
        }

        StringBuilder output = new StringBuilder("[Lucian Jump Handoff]\n");
        int start = (handoffDiagnosticWriteIndex - handoffDiagnosticCount + handoffDiagnostics.Length) % handoffDiagnostics.Length;
        for (int offset = 0; offset < handoffDiagnosticCount; offset++)
        {
            HandoffDiagnosticSample sample = handoffDiagnostics[(start + offset) % handoffDiagnostics.Length];
            output.Append("f=").Append(sample.frame)
                .Append(" t=").Append(sample.time.ToString("F2"))
                .Append(" phase=").Append(sample.phase)
                .Append(" ground=").Append(sample.grounded)
                .Append(" approach=").Append(sample.approaching)
                .Append(" landing=").Append(sample.landing)
                .Append(" planar=").Append(sample.planarSpeed.ToString("F2"))
                .Append(" vertical=").Append(sample.verticalSpeed.ToString("F2"))
                .Append(" root=").Append(sample.rootMotionMagnitude.ToString("F3"))
                .Append(" state=").Append(sample.stateHash)
                .Append(" nt=").Append(sample.normalizedTime.ToString("F2"))
                .AppendLine();
        }

        Debug.Log(output.ToString(), this);
#endif
    }

    private bool HasParameter(string parameter, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(parameter)) return false;
        foreach (AnimatorControllerParameter candidate in animator.parameters)
        {
            if (candidate.name == parameter && candidate.type == type) return true;
        }
        return false;
    }

    private void SetAnimatorBool(string parameter, bool value)
    {
        if (HasParameter(parameter, AnimatorControllerParameterType.Bool)) animator.SetBool(parameter, value);
    }

    private void SetAnimatorInteger(string parameter, int value)
    {
        if (HasParameter(parameter, AnimatorControllerParameterType.Int)) animator.SetInteger(parameter, value);
    }

    private void SetAnimatorTrigger(string parameter)
    {
        if (HasParameter(parameter, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(parameter);
    }

    private void ResetAnimatorTrigger(string parameter)
    {
        if (HasParameter(parameter, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(parameter);
    }
}
