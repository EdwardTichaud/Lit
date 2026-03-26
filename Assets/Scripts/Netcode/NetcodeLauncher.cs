using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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
    [Header("Shortcuts")]
    [SerializeField] private bool enableShortcuts = true;
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

    private NetcodeConnectionAttemptInfo lastConnectionAttempt;

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
            NetworkManager.Singleton.Shutdown();
        }
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

        if (!NetcodeSessionCode.TryCreateEndpoint(code, normalizedAddress, sessionBasePort, sessionPortRange, out endpoint))
        {
            error = "Code de session invalide.";
            return false;
        }

        error = string.Empty;
        return true;
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
        WorldSaveAdapter adapter = FindFirstObjectByType<WorldSaveAdapter>();
#else
        WorldSaveAdapter adapter = FindObjectOfType<WorldSaveAdapter>();
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
