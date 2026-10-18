using Unity.Netcode;

/// <summary>Local singleton controllers are not authoritative network actor executors.</summary>
public static class CombatLocalCinematicPolicy
{
    public const string NetworkExecutionUnavailable = "Network cinematic/QTE execution requires actor-owned transport and playback; local host input is not authority.";
    public static bool AllowsLocalExecution
    {
        get
        {
            var network = NetworkManager.Singleton;
            return network == null || !network.IsListening && !network.ShutdownInProgress;
        }
    }
}
