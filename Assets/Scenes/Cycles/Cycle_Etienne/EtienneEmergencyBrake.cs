using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One physical control: release, combat brake, then explicit retry of a failed memory.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
public sealed class EtienneEmergencyBrake : NetworkBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
{
    public CycleController cycle;
    public DeadWeightBoss boss;
    public Transform handle;
    [SerializeField, Min(.1f)] private float interactionDistance = 2f;
    private Collider detection;
    private Quaternion restingPose;
    private double nextRetry;

    private void Awake() { detection = GetComponent<BoxCollider>(); if (handle != null) restingPose = handle.localRotation; }
    private void OnEnable() => LocalInputRouter.Interact += OnCombatInteract;
    private void OnDisable() => LocalInputRouter.Interact -= OnCombatInteract;
    private void OnCombatInteract(InputAction.CallbackContext context)
    {
        var combat = RealTimeCombatManager.Instance;
        if (!context.performed || InputFocusStack.HasAnyFocus() || boss == null || !boss.IsBossEngaged ||
            combat == null || !combat.IsCombatActive || combat.EngagedEnemy != boss.Enemy) return;
        // Standard world selection is intentionally disabled in combat. Only this
        // encounter's nearby lever bypasses it, with the same authoritative checks.
        if (TryHandleLocalInteract()) LocalInputRouter.ConsumeInteract();
    }
    private void Update()
    {
        if (handle == null || cycle == null) return;
        float angle = boss != null && boss.IsBrakeHolding ? 65f : cycle.IsSceneStepCompleted("brake_released") ? 25f : 0f;
        handle.localRotation = Quaternion.Slerp(handle.localRotation, restingPose * Quaternion.Euler(angle, 0, 0), Time.deltaTime * 8);
    }
    public bool CanBeDetectedBy(SquadCharacterController character) => isActiveAndEnabled && character != null &&
        character.CurrentHp > 0 && cycle != null && cycle.Status != CycleStatus.Unavailable && cycle.Status != CycleStatus.Completed &&
        CharacterInteractionDetection.IsInActiveFlameInfluence(this) && HasAction();
    private bool HasAction() => cycle != null && (cycle.IsSceneEventActive(CycleStepKind.Interaction, "emergency_brake") ||
        boss != null && boss.IsBossEngaged && boss.BrakeCooldownRemaining <= 0 ||
        cycle.IsSceneEventActive(CycleStepKind.SequenceCompleted, "last_relief"));
    public Collider GetInteractionDetectionCollider() => detection != null ? detection : detection = GetComponent<Collider>();
    public Transform GetInteractionAnchor() => transform;
    public float GetInteractionMaxDistance(SquadCharacterController _) => interactionDistance;
    public int GetInteractionPriority(SquadCharacterController _) => 10;
    public void SetDetectedCharacter(GameObject _) { }
    public bool TryHandleLocalInteract()
    {
        Transform player = LocalPlayerContext.LocalCharacterRoot;
        if (!Validate(player)) return false;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!IsSpawned) return false;
            UseServerRpc();
            return true;
        }
        return Use(player);
    }
    private bool Validate(Transform player)
    {
        SquadCharacterController character = player != null ? player.GetComponentInChildren<SquadCharacterController>() : null;
        if (character == null && player != null) character = player.GetComponentInParent<SquadCharacterController>();
        var bridge = character != null ? character.GetComponent<LitUccInteractionBridge>() : null;
        return CanBeDetectedBy(character) && (bridge == null || bridge.CanUseLitInteractable(this)) && CharacterInteractionDetection.IsCharacterWithinRange(player,
            GetInteractionDetectionCollider(), transform, interactionDistance);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UseServerRpc(RpcParams rpc = default) => Use(NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId));
    private bool Use(Transform player)
    {
        if (!Validate(player)) return false;
        if (cycle.IsSceneEventActive(CycleStepKind.Interaction, "emergency_brake"))
            return cycle.TryReportSceneEvent(CycleStepKind.Interaction, "emergency_brake");
        if (boss != null && boss.IsBossEngaged) return boss.TryApplyEmergencyBrake();
        if (Time.unscaledTimeAsDouble < nextRetry) return false;
        nextRetry = Time.unscaledTimeAsDouble + 1;
        return cycle.TryRetrySceneSequence("last_relief");
    }
}
