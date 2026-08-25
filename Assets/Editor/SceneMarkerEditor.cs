#if UNITY_EDITOR
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(SceneMarker))]
public sealed class SceneMarkerEditor : Editor
{
    private SerializedProperty assetTypeProperty;
    private SerializedProperty characterDataProperty;
    private SerializedProperty itemProperty;
    private SerializedProperty ghostProperty;

    private void OnEnable()
    {
        assetTypeProperty = serializedObject.FindProperty("assetType");
        characterDataProperty = serializedObject.FindProperty("characterData");
        itemProperty = serializedObject.FindProperty("item");
        ghostProperty = serializedObject.FindProperty("ghost");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(assetTypeProperty, new GUIContent("Type"));
        SceneMarker.MarkerAssetType assetType = (SceneMarker.MarkerAssetType)assetTypeProperty.enumValueIndex;
        if (assetType == SceneMarker.MarkerAssetType.Item)
        {
            EditorGUILayout.PropertyField(itemProperty, new GUIContent("Item"));
        }
        else if (assetType == SceneMarker.MarkerAssetType.Ghost)
        {
            EditorGUILayout.PropertyField(ghostProperty, new GUIContent("Ghost Data"));
        }
        else
        {
            EditorGUILayout.PropertyField(characterDataProperty, new GUIContent("Character Data"));
        }
        serializedObject.ApplyModifiedProperties();

        SceneMarker marker = (SceneMarker)target;
        if (marker.UsesItem)
        {
            EditorGUILayout.HelpBox(marker.Item == null
                ? "Assigne un Item avant de baker le marker."
                : "Le marker d'item utilise le World Prefab de l'Item. Bake Replace Marker instancie l'objet interactif.",
                marker.Item == null ? MessageType.Info : MessageType.None);
            DrawItemBakeButton(marker);
        }
        else if (marker.UsesGhost)
        {
            EditorGUILayout.HelpBox(marker.Ghost == null
                ? "Assigne un GhostData."
                : "Le marker de fantome est une source d'auteur ; utilise le flux de bake narratif existant.",
                marker.Ghost == null ? MessageType.Info : MessageType.None);
        }
        else if (marker.CharacterData == null)
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

    [MenuItem("Lit/Scene Marker/Create Item", false, 11)]
    private static void CreateItemMarker(MenuCommand command)
    {
        GameObject markerObject = new GameObject("ItemSceneMarker");
        GameObjectUtility.SetParentAndAlign(markerObject, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(markerObject, "Create Item Scene Marker");
        SceneMarker marker = Undo.AddComponent<SceneMarker>(markerObject);
        marker.SetItem(null);
        Selection.activeGameObject = markerObject;
    }

    [MenuItem("Lit/Scene Marker/Migrate Selected Legacy Item Marker", false, 30)]
    private static void MigrateSelectedLegacyItemMarker()
    {
        ItemSceneMarker legacy = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<ItemSceneMarker>()
            : null;
        if (legacy == null)
        {
            return;
        }

        GameObject root = legacy.gameObject;
        SceneMarker marker = Undo.AddComponent<SceneMarker>(root);
        if (legacy.UsesGhost)
        {
            marker.SetGhost(legacy.Ghost);
        }
        else if (legacy.UsesEnemy)
        {
            marker.SetCharacterData(legacy.Enemy);
        }
        else
        {
            marker.SetItem(legacy.Item);
        }

        Undo.DestroyObjectImmediate(legacy);
        EditorUtility.SetDirty(marker);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }

    [MenuItem("Lit/Scene Marker/Migrate Selected Legacy Item Marker", true)]
    private static bool CanMigrateSelectedLegacyItemMarker()
    {
        return Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<ItemSceneMarker>() != null;
    }

    private static void DrawItemBakeButton(SceneMarker marker)
    {
        if (marker == null || marker.Item == null || marker.Item.ResolveWorldPrefab() == null)
        {
            return;
        }

        if (GUILayout.Button("Bake Replace Marker"))
        {
            BakeItemMarker(marker);
        }
    }

    private static void BakeItemMarker(SceneMarker marker)
    {
        GameObject prefab = marker.Item.ResolveWorldPrefab();
        GameObject root = marker.gameObject;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, root.scene) as GameObject;
        if (instance == null)
        {
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Bake Item Scene Marker");
        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = prefab.transform.localRotation;
        instance.transform.localScale = prefab.transform.localScale;
        root.name = string.IsNullOrWhiteSpace(marker.Item.itemName) ? marker.Item.name : marker.Item.itemName;

        NetworkObject networkObject = root.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            networkObject = Undo.AddComponent<NetworkObject>(root);
        }

        InteractableItem interactable = root.GetComponent<InteractableItem>();
        if (interactable == null)
        {
            interactable = Undo.AddComponent<InteractableItem>(root);
        }

        Undo.RecordObject(interactable, "Bake Item Scene Marker");
        interactable.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
        interactable.representedItem = marker.Item;
        interactable.allowTake = true;
        EditorUtility.SetDirty(interactable);
        Undo.DestroyObjectImmediate(marker);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
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
        CharacterData data = characterInfo != null ? characterInfo.CharacterData : null;
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
        return source != null && source.GetComponentInChildren<CharacterInfo>(true) != null;
    }
}
#endif
