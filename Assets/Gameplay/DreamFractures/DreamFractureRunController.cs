using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.DreamFractures
{
    [Serializable]
    public sealed class DreamFractureRunState
    {
        public DreamFracturePhase phase;
        public string definitionId, runId, proposalId, message;
        public ulong[] participants = Array.Empty<ulong>();
        public ulong[] ready = Array.Empty<ulong>();
        public int initialPlayers, score;
        public double seconds, countdownEnds, publishedAt;
        public bool hadDisconnect;
        public bool paused;
        public string[] scoredEvents = Array.Empty<string>();
        public DreamFractureResult result;
    }

    /// <summary>Session lifetime. Owns runs, never the narrative progression service.</summary>
    public sealed partial class DreamFractureRunController : MonoBehaviour
    {
        private const string CommandChannel = "Lit.DreamFractures.Command";
        private const string StateChannel = "Lit.DreamFractures.State";
        [Serializable] private sealed class Command { public string action, definitionId, proposalId; }
        public static DreamFractureRunController Instance { get; private set; }
        public DreamFractureRunState State { get; private set; } = new DreamFractureRunState();
        public DreamFractureCatalog Catalog { get; private set; }
        public DreamFractureRecords Records { get; private set; }
        public DreamFractureDefinitionSO Definition => Catalog != null ? Catalog.Find(State.definitionId) : null;
        public bool Authority => !Online || NetworkManager.Singleton.IsServer;
        public bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        public ulong LocalId => Online ? NetworkManager.Singleton.LocalClientId : 0;
        public static bool BlocksLateJoin(ulong clientId) => Instance != null &&
            Instance.State.phase >= DreamFracturePhase.Loading &&
            Instance.State.phase <= DreamFracturePhase.Returning &&
            !Instance.State.participants.Contains(clientId);
        public double RemainingCountdown => Math.Max(0, State.countdownEnds - Now);
        public double DisplaySeconds => State.seconds + (!Authority && State.phase == DreamFracturePhase.Running &&
            !State.paused ? Math.Max(0, Now - State.publishedAt) : 0);
        private double Now => Online ? NetworkManager.Singleton.ServerTime.Time : Time.unscaledTimeAsDouble;
        private NetworkManager network;
        private DreamFractureAttempt attempt;
        private IDreamFractureChallenge challenge;
        private double stageStarted, nextPublish, nextRequest;
        private string recordedRun;
        private bool countdownLocked;
        private DreamFracturePortal portal;

        public static DreamFractureRunController Ensure(Transform parent)
        {
            if (Instance != null) return Instance;
            return parent.gameObject.AddComponent<DreamFractureRunController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Catalog = Resources.Load<DreamFractureCatalog>("DreamFractures/Catalog");
            try { Records = new DreamFractureRecords(Path.Combine(Application.persistentDataPath, "DreamFractures", "records.json")); }
            catch (Exception error) { Debug.LogWarning("Records oniriques indisponibles. " + error.Message); }
        }

        private void OnDestroy()
        {
            UnhookNetwork();
            ReleaseFocus();
            if (Instance == this) Instance = null;
        }

        private void HookNetwork()
        {
            var current = Online ? NetworkManager.Singleton : null;
            if (network == current) return;
            UnhookNetwork();
            network = current;
            if (network == null) return;
            network.CustomMessagingManager.RegisterNamedMessageHandler(CommandChannel, ReceiveCommand);
            network.CustomMessagingManager.RegisterNamedMessageHandler(StateChannel, ReceiveState);
        }

        private void UnhookNetwork()
        {
            if (network != null && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(CommandChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(StateChannel);
            }
            network = null;
        }

        private static void Send(NetworkManager manager, string channel, ulong target, string json)
        {
            using var writer = new FastBufferWriter(System.Text.Encoding.UTF8.GetByteCount(json) * 2 + 16, Allocator.Temp);
            writer.WriteValueSafe(json);
            manager.CustomMessagingManager.SendNamedMessage(channel, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        private void ReceiveCommand(ulong sender, FastBufferReader reader)
        {
            if (!Authority || !network.ConnectedClientsIds.Contains(sender)) return;
            reader.ReadValueSafe(out string json);
            if (json.Length > 512) return;
            try { Handle(sender, JsonUtility.FromJson<Command>(json)); }
            catch (ArgumentException) { /* Ignore malformed peer commands. */ }
        }

        private void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (Authority || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string json);
            State = JsonUtility.FromJson<DreamFractureRunState>(json);
            AcceptResult();
        }

        public void Request(string action, string definitionId = null)
        {
            HookNetwork();
            var command = new Command { action = action, definitionId = definitionId, proposalId = State.proposalId };
            if (Authority) Handle(LocalId, command);
            else if (network != null) Send(network, CommandChannel, NetworkManager.ServerClientId, JsonUtility.ToJson(command));
        }

        private Transform Player(ulong id) => Online
            ? NetcodePlayerUtils.GetPlayerTransform(id) : LocalPlayerContext.LocalCharacterRoot;
        private static SquadCharacterController Character(Transform root) => root != null
            ? root.GetComponentInChildren<SquadCharacterController>() : null;
        private ulong[] Connected() => Online
            ? NetworkManager.Singleton.ConnectedClientsIds.OrderBy(id => id).ToArray() : new ulong[] { 0 };
        private bool Available(ulong id, bool requirePortal)
        {
            var player = Player(id);
            var character = Character(player);
            if (character == null || character.CurrentHp <= 0) return false;
            var movement = character.GetComponent<LitOpsiveLocomotionBridge>();
            if (movement != null && (movement.IsCombatLockActive || movement.IsExternalLockActive)) return false;
            if (Online && id != NetworkManager.ServerClientId && JoinSyncSystem.Instance != null &&
                !JoinSyncSystem.Instance.IsClientReady(id)) return false;
            if (!requirePortal) return true;
            if (portal == null) portal = FindAnyObjectByType<DreamFracturePortal>();
            return portal != null && portal.IsAvailable(player, portal.gatheringRadius);
        }

        private void Handle(ulong sender, Command command)
        {
            if (command == null) return;
            if (command.action == "state") { Publish(); return; }
            if (command.action == "objective" && State.phase == DreamFracturePhase.Running &&
                State.participants.Contains(sender) && challenge is IDreamFractureInteraction interaction)
            {
                interaction.TryInteract(command.definitionId, Player(sender));
                return;
            }
            bool host = !Online || sender == NetworkManager.ServerClientId;
            if (command.action == "select" && host &&
                (State.phase == DreamFracturePhase.Idle || State.phase == DreamFracturePhase.Preparing) &&
                Available(sender, true))
            {
                var definition = Catalog != null ? Catalog.Find(command.definitionId) : null;
                if (definition == null || !definition.IsValid || !definition.IsVisible || !definition.available) return;
                State = new DreamFractureRunState
                {
                    phase = DreamFracturePhase.Preparing, definitionId = definition.id,
                    participants = Connected(), proposalId = Guid.NewGuid().ToString("N")
                };
                State.initialPlayers = State.participants.Length;
            }
            else if (command.action == "ready" &&
                (State.phase == DreamFracturePhase.Preparing || State.phase == DreamFracturePhase.Results))
            {
                bool atPortal = State.phase == DreamFracturePhase.Preparing;
                if (command.proposalId != State.proposalId || !State.participants.Contains(sender) ||
                    !Available(sender, atPortal)) return;
                var ready = State.ready.ToList();
                if (!ready.Remove(sender)) ready.Add(sender);
                State.ready = ready.ToArray();
            }
            else if (command.action == "cancel" && State.phase == DreamFracturePhase.Preparing)
                State = new DreamFractureRunState();
            else if (command.action == "abandon" && host &&
                (State.phase == DreamFracturePhase.Running || State.phase == DreamFracturePhase.Countdown))
                Finish(DreamFractureOutcome.Abandoned);
            else if (command.action == "return" && host && State.phase == DreamFracturePhase.Results)
                ReturnHome();
            else return;
            Publish();
        }

        private void Update()
        {
            HookNetwork();
            UpdateFocus();
            if (!Authority)
            {
                if (Time.unscaledTimeAsDouble >= nextRequest)
                {
                    nextRequest = Time.unscaledTimeAsDouble + 2;
                    Request("state");
                }
                return;
            }
            var connected = Connected();
            if (State.phase == DreamFracturePhase.Preparing)
            {
                if (!connected.SequenceEqual(State.participants))
                {
                    State.participants = connected;
                    State.initialPlayers = connected.Length;
                    State.ready = Array.Empty<ulong>();
                    State.proposalId = Guid.NewGuid().ToString("N");
                }
                State.ready = State.ready.Where(id => Available(id, true)).ToArray();
                if (State.participants.Length > 0 && State.ready.Length == State.participants.Length) StartTravel();
            }
            else if (State.phase == DreamFracturePhase.Loading)
            {
                if (GameFlowService.Instance != null && !GameFlowService.Instance.IsTransitioning)
                {
                    if (!SceneManager.GetSceneByName(Definition.zone.PrimarySceneName).isLoaded)
                        RecoverFailedTravel();
                    else if (State.participants.Where(connected.Contains).All(id => Available(id, false)))
                    {
                        challenge = FindChallenge();
                        if (challenge == null) { State.message = "Aucun défi configuré dans cette zone."; Finish(DreamFractureOutcome.Failed); }
                        else
                        {
                            challenge.ResetChallenge();
                            State.countdownEnds = Now + 3;
                            State.phase = DreamFracturePhase.Countdown;
                        }
                    }
                }
                if (Time.realtimeSinceStartupAsDouble - stageStarted > 120)
                { State.message = "Chargement du défi interrompu."; Finish(DreamFractureOutcome.Failed); }
            }
            else if (State.phase == DreamFracturePhase.Countdown && RemainingCountdown <= 0)
            {
                State.phase = DreamFracturePhase.Running;
                challenge.BeginChallenge(this);
            }
            else if (State.phase == DreamFracturePhase.Running)
            {
                State.paused = !Online && Time.timeScale <= 0;
                attempt.Tick(State.paused ? 0 : Time.unscaledDeltaTime);
                State.seconds = attempt.Seconds;
                if (!State.participants.Where(connected.Contains).Any(id => Character(Player(id))?.CurrentHp > 0))
                    Finish(DreamFractureOutcome.Failed);
            }
            else if (State.phase == DreamFracturePhase.Results)
            {
                var remaining = State.participants.Where(connected.Contains).ToArray();
                State.ready = State.ready.Where(remaining.Contains).ToArray();
                if (remaining.Length > 0 && remaining.All(State.ready.Contains)) Restart();
            }
            else if (State.phase == DreamFracturePhase.Returning &&
                GameFlowService.Instance != null && !GameFlowService.Instance.IsTransitioning)
            {
                if (IsInMaison()) State = new DreamFractureRunState();
                else { State.phase = DreamFracturePhase.Results; State.message = "Retour impossible. Réessayez."; }
            }
            if (State.phase >= DreamFracturePhase.Loading && State.phase <= DreamFracturePhase.Results &&
                State.participants.Any(id => !connected.Contains(id))) State.hadDisconnect = true;
            if (Time.unscaledTimeAsDouble >= nextPublish)
            { nextPublish = Time.unscaledTimeAsDouble + .2; Publish(); }
        }

        private IDreamFractureChallenge FindChallenge() => FindObjectsByType<MonoBehaviour>()
            .Where(b => b.gameObject.scene.name == Definition.zone.PrimarySceneName)
            .OfType<IDreamFractureChallenge>().FirstOrDefault();
        private bool IsInMaison() => GameFlowService.Instance != null &&
            SceneManager.GetActiveScene().name == GameFlowService.Instance.HubSceneName;

        private void StartTravel()
        {
            var definition = Definition;
            if (definition == null || !definition.IsValid || !IsInMaison()) return;
            State.runId = Guid.NewGuid().ToString("N");
            State.phase = DreamFracturePhase.Loading;
            stageStarted = Time.realtimeSinceStartupAsDouble;
            attempt = new DreamFractureAttempt();
            State.result = null;
            State.score = 0;
            State.scoredEvents = Array.Empty<string>();
            State.seconds = 0;
            State.hadDisconnect = false;
            State.ready = Array.Empty<ulong>();
            if (!GameFlowService.TravelToZone(definition.zone, null)) RecoverFailedTravel();
            CloseCatalog();
            Publish();
        }

        private void RecoverFailedTravel()
        {
            State.phase = DreamFracturePhase.Preparing;
            State.ready = Array.Empty<ulong>();
            State.message = "Le voyage a échoué. Vous pouvez réessayer.";
        }

        private void Restart()
        {
            challenge = FindChallenge();
            if (challenge == null) return;
            State.participants = State.participants.Where(Connected().Contains).ToArray();
            State.initialPlayers = State.participants.Length;
            State.runId = Guid.NewGuid().ToString("N");
            State.result = null;
            State.ready = Array.Empty<ulong>();
            State.score = 0;
            State.scoredEvents = Array.Empty<string>();
            State.seconds = 0;
            State.hadDisconnect = false;
            State.message = null;
            attempt = new DreamFractureAttempt();
            challenge.ResetChallenge();
            State.phase = DreamFracturePhase.Countdown;
            State.countdownEnds = Now + 3;
        }

        public bool ReportScore(IDreamFractureChallenge source, string eventId, int points)
        {
            if (!Authority || State.phase != DreamFracturePhase.Running || source != challenge ||
                !attempt.AddScore(eventId, points)) return false;
            State.score = attempt.Score;
            State.scoredEvents = State.scoredEvents.Concat(new[] { eventId }).ToArray();
            Publish();
            return true;
        }

        public void Resolve(IDreamFractureChallenge source, bool success)
        {
            if (Authority && State.phase == DreamFracturePhase.Running && source == challenge)
                Finish(success ? DreamFractureOutcome.Success : DreamFractureOutcome.Failed);
        }

        private void Finish(DreamFractureOutcome outcome)
        {
            if (State.phase == DreamFracturePhase.Results || attempt == null || Definition == null) return;
            State.result = attempt.Finish(State.runId, Definition, State.initialPlayers, State.hadDisconnect, outcome);
            State.phase = DreamFracturePhase.Results;
            State.proposalId = Guid.NewGuid().ToString("N");
            State.ready = Array.Empty<ulong>();
            AcceptResult();
            Publish();
        }

        private void AcceptResult()
        {
            if (State.result == null || !State.participants.Contains(LocalId) || recordedRun == State.result.runId) return;
            try
            {
                if (Records != null && Records.Record(State.result)) recordedRun = State.result.runId;
            }
            catch (Exception error)
            {
                State.message = "Enregistrement du record impossible.";
                Debug.LogWarning(State.message + " " + error.Message);
            }
        }

        private void ReturnHome()
        {
            if (IsInMaison()) { State = new DreamFractureRunState(); return; }
            if (GameFlowService.ReturnToHub()) { State.phase = DreamFracturePhase.Returning; Publish(); }
            else State.message = "Le retour est indisponible. Réessayez.";
        }

        private void Publish()
        {
            if (!Authority) return;
            State.publishedAt = Now;
            if (Online)
            {
                PrivateSessionService.Instance?.RefreshDreamFractureWaiting();
                string json = JsonUtility.ToJson(State);
                foreach (ulong id in network.ConnectedClientsIds)
                    if (id != NetworkManager.ServerClientId) Send(network, StateChannel, id, json);
            }
        }
    }
}
