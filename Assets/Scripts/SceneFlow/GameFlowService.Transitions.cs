using System;
using System.Collections;
using System.Collections.Generic;
using Lit.Performance;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class GameFlowService
{
    private string transitionFailure;
    private bool zoneTransitionInProgress;
    public bool IsZoneTransitionInProgress => zoneTransitionInProgress;
    public bool HasUnsettledSceneOperations => pendingPostLoadingScenes.Count > 0 ||
        pendingSceneOperations.Exists(operation => operation != null && !operation.isDone);
    public bool RequiresWorldRecovery => WorldStateManager.Instance != null && WorldStateManager.Instance.RequiresRecovery;
    public string LastTransitionFailure { get; private set; }
    private static int ExpectedTransitionPlayers => NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer
        ? Math.Max(1, NetworkManager.Singleton.ConnectedClientsIds.Count) : 1;

    private static HashSet<string> ManifestSceneSet(ZoneManifest manifest)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (manifest == null) return names;
        names.Add(manifest.PrimarySceneName);
        foreach (string name in manifest.LoadingSceneNames)
            if (!CycleProgressionService.ShouldSkipScene(name)) names.Add(name);
        foreach (string name in manifest.PostLoadingSceneNames)
            if (SceneManager.GetSceneByName(name).isLoaded) names.Add(name);
        return names;
    }

    private void FailTransition(string reason)
    {
        if (string.IsNullOrEmpty(transitionFailure)) transitionFailure = reason;
        LastTransitionFailure = reason;
        Debug.LogWarning("[GameFlow] Transition refusee : " + reason, this);
    }

    private static IEnumerator CheckedSceneOperation(string name, LoadSceneMode mode, bool unload, string message)
    {
        var flow = Instance;
        if (flow == null) yield break;
        var manager = NetworkManager.Singleton;
        double timeout = manager != null && manager.IsListening
            ? Math.Max(1, manager.NetworkConfig.LoadSceneTimeOut) : 120;
        double deadline = Time.realtimeSinceStartupAsDouble + timeout;
        bool network = manager != null && manager.IsListening;
        SceneTransitionProfiler.Mark((unload ? "Dechargement demande (" : "Prechargement demande (") + name + ")");
        if (network)
        {
            if (!manager.IsServer || manager.SceneManager == null || !manager.NetworkConfig.EnableSceneManagement)
            { flow.FailTransition("Operation de scene sans autorite serveur : " + name); yield break; }
            var completedClients = new HashSet<ulong>();
            var timedOutClients = new HashSet<ulong>();
            var expectedClients = new HashSet<ulong>(manager.ConnectedClientsIds);
            bool completed = false;
            bool accepted = false;
            void OnEvent(SceneEvent e)
            {
                if (!accepted || e.SceneName != name) return;
                if (e.SceneEventType == (unload ? SceneEventType.UnloadComplete : SceneEventType.LoadComplete))
                    completedClients.Add(e.ClientId);
                if (e.SceneEventType != (unload ? SceneEventType.UnloadEventCompleted : SceneEventType.LoadEventCompleted)) return;
                completed = true;
                if (e.ClientsThatCompleted != null) foreach (ulong id in e.ClientsThatCompleted) completedClients.Add(id);
                if (e.ClientsThatTimedOut != null) foreach (ulong id in e.ClientsThatTimedOut) timedOutClients.Add(id);
            }
            var sceneManager = manager.SceneManager;
            sceneManager.ActiveSceneSynchronizationEnabled = true;
            sceneManager.OnSceneEvent += OnEvent;
            try
            {
                SceneEventProgressStatus status;
                do
                {
                    if (!manager.IsListening) { flow.FailTransition("Transport arrete : " + name); yield break; }
                    if (!TryRequestNetworkScene(sceneManager, name, mode, unload, out status, out string requestIssue))
                    { flow.FailTransition(requestIssue); yield break; }
                    if (status == SceneEventProgressStatus.SceneEventInProgress) yield return null;
                }
                while (status == SceneEventProgressStatus.SceneEventInProgress && Time.realtimeSinceStartupAsDouble < deadline);
                if (status != SceneEventProgressStatus.Started)
                { flow.FailTransition("Netcode " + status + " : " + name); yield break; }
                accepted = true;
                deadline = Time.realtimeSinceStartupAsDouble + timeout;
                while (!completed && manager.IsListening && Time.realtimeSinceStartupAsDouble < deadline + 5)
                { SceneTransitionProfiler.Pulse(); yield return null; }
                bool hostReady = unload ? !SceneManager.GetSceneByName(name).isLoaded : SceneManager.GetSceneByName(name).isLoaded;
                if (!manager.IsListening || !hostReady || timedOutClients.Contains(NetworkManager.ServerClientId))
                { flow.FailTransition("L'hote n'a pas termine " + name); yield break; }
                // Never start another scene event or disconnect from inside an NGO event callback.
                bool disconnected = false;
                foreach (ulong id in expectedClients)
                    if (id != NetworkManager.ServerClientId && manager.ConnectedClients.ContainsKey(id) &&
                        (timedOutClients.Contains(id) || !completedClients.Contains(id)))
                    {
                        manager.DisconnectClient(id, "Le chargement de la zone n'a pas termine. Vous pouvez rejoindre la session a nouveau.");
                        disconnected = true;
                    }
                if (disconnected) yield return null;
                if (!completed)
                {
                    double settle = Time.realtimeSinceStartupAsDouble + 5;
                    while (!completed && manager.IsListening && Time.realtimeSinceStartupAsDouble < settle) yield return null;
                }
                if (!completed) flow.FailTransition("L'evenement reseau n'a pas termine : " + name);
            }
            finally { sceneManager.OnSceneEvent -= OnEvent; }
            yield break;
        }
        AsyncOperation operation = null;
        try
        {
            operation = unload ? SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName(name)) : SceneManager.LoadSceneAsync(name, mode);
        }
        catch (Exception exception) { flow.FailTransition(name + " : " + exception.Message); }
        if (operation == null) { flow.FailTransition("Aucune operation Unity pour " + name); yield break; }
        flow.pendingSceneOperations.Add(operation);
        if (!unload)
        {
            operation.allowSceneActivation = false;
            while (operation.progress < .9f && Time.realtimeSinceStartupAsDouble < deadline)
            { SceneTransitionProfiler.Pulse(); yield return null; }
            yield return null;
            SceneTransitionProfiler.Mark("Prechargement termine (" + name + ")");
            using (SceneTransitionProfiler.SceneActivation.Auto()) operation.allowSceneActivation = true;
            SceneTransitionProfiler.Mark("Activation demandee (" + name + ")");
        }
        while (!operation.isDone && Time.realtimeSinceStartupAsDouble < deadline)
        { SceneTransitionProfiler.Pulse(); yield return null; }
        if (!operation.isDone) flow.FailTransition("Delai Unity depasse : " + name);
        else
        {
            if (!unload) LoadingScreenService.SetProgress(1, message);
            SceneTransitionProfiler.Mark((unload ? "Dechargement termine (" : "Activation terminee (") + name + ")");
        }
        flow.pendingSceneOperations.RemoveAll(op => op == null || op.isDone);
    }

    private static bool TryRequestNetworkScene(NetworkSceneManager manager, string name, LoadSceneMode mode,
        bool unload, out SceneEventProgressStatus status, out string issue)
    {
        status = default;
        issue = null;
        try
        {
            status = unload ? manager.UnloadScene(SceneManager.GetSceneByName(name)) : manager.LoadScene(name, mode);
            return true;
        }
        catch (Exception exception) { issue = name + " : " + exception.Message; return false; }
    }

    private IEnumerator SafeZoneTransition(ZoneManifest destination, IReadOnlyList<Pose> requested, string spawnId)
    {
        zoneTransitionInProgress = true;
        InputFocusStack.Push(this);
        InputModeCoordinator.Enter(this, InputMode.Disabled);
        transitionFailure = null;
        LastTransitionFailure = null;
        string sourceName = activeGameplaySceneName;
        ZoneManifest sourceManifest = ResolveGameplayManifest(sourceName);
        var sourceScenes = CaptureLoadedGameplayScenes();
        var previouslyLoaded = CaptureLoadedUnitySceneNames();
        var playerPoses = new Dictionary<GameObject, Pose>();
        WorldSnapshot checkpoint = null;
        bool committed = false;
        bool destinationStarted = false;
        SceneTransitionProfiler.Begin(sourceName + " -> " + destination.PrimarySceneName);
        try
        {
            yield return LoadingScreenService.ShowAndWaitForPresentation(destination.LoadingMessage);
            bool drained = false;
            yield return FinishPostLoadingRequests(true, success => drained = success);
            if (!drained) FailTransition("Operations precedentes non terminees.");
            var world = WorldStateManager.Instance;
            if (transitionFailure == null && world != null)
            {
                checkpoint = world.CaptureSnapshot("transition checkpoint");
                if (!world.LastCaptureSucceeded || checkpoint == null)
                {
                    checkpoint = null;
                    FailTransition("Impossible de conserver la zone de depart.");
                }
                else if (!world.CaptureScenesBeforeUnload(sourceScenes))
                    FailTransition("Impossible d'archiver la zone de depart.");
            }
            if (SquadManager.Instance == null) FailTransition("Escouade absente.");
            else foreach (var player in SquadManager.Instance.GetSceneTransitionPlayers())
                if (player != null) playerPoses[player] = new Pose(player.transform.position, player.transform.rotation);
            if (transitionFailure == null)
            {
                destinationStarted = true;
                yield return StopProximityStreaming();
                ReleaseZonePresentation(preserveAudio: true);
                loadedZoneSceneNames.Clear();
                yield return LoadAdditiveRoutine(destination.PrimarySceneName, destination.LoadingMessage);
                if (transitionFailure == null)
                {
                    AddLoadedGameplayScene(destination.PrimarySceneName);
                    yield return LoadManifestLoadingScenes(destination, destination.LoadingMessage);
                }
                if (!ValidateManifestScenesLoaded(destination, out string missing)) FailTransition(missing);
            }
            var targetNames = ManifestSceneSet(destination);
            if (transitionFailure == null)
            {
                var scene = SceneManager.GetSceneByName(destination.PrimarySceneName);
                SceneManager.SetActiveScene(scene);
                InstallZonePresentation(destination, destination.PrimarySceneName);
                if (world != null && !world.RestoreArchivedScenes(loadedZoneSceneNames.FindAll(n => !previouslyLoaded.Contains(n))))
                    FailTransition("La restauration de la destination a echoue.");
            }
            if (transitionFailure == null)
            {
                yield return RebuildNavigationForLoadedWorldRoutine("arrivee " + destination.PrimarySceneName, destination);
                if (NavMeshWorldService.Instance != null && !NavMeshWorldService.Instance.IsReady)
                    FailTransition("Navigation de la destination indisponible.");
                var poses = requested;
                if (poses == null)
                {
                    var found = ResolveZoneSpawns(spawnId, destination.PrimarySceneName, targetNames);
                    var resolved = new List<Pose>();
                    foreach (var point in found) resolved.Add(new Pose(point.transform.position, point.transform.rotation));
                    poses = resolved;
                }
                if (!SceneTransitionPlacement.Validate(SquadManager.Instance.GetSceneTransitionPlayers(), poses, targetNames, out string placementIssue))
                    FailTransition(placementIssue);
                if (transitionFailure == null && !SquadManager.Instance.TryMoveSquadToDestinationPoints(poses, out placementIssue))
                    FailTransition(placementIssue);
            }
            if (transitionFailure == null)
            {
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                PreserveUccSimulationManager();
                activeGameplaySceneName = destination.PrimarySceneName;
                foreach (string old in sourceScenes)
                {
                    if (targetNames.Contains(old) || ShouldKeepHubLoadedForLateJoin() && IsHubManifestScene(old)) continue;
                    yield return UnloadSceneIfLoaded(old);
                    if (transitionFailure != null) break;
                }
                committed = transitionFailure == null;
            }
            if (!committed)
            {
                string failure = transitionFailure ?? "Transition interrompue.";
                transitionFailure = null;
                // Drain native requests before removing only scenes created by this attempt.
                foreach (var operation in pendingSceneOperations.ToArray())
                {
                    if (operation == null || operation.isDone) continue;
                    operation.allowSceneActivation = true;
                    double drainDeadline = Time.realtimeSinceStartupAsDouble + 120;
                    while (!operation.isDone && Time.realtimeSinceStartupAsDouble < drainDeadline) yield return null;
                    if (!operation.isDone) FailTransition("Operation native toujours en cours.");
                }
                if (transitionFailure == null)
                {
                    // A failed retirement may already have removed a source scene; reload it before rollback.
                    foreach (string old in sourceScenes) yield return LoadAdditiveRoutine(old, "Retour a la zone precedente...");
                    var sourceScene = SceneManager.GetSceneByName(sourceName);
                    if (sourceScene.IsValid() && sourceScene.isLoaded) SceneManager.SetActiveScene(sourceScene);
                    foreach (string added in new List<string>(loadedZoneSceneNames))
                        if (!previouslyLoaded.Contains(added) && !sourceScenes.Contains(added)) yield return UnloadSceneIfLoaded(added);
                    loadedZoneSceneNames.Clear(); loadedZoneSceneNames.AddRange(sourceScenes);
                    if (destinationStarted && world != null && checkpoint != null)
                    {
                        var network = NetworkManager.Singleton;
                        if (network != null && network.IsListening && network.IsServer)
                            RetainConnectedCheckpointPlayers(checkpoint, new HashSet<ulong>(network.ConnectedClientsIds));
                        if (!world.ApplySnapshot(checkpoint, true)) FailTransition("Echec de restauration du point de retour.");
                    }
                    if (transitionFailure == null && playerPoses.Count > 0)
                    {
                        // A timed-out client may have left: never give its saved pose to the next player.
                        var rollbackPoses = new List<Pose>();
                        foreach (var player in SquadManager.Instance.GetSceneTransitionPlayers())
                        {
                            if (!playerPoses.TryGetValue(player, out Pose pose))
                            { FailTransition("Un personnage du point de retour a change."); break; }
                            rollbackPoses.Add(pose);
                        }
                        if (transitionFailure == null && !SquadManager.Instance.TryMoveSquadToDestinationPoints(rollbackPoses, out string rollbackIssue))
                            FailTransition(rollbackIssue);
                    }
                    activeGameplaySceneName = sourceName;
                    InstallZonePresentation(sourceManifest, sourceName);
                    yield return RebuildNavigationForLoadedWorldRoutine("retour annule", sourceManifest);
                }
                LastTransitionFailure = failure;
                if (transitionFailure != null)
                {
                    world?.RequireRecovery(transitionFailure);
                    // Do not strand the player behind a permanent, non-interactive black overlay.
                    // Recovery has latched exports off; leave without saving through the normal teardown.
                    Debug.LogError("[GameFlow] Retour impossible, fermeture sans sauvegarde : " + transitionFailure, this);
                    yield return RejectInitialStart();
                    yield break;
                }
                InfoBoxUI.TryShowToast(failure + " La zone precedente a ete conservee.", 8);
            }
            EnforceSingleAudioListener();
            RestoreLocalGameplayInputAfterSessionStart();
            StartProximityStreaming(activeGameplaySceneName);
            LoadingScreenService.HideWhenSceneIsReady();
            yield return LoadingScreenService.WaitUntilHidden();
            SceneTransitionProfiler.End(committed ? "Transition validee" : "Zone precedente restauree");
            StartPostLoading(committed ? destination : sourceManifest);
        }
        finally
        {
            if (!RequiresWorldRecovery)
            {
                InputFocusStack.Pop(this);
                InputModeCoordinator.Exit(this);
            }
            zoneTransitionInProgress = false;
            transitionRoutine = null;
        }
    }

    private static void RetainConnectedCheckpointPlayers(WorldSnapshot checkpoint, ISet<ulong> connected)
    {
        // Only the private rollback copy is filtered. CharacterData and persistent player-to-character
        // bindings stay in the session store for rejoin; never resurrect an actor whose owner timed out.
        var disconnectedActors = new HashSet<string>(StringComparer.Ordinal);
        checkpoint.Players.RemoveAll(player =>
        {
            if (player == null || connected.Contains(player.OwnerClientId)) return false;
            if (!string.IsNullOrEmpty(player.ControlledObjectId)) disconnectedActors.Add(player.ControlledObjectId);
            return true;
        });
        checkpoint.RuntimeObjects.RemoveAll(actor => actor != null && disconnectedActors.Contains(actor.PersistentId));
        foreach (var archive in checkpoint.SceneArchives)
            archive.RuntimeObjects.RemoveAll(actor => actor != null && disconnectedActors.Contains(actor.PersistentId));
    }

    private static IReadOnlyList<ZoneSpawnPoint> ResolveZoneSpawns(string spawnId, string primary, ICollection<string> scope)
    {
        string id = string.IsNullOrWhiteSpace(spawnId) ? "Default" : spawnId;
        var found = ZoneSpawnPoint.FindAll(id, scope);
        // Only an implicit editor/new-game arrival may use the zone's authored non-Default group.
        if (found.Count == 0 && string.IsNullOrWhiteSpace(spawnId))
        {
            var first = ZoneSpawnPoint.FindFirstInScene(primary);
            if (first != null) found = ZoneSpawnPoint.FindAll(first.SpawnId, scope);
        }
        return found;
    }

    private IEnumerator RejectInitialStart()
    {
        string reason = transitionFailure ?? "Le demarrage de la partie a echoue.";
        LastTransitionFailure = reason;
        var manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
        {
            NetcodeBootstrap.ShutdownActiveNetworkManager();
            yield return null;
        }
        DestroyGameplaySession();
        yield return null;
        loadedZoneSceneNames.Clear();
        activeGameplaySceneName = null;
        transitionFailure = null;
        yield return LoadSingleRoutine(menuSceneName, "Retour au menu...");
        if (transitionFailure != null)
        {
            LoadingScreenService.SetProgress(0, "Le retour au menu a echoue. La sauvegarde n'a pas ete modifiee.");
            transitionRoutine = null;
            yield break;
        }
        EnforceSingleAudioListener();
        LoadingScreenService.HideWhenSceneIsReady();
        InfoBoxUI.TryShowToast(reason + " La sauvegarde n'a pas ete modifiee.", 8);
        SceneTransitionProfiler.End("Demarrage refuse");
        transitionRoutine = null;
    }
}
