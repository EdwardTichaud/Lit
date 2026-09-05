using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

public enum NavMeshWorldState
{
    Unloaded,
    Loading,
    Building,
    Validating,
    Ready,
    Failed,
    Invalidating
}

/// <summary>
/// Single owner of the world NavMesh lifecycle. SquadAIManager remains a
/// compatibility facade for formation logic and its existing runtime builder.
/// </summary>
[DisallowMultipleComponent]
public sealed class NavMeshWorldService : MonoBehaviour
{
    public struct NavMeshWorldReport
    {
        public string zoneId;
        public NavMeshWorldState state;
        public string source;
        public string reason;
        public int anchorCount;
        public int coveredAnchorCount;
        public int triangulatedVertexCount;
        public Bounds bounds;
    }

    public static NavMeshWorldService Instance { get; private set; }

    [SerializeField] private NavMeshSurface surface;
    [SerializeField, Min(0.05f)] private float anchorSampleRadius = 1.5f;
    [SerializeField, Min(0f)] private float anchorPositionTolerance = 0.15f;
    [SerializeField] private bool logDiagnostics = true;

    private string currentZoneId;
    private Coroutine buildRoutine;
    private bool runtimeBuildInProgress;
    private NavMeshWorldReport lastReport;
    private GameObject runtimeSurfaceHost;

    public NavMeshWorldState State { get; private set; } = NavMeshWorldState.Unloaded;
    public bool IsReady => State == NavMeshWorldState.Ready;
    public bool IsRuntimeBuildInProgress => runtimeBuildInProgress;
    public NavMeshWorldReport LastReport => lastReport;
    internal NavMeshSurface Surface => surface;

    public event Action<NavMeshWorldReport> WorldReady;
    public event Action<NavMeshWorldReport> BuildFailed;
    public event Action<NavMeshWorldState> StateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        surface ??= GetComponent<NavMeshSurface>();
        // GameplaySessionRoot is authored at the district spawn position. A
        // NavMeshData, however, is a world dataset and must use the same
        // origin as the editor bake. Keeping the surface under that offset
        // root makes the exact same asset reappear several metres below the
        // actors at runtime.
        if (surface != null && surface.transform.position != Vector3.zero)
        {
            surface.RemoveData();
            surface.enabled = false;
            surface = null;
        }

        if (surface == null)
        {
            runtimeSurfaceHost = new GameObject("NavMeshWorldSurface");
            runtimeSurfaceHost.transform.SetParent(transform, false);
            runtimeSurfaceHost.transform.position = Vector3.zero;
            runtimeSurfaceHost.transform.rotation = Quaternion.identity;
            runtimeSurfaceHost.transform.localScale = Vector3.one;
            surface = runtimeSurfaceHost.AddComponent<NavMeshSurface>();
        }

