using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// Validation-copy only. Never installed in the shipping project's Assets.
[DefaultExecutionOrder(-20000)]
public sealed class PersistencePlayerProbe : MonoBehaviour
{
    public ZoneManifest house;
    public ZoneManifest district;
    public string runDirectory;
    [Serializable] private sealed class Result
    {
        public bool passed;
        public bool fullProductionReadiness;
        public string reason, runDirectory;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
        public string scope = "Real solo Bootstrap/menu, interrupted native postload at 90 percent using a tiny fixture cell, existing inventory drop/partial pickup, authored portal, scene archives, enemy-state fixture, explicit file-lock exit-save failure and disk reload. UCC approach fixtures and explicit enemy defeat, not manual movement/combat victory or multiplayer proof.";
    }
    private readonly Result result = new Result();
    private int stage, expectedInventory, savedHealth;
    private string firstActorId;
    private double stageStarted, heartbeat;
    private bool finished;
    private int settledFrames;
    private double controlWaitStarted;
    private string sessionId, saveId, characterId, dropId, dropScene, itemId, team, defeatedMarker;
    private Vector3 savedPosition;
    private InteractableItem dropped;
    private SquadCharacterController firstActor;
    private Item droppedItem;
    private FileStream saveFailureLock;
    private byte[] committedCharacterBytes;
    private string committedCharacterPath;
    private SaveSessionManager exitSaveManager;
    private HashSet<string> slotsBeforeExit;
    private string lockedExitSlotPath;
    private bool interruptedLoadTestStarted, interruptedLoadTestDone;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private void Awake()
    {
        if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("-persistence-validation"))
        { enabled = false; return; }
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        result.runDirectory = Path.GetFullPath(runDirectory);
        Directory.CreateDirectory(result.runDirectory);
        Application.logMessageReceived += ObserveLog;
        Advance(0);
    }
    private void OnDestroy()
    {
        saveFailureLock?.Dispose();
        if (exitSaveManager != null) exitSaveManager.CatalogChanged -= LockNewExitSlot;
        Application.logMessageReceived -= ObserveLog;
    }
    private void ObserveLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            result.errors.Add(message + "\n" + stack);
    }
    private void Update()
    {
        if (finished) return;
        try
        {
            if (Time.realtimeSinceStartupAsDouble > heartbeat)
            {
                heartbeat = Time.realtimeSinceStartupAsDouble + 15;
                Debug.Log("[PersistencePlayer] heartbeat stage=" + stage + " frame=" + Time.frameCount +
                    " active=" + SceneManager.GetActiveScene().name + " transitioning=" + GameFlowService.Instance?.IsTransitioning);
                WriteReport("Running stage=" + stage);
            }
            if (Time.realtimeSinceStartupAsDouble - stageStarted > 240)
                throw new TimeoutException("Stage " + stage + " timeout; nav=" + NavMeshWorldService.Instance?.State);
            Tick();
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private void Tick()
    {
        var flow = GameFlowService.Instance;
        var root = LocalPlayerContext.LocalCharacterRoot;
        var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
        if (stage == 0)
        {
            var saves = SaveSessionManager.Instance;
            if (flow == null || saves == null || flow.IsTransitioning || Time.frameCount < 10 ||
                !SceneManager.GetSceneByName("MainMenu").isLoaded) return;
            if (!interruptedLoadTestDone)
            {
                if (!interruptedLoadTestStarted)
                {
                    interruptedLoadTestStarted = true;
                    StartCoroutine(RunFixture(VerifyInterruptedPostLoading(flow)));
                }
                return;
            }
            Check(!flow.HasGameplaySession && !saves.HasActiveSave, "Actual Bootstrap reached menu without an existing game");
            Check(Path.GetFullPath(saves.GetSavesRoot()) == Path.Combine(result.runDirectory, "Saves"), "All saves isolated from user data");
            var session = saves.CreateSession("Persistence player validation", SaveSessionType.Solo);
            Check(session != null, "Real solo session created");
            var slot = saves.CreateSave(session.sessionId, "Continuity roundtrip");
            Check(slot != null, "Real disposable save slot created");
            sessionId = session.sessionId; saveId = slot.saveId;
            saves.SetActiveSave(sessionId, saveId);
            Check(GameFlowService.StartOrLoadGame(house.PrimarySceneName), "Menu to Maison requested through production flow");
            Advance(1); return;
        }
        if (flow == null) return;
        if (stage == 8)
        {
            if (flow.IsTransitioning || flow.HasGameplaySession) return;
            Check(root == null, "Menu teardown released the controlled actor");
            SaveSessionManager.Instance.SetActiveSave(sessionId, saveId);
            Check(GameFlowService.StartOrLoadGame(), "Reload requested from actual slot metadata");
            Advance(9); return;
        }
        if (stage == 70 && !flow.IsTransitioning && actor == null)
            throw new InvalidOperationException("Exit-save failure fixture did not preserve the controlled actor; inspect the actual locked target.");
        if (flow.IsTransitioning || actor == null) return;
        if (stage == 1 || stage == 4 || stage == 5 || stage == 9 || stage == 10 || stage == 11)
        {
            bool controlled = actor.IsGrounded && InputModeCoordinator.CurrentMode == InputMode.Exploration;
            if (controlled) settledFrames++; else settledFrames = 0;
            if (controlWaitStarted <= 0) controlWaitStarted = Time.realtimeSinceStartupAsDouble;
            if (settledFrames < 3)
            {
                if (!controlled && Time.realtimeSinceStartupAsDouble - controlWaitStarted > 10)
                    throw new InvalidOperationException("Controls did not settle after transition: grounded=" + actor.IsGrounded +
                        " mode=" + InputModeCoordinator.CurrentMode + " actor=" + actor.transform.position + " fixedDelta=" + Time.fixedDeltaTime +
                        " scale=" + Time.timeScale + " nav=" + NavMeshWorldService.Instance?.State);
                return;
            }
        }
        switch (stage)
        {
            case 1:
                if (!ZoneReady(house)) return;
                ValidateControl(actor);
                characterId = actor.CharacterData.UniqueId; firstActorId = actor.GetEntityId().ToString(); firstActor = actor;
                team = TeamState(); savedHealth = Mathf.Max(1, actor.MaxHp - 9); actor.SetCurrentHp(savedHealth);
                droppedItem = actor.Items.Where(item => item != null).Distinct().FirstOrDefault(item =>
                    actor.Items.Count(candidate => candidate == item) >= 3 && !item.isBuilding &&
                    item.CanInstantDropFromInventory(actor, false, out _) && item.ResolveWorldPrefab() != null);
                Check(droppedItem != null, "Eligible authored starter inventory item available, no injected reward");
                itemId = ItemIdUtils.GetItemId(droppedItem);
                expectedInventory = Count(actor, itemId) - 2;
                var inventory = actor.GetComponent<NetworkInventory>();
                Check(inventory != null, "Existing production inventory available");
                Check(inventory.RequestDropItem(droppedItem, 3, actor.transform.position + actor.transform.forward,
                    Quaternion.identity, false, true), "Three real inventory units dropped through production API");
                Advance(2); return;
            case 2:
                if (Time.realtimeSinceStartupAsDouble - stageStarted < .75) return;
                dropped = FindObjectsByType<InteractableItem>(FindObjectsSortMode.None).SingleOrDefault(container =>
                    container.GetComponent<PersistentNetworkObject>()?.RuntimePrefabId == PersistentWorldSceneInstaller.BuildDroppedLootPrefabId(itemId));
                Check(dropped != null, "Dropped world object has its real runtime identity");
                var persistent = dropped.GetComponent<PersistentNetworkObject>();
                dropId = persistent.PersistentId; dropScene = dropped.gameObject.scene.name;
                Check(house.LoadingSceneNames.Concat(new[] { house.PrimarySceneName }).Contains(dropScene), "Drop belongs to a departing Maison scene, not persistence root");
                Check(dropped.GetComponent<PersistentContainerState>() != null && Quantity(dropped, itemId) == 3,
                    "Presentation-info pickup retains its container provider and quantity");
                if (!actor.IsFlameEquipped) actor.ToggleFlame();
                Advance(3); return;
            case 3:
                if (Time.realtimeSinceStartupAsDouble - stageStarted < .75) return;
                Take(actor, dropped, itemId, 1);
                Check(Count(actor, itemId) == expectedInventory && Quantity(dropped, itemId) == 2, "Partial pickup conserves inventory and world quantities");
                team = TeamState();
                UsePortal(actor); Advance(4); return;
            case 4:
                if (!ZoneReady(district)) return;
                ValidateSameActor(actor);
                Check(!SceneManager.GetSceneByName(house.PrimarySceneName).isLoaded && FindDrop() == null,
                    "Solo Maison unloaded and dropped object absent while exploring District");
                var marker = FindObjectsByType<SceneMarker>(FindObjectsSortMode.None).FirstOrDefault(candidate =>
                    candidate.CharacterData != null && candidate.CharacterData.UniqueId == "c54e94b14c7826c4482529b6d4d739ec" && candidate.RuntimeInstance != null);
                Check(marker != null, "Authored District enemy materialized");
                defeatedMarker = marker.MarkerId;
                var health = marker.RuntimeInstance.GetComponent<CharacterInfo>();
                Check(health != null, "Enemy health provider available"); health.ForceDefeat();
                Check(health.IsDead, "Explicit persistence defeat fixture applied to actual enemy");
                Check(GameFlowService.ReturnToHub("Default"), "Normal return-to-hub requested"); Advance(5); return;
            case 5:
                if (!ZoneReady(house)) return;
                ValidateSameActor(actor); ValidateDrop();
                var world = WorldStateManager.Instance.CaptureSnapshot("player-validation");
                Check(world != null && world.SceneArchives.Any(archive => archive.SceneName.StartsWith("District_1_", StringComparison.Ordinal)),
                    "Unloaded District retained as archive data");
                Check(TryGroundPosition(actor.transform, actor.transform.position, out var pose), "Clear physical save position found away from spawn");
                Check(actor.TrySetUccExternalPositionAndRotation(pose, actor.transform.rotation, true), "Save position set using UCC, not Transform");
                Advance(6); return;
            case 6:
                if (Time.realtimeSinceStartupAsDouble - stageStarted < .75) return;
                savedPosition = actor.transform.position;
                Check(Vector3.ProjectOnPlane(savedPosition - ZoneSpawnPoint.FindFirstInScene(house.PrimarySceneName).transform.position, Vector3.up).magnitude > 1,
                    "Saved position differs from new-game spawn");
                Check(CharacterStateStore.Instance != null, "Production character/world save store exists");
                CharacterStateStore.Instance.Save(); Advance(7); return;
            case 7:
                if (CharacterStateStore.Instance.IsSaving) return;
                Check(CharacterStateStore.Instance.LastSaveSucceeded, "Transactional character/world/metadata save committed");
                Check(Directory.GetFiles(Path.Combine(result.runDirectory, "Saves"), "*", SearchOption.AllDirectories).Length >= 3, "Real files written in isolated slot");
                committedCharacterPath = SaveSessionManager.Instance.GetActiveSaveFilePath(CharacterStateStore.Instance.saveFileName);
                committedCharacterBytes = File.ReadAllBytes(committedCharacterPath);
                Check(PausePanelController.Instance != null, "Actual pause save controller available for new exit slot");
                exitSaveManager = SaveSessionManager.Instance;
                slotsBeforeExit = new HashSet<string>(exitSaveManager.GetSession(sessionId).saves.Select(slot => slot.saveId));
                exitSaveManager.CatalogChanged += LockNewExitSlot;
                Check(GameFlowService.QuitToMainMenu(), "Real save-and-exit requested with explicit new-slot metadata lock fixture");
                Advance(70); return;
            case 70:
                Check(!CharacterStateStore.Instance.IsSaving && !CharacterStateStore.Instance.LastSaveSucceeded,
                    "Locked new-slot metadata makes real exit save fail");
                Check(saveFailureLock != null && !string.IsNullOrEmpty(lockedExitSlotPath), "Failure fixture locked the actual exit slot, not the previous save");
                Check(flow.HasGameplaySession && actor == firstActor && SceneManager.GetSceneByName(house.PrimarySceneName).isLoaded,
                    "Failed exit save preserves live session, actor and Maison");
                Check(NavMeshWorldService.Instance.IsReady, "Failed exit preserves ready navigation");
                if (InfoBoxUI.Instance != null && InputFocusStack.HasFocus(InfoBoxUI.Instance))
                {
                    Check(InputModeCoordinator.CurrentMode == InputMode.UserInterface, "Failure notice correctly owns UI input focus");
                    InfoBoxUI.Instance.HideImmediate(); // Explicit acknowledgement fixture, not a focus-stack reset.
                }
                Check(InputModeCoordinator.CurrentMode == InputMode.Exploration,
                    "Acknowledging failure notice restores exploration; mode=" + InputModeCoordinator.CurrentMode);
                saveFailureLock.Dispose(); saveFailureLock = null;
                SaveMetadataWriter.RecoverPendingBatch(lockedExitSlotPath);
                Check(File.ReadAllBytes(committedCharacterPath).SequenceEqual(committedCharacterBytes),
                    "Failed exit transaction preserves previously committed character bytes");
                ValidateDrop();
                actor.RestoreHealthToMax();
                Check(GameFlowService.OpenMainMenu(), "Return menu without saving unsaved health divergence"); Advance(8); return;
            case 9:
                if (!ZoneReady(house)) return;
                ValidateControl(actor);
                Check(firstActor == null && actor.GetEntityId().ToString() != firstActorId && actor.CharacterData.UniqueId == characterId,
                    "Disk reload recreated controlled character with original identity");
                Check(Count(actor, itemId) == expectedInventory && actor.CurrentHp == savedHealth && TeamState() == team,
                    "Disk restored inventory, health and existing team progression");
                Check(Vector3.ProjectOnPlane(actor.transform.position - savedPosition, Vector3.up).magnitude < .15f &&
                    Mathf.Abs(actor.transform.position.y - savedPosition.y) < .35f, "Disk restored actual saved pose instead of spawn");
                Check(WorldSaveAdapter.Instance.LastRestoreSucceeded && WorldSaveAdapter.Instance.LastRestoreIdentityValidated &&
                    WorldSaveAdapter.Instance.LastRestoreIdentityIssues == 0, "World restore passed identity validation");
                Check(!SceneManager.GetSceneByName(district.PrimarySceneName).isLoaded, "Reload did not materialize archived District");
                ValidateDrop(); UsePortal(actor); Advance(10); return;
            case 10:
                if (!ZoneReady(district)) return;
                ValidateControl(actor);
                var restored = FindObjectsByType<SceneMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .SingleOrDefault(candidate => candidate.MarkerId == defeatedMarker);
                Check(restored != null && restored.RuntimeInstance != null && restored.RuntimeInstance.GetComponent<CharacterInfo>()?.IsDead == true,
                    "Previously archived District enemy remains defeated after real disk reload and revisit");
                Check(Count(actor, itemId) == expectedInventory, "Inventory survived second authored portal");
                Check(GameFlowService.ReturnToHub("Default"), "Second return-to-hub requested"); Advance(11); return;
            case 11:
                if (!ZoneReady(house)) return;
                ValidateDrop(); ValidateControl(actor);
                Check(Count(actor, itemId) == expectedInventory, "Inventory and partially consumed drop consistent after second roundtrip");
                Check(result.errors.Count == 0, "No runtime errors during full solo recipe");
                Finish(true, "Solo real zone/archive/item/save continuity recipe complete; multiplayer and authored scene-pickup gates remain separate."); return;
        }
    }
    private void LockNewExitSlot()
    {
        var slot = exitSaveManager.GetSession(sessionId)?.saves.FirstOrDefault(candidate => !slotsBeforeExit.Contains(candidate.saveId));
        if (slot == null) return;
        lockedExitSlotPath = slot.directoryPath;
        saveFailureLock = new FileStream(Path.Combine(slot.directoryPath, "meta.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        exitSaveManager.CatalogChanged -= LockNewExitSlot;
        Check(slot.saveId != saveId, "New exit slot metadata locked before transaction publication");
    }
    private IEnumerator RunFixture(IEnumerator fixture)
    {
        while (true)
        {
            bool more;
            try { more = fixture.MoveNext(); }
            catch (Exception error) { Finish(false, error.ToString()); yield break; }
            if (!more) yield break;
            yield return fixture.Current;
        }
    }
    private IEnumerator VerifyInterruptedPostLoading(GameFlowService flow)
    {
        const string cell = "PendingPostCell";
        var routine = (IEnumerator)typeof(GameFlowService).GetMethod("LoadAdditiveAfterGameplayRoutine", Private)
            .Invoke(flow, new object[] { cell });
        var handle = flow.StartCoroutine(routine);
        typeof(GameFlowService).GetField("postLoadingRoutine", Private).SetValue(flow, handle);
        var operations = (List<AsyncOperation>)typeof(GameFlowService).GetField("pendingSceneOperations", Private).GetValue(flow);
        var operation = operations.Last();
        while (operation.progress < .9f && !operation.isDone) yield return null;
        Check(!operation.isDone && !operation.allowSceneActivation, "Native postload captured at 90 percent before activation");
        typeof(GameFlowService).GetMethod("StopPostLoadingRoutine", Private).Invoke(flow, null);
        Check(!operation.allowSceneActivation, "Cancelling coroutine leaves native request pending until opaque overlay");
        yield return LoadingScreenService.ShowAndWaitForPresentation("Validation interruption de chargement");
        bool? completed = null;
        var drain = (IEnumerator)typeof(GameFlowService).GetMethod("FinishPostLoadingRequests", Private)
            .Invoke(flow, new object[] { true, new Action<bool>(ready => completed = ready) });
        while (drain.MoveNext()) yield return drain.Current;
        Check(completed == true && operation.isDone && SceneManager.GetSceneByName(cell).isLoaded,
            "Opaque-screen drain completes the interrupted native request");
        var owned = (List<string>)typeof(GameFlowService).GetField("loadedZoneSceneNames", Private).GetValue(flow);
        Check(owned.Contains(cell), "Interrupted scene retains flow ownership for transition teardown");
        var unload = SceneManager.UnloadSceneAsync(cell);
        while (unload != null && !unload.isDone) yield return null;
        owned.Remove(cell);
        Check(!SceneManager.GetSceneByName(cell).isLoaded && SceneManager.GetSceneByName("MainMenu").isLoaded,
            "Native unload queue proceeds after interrupted preload without losing menu");
        LoadingScreenService.HideWhenSceneIsReady();
        yield return LoadingScreenService.WaitUntilHidden();
        interruptedLoadTestDone = true;
    }
    private bool ZoneReady(ZoneManifest zone) => !GameFlowService.Instance.IsTransitioning &&
        zone.LoadingSceneNames.Concat(zone.PostLoadingSceneNames).Concat(new[] { zone.PrimarySceneName }).All(name => SceneManager.GetSceneByName(name).isLoaded) &&
        NavMeshWorldService.Instance != null && NavMeshWorldService.Instance.IsReady && NavMeshWorldService.Instance.LastReport.zoneId == zone.PrimarySceneName;
    private void ValidateControl(SquadCharacterController actor)
    {
        Check(actor.IsGrounded && InputModeCoordinator.CurrentMode == InputMode.Exploration, "Grounding and exploration restored in " + SceneManager.GetActiveScene().name);
        Check(FindObjectsByType<SquadCharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(candidate => candidate.CharacterData == actor.CharacterData) == 1, "Exactly one controlled-character instance");
    }
    private void ValidateSameActor(SquadCharacterController actor)
    {
        ValidateControl(actor);
        Check(actor == firstActor && actor.CurrentHp == savedHealth && Count(actor, itemId) == expectedInventory && TeamState() == team,
            "Zone transfer preserved same actor, inventory, health and team");
    }
    private static int Count(SquadCharacterController actor, string id) => actor.Items.Count(item => item != null && ItemIdUtils.GetItemId(item) == id);
    private static int Quantity(InteractableItem container, string id) => container.storedItems.Where(entry => entry?.item != null && ItemIdUtils.GetItemId(entry.item) == id).Sum(entry => entry.quantity);
    private InteractableItem FindDrop() => FindObjectsByType<PersistentNetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .SingleOrDefault(candidate => candidate.PersistentId == dropId)?.GetComponent<InteractableItem>();
    private void ValidateDrop()
    {
        var container = FindDrop();
        Check(container != null && container.gameObject.scene.name == dropScene && Quantity(container, itemId) == 2,
            "World drop retains ID, scene and remaining quantity after return/reload");
    }
    private void Take(SquadCharacterController actor, InteractableItem container, string id, int quantity)
    {
        Check(container.CanBeDetectedBy(actor) && !container.IsLockedForInteraction(), "Real pickup passes influence and lock gates");
        var entry = container.storedItems.First(value => value.item != null && ItemIdUtils.GetItemId(value.item) == id);
        Check(entry.item.CanTakeFromContainer(container, out _), "Item allows normal pickup");
        container.SetDetectedCharacter(actor.gameObject);
        Check((bool)typeof(InteractableItem).GetMethod("PerformTake", Private).Invoke(container, new object[] { entry, quantity }),
            "Production take action accepted; no direct inventory mutation");
    }
    private void UsePortal(SquadCharacterController actor)
    {
        var portal = FindObjectsByType<PortalController>(FindObjectsSortMode.None).Single(candidate => candidate.IsSceneTransition &&
            (ZoneManifest)typeof(PortalController).GetField("destinationZone", Private).GetValue(candidate) == district);
        Check(TryGroundPosition(actor.transform, portal.GetInteractionAnchor().position, out var near, sameFloorAsActor: false),
            "Clear physical approach to actual authored portal (actor=" + actor.transform.position + ", portal=" + portal.GetInteractionAnchor().position + ")");
        Check(actor.TrySetUccExternalPositionAndRotation(near, actor.transform.rotation, true), "Only actor approaches portal via UCC fixture");
        Physics.SyncTransforms();
        Check(portal.ServerTryUse(actor.gameObject, out _, out _), "Actual portal admitted scene transition");
    }
    private static bool TryGroundPosition(Transform actor, Vector3 anchor, out Vector3 position, bool sameFloorAsActor = true)
    {
        foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
        foreach (float distance in new[] { 2f, 1.5f, 1f })
        foreach (var hit in Physics.RaycastAll(anchor + direction * distance + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
        {
            if (hit.transform.GetComponentInParent<SquadCharacterController>() != null || hit.normal.y < .7f ||
                Mathf.Abs(hit.point.y - (sameFloorAsActor ? actor.position.y : anchor.y)) > (sameFloorAsActor ? .75f : 2.5f)) continue;
            var candidate = hit.point + Vector3.up * .02f;
            if (Physics.OverlapCapsule(candidate + Vector3.up * .3f, candidate + Vector3.up * 1.5f, .25f, ~0, QueryTriggerInteraction.Ignore)
                .Any(collider => !collider.transform.IsChildOf(actor))) continue;
            position = candidate; return true;
        }
        position = default; return false;
    }
    private string TeamState()
    {
        var progression = SoloTeamProgression.Resolve();
        Check(progression != null, "Solo progression exists");
        return string.Join("|", Enum.GetValues(typeof(CombatSkillKit)).Cast<CombatSkillKit>().Select(kit =>
            kit + ":" + progression.IsRecruited(kit) + ":" + string.Concat(Enumerable.Range(0, 4).Select(slot => progression.IsUnlocked(kit, slot) ? "1" : "0"))));
    }
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        result.checks.Add(message); Debug.Log("[PersistencePlayer] PASS " + message);
    }
    private void Advance(int next) { stage = next; settledFrames = 0; controlWaitStarted = 0; stageStarted = Time.realtimeSinceStartupAsDouble; WriteReport("Running stage=" + next); }
    private void WriteReport(string reason)
    {
        result.reason = reason;
        File.WriteAllText(Path.Combine(result.runDirectory, "result.json"), JsonUtility.ToJson(result, true));
    }
    private void Finish(bool passed, string reason)
    {
        saveFailureLock?.Dispose(); saveFailureLock = null;
        if (exitSaveManager != null) exitSaveManager.CatalogChanged -= LockNewExitSlot;
        finished = true; result.passed = passed; WriteReport(reason);
        Debug.Log("[PersistencePlayer] " + (passed ? "COMPLETE" : "FAILED") + " " + reason);
        Application.Quit(passed ? 0 : 1);
    }
}
