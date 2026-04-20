using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(StarterInspiredThirdPersonMotor))]
[RequireComponent(typeof(Animator))]
public sealed class StarterMotorAnimatorDriver : MonoBehaviour
{
    private enum JumpPhase
    {
        Grounded = 0,
        Takeoff = 1,
        Airborne = 2,
        LandingRecovery = 3
    }

    private enum LandingType
    {
        None = 0,
        Recovery = 1
    }

    [Header("References")]
    [SerializeField] private StarterInspiredThirdPersonMotor motor;
    [SerializeField] private Animator animator;
    [SerializeField] private bool disableRootMotion = true;

    [Header("Locomotion")]
    [SerializeField, Min(0.01f)] private float motorFullSpeed = 3.25f;
    [SerializeField, Min(0.01f)] private float locomotionBlendMax = 3.25f;
    [SerializeField, Min(0f)] private float speedDampTime = 0.05f;
    [SerializeField, Min(0f)] private float motionSpeedDampTime = 0.05f;
    [SerializeField, Min(0f)] private float movingEnterSpeedThreshold = 0.32f;
    [SerializeField, Min(0f)] private float movingExitSpeedThreshold = 0.12f;
    [SerializeField, Range(0f, 1f)] private float walkTierThreshold = 0.33f;
    [SerializeField, Range(0f, 1f)] private float jogTierThreshold = 0.72f;

    [Header("Airborne")]
    [SerializeField, Min(0f)] private float landingVisualHoldTime = 0.34f;
    [SerializeField] private bool crossFadeJumpStates = true;
    [SerializeField, Min(0f)] private float jumpCrossFadeDuration = 0.08f;
    [SerializeField, Min(0f)] private float fallCrossFadeDuration = 0.1f;
    [SerializeField, Min(0f)] private float landingCrossFadeDuration = 0.08f;
    [SerializeField] private int animatorLayer = 0;

    [Header("State Names")]
    [SerializeField] private string jumpTakeoffStateName = "Jump_Takeoff";
    [SerializeField] private string jumpAirborneStateName = "Jump_Airborne";
    [SerializeField] private string freeFallStateName = "Falling";
    [SerializeField] private string landingStateName = "Landing";
    [SerializeField] private string jumpLandingStateName = "Jump_Land";
    [SerializeField] private string heavyLandingStateName = "Landing_Hard";

