#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ItemSceneMarker))]
[CanEditMultipleObjects]
public class ItemSceneMarkerEditor : Editor
{
    private const string UndoLabel = "Bake Item Scene Marker";
    private const string DefaultMaisonChestTag = "MaisonChest";
    private const int DefaultMaisonChestCapacity = 100;

    private SerializedProperty assetTypeProperty;
    private SerializedProperty itemProperty;
    private SerializedProperty enemyProperty;

    private void OnEnable()
    {
        assetTypeProperty = serializedObject.FindProperty("assetType");
        itemProperty = serializedObject.FindProperty("item");
        enemyProperty = serializedObject.FindProperty("enemy");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(assetTypeProperty);
        ItemSceneMarker.MarkerAssetType assetType = (ItemSceneMarker.MarkerAssetType)assetTypeProperty.enumValueIndex;
        if (assetType == ItemSceneMarker.MarkerAssetType.Enemy)
        {
            EditorGUILayout.PropertyField(enemyProperty);
        }
        else
        {
            EditorGUILayout.PropertyField(itemProperty);
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        string validationMessage = GetValidationMessage();
        using (new EditorGUI.DisabledScope(!string.IsNullOrWhiteSpace(validationMessage)))
        {
            if (GUILayout.Button("Bake Replace Marker"))
            {
                BakeTargets();
            }
        }

        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            EditorGUILayout.HelpBox(validationMessage, MessageType.Info);
        }
    }

    [MenuItem("Lit/Item/Scene Marker", false, 10)]
    private static void CreateMarker(MenuCommand command)
    {
        GameObject markerObject = new GameObject("ItemSceneMarker");
        GameObjectUtility.SetParentAndAlign(markerObject, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(markerObject, "Create Item Scene Marker");
        Undo.AddComponent<ItemSceneMarker>(markerObject);
        Selection.activeGameObject = markerObject;
    }

    [MenuItem("Lit/Enemy/Scene Marker", false, 10)]
    private static void CreateEnemyMarker(MenuCommand command)
    {
        GameObject markerObject = new GameObject("EnemySceneMarker");
        GameObjectUtility.SetParentAndAlign(markerObject, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(markerObject, "Create Enemy Scene Marker");
        ItemSceneMarker marker = Undo.AddComponent<ItemSceneMarker>(markerObject);
        marker.SetAssetType(ItemSceneMarker.MarkerAssetType.Enemy);
        EditorUtility.SetDirty(marker);
        Selection.activeGameObject = markerObject;
    }

    [MenuItem("Lit/Item/Convert Selected Legacy Hierarchy", false, 20)]
    private static void ConvertSelectedLegacyHierarchy()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return;
        }

        List<GameObject> convertedObjects = new List<GameObject>();
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject selectedObject = selectedObjects[i];
            if (ConvertLegacyHierarchy(selectedObject))
            {
                convertedObjects.Add(selectedObject);
            }
        }

