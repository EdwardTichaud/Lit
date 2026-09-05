using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lives with NetcodeBootstrap; menu destruction never owns/cancels networking.
public sealed class PrivateSessionService : MonoBehaviour
{
    private const string StateMessage = "lit.private.state.v1";
    private const string CommandMessage = "lit.private.command.v1";
    public static PrivateSessionService Instance { get; private set; }
    public PrivateSessionPhase Phase { get; private set; }
    public PrivateSessionError Error { get; private set; }
    public bool CanRetry => Phase == PrivateSessionPhase.Failed && Error != PrivateSessionError.Incompatible;
    public string Message { get; private set; } = string.Empty;
    public PrivateLobbyState Lobby { get; private set; } = new PrivateLobbyState();
    public event Action Changed;
    public bool IsBusy => Phase == PrivateSessionPhase.Preparing || Phase == PrivateSessionPhase.Connecting ||
        Phase == PrivateSessionPhase.Loading || Phase == PrivateSessionPhase.Returning;
    public bool IsActive => Phase != PrivateSessionPhase.Idle && Phase != PrivateSessionPhase.Failed;
    public bool IsHost => manager != null && manager.IsHost;
    public string JoinCode => launcher != null ? launcher.ActiveRelayJoinCode : string.Empty;
    public ulong LocalClientId => manager != null ? manager.LocalClientId : ulong.MaxValue;
    private NetworkManager manager;
    private NetcodeLauncher launcher;
    private CancellationTokenSource attempt;
    private int generation;
    private float deadline;
    private float nextRequest;
    private bool messagesRegistered;
    private bool returning;

    private void Awake() { Instance = this; }
    private void OnDestroy()
    {
        generation++;
        attempt?.Cancel();
        attempt?.Dispose();
        Unhook();
        if (Instance == this) Instance = null;
    }

    public bool StartHost() => Begin(true, null);
    public bool Join(string code) => Begin(false, code);

    private bool Begin(bool host, string code)
    {
        if (IsActive) return false;
        manager = NetworkManager.Singleton;
        launcher = GetComponent<NetcodeLauncher>();
        if (manager == null || launcher == null || manager.IsListening || manager.ShutdownInProgress)
        { Fail(PrivateSessionError.Unavailable, "La connexion précédente se ferme. Réessayez dans un instant."); return false; }
        if (!host && !NetcodeRelayCode.IsValid(code))
        { Fail(PrivateSessionError.Unavailable, "Code d’invitation invalide."); return false; }
        try
        {
            GameplayRuntimeReset.PrepareForGameplayStart("private_session_prepare");
            if (!host) SaveSessionManager.Instance?.ClearActiveSave();
            SaveSessionManager.Instance?.SetCurrentSessionType(SaveSessionType.Multiplayer);
            Lobby = new PrivateLobbyState();
            if (host)
            {
                PrivateSessionRoster roster = Resources.Load<PrivateSessionRoster>("PrivateSessionRoster");
                CharacterData[] characters = roster != null ? roster.Resolve() : Array.Empty<CharacterData>();
                if (characters.Length == 0) throw new InvalidOperationException("Aucun personnage disponible pour cette partie.");
                Lobby.characterIds = characters.Select(c => c.characterId).ToArray();
                Lobby.characterNames = characters.Select(c => c.characterName).ToArray();
                Lobby.sessionName = SaveSessionManager.Instance?.CurrentSessionName ?? "Partie privée";
                Lobby.saveName = SaveSessionManager.Instance?.CurrentSaveName ?? "Nouvelle partie";
            }
            Hook();
            attempt?.Dispose();
            attempt = new CancellationTokenSource();
            int id = ++generation;
            deadline = Time.unscaledTime + 30f;
            SetPhase(PrivateSessionPhase.Preparing, host ? "Création du salon…" : "Recherche de la partie…");
            _ = ConnectAsync(host, code, id, attempt.Token);
            return true;
        }
        catch (Exception ex) { Debug.LogException(ex, this); Fail(PrivateSessionError.Storage, "Impossible de préparer cette partie. Vérifiez la sauvegarde."); return false; }
    }

