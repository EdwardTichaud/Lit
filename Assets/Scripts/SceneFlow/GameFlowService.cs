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
    [Tooltip("Manifeste du hub. Tant qu'il reference Maison, le comportement actuel est conserve. Il pourra ensuite pointer vers Maison_Critical et ses sous-scenes.")]
    [SerializeField] private ZoneManifest hubManifest;
    [SerializeField] private bool loadMenuAfterBootstrap = true;
    [Header("Loading messages")]
    [Tooltip("Texte affiche pendant le retour au menu principal.")]
    public string returnToMenuLoadingMessage = "Retour au menu principal...";
    [Tooltip("Prefab instancie au demarrage d'une partie et detruit au retour au menu.")]
    [SerializeField] private GameplaySessionRoot gameplaySessionPrefab;
    [Header("Post loading")]
    [Tooltip("Nombre minimal d'images stables entre deux activations de sous-scenes decoratives.")]
    [SerializeField, Min(1)] private int postLoadingStableFrames = 15;
    [Tooltip("Une image depassant ce seuil remet le compteur de stabilite a zero.")]
    [SerializeField, Min(0.01f)] private float postLoadingMaximumStableFrameSeconds = 0.05f;

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
    private bool postLoadingPriorityApplied;
    private ThreadPriority previousBackgroundLoadingPriority;

    public bool IsTransitioning => transitionRoutine != null;
    public bool HasGameplaySession => gameplaySessionRoot != null;
    public string HubSceneName => hubManifest != null && hubManifest.IsValid
        ? hubManifest.PrimarySceneName
        : hubSceneName;
    /// <summary>
    /// Scene primaire du hub resolue par le manifeste. Les appelants externes
    /// (menu et synchronisation reseau) ne doivent plus coder "Maison".
    /// </summary>
    public static string InitialGameplaySceneName => Instance != null
        ? Instance.HubSceneName
        : DefaultHubSceneName;

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

        string sceneToTest = string.IsNullOrWhiteSpace(editorStartSceneName) ? HubSceneName : editorStartSceneName;
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

        return Instance.BeginGameplay(string.IsNullOrWhiteSpace(initialSceneName) ? Instance.HubSceneName : initialSceneName);
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
        ZoneManifest manifest = ResolveGameplayManifest(initialSceneName);
        string sceneToLoad = manifest != null ? manifest.PrimarySceneName : initialSceneName;
        if (IsTransitioning || !CanLoad(sceneToLoad))
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
        transitionRoutine = StartCoroutine(LoadInitialGameplayRoutine(sceneToLoad, initialSpawnId, manifest));
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
        if (IsTransitioning || !HasGameplaySession || !CanLoad(HubSceneName))
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

    private IEnumerator LoadInitialGameplayRoutine(string sceneName, string spawnId, ZoneManifest manifest)
    {
        SceneTransitionProfiler.Begin($"Demarrage -> {sceneName}");
        string loadingMessage = manifest != null ? manifest.LoadingMessage : "Chargement de la partie...";
        yield return LoadSingleRoutine(sceneName, loadingMessage);
        SceneTransitionProfiler.Mark("Scene activee");
        loadedZoneSceneNames.Clear();
        AddLoadedGameplayScene(sceneName);
        if (manifest != null)
        {
            yield return LoadManifestLoadingScenes(manifest, loadingMessage);
        }

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
        StartPostLoading(manifest);
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
        List<string> previousScenes = CaptureLoadedGameplayScenes();
        loadedZoneSceneNames.Clear();

        yield return LoadAdditiveRoutine(destination.PrimarySceneName, destination.LoadingMessage);
        SceneTransitionProfiler.Mark("Scene de zone activee");
        AddLoadedGameplayScene(destination.PrimarySceneName);
        yield return LoadManifestLoadingScenes(destination, destination.LoadingMessage);

        Scene targetScene = SceneManager.GetSceneByName(destination.PrimarySceneName);
        if (targetScene.IsValid() && targetScene.isLoaded)
        {
            SceneManager.SetActiveScene(targetScene);
        }

        for (int i = previousScenes.Count - 1; i >= 0; i--)
        {
            yield return UnloadSceneIfLoaded(previousScenes[i]);
            SceneTransitionProfiler.Mark($"Sous-scene precedente dechargee ({previousScenes[i]})");
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
        string hubScene = HubSceneName;
        SceneTransitionProfiler.Begin($"{activeGameplaySceneName} -> {hubScene}");
        yield return LoadingScreenService.ShowAndWaitForPresentation("Retour a la Maison...");
        SceneTransitionProfiler.ResetFrameGapMeasurement();
        SceneTransitionProfiler.Mark("Overlay opaque");
        List<string> previousScenes = CaptureLoadedGameplayScenes();
        loadedZoneSceneNames.Clear();

        yield return LoadAdditiveRoutine(hubScene, "Retour a la Maison...");
        SceneTransitionProfiler.Mark("Maison activee");
        AddLoadedGameplayScene(hubScene);
        if (hubManifest != null && hubManifest.IsValid)
        {
            yield return LoadManifestLoadingScenes(hubManifest, "Retour a la Maison...");
        }

        Scene loadedHubScene = SceneManager.GetSceneByName(hubScene);
        if (loadedHubScene.IsValid() && loadedHubScene.isLoaded)
        {
            SceneManager.SetActiveScene(loadedHubScene);
        }

        for (int i = previousScenes.Count - 1; i >= 0; i--)
        {
            yield return UnloadSceneIfLoaded(previousScenes[i]);
            SceneTransitionProfiler.Mark($"Sous-scene dechargee ({previousScenes[i]})");
        }

        activeGameplaySceneName = hubScene;
        PlaceSquadAtSpawn(spawnId);
        SceneTransitionProfiler.Mark("Escouade placee");
        LoadingScreenService.HideWhenSceneIsReady();
        SceneTransitionProfiler.End("Ecran pret a disparaitre");
        transitionRoutine = null;
        StartPostLoading(hubManifest);
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
        // Le Volume du decor du menu est enregistre lors de l'activation de
        // MainMenu. On le laisse affecter une image complete derriere
        // l'overlay opaque afin d'eviter un changement brutal d'exposition
        // pendant le fondu de sortie.
        LoadingScreenService.PrepareSceneReveal();
        yield return null;
        yield return new WaitForEndOfFrame();
        SceneTransitionProfiler.Mark("Menu rendu sous overlay");
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
        if (destination == null || destination.PostLoadingSceneNames.Count == 0)
        {
            return;
        }

        int generation = ++postLoadingGeneration;
        BeginPostLoadingPriority();
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

            yield return WaitForStableGameplayFrames(generation);
            if (generation != postLoadingGeneration)
            {
                yield break;
            }

            SceneTransitionProfiler.Mark($"Post chargement demande ({sceneName})");
            yield return LoadAdditiveAfterGameplayRoutine(sceneName);
            if (generation != postLoadingGeneration)
            {
                yield break;
            }

            AddLoadedGameplayScene(sceneName);
            SceneTransitionProfiler.Mark($"Post chargement active ({sceneName})");

            yield return WaitForStableGameplayFrames(generation);
        }

        if (generation == postLoadingGeneration)
        {
            postLoadingRoutine = null;
            EndPostLoadingPriority();
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

        // Le chargement reste sequentiel afin de ne jamais empiler deux
        // activations pendant que le joueur explore la zone.
        yield return null;
        SceneTransitionProfiler.Mark($"Post activation demandee ({sceneName})");
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
        if (postLoadingRoutine != null)
        {
            StopCoroutine(postLoadingRoutine);
            postLoadingRoutine = null;
        }

        EndPostLoadingPriority();
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

    private ZoneManifest ResolveGameplayManifest(string sceneName)
    {
        if (hubManifest == null || !hubManifest.IsValid)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(sceneName) ||
               string.Equals(sceneName, hubSceneName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sceneName, hubManifest.PrimarySceneName, StringComparison.OrdinalIgnoreCase)
            ? hubManifest
            : null;
    }

    private IEnumerator LoadManifestLoadingScenes(ZoneManifest manifest, string loadingMessage)
    {
        if (manifest == null)
        {
            yield break;
        }

        for (int i = 0; i < manifest.LoadingSceneNames.Count; i++)
        {
            string additionalScene = manifest.LoadingSceneNames[i];
            if (string.IsNullOrWhiteSpace(additionalScene) || !CanLoad(additionalScene))
            {
                continue;
            }

            yield return LoadAdditiveRoutine(additionalScene, loadingMessage);
            AddLoadedGameplayScene(additionalScene);
            SceneTransitionProfiler.Mark($"Sous-scene activee ({additionalScene})");
        }
    }

    private List<string> CaptureLoadedGameplayScenes()
    {
        List<string> result = new List<string>(loadedZoneSceneNames);
        if (!string.IsNullOrWhiteSpace(activeGameplaySceneName) &&
            !result.Contains(activeGameplaySceneName))
        {
            result.Add(activeGameplaySceneName);
        }

        return result;
    }

    private void AddLoadedGameplayScene(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName) && !loadedZoneSceneNames.Contains(sceneName))
        {
            loadedZoneSceneNames.Add(sceneName);
        }
    }

    private IEnumerator WaitForStableGameplayFrames(int generation)
    {
        int stableFrames = 0;
        while (stableFrames < postLoadingStableFrames)
        {
            if (generation != postLoadingGeneration)
            {
                yield break;
            }

            if (Time.unscaledDeltaTime <= postLoadingMaximumStableFrameSeconds)
            {
                stableFrames++;
            }
            else
            {
                stableFrames = 0;
            }

            yield return null;
        }
    }

    private void BeginPostLoadingPriority()
    {
        if (postLoadingPriorityApplied)
        {
            return;
        }

        previousBackgroundLoadingPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        postLoadingPriorityApplied = true;
    }

    private void EndPostLoadingPriority()
    {
        if (!postLoadingPriorityApplied)
        {
            return;
        }

        Application.backgroundLoadingPriority = previousBackgroundLoadingPriority;
        postLoadingPriorityApplied = false;
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
