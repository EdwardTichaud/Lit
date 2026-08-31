using System;
using System.Collections;
using System.Collections.Generic;
using Lit.Story;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

// Gere le follow de la squad et la generation automatique du NavMesh.
public class SquadAIManager : MonoBehaviour
{
    public static SquadAIManager Instance { get; private set; }

    public struct NavMeshBuildReport
    {
        public bool succeeded;
        public int totalColliderCount;
        public int includedColliderCount;
        public int sourceCount;
        public Bounds bounds;
        public string reason;
    }

    [Header("References")]
    [SerializeField, Tooltip("Reference au SquadManager (auto-resolve si null).")]
    private SquadManager squadManager;

    [Header("NavMesh Surface")]
    [SerializeField, Tooltip("Surface NavMesh geree par ce manager.")]
    private NavMeshSurface navMeshSurface;
    [SerializeField, Tooltip("Ajoute un NavMeshSurface si manquant.")]
    private bool autoAddNavMeshSurface = true;
    [SerializeField, Tooltip("Build du NavMesh au Start.")]
    private bool buildNavMeshOnStart = true;
    [SerializeField, Tooltip("Autorise les rebuilds NavMesh demandes par le code. Le NavMesh n'est plus rebake toutes les X secondes sans changement.")]
    private bool autoUpdateNavMesh;
    [SerializeField, Tooltip("Delai minimal entre deux rebuilds NavMesh demandes.")]
    private float navMeshUpdateInterval = 2f;
    [SerializeField, Tooltip("AgentTypeId utilise pour le bake.")]
    private int agentTypeId = 0;
    [SerializeField, Tooltip("Mode de collecte des sources NavMesh.")]
    private CollectObjects collectObjects = CollectObjects.Volume;
    [SerializeField, Tooltip("LayerMask des colliders utilises pour le NavMesh.")]
    private LayerMask navMeshLayerMask = ~0;
    [SerializeField, Tooltip("Type de geometrie a collecter.")]
    private NavMeshCollectGeometry collectGeometry = NavMeshCollectGeometry.PhysicsColliders;
    [SerializeField, Tooltip("Ignore les NavMeshAgent lors du bake.")]
    private bool ignoreNavMeshAgents = true;
    [SerializeField, Tooltip("Ignore les NavMeshObstacle lors du bake.")]
    private bool ignoreNavMeshObstacles = true;
    [SerializeField, Tooltip("Active la height mesh.")]
    private bool buildHeightMesh = false;
    [SerializeField, Tooltip("Area par defaut du NavMesh.")]
    private int defaultArea = 0;
    [SerializeField, Tooltip("Calcule automatiquement les bounds via tous les colliders. A eviter en runtime dans les grosses scenes.")]
    private bool autoCalculateBounds;
    [SerializeField, Tooltip("Taille des bounds si autoCalculateBounds = false.")]
    private Vector3 navMeshBoundsSize = new Vector3(200f, 40f, 200f);
    [SerializeField, Tooltip("Padding applique aux bounds.")]
    private Vector3 navMeshBoundsPadding = new Vector3(2f, 2f, 2f);
    [SerializeField, Tooltip("Centre custom des bounds (sinon transform).")]
    private Transform navMeshBoundsCenter;

    [Header("Formation (Fan)")]
    [SerializeField, Tooltip("Angle total du cone de formation.")]
    private float fanAngle = 120f;
    [SerializeField, Tooltip("Rayon de base de la formation.")]
    private float fanRadius = 1.5f;
    [SerializeField, Tooltip("Ecart de rayon entre rangs.")]
    private float fanRadiusStep = 0.8f;
    [SerializeField, Tooltip("Nombre d'agents par rang.")]
    private int fanRowSize = 3;
    [SerializeField, Tooltip("Passe en file indienne en couloir etroit.")]
    private bool useSingleFileInNarrowCorridors = true;
    [SerializeField, Tooltip("Distance de test pour detecter le couloir.")]
    private float corridorCheckDistance = 2f;
    [SerializeField, Tooltip("Largeur max pour considerer un couloir.")]
    private float corridorWidthThreshold = 2.2f;
    [SerializeField, Tooltip("Ecart entre membres en file indienne.")]
    private float singleFileSpacing = 1.4f;
    [SerializeField, Tooltip("Offset lateral en file indienne.")]
    private float singleFileSideOffset = 0.1f;
    [SerializeField, Tooltip("Utilise la vitesse du leader pour la formation.")]
    private bool useLeaderVelocityForFormation = true;
    [SerializeField, Tooltip("Affiche les gizmos de formation.")]
    private bool drawFormationGizmos = true;
    [SerializeField, Tooltip("Couleur gizmo leader.")]
    private Color leaderGizmoColor = new Color(1f, 0.8f, 0.1f, 0.9f);
    [SerializeField, Tooltip("Couleur gizmo follower.")]
    private Color followerGizmoColor = new Color(0.2f, 0.8f, 1f, 0.9f);
    [SerializeField, Tooltip("Rayon des spheres gizmo.")]
    private float gizmoSphereRadius = 0.2f;

    [Header("Debug")]
    [SerializeField, Tooltip("Declenche un rebuild au prochain Update.")]
    private bool rebuildNavMeshNow = false;
    [SerializeField, Tooltip("Affiche un warning quand certains meshes ne peuvent pas etre inclus dans le NavMesh.")]
    private bool warnUnreadableMeshes;
    [SerializeField, Tooltip("Journalise les bounds et le nombre de sources de chaque bake NavMesh.")]
    private bool logNavMeshBuildDiagnostics = true;

