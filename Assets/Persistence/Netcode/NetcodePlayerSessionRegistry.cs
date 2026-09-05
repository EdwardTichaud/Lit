using System.Collections.Generic;

// Registre runtime clientId -> playerId pour retrouver l'identite en server.
public static class NetcodePlayerSessionRegistry
{
    private static readonly Dictionary<ulong, string> clientToPlayer = new Dictionary<ulong, string>();

    public static int Count => clientToPlayer.Count;

    public static bool ContainsPlayer(string playerId)
    {
        return clientToPlayer.ContainsValue(playerId);
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime() => Clear();

    public static void Register(ulong clientId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        clientToPlayer[clientId] = playerId;
    }

    public static bool TryGetPlayerId(ulong clientId, out string playerId)
    {
        return clientToPlayer.TryGetValue(clientId, out playerId);
    }

    public static void Unregister(ulong clientId)
    {
        clientToPlayer.Remove(clientId);
    }

    public static void Clear()
    {
        clientToPlayer.Clear();
    }
}
