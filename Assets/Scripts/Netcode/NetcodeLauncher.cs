using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

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
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsListening)
        {
            return;
        }

        NetcodePrefabRegistry.EnsureInitialized();
        NetcodeSceneObjectInstaller.PrepareActiveScene();
        ApplyConnectionPayload(manager);
        EnsureTransport(manager);
        ConfigureTransport(manager, connectAddress, connectPort, listenAddress);
        manager.StartHost();
    }

    public void StartClient()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsListening)
        {
            return;
        }

        NetcodePrefabRegistry.EnsureInitialized();
        NetcodeSceneObjectInstaller.PrepareActiveScene();
        ApplyConnectionPayload(manager);
        EnsureTransport(manager);
        ConfigureTransport(manager, connectAddress, connectPort, null);
        manager.StartClient();
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
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsListening)
        {
            return false;
        }

        NetcodePrefabRegistry.EnsureInitialized();
        NetcodeSceneObjectInstaller.PrepareActiveScene();
        ApplyConnectionPayload(manager);
        EnsureTransport(manager);
        ConfigureTransport(manager, address, port, listenOverride ?? listenAddress);
        return manager.StartHost();
    }

    public bool StartClientWithConnection(string address, ushort port)
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
        ConfigureTransport(manager, address, port, null);
        return manager.StartClient();
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

        if (string.IsNullOrWhiteSpace(address))
        {
            address = connectAddress;
        }

        if (port == 0)
        {
            port = connectPort;
        }

        if (!string.IsNullOrWhiteSpace(listenOverride))
        {
            transport.SetConnectionData(address, port, listenOverride);
        }
        else
        {
            transport.SetConnectionData(address, port);
        }
    }
}
