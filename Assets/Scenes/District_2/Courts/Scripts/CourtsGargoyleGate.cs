using UnityEngine;

/// <summary>Existing Brains combat, gated by the completed local introduction. Flame rules remain in Brains.</summary>
[DisallowMultipleComponent]
public sealed class CourtsGargoyleGate : MonoBehaviour, ILitBrainsEncounterPolicy
{
    public CycleDefinition cycle;
    public bool AllowsDecisions => CycleProgressionService.Instance != null &&
        CycleProgressionService.Instance.IsReady && CycleProgressionService.Instance.IsStepCompleted(cycle, "introduced");
    public bool AllowsDamage => AllowsDecisions;
    public bool IsReturning => false;
    public bool IsFlameDormant => false;
    public bool FilterIncomingDamage(int requested, SquadCharacterController source, out int permitted)
    {
        permitted = AllowsDamage ? requested : 0;
        return AllowsDamage;
    }
}
