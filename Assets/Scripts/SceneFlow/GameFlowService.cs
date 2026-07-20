using System;
using System.Collections;
using System.Collections.Generic;
using Lit.Performance;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unique point d'entree des changements de scenes du jeu.
/// Bootstrap cree ce service avant le chargement de la premiere scene.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class GameFlowService : MonoBehaviour
{
    private const string BootstrapSceneName = "Bootstrap";
    private const string DefaultMenuSceneName = "MainMenu";
    private const string DefaultHubSceneName = "Maison";

    public static GameFlowService Instance { get; private set; }
    /// <summary>Vrai entre la creation de GameplaySessionRoot et le chargement de la scene de jeu.</summary>
    public static bool IsPreparingGameplayScene { get; private set; }

    [SerializeField] private string menuSceneName = DefaultMenuSceneName;
    [SerializeField] private string hubSceneName = DefaultHubSceneName;
    [SerializeField] private bool loadMenuAfterBootstrap = true;
    [Header("Loading messages")]
    [Tooltip("Texte affiche pendant le retour au menu principal.")]
    public string returnToMenuLoadingMessage = "Retour au menu principal...";
    [Tooltip("Prefab instancie au demarrage d'une partie et detruit au retour au menu.")]
    [SerializeField] private GameplaySessionRoot gameplaySessionPrefab;

#if UNITY_EDITOR
    [Header("Editor test startup")]
    [Tooltip("Ignore le menu lors d'un Play lance depuis Bootstrap et ouvre directement la scene de test avec une session de gameplay complete.")]
    [SerializeField] private bool editorStartGameplayDirectly;
    [Tooltip("Nom de la scene de gameplay a ouvrir pour le test. Laisser vide pour utiliser Maison.")]
    [SerializeField] private string editorStartSceneName = DefaultHubSceneName;
    [Tooltip("Identifiant du ZoneSpawnPoint dans la scene de test. Laisser vide pour conserver le spawn normal de la scene.")]
    [SerializeField] private string editorStartSpawnId;
#endif

    private readonly List<string> loadedZoneSceneNames = new List<string>();
    private GameplaySessionRoot gameplaySessionRoot;
    private Coroutine transitionRoutine;
    private Coroutine postLoadingRoutine;
    private int postLoadingGeneration;
    private string activeGameplaySceneName;

    public bool IsTransitioning => transitionRoutine != null;
    public bool HasGameplaySession => gameplaySessionRoot != null;
    public string HubSceneName => hubSceneName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateApplicationRoot()
    {
        if (Instance != null)
        {
            return;
        }

        // En lancement normal, Bootstrap contient deja ApplicationRoot et ce
        // service. Ce filet de securite ne sert qu'aux tests directs d'une
        // scene dans l'editeur.
        if (FindAnyObjectByType<GameFlowService>() != null)
        {
            return;
        }

        GameObject root = new GameObject("ApplicationRoot");
        root.AddComponent<ApplicationRoot>();
        root.AddComponent<GameFlowService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Lorsque le composant est configure dans Bootstrap, ApplicationRoot
        // rend deja toute sa hierarchie persistante. Ne pas detachacher ce
        // service de sa racine, sinon la hierarchie devient trompeuse.
        if (GetComponentInParent<ApplicationRoot>() == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        TryOpenMenuFromBootstrap(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryOpenMenuFromBootstrap(scene);
    }

    private void TryOpenMenuFromBootstrap(Scene scene)
    {
#if UNITY_EDITOR
        if (TryStartEditorGameplayFromBootstrap(scene))
        {
            return;
        }
#endif

        if (!loadMenuAfterBootstrap || !string.Equals(scene.name, BootstrapSceneName, StringComparison.OrdinalIgnoreCase) || IsTransitioning)
        {
            return;
        }

        GameplayRuntimeReset.ResetForMenuScene("bootstrap_startup");
        OpenMainMenu();
    }

#if UNITY_EDITOR
    private bool TryStartEditorGameplayFromBootstrap(Scene scene)
    {
        if (!editorStartGameplayDirectly ||
            !string.Equals(scene.name, BootstrapSceneName, StringComparison.OrdinalIgnoreCase) ||
            IsTransitioning)
        {
            return false;
        }

        string sceneToTest = string.IsNullOrWhiteSpace(editorStartSceneName) ? hubSceneName : editorStartSceneName;
        if (BeginGameplay(sceneToTest, editorStartSpawnId))
        {
            return true;
        }

        Debug.LogError($"[GameFlow] Unable to start editor test scene '{sceneToTest}'. Check Build Settings and the GameplaySessionRoot prefab reference.", this);
        return false;
    }
#endif

    public static bool StartOrLoadGame(string initialSceneName = null)
    {
        if (Instance == null)
        {
            return false;
        }

        return Instance.BeginGameplay(string.IsNullOrWhiteSpace(initialSceneName) ? Instance.hubSceneName : initialSceneName);
    }

    public static bool TravelToZone(ZoneManifest destination, string spawnId = null)
    {
        return Instance != null && Instance.BeginZoneTravel(destination, spawnId);
    }

    public static bool ReturnToHub(string spawnId = null)
    {
        return Instance != null && Instance.BeginReturnToHub(spawnId);
    }

    public static bool OpenMainMenu()
    {
        return Instance != null && Instance.BeginReturnToMenu();
    }

    private bool BeginGameplay(string initialSceneName, string initialSpawnId = null)
    {
        if (IsTransitioning || !CanLoad(initialSceneName))
        {
            return false;
        }

        GameplayRuntimeReset.PrepareForGameplayStart("game_flow_start_gameplay");
        IsPreparingGameplayScene = true;
        if (!EnsureGameplaySession())
        {
            IsPreparingGameplayScene = false;
            return false;
        }
        transitionRoutine = StartCoroutine(LoadInitialGameplayRoutine(initialSceneName, initialSpawnId));
        return true;
    }

    private bool BeginZoneTravel(ZoneManifest destination, string spawnId)
    {
        if (IsTransitioning || !HasGameplaySession || destination == null || !destination.IsValid || !CanLoad(destination.PrimarySceneName))
        {
            return false;
        }

        StopPostLoadingRoutine();
        transitionRoutine = StartCoroutine(TravelToZoneRoutine(destination, spawnId));
        return true;
    }

    private bool BeginReturnToHub(string spawnId)
    {
        if (IsTransitioning || !HasGameplaySession || !CanLoad(hubSceneName))
        {
            return false;
        }

        StopPostLoadingRoutine();
        transitionRoutine = StartCoroutine(ReturnToHubRoutine(spawnId));
        return true;
    }

    private bool BeginReturnToMenu()
    {
        if (IsTransitioning || !CanLoad(menuSceneName))
        {
            return false;
        }

        StopPostLoadingRoutine();
        transitionRoutine = StartCoroutine(ReturnToMenuRoutine());
        return true;
    }

    private IEnumerator LoadInitialGameplayRoutine(string sceneName, string spawnId)
    {
        SceneTransitionProfiler.Begin($"Demarrage -> {sceneName}");
        yield return LoadSingleRoutine(sceneName, "Chargement de la partie...");
        SceneTransitionProfiler.Mark("Scene activee");
        activeGameplaySceneName = sceneName;
        IsPreparingGameplayScene = false;
        SquadManager.Instance?.InitializeForLoadedGameplayScene();
        // Les personnages de l'escouade peuvent etre crees dans Start() par
        // la scene chargee. On attend une frame avant de les placer.
        yield return null;
        SceneTransitionProfiler.Pulse();
        AdoptGameplayManagers();
        SceneTransitionProfiler.Mark("Managers prets");
        RestoreLocalGameplayInputAfterSessionStart();
        PlaceSquadAtSpawn(spawnId);
        SceneTransitionProfiler.Mark("Escouade placee");
        LoadingScreenService.HideWhenSceneIsReady();
        SceneTransitionProfiler.End("Ecran pret a disparaitre");
        transitionRoutine = null;
    }

    private IEnumerator TravelToZoneRoutine(ZoneManifest destination, string spawnId)
    {
        SceneTransitionProfiler.Begin($"{activeGameplaySceneName} -> {destination.PrimarySceneName}");
        // Le son du portail a deja ete lance par PortalController. On attend
        // que l'overlay soit visible avant de demarrer le chargement lourd.
        using (SceneTransitionProfiler.OverlayPresentation.Auto())
        {
            SceneTransitionProfiler.Mark("Presentation de l'overlay");
        }
        yield return LoadingScreenService.ShowAndWaitForPresentation(destination.LoadingMessage);
        SceneTransitionProfiler.ResetFrameGapMeasurement();
        SceneTransitionProfiler.Mark("Overlay opaque");
        yield return LoadAdditiveRoutine(destination.PrimarySceneName, destination.LoadingMessage);
        SceneTransitionProfiler.Mark("Scene de zone activee");
        loadedZoneSceneNames.Add(destination.PrimarySceneName);

        for (int i = 0; i < destination.LoadingSceneNames.Count; i++)
        {
            string additionalScene = destination.LoadingSceneNames[i];
            if (!string.IsNullOrWhiteSpace(additionalScene) && CanLoad(additionalScene))
            {
                yield return LoadAdditiveRoutine(additionalScene, destination.LoadingMessage);
                SceneTransitionProfiler.Mark($"Sous-scene activee ({additionalScene})");
                loadedZoneSceneNames.Add(additionalScene);
            }
        }

        Scene targetScene = SceneManager.GetSceneByName(destination.PrimarySceneName);
        if (targetScene.IsValid() && targetScene.isLoaded)
        {
            SceneManager.SetActiveScene(targetScene);
        }

        if (!string.IsNullOrWhiteSpace(activeGameplaySceneName) && !string.Equals(activeGameplaySceneName, destination.PrimarySceneName, StringComparison.OrdinalIgnoreCase))
        {
            yield return UnloadSceneIfLoaded(activeGameplaySceneName);
            SceneTransitionProfiler.Mark("Scene precedente dechargee");
        }

        activeGameplaySceneName = destination.PrimarySceneName;
        PlaceSquadAtSpawn(spawnId);
        SceneTransitionProfiler.Mark("Escouade placee");
        LoadingScreenService.HideWhenSceneIsReady();
        SceneTransitionProfiler.End("Ecran pret a disparaitre");
        transitionRoutine = null;
        StartPostLoading(destination);
    }

    private IEnumerator ReturnToHubRoutine(string spawnId)
    {
        SceneTransitionProfiler.Begin($"{activeGameplaySceneName} -> {hubSceneName}");
        yield return LoadingScreenService.ShowAndWaitForPresentation("Retour a la Maison...");
        SceneTransitionProfiler.ResetFrameGapMeasurement();
        SceneTransitionProfiler.Mark("Overlay opaque");
        yield return LoadAdditiveRoutine(hubSceneName, "Retour a la Maison...");
        SceneTransitionProfiler.Mark("Maison activee");
        Scene hubScene = SceneManager.GetSceneByName(hubSceneName);
        if (hubScene.IsValid() && hubScene.isLoaded)
        {
            SceneManager.SetActiveScene(hubScene);
        }

        for (int i = loadedZoneSceneNames.Count - 1; i >= 0; i--)
        {
            yield return UnloadSceneIfLoaded(loadedZoneSceneNames[i]);
            SceneTransitionProfiler.Mark($"Sous-scene dechargee ({loadedZoneSceneNames[i]})");
        }

        loadedZoneSceneNames.Clear();
        if (!string.IsNullOrWhiteSpace(activeGameplaySceneName) && !string.Equals(activeGameplaySceneName, hubSceneName, StringComparison.OrdinalIgnoreCase))
        {
            yield return UnloadSceneIfLoaded(activeGameplaySceneName);
            SceneTransitionProfiler.Mark("Scene precedente dechargee");
        }

        activeGameplaySceneName = hubSceneName;
        PlaceSquadAtSpawn(spawnId);
        SceneTransitionProfiler.Mark("Escouade placee");
        LoadingScreenService.HideWhenSceneIsReady();
        SceneTransitionProfiler.End("Ecran pret a disparaitre");
        transitionRoutine = null;
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        SceneTransitionProfiler.Begin($"{activeGameplaySceneName} -> {menuSceneName}");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        DestroyGameplaySession();
        loadedZoneSceneNames.Clear();
        activeGameplaySceneName = null;
        yield return LoadSingleRoutine(menuSceneName, returnToMenuLoadingMessage);
        SceneTransitionProfiler.Mark("Menu active");
        LoadingScreenService.HideWhenSceneIsReady();
        SceneTransitionProfiler.End("Ecran pret a disparaitre");
        transitionRoutine = null;
    }

    private IEnumerator LoadSingleRoutine(string sceneName, string loadingMessage)
    {
        yield return LoadingScreenService.ShowAndWaitForPresentation(loadingMessage);
        SceneTransitionProfiler.ResetFrameGapMeasurement();
        SceneTransitionProfiler.Mark("Overlay opaque");
        AsyncOperation operation;
        using (SceneTransitionProfiler.SceneLoadRequest.Auto())
        {
            operation = LoadScene(sceneName, LoadSceneMode.Single);
        }
        SceneTransitionProfiler.Mark("Prechargement demande");
        yield return WaitForScenePreloadAndActivation(operation, sceneName, loadingMessage);
    }

    private static IEnumerator LoadAdditiveRoutine(string sceneName, string loadingMessage)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            yield break;
        }

        AsyncOperation operation;
        using (SceneTransitionProfiler.SceneLoadRequest.Auto())
        {
            operation = LoadScene(sceneName, LoadSceneMode.Additive);
        }
        SceneTransitionProfiler.Mark($"Prechargement demande ({sceneName})");
        yield return WaitForScenePreloadAndActivation(operation, sceneName, loadingMessage);
    }

    private void StartPostLoading(ZoneManifest destination)
    {
        if (destination.PostLoadingSceneNames.Count == 0)
        {
            return;
        }

        int generation = ++postLoadingGeneration;
        postLoadingRoutine = StartCoroutine(LoadPostLoadingScenesRoutine(destination, generation));
    }

    private IEnumerator LoadPostLoadingScenesRoutine(ZoneManifest destination, int generation)
    {
        yield return LoadingScreenService.WaitUntilHidden();
        if (generation != postLoadingGeneration)
        {
            yield break;
        }

        for (int i = 0; i < destination.PostLoadingSceneNames.Count; i++)
        {
            string sceneName = destination.PostLoadingSceneNames[i];
            if (string.IsNullOrWhiteSpace(sceneName) || !CanLoad(sceneName))
            {
                continue;
            }

            yield return LoadAdditiveAfterGameplayRoutine(sceneName);
            if (generation != postLoadingGeneration)
            {
                yield break;
            }

            if (!loadedZoneSceneNames.Contains(sceneName))
            {
                loadedZoneSceneNames.Add(sceneName);
            }

            // Une scene par passage de frame : les futures phases pourront
            // decouper davantage chaque contenu sans bloquer ce flux.
            yield return null;
        }

        if (generation == postLoadingGeneration)
        {
            postLoadingRoutine = null;
        }
    }

    private static IEnumerator LoadAdditiveAfterGameplayRoutine(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            yield break;
        }

        AsyncOperation operation = LoadScene(sceneName, LoadSceneMode.Additive);
        if (operation == null)
        {
            while (!SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                yield return null;
            }

            yield break;
        }

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        // Le chargement est volontairement sequentiel pour ne pas empiler les
        // activations de scenes pendant que le joueur explore la zone.
        yield return null;
        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForScenePreloadAndActivation(AsyncOperation operation, string sceneName, string loadingMessage)
    {
        if (operation == null)
        {
            while (!SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                SceneTransitionProfiler.Pulse();
                yield return null;
            }

            LoadingScreenService.SetProgress(1f, loadingMessage);
            yield break;
        }

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            SceneTransitionProfiler.Pulse();
            LoadingScreenService.SetProgress(Mathf.Clamp01(operation.progress / 0.9f), loadingMessage);
            yield return null;
        }

        // Tous les assets sont prets. L'activation peut encore provoquer un
        // pic, mais il se produit derriere l'overlay deja opaque.
        LoadingScreenService.SetProgress(0.9f, loadingMessage);
        yield return null;
        SceneTransitionProfiler.Pulse();
        SceneTransitionProfiler.Mark($"Prechargement termine ({sceneName})");
        using (SceneTransitionProfiler.SceneActivation.Auto())
        {
            operation.allowSceneActivation = true;
        }
        SceneTransitionProfiler.Mark($"Activation demandee ({sceneName})");

        while (!operation.isDone)
        {
            SceneTransitionProfiler.Pulse();
            yield return null;
        }

        SceneTransitionProfiler.Pulse();
        LoadingScreenService.SetProgress(1f, loadingMessage);
    }

    private static IEnumerator UnloadSceneIfLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            yield break;
        }

        AsyncOperation operation;
        using (SceneTransitionProfiler.SceneUnloadRequest.Auto())
        {
            operation = UnloadScene(scene);
        }
        SceneTransitionProfiler.Mark($"Dechargement demande ({sceneName})");
        while ((operation != null && !operation.isDone) ||
               (operation == null && scene.isLoaded))
        {
            SceneTransitionProfiler.Pulse();
            yield return null;
        }
    }

    private bool EnsureGameplaySession()
    {
        if (gameplaySessionRoot != null)
        {
            return true;
        }

        if (gameplaySessionPrefab == null)
        {
            Debug.LogError("[GameFlow] GameplaySessionRoot prefab is not assigned on Bootstrap/ApplicationRoot.", this);
            return false;
        }

        gameplaySessionRoot = Instantiate(gameplaySessionPrefab);
        gameplaySessionRoot.name = gameplaySessionPrefab.name;
        return true;
    }

    private void DestroyGameplaySession()
    {
        StopPostLoadingRoutine();
        IsPreparingGameplayScene = false;
        if (gameplaySessionRoot != null)
        {
            Destroy(gameplaySessionRoot.gameObject);
            gameplaySessionRoot = null;
        }

        GameplayRuntimeReset.ResetForMenuScene("game_flow_return_to_menu");
    }

    private void StopPostLoadingRoutine()
    {
        postLoadingGeneration++;
        if (postLoadingRoutine == null)
        {
            return;
        }

        StopCoroutine(postLoadingRoutine);
        postLoadingRoutine = null;
    }

    private void AdoptGameplayManagers()
    {
        if (gameplaySessionRoot == null)
        {
            return;
        }

        Adopt(SquadManager.Instance);
        Adopt(KnowledgeManager.Instance);
        Adopt(CombatSessionManager.Instance);
    }

    private void Adopt(Component component)
    {
        if (component == null || component.transform.parent == gameplaySessionRoot.transform)
        {
            return;
        }

        // Netcode interdit le re-parentage d'un NetworkObject hors d'une
        // synchronisation de scene. Ces objets restent persistants par leur
        // propre cycle de vie; seul un service local peut etre range sous la
        // racine de session.
        if (component.GetComponentInParent<NetworkObject>() != null)
        {
            return;
        }

        component.transform.SetParent(gameplaySessionRoot.transform, true);
    }

    private void PlaceSquadAtSpawn(string spawnId)
    {
        if (string.IsNullOrWhiteSpace(spawnId) || SquadManager.Instance == null)
        {
            return;
        }

        ZoneSpawnPoint spawn = ZoneSpawnPoint.Find(spawnId);
        if (spawn != null)
        {
            using (SceneTransitionProfiler.SquadPlacement.Auto())
            {
                SquadManager.Instance.MoveSquadToSpawn(spawn.transform);
            }
            return;
        }

        Debug.LogWarning($"[GameFlow] No ZoneSpawnPoint with id '{spawnId}' was found in the loaded scenes. Squad position was left unchanged.");
    }

    private static void RestoreLocalGameplayInputAfterSessionStart()
    {
        // Un client multijoueur reste volontairement bloque jusqu'a la fin de
        // JoinSyncSystem. En local, une nouvelle session ne doit jamais
        // conserver le focus d'un menu, d'un dialogue ou d'un combat precedent.
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
        {
            return;
        }

        InputFocusStack.Clear();
        LocalPlayerInput.SetCombatInputActive(false);
        SquadManager.Instance?.ResetInputLocksForNewSession();
    }

    private static bool CanLoad(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    private static AsyncOperation LoadScene(string sceneName, LoadSceneMode mode)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening && networkManager.IsServer && networkManager.SceneManager != null)
        {
            networkManager.SceneManager.LoadScene(sceneName, mode);
            return null;
        }

        return SceneManager.LoadSceneAsync(sceneName, mode);
    }

    private static AsyncOperation UnloadScene(Scene scene)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening && networkManager.IsServer && networkManager.SceneManager != null)
        {
            networkManager.SceneManager.UnloadScene(scene);
            return null;
        }

        return SceneManager.UnloadSceneAsync(scene);
    }
}
