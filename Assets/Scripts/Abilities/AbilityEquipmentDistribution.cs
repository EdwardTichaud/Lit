using System;
using System.Collections.Generic;

/// <summary>Validates restored multiplayer loadouts before redistributing; preserves Maison exchanges.</summary>
public static class AbilityEquipmentDistribution
{
    public static bool IsCompleteDistribution(this AbilitySaveData state, IReadOnlyList<string> players)
    {
        if (players == null || players.Count == 0) return false;
        var seen = new HashSet<string>();
        var counts = new Dictionary<string, int>();
        foreach (string player in players) counts[player] = 0;
        foreach (var slot in state.slots)
        {
            if (slot == null || slot.playerId == AbilityService.SoloPlayerId) continue;
            if (!counts.ContainsKey(slot.playerId) || !state.IsUnlocked(slot.abilityId) || !seen.Add(slot.abilityId)) return false;
            counts[slot.playerId]++;
        }
        if (seen.Count != state.unlockedIds.Count) return false;
        int min = int.MaxValue, max = 0;
        foreach (int count in counts.Values) { min = Math.Min(min, count); max = Math.Max(max, count); }
        return max - min <= 1;
    }
}
