using System;
using System.Collections;
using System.Collections.Generic;
using Lit.Performance;
using Opsive.UltimateCharacterController;
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
    [Tooltip("Manifeste du hub : Maison_Core et ses sous-scenes semantiques.")]
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
    [Header("Loading scenes")]
    [Tooltip("Nombre minimal d'images laissees au rendu entre deux sous-scenes obligatoires du manifeste.")]
    [SerializeField, Min(1)] private int loadingSceneStableFrames = 12;
    [Tooltip("Une image longue relance la periode de stabilisation avant la sous-scene suivante.")]
    [SerializeField, Min(0.01f)] private float loadingSceneMaximumStableFrameSeconds = 0.05f;

#if UNITY_EDITOR
    [Header("Editor test startup")]
    [Tooltip("Ignore le menu lors d'un Play lance depuis Bootstrap et ouvre directement la scene de test avec une session de gameplay complete.")]
    [SerializeField] private bool editorStartGameplayDirectly;
    [Tooltip("Zone a charger lors d'un test direct. Le manifeste garantit que la scene Core et les scenes obligatoires sont chargees dans le meme ordre qu'en jeu.")]
    [SerializeField] private ZoneManifest editorStartManifest;
    [SerializeField, HideInInspector, Tooltip("Compatibilite avec les anciennes configurations de test. Utiliser Editor Start Manifest a la place.")]
    private string editorStartSceneName = DefaultHubSceneName;
    [Tooltip("Identifiant du ZoneSpawnPoint dans la scene de test. Laisser vide pour conserver le spawn normal de la scene.")]
    [SerializeField] private string editorStartSpawnId;
