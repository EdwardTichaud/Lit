using UnityEditor;

[InitializeOnLoad]
public static class CharacterDataIdAssigner
{
    private const string AutoAssignOnReloadKey = "Lit.CharacterData.AutoAssignIdsOnReload";

    static CharacterDataIdAssigner()
    {
        if (!EditorPrefs.GetBool(AutoAssignOnReloadKey, false))
        {
            return;
        }

        AssignIds();
    }

    [MenuItem("Lit/CharacterData/Refresh Unique IDs")]
    public static void AssignIds()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterData");
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        bool changedAny = false;
        for (int i = 0; i < guids.Length; i++)
        {
            string guid = guids[i];
            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (character == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(character);
            SerializedProperty prop = serialized.FindProperty("uniqueId");
            if (prop == null)
            {
                continue;
            }

            if (prop.stringValue != guid)
            {
                prop.stringValue = guid;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changedAny = true;
            }
        }

        if (changedAny)
        {
            AssetDatabase.SaveAssets();
        }
    }
}
