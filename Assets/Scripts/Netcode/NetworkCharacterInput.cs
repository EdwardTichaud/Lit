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
            SubmitMove(Vector2.zero);
        }
    }

    private void RegisterInput()
    {
        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Move += OnMoveChanged;
        LocalInputRouter.ToggleTorch += OnToggleTorch;
    }

    private void UnregisterInput()
    {
        LocalInputRouter.Move -= OnMoveChanged;
        LocalInputRouter.ToggleTorch -= OnToggleTorch;
        rawMoveInput = Vector2.zero;
        pendingMove = Vector2.zero;
        lastSentMove = Vector2.zero;
        UpdateLocalAnimationPreview(Vector2.zero);
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            rawMoveInput = Vector2.zero;
            pendingMove = Vector2.zero;
            UpdateLocalAnimationPreview(Vector2.zero);
            return;
        }

        if (IsGameplayInputBlocked())
        {
            rawMoveInput = Vector2.zero;
            pendingMove = Vector2.zero;
            UpdateLocalAnimationPreview(Vector2.zero);
            if (lastSentMove != Vector2.zero)
            {
                SubmitMove(Vector2.zero);
            }

            return;
        }

        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        rawMoveInput = LocalInputRouter.MoveValue;
        pendingMove = controller != null
            ? controller.GetWorldSpaceInput(rawMoveInput)
            : rawMoveInput;
        UpdateLocalAnimationPreview(pendingMove);

        if (Time.time < nextSendTime && (pendingMove - lastSentMove).sqrMagnitude < 0.0001f)
        {
            return;
        }

        SubmitMove(pendingMove);
    }

    private void OnMoveChanged(Vector2 value)
    {
        rawMoveInput = value;
        if (!IsOwner || !IsAssignedToLocalClient() || IsGameplayInputBlocked())
        {
            pendingMove = Vector2.zero;
            UpdateLocalAnimationPreview(Vector2.zero);
            return;
        }

        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        pendingMove = controller != null
            ? controller.GetWorldSpaceInput(value)
            : value;
        UpdateLocalAnimationPreview(pendingMove);
    }

    private void OnToggleTorch(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            return;
        }

        if (IsGameplayInputBlocked())
        {
            return;
        }

        ToggleTorchServerRpc();
    }

    private static bool IsGameplayInputBlocked()
    {
        return InputFocusStack.HasAnyFocus() ||
               JoinSyncSystem.IsGameplayBlocked ||
               (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked());
    }

    private void SubmitMove(Vector2 value)
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            return;
        }

        lastSentMove = value;
        nextSendTime = Time.time + Mathf.Max(0.01f, sendInterval);

        if (ShouldUseHostLocalMovePath())
        {
            ApplyHostLocalMove(rawMoveInput);
            return;
        }

        SubmitMoveServerRpc(value);
    }

    private bool ShouldUseHostLocalMovePath()
    {
        return IsServer && IsOwner && IsSpawned && IsAssignedToLocalClient();
    }

    private void ApplyHostLocalMove(Vector2 input)
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        if (input.sqrMagnitude < 0.0001f)
        {
            controller.Stop();
            return;
        }

        controller.Move(input);
    }

    [ServerRpc]
    private void SubmitMoveServerRpc(Vector2 input)
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        controller.MoveWorld(input);
    }

    [ServerRpc]
    private void ToggleTorchServerRpc()
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        controller.ToggleTorch();
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
            return true;
        }

        if (!service.TryGetAssignedCharacterId(NetworkManager.Singleton.LocalClientId, out string characterId))
        {
            return false;
        }

        string localId = ResolveCharacterId();
        if (string.IsNullOrWhiteSpace(localId))
        {
            return false;
        }

        return string.Equals(characterId, localId, System.StringComparison.Ordinal);
    }

    private void UpdateLocalAnimationPreview(Vector2 worldInput)
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        if (worldInput == Vector2.zero)
        {
            controller.ClearLocalAnimationPreview();
            return;
        }

        controller.SetLocalAnimationPreview(worldInput);
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
