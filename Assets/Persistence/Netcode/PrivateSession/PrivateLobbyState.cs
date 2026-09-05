using System;
using System.Collections.Generic;
using System.Linq;

public enum PrivateSessionPhase { Idle, Preparing, Connecting, Lobby, Loading, Playing, Returning, Failed }
public enum PrivateSessionError { None, Cancelled, Timeout, CodeExpired, Unavailable, Full, Incompatible, DuplicateIdentity, Storage, Scene }

[Serializable]
public sealed class PrivateLobbyMember
{
    public ulong clientId;
    public string characterId;
    public bool ready;
}

// Server-owned decisions, independent of scene objects and transport timing.
[Serializable]
public sealed class PrivateLobbyState
{
    public int revision;
    public PrivateSessionPhase phase;
    public string sessionName;
    public string saveName;
    public string[] characterIds = Array.Empty<string>();
    public string[] characterNames = Array.Empty<string>();
    public List<PrivateLobbyMember> members = new List<PrivateLobbyMember>();

    public bool CanStart => phase == PrivateSessionPhase.Lobby && members.Count > 0 &&
        members.All(m => m.ready && characterIds.Contains(m.characterId)) &&
        members.Select(m => m.characterId).Distinct().Count() == members.Count;

    public bool Add(ulong clientId)
    {
        if (members.Any(m => m.clientId == clientId)) return true;
        string free = characterIds.FirstOrDefault(id => !members.Any(m => m.characterId == id));
        if (members.Count >= 4 || free == null) return false;
        members.Add(new PrivateLobbyMember { clientId = clientId, characterId = free });
        InvalidateReady();
        return true;
    }

    public bool Reserve(ulong clientId, string characterId)
    {
        if (phase != PrivateSessionPhase.Lobby || !characterIds.Contains(characterId)) return false;
        PrivateLobbyMember member = members.Find(m => m.clientId == clientId);
        if (member == null || members.Any(m => m.clientId != clientId && m.characterId == characterId)) return false;
        if (member.characterId == characterId) return true;
        member.characterId = characterId;
        InvalidateReady();
        return true;
    }

    public void InvalidateReady()
    {
        foreach (PrivateLobbyMember member in members) member.ready = false;
        revision++;
    }
}