    private async Task ConnectAsync(bool host, string code, int id, CancellationToken token)
    {
        try
        {
            NetcodeRelayResult result = host ? await launcher.StartRelayHostAsync(token) : await launcher.StartRelayClientAsync(code, token);
            if (this == null || token.IsCancellationRequested || id != generation) return;
            if (!result.Succeeded) { Fail(result.ErrorKind, result.Error); return; }
            RegisterMessages();
            if (host)
            {
                Lobby.Add(manager.LocalClientId);
                Lobby.phase = PrivateSessionPhase.Lobby;
                SetPhase(PrivateSessionPhase.Lobby, "Invitez vos amis, choisissez vos personnages et confirmez que vous êtes prêts.");
                Broadcast();
            }
            else
            {
                deadline = Time.unscaledTime + 15f;
                SetPhase(PrivateSessionPhase.Connecting, "Connexion au salon…");
                nextRequest = 0f;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (this != null && id == generation) { Debug.LogException(ex, this); Fail(PrivateSessionError.Unavailable, "Le service de connexion est indisponible. Réessayez."); }
        }
    }

    private void Update()
    {
        if (!IsActive) return;
        RegisterMessages();
        if (Phase == PrivateSessionPhase.Returning) return;
        if ((Phase == PrivateSessionPhase.Preparing || Phase == PrivateSessionPhase.Connecting || Phase == PrivateSessionPhase.Loading) && Time.unscaledTime >= deadline)
        { Fail(PrivateSessionError.Timeout, "Le délai de connexion ou de chargement est dépassé. Réessayez."); return; }
        if (manager == null) { Fail(PrivateSessionError.Unavailable, "La session a été fermée."); return; }
        if (!manager.IsServer && manager.IsConnectedClient && Phase == PrivateSessionPhase.Connecting && Time.unscaledTime >= nextRequest)
        { nextRequest = Time.unscaledTime + 1f; SendCommand("refresh", null); }
        if (Phase == PrivateSessionPhase.Loading && SceneManager.GetActiveScene().name != MainMenuController.DefaultMenuSceneName &&
            GameFlowService.Instance != null && !GameFlowService.Instance.IsTransitioning &&
            (IsHost || (JoinSyncSystem.Instance != null && JoinSyncSystem.Instance.IsLocalWorldReady)))
        {
            SetPhase(PrivateSessionPhase.Playing, string.Empty);
            if (IsHost) { Lobby.phase = PrivateSessionPhase.Playing; Broadcast(); }
        }
        if ((Phase == PrivateSessionPhase.Lobby || Phase == PrivateSessionPhase.Playing) && !manager.IsListening)
            Fail(PrivateSessionError.Unavailable, "L’hôte a fermé la partie. Vous pouvez rejoindre une nouvelle invitation.");
    }

    private void Hook()
    {
        manager.OnClientConnectedCallback += Connected;
        manager.OnClientDisconnectCallback += Disconnected;
        manager.OnTransportFailure += TransportFailed;
    }
    private void Unhook()
    {
        if (manager == null) return;
        manager.OnClientConnectedCallback -= Connected;
        manager.OnClientDisconnectCallback -= Disconnected;
        manager.OnTransportFailure -= TransportFailed;
        if (messagesRegistered && manager.CustomMessagingManager != null)
        {
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(StateMessage);
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(CommandMessage);
        }
        messagesRegistered = false;
    }
    private void RegisterMessages()
    {
        if (messagesRegistered || manager == null || !manager.IsListening || manager.CustomMessagingManager == null) return;
        manager.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, ReceiveState);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(CommandMessage, ReceiveCommand);
        messagesRegistered = true;
    }
    public bool EnsureReservation(ulong id)
    {
        return manager != null && manager.IsServer && Lobby.Add(id);
    }
    private void Connected(ulong id)
    {
        RegisterMessages();
        if (!manager.IsServer) return;
        if (!Lobby.Add(id)) { manager.DisconnectClient(id, "Aucun personnage disponible."); return; }
        Broadcast();
    }
    private void Disconnected(ulong id)
    {
        if (returning || !IsActive) return;
        if (manager != null && manager.IsServer && id != manager.LocalClientId)
        {
            Lobby.members.RemoveAll(m => m.clientId == id);
            NetcodePlayerSessionRegistry.Unregister(id);
            Lobby.InvalidateReady();
            Broadcast();
            return;
        }
        string reason = manager != null ? manager.DisconnectReason : string.Empty;
        Fail(PrivateSessionError.Unavailable, string.IsNullOrWhiteSpace(reason) ? "L’hôte a fermé la partie ou la connexion a été perdue." : reason);
    }
    private void TransportFailed() { if (IsActive && !returning) Fail(PrivateSessionError.Unavailable, "Connexion interrompue. Vérifiez votre accès Internet puis réessayez."); }

    public void Reserve(string characterId) => SendCommand("reserve", characterId);
    public void ToggleReady() => SendCommand("ready", null);
    public void Launch()
    {
        if (!IsHost || !Lobby.CanStart) return;
        Lobby.phase = PrivateSessionPhase.Loading;
        deadline = Time.unscaledTime + 120f;
        SetPhase(PrivateSessionPhase.Loading, "Chargement de la partie…");
        Broadcast();
        if (!GameFlowService.StartOrLoadGame()) Fail(PrivateSessionError.Scene, "Impossible de charger la partie.");
    }
    public void CharacterAssigned(ulong id, string characterId)
    {
        if (!IsHost) return;
        PrivateLobbyMember member = Lobby.members.Find(m => m.clientId == id);
        if (member != null) { member.characterId = characterId; Lobby.revision++; Broadcast(); }
    }
    public string ReservedCharacter(ulong id) => Lobby.members.Find(m => m.clientId == id)?.characterId;

