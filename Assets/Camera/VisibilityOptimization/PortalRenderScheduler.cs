using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1045)]
[DisallowMultipleComponent]
public sealed class PortalRenderScheduler : MonoBehaviour
{
    private static PortalRenderScheduler instance;

    [Header("References")]
    [SerializeField] private Camera referenceCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private bool fallbackToMainCamera = true;
    [SerializeField] private bool fallbackToControlledPlayer = true;

    [Header("Budget")]
    [SerializeField, Min(1)] private int maxPortalCamerasPerFrame = 1;
    [SerializeField, Min(0f)] private float activationDistance = 12f;
    [SerializeField, Min(1f)] private float portalFarClip = 30f;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;
    [SerializeField, Min(0.1f)] private float fallbackBoundsSize = 2f;

    [Header("Visibility")]
    [SerializeField] private bool requirePlayerDistance = true;
    [SerializeField] private bool requireMainCameraFrustum = true;
    [SerializeField] private bool requireFacingCamera = true;
    [SerializeField, Range(0f, 1f)] private float minimumFacingDot = 0.15f;
    [SerializeField] private bool treatPortalSurfacesAsDoubleSided = true;

    [Header("Camera Mask")]
    [SerializeField] private string[] excludedLayerNames = { "UI", "Overlay", "VisualEffect", "Ignore Raycast" };

    [Header("Debug")]
    [SerializeField] private bool logSelection;

    private readonly List<PortalCameraEntry> entries = new List<PortalCameraEntry>();
    private readonly List<PortalCameraCandidate> candidates = new List<PortalCameraCandidate>();
    private readonly Dictionary<string, Renderer[]> fallbackRenderersByPortalKey = new Dictionary<string, Renderer[]>();
    private readonly Plane[] frustumPlanes = new Plane[6];

    private float nextRefreshTime;
    private int excludedLayerMask;

    public static PortalRenderScheduler Instance => instance;

    private sealed class PortalCameraEntry
    {
        public Component Owner;
        public Camera Camera;
        public Renderer[] PortalRenderers = Array.Empty<Renderer>();
        public bool Requested;
        public float LastScore;
        public bool Captured;
        public bool InitialEnabled;
        public float InitialFarClip;
        public int InitialCullingMask;
    }

    private struct PortalCameraCandidate
    {
        public PortalCameraEntry Entry;
        public float Score;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static bool RequestPortalCameras(
        Component owner,
        Camera[] cameras,
        Renderer[] portalRenderers,
        bool wantsRendering,
        Camera cameraHint,
        Transform playerHint)
    {
        PortalRenderScheduler scheduler = EnsureInstance();
        if (scheduler == null || owner == null)
        {
            return false;
        }

        scheduler.RegisterOrUpdate(owner, cameras, portalRenderers, wantsRendering, cameraHint, playerHint);
        return true;
    }

    public static void Release(Component owner)
    {
        if (instance != null)
        {
            instance.ReleaseOwner(owner);
        }
    }

    private static PortalRenderScheduler EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<PortalRenderScheduler>();
        if (instance != null || !Application.isPlaying)
        {
            return instance;
        }

        GameObject host = new GameObject("PortalRenderScheduler");
        instance = host.AddComponent<PortalRenderScheduler>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        RefreshExcludedLayerMask();
    }

    private void OnEnable()
    {
        RefreshExcludedLayerMask();
        nextRefreshTime = 0f;
    }

    private void OnDisable()
    {
        DisableAllManagedCameras();
    }

