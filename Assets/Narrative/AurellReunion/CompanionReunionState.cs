using UnityEngine;

/// <summary>Session facts shared by the house presentation and the first companion cycle.</summary>
public static class CompanionReunionState
{
    public const string CycleId = "district1.aurell_reunion";
    public const string ArrivalStep = "arrival";
    public const string ReunionStep = "reunion_completed";
    public static bool HasDeparted
    {
        get
        {
            var service = CycleProgressionService.Instance;
            var rules = service != null ? service.GetComponent<WorldRulesStateManager>() : Object.FindAnyObjectByType<WorldRulesStateManager>();
            return rules != null && rules.TryGetInt("narrative." + CycleId + ".step." + ArrivalStep, out int value) && value != 0;
        }
    }

    public static bool ShouldBePresent(CombatSkillKit member) => ShouldBePresent(SoloTeamProgression.IsSolo, HasDeparted,
        SoloTeamProgression.Resolve()?.IsRecruited(member) == true);
    public static bool ShouldBePresent(bool solo, bool departed, bool recruited) => !solo || !departed || recruited;
}
