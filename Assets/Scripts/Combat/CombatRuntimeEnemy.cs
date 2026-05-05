using System;
using UnityEngine;

[Serializable]
public sealed class CombatRuntimeEnemy
{
    public CombatRuntimeEnemy(string displayName, int currentHp, int maxHp, int attackDamage)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Ennemi" : displayName;
        MaxHp = Mathf.Max(1, maxHp);
        CurrentHp = Mathf.Clamp(currentHp > 0 ? currentHp : MaxHp, 0, MaxHp);
        AttackDamage = Mathf.Max(0, attackDamage);
    }

    public string DisplayName { get; }
    public int CurrentHp { get; private set; }
    public int MaxHp { get; }
    public int AttackDamage { get; }
    public bool IsAlive => CurrentHp > 0;

    public int ApplyDamage(int amount)
    {
        int sanitized = Mathf.Max(0, amount);
        if (sanitized <= 0 || CurrentHp <= 0)
        {
            return 0;
        }

        int before = CurrentHp;
        CurrentHp = Mathf.Max(0, CurrentHp - sanitized);
        return before - CurrentHp;
    }
}
