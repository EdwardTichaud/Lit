// Role:
// Shared vocabulary and requirements for the Knowledge-driven narrative layer.
// Usage:
// KnowledgeSO assets use categories/tags; ghosts, readables, triggers, and future
// dialogue components use KnowledgeRequirement to check what the player knows.
// Responsibilities:
// Keep knowledge checks simple, data-driven, and compatible with KnowledgeManager persistence.
// Dependencies:
// KnowledgeSO and KnowledgeManager.
// Precautions:
// Do not store runtime state here. Persistent knowledge ownership remains in KnowledgeManager.
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main narrative category of a knowledge entry.
/// </summary>
public enum KnowledgeCategory
{
    Unknown = 0,
    Inhabitant = 1,
    Lineage = 2,
    District = 3,
    Movement = 4,
    Registry = 5,
    Object = 6,
    Ritual = 7,
    Vigil = 8,
    Architecture = 9,
    Anomaly = 10,
    Temporality = 11,
    ReligiousCurrent = 12,
    Flame = 13,
    Room = 14,
    Disappearance = 15,
    Truth = 16,
    Statue = 17,
    Singer = 18
}

/// <summary>
/// Primary way a knowledge can be discovered.
/// </summary>
public enum KnowledgeSourceType
{
    Unknown = 0,
    Readable = 1,
    Registry = 2,
    Object = 3,
    Room = 4,
    Place = 5,
    TemporalObservation = 6,
    Ghost = 7,
    Contradiction = 8,
    LineageConnection = 9,
    Manual = 10
}

/// <summary>
/// Narrative weight of a knowledge entry.
/// </summary>
public enum KnowledgeImportance
{
    Minor = 0,
    Useful = 1,
    Important = 2,
    Major = 3
}

/// <summary>
/// Requires the player to own several entries in the same category.
/// Useful for implicit understanding, for example "knows enough about this district".
/// </summary>
[Serializable]
public class KnowledgeCategoryCountRequirement
{
    public KnowledgeCategory category = KnowledgeCategory.Unknown;
    [Min(1)]
    public int minimumCount = 1;
}

/// <summary>
/// Requires the player to own several entries carrying the same tag.
/// Tags keep combined knowledge simple without creating a parallel deduction system.
/// </summary>
[Serializable]
public class KnowledgeTagCountRequirement
{
    public string tag;
    [Min(1)]
    public int minimumCount = 1;
}

/// <summary>
/// Data-driven condition that checks whether the player owns enough knowledge.
/// Empty requirements are considered satisfied.
/// </summary>
[Serializable]
public class KnowledgeRequirement
{
    [Tooltip("Toutes ces connaissances doivent etre possedees.")]
    public List<KnowledgeSO> requiredKnowledge = new List<KnowledgeSO>();
    [Tooltip("Au moins une de ces connaissances doit etre possedee si la liste n'est pas vide.")]
    public List<KnowledgeSO> anyKnowledge = new List<KnowledgeSO>();
    [Tooltip("Le joueur doit posseder au moins une connaissance dans chaque categorie listee.")]
    public List<KnowledgeCategory> requiredCategories = new List<KnowledgeCategory>();
    [Tooltip("Le joueur doit posseder au moins une connaissance portant chaque tag liste.")]
    public List<string> requiredTags = new List<string>();
    [Tooltip("Le joueur doit posseder au moins N connaissances dans chaque categorie configuree.")]
    public List<KnowledgeCategoryCountRequirement> requiredCategoryCounts = new List<KnowledgeCategoryCountRequirement>();
    [Tooltip("Le joueur doit posseder au moins N connaissances portant chaque tag configure.")]
    public List<KnowledgeTagCountRequirement> requiredTagCounts = new List<KnowledgeTagCountRequirement>();

    public bool IsSatisfied()
    {
        return IsSatisfied(KnowledgeManager.Instance);
    }

    public bool IsSatisfied(KnowledgeManager manager)
    {
        manager = manager != null ? manager : KnowledgeManager.Instance;
        if (manager == null)
        {
            return IsEmpty();
        }

        if (!HasAllRequiredKnowledge(manager))
        {
            return false;
        }

        if (!HasAnyRequiredKnowledge(manager))
        {
            return false;
        }

        if (!HasRequiredCategories(manager))
        {
            return false;
        }

        if (!HasRequiredTags(manager))
        {
            return false;
        }

        if (!HasRequiredCategoryCounts(manager))
        {
            return false;
        }

        return HasRequiredTagCounts(manager);
    }

