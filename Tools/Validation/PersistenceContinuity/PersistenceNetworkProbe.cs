using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Validation-copy only. Direct loopback transport exercises real NGO objects,
// ownership and snapshots; it does not qualify Relay/invitation UX.
[DefaultExecutionOrder(-19000)]
public sealed class PersistenceNetworkProbe : MonoBehaviour
{
    public ZoneManifest house, district;
    public string runDirectory, sharedDirectory, role;
    [Serializable] private sealed class Report
    {
        public bool passed, fullProductionReadiness, functionalGatePassed;
        public string reason, scope = "Two independent Unity processes, production Bootstrap/NGO direct loopback, host starts Maison then uses its authored District portal before late join, local session readiness, one initial snapshot application, real inventory RPC and host save. Authored Flame.SetLit(true) and UCC/network approach fixtures; no ignition-input, live-client zone portal/rejoin/Relay/manual-input proof.";
        public List<string> checks = new List<string>(), errors = new List<string>();
    }
    [Serializable] private sealed class Offer { public string dropId, itemId; public int quantity; }
    private readonly Report report = new Report();
    private int stage, ownInventoryBefore, hostInventoryAfterDrop;
    private double stageStarted, heartbeat, synchronizedAt;
    private bool finished, synchronized, clientMenuReadySignaled;
    private string dropId, itemId;
    private HashSet<string> identitiesBeforeDrop;
    private const ushort Port = 17739;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private NetworkManager manager;
    private SquadCharacterController firstActor;
    private int clientSnapshotApplications;
    private WorldStateManager observedWorld;
    private bool RoundTrip => Environment.GetCommandLineArgs().Contains("-persistence-network-roundtrip");
    private void SnapshotApplied(WorldSnapshot snapshot) => clientSnapshotApplications++;

