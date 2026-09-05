using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct NetcodeConnectionAttemptInfo
{
    public string Mode;
    public string Code;
    public string Address;
    public ushort Port;
    public string ListenAddress;
    public bool SessionDerived;

    public bool IsValid
    {
        get
        {
            return !string.IsNullOrWhiteSpace(Mode)
                && !string.IsNullOrWhiteSpace(Address)
                && Port != 0;
        }
    }

    public string EndpointLabel
    {
        get
        {
            return $"{Address}:{Port}";
        }
    }

    public string ListenLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ListenAddress))
            {
                return EndpointLabel;
            }

            return $"{ListenAddress}:{Port}";
        }
    }
}

// Commandes rapides pour lancer un host/client en runtime.
public class NetcodeLauncher : MonoBehaviour
{
    [Header("Legacy direct-IP shortcuts (development only)")]
    [SerializeField, Tooltip("Desactive par defaut : Relay est le seul parcours joueur expose.")]
    private bool enableShortcuts = false;
    [SerializeField] private Key startHostKey = Key.F5;
    [SerializeField] private Key startClientKey = Key.F6;
    [SerializeField] private Key shutdownKey = Key.F7;

    [Header("Connection")]
    [SerializeField] private string connectAddress = "127.0.0.1";
    [SerializeField] private ushort connectPort = 7777;
    [SerializeField] private string listenAddress = "0.0.0.0";

    [Header("Session Connection")]
    [SerializeField] private ushort sessionBasePort = 7000;
    [SerializeField] private ushort sessionPortRange = 1000;
    [SerializeField] private string hostLoopbackAddress = "127.0.0.1";
    [SerializeField] private string defaultJoinAddress = "127.0.0.1";
    [SerializeField] private bool logConnectionFlow = true;

    [Header("Relay (remote test)")]
    [SerializeField, Min(1)] private int relayMaxJoiningPlayers = 3;
    [SerializeField] private string relayConnectionType = "dtls";

    private NetcodeConnectionAttemptInfo lastConnectionAttempt;

    public string ActiveRelayJoinCode { get; private set; } = string.Empty;

    public ushort SessionBasePort => sessionBasePort;

    public ushort SessionPortRange => sessionPortRange;

    public string SessionListenAddress => ResolveListenAddress();

    public string SessionHostLoopbackAddress => ResolveHostLoopbackAddress();

    public string SessionDefaultJoinAddress => ResolveJoinAddress(string.Empty);

