using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CombatDodgeDashProfile
{
    [Tooltip("Full Animator state path using this profile.")]
    public string statePath;
    [Min(0.01f)] public float distance = 3f;
    [Min(0.01f)] public float durationSeconds = 0.52f;
    [Min(0.01f), Tooltip("Initial UCC velocity-change applied once when the dodge begins. The remaining motion comes from UCC inertia.")]
    public float impulseSpeed = 16f;
    [Tooltip("Normalized distance travelled over the normalized dodge duration.")]
    public AnimationCurve distanceOverTime = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0.05f, 1f), Tooltip("Stops the dash when UCC resolves less than this fraction of a requested movement step.")]
    public float blockedStepRatio = 0.35f;

    public float EvaluateDistance(float normalizedTime)
    {
        return Mathf.Clamp01(distanceOverTime != null ? distanceOverTime.Evaluate(Mathf.Clamp01(normalizedTime)) : normalizedTime);
    }
}

[Serializable]
public sealed class CombatMobilityActionSettings
{
    [Min(0f)] public float cooldownSeconds = 0.25f;
    [Range(0f, 0.25f)] public float entryBlendSeconds = 0.05f;
    [Range(0.05f, 1f)] public float recoveryNormalizedTime = 0.72f;
    [Range(0f, 1f)] public float movementCancelNormalizedTime = 0.7f;
    [Header("In-Place Dodge Dash")]
    public List<CombatDodgeDashProfile> dashProfiles = new List<CombatDodgeDashProfile>();

    public CombatDodgeDashProfile FindDashProfile(string statePath)
    {
        if (dashProfiles == null) return null;
        for (int i = 0; i < dashProfiles.Count; i++)
        {
            CombatDodgeDashProfile profile = dashProfiles[i];
            if (profile != null && string.Equals(profile.statePath, statePath, StringComparison.Ordinal)) return profile;
        }

        return null;
    }
}

[DisallowMultipleComponent]
public sealed class CombatMobilityController : MonoBehaviour
{
    private enum MobilityCommand
    {
        None,
        Dodge,
        Jump
    }

    [Header("References")]
    [SerializeField] private RealTimeCombatInput combatInput;
    [SerializeField] private PlayerActionPresentationController actionPresentation;
    private PlayerScriptedDodgeController scriptedDodgeController;

    [Header("Input Buffer")]
    [SerializeField, Min(0f)] private float mobilityInputBufferSeconds = 0.12f;

    [Header("Dodge")]
    [SerializeField] private string dodgeForwardState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Dodge_F_Root";
    [SerializeField] private string dodgeBackwardState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Dodge_B_Root";
    [SerializeField] private string dodgeLeftState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Dodge_L_Root";
    [SerializeField] private string dodgeRightState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Dodge_R_Root";
    [SerializeField] private CombatMobilityActionSettings dodge = new CombatMobilityActionSettings
    {
        cooldownSeconds = 0.25f,
        entryBlendSeconds = 0.05f,
        recoveryNormalizedTime = 0.72f,
        movementCancelNormalizedTime = 0.7f
    };
    [SerializeField, Min(0f)] private float dodgeStartupSeconds = 0.05f;
    [SerializeField, Min(0f)] private float dodgeInvulnerabilitySeconds = 0.18f;

    private MobilityCommand bufferedCommand;
    private float bufferedCommandExpiresAt;
    private float dodgeReadyAt;
    private float damageInvulnerableUntil;
    private Coroutine dodgeInvulnerabilityRoutine;

    public bool IsDamageInvulnerable => Time.unscaledTime < damageInvulnerableUntil;

    public bool HasDodgeDashProfile(string statePath)
    {
        CombatDodgeDashProfile profile = dodge != null ? dodge.FindDashProfile(statePath) : null;
        return profile != null && profile.distance > 0f && profile.durationSeconds > 0f && profile.impulseSpeed > 0f;
    }

    public void ConfigureDodgeDashProfile(string statePath, float distance, float durationSeconds, AnimationCurve distanceOverTime)
    {
        if (dodge == null || string.IsNullOrWhiteSpace(statePath)) return;
        if (dodge.dashProfiles == null) dodge.dashProfiles = new List<CombatDodgeDashProfile>();

        CombatDodgeDashProfile profile = dodge.FindDashProfile(statePath);
        if (profile == null)
        {
            profile = new CombatDodgeDashProfile { statePath = statePath };
            dodge.dashProfiles.Add(profile);
        }

        profile.distance = Mathf.Max(0.01f, distance);
        profile.durationSeconds = Mathf.Max(0.01f, durationSeconds);
        profile.distanceOverTime = distanceOverTime ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        bufferedCommand = MobilityCommand.None;
        damageInvulnerableUntil = 0f;
        if (dodgeInvulnerabilityRoutine != null)
        {
            StopCoroutine(dodgeInvulnerabilityRoutine);
            dodgeInvulnerabilityRoutine = null;
        }

        scriptedDodgeController?.CancelDodge();
    }