    private void OnDestroy()
    {
        RestoreAllManagedCameras();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnValidate()
    {
        maxPortalCamerasPerFrame = Mathf.Max(1, maxPortalCamerasPerFrame);
        activationDistance = Mathf.Max(0f, activationDistance);
        portalFarClip = Mathf.Max(1f, portalFarClip);
        refreshInterval = Mathf.Max(0.02f, refreshInterval);
        fallbackBoundsSize = Mathf.Max(0.1f, fallbackBoundsSize);
        RefreshExcludedLayerMask();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        EvaluatePortalBudget();
    }

    private void RegisterOrUpdate(
        Component owner,
        Camera[] cameras,
        Renderer[] portalRenderers,
        bool wantsRendering,
        Camera cameraHint,
        Transform playerHint)
    {
        if (IsUsableCamera(cameraHint))
        {
            referenceCamera = cameraHint;
        }

        if (IsUsableTransform(playerHint))
        {
            playerTarget = playerHint;
        }

        Renderer[] sharedRenderers = FilterRenderers(portalRenderers);
        MarkMissingOwnerCamerasReleased(owner, cameras);

        for (int i = 0; cameras != null && i < cameras.Length; i++)
        {
            Camera portalCamera = cameras[i];
            if (portalCamera == null)
            {
                continue;
            }

            PortalCameraEntry entry = FindEntry(owner, portalCamera);
            if (entry == null)
            {
                entry = new PortalCameraEntry
                {
                    Owner = owner,
                    Camera = portalCamera
                };
                entries.Add(entry);
            }

            entry.PortalRenderers = sharedRenderers.Length > 0
                ? sharedRenderers
                : ResolveFallbackPortalRenderers(portalCamera);
            entry.Requested = wantsRendering;
            CaptureInitialState(entry);
            ApplyManagedCameraSettings(entry);
            if (!wantsRendering)
            {
                SetCameraEnabled(entry, false);
            }
        }
    }

    private void ReleaseOwner(Component owner)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            PortalCameraEntry entry = entries[i];
            if (entry.Owner != owner)
            {
                continue;
            }

            SetCameraEnabled(entry, false);
            entries.RemoveAt(i);
        }
    }

    private void MarkMissingOwnerCamerasReleased(Component owner, Camera[] currentCameras)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            PortalCameraEntry entry = entries[i];
            if (entry.Owner != owner || ContainsCamera(currentCameras, entry.Camera))
            {
                continue;
            }

            entry.Requested = false;
            SetCameraEnabled(entry, false);
        }
    }

    private void EvaluatePortalBudget()
    {
        PruneInvalidEntries();
        candidates.Clear();

        Camera camera = ResolveReferenceCamera();
        Transform player = ResolvePlayerTarget();

        if (camera != null)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            PortalCameraEntry entry = entries[i];
            ApplyManagedCameraSettings(entry);
            SetCameraEnabled(entry, false);

            if (!entry.Requested || entry.Camera == null || !entry.Camera.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!PassesVisibility(entry, camera, player, out float score))
            {
                continue;
            }

            entry.LastScore = score;
            candidates.Add(new PortalCameraCandidate { Entry = entry, Score = score });
        }

        candidates.Sort((left, right) => left.Score.CompareTo(right.Score));

        int enabledCount = Mathf.Min(maxPortalCamerasPerFrame, candidates.Count);
        for (int i = 0; i < enabledCount; i++)
        {
            SetCameraEnabled(candidates[i].Entry, true);
            if (logSelection)
            {
                Debug.Log($"[PortalRenderScheduler] Rendering '{candidates[i].Entry.Camera.name}' score={candidates[i].Score:0.00}.", candidates[i].Entry.Camera);
            }
        }
    }

    private bool PassesVisibility(PortalCameraEntry entry, Camera camera, Transform player, out float score)
    {
        score = float.MaxValue;
        if (entry == null || entry.Camera == null)
        {
            return false;
        }

        Vector3 focusPosition = player != null
            ? player.position
            : camera != null ? camera.transform.position : transform.position;

        Vector3 portalPosition = ResolvePortalPosition(entry);
        float distance = Vector3.Distance(focusPosition, portalPosition);
        if (requirePlayerDistance && player == null)
        {
            return false;
        }

        if (activationDistance > 0f && distance > activationDistance)
        {
            return false;
        }

        if (requireMainCameraFrustum && camera != null && !IsInMainCamera(entry, camera))
        {
            return false;
        }

        if (requireFacingCamera && HasAnyRenderer(entry.PortalRenderers) && !IsFacingCamera(entry, camera))
        {
            return false;
        }

        float viewportPenalty = CalculateViewportPenalty(camera, portalPosition);
        score = distance + viewportPenalty;
        return true;
    }

    private bool IsInMainCamera(PortalCameraEntry entry, Camera camera)
    {
        if (camera == null)
        {
            return false;
        }

        if (HasAnyRenderer(entry.PortalRenderers))
        {
            for (int i = 0; i < entry.PortalRenderers.Length; i++)
            {
                Renderer renderer = entry.PortalRenderers[i];
                if (renderer != null && GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds))
                {
                    return true;
                }
            }

            return false;
        }

        Bounds bounds = new Bounds(entry.Camera.transform.position, Vector3.one * fallbackBoundsSize);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
    }

    private bool IsFacingCamera(PortalCameraEntry entry, Camera camera)
    {
        if (entry == null || camera == null || !HasAnyRenderer(entry.PortalRenderers))
        {
            return true;
        }

        for (int i = 0; i < entry.PortalRenderers.Length; i++)
        {
            Renderer renderer = entry.PortalRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Vector3 toCamera = camera.transform.position - renderer.bounds.center;
            if (toCamera.sqrMagnitude <= 0.001f)
            {
                return true;
            }

            float dot = Vector3.Dot(renderer.transform.forward, toCamera.normalized);
            if (treatPortalSurfacesAsDoubleSided)
            {
                dot = Mathf.Abs(dot);
            }

            if (dot >= minimumFacingDot)
            {
                return true;
            }
        }

        return false;
    }

    private float CalculateViewportPenalty(Camera camera, Vector3 position)
    {
        if (camera == null)
        {
            return 0f;
        }

        Vector3 viewport = camera.WorldToViewportPoint(position);
        if (viewport.z <= 0f)
        {
            return 1000f;
        }

        float centerOffset = Mathf.Abs(viewport.x - 0.5f) + Mathf.Abs(viewport.y - 0.5f);
        return centerOffset * 4f;
    }

    private Vector3 ResolvePortalPosition(PortalCameraEntry entry)
    {
        if (entry != null && HasAnyRenderer(entry.PortalRenderers))
        {
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < entry.PortalRenderers.Length; i++)
            {
                Renderer renderer = entry.PortalRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
            {
                return bounds.center;
            }
        }

        return entry != null && entry.Camera != null ? entry.Camera.transform.position : transform.position;
    }

    private Camera ResolveReferenceCamera()
    {
        if (IsUsableCamera(referenceCamera))
        {
            return referenceCamera;
        }

        if (fallbackToMainCamera)
        {
            Camera mainCamera = Camera.main;
            if (IsUsableCamera(mainCamera))
            {
                referenceCamera = mainCamera;
                return referenceCamera;
            }
        }

        return null;
    }

    private Transform ResolvePlayerTarget()
    {
        if (IsUsableTransform(playerTarget))
        {
            return playerTarget;
        }

        Transform contextRoot = LocalPlayerContext.LocalCharacterRoot;
        if (IsUsableTransform(contextRoot))
        {
            playerTarget = contextRoot;
            return playerTarget;
        }

        if (fallbackToControlledPlayer)
        {
            GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
            if (controlled != null && controlled.activeInHierarchy)
            {
                playerTarget = controlled.transform;
                return playerTarget;
            }
        }

        if (SquadManager.Instance != null &&
            SquadManager.Instance.currentCharacter != null &&
            IsUsableTransform(SquadManager.Instance.currentCharacter.transform))
        {
            playerTarget = SquadManager.Instance.currentCharacter.transform;
            return playerTarget;
        }

        return null;
    }

    private void ApplyManagedCameraSettings(PortalCameraEntry entry)
    {
        if (entry == null || entry.Camera == null)
        {
            return;
        }

        CaptureInitialState(entry);
        entry.Camera.farClipPlane = portalFarClip;
        entry.Camera.cullingMask = entry.InitialCullingMask & ~excludedLayerMask;
    }

    private void CaptureInitialState(PortalCameraEntry entry)
    {
        if (entry == null || entry.Camera == null || entry.Captured)
        {
            return;
        }

        entry.InitialEnabled = entry.Camera.enabled;
        entry.InitialFarClip = entry.Camera.farClipPlane;
        entry.InitialCullingMask = entry.Camera.cullingMask;
        entry.Captured = true;
    }

    private void SetCameraEnabled(PortalCameraEntry entry, bool enabled)
    {
        if (entry != null && entry.Camera != null)
        {
            entry.Camera.enabled = enabled;
        }
    }

    private void DisableAllManagedCameras()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            SetCameraEnabled(entries[i], false);
        }
    }

    private void RestoreAllManagedCameras()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            PortalCameraEntry entry = entries[i];
            if (entry == null || entry.Camera == null || !entry.Captured)
            {
                continue;
            }

            entry.Camera.farClipPlane = entry.InitialFarClip;
            entry.Camera.cullingMask = entry.InitialCullingMask;
            entry.Camera.enabled = entry.InitialEnabled;
        }
    }

    private void PruneInvalidEntries()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            PortalCameraEntry entry = entries[i];
            if (entry == null || entry.Owner == null || entry.Camera == null)
            {
                entries.RemoveAt(i);
            }
        }
    }

    private PortalCameraEntry FindEntry(Component owner, Camera camera)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            PortalCameraEntry entry = entries[i];
            if (entry.Owner == owner && entry.Camera == camera)
            {
                return entry;
            }
        }

        return null;
    }

    private static bool ContainsCamera(Camera[] cameras, Camera camera)
    {
        if (camera == null)
        {
            return false;
        }

        for (int i = 0; cameras != null && i < cameras.Length; i++)
        {
            if (cameras[i] == camera)
            {
                return true;
            }
        }

        return false;
    }

    private static Renderer[] FilterRenderers(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
        {
            return Array.Empty<Renderer>();
        }

        List<Renderer> filtered = new List<Renderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                filtered.Add(renderers[i]);
            }
        }

        return filtered.Count > 0 ? filtered.ToArray() : Array.Empty<Renderer>();
    }

    private Renderer[] ResolveFallbackPortalRenderers(Camera portalCamera)
    {
        string portalKey = ExtractPortalKey(portalCamera != null ? portalCamera.name : string.Empty);
        if (string.IsNullOrEmpty(portalKey))
        {
            return Array.Empty<Renderer>();
        }

        if (fallbackRenderersByPortalKey.TryGetValue(portalKey, out Renderer[] cachedRenderers))
        {
            return cachedRenderers;
        }

        PortalController[] portals = FindObjectsByType<PortalController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            PortalController portal = portals[i];
            if (portal == null || ExtractPortalKey(portal.name) != portalKey)
            {
                continue;
            }

            Renderer[] renderers = FilterRenderers(portal.GetComponentsInChildren<Renderer>(true));
            fallbackRenderersByPortalKey[portalKey] = renderers;
            return renderers;
        }

        fallbackRenderersByPortalKey[portalKey] = Array.Empty<Renderer>();
        return Array.Empty<Renderer>();
    }

    private static string ExtractPortalKey(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        string key = string.Empty;
        for (int i = 0; i < source.Length; i++)
        {
            if (char.IsDigit(source[i]))
            {
                key += source[i];
            }
        }

        return key;
    }

    private static bool HasAnyRenderer(Renderer[] renderers)
    {
        for (int i = 0; renderers != null && i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshExcludedLayerMask()
    {
        excludedLayerMask = 0;
        for (int i = 0; excludedLayerNames != null && i < excludedLayerNames.Length; i++)
        {
            string layerName = excludedLayerNames[i];
            if (string.IsNullOrWhiteSpace(layerName))
            {
                continue;
            }

            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                excludedLayerMask |= 1 << layer;
            }
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
}
