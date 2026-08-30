using Unity.Netcode;
using Lit.Performance;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bootstrap runtime pour Netcode (NetworkManager, prefabs, scene objects).
[DefaultExecutionOrder(-10000)]
public class NetcodeBootstrap : MonoBehaviour
{
    private static NetcodeBootstrap instance;
    private static bool applicationQuitting;

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool autoCreateNetworkManager = true;
    [SerializeField] private bool autoCreateLauncher = true;
    [SerializeField] private bool autoCreateSpawner = true;
    [SerializeField] private bool autoCreateLobbyUI = true;
    [SerializeField] private bool autoCreateConnectionApproval = true;
    [SerializeField] private bool autoCreatePersistentWorldSystems = true;
    [SerializeField] private bool enableSceneManagement = true;
    [SerializeField] private bool disablePortalAudioListeners = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntime()
    {
        applicationQuitting = false;
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

        DisablePortalAudioListenersInChildren();
        LocalInputRouter.EnsureInitialized();
        EnsureNetworkManager();
        EnsurePersistentWorldSystems();

        if (autoCreateLauncher)
        {
            NetcodeRuntimeUtilities.GetOrAdd<NetcodeLauncher>(gameObject);
            NetcodeRuntimeUtilities.GetOrAdd<NetcodeRelaySessionOverlay>(gameObject);
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

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
        // NetworkManager already performs its own shutdown from
        // OnApplicationQuit/OnDestroy. Preparing its private fields here made
        // that second shutdown dispose an already-cleared SceneManager, which
        // produced a NullReferenceException while the application closed.
        // Keep the flag for our diagnostics, but let NGO own its teardown.
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneTransitionProfiler.Mark($"Initialisation Netcode debut ({scene.name})");
        DisablePortalAudioListenersInChildren();
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
        // L'ancienne UI IP/port ne doit jamais etre exposee dans le flux Relay.
        // Le code d'invitation est affiche par NetcodeRelaySessionOverlay.
        return false;
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
                SafeShutdownNetworkManager(candidate);
            }

            Destroy(candidate.gameObject);
        }
    }

    private static void ShutdownNetworkManagerBeforeUnityTeardown()
    {
#if UNITY_EDITOR
        NetworkManager[] managers = FindNetworkManagers();
        if (managers == null || managers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < managers.Length; i++)
        {
            NetworkManager manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            PrepareNetworkManagerForEditorTeardown(manager);
            SafeShutdownNetworkManager(manager);
        }
#else
        ShutdownActiveNetworkManager();
#endif
    }

    public static void ShutdownActiveNetworkManager()
    {
        SafeShutdownNetworkManager(NetworkManager.Singleton);
    }

    private static void SafeShutdownNetworkManager(NetworkManager manager)
    {
        if (manager == null || !manager.IsListening)
        {
            return;
        }

        try
        {
            manager.Shutdown();
        }
        catch (System.NullReferenceException ex)
        {
            if (!applicationQuitting)
            {
                Debug.LogException(ex, manager);
            }
        }
    }

#if UNITY_EDITOR
    private static void PrepareNetworkManagerForEditorTeardown(NetworkManager manager)
    {
        if (manager == null)
        {
            return;
        }

        SetPrivateField(manager, "m_ShuttingDown", true);
        ClearPrivateField(manager, "<SpawnManager>k__BackingField");
        ClearPrivateField(manager, "<SceneManager>k__BackingField");
        ClearPrivateField(manager, "<NetworkTimeSystem>k__BackingField");
        ClearPrivateField(manager, "<NetworkTickSystem>k__BackingField");
    }

    private static void ClearPrivateField(object target, string fieldName)
    {
        SetPrivateField(target, fieldName, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null || string.IsNullOrEmpty(fieldName))
        {
            return;
        }

        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field == null)
        {
            return;
        }

        field.SetValue(target, value);
    }
#endif

    private void DisablePortalAudioListenersInChildren()
    {
        if (!disablePortalAudioListeners)
        {
            return;
        }

        AudioListener[] listeners = GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null || !IsPortalAudioListener(listener))
            {
                continue;
            }

            listener.enabled = false;
        }
    }

    private static bool IsPortalAudioListener(AudioListener listener)
    {
        Camera camera = listener.GetComponent<Camera>();
        if (camera == null)
        {
            return false;
        }

        string objectName = listener.name;
        return camera.targetTexture != null ||
               (!string.IsNullOrEmpty(objectName) &&
                objectName.IndexOf("PortalCam", System.StringComparison.OrdinalIgnoreCase) >= 0);
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
