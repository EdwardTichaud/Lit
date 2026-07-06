using System;
using UnityEngine;

[DefaultExecutionOrder(1040)]
[DisallowMultipleComponent]
public sealed class PortalVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera referenceCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private bool fallbackToMainCamera = true;
    [SerializeField] private bool fallbackToControlledPlayer = true;

    [Header("Visibility")]
    [SerializeField, Min(0f)] private float playerVisibleDistance = 14f;
    [SerializeField, Min(0f)] private float frustumMarginDegrees = 8f;
    [SerializeField, Min(0f)] private float boundsPadding = 1.5f;
    [SerializeField, Min(0.1f)] private float minimumBoundsSize = 1f;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.12f;
    [SerializeField] private bool hideWhenNoReferenceCamera = true;
    [SerializeField] private bool hideWhenNoPlayerAndOffscreen = true;
    [SerializeField] private bool refreshTargetsOnEnable = true;

    [Header("Targets")]
    [SerializeField] private bool controlChildCameras = true;
    [SerializeField] private bool forceEnableCamerasWhenVisible = true;
    [SerializeField] private bool controlRenderers = true;
    [SerializeField] private bool controlParticleSystems = true;
    [SerializeField] private bool controlColliders;
    [SerializeField] private Camera[] targetCameras = Array.Empty<Camera>();
    [SerializeField] private Renderer[] targetRenderers = Array.Empty<Renderer>();
    [SerializeField] private ParticleSystem[] targetParticleSystems = Array.Empty<ParticleSystem>();
    [SerializeField] private Collider[] targetColliders = Array.Empty<Collider>();

    [Header("Debug")]
    [SerializeField] private bool drawGizmos;

    private readonly Plane[] frustumPlanes = new Plane[6];
    private bool[] cameraEnabledStates = Array.Empty<bool>();
    private bool[] rendererEnabledStates = Array.Empty<bool>();
    private bool[] colliderEnabledStates = Array.Empty<bool>();
    private ParticleState[] particleStates = Array.Empty<ParticleState>();
    private float nextRefreshTime;
    private Bounds cachedBounds;
    private bool hasCachedBounds;
    private bool isVisible = true;
    private bool capturedInitialStates;

    private struct ParticleState
    {
        public bool WasPlaying;
        public bool WasPaused;
        public bool EmissionEnabled;
    }

    private void Reset()
    {
        RefreshTargets();
    }

    private void Awake()
    {
        RefreshTargets();
        CaptureInitialStates(force: true);
        EvaluateVisibility(force: true);
    }

    private void OnEnable()
    {
        if (refreshTargetsOnEnable)
        {
            RefreshTargets();
            CaptureInitialStates();
        }

        nextRefreshTime = 0f;
        EvaluateVisibility(force: true);
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        EvaluateVisibility(force: false);
    }

    [ContextMenu("Refresh Targets")]
    public void RefreshTargets()
    {
        if (controlChildCameras)
        {
            targetCameras = FilterOwnedTargets(GetComponentsInChildren<Camera>(true));
        }
        else
        {
            targetCameras = Array.Empty<Camera>();
        }

        if (controlRenderers)
        {
            targetRenderers = FilterOwnedTargets(GetComponentsInChildren<Renderer>(true));
        }
        else
        {
            targetRenderers = Array.Empty<Renderer>();
        }

        if (controlParticleSystems)
        {
            targetParticleSystems = FilterOwnedTargets(GetComponentsInChildren<ParticleSystem>(true));
        }
        else
        {
            targetParticleSystems = Array.Empty<ParticleSystem>();
        }

        if (controlColliders)
        {
            targetColliders = FilterOwnedTargets(GetComponentsInChildren<Collider>(true));
        }
        else
        {
            targetColliders = Array.Empty<Collider>();
        }

        RecalculateBounds();
    }

    private void CaptureInitialStates(bool force = false)
    {
        if (capturedInitialStates && !force)
        {
            return;
        }

        CaptureCameraStates();
        CaptureRendererStates();
        CaptureParticleStates();
        CaptureColliderStates();
        capturedInitialStates = true;
    }

    private void EvaluateVisibility(bool force)
    {
        bool nextVisible = ShouldBeVisible();
        if (!force && nextVisible == isVisible)
        {
            return;
        }

        isVisible = nextVisible;
        ApplyVisibility(nextVisible);
    }

    private bool ShouldBeVisible()
    {
        Camera camera = ResolveReferenceCamera();
        Transform player = ResolvePlayerTarget();

        if (player != null)
        {
            playerTarget = player;
        }

        bool inCamera = camera != null && IsInsideCamera(camera);
        bool nearPlayer = player != null && IsNearPlayer(player);

        if (camera == null && hideWhenNoReferenceCamera)
        {
            return nearPlayer;
        }

        if (player == null && hideWhenNoPlayerAndOffscreen)
        {
            return inCamera;
        }

        return inCamera || nearPlayer;
    }

    private Camera ResolveReferenceCamera()
    {
        if (IsUsableReferenceCamera(referenceCamera))
        {
            return referenceCamera;
        }

        if (fallbackToMainCamera)
        {
            Camera mainCamera = Camera.main;
            if (IsUsableReferenceCamera(mainCamera))
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

        if (SquadManager.Instance != null && IsUsableTransform(SquadManager.Instance.currentCharacter != null
                ? SquadManager.Instance.currentCharacter.transform
                : null))
        {
            return SquadManager.Instance.currentCharacter.transform;
        }

        return null;
    }

    private bool IsInsideCamera(Camera camera)
    {
        if (camera == null)
        {
            return false;
        }

        Bounds bounds = CurrentBounds();
        GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
        if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
        {
            return true;
        }

        if (frustumMarginDegrees <= 0f)
        {
            return false;
        }

        return IsBoundingSphereInsideCameraMargin(camera, bounds, frustumMarginDegrees);
    }

    private bool IsNearPlayer(Transform player)
    {
        if (player == null || playerVisibleDistance <= 0f)
        {
            return false;
        }

        return CurrentBounds().SqrDistance(player.position) <= playerVisibleDistance * playerVisibleDistance;
    }

    private Bounds CurrentBounds()
    {
        if (!hasCachedBounds)
        {
            RecalculateBounds();
        }

        return cachedBounds;
    }

    private void RecalculateBounds()
    {
        bool hasBounds = false;
        Bounds merged = new Bounds(transform.position, Vector3.one * minimumBoundsSize);

        EncapsulateRenderers(ref merged, ref hasBounds);
        EncapsulateColliders(ref merged, ref hasBounds);
        EncapsulateCameras(ref merged, ref hasBounds);

        if (!hasBounds)
        {
            merged = new Bounds(transform.position, Vector3.one * minimumBoundsSize);
        }

        if (boundsPadding > 0f)
        {
            merged.Expand(boundsPadding * 2f);
        }

        float minSize = Mathf.Max(0.1f, minimumBoundsSize);
        Vector3 size = merged.size;
        size.x = Mathf.Max(size.x, minSize);
        size.y = Mathf.Max(size.y, minSize);
        size.z = Mathf.Max(size.z, minSize);
        merged.size = size;

        cachedBounds = merged;
        hasCachedBounds = true;
    }

    private void EncapsulateRenderers(ref Bounds merged, ref bool hasBounds)
    {
        for (int i = 0; targetRenderers != null && i < targetRenderers.Length; i++)
        {
            Renderer target = targetRenderers[i];
            if (target == null)
            {
                continue;
            }

            Encapsulate(ref merged, ref hasBounds, target.bounds);
        }
    }

    private void EncapsulateColliders(ref Bounds merged, ref bool hasBounds)
    {
        for (int i = 0; targetColliders != null && i < targetColliders.Length; i++)
        {
            Collider target = targetColliders[i];
            if (target == null)
            {
                continue;
            }

            Encapsulate(ref merged, ref hasBounds, target.bounds);
        }
    }

    private void EncapsulateCameras(ref Bounds merged, ref bool hasBounds)
    {
        for (int i = 0; targetCameras != null && i < targetCameras.Length; i++)
        {
            Camera target = targetCameras[i];
            if (target == null)
            {
                continue;
            }

            Encapsulate(ref merged, ref hasBounds, new Bounds(target.transform.position, Vector3.one * minimumBoundsSize));
        }
    }

    private static void Encapsulate(ref Bounds merged, ref bool hasBounds, Bounds bounds)
    {
        if (!hasBounds)
        {
            merged = bounds;
            hasBounds = true;
            return;
        }

        merged.Encapsulate(bounds);
    }

    private void ApplyVisibility(bool visible)
    {
        ApplyCameraVisibility(visible);
        ApplyRendererVisibility(visible);
        ApplyParticleVisibility(visible);
        ApplyColliderVisibility(visible);
    }

    private void ApplyCameraVisibility(bool visible)
    {
        if (!controlChildCameras)
        {
            return;
        }

        for (int i = 0; targetCameras != null && i < targetCameras.Length; i++)
        {
            Camera target = targetCameras[i];
            if (target == null || target == referenceCamera)
            {
                continue;
            }

            target.enabled = visible && (forceEnableCamerasWhenVisible || cameraEnabledStates.Length <= i || cameraEnabledStates[i]);
        }
    }

    private void ApplyRendererVisibility(bool visible)
    {
        if (!controlRenderers)
        {
            return;
        }

        for (int i = 0; targetRenderers != null && i < targetRenderers.Length; i++)
        {
            Renderer target = targetRenderers[i];
            if (target == null)
            {
                continue;
            }

            target.enabled = visible && (rendererEnabledStates.Length <= i || rendererEnabledStates[i]);
        }
    }

    private void ApplyParticleVisibility(bool visible)
    {
        if (!controlParticleSystems)
        {
            return;
        }

        for (int i = 0; targetParticleSystems != null && i < targetParticleSystems.Length; i++)
        {
            ParticleSystem target = targetParticleSystems[i];
            if (target == null)
            {
                continue;
            }

            if (visible)
            {
                RestoreParticle(target, i);
            }
            else if (target.isPlaying)
            {
                target.Pause(true);
            }
        }
    }

    private void ApplyColliderVisibility(bool visible)
    {
        if (!controlColliders)
        {
            return;
        }

        for (int i = 0; targetColliders != null && i < targetColliders.Length; i++)
        {
            Collider target = targetColliders[i];
            if (target == null)
            {
                continue;
            }

            target.enabled = visible && (colliderEnabledStates.Length <= i || colliderEnabledStates[i]);
        }
    }

    private void RestoreParticle(ParticleSystem target, int index)
    {
        if (particleStates.Length <= index)
        {
            return;
        }

        ParticleState state = particleStates[index];
        ParticleSystem.EmissionModule emission = target.emission;
        emission.enabled = state.EmissionEnabled;

        if (state.WasPlaying)
        {
            target.Play(true);
        }
        else if (state.WasPaused)
        {
            target.Pause(true);
        }
    }

    private void CaptureCameraStates()
    {
        EnsureStateArray(ref cameraEnabledStates, targetCameras != null ? targetCameras.Length : 0);
        for (int i = 0; targetCameras != null && i < targetCameras.Length; i++)
        {
            cameraEnabledStates[i] = targetCameras[i] != null && targetCameras[i].enabled;
        }
    }

    private void CaptureRendererStates()
    {
        EnsureStateArray(ref rendererEnabledStates, targetRenderers != null ? targetRenderers.Length : 0);
        for (int i = 0; targetRenderers != null && i < targetRenderers.Length; i++)
        {
            rendererEnabledStates[i] = targetRenderers[i] != null && targetRenderers[i].enabled;
        }
    }

    private void CaptureParticleStates()
    {
        EnsureStateArray(ref particleStates, targetParticleSystems != null ? targetParticleSystems.Length : 0);
        for (int i = 0; targetParticleSystems != null && i < targetParticleSystems.Length; i++)
        {
            ParticleSystem target = targetParticleSystems[i];
            if (target == null)
            {
                particleStates[i] = default;
                continue;
            }

            ParticleSystem.EmissionModule emission = target.emission;
            particleStates[i] = new ParticleState
            {
                WasPlaying = target.isPlaying,
                WasPaused = target.isPaused,
                EmissionEnabled = emission.enabled
            };
        }
    }

    private void CaptureColliderStates()
    {
        EnsureStateArray(ref colliderEnabledStates, targetColliders != null ? targetColliders.Length : 0);
        for (int i = 0; targetColliders != null && i < targetColliders.Length; i++)
        {
            colliderEnabledStates[i] = targetColliders[i] != null && targetColliders[i].enabled;
        }
    }

    private T[] FilterOwnedTargets<T>(T[] targets) where T : Component
    {
        if (targets == null || targets.Length == 0)
        {
            return Array.Empty<T>();
        }

        int count = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return Array.Empty<T>();
        }

        T[] filtered = new T[count];
        int index = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            T target = targets[i];
            if (target != null)
            {
                filtered[index++] = target;
            }
        }

        return filtered;
    }

    private static bool IsBoundingSphereInsideCameraMargin(Camera camera, Bounds bounds, float marginDegrees)
    {
        Vector3 center = bounds.center;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
        Vector3 localCenter = camera.transform.InverseTransformPoint(center);
        float z = localCenter.z;
        if (z + radius < camera.nearClipPlane || z - radius > camera.farClipPlane)
        {
            return false;
        }

        if (z <= 0f)
        {
            return false;
        }

        float marginRadians = marginDegrees * Mathf.Deg2Rad;
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

    private static bool IsUsableReferenceCamera(Camera camera)
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

    private static void EnsureStateArray(ref bool[] states, int requiredLength)
    {
        if (states == null || states.Length != requiredLength)
        {
            states = new bool[requiredLength];
        }
    }

    private static void EnsureStateArray(ref ParticleState[] states, int requiredLength)
    {
        if (states == null || states.Length != requiredLength)
        {
            states = new ParticleState[requiredLength];
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Bounds bounds = Application.isPlaying ? CurrentBounds() : new Bounds(transform.position, Vector3.one * minimumBoundsSize);
        Gizmos.color = isVisible ? Color.green : Color.gray;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
