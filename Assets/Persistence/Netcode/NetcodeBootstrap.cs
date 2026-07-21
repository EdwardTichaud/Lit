using Unity.Netcode;
using Lit.Performance;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bootstrap runtime pour Netcode (NetworkManager, prefabs, scene objects).
public class NetcodeBootstrap : MonoBehaviour
{
    private static NetcodeBootstrap instance;

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool autoCreateNetworkManager = true;
    [SerializeField] private bool autoCreateLauncher = true;
    [SerializeField] private bool autoCreateSpawner = true;
    [SerializeField] private bool autoCreateLobbyUI = true;
    [SerializeField] private bool autoCreateConnectionApproval = true;
    [SerializeField] private bool autoCreatePersistentWorldSystems = true;
    [SerializeField] private bool enableSceneManagement = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntime()
    {
        if (instance != null)
        {
            return;
        }

        NetcodeBootstrap existing = null;
#if UNITY_2023_1_OR_NEWER
        existing = FindAnyObjectByType<NetcodeBootstrap>();
#else
        existing = FindAnyObjectByType<NetcodeBootstrap>();
#endif
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject host = new GameObject("NetcodeBootstrap");
        host.AddComponent<NetcodeBootstrap>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (dontDestroyOnLoad)
        {
            RuntimePersistenceUtility.DontDestroyOnLoadRoot(gameObject);
        }

        LocalInputRouter.EnsureInitialized();
        EnsureNetworkManager();
        EnsurePersistentWorldSystems();

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

        NetcodeSceneObjectInstaller.PrepareScene(SceneManager.GetActiveScene());
        NetcodePrefabRegistry.Refresh();
        SyncLobbyUI(SceneManager.GetActiveScene());

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneTransitionProfiler.Mark($"Initialisation Netcode debut ({scene.name})");
        EnsureNetworkManager();
        NetcodeSceneObjectInstaller.PrepareScene(scene);
        NetcodePrefabRegistry.Refresh();

        SyncLobbyUI(scene);
        SceneTransitionProfiler.Mark($"Initialisation Netcode fin ({scene.name})");
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

        if (SaveSessionManager.Instance.CurrentSessionType != SaveSessionType.Multiplayer)
        {
            return false;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsHost)
        {
            return false;
        }

        return true;
    }

    private void EnsureNetworkManager()
    {
        NetworkManager manager = ResolveNetworkManager();
        if (manager != null)
        {
            if (dontDestroyOnLoad)
            {
                RuntimePersistenceUtility.DontDestroyOnLoadRoot(manager.gameObject);
            }

            EnsureNetworkConfig(manager);
            DestroyDuplicateNetworkManagers(manager);
            return;
        }

        if (!autoCreateNetworkManager)
        {
            return;
        }

        GameObject managerHost = new GameObject("NetworkManager");
        if (dontDestroyOnLoad)
        {
            RuntimePersistenceUtility.DontDestroyOnLoadRoot(managerHost);
        }

        manager = managerHost.AddComponent<NetworkManager>();
        managerHost.AddComponent<UnityTransport>();

        EnsureNetworkConfig(manager);
        DestroyDuplicateNetworkManagers(manager);
    }

    private static NetworkManager ResolveNetworkManager()
    {
        if (NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton;
        }

        NetworkManager[] managers = FindNetworkManagers();
        if (managers == null || managers.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < managers.Length; i++)
        {
            NetworkManager manager = managers[i];
            if (manager != null && manager.IsListening)
            {
                return manager;
            }
        }

        return managers[0];
    }

    private static NetworkManager[] FindNetworkManagers()
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return FindObjectsOfType<NetworkManager>(true);
#endif
    }

    private static void DestroyDuplicateNetworkManagers(NetworkManager keep)
    {
        if (keep == null)
        {
            return;
        }

        NetworkManager[] managers = FindNetworkManagers();
        if (managers == null || managers.Length <= 1)
        {
            return;
        }

        for (int i = 0; i < managers.Length; i++)
        {
            NetworkManager candidate = managers[i];
            if (candidate == null || candidate == keep)
            {
                continue;
            }

            if (candidate.IsListening)
            {
                candidate.Shutdown();
            }

            Destroy(candidate.gameObject);
        }
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

    private void EnsurePersistentWorldSystems()
    {
        if (!autoCreatePersistentWorldSystems)
        {
            return;
        }

        NetcodeRuntimeUtilities.GetOrAdd<NetworkObjectRegistry>(gameObject);
        NetcodeRuntimeUtilities.GetOrAdd<SpawnManager>(gameObject);
        NetcodeRuntimeUtilities.GetOrAdd<WorldRulesStateManager>(gameObject);
        NetcodeRuntimeUtilities.GetOrAdd<WorldStateManager>(gameObject);
        NetcodeRuntimeUtilities.GetOrAdd<PersistentWorldSyncOverlay>(gameObject);
        NetcodeRuntimeUtilities.GetOrAdd<JoinSyncSystem>(gameObject);
        NetcodeRuntimeUtilities.GetOrAdd<WorldSaveAdapter>(gameObject);
    }
}
