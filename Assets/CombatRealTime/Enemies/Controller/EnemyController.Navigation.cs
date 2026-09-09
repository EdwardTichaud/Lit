using System;
using UnityEngine;
using UnityEngine.AI;

public sealed partial class EnemyController
{
    public enum ReadinessStatus
    {
        Ready,
        WaitingForWorld,
        RetryPending,
        Invalid
    }

    [SerializeField]
    private NavMeshAgent NavigationNavigationAgent;
    private float NavigationSampleDistance { get => Configuration.NavigationSampleDistance; set => Configuration.NavigationSampleDistance = value; }
    private float NavigationReattachTolerance { get => Configuration.NavigationReattachTolerance; set => Configuration.NavigationReattachTolerance = value; }
    private float NavigationRetryInterval { get => Configuration.NavigationRetryInterval; set => Configuration.NavigationRetryInterval = value; }
    private float NavigationRebuildRequestInterval { get => Configuration.NavigationRebuildRequestInterval; set => Configuration.NavigationRebuildRequestInterval = value; }
    private bool NavigationLogDiagnostics { get => Configuration.NavigationLogDiagnostics; set => Configuration.NavigationLogDiagnostics = value; }
    private SquadAIManager NavigationNavMeshManager;
    private NavMeshWorldService NavigationNavMeshWorld;
    private float NavigationNextRetryAt;
    private float NavigationNextRebuildRequestAt;
    private string NavigationLastFailure;
    private bool NavigationReadinessLogged;
    public event Action<bool> NavigationReadinessChanged;
    public NavMeshAgent Agent => NavigationNavigationAgent;
    public bool IsReady => NavigationNavigationAgent != null && NavigationNavigationAgent.isActiveAndEnabled && NavigationNavigationAgent.isOnNavMesh;
    public ReadinessStatus Status { get; private set; } = ReadinessStatus.WaitingForWorld;
    public string LastFailure => NavigationLastFailure;

    private void NavigationAwake()
    {
        NavigationNavigationAgent ??= GetComponent<NavMeshAgent>();
        NavigationBindWorld(FindAnyObjectByType<NavMeshWorldService>());
        NavigationBindManager(FindAnyObjectByType<SquadAIManager>());
        if (NavigationNavigationAgent != null)
        {
            NavigationNavigationAgent.updateRotation = false;
            // Never let Unity attach an agent to a stale/global polygon while
            // the world bake is still pending. The component is re-enabled
            // only after the manager has validated the current world data and
            // the local projection is within the strict tolerance below.
            if (NavigationNavMeshWorld != null && !NavigationNavMeshWorld.IsReady)
            {
                NavigationNavigationAgent.enabled = false;
            }
            else if (NavigationNavMeshWorld == null && (NavigationNavMeshManager == null || !NavigationNavMeshManager.IsNavMeshReady))
            {
                NavigationNavigationAgent.enabled = false;
            }
        }
    }

    private void NavigationOnEnable()
    {
        NavigationBindWorld(FindAnyObjectByType<NavMeshWorldService>());
        NavigationBindManager(FindAnyObjectByType<SquadAIManager>());
        NavigationNextRetryAt = 0f;
    }

