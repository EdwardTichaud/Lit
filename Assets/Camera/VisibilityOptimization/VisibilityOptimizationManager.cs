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
    [SerializeField] private bool autoDiscoverObjects = true;
    [SerializeField, Min(0.1f)] private float rescanInterval = 3f;
    [SerializeField, Min(0.05f)] private float evaluationInterval = 0.15f;
    [SerializeField, Min(1)] private int maxEvaluationsPerFrame = 512;
    [SerializeField, Min(0f)] private float offscreenGraceSeconds = 0.2f;
    [SerializeField] private bool restoreEverythingWhenDisabled = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos;
    [SerializeField] private bool logStateChanges;

    private readonly List<OptimizableObject> objects = new List<OptimizableObject>();
    private readonly HashSet<OptimizableObject> objectSet = new HashSet<OptimizableObject>();
    private readonly Dictionary<OptimizableObject, float> invisibleSince = new Dictionary<OptimizableObject, float>();
    private readonly Plane[] frustumPlanes = new Plane[6];

    private float nextEvaluationTime;
    private float nextRescanTime;
    private int nextEvaluationIndex;

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
        if (autoDiscoverObjects)
        {
            DiscoverSceneObjects();
        }

        nextEvaluationIndex = 0;
        nextEvaluationTime = 0f;
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

        ProcessEvaluations(now, localPlayer);
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
    }

    private void UnregisterInternal(OptimizableObject optimizableObject)
    {
        if (optimizableObject == null || !objectSet.Remove(optimizableObject))
        {
            return;
        }

        optimizableObject.RestoreAll();
        invisibleSince.Remove(optimizableObject);
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

    private void ProcessEvaluations(float now, Transform localPlayer)
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

            EvaluateObject(optimizableObject, now, localPlayer);
            nextEvaluationIndex++;
            budget--;
        }
    }

    private void EvaluateObject(OptimizableObject optimizableObject, float now, Transform localPlayer)
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

        Bounds bounds = optimizableObject.CurrentBounds;
        bool inFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        bool nearLocalPlayer = optimizableObject.IsNearLocalPlayer(localPlayer);
        bool visible = inFrustum || nearLocalPlayer;
        visible = ApplyOffscreenGrace(optimizableObject, visible, now);

        string reason = BuildReason(visible, inFrustum, nearLocalPlayer);
        VisibilityOptimizationState previousState = optimizableObject.CurrentState;
        optimizableObject.ApplyVisibility(visible, reason);
        if (logStateChanges && previousState != optimizableObject.CurrentState)
        {
            Debug.Log(
                $"[VisibilityOptimization] {optimizableObject.name}: {previousState} -> {optimizableObject.CurrentState} reason='{reason}'",
                optimizableObject);
        }
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

        if (TryFindGameplayCamera(out camera))
        {
            targetCamera = camera;
            return true;
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

    private static string BuildReason(bool visible, bool inFrustum, bool nearLocalPlayer)
    {
        if (nearLocalPlayer)
        {
            return "near_local_player";
        }

        if (inFrustum)
        {
            return "inside_camera_frustum";
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
