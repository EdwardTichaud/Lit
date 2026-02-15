using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Scriptable Objects/CharacterData")]
public class CharacterData : ScriptableObject
{
    [SerializeField, HideInInspector] private string uniqueId;
    public string characterId;
    public string characterName;
    public Sprite portrait;
    public GameObject model;
    public List<Skill> skills;
    public CharacterStats stats = new CharacterStats();
    public List<Item> starterItems;
    public int hp = 10;

    [Header("Voice Lines")]
    public List<VoiceLineData> voiceLines = new List<VoiceLineData>();

    [Header("Maison")]
    public int maisonWaitingPoint = 0;

    [Header("Inventory (Runtime)")]
    public List<Item> inventoryItems = new List<Item>();
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

    public void SetInventory(List<Item> items, int torchSeconds, bool equipped, bool markInitialized = true)
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
