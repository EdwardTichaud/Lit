// Role:
// ScriptableObject data for knowledge entries unlocked by exploration.
// Usage:
// Referenced by KnowledgeManager and knowledge/readable systems.
// Responsibilities:
// Store stable identity, display title, description, and lightweight narrative links.
// Dependencies:
// UnityEditor in OnValidate for asset GUID synchronization.
// Precautions:
// uniqueId/knowledgeId may be used for persistence. Do not regenerate existing IDs casually.
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data asset for one unlockable knowledge entry.
/// </summary>
[CreateAssetMenu(fileName = "Knowledge", menuName = "Scriptable Objects/Knowledge")]
public class KnowledgeSO : ScriptableObject
{
    /// <summary>Hidden stable ID synchronized with the asset GUID when possible.</summary>
    [SerializeField, HideInInspector] private string uniqueId;

    [Header("Identity")]
    /// <summary>Public knowledge ID used by gameplay and persistence.</summary>
    [Tooltip("Identifiant unique (auto-genere si vide).")]
    public string knowledgeId;
    /// <summary>Display title shown to the player or UI.</summary>
    [Tooltip("Nom affiche de la connaissance.")]
    public string title;

    [Header("Description")]
    /// <summary>Description text for this knowledge entry.</summary>
    [TextArea]
    public string description;

    [Header("Classification")]
    /// <summary>Main investigation category used by requirements and future UI filters.</summary>
    public KnowledgeCategory category = KnowledgeCategory.Unknown;
    /// <summary>Primary way this knowledge is normally discovered.</summary>
    public KnowledgeSourceType sourceType = KnowledgeSourceType.Unknown;
    /// <summary>Narrative weight used by future UI and progression filters.</summary>
    public KnowledgeImportance importance = KnowledgeImportance.Useful;
    /// <summary>Freeform tags used for implicit requirements and cross-system filtering.</summary>
    public List<string> tags = new List<string>();

    [Header("Narrative Links")]
    /// <summary>District ID linked to this knowledge, if any.</summary>
    public string districtId;
    /// <summary>Room or chamber ID linked to this knowledge, if any.</summary>
    public string roomId;
    /// <summary>Person/inhabitant record ID linked to this knowledge, if any.</summary>
    public string personId;
    /// <summary>Lineage ID linked to this knowledge, if any.</summary>
    public string lineageId;
    /// <summary>Object ID linked to this knowledge, if any.</summary>
    public string objectId;
    /// <summary>Readable item that can expose this knowledge, if any.</summary>
    public Item readableItem;
    /// <summary>Temporal age most closely associated with this knowledge.</summary>
    public TemporalAge associatedAge = TemporalAge.Age666;

    [Header("Combat Passif")]
    [Tooltip("Si actif et si la connaissance est debloquee, ce bonus est applique automatiquement au combat temps reel.")]
    public bool combatBonusEnabled;
    [Tooltip("Effet passif permanent de cette connaissance dans le combat temps reel.")]
    public CombatKnowledgeModifier combatModifier = new CombatKnowledgeModifier
    {
        lightDamageMultiplier = 1f,
        counterDamageMultiplier = 1f
    };

    /// <summary>Stable internal ID, normally the asset GUID in editor.</summary>
    public string UniqueId => uniqueId;
    public CombatKnowledgeModifier CombatModifier => combatModifier;
    public bool CombatBonusEnabled => combatBonusEnabled;

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tags == null)
        {
            return false;
        }

        string trimmed = tag.Trim();
        for (int i = 0; i < tags.Count; i++)
        {
            string candidate = tags[i];
            if (!string.IsNullOrWhiteSpace(candidate) &&
                string.Equals(candidate.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Unity calls OnValidate in the editor when the asset changes.
        // The asset GUID gives a stable ID that survives renames and moves.
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (!string.IsNullOrEmpty(path))
        {
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid) && uniqueId != guid)
            {
                uniqueId = guid;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            if (string.IsNullOrWhiteSpace(knowledgeId))
            {
                knowledgeId = guid;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            return;
        }

        // Fallback for unsaved or temporary assets that do not have an AssetDatabase path yet.
        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            uniqueId = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        if (string.IsNullOrWhiteSpace(knowledgeId))
        {
            knowledgeId = uniqueId;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
