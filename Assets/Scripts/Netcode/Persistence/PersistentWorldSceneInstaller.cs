using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PersistentWorldSceneInstaller
{
    public const string CharacterPrefabPrefix = "character:";
    public const string BuildingPrefabPrefix = "building:";
    public const string ItemPrefabPrefix = "item:";
    public const string DroppedLootPrefabPrefix = "itemdrop:";
    public const string SingletonPrefabPrefix = "singleton:";
    public const string CharacterPersistentPrefix = "runtime:character:";
    public const string BuildingPersistentPrefix = "runtime:building:";
    public const string KnowledgeManagerPersistentId = "runtime:singleton:knowledge-manager";
    public const string KnowledgeManagerPrefabId = SingletonPrefabPrefix + "knowledge-manager";

    public static void PrepareScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        if (roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            PrepareSceneBraziers(root);
            PrepareSceneContainers(root);
            PrepareScenePuzzles(root);
            PrepareSceneBuildings(root);
            PrepareSceneInteractables(root);
            PrepareSceneCharacters(root);
        }
    }

    public static void EnsureRuntimeCharacterIdentity(GameObject target, string characterId)
    {
        if (target == null || string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        PersistentNetworkObject persistentObject = EnsurePersistentObject(target, false);
        if (persistentObject == null)
        {
            return;
        }

        string expectedPersistentId = BuildCharacterPersistentId(characterId);
        string expectedPrefabId = BuildCharacterPrefabId(characterId);
        if (persistentObject.ObjectKind == PersistentObjectKind.RuntimeSpawned &&
            string.Equals(persistentObject.PersistentId, expectedPersistentId, StringComparison.Ordinal) &&
            string.Equals(persistentObject.RuntimePrefabId ?? string.Empty, expectedPrefabId, StringComparison.Ordinal))
        {
            EnsureRuntimeNetworkHash(target, expectedPrefabId);
            return;
        }

        PersistentWorldDebug.Log(
            $"character persistent binding characterId='{characterId}' previousId='{persistentObject.PersistentId}' previousKind='{persistentObject.ObjectKind}' previousPrefab='{persistentObject.RuntimePrefabId}' nextId='{expectedPersistentId}' nextPrefab='{expectedPrefabId}' path='{PersistentWorldDebug.DescribeTransform(target.transform)}'",
            target);
        persistentObject.AssignRuntimeIdentity(expectedPersistentId, expectedPrefabId);
        EnsureRuntimeNetworkHash(target, expectedPrefabId);
    }

    public static void EnsureRuntimeBuildingInstance(BuildingInfoInteractable info, Item building, ulong networkId)
    {
        if (info == null || networkId == 0)
        {
            return;
        }

        string buildingItemId = building != null ? ItemIdUtils.GetItemId(building) : info.BuildingItemId;
        if (string.IsNullOrWhiteSpace(buildingItemId))
        {
            buildingItemId = info.BuildId;
        }

        PersistentNetworkObject persistentObject = EnsurePersistentObject(info.gameObject, false);
        if (persistentObject == null)
        {
            return;
        }

        string persistentId = BuildRuntimeBuildingPersistentId(networkId, buildingItemId);
        string prefabId = BuildBuildingPrefabId(buildingItemId);
        persistentObject.AssignRuntimeIdentity(
            persistentId,
            prefabId);

        persistentObject.SetDestroyIfMissingFromSnapshot(true);
        EnsureRuntimeNetworkHash(info.gameObject, persistentId);
        NetcodeRuntimeUtilities.GetOrAdd<PersistentBuildingState>(info.gameObject);
    }

    public static void EnsureRuntimeItemInstance(GameObject target, Item item, string persistentId, bool droppedLoot)
    {
        if (target == null || item == null || string.IsNullOrWhiteSpace(persistentId))
        {
            return;
        }

        string itemId = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId = item.name;
        }

        PersistentNetworkObject persistentObject = EnsurePersistentObject(target, false);
        if (persistentObject == null)
        {
            return;
        }

        persistentObject.AssignRuntimeIdentity(
            persistentId,
            droppedLoot ? BuildDroppedLootPrefabId(itemId) : BuildItemPrefabId(itemId));
        persistentObject.SetDestroyIfMissingFromSnapshot(true);

        if (droppedLoot)
        {
            InteractableItem lootContainer = target.GetComponent<InteractableItem>() ?? target.GetComponentInChildren<InteractableItem>(true);
            if (lootContainer != null && lootContainer.GetComponentInParent<BuildingInfoInteractable>(true) == null)
            {
                NetcodeRuntimeUtilities.GetOrAdd<PersistentContainerState>(lootContainer.gameObject);
            }
        }
    }

    public static void EnsureRuntimeKnowledgeManager(KnowledgeManager manager)
    {
        if (manager == null)
        {
            return;
        }

        PersistentNetworkObject persistentObject = EnsurePersistentSingletonObject(
            manager.gameObject,
            KnowledgeManagerPersistentId,
            nameof(KnowledgeManager));
        if (persistentObject == null)
        {
            return;
        }

        NetcodeRuntimeUtilities.GetOrAdd<PersistentKnowledgeState>(manager.gameObject);
    }

    public static int EnsureLiveManagedSingletons(UnityEngine.Object context, string reason)
    {
        int ensuredCount = 0;

#if UNITY_2023_1_OR_NEWER
        KnowledgeManager manager = KnowledgeManager.Instance != null
            ? KnowledgeManager.Instance
            : UnityEngine.Object.FindFirstObjectByType<KnowledgeManager>(FindObjectsInactive.Include);
#else
        KnowledgeManager manager = KnowledgeManager.Instance != null
            ? KnowledgeManager.Instance
            : UnityEngine.Object.FindObjectOfType<KnowledgeManager>(true);
#endif
        if (manager != null)
        {
            EnsureRuntimeKnowledgeManager(manager);
            ensuredCount++;
            PersistentWorldDebug.Log(
                $"live singleton prepared singleton='{nameof(KnowledgeManager)}' persistentId='{KnowledgeManagerPersistentId}' reason='{reason}' scene='{DescribeScene(manager.gameObject.scene)}' path='{PersistentWorldDebug.DescribeTransform(manager.transform)}'",
                manager);
        }

        return ensuredCount;
    }

    public static int NormalizeManagedSingletonSnapshots(WorldSnapshot snapshot, UnityEngine.Object context, string reason)
    {
        if (snapshot == null)
        {
            return 0;
        }

        if (snapshot.SceneObjects == null)
        {
            snapshot.SceneObjects = new System.Collections.Generic.List<PersistentObjectSnapshot>();
        }

        if (snapshot.RuntimeObjects == null)
        {
            snapshot.RuntimeObjects = new System.Collections.Generic.List<PersistentObjectSnapshot>();
        }

        int normalizedCount = 0;
        normalizedCount += NormalizeManagedSingletonSnapshotList(snapshot.SceneObjects, moveToSceneList: false, null, context, reason);
        normalizedCount += NormalizeManagedSingletonSnapshotList(snapshot.RuntimeObjects, moveToSceneList: true, snapshot.SceneObjects, context, reason);
        return normalizedCount;
    }

    public static int EnsureManagedSingletonsForSnapshot(WorldSnapshot snapshot, UnityEngine.Object context, string reason)
    {
        if (snapshot == null)
        {
            return 0;
        }

        int resolvedCount = 0;
        resolvedCount += EnsureManagedSingletonSnapshotList(snapshot.SceneObjects, context, reason);
        resolvedCount += EnsureManagedSingletonSnapshotList(snapshot.RuntimeObjects, context, reason);
        return resolvedCount;
    }

    public static bool TryPrepareManagedSingletonResolvedInstance(
        PersistentObjectSnapshot snapshot,
        PersistentNetworkObject persistentObject,
        UnityEngine.Object context,
        string reason)
    {
        if (persistentObject == null)
        {
            return false;
        }

        KnowledgeManager manager = persistentObject.GetComponent<KnowledgeManager>();
        if (manager != null)
        {
            EnsureRuntimeKnowledgeManager(manager);
            PersistentNetworkObject resolvedPersistentObject = manager.GetComponent<PersistentNetworkObject>();
            PersistentWorldDebug.LogSnapshotObjectAudit(
                reason,
                "runtime",
                snapshot,
                "resolved-in-place",
                context,
                resolvedPersistentObject,
                "singleton fallback guarded");
            return true;
        }

        return false;
    }

    public static bool TryResolveManagedSingletonPersistentObject(
        string persistentId,
        string runtimePrefabId,
        out PersistentNetworkObject persistentObject,
        UnityEngine.Object context,
        string reason)
    {
        persistentObject = null;
        if (!TryGetManagedSingletonDescriptor(persistentId, runtimePrefabId, out string resolvedPersistentId, out _, out string singletonName))
        {
            return false;
        }

        switch (resolvedPersistentId)
        {
            case KnowledgeManagerPersistentId:
            {
                KnowledgeManager manager = KnowledgeManager.GetOrCreate();
                EnsureRuntimeKnowledgeManager(manager);
                persistentObject = manager != null ? manager.GetComponent<PersistentNetworkObject>() : null;
                if (persistentObject == null)
                {
                    PersistentWorldDebug.Error(
                        $"singleton persistent object resolve failed singleton='{singletonName}' persistentId='{resolvedPersistentId}' reason='{reason}'",
                        context);
                    return true;
                }

                PersistentWorldDebug.Log(
                    $"singleton persistent object resolved in place singleton='{singletonName}' persistentId='{persistentObject.PersistentId}' reason='{reason}' scene='{DescribeScene(manager.gameObject.scene)}' path='{PersistentWorldDebug.DescribeTransform(manager.transform)}'",
                    persistentObject);
                return true;
            }
        }

        return false;
    }

    public static void PrepareRuntimeReconstructedObject(GameObject target, string runtimePrefabId, string persistentId)
    {
        if (target == null)
        {
            return;
        }

        PersistentNetworkObject persistentObject = EnsurePersistentObject(target, false);
        if (persistentObject == null)
        {
            return;
        }

        persistentObject.AssignRuntimeIdentity(persistentId, runtimePrefabId);

        if (target.GetComponent<BuildingInfoInteractable>() != null)
        {
            NetcodeRuntimeUtilities.GetOrAdd<PersistentBuildingState>(target);
        }

        if (target.GetComponent<TwoLeverPuzzle>() != null)
        {
            NetcodeRuntimeUtilities.GetOrAdd<PersistentPuzzleElementState>(target);
        }

        if (target.GetComponent<Brasero>() != null)
        {
            NetcodeRuntimeUtilities.GetOrAdd<PersistentBrazierState>(target);
        }

        if (target.GetComponent<InteractableItem>() != null && target.GetComponentInParent<BuildingInfoInteractable>(true) == null)
        {
            NetcodeRuntimeUtilities.GetOrAdd<PersistentContainerState>(target);
        }

        if (target.GetComponent<TrouEtroit>() != null)
        {
            NetcodeRuntimeUtilities.GetOrAdd<PersistentSecretPassageState>(target);
        }

        if (target.GetComponent<KnowledgeManager>() != null)
        {
            NetcodeRuntimeUtilities.GetOrAdd<PersistentKnowledgeState>(target);
        }
    }

    public static string BuildCharacterPersistentId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId)
            ? string.Empty
            : $"{CharacterPersistentPrefix}{characterId}";
    }

    public static string BuildCharacterPrefabId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId)
            ? string.Empty
            : $"{CharacterPrefabPrefix}{characterId}";
    }

    public static string BuildRuntimeBuildingPersistentId(ulong networkId, string buildingItemId)
    {
        return $"{BuildingPersistentPrefix}{buildingItemId}:{networkId}";
    }

    public static string BuildBuildingPrefabId(string buildingItemId)
    {
        return string.IsNullOrWhiteSpace(buildingItemId)
            ? string.Empty
            : $"{BuildingPrefabPrefix}{buildingItemId}";
    }

    public static string BuildItemPrefabId(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId)
            ? string.Empty
            : $"{ItemPrefabPrefix}{itemId}";
    }

    public static string BuildDroppedLootPrefabId(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId)
            ? string.Empty
            : $"{DroppedLootPrefabPrefix}{itemId}";
    }

    public static string DescribeExpectedResolutionMode(PersistentObjectSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return "unknown";
        }

        if (IsManagedSingletonSnapshot(snapshot))
        {
            return "singleton-normalized";
        }

        if (snapshot.ObjectKind == PersistentObjectKind.ScenePlaced)
        {
            return "scene-resolved";
        }

        string runtimePrefabId = snapshot.RuntimePrefabId ?? string.Empty;
        if (runtimePrefabId.StartsWith(CharacterPrefabPrefix, StringComparison.Ordinal))
        {
            return "resolved-in-place";
        }

        return "runtime-reconstructed";
    }

    public static bool IsManagedSingletonIdentity(string persistentId, string runtimePrefabId = null)
    {
        return TryGetManagedSingletonDescriptor(persistentId, runtimePrefabId, out _, out _, out _);
    }

    public static bool TryValidatePersistentIdentity(
        PersistentObjectKind objectKind,
        string persistentId,
        string runtimePrefabId,
        out string reason)
    {
        string resolvedPersistentId = persistentId ?? string.Empty;
        string resolvedRuntimePrefabId = runtimePrefabId ?? string.Empty;
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(resolvedPersistentId))
        {
            reason = "persistent ID is missing";
            return false;
        }

        if (objectKind == PersistentObjectKind.RuntimeSpawned)
        {
            if (!resolvedPersistentId.StartsWith("runtime:", StringComparison.Ordinal))
            {
                reason = "runtime-spawned object must use a runtime: persistent ID";
                return false;
            }

            if (string.IsNullOrWhiteSpace(resolvedRuntimePrefabId))
            {
                reason = "runtime-spawned object is missing its runtime prefab ID";
                return false;
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(resolvedRuntimePrefabId))
        {
            reason = "scene-placed object must not carry a runtime prefab ID";
            return false;
        }

        if (resolvedPersistentId.StartsWith("scene:", StringComparison.Ordinal))
        {
            return true;
        }

        if (resolvedPersistentId.StartsWith("runtime:", StringComparison.Ordinal) &&
            !IsManagedSingletonIdentity(resolvedPersistentId, resolvedRuntimePrefabId))
        {
            reason = "scene-placed object cannot use a runtime: persistent ID unless it is a managed singleton";
            return false;
        }

        return true;
    }

    private static void PrepareSceneBraziers(GameObject root)
    {
        Brasero[] braziers = root.GetComponentsInChildren<Brasero>(true);
        for (int i = 0; i < braziers.Length; i++)
        {
            Brasero brazier = braziers[i];
            if (brazier == null)
            {
                continue;
            }

            if (EnsurePersistentSceneObject(brazier.gameObject) == null)
            {
                continue;
            }

            NetcodeRuntimeUtilities.GetOrAdd<PersistentBrazierState>(brazier.gameObject);
        }
    }

    private static void PrepareSceneContainers(GameObject root)
    {
        InteractableItem[] containers = root.GetComponentsInChildren<InteractableItem>(true);
        for (int i = 0; i < containers.Length; i++)
        {
            InteractableItem container = containers[i];
            if (container == null)
            {
                continue;
            }

            if (container.GetComponentInParent<BuildingInfoInteractable>(true) != null)
            {
                continue;
            }

            if (EnsurePersistentSceneObject(container.gameObject) == null)
            {
                continue;
            }

            NetcodeRuntimeUtilities.GetOrAdd<PersistentContainerState>(container.gameObject);
        }
    }

    private static void PrepareScenePuzzles(GameObject root)
    {
        TwoLeverPuzzle[] puzzles = root.GetComponentsInChildren<TwoLeverPuzzle>(true);
        for (int i = 0; i < puzzles.Length; i++)
        {
            TwoLeverPuzzle puzzle = puzzles[i];
            if (puzzle == null)
            {
                continue;
            }

            if (EnsurePersistentSceneObject(puzzle.gameObject) == null)
            {
                continue;
            }

            NetcodeRuntimeUtilities.GetOrAdd<PersistentPuzzleElementState>(puzzle.gameObject);
        }

        ReadableSentencePuzzle[] readableSentencePuzzles = root.GetComponentsInChildren<ReadableSentencePuzzle>(true);
        for (int i = 0; i < readableSentencePuzzles.Length; i++)
        {
            ReadableSentencePuzzle puzzle = readableSentencePuzzles[i];
            if (puzzle == null)
            {
                continue;
            }

            if (EnsurePersistentSceneObject(puzzle.gameObject) == null)
            {
                continue;
            }

            NetcodeRuntimeUtilities.GetOrAdd<PersistentReadableSentencePuzzleState>(puzzle.gameObject);
        }
    }

    private static void PrepareSceneBuildings(GameObject root)
    {
        BuildingInfoInteractable[] buildings = root.GetComponentsInChildren<BuildingInfoInteractable>(true);
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingInfoInteractable building = buildings[i];
            if (building == null)
            {
                continue;
            }

            if (IsRuntimeBuiltBuilding(building))
            {
                if (building.NetworkBuildingId != 0)
                {
                    EnsureRuntimeBuildingInstance(building, building.BuildingItem, building.NetworkBuildingId);
                }

                PersistentWorldDebug.Log(
                    $"skip scene building persistence path='{PersistentWorldDebug.DescribeTransform(building.transform)}' reason='runtime_built_building' networkId={building.NetworkBuildingId}",
                    building);
                continue;
            }

            if (EnsurePersistentSceneObject(building.gameObject) == null)
            {
                continue;
            }

            NetcodeRuntimeUtilities.GetOrAdd<PersistentBuildingState>(building.gameObject);
        }
    }

    private static void PrepareSceneInteractables(GameObject root)
    {
        TrouEtroit[] passages = root.GetComponentsInChildren<TrouEtroit>(true);
        for (int i = 0; i < passages.Length; i++)
        {
            TrouEtroit passage = passages[i];
            if (passage == null)
            {
                continue;
            }

            if (EnsurePersistentSceneObject(passage.gameObject) == null)
            {
                continue;
            }

            NetcodeRuntimeUtilities.GetOrAdd<PersistentSecretPassageState>(passage.gameObject);
        }
    }

    private static void PrepareSceneCharacters(GameObject root)
    {
        SquadCharacterController[] characters = root.GetComponentsInChildren<SquadCharacterController>(true);
        for (int i = 0; i < characters.Length; i++)
        {
            SquadCharacterController controller = characters[i];
            if (controller == null)
            {
                continue;
            }

            if (IsNetworkManagedCharacter(controller.gameObject))
            {
                PersistentWorldDebug.Log(
                    $"skip scene character persistence path='{PersistentWorldDebug.DescribeTransform(controller.transform)}' reason='network_managed_character'",
                    controller);
                continue;
            }

            EnsurePersistentSceneObject(controller.gameObject);
        }
    }

    private static PersistentNetworkObject EnsurePersistentSceneObject(GameObject target)
    {
        PersistentNetworkObject persistentObject = EnsurePersistentObject(target, true);
        if (persistentObject == null)
        {
            return null;
        }

        persistentObject.AssignSceneIdentity(BuildScenePersistentId(target.transform));
        persistentObject.SetDestroyIfMissingFromSnapshot(false);
        return persistentObject;
    }

    private static PersistentNetworkObject EnsurePersistentSingletonObject(GameObject target, string persistentId, string singletonName)
    {
        PersistentNetworkObject persistentObject = EnsurePersistentObject(target, false);
        if (persistentObject == null)
        {
            return null;
        }

        string previousPersistentId = persistentObject.PersistentId;
        PersistentObjectKind previousKind = persistentObject.ObjectKind;
        string previousPrefabId = persistentObject.RuntimePrefabId ?? string.Empty;
        bool identityChanged =
            !string.Equals(previousPersistentId, persistentId, StringComparison.Ordinal) ||
            previousKind != PersistentObjectKind.ScenePlaced ||
            !string.IsNullOrWhiteSpace(previousPrefabId) ||
            persistentObject.DestroyIfMissingFromSnapshot;

        persistentObject.AssignSceneIdentity(persistentId);
        persistentObject.SetDestroyIfMissingFromSnapshot(false);

        if (identityChanged)
        {
            PersistentWorldDebug.Log(
                $"singleton persistent binding singleton='{singletonName}' persistentId='{persistentId}' previousId='{previousPersistentId}' previousKind='{previousKind}' previousPrefab='{previousPrefabId}' scene='{DescribeScene(target.scene)}' path='{PersistentWorldDebug.DescribeTransform(target.transform)}'",
                target);
        }

        return persistentObject;
    }

    private static PersistentNetworkObject EnsurePersistentObject(GameObject target, bool sceneObject)
    {
        if (target == null)
        {
            return null;
        }

        NetworkObject ownNetworkObject = target.GetComponent<NetworkObject>();
        NetworkObject parentNetworkObject = target.transform.parent != null
            ? target.transform.parent.GetComponentInParent<NetworkObject>(true)
            : null;

        if (ownNetworkObject == null && parentNetworkObject != null)
        {
            PersistentWorldDebug.Log($"Skipping persistent install on '{target.name}' because it is nested under NetworkObject '{parentNetworkObject.name}'.", target);
            return null;
        }

        if (ownNetworkObject == null)
        {
            ownNetworkObject = NetcodeRuntimeUtilities.GetOrAdd<NetworkObject>(target);
        }

        if (sceneObject)
        {
            NetcodeRuntimeUtilities.EnsureSceneObjectHash(ownNetworkObject, NetcodeSceneIdUtility.GetStableId(target.transform));
        }
        else
        {
            uint hash = NetcodeStableHash.Hash32(target.name);
            NetcodeRuntimeUtilities.EnsureNetworkObjectHash(ownNetworkObject, hash);
        }

        return NetcodeRuntimeUtilities.GetOrAdd<PersistentNetworkObject>(target);
    }

    private static void EnsureRuntimeNetworkHash(GameObject target, string hashKey)
    {
        if (target == null)
        {
            return;
        }

        NetworkObject networkObject = target.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            return;
        }

        uint hash = NetcodeStableHash.Hash32(string.IsNullOrWhiteSpace(hashKey) ? target.name : hashKey);
        NetcodeRuntimeUtilities.EnsureNetworkObjectHash(networkObject, hash);
    }

    private static string BuildScenePersistentId(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        Scene scene = target.gameObject.scene;
        string sceneName = scene.IsValid() ? scene.name : "NoScene";
        uint stableId = NetcodeSceneIdUtility.GetStableId(target);
        return $"scene:{sceneName}:{stableId:X8}";
    }

    private static int NormalizeManagedSingletonSnapshotList(
        System.Collections.Generic.List<PersistentObjectSnapshot> sourceList,
        bool moveToSceneList,
        System.Collections.Generic.List<PersistentObjectSnapshot> sceneList,
        UnityEngine.Object context,
        string reason)
    {
        if (sourceList == null)
        {
            return 0;
        }

        int normalizedCount = 0;
        for (int i = sourceList.Count - 1; i >= 0; i--)
        {
            PersistentObjectSnapshot snapshot = sourceList[i];
            if (!IsManagedSingletonSnapshot(snapshot))
            {
                continue;
            }

            bool changed = TryNormalizeManagedSingletonSnapshot(snapshot, context, reason, out _);
            bool counted = changed;
            if (counted)
            {
                normalizedCount++;
            }

            if (!moveToSceneList)
            {
                continue;
            }

            sourceList.RemoveAt(i);
            if (sceneList == null)
            {
                continue;
            }

            bool duplicateExists = false;
            for (int sceneIndex = 0; sceneIndex < sceneList.Count; sceneIndex++)
            {
                PersistentObjectSnapshot sceneSnapshot = sceneList[sceneIndex];
                if (sceneSnapshot == null)
                {
                    continue;
                }

                if (!string.Equals(sceneSnapshot.PersistentId, snapshot.PersistentId, StringComparison.Ordinal))
                {
                    continue;
                }

                duplicateExists = true;
                PersistentWorldDebug.Warn(
                    $"singleton snapshot duplicate after normalization singletonId='{snapshot.PersistentId}' reason='{reason}'",
                    context);
                break;
            }

            if (!duplicateExists)
            {
                sceneList.Add(snapshot);
            }

            if (!counted)
            {
                normalizedCount++;
            }
        }

        return normalizedCount;
    }

    private static bool TryNormalizeManagedSingletonSnapshot(
        PersistentObjectSnapshot snapshot,
        UnityEngine.Object context,
        string reason,
        out bool movedToScene)
    {
        movedToScene = false;
        if (snapshot == null ||
            !TryGetManagedSingletonDescriptor(snapshot.PersistentId, snapshot.RuntimePrefabId, out string resolvedPersistentId, out string previousPrefabIdForLookup, out string singletonName))
        {
            return false;
        }

        string previousPersistentId = snapshot.PersistentId ?? string.Empty;
        PersistentObjectKind previousKind = snapshot.ObjectKind;
        string previousPrefabId = snapshot.RuntimePrefabId ?? string.Empty;
        bool changed =
            !string.Equals(previousPersistentId, resolvedPersistentId, StringComparison.Ordinal) ||
            previousKind != PersistentObjectKind.ScenePlaced ||
            !string.IsNullOrWhiteSpace(previousPrefabId) ||
            snapshot.DestroyIfMissing;

        snapshot.PersistentId = resolvedPersistentId;
        snapshot.ObjectKind = PersistentObjectKind.ScenePlaced;
        snapshot.RuntimePrefabId = string.Empty;
        snapshot.DestroyIfMissing = false;
        movedToScene = previousKind != PersistentObjectKind.ScenePlaced;

        if (changed)
        {
            PersistentWorldDebug.Log(
                $"singleton snapshot normalized singleton='{singletonName}' persistentId='{resolvedPersistentId}' previousId='{previousPersistentId}' previousKind='{previousKind}' previousPrefab='{previousPrefabId}' previousLookupPrefab='{previousPrefabIdForLookup}' reason='{reason}'",
                context);
        }

        return changed;
    }

    private static int EnsureManagedSingletonSnapshotList(
        System.Collections.Generic.List<PersistentObjectSnapshot> snapshots,
        UnityEngine.Object context,
        string reason)
    {
        if (snapshots == null)
        {
            return 0;
        }

        int resolvedCount = 0;
        for (int i = 0; i < snapshots.Count; i++)
        {
            PersistentObjectSnapshot snapshot = snapshots[i];
            if (snapshot == null)
            {
                continue;
            }

            if (!TryResolveManagedSingletonPersistentObject(snapshot.PersistentId, snapshot.RuntimePrefabId, out _, context, reason))
            {
                continue;
            }

            resolvedCount++;
        }

        return resolvedCount;
    }

    private static bool TryGetManagedSingletonDescriptor(
        string persistentId,
        string runtimePrefabId,
        out string resolvedPersistentId,
        out string previousPrefabIdForLookup,
        out string singletonName)
    {
        if (string.Equals(persistentId, KnowledgeManagerPersistentId, StringComparison.Ordinal) ||
            string.Equals(runtimePrefabId, KnowledgeManagerPrefabId, StringComparison.Ordinal))
        {
            resolvedPersistentId = KnowledgeManagerPersistentId;
            previousPrefabIdForLookup = KnowledgeManagerPrefabId;
            singletonName = nameof(KnowledgeManager);
            return true;
        }

        resolvedPersistentId = string.Empty;
        previousPrefabIdForLookup = string.Empty;
        singletonName = string.Empty;
        return false;
    }

    public static bool IsManagedSingletonSnapshot(PersistentObjectSnapshot snapshot)
    {
        return snapshot != null &&
               IsManagedSingletonIdentity(snapshot.PersistentId, snapshot.RuntimePrefabId);
    }

    private static bool IsRuntimeBuiltBuilding(BuildingInfoInteractable building)
    {
        if (building == null)
        {
            return false;
        }

        if (building.NetworkBuildingId != 0)
        {
            return true;
        }

        PersistentNetworkObject persistentObject = building.GetComponent<PersistentNetworkObject>();
        return persistentObject != null &&
               persistentObject.ObjectKind == PersistentObjectKind.RuntimeSpawned &&
               !string.IsNullOrWhiteSpace(persistentObject.RuntimePrefabId) &&
               persistentObject.RuntimePrefabId.StartsWith(BuildingPrefabPrefix, StringComparison.Ordinal);
    }

    private static bool IsNetworkManagedCharacter(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        return target.GetComponent<NetcodeCharacterIdentity>() != null ||
               target.GetComponent<NetcodeLocalPlayer>() != null ||
               target.GetComponent<NetworkCharacterInput>() != null ||
               target.GetComponent<NetworkInventory>() != null;
    }

    private static string DescribeScene(Scene scene)
    {
        return scene.IsValid() ? scene.name : "NoScene";
    }
}
