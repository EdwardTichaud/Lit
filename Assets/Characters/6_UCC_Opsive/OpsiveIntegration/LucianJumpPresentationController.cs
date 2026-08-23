using System.Collections.Generic;
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

    [Header("References")]
    [SerializeField] private UltimateCharacterLocomotion locomotion;
    [SerializeField] private Animator animator;

    [Header("Animator")]
    [SerializeField] private string jumpStartTrigger = "JumpStartTrigger";
    [SerializeField] private string landingTrigger = "LandingTrigger";
    [SerializeField] private string activeParam = "JumpPresentationActive";
    [SerializeField] private string airborneParam = "IsAirborne";
    [SerializeField] private string phaseParam = "JumpPhase";
    [SerializeField] private string landingTypeParam = "LandingType";

    [Header("Feel")]
    [SerializeField, Min(0.05f)] private float landingLockSeconds = 0.25f;
    [SerializeField, Min(0f)] private float hardLandingHeight = 3f;
    [SerializeField, Min(0f)] private float apexVelocityThreshold = 0.7f;
    [SerializeField, Range(0.1f, 1f)] private float apexGravityMultiplier = 0.35f;
    [SerializeField, Range(0.1f, 1f)] private float descentGravityMultiplier = 0.75f;

    [Header("Development")]
    [SerializeField] private bool debugTrace;

    private readonly Queue<string> trace = new Queue<string>(48);
    private bool jumpActive;
    private bool leftGround;
    private bool landingRequested;
    private bool gravityOverridden;
    private float baseGravity;
    private float landingUnlockTime;
    private PresentationPhase phase;

    public bool PresentationLocked => landingRequested && Time.unscaledTime < landingUnlockTime;

    private void Awake()
    {
        if (locomotion == null) locomotion = GetComponent<UltimateCharacterLocomotion>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
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
        UpdateGravity(verticalVelocity);

        if (landingRequested && Time.unscaledTime >= landingUnlockTime)
        {
            SetAnimatorBool(activeParam, false);
            jumpActive = false;
            landingRequested = false;
            SetPhase(PresentationPhase.Grounded);
            RestoreGravity();
            Trace("Landing presentation released.");
            DumpTrace();
        }
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
        landingUnlockTime = 0f;
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

        if (leftGround)
        {
            RequestLanding(0f, "OnCharacterGrounded");
        }
    }

    private void OnLanded(float fallHeight)
    {
        Trace("UCC OnLand height=" + fallHeight.ToString("F2") + ".");
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
        landingUnlockTime = Time.unscaledTime + landingLockSeconds;
        RestoreGravity();
        SetAnimatorBool(airborneParam, false);
        SetAnimatorInteger(landingTypeParam, fallHeight >= hardLandingHeight ? 1 : 0);
        SetPhase(PresentationPhase.Landing);
        ResetAnimatorTrigger(landingTrigger);
        SetAnimatorTrigger(landingTrigger);
        Trace("Landing requested by " + source + ".");
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

        float multiplier = verticalVelocity > apexVelocityThreshold ? 1f :
            verticalVelocity >= -apexVelocityThreshold ? apexGravityMultiplier : descentGravityMultiplier;
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
        landingUnlockTime = 0f;
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
