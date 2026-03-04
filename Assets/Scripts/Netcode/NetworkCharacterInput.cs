using Unity.Netcode;
using UnityEngine;

// Transmet les inputs locaux au serveur pour un personnage possede.
[RequireComponent(typeof(NetworkObject))]
public class NetworkCharacterInput : NetworkBehaviour
{
    [SerializeField] private SquadCharacterController controller;
    [SerializeField, Tooltip("Intervalle d'envoi des inputs (s).")]
    private float sendInterval = 0.05f;

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
        pendingMove = Vector2.zero;
        lastSentMove = Vector2.zero;
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            return;
        }

        if (IsGameplayInputBlocked())
        {
            if (lastSentMove != Vector2.zero)
            {
                SubmitMove(Vector2.zero);
            }

            return;
        }

        if (Time.time < nextSendTime && (pendingMove - lastSentMove).sqrMagnitude < 0.0001f)
        {
            return;
        }

        SubmitMove(pendingMove);
    }

    private void OnMoveChanged(Vector2 value)
    {
        if (!IsOwner || !IsAssignedToLocalClient() || IsGameplayInputBlocked())
        {
            pendingMove = Vector2.zero;
            return;
        }

        pendingMove = value;
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
        return InputFocusStack.HasAnyFocus();
    }

    private void SubmitMove(Vector2 value)
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            return;
        }

        lastSentMove = value;
        nextSendTime = Time.time + Mathf.Max(0.01f, sendInterval);
        SubmitMoveServerRpc(value);
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

        controller.Move(input);
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
            return !NetworkManager.Singleton.IsHost;
        }

        if (!service.TryGetAssignedCharacterId(NetworkManager.Singleton.LocalClientId, out string characterId))
        {
            return !NetworkManager.Singleton.IsHost;
        }

        string localId = ResolveCharacterId();
        if (string.IsNullOrWhiteSpace(localId))
        {
            return false;
        }

        return string.Equals(characterId, localId, System.StringComparison.Ordinal);
    }

    private string ResolveCharacterId()
    {
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