    [Header("Follow")]
    [SerializeField, Tooltip("Follow actif meme en mode selection.")]
    private bool followWhenSelectionOn = false;
    [SerializeField, Tooltip("Distance d'arret autour de la cible.")]
    private float followStopDistance = 0.5f;
    [SerializeField, Tooltip("Distance pour commencer a rattraper.")]
    private float followCatchUpDistance = 2f;
    [SerializeField, Tooltip("Laisse les compagnons sur place tant que le leader reste dans leur zone de confort.")]
    private bool holdPositionInsideComfortZone = true;
    [SerializeField, Tooltip("Distance au leader a partir de laquelle un compagnon quitte sa position et reprend le follow.")]
    private float followResumeDistance = 7f;
    [SerializeField, Tooltip("Distance au leader sous laquelle un compagnon en follow peut de nouveau rester sur place. Doit etre inferieure a Follow Resume Distance.")]
    private float followRestDistance = 4f;
    [SerializeField, Tooltip("Suspend completement le follow pendant une StorySequence pour conserver la mise en scene.")]
    private bool suspendFollowDuringStorySequences = true;
    [SerializeField, Tooltip("Input max injecte au controller.")]
    private float followMaxInput = 1f;
    [SerializeField, Tooltip("Offsets de formation en espace leader.")]
    private bool followOffsetsInLeaderSpace = true;
    [SerializeField, Tooltip("Utilise le NavMesh pour la direction.")]
    private bool useNavMeshDirection = true;
    [SerializeField, Tooltip("Ajoute un SquadFollowerAgent si manquant.")]
    private bool autoAddNavMeshFollowers = true;
    [SerializeField, Tooltip("Autorise explicitement la teleportation de rattrapage des followers. Desactive par defaut pour les enigmes de separation.")]
    private bool allowFollowerTeleportCatchUp = false;
    [SerializeField, Tooltip("Distance max au leader pour piloter activement un membre groupe. Au-dela, il reste groupe logiquement mais ne suit pas et ne se teleporte pas.")]
    private float maxActiveFollowDistance = 20f;
    [SerializeField, Tooltip("Suspend le follow au-dela de maxActiveFollowDistance. Desactive par defaut: les followers continuent a chercher un chemin NavMesh meme loin.")]
    private bool suspendFollowBeyondMaxActiveDistance = false;
    [SerializeField, Tooltip("Rayon de separation pour eviter collisions.")]
    private float separationRadius = 1.1f;
    [SerializeField, Tooltip("Force de separation appliquee.")]
    private float separationStrength = 1f;
    [Header("Catch Up")]
    [SerializeField, Tooltip("Distance a partir de laquelle on accelere.")]
    private float catchUpDistance = 6f;
    [SerializeField, Tooltip("Distance de teleportation si trop loin.")]
    private float teleportDistance = 12f;
    [SerializeField, Tooltip("Temps bloque avant teleport.")]
    private float stuckTimeBeforeTeleport = 2.5f;
    [SerializeField, Tooltip("Progress minimal pour considerer qu'on avance.")]
    private float minProgressDistance = 0.35f;
    [SerializeField, Tooltip("Rayon de recherche de point de teleport.")]
    private float teleportSampleRadius = 1.5f;

    private float nextNavMeshUpdateTime;
    private bool navMeshDirty;
    private bool explicitNavMeshRebuildRequested;
    private Coroutine sceneNavMeshRebuildRoutine;
    private readonly HashSet<Mesh> warnedUnreadableMeshes = new HashSet<Mesh>();

    /// <summary>
    /// True only after a successful bake for the currently loaded gameplay
    /// world. Scene markers use this to avoid spawning agents onto a NavMesh
    /// that still belongs to the zone being unloaded.
    /// </summary>
    public bool IsNavMeshReady { get; private set; }

    /// <summary>Raised after every explicit or automatic NavMesh bake attempt.</summary>
    public event Action<NavMeshBuildReport> NavMeshRebuildCompleted;

    private class FollowerState
    {
        public Vector3 lastPosition;
        public float lastProgressTime;
        public bool followingLeader;
        public bool suspendedByLeaderDistance;
    }

    private readonly Dictionary<GameObject, FollowerState> followerStates = new Dictionary<GameObject, FollowerState>();
    private readonly List<int> leaderGroupIndices = new List<int>();
    private GameObject currentFollowLeader;
    private bool followersStoppedForStorySequence;

