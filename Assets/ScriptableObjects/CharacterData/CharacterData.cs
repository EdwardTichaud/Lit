using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Scriptable Objects/CharacterData")]
public class CharacterData : ScriptableObject
{
    [System.Serializable]
    public class StarterItemStack
    {
        [Tooltip("Item de depart.")]
        public Item item;
        [Min(1)]
        [Tooltip("Quantite initiale (pour la torche: secondes).")]
        public int quantity = 1;
    }

    [SerializeField, HideInInspector] private string uniqueId;
    public string characterId;
    public string characterName;
    public Sprite portrait;
    public GameObject model;
    public GameObject worldPrefab;
    public List<Skill> skills;
    public CharacterStats stats = new CharacterStats();
    public List<StarterItemStack> starterItemsWithQuantity = new List<StarterItemStack>();
    public int hp = 10;

    [Header("Combat")]
    [Tooltip("Indique que ce CharacterData represente un ennemi.")]
    public bool isEnemy;
    [Min(0)]
    [Tooltip("PV au debut du combat. 0 utilise les PV max resolus.")]
    public int combatCurrentHp;
    [Min(0)]
    [Tooltip("Degats bruts infliges pendant le tour ennemi.")]
    public int attackDamage = 4;
    [Tooltip("Ennemis additionnels ajoutes a la meme session solo.")]
    public List<CombatEnemyDefinition> additionalEnemies = new List<CombatEnemyDefinition>();

    [Header("Voice Lines")]
    public List<VoiceLineData> voiceLines = new List<VoiceLineData>();

    [Header("Maison")]
    public int maisonWaitingPoint = 0;

    [Header("Inventory (Runtime)")]
    public List<Item> inventoryItems = new List<Item>();
    public List<Item> equippedInteractionItems = new List<Item>();
    public int torchSecondsRemaining;
    public bool torchEquipped;
    public bool inventoryInitialized;

    public string UniqueId => uniqueId;

#if UNITY_EDITOR
    private void OnValidate()
    {
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

    public GameObject ResolveWorldPrefab()
    {
        return worldPrefab != null ? worldPrefab : model;
    }

    public string ResolveDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(characterName))
        {
            return characterName;
        }

        return !string.IsNullOrWhiteSpace(name) ? name : "Ennemi";
    }

    public int ResolveMaxHp()
    {
        return hp > 0 ? hp : 8;
    }

    public int ResolveCurrentHp(int resolvedMaxHp)
    {
        return Mathf.Clamp(combatCurrentHp > 0 ? combatCurrentHp : resolvedMaxHp, 0, Mathf.Max(1, resolvedMaxHp));
    }

    public CombatEnemyDefinition CreatePrimaryCombatDefinition(CombatHealth healthOverride = null)
    {
        int resolvedMaxHp = healthOverride != null ? Mathf.Max(1, healthOverride.MaxHp) : ResolveMaxHp();
        int resolvedCurrentHp = healthOverride != null && healthOverride.CurrentHp > 0
            ? Mathf.Clamp(healthOverride.CurrentHp, 0, resolvedMaxHp)
            : ResolveCurrentHp(resolvedMaxHp);

        return new CombatEnemyDefinition(ResolveDisplayName(), resolvedMaxHp, resolvedCurrentHp, attackDamage);
    }

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

    public void SetInventory(List<Item> items, int torchSeconds, bool equipped, bool markInitialized = true)
    {
        SetInventory(items, torchSeconds, equipped, markInitialized, null);
    }

    public void SetInventory(List<Item> items, int torchSeconds, bool equipped, bool markInitialized, List<Item> equippedItems)
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

        torchSecondsRemaining = Mathf.Max(0, torchSeconds);
        torchEquipped = equipped;
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

    public bool HasSkill(Skill skill)
    {
        if (skill == null || skills == null)
        {
            return false;
        }

        return skills.Contains(skill);
    }

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

    public void RemoveSkill(Skill skill)
    {
        if (skill == null || skills == null)
        {
            return;
        }

        skills.Remove(skill);
    }

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
        roll = Random.Range(1, 21);
        total = roll + modifier;
        return total >= skill.difficultyClass;
    }

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

    public static int GetStatModifier(int statValue)
    {
        return Mathf.FloorToInt((statValue - 10) / 2f);
    }
}

[System.Serializable]
public class CharacterStats
{
    public int strength = 10;
    public int dexterity = 10;
    public int constitution = 10;
    public int intelligence = 10;
    public int wisdom = 10;
    public int charisma = 10;
}