    private void Update()
    {
        if (bufferedCommand == MobilityCommand.None)
        {
            return;
        }

        if (Time.unscaledTime > bufferedCommandExpiresAt)
        {
            bufferedCommand = MobilityCommand.None;
            return;
        }

        TryExecute(bufferedCommand, false);
    }

    public void RequestDodge()
    {
        TryExecute(MobilityCommand.Dodge, true);
    }

    public bool TryDodgeImmediate() => TryExecute(MobilityCommand.Dodge, false);

    public void RequestJump()
    {
        TryExecute(MobilityCommand.Jump, true);
    }

    private void ResolveReferences()
    {
        if (combatInput == null) combatInput = GetComponent<RealTimeCombatInput>();
        if (actionPresentation == null)
        {
            Transform player = RealTimeCombatManager.Instance != null
                ? RealTimeCombatManager.Instance.PlayerRoot
                : LocalPlayerContext.LocalCharacterRoot;
            if (player != null) actionPresentation = player.GetComponentInChildren<PlayerActionPresentationController>(true);
        }
    }

    private PlayerScriptedDodgeController ResolveScriptedDodgeController(RealTimeCombatManager manager)
    {
        Transform playerRoot = manager != null ? manager.PlayerRoot : LocalPlayerContext.LocalCharacterRoot;
        if (playerRoot == null) return null;

        scriptedDodgeController = playerRoot.GetComponentInChildren<PlayerScriptedDodgeController>(true);
        return scriptedDodgeController;
    }

    private bool TryExecute(MobilityCommand command, bool allowBuffer)
    {
        RealTimeCombatManager manager = RealTimeCombatManager.Instance;
        if (manager == null || !manager.IsCombatActive || manager.IsCinematicSequenceActive || manager.PlayerRoot == null)
        {
            return false;
        }

        ResolveReferences();
        LitOpsiveLocomotionBridge bridge = manager.PlayerRoot.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        if (bridge == null || !bridge.IsDriving || bridge.IsInputSuppressedByUcc)
        {
            return false;
        }

        if (command == MobilityCommand.Dodge && IsDodgeBlockedByJump(manager.PlayerRoot, bridge))
        {
            if (bufferedCommand == MobilityCommand.Dodge)
            {
                bufferedCommand = MobilityCommand.None;
            }

            return false;
        }

        if (!IsOffCooldown(command))
        {
            return false;
        }

        if (actionPresentation != null && !actionPresentation.CanCancelToMobility)
        {
            if (allowBuffer)
            {
                bufferedCommand = command;
                bufferedCommandExpiresAt = Time.unscaledTime + mobilityInputBufferSeconds;
            }

            return false;
        }

        if (actionPresentation != null && !actionPresentation.CancelActionForMobility())
        {
            return false;
        }

        combatInput?.CancelBufferedBasicSkills();
        bufferedCommand = MobilityCommand.None;

        switch (command)
        {
            case MobilityCommand.Dodge:
                return ExecuteDodge(manager, bridge);
            case MobilityCommand.Jump:
                return ExecuteJump(bridge);
            default:
                return false;
        }
    }

