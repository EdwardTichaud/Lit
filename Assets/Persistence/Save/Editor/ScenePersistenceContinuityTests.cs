#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ScenePersistenceContinuityTests
{
    private GameObject services;
    private WorldStateManager world;
    private NetworkObjectRegistry registry;
    private Scene zone;
    private string sceneName;
    private string temporaryBaselinePath;

    [SetUp]
    public void SetUp()
    {
        Scene active = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(active.path))
        {
            Assert.That(active.isDirty, Is.False, "Never discard an edited user scene for a test fixture.");
            // Unity's Test Runner starts with its unsaved default camera/light
            // scene. Replace this clean temporary scene before creating the
            // saved baseline required by additive Editor scene APIs.
            active = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            temporaryBaselinePath = "Assets/__LitArchiveBaseline_" + Guid.NewGuid().ToString("N") + ".unity";
            Assert.That(EditorSceneManager.SaveScene(active, temporaryBaselinePath), Is.True);
        }
        services = new GameObject("Archive test services");
        services.SetActive(false);
        registry = services.AddComponent<NetworkObjectRegistry>();
        var spawner = services.AddComponent<SpawnManager>();
        world = services.AddComponent<WorldStateManager>();
        SetField(world, "registry", registry);
        SetField(world, "spawnManager", spawner);
        sceneName = "LitArchive_" + Guid.NewGuid().ToString("N");
        zone = CreateZone();
    }

    [TearDown]
    public void TearDown()
    {
        if (zone.IsValid() && zone.isLoaded) EditorSceneManager.CloseScene(zone, true);
        if (services != null) Object.DestroyImmediate(services);
        if (!string.IsNullOrEmpty(temporaryBaselinePath))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(temporaryBaselinePath);
            temporaryBaselinePath = null;
        }
    }

    [Test]
    public void SceneReturnRestoresPayloadAndPoseWithoutRemovingPersistentSessionObjects()
    {
        var actor = CreateSceneObject("scene:archive:container");
        var state = actor.gameObject.AddComponent<SceneArchiveTestState>();
        state.Value = 17;
        actor.transform.position = new Vector3(5, 2, 3);
        Assert.That(world.CaptureScenesBeforeUnload(new[] { sceneName }), Is.True);
        EditorSceneManager.CloseScene(zone, true);
        zone = CreateZone();
        actor = CreateSceneObject("scene:archive:container");
        state = actor.gameObject.AddComponent<SceneArchiveTestState>();
        state.Value = 0;
        var squad = new GameObject("Persistent squad sentinel");
        squad.SetActive(false);
        var persistent = squad.AddComponent<PersistentNetworkObject>();
        persistent.AssignRuntimeIdentity("runtime:test:squad", "character:test");
        try
        {
            Assert.That(world.RestoreArchivedScenes(new[] { sceneName }), Is.True);
            Assert.That(state.Value, Is.EqualTo(17));
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(5, 2, 3)));
            Assert.That(squad != null, Is.True);
            Assert.That(persistent.PersistentId, Is.EqualTo("runtime:test:squad"));
        }
        finally { if (squad != null) Object.DestroyImmediate(squad); }
    }

    [Test]
    public void CollectedSceneItemDoesNotReappearAfterItsSceneIsRecreated()
    {
        var item = CreateSceneObject("scene:archive:collected-item");
        world.RecordSceneObjectRemoved(item);
        Object.DestroyImmediate(item.gameObject);
        world.CaptureScenesBeforeUnload(new[] { sceneName });
        EditorSceneManager.CloseScene(zone, true);
        zone = CreateZone();
        item = CreateSceneObject("scene:archive:collected-item");
        item.gameObject.SetActive(true);
        Assert.That(world.RestoreArchivedScenes(new[] { sceneName }), Is.True);
        Assert.That(item.gameObject.activeSelf, Is.False);
        Assert.That(world.RestoreArchivedScenes(new[] { sceneName }), Is.True);
        Assert.That(item.gameObject.activeSelf, Is.False, "Restoration must remain idempotent.");
    }

    [Test]
    public void SessionResetForgetsPriorCampaignSceneState()
    {
        var actor = CreateSceneObject("scene:archive:state");
        var state = actor.gameObject.AddComponent<SceneArchiveTestState>();
        state.Value = 17;
        world.CaptureScenesBeforeUnload(new[] { sceneName });
        world.ResetSceneArchives();
        state.Value = 42;
        Assert.That(world.RestoreArchivedScenes(new[] { sceneName }), Is.True);
        Assert.That(state.Value, Is.EqualTo(42));
    }

    [Test]
    public void ExplicitlyDisabledProviderRemainsExcludedOnAnInactiveObject()
    {
        var actor = CreateSceneObject("scene:archive:disabled-provider");
        var state = actor.gameObject.AddComponent<SceneArchiveTestState>();
        state.enabled = false;
        Assert.That(actor.CaptureSnapshot(null).StateBlobs, Is.Empty);
    }

    [Test]
    public void InactiveContainerResolvesItsStateWithoutDependingOnAwake()
    {
        var actor = CreateSceneObject("scene:archive:inactive-container");
        var container = actor.gameObject.AddComponent<InteractableItem>();
        container.isLocked = true;
        actor.gameObject.AddComponent<PersistentContainerState>();
        var captured = actor.CaptureSnapshot(null);
        Assert.That(captured.StateBlobs.Count, Is.EqualTo(1));
        container.isLocked = false;
        Assert.That(actor.ApplyProviderStates(captured, PersistentApplyPhase.ApplyGameplayState, null), Is.True);
        Assert.That(container.isLocked, Is.True);
        Assert.That(actor.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void SerializedArchiveIsImportedWithoutInstantiatingTheUnloadedZone()
    {
        var actor = CreateSceneObject("scene:archive:saved-state");
        var state = actor.gameObject.AddComponent<SceneArchiveTestState>();
        state.Value = 23;
        world.CaptureScenesBeforeUnload(new[] { sceneName });
        var archives = (Dictionary<string, PersistentSceneArchive>)typeof(WorldStateManager)
            .GetField("sceneArchives", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world);
        var saved = new WorldSnapshot { SceneName = SceneManager.GetActiveScene().name };
        saved.SceneArchives.Add(archives[sceneName]);
        var serializer = new SnapshotSerializer();
        byte[] payload = serializer.Serialize(saved);
        EditorSceneManager.CloseScene(zone, true);
        world.ResetSceneArchives();
        Assert.That(world.ApplySnapshot(serializer.Deserialize(payload), true), Is.True);
        Assert.That(SceneManager.GetSceneByName(sceneName).IsValid(), Is.False,
            "An archived zone is data, not a request to load its scenes.");
        zone = CreateZone();
        actor = CreateSceneObject("scene:archive:saved-state");
        state = actor.gameObject.AddComponent<SceneArchiveTestState>();
        Assert.That(world.RestoreArchivedScenes(new[] { sceneName }), Is.True);
        Assert.That(state.Value, Is.EqualTo(23));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void SnapshotFormatKeepsLegacyCompatibilityAndRoundTripsUnloadedScenes(int version)
    {
        var snapshot = new WorldSnapshot { SchemaVersion = version, SceneName = "Maison_Core" };
        snapshot.SceneArchives.Add(new PersistentSceneArchive
        {
            SceneName = "District_1_Enemies",
            SceneObjects = new List<PersistentObjectSnapshot>
            {
                new PersistentObjectSnapshot { PersistentId = "scene:test:enemy", SceneName = "District_1_Enemies",
                    StateBlobs = new List<StateBlobSnapshot> { new StateBlobSnapshot { ProviderId = "health", Payload = new byte[] { 0, 17 } } } }
            },
            RemovedSceneObjectIds = new List<string> { "scene:test:collected-item" }
        });
        var serializer = new SnapshotSerializer();
        var restored = serializer.Deserialize(serializer.Serialize(snapshot));
        Assert.That(restored.SchemaVersion, Is.EqualTo(version));
        Assert.That(restored.SceneName, Is.EqualTo("Maison_Core"));
        Assert.That(restored.SceneArchives.Count, Is.EqualTo(version >= 3 ? 1 : 0));
        if (version < 3) return;
        Assert.That(restored.SceneArchives[0].SceneObjects[0].StateBlobs[0].Payload, Is.EqualTo(new byte[] { 0, 17 }));
        Assert.That(restored.SceneArchives[0].RemovedSceneObjectIds, Is.EqualTo(new[] { "scene:test:collected-item" }));
    }

    [Test]
    public void RuntimeIdsInUnloadedScenesCannotBeReissuedAfterLoadingTheSave()
    {
        var saved = new WorldSnapshot { SceneName = SceneManager.GetActiveScene().name };
        saved.SceneArchives.Add(new PersistentSceneArchive
        {
            SceneName = "UnloadedZone",
            RuntimeObjects = new List<PersistentObjectSnapshot>
            {
                new PersistentObjectSnapshot { PersistentId = "runtime:loot:item:1", RuntimePrefabId = "item:item",
                    ObjectKind = PersistentObjectKind.RuntimeSpawned, SceneName = "UnloadedZone" }
            }
        });
        Assert.That(world.ApplySnapshot(saved, true), Is.True);
        var spawner = services.GetComponent<SpawnManager>();
        Assert.That(spawner.AllocatePersistentId("loot", "item"), Is.EqualTo("runtime:loot:item:2"));
        Assert.That(SceneManager.GetSceneByName("UnloadedZone").IsValid(), Is.False);
    }

    [Test]
    public void LateJoinExportsTheCurrentWorldWithoutResurrectingConsumedSavedRuntimeObjects()
    {
        var saved = new WorldSnapshot();
        saved.RuntimeObjects.Add(RuntimeSnapshot("runtime:consumed", "item:old"));
        var adapter = LateJoinAdapter(saved);
        Assert.That(adapter.ValidatePostLoadLateJoinSnapshot(new WorldSnapshot(), 7), Is.True);
    }

    [Test]
    public void LateJoinKeepsUnloadedRuntimeArchivesAsData()
    {
        var saved = new WorldSnapshot();
        saved.RuntimeObjects.Add(RuntimeSnapshot("runtime:archived", "item:old"));
        var outgoing = new WorldSnapshot();
        outgoing.SceneArchives.Add(new PersistentSceneArchive
        {
            SceneName = "UnloadedZone",
            RuntimeObjects = new List<PersistentObjectSnapshot> { RuntimeSnapshot("runtime:archived", "item:old") }
        });
        Assert.That(LateJoinAdapter(saved).ValidatePostLoadLateJoinSnapshot(outgoing, 7), Is.True);
        Assert.That(SceneManager.GetSceneByName("UnloadedZone").IsValid(), Is.False);
    }

    [Test]
    public void LateJoinAcceptsNewIdsCreatedAfterTheDiskRestore()
    {
        var saved = new WorldSnapshot();
        saved.RuntimeObjects.Add(RuntimeSnapshot("runtime:consumed", "item:old"));
        var outgoing = new WorldSnapshot();
        outgoing.RuntimeObjects.Add(RuntimeSnapshot("runtime:new", "item:new"));
        Assert.That(LateJoinAdapter(saved).ValidatePostLoadLateJoinSnapshot(outgoing, 7), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LateJoinRejectsReusingAnExistingIdForAnotherPrefab(bool archived)
    {
        var saved = new WorldSnapshot();
        var original = RuntimeSnapshot("runtime:immutable", "item:old");
        if (archived)
            saved.SceneArchives.Add(new PersistentSceneArchive
            {
                SceneName = "UnloadedZone", RuntimeObjects = new List<PersistentObjectSnapshot> { original }
            });
        else saved.RuntimeObjects.Add(original);
        var outgoing = new WorldSnapshot();
        outgoing.RuntimeObjects.Add(RuntimeSnapshot("runtime:immutable", "item:other"));
        LogAssert.Expect(LogType.Error, new Regex("runtime identity continuity failed.*actualPrefab='item:other'"));
        LogAssert.Expect(LogType.Error, new Regex("post-load late-join snapshot validation.*success=False"));
        Assert.That(LateJoinAdapter(saved).ValidatePostLoadLateJoinSnapshot(outgoing, 7), Is.False);
    }

    [Test]
    public void LateJoinRejectsAMissingOutgoingSnapshot()
    {
        LogAssert.Expect(LogType.Error, new Regex("late-join snapshot missing clientId=7"));
        Assert.That(LateJoinAdapter(new WorldSnapshot()).ValidatePostLoadLateJoinSnapshot(null, 7), Is.False);
    }

    [Test]
    public void ImmediateDiskRestoreStillRequiresEverySavedRuntimeRoot()
    {
        var saved = new WorldSnapshot();
        saved.RuntimeObjects.Add(RuntimeSnapshot("runtime:required", "item:old"));
        LogAssert.Expect(LogType.Error, new Regex("runtime identity continuity failed.*reason='missing_after_restore'"));
        var issues = (int)typeof(WorldSaveAdapter).GetMethod("ValidateRuntimeIdentityContinuity",
            BindingFlags.NonPublic | BindingFlags.Instance).Invoke(LateJoinAdapter(saved), new object[]
            { saved.RuntimeObjects, new List<PersistentObjectSnapshot>(), "immediate_restore_test", true });
        Assert.That(issues, Is.EqualTo(1));
    }

    [Test]
    public void ExportStructureValidationRejectsAnArchiveWithWrongSceneOwnershipWithoutApplyingIt()
    {
        var snapshot = new WorldSnapshot();
        snapshot.SceneArchives.Add(new PersistentSceneArchive
        {
            SceneName = "UnloadedZone",
            RuntimeObjects = new List<PersistentObjectSnapshot>
            {
                RuntimeSnapshot("runtime:foreign", "item:old", "DifferentZone")
            }
        });
        var result = ValidateExportStructure(snapshot);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(world.LastApplyResult, Is.Null);
        Assert.That(SceneManager.GetSceneByName("UnloadedZone").IsValid(), Is.False);
    }

    [Test]
    public void ExportStructureValidationRejectsDuplicateLiveIdentities()
    {
        var snapshot = new WorldSnapshot();
        snapshot.RuntimeObjects.Add(RuntimeSnapshot("runtime:duplicate", "item:old"));
        snapshot.RuntimeObjects.Add(RuntimeSnapshot("runtime:duplicate", "item:old"));
        LogAssert.Expect(LogType.Error, new Regex("duplicate snapshot persistent id detected id='runtime:duplicate'"));
        Assert.That(ValidateExportStructure(snapshot).Succeeded, Is.False);
        Assert.That(world.LastApplyResult, Is.Null);
    }

    private SnapshotApplyResult ValidateExportStructure(WorldSnapshot snapshot) =>
        (SnapshotApplyResult)typeof(WorldStateManager).GetMethod("ValidateSnapshotStructure",
            BindingFlags.NonPublic | BindingFlags.Instance).Invoke(world, new object[] { snapshot });

    [Test]
    public void SoloDroppedLootReplacesPrefabSceneIdentityAndCapturesItsQuantity()
    {
        WithLocalDropFixture((item, container, allocator) =>
        {
            PersistentWorldSceneInstaller.EnsureLocalDroppedItemInstance(container, item);
            var persistent = container.GetComponent<PersistentNetworkObject>();
            Assert.That(persistent.ObjectKind, Is.EqualTo(PersistentObjectKind.RuntimeSpawned));
            Assert.That(persistent.PersistentId, Is.EqualTo("runtime:dropped-loot:archive_test_drop:1"));
            Assert.That(persistent.RuntimePrefabId, Is.EqualTo("itemdrop:archive_test_drop"));
            var snapshot = persistent.CaptureSnapshot(null);
            Assert.That(snapshot.StateBlobs.Count, Is.EqualTo(1));
            Assert.That(snapshot.StateBlobs[0].ProviderId, Is.EqualTo("container"));
            StringAssert.Contains("\"Quantity\":3", System.Text.Encoding.UTF8.GetString(snapshot.StateBlobs[0].Payload));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RuntimeItemPersistencePreservesTheRegisteredNetworkPrefabHash(bool droppedLoot)
    {
        WithLocalDropFixture((item, container, allocator) =>
        {
            var networkObject = container.GetComponent<Unity.Netcode.NetworkObject>();
            uint registeredHash = NetcodeStableHash.Hash32("item:" + item.itemId + (droppedLoot ? ":loot" : ":plain"));
            NetcodeRuntimeUtilities.EnsureNetworkObjectHash(networkObject, registeredHash);
            string id = "runtime:test:item:1";
            PersistentWorldSceneInstaller.EnsureRuntimeItemInstance(container.gameObject, item, id, droppedLoot);
            var persistent = container.GetComponent<PersistentNetworkObject>();
            PersistentWorldSceneInstaller.PrepareRuntimeReconstructedObject(container.gameObject, persistent.RuntimePrefabId, id);
            container.name = "A differently named runtime clone";
            PersistentWorldSceneInstaller.EnsureRuntimeItemInstance(container.gameObject, item, id, droppedLoot);
            foreach (string fieldName in new[] { "GlobalObjectIdHash", "PrefabGlobalObjectIdHash" })
            {
                uint actual = (uint)typeof(Unity.Netcode.NetworkObject).GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(networkObject);
                Assert.That(actual, Is.EqualTo(registeredHash), fieldName + " must retain the registered factory identity.");
            }
            Assert.That(persistent.PersistentId, Is.EqualTo(id));
        });
    }

    [TestCase(false, true)]
    [TestCase(true, true)]
    [TestCase(false, false)]
    public void NetworkItemFactoryInstallsContainerProviderBeforeReceivingSnapshot(bool fallback, bool loot)
    {
        WithLocalDropFixture((item, source, allocator) =>
        {
            var infoType = typeof(NetcodePrefabRegistry).GetNestedType("ItemSpawnInfo", BindingFlags.NonPublic);
            object info = Activator.CreateInstance(infoType, true);
            infoType.GetField("item").SetValue(info, item);
            infoType.GetField("sourcePrefab").SetValue(info, fallback ? null : source.gameObject);
            infoType.GetField("plainHash").SetValue(info, 123u);
            infoType.GetField("lootHash").SetValue(info, 456u);
            GameObject clone = null;
            try
            {
                clone = (GameObject)typeof(NetcodePrefabRegistry).GetMethod("CreateItemInstance",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { info, loot, Vector3.zero, Quaternion.identity, null });
                Assert.That(clone, Is.Not.Null);
                Assert.That(clone.GetComponents<PersistentContainerState>().Length, Is.EqualTo(loot ? 1 : 0));
                var persistent = clone.GetComponent<PersistentNetworkObject>();
                Assert.That(persistent, Is.Not.Null);
                // Editor OnValidate can assign a scene GlobalObjectId to the
                // active fallback primitive; this is not a runtime allocation.
                if (!fallback) Assert.That(persistent.PersistentId, Is.EqualTo(source.GetComponent<PersistentNetworkObject>().PersistentId));
                Assert.That(persistent.IsRuntimeObject, Is.False, "The factory must not allocate a server runtime identity.");
                if (loot)
                {
                    WorldPickupUtility.ConfigureLootContainer(clone.GetComponent<InteractableItem>(), item, 3, true);
                    var snapshot = persistent.CaptureSnapshot(null);
                    Assert.That(snapshot.StateBlobs.Count, Is.EqualTo(1));
                    Assert.That(snapshot.StateBlobs[0].ProviderId, Is.EqualTo("container"));
                    StringAssert.Contains("\"Quantity\":3", System.Text.Encoding.UTF8.GetString(snapshot.StateBlobs[0].Payload));
                }
            }
            finally { if (clone != null) Object.DestroyImmediate(clone); }
        });
    }

    [Test]
    public void ReconfiguringSoloLootDoesNotAllocateAnotherIdentityOrResetPartialQuantity()
    {
        WithLocalDropFixture((item, container, allocator) =>
        {
            PersistentWorldSceneInstaller.EnsureLocalDroppedItemInstance(container, item);
            string id = container.GetComponent<PersistentNetworkObject>().PersistentId;
            Assert.That(container.RemoveItems(item, 1), Is.EqualTo(1));
            PersistentWorldSceneInstaller.EnsureLocalDroppedItemInstance(container, item);
            var persistent = container.GetComponent<PersistentNetworkObject>();
            Assert.That(persistent.PersistentId, Is.EqualTo(id));
            Assert.That(container.GetComponents<PersistentContainerState>().Length, Is.EqualTo(1));
            Assert.That(allocator.AllocatePersistentId("dropped-loot", item.itemId), Is.EqualTo("runtime:dropped-loot:archive_test_drop:2"));
            StringAssert.Contains("\"Quantity\":2", System.Text.Encoding.UTF8.GetString(persistent.CaptureSnapshot(null).StateBlobs[0].Payload));
        });
    }

    [Test]
    public void SeparateSoloDropsNeverReuseTheirPrefabSceneId()
    {
        WithLocalDropFixture((item, first, allocator) =>
        {
            var secondRoot = CreateSceneObject("scene:prefab:second-copy");
            var second = secondRoot.gameObject.AddComponent<InteractableItem>();
            PersistentWorldSceneInstaller.EnsureLocalDroppedItemInstance(first, item);
            PersistentWorldSceneInstaller.EnsureLocalDroppedItemInstance(second, item);
            Assert.That(first.GetComponent<PersistentNetworkObject>().PersistentId, Is.EqualTo("runtime:dropped-loot:archive_test_drop:1"));
            Assert.That(secondRoot.PersistentId, Is.EqualTo("runtime:dropped-loot:archive_test_drop:2"));
        });
    }

    [Test]
    public void ScenePickupWithPresentationInfoCapturesItsContainerQuantity()
    {
        WithLocalDropFixture((item, container, allocator) =>
        {
            PersistentWorldSceneInstaller.PrepareScene(zone);
            Assert.That(container.GetComponent<PersistentContainerState>(), Is.Not.Null);
            var snapshot = container.GetComponent<PersistentNetworkObject>().CaptureSnapshot(null);
            Assert.That(snapshot.ObjectKind, Is.EqualTo(PersistentObjectKind.ScenePlaced));
            StringAssert.Contains("\"Quantity\":3", System.Text.Encoding.UTF8.GetString(snapshot.StateBlobs[0].Payload));
        });
    }

    [Test]
    public void ReconstructedPickupWithPresentationInfoCapturesItsContainerQuantity()
    {
        WithLocalDropFixture((item, container, allocator) =>
        {
            PersistentWorldSceneInstaller.PrepareRuntimeReconstructedObject(container.gameObject,
                PersistentWorldSceneInstaller.BuildDroppedLootPrefabId(item.itemId), "runtime:drop:restored");
            Assert.That(container.GetComponent<PersistentContainerState>(), Is.Not.Null);
            var snapshot = container.GetComponent<PersistentNetworkObject>().CaptureSnapshot(null);
            Assert.That(snapshot.PersistentId, Is.EqualTo("runtime:drop:restored"));
            StringAssert.Contains("\"Quantity\":3", System.Text.Encoding.UTF8.GetString(snapshot.StateBlobs[0].Payload));
        });
    }

    [Test]
    public void PrefabAssetsNeverRegisterAsLiveWorldObjects()
    {
        string path = "Assets/__LitPersistencePrefab_" + Guid.NewGuid().ToString("N") + ".prefab";
        var source = CreateSceneObject("scene:asset-not-live");
        try
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(source.gameObject, path);
            Assert.That(prefab, Is.Not.Null);
            var asset = prefab.GetComponent<PersistentNetworkObject>();
            Assert.That(asset.gameObject.scene.IsValid(), Is.False);
            registry.Register(asset);
            Assert.That(registry.TryGet("scene:asset-not-live", out _), Is.False);
            Assert.That(registry.GetAllObjects().Contains(asset), Is.False);
            registry.Register(source);
            Assert.That(registry.TryGet("scene:asset-not-live", out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(source));
        }
        finally { AssetDatabase.DeleteAsset(path); }
    }

    [Test]
    public void AuthoredLootSlotResolvesItsOwnScriptInsteadOfAWorldContainer()
    {
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/LootSlotUI.cs");
        Assert.That(script, Is.Not.Null);
        Assert.That(script.GetClass(), Is.EqualTo(typeof(LootSlotUI)));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Slots/UI_Slot_InventorySlot_2.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<LootSlotUI>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<InteractableItem>(), Is.Null,
            "A displayed inventory/loot slot must not create a phantom world container.");
    }

    [Test]
    public void ExitSaveWithoutAStoreReportsFailureAndKeepsTheSession()
    {
        var flow = services.AddComponent<GameFlowService>();
        var sessionObject = new GameObject("Inactive continuity session");
        sessionObject.SetActive(false);
        SceneManager.MoveGameObjectToScene(sessionObject, zone);
        var session = sessionObject.AddComponent<GameplaySessionRoot>();
        SetField(flow, "gameplaySessionRoot", session);
        var singleton = typeof(CharacterStateStore).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = singleton.GetValue(null);
        singleton.SetValue(null, null);
        try
        {
            bool? saved = null;
            LogAssert.Expect(LogType.Warning, "[GameFlow] Sauvegarde de sortie impossible : CharacterStateStore absent.");
            var routine = (System.Collections.IEnumerator)typeof(GameFlowService).GetMethod("SaveBeforeReturningToMenuRoutine",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow,
                new object[] { null, new Action<bool>(success => saved = success) });
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(saved, Is.EqualTo(false));
            Assert.That(flow.HasGameplaySession, Is.True);
        }
        finally { singleton.SetValue(null, previous); Object.DestroyImmediate(sessionObject); }
    }

    [Test]
    public void EmptyInterruptedPostLoadingQueueCompletesWithoutChangingTheZone()
    {
        var flow = services.AddComponent<GameFlowService>();
        bool? ready = null;
        var routine = (System.Collections.IEnumerator)typeof(GameFlowService).GetMethod("FinishPostLoadingRequests",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow,
            new object[] { true, new Action<bool>(success => ready = success) });
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(ready, Is.EqualTo(true));
        Assert.That(zone.isLoaded, Is.True);
    }

    [Test]
    public void ClientSessionHooksLeaveSoloSessionOwnershipUnchanged()
    {
        var flow = services.AddComponent<GameFlowService>();
        foreach (string method in new[] { "EnsureSynchronizedClientSession", "FinishSynchronizedClientWorld" })
            Assert.That((bool)typeof(GameFlowService).GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, new object[] { sceneName }), Is.True);
        Assert.That(flow.HasGameplaySession, Is.False);
    }

    [TestCase("Lucian/Player_Model_Lucian")]
    [TestCase("Vesta/Vesta")]
    [TestCase("Soren/Soren")]
    [TestCase("Aurell/Aurell")]
    public void AuthoredPlayersKeepOneServerAuthoritativeUccTransform(string prefab)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/1_Squad/" + prefab + ".prefab");
        Assert.That(root, Is.Not.Null);
        var transforms = root.GetComponents<Unity.Netcode.Components.NetworkTransform>();
        Assert.That(transforms.Length, Is.EqualTo(1));
        Assert.That(transforms[0], Is.TypeOf<ServerUccNetworkTransform>());
        Assert.That(transforms[0].IsServerAuthoritative(), Is.True);
    }

    [Test]
    public void UccNetworkTransformDoesNotApplyReplicationOutsideASpawnedSession()
    {
        var root = new GameObject("Offline pose guard");
        root.SetActive(false);
        try
        {
            var sync = root.AddComponent<ServerUccNetworkTransform>();
            Vector3 position = new Vector3(4, 2, -7);
            root.transform.position = position;
            typeof(ServerUccNetworkTransform).GetMethod("OnTransformUpdated",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sync, null);
            Assert.That(root.transform.position, Is.EqualTo(position));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void InactiveCombatActorExposesItsNetworkListsBeforeAwakeAndKeepsThemAfterAwake()
    {
        var runtime = services.AddComponent<Lit.Combat.Migration.Network.CombatNetworkActorRuntime>();
        var type = runtime.GetType();
        var control = type.GetField("controlStates", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
        var cooldown = type.GetField("cooldownStates", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
        Assert.That(control, Is.Not.Null);
        Assert.That(cooldown, Is.Not.Null);
        // Same entry point used by NGO before serializing an inactive actor.
        typeof(Unity.Netcode.NetworkBehaviour).GetMethod("InitializeVariables",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, null);
        type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, null);
        Assert.That(type.GetField("controlStates", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime), Is.SameAs(control));
        Assert.That(type.GetField("cooldownStates", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime), Is.SameAs(cooldown));
    }

    private void WithLocalDropFixture(Action<Item, InteractableItem, SpawnManager> test)
    {
        var item = ScriptableObject.CreateInstance<Item>();
        item.itemId = "archive_test_drop";
        var root = CreateSceneObject("scene:prefab:local-drop");
        root.gameObject.AddComponent<BoxCollider>();
        var info = root.gameObject.AddComponent<BuildingInfoInteractable>();
        SetField(info, "buildingItem", item);
        Assert.That(info.IsLegacyBuilding, Is.False);
        var container = root.gameObject.AddComponent<InteractableItem>();
        container.representedItem = item;
        container.storedItems.Add(new InteractableItem.LootItemEntry { item = item, quantity = 3 });
        var allocator = services.GetComponent<SpawnManager>();
        var singleton = typeof(SpawnManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = singleton.GetValue(null);
        singleton.SetValue(null, allocator);
        try { test(item, container, allocator); }
        finally
        {
            singleton.SetValue(null, previous);
            Object.DestroyImmediate(root.gameObject);
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void PreparingAnAdditiveSceneRegistersItsInactiveContainerDefinitions()
    {
        WithItemRegistry((itemRegistry, seed) =>
        {
            var authored = ScriptableObject.CreateInstance<Item>();
            authored.itemId = "scene_only_" + Guid.NewGuid().ToString("N");
            try
            {
                var root = CreateSceneObject("scene:archive:scene-only-item");
                var container = root.gameObject.AddComponent<InteractableItem>();
                container.representedItem = authored;
                container.storedItems.Add(new InteractableItem.LootItemEntry { item = authored, quantity = 3 });
                Assert.That(ItemRegistry.Resolve(authored.itemId), Is.Null);
                PersistentWorldSceneInstaller.PrepareScene(zone);
                Assert.That(ItemRegistry.Resolve(authored.itemId), Is.SameAs(authored));
                PersistentWorldSceneInstaller.PrepareScene(zone);
                Assert.That(itemRegistry.items.FindAll(item => item == authored).Count, Is.EqualTo(1));
                Assert.That(container.GetComponent<PersistentContainerState>(), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(authored); }
        });
    }

    [Test]
    public void MissingContainerDefinitionDoesNotEraseLiveContents()
    {
        WithItemRegistry((itemRegistry, seed) =>
        {
            var missing = ScriptableObject.CreateInstance<Item>();
            missing.itemId = "missing_" + Guid.NewGuid().ToString("N");
            try
            {
                var root = CreateSceneObject("scene:archive:missing-definition");
                var container = root.gameObject.AddComponent<InteractableItem>();
                container.representedItem = missing;
                container.storedItems.Add(new InteractableItem.LootItemEntry { item = missing, quantity = 3 });
                var provider = root.gameObject.AddComponent<PersistentContainerState>();
                byte[] payload = provider.CaptureState(null);
                container.representedItem = seed;
                container.storedItems.Clear();
                container.storedItems.Add(new InteractableItem.LootItemEntry { item = seed, quantity = 7 });
                container.allowTake = false;
                LogAssert.Expect(LogType.Error, new Regex("unresolvedContainerItemId='" + missing.itemId + "'"));
                LogAssert.Expect(LogType.Error, new Regex("unresolvedItemId='" + missing.itemId + "'"));
                provider.ApplyState(payload, PersistentApplyPhase.ApplyGameplayState, null);
                Assert.That(container.representedItem, Is.SameAs(seed));
                Assert.That(container.storedItems.Count, Is.EqualTo(1));
                Assert.That(container.storedItems[0].item, Is.SameAs(seed));
                Assert.That(container.storedItems[0].quantity, Is.EqualTo(7));
                Assert.That(container.allowTake, Is.False);
            }
            finally { Object.DestroyImmediate(missing); }
        });
    }

    [Test]
    public void LoadedItemRegistrationNeverRebindsAnExistingIdentity()
    {
        WithItemRegistry((itemRegistry, seed) =>
        {
            var duplicate = ScriptableObject.CreateInstance<Item>();
            duplicate.itemId = seed.itemId;
            try
            {
                Assert.That(ItemRegistry.RegisterLoadedDefinition(seed), Is.True);
                Assert.That(ItemRegistry.RegisterLoadedDefinition(duplicate), Is.False);
                Assert.That(ItemRegistry.Resolve(seed.itemId), Is.SameAs(seed));
                Assert.That(itemRegistry.items.Count, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(duplicate); }
        });
    }

    private void WithItemRegistry(Action<ItemRegistry, Item> test)
    {
        var seed = ScriptableObject.CreateInstance<Item>();
        seed.itemId = "registry_seed_" + Guid.NewGuid().ToString("N");
        var itemRegistry = services.AddComponent<ItemRegistry>();
        itemRegistry.items = new List<Item> { seed };
        itemRegistry.BuildLookup();
        var singleton = typeof(ItemRegistry).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = singleton.GetValue(null);
        singleton.SetValue(null, itemRegistry);
        try { test(itemRegistry, seed); }
        finally { singleton.SetValue(null, previous); Object.DestroyImmediate(seed); }
    }

    private WorldSaveAdapter LateJoinAdapter(WorldSnapshot saved)
    {
        var adapter = services.AddComponent<WorldSaveAdapter>();
        SetField(adapter, "worldStateManager", world);
        SetField(adapter, "registry", registry);
        SetField(adapter, "<HasRestoredWorldSnapshotThisSession>k__BackingField", true);
        SetField(adapter, "<LastLoadedSnapshot>k__BackingField", saved);
        return adapter;
    }

    private static PersistentObjectSnapshot RuntimeSnapshot(string id, string prefab, string scene = "UnloadedZone") =>
        new PersistentObjectSnapshot
        {
            PersistentId = id, RuntimePrefabId = prefab, SceneName = scene, ObjectKind = PersistentObjectKind.RuntimeSpawned
        };

    private PersistentNetworkObject CreateSceneObject(string id)
    {
        var go = new GameObject("Archive object");
        go.SetActive(false);
        SceneManager.MoveGameObjectToScene(go, zone);
        var persistent = go.AddComponent<PersistentNetworkObject>();
        persistent.AssignSceneIdentity(id);
        return persistent;
    }

    private Scene CreateZone()
    {
        Scene active = SceneManager.GetActiveScene();
        Scene created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        created.name = sceneName;
        if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        return created;
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
}

public sealed class SceneArchiveTestState : MonoBehaviour, IPersistentStateProvider
{
    public int Value;
    public string ProviderId => "archive_test_state";
    public byte[] CaptureState(PersistentStateContext context) => BitConverter.GetBytes(Value);
    public void ApplyState(byte[] data, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (phase == PersistentApplyPhase.ApplyGameplayState) Value = BitConverter.ToInt32(data, 0);
    }
}
#endif