    [Header("Parameters")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string motionSpeedParam = "MotionSpeed";
    [SerializeField] private string groundedParam = "Grounded";
    [SerializeField] private string jumpBoolParam = "Jump";
    [SerializeField] private string freeFallParam = "FreeFall";
    [SerializeField] private string verticalSpeedParam = "VerticalSpeed";
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveStartTriggerParam = "MoveStartTrigger";
    [SerializeField] private string moveStopTriggerParam = "MoveStopTrigger";
    [SerializeField] private string locomotionTierParam = "LocomotionTier";
    [SerializeField] private string jumpTriggerParam = "JumpTrigger";
    [SerializeField] private string isAirborneParam = "IsAirborne";
    [SerializeField] private string landingTriggerParam = "LandTrigger";
    [SerializeField] private string landingTriggerFallbackParam = "LandingTrigger";
    [SerializeField] private string landingBoolParam = "Landing";
    [SerializeField] private string landingTypeParam = "LandingType";
    [SerializeField] private string jumpFromMovementParam = "JumpFromMovement";
    [SerializeField] private string jumpPhaseParam = "JumpPhase";

    [Header("Debug")]
    [SerializeField] private bool showDebugValues = true;
    [SerializeField] private float debugAnimatorSpeed;
    [SerializeField] private float debugMotionSpeed;
    [SerializeField] private bool debugGrounded;
    [SerializeField] private bool debugIsMoving;
    [SerializeField] private bool debugAirborne;
    [SerializeField] private bool debugFreeFall;
    [SerializeField] private bool debugJumpTriggered;
    [SerializeField] private bool debugLandingTriggered;
    [SerializeField] private StarterInspiredThirdPersonMotor.LandingSeverity debugLandingSeverity;
    [SerializeField] private float debugLocomotionTier;
    [SerializeField] private int debugJumpPhase;
    [SerializeField] private int debugLandingType;
    [SerializeField] private int debugCurrentStateShortHash;
    [SerializeField] private bool debugRootMotionDisabled;

    private readonly Dictionary<int, AnimatorControllerParameterType> parameterTypes = new();
    private float landingVisualTimer;
    private bool wasMoving;
    private bool wasFreeFalling;
    private bool jumpSequenceActive;
    private bool lastJumpFromMovement;
    private float lastMovingLocomotionTier = 1f;

    private int speedHash;
    private int motionSpeedHash;
    private int groundedHash;
    private int jumpBoolHash;
    private int freeFallHash;
    private int verticalSpeedHash;
    private int isMovingHash;
    private int moveStartTriggerHash;
    private int moveStopTriggerHash;
    private int locomotionTierHash;
    private int jumpTriggerHash;
    private int isAirborneHash;
    private int landingTriggerHash;
    private int landingTriggerFallbackHash;
    private int landingBoolHash;
    private int landingTypeHash;
    private int jumpFromMovementHash;
    private int jumpPhaseHash;

    public float DebugAnimatorSpeed => debugAnimatorSpeed;
    public float DebugMotionSpeed => debugMotionSpeed;
    public bool DebugGrounded => debugGrounded;
    public bool DebugIsMoving => debugIsMoving;
    public bool DebugAirborne => debugAirborne;
    public bool DebugFreeFall => debugFreeFall;
    public bool DebugJumpTriggered => debugJumpTriggered;
    public bool DebugLandingTriggered => debugLandingTriggered;
    public StarterInspiredThirdPersonMotor.LandingSeverity DebugLandingSeverity => debugLandingSeverity;
    public float DebugLocomotionTier => debugLocomotionTier;
    public int DebugJumpPhase => debugJumpPhase;
    public int DebugLandingType => debugLandingType;

    private void Reset()
    {
        motor = GetComponent<StarterInspiredThirdPersonMotor>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<StarterInspiredThirdPersonMotor>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        RefreshHashes();
        CacheAnimatorParameters();
        ConfigureAnimator();
        Tick(0f);
    }

    private void OnValidate()
    {
        motorFullSpeed = Mathf.Max(0.01f, motorFullSpeed);
        locomotionBlendMax = Mathf.Max(0.01f, locomotionBlendMax);
        speedDampTime = Mathf.Max(0f, speedDampTime);
        motionSpeedDampTime = Mathf.Max(0f, motionSpeedDampTime);
        movingEnterSpeedThreshold = Mathf.Max(0f, movingEnterSpeedThreshold);
        movingExitSpeedThreshold = Mathf.Clamp(movingExitSpeedThreshold, 0f, movingEnterSpeedThreshold);
        walkTierThreshold = Mathf.Clamp01(walkTierThreshold);
        jogTierThreshold = Mathf.Clamp(jogTierThreshold, walkTierThreshold, 1f);
        landingVisualHoldTime = Mathf.Max(0f, landingVisualHoldTime);
        jumpCrossFadeDuration = Mathf.Max(0f, jumpCrossFadeDuration);
        fallCrossFadeDuration = Mathf.Max(0f, fallCrossFadeDuration);
        landingCrossFadeDuration = Mathf.Max(0f, landingCrossFadeDuration);
        animatorLayer = Mathf.Max(0, animatorLayer);
        RefreshHashes();
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    public void ResetAnimationState()
    {
        landingVisualTimer = 0f;
        wasMoving = false;
        wasFreeFalling = false;
        jumpSequenceActive = false;
        lastJumpFromMovement = false;
        lastMovingLocomotionTier = 1f;
        debugJumpTriggered = false;
        debugLandingTriggered = false;
        debugLandingSeverity = StarterInspiredThirdPersonMotor.LandingSeverity.None;
        debugLocomotionTier = 1f;

        if (animator == null)
        {
            return;
        }

        ConfigureAnimator();
        if (parameterTypes.Count == 0 && animator.runtimeAnimatorController != null)
        {
            CacheAnimatorParameters();
        }

        SetFloat(speedHash, 0f, 0f, 0f);
        SetFloat(motionSpeedHash, 0f, 0f, 0f);
        SetFloat(verticalSpeedHash, 0f, 0f, 0f);
        SetFloat(locomotionTierHash, 1f, 0f, 0f);
        SetBool(isMovingHash, false);
        SetBool(isAirborneHash, false);
        SetBool(freeFallHash, false);
        SetBool(jumpBoolHash, false);
        SetBool(landingBoolHash, false);
        SetBool(jumpFromMovementHash, false);
        SetBool(groundedHash, motor == null || motor.StableGrounded);
        SetInt(jumpPhaseHash, (int)JumpPhase.Grounded);
        SetInt(landingTypeHash, (int)LandingType.None);
    }

    private void Tick(float deltaTime)
    {
        debugJumpTriggered = false;
        debugLandingTriggered = false;

        if (motor == null || animator == null)
        {
            return;
        }

        ConfigureAnimator();

        if (parameterTypes.Count == 0 && animator.runtimeAnimatorController != null)
        {
            CacheAnimatorParameters();
        }

        if (motor.IsLadderTraversalActive)
        {
            HoldNeutralForLadder();
            return;
        }

        UpdateLandingTimer(deltaTime);
        if (motor.LandingTriggered)
        {
            landingVisualTimer = landingVisualHoldTime;
        }

        float actualSpeed = motor.ActualSpeed;
        float normalizedSpeed = Mathf.Clamp01(actualSpeed / motorFullSpeed);
        float animatorSpeed = normalizedSpeed * locomotionBlendMax;
        float motionSpeed = normalizedSpeed;
        bool landingActive = landingVisualTimer > 0f;
        bool moving = ResolveMovingState(actualSpeed, motor.StableGrounded, landingActive);
        float locomotionTier = ResolveDrivenLocomotionTier(actualSpeed, moving);
        bool airborne = motor.Airborne || !motor.StableGrounded;
        bool freeFall = motor.FreeFall;

        if (motor.JumpStarted)
        {
            jumpSequenceActive = true;
            lastJumpFromMovement = actualSpeed > movingExitSpeedThreshold;
        }

        DriveLocomotion(animatorSpeed, motionSpeed, locomotionTier, moving, deltaTime);
        DriveAirborne(airborne, freeFall, landingActive);
        DriveTransitions(actualSpeed, moving, freeFall);

        wasMoving = moving;
        wasFreeFalling = freeFall;

        UpdateDebugValues(animatorSpeed, motionSpeed, locomotionTier, moving, airborne, freeFall, landingActive);
    }

    private void HoldNeutralForLadder()
    {
        landingVisualTimer = 0f;
        wasMoving = false;
        wasFreeFalling = false;
        jumpSequenceActive = false;
        lastJumpFromMovement = false;

        SetFloat(speedHash, 0f, 0f, 0f);
        SetFloat(motionSpeedHash, 0f, 0f, 0f);
        SetFloat(verticalSpeedHash, 0f, 0f, 0f);
        SetFloat(locomotionTierHash, 0f, 0f, 0f);
        SetBool(groundedHash, false);
        SetBool(isMovingHash, false);
        SetBool(isAirborneHash, false);
        SetBool(freeFallHash, false);
        SetBool(jumpBoolHash, false);
        SetBool(landingBoolHash, false);
        SetBool(jumpFromMovementHash, false);
        SetInt(jumpPhaseHash, (int)JumpPhase.Grounded);
        SetInt(landingTypeHash, (int)LandingType.None);
        ResetTrigger(moveStartTriggerHash);
        ResetTrigger(moveStopTriggerHash);
        ResetTrigger(jumpTriggerHash);
        ResetTrigger(landingTriggerHash);
        ResetTrigger(landingTriggerFallbackHash);

        if (!showDebugValues)
        {
            return;
        }

        debugAnimatorSpeed = 0f;
        debugMotionSpeed = 0f;
        debugGrounded = false;
        debugIsMoving = false;
        debugAirborne = false;
        debugFreeFall = false;
        debugLandingTriggered = false;
        debugLandingSeverity = StarterInspiredThirdPersonMotor.LandingSeverity.None;
        debugLocomotionTier = 0f;
        debugJumpPhase = (int)JumpPhase.Grounded;
        debugLandingType = (int)LandingType.None;
        debugRootMotionDisabled = animator != null && !animator.applyRootMotion;
        debugCurrentStateShortHash = animator != null && animator.layerCount > animatorLayer
            ? animator.GetCurrentAnimatorStateInfo(animatorLayer).shortNameHash
            : 0;
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            return;
        }

        if (disableRootMotion)
        {
            animator.applyRootMotion = false;
        }
    }

    private void UpdateLandingTimer(float deltaTime)
    {
        if (landingVisualTimer > 0f)
        {
            landingVisualTimer = Mathf.Max(0f, landingVisualTimer - deltaTime);
            if (landingVisualTimer <= 0f)
            {
                jumpSequenceActive = false;
            }
        }
    }

    private bool ResolveMovingState(float actualSpeed, bool grounded, bool landingActive)
    {
        if (!grounded || landingActive)
        {
            return false;
        }

        if (actualSpeed >= movingEnterSpeedThreshold)
        {
            return true;
        }

        if (actualSpeed <= movingExitSpeedThreshold)
        {
            return false;
        }

        return wasMoving;
    }

    private float ResolveDrivenLocomotionTier(float actualSpeed, bool moving)
    {
        if (moving)
        {
            float tierSelectionSpeed = Mathf.Max(actualSpeed, motor.DesiredSpeed);
            lastMovingLocomotionTier = ResolveLocomotionTier(Mathf.Clamp01(tierSelectionSpeed / motorFullSpeed));
            return lastMovingLocomotionTier;
        }

        return lastMovingLocomotionTier;
    }

    private void DriveLocomotion(
        float animatorSpeed,
        float motionSpeed,
        float locomotionTier,
        bool moving,
        float deltaTime)
    {
        SetFloat(speedHash, animatorSpeed, speedDampTime, deltaTime);
        SetFloat(motionSpeedHash, motionSpeed, motionSpeedDampTime, deltaTime);
        SetBool(groundedHash, motor.StableGrounded);
        SetBool(isMovingHash, moving);
        SetFloat(locomotionTierHash, locomotionTier, 0f, deltaTime);
        SetFloat(verticalSpeedHash, motor.VerticalVelocity, 0f, deltaTime);
    }

    private void DriveAirborne(bool airborne, bool freeFall, bool landingActive)
    {
        JumpPhase phase = ResolveJumpPhase(airborne, landingActive);
        LandingType landingType = landingActive ? LandingType.Recovery : LandingType.None;

        SetBool(isAirborneHash, airborne && !landingActive);
        SetBool(freeFallHash, freeFall);
        SetBool(jumpBoolHash, jumpSequenceActive && !landingActive);
        SetBool(landingBoolHash, landingActive);
        SetBool(jumpFromMovementHash, lastJumpFromMovement);
        SetInt(jumpPhaseHash, (int)phase);
        SetInt(landingTypeHash, (int)landingType);
    }

    private void DriveTransitions(float actualSpeed, bool moving, bool freeFall)
    {
        if (moving && !wasMoving)
        {
            SetTrigger(moveStartTriggerHash);
        }
        else if (!moving && wasMoving && motor.StableGrounded)
        {
            SetTrigger(moveStopTriggerHash);
        }

        if (motor.JumpStarted)
        {
            SetTrigger(jumpTriggerHash);
            debugJumpTriggered = true;
            CrossFadeState(jumpTakeoffStateName, jumpCrossFadeDuration);
            return;
        }

        if (freeFall && !wasFreeFalling)
        {
            CrossFadeState(jumpSequenceActive ? jumpAirborneStateName : freeFallStateName, fallCrossFadeDuration);
        }

        if (motor.LandingTriggered)
        {
            SetTrigger(landingTriggerHash);
            SetTrigger(landingTriggerFallbackHash);
            debugLandingTriggered = true;
            CrossFadeState(ResolveLandingStateName(), landingCrossFadeDuration);
        }
    }

    private JumpPhase ResolveJumpPhase(bool airborne, bool landingActive)
    {
        if (landingActive)
        {
            return JumpPhase.LandingRecovery;
        }

        if (motor.JumpStarted)
        {
            return JumpPhase.Takeoff;
        }

        if (airborne)
        {
            return JumpPhase.Airborne;
        }

        return JumpPhase.Grounded;
    }

    private string ResolveLandingStateName()
    {
        if (motor.LastLandingSeverity == StarterInspiredThirdPersonMotor.LandingSeverity.Heavy &&
            HasAnimatorState(heavyLandingStateName))
        {
            return heavyLandingStateName;
        }

        if (jumpSequenceActive && HasAnimatorState(jumpLandingStateName))
        {
            return jumpLandingStateName;
        }

        return landingStateName;
    }

    private float ResolveLocomotionTier(float normalizedSpeed)
    {
        if (normalizedSpeed >= jogTierThreshold)
        {
            return 3f;
        }

        if (normalizedSpeed >= walkTierThreshold)
        {
            return 2f;
        }

        return 1f;
    }

    private void CrossFadeState(string stateName, float duration)
    {
        if (!crossFadeJumpStates || string.IsNullOrWhiteSpace(stateName) || animator == null)
        {
            return;
        }

        if (!TryResolveStateHash(stateName, out int stateHash))
        {
            return;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        if (current.shortNameHash == stateHash || current.fullPathHash == stateHash)
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, duration, animatorLayer);
    }

    private bool HasAnimatorState(string stateName)
    {
        return TryResolveStateHash(stateName, out _);
    }

    private bool TryResolveStateHash(string stateName, out int stateHash)
    {
        stateHash = 0;
        if (animator == null || string.IsNullOrWhiteSpace(stateName) || animator.layerCount <= animatorLayer)
        {
            return false;
        }

        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(animatorLayer, shortHash))
        {
            stateHash = shortHash;
            return true;
        }

        string layerName = animator.GetLayerName(animatorLayer);
        int fullHash = Animator.StringToHash($"{layerName}.{stateName}");
        if (animator.HasState(animatorLayer, fullHash))
        {
            stateHash = fullHash;
            return true;
        }

        return false;
    }

