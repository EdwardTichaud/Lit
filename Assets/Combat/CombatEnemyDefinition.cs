using System.Collections.Generic;
using UnityEngine;

// Role: configuration serialisable d'un ennemi de combat et de ses attaques.
// Usage: renseignee dans CombatAggroEnemy ou produite par CharacterData avant la session.
// Responsibilities: fournir nom, PV, degats et attaques, puis produire une copie runtime propre.
// Dependencies: Unity serialization, Mathf.
// Precautions: les champs publics sont serialises dans les scenes/prefabs; ne pas les renommer sans migration.
/// <summary>
/// Definition d'une attaque ennemie jouee pendant le tour de combat.
/// </summary>
[System.Serializable]
public class CombatEnemyAttackDefinition
{
    /// <summary>
    /// Nom affiche dans le journal/HUD de combat.
    /// </summary>
    [Tooltip("Nom affiche dans le journal/HUD de combat.")]
    public string displayName = "Attaque";
    /// <summary>
    /// Degats bruts de cette attaque.
    /// </summary>
    [Min(0)]
    [Tooltip("Degats bruts infliges par cette attaque.")]
    public int damage = 4;
    /// <summary>
    /// Etat Animator ou trigger a jouer. Vide utilise Attack_Base.
    /// </summary>
    [Tooltip("Nom d'etat Animator ou trigger a jouer. Vide utilise Attack_Base.")]
    public string animationName = "Attack_Base";
    /// <summary>
    /// Prefab VFX local instancie au moment de l'attaque.
    /// </summary>
    [Tooltip("Prefab VFX local instancie au moment de l'attaque.")]
    public GameObject vfxPrefab;
    /// <summary>
    /// Position locale du VFX par rapport a l'ennemi.
    /// </summary>
    [Tooltip("Position locale du VFX par rapport a l'ennemi.")]
    public Vector3 vfxLocalOffset = new Vector3(0f, 1f, 0.75f);
    /// <summary>
    /// Rotation locale du VFX par rapport a l'ennemi.
    /// </summary>
    [Tooltip("Rotation locale du VFX par rapport a l'ennemi.")]
    public Vector3 vfxLocalEulerAngles;
    /// <summary>
    /// Duree avant destruction du VFX. 0 laisse le prefab se gerer seul.
    /// </summary>
    [Min(0f)]
    [Tooltip("Duree avant destruction du VFX. 0 laisse le prefab se gerer seul.")]
    public float vfxLifetime = 2f;

    /// <summary>
    /// Constructeur vide requis par la serialisation Unity.
    /// </summary>
    public CombatEnemyAttackDefinition()
    {
    }

    /// <summary>
    /// Cree une attaque nettoyee depuis des valeurs de code.
    /// </summary>
    public CombatEnemyAttackDefinition(
        string displayName,
        int damage,
        string animationName,
        GameObject vfxPrefab,
        Vector3 vfxLocalOffset,
        Vector3 vfxLocalEulerAngles,
        float vfxLifetime)
    {
        this.displayName = string.IsNullOrWhiteSpace(displayName) ? "Attaque" : displayName;
        this.damage = Mathf.Max(0, damage);
        this.animationName = string.IsNullOrWhiteSpace(animationName) ? "Attack_Base" : animationName;
        this.vfxPrefab = vfxPrefab;
        this.vfxLocalOffset = vfxLocalOffset;
        this.vfxLocalEulerAngles = vfxLocalEulerAngles;
        this.vfxLifetime = Mathf.Max(0f, vfxLifetime);
    }

    /// <summary>
    /// Cree une copie runtime en appliquant un fallback de degats si besoin.
    /// </summary>
    public CombatEnemyAttackDefinition CreateRuntimeCopy(int fallbackDamage)
    {
        return new CombatEnemyAttackDefinition(
            displayName,
            damage > 0 ? damage : fallbackDamage,
            animationName,
            vfxPrefab,
            vfxLocalOffset,
            vfxLocalEulerAngles,
            vfxLifetime);
    }
}

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
    /// Attaques disponibles pour cet ennemi. Vide conserve l'attaque de base existante.
    /// </summary>
    [Tooltip("Attaques disponibles pour cet ennemi. Vide conserve l'attaque de base existante.")]
    public List<CombatEnemyAttackDefinition> attacks = new List<CombatEnemyAttackDefinition>();

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
        attacks = new List<CombatEnemyAttackDefinition>();
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
        CombatEnemyDefinition copy = new CombatEnemyDefinition(resolvedName, resolvedMaxHp, resolvedCurrentHp, attackDamage)
        {
            attacks = CreateRuntimeAttackCopies(attacks, attackDamage)
        };

        return copy;
    }

    /// <summary>
    /// Copie une liste d'attaques en ignorant les entrees vides.
    /// </summary>
    public static List<CombatEnemyAttackDefinition> CreateRuntimeAttackCopies(
        IList<CombatEnemyAttackDefinition> source,
        int fallbackDamage)
    {
        List<CombatEnemyAttackDefinition> result = new List<CombatEnemyAttackDefinition>();
        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            CombatEnemyAttackDefinition attack = source[i];
            if (attack == null)
            {
                continue;
            }

            result.Add(attack.CreateRuntimeCopy(fallbackDamage));
        }

        return result;
    }
}
