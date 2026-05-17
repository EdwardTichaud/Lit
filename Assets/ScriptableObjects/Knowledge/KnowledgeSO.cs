// Role:
// ScriptableObject data for knowledge entries unlocked by exploration.
// Usage:
// Referenced by KnowledgeManager and knowledge/readable systems.
// Responsibilities:
// Store stable identity, display title, and description.
// Dependencies:
// UnityEditor in OnValidate for asset GUID synchronization.
// Precautions:
// uniqueId/knowledgeId may be used for persistence. Do not regenerate existing IDs casually.
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

    /// <summary>Stable internal ID, normally the asset GUID in editor.</summary>
    public string UniqueId => uniqueId;

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