    private void NavigationOnDisable()
    {
        if (NavigationNavigationAgent != null)
            NavigationNavigationAgent.enabled = false;
        NavigationReadinessLogged = false;
        NavigationBindWorld(null);
        NavigationBindManager(null);
    }

#if UNITY_EDITOR


#endif
    /// <summary>Returns true only when the actor is safely attached to the current world NavMesh.</summary>
    public bool EnsureReady()
    {
        EnemyController motor = GetComponent<EnemyController>();
        if (motor != null && motor.State != CombatEnemyPhysicsState.Navigation)
        {
            Status = ReadinessStatus.WaitingForWorld;
            return false;
        }

        if (NavigationNavigationAgent == null || !gameObject.activeInHierarchy)
        {
            Status = ReadinessStatus.Invalid;
            NavigationReportFailure("NavMeshAgent absent ou actor inactif");
            return false;
        }

        NavigationNavigationAgent.updateRotation = false;
        NavigationBindWorld(NavigationNavMeshWorld != null ? NavigationNavMeshWorld : FindAnyObjectByType<NavMeshWorldService>());
        NavigationBindManager(NavigationNavMeshManager != null ? NavigationNavMeshManager : FindAnyObjectByType<SquadAIManager>());
        if (NavigationNavMeshWorld != null)
        {
            if (!NavigationNavMeshWorld.IsReady)
            {
                if (NavigationNavigationAgent.enabled)
                    NavigationNavigationAgent.enabled = false;
                Status = ReadinessStatus.WaitingForWorld;
                NavigationReportFailure("NavMeshWorldService non pret | etat=" + NavigationNavMeshWorld.State);
                return false;
            }

            if (IsReady)
            {
                Status = ReadinessStatus.Ready;
                NavigationClearFailure();
                return true;
            }

            if (Time.unscaledTime < NavigationNextRetryAt)
            {
                Status = ReadinessStatus.RetryPending;
                return false;
            }

            NavigationNextRetryAt = Time.unscaledTime + NavigationRetryInterval;
            if (NavigationNavMeshWorld.TryRegisterAgent(NavigationNavigationAgent, transform.position))
            {
                Status = ReadinessStatus.Ready;
                NavigationClearFailure();
                if (!NavigationReadinessLogged)
                {
                    NavigationReadinessLogged = true;
                    Debug.Log("[EnemyNavigation] " + name + " pret | agent=enabled | onNavMesh=" + NavigationNavigationAgent.isOnNavMesh + " | position=" + transform.position, this);
                }

                NavigationReadinessChanged?.Invoke(true);
                return true;
            }

            NavigationReportFailure("projection locale refusee par NavMeshWorldService | actor=" + transform.position);
            Status = ReadinessStatus.Invalid;
            return false;
        }

        if (NavigationNavMeshManager == null)
        {
            if (NavigationNavigationAgent.enabled)
                NavigationNavigationAgent.enabled = false;
            Status = ReadinessStatus.Invalid;
            NavigationReportFailure("SquadAIManager introuvable");
            return false;
        }

        // This check must happen before IsReady. A stale NavMeshData from a
        // previous zone can make Unity report isOnNavMesh=true even though the
        // current world bake has failed. Accepting that state is what caused
        // the Juggernaut to jump to (-9.21, -89.92, 47.18).
        if (!NavigationNavMeshManager.IsNavMeshReady)
        {
            if (NavigationNavigationAgent.enabled)
                NavigationNavigationAgent.enabled = false;
            Status = ReadinessStatus.WaitingForWorld;
            NavigationRequestRebuild("NavMesh de monde non pret");
            NavigationReportFailure("NavMesh de monde non pret");
            return false;
        }

        if (IsReady)
        {
            Status = ReadinessStatus.Ready;
            NavigationClearFailure();
            return true;
        }

        if (Time.unscaledTime < NavigationNextRetryAt)
        {
            Status = ReadinessStatus.RetryPending;
            return false;
        }

        NavigationNextRetryAt = Time.unscaledTime + NavigationRetryInterval;
        int areaMask = NavigationNavigationAgent.areaMask == 0 ? NavMesh.AllAreas : NavigationNavigationAgent.areaMask;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavigationSampleDistance, areaMask))
        {
            if (NavigationNavigationAgent.enabled)
                NavigationNavigationAgent.enabled = false;
            Status = ReadinessStatus.Invalid;
            NavigationRequestRebuild("aucune projection NavMesh locale");
            NavigationReportFailure("aucune projection locale | actor=" + transform.position + " | rayon=" + NavigationSampleDistance.ToString("F2") + " | masque=" + areaMask);
            return false;
        }

        Vector3 offset = hit.position - transform.position;
        offset.y = 0f;
        bool closeEnough = offset.sqrMagnitude <= NavigationReattachTolerance * NavigationReattachTolerance && Mathf.Abs(hit.position.y - transform.position.y) <= NavigationReattachTolerance;
        if (!closeEnough)
        {
            if (NavigationNavigationAgent.enabled)
                NavigationNavigationAgent.enabled = false;
            Status = ReadinessStatus.Invalid;
            NavigationReportFailure("projection trop eloignee | actor=" + transform.position + " | nav=" + hit.position);
            return false;
        }

        if (!NavigationNavigationAgent.enabled)
            NavigationNavigationAgent.enabled = true;
        if (!NavigationNavigationAgent.isOnNavMesh)
        {
            NavigationNavigationAgent.enabled = false;
            Status = ReadinessStatus.Invalid;
            NavigationReportFailure("agent active mais rattachement local refuse; aucun Warp autorise");
            return false;
        }

        if (IsReady)
        {
            Status = ReadinessStatus.Ready;
            NavigationClearFailure();
            NavigationReadinessChanged?.Invoke(true);
            return true;
        }

        NavigationReportFailure("agent actif mais hors NavMesh");
        Status = ReadinessStatus.Invalid;
        return false;
    }

    public void Stop()
    {
        if (!IsReady)
            return;
        NavigationNavigationAgent.isStopped = true;
        NavigationNavigationAgent.ResetPath();
    }

    private void NavigationRequestRebuild(string reason)
    {
        if (Time.unscaledTime < NavigationNextRebuildRequestAt)
            return;
        NavigationNextRebuildRequestAt = Time.unscaledTime + NavigationRebuildRequestInterval;
        if (NavigationNavMeshManager == null)
        {
            NavigationReportFailure(reason + " | SquadAIManager introuvable");
            return;
        }

        NavigationNavMeshManager.RequestNavMeshRebuild("ennemi en attente: " + name + " | " + reason);
    }

    private void NavigationBindManager(SquadAIManager next)
    {
        if (NavigationNavMeshManager == next)
            return;
        if (NavigationNavMeshManager != null)
            NavigationNavMeshManager.NavMeshRebuildCompleted -= NavigationOnNavMeshRebuilt;
        NavigationNavMeshManager = next;
        if (NavigationNavMeshManager != null)
            NavigationNavMeshManager.NavMeshRebuildCompleted += NavigationOnNavMeshRebuilt;
    }

    private void NavigationBindWorld(NavMeshWorldService next)
    {
        if (NavigationNavMeshWorld == next)
            return;
        if (NavigationNavMeshWorld != null)
        {
            NavigationNavMeshWorld.WorldReady -= NavigationOnWorldReady;
            NavigationNavMeshWorld.BuildFailed -= NavigationOnWorldBuildFailed;
            NavigationNavMeshWorld.StateChanged -= NavigationOnWorldStateChanged;
        }

        NavigationNavMeshWorld = next;
        if (NavigationNavMeshWorld != null)
        {
            NavigationNavMeshWorld.WorldReady += NavigationOnWorldReady;
            NavigationNavMeshWorld.BuildFailed += NavigationOnWorldBuildFailed;
            NavigationNavMeshWorld.StateChanged += NavigationOnWorldStateChanged;
        }
    }

    private void NavigationOnWorldReady(NavMeshWorldService.NavMeshWorldReport _)
    {
        NavigationNextRetryAt = 0f;
    }

    private void NavigationOnWorldStateChanged(NavMeshWorldState next)
    {
        if (next == NavMeshWorldState.Ready)
            return;
        if (NavigationNavigationAgent != null)
            NavigationNavigationAgent.enabled = false;
        Status = ReadinessStatus.WaitingForWorld;
        NavigationReadinessLogged = false;
        NavigationReadinessChanged?.Invoke(false);
    }

    private void NavigationOnWorldBuildFailed(NavMeshWorldService.NavMeshWorldReport report)
    {
        if (NavigationNavigationAgent != null && NavigationNavigationAgent.enabled)
            NavigationNavigationAgent.enabled = false;
        NavigationReportFailure("NavMeshWorldService echoue | " + report.reason);
    }

    private void NavigationOnNavMeshRebuilt(SquadAIManager.NavMeshBuildReport report)
    {
        NavigationNextRetryAt = 0f;
        if (!report.succeeded && (NavigationNavMeshManager == null || !NavigationNavMeshManager.IsNavMeshReady))
        {
            if (NavigationNavigationAgent != null && NavigationNavigationAgent.enabled)
                NavigationNavigationAgent.enabled = false;
            NavigationReportFailure("bake NavMesh echoue | " + report.reason + " | sources=" + report.sourceCount);
        }
    }

    private void NavigationClearFailure()
    {
        if (NavigationLastFailure == null)
            return;
        NavigationLastFailure = null;
        if (NavigationLogDiagnostics)
            Debug.Log("[EnemyNavigation] " + name + " NavMesh pret | position=" + transform.position, this);
    }

    private void NavigationReportFailure(string reason)
    {
        if (NavigationLastFailure == reason)
            return;
        NavigationLastFailure = reason;
        NavigationReadinessChanged?.Invoke(false);
        if (NavigationLogDiagnostics)
            Debug.LogWarning("[EnemyNavigation] " + name + " en attente | " + reason, this);
    }
}