    private bool ExecuteDodge(RealTimeCombatManager manager, LitOpsiveLocomotionBridge bridge)
    {
        Vector2 movementInput = bridge.IsCombatLockActive
            ? bridge.CombatLockLocalInput
            : bridge.CurrentWorldMoveInput;
        bool hasExplicitDirection = movementInput.sqrMagnitude > 0.0001f;
        Vector3 direction;
        string state;
        if (hasExplicitDirection)
        {
            direction = new Vector3(bridge.CurrentWorldMoveInput.x, 0f, bridge.CurrentWorldMoveInput.y).normalized;
            state = ResolveDodgeState(manager.PlayerRoot, direction);
        }
        else
        {
            direction = ResolveMovementDirection(manager.PlayerRoot, fallbackBackward: true);
            state = dodgeBackwardState;
        }

        CombatDodgeDashProfile dashProfile = dodge.FindDashProfile(state);
        if (dashProfile == null || dashProfile.distance <= 0f || dashProfile.durationSeconds <= 0f)
        {
            Debug.LogError("[Combat Dodge] Missing in-place dash profile for '" + state + "'.", this);
            return false;
        }

        if (!TryPlayMobilityState(state, dodge, PlayerActionMovementPolicy.ExistingScripted, "Dodge"))
        {
            return false;
        }

        scriptedDodgeController = ResolveScriptedDodgeController(manager);
        if (scriptedDodgeController == null || !scriptedDodgeController.TryStartDodge(bridge, actionPresentation, direction, dashProfile))
        {
            return false;
        }

        dodgeReadyAt = Time.unscaledTime + dodge.cooldownSeconds;
        if (dodgeInvulnerabilityRoutine != null)
        {
            StopCoroutine(dodgeInvulnerabilityRoutine);
        }

        dodgeInvulnerabilityRoutine = StartCoroutine(GrantDodgeInvulnerability());
        return true;
    }

    private bool ExecuteJump(LitOpsiveLocomotionBridge bridge)
    {
        Vector2 worldInput = bridge.CurrentWorldMoveInput;
        bool started = bridge.Jump(worldInput, worldInput.sqrMagnitude > 0.0001f);
        return started;
    }

    private bool TryPlayMobilityState(
        string stateName,
        CombatMobilityActionSettings settings,
        PlayerActionMovementPolicy movementPolicy,
        string debugName)
    {
        if (actionPresentation == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        actionPresentation.ClearActionFacingTarget();
        PlayerActionPresentationProfile profile = new PlayerActionPresentationProfile
        {
            entryBlendSeconds = settings.entryBlendSeconds,
            chainNormalizedTime = 0.05f,
            chainTransitionNormalizedTime = 0.05f,
            mobilityCancelNormalizedTime = settings.movementCancelNormalizedTime,
            recoveryNormalizedTime = settings.recoveryNormalizedTime,
            exitBlendSeconds = 0.08f,
            movementPolicy = movementPolicy,
            facingMode = PlayerActionFacingMode.UccBody,
            allowMoveAfterRecovery = true,
            allowDodgeAfterRecovery = true,
            allowMobilityCancel = true
        };

        return actionPresentation.TryPlayCombatState(stateName, profile, debugName);
    }

    private IEnumerator GrantDodgeInvulnerability()
    {
        if (dodgeStartupSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(dodgeStartupSeconds);
        }

        damageInvulnerableUntil = Time.unscaledTime + dodgeInvulnerabilitySeconds;
        yield return new WaitForSecondsRealtime(dodgeInvulnerabilitySeconds);
        if (Time.unscaledTime >= damageInvulnerableUntil)
        {
            damageInvulnerableUntil = 0f;
        }

        dodgeInvulnerabilityRoutine = null;
    }

    private bool IsOffCooldown(MobilityCommand command)
    {
        switch (command)
        {
            case MobilityCommand.Dodge:
                return Time.unscaledTime >= dodgeReadyAt;
            default:
                return true;
        }
    }

    private static bool IsDodgeBlockedByJump(Transform playerRoot, LitOpsiveLocomotionBridge bridge)
    {
        PlayerScriptedJumpController jump = playerRoot != null
            ? playerRoot.GetComponentInChildren<PlayerScriptedJumpController>(true)
            : null;
        return (jump != null && jump.IsActive) || (bridge != null && !bridge.Grounded);
    }

    private static Vector3 ResolveMovementDirection(Transform player, bool fallbackBackward)
    {
        LitOpsiveLocomotionBridge bridge = player != null
            ? player.GetComponentInChildren<LitOpsiveLocomotionBridge>(true)
            : null;
        Vector2 input = bridge != null ? bridge.CurrentWorldMoveInput : Vector2.zero;
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        Vector3 fallback = player != null ? player.forward : Vector3.forward;
        return fallbackBackward ? -fallback : fallback;
    }

    private string ResolveDodgeState(Transform player, Vector3 worldDirection)
    {
        if (player == null)
        {
            return dodgeForwardState;
        }

        float forward = Vector3.Dot(player.forward, worldDirection);
        float right = Vector3.Dot(player.right, worldDirection);
        if (Mathf.Abs(forward) >= Mathf.Abs(right))
        {
            return forward >= 0f ? dodgeForwardState : dodgeBackwardState;
        }

        return right >= 0f ? dodgeRightState : dodgeLeftState;
    }
}
