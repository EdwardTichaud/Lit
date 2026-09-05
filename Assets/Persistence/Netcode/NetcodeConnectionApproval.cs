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
        response.Approved = false;
        response.CreatePlayerObject = false;
        response.Pending = false;
        if (!NetcodeClientIdentity.TryGetPlayerId(request.Payload, out string playerId))
        { response.Reason = "Version du jeu incompatible ou identité invalide."; return; }
        if (NetcodePlayerSessionRegistry.ContainsPlayer(playerId))
        { response.Reason = "Ce joueur est déjà connecté."; return; }
        NetworkManager manager = NetworkManager.Singleton;
        int reserved = PrivateSessionService.Instance != null && PrivateSessionService.Instance.IsActive
            ? PrivateSessionService.Instance.Lobby.characterIds.Length : 4;
        int capacity = Mathf.Min(Mathf.Clamp(maxPlayers, 1, 4), reserved);
        // The registry includes approvals not yet reflected in ConnectedClientsIds.
        if (NetcodePlayerSessionRegistry.Count >= capacity)
        { response.Reason = "Partie complète : aucun personnage disponible."; return; }
        NetcodePlayerSessionRegistry.Register(request.ClientNetworkId, playerId);
        response.Approved = true;
        response.Reason = string.Empty;
    }
}
