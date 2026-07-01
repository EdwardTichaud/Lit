using System.Collections.Generic;
using UnityEngine;

// Role: ScriptableObject de donnees pour personnages joueurs et ennemis.
// Usage: reference par les prefabs de personnages, la squad, le combat, les voice lines et l'inventaire de depart.
// Responsibilities: stocker identite, stats, competences, inventaire runtime et definitions de combat.
// Dependencies: Item, Skill, CharacterStats, CombatEnemyDefinition, VoiceLineData.
// Precautions: plusieurs champs publics sont serialises dans des assets; ne pas les renommer sans migration Unity.
/// <summary>
/// Donnees centrales d'un personnage jouable ou ennemi.
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "Scriptable Objects/CharacterData")]
public class CharacterData : ScriptableObject
{
    /// <summary>
    /// Stack d'item donne au personnage au demarrage.
    /// </summary>
    [System.Serializable]
    public class StarterItemStack
    {
        /// <summary>
        /// Item donne au personnage.
        /// </summary>
        [Tooltip("Item de depart.")]
        public Item item;
        /// <summary>
        /// Quantite initiale. Pour la flamme, cette valeur represente des secondes.
        /// </summary>
        [Min(1)]
        [Tooltip("Quantite initiale (pour la flamme: secondes).")]
        public int quantity = 1;
    }

    [SerializeField, HideInInspector] private string uniqueId;
    /// <summary>Identifiant gameplay optionnel du personnage.</summary>
    public string characterId;
    /// <summary>Nom affiche dans l'UI.</summary>
    public string characterName;
    /// <summary>Portrait utilise par les interfaces.</summary>
    public Sprite portrait;
    /// <summary>Modele visuel principal du personnage.</summary>
    public GameObject model;
    /// <summary>Prefab monde a utiliser si different du modele.</summary>
    public GameObject worldPrefab;
    /// <summary>Competences possedees par le personnage.</summary>
    public List<Skill> skills;
    /// <summary>Statistiques de type JDR utilisees par les checks.</summary>
    public CharacterStats stats = new CharacterStats();
    /// <summary>Items donnes au debut ou lors de l'initialisation d'inventaire.</summary>
    public List<StarterItemStack> starterItemsWithQuantity = new List<StarterItemStack>();
    /// <summary>Points de vie maximum par defaut.</summary>
    public int hp = 10;

    [Header("Combat")]
    /// <summary>Indique si cette donnee represente un ennemi.</summary>
    [Tooltip("Indique que ce CharacterData represente un ennemi.")]
    public bool isEnemy;
    /// <summary>PV de depart en combat. 0 utilise les PV max.</summary>
    [Min(0)]
    [Tooltip("PV au debut du combat. 0 utilise les PV max resolus.")]
    public int combatCurrentHp;
    /// <summary>Degats bruts infliges par cet ennemi pendant son tour.</summary>
    [Min(0)]
    [Tooltip("Degats bruts infliges pendant le tour ennemi.")]
    public int attackDamage = 4;
    /// <summary>Attaques nommees disponibles pour cet ennemi.</summary>
    [Tooltip("Attaques nommees disponibles pour cet ennemi. Vide conserve l'attaque de base.")]
    public List<CombatEnemyAttackDefinition> combatAttacks = new List<CombatEnemyAttackDefinition>();
    /// <summary>Ennemis supplementaires ajoutes a une session de combat.</summary>
    [Tooltip("Ennemis additionnels ajoutes a la meme session solo.")]
    public List<CombatEnemyDefinition> additionalEnemies = new List<CombatEnemyDefinition>();

    [Header("Voice Lines")]
    /// <summary>Voice lines disponibles pour ce personnage.</summary>
    public List<VoiceLineData> voiceLines = new List<VoiceLineData>();

    [Header("Maison")]
    /// <summary>Index de point d'attente dans la maison.</summary>
    public int maisonWaitingPoint = 0;

    [Header("Inventory (Runtime)")]
    /// <summary>Items runtime portes par le personnage.</summary>
    public List<Item> inventoryItems = new List<Item>();
    /// <summary>Items equipes pour les interactions de monde.</summary>
    public List<Item> equippedInteractionItems = new List<Item>();
    /// <summary>Temps restant de flamme en secondes.</summary>
    public int flameSecondsRemaining;
    /// <summary>Indique si la flamme est equipee.</summary>
    public bool flameEquipped;
    /// <summary>Indique si l'inventaire runtime a deja ete initialise.</summary>
    public bool inventoryInitialized;