#endif

    private readonly List<string> loadedZoneSceneNames = new List<string>();
    private GameplaySessionRoot gameplaySessionRoot;
    private Coroutine transitionRoutine;
    private Coroutine postLoadingRoutine;
    private ProximitySceneStreamingController proximityStreaming;
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

        ZoneManifest manifestToTest = editorStartManifest != null && editorStartManifest.IsValid
            ? editorStartManifest
            : ResolveGameplayManifest(editorStartSceneName);
        string sceneToTest = manifestToTest != null
            ? manifestToTest.PrimarySceneName
            : (string.IsNullOrWhiteSpace(editorStartSceneName) ? HubSceneName : editorStartSceneName);

        if (BeginGameplay(sceneToTest, editorStartSpawnId, manifestToTest, usePrimarySceneSpawnFallback: true))
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

    public static bool TravelToZone(ZoneManifest destination, IReadOnlyList<Pose> destinationPoints)
    {
        return Instance != null && Instance.BeginZoneTravel(destination, destinationPoints);
    }

    public static bool ReturnToHub(string spawnId = null)
    {
        return Instance != null && Instance.BeginReturnToHub(spawnId);
    }

    public static bool OpenMainMenu()
    {
        return Instance != null && Instance.BeginReturnToMenu();
    }

    private bool BeginGameplay(string initialSceneName, string initialSpawnId = null, ZoneManifest forcedManifest = null, bool usePrimarySceneSpawnFallback = false)
    {
        ZoneManifest manifest = forcedManifest != null && forcedManifest.IsValid
            ? forcedManifest
            : ResolveGameplayManifest(initialSceneName);
        string sceneToLoad = manifest != null ? manifest.PrimarySceneName : initialSceneName;
        if (IsTransitioning || !CanLoad(sceneToLoad))
        {
            return false;
        }

        // Le menu a deja prepare le runtime avant StartHost. Le refaire apres
        // l'initialisation NGO effacerait les assignations du spawner pendant
        // que le NetworkSceneManager commence a charger Maison.
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            GameplayRuntimeReset.PrepareForGameplayStart("game_flow_start_gameplay");
        }
        IsPreparingGameplayScene = true;
        if (!EnsureGameplaySession())
        {
            IsPreparingGameplayScene = false;
            return false;
        }
        transitionRoutine = StartCoroutine(LoadInitialGameplayRoutine(sceneToLoad, initialSpawnId, manifest, usePrimarySceneSpawnFallback));
        return true;
    }

    private bool BeginZoneTravel(ZoneManifest destination, IReadOnlyList<Pose> destinationPoints)
    {
        if (IsTransitioning || !HasGameplaySession || destination == null || !destination.IsValid || !CanLoad(destination.PrimarySceneName))
        {
            return false;
        }

        StopPostLoadingRoutine();
        transitionRoutine = StartCoroutine(TravelToZoneRoutine(destination, destinationPoints));
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

    private IEnumerator LoadInitialGameplayRoutine(string sceneName, string spawnId, ZoneManifest manifest, bool usePrimarySceneSpawnFallback)
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
        PreserveUccSimulationManager();
        AdoptGameplayManagers();
        SceneTransitionProfiler.Mark("Managers prets");
        yield return PlaceSquadAtSpawnRoutine(spawnId, sceneName, usePrimarySceneSpawnFallback);
        RestoreLocalGameplayInputAfterSessionStart();
        StartProximityStreaming(sceneName);
#if UNITY_EDITOR
        StartCoroutine(LogZoneControlProbe(sceneName));
#endif
        SceneTransitionProfiler.Mark("Escouade placee");
        LoadingScreenService.HideWhenSceneIsReady();
        SceneTransitionProfiler.End("Ecran pret a disparaitre");
        transitionRoutine = null;
        StartPostLoading(manifest);
    }

    private IEnumerator TravelToZoneRoutine(ZoneManifest destination, IReadOnlyList<Pose> destinationPoints)
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
        yield return StopProximityStreaming();
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

        // UCC cree son SimulationManager a la premiere apparition du joueur.
        // Sans cette promotion, il est cree dans Maison puis detruit avec elle
        // et UCC ne fait plus qu'actualiser sa vitesse interne sans appliquer
        // la position au Rigidbody/Transform dans la zone suivante.
        PreserveUccSimulationManager();
        bool retainHubForLateJoin = ShouldKeepHubLoadedForLateJoin();
        for (int i = previousScenes.Count - 1; i >= 0; i--)
        {
            if (retainHubForLateJoin && IsHubManifestScene(previousScenes[i]))
            {
                continue;
            }

            yield return UnloadSceneIfLoaded(previousScenes[i]);
            SceneTransitionProfiler.Mark($"Sous-scene precedente dechargee ({previousScenes[i]})");
        }

        activeGameplaySceneName = destination.PrimarySceneName;
        // Une transition de zone ferme le contexte de jeu precedent. Sans
        // cette remise a zero, un focus UI laisse par Maison bloque le moteur
        // du personnage et la camera, alors que l'Animator peut encore voir
        // l'input brut et jouer une animation de marche sur place.
        yield return PlaceSquadAtPortalDestinationRoutine(destinationPoints);
        RestoreLocalGameplayInputAfterSessionStart();
        StartProximityStreaming(destination.PrimarySceneName);
#if UNITY_EDITOR
        StartCoroutine(LogZoneControlProbe(destination.PrimarySceneName));
#endif
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
        yield return StopProximityStreaming();
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

        PreserveUccSimulationManager();
        for (int i = previousScenes.Count - 1; i >= 0; i--)
        {
            yield return UnloadSceneIfLoaded(previousScenes[i]);
            SceneTransitionProfiler.Mark($"Sous-scene dechargee ({previousScenes[i]})");
        }

        activeGameplaySceneName = hubScene;
        yield return PlaceSquadAtSpawnRoutine(spawnId);
        RestoreLocalGameplayInputAfterSessionStart();
        StartProximityStreaming(hubScene);
#if UNITY_EDITOR
        StartCoroutine(LogZoneControlProbe(hubScene));
