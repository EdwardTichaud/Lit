using UnityEngine;

[CreateAssetMenu(fileName = "Knowledge", menuName = "Scriptable Objects/Knowledge")]
public class KnowledgeSO : ScriptableObject
{
    [SerializeField, HideInInspector] private string uniqueId;

    [Header("Identity")]
    [Tooltip("Identifiant unique (auto-genere si vide).")]
    public string knowledgeId;
    [Tooltip("Nom affiche de la connaissance.")]
    public string title;

    [Header("Description")]
    [TextArea]
    public string description;

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

            if (string.IsNullOrWhiteSpace(knowledgeId))
            {
                knowledgeId = guid;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            return;
        }

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
