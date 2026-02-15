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

        agent.nextPosition = transform.position;

        float now = Time.time;
        if (now >= nextUpdateTime || (destination - lastDestination).sqrMagnitude > 0.25f)
        {
            agent.SetDestination(destination);
            lastDestination = destination;
            nextUpdateTime = now + Mathf.Max(0.02f, destinationUpdateInterval);
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
            return agent.isOnNavMesh;
        }

        return false;
    }
}
