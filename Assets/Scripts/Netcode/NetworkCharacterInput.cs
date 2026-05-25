using Unity.Netcode;
using UnityEngine;

// Transmet les inputs locaux au serveur pour un personnage possede.
[RequireComponent(typeof(NetworkObject))]
public class NetworkCharacterInput : NetworkBehaviour
{
    [SerializeField] private SquadCharacterController controller;
    [SerializeField, Tooltip("Intervalle d'envoi des inputs (s).")]
    private float sendInterval = 0.05f;

    private Vector2 rawMoveInput;
    private Vector2 pendingMove;
    private Vector2 lastSentMove;
    private bool wantsRun;
    private bool lastSentRun;
    private bool triggerMuninRequested;
    private float nextSendTime;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            RegisterInput();
        }
    }

    public override void OnNetworkDespawn()
    {
        UnregisterInput();
    }

    public override void OnGainedOwnership()
    {
        RegisterInput();
    }

    public override void OnLostOwnership()
    {
        UnregisterInput();
    }

    private void OnDisable()
    {
        if (IsOwner && IsAssignedToLocalClient())
        {
            SubmitMove(Vector2.zero, false);
        }
    }

    private void RegisterInput()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Move += OnMoveChanged;
        LocalInputRouter.Jump += OnJump;
        LocalInputRouter.TriggerMunin += OnTriggerMunin;
    }

    private void UnregisterInput()
    {
        LocalInputRouter.Move -= OnMoveChanged;
        LocalInputRouter.Jump -= OnJump;
        LocalInputRouter.TriggerMunin -= OnTriggerMunin;
        rawMoveInput = Vector2.zero;
        pendingMove = Vector2.zero;
        lastSentMove = Vector2.zero;
        wantsRun = false;
        lastSentRun = false;
        triggerMuninRequested = false;
        if (controller != null)
        {
            controller.SetSprintModifier(false);
        }
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            rawMoveInput = Vector2.zero;
            pendingMove = Vector2.zero;
            wantsRun = false;
            triggerMuninRequested = false;
            return;
        }

        if (IsGameplayInputBlocked())
        {
            rawMoveInput = Vector2.zero;
            pendingMove = Vector2.zero;
            wantsRun = false;
            triggerMuninRequested = false;
            if (controller != null)
            {
                controller.SetSprintModifier(false);
            }
            if (lastSentMove != Vector2.zero || lastSentRun)
            {
                SubmitMove(Vector2.zero, false);
            }

            return;
        }

        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller != null && controller.IsMovementInputSuppressed)
        {
            rawMoveInput = Vector2.zero;
            pendingMove = Vector2.zero;
            wantsRun = false;
            triggerMuninRequested = false;
            controller.SetSprintModifier(false);
            if (lastSentMove != Vector2.zero || lastSentRun)
            {
                SubmitMove(Vector2.zero, false);
            }

            return;
        }

        rawMoveInput = LocalInputRouter.MoveValue;
        wantsRun = LocalInputRouter.RightShoulderPressed;
        if (controller != null)
        {
            controller.SetSprintModifier(wantsRun);
        }

        pendingMove = controller != null
            ? controller.GetWorldSpaceInput(rawMoveInput)
            : rawMoveInput;

        HandleTriggerMuninRequest();

        if (Time.time < nextSendTime &&
            (pendingMove - lastSentMove).sqrMagnitude < 0.0001f &&
            wantsRun == lastSentRun)
        {
            return;
        }

        SubmitMove(pendingMove, wantsRun);
    }

    private void OnMoveChanged(Vector2 value)
    {
        rawMoveInput = value;
        if (!IsOwner || !IsAssignedToLocalClient() || IsGameplayInputBlocked())
        {
            pendingMove = Vector2.zero;
            return;
        }

        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller != null && controller.IsMovementInputSuppressed)
        {
            pendingMove = Vector2.zero;
            return;
        }

        pendingMove = controller != null
            ? controller.GetWorldSpaceInput(value)
            : value;
    }

    private void OnTriggerMunin(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            return;
        }

        if (IsGameplayInputBlocked())
        {
            return;
        }

        triggerMuninRequested = true;
    }

    private void HandleTriggerMuninRequest()
    {
        if (!triggerMuninRequested)
        {
            return;
        }

        triggerMuninRequested = false;
        if (!LocalInputRouter.TryConsumeTriggerMunin())
        {
            return;
        }

        TriggerMuninServerRpc();
    }

    private void OnJump(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            return;
        }

        if (IsGameplayInputBlocked())
        {
            return;
        }

        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller != null && controller.IsJumpCommitted)
        {
            return;
        }

        Vector2 jumpWorldInput = pendingMove;
        if (jumpWorldInput == Vector2.zero && rawMoveInput != Vector2.zero && controller != null)
        {
            jumpWorldInput = controller.GetWorldSpaceInput(rawMoveInput);
        }

        if (ShouldUseHostLocalMovePath())
        {
            ApplyHostLocalJump(jumpWorldInput);
            return;
        }

        SubmitJumpServerRpc(jumpWorldInput);
    }

    private static bool IsGameplayInputBlocked()
    {
        return InputFocusStack.HasAnyFocus() ||
               JoinSyncSystem.IsGameplayBlocked ||
               (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked());
    }

    private void SubmitMove(Vector2 value, bool runPressed)
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            return;
        }

        lastSentMove = value;
        lastSentRun = runPressed;
        nextSendTime = Time.time + Mathf.Max(0.01f, sendInterval);

        if (ShouldUseHostLocalMovePath())
        {
            ApplyHostLocalMove(rawMoveInput, runPressed);
            return;
        }

        SubmitMoveServerRpc(value, runPressed);
    }

    private bool ShouldUseHostLocalMovePath()
    {
        return IsServer && IsOwner && IsSpawned && IsAssignedToLocalClient();
    }

    private void ApplyHostLocalMove(Vector2 input, bool runPressed)
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        controller.SetSprintModifier(runPressed);
        controller.Move(input);
    }

    private void ApplyHostLocalJump(Vector2 worldInput)
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        controller.QueueCommittedJumpInput(worldInput, isWorldSpace: true);
        controller.Jump();
    }

    [ServerRpc]
    private void SubmitMoveServerRpc(Vector2 input, bool runPressed)
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        controller.SetSprintModifier(runPressed);
        controller.MoveWorld(input);
    }

    [ServerRpc]
    private void SubmitJumpServerRpc(Vector2 worldInput)
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        controller.QueueCommittedJumpInput(worldInput, isWorldSpace: true);
        controller.Jump();
    }

    [ServerRpc]
    private void TriggerMuninServerRpc()
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        controller.TriggerMunin();
        UpdateTorchClientRpc(controller.IsTorchEquipped, controller.TorchSecondsRemaining);
    }

    [ClientRpc]
    private void UpdateTorchClientRpc(bool equipped, int torchSeconds)
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        controller.ApplyTorchState(torchSeconds, equipped);
    }

    private bool IsAssignedToLocalClient()
    {
        if (NetworkManager.Singleton == null)
        {
            return true;
        }

        WorldInteractionService service = WorldInteractionService.Instance;
        if (service == null)
        {
            return ShouldPreserveLocalInputWithoutResolvedAssignment();
        }

        if (!service.TryGetAssignedCharacterId(NetworkManager.Singleton.LocalClientId, out string characterId))
        {
            return ShouldPreserveLocalInputWithoutResolvedAssignment();
        }

        string localId = ResolveCharacterId();
        if (string.IsNullOrWhiteSpace(localId))
        {
            return ShouldPreserveLocalInputWithoutResolvedAssignment();
        }

        return string.Equals(characterId, localId, System.StringComparison.Ordinal);
    }

    private bool ShouldPreserveLocalInputWithoutResolvedAssignment()
    {
        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot != null)
        {
            return IsSameOrRelatedTransform(transform, localRoot);
        }

        return IsOwner;
    }

    private static bool IsSameOrRelatedTransform(Transform current, Transform candidate)
    {
        if (current == null || candidate == null)
        {
            return false;
        }

        return current == candidate || current.IsChildOf(candidate) || candidate.IsChildOf(current);
    }

    private string ResolveCharacterId()
    {
        NetcodeCharacterIdentity identity = GetComponent<NetcodeCharacterIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.CharacterId))
        {
            return identity.CharacterId;
        }

        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        CharacterData data = controller != null ? controller.CharacterData : null;
        if (data == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(data.UniqueId))
        {
            return data.UniqueId;
        }

        if (!string.IsNullOrWhiteSpace(data.characterId))
        {
            return data.characterId;
        }

        if (!string.IsNullOrWhiteSpace(data.characterName))
        {
            return data.characterName;
        }

        return data.name;
    }
}
