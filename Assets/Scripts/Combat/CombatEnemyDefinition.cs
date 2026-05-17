using UnityEngine;

// Role: configuration serialisable d'un ennemi de combat.
// Usage: renseignee dans CombatAggroEnemy ou produite par CharacterData avant la session.
// Responsibilities: fournir nom, PV et degats, puis produire une copie runtime propre.
// Dependencies: Unity serialization, Mathf.
// Precautions: les champs publics sont serialises dans les scenes/prefabs; ne pas les renommer sans migration.
/// <summary>
/// Definition d'un ennemi avant entree dans une session de combat.
/// </summary>
[System.Serializable]
public class CombatEnemyDefinition
{
    /// <summary>
    /// Nom affiche dans le HUD de combat.
    /// </summary>
    [Tooltip("Nom affiche dans le HUD de combat.")]
    public string displayName = "Ennemi";
    /// <summary>
    /// PV maximum de cet ennemi.
    /// </summary>
    [Min(1)]
    [Tooltip("PV max de cet ennemi dans une session de combat.")]
    public int maxHp = 8;
    /// <summary>
    /// PV de depart. La valeur 0 signifie "utiliser les PV max".
    /// </summary>
    [Min(0)]
    [Tooltip("PV courants au debut du combat. 0 utilise les PV max.")]
    public int currentHp;
    /// <summary>
    /// Degats infliges par l'ennemi pendant son tour.
    /// </summary>
    [Min(0)]
    [Tooltip("Degats bruts infliges par cet ennemi pendant son tour.")]
    public int attackDamage = 4;

    /// <summary>
    /// Constructeur vide requis par la serialisation Unity.
    /// </summary>
    public CombatEnemyDefinition()
    {
    }

    /// <summary>
    /// Cree une definition nettoyee depuis des valeurs de code.
    /// </summary>
    public CombatEnemyDefinition(string displayName, int maxHp, int currentHp, int attackDamage)
    {
        this.displayName = string.IsNullOrWhiteSpace(displayName) ? "Ennemi" : displayName;
        this.maxHp = Mathf.Max(1, maxHp);
        this.currentHp = Mathf.Clamp(currentHp > 0 ? currentHp : this.maxHp, 0, this.maxHp);
        this.attackDamage = Mathf.Max(0, attackDamage);
    }

    /// <summary>
    /// Cree une copie prete pour le combat, en suffixant le nom si plusieurs ennemis partagent la session.
    /// </summary>
    public CombatEnemyDefinition CreateRuntimeCopy(int index, int total)
    {
        string resolvedName = string.IsNullOrWhiteSpace(displayName) ? "Ennemi" : displayName;
        if (total > 1)
        {
            resolvedName = $"{resolvedName} {index + 1}";
        }

        int resolvedMaxHp = Mathf.Max(1, maxHp);
        int resolvedCurrentHp = Mathf.Clamp(currentHp > 0 ? currentHp : resolvedMaxHp, 0, resolvedMaxHp);
        return new CombatEnemyDefinition(resolvedName, resolvedMaxHp, resolvedCurrentHp, attackDamage);
    }
}