    [Serializable] private sealed class Command { public string action; public string characterId; public int revision; }
    private void SendCommand(string action, string characterId)
    {
        Command command = new Command { action = action, characterId = characterId, revision = Lobby.revision };
        if (IsHost) { ApplyCommand(manager.LocalClientId, command); return; }
        if (manager == null || !manager.IsConnectedClient || !messagesRegistered) return;
        Send(CommandMessage, NetworkManager.ServerClientId, JsonUtility.ToJson(command));
    }
    private void ReceiveCommand(ulong sender, FastBufferReader reader)
    {
        if (!IsHost || !manager.ConnectedClientsIds.Contains(sender) || reader.Length > 2048) return;
        try { reader.ReadValueSafe(out string json); ApplyCommand(sender, JsonUtility.FromJson<Command>(json)); }
        catch (Exception ex) { Debug.LogWarning("Commande de salon invalide : " + ex.Message, this); }
    }
    private void ApplyCommand(ulong sender, Command command)
    {
        if (command == null) return;
        PrivateLobbyMember member = Lobby.members.Find(m => m.clientId == sender);
        if (member == null) return;
        if (Lobby.phase == PrivateSessionPhase.Lobby && command.revision == Lobby.revision)
        {
            if (command.action == "reserve") Lobby.Reserve(sender, command.characterId);
            if (command.action == "ready") { member.ready = !member.ready; Lobby.revision++; }
        }
        Broadcast();
    }
    private void Broadcast()
    {
        if (!IsHost || !messagesRegistered) return;
        string json = JsonUtility.ToJson(Lobby);
        foreach (ulong id in manager.ConnectedClientsIds)
            if (id != manager.LocalClientId) Send(StateMessage, id, json);
        Changed?.Invoke();
    }
    private void Send(string name, ulong id, string json)
    {
        using (FastBufferWriter writer = new FastBufferWriter(8192, Allocator.Temp))
        { writer.WriteValueSafe(json); manager.CustomMessagingManager.SendNamedMessage(name, id, writer, NetworkDelivery.ReliableSequenced); }
    }
    private void ReceiveState(ulong sender, FastBufferReader reader)
    {
        if (manager == null || manager.IsServer || sender != NetworkManager.ServerClientId || reader.Length > 8192 || !IsActive) return;
        try
        {
            reader.ReadValueSafe(out string json);
            PrivateLobbyState incoming = JsonUtility.FromJson<PrivateLobbyState>(json);
            if (incoming == null || incoming.members == null || incoming.members.Count > 4) return;
            Lobby = incoming;
            if (Lobby.phase == PrivateSessionPhase.Lobby) SetPhase(PrivateSessionPhase.Lobby, "Choisissez votre personnage et confirmez que vous êtes prêt.");
            else if (Lobby.phase == PrivateSessionPhase.Loading || Lobby.phase == PrivateSessionPhase.Playing)
            {
                if (Phase != PrivateSessionPhase.Loading && Phase != PrivateSessionPhase.Playing) deadline = Time.unscaledTime + 120f;
                if (Phase != PrivateSessionPhase.Playing) { SetPhase(PrivateSessionPhase.Loading, "Synchronisation de la partie…"); LoadingScreenService.Show(Message); }
            }
        }
        catch (Exception ex) { Debug.LogWarning("État de salon invalide : " + ex.Message, this); }
    }

    public void Leave() => Fail(PrivateSessionError.Cancelled, "Vous avez quitté la session.");
    public void ReportSyncFailure(string message) => Fail(PrivateSessionError.Timeout, message);
    public void AcknowledgeError() { if (Phase == PrivateSessionPhase.Failed) SetPhase(PrivateSessionPhase.Idle, string.Empty); }
    private void Fail(PrivateSessionError error, string message)
    {
        if (returning) return;
        returning = true;
        generation++;
        attempt?.Cancel();
        Unhook();
        Error = error;
        Message = message;
        if (launcher != null) launcher.Shutdown();
        else NetcodeBootstrap.ShutdownActiveNetworkManager();
        Phase = PrivateSessionPhase.Returning;
        Changed?.Invoke();
        StartCoroutine(ReturnRoutine());
    }
    private IEnumerator ReturnRoutine()
    {
        // Wait for NGO shutdown and any committed Unity scene operation before returning.
        while (manager != null && (manager.IsListening || manager.ShutdownInProgress)) yield return null;
        if (GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning)
            yield return GameFlowService.Instance.FinishCancelledSessionTransition();
        if (SceneManager.GetActiveScene().name != MainMenuController.DefaultMenuSceneName)
        {
            if (!GameFlowService.OpenMainMenu()) Debug.LogError("Impossible de revenir au menu principal.", this);
            while (GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning) yield return null;
        }
        returning = false;
        Phase = PrivateSessionPhase.Failed;
        LoadingScreenService.HideImmediately();
        Changed?.Invoke();
    }
    private void SetPhase(PrivateSessionPhase phase, string message)
    {
        Phase = phase;
        Message = message;
        if (phase != PrivateSessionPhase.Failed) Error = PrivateSessionError.None;
        Changed?.Invoke();
    }
}