    private void RefreshHashes()
    {
        speedHash = Animator.StringToHash(speedParam);
        motionSpeedHash = Animator.StringToHash(motionSpeedParam);
        groundedHash = Animator.StringToHash(groundedParam);
        jumpBoolHash = Animator.StringToHash(jumpBoolParam);
        freeFallHash = Animator.StringToHash(freeFallParam);
        verticalSpeedHash = Animator.StringToHash(verticalSpeedParam);
        isMovingHash = Animator.StringToHash(isMovingParam);
        moveStartTriggerHash = Animator.StringToHash(moveStartTriggerParam);
        moveStopTriggerHash = Animator.StringToHash(moveStopTriggerParam);
        locomotionTierHash = Animator.StringToHash(locomotionTierParam);
        jumpTriggerHash = Animator.StringToHash(jumpTriggerParam);
        isAirborneHash = Animator.StringToHash(isAirborneParam);
        landingTriggerHash = Animator.StringToHash(landingTriggerParam);
        landingTriggerFallbackHash = Animator.StringToHash(landingTriggerFallbackParam);
        landingBoolHash = Animator.StringToHash(landingBoolParam);
        landingTypeHash = Animator.StringToHash(landingTypeParam);
        jumpFromMovementHash = Animator.StringToHash(jumpFromMovementParam);
        jumpPhaseHash = Animator.StringToHash(jumpPhaseParam);
    }