    private void Awake()
    {
        Instance = this;
        if (squadManager == null)
        {
            squadManager = SquadManager.Instance;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (sceneNavMeshRebuildRoutine != null)
        {
            StopCoroutine(sceneNavMeshRebuildRoutine);
            sceneNavMeshRebuildRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (squadManager != null)
        {
            squadManager.EnsureGroups();
        }

        if (buildNavMeshOnStart)
        {
            // GameplaySessionRoot is created before the additive zone scenes.
            // Building here used to produce an empty NavMesh, then several
            // more bakes while each District sub-scene was activated.
            if (IsGameplayWorldBeingPrepared())
            {
                InvalidateNavMeshForSceneTransition();
            }
            else
            {
                RebuildNavMeshForLoadedWorld("demarrage direct");
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        // A zone is an additive group. Do not bake once per sub-scene: at this
        // point floors, colliders and enemy markers do not necessarily belong
        // to the same world yet. GameFlowService requests the single bake once
        // the complete required group is present.
        InvalidateNavMeshForSceneTransition();

        if (IsGameplayWorldBeingPrepared())
        {
            return;
        }

        if (sceneNavMeshRebuildRoutine != null)
        {
            StopCoroutine(sceneNavMeshRebuildRoutine);
        }

        sceneNavMeshRebuildRoutine = StartCoroutine(RequestNavMeshRebuildAfterSceneLoad(scene.name));
    }

    private IEnumerator RequestNavMeshRebuildAfterSceneLoad(string sceneName)
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        sceneNavMeshRebuildRoutine = null;
        RebuildNavMeshForLoadedWorld("scene chargee hors transition: " + sceneName);
    }

    private void OnValidate()
    {
        navMeshUpdateInterval = Mathf.Max(0.2f, navMeshUpdateInterval);
        fanRadius = Mathf.Max(0f, fanRadius);
        fanRadiusStep = Mathf.Max(0f, fanRadiusStep);
        fanRowSize = Mathf.Max(1, fanRowSize);
        corridorCheckDistance = Mathf.Max(0f, corridorCheckDistance);
        corridorWidthThreshold = Mathf.Max(0f, corridorWidthThreshold);
        singleFileSpacing = Mathf.Max(0.01f, singleFileSpacing);
        followStopDistance = Mathf.Max(0f, followStopDistance);
        followCatchUpDistance = Mathf.Max(0f, followCatchUpDistance);
        followRestDistance = Mathf.Max(0f, followRestDistance);
        followResumeDistance = Mathf.Max(followRestDistance, followResumeDistance);
        followMaxInput = Mathf.Clamp01(followMaxInput);
        maxActiveFollowDistance = Mathf.Max(0f, maxActiveFollowDistance);
        separationRadius = Mathf.Max(0f, separationRadius);
        separationStrength = Mathf.Max(0f, separationStrength);
        catchUpDistance = Mathf.Max(followStopDistance, catchUpDistance);
        teleportDistance = Mathf.Max(catchUpDistance, teleportDistance);
        stuckTimeBeforeTeleport = Mathf.Max(0f, stuckTimeBeforeTeleport);
        minProgressDistance = Mathf.Max(0f, minProgressDistance);
        teleportSampleRadius = Mathf.Max(0.1f, teleportSampleRadius);
    }

    private void Update()
    {
        // NavMesh is owned by the server/local simulation, independently from
        // squad follow availability or a temporarily locked player input.
        if (ShouldDriveFollowers())
        {
            ProcessNavMeshRebuildRequests();
        }

        // Boucle principale: update des followers.
        if (squadManager == null)
        {
            squadManager = SquadManager.Instance;
            if (squadManager == null)
            {
                return;
            }
        }

        if (!ShouldDriveFollowers())
        {
            return;
        }

        if (suspendFollowDuringStorySequences && StorySequenceRunner.IsAnySequencePlaying)
        {
            if (!followersStoppedForStorySequence)
            {
                StopFollowers();
                followersStoppedForStorySequence = true;
            }

            return;
        }

        followersStoppedForStorySequence = false;

        if (squadManager.IsInputLocked())
        {
            StopFollowers();
            return;
        }

        if (squadManager.charactersSelectionOn && !followWhenSelectionOn)
        {
            StopFollowers();
            return;
        }

        UpdateFollowers();
    }

    private static bool ShouldDriveFollowers()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    [ContextMenu("Rebuild NavMesh Now")]
    public void DebugRebuildNavMesh()
    {
        BuildNavMesh();
        navMeshDirty = false;
        explicitNavMeshRebuildRequested = false;
    }

    public void RequestNavMeshRebuild(string reason = null)
    {
        if (IsGameplayWorldBeingPrepared())
        {
            // Keep the request as an intention, but never execute it against
            // a half-loaded additive world.
            navMeshDirty = true;
            explicitNavMeshRebuildRequested = true;
            return;
        }

        // An explicit request comes from scene loading or an actor waiting for a
        // local polygon. It must never be discarded because periodic auto-update
        // is disabled for performance.
        navMeshDirty = true;
        explicitNavMeshRebuildRequested = true;
        rebuildNavMeshNow = true;
        if (logNavMeshBuildDiagnostics && !string.IsNullOrWhiteSpace(reason))
        {
            Debug.Log("[SquadAIManager] Rebuild NavMesh demande | " + reason + ".", this);
        }
    }

    private void ProcessNavMeshRebuildRequests()
    {
        if (IsGameplayWorldBeingPrepared())
        {
            return;
        }

        if (rebuildNavMeshNow || explicitNavMeshRebuildRequested)
        {
            rebuildNavMeshNow = false;
            explicitNavMeshRebuildRequested = false;
            BuildNavMesh();
            navMeshDirty = false;
            return;
        }

        if (autoUpdateNavMesh && navMeshDirty && Time.time >= nextNavMeshUpdateTime)
        {
            BuildNavMesh();
            navMeshDirty = false;
        }
    }

    private bool BuildNavMesh()
    {
        NavMeshSurface surface = EnsureNavMeshSurface();
        if (surface == null)
        {
            PublishNavMeshBuildReport(false, 0, 0, 0, new Bounds(transform.position, Vector3.zero),
                "NavMeshSurface introuvable");
            return false;
        }

        ApplySurfaceSettings(surface);

        int totalColliderCount = 0;
        int includedColliderCount = 0;

        if (surface.collectObjects == CollectObjects.Volume)
        {
            Bounds bounds = CalculateNavMeshBounds(out totalColliderCount, out includedColliderCount);
            Vector3 localCenter = surface.transform.InverseTransformPoint(bounds.center);
            surface.center = localCenter;
            surface.size = bounds.size;
        }

        bool built = BuildNavMeshData(surface, totalColliderCount, includedColliderCount);
        nextNavMeshUpdateTime = Time.time + Mathf.Max(0.2f, navMeshUpdateInterval);
        return built;
    }

    /// <summary>
    /// Called by GameFlowService after all mandatory scenes of the destination
    /// are active and all scenes of the previous zone are gone.
    /// </summary>
    public bool RebuildNavMeshForLoadedWorld(string reason = null)
    {
        bool built = BuildNavMesh();
        navMeshDirty = false;
        explicitNavMeshRebuildRequested = false;
        rebuildNavMeshNow = false;

        if (logNavMeshBuildDiagnostics && !string.IsNullOrWhiteSpace(reason))
        {
            Debug.Log("[SquadAIManager] Bake NavMesh de monde termine | " + reason +
                      " | success=" + built + ".", this);
        }

        return built;
    }

    /// <summary>Invalidates stale data before an additive zone transition.</summary>
    public void InvalidateNavMeshForSceneTransition()
    {
        IsNavMeshReady = false;
        NavMeshSurface surface = EnsureNavMeshSurface();
        if (surface == null)
        {
            return;
        }

        surface.RemoveData();
        surface.navMeshData = null;
    }

    private static bool IsGameplayWorldBeingPrepared()
    {
        GameFlowService flow = GameFlowService.Instance;
        return flow != null && (flow.IsTransitioning || GameFlowService.IsPreparingGameplayScene);
    }

    private Bounds CalculateNavMeshBounds(out int totalColliderCount, out int includedColliderCount)
    {
        totalColliderCount = 0;
        includedColliderCount = 0;
        Vector3 center = navMeshBoundsCenter != null ? navMeshBoundsCenter.position : transform.position;
        if (!autoCalculateBounds)
        {
            return new Bounds(center, navMeshBoundsSize);
        }

        Collider[] colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude);
        totalColliderCount = colliders.Length;
        bool hasBounds = false;
        Bounds bounds = new Bounds(center, Vector3.zero);
        int mask = navMeshLayerMask.value;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null)
            {
                continue;
            }

            if ((mask & (1 << col.gameObject.layer)) == 0)
            {
                continue;
            }

            includedColliderCount++;

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(center, navMeshBoundsSize);
        }

        bounds.Expand(navMeshBoundsPadding);
        return bounds;
    }

