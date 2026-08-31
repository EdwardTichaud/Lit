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
                : "Bake in Scene instancie le World Prefab et configure l'objet interactif directement dans la scene.",
                marker.Item == null ? MessageType.Info : MessageType.None);
            DrawBakeButton(marker);
        }
        else if (marker.UsesGhost)
        {
            EditorGUILayout.HelpBox(marker.Ghost == null
                ? "Assigne un GhostData."
                : "Bake in Scene instancie le World Prefab et lie le GhostData au GhostController de la scene.",
                marker.Ghost == null ? MessageType.Info : MessageType.None);
            DrawBakeButton(marker);
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
            EditorGUILayout.HelpBox(
                marker.BakedCharacterInstance == null
                    ? "Bake in Scene place l'acteur dans la scene. Aucun prefab de personnage n'est instancie au lancement."
                    : "Cet acteur est deja baked dans la scene. Re-bake le remplace par le World Prefab courant.",
                MessageType.None);
            DrawBakeButton(marker);
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

    private static void DrawBakeButton(SceneMarker marker)
    {
        if (marker == null || marker.ResolvePreviewPrefab() == null)
        {
            return;
        }

        if (GUILayout.Button("Bake in Scene"))
        {
            BakeMarker(marker);
        }
    }

    private static void BakeMarker(SceneMarker marker)
    {
        if (marker == null)
        {
            return;
        }

        if (marker.UsesItem)
        {
            BakeItemMarker(marker);
            return;
        }

        if (marker.UsesGhost)
        {
            BakeGhostMarker(marker);
            return;
        }

        if (marker.UsesCharacter)
        {
            BakeCharacterMarker(marker);
        }
    }

    private static void BakeCharacterMarker(SceneMarker marker)
    {
        CharacterData characterData = marker.CharacterData;
        GameObject prefab = characterData != null ? characterData.ResolveWorldPrefab() : null;
        if (prefab == null)
        {
            return;
        }

        GameObject root = marker.gameObject;
        if (marker.BakedCharacterInstance != null)
        {
            Undo.DestroyObjectImmediate(marker.BakedCharacterInstance);
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, root.scene) as GameObject;
        if (instance == null)
        {
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Bake Character Scene Marker");
        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = prefab.transform.localRotation;
        instance.transform.localScale = prefab.transform.localScale;

        CharacterInfo characterInfo = instance.GetComponent<CharacterInfo>();
        if (characterInfo == null)
        {
            characterInfo = Undo.AddComponent<CharacterInfo>(instance);
        }

        Undo.RecordObject(characterInfo, "Bake Character Scene Marker");
        characterInfo.SetCharacterData(characterData);
        EditorUtility.SetDirty(characterInfo);

        Undo.RecordObject(marker, "Bake Character Scene Marker");
        marker.SetBakedCharacterInstance(instance);
        SceneMarker.ConfigureSpawnedCharacter(instance, characterData, marker.MarkerId, characterData.worldPrefab);
        EditorUtility.SetDirty(marker);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
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

    private static void BakeGhostMarker(SceneMarker marker)
    {
        GhostData ghostData = marker.Ghost;
        GameObject prefab = ghostData != null ? ghostData.ResolveWorldPrefab() : null;
        if (prefab == null)
        {
            return;
        }

        GameObject root = marker.gameObject;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, root.scene) as GameObject;
        if (instance == null)
        {
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Bake Ghost Scene Marker");
        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = prefab.transform.localRotation;
        instance.transform.localScale = prefab.transform.localScale;

        GhostController ghostController = instance.GetComponentInChildren<GhostController>(true);
        if (ghostController == null)
        {
            Undo.DestroyObjectImmediate(instance);
            Debug.LogError("[SceneMarker] Le World Prefab du fantome doit contenir un GhostController.", root);
            return;
        }

        Undo.RecordObject(ghostController, "Bake Ghost Scene Marker");
        ghostController.SetGhostData(ghostData);
        EditorUtility.SetDirty(ghostController);

        root.name = string.IsNullOrWhiteSpace(ghostData.displayName) ? ghostData.name : ghostData.displayName;
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
