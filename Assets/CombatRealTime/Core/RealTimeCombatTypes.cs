using System;
using UnityEngine;

public enum RealTimeCombatReaction
{
    None,
    Counter,
    Dodge,
    Jump
}

public enum RealTimeCombatRange
{
    Melee,
    Ranged
}

public enum CombatClarityRank
{
    F,
    E,
    D,
    C,
    B,
    A,
    S
}

public enum LightSkillClarityTier
{
    E,
    D,
    C,
    B,
    A,
    S
}

[Serializable]
public struct CombatKnowledgeModifier
{
    [Tooltip("Bonus fixe ajoute a chaque gain de Clarite.")]
    public float clarityBonus;
    [Min(0f), Tooltip("Multiplicateur applique aux degats de lumiere.")]
    public float lightDamageMultiplier;
    [Min(0f), Tooltip("Multiplicateur applique aux degats renvoyes par un contre.")]
    public float counterDamageMultiplier;

    public static CombatKnowledgeModifier Identity => new CombatKnowledgeModifier
    {
        lightDamageMultiplier = 1f,
        counterDamageMultiplier = 1f
    };
}

public readonly struct RealTimeCombatReactionWindow
{
    public readonly Transform Enemy;
    public readonly SkillSO Skill;
    public readonly int IncomingDamage;
    public readonly bool IsOpen;

    public RealTimeCombatReactionWindow(Transform enemy, SkillSO skill, int incomingDamage, bool isOpen)
    {
        Enemy = enemy;
        Skill = skill;
        IncomingDamage = incomingDamage;
        IsOpen = isOpen;
    }
}
