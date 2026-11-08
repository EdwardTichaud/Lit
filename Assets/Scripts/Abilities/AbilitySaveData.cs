using System;
using System.Collections.Generic;

[Serializable]
public sealed class AbilitySlot
{
    public string playerId;
    public string abilityId;
}

/// <summary>Session unlocks and multiple equipment entries per human identity. Legacy entries remain readable.</summary>
[Serializable]
public sealed class AbilitySaveData
{
    public int equipmentVersion;
    public List<string> unlockedIds = new List<string>();
    public List<AbilitySlot> slots = new List<AbilitySlot>();

    public bool HasEquipped(string playerId, string id) => !string.IsNullOrWhiteSpace(playerId) &&
        IsUnlocked(id) && slots.Exists(s => s != null && s.playerId == playerId && s.abilityId == id);

    public bool AddEquipped(string playerId, string id)
    {
        if (string.IsNullOrWhiteSpace(playerId) || !IsUnlocked(id) || HasEquipped(playerId, id)) return false;
        slots.Add(new AbilitySlot { playerId = playerId, abilityId = id });
        return true;
    }

    public bool Toggle(string playerId, string id)
    {
        if (string.IsNullOrWhiteSpace(playerId) || !IsUnlocked(id)) return false;
        if (HasEquipped(playerId, id)) slots.RemoveAll(s => s != null && s.playerId == playerId && s.abilityId == id);
        else AddEquipped(playerId, id);
        return true;
    }

    /// <summary>Every unlocked capability has exactly one owner; extra capabilities are spread round-robin.</summary>
    public void Distribute(IReadOnlyList<string> players)
    {
        if (players == null || players.Count == 0) return;
        slots.RemoveAll(s => s != null && s.playerId != AbilityService.SoloPlayerId);
        for (int i = 0; i < unlockedIds.Count; i++) AddEquipped(players[i % players.Count], unlockedIds[i]);
    }

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
