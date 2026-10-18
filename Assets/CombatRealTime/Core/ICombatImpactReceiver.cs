using UnityEngine;

public enum CombatImpactOrigin { Basic, LightSkill, Counter, Passive }

public readonly struct CombatImpact
{
    public readonly int Damage;
    public readonly float Interruption;
    public readonly CombatImpactOrigin Origin;
    public readonly bool DamageModifiersApplied;
    public readonly SquadCharacterController Source;
    public CombatImpact(int damage, float interruption, CombatImpactOrigin origin, bool damageModifiersApplied = false,
        SquadCharacterController source = null)
    {
        Damage = Mathf.Max(0, damage);
        Interruption = Mathf.Max(0f, interruption);
        Origin = origin;
        DamageModifiersApplied = damageModifiersApplied;
        Source = source;
    }
}

/// <summary>Optional impact contract. Legacy enemies keep ReceiveDamage(int).</summary>
public interface ICombatImpactReceiver
{
    int ReceiveImpact(CombatImpact impact);
}

/// <summary>Optional vulnerability state, independent of damage reception.</summary>
public interface ICombatKnockoutReceiver
{
    void KnockedOut();
}