#endif
        SceneTransitionProfiler.Mark("Escouade placee");
        LoadingScreenService.HideWhenSceneIsReady();
        SceneTransitionProfiler.End("Ecran pret a disparaitre");
        transitionRoutine = null;
        StartPostLoading(hubManifest);
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        SceneTransitionProfiler.Begin($"{activeGameplaySceneName} -> {menuSceneName}");
        yield return LoadingScreenService.ShowAndWaitForPresentation(returnToMenuLoadingMessage);
        yield return StopProximityStreaming();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetcodeBootstrap.ShutdownActiveNetworkManager();
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

    private void StartProximityStreaming(string primarySceneName)
    {
        if (string.IsNullOrWhiteSpace(primarySceneName))
        {
            return;
        }

        if (proximityStreaming == null)
        {
            proximityStreaming = GetComponent<ProximitySceneStreamingController>();
            if (proximityStreaming == null)
            {
                proximityStreaming = gameObject.AddComponent<ProximitySceneStreamingController>();
            }
        }

        proximityStreaming.BeginForPrimaryScene(primarySceneName);
    }

    private IEnumerator StopProximityStreaming()
    {
        if (proximityStreaming != null)
        {
            yield return proximityStreaming.StopAndUnload();
        }
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

    private IEnumerator PlaceSquadAtSpawnRoutine(string spawnId, string primarySceneName = null, bool usePrimarySceneSpawnFallback = false)
    {
        if (SquadManager.Instance == null)
        {
            yield break;
        }

        bool hasRequestedSpawn = !string.IsNullOrWhiteSpace(spawnId);
        IReadOnlyList<ZoneSpawnPoint> partySpawns = hasRequestedSpawn
            ? ZoneSpawnPoint.FindAll(spawnId)
            : null;
        ZoneSpawnPoint spawn = partySpawns != null && partySpawns.Count > 0 ? partySpawns[0] : null;
        if (spawn == null && usePrimarySceneSpawnFallback)
        {
            spawn = ZoneSpawnPoint.FindFirstInScene(primarySceneName);
        }

        if (spawn != null)
        {
            using (SceneTransitionProfiler.SquadPlacement.Auto())
            {
                if (partySpawns != null && partySpawns.Count > 1)
                {
                    SquadManager.Instance.MoveSquadToSpawns(partySpawns);
                }
                else
                {
                    SquadManager.Instance.MoveSquadToSpawn(spawn.transform);
                }
            }

            // UCC traite le snap de l'Animator et le sol dans son cycle de
            // physique. L'input/camera ne sont rendus qu'apres ce cycle.
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            LocalInputRouter.RaiseCameraRecenter();
            yield break;
        }

        if (hasRequestedSpawn)
        {
            Debug.LogWarning($"[GameFlow] No ZoneSpawnPoint with id '{spawnId}' was found in the loaded scenes. Squad position was left unchanged.");
        }
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
        GamepadInputContextStack.Clear();
        LocalInputRouter.ClearInteractionAndJumpSuppressions();
        LocalPlayerInput.SetCombatInputActive(false);
        SquadManager.Instance?.ResetInputLocksForNewSession();
        LocalInputRouter.RaiseCameraRecenter();
    }

    /// <summary>
    /// SimulationManager est l'infrastructure UCC qui applique les positions
    /// calculees par les locomotions et les cameras. UCC le cree a la volee;
    /// il ne doit donc jamais appartenir a une scene de hub ou de zone
    /// dechargeable.
    /// </summary>
    private static void PreserveUccSimulationManager()
    {
        SimulationManager simulationManager = FindAnyObjectByType<SimulationManager>(FindObjectsInactive.Include);
        if (simulationManager == null)
        {
            return;
        }

        Scene managerScene = simulationManager.gameObject.scene;
        if (managerScene.name == "DontDestroyOnLoad")
        {
            return;
        }

        DontDestroyOnLoad(simulationManager.gameObject);
        Debug.Log("[GameFlow] UCC SimulationManager moved to DontDestroyOnLoad before scene unloading.", simulationManager);
    }

#if UNITY_EDITOR
    private static IEnumerator LogZoneControlProbe(string sceneName)
    {
        // Une seule trace, apres que la physique et le binder de camera aient
        // eu le temps de traiter le teleport. Elle sert a diagnostiquer une
        // zone sans deviner entre le sol, UCC, l'input et la camera.
        yield return new WaitForSecondsRealtime(1f);

        GameObject character = SquadManager.Instance != null ? SquadManager.Instance.currentCharacter : null;
        SquadCharacterController controller = character != null
            ? character.GetComponent<SquadCharacterController>()
            : null;
        Rigidbody body = character != null ? character.GetComponent<Rigidbody>() : null;
        CapsuleCollider capsule = character != null ? character.GetComponent<CapsuleCollider>() : null;

        string ground = "none";
        if (character != null && Physics.Raycast(character.transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            ground = $"{hit.collider.name} at y={hit.point.y:F2}, distance={hit.distance:F2}";
        }

        Camera mainCamera = Camera.main;
        LitOpsiveLocomotionBridge bridge = character != null
            ? character.GetComponent<LitOpsiveLocomotionBridge>()
            : null;
        Debug.Log(
            $"[ZoneControlProbe] scene='{sceneName}' character='{(character != null ? character.name : "none")}' " +
            $"position={(character != null ? character.transform.position.ToString("F3") : "n/a")} " +
            $"grounded={(controller != null && controller.IsGrounded)} " +
            $"movementSuppressed={(controller != null && controller.IsMovementInputSuppressed)} " +
            $"externalDriver={(controller != null && controller.IsExternalLocomotionDriverActive)} " +
            $"uccInputSuppressed={(bridge != null && bridge.IsInputSuppressedByUcc)} " +
            $"uccExternalLock={(bridge != null && bridge.IsExternalLockActive)} " +
            $"uccTraversalLock={(bridge != null && bridge.IsScriptedTraversalActive)} " +
            $"bodyKinematic={(body != null && body.isKinematic)} velocity={(body != null ? body.linearVelocity.ToString("F3") : "n/a")} " +
            $"capsule={(capsule != null && capsule.enabled)} ground='{ground}' " +
            $"inputFocus={InputFocusStack.HasAnyFocus()} cameraFocusBlocked={InputFocusStack.HasAnyFocusBlockingCamera()} " +
            $"mainCamera='{(mainCamera != null ? mainCamera.name : "none")}' cameraEnabled={(mainCamera != null && mainCamera.isActiveAndEnabled)}");
    }
#endif

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

            // Cette attente est volontaire : LoadingScenes est une file et non
            // un lot. Unity finit completement une activation et le rendu a le
            // temps de respirer avant que la scene obligatoire suivante ne soit
            // demandee.
            yield return WaitForStableLoadingFrames();
            SceneTransitionProfiler.Mark($"Phase loading {i + 1}/{manifest.LoadingSceneNames.Count} demandee ({additionalScene})");
            yield return LoadAdditiveRoutine(additionalScene, loadingMessage);
            AddLoadedGameplayScene(additionalScene);
            SceneTransitionProfiler.Mark($"Phase loading {i + 1}/{manifest.LoadingSceneNames.Count} activee ({additionalScene})");
            yield return WaitForStableLoadingFrames();
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

    private IEnumerator PlaceSquadAtPortalDestinationRoutine(IReadOnlyList<Pose> destinationPoints)
    {
        if (SquadManager.Instance == null || destinationPoints == null || destinationPoints.Count == 0)
        {
            Debug.LogWarning("[GameFlow] Le portail de changement de zone n'a aucun Destination Point. L'escouade conserve sa position.");
            yield break;
        }

        using (SceneTransitionProfiler.SquadPlacement.Auto())
        {
            SquadManager.Instance.MoveSquadToDestinationPoints(destinationPoints);
        }

        yield return new WaitForFixedUpdate();
        Physics.SyncTransforms();
        LocalInputRouter.RaiseCameraRecenter();
    }

    // La Maison reste chargee pendant une session reseau hors hub : un nouveau
    // joueur peut donc toujours y apparaitre et rejoindre le groupe par portail.
    private static bool ShouldKeepHubLoadedForLateJoin()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager != null && manager.IsListening;
    }

    private bool IsHubManifestScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (string.Equals(sceneName, HubSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return hubManifest != null && hubManifest.IsValid &&
               (ContainsSceneName(hubManifest.LoadingSceneNames, sceneName) ||
                ContainsSceneName(hubManifest.PostLoadingSceneNames, sceneName));
    }

    private static bool ContainsSceneName(IReadOnlyList<string> sceneNames, string sceneName)
    {
        if (sceneNames == null)
        {
            return false;
        }

        for (int i = 0; i < sceneNames.Count; i++)
        {
            if (string.Equals(sceneNames[i], sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private IEnumerator WaitForStableLoadingFrames()
    {
        int stableFrames = 0;
        while (stableFrames < loadingSceneStableFrames)
        {
            if (Time.unscaledDeltaTime <= loadingSceneMaximumStableFrameSeconds)
            {
                stableFrames++;
            }
            else
            {
                stableFrames = 0;
            }

            SceneTransitionProfiler.Pulse();
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
