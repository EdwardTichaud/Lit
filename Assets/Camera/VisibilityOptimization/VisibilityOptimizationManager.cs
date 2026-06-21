using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1050)]
[DisallowMultipleComponent]
public sealed class VisibilityOptimizationManager : MonoBehaviour
{
    [Serializable]
    public sealed class CategoryProfile
    {
        public VisibilityOptimizationCategory category = VisibilityOptimizationCategory.Decoration;
        [Min(1f)] public float visibleDistance = 90f;
        [Min(1f)] public float lightDistance = 45f;
        [Min(1f)] public float pauseDistance = 120f;
        [Min(0f)] public float playerKeepAliveDistance = 18f;
        [Min(0f)] public float hysteresisDistance = 8f;
        public bool useFrustumCulling = true;
        public bool allowRendererDisable = true;
        public bool allowLightDisable = true;
        public bool allowPause = false;
    }

    private static readonly HashSet<OptimizableObject> PendingObjects = new HashSet<OptimizableObject>();
    private static VisibilityOptimizationManager instance;

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private bool fallbackToMainCamera = true;
    [SerializeField] private bool fallbackToControlledPlayer = true;

    [Header("Runtime")]
    [SerializeField] private bool optimizationEnabled = true;
    [SerializeField] private bool autoDiscoverObjects = true;
    [SerializeField, Min(0.1f)] private float rescanInterval = 3f;
    [SerializeField, Min(0.05f)] private float evaluationInterval = 0.2f;
    [SerializeField, Min(1)] private int maxEvaluationsPerFrame = 256;
    [SerializeField, Min(0f)] private float offscreenGraceSeconds = 0.25f;
    [SerializeField] private bool restoreEverythingWhenDisabled = true;

    [Header("Profiles")]
    [SerializeField] private CategoryProfile[] profiles =
    {
        new CategoryProfile { category = VisibilityOptimizationCategory.StaticMesh, visibleDistance = 120f, lightDistance = 55f, pauseDistance = 160f, playerKeepAliveDistance = 8f, allowPause = false },
        new CategoryProfile { category = VisibilityOptimizationCategory.DynamicObject, visibleDistance = 95f, lightDistance = 50f, pauseDistance = 130f, playerKeepAliveDistance = 18f, allowPause = false },
        new CategoryProfile { category = VisibilityOptimizationCategory.Light, visibleDistance = 80f, lightDistance = 45f, pauseDistance = 120f, playerKeepAliveDistance = 10f, allowRendererDisable = false },
        new CategoryProfile { category = VisibilityOptimizationCategory.NPC, visibleDistance = 110f, lightDistance = 50f, pauseDistance = 140f, playerKeepAliveDistance = 35f, allowPause = true },
        new CategoryProfile { category = VisibilityOptimizationCategory.Decoration, visibleDistance = 85f, lightDistance = 40f, pauseDistance = 115f, playerKeepAliveDistance = 6f, allowPause = false },
        new CategoryProfile { category = VisibilityOptimizationCategory.Interactive, visibleDistance = 95f, lightDistance = 45f, pauseDistance = 130f, playerKeepAliveDistance = 28f, allowPause = false },
        new CategoryProfile { category = VisibilityOptimizationCategory.Critical, visibleDistance = 10000f, lightDistance = 10000f, pauseDistance = 10000f, playerKeepAliveDistance = 10000f, useFrustumCulling = false, allowRendererDisable = false, allowLightDisable = false, allowPause = false }
    };

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos;
    [SerializeField] private bool logStateChanges;

    private readonly List<OptimizableObject> objects = new List<OptimizableObject>();
    private readonly Dictionary<OptimizableObject, int> objectIndices = new Dictionary<OptimizableObject, int>();
    private readonly Queue<int> pendingEvaluationQueue = new Queue<int>();
    private readonly HashSet<int> pendingEvaluationIndices = new HashSet<int>();
    private readonly Dictionary<OptimizableObject, float> invisibleSince = new Dictionary<OptimizableObject, float>();
    private readonly Plane[] frustumPlanes = new Plane[6];

    private float nextEvaluationTime;
    private float nextRescanTime;
    private bool queuedAll;

    public static VisibilityOptimizationManager Instance => instance;

    public static void Register(OptimizableObject optimizableObject)
    {
        if (optimizableObject == null)
        {
            return;
        }

        if (instance == null)
        {
            PendingObjects.Add(optimizableObject);
            return;
        }

        instance.RegisterInternal(optimizableObject);
    }

