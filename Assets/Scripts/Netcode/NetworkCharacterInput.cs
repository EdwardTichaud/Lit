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
        if (!IsOwner)
        {
            return;
        }

        LocalInputRouter.EnsureInitialized();
        LocalInputRouter.Move += OnMoveChanged;
        LocalInputRouter.ToggleTorch += OnToggleTorch;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            return;
        }

        LocalInputRouter.Move -= OnMoveChanged;
        LocalInputRouter.ToggleTorch -= OnToggleTorch;
    }

    private void OnDisable()
    {
        if (IsOwner)
        {
            SubmitMove(Vector2.zero);
        }
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
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
        if (IsGameplayInputBlocked())
        {
            pendingMove = Vector2.zero;
            return;
        }

        pendingMove = value;
    }

    private void OnToggleTorch(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned)
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
        if (!IsOwner || !IsSpawned)
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
}
