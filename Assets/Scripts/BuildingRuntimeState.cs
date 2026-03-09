using System.Collections.Generic;
using UnityEngine;

// Etat runtime des niveaux de buildings (ne modifie jamais les ScriptableObjects Item).
public static class BuildingRuntimeState
{
    private static readonly Dictionary<string, int> levels = new Dictionary<string, int>();

    public static int GetLevel(Item building)
    {
        if (building == null)
        {
            return 0;
        }

        string id = ItemIdUtils.GetItemId(building);
        if (string.IsNullOrWhiteSpace(id))
        {
            return 0;
        }

        return levels.TryGetValue(id, out int level) ? Mathf.Max(0, level) : 0;
    }

    public static void SetLevel(Item building, int level, bool onlyIncrease = true)
    {
        if (building == null)
        {
            return;
        }

        string id = ItemIdUtils.GetItemId(building);
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        int clamped = Mathf.Max(0, level);
        int maxLevel = Mathf.Max(1, building.buildingMaxLevel);
        clamped = Mathf.Clamp(clamped, 0, maxLevel);

        if (onlyIncrease && levels.TryGetValue(id, out int current) && current >= clamped)
        {
            return;
        }

        levels[id] = clamped;
    }

    public static void SetLevelById(string id, int level, bool onlyIncrease = true)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        int clamped = Mathf.Max(0, level);
        if (onlyIncrease && levels.TryGetValue(id, out int current) && current >= clamped)
        {
            return;
        }

        levels[id] = clamped;
    }

    public static void ResetLevel(Item building)
    {
        SetLevel(building, 0, false);
    }

    public static void Clear()
    {
        levels.Clear();
    }
}
