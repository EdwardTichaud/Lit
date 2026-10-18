#if UNITY_INCLUDE_TESTS
using UnityEngine;

[AddComponentMenu("")]
public sealed class LitBrainsSourcePolicyProbe : MonoBehaviour, ILitBrainsEncounterPolicy
{
    public SquadCharacterController receivedSource;
    public bool AllowsDecisions => true;
    public bool AllowsDamage => true;
    public bool IsReturning => false;
    public bool IsFlameDormant => false;
    public bool FilterIncomingDamage(int requested, SquadCharacterController source, out int permitted)
    {
        receivedSource = source;
        permitted = requested;
        return true;
    }
}
#endif
