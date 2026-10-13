using UnityEngine;

/// <summary>Explicit lab fixture; never applies to an active campaign save.</summary>
public sealed class SoloTeamTestProfile : MonoBehaviour
{
    [SerializeField] private SoloTeamSaveData profile;
    [SerializeField] private bool unlockAllCatalogSkills = true;
    private void Start()
    {
        if (SaveSessionManager.Instance != null && SaveSessionManager.Instance.HasActiveSave) return;
        Apply();
    }
    public void Apply()
    {
        if (SaveSessionManager.Instance != null && SaveSessionManager.Instance.HasActiveSave) return;
        var team = SoloTeamProgression.Resolve();
        if (team == null) return;
        if (!unlockAllCatalogSkills) { team.Restore(profile); return; }
        var all = new SoloTeamSaveData();
        foreach (CombatSkillKit kit in System.Enum.GetValues(typeof(CombatSkillKit)))
            all.members.Add(new SoloTeamMemberState { kit = kit, recruited = true, unlockedSlots = 15 });
        team.Restore(all);
    }
}
