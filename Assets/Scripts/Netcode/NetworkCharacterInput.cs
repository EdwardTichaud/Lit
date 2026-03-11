using Unity.Netcode;
using UnityEngine;

// Route les inputs locaux vers le personnage controle localement par son proprietaire.
[RequireComponent(typeof(NetworkObject))]
public class NetworkCharacterInput : NetworkBehaviour
{
    [SerializeField] private SquadCharacterController controller;

    private Vector2 pendingMove;

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
            pendingMove = Vector2.zero;
            if (controller == null)
            {
                controller = GetComponent<SquadCharacterController>();
            }

            controller?.Stop();
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
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        controller?.Stop();
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned || !IsAssignedToLocalClient())
        {
            return;
        }

        if (IsGameplayInputBlocked())
        {
            pendingMove = Vector2.zero;
            controller?.Move(Vector2.zero);
            return;
        }

        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        controller?.Move(pendingMove);
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
