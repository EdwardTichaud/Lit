using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Owns the safe NavMesh lifecycle for one enemy. It never moves the actor
/// transform itself: a missing NavMesh keeps the encounter waiting until the
/// world surface is ready or has been rebuilt.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyNavigationController : MonoBehaviour
{
    public enum ReadinessStatus { Ready, WaitingForWorld, RetryPending, Invalid }
    [SerializeField] private NavMeshAgent navigationAgent;
    [SerializeField, Min(0.1f)] private float sampleDistance = 1.5f;
    [SerializeField, Min(0f)] private float reattachTolerance = 0.15f;
    [SerializeField, Min(0.02f)] private float retryInterval = 0.25f;
    [SerializeField, Min(0.1f)] private float rebuildRequestInterval = 1f;
    [SerializeField] private bool logDiagnostics;

    private SquadAIManager navMeshManager;
    private NavMeshWorldService navMeshWorld;
    private float nextRetryAt;
    private float nextRebuildRequestAt;
    private string lastFailure;
    private bool readinessLogged;

    public event Action<bool> NavigationReadinessChanged;
    public NavMeshAgent Agent => navigationAgent;
    public bool IsReady => navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh;
    public ReadinessStatus Status { get; private set; } = ReadinessStatus.WaitingForWorld;
    public string LastFailure => lastFailure;

    private void Reset() => navigationAgent = GetComponent<NavMeshAgent>();

    private void Awake()
    {
        navigationAgent ??= GetComponent<NavMeshAgent>();
        BindWorld(FindAnyObjectByType<NavMeshWorldService>());
        BindManager(FindAnyObjectByType<SquadAIManager>());
        if (navigationAgent != null)
        {
            navigationAgent.updateRotation = false;
            // Never let Unity attach an agent to a stale/global polygon while
            // the world bake is still pending. The component is re-enabled
            // only after the manager has validated the current world data and
            // the local projection is within the strict tolerance below.
            if (navMeshWorld != null && !navMeshWorld.IsReady)
            {
                navigationAgent.enabled = false;
            }
            else if (navMeshWorld == null && (navMeshManager == null || !navMeshManager.IsNavMeshReady))
            {
                navigationAgent.enabled = false;
            }
        }
    }

    private void OnEnable()
    {
        BindWorld(FindAnyObjectByType<NavMeshWorldService>());
        BindManager(FindAnyObjectByType<SquadAIManager>());
        nextRetryAt = 0f;
    }

    private void OnDisable()
    {
        readinessLogged = false;
        BindWorld(null);
        BindManager(null);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        sampleDistance = Mathf.Max(.1f, sampleDistance);
        reattachTolerance = Mathf.Max(0f, reattachTolerance);
        retryInterval = Mathf.Max(.02f, retryInterval);
        rebuildRequestInterval = Mathf.Max(.1f, rebuildRequestInterval);
    }
#endif

    /// <summary>Returns true only when the actor is safely attached to the current world NavMesh.</summary>
    public bool EnsureReady()
    {
        CombatEnemyPhysicsMotor motor = GetComponent<CombatEnemyPhysicsMotor>();
        if (motor != null && motor.State != CombatEnemyPhysicsState.Navigation)
        {
            Status = ReadinessStatus.WaitingForWorld;
            return false;
        }
        if (navigationAgent == null || !gameObject.activeInHierarchy)
        {
            Status = ReadinessStatus.Invalid;
            ReportFailure("NavMeshAgent absent ou actor inactif");
            return false;
        }

        navigationAgent.updateRotation = false;
        BindWorld(navMeshWorld != null ? navMeshWorld : FindAnyObjectByType<NavMeshWorldService>());
        BindManager(navMeshManager != null ? navMeshManager : FindAnyObjectByType<SquadAIManager>());

        if (navMeshWorld != null)
        {
            if (!navMeshWorld.IsReady)
            {
                if (navigationAgent.enabled) navigationAgent.enabled = false;
                Status = ReadinessStatus.WaitingForWorld;
                ReportFailure("NavMeshWorldService non pret | etat=" + navMeshWorld.State);
                return false;
            }

            if (IsReady)
            {
                Status = ReadinessStatus.Ready;
                ClearFailure();
                return true;
            }
            if (Time.unscaledTime < nextRetryAt)
            {
                Status = ReadinessStatus.RetryPending;
                return false;
            }
            nextRetryAt = Time.unscaledTime + retryInterval;
            if (navMeshWorld.TryRegisterAgent(navigationAgent, transform.position))
            {
                Status = ReadinessStatus.Ready;
                ClearFailure();
                if (!readinessLogged)
                {
                    readinessLogged = true;
                    Debug.Log("[EnemyNavigation] " + name + " pret | agent=enabled | onNavMesh=" +
                              navigationAgent.isOnNavMesh + " | position=" + transform.position, this);
                }
                NavigationReadinessChanged?.Invoke(true);
                return true;
            }

            ReportFailure("projection locale refusee par NavMeshWorldService | actor=" + transform.position);
            Status = ReadinessStatus.Invalid;
            return false;
        }

        if (navMeshManager == null)
        {
            if (navigationAgent.enabled) navigationAgent.enabled = false;
            Status = ReadinessStatus.Invalid;
            ReportFailure("SquadAIManager introuvable");
            return false;
        }

        // This check must happen before IsReady. A stale NavMeshData from a
        // previous zone can make Unity report isOnNavMesh=true even though the
        // current world bake has failed. Accepting that state is what caused
        // the Juggernaut to jump to (-9.21, -89.92, 47.18).
        if (!navMeshManager.IsNavMeshReady)
        {
            if (navigationAgent.enabled) navigationAgent.enabled = false;
            Status = ReadinessStatus.WaitingForWorld;
            RequestRebuild("NavMesh de monde non pret");
            ReportFailure("NavMesh de monde non pret");
            return false;
        }

        if (IsReady)
        {
            Status = ReadinessStatus.Ready;
            ClearFailure();
            return true;
        }
        if (Time.unscaledTime < nextRetryAt)
        {
            Status = ReadinessStatus.RetryPending;
            return false;
        }
        nextRetryAt = Time.unscaledTime + retryInterval;

        int areaMask = navigationAgent.areaMask == 0 ? NavMesh.AllAreas : navigationAgent.areaMask;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, sampleDistance, areaMask))
        {
            if (navigationAgent.enabled) navigationAgent.enabled = false;
            Status = ReadinessStatus.Invalid;
            RequestRebuild("aucune projection NavMesh locale");
            ReportFailure("aucune projection locale | actor=" + transform.position + " | rayon=" + sampleDistance.ToString("F2") + " | masque=" + areaMask);
            return false;
        }

        Vector3 offset = hit.position - transform.position;
        offset.y = 0f;
        bool closeEnough = offset.sqrMagnitude <= reattachTolerance * reattachTolerance &&
                           Mathf.Abs(hit.position.y - transform.position.y) <= reattachTolerance;
        if (!closeEnough)
        {
            if (navigationAgent.enabled) navigationAgent.enabled = false;
            Status = ReadinessStatus.Invalid;
            ReportFailure("projection trop eloignee | actor=" + transform.position + " | nav=" + hit.position);
            return false;
        }

        if (!navigationAgent.enabled) navigationAgent.enabled = true;
        if (!navigationAgent.isOnNavMesh)
        {
            navigationAgent.enabled = false;
            Status = ReadinessStatus.Invalid;
            ReportFailure("agent active mais rattachement local refuse; aucun Warp autorise");
            return false;
        }

        if (IsReady)
        {
            Status = ReadinessStatus.Ready;
            ClearFailure();
            NavigationReadinessChanged?.Invoke(true);
            return true;
        }

        ReportFailure("agent actif mais hors NavMesh");
        Status = ReadinessStatus.Invalid;
        return false;
    }

    public void Stop()
    {
        if (!IsReady) return;
        navigationAgent.isStopped = true;
        navigationAgent.ResetPath();
    }

    private void RequestRebuild(string reason)
    {
        if (Time.unscaledTime < nextRebuildRequestAt) return;
        nextRebuildRequestAt = Time.unscaledTime + rebuildRequestInterval;
        if (navMeshManager == null)
        {
            ReportFailure(reason + " | SquadAIManager introuvable");
            return;
        }
        navMeshManager.RequestNavMeshRebuild("ennemi en attente: " + name + " | " + reason);
    }

    private void BindManager(SquadAIManager next)
    {
        if (navMeshManager == next) return;
        if (navMeshManager != null) navMeshManager.NavMeshRebuildCompleted -= OnNavMeshRebuilt;
        navMeshManager = next;
        if (navMeshManager != null) navMeshManager.NavMeshRebuildCompleted += OnNavMeshRebuilt;
    }

    private void BindWorld(NavMeshWorldService next)
    {
        if (navMeshWorld == next) return;
        if (navMeshWorld != null)
        {
            navMeshWorld.WorldReady -= OnWorldReady;
            navMeshWorld.BuildFailed -= OnWorldBuildFailed;
        }
        navMeshWorld = next;
        if (navMeshWorld != null)
        {
            navMeshWorld.WorldReady += OnWorldReady;
            navMeshWorld.BuildFailed += OnWorldBuildFailed;
        }
    }

    private void OnWorldReady(NavMeshWorldService.NavMeshWorldReport _)
    {
        nextRetryAt = 0f;
    }

    private void OnWorldBuildFailed(NavMeshWorldService.NavMeshWorldReport report)
    {
        if (navigationAgent != null && navigationAgent.enabled) navigationAgent.enabled = false;
        ReportFailure("NavMeshWorldService echoue | " + report.reason);
    }

    private void OnNavMeshRebuilt(SquadAIManager.NavMeshBuildReport report)
    {
        nextRetryAt = 0f;
        if (!report.succeeded && (navMeshManager == null || !navMeshManager.IsNavMeshReady))
        {
            if (navigationAgent != null && navigationAgent.enabled) navigationAgent.enabled = false;
            ReportFailure("bake NavMesh echoue | " + report.reason + " | sources=" + report.sourceCount);
        }
    }

    private void ClearFailure()
    {
        if (lastFailure == null) return;
        lastFailure = null;
        if (logDiagnostics) Debug.Log("[EnemyNavigation] " + name + " NavMesh pret | position=" + transform.position, this);
    }

    private void ReportFailure(string reason)
    {
        if (lastFailure == reason) return;
        lastFailure = reason;
        NavigationReadinessChanged?.Invoke(false);
        if (logDiagnostics) Debug.LogWarning("[EnemyNavigation] " + name + " en attente | " + reason, this);
    }
}
