using System;
using System.Collections.Generic;
using UnityEngine;

// Role: represente un ennemi vivant pendant une session de combat.
// Usage: cree depuis CombatEnemyDefinition par CombatSessionManager.
// Responsibilities: stocker PV, nom, degats et attaques, appliquer des degats sans reference Unity.
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
        : this(displayName, currentHp, maxHp, attackDamage, null)
    {
    }

    /// <summary>
    /// Cree un ennemi runtime avec valeurs nettoyees et attaques optionnelles.
    /// </summary>
    public CombatRuntimeEnemy(
        string displayName,
        int currentHp,
        int maxHp,
        int attackDamage,
        IList<CombatEnemyAttackDefinition> attacks)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Ennemi" : displayName;
        MaxHp = Mathf.Max(1, maxHp);
        CurrentHp = Mathf.Clamp(currentHp > 0 ? currentHp : MaxHp, 0, MaxHp);
        AttackDamage = Mathf.Max(0, attackDamage);
        Attacks = CombatEnemyDefinition.CreateRuntimeAttackCopies(attacks, AttackDamage);
    }

    /// <summary>Nom affiche dans le HUD de combat.</summary>
    public string DisplayName { get; }
    /// <summary>PV courants de cet ennemi.</summary>
    public int CurrentHp { get; private set; }
    /// <summary>PV maximum de cet ennemi.</summary>
    public int MaxHp { get; }
    /// <summary>Degats bruts infliges pendant le tour ennemi.</summary>
    public int AttackDamage { get; }
    /// <summary>Attaques configurees pour cet ennemi.</summary>
    public List<CombatEnemyAttackDefinition> Attacks { get; }
    /// <summary>Indique si l'ennemi peut encore agir ou etre cible.</summary>
    public bool IsAlive => CurrentHp > 0;

    private int nextAttackIndex;

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

    /// <summary>
    /// Reinitialise cet ennemi pour rejouer la session de combat courante.
    /// </summary>
    public void RestoreForRetry()
    {
        CurrentHp = MaxHp;
        nextAttackIndex = 0;
    }

    /// <summary>
    /// Selectionne la prochaine attaque configuree. Retourne null pour conserver l'attaque de base.
    /// </summary>
    public CombatEnemyAttackDefinition SelectNextAttack(out int attackIndex)
    {
        attackIndex = -1;
        if (Attacks == null || Attacks.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < Attacks.Count; i++)
        {
            int index = (nextAttackIndex + i) % Attacks.Count;
            CombatEnemyAttackDefinition attack = Attacks[index];
            if (attack == null)
            {
                continue;
            }

            nextAttackIndex = (index + 1) % Attacks.Count;
            attackIndex = index;
            return attack;
        }

        return null;
    }

    /// <summary>
    /// Retourne une attaque par index sans modifier l'ordre de selection.
    /// </summary>
    public CombatEnemyAttackDefinition GetAttack(int attackIndex)
    {
        if (Attacks == null || attackIndex < 0 || attackIndex >= Attacks.Count)
        {
            return null;
        }

        return Attacks[attackIndex];
    }
}