    private void CacheAnimatorParameters()
    {
        parameterTypes.Clear();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (!parameterTypes.ContainsKey(parameter.nameHash))
            {
                parameterTypes.Add(parameter.nameHash, parameter.type);
            }
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        return parameterTypes.TryGetValue(hash, out AnimatorControllerParameterType registeredType) &&
               registeredType == type;
    }

    private void SetFloat(int hash, float value, float dampTime, float deltaTime)
    {
        if (!HasParameter(hash, AnimatorControllerParameterType.Float))
        {
            return;
        }

        if (dampTime > 0f && deltaTime > 0f)
        {
            animator.SetFloat(hash, value, dampTime, deltaTime);
            return;
        }

        animator.SetFloat(hash, value);
    }

    private void SetInt(int hash, int value)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(hash, value);
        }
    }

    private void SetBool(int hash, bool value)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(hash, value);
        }
    }

    private void SetTrigger(int hash)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(hash);
            animator.SetTrigger(hash);
        }
    }

    private void ResetTrigger(int hash)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(hash);
        }
    }

    private void UpdateDebugValues(
        float animatorSpeed,
        float motionSpeed,
        float locomotionTier,
        bool moving,
        bool airborne,
        bool freeFall,
        bool landingActive)
    {
        if (!showDebugValues)
        {
            return;
        }

        debugAnimatorSpeed = animatorSpeed;
        debugMotionSpeed = motionSpeed;
        debugLocomotionTier = locomotionTier;
        debugGrounded = motor.StableGrounded;
        debugIsMoving = moving;
        debugAirborne = airborne;
        debugFreeFall = freeFall;
        debugLandingTriggered |= motor.LandingTriggered;
        debugLandingSeverity = motor.LastLandingSeverity;
        debugJumpPhase = (int)ResolveJumpPhase(airborne, landingActive);
        debugLandingType = landingActive ? (int)LandingType.Recovery : (int)LandingType.None;
        debugRootMotionDisabled = animator != null && !animator.applyRootMotion;
        debugCurrentStateShortHash = animator != null && animator.layerCount > animatorLayer
            ? animator.GetCurrentAnimatorStateInfo(animatorLayer).shortNameHash
            : 0;
    }
}
