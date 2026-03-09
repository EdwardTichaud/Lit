using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bootstrap runtime pour Netcode (NetworkManager, prefabs, scene objects).
public class NetcodeBootstrap : MonoBehaviour
{
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool autoCreateNetworkManager = true;
    [SerializeField] private bool autoCreateLauncher = true;
    [SerializeField] private bool autoCreateSpawner = true;
    [SerializeField] private bool autoCreateLobbyUI = true;
    [SerializeField] private bool autoCreateConnectionApproval = true;
    [SerializeField] private bool enableSceneManagement = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRuntime()
    {
        NetcodeBootstrap existing = null;
#if UNITY_2023_1_OR_NEWER
        existing = FindFirstObjectByType<NetcodeBootstrap>();
#else
        existing = FindObjectOfType<NetcodeBootstrap>();
#endif
        if (existing != null)
        {
            return;
        }

        GameObject host = new GameObject("NetcodeBootstrap");
        host.AddComponent<NetcodeBootstrap>();
    }

    private void Awake()
    {
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        LocalInputRouter.EnsureInitialized();
        EnsureNetworkManager();

        if (autoCreateLauncher)
        {
            NetcodeRuntimeUtilities.GetOrAdd<NetcodeLauncher>(gameObject);
        }

        if (autoCreateSpawner)
        {
            NetcodeRuntimeUtilities.GetOrAdd<NetcodePlayerSpawner>(gameObject);
        }

        if (autoCreateConnectionApproval)
        {
            NetcodeRuntimeUtilities.GetOrAdd<NetcodeConnectionApproval>(gameObject);
        }

        SyncLobbyUI(SceneManager.GetActiveScene());

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        NetcodeSceneObjectInstaller.PrepareScene(scene);
        NetcodePrefabRegistry.Refresh();

        SyncLobbyUI(scene);
    }

    private static bool IsMenuScene(string sceneName)
    {
        return string.Equals(sceneName, MainMenuController.DefaultMenuSceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void SyncLobbyUI(Scene scene)
    {
        if (ShouldCreateLobbyUI(scene))
        {
            NetcodeRuntimeUtilities.GetOrAdd<NetcodeLobbyUI>(gameObject);
            return;
        }

        NetcodeLobbyUI lobby = GetComponent<NetcodeLobbyUI>();
        if (lobby != null)
        {
            Destroy(lobby);
        }
    }

    private bool ShouldCreateLobbyUI(Scene scene)
    {
        if (!autoCreateLobbyUI || IsMenuScene(scene.name))
        {
            return false;
        }

        if (SaveSessionManager.Instance == null)
        {
            return false;
        }

        return SaveSessionManager.Instance.CurrentSessionType == SaveSessionType.Multiplayer;
    }

    private void EnsureNetworkManager()
    {
        if (NetworkManager.Singleton != null)
        {
            EnsureNetworkConfig(NetworkManager.Singleton);
            return;
        }

#if UNITY_2023_1_OR_NEWER
        NetworkManager existing = FindFirstObjectByType<NetworkManager>();
#else
        NetworkManager existing = FindObjectOfType<NetworkManager>();
#endif
        if (existing != null)
        {
            EnsureNetworkConfig(existing);
            return;
        }

        if (!autoCreateNetworkManager)
        {
            return;
        }

        GameObject managerHost = new GameObject("NetworkManager");
        DontDestroyOnLoad(managerHost);

        NetworkManager manager = managerHost.AddComponent<NetworkManager>();
        managerHost.AddComponent<UnityTransport>();

        EnsureNetworkConfig(manager);
    }

    private void EnsureNetworkConfig(NetworkManager manager)
    {
        if (manager == null)
        {
            return;
        }

        if (manager.NetworkConfig == null)
        {
            manager.NetworkConfig = new NetworkConfig();
        }

        manager.NetworkConfig.EnableSceneManagement = enableSceneManagement;
        manager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
        manager.NetworkConfig.ConnectionApproval = autoCreateConnectionApproval;

        if (manager.NetworkConfig.NetworkTransport == null)
        {
            NetworkTransport transport = manager.GetComponent<NetworkTransport>();
            if (transport == null)
            {
                transport = manager.gameObject.AddComponent<UnityTransport>();
            }

            manager.NetworkConfig.NetworkTransport = transport;
        }
    }
}
