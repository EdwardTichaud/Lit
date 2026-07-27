using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CombatAttackLibrary", menuName = "Lit/Combat Real Time/Attack Library")]
public sealed class CombatAttackLibrary : ScriptableObject
{
    [SerializeField] private List<CombatAttackDefinition> attacks = new List<CombatAttackDefinition>();

    public IReadOnlyList<CombatAttackDefinition> Attacks => attacks;

    public CombatAttackDefinition GetAttack(int index)
    {
        return index >= 0 && index < attacks.Count ? attacks[index] : null;
    }
}
