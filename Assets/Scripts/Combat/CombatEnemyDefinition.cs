using UnityEngine;

[System.Serializable]
public class CombatEnemyDefinition
{
    [Tooltip("Nom affiche dans le HUD de combat.")]
    public string displayName = "Ennemi";
    [Min(1)]
    [Tooltip("PV max de cet ennemi dans une session de combat.")]
    public int maxHp = 8;
    [Min(0)]
    [Tooltip("PV courants au debut du combat. 0 utilise les PV max.")]
    public int currentHp;
    [Min(0)]
    [Tooltip("Degats bruts infliges par cet ennemi pendant son tour.")]
    public int attackDamage = 4;

    public CombatEnemyDefinition()
    {
    }

    public CombatEnemyDefinition(string displayName, int maxHp, int currentHp, int attackDamage)
    {
        this.displayName = string.IsNullOrWhiteSpace(displayName) ? "Ennemi" : displayName;
        this.maxHp = Mathf.Max(1, maxHp);
        this.currentHp = Mathf.Clamp(currentHp > 0 ? currentHp : this.maxHp, 0, this.maxHp);
        this.attackDamage = Mathf.Max(0, attackDamage);
    }

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
