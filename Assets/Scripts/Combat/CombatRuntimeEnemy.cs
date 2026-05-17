using System;
using UnityEngine;

// Role: represente un ennemi vivant pendant une session de combat.
// Usage: cree depuis CombatEnemyDefinition par CombatSessionManager.
// Responsibilities: stocker PV, nom et degats, appliquer des degats sans reference Unity.
// Dependencies: Mathf uniquement; ne depend pas d'un GameObject.
// Precautions: garder cette classe simple pour pouvoir la serialiser ou la tester facilement.
/// <summary>
/// Donnee runtime d'un ennemi dans une session de combat.
/// </summary>
[Serializable]
public sealed class CombatRuntimeEnemy
{
    /// <summary>
    /// Cree un ennemi runtime avec valeurs nettoyees.
    /// </summary>
    public CombatRuntimeEnemy(string displayName, int currentHp, int maxHp, int attackDamage)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Ennemi" : displayName;
        MaxHp = Mathf.Max(1, maxHp);
        CurrentHp = Mathf.Clamp(currentHp > 0 ? currentHp : MaxHp, 0, MaxHp);
        AttackDamage = Mathf.Max(0, attackDamage);
    }

    /// <summary>Nom affiche dans le HUD de combat.</summary>
    public string DisplayName { get; }
    /// <summary>PV courants de cet ennemi.</summary>
    public int CurrentHp { get; private set; }
    /// <summary>PV maximum de cet ennemi.</summary>
    public int MaxHp { get; }
    /// <summary>Degats bruts infliges pendant le tour ennemi.</summary>
    public int AttackDamage { get; }
    /// <summary>Indique si l'ennemi peut encore agir ou etre cible.</summary>
    public bool IsAlive => CurrentHp > 0;

    /// <summary>
    /// Applique des degats et retourne le montant reellement retire.
    /// </summary>
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
