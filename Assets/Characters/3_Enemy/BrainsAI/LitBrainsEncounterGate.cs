using UnityEngine;

/// <summary>Explicit gate for an encounter adapter; starts without invented boss or ghost behaviour.</summary>
[DisallowMultipleComponent]
public sealed class LitBrainsEncounterGate : MonoBehaviour, ILitBrainsEncounterPolicy
{
    public bool decisionsEnabled = true;
    public bool damageEnabled = true;
    public bool returning;
    public bool flameDormant;
    public bool AllowsDecisions => decisionsEnabled && !returning;
    public bool AllowsDamage => damageEnabled;
    public bool IsReturning => returning;
    public bool IsFlameDormant => flameDormant;
    public bool FilterIncomingDamage(int requested, SquadCharacterController source, out int permitted)
    {
        permitted = AllowsDamage && !flameDormant ? Mathf.Max(0, requested) : 0;
        return AllowsDamage && !flameDormant;
    }
}
