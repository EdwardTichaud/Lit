using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class CombatMobilityActionSettings
{
    [Min(0f)] public float cooldownSeconds = 0.25f;
    [Range(0f, 0.25f)] public float entryBlendSeconds = 0.05f;
    [Range(0.05f, 1f)] public float recoveryNormalizedTime = 0.72f;
    [Range(0f, 1f)] public float movementCancelNormalizedTime = 0.7f;
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

    [Header("Input Buffer")]
    [SerializeField, Min(0f)] private float mobilityInputBufferSeconds = 0.12f;

    [Header("Dodge")]
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
        Vector2 movementInput = bridge.CurrentWorldMoveInput;
        bool hasExplicitDirection = movementInput.sqrMagnitude > 0.0001f;
        Vector3 direction;
        PlayerModelAnimationState state;
        if (hasExplicitDirection)
        {
            direction = new Vector3(movementInput.x, 0f, movementInput.y).normalized;
            manager.FacePlayerTowardsDirection(direction);
            state = ResolveDodgeState(manager.PlayerRoot, direction);
        }
        else
        {
            manager.FacePlayerTowardsLockedEnemy();
            direction = ResolveMovementDirection(manager.PlayerRoot, fallbackBackward: true);
            state = PlayerModelAnimationState.DodgeBackward;
        }

        if (!TryPlayMobilityState(state, dodge, PlayerActionRootMotionMode.AuthoredRootMotion, "Dodge"))
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
        return bridge.Jump(worldInput, worldInput.sqrMagnitude > 0.0001f);
    }

    private bool TryPlayMobilityState(
        PlayerModelAnimationState state,
        CombatMobilityActionSettings settings,
        PlayerActionRootMotionMode rootMotionMode,
        string debugName)
    {
        if (actionPresentation == null)
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
            exitBlendSeconds = 0.06f,
            rootMotionMode = rootMotionMode,
            facingMode = PlayerActionFacingMode.UccBody,
            allowMoveAfterRecovery = true,
            allowDodgeAfterRecovery = true,
            allowMobilityCancel = true
        };

        return actionPresentation.TryPlayPlayerModelState(state, profile, debugName);
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

    private PlayerModelAnimationState ResolveDodgeState(Transform player, Vector3 worldDirection)
    {
        if (player == null)
        {
            return PlayerModelAnimationState.DodgeForward;
        }

        float forward = Vector3.Dot(player.forward, worldDirection);
        float right = Vector3.Dot(player.right, worldDirection);
        if (Mathf.Abs(forward) >= Mathf.Abs(right))
        {
            return forward >= 0f ? PlayerModelAnimationState.DodgeForward : PlayerModelAnimationState.DodgeBackward;
        }

        return right >= 0f ? PlayerModelAnimationState.DodgeRight : PlayerModelAnimationState.DodgeLeft;
    }
}
