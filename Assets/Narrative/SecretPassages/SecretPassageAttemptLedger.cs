using System;
using Unity.Netcode;

/// <summary>Trusted host rule. Uses the existing world snapshot, not a new save file.</summary>
public static class SecretPassageAttemptLedger
{
    public enum Result { Unavailable, AlreadyKnown, Failed, Succeeded }
    private const int Failed = 1, Succeeded = 2;

    public static string AttemptKey(string passageId, string characterId)
    {
        if (string.IsNullOrWhiteSpace(passageId) || string.IsNullOrWhiteSpace(characterId)) return null;
        // A raw separator cannot occur inside either escaped identity.
        return "world.secret_passage.observer." + Uri.EscapeDataString(passageId) + "|" + Uri.EscapeDataString(characterId);
    }

    public static bool HasAttempted(WorldRulesStateManager world, string passageId, string characterId)
    {
        string key = AttemptKey(passageId, characterId);
        return world != null && key != null && world.TryGetInt(key, out _);
    }

    public static Result RecordedResult(WorldRulesStateManager world, string passageId, string characterId)
    {
        string key = AttemptKey(passageId, characterId);
        if (world == null || key == null || !world.TryGetInt(key, out int value)) return Result.Unavailable;
        return value == Failed ? Result.Failed : value == Succeeded ? Result.Succeeded : Result.Unavailable;
    }

    /// <summary>Caller must validate proximity, equipped ability and server-owned character identity.
    /// Cached success allows retrying knowledge delivery without another roll. Never exposes the die.</summary>
    public static Result Observe(WorldRulesStateManager world, string passageId, string characterId,
        bool hasObserver, bool passageKnown, Func<int> rollD6)
    {
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening && !network.IsServer) return Result.Unavailable;
        if (passageKnown) return Result.AlreadyKnown;
        string key = AttemptKey(passageId, characterId);
        if (world == null || key == null || !hasObserver) return Result.Unavailable;
        if (world.TryGetInt(key, out int previous))
            return previous == Succeeded ? Result.Succeeded : previous == Failed ? Result.Failed : Result.Unavailable;
        if (rollD6 == null) return Result.Unavailable;
        int roll = rollD6();
        if (roll < 1 || roll > 6) throw new ArgumentOutOfRangeException(nameof(rollD6), "Expected a d6 result.");
        bool success = roll >= 2;
        world.SetInt(key, success ? Succeeded : Failed);
        return success ? Result.Succeeded : Result.Failed;
    }
}
