using Unity.Netcode;
using UnityEngine;

// Connection approval Netcode : lit l'identite client pour attribuer un personnage persistant.
public class NetcodeConnectionApproval : MonoBehaviour
{
    [SerializeField] private bool enableApproval = true;
    [SerializeField] private int maxPlayers = 4;

    private void Awake()
    {
        RegisterCallback();
    }

    private void OnDestroy()
    {
        UnregisterCallback();
    }

    private void RegisterCallback()
    {
        if (!enableApproval)
        {
            return;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            return;
        }

        manager.NetworkConfig.ConnectionApproval = true;
        manager.ConnectionApprovalCallback = OnConnectionApproval;
    }

    private void UnregisterCallback()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            return;
        }

        if (manager.ConnectionApprovalCallback == OnConnectionApproval)
        {
            manager.ConnectionApprovalCallback = null;
        }
    }

    private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string playerId;
        if (!NetcodeClientIdentity.TryGetPlayerId(request.Payload, out playerId))
        {
            playerId = $"client-{request.ClientNetworkId}";
        }

        playerId = ResolveRuntimePlayerId(playerId, request.ClientNetworkId);
        NetcodePlayerSessionRegistry.Register(request.ClientNetworkId, playerId);

        int currentConnections = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClientsIds.Count
            : 0;

        response.Approved = maxPlayers <= 0 || currentConnections < maxPlayers;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = response.Approved ? string.Empty : "Serveur plein.";
    }

    private static string ResolveRuntimePlayerId(string playerId, ulong clientId)
    {
        string resolved = string.IsNullOrWhiteSpace(playerId)
            ? $"client-{clientId}"
            : playerId.Trim();

        if (!IsPlayerIdInUse(resolved, clientId))
        {
            return resolved;
        }

        int suffix = 2;
        string candidate = $"{resolved}#{suffix}";
        while (IsPlayerIdInUse(candidate, clientId))
        {
            suffix++;
            candidate = $"{resolved}#{suffix}";
        }

        return candidate;
    }

    private static bool IsPlayerIdInUse(string playerId, ulong clientId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.ConnectedClientsIds == null)
        {
            return false;
        }

        for (int i = 0; i < manager.ConnectedClientsIds.Count; i++)
        {
            ulong otherClientId = manager.ConnectedClientsIds[i];
            if (otherClientId == clientId)
            {
                continue;
            }

            if (NetcodePlayerSessionRegistry.TryGetPlayerId(otherClientId, out string otherPlayerId)
                && string.Equals(otherPlayerId, playerId, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