    private void Awake()
    {
        if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("-persistence-validation") ||
            role != "host" && role != "client") { enabled = false; return; }
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        if (RoundTrip) report.scope = report.scope.Replace("live-client zone portal/rejoin/Relay/manual-input proof.", "rejoin/Relay/manual-input proof.") +
            " Also exercises District-to-Maison return and the authored Maison-to-District portal with the live client, retained actors/inventory and archived drop quantity. Still no reconnect/disk-reload proof.";
        Directory.CreateDirectory(runDirectory);
        Directory.CreateDirectory(sharedDirectory);
        Application.logMessageReceived += Observe;
        Advance(0);
    }
    private void OnDestroy()
    {
        if (observedWorld != null) observedWorld.SnapshotApplied -= SnapshotApplied;
        Application.logMessageReceived -= Observe;
        if (manager != null && manager.SceneManager != null) manager.SceneManager.OnSceneEvent -= SceneEvent;
    }
    private void SceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete)
        { synchronized = true; synchronizedAt = Time.realtimeSinceStartupAsDouble; }
    }
    private void Observe(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.errors.Add(message + "\n" + stack);
    }
    private void Update()
    {
        if (finished) return;
        try
        {
            if (File.Exists(Path.Combine(sharedDirectory, "cancel-requested.txt")))
            {
                Finish(false, "Validation cancelled by its coordinator; inspect captured stage and errors.");
                return;
            }
            string peer = role == "host" ? "client" : "host";
            string peerDirectory = Path.Combine(sharedDirectory, peer);
            if (File.Exists(Path.Combine(peerDirectory, "editor-complete.txt")))
            {
                var peerReport = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(peerDirectory, "result.json")));
                if (!peerReport.passed)
                    throw new InvalidOperationException("Independent " + peer + " validation failed: " + peerReport.reason);
            }
            if (Time.realtimeSinceStartupAsDouble > heartbeat)
            {
                heartbeat = Time.realtimeSinceStartupAsDouble + 15;
                Write("Running stage=" + stage + " role=" + role);
                Debug.Log("[PersistenceNetwork] " + report.reason + " connected=" + manager?.IsConnectedClient +
                    " session=" + GameFlowService.Instance?.HasGameplaySession + " sync=" + JoinSyncSystem.Instance?.IsLocalWorldReady +
                    " actorPosition=" + LocalPlayerContext.LocalCharacterRoot?.position +
                    " dropPosition=" + (string.IsNullOrEmpty(dropId) ? "unassigned" : FindDrop()?.transform.position.ToString()) +
                    " actors=" + DescribeActorPoses());
            }
            // Waiting for the other editor's startup is not a connection attempt.
            // The editor wrapper still bounds the whole recipe to 30 minutes.
            if (role == "client" && stage == 0 && clientMenuReadySignaled && !Exists("host-ready"))
                stageStarted = Time.realtimeSinceStartupAsDouble;
            double stageBudget = Environment.GetCommandLineArgs().Contains("-persistence-pose-diagnostic") &&
                ((role == "client" && stage == 2) || (role == "host" && stage == 4)) ? 45 : 240;
            if (RoundTrip && (stage == 11 || stage == 5 || stage == 6 || stage == 7 || stage == 8))
                stageBudget = 600; // Editor scene import/activation is not a transport timeout.
            if (Time.realtimeSinceStartupAsDouble - stageStarted > stageBudget)
                throw new TimeoutException("stage=" + stage + " role=" + role + " syncEvent=" + synchronized +
                    " localRoot=" + LocalPlayerContext.LocalCharacterRoot + " session=" + GameFlowService.Instance?.HasGameplaySession +
                    " nav=" + NavMeshWorldService.Instance?.State + " mode=" + InputModeCoordinator.CurrentMode +
                    " actorPosition=" + LocalPlayerContext.LocalCharacterRoot?.position +
                    " dropPosition=" + (string.IsNullOrEmpty(dropId) ? "unassigned" : FindDrop()?.transform.position.ToString()) +
                    " actors=" + DescribeActorPoses());
            Tick();
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private void Tick()
    {
        var flow = GameFlowService.Instance;
        if (flow == null) return;
        if (stage == 0)
        {
            if (flow.IsTransitioning || !SceneManager.GetSceneByName("MainMenu").isLoaded || Time.frameCount < 10) return;
            if (role == "client" && !clientMenuReadySignaled)
            {
                Signal("client-menu-ready");
                clientMenuReadySignaled = true;
            }
            if (role == "client" && !Exists("host-ready")) return;
            Check(!flow.HasGameplaySession, "Bootstrap menu has no pre-created gameplay session");
            GameplayRuntimeReset.PrepareForGameplayStart("persistence_network_fixture_prepare");
            var saves = SaveSessionManager.Instance;
            Check(Path.GetFullPath(saves.GetSavesRoot()) == Path.Combine(runDirectory, "Saves"), "Profile saves isolated");
            saves.SetCurrentSessionType(SaveSessionType.Multiplayer);
            manager = NetworkManager.Singleton;
            var launcher = FindAnyObjectByType<NetcodeLauncher>();
            Check(manager != null && launcher != null, "Authored network bootstrap and launcher available");
            if (role == "host")
            {
                var session = saves.CreateSession("Network continuity fixture", SaveSessionType.Multiplayer);
                var slot = saves.CreateSave(session.sessionId, "Host authority");
                saves.SetActiveSave(session.sessionId, slot.saveId);
                Check(launcher.StartHostWithConnection("127.0.0.1", Port, "127.0.0.1"), "Real loopback host listening");
                Check(GameFlowService.StartOrLoadGame(house.PrimarySceneName), "Host starts actual Maison manifest");
            }
            else
            {
                observedWorld = WorldStateManager.Instance;
                Check(observedWorld != null, "Client world snapshot service available before connection");
                observedWorld.SnapshotApplied += SnapshotApplied;
                saves.ClearActiveSave();
                Check(launcher.StartClientWithConnection("127.0.0.1", Port), "Real independent client connects late");
                manager.SceneManager.OnSceneEvent += SceneEvent;
            }
            Advance(1); return;
        }
        var actor = LocalPlayerContext.LocalCharacterRoot != null ? LocalPlayerContext.LocalCharacterRoot.GetComponent<SquadCharacterController>() : null;
        if (role == "client") { TickClient(flow, actor); return; }
        if (stage == 1)
        {
            if (flow.IsTransitioning || actor == null || !Ready(house)) return;
            UseAuthoredPortal(actor);
            Advance(11); return;
        }
        if (stage == 11)
        {
            if (flow.IsTransitioning || actor == null || !Ready(district)) return;
            Check(manager.IsServer && manager.IsHost, "Only host is the authoritative server");
            firstActor = actor;
            PrepareLightingFixture(actor);
            var item = actor.Items.Where(value => value != null).Distinct().FirstOrDefault(value =>
                actor.Items.Count(other => other == value) >= 3 && !value.isBuilding && value.CanInstantDropFromInventory(actor, false, out _) && value.ResolveWorldPrefab() != null);
            Check(item != null, "Authored host inventory supports real three-unit drop");
            itemId = ItemIdUtils.GetItemId(item);
            identitiesBeforeDrop = new HashSet<string>(FindObjectsByType<PersistentNetworkObject>(FindObjectsInactive.Include).Select(value => value.PersistentId));
            hostInventoryAfterDrop = Count(actor, itemId) - 3;
            Check(actor.GetComponent<NetworkInventory>().RequestDropItem(item, 3, actor.transform.position + actor.transform.forward * .4f, Quaternion.identity, false, true), "Host production drop requested");
            Advance(2); return;
        }
        if (stage == 2)
        {
            var drop = FindObjectsByType<PersistentNetworkObject>(FindObjectsInactive.Include).FirstOrDefault(value =>
                !identitiesBeforeDrop.Contains(value.PersistentId) && value.ObjectKind == PersistentObjectKind.RuntimeSpawned && value.GetComponent<InteractableItem>() is InteractableItem container && Quantity(container, itemId) == 3);
            if (drop == null) return;
            dropId = drop.PersistentId;
            foreach (var entry in FindObjectsByType<NetworkObject>(FindObjectsInactive.Include))
            {
                if (!entry.IsSpawned) continue;
                Debug.Log("[PersistenceNetwork] ObjectHash name='" + entry.name + "' scene='" + entry.gameObject.scene.name +
                    "' hash=" + typeof(NetworkObject).GetField("GlobalObjectIdHash", Private)?.GetValue(entry) +
                    " prefabHash=" + typeof(NetworkObject).GetField("PrefabGlobalObjectIdHash", Private)?.GetValue(entry) +
                    " inScene=" + entry.IsSceneObject + " id='" + entry.GetComponent<PersistentNetworkObject>()?.PersistentId + "'");
            }
            Check(drop.GetComponent<NetworkObject>()?.IsSpawned == true, "Drop has actual spawned NGO identity before late join");
            Check(drop.GetComponent<InteractableItem>().CanBeDetectedBy(actor), "Drop satisfies real authored lit-influence gate before late join");
            File.WriteAllText(Path.Combine(sharedDirectory, "offer.json"), JsonUtility.ToJson(new Offer {dropId=dropId,itemId=itemId,quantity=3}));
            Signal("host-ready"); Advance(3); return;
        }
        if (stage == 3)
        {
            if (!Exists("client-world-ready")) return;
            Check(manager.ConnectedClientsIds.Count == 2, "Host sees one real remote client");
            ulong remote = manager.ConnectedClientsIds.Single(id => id != manager.LocalClientId);
            Check(JoinSyncSystem.Instance.IsClientReady(remote), "Host received client snapshot acknowledgement");
            var remoteActor = NetcodePlayerUtils.GetPlayerTransform(remote)?.GetComponent<SquadCharacterController>();
            Check(remoteActor != null && remoteActor != actor, "Separate actor assigned to remote player");
            Check(remoteActor.GetComponent<NetworkObject>().OwnerClientId == remote, "Remote actor retains correct NGO owner");
            Vector3 approachPosition = actor.transform.position + actor.transform.right * .6f;
            Check(remoteActor.TryTeleportForSceneTransition(approachPosition, actor.transform.rotation),
                "Server UCC approach fixture positions remote actor near real drop");
            // This is an explicit approach fixture, not a movement test. Commit
            // the authoritative pose through NGO as well as the UCC simulation.
            var remoteTransform = remoteActor.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            Check(remoteTransform != null, "Remote approach fixture has a real network transform");
            remoteTransform.Teleport(approachPosition, actor.transform.rotation, remoteActor.transform.localScale);
            Signal("take-requested"); Advance(4); return;
        }
        if (stage == 4)
        {
            if (!Exists("client-take-confirmed") || Quantity(FindDrop(), itemId) != 2) return;
            Check(Count(actor, itemId) == hostInventoryAfterDrop, "Remote pickup does not credit host inventory");
            Check(actor == firstActor, "Host actor unchanged by late join and remote pickup");
            Check(WorldStateManager.Instance.CaptureSnapshot("network continuity fixture") != null && WorldStateManager.Instance.LastCaptureSucceeded,
                "Authoritative world snapshot captures both players and partially consumed drop");
            CharacterStateStore.Instance.Save(); Advance(5); return;
        }
        if (stage == 5)
        {
            if (CharacterStateStore.Instance.IsSaving) return;
            Check(CharacterStateStore.Instance.LastSaveSucceeded, "Host commits actual multiplayer save");
            Signal("host-saved"); Advance(6); return;
        }
        if (stage == 6 && RoundTrip)
        {
            if (!Exists("client-save-observed")) return;
            Check(GameFlowService.ReturnToHub("Default"), "Host requests actual production return-to-hub with live client");
            Advance(7); return;
        }
        if (stage == 7)
        {
            if (!Ready(house) || !Exists("client-house-return-ready")) return;
            Check(!SceneManager.GetSceneByName(district.PrimarySceneName).isLoaded, "Host releases District Core in Maison");
            Check(actor == firstActor && Count(actor, itemId) == hostInventoryAfterDrop, "Host actor and inventory retained through return-to-hub");
            CheckPlayers(actor);
            UseAuthoredPortal(actor);
            Advance(8); return;
        }
        if (stage == 8)
        {
            if (!Ready(district) || !Exists("client-district-ready")) return;
            Check(actor == firstActor && Count(actor, itemId) == hostInventoryAfterDrop, "Host actor and inventory retained after roundtrip to District");
            Check(Quantity(FindDrop(), itemId) == 2, "Host restores the partially consumed drop archive after roundtrip");
            CheckPlayers(actor);
            CharacterStateStore.Instance.Save(); Advance(9); return;
        }
        if (stage == 9)
        {
            if (CharacterStateStore.Instance.IsSaving) return;
            Check(CharacterStateStore.Instance.LastSaveSucceeded, "Host commits actual multiplayer save after portal roundtrip");
            Signal("roundtrip-saved"); Advance(10); return;
        }
        if (stage == 10 && Exists("client-roundtrip-complete"))
        {
            report.functionalGatePassed = true;
            Check(report.errors.Count == 0, "No host runtime errors during network portal roundtrip");
            Finish(true, "Late join, partial pickup, host save and live client portal roundtrip validated; reconnect/disk reload remain separate gates.");
        }
        if (stage == 6 && !RoundTrip && Exists("client-complete"))
        {
            report.functionalGatePassed = true;
            Check(report.errors.Count == 0, "No runtime errors during first actual network continuity gate");
            Finish(true, "Late join, authoritative partial pickup and host disk commit validated. Zone transfer/rejoin/disk reload remain separate gates.");
        }
    }
    private void TickClient(GameFlowService flow, SquadCharacterController actor)
    {
        if (stage == 1)
        {
            if (!manager.IsConnectedClient || !synchronized || JoinSyncSystem.Instance == null || !JoinSyncSystem.Instance.IsLocalWorldReady || actor == null) return;
            if (Time.realtimeSinceStartupAsDouble - synchronizedAt < 2) return;
            Check(flow.HasGameplaySession, "Client owns a complete local gameplay session after native scene sync");
            Check(clientSnapshotApplications == 1, "Client applies world snapshot once while preparing local services");
            Check(!manager.IsServer && !SaveSessionManager.Instance.HasActiveSave, "Client neither owns server authority nor writes host slot");
            Check(actor.GetComponent<NetworkObject>() is NetworkObject networkActor && networkActor.IsSpawned && networkActor.OwnerClientId == manager.LocalClientId,
                "Client controls only its real owned network actor");
            Check(NavMeshWorldService.Instance != null && NavMeshWorldService.Instance.IsReady, "Client gameplay navigation service ready");
            var offer = JsonUtility.FromJson<Offer>(File.ReadAllText(Path.Combine(sharedDirectory, "offer.json")));
            dropId = offer.dropId; itemId = offer.itemId;
            Check(Quantity(FindDrop(), itemId) == offer.quantity, "Late join reconstructed existing drop with stable ID and quantity");
            ownInventoryBefore = Count(actor, itemId);
            firstActor = actor;
            Signal("client-world-ready"); Advance(2); return;
        }
        if (stage == 2)
        {
            if (!Exists("take-requested") || actor == null) return;
            var drop = FindDrop();
            if (drop == null || Vector3.Distance(actor.transform.position, drop.transform.position) > 2) return;
            if (!drop.CanBeDetectedBy(actor)) return; // Wait for the real replicated Flame influence, never bypass it.
            Check(drop.CanBeDetectedBy(actor) && !drop.IsLockedForInteraction(), "Remote pickup satisfies production influence/lock gates");
            drop.SetDetectedCharacter(actor.gameObject);
            var entry = drop.storedItems.Single(value => value.item != null && ItemIdUtils.GetItemId(value.item) == itemId);
            Check((bool)typeof(InteractableItem).GetMethod("PerformTake", Private).Invoke(drop, new object[] {entry, 1}), "Remote take uses existing production ServerRpc");
            Advance(3); return;
        }
        if (stage == 3)
        {
            if (actor == null || Count(actor, itemId) != ownInventoryBefore + 1 || Quantity(FindDrop(), itemId) != 2) return;
            Check(true, "Client inventory and shared world quantity converge after server-authoritative pickup");
            Signal("client-take-confirmed"); Advance(4); return;
        }
        if (stage == 4 && Exists("host-saved"))
        {
            Check(!Directory.Exists(Path.Combine(runDirectory, "Saves")) || !Directory.EnumerateFiles(Path.Combine(runDirectory, "Saves"), "CharacterState.json", SearchOption.AllDirectories).Any(),
                "Remote client wrote no character save to disk");
            if (RoundTrip)
            {
                Signal("client-save-observed"); Advance(5); return;
            }
            report.functionalGatePassed = true;
            Check(report.errors.Count == 0, "No runtime errors during client continuity gate");
            Signal("client-complete"); Finish(true, "Actual client late join and pickup validated; transition/rejoin/reload remain unqualified.");
        }
        if (stage == 5)
        {
            if (!Ready(house) || JoinSyncSystem.Instance?.IsLocalWorldReady != true || actor == null ||
                SceneManager.GetActiveScene().name != house.PrimarySceneName) return;
            Check(clientSnapshotApplications > 1, "Client receives a new authoritative snapshot after portal");
            Check(!SceneManager.GetSceneByName(district.PrimarySceneName).isLoaded, "Client releases District Core in Maison");
            Check(actor == firstActor && Count(actor, itemId) == ownInventoryBefore + 1, "Client actor and credited inventory retained in Maison");
            CheckPlayers(actor);
            Signal("client-house-return-ready"); Advance(6); return;
        }
        if (stage == 6)
        {
            if (!Ready(district) || JoinSyncSystem.Instance?.IsLocalWorldReady != true || actor == null ||
                SceneManager.GetActiveScene().name != district.PrimarySceneName) return;
            Check(actor == firstActor && Count(actor, itemId) == ownInventoryBefore + 1, "Client actor and credited inventory retained after return to District");
            Check(Quantity(FindDrop(), itemId) == 2, "Client restores archived drop ID and partial quantity after return");
            CheckPlayers(actor);
            Signal("client-district-ready"); Advance(7); return;
        }
        if (stage == 7 && Exists("roundtrip-saved"))
        {
            Check(!Directory.Exists(Path.Combine(runDirectory, "Saves")) || !Directory.EnumerateFiles(Path.Combine(runDirectory, "Saves"), "CharacterState.json", SearchOption.AllDirectories).Any(),
                "Client writes no character save during portal roundtrip");
            report.functionalGatePassed = true;
            Signal("client-roundtrip-complete");
            Check(report.errors.Count == 0, "No client runtime errors during network portal roundtrip");
            Finish(true, "Client late join, partial pickup, archive restoration and portal roundtrip validated; reconnect/disk reload remain unqualified.");
        }
    }
    private void CheckPlayers(SquadCharacterController actor)
    {
        var players = FindObjectsByType<NetworkCharacterInput>(FindObjectsInactive.Include)
            .Select(value => value.GetComponent<NetworkObject>()).Where(value => value != null && value.IsSpawned).ToArray();
        Check(players.Length == 2 && players.Select(value => value.OwnerClientId).Distinct().Count() == 2,
            "Exactly one spawned actor per real player after scene transition");
        Check(actor.GetComponent<NetworkObject>().OwnerClientId == manager.LocalClientId,
            "Local control ownership retained after scene transition");
    }
    private void UseAuthoredPortal(SquadCharacterController actor)
    {
        var portal = FindObjectsByType<PortalController>(FindObjectsSortMode.None).Single(value => value.IsSceneTransition &&
            (ZoneManifest)typeof(PortalController).GetField("destinationZone", Private).GetValue(value) == district);
        Vector3 anchor = portal.GetInteractionAnchor().position;
        foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
        foreach (float distance in new[] { 1f, 1.5f, 2f })
        foreach (var hit in Physics.RaycastAll(anchor + direction * distance + Vector3.up * 2, Vector3.down, 5,
            ~0, QueryTriggerInteraction.Ignore).OrderBy(value => value.distance))
        {
            if (hit.normal.y < .7f || hit.transform.GetComponentInParent<SquadCharacterController>() != null || Mathf.Abs(hit.point.y - anchor.y) > 2.5f) continue;
            Vector3 position = hit.point + Vector3.up * .02f;
            if (Physics.OverlapCapsule(position + Vector3.up * .3f, position + Vector3.up * 1.5f, .25f,
                ~0, QueryTriggerInteraction.Ignore).Any(collider => !collider.transform.IsChildOf(actor.transform))) continue;
            Check(actor.TryTeleportForSceneTransition(position, actor.transform.rotation), "UCC fixture approaches the actual authored Maison portal");
            actor.GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(position, actor.transform.rotation, actor.transform.localScale);
            Check(portal.ServerTryUse(actor.gameObject, out _, out _), "Actual authored portal admits server-authoritative zone transition");
            return;
        }
        throw new InvalidOperationException("No safe physical approach to the authored Maison portal.");
    }
    private bool Ready(ZoneManifest zone) => !GameFlowService.Instance.IsTransitioning &&
        zone.LoadingSceneNames.Concat(zone.PostLoadingSceneNames).Concat(new[]{zone.PrimarySceneName}).All(name=>SceneManager.GetSceneByName(name).isLoaded) && NavMeshWorldService.Instance?.IsReady == true;
    private void PrepareLightingFixture(SquadCharacterController actor)
    {
        foreach (var value in FindObjectsByType<Flame>(FindObjectsInactive.Include))
            Debug.Log("[PersistenceNetwork] LightingCandidate " + value.name + " active=" + value.isActiveAndEnabled +
                " ancient=" + value.IsAncientFlame + " spawned=" + value.GetComponent<NetworkObject>()?.IsSpawned +
                " anchor=" + value.GetInteractionAnchor()?.position + " lightRange=" + value.flameLight?.range);
        foreach (var flame in FindObjectsByType<Flame>(FindObjectsSortMode.None)
            .Where(value => value.GetComponent<NetworkObject>()?.IsSpawned == true)
            .OrderBy(value => value.IsAncientFlame).ThenBy(value => Vector3.Distance(actor.transform.position, value.transform.position)))
        {
            Vector3 anchor = flame.GetInteractionAnchor().position;
            foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left,
                new Vector3(1,0,1).normalized, new Vector3(-1,0,1).normalized, new Vector3(1,0,-1).normalized, new Vector3(-1,0,-1).normalized })
            foreach (float distance in new[] { .8f, 1.2f, 2f, 3f, 4.5f })
            foreach (var hit in Physics.RaycastAll(anchor + direction * distance + Vector3.up * 4, Vector3.down, 20,
                ~0, QueryTriggerInteraction.Ignore).OrderBy(value => value.distance))
            {
                if (hit.normal.y < .7f || hit.transform.GetComponentInParent<SquadCharacterController>() != null) continue;
                Vector3 position = hit.point + Vector3.up * .02f;
                if (Physics.OverlapCapsule(position + Vector3.up * .3f, position + Vector3.up * 1.5f, .25f,
                    ~0, QueryTriggerInteraction.Ignore).Any(collider => !collider.transform.IsChildOf(actor.transform))) continue;
                flame.SetLit(true);
                if (!flame.ProvidesLitInfluenceTo(null, position + Vector3.up * .4f)) continue;
                Vector3 facing = anchor - position; facing.y = 0;
                Quaternion rotation = facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : actor.transform.rotation;
                Check(actor.TryTeleportForSceneTransition(position, rotation), "UCC approach fixture reaches authored Flame");
                actor.GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(position, rotation, actor.transform.localScale);
                // Explicit fixture setup through the actual authoritative Flame,
                // not a disabled interaction rule or a fake influence callback.
                flame.SetLit(true);
                Check(flame.IsEffectivelyLit, "Authored Flame lighting fixture active through production API");
                return;
            }
        }
        throw new InvalidOperationException("No authored Flame with a safe physical approach and real influence for the pickup fixture.");
    }
    private static string DescribeActorPoses()
    {
        return string.Join("; ", FindObjectsByType<NetcodeCharacterIdentity>(FindObjectsInactive.Include).Select(identity =>
        {
            var network = identity.GetComponent<NetworkObject>();
            if (network == null || !network.IsSpawned) return identity.name + ":not-spawned";
            var sync = identity.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            var motor = identity.GetComponent<Opsive.UltimateCharacterController.Character.CharacterLocomotion>();
            var body = identity.GetComponent<Rigidbody>();
            return identity.name + " owner=" + network.OwnerClientId + " world=" + identity.transform.position +
                " ngo=" + sync?.GetSpaceRelativePosition(true) + " ngoEnabled=" + sync?.enabled +
                " authority=" + sync?.CanCommitToTransform + " localSpace=" + sync?.InLocalSpace +
                " ucc=" + motor?.Position + " uccEnabled=" + motor?.enabled + " body=" + body?.position;
        }));
    }
    private InteractableItem FindDrop() => FindObjectsByType<PersistentNetworkObject>(FindObjectsInactive.Include).SingleOrDefault(value=>value.PersistentId==dropId)?.GetComponent<InteractableItem>();
    private static int Count(SquadCharacterController actor, string id) => actor.Items.Count(item=>item!=null && ItemIdUtils.GetItemId(item)==id);
    private static int Quantity(InteractableItem container, string id) => container?.storedItems?.Where(entry=>entry?.item!=null && ItemIdUtils.GetItemId(entry.item)==id).Sum(entry=>entry.quantity) ?? -1;
    private bool Exists(string name) => File.Exists(Path.Combine(sharedDirectory,name));
    private void Signal(string name) => File.WriteAllText(Path.Combine(sharedDirectory,name), role);
    private void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); report.checks.Add(message); Debug.Log("[PersistenceNetwork] PASS "+message); }
    private void Advance(int next) { stage=next; stageStarted=Time.realtimeSinceStartupAsDouble; Write("Running stage="+stage+" role="+role); }
    private void Write(string reason) { report.reason=reason; File.WriteAllText(Path.Combine(runDirectory,"result.json"),JsonUtility.ToJson(report,true)); }
    private void Finish(bool passed,string reason) { finished=true;report.passed=passed;Write(reason); Debug.Log("[PersistencePlayer] "+(passed?"COMPLETE ":"FAILED ")+role+" "+reason); Application.Quit(passed?0:1); }
}