        if (surface == null)
        {
            SquadAIManager manager = FindAnyObjectByType<SquadAIManager>();
            if (manager != null)
            {
                surface = manager.GetComponent<NavMeshSurface>();
            }
        }
        if (surface == null)
        {
            surface = gameObject.AddComponent<NavMeshSurface>();
        }
    }

    private void OnDestroy()
    {
        if (runtimeSurfaceHost != null)
        {
            Destroy(runtimeSurfaceHost);
            runtimeSurfaceHost = null;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        // A scene opened directly in the editor has no GameFlow transition to
        // call BeginWorld. In that case, start the same controlled fallback
        // after the scene has had a frame to register its colliders.
        if (State != NavMeshWorldState.Unloaded || !IsGameplayScene(SceneManager.GetActiveScene()))
        {
            return;
        }

        // GameFlowService is the only owner of the normal zone lifecycle. A
        // service object can wake up before the additive loading queue has
        // finished, so never start a competing build merely because the
        // active scene happens to be a gameplay scene. Direct scene launches
        // have no GameFlowService and use this fallback instead.
        GameFlowService flow = GameFlowService.Instance;
        if (flow != null)
        {
            return;
        }

        BeginWorld(SceneManager.GetActiveScene().name, null);
    }

    public void BeginWorld(string zoneId, ZoneManifest manifest)
    {
        currentZoneId = zoneId;
        StopBuildRoutine();
        InvalidateWorldInternal("nouvelle zone");
        SetState(NavMeshWorldState.Loading);
        buildRoutine = StartCoroutine(BeginWorldRoutine(manifest));
    }

    public void InvalidateWorld(string reason)
    {
        StopBuildRoutine();
        InvalidateWorldInternal(reason);
    }

    public void RequestRebuild(string reason = null)
    {
        if (State == NavMeshWorldState.Loading || State == NavMeshWorldState.Building ||
            State == NavMeshWorldState.Validating)
        {
            return;
        }

        if (logDiagnostics)
        {
            Debug.Log("[NavMeshWorld] Rebuild demande | zone=" + currentZoneId +
                      " | reason=" + (string.IsNullOrWhiteSpace(reason) ? "aucune" : reason), this);
        }

        if (buildRoutine == null)
        {
            buildRoutine = StartCoroutine(BuildRuntimeRoutine(reason));
        }
    }

    public bool TryValidatePosition(Vector3 position, int areaMask, out NavMeshHit hit)
    {
        hit = default;
        if (!IsReady)
        {
            return false;
        }

        return TrySamplePosition(position, areaMask, out hit);
    }

    private bool TrySamplePosition(Vector3 position, int areaMask, out NavMeshHit hit)
    {
        hit = default;

        int mask = areaMask == 0 ? NavMesh.AllAreas : areaMask;
        if (!NavMesh.SamplePosition(position, out hit, anchorSampleRadius, mask))
        {
            return false;
        }

        Vector3 delta = hit.position - position;
        return new Vector2(delta.x, delta.z).magnitude <= anchorPositionTolerance &&
               Mathf.Abs(delta.y) <= anchorPositionTolerance;
    }

    public bool TryRegisterAgent(NavMeshAgent agent, Vector3 expectedPosition)
    {
        if (agent == null)
        {
            return false;
        }

        if (!IsReady)
        {
            if (agent.enabled) agent.enabled = false;
            return false;
        }

        int areaMask = agent.areaMask == 0 ? NavMesh.AllAreas : agent.areaMask;
        if (!TryValidatePosition(expectedPosition, areaMask, out NavMeshHit hit))
        {
            if (agent.enabled) agent.enabled = false;
            if (logDiagnostics)
            {
                string nearest = NavMesh.SamplePosition(expectedPosition, out NavMeshHit nearestHit,
                    Mathf.Max(anchorSampleRadius, 4f), areaMask)
                    ? " nearest=" + nearestHit.position + " delta=" + (nearestHit.position - expectedPosition)
                    : " nearest=none";
                Debug.LogWarning("[NavMeshWorld] Agent refuse | actor=" + agent.name +
                                 " | expected=" + expectedPosition + " | tolerance=" + anchorPositionTolerance +
                                 " | areaMask=" + areaMask + nearest, agent);
            }
            return false;
        }

        agent.updateRotation = false;
        if (!agent.enabled)
        {
            agent.enabled = true;
        }

        // Enabling an agent at a validated pose must attach it naturally. A
        // Warp is deliberately forbidden here: it can silently move an actor
        // to another floor when stale data is present.
        if (!agent.isOnNavMesh)
        {
            agent.enabled = false;
            if (logDiagnostics)
            {
                Debug.LogWarning("[NavMeshWorld] Agent active mais hors NavMesh | actor=" + agent.name +
                                 " | expected=" + expectedPosition + " | sample=" + hit.position, agent);
            }
            return false;
        }

        if (logDiagnostics)
        {
            Debug.Log("[NavMeshWorld] Agent valide | actor=" + agent.name +
                      " | position=" + agent.transform.position + " | sample=" + hit.position +
                      " | onNavMesh=" + agent.isOnNavMesh, agent);
        }

        return true;
    }

    internal bool RebuildRuntimeNow(string reason)
    {
        if (runtimeBuildInProgress)
        {
            return false;
        }

        runtimeBuildInProgress = true;
        SetState(NavMeshWorldState.Building);
        // Remove every runtime dataset, not only the one currently referenced
        // by our surface. Scene NavMeshSettings or a previous additive zone can
        // otherwise leave a valid-looking polygon on another floor.
        NavMesh.RemoveAllNavMeshData();
        bool success = SquadAIManager.Instance != null &&
                       SquadAIManager.Instance.RebuildNavMeshForLoadedWorld(reason);
        runtimeBuildInProgress = false;
        SetState(success ? NavMeshWorldState.Validating : NavMeshWorldState.Failed);
        bool worldValid = ValidateWorld("runtime colliders");
        if (!success || !worldValid)
        {
            Fail("bake runtime invalide", "runtime colliders");
            return false;
        }

        SetReady("runtime colliders");
        return true;
    }

    private IEnumerator BeginWorldRoutine(ZoneManifest manifest)
    {
        yield return null;
        yield return new WaitForFixedUpdate();
        Physics.SyncTransforms();

        if (manifest != null && manifest.BakedNavMeshData != null &&
            (manifest.BakedNavMeshAgentTypeId < 0 ||
             manifest.BakedNavMeshAgentTypeId == surface.agentTypeID))
        {
            SetState(NavMeshWorldState.Validating);
            surface.RemoveData();
            surface.navMeshData = manifest.BakedNavMeshData;
            surface.AddData();
            if (ValidateWorld("asset pre-bake"))
            {
                SetReady("asset pre-bake");
                buildRoutine = null;
                yield break;
            }

            surface.RemoveData();
            surface.navMeshData = null;
            if (logDiagnostics)
            {
                Debug.LogWarning("[NavMeshWorld] NavMeshData pre-bake refuse pour la zone " + currentZoneId + ". Fallback runtime.", this);
            }
        }

        RebuildRuntimeNow("zone complete chargee");
        buildRoutine = null;
    }

    private IEnumerator BuildRuntimeRoutine(string reason)
    {
        yield return null;
        yield return new WaitForFixedUpdate();
        Physics.SyncTransforms();
        RebuildRuntimeNow(reason);
        buildRoutine = null;
    }

    private bool ValidateWorld(string source)
    {
        SceneMarker[] markers = FindObjectsByType<SceneMarker>(FindObjectsInactive.Exclude);
        int anchors = 0;
        int covered = 0;
        string firstUncovered = null;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < markers.Length; i++)
        {
            SceneMarker marker = markers[i];
            if (marker == null || !marker.isActiveAndEnabled || !marker.UsesCharacter ||
                !IsGameplayScene(marker.gameObject.scene))
            {
                continue;
            }

            anchors++;
            bounds.Encapsulate(marker.transform.position);
            hasBounds = true;
            if (TrySamplePosition(marker.transform.position, NavMesh.AllAreas, out _))
            {
                covered++;
            }
            else if (firstUncovered == null)
            {
                if (NavMesh.SamplePosition(marker.transform.position, out NavMeshHit nearest,
                                            anchorSampleRadius, NavMesh.AllAreas))
                {
                    Vector3 delta = nearest.position - marker.transform.position;
                    firstUncovered = marker.name + " attendu=" + marker.transform.position +
                                     " proche=" + nearest.position + " delta=" + delta;
                }
                else
                {
                    firstUncovered = marker.name + " attendu=" + marker.transform.position +
                                     " aucun polygone dans " + anchorSampleRadius.ToString("F2") + "m";
                }
            }
        }

        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        lastReport = new NavMeshWorldReport
        {
            zoneId = currentZoneId,
            state = State,
            source = source,
            reason = firstUncovered,
            anchorCount = anchors,
            coveredAnchorCount = covered,
            triangulatedVertexCount = triangulation.vertices != null ? triangulation.vertices.Length : 0,
            bounds = hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.zero)
        };

        return triangulation.vertices != null && triangulation.vertices.Length > 0 &&
               anchors > 0 && covered == anchors;
    }

    private void SetReady(string source)
    {
        SetState(NavMeshWorldState.Ready);
        lastReport.state = State;
        lastReport.source = source;
        lastReport.reason = null;
        if (logDiagnostics)
        {
            Debug.Log("[NavMeshWorld] Ready | zone=" + currentZoneId + " | source=" + source +
                      " | anchors=" + lastReport.coveredAnchorCount + "/" + lastReport.anchorCount +
                      " | vertices=" + lastReport.triangulatedVertexCount, this);
        }
        WorldReady?.Invoke(lastReport);
    }

    private void Fail(string reason, string source)
    {
        if (!string.IsNullOrWhiteSpace(lastReport.reason))
        {
            reason += " | " + lastReport.reason;
        }
        SetState(NavMeshWorldState.Failed);
        lastReport.state = State;
        lastReport.source = source;
        lastReport.reason = reason;
        if (logDiagnostics)
        {
            Debug.LogWarning("[NavMeshWorld] Failed | zone=" + currentZoneId + " | reason=" + reason +
                             " | anchors=" + lastReport.coveredAnchorCount + "/" + lastReport.anchorCount, this);
        }
        BuildFailed?.Invoke(lastReport);
    }

    private void InvalidateWorldInternal(string reason)
    {
        SetState(NavMeshWorldState.Invalidating);
        if (surface != null)
        {
            surface.RemoveData();
            surface.navMeshData = null;
        }
        NavMesh.RemoveAllNavMeshData();
        lastReport = new NavMeshWorldReport
        {
            zoneId = currentZoneId,
            state = NavMeshWorldState.Unloaded,
            reason = reason
        };
        SetState(NavMeshWorldState.Unloaded);
    }

    private void StopBuildRoutine()
    {
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }
    }

    private void SetState(NavMeshWorldState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(state);
    }

    private static bool IsGameplayScene(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && scene.name != "DontDestroyOnLoad" &&
               scene.name != "Bootstrap" && scene.name != "MainMenu";
    }
}
