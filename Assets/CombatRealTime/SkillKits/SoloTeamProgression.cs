using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public sealed class SoloTeamSaveData
{
    public List<SoloTeamMemberState> members = new List<SoloTeamMemberState>();
}

[Serializable]
public sealed class SoloTeamMemberState
{
    public CombatSkillKit kit;
    public bool recruited;
    public int unlockedSlots;
}

/// <summary>Session-owned recruitment; catalog assets remain authoring definitions.</summary>
[DisallowMultipleComponent]
public sealed class SoloTeamProgression : MonoBehaviour
{
    private readonly Dictionary<CombatSkillKit, SoloTeamMemberState> members = new();
    public event Action Changed;
    public event Action<CombatSkillKit> MemberRecruited;
    private CombatSkillKitCatalog catalog;
    public CombatSkillKitCatalog Catalog => catalog != null ? catalog : catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
    public static bool IsSolo => (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) &&
        (PrivateSessionService.Instance == null || !PrivateSessionService.Instance.IsActive) &&
        (SaveSessionManager.Instance == null || !SaveSessionManager.Instance.HasActiveSave ||
            SaveSessionManager.Instance.CurrentSessionType != SaveSessionType.Multiplayer);

    public static SoloTeamProgression Resolve()
    {
        if (!IsSolo) return null;
        // The persistent squad owns campaign state. Standalone combat labs own
        // their own state instead, with no static state leaking between plays.
        var host = SquadManager.Instance != null ? SquadManager.Instance.gameObject :
            RealTimeCombatManager.Instance != null ? RealTimeCombatManager.Instance.gameObject : null;
        if (host == null) return null;
        return host.GetComponent<SoloTeamProgression>() ?? host.AddComponent<SoloTeamProgression>();
    }

    private void Awake() => InitializeNewGame();

    public void InitializeNewGame() => ResetState(true);

    private void ResetState(bool notify)
    {
        members.Clear();
        foreach (CombatSkillKit kit in Enum.GetValues(typeof(CombatSkillKit)))
            members[kit] = new SoloTeamMemberState { kit = kit, recruited = kit == CombatSkillKit.Lucian,
                unlockedSlots = kit == CombatSkillKit.Lucian ? 7 : 0 };
        if (notify) Changed?.Invoke();
    }

    public bool IsRecruited(CombatSkillKit kit) => members.TryGetValue(kit, out var state) && state.recruited;
    public bool IsUnlocked(CombatSkillKit kit, int slot) => slot >= 0 && slot < 4 && IsRecruited(kit) &&
        (members[kit].unlockedSlots & (1 << slot)) != 0 && Catalog != null && Catalog.GetSkill(kit, slot) != null;

    public bool RecruitMember(CombatSkillKit kit)
    {
        if (!IsSolo || !members.TryGetValue(kit, out var state) || state.recruited) return false;
        state.recruited = true;
        state.unlockedSlots |= 1;
        Changed?.Invoke();
        MemberRecruited?.Invoke(kit);
        var skill = Catalog != null ? Catalog.GetSkill(kit, 0) : null;
        string direction = kit switch { CombatSkillKit.Vesta => "droite", CombatSkillKit.Soren => "bas",
            CombatSkillKit.Aurell => "gauche", _ => "haut" };
        SoloTeamAnnouncement.Show($"{kit} rejoint l’équipe\n{skill?.SkillName ?? "Compétence"} — croix {direction}, puis gauche");
        return true;
    }

    public bool UnlockSkill(CombatSkillKit kit, int slot)
    {
        if (!IsSolo || !IsRecruited(kit) || slot < 0 || slot > 3 || Catalog == null ||
            Catalog.GetSkill(kit, slot) == null || IsUnlocked(kit, slot)) return false;
        members[kit].unlockedSlots |= 1 << slot;
        Changed?.Invoke();
        return true;
    }

    public SkillSO[] GetAvailableSkills(CombatSkillKit kit)
    {
        var result = new SkillSO[4];
        for (int i = 0; i < 4; i++) if (IsUnlocked(kit, i)) result[i] = Catalog.GetSkill(kit, i);
        return result;
    }

    public static bool AllowsSkill(SkillSO skill)
    {
        if (!IsSolo || skill == null || skill is BasicSkillsSO) return true;
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        if (catalog == null || !catalog.TryFind(skill, out var kit, out int slot)) return true;
        return Resolve()?.IsUnlocked(kit, slot) == true;
    }

    public SoloTeamSaveData Capture()
    {
        var result = new SoloTeamSaveData();
        foreach (var state in members.Values) result.members.Add(new SoloTeamMemberState {
            kit = state.kit, recruited = state.recruited, unlockedSlots = state.unlockedSlots });
        return result;
    }

    public void Restore(SoloTeamSaveData saved, List<string> legacyRoster = null)
    {
        ResetState(false);
        if (saved?.members != null)
        {
            foreach (var entry in saved.members)
                if (entry != null && entry.kit != CombatSkillKit.Lucian && members.TryGetValue(entry.kit, out var state))
                { state.recruited = entry.recruited; state.unlockedSlots = entry.unlockedSlots & 15; }
            foreach (var entry in saved.members)
                if (entry != null && entry.kit == CombatSkillKit.Lucian) members[entry.kit].unlockedSlots |= entry.unlockedSlots & 15;
        }
        else if (legacyRoster != null && Catalog != null)
        {
            foreach (var pair in members)
            {
                var character = Catalog.GetCharacter(pair.Key);
                if (character == null || !legacyRoster.Contains(character.characterId) &&
                    (string.IsNullOrWhiteSpace(character.UniqueId) || !legacyRoster.Contains(character.UniqueId))) continue;
                pair.Value.recruited = true;
                pair.Value.unlockedSlots |= 1;
            }
        }
        Changed?.Invoke();
    }

    public List<CharacterData> GetRoster()
    {
        var result = new List<CharacterData>();
        if (Catalog == null) return result;
        foreach (CombatSkillKit kit in Enum.GetValues(typeof(CombatSkillKit)))
            if (IsRecruited(kit) && Catalog.GetCharacter(kit) != null) result.Add(Catalog.GetCharacter(kit));
        return result;
    }
}