    [Header("Munin (Runtime)")]
    /// <summary>Charges runtime restantes de Munin.</summary>
    public int muninChargesRemaining;
    /// <summary>Maximum runtime de charges de Munin.</summary>
    public int muninMaxCharges = 10;
    /// <summary>Indique si un etat runtime de charges Munin a ete applique.</summary>
    public bool muninChargesInitialized;

    /// <summary>
    /// Identifiant stable derive du GUID d'asset quand disponible.
    /// </summary>
    public string UniqueId => uniqueId;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Dans l'editeur, on garde un id stable base sur le GUID Unity de l'asset.
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (!string.IsNullOrEmpty(path))
        {
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid) && uniqueId != guid)
            {
                uniqueId = guid;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            uniqueId = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    /// <summary>
    /// Vue lecture seule de l'inventaire runtime, avec creation de liste si besoin.
    /// </summary>
    public IReadOnlyList<Item> InventoryItems
    {
        get
        {
            if (inventoryItems == null)
            {
                inventoryItems = new List<Item>();
            }

            return inventoryItems;
        }
    }

    /// <summary>
    /// Retourne le prefab monde, ou le modele si aucun prefab specifique n'est configure.
    /// </summary>
    public GameObject ResolveWorldPrefab()
    {
        return worldPrefab != null ? worldPrefab : model;
    }

    /// <summary>
    /// Retourne le nom affiche, avec fallback sur le nom d'asset.
    /// </summary>
    public string ResolveDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(characterName))
        {
            return characterName;
        }

        return !string.IsNullOrWhiteSpace(name) ? name : "Ennemi";
    }

    /// <summary>
    /// Retourne les PV max avec fallback pour les ennemis incomplets.
    /// </summary>
    public int ResolveMaxHp()
    {
        return hp > 0 ? hp : 8;
    }

    /// <summary>
    /// Retourne les PV de depart de combat bornes par les PV max.
    /// </summary>
    public int ResolveCurrentHp(int resolvedMaxHp)
    {
        return Mathf.Clamp(combatCurrentHp > 0 ? combatCurrentHp : resolvedMaxHp, 0, Mathf.Max(1, resolvedMaxHp));
    }

    /// <summary>
    /// Cree la definition de combat de l'ennemi principal.
    /// </summary>
    public CombatEnemyDefinition CreatePrimaryCombatDefinition(CombatHealth healthOverride = null)
    {
        int resolvedMaxHp = healthOverride != null ? Mathf.Max(1, healthOverride.MaxHp) : ResolveMaxHp();
        int resolvedCurrentHp = healthOverride != null && healthOverride.CurrentHp > 0
            ? Mathf.Clamp(healthOverride.CurrentHp, 0, resolvedMaxHp)
            : ResolveCurrentHp(resolvedMaxHp);

        CombatEnemyDefinition definition = new CombatEnemyDefinition(ResolveDisplayName(), resolvedMaxHp, resolvedCurrentHp, attackDamage)
        {
            attacks = CombatEnemyDefinition.CreateRuntimeAttackCopies(combatAttacks, attackDamage)
        };

        return definition;
    }

    /// <summary>
    /// Cree toutes les definitions de combat associees a ce personnage.
    /// </summary>
    public List<CombatEnemyDefinition> CreateCombatDefinitions(CombatHealth healthOverride = null)
    {
        List<CombatEnemyDefinition> result = new List<CombatEnemyDefinition>
        {
            CreatePrimaryCombatDefinition(healthOverride)
        };

        if (additionalEnemies == null)
        {
            return result;
        }

        int total = additionalEnemies.Count + 1;
        for (int i = 0; i < additionalEnemies.Count; i++)
        {
            CombatEnemyDefinition enemy = additionalEnemies[i];
            if (enemy != null)
            {
                result.Add(enemy.CreateRuntimeCopy(result.Count, total));
            }
        }

        return result;
    }

    /// <summary>
    /// Remplace l'inventaire runtime sans liste d'items equipes.
    /// </summary>
    public void SetInventory(List<Item> items, int flameSeconds, bool equipped, bool markInitialized = true)
    {
        SetInventory(items, flameSeconds, equipped, markInitialized, null);
    }

    /// <summary>
    /// Remplace l'inventaire runtime et les items d'interaction equipes.
    /// </summary>
    public void SetInventory(List<Item> items, int flameSeconds, bool equipped, bool markInitialized, List<Item> equippedItems)
    {
        if (inventoryItems == null)
        {
            inventoryItems = new List<Item>();
        }
        else
        {
            inventoryItems.Clear();
        }

        if (items != null && items.Count > 0)
        {
            inventoryItems.AddRange(items);
        }

        flameSecondsRemaining = Mathf.Max(0, flameSeconds);
        flameEquipped = equipped;
        if (equippedInteractionItems == null)
        {
            equippedInteractionItems = new List<Item>();
        }
        else
        {
            equippedInteractionItems.Clear();
        }

        if (equippedItems != null)
        {
            for (int i = 0; i < equippedItems.Count; i++)
            {
                Item item = equippedItems[i];
                if (item == null || equippedInteractionItems.Contains(item))
                {
                    continue;
                }

                equippedInteractionItems.Add(item);
            }
        }

        if (markInitialized)
        {
            inventoryInitialized = true;
        }
    }

    /// <summary>
    /// Indique si ce personnage possede une competence donnee.
    /// </summary>
    public bool HasSkill(Skill skill)
    {
        if (skill == null || skills == null)
        {
            return false;
        }

        return skills.Contains(skill);
    }

    /// <summary>
    /// Ajoute une competence si elle n'est pas deja presente.
    /// </summary>
    public void AddSkill(Skill skill)
    {
        if (skill == null)
        {
            return;
        }

        if (skills == null)
        {
            skills = new List<Skill>();
        }

        if (!skills.Contains(skill))
        {
            skills.Add(skill);
        }
    }

    /// <summary>
    /// Retire une competence de la liste.
    /// </summary>
    public void RemoveSkill(Skill skill)
    {
        if (skill == null || skills == null)
        {
            return;
        }

        skills.Remove(skill);
    }

    /// <summary>
    /// Remplace toute la liste de competences en supprimant les doublons et nulls.
    /// </summary>
    public void SetSkills(List<Skill> newSkills)
    {
        if (skills == null)
        {
            skills = new List<Skill>();
        }
        else
        {
            skills.Clear();
        }

        if (newSkills == null)
        {
            return;
        }

        for (int i = 0; i < newSkills.Count; i++)
        {
            Skill skill = newSkills[i];
            if (skill != null && !skills.Contains(skill))
            {
                skills.Add(skill);
            }
        }
    }

    /// <summary>
    /// Effectue un test de competence et retourne le detail du jet.
    /// </summary>
    public bool TryCheckSkill(Skill skill, out int roll, out int modifier, out int total)
    {
        roll = 0;
        modifier = 0;
        total = 0;

        if (skill == null)
        {
            return false;
        }

        if (!HasSkill(skill))
        {
            return false;
        }

        if (!skill.requiresRoll)
        {
            roll = 0;
            modifier = 0;
            total = skill.difficultyClass;
            return true;
        }

        int statValue = GetStatValue(skill.linkedStat);
        modifier = GetStatModifier(statValue);
        // Random.Range(int,int) exclut la borne max, donc 21 produit un d20.
        roll = Random.Range(1, 21);
        total = roll + modifier;
        return total >= skill.difficultyClass;
    }

    /// <summary>
    /// Retourne la valeur brute d'une statistique.
    /// </summary>
    public int GetStatValue(StatType stat)
    {
        if (stats == null)
        {
            return 10;
        }

        switch (stat)
        {
            case StatType.Strength:
                return stats.strength;
            case StatType.Dexterity:
                return stats.dexterity;
            case StatType.Constitution:
                return stats.constitution;
            case StatType.Intelligence:
                return stats.intelligence;
            case StatType.Wisdom:
                return stats.wisdom;
            case StatType.Charisma:
                return stats.charisma;
            default:
                return 10;
        }
    }

    /// <summary>
    /// Calcule le modificateur de stat au format JDR: (valeur - 10) / 2 arrondi vers le bas.
    /// </summary>
    public static int GetStatModifier(int statValue)
    {
        return Mathf.FloorToInt((statValue - 10) / 2f);
    }
}

/// <summary>
/// Bloc de statistiques de base associe a un personnage.
/// </summary>
[System.Serializable]
public class CharacterStats
{
    /// <summary>Force physique.</summary>
    public int strength = 10;
    /// <summary>Adresse et agilite.</summary>
    public int dexterity = 10;
    /// <summary>Endurance et resistance.</summary>
    public int constitution = 10;
    /// <summary>Raisonnement et savoir.</summary>
    public int intelligence = 10;
    /// <summary>Perception et intuition.</summary>
    public int wisdom = 10;
    /// <summary>Presence sociale.</summary>
    public int charisma = 10;
}