    public static void Unregister(OptimizableObject optimizableObject)
    {
        if (optimizableObject == null)
        {
            return;
        }

        PendingObjects.Remove(optimizableObject);
        if (instance != null)
        {
            instance.UnregisterInternal(optimizableObject);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ValidateProfiles();
    }

    private void OnEnable()
    {
        ValidateProfiles();
        RegisterPendingObjects();
        if (autoDiscoverObjects)
        {
            DiscoverSceneObjects();
        }

        QueueAllEvaluations();
    }

    private void OnDisable()
    {
        if (restoreEverythingWhenDisabled)
        {
            RestoreAllObjects();
        }

        ClearQueues();
    }

    private void OnDestroy()
    {
        if (restoreEverythingWhenDisabled)
        {
            RestoreAllObjects();
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnValidate()
    {
        rescanInterval = Mathf.Max(0.1f, rescanInterval);
        evaluationInterval = Mathf.Max(0.05f, evaluationInterval);
        maxEvaluationsPerFrame = Mathf.Max(1, maxEvaluationsPerFrame);
        offscreenGraceSeconds = Mathf.Max(0f, offscreenGraceSeconds);
        ValidateProfiles();
    }

    private void LateUpdate()
    {
        if (!optimizationEnabled)
        {
            RestoreAllObjects();
            return;
        }

        RegisterPendingObjects();
        if (!ResolveCamera())
        {
            RestoreAllObjects();
            return;
        }

        ResolvePlayerTarget();
        GeometryUtility.CalculateFrustumPlanes(targetCamera, frustumPlanes);

        float now = Time.unscaledTime;
        if (autoDiscoverObjects && now >= nextRescanTime)
        {
            nextRescanTime = now + rescanInterval;
            DiscoverSceneObjects();
        }

        if (now >= nextEvaluationTime || !queuedAll)
        {
            nextEvaluationTime = now + evaluationInterval;
            QueueAllEvaluations();
        }

        ProcessPendingEvaluations(now);
    }

    private void RegisterInternal(OptimizableObject optimizableObject)
    {
        if (optimizableObject == null || objectIndices.ContainsKey(optimizableObject))
        {
            return;
        }

        int index = objects.Count;
        objects.Add(optimizableObject);
        objectIndices.Add(optimizableObject, index);
        invisibleSince[optimizableObject] = -1f;
        EnqueueEvaluation(index);
    }

    private void UnregisterInternal(OptimizableObject optimizableObject)
    {
        if (optimizableObject == null || !objectIndices.TryGetValue(optimizableObject, out int index))
        {
            return;
        }

        optimizableObject.RestoreAll();
        RemoveAt(index);
    }

    private void RemoveAt(int index)
    {
        int lastIndex = objects.Count - 1;
        OptimizableObject removed = objects[index];
        objectIndices.Remove(removed);
        invisibleSince.Remove(removed);

        if (index != lastIndex)
        {
            OptimizableObject moved = objects[lastIndex];
            objects[index] = moved;
            objectIndices[moved] = index;
            EnqueueEvaluation(index);
        }

        objects.RemoveAt(lastIndex);
        queuedAll = false;
    }

    private void RegisterPendingObjects()
    {
        if (PendingObjects.Count == 0)
        {
            return;
        }

        foreach (OptimizableObject optimizableObject in PendingObjects)
        {
            if (optimizableObject != null)
            {
                RegisterInternal(optimizableObject);
            }
        }

        PendingObjects.Clear();
    }

    private void DiscoverSceneObjects()
    {
#if UNITY_2023_1_OR_NEWER
        OptimizableObject[] sceneObjects = FindObjectsByType<OptimizableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        OptimizableObject[] sceneObjects = FindObjectsByType<OptimizableObject>(FindObjectsInactive.Include);
#endif
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            RegisterInternal(sceneObjects[i]);
        }
    }

    private void QueueAllEvaluations()
    {
        PruneNullObjects();
        for (int i = 0; i < objects.Count; i++)
        {
            EnqueueEvaluation(i);
        }

        queuedAll = true;
    }

    private void EnqueueEvaluation(int index)
    {
        if (index < 0 || index >= objects.Count)
        {
            return;
        }

        if (!pendingEvaluationIndices.Add(index))
        {
            return;
        }

        pendingEvaluationQueue.Enqueue(index);
    }

    private void ProcessPendingEvaluations(float now)
    {
        int budget = Mathf.Max(1, maxEvaluationsPerFrame);
        while (budget > 0 && pendingEvaluationQueue.Count > 0)
        {
            int index = pendingEvaluationQueue.Dequeue();
            pendingEvaluationIndices.Remove(index);

            if (index >= 0 && index < objects.Count)
            {
                EvaluateObject(index, now);
                budget--;
            }
        }
    }

    private void EvaluateObject(int index, float now)
    {
        OptimizableObject optimizableObject = objects[index];
        if (optimizableObject == null)
        {
            return;
        }

        CategoryProfile profile = GetProfile(optimizableObject.Category);
        Bounds bounds = optimizableObject.CurrentBounds;
        Vector3 center = bounds.center;
        float cameraDistance = targetCamera != null
            ? Vector3.Distance(targetCamera.transform.position, center)
            : 0f;
        float playerDistance = playerTarget != null
            ? Vector3.Distance(playerTarget.position, center)
            : float.PositiveInfinity;

        float visibleDistance = ResolveDistance(optimizableObject.VisibleDistanceOverride, profile.visibleDistance, optimizableObject.DistanceMultiplier);
        float lightDistance = ResolveDistance(optimizableObject.LightDistanceOverride, profile.lightDistance, optimizableObject.DistanceMultiplier);
        float pauseDistance = ResolveDistance(optimizableObject.PauseDistanceOverride, profile.pauseDistance, optimizableObject.DistanceMultiplier);
        bool inFrustum = !profile.useFrustumCulling || GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        bool nearPlayer = playerDistance <= profile.playerKeepAliveDistance;

        if (!optimizableObject.OptimizationEnabled || optimizableObject.NeverCull)
        {
            ApplyEvaluation(
                optimizableObject,
                VisibilityOptimizationState.Excluded,
                lightsVisible: true,
                pause: false,
                cameraDistance,
                playerDistance,
                inFrustum,
                "excluded");
            return;
        }

        bool distanceVisible = IsVisibleByDistance(optimizableObject, cameraDistance, visibleDistance, profile.hysteresisDistance);
        bool rendererVisible = nearPlayer || distanceVisible && inFrustum;
        rendererVisible = ApplyOffscreenGrace(optimizableObject, rendererVisible, now);

        bool lightsVisible = !profile.allowLightDisable || nearPlayer || rendererVisible && cameraDistance <= lightDistance;
        bool pause = profile.allowPause &&
                     !nearPlayer &&
                     cameraDistance > pauseDistance &&
                     (!inFrustum || !rendererVisible);

        VisibilityOptimizationState state;
        if (rendererVisible || !profile.allowRendererDisable)
        {
            state = lightsVisible ? VisibilityOptimizationState.Visible : VisibilityOptimizationState.LightCulled;
        }
        else
        {
            state = pause ? VisibilityOptimizationState.Paused : VisibilityOptimizationState.RendererCulled;
        }

        if (!profile.allowRendererDisable && state == VisibilityOptimizationState.RendererCulled)
        {
            state = lightsVisible ? VisibilityOptimizationState.Visible : VisibilityOptimizationState.LightCulled;
        }

        ApplyEvaluation(
            optimizableObject,
            state,
            lightsVisible,
            pause,
            cameraDistance,
            playerDistance,
            inFrustum,
            BuildReason(rendererVisible, lightsVisible, pause, nearPlayer, inFrustum, cameraDistance, visibleDistance));
    }

    private bool IsVisibleByDistance(OptimizableObject optimizableObject, float cameraDistance, float visibleDistance, float hysteresis)
    {
        float adjustedDistance = visibleDistance;
        if (optimizableObject.CurrentState == VisibilityOptimizationState.Visible ||
            optimizableObject.CurrentState == VisibilityOptimizationState.LightCulled)
        {
            adjustedDistance += hysteresis;
        }
        else
        {
            adjustedDistance = Mathf.Max(0f, adjustedDistance - hysteresis);
        }

        return cameraDistance <= adjustedDistance;
    }

    private bool ApplyOffscreenGrace(OptimizableObject optimizableObject, bool rendererVisible, float now)
    {
        if (rendererVisible || offscreenGraceSeconds <= 0f)
        {
            invisibleSince[optimizableObject] = -1f;
            return rendererVisible;
        }

        if (!invisibleSince.TryGetValue(optimizableObject, out float since) || since < 0f)
        {
            invisibleSince[optimizableObject] = now;
            return true;
        }

        return now - since < offscreenGraceSeconds;
    }

    private void ApplyEvaluation(
        OptimizableObject optimizableObject,
        VisibilityOptimizationState state,
        bool lightsVisible,
        bool pause,
        float cameraDistance,
        float playerDistance,
        bool inFrustum,
        string reason)
    {
        VisibilityPauseContext context = new VisibilityPauseContext(
            state,
            optimizableObject.Category,
            cameraDistance,
            playerDistance,
            inFrustum,
            reason);

        VisibilityOptimizationState previousState = optimizableObject.CurrentState;
        optimizableObject.ApplyEvaluation(state, lightsVisible, pause, context);
        if (logStateChanges && previousState != optimizableObject.CurrentState)
        {
            Debug.Log(
                $"[VisibilityOptimization] {optimizableObject.name}: {previousState} -> {optimizableObject.CurrentState} reason='{reason}'",
                optimizableObject);
        }
    }

    private bool ResolveCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
        {
            return true;
        }

        if (!fallbackToMainCamera)
        {
            return false;
        }

        targetCamera = Camera.main;
        return targetCamera != null && targetCamera.isActiveAndEnabled;
    }

    private void ResolvePlayerTarget()
    {
        if (!fallbackToControlledPlayer || playerTarget != null && playerTarget.gameObject.activeInHierarchy)
        {
            return;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled != null && controlled.activeInHierarchy)
        {
            playerTarget = controlled.transform;
        }
    }

    private void RestoreAllObjects()
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null)
            {
                objects[i].RestoreAll();
            }
        }
    }

    private void PruneNullObjects()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (objects[i] == null)
            {
                RemoveAt(i);
            }
        }
    }

    private void ClearQueues()
    {
        pendingEvaluationQueue.Clear();
        pendingEvaluationIndices.Clear();
        queuedAll = false;
    }

    private CategoryProfile GetProfile(VisibilityOptimizationCategory category)
    {
        ValidateProfiles();
        for (int i = 0; i < profiles.Length; i++)
        {
            if (profiles[i] != null && profiles[i].category == category)
            {
                return profiles[i];
            }
        }

        return profiles[0];
    }

    private void ValidateProfiles()
    {
        if (profiles == null || profiles.Length == 0)
        {
            profiles = new[] { new CategoryProfile() };
        }

        for (int i = 0; i < profiles.Length; i++)
        {
            if (profiles[i] == null)
            {
                profiles[i] = new CategoryProfile();
            }

            profiles[i].visibleDistance = Mathf.Max(1f, profiles[i].visibleDistance);
            profiles[i].lightDistance = Mathf.Max(1f, profiles[i].lightDistance);
            profiles[i].pauseDistance = Mathf.Max(1f, profiles[i].pauseDistance);
            profiles[i].playerKeepAliveDistance = Mathf.Max(0f, profiles[i].playerKeepAliveDistance);
            profiles[i].hysteresisDistance = Mathf.Max(0f, profiles[i].hysteresisDistance);
        }
    }

    private static float ResolveDistance(float overrideValue, float profileValue, float multiplier)
    {
        float value = overrideValue >= 0f ? overrideValue : profileValue;
        return Mathf.Max(0f, value * Mathf.Max(0.1f, multiplier));
    }

    private static string BuildReason(
        bool rendererVisible,
        bool lightsVisible,
        bool pause,
        bool nearPlayer,
        bool inFrustum,
        float cameraDistance,
        float visibleDistance)
    {
        if (nearPlayer)
        {
            return "near_player_keep_alive";
        }

        if (pause)
        {
            return "far_paused";
        }

        if (!inFrustum)
        {
            return "outside_camera_frustum";
        }

        if (!rendererVisible)
        {
            return $"distance_culled cameraDistance={cameraDistance:0.0} visibleDistance={visibleDistance:0.0}";
        }

        if (!lightsVisible)
        {
            return "light_distance_culled";
        }

        return "visible";
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        for (int i = 0; i < objects.Count; i++)
        {
            OptimizableObject optimizableObject = objects[i];
            if (optimizableObject == null)
            {
                continue;
            }

            BoundingSphere sphere = optimizableObject.CurrentBoundingSphere;
            Gizmos.color = optimizableObject.CurrentState == VisibilityOptimizationState.Visible
                ? Color.green
                : Color.gray;
            Gizmos.DrawWireSphere(sphere.position, sphere.radius);
        }
    }
}
