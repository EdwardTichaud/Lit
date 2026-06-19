using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class CastleRoamingMonster : MonoBehaviour
{
    private enum MonsterMode
    {
        Patrol,
        Chase
    }

    private enum PatrolAreaMode
    {
        WholeNavMesh,
        RadiusAroundCenter
    }

    [Header("NavMesh")]
    [SerializeField, Tooltip("Agent NavMesh qui deplace le monstre.")]
    private NavMeshAgent agent;
    [SerializeField, Tooltip("Mask des zones NavMesh autorisees.")]
    private int navMeshAreaMask = NavMesh.AllAreas;
    [SerializeField, Min(0.05f), Tooltip("Distance de recherche pour recaler le monstre sur le NavMesh.")]
    private float navMeshSampleDistance = 2f;
    [SerializeField, Min(0.05f), Tooltip("Delai entre deux essais quand le NavMesh n'est pas encore disponible.")]
    private float navMeshRetryInterval = 0.5f;
    [SerializeField, Tooltip("Desactive l'agent tant que le monstre n'a pas ete recale sur le NavMesh.")]
    private bool disableAgentUntilNavMeshReady = true;
    [SerializeField, Min(0.01f), Tooltip("Rayon physique de l'agent.")]
    private float agentRadius = 0.65f;
    [SerializeField, Min(0.1f), Tooltip("Hauteur physique de l'agent.")]
    private float agentHeight = 2f;
    [SerializeField, Min(0.01f), Tooltip("Vitesse en patrouille.")]
    private float patrolSpeed = 2.4f;
    [SerializeField, Min(0.01f), Tooltip("Vitesse en poursuite.")]
    private float chaseSpeed = 4.2f;
    [SerializeField, Min(0.01f), Tooltip("Acceleration de l'agent.")]
    private float agentAcceleration = 12f;
    [SerializeField, Min(1f), Tooltip("Vitesse de rotation de l'agent.")]
    private float agentAngularSpeed = 360f;

    [Header("Patrol")]
    [SerializeField, Tooltip("Source des destinations de patrouille aleatoires.")]
    private PatrolAreaMode patrolAreaMode = PatrolAreaMode.WholeNavMesh;
    [SerializeField, Tooltip("Centre de patrouille. Si vide, la position de depart est utilisee.")]
    private Transform patrolCenter;
    [SerializeField, Min(0.5f), Tooltip("Rayon autour du centre si Patrol Area Mode = Radius Around Center.")]
    private float patrolRadius = 35f;
    [SerializeField, Min(0f), Tooltip("Distance minimale souhaitee entre la position actuelle et la prochaine destination.")]
    private float minPatrolLegDistance = 10f;
    [SerializeField, Min(0.05f), Tooltip("Distance de recherche pour recaler une destination de patrouille sur le NavMesh.")]
    private float patrolDestinationSampleDistance = 4f;
    [SerializeField, Min(0.05f), Tooltip("Distance a partir de laquelle une destination est consideree atteinte.")]
    private float patrolArrivalDistance = 1.2f;
    [SerializeField, Min(0f), Tooltip("Pause avant de choisir un nouveau point.")]
    private float patrolPauseDuration = 0.6f;
    [SerializeField, Min(0.02f), Tooltip("Intervalle minimal entre deux SetDestination en patrouille.")]
    private float patrolRepathInterval = 0.35f;
    [SerializeField, Min(1), Tooltip("Nombre d'essais pour trouver un point de patrouille valide.")]
    private int patrolPointAttempts = 32;

    [Header("Centre des couloirs")]
    [SerializeField, Tooltip("Force les points de patrouille a garder une marge avec les bords du NavMesh.")]
    private bool keepPatrolCenteredInCorridors = true;
    [SerializeField, Min(0f), Tooltip("Distance minimale entre les points de patrouille et le bord du NavMesh.")]
    private float wallClearance = 0.9f;
    [SerializeField, Min(0f), Tooltip("Marge ajoutee quand un point doit etre repousse depuis un bord.")]
    private float wallCorrectionPadding = 0.35f;
    [SerializeField, Min(0.05f), Tooltip("Cooldown entre deux corrections si l'agent approche trop d'un bord.")]
    private float wallCorrectionInterval = 0.4f;
    [SerializeField, Tooltip("Autorise une destination non centree si aucun point centre n'a ete trouve.")]
    private bool allowUncenteredPatrolFallback;

    [Header("Detection")]
    [SerializeField, Tooltip("Point des yeux. Si vide, on utilise transform + eyeHeight.")]
    private Transform eye;
    [SerializeField, Min(0f), Tooltip("Hauteur des yeux si aucun transform eye n'est assigne.")]
    private float eyeHeight = 1.65f;
    [SerializeField, Min(0.1f), Tooltip("Distance de vision.")]
    private float viewDistance = 18f;
    [SerializeField, Range(1f, 180f), Tooltip("Angle de vision frontal.")]
    private float viewAngle = 85f;
    [SerializeField, Min(0f), Tooltip("Hauteur visee sur les personnages.")]
    private float targetAimHeight = 1.1f;
    [SerializeField, Tooltip("Layers qui bloquent la ligne de vue.")]
    private LayerMask lineOfSightBlockers = ~0;
    [SerializeField, Min(0f), Tooltip("Temps pendant lequel le monstre continue vers la derniere position vue.")]
    private float chaseMemoryDuration = 3f;
    [SerializeField, Min(0.02f), Tooltip("Intervalle minimal entre deux SetDestination en poursuite.")]
    private float chaseRepathInterval = 0.12f;

    [Header("Lumiere")]
    [SerializeField, Min(0.1f), Tooltip("A cette distance, le personnage est immobilise et force dans le noir.")]
    private float lightDangerRadius = 20f;
    [SerializeField, Min(0.1f), Tooltip("A cette distance, une flamme allumee attire le monstre.")]
    private float lightAttractionRadius = 20f;
    [SerializeField, Tooltip("Immobilise les personnages trop proches du monstre.")]
    private bool immobilizeCharactersInDangerRadius = true;
    [SerializeField, Tooltip("Eteint automatiquement la flamme des personnages trop proches.")]
    private bool forceFlameOffInDangerRadius = true;
    [SerializeField, Tooltip("Restaure la flamme quand le personnage sort du rayon de danger.")]
    private bool restoreFlameWhenSafe;
    [SerializeField, Tooltip("La lumiere attire seulement si la ligne de vue n'est pas bloquee.")]
    private bool lightAttractionRequiresLineOfSight;

    [Header("Capture")]
    [SerializeField, Min(0.05f), Tooltip("Distance de contact qui envoie le personnage dans le neant.")]
    private float catchDistance = 1.1f;
    [SerializeField, Tooltip("Desactive le GameObject du personnage capture.")]
    private bool disableCaughtCharacter = true;
    [SerializeField, Tooltip("Si assigne, le personnage capture est place ici avant de disparaitre.")]
    private Transform voidSink;
    [SerializeField, Tooltip("Offset applique a la position de neant.")]
    private Vector3 voidOffset = new Vector3(0f, -250f, 0f);
    [SerializeField, Tooltip("Despawn le NetworkObject capture quand le serveur pilote la partie.")]
    private bool despawnNetworkCaughtCharacters;

    [Header("Debug")]
    [SerializeField, Tooltip("Affiche les gizmos de patrouille, vision et danger.")]
    private bool drawGizmos = true;
    [SerializeField, Tooltip("Affiche des warnings quand aucun point de patrouille centre n'est trouve.")]
    private bool warnWhenPatrolPointMissing;

    private NavMeshPath validationPath;
    private readonly RaycastHit[] lineOfSightHits = new RaycastHit[16];
    private readonly Dictionary<SquadCharacterController, ThreatenedCharacterState> threatenedCharacters = new Dictionary<SquadCharacterController, ThreatenedCharacterState>();
    private readonly HashSet<SquadCharacterController> threatenedThisFrame = new HashSet<SquadCharacterController>();
    private readonly List<SquadCharacterController> threatReleaseBuffer = new List<SquadCharacterController>();
    private readonly HashSet<SquadCharacterController> caughtCharacters = new HashSet<SquadCharacterController>();

    private MonsterMode mode = MonsterMode.Patrol;
    private Vector3 fallbackPatrolCenter;
    private Vector3 currentPatrolDestination;
    private Vector3 lastDestination;
    private Vector3 lastKnownTargetPosition;
    private SquadCharacterController chaseTarget;
    private float nextPatrolDecisionTime;
    private float nextDestinationUpdateTime;
    private float nextWallCorrectionTime;
    private float nextNavMeshRetryTime;
    private float lastSeenTargetTime = float.NegativeInfinity;

    private class ThreatenedCharacterState
    {
        public bool movementSuppressed;
        public bool hasFlameSnapshot;
        public bool flameWasEquipped;
        public int flameSeconds;
    }

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null && disableAgentUntilNavMeshReady && !Application.isPlaying)
        {
            agent.enabled = false;
        }
    }

    private void Awake()
    {
        EnsureRuntimeCollections();
        ResolveAgentReference();

        fallbackPatrolCenter = patrolCenter != null ? patrolCenter.position : transform.position;
        ConfigureAgent();
    }

    private void OnEnable()
    {
        EnsureRuntimeCollections();
        ResolveAgentReference();

        if (!Application.isPlaying)
        {
            if (agent != null && disableAgentUntilNavMeshReady)
            {
                agent.enabled = false;
            }

            return;
        }

        ConfigureAgent();
        nextNavMeshRetryTime = 0f;
        if (agent != null && disableAgentUntilNavMeshReady)
        {
            agent.enabled = false;
        }
    }

    private void OnDisable()
    {
        ReleaseAllThreatStates();
    }

    private void OnValidate()
    {
        navMeshSampleDistance = Mathf.Max(0.05f, navMeshSampleDistance);
        navMeshRetryInterval = Mathf.Max(0.05f, navMeshRetryInterval);
        agentRadius = Mathf.Max(0.01f, agentRadius);
        agentHeight = Mathf.Max(agentRadius * 2f, agentHeight);
        patrolSpeed = Mathf.Max(0.01f, patrolSpeed);
        chaseSpeed = Mathf.Max(0.01f, chaseSpeed);
        agentAcceleration = Mathf.Max(0.01f, agentAcceleration);
        agentAngularSpeed = Mathf.Max(1f, agentAngularSpeed);
        patrolRadius = Mathf.Max(0.5f, patrolRadius);
        minPatrolLegDistance = Mathf.Max(0f, minPatrolLegDistance);
        patrolDestinationSampleDistance = Mathf.Max(0.05f, patrolDestinationSampleDistance);
        patrolArrivalDistance = Mathf.Max(0.05f, patrolArrivalDistance);
        patrolPauseDuration = Mathf.Max(0f, patrolPauseDuration);
        patrolRepathInterval = Mathf.Max(0.02f, patrolRepathInterval);
        patrolPointAttempts = Mathf.Max(1, patrolPointAttempts);
        wallClearance = Mathf.Max(0f, wallClearance);
        wallCorrectionPadding = Mathf.Max(0f, wallCorrectionPadding);
        wallCorrectionInterval = Mathf.Max(0.05f, wallCorrectionInterval);
        eyeHeight = Mathf.Max(0f, eyeHeight);
        viewDistance = Mathf.Max(0.1f, viewDistance);
        viewAngle = Mathf.Clamp(viewAngle, 1f, 180f);
        targetAimHeight = Mathf.Max(0f, targetAimHeight);
        chaseMemoryDuration = Mathf.Max(0f, chaseMemoryDuration);
        chaseRepathInterval = Mathf.Max(0.02f, chaseRepathInterval);
        lightDangerRadius = Mathf.Max(0.1f, lightDangerRadius);
        lightAttractionRadius = Mathf.Max(0.1f, lightAttractionRadius);
        catchDistance = Mathf.Max(0.05f, catchDistance);

        if (!Application.isPlaying && disableAgentUntilNavMeshReady)
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (agent != null)
            {
                agent.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (CanRunAuthority())
        {
            TickMonsterBrain();
        }

        ApplyDangerResponses();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCatchFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCatchFromCollider(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        TryCatchFromCollider(collision.collider);
    }

    private static bool CanRunAuthority()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    private void EnsureRuntimeCollections()
    {
        if (validationPath == null)
        {
            validationPath = new NavMeshPath();
        }
    }

    private void ResolveAgentReference()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    private void ConfigureAgent()
    {
        if (agent == null)
        {
            return;
        }

        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.autoBraking = true;
        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.speed = mode == MonsterMode.Chase ? chaseSpeed : patrolSpeed;
        agent.acceleration = agentAcceleration;
        agent.angularSpeed = agentAngularSpeed;
        agent.stoppingDistance = Mathf.Min(catchDistance * 0.5f, 0.4f);
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    private void TickMonsterBrain()
    {
        EnsureRuntimeCollections();
        ResolveAgentReference();

        if (!TryPrepareAgentForNavigation())
        {
            return;
        }

        ConfigureAgent();

        if (TryFindBestAwarenessTarget(out SquadCharacterController target))
        {
            BeginChase(target);
        }

        if (mode == MonsterMode.Chase)
        {
            UpdateChase();
        }
        else
        {
            UpdatePatrol();
        }
    }

    private bool TryPrepareAgentForNavigation()
    {
        if (Time.time < nextNavMeshRetryTime)
        {
            return false;
        }

        if (!TryFindNavMeshPosition(transform.position, out Vector3 navPosition))
        {
            DisableAgentUntilRetry();
            nextNavMeshRetryTime = Time.time + navMeshRetryInterval;
            return false;
        }

        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
            if (disableAgentUntilNavMeshReady)
            {
                agent.enabled = false;
            }
        }

        if (!agent.enabled)
        {
            transform.position = navPosition;
            agent.enabled = true;
            ConfigureAgent();
        }

        if (agent.isOnNavMesh)
        {
            return true;
        }

        if (agent.Warp(navPosition))
        {
            return agent.isOnNavMesh;
        }

        DisableAgentUntilRetry();
        nextNavMeshRetryTime = Time.time + navMeshRetryInterval;
        return false;
    }

    private bool TryFindNavMeshPosition(Vector3 position, out Vector3 navPosition)
    {
        navPosition = Vector3.zero;
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleDistance, navMeshAreaMask))
        {
            return false;
        }

        navPosition = hit.position;
        return true;
    }

    private void DisableAgentUntilRetry()
    {
        if (!disableAgentUntilNavMeshReady || agent == null || !agent.enabled)
        {
            return;
        }

        agent.enabled = false;
    }

    private void UpdatePatrol()
    {
        agent.speed = patrolSpeed;
        agent.isStopped = false;

        if (Time.time < nextPatrolDecisionTime)
        {
            KeepAgentCenteredIfNeeded();
            return;
        }

        bool needsDestination = !agent.hasPath ||
                                agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                                (!agent.pathPending && agent.remainingDistance <= patrolArrivalDistance);

        if (!needsDestination)
        {
            KeepAgentCenteredIfNeeded();
            return;
        }

        PickNextPatrolDestination(false);
    }

    private void PickNextPatrolDestination(bool immediate)
    {
        if (!TryPrepareAgentForNavigation())
        {
            return;
        }

        if (!TryFindPatrolDestination(out Vector3 destination))
        {
            if (warnWhenPatrolPointMissing)
            {
                Debug.LogWarning("CastleRoamingMonster: aucun point de patrouille centre trouve sur le NavMesh.", this);
            }

            nextPatrolDecisionTime = Time.time + Mathf.Max(0.25f, patrolPauseDuration);
            return;
        }

        currentPatrolDestination = destination;
        float previousNextUpdate = nextDestinationUpdateTime;
        if (immediate)
        {
            nextDestinationUpdateTime = 0f;
        }

        if (TrySetAgentDestination(destination, patrolRepathInterval))
        {
            nextPatrolDecisionTime = Time.time + Mathf.Max(0.05f, patrolPauseDuration);
            return;
        }

        nextDestinationUpdateTime = previousNextUpdate;
    }

    private bool TryFindPatrolDestination(out Vector3 destination)
    {
        destination = Vector3.zero;
        Vector3 center = patrolCenter != null ? patrolCenter.position : fallbackPatrolCenter;
        Vector3 origin = ResolveCurrentNavPosition();
        float minDistanceSqr = minPatrolLegDistance * minPatrolLegDistance;
        NavMeshTriangulation triangulation = patrolAreaMode == PatrolAreaMode.WholeNavMesh
            ? NavMesh.CalculateTriangulation()
            : new NavMeshTriangulation();
        bool hasWholeNavMeshCandidates = HasUsableTriangulation(triangulation);
        bool hasShortFallback = false;
        Vector3 shortFallback = Vector3.zero;
        float shortFallbackScore = float.NegativeInfinity;
        bool hasUncenteredFallback = false;
        Vector3 uncenteredFallback = Vector3.zero;
        float uncenteredFallbackScore = float.NegativeInfinity;
        int attempts = patrolAreaMode == PatrolAreaMode.WholeNavMesh
            ? Mathf.Max(patrolPointAttempts, 96)
            : patrolPointAttempts;

        for (int i = 0; i < attempts; i++)
        {
            if (!TryPickPatrolCandidate(center, triangulation, hasWholeNavMeshCandidates, out Vector3 point))
            {
                continue;
            }

            if (!TryResolveCenteredNavMeshPoint(point, out Vector3 centeredPoint))
            {
                if (allowUncenteredPatrolFallback && HasCompletePath(point))
                {
                    RecordPatrolFallback(
                        point,
                        origin,
                        minDistanceSqr,
                        ref hasUncenteredFallback,
                        ref uncenteredFallback,
                        ref uncenteredFallbackScore);
                }

                continue;
            }

            if (!HasCompletePath(centeredPoint))
            {
                continue;
            }

            float distanceSqr = FlatDistanceSqr(origin, centeredPoint);
            if (distanceSqr < minDistanceSqr)
            {
                RecordPatrolFallback(
                    centeredPoint,
                    origin,
                    minDistanceSqr,
                    ref hasShortFallback,
                    ref shortFallback,
                    ref shortFallbackScore);
                continue;
            }

            destination = centeredPoint;
            return true;
        }

        if (hasShortFallback)
        {
            destination = shortFallback;
            return true;
        }

        if (allowUncenteredPatrolFallback && hasUncenteredFallback)
        {
            destination = uncenteredFallback;
            return true;
        }

        return false;
    }

    private bool TryPickPatrolCandidate(
        Vector3 center,
        NavMeshTriangulation triangulation,
        bool hasWholeNavMeshCandidates,
        out Vector3 point)
    {
        if (patrolAreaMode == PatrolAreaMode.WholeNavMesh &&
            hasWholeNavMeshCandidates &&
            TryPickWholeNavMeshPoint(triangulation, out point))
        {
            return true;
        }

        return TryPickRadiusPatrolPoint(center, out point);
    }

    private bool TryPickWholeNavMeshPoint(NavMeshTriangulation triangulation, out Vector3 point)
    {
        point = Vector3.zero;
        int triangleCount = triangulation.indices.Length / 3;
        if (triangleCount <= 0)
        {
            return false;
        }

        for (int i = 0; i < 8; i++)
        {
            int triangleIndex = Random.Range(0, triangleCount);
            if (!IsTriangleAreaAllowed(triangulation, triangleIndex))
            {
                continue;
            }

            int index = triangleIndex * 3;
            Vector3 a = triangulation.vertices[triangulation.indices[index]];
            Vector3 b = triangulation.vertices[triangulation.indices[index + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[index + 2]];
            Vector3 candidate = RandomPointInTriangle(a, b, c);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit sample, patrolDestinationSampleDistance, navMeshAreaMask))
            {
                continue;
            }

            point = sample.position;
            return true;
        }

        return false;
    }

    private bool TryPickRadiusPatrolPoint(Vector3 center, out Vector3 point)
    {
        point = Vector3.zero;
        Vector2 random = Random.insideUnitCircle * patrolRadius;
        Vector3 candidate = center + new Vector3(random.x, 0f, random.y);
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit sample, patrolDestinationSampleDistance, navMeshAreaMask))
        {
            return false;
        }

        point = sample.position;
        return true;
    }

    private static bool HasUsableTriangulation(NavMeshTriangulation triangulation)
    {
        return triangulation.vertices != null &&
               triangulation.indices != null &&
               triangulation.vertices.Length > 0 &&
               triangulation.indices.Length >= 3;
    }

    private bool IsTriangleAreaAllowed(NavMeshTriangulation triangulation, int triangleIndex)
    {
        if (triangulation.areas == null || triangleIndex < 0 || triangleIndex >= triangulation.areas.Length)
        {
            return true;
        }

        int area = triangulation.areas[triangleIndex];
        return area >= 0 && area < 32 && (navMeshAreaMask & (1 << area)) != 0;
    }

    private static Vector3 RandomPointInTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        float u = Random.value;
        float v = Random.value;
        if (u + v > 1f)
        {
            u = 1f - u;
            v = 1f - v;
        }

        return a + (b - a) * u + (c - a) * v;
    }

    private static void RecordPatrolFallback(
        Vector3 point,
        Vector3 origin,
        float minDistanceSqr,
        ref bool hasFallback,
        ref Vector3 fallback,
        ref float fallbackScore)
    {
        float distanceSqr = FlatDistanceSqr(origin, point);
        float score = distanceSqr >= minDistanceSqr
            ? distanceSqr + minDistanceSqr
            : distanceSqr;
        if (hasFallback && score <= fallbackScore)
        {
            return;
        }

        fallback = point;
        fallbackScore = score;
        hasFallback = true;
    }

    private Vector3 ResolveCurrentNavPosition()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh ? agent.nextPosition : transform.position;
    }

    private static float FlatDistanceSqr(Vector3 a, Vector3 b)
    {
        Vector3 delta = a - b;
        delta.y = 0f;
        return delta.sqrMagnitude;
    }

    private bool TryResolveCenteredNavMeshPoint(Vector3 point, out Vector3 centeredPoint)
    {
        centeredPoint = point;
        if (!keepPatrolCenteredInCorridors || wallClearance <= 0f)
        {
            return true;
        }

        for (int i = 0; i < 4; i++)
        {
            if (!NavMesh.FindClosestEdge(centeredPoint, out NavMeshHit edge, navMeshAreaMask))
            {
                return false;
            }

            if (edge.distance >= wallClearance)
            {
                return true;
            }

            Vector3 awayFromEdge = centeredPoint - edge.position;
            awayFromEdge.y = 0f;
            if (awayFromEdge.sqrMagnitude <= 0.0001f)
            {
                awayFromEdge = edge.normal;
                awayFromEdge.y = 0f;
            }

            if (awayFromEdge.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float correctionDistance = wallClearance - edge.distance + wallCorrectionPadding;
            Vector3 corrected = centeredPoint + awayFromEdge.normalized * correctionDistance;
            if (!NavMesh.SamplePosition(corrected, out NavMeshHit correctedSample, navMeshSampleDistance, navMeshAreaMask))
            {
                return false;
            }

            centeredPoint = correctedSample.position;
        }

        // Si le couloir est plus etroit que wallClearance, on garde le meilleur point corrige.
        return true;
    }

    private bool HasCompletePath(Vector3 destination)
    {
        if (validationPath == null)
        {
            validationPath = new NavMeshPath();
        }

        Vector3 start = agent != null && agent.enabled && agent.isOnNavMesh ? agent.nextPosition : transform.position;
        if (!NavMesh.CalculatePath(start, destination, navMeshAreaMask, validationPath))
        {
            return false;
        }

        return validationPath.status == NavMeshPathStatus.PathComplete;
    }

    private void KeepAgentCenteredIfNeeded()
    {
        if (!keepPatrolCenteredInCorridors ||
            wallClearance <= 0f ||
            Time.time < nextWallCorrectionTime ||
            mode != MonsterMode.Patrol)
        {
            return;
        }

        Vector3 navPosition = agent != null && agent.enabled && agent.isOnNavMesh ? agent.nextPosition : transform.position;
        if (!NavMesh.FindClosestEdge(navPosition, out NavMeshHit edge, navMeshAreaMask))
        {
            return;
        }

        if (edge.distance >= wallClearance)
        {
            return;
        }

        if (agent != null &&
            agent.hasPath &&
            !agent.pathPending &&
            agent.pathStatus != NavMeshPathStatus.PathInvalid &&
            agent.remainingDistance > Mathf.Max(patrolArrivalDistance, wallClearance + wallCorrectionPadding))
        {
            return;
        }

        Vector3 awayFromEdge = navPosition - edge.position;
        awayFromEdge.y = 0f;
        if (awayFromEdge.sqrMagnitude <= 0.0001f)
        {
            awayFromEdge = edge.normal;
            awayFromEdge.y = 0f;
        }

        if (awayFromEdge.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 corrected = navPosition + awayFromEdge.normalized * (wallClearance - edge.distance + wallCorrectionPadding);
        if (TryResolveCenteredNavMeshPoint(corrected, out Vector3 centeredPoint) && HasCompletePath(centeredPoint))
        {
            nextWallCorrectionTime = Time.time + wallCorrectionInterval;
            nextDestinationUpdateTime = 0f;
            TrySetAgentDestination(centeredPoint, patrolRepathInterval);
        }
    }

    private void BeginChase(SquadCharacterController target)
    {
        if (!IsValidCharacterTarget(target))
        {
            return;
        }

        chaseTarget = target;
        mode = MonsterMode.Chase;
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        lastKnownTargetPosition = target.transform.position;
        lastSeenTargetTime = Time.time;
    }

    private void UpdateChase()
    {
        agent.speed = chaseSpeed;
        agent.isStopped = false;

        if (!IsValidCharacterTarget(chaseTarget))
        {
            ResumePatrol();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, chaseTarget.transform.position);
        if (distanceToTarget <= catchDistance)
        {
            CatchCharacter(chaseTarget);
            ResumePatrol();
            return;
        }

        bool canSeeTarget = CanSeeCharacter(chaseTarget);
        bool canSenseLight = CanSenseCharacterLight(chaseTarget);
        if (canSeeTarget || canSenseLight)
        {
            lastKnownTargetPosition = chaseTarget.transform.position;
            lastSeenTargetTime = Time.time;
            TrySetAgentDestination(lastKnownTargetPosition, chaseRepathInterval);
            return;
        }

        if (Time.time - lastSeenTargetTime <= chaseMemoryDuration)
        {
            TrySetAgentDestination(lastKnownTargetPosition, chaseRepathInterval);
            return;
        }

        ResumePatrol();
    }

    private void ResumePatrol()
    {
        chaseTarget = null;
        mode = MonsterMode.Patrol;
        agent.speed = patrolSpeed;
        nextPatrolDecisionTime = 0f;
        PickNextPatrolDestination(true);
    }

    private bool TrySetAgentDestination(Vector3 destination, float interval)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return false;
        }

        if (Time.time < nextDestinationUpdateTime &&
            (destination - lastDestination).sqrMagnitude <= 0.25f)
        {
            return true;
        }

        if (!agent.SetDestination(destination))
        {
            return false;
        }

        lastDestination = destination;
        nextDestinationUpdateTime = Time.time + Mathf.Max(0.02f, interval);
        return true;
    }

    private bool TryFindBestAwarenessTarget(out SquadCharacterController target)
    {
        target = null;
        float bestScore = float.NegativeInfinity;
        IReadOnlyList<SquadCharacterController> characters = SquadCharacterController.ActiveCharacters;
        if (characters == null)
        {
            return false;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            SquadCharacterController candidate = characters[i];
            if (!IsValidCharacterTarget(candidate))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            bool canSee = distance <= viewDistance && CanSeeCharacter(candidate);
            bool canSenseLight = distance <= lightAttractionRadius && CanSenseCharacterLight(candidate);
            if (!canSee && !canSenseLight)
            {
                continue;
            }

            float score = -distance;
            if (canSee)
            {
                score += 1000f;
            }

            if (canSenseLight)
            {
                score += 500f;
            }

            if (candidate == chaseTarget)
            {
                score += 250f;
            }

            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            target = candidate;
        }

        return target != null;
    }

    private bool CanSeeCharacter(SquadCharacterController character)
    {
        if (!IsValidCharacterTarget(character))
        {
            return false;
        }

        Vector3 eyePosition = ResolveEyePosition();
        Vector3 targetPoint = ResolveTargetPoint(character);
        Vector3 toTarget = targetPoint - eyePosition;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f || distance > viewDistance)
        {
            return false;
        }

        Vector3 flatToTarget = toTarget;
        flatToTarget.y = 0f;
        if (flatToTarget.sqrMagnitude <= 0.0001f)
        {
            flatToTarget = toTarget;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
        }

        float dot = Vector3.Dot(forward.normalized, flatToTarget.normalized);
        float minDot = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
        if (dot < minDot)
        {
            return false;
        }

        return HasClearLineOfSight(eyePosition, targetPoint, character);
    }

    private bool CanSenseCharacterLight(SquadCharacterController character)
    {
        if (!IsValidCharacterTarget(character) || !character.IsFlameEquipped)
        {
            return false;
        }

        Vector3 eyePosition = ResolveEyePosition();
        Vector3 targetPoint = ResolveTargetPoint(character);
        float distance = Vector3.Distance(eyePosition, targetPoint);
        if (distance > lightAttractionRadius)
        {
            return false;
        }

        return !lightAttractionRequiresLineOfSight || HasClearLineOfSight(eyePosition, targetPoint, character);
    }

    private bool HasClearLineOfSight(Vector3 origin, Vector3 targetPoint, SquadCharacterController target)
    {
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            toTarget / distance,
            lineOfSightHits,
            distance,
            lineOfSightBlockers,
            QueryTriggerInteraction.Ignore);

        float closestBlockingDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = lineOfSightHits[i].collider;
            if (hitCollider == null || ShouldIgnoreSightCollider(hitCollider, target))
            {
                continue;
            }

            if (lineOfSightHits[i].distance < closestBlockingDistance)
            {
                closestBlockingDistance = lineOfSightHits[i].distance;
            }
        }

        return float.IsPositiveInfinity(closestBlockingDistance);
    }

    private bool ShouldIgnoreSightCollider(Collider hitCollider, SquadCharacterController target)
    {
        Transform hitTransform = hitCollider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
        {
            return true;
        }

        if (target == null)
        {
            return false;
        }

        return hitTransform == target.transform || hitTransform.IsChildOf(target.transform);
    }

    private Vector3 ResolveEyePosition()
    {
        return eye != null ? eye.position : transform.position + Vector3.up * eyeHeight;
    }

    private Vector3 ResolveTargetPoint(SquadCharacterController character)
    {
        if (character == null)
        {
            return Vector3.zero;
        }

        Collider targetCollider = character.GetComponent<Collider>();
        if (targetCollider == null)
        {
            targetCollider = character.GetComponentInChildren<Collider>();
        }

        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        return character.transform.position + Vector3.up * targetAimHeight;
    }

    private void ApplyDangerResponses()
    {
        if (threatenedThisFrame == null || threatenedCharacters == null || threatReleaseBuffer == null)
        {
            return;
        }

        if (!immobilizeCharactersInDangerRadius && !forceFlameOffInDangerRadius)
        {
            ReleaseAllThreatStates();
            return;
        }

        threatenedThisFrame.Clear();
        IReadOnlyList<SquadCharacterController> characters = SquadCharacterController.ActiveCharacters;
        if (characters != null)
        {
            float radiusSqr = lightDangerRadius * lightDangerRadius;
            for (int i = 0; i < characters.Count; i++)
            {
                SquadCharacterController character = characters[i];
                if (!IsValidCharacterTarget(character))
                {
                    continue;
                }

                if ((character.transform.position - transform.position).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                threatenedThisFrame.Add(character);
                ApplyThreatState(character);
            }
        }

        threatReleaseBuffer.Clear();
        foreach (KeyValuePair<SquadCharacterController, ThreatenedCharacterState> pair in threatenedCharacters)
        {
            if (!threatenedThisFrame.Contains(pair.Key))
            {
                threatReleaseBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < threatReleaseBuffer.Count; i++)
        {
            ReleaseThreatState(threatReleaseBuffer[i], restoreFlameWhenSafe);
        }
    }

    private void ApplyThreatState(SquadCharacterController character)
    {
        if (character == null)
        {
            return;
        }

        if (!threatenedCharacters.TryGetValue(character, out ThreatenedCharacterState state))
        {
            state = new ThreatenedCharacterState();
            threatenedCharacters.Add(character, state);
        }

        if (immobilizeCharactersInDangerRadius)
        {
            if (!state.movementSuppressed)
            {
                character.PushScriptedMovementSuppression();
                state.movementSuppressed = true;
            }

            character.Stop();
        }

        if (forceFlameOffInDangerRadius && character.IsFlameEquipped)
        {
            if (!state.hasFlameSnapshot)
            {
                state.hasFlameSnapshot = true;
                state.flameWasEquipped = character.IsFlameEquipped;
                state.flameSeconds = character.FlameSecondsRemaining;
            }

            character.ApplyFlameState(character.FlameSecondsRemaining, false);
        }
    }

    private void ReleaseAllThreatStates()
    {
        if (threatReleaseBuffer == null || threatenedCharacters == null)
        {
            return;
        }

        threatReleaseBuffer.Clear();
        foreach (KeyValuePair<SquadCharacterController, ThreatenedCharacterState> pair in threatenedCharacters)
        {
            threatReleaseBuffer.Add(pair.Key);
        }

        for (int i = 0; i < threatReleaseBuffer.Count; i++)
        {
            ReleaseThreatState(threatReleaseBuffer[i], restoreFlameWhenSafe);
        }
    }

    private void ReleaseThreatState(SquadCharacterController character, bool restoreFlame)
    {
        if (!threatenedCharacters.TryGetValue(character, out ThreatenedCharacterState state))
        {
            return;
        }

        threatenedCharacters.Remove(character);

        if (character == null)
        {
            return;
        }

        if (state.movementSuppressed)
        {
            character.PopScriptedMovementSuppression();
        }

        if (restoreFlame &&
            state.hasFlameSnapshot &&
            state.flameWasEquipped &&
            character.gameObject.activeInHierarchy)
        {
            int restoreSeconds = Mathf.Max(character.FlameSecondsRemaining, state.flameSeconds);
            character.ApplyFlameState(restoreSeconds, true);
        }
    }

    private void TryCatchFromCollider(Collider other)
    {
        if (!CanRunAuthority())
        {
            return;
        }

        SquadCharacterController character = other != null ? other.GetComponentInParent<SquadCharacterController>() : null;
        if (!IsValidCharacterTarget(character))
        {
            return;
        }

        CatchCharacter(character);
    }

    private void CatchCharacter(SquadCharacterController character)
    {
        if (!CanRunAuthority())
        {
            return;
        }

        if (!IsValidCharacterTarget(character) || caughtCharacters.Contains(character))
        {
            return;
        }

        caughtCharacters.Add(character);
        ReleaseThreatState(character, false);
        character.ApplyFlameState(character.FlameSecondsRemaining, false);
        character.Stop();
        character.SetCurrentHp(0);

        Transform root = character.transform;
        MoveCaughtCharacterToVoid(root);

        if (TryDespawnNetworkCharacter(character))
        {
            return;
        }

        if (disableCaughtCharacter)
        {
            root.gameObject.SetActive(false);
        }
    }

    private void MoveCaughtCharacterToVoid(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Vector3 position = voidSink != null ? voidSink.position + voidOffset : target.position + voidOffset;
        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
        }

        target.position = position;
    }

    private bool TryDespawnNetworkCharacter(SquadCharacterController character)
    {
        if (!despawnNetworkCaughtCharacters || character == null)
        {
            return false;
        }

        NetworkObject networkObject = character.GetComponentInParent<NetworkObject>();
        NetworkManager manager = NetworkManager.Singleton;
        if (networkObject == null ||
            manager == null ||
            !manager.IsListening ||
            !manager.IsServer ||
            !networkObject.IsSpawned)
        {
            return false;
        }

        networkObject.Despawn(true);
        return true;
    }

    private bool IsValidCharacterTarget(SquadCharacterController character)
    {
        return character != null &&
               character.isActiveAndEnabled &&
               character.gameObject.activeInHierarchy &&
               character.CurrentHp > 0 &&
               !caughtCharacters.Contains(character);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Vector3 center = patrolCenter != null
            ? patrolCenter.position
            : (Application.isPlaying ? fallbackPatrolCenter : transform.position);

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(center, patrolRadius);

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, lightDangerRadius);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        Vector3 eyePosition = eye != null ? eye.position : transform.position + Vector3.up * eyeHeight;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
        }

        Vector3 left = Quaternion.AngleAxis(-viewAngle * 0.5f, Vector3.up) * forward.normalized;
        Vector3 right = Quaternion.AngleAxis(viewAngle * 0.5f, Vector3.up) * forward.normalized;
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.55f);
        Gizmos.DrawLine(eyePosition, eyePosition + left * viewDistance);
        Gizmos.DrawLine(eyePosition, eyePosition + right * viewDistance);
        Gizmos.DrawLine(eyePosition + left * viewDistance, eyePosition + right * viewDistance);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(currentPatrolDestination, 0.25f);
            if (mode == MonsterMode.Chase)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(lastKnownTargetPosition, 0.3f);
            }
        }
    }
}
