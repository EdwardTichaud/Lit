using System;
using System.Collections.Generic;

[Serializable]
public sealed class AbilitySlot
{
    public string playerId;
    public string abilityId;
}

/// <summary>Session unlocks, but exactly one slot per human identity, not per squad character.</summary>
[Serializable]
public sealed class AbilitySaveData
{
    public List<string> unlockedIds = new List<string>();
    public List<AbilitySlot> slots = new List<AbilitySlot>();

    public bool IsUnlocked(string id) => !string.IsNullOrWhiteSpace(id) && unlockedIds.Contains(id);
    public bool Unlock(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || IsUnlocked(id)) return false;
        unlockedIds.Add(id);
        return true;
    }
    public string Equipped(string playerId)
    {
        var slot = slots.Find(value => value != null && value.playerId == playerId);
        return slot != null ? slot.abilityId : string.Empty;
    }
    public bool Equip(string playerId, string id)
    {
        if (string.IsNullOrWhiteSpace(playerId) || !IsUnlocked(id)) return false;
        var slot = slots.Find(value => value != null && value.playerId == playerId);
        if (slot == null) { slot = new AbilitySlot { playerId = playerId }; slots.Add(slot); }
        if (slot.abilityId == id) return false;
        slot.abilityId = id;
        return true;
    }
}