        if (convertedObjects.Count > 0)
        {
            Selection.objects = convertedObjects.ToArray();
        }
    }

    [MenuItem("Lit/Item/Convert Selected Legacy Hierarchy", true)]
    private static bool ValidateConvertSelectedLegacyHierarchy()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject selectedObject = selectedObjects[i];
            if (IsLegacyHierarchyConvertible(selectedObject))
            {
                return true;
            }
        }

        return false;
    }

    private string GetValidationMessage()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not ItemSceneMarker marker)
            {
                continue;
            }

            if (marker.UsesEnemy)
            {
                if (marker.Enemy == null)
                {
                    return "Assigne un CharacterData ennemi avant le bake.";
                }

                if (!marker.Enemy.isEnemy)
                {
                    return "Le CharacterData selectionne doit avoir isEnemy active.";
                }
            }
            else if (marker.Item == null)
            {
                return "Assigne un Item avant le bake.";
            }

            if (marker.ResolvePreviewPrefab() == null)
            {
                return marker.UsesEnemy
                    ? "Le CharacterData ennemi selectionne ne resolve aucun prefab de monde."
                    : "L'Item selectionne ne resolve aucun prefab de monde.";
            }
        }

        return string.Empty;
    }

    private void BakeTargets()
    {
        List<GameObject> bakedObjects = new List<GameObject>();
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not ItemSceneMarker marker)
            {
                continue;
            }

            GameObject baked = BakeMarker(marker);
            if (baked != null)
            {
                bakedObjects.Add(baked);
            }
        }

        if (bakedObjects.Count > 0)
        {
            Selection.objects = bakedObjects.ToArray();
        }
    }

    private static GameObject BakeMarker(ItemSceneMarker marker)
    {
        if (marker == null)
        {
            return null;
        }

        GameObject prefab = marker.ResolvePreviewPrefab();
        if (prefab == null)
        {
            return null;
        }

        GameObject root = marker.gameObject;
        Undo.RecordObject(root, UndoLabel);
        root.name = marker.UsesEnemy ? ResolveRootName(marker.Enemy) : ResolveRootName(marker.Item);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, root.scene) as GameObject;
        if (instance == null)
        {
            instance = Object.Instantiate(prefab);
            SceneManagerMoveToRootScene(instance, root);
        }

        if (instance == null)
        {
            return null;
        }

        Undo.RegisterCreatedObjectUndo(instance, UndoLabel);
        AttachModelUnderRoot(root.transform, prefab.transform, instance.transform);

        if (marker.UsesEnemy)
        {
            ConfigureEnemy(instance, marker.Enemy);
        }
        else if (marker.Item.isBuilding)
        {
            ConfigureBuilding(instance, marker.Item);
        }
        else
        {
            ConfigurePickup(instance, marker.Item);
        }

        EnsureOutlineTargetsWithUndo(instance);
        Undo.DestroyObjectImmediate(marker);
        EditorSceneManager.MarkSceneDirty(root.scene);
        return root;
    }

    private static bool ConvertLegacyHierarchy(GameObject root)
    {
        if (!IsLegacyHierarchyConvertible(root))
        {
            return false;
        }

        Transform modelTransform = ResolveLegacyModelRoot(root.transform);
        if (modelTransform == null)
        {
            return false;
        }

        GameObject modelRoot = modelTransform.gameObject;
        NetworkObject sourceNetworkObject = root.GetComponent<NetworkObject>();
        NetworkObject modelNetworkObject = modelRoot.GetComponent<NetworkObject>();
        if (modelNetworkObject == null
            && (sourceNetworkObject != null
                || root.GetComponent<InteractableItem>() != null
                || root.GetComponent<BuildingInfoInteractable>() != null))
        {
            modelNetworkObject = EnsureComponent<NetworkObject>(modelRoot);
            if (sourceNetworkObject != null && modelNetworkObject != null)
            {
                Undo.RecordObject(modelNetworkObject, UndoLabel);
                EditorUtility.CopySerialized(sourceNetworkObject, modelNetworkObject);
                EditorUtility.SetDirty(modelNetworkObject);
            }
        }

        Collider sourceCollider = root.GetComponent<Collider>();
        Collider interactionCollider = EnsureLegacyInteractionColliderOnModel(root, modelRoot, sourceCollider);

        BuildingInfoInteractable sourceInfo = root.GetComponent<BuildingInfoInteractable>();
        if (sourceInfo != null)
        {
            BuildingInfoInteractable destinationInfo = CopyComponentToRoot(sourceInfo, modelRoot);
            if (destinationInfo != null)
            {
                Undo.RecordObject(destinationInfo, UndoLabel);
                destinationInfo.interactionTrigger = interactionCollider;
                EditorUtility.SetDirty(destinationInfo);
            }
        }

        InteractableItem sourceItem = root.GetComponent<InteractableItem>();
        if (sourceItem != null)
        {
            InteractableItem destinationItem = CopyComponentToRoot(sourceItem, modelRoot);
            if (destinationItem != null)
            {
                Undo.RecordObject(destinationItem, UndoLabel);
                destinationItem.interactionTrigger = interactionCollider;
                ClearRecoverableWorldInfoReference(destinationItem);
                EditorUtility.SetDirty(destinationItem);
            }
        }

        TorchVisionSensitive sourceTorchVision = root.GetComponent<TorchVisionSensitive>();
        if (sourceTorchVision != null)
        {
            TorchVisionSensitive destinationTorchVision = CopyComponentToRoot(sourceTorchVision, modelRoot);
            ConfigureTorchVisionSensitive(destinationTorchVision, modelRoot);
        }

        if (sourceCollider != null)
        {
            Undo.DestroyObjectImmediate(sourceCollider);
        }

        if (sourceNetworkObject != null)
        {
            Undo.DestroyObjectImmediate(sourceNetworkObject);
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        return true;
    }

    private static bool IsLegacyHierarchyConvertible(GameObject root)
    {
        if (root == null || !root.scene.IsValid())
        {
            return false;
        }

        if (root.GetComponent<InteractableItem>() == null
            && root.GetComponent<BuildingInfoInteractable>() == null
            && root.GetComponent<TorchVisionSensitive>() == null
            && root.GetComponent<NetworkObject>() == null
            && root.GetComponent<Collider>() == null)
        {
            return false;
        }

        Transform modelTransform = ResolveLegacyModelRoot(root.transform);
        return modelTransform != null && modelTransform != root.transform;
    }

    private static void SceneManagerMoveToRootScene(GameObject instance, GameObject root)
    {
        if (instance == null || root == null)
        {
            return;
        }

        UnityEngine.SceneManagement.Scene scene = root.scene;
        if (scene.IsValid())
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
        }
    }

    private static void AttachModelUnderRoot(Transform rootTransform, Transform prefabTransform, Transform instanceTransform)
    {
        if (rootTransform == null || instanceTransform == null)
        {
            return;
        }

        Undo.RecordObject(instanceTransform, UndoLabel);
        Undo.SetTransformParent(instanceTransform, rootTransform, UndoLabel);
        instanceTransform.localPosition = Vector3.zero;
        instanceTransform.localRotation = prefabTransform != null ? prefabTransform.localRotation : Quaternion.identity;
        Vector3 prefabScale = prefabTransform != null ? prefabTransform.localScale : Vector3.one;
        instanceTransform.localScale = prefabScale;
    }

    private static Transform ResolveLegacyModelRoot(Transform rootTransform)
    {
        if (rootTransform == null || rootTransform.childCount == 0)
        {
            return null;
        }

        if (rootTransform.childCount == 1)
        {
            return rootTransform.GetChild(0);
        }

        for (int i = 0; i < rootTransform.childCount; i++)
        {
            Transform child = rootTransform.GetChild(i);
            if (child != null && child.name.EndsWith("_Model"))
            {
                return child;
            }
        }

        for (int i = 0; i < rootTransform.childCount; i++)
        {
            Transform child = rootTransform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.GetComponent<NetworkObject>() != null || child.GetComponentInChildren<Renderer>(true) != null)
            {
                return child;
            }
        }

        return rootTransform.GetChild(0);
    }

    private static void ConfigurePickup(GameObject modelRoot, Item item)
    {
        if (modelRoot == null || item == null)
        {
            return;
        }

        InteractableItem sourceContainer = FindComponentOnRootOrChildren<InteractableItem>(modelRoot);
        Collider sourceCollider = sourceContainer != null ? sourceContainer.interactionTrigger : null;
        Collider interactionCollider = EnsureInteractionColliderOnRoot(modelRoot, sourceCollider);

        GameObject sourceContainerObject = sourceContainer != null ? sourceContainer.gameObject : null;
        InteractableItem container = CopyComponentToRoot(sourceContainer, modelRoot);
        if (container == null)
        {
            EnsureComponent<NetworkObject>(modelRoot);
            container = EnsureComponent<InteractableItem>(modelRoot);
        }

        if (container == null)
        {
            return;
        }

        Undo.RecordObject(container, UndoLabel);
        container.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
        container.representedItem = item;
        container.destroyWhenStorageEmpty = true;
        container.allowTake = true;
        container.storedItems = new List<InteractableItem.LootItemEntry>
        {
            new InteractableItem.LootItemEntry { item = item, quantity = 1 }
        };
        container.interactionTrigger = interactionCollider;
        ClearRecoverableWorldInfoReference(container);
        TryRemoveUnusedNetworkObject(sourceContainerObject, modelRoot);
        EditorUtility.SetDirty(container);
    }

    private static void ConfigureBuilding(GameObject modelRoot, Item item)
    {
        if (modelRoot == null || item == null)
        {
            return;
        }

        BuildingInfoInteractable sourceInfo = FindComponentOnRootOrChildren<BuildingInfoInteractable>(modelRoot);
        InteractableItem sourceContainer = FindComponentOnRootOrChildren<InteractableItem>(modelRoot);
        bool hadSourceContainer = sourceContainer != null;
        Collider sourceCollider = sourceInfo != null
            ? sourceInfo.interactionTrigger
            : (sourceContainer != null ? sourceContainer.interactionTrigger : null);
        Collider interactionCollider = EnsureInteractionColliderOnRoot(modelRoot, sourceCollider);

        BuildingInfoInteractable info = CopyComponentToRoot(sourceInfo, modelRoot);
        if (info == null)
        {
            info = EnsureComponent<BuildingInfoInteractable>(modelRoot);
        }

        if (info == null)
        {
            return;
        }

        Undo.RecordObject(info, UndoLabel);
        SerializedObject serializedInfo = new SerializedObject(info);
        serializedInfo.Update();
        serializedInfo.FindProperty("buildId").stringValue = ResolveItemId(item);
        serializedInfo.FindProperty("buildingItem").objectReferenceValue = item;
        serializedInfo.FindProperty("level").intValue = 1;
        SerializedProperty networkIdProperty = serializedInfo.FindProperty("networkBuildingId");
        if (networkIdProperty != null)
        {
            networkIdProperty.ulongValue = 0ul;
        }

        serializedInfo.ApplyModifiedProperties();

        info.enabled = true;
        info.interactionTrigger = interactionCollider;
        EditorUtility.SetDirty(info);

        InteractableItem container = null;
        GameObject sourceContainerObject = sourceContainer != null ? sourceContainer.gameObject : null;
        if (sourceContainer != null || item.isHomeChest)
        {
            container = CopyComponentToRoot(sourceContainer, modelRoot);
            if (container == null)
            {
                EnsureComponent<NetworkObject>(modelRoot);
                container = EnsureComponent<InteractableItem>(modelRoot);
            }
        }

        if (container != null)
        {
            Undo.RecordObject(container, UndoLabel);
            if (!hadSourceContainer)
            {
                container.interactableCategory = InteractableItem.InteractableCategory.Container;
                if (container.storedItems == null)
                {
                    container.storedItems = new List<InteractableItem.LootItemEntry>();
                }
            }

            container.representedItem = item;
            container.interactionTrigger = interactionCollider;
            ClearRecoverableWorldInfoReference(container);
            if (item.isHomeChest)
            {
                ApplyHomeChestDefaults(modelRoot, container);
            }

            TryRemoveUnusedNetworkObject(sourceContainerObject, modelRoot);
            EditorUtility.SetDirty(container);
        }
        else if (item.isHomeChest)
        {
            TryAssignMaisonChestTag(modelRoot, ResolveMaisonChestTag());
        }
    }

    private static void ConfigureEnemy(GameObject modelRoot, CharacterData enemy)
    {
        if (modelRoot == null || enemy == null)
        {
            return;
        }

        EnemyInfo enemyInfo = CopyComponentToRoot(FindComponentOnRootOrChildren<EnemyInfo>(modelRoot), modelRoot);
        if (enemyInfo == null)
        {
            enemyInfo = EnsureComponent<EnemyInfo>(modelRoot);
        }

        if (enemyInfo != null)
        {
            Undo.RecordObject(enemyInfo, UndoLabel);
            enemyInfo.SetEnemy(enemy);
            EditorUtility.SetDirty(enemyInfo);
        }

        CharacterInfo characterInfo = CopyComponentToRoot(FindComponentOnRootOrChildren<CharacterInfo>(modelRoot), modelRoot);
        if (characterInfo == null)
        {
            characterInfo = EnsureComponent<CharacterInfo>(modelRoot);
        }

        if (characterInfo != null)
        {
            Undo.RecordObject(characterInfo, UndoLabel);
            characterInfo.SetCharacterData(enemy);
            EditorUtility.SetDirty(characterInfo);
        }

        CombatHealth health = CopyComponentToRoot(FindComponentOnRootOrChildren<CombatHealth>(modelRoot), modelRoot);
        if (health == null)
        {
            health = EnsureComponent<CombatHealth>(modelRoot);
        }

        if (health != null)
        {
            Undo.RecordObject(health, UndoLabel);
            int maxHp = enemy.ResolveMaxHp();
            health.SetHealth(enemy.ResolveCurrentHp(maxHp), maxHp);
            EditorUtility.SetDirty(health);
        }

        CombatAggroEnemy aggro = CopyComponentToRoot(FindComponentOnRootOrChildren<CombatAggroEnemy>(modelRoot), modelRoot);
        if (aggro == null)
        {
            aggro = EnsureComponent<CombatAggroEnemy>(modelRoot);
        }

        if (aggro != null)
        {
            Undo.RecordObject(aggro, UndoLabel);
            aggro.SetEnemy(enemy);
            EditorUtility.SetDirty(aggro);
        }
    }

    private static T FindComponentOnRootOrChildren<T>(GameObject root) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        T direct = root.GetComponent<T>();
        if (direct != null)
        {
            return direct;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null)
            {
                continue;
            }

            if (component.gameObject == root)
            {
                continue;
            }

            return component;
        }

        return null;
    }

    private static T CopyComponentToRoot<T>(T source, GameObject root) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        T destination = EnsureComponent<T>(root);
        if (source == null || destination == null || source == destination)
        {
            return destination;
        }

        Undo.RecordObject(destination, UndoLabel);
        EditorUtility.CopySerialized(source, destination);
        Undo.DestroyObjectImmediate(source);
        return destination;
    }

    private static void EnsureOutlineTargetsWithUndo(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponent<RuntimeOutlineTarget>() != null)
            {
                continue;
            }

            Undo.AddComponent<RuntimeOutlineTarget>(renderer.gameObject);
            EditorUtility.SetDirty(renderer.gameObject);
        }
    }

    private static Collider EnsureLegacyInteractionColliderOnModel(GameObject sourceRoot, GameObject modelRoot, Collider sourceCollider)
    {
        if (modelRoot == null)
        {
            return null;
        }

        if (TryCopySupportedColliderToModel(modelRoot, sourceCollider, out Collider copiedCollider))
        {
            return copiedCollider;
        }

        Bounds worldBounds;
        if (!WorldPickupUtility.TryCalculateBounds(modelRoot, out worldBounds)
            && (sourceRoot == null || !WorldPickupUtility.TryCalculateBounds(sourceRoot, out worldBounds)))
        {
            return null;
        }

        if (!TryBuildLocalBoundsFromWorldBounds(modelRoot.transform, worldBounds, out Bounds localBounds))
        {
            return null;
        }

        BoxCollider boxCollider = ReplaceCollider<BoxCollider>(modelRoot);
        if (boxCollider == null)
        {
            return null;
        }

        Undo.RecordObject(boxCollider, UndoLabel);
        boxCollider.center = localBounds.center;
        boxCollider.size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(localBounds.size.x)),
            Mathf.Max(0.01f, Mathf.Abs(localBounds.size.y)),
            Mathf.Max(0.01f, Mathf.Abs(localBounds.size.z)));
        boxCollider.isTrigger = true;
        EditorUtility.SetDirty(boxCollider);
        return boxCollider;
    }

    private static bool TryCopySupportedColliderToModel(GameObject modelRoot, Collider sourceCollider, out Collider copiedCollider)
    {
        copiedCollider = null;
        if (modelRoot == null || sourceCollider == null)
        {
            return false;
        }

        if (sourceCollider is BoxCollider sourceBox)
        {
            if (!TryBuildLocalBoundsFromBoxCollider(modelRoot.transform, sourceBox, out Bounds localBounds))
            {
                return false;
            }

            BoxCollider destination = ReplaceCollider<BoxCollider>(modelRoot);
            if (destination == null)
            {
                return false;
            }

            Undo.RecordObject(destination, UndoLabel);
            destination.center = localBounds.center;
            destination.size = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(localBounds.size.x)),
                Mathf.Max(0.01f, Mathf.Abs(localBounds.size.y)),
                Mathf.Max(0.01f, Mathf.Abs(localBounds.size.z)));
            destination.isTrigger = true;
            destination.enabled = sourceBox.enabled;
            destination.sharedMaterial = sourceBox.sharedMaterial;
            EditorUtility.SetDirty(destination);
            copiedCollider = destination;
            return true;
        }

        if (!TryBuildLocalBoundsFromWorldBounds(modelRoot.transform, sourceCollider.bounds, out Bounds fallbackBounds))
        {
            return false;
        }

        BoxCollider fallbackCollider = ReplaceCollider<BoxCollider>(modelRoot);
        if (fallbackCollider == null)
        {
            return false;
        }

        Undo.RecordObject(fallbackCollider, UndoLabel);
        fallbackCollider.center = fallbackBounds.center;
        fallbackCollider.size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(fallbackBounds.size.x)),
            Mathf.Max(0.01f, Mathf.Abs(fallbackBounds.size.y)),
            Mathf.Max(0.01f, Mathf.Abs(fallbackBounds.size.z)));
        fallbackCollider.isTrigger = true;
        fallbackCollider.enabled = sourceCollider.enabled;
        fallbackCollider.sharedMaterial = sourceCollider.sharedMaterial;
        EditorUtility.SetDirty(fallbackCollider);
        copiedCollider = fallbackCollider;
        return true;
    }

    private static bool TryBuildLocalBoundsFromBoxCollider(Transform target, BoxCollider source, out Bounds localBounds)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);
        if (target == null || source == null)
        {
            return false;
        }

        Vector3 center = source.center;
        Vector3 extents = source.size * 0.5f;
        bool hasBounds = false;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 sourceCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 worldCorner = source.transform.TransformPoint(sourceCorner);
                    Vector3 localCorner = target.InverseTransformPoint(worldCorner);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }
        }

        return hasBounds;
    }

    private static void ConfigureTorchVisionSensitive(TorchVisionSensitive component, GameObject modelRoot)
    {
        if (component == null || modelRoot == null)
        {
            return;
        }

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        Collider[] colliders = modelRoot.GetComponentsInChildren<Collider>(true);

        SerializedObject serializedComponent = new SerializedObject(component);
        serializedComponent.Update();
        serializedComponent.FindProperty("distanceReference").objectReferenceValue = modelRoot.transform;
        serializedComponent.FindProperty("visualRoot").objectReferenceValue = modelRoot.transform;
        SetObjectReferenceArray(serializedComponent.FindProperty("targetRenderers"), renderers);
        SetObjectReferenceArray(serializedComponent.FindProperty("colliders"), colliders);
        SetObjectReferenceArray(serializedComponent.FindProperty("behaviours"), new Behaviour[0]);
        serializedComponent.ApplyModifiedProperties();
        EditorUtility.SetDirty(component);
    }

    private static void SetObjectReferenceArray(SerializedProperty arrayProperty, Object[] values)
    {
        if (arrayProperty == null || !arrayProperty.isArray)
        {
            return;
        }

        int valueCount = values != null ? values.Length : 0;
        arrayProperty.arraySize = valueCount;
        for (int i = 0; i < valueCount; i++)
        {
            arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static Collider EnsureInteractionColliderOnRoot(GameObject root, Collider sourceCollider)
    {
        if (root == null)
        {
            return null;
        }

        if (TryCopySupportedRootColliderToRoot(root, sourceCollider, out Collider copiedCollider))
        {
            return copiedCollider;
        }

        if (!WorldPickupUtility.TryCalculateBounds(root, out Bounds worldBounds))
        {
            return null;
        }

        if (!TryBuildLocalBoundsFromWorldBounds(root.transform, worldBounds, out Bounds localBounds))
        {
            return null;
        }

        BoxCollider boxCollider = ReplaceCollider<BoxCollider>(root);
        if (boxCollider == null)
        {
            return null;
        }

        Undo.RecordObject(boxCollider, UndoLabel);
        boxCollider.center = localBounds.center;
        boxCollider.size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(localBounds.size.x)),
            Mathf.Max(0.01f, Mathf.Abs(localBounds.size.y)),
            Mathf.Max(0.01f, Mathf.Abs(localBounds.size.z)));
        boxCollider.isTrigger = true;
        EditorUtility.SetDirty(boxCollider);
        return boxCollider;
    }

    private static bool TryCopySupportedRootColliderToRoot(GameObject root, Collider sourceCollider, out Collider copiedCollider)
    {
        copiedCollider = null;
        if (root == null || sourceCollider == null || sourceCollider.transform != root.transform)
        {
            return false;
        }

        if (sourceCollider is BoxCollider sourceBox)
        {
            BoxCollider destination = ReplaceCollider<BoxCollider>(root);
            if (destination == null)
            {
                return false;
            }

            Undo.RecordObject(destination, UndoLabel);
            destination.center = sourceBox.center;
            destination.size = sourceBox.size;
            destination.isTrigger = true;
            destination.enabled = sourceBox.enabled;
            destination.sharedMaterial = sourceBox.sharedMaterial;
            EditorUtility.SetDirty(destination);
            copiedCollider = destination;
            if (destination != sourceBox)
            {
                Undo.DestroyObjectImmediate(sourceBox);
            }
            return true;
        }

        if (sourceCollider is SphereCollider sourceSphere)
        {
            SphereCollider destination = ReplaceCollider<SphereCollider>(root);
            if (destination == null)
            {
                return false;
            }

            Undo.RecordObject(destination, UndoLabel);
            destination.center = sourceSphere.center;
            destination.radius = sourceSphere.radius;
            destination.isTrigger = true;
            destination.enabled = sourceSphere.enabled;
            destination.sharedMaterial = sourceSphere.sharedMaterial;
            EditorUtility.SetDirty(destination);
            copiedCollider = destination;
            if (destination != sourceSphere)
            {
                Undo.DestroyObjectImmediate(sourceSphere);
            }
            return true;
        }

        if (sourceCollider is CapsuleCollider sourceCapsule)
        {
            CapsuleCollider destination = ReplaceCollider<CapsuleCollider>(root);
            if (destination == null)
            {
                return false;
            }

            Undo.RecordObject(destination, UndoLabel);
            destination.center = sourceCapsule.center;
            destination.radius = sourceCapsule.radius;
            destination.height = sourceCapsule.height;
            destination.direction = sourceCapsule.direction;
            destination.isTrigger = true;
            destination.enabled = sourceCapsule.enabled;
            destination.sharedMaterial = sourceCapsule.sharedMaterial;
            EditorUtility.SetDirty(destination);
            copiedCollider = destination;
            if (destination != sourceCapsule)
            {
                Undo.DestroyObjectImmediate(sourceCapsule);
            }
            return true;
        }

        return false;
    }

    private static bool TryBuildLocalBoundsFromWorldBounds(Transform target, Bounds worldBounds, out Bounds localBounds)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);
        if (target == null)
        {
            return false;
        }

        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;
        bool hasBounds = false;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 localCorner = target.InverseTransformPoint(worldCorner);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }
        }

        return hasBounds;
    }

    private static T ReplaceCollider<T>(GameObject host) where T : Collider
    {
        if (host == null)
        {
            return null;
        }

        T desiredCollider = null;
        Collider[] colliders = host.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (collider is T typedCollider)
            {
                desiredCollider = typedCollider;
                continue;
            }

            Undo.DestroyObjectImmediate(collider);
        }

        if (desiredCollider != null)
        {
            return desiredCollider;
        }

        return Undo.AddComponent<T>(host);
    }

    private static void ClearRecoverableWorldInfoReference(InteractableItem container)
    {
        if (container == null)
        {
            return;
        }

        SerializedObject serializedContainer = new SerializedObject(container);
        serializedContainer.Update();
        SerializedProperty recoverableWorldInfoProperty = serializedContainer.FindProperty("recoverableWorldInfo");
        if (recoverableWorldInfoProperty != null)
        {
            recoverableWorldInfoProperty.objectReferenceValue = null;
            serializedContainer.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            serializedContainer.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void TryRemoveUnusedNetworkObject(GameObject sourceObject, GameObject host)
    {
        if (sourceObject == null || host == null || sourceObject == host)
        {
            return;
        }

        NetworkObject networkObject = sourceObject.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            return;
        }

        NetworkBehaviour[] remainingBehaviours = sourceObject.GetComponents<NetworkBehaviour>();
        if (remainingBehaviours != null && remainingBehaviours.Length > 0)
        {
            return;
        }

        Undo.DestroyObjectImmediate(networkObject);
    }

    private static void ApplyHomeChestDefaults(GameObject instance, InteractableItem container)
    {
        string tag = ResolveMaisonChestTag();
        int capacity = ResolveMaisonChestCapacity();
        bool forceNonCollectable = ResolveMaisonForceNonCollectable();

        TryAssignMaisonChestTag(instance, tag);
        if (container == null)
        {
            return;
        }

        if (container.maxStoredQuantity <= 0 && capacity > 0)
        {
            container.maxStoredQuantity = capacity;
        }

        if (forceNonCollectable)
        {
            container.allowTake = false;
        }
    }

    private static void TryAssignMaisonChestTag(GameObject instance, string tag)
    {
        if (instance == null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        try
        {
            instance.tag = tag;
        }
        catch (UnityException)
        {
            // Tag not defined in project settings, ignore.
        }
    }

    private static string ResolveMaisonChestTag()
    {
        Maison maison = FindMaison();
        if (maison != null && !string.IsNullOrWhiteSpace(maison.maisonChestTag))
        {
            return maison.maisonChestTag;
        }

        return DefaultMaisonChestTag;
    }

    private static int ResolveMaisonChestCapacity()
    {
        Maison maison = FindMaison();
        return maison != null ? maison.maisonChestCapacity : DefaultMaisonChestCapacity;
    }

    private static bool ResolveMaisonForceNonCollectable()
    {
        Maison maison = FindMaison();
        return maison == null || maison.forceMaisonChestNonCollectable;
    }

    private static string ResolveRootName(Item item)
    {
        if (item == null)
        {
            return "Item";
        }

        if (!string.IsNullOrWhiteSpace(item.name))
        {
            return item.name;
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            return item.itemName;
        }

        return "Item";
    }

    private static string ResolveRootName(CharacterData enemy)
    {
        if (enemy == null)
        {
            return "Enemy";
        }

        if (!string.IsNullOrWhiteSpace(enemy.name))
        {
            return enemy.name;
        }

        string displayName = enemy.ResolveDisplayName();
        return !string.IsNullOrWhiteSpace(displayName) ? displayName : "Enemy";
    }

    private static Maison FindMaison()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<Maison>();
#else
        return Object.FindObjectOfType<Maison>();
#endif
    }

    private static string ResolveItemId(Item item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.itemId))
        {
            return item.itemId;
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            return item.itemName;
        }

        return item.name;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        if (target == null)
        {
            return null;
        }

        T existing = target.GetComponent<T>();
        if (existing != null)
        {
            return existing;
        }

        return Undo.AddComponent<T>(target);
    }
}
#endif