    public bool IsEmpty()
    {
        return IsNullOrEmpty(requiredKnowledge)
            && IsNullOrEmpty(anyKnowledge)
            && IsNullOrEmpty(requiredCategories)
            && IsNullOrEmpty(requiredTags)
            && IsNullOrEmpty(requiredCategoryCounts)
            && IsNullOrEmpty(requiredTagCounts);
    }

    public int GetSpecificityScore()
    {
        return CountNonNull(requiredKnowledge)
            + CountNonNull(anyKnowledge)
            + CountMeaningfulCategories(requiredCategories)
            + CountNonEmptyStrings(requiredTags)
            + CountCategoryCountRequirements(requiredCategoryCounts)
            + CountTagCountRequirements(requiredTagCounts);
    }

    private bool HasAllRequiredKnowledge(KnowledgeManager manager)
    {
        if (requiredKnowledge == null)
        {
            return true;
        }

        for (int i = 0; i < requiredKnowledge.Count; i++)
        {
            KnowledgeSO knowledge = requiredKnowledge[i];
            if (knowledge != null && !manager.HasKnowledge(knowledge))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasAnyRequiredKnowledge(KnowledgeManager manager)
    {
        if (anyKnowledge == null || anyKnowledge.Count == 0)
        {
            return true;
        }

        bool hasConfiguredKnowledge = false;
        for (int i = 0; i < anyKnowledge.Count; i++)
        {
            KnowledgeSO knowledge = anyKnowledge[i];
            if (knowledge == null)
            {
                continue;
            }

            hasConfiguredKnowledge = true;
            if (manager.HasKnowledge(knowledge))
            {
                return true;
            }
        }

        return !hasConfiguredKnowledge;
    }

    private bool HasRequiredCategories(KnowledgeManager manager)
    {
        if (requiredCategories == null)
        {
            return true;
        }

        for (int i = 0; i < requiredCategories.Count; i++)
        {
            KnowledgeCategory category = requiredCategories[i];
            if (category != KnowledgeCategory.Unknown && !manager.HasKnowledgeInCategory(category))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasRequiredTags(KnowledgeManager manager)
    {
        if (requiredTags == null)
        {
            return true;
        }

        for (int i = 0; i < requiredTags.Count; i++)
        {
            string tag = requiredTags[i];
            if (!string.IsNullOrWhiteSpace(tag) && !manager.HasKnowledgeWithTag(tag))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasRequiredCategoryCounts(KnowledgeManager manager)
    {
        if (requiredCategoryCounts == null)
        {
            return true;
        }

        for (int i = 0; i < requiredCategoryCounts.Count; i++)
        {
            KnowledgeCategoryCountRequirement requirement = requiredCategoryCounts[i];
            if (requirement == null || requirement.category == KnowledgeCategory.Unknown)
            {
                continue;
            }

            int minimum = Mathf.Max(1, requirement.minimumCount);
            if (manager.CountKnowledgeInCategory(requirement.category) < minimum)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasRequiredTagCounts(KnowledgeManager manager)
    {
        if (requiredTagCounts == null)
        {
            return true;
        }

        for (int i = 0; i < requiredTagCounts.Count; i++)
        {
            KnowledgeTagCountRequirement requirement = requiredTagCounts[i];
            if (requirement == null || string.IsNullOrWhiteSpace(requirement.tag))
            {
                continue;
            }

            int minimum = Mathf.Max(1, requirement.minimumCount);
            if (manager.CountKnowledgeWithTag(requirement.tag) < minimum)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNullOrEmpty<T>(List<T> list)
    {
        return list == null || list.Count == 0;
    }

    private static int CountNonNull(List<KnowledgeSO> list)
    {
        if (list == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountMeaningfulCategories(List<KnowledgeCategory> categories)
    {
        if (categories == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < categories.Count; i++)
        {
            if (categories[i] != KnowledgeCategory.Unknown)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountNonEmptyStrings(List<string> values)
    {
        if (values == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountCategoryCountRequirements(List<KnowledgeCategoryCountRequirement> requirements)
    {
        if (requirements == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < requirements.Count; i++)
        {
            KnowledgeCategoryCountRequirement requirement = requirements[i];
            if (requirement != null && requirement.category != KnowledgeCategory.Unknown)
            {
                count += Mathf.Max(1, requirement.minimumCount);
            }
        }

        return count;
    }

    private static int CountTagCountRequirements(List<KnowledgeTagCountRequirement> requirements)
    {
        if (requirements == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < requirements.Count; i++)
        {
            KnowledgeTagCountRequirement requirement = requirements[i];
            if (requirement != null && !string.IsNullOrWhiteSpace(requirement.tag))
            {
                count += Mathf.Max(1, requirement.minimumCount);
            }
        }

        return count;
    }
}
