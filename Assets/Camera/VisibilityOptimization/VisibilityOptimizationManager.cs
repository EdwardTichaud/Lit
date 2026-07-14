using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(1050)]
[DisallowMultipleComponent]
public sealed class VisibilityOptimizationManager : MonoBehaviour
{
    private static readonly HashSet<OptimizableObject> PendingObjects = new HashSet<OptimizableObject>();
    private static VisibilityOptimizationManager instance;

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private bool fallbackToMainCamera = true;
    [SerializeField] private bool fallbackToControlledPlayer = true;

    [Header("Runtime")]
    [SerializeField] private bool optimizationEnabled = true;
    [SerializeField] private bool discoverObjectsOnEnable = true;
    [SerializeField, Tooltip("A garder desactive en production: les OptimizableObject s'inscrivent eux-memes au manager.")]
    private bool autoDiscoverObjects;
    [SerializeField, Min(0.1f)] private float rescanInterval = 8f;
    [SerializeField, Min(0.05f)] private float evaluationInterval = 0.15f;
    [SerializeField, Min(1)] private int maxEvaluationsPerFrame = 128;
    [SerializeField, Min(0f)] private float offscreenGraceSeconds = 0.2f;
    [SerializeField] private bool restoreEverythingWhenDisabled = true;

    [Header("Obstruction Culling")]
    [SerializeField] private bool obstructionCullingEnabled = true;
    [SerializeField] private LayerMask obstructionLayers = (1 << 3) | (1 << 7) | (1 << 9) | (1 << 11);
    [SerializeField] private QueryTriggerInteraction obstructionTriggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField, Min(0.02f)] private float obstructionCheckInterval = 0.35f;
    [SerializeField, Min(0)] private int maxObstructionChecksPerFrame = 12;
    [SerializeField, Min(0f)] private float obstructionNearDistance = 2f;
    [SerializeField, Range(1, 5)] private int obstructionSampleCount = 1;
    [SerializeField, Min(0f)] private float obstructionSampleSpread = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos;
    [SerializeField] private bool logStateChanges;

    private readonly List<OptimizableObject> objects = new List<OptimizableObject>();
    private readonly HashSet<OptimizableObject> objectSet = new HashSet<OptimizableObject>();
    private readonly Dictionary<OptimizableObject, float> invisibleSince = new Dictionary<OptimizableObject, float>();
    private readonly Dictionary<OptimizableObject, float> lastObstructionCheckTime = new Dictionary<OptimizableObject, float>();
    private readonly Dictionary<OptimizableObject, bool> lastObstructionVisibility = new Dictionary<OptimizableObject, bool>();
    private readonly Plane[] frustumPlanes = new Plane[6];

    private float nextEvaluationTime;
    private float nextRescanTime;
    private float nextCameraSearchTime;
    private int nextEvaluationIndex;
    private int obstructionChecksFrame = -1;
    private int obstructionChecksThisFrame;
    private const float CameraSearchInterval = 1f;

    public static VisibilityOptimizationManager Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PendingObjects.Clear();
        instance = null;
    }

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
    }

    private void OnEnable()
    {
        RegisterPendingObjects();
        if (discoverObjectsOnEnable || autoDiscoverObjects)
        {
            DiscoverSceneObjects();
        }

        nextEvaluationIndex = 0;
        nextEvaluationTime = 0f;
        nextRescanTime = Time.unscaledTime + rescanInterval;
    }

    private void OnDisable()
    {
        if (restoreEverythingWhenDisabled)
        {
            RestoreAllObjects();
        }

        nextEvaluationIndex = 0;
    }

    private void OnDestroy()
    {
        if (restoreEverythingWhenDisabled)
        {
            RestoreAllObjects();
        }

        objects.Clear();
        objectSet.Clear();
        invisibleSince.Clear();
        lastObstructionCheckTime.Clear();
        lastObstructionVisibility.Clear();

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
        obstructionCheckInterval = Mathf.Max(0.02f, obstructionCheckInterval);
        maxObstructionChecksPerFrame = Mathf.Max(0, maxObstructionChecksPerFrame);
        obstructionNearDistance = Mathf.Max(0f, obstructionNearDistance);
        obstructionSampleCount = Mathf.Clamp(obstructionSampleCount, 1, 5);
        obstructionSampleSpread = Mathf.Max(0f, obstructionSampleSpread);
    }

    private void LateUpdate()
    {
        RegisterPendingObjects();

        if (!optimizationEnabled)
        {
            RestoreAllObjects();
            return;
        }

        if (!ResolveVisibilityContext(out Camera resolvedCamera, out Transform localPlayer))
        {
            RestoreAllObjects();
            return;
        }

        GeometryUtility.CalculateFrustumPlanes(resolvedCamera, frustumPlanes);

        float now = Time.unscaledTime;
        if (autoDiscoverObjects && now >= nextRescanTime)
        {
            nextRescanTime = now + rescanInterval;
            DiscoverSceneObjects();
        }

        if (now >= nextEvaluationTime || nextEvaluationIndex >= objects.Count)
        {
            PruneNullObjects();
            nextEvaluationIndex = 0;
            nextEvaluationTime = now + evaluationInterval;
        }

        ProcessEvaluations(resolvedCamera, now, localPlayer);
    }

    private void RegisterInternal(OptimizableObject optimizableObject)
    {
        if (optimizableObject == null || objectSet.Contains(optimizableObject))
        {
            return;
        }

        objects.Add(optimizableObject);
        objectSet.Add(optimizableObject);
        invisibleSince[optimizableObject] = -1f;
        lastObstructionCheckTime[optimizableObject] = -1f;
        lastObstructionVisibility[optimizableObject] = true;
    }

    private void UnregisterInternal(OptimizableObject optimizableObject)
    {
        if (optimizableObject == null || !objectSet.Remove(optimizableObject))
        {
            return;
        }

        optimizableObject.RestoreAll();
        invisibleSince.Remove(optimizableObject);
        lastObstructionCheckTime.Remove(optimizableObject);
        lastObstructionVisibility.Remove(optimizableObject);
        int index = objects.IndexOf(optimizableObject);
        if (index >= 0)
        {
            objects.RemoveAt(index);
            if (nextEvaluationIndex > index)
            {
                nextEvaluationIndex--;
            }
        }
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
        OptimizableObject[] sceneObjects = FindObjectsByType<OptimizableObject>(FindObjectsInactive.Exclude);
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            RegisterInternal(sceneObjects[i]);
        }
    }

    private void ProcessEvaluations(Camera resolvedCamera, float now, Transform localPlayer)
    {
        int budget = Mathf.Max(1, maxEvaluationsPerFrame);
        while (budget > 0 && nextEvaluationIndex < objects.Count)
        {
            OptimizableObject optimizableObject = objects[nextEvaluationIndex];
            if (optimizableObject == null)
            {
                RemoveAt(nextEvaluationIndex);
                continue;
            }

            EvaluateObject(resolvedCamera, optimizableObject, now, localPlayer);
            nextEvaluationIndex++;
            budget--;
        }
    }

    private void EvaluateObject(Camera resolvedCamera, OptimizableObject optimizableObject, float now, Transform localPlayer)
    {
        if (optimizableObject == null)
        {
            return;
        }

        if (!optimizableObject.OptimizationEnabled || optimizableObject.NeverCull)
        {
            optimizableObject.ApplyVisibility(true, "excluded");
            invisibleSince[optimizableObject] = -1f;
            return;
        }

        bool inFrustum = IsInCameraVisibilityRange(resolvedCamera, optimizableObject, out bool inCameraMargin);
        bool nearLocalPlayer = optimizableObject.IsNearLocalPlayer(localPlayer);
        bool visible = inFrustum || nearLocalPlayer;
        bool obstructed = false;
        if (visible && inFrustum && !nearLocalPlayer)
        {
            visible = PassesObstructionCulling(resolvedCamera, optimizableObject, now, out obstructed);
        }

        visible = ApplyOffscreenGrace(optimizableObject, visible, now);

        string reason = BuildReason(visible, inFrustum, inCameraMargin, nearLocalPlayer, obstructed);
        VisibilityOptimizationState previousState = optimizableObject.CurrentState;
        optimizableObject.ApplyVisibility(visible, reason);
        if (logStateChanges && previousState != optimizableObject.CurrentState)
        {
            Debug.Log(
                $"[VisibilityOptimization] {optimizableObject.name}: {previousState} -> {optimizableObject.CurrentState} reason='{reason}'",
                optimizableObject);
        }
    }

    private bool IsInCameraVisibilityRange(Camera camera, OptimizableObject optimizableObject, out bool inCameraMargin)
    {
        inCameraMargin = false;
        Bounds bounds = optimizableObject.CurrentBounds;
        if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
        {
            return true;
        }

        bool currentlyVisible = optimizableObject.CurrentState == VisibilityOptimizationState.Visible;
        float marginDegrees = optimizableObject.GetFrustumMarginDegrees(currentlyVisible);
        if (marginDegrees <= 0f)
        {
            return false;
        }

        if (!IsBoundingSphereInsideCameraMargin(camera, optimizableObject.CurrentBoundingSphere, marginDegrees))
        {
            return false;
        }

        inCameraMargin = true;
        return true;
    }

    private bool PassesObstructionCulling(Camera camera, OptimizableObject optimizableObject, float now, out bool obstructed)
    {
        obstructed = false;
        if (!obstructionCullingEnabled ||
            maxObstructionChecksPerFrame == 0 ||
            camera == null ||
            optimizableObject == null)
        {
            return true;
        }

        if (Time.frameCount != obstructionChecksFrame)
        {
            obstructionChecksFrame = Time.frameCount;
            obstructionChecksThisFrame = 0;
        }

        if (lastObstructionCheckTime.TryGetValue(optimizableObject, out float lastCheck) &&
            now - lastCheck < obstructionCheckInterval)
        {
            bool cachedVisible = !lastObstructionVisibility.TryGetValue(optimizableObject, out bool cached) || cached;
            obstructed = !cachedVisible;
            return cachedVisible;
        }

        if (obstructionChecksThisFrame >= maxObstructionChecksPerFrame)
        {
            bool cachedVisible = !lastObstructionVisibility.TryGetValue(optimizableObject, out bool cached) || cached;
            obstructed = !cachedVisible;
            return cachedVisible;
        }

        obstructionChecksThisFrame++;
        bool visible = HasUnobstructedSample(camera, optimizableObject);
        lastObstructionCheckTime[optimizableObject] = now;
        lastObstructionVisibility[optimizableObject] = visible;
        obstructed = !visible;
        return visible;
    }

    private bool HasUnobstructedSample(Camera camera, OptimizableObject optimizableObject)
    {
        Bounds bounds = optimizableObject.CurrentBounds;
        Vector3 cameraPosition = camera.transform.position;
        int samples = Mathf.Clamp(obstructionSampleCount, 1, 5);
        for (int i = 0; i < samples; i++)
        {
            Vector3 sample = GetObstructionSample(bounds, camera.transform, i, samples);
            Vector3 toSample = sample - cameraPosition;
            float distance = toSample.magnitude;
            if (distance <= obstructionNearDistance || distance <= 0.001f)
            {
                return true;
            }

            Vector3 direction = toSample / distance;
            float rayDistance = Mathf.Max(0f, distance - obstructionNearDistance);
            if (!Physics.Raycast(
                    cameraPosition,
                    direction,
                    out RaycastHit hit,
                    rayDistance,
                    obstructionLayers,
                    obstructionTriggerInteraction))
            {
                return true;
            }

            if (IsOwnedByOptimizable(hit.transform, optimizableObject))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetObstructionSample(Bounds bounds, Transform cameraTransform, int index, int sampleCount)
    {
        if (sampleCount <= 1 || index == 0)
        {
            return bounds.center;
        }

        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
        Vector3 up = cameraTransform != null ? cameraTransform.up : Vector3.up;
        float radius = Mathf.Max(bounds.extents.magnitude * obstructionSampleSpread, 0.05f);

        switch (index)
        {
            case 1:
                return bounds.center + up * radius;
            case 2:
                return bounds.center - up * radius;
            case 3:
                return bounds.center + right * radius;
            default:
                return bounds.center - right * radius;
        }
    }

    private static bool IsOwnedByOptimizable(Transform hitTransform, OptimizableObject optimizableObject)
    {
        if (hitTransform == null || optimizableObject == null)
        {
            return false;
        }

        Transform owner = optimizableObject.transform;
        return hitTransform == owner || hitTransform.IsChildOf(owner);
    }

    private static bool IsBoundingSphereInsideCameraMargin(Camera camera, BoundingSphere sphere, float marginDegrees)
    {
        if (camera == null)
        {
            return false;
        }

        Vector3 localCenter = camera.transform.InverseTransformPoint(sphere.position);
        float radius = Mathf.Max(0f, sphere.radius);
        float z = localCenter.z;
        if (z + radius < camera.nearClipPlane || z - radius > camera.farClipPlane)
        {
            return false;
        }

        float marginRadians = marginDegrees * Mathf.Deg2Rad;
        if (camera.orthographic)
        {
            float worldMargin = Mathf.Tan(marginRadians) * Mathf.Max(1f, Mathf.Abs(z));
            float verticalExtent = camera.orthographicSize + worldMargin + radius;
            float horizontalExtent = camera.orthographicSize * camera.aspect + worldMargin + radius;
            return Mathf.Abs(localCenter.x) <= horizontalExtent &&
                   Mathf.Abs(localCenter.y) <= verticalExtent;
        }

        if (z <= 0f)
        {
            return false;
        }

        float distance = localCenter.magnitude;
        if (distance <= radius)
        {
            return true;
        }

        float verticalHalfFov = camera.fieldOfView * 0.5f * Mathf.Deg2Rad + marginRadians;
        float horizontalHalfFov = Mathf.Atan(Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * camera.aspect) + marginRadians;
        float angularRadius = Mathf.Asin(Mathf.Clamp01(radius / Mathf.Max(distance, 0.0001f)));
        float horizontalAngle = Mathf.Atan2(localCenter.x, z);
        float verticalAngle = Mathf.Atan2(localCenter.y, z);

        return Mathf.Abs(horizontalAngle) <= horizontalHalfFov + angularRadius &&
               Mathf.Abs(verticalAngle) <= verticalHalfFov + angularRadius;
    }

    private bool ApplyOffscreenGrace(OptimizableObject optimizableObject, bool visible, float now)
    {
        if (visible || offscreenGraceSeconds <= 0f)
        {
            invisibleSince[optimizableObject] = -1f;
            return visible;
        }

        if (!invisibleSince.TryGetValue(optimizableObject, out float since) || since < 0f)
        {
            invisibleSince[optimizableObject] = now;
            return true;
        }

        return now - since < offscreenGraceSeconds;
    }

    private bool ResolveVisibilityContext(out Camera camera, out Transform localPlayer)
    {
        localPlayer = ResolveLocalPlayerTarget();
        if (localPlayer != null)
        {
            playerTarget = localPlayer;
        }

        bool multiplayerListening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (multiplayerListening && localPlayer == null)
        {
            camera = null;
            return false;
        }

        if (IsUsableCamera(targetCamera))
        {
            camera = targetCamera;
            return true;
        }

        float now = Time.unscaledTime;
        if (now >= nextCameraSearchTime)
        {
            nextCameraSearchTime = now + CameraSearchInterval;
            if (TryFindGameplayCamera(out camera))
            {
                targetCamera = camera;
                return true;
            }
        }

        if (fallbackToMainCamera && (!multiplayerListening || localPlayer != null))
        {
            Camera mainCamera = Camera.main;
            if (IsUsableCamera(mainCamera))
            {
                camera = mainCamera;
                targetCamera = mainCamera;
                return true;
            }
        }

        camera = null;
        return false;
    }

    private Transform ResolveLocalPlayerTarget()
    {
        Transform contextRoot = LocalPlayerContext.LocalCharacterRoot;
        if (IsUsableTransform(contextRoot))
        {
            return contextRoot;
        }

        if (fallbackToControlledPlayer)
        {
            GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
            if (controlled != null && controlled.activeInHierarchy)
            {
                return controlled.transform;
            }
        }

        bool multiplayerListening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!multiplayerListening && IsUsableTransform(playerTarget))
        {
            return playerTarget;
        }

        return null;
    }

    private static bool TryFindGameplayCamera(out Camera camera)
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (!IsUsableCamera(candidate))
            {
                continue;
            }

            if (candidate.GetComponent<LitUccCameraCharacterBinder>() != null)
            {
                camera = candidate;
                return true;
            }
        }

        camera = null;
        return false;
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

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= objects.Count)
        {
            return;
        }

        OptimizableObject removed = objects[index];
        objects.RemoveAt(index);
        objectSet.Remove(removed);
        invisibleSince.Remove(removed);
        lastObstructionCheckTime.Remove(removed);
        lastObstructionVisibility.Remove(removed);
        if (nextEvaluationIndex > index)
        {
            nextEvaluationIndex--;
        }
    }

    private static bool IsUsableCamera(Camera camera)
    {
        return camera != null &&
               camera.isActiveAndEnabled &&
               camera.gameObject.activeInHierarchy &&
               camera.cameraType == CameraType.Game;
    }

    private static bool IsUsableTransform(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }

    private static string BuildReason(bool visible, bool inFrustum, bool inCameraMargin, bool nearLocalPlayer, bool obstructed)
    {
        if (nearLocalPlayer)
        {
            return "near_local_player";
        }

        if (obstructed && !visible)
        {
            return "camera_obstructed";
        }

        if (inFrustum)
        {
            return inCameraMargin ? "inside_camera_margin" : "inside_camera_frustum";
        }

        return visible ? "offscreen_grace" : "outside_camera_frustum";
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
