using UnityEngine;
using UnityEngine.AI;

// Wrapper NavMeshAgent pour calculer une direction de follow sans deplacer le transform.
[DisallowMultipleComponent]
public class SquadFollowerAgent : MonoBehaviour
{
    [Header("NavMesh")]
    [SerializeField, Tooltip("NavMeshAgent utilise pour le pathfinding.")]
    private NavMeshAgent agent;
    [SerializeField, Tooltip("Intervalle entre SetDestination.")]
    private float destinationUpdateInterval = 0.1f;
    [SerializeField, Tooltip("Stopping distance utilisee par l'agent.")]
    private float stoppingDistance = 0.5f;
    [SerializeField, Tooltip("Vitesse de l'agent (pour le path).")]
    private float agentSpeed = 3.5f;
    [SerializeField, Tooltip("Acceleration de l'agent.")]
    private float agentAcceleration = 12f;
    [SerializeField, Tooltip("Rayon de l'agent.")]
    private float agentRadius = 0.4f;
    [SerializeField, Tooltip("Hauteur de l'agent.")]
    private float agentHeight = 1.8f;
    [SerializeField, Tooltip("Priorite d'avoidance (0=prioritaire).")]
    private int avoidancePriority = 50;
    [SerializeField, Tooltip("Qualite d'avoidance des obstacles.")]
    private ObstacleAvoidanceType avoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    [SerializeField, Tooltip("Distance de sample pour rejoindre le NavMesh.")]
    private float navMeshSampleDistance = 1.5f;
    [SerializeField, Tooltip("Distance de sample pour ramener la destination de formation sur le NavMesh.")]
    private float destinationSampleDistance = 2f;
    [SerializeField, Tooltip("Distance minimale au steering target avant de l'utiliser comme direction.")]
    private float steeringTargetMinDistance = 0.05f;
    [SerializeField, Tooltip("Warp sur le NavMesh si hors navmesh.")]
    private bool warpToNavMesh = true;

    private float nextUpdateTime;
    private Vector3 lastDestination;

    private void Awake()
    {
        EnsureAgent();
        ConfigureAgent();
    }

    private void OnEnable()
    {
        EnsureAgent();
        ConfigureAgent();
    }

    private void OnValidate()
    {
        destinationUpdateInterval = Mathf.Max(0.02f, destinationUpdateInterval);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        agentSpeed = Mathf.Max(0.01f, agentSpeed);
        agentAcceleration = Mathf.Max(0.01f, agentAcceleration);
        agentRadius = Mathf.Max(0.01f, agentRadius);
        agentHeight = Mathf.Max(agentRadius * 2f, agentHeight);
        navMeshSampleDistance = Mathf.Max(0.05f, navMeshSampleDistance);
        destinationSampleDistance = Mathf.Max(0.05f, destinationSampleDistance);
        steeringTargetMinDistance = Mathf.Max(0.01f, steeringTargetMinDistance);
    }

    private void EnsureAgent()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }
    }

    private void ConfigureAgent()
    {
        if (agent == null)
        {
            return;
        }

        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.autoBraking = true;
        agent.speed = agentSpeed;
        agent.acceleration = agentAcceleration;
        agent.stoppingDistance = stoppingDistance;
        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.avoidancePriority = avoidancePriority;
        agent.obstacleAvoidanceType = avoidanceType;
    }

    public bool TryGetDesiredDirection(Vector3 destination, out Vector3 desiredDirection)
    {
        desiredDirection = Vector3.zero;
        if (agent == null)
        {
            return false;
        }

        if (!EnsureOnNavMesh())
        {
            return false;
        }

        Vector3 agentPosition = ResolveAgentNavMeshPosition();
        agent.nextPosition = agentPosition;
        Vector3 navDestination = ResolveDestinationOnNavMesh(destination);

        float now = Time.time;
        if (now >= nextUpdateTime || (navDestination - lastDestination).sqrMagnitude > 0.25f)
        {
            if (!agent.SetDestination(navDestination))
            {
                return false;
            }

            lastDestination = navDestination;
            nextUpdateTime = now + Mathf.Max(0.02f, destinationUpdateInterval);
        }

        if (!agent.pathPending &&
            agent.hasPath &&
            agent.pathStatus != NavMeshPathStatus.PathInvalid)
        {
            Vector3 steeringDirection = agent.steeringTarget - agentPosition;
            steeringDirection.y = 0f;
            if (steeringDirection.sqrMagnitude >= steeringTargetMinDistance * steeringTargetMinDistance)
            {
                desiredDirection = steeringDirection.normalized;
                return true;
            }
        }

        Vector3 desiredVelocity = agent.desiredVelocity;
        desiredVelocity.y = 0f;
        if (desiredVelocity.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        desiredDirection = desiredVelocity.normalized;
        return true;
    }

    private Vector3 ResolveAgentNavMeshPosition()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return agent != null && agent.isOnNavMesh ? agent.nextPosition : transform.position;
    }

    private Vector3 ResolveDestinationOnNavMesh(Vector3 destination)
    {
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, destinationSampleDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return destination;
    }

    private bool EnsureOnNavMesh()
    {
        if (agent.isOnNavMesh)
        {
            return true;
        }

        if (!warpToNavMesh)
        {
            return false;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            lastDestination = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            nextUpdateTime = 0f;
            return agent.isOnNavMesh;
        }

        return false;
    }
}
