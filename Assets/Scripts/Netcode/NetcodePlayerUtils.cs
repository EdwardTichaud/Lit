using Unity.Netcode;
using UnityEngine;

// Utilitaires pour resoudre les objets joueurs Netcode.
public static class NetcodePlayerUtils
{
    public static Transform GetPlayerTransform(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            return null;
        }

        if (NetworkManager.Singleton.ConnectedClients != null
            && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
            && client != null
            && client.PlayerObject != null)
        {
            return client.PlayerObject.transform;
        }

        return null;
    }
}