    private void Update()
    {
        if (!enableShortcuts)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[startHostKey].wasPressedThisFrame)
        {
            StartHost();
        }
        else if (Keyboard.current[startClientKey].wasPressedThisFrame)
        {
            StartClient();
        }
        else if (Keyboard.current[shutdownKey].wasPressedThisFrame)
        {
            Shutdown();
        }
    }

    public void StartHost()
    {
        StartHostInternal(
            BuildConnectionAttempt(
                mode: "host_shortcut",
                code: string.Empty,
                address: connectAddress,
                port: connectPort,
                listenOverride: listenAddress,
                sessionDerived: false));
    }

    public void StartClient()
    {
        StartClientInternal(
            BuildConnectionAttempt(
                mode: "client_shortcut",
                code: string.Empty,
                address: connectAddress,
                port: connectPort,
                listenOverride: null,
                sessionDerived: false));
    }

    public void Shutdown()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetcodeBootstrap.ShutdownActiveNetworkManager();
        }

        ActiveRelayJoinCode = string.Empty;
    }

    /// <summary>
    /// Cree une allocation Relay et demarre le host. Cette voie ne configure
    /// jamais l'endpoint IP direct utilise uniquement par les raccourcis dev.
    /// </summary>
    public async Task<NetcodeRelayResult> StartRelayHostAsync(CancellationToken cancellationToken = default)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            return NetcodeRelayResult.Failure("NetworkManager manquant.");
        }

        if (manager.IsListening)
        {
            return NetcodeRelayResult.Failure("Une connexion reseau est deja active.");
        }

        try
        {
            await EnsureUnityServicesSignedInAsync();
            cancellationToken.ThrowIfCancellationRequested();
            NetcodePrefabRegistry.EnsureInitialized();
            NetcodeSceneObjectInstaller.PrepareActiveScene();
            TryRestoreHostWorldBeforeStart();
            ApplyConnectionPayload(manager);
            EnsureTransport(manager);

            int connections = Mathf.Max(1, relayMaxJoiningPlayers);
            Unity.Services.Relay.Models.Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(connections);
            cancellationToken.ThrowIfCancellationRequested();
            UnityTransport transport = manager.GetComponent<UnityTransport>();
            transport.SetRelayServerData(allocation.ToRelayServerData(ResolveRelayConnectionType()));

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            cancellationToken.ThrowIfCancellationRequested();
            if (!manager.StartHost())
            {
                return NetcodeRelayResult.Failure("Le host Relay n'a pas pu demarrer.");
            }

            ActiveRelayJoinCode = NetcodeRelayCode.Normalize(joinCode);
            lastConnectionAttempt = new NetcodeConnectionAttemptInfo
            {
                Mode = "relay_host",
                Code = ActiveRelayJoinCode,
                Address = "relay",
                Port = 1,
                ListenAddress = "relay",
                SessionDerived = true
            };
            Debug.Log($"[NetcodeRelay] host started code='{ActiveRelayJoinCode}' maxJoiners={connections}", this);
            return NetcodeRelayResult.Success(ActiveRelayJoinCode);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NetcodeRelay] host start failed: {exception.Message}", this);
            return ToRelayFailure(exception);
        }
    }

    /// <summary>
    /// Rejoint une allocation Relay existante et demarre le client NGO.
    /// </summary>
    public async Task<NetcodeRelayResult> StartRelayClientAsync(string joinCode, CancellationToken cancellationToken = default)
    {
        string normalizedCode = NetcodeRelayCode.Normalize(joinCode);
        if (!NetcodeRelayCode.IsValid(normalizedCode))
        {
            return NetcodeRelayResult.Failure("Code d'invitation Relay invalide.");
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            return NetcodeRelayResult.Failure("NetworkManager manquant.");
        }

        if (manager.IsListening)
        {
            return NetcodeRelayResult.Failure("Une connexion reseau est deja active.");
        }

        try
        {
            await EnsureUnityServicesSignedInAsync();
            cancellationToken.ThrowIfCancellationRequested();
            NetcodePrefabRegistry.EnsureInitialized();
            NetcodeSceneObjectInstaller.PrepareActiveScene();
            ApplyConnectionPayload(manager);
            EnsureTransport(manager);

            Unity.Services.Relay.Models.JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(normalizedCode);
            cancellationToken.ThrowIfCancellationRequested();
            UnityTransport transport = manager.GetComponent<UnityTransport>();
            transport.SetRelayServerData(allocation.ToRelayServerData(ResolveRelayConnectionType()));

            if (!manager.StartClient())
            {
                return NetcodeRelayResult.Failure("Le client Relay n'a pas pu demarrer.");
            }

            ActiveRelayJoinCode = normalizedCode;
            lastConnectionAttempt = new NetcodeConnectionAttemptInfo
            {
                Mode = "relay_client",
                Code = normalizedCode,
                Address = "relay",
                Port = 1,
                ListenAddress = string.Empty,
                SessionDerived = true
            };
            Debug.Log($"[NetcodeRelay] client started code='{normalizedCode}'", this);
            return NetcodeRelayResult.Success(normalizedCode);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NetcodeRelay] join failed: {exception.Message}", this);
            return ToRelayFailure(exception);
        }
    }

    private async Task EnsureUnityServicesSignedInAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument.StartsWith("-lit-profile=", StringComparison.Ordinal))
                    AuthenticationService.Instance.SwitchProfile(argument.Substring("-lit-profile=".Length));
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private string ResolveRelayConnectionType()
    {
        string value = string.IsNullOrWhiteSpace(relayConnectionType) ? "dtls" : relayConnectionType.Trim().ToLowerInvariant();
        return value == "udp" || value == "dtls" || value == "wss" ? value : "dtls";
    }

    private static NetcodeRelayResult ToRelayFailure(Exception exception)
    {
        if (exception is OperationCanceledException)
            return NetcodeRelayResult.Failure("Connexion annulée.", PrivateSessionError.Cancelled);
        if (exception is RelayServiceException relay &&
            (relay.Reason == RelayExceptionReason.JoinCodeNotFound || relay.Reason == RelayExceptionReason.AllocationNotFound))
            return NetcodeRelayResult.Failure("Ce code d’invitation est introuvable ou expiré. Demandez un nouveau code à l’hôte.", PrivateSessionError.CodeExpired);
        return NetcodeRelayResult.Failure("Le service de connexion est indisponible. Vérifiez votre accès Internet puis réessayez.");
    }

    public bool StartHostWithConnection(string address, ushort port, string listenOverride = null)
    {
        return StartHostInternal(
            BuildConnectionAttempt(
                mode: "host_direct",
                code: string.Empty,
                address: address,
                port: port,
                listenOverride: listenOverride,
                sessionDerived: false));
    }

    public bool StartClientWithConnection(string address, ushort port)
    {
        return StartClientInternal(
            BuildConnectionAttempt(
                mode: "client_direct",
                code: string.Empty,
                address: address,
                port: port,
                listenOverride: null,
                sessionDerived: false));
    }

    public bool StartHostWithSessionEndpoint(NetcodeSessionEndpoint endpoint, string listenOverride = null)
    {
        if (!endpoint.IsValid)
        {
            return false;
        }

        return StartHostInternal(
            BuildConnectionAttempt(
                mode: "host_session",
                code: endpoint.Code,
                address: endpoint.Address,
                port: endpoint.Port,
                listenOverride: listenOverride,
                sessionDerived: true));
    }

    public bool StartClientWithSessionEndpoint(NetcodeSessionEndpoint endpoint)
    {
        if (!endpoint.IsValid)
        {
            return false;
        }

        return StartClientInternal(
            BuildConnectionAttempt(
                mode: "client_session",
                code: endpoint.Code,
                address: endpoint.Address,
                port: endpoint.Port,
                listenOverride: null,
                sessionDerived: true));
    }

    public bool TryResolveSessionPort(string code, out ushort port, out string normalizedCode)
    {
        return NetcodeSessionCode.TryGetPort(code, sessionBasePort, sessionPortRange, out port, out normalizedCode);
    }

    public bool TryResolveHostEndpoint(string code, out NetcodeSessionEndpoint endpoint, out string error)
    {
        string address = ResolveHostLoopbackAddress();
        if (string.IsNullOrWhiteSpace(address))
        {
            endpoint = default;
            error = "Adresse loopback host invalide.";
            return false;
        }

        if (!NetcodeSessionCode.TryCreateEndpoint(code, address, sessionBasePort, sessionPortRange, out endpoint))
        {
            error = "Code de session invalide.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryResolveJoinEndpoint(string code, string address, out NetcodeSessionEndpoint endpoint, out string error)
    {
        string normalizedAddress = ResolveJoinAddress(address);
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            endpoint = default;
            error = "Adresse IP invalide.";
            return false;
        }

        if (!NetcodeSessionCode.TryCreateEndpointFromJoinInput(code, normalizedAddress, sessionBasePort, sessionPortRange, out endpoint))
        {
            error = "Code de session invalide.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryResolveJoinEndpoint(string joinCode, out NetcodeSessionEndpoint endpoint, out string error)
    {
        return TryResolveJoinEndpoint(joinCode, string.Empty, out endpoint, out error);
    }

    public string CreateJoinCode(string sessionCode, string advertisedAddress)
    {
        string normalizedAddress = ResolveJoinAddress(advertisedAddress);
        return NetcodeSessionCode.CreateJoinCode(sessionCode, normalizedAddress);
    }

    public string ResolveJoinAddress(string address)
    {
        string fallback = NetcodeSessionCode.NormalizeAddress(defaultJoinAddress, ResolveHostLoopbackAddress());
        return NetcodeSessionCode.NormalizeAddress(address, fallback);
    }

    public string ResolveHostLoopbackAddress()
    {
        return NetcodeSessionCode.NormalizeAddress(hostLoopbackAddress, "127.0.0.1");
    }

    public string ResolveListenAddress(string listenOverride = null)
    {
        return NetcodeSessionCode.NormalizeAddress(listenOverride, listenAddress);
    }

    public bool TryGetLastConnectionAttempt(out NetcodeConnectionAttemptInfo attempt)
    {
        attempt = lastConnectionAttempt;
        return attempt.IsValid;
    }

    private static void ApplyConnectionPayload(NetworkManager manager)
    {
        if (manager == null || manager.NetworkConfig == null)
        {
            return;
        }

        manager.NetworkConfig.ConnectionData = NetcodeClientIdentity.BuildPayload();
    }

    private static void EnsureTransport(NetworkManager manager)
    {
        if (manager == null)
        {
            return;
        }

        if (manager.NetworkConfig == null)
        {
            manager.NetworkConfig = new NetworkConfig();
        }

        if (manager.NetworkConfig.NetworkTransport != null)
        {
            return;
        }

        NetworkTransport transport = manager.GetComponent<NetworkTransport>();
        if (transport == null)
        {
            transport = manager.gameObject.AddComponent<UnityTransport>();
        }

        manager.NetworkConfig.NetworkTransport = transport;
    }

    private bool StartHostInternal(NetcodeConnectionAttemptInfo attempt)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsListening)
        {
            return false;
        }

        NetcodePrefabRegistry.EnsureInitialized();
        NetcodeSceneObjectInstaller.PrepareActiveScene();
        TryRestoreHostWorldBeforeStart();
        ApplyConnectionPayload(manager);
        EnsureTransport(manager);
        ConfigureTransport(manager, attempt.Address, attempt.Port, attempt.ListenAddress);
        lastConnectionAttempt = attempt;
        bool started = manager.StartHost();
        LogConnectionAttempt(started, attempt);
        return started;
    }

    private bool StartClientInternal(NetcodeConnectionAttemptInfo attempt)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsListening)
        {
            return false;
        }

        NetcodePrefabRegistry.EnsureInitialized();
        NetcodeSceneObjectInstaller.PrepareActiveScene();
        ApplyConnectionPayload(manager);
        EnsureTransport(manager);
        ConfigureTransport(manager, attempt.Address, attempt.Port, null);
        lastConnectionAttempt = attempt;
        bool started = manager.StartClient();
        LogConnectionAttempt(started, attempt);
        return started;
    }

    private NetcodeConnectionAttemptInfo BuildConnectionAttempt(
        string mode,
        string code,
        string address,
        ushort port,
        string listenOverride,
        bool sessionDerived)
    {
        string normalizedAddress = sessionDerived
            ? ResolveJoinAddress(address)
            : NetcodeSessionCode.NormalizeAddress(address, connectAddress);
        ushort resolvedPort = port != 0 ? port : connectPort;
        string normalizedListen = mode.StartsWith("host", System.StringComparison.OrdinalIgnoreCase)
            ? ResolveListenAddress(listenOverride)
            : string.Empty;

        return new NetcodeConnectionAttemptInfo
        {
            Mode = string.IsNullOrWhiteSpace(mode) ? "unknown" : mode,
            Code = NetcodeSessionCode.Normalize(code),
            Address = normalizedAddress,
            Port = resolvedPort,
            ListenAddress = normalizedListen,
            SessionDerived = sessionDerived
        };
    }

    private void ConfigureTransport(NetworkManager manager, string address, ushort port, string listenOverride)
    {
        if (manager == null)
        {
            return;
        }

        UnityTransport transport = manager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            return;
        }

        address = NetcodeSessionCode.NormalizeAddress(address, connectAddress);
        port = port == 0 ? connectPort : port;
        listenOverride = NetcodeSessionCode.NormalizeAddress(listenOverride);

        if (!string.IsNullOrWhiteSpace(listenOverride))
        {
            transport.SetConnectionData(address, port, listenOverride);
        }
        else
        {
            transport.SetConnectionData(address, port);
        }
    }

    private void LogConnectionAttempt(bool started, NetcodeConnectionAttemptInfo attempt)
    {
        if (!logConnectionFlow)
        {
            return;
        }

        string code = string.IsNullOrWhiteSpace(attempt.Code) ? "n/a" : attempt.Code;
        string listen = string.IsNullOrWhiteSpace(attempt.ListenAddress) ? "n/a" : attempt.ListenAddress;
        Debug.Log(
            $"[NetcodeConnect] mode='{attempt.Mode}' started={started} code='{code}' target='{attempt.EndpointLabel}' listen='{listen}' sessionDerived={attempt.SessionDerived}",
            this);
    }

    private static void TryRestoreHostWorldBeforeStart()
    {
#if UNITY_2023_1_OR_NEWER
        WorldSaveAdapter adapter = FindAnyObjectByType<WorldSaveAdapter>();
#else
        WorldSaveAdapter adapter = FindAnyObjectByType<WorldSaveAdapter>();
#endif
        if (adapter == null || !adapter.HasSavedWorldSnapshot())
        {
            return;
        }

        bool restored = adapter.EnsureHostWorldRestoredFromSave("netcode_launcher_start_host");
        if (!restored)
        {
            PersistentWorldDebug.Error(
                $"host start requested before world snapshot restore completed path='{adapter.LastRestoreSnapshotPath}' reason='{adapter.LastRestoreReason}'",
                adapter);
        }
    }
}
