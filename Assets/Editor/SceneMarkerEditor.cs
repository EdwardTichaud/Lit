#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(SceneMarker))]
public sealed class SceneMarkerEditor : Editor
{
    private SerializedProperty characterDataProperty;

    private void OnEnable()
    {
        characterDataProperty = serializedObject.FindProperty("characterData");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(characterDataProperty, new GUIContent("Character Data"));
        serializedObject.ApplyModifiedProperties();

        SceneMarker marker = (SceneMarker)target;
        if (marker.CharacterData == null)
        {
            EditorGUILayout.HelpBox("Assigne un CharacterData.", MessageType.Info);
        }
        else if (marker.CharacterData.worldPrefab == null)
        {
            EditorGUILayout.HelpBox("Le CharacterData doit definir un World Prefab. Le marker ne bascule jamais sur Model.", MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox("Le World Prefab sera instancie au lancement. Ne place pas de copie du personnage dans la scene.", MessageType.None);
        }
    }

    [MenuItem("Lit/Scene Marker/Create", false, 10)]
    private static void CreateMarker(MenuCommand command)
    {
        GameObject markerObject = new GameObject("SceneMarker");
        GameObjectUtility.SetParentAndAlign(markerObject, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(markerObject, "Create Scene Marker");
        Undo.AddComponent<SceneMarker>(markerObject);
        Selection.activeGameObject = markerObject;
    }

    [MenuItem("Lit/Scene Marker/Convert Selected Character", false, 20)]
    private static void ConvertSelectedCharacter()
    {
        GameObject source = Selection.activeGameObject;
        if (source == null)
        {
            return;
        }

        CharacterInfo characterInfo = source.GetComponentInChildren<CharacterInfo>(true);
        EnemyInfo enemyInfo = source.GetComponentInChildren<EnemyInfo>(true);
        CharacterData data = characterInfo != null ? characterInfo.CharacterData : enemyInfo != null ? enemyInfo.CharacterData : null;
        if (data == null)
        {
            Debug.LogWarning("[SceneMarker] Aucun CharacterData trouve sur la selection.", source);
            return;
        }

        Transform sourceTransform = source.transform;
        GameObject markerObject = new GameObject("SceneMarker_" + data.ResolveDisplayName());
        Undo.RegisterCreatedObjectUndo(markerObject, "Convert Character To Scene Marker");
        markerObject.transform.SetParent(sourceTransform.parent, true);
        markerObject.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
        markerObject.transform.localScale = sourceTransform.localScale;
        SceneMarker marker = Undo.AddComponent<SceneMarker>(markerObject);
        marker.SetCharacterData(data);
        EditorUtility.SetDirty(marker);
        Undo.DestroyObjectImmediate(source);
        EditorSceneManager.MarkSceneDirty(markerObject.scene);
        Selection.activeGameObject = markerObject;
    }

    [MenuItem("Lit/Scene Marker/Convert Selected Character", true)]
    private static bool CanConvertSelectedCharacter()
    {
        GameObject source = Selection.activeGameObject;
        return source != null && (source.GetComponentInChildren<CharacterInfo>(true) != null || source.GetComponentInChildren<EnemyInfo>(true) != null);
    }
}
#endif