    private NavMeshSurface EnsureNavMeshSurface()
    {
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
        }

        if (navMeshSurface == null && autoAddNavMeshSurface)
        {
            navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
        }

        return navMeshSurface;
    }

    private void ApplySurfaceSettings(NavMeshSurface surface)
    {
        if (surface == null)
        {
            return;
        }

        surface.agentTypeID = agentTypeId;
        surface.collectObjects = collectObjects;
        surface.layerMask = navMeshLayerMask;
        surface.useGeometry = collectGeometry;
        surface.ignoreNavMeshAgent = ignoreNavMeshAgents;
        surface.ignoreNavMeshObstacle = ignoreNavMeshObstacles;
        surface.defaultArea = defaultArea;
        surface.buildHeightMesh = buildHeightMesh;
    }

    private bool BuildNavMeshData(NavMeshSurface surface, int totalColliderCount, int includedColliderCount)
    {
        if (surface == null)
        {
            PublishNavMeshBuildReport(false, totalColliderCount, includedColliderCount, 0,
                new Bounds(transform.position, Vector3.zero), "NavMeshSurface introuvable");
            return false;
        }

        List<NavMeshBuildSource> sources = CollectSources(surface);
        FilterUnreadableMeshSources(sources);

        Bounds surfaceBounds = new Bounds(surface.center, Abs(surface.size));
        if (surface.collectObjects != CollectObjects.Volume)
        {
            surfaceBounds = CalculateWorldBounds(surface, sources);
        }

        if (sources.Count == 0)
        {
            IsNavMeshReady = false;
            surface.RemoveData();
            surface.navMeshData = null;
            string reason = "aucune source NavMesh collectee";
            if (logNavMeshBuildDiagnostics)
            {
                Debug.LogWarning("[SquadAIManager] Bake NavMesh ignore : " + reason +
                                 " | colliders=" + totalColliderCount + " | retenusLayer=" + includedColliderCount +
                                 " | layerMask=" + surface.layerMask.value + " | bounds=" + surfaceBounds + ".", this);
            }
            PublishNavMeshBuildReport(false, totalColliderCount, includedColliderCount, 0, surfaceBounds, reason);
            return false;
        }

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            surface.GetBuildSettings(),
            sources,
            surfaceBounds,
            surface.transform.position,
            surface.transform.rotation);

        if (data == null)
        {
            IsNavMeshReady = false;
            Debug.LogWarning("[SquadAIManager] Bake NavMesh ignore : aucune donnee produite | sources=" +
                             sources.Count + " | bounds=" + surfaceBounds + ".", this);
            PublishNavMeshBuildReport(false, totalColliderCount, includedColliderCount, sources.Count,
                surfaceBounds, "NavMeshBuilder n'a produit aucune donnee");
            return false;
        }

        data.name = surface.gameObject.name;
        surface.RemoveData();
        surface.navMeshData = data;
        if (surface.isActiveAndEnabled)
        {
            surface.AddData();
        }

        IsNavMeshReady = true;

        if (logNavMeshBuildDiagnostics)
        {
            Debug.Log("[SquadAIManager] NavMesh reconstruit | sources=" + sources.Count +
                      " | colliders=" + totalColliderCount + " | retenusLayer=" + includedColliderCount +
                      " | bounds=" + surfaceBounds + " | scene=" + gameObject.scene.name + ".", this);
        }

        PublishNavMeshBuildReport(true, totalColliderCount, includedColliderCount, sources.Count,
            surfaceBounds, null);
        return true;
    }

    private void PublishNavMeshBuildReport(
        bool succeeded,
        int totalColliderCount,
        int includedColliderCount,
        int sourceCount,
        Bounds bounds,
        string reason)
    {
        NavMeshRebuildCompleted?.Invoke(new NavMeshBuildReport
        {
            succeeded = succeeded,
            totalColliderCount = totalColliderCount,
            includedColliderCount = includedColliderCount,
            sourceCount = sourceCount,
            bounds = bounds,
            reason = reason
        });
    }

    private List<NavMeshBuildSource> CollectSources(NavMeshSurface surface)
    {
        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();

        List<NavMeshModifier> modifiers;
        if (surface.collectObjects == CollectObjects.Children)
        {
            modifiers = new List<NavMeshModifier>(surface.GetComponentsInChildren<NavMeshModifier>());
            modifiers.RemoveAll(x => !x.isActiveAndEnabled);
        }
        else
        {
            modifiers = NavMeshModifier.activeModifiers;
        }

        for (int i = 0; i < modifiers.Count; i++)
        {
            NavMeshModifier modifier = modifiers[i];
            if (modifier == null)
            {
                continue;
            }

            if ((surface.layerMask & (1 << modifier.gameObject.layer)) == 0)
            {
                continue;
            }

            if (!modifier.AffectsAgentType(surface.agentTypeID))
            {
                continue;
            }

            NavMeshBuildMarkup markup = new NavMeshBuildMarkup
            {
                root = modifier.transform,
                overrideArea = modifier.overrideArea,
                area = modifier.area,
                ignoreFromBuild = modifier.ignoreFromBuild,
                applyToChildren = modifier.applyToChildren,
                overrideGenerateLinks = modifier.overrideGenerateLinks,
                generateLinks = modifier.generateLinks
            };
            markups.Add(markup);
        }

        switch (surface.collectObjects)
        {
            default:
            case CollectObjects.All:
                CollectSourcesInHierarchy(null, surface, markups, false, sources);
                break;
            case CollectObjects.Children:
                CollectSourcesInHierarchy(surface.transform, surface, markups, false, sources);
                break;
            case CollectObjects.Volume:
                CollectSourcesInVolume(GetInflatedWorldBounds(surface), surface, markups, false, sources);
                break;
            case CollectObjects.MarkedWithModifier:
                CollectSourcesInHierarchy(null, surface, markups, true, sources);
                break;
        }

        if (surface.ignoreNavMeshAgent)
        {
            sources.RemoveAll(x =>
                x.component != null && x.component.gameObject.GetComponent<NavMeshAgent>() != null);
        }

        if (surface.ignoreNavMeshObstacle)
        {
            sources.RemoveAll(x =>
                x.component != null && x.component.gameObject.GetComponent<NavMeshObstacle>() != null);
        }

        AppendModifierVolumes(surface, sources);

        return sources;
    }

    private void CollectSourcesInVolume(
        Bounds includedWorldBounds,
        NavMeshSurface surface,
        List<NavMeshBuildMarkup> markups,
        bool includeOnlyMarkedObjects,
        List<NavMeshBuildSource> results)
    {
        NavMeshBuilder.CollectSources(
            includedWorldBounds,
            surface.layerMask,
            surface.useGeometry,
            surface.defaultArea,
            false,
            markups,
            includeOnlyMarkedObjects,
            results);
    }

    private void CollectSourcesInHierarchy(
        Transform root,
        NavMeshSurface surface,
        List<NavMeshBuildMarkup> markups,
        bool includeOnlyMarkedObjects,
        List<NavMeshBuildSource> results)
    {
        NavMeshBuilder.CollectSources(
            root,
            surface.layerMask,
            surface.useGeometry,
            surface.defaultArea,
            false,
            markups,
            includeOnlyMarkedObjects,
            results);
    }

    private void AppendModifierVolumes(NavMeshSurface surface, List<NavMeshBuildSource> sources)
    {
        List<NavMeshModifierVolume> modifiers;
        if (surface.collectObjects == CollectObjects.Children)
        {
            modifiers = new List<NavMeshModifierVolume>(surface.GetComponentsInChildren<NavMeshModifierVolume>());
            modifiers.RemoveAll(x => !x.isActiveAndEnabled);
        }
        else
        {
            modifiers = NavMeshModifierVolume.activeModifiers;
        }

        for (int i = 0; i < modifiers.Count; i++)
        {
            NavMeshModifierVolume modifier = modifiers[i];
            if (modifier == null)
            {
                continue;
            }

            if ((surface.layerMask & (1 << modifier.gameObject.layer)) == 0)
            {
                continue;
            }

            if (!modifier.AffectsAgentType(surface.agentTypeID))
            {
                continue;
            }

            Vector3 center = modifier.transform.TransformPoint(modifier.center);
            Vector3 scale = modifier.transform.lossyScale;
            Vector3 size = new Vector3(
                modifier.size.x * Mathf.Abs(scale.x),
                modifier.size.y * Mathf.Abs(scale.y),
                modifier.size.z * Mathf.Abs(scale.z));

            NavMeshBuildSource source = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                transform = Matrix4x4.TRS(center, modifier.transform.rotation, Vector3.one),
                size = size,
                area = modifier.area
            };
            sources.Add(source);
        }
    }

    private void FilterUnreadableMeshSources(List<NavMeshBuildSource> sources)
    {
        int newWarnings = 0;
        List<string> sampleNames = null;
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            NavMeshBuildSource source = sources[i];
            if (source.shape != NavMeshBuildSourceShape.Mesh)
            {
                continue;
            }

            Mesh mesh = source.sourceObject as Mesh;
            if (mesh != null && !mesh.isReadable)
            {
                // BuildNavMeshData still reads MeshCollider source meshes.
                // Keeping a non-readable mesh works only in the editor and
                // floods the console (or fails outright in a player build).
                sources.RemoveAt(i);
                if (!warnedUnreadableMeshes.Contains(mesh))
                {
                    warnedUnreadableMeshes.Add(mesh);
                    newWarnings += 1;
                    if (sampleNames == null)
                    {
                        sampleNames = new List<string>();
                    }

                    if (sampleNames.Count < 5)
                    {
                        sampleNames.Add(mesh.name);
                    }
                }
            }
        }

        if (newWarnings > 0 && warnUnreadableMeshes)
        {
            string suffix = sampleNames != null && sampleNames.Count > 0
                ? $" Ex: {string.Join(", ", sampleNames)}"
                : string.Empty;
            Debug.LogWarning($"SquadAIManager: {newWarnings} mesh(es) non lisibles ignores pour le NavMesh. Active Read/Write sur les meshes si tu veux les inclure.{suffix}", this);
        }
    }

    private Bounds GetInflatedWorldBounds(NavMeshSurface surface)
    {
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(surface.agentTypeID);
        float agentRadius = settings.agentTypeID != -1 ? settings.agentRadius : 0f;

        Bounds localBounds = new Bounds(surface.center, Abs(surface.size));
        localBounds.Expand(new Vector3(agentRadius, 0f, agentRadius));

        Matrix4x4 localToWorld = Matrix4x4.TRS(surface.transform.position, surface.transform.rotation, Vector3.one);
        return GetWorldBounds(localToWorld, localBounds);
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private static Bounds GetWorldBounds(Matrix4x4 mat, Bounds bounds)
    {
        Vector3 absAxisX = Abs(mat.MultiplyVector(Vector3.right));
        Vector3 absAxisY = Abs(mat.MultiplyVector(Vector3.up));
        Vector3 absAxisZ = Abs(mat.MultiplyVector(Vector3.forward));
        Vector3 worldPosition = mat.MultiplyPoint(bounds.center);
        Vector3 worldSize = absAxisX * bounds.size.x + absAxisY * bounds.size.y + absAxisZ * bounds.size.z;
        return new Bounds(worldPosition, worldSize);
    }

    private Bounds CalculateWorldBounds(NavMeshSurface surface, List<NavMeshBuildSource> sources)
    {
        Matrix4x4 worldToLocal = Matrix4x4.TRS(surface.transform.position, surface.transform.rotation, Vector3.one).inverse;

        Bounds result = new Bounds();
        for (int i = 0; i < sources.Count; i++)
        {
            NavMeshBuildSource source = sources[i];
            switch (source.shape)
            {
                case NavMeshBuildSourceShape.Mesh:
                {
                    Mesh mesh = source.sourceObject as Mesh;
                    if (mesh != null)
                    {
                        result.Encapsulate(GetWorldBounds(worldToLocal * source.transform, mesh.bounds));
                    }
                    break;
                }
                case NavMeshBuildSourceShape.Terrain:
                {
                    TerrainData terrainData = source.sourceObject as TerrainData;
                    if (terrainData != null)
                    {
                        result.Encapsulate(GetWorldBounds(
                            worldToLocal * source.transform,
                            new Bounds(0.5f * terrainData.size, terrainData.size)));
                    }
                    break;
                }
                case NavMeshBuildSourceShape.Box:
                case NavMeshBuildSourceShape.Sphere:
                case NavMeshBuildSourceShape.Capsule:
                case NavMeshBuildSourceShape.ModifierBox:
                    result.Encapsulate(GetWorldBounds(worldToLocal * source.transform,
                        new Bounds(Vector3.zero, source.size)));
                    break;
            }
        }

        result.Expand(0.1f);
        return result;
    }

    private void UpdateFollowers()
    {
        if (!TryGetLeaderGroup(out GameObject leader, out int leaderIndex, out List<int> groupIndices))
        {
            return;
        }

        if (currentFollowLeader != leader)
        {
            currentFollowLeader = leader;
            ResetFollowLeashStates();
        }

        int followerCount = CountActiveFollowers(leader, leaderIndex, groupIndices);

        Vector3 leaderForward = GetLeaderForward(leader);
        Quaternion formationRotation = followOffsetsInLeaderSpace
            ? Quaternion.LookRotation(leaderForward, Vector3.up)
            : Quaternion.identity;
        bool useSingleFile = ShouldUseSingleFile(leader.transform, leaderForward);

        int order = 0;
        for (int i = 0; i < groupIndices.Count; i++)
        {
            int index = groupIndices[i];
            if (!TryGetFollowerContext(leader, leaderIndex, index, out GameObject follower, out SquadCharacterController controller))
            {
                continue;
            }

            FollowerState state = GetFollowerState(follower);
            if (!IsWithinActiveLeaderRange(leader, follower))
            {
                SuspendFollowerOutOfRange(follower, controller, state);
                continue;
            }

            bool resumedFromRangeSuspension = ResumeFollowerFromDistanceSuspension(follower, state);
            SquadFollowerAgent navFollower = useNavMeshDirection ? GetFollowerAgent(follower) : null;
            if (!ShouldFollowerMoveTowardLeader(leader, follower, state))
            {
                NetcodePlayerUtils.LogControlDecision(
                    "follower_ai",
                    follower,
                    followerAiEnabled: true,
                    waitingPointEnabled: false,
                    movementMode: "follower_hold_position",
                    reason: "leader remains inside follower comfort zone");
                controller.Stop();
                order++;
                continue;
            }

            Vector3 offset = useSingleFile
                ? GetSingleFileOffset(order)
                : GetFanOffset(order, followerCount);
            offset = formationRotation * offset;

            Vector3 targetPosition = leader.transform.position + offset;
            Vector3 toTarget = targetPosition - follower.transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (allowFollowerTeleportCatchUp &&
                !resumedFromRangeSuspension &&
                TryTeleportIfNeeded(follower, targetPosition, distance))
            {
                order++;
                continue;
            }

            if (distance <= followStopDistance)
            {
                NetcodePlayerUtils.LogControlDecision(
                    "follower_ai",
                    follower,
                    followerAiEnabled: true,
                    waitingPointEnabled: false,
                    movementMode: "follower_idle",
                    reason: "this character is follower/waiting");
                controller.Stop();
                UpdateFollowerProgress(follower);
                order++;
                continue;
            }

            Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
            if (navFollower != null)
            {
                if (navFollower.TryGetDesiredDirection(targetPosition, out Vector3 navDirection))
                {
                    direction = navDirection;
                }
            }

            direction = ApplySeparation(follower, direction, groupIndices);

            NetcodePlayerUtils.LogControlDecision(
                "follower_ai",
                follower,
                followerAiEnabled: true,
                waitingPointEnabled: false,
                movementMode: "follower_ai",
                reason: "this character is follower/waiting");
            float inputScale = followCatchUpDistance <= 0f
                ? 1f
                : Mathf.Clamp01((distance - followStopDistance) / followCatchUpDistance);
            inputScale *= Mathf.Clamp01(followMaxInput);

            if (distance > catchUpDistance)
            {
                inputScale = 1f;
            }

            controller.MoveWorld(new Vector2(direction.x, direction.z) * inputScale);
            UpdateFollowerProgress(follower);
            order++;
        }
    }

    private bool ShouldFollowerMoveTowardLeader(
        GameObject leader,
        GameObject follower,
        FollowerState state)
    {
        if (leader == null || follower == null || state == null)
        {
            return false;
        }

        if (!holdPositionInsideComfortZone)
        {
            BeginFollowing(follower, state);
            return true;
        }

        Vector3 toLeader = leader.transform.position - follower.transform.position;
        toLeader.y = 0f;
        float leaderDistance = toLeader.magnitude;

        if (state.followingLeader)
        {
            if (leaderDistance <= followRestDistance)
            {
                state.followingLeader = false;
                state.lastPosition = follower.transform.position;
                state.lastProgressTime = Time.time;
                return false;
            }

            return true;
        }

        if (leaderDistance < followResumeDistance)
        {
            return false;
        }

        BeginFollowing(follower, state);
        return true;
    }

    private static void BeginFollowing(GameObject follower, FollowerState state)
    {
        if (state.followingLeader)
        {
            return;
        }

        state.followingLeader = true;
        state.lastPosition = follower.transform.position;
        state.lastProgressTime = Time.time;
    }

    private void ResetFollowLeashStates()
    {
        foreach (KeyValuePair<GameObject, FollowerState> pair in followerStates)
        {
            GameObject follower = pair.Key;
            FollowerState state = pair.Value;
            if (state == null)
            {
                continue;
            }

            state.followingLeader = false;
            if (follower != null)
            {
                state.lastPosition = follower.transform.position;
            }

            state.lastProgressTime = Time.time;
        }
    }

    private int CountActiveFollowers(GameObject leader, int leaderIndex, List<int> groupIndices)
    {
        if (groupIndices == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < groupIndices.Count; i++)
        {
            int index = groupIndices[i];
            if (!TryGetFollowerContext(leader, leaderIndex, index, out GameObject follower, out _))
            {
                continue;
            }

            if (IsWithinActiveLeaderRange(leader, follower))
            {
                count++;
            }
        }

        return count;
    }

    private bool TryGetFollowerContext(
        GameObject leader,
        int leaderIndex,
        int index,
        out GameObject follower,
        out SquadCharacterController controller)
    {
        follower = null;
        controller = null;

        if (leader == null
            || squadManager == null
            || squadManager.squadCharacters == null
            || index == leaderIndex
            || index < 0
            || index >= squadManager.squadCharacters.Count)
        {
            return false;
        }

        GameObject candidate = squadManager.squadCharacters[index];
        if (candidate == null || candidate == leader || Zone.IsCharacterInMaison(candidate))
        {
            return false;
        }

        if (NetcodePlayerUtils.ShouldUsePlayerControl(candidate, out _))
        {
            return false;
        }

        SquadCharacterController candidateController = candidate.GetComponent<SquadCharacterController>();
        if (candidateController == null)
        {
            return false;
        }

        follower = candidate;
        controller = candidateController;
        return true;
    }

    private bool IsWithinActiveLeaderRange(GameObject leader, GameObject follower)
    {
        if (!suspendFollowBeyondMaxActiveDistance ||
            leader == null ||
            follower == null ||
            maxActiveFollowDistance <= 0f)
        {
            return true;
        }

        Vector3 delta = leader.transform.position - follower.transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= maxActiveFollowDistance * maxActiveFollowDistance;
    }

    private void SuspendFollowerOutOfRange(GameObject follower, SquadCharacterController controller, FollowerState state)
    {
        if (follower == null || state == null)
        {
            return;
        }

        if (controller != null)
        {
            controller.Stop();
        }

        state.lastPosition = follower.transform.position;
        state.lastProgressTime = Time.time;
        state.suspendedByLeaderDistance = true;
    }

    private bool ResumeFollowerFromDistanceSuspension(GameObject follower, FollowerState state)
    {
        if (follower == null || state == null || !state.suspendedByLeaderDistance)
        {
            return false;
        }

        state.suspendedByLeaderDistance = false;
        state.lastPosition = follower.transform.position;
        state.lastProgressTime = Time.time;
        return true;
    }

    private bool TryGetLeaderGroup(out GameObject leader, out int leaderIndex, out List<int> groupIndices)
    {
        leader = null;
        leaderIndex = -1;
        groupIndices = leaderGroupIndices;
        groupIndices.Clear();

        if (squadManager == null || squadManager.currentCharacter == null)
        {
            return false;
        }

        if (squadManager.squadCharacters == null || squadManager.currentSquad == null)
        {
            return false;
        }

        leader = squadManager.currentCharacter;
        leaderIndex = squadManager.squadCharacters.IndexOf(leader);
        if (leaderIndex < 0 || leaderIndex >= squadManager.currentSquad.Count)
        {
            return false;
        }

        CharacterData leaderData = squadManager.currentSquad[leaderIndex];
        int leaderGroupId = squadManager.GetGroupId(leaderData);
        if (leaderGroupId < 0)
        {
            return false;
        }

        for (int i = 0; i < squadManager.currentSquad.Count; i++)
        {
            CharacterData data = squadManager.currentSquad[i];
            if (data == null)
            {
                continue;
            }

            if (squadManager.GetGroupId(data) == leaderGroupId)
            {
                groupIndices.Add(i);
            }
        }

        return groupIndices.Count > 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawFormationGizmos)
        {
            return;
        }

        if (squadManager == null)
        {
            squadManager = SquadManager.Instance;
        }

        if (!TryGetLeaderGroup(out GameObject leader, out int leaderIndex, out List<int> groupIndices))
        {
            return;
        }

        int followerCount = CountActiveFollowers(leader, leaderIndex, groupIndices);
        if (followerCount <= 0)
        {
            return;
        }

        Transform leaderTransform = leader.transform;
        Vector3 leaderForward = GetLeaderForward(leader);
        Quaternion formationRotation = followOffsetsInLeaderSpace
            ? Quaternion.LookRotation(leaderForward, Vector3.up)
            : Quaternion.identity;
        bool useSingleFile = ShouldUseSingleFile(leaderTransform, leaderForward);

        Gizmos.color = leaderGizmoColor;
        Gizmos.DrawSphere(leaderTransform.position, gizmoSphereRadius * 1.1f);

        int order = 0;
        for (int i = 0; i < groupIndices.Count; i++)
        {
            int index = groupIndices[i];
            if (!TryGetFollowerContext(leader, leaderIndex, index, out GameObject follower, out _))
            {
                continue;
            }

            if (!IsWithinActiveLeaderRange(leader, follower))
            {
                continue;
            }

            Vector3 offset = useSingleFile
                ? GetSingleFileOffset(order)
                : GetFanOffset(order, followerCount);
            offset = formationRotation * offset;

            Vector3 targetPosition = leaderTransform.position + offset;
            Gizmos.color = followerGizmoColor;
            Gizmos.DrawWireSphere(targetPosition, gizmoSphereRadius);
            Gizmos.DrawLine(leaderTransform.position, targetPosition);
            order++;
        }
    }

    private Vector3 GetFanOffset(int order, int followerCount)
    {
        int row = fanRowSize > 0 ? order / fanRowSize : 0;
        int indexInRow = fanRowSize > 0 ? order % fanRowSize : 0;
        int remaining = followerCount - row * fanRowSize;
        int rowCount = fanRowSize > 0 ? Mathf.Min(fanRowSize, remaining) : 1;

        float radius = fanRadius + fanRadiusStep * row;
        float angle = 0f;
        if (rowCount > 1)
        {
            float t = rowCount == 1 ? 0.5f : (float)indexInRow / (rowCount - 1f);
            angle = Mathf.Lerp(-fanAngle * 0.5f, fanAngle * 0.5f, t);
        }

        Vector3 baseDir = Vector3.back;
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
        return dir * radius;
    }

    private Vector3 GetSingleFileOffset(int order)
    {
        float back = singleFileSpacing * (order + 1);
        float side = (order % 2 == 0 ? -1f : 1f) * singleFileSideOffset;
        return new Vector3(side, 0f, -back);
    }

    private bool ShouldUseSingleFile(Transform leaderTransform, Vector3 leaderForward)
    {
        if (!useSingleFileInNarrowCorridors)
        {
            return false;
        }

        if (leaderTransform == null)
        {
            return false;
        }

        if (corridorWidthThreshold <= 0f || corridorCheckDistance <= 0f)
        {
            return false;
        }

        Vector3 forward = leaderForward.sqrMagnitude > 0.0001f ? leaderForward.normalized : leaderTransform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 center = leaderTransform.position;

        float left = SampleNavMeshClearance(center, -right, corridorCheckDistance);
        float rightClear = SampleNavMeshClearance(center, right, corridorCheckDistance);
        float width = left + rightClear;

        return width > 0f && width <= corridorWidthThreshold;
    }

    private float SampleNavMeshClearance(Vector3 center, Vector3 direction, float distance)
    {
        if (distance <= 0f)
        {
            return 0f;
        }

        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        if (NavMesh.SamplePosition(center, out NavMeshHit startHit, 1f, NavMesh.AllAreas))
        {
            Vector3 start = startHit.position;
            Vector3 end = start + dir * distance;
            if (NavMesh.Raycast(start, end, out NavMeshHit hit, NavMesh.AllAreas))
            {
                return hit.distance;
            }

            return distance;
        }

        return distance;
    }

    private Vector3 GetLeaderForward(GameObject leader)
    {
        if (leader == null)
        {
            return Vector3.forward;
        }

        if (useLeaderVelocityForFormation)
        {
            LitOpsiveLocomotionBridge uccBridge = leader.GetComponent<LitOpsiveLocomotionBridge>();
            if (uccBridge != null && uccBridge.IsDriving)
            {
                Vector3 velocity = uccBridge.PlanarVelocity;
                if (velocity.sqrMagnitude > 0.01f)
                {
                    return velocity.normalized;
                }
            }
        }

        Vector3 forward = leader.transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private Vector3 ApplySeparation(GameObject self, Vector3 desiredDirection, List<int> groupIndices)
    {
        if (separationRadius <= 0f || separationStrength <= 0f)
        {
            return desiredDirection;
        }

        if (self == null || groupIndices == null || squadManager == null || squadManager.squadCharacters == null)
        {
            return desiredDirection;
        }

        Vector3 position = self.transform.position;
        Vector3 separation = Vector3.zero;

        for (int i = 0; i < groupIndices.Count; i++)
        {
            int index = groupIndices[i];
            if (index < 0 || index >= squadManager.squadCharacters.Count)
            {
                continue;
            }

            GameObject other = squadManager.squadCharacters[index];
            if (other == null || other == self)
            {
                continue;
            }

            Vector3 toSelf = position - other.transform.position;
            toSelf.y = 0f;
            float distance = toSelf.magnitude;
            if (distance <= 0.0001f || distance > separationRadius)
            {
                continue;
            }

            float weight = (separationRadius - distance) / separationRadius;
            separation += toSelf.normalized * weight;
        }

        if (separation.sqrMagnitude < 0.0001f)
        {
            return desiredDirection;
        }

        Vector3 combined = desiredDirection + separation * separationStrength;
        combined.y = 0f;
        if (combined.sqrMagnitude < 0.0001f)
        {
            return desiredDirection;
        }

        return combined.normalized;
    }

    private bool TryTeleportIfNeeded(GameObject follower, Vector3 targetPosition, float distance)
    {
        if (follower == null)
        {
            return false;
        }

        FollowerState state = GetFollowerState(follower);

        if (distance >= teleportDistance)
        {
            return TeleportFollower(follower, targetPosition, state);
        }

        if (distance >= catchUpDistance)
        {
            float idleTime = Time.time - state.lastProgressTime;
            float moved = Vector3.Distance(follower.transform.position, state.lastPosition);
            if (idleTime >= stuckTimeBeforeTeleport && moved < minProgressDistance)
            {
                return TeleportFollower(follower, targetPosition, state);
            }
        }

        return false;
    }

    private bool TeleportFollower(GameObject follower, Vector3 targetPosition, FollowerState state)
    {
        Vector3 destination = targetPosition;
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, teleportSampleRadius, NavMesh.AllAreas))
        {
            destination = hit.position;
        }

        LitUccFollowerBridge uccFollower = follower.GetComponent<LitUccFollowerBridge>();
        if (uccFollower != null && uccFollower.TryTeleport(destination))
        {
            state.lastPosition = destination;
            state.lastProgressTime = Time.time;
            return true;
        }

        LitOpsiveLocomotionBridge uccBridge = follower.GetComponent<LitOpsiveLocomotionBridge>();
        if (uccBridge != null && uccBridge.SetExternalPositionAndRotation(destination, follower.transform.rotation, stopActiveAbilities: true))
        {
            uccBridge.StopBridgeInput();
            state.lastPosition = destination;
            state.lastProgressTime = Time.time;
            return true;
        }

        return false;
    }

    private void UpdateFollowerProgress(GameObject follower)
    {
        if (follower == null)
        {
            return;
        }

        FollowerState state = GetFollowerState(follower);
        float moved = Vector3.Distance(follower.transform.position, state.lastPosition);
        if (moved >= minProgressDistance)
        {
            state.lastPosition = follower.transform.position;
            state.lastProgressTime = Time.time;
        }
    }

    private FollowerState GetFollowerState(GameObject follower)
    {
        if (!followerStates.TryGetValue(follower, out FollowerState state))
        {
            state = new FollowerState
            {
                lastPosition = follower.transform.position,
                lastProgressTime = Time.time
            };
            followerStates[follower] = state;
        }

        return state;
    }

    private void StopFollowers()
    {
        if (squadManager == null || squadManager.squadCharacters == null)
        {
            return;
        }

        GameObject leader = squadManager.currentCharacter;
        for (int i = 0; i < squadManager.squadCharacters.Count; i++)
        {
            GameObject character = squadManager.squadCharacters[i];
            if (character == null || character == leader)
            {
                continue;
            }

            if (NetcodePlayerUtils.ShouldUsePlayerControl(character, out _))
            {
                NetcodePlayerUtils.LogControlDecision(
                    "follower_stop",
                    character,
                    followerAiEnabled: false,
                    waitingPointEnabled: false,
                    movementMode: "player_owned_skip",
                    reason: "follower stop skipped because character is player-owned");
                continue;
            }

            SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
            if (controller != null)
            {
                controller.Stop();
            }
        }
    }

    private SquadFollowerAgent GetFollowerAgent(GameObject character)
    {
        if (!useNavMeshDirection || character == null)
        {
            return null;
        }

        SquadFollowerAgent agent = character.GetComponent<SquadFollowerAgent>();
        if (agent == null && autoAddNavMeshFollowers)
        {
            if (!SquadFollowerAgent.HasNavMeshNear(character.transform.position, teleportSampleRadius))
            {
                return null;
            }

            agent = character.AddComponent<SquadFollowerAgent>();
        }

        return agent;
    }
}
