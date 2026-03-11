using Unity.Netcode;
using UnityEngine;

// Validation serveur partagee pour les RPC d'interaction.
public static class NetcodeServerRpcValidation
{
    public readonly struct PlayerContext
    {
        public PlayerContext(ulong clientId, Transform playerRoot, SquadCharacterController controller, NetworkInventory inventory)
        {
            ClientId = clientId;
            PlayerRoot = playerRoot;
            Controller = controller;
            Inventory = inventory;
        }

        public ulong ClientId { get; }
        public Transform PlayerRoot { get; }
        public SquadCharacterController Controller { get; }
        public NetworkInventory Inventory { get; }
        public GameObject PlayerObject => PlayerRoot != null ? PlayerRoot.gameObject : null;
    }

    public static bool TryResolvePlayerContext(
        Component source,
        ServerRpcParams rpcParams,
        out PlayerContext context,
        out string reason,
        bool requireController = true,
        bool requireInventory = false)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(clientId);
        if (playerRoot == null)
        {
            reason = "Aucun personnage reseau assigne.";
            context = default;
            LogRejection(source, clientId, reason);
            return false;
        }

        SquadCharacterController controller = null;
        if (requireController)
        {
            controller = playerRoot.GetComponent<SquadCharacterController>();
            if (controller == null)
            {
                controller = playerRoot.GetComponentInChildren<SquadCharacterController>(true);
            }

            if (controller == null)
            {
                reason = "Controleur de personnage introuvable.";
                context = default;
                LogRejection(source, clientId, reason);
                return false;
            }
        }

        NetworkInventory inventory = null;
        if (requireInventory)
        {
            inventory = playerRoot.GetComponent<NetworkInventory>();
            if (inventory == null)
            {
                inventory = playerRoot.GetComponentInChildren<NetworkInventory>(true);
            }

            if (inventory == null)
            {
                reason = "Inventaire reseau introuvable.";
                context = default;
                LogRejection(source, clientId, reason);
                return false;
            }

            if (inventory.OwnerClientId != clientId)
            {
                reason = "Inventaire non possede par ce client.";
                context = default;
                LogRejection(source, clientId, reason, $"owner={inventory.OwnerClientId}");
                return false;
            }
        }

        context = new PlayerContext(clientId, playerRoot, controller, inventory);
        reason = string.Empty;
        return true;
    }

    public static bool TryResolveOwnedInventoryContext(
        NetworkInventory inventory,
        ServerRpcParams rpcParams,
        out PlayerContext context,
        out string reason)
    {
        if (inventory == null)
        {
            reason = "Inventaire reseau indisponible.";
            context = default;
            LogRejection(null, rpcParams.Receive.SenderClientId, reason);
            return false;
        }

        if (!TryResolvePlayerContext(inventory, rpcParams, out context, out reason, requireController: true, requireInventory: false))
        {
            return false;
        }

        if (inventory.OwnerClientId != context.ClientId)
        {
            reason = "Inventaire non possede par ce client.";
            context = default;
            LogRejection(inventory, rpcParams.Receive.SenderClientId, reason, $"owner={inventory.OwnerClientId}");
            return false;
        }

        context = new PlayerContext(context.ClientId, context.PlayerRoot, context.Controller, inventory);
        return true;
    }

    public static bool TryValidateRange(
        Component source,
        PlayerContext context,
        Vector3 targetPosition,
        float maxDistance,
        string actionLabel,
        out string reason)
    {
        if (context.PlayerRoot == null)
        {
            reason = "Aucun personnage reseau assigne.";
            LogRejection(source, context.ClientId, reason);
            return false;
        }

        float resolvedDistance = Mathf.Max(0.05f, maxDistance);
        if ((context.PlayerRoot.position - targetPosition).sqrMagnitude <= resolvedDistance * resolvedDistance)
        {
            reason = string.Empty;
            return true;
        }

        reason = BuildOutOfRangeReason(actionLabel);
        LogRejection(source, context.ClientId, reason, $"maxDistance={resolvedDistance:0.###}");
        return false;
    }

    public static bool TryValidateRange(
        Component source,
        PlayerContext context,
        Collider targetCollider,
        float maxDistance,
        string actionLabel,
        out string reason)
    {
        if (context.PlayerRoot == null)
        {
            reason = "Aucun personnage reseau assigne.";
            LogRejection(source, context.ClientId, reason);
            return false;
        }

        if (targetCollider == null)
        {
            reason = string.Empty;
            return true;
        }

        float resolvedDistance = Mathf.Max(0.05f, maxDistance);
        Vector3 closest = targetCollider.ClosestPoint(context.PlayerRoot.position);
        if ((closest - context.PlayerRoot.position).sqrMagnitude <= resolvedDistance * resolvedDistance)
        {
            reason = string.Empty;
            return true;
        }

        reason = BuildOutOfRangeReason(actionLabel);
        LogRejection(source, context.ClientId, reason, $"maxDistance={resolvedDistance:0.###}");
        return false;
    }

    public static ClientRpcParams BuildClientRpcParams(ServerRpcParams rpcParams)
    {
        return BuildClientRpcParams(rpcParams.Receive.SenderClientId);
    }

    public static ClientRpcParams BuildClientRpcParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };
    }

    public static void LogRejection(Component source, ulong clientId, string reason, string details = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        string sourceName = source != null
            ? $"{source.GetType().Name}:{source.name}"
            : "unknown";
        if (string.IsNullOrWhiteSpace(details))
        {
            Debug.LogWarning($"NetcodeServerRpcValidation: RPC refusee pour client {clientId} sur {sourceName}. {reason}");
            return;
        }

        Debug.LogWarning($"NetcodeServerRpcValidation: RPC refusee pour client {clientId} sur {sourceName}. {reason} ({details})");
    }

    private static string BuildOutOfRangeReason(string actionLabel)
    {
        if (string.IsNullOrWhiteSpace(actionLabel))
        {
            return "Trop loin.";
        }

        return $"Trop loin pour {actionLabel.Trim()}.";
    }
}
