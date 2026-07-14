using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[DefaultExecutionOrder(1048)]
[DisallowMultipleComponent]
public sealed class LightRenderBudgetManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera referenceCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private bool fallbackToMainCamera = true;
    [SerializeField] private bool fallbackToControlledPlayer = true;

    [Header("Discovery")]
    [SerializeField] private bool discoverLightsOnEnable = true;
    [SerializeField] private bool includeInactiveLights;
    [SerializeField] private bool skipBakedLights = true;
    [SerializeField] private LayerMask managedLightLayers = ~0;
    [SerializeField] private bool includePointLights = true;
    [SerializeField] private bool includeSpotLights = true;
    [SerializeField] private bool includeDirectionalLights;
    [SerializeField] private bool includeAreaLights;

    [Header("Budget")]
    [SerializeField, Min(0.02f)] private float evaluationInterval = 0.2f;
    [SerializeField, Min(0)] private int maxRealtimeLights = 24;
    [SerializeField, Min(0)] private int maxShadowedLights = 8;
    [SerializeField, Min(0f)] private float lightCullDistance = 22f;
    [SerializeField, Min(0f)] private float shadowCullDistance = 12f;
    [SerializeField, Min(0f)] private float inViewDistanceMultiplier = 1.25f;

    [Header("HDRP")]
    [SerializeField, Min(128)] private int hdrpShadowResolutionCap = 512;
    [SerializeField] private bool enableContactShadowsForBudgetedLights;

    [Header("Runtime")]
    [SerializeField] private bool restoreInitialStateWhenDisabled = true;
    [SerializeField] private bool logBudget;

    private readonly List<ManagedLight> managedLights = new List<ManagedLight>();
    private readonly List<ManagedLight> lightCandidates = new List<ManagedLight>();
    private readonly List<ManagedLight> shadowCandidates = new List<ManagedLight>();
    private readonly Plane[] frustumPlanes = new Plane[6];

    private float nextEvaluationTime;
    private int lastEnabledLightCount;
    private int lastShadowedLightCount;

    private sealed class ManagedLight
    {
        public Light Light;
        public HDAdditionalLightData HdLight;
        public LightRenderPriority Priority;
        public bool OriginalEnabled;
        public LightShadows OriginalShadows;
        public LightRenderMode OriginalRenderMode;
        public bool OriginalContactShadowUseOverride;
        public bool OriginalContactShadowOverride;
        public bool Captured;
        public float Distance;
        public float Score;
        public bool Critical;
        public bool InView;
    }

    private void OnEnable()
    {
        ValidateFields();
        if (discoverLightsOnEnable)
        {
            DiscoverSceneLights();
        }

        nextEvaluationTime = 0f;
    }

    private void OnDisable()
    {
        if (restoreInitialStateWhenDisabled)
        {
            RestoreAllLights();
        }
    }

    private void OnDestroy()
    {
        if (restoreInitialStateWhenDisabled)
        {
            RestoreAllLights();
        }
    }

    private void OnValidate()
    {
        ValidateFields();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = Time.unscaledTime + evaluationInterval;
        ApplyBudget();
    }

    [ContextMenu("Discover Scene Lights")]
    public void DiscoverSceneLights()
    {
        managedLights.Clear();
        FindObjectsInactive inactiveMode = includeInactiveLights ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        Light[] lights = FindObjectsByType<Light>(inactiveMode, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light targetLight = lights[i];
            if (!ShouldManageLight(targetLight))
            {
                continue;
            }

            ManagedLight managed = new ManagedLight
            {
                Light = targetLight,
                HdLight = targetLight.GetComponent<HDAdditionalLightData>(),
                Priority = targetLight.GetComponent<LightRenderPriority>()
            };
            CaptureInitialState(managed);
            managedLights.Add(managed);
        }
    }

    [ContextMenu("Apply Budget Now")]
    public void ApplyBudget()
    {
        PruneInvalidLights();
        lightCandidates.Clear();
        shadowCandidates.Clear();

        Camera camera = ResolveReferenceCamera();
        Transform focusTarget = ResolveFocusTarget();
        Vector3 focusPosition = focusTarget != null
            ? focusTarget.position
            : camera != null ? camera.transform.position : transform.position;

        if (camera != null)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
        }

        for (int i = 0; i < managedLights.Count; i++)
        {
            ManagedLight managed = managedLights[i];
            CaptureInitialState(managed);

            if (managed.Light == null || !ShouldManageLight(managed.Light) || !managed.OriginalEnabled)
            {
                SetManagedLightEnabled(managed, false);
                continue;
            }

            managed.Priority = managed.Priority != null ? managed.Priority : managed.Light.GetComponent<LightRenderPriority>();
            managed.Critical = managed.Priority != null && managed.Priority.Critical;
            managed.Distance = CalculateDistance(managed.Light, focusPosition);
            managed.InView = camera != null && IsRelevantToCamera(managed.Light, camera);
            managed.Score = CalculateScore(managed);

            if (ShouldEnableCandidate(managed))
            {
                lightCandidates.Add(managed);
            }
            else
            {
                SetManagedLightEnabled(managed, false);
            }
        }

        lightCandidates.Sort((left, right) => left.Score.CompareTo(right.Score));

        int enabledLights = 0;
        for (int i = 0; i < lightCandidates.Count; i++)
        {
            ManagedLight managed = lightCandidates[i];
            bool enabled = managed.Critical || enabledLights < maxRealtimeLights;
            SetManagedLightEnabled(managed, enabled);
            if (!enabled)
            {
                continue;
            }

            enabledLights++;
            if (CanUseShadowBudget(managed))
            {
                shadowCandidates.Add(managed);
            }
            else
            {
                SetManagedLightShadows(managed, false);
            }
        }

        shadowCandidates.Sort((left, right) => left.Score.CompareTo(right.Score));

        int shadowedLights = 0;
        for (int i = 0; i < shadowCandidates.Count; i++)
        {
            ManagedLight managed = shadowCandidates[i];
            bool allowShadows = managed.Critical || shadowedLights < maxShadowedLights;
            SetManagedLightShadows(managed, allowShadows);
            if (allowShadows)
            {
                shadowedLights++;
            }
        }

        lastEnabledLightCount = enabledLights;
        lastShadowedLightCount = shadowedLights;
        if (logBudget)
        {
            Debug.Log($"[LightRenderBudget] realtime={lastEnabledLightCount}, shadowed={lastShadowedLightCount}, managed={managedLights.Count}.", this);
        }
    }

    private bool ShouldEnableCandidate(ManagedLight managed)
    {
        if (managed == null || managed.Light == null)
        {
            return false;
        }

        if (managed.Critical)
        {
            return true;
        }

        float distanceLimit = managed.InView
            ? lightCullDistance * Mathf.Max(1f, inViewDistanceMultiplier)
            : lightCullDistance;
        return managed.Distance <= distanceLimit;
    }

    private bool CanUseShadowBudget(ManagedLight managed)
    {
        if (managed == null || managed.Light == null || managed.OriginalShadows == LightShadows.None)
        {
            return false;
        }

        return managed.Critical || managed.Distance <= shadowCullDistance;
    }

    private float CalculateScore(ManagedLight managed)
    {
        if (managed == null)
        {
            return float.MaxValue;
        }

        int priority = managed.Priority != null ? managed.Priority.Priority : 0;
        float score = managed.Distance;
        score -= priority * 2f;
        score += managed.InView ? -4f : 4f;
        score += managed.OriginalShadows != LightShadows.None ? -1f : 0f;
        if (managed.Critical)
        {
            score -= 10000f;
        }

        return score;
    }

    private float CalculateDistance(Light targetLight, Vector3 focusPosition)
    {
        if (targetLight == null || targetLight.type == LightType.Directional)
        {
            return 0f;
        }

        return Vector3.Distance(focusPosition, targetLight.transform.position);
    }

    private bool IsRelevantToCamera(Light targetLight, Camera camera)
    {
        if (targetLight == null || camera == null)
        {
            return false;
        }

        if (targetLight.type == LightType.Directional)
        {
            return true;
        }

        float radius = Mathf.Max(0.25f, targetLight.range);
        Bounds bounds = new Bounds(targetLight.transform.position, Vector3.one * radius * 2f);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
    }

    private void SetManagedLightEnabled(ManagedLight managed, bool enabled)
    {
        if (managed == null || managed.Light == null)
        {
            return;
        }

        managed.Light.enabled = enabled;
        if (!enabled)
        {
            SetManagedLightShadows(managed, false);
        }
    }

    private void SetManagedLightShadows(ManagedLight managed, bool enabled)
    {
        if (managed == null || managed.Light == null)
        {
            return;
        }

        managed.Light.shadows = enabled ? managed.OriginalShadows : LightShadows.None;
        if (managed.HdLight != null)
        {
            managed.HdLight.SetShadowResolution(hdrpShadowResolutionCap);
            managed.HdLight.useContactShadow.useOverride = true;
            managed.HdLight.useContactShadow.@override = enabled && enableContactShadowsForBudgetedLights;
        }
    }

    private void RestoreAllLights()
    {
        for (int i = 0; i < managedLights.Count; i++)
        {
            ManagedLight managed = managedLights[i];
            if (managed == null || managed.Light == null || !managed.Captured)
            {
                continue;
            }

            managed.Light.enabled = managed.OriginalEnabled;
            managed.Light.shadows = managed.OriginalShadows;
            managed.Light.renderMode = managed.OriginalRenderMode;
            if (managed.HdLight != null)
            {
                managed.HdLight.useContactShadow.useOverride = managed.OriginalContactShadowUseOverride;
                managed.HdLight.useContactShadow.@override = managed.OriginalContactShadowOverride;
            }
        }
    }

    private void CaptureInitialState(ManagedLight managed)
    {
        if (managed == null || managed.Light == null || managed.Captured)
        {
            return;
        }

        managed.OriginalEnabled = managed.Light.enabled;
        managed.OriginalShadows = managed.Light.shadows;
        managed.OriginalRenderMode = managed.Light.renderMode;
        if (managed.HdLight != null)
        {
            managed.OriginalContactShadowUseOverride = managed.HdLight.useContactShadow.useOverride;
            managed.OriginalContactShadowOverride = managed.HdLight.useContactShadow.@override;
        }

        managed.Captured = true;
    }

    private void PruneInvalidLights()
    {
        for (int i = managedLights.Count - 1; i >= 0; i--)
        {
            if (managedLights[i] == null || managedLights[i].Light == null)
            {
                managedLights.RemoveAt(i);
            }
        }
    }

    private bool ShouldManageLight(Light targetLight)
    {
        if (targetLight == null)
        {
            return false;
        }

        if ((managedLightLayers.value & (1 << targetLight.gameObject.layer)) == 0)
        {
            return false;
        }

        if (skipBakedLights && targetLight.lightmapBakeType == LightmapBakeType.Baked)
        {
            return false;
        }

        switch (targetLight.type)
        {
            case LightType.Point:
                return includePointLights;
            case LightType.Spot:
                return includeSpotLights;
            case LightType.Directional:
                return includeDirectionalLights;
            case LightType.Rectangle:
            case LightType.Disc:
                return includeAreaLights;
            default:
                return false;
        }
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

    private Transform ResolveFocusTarget()
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

    private void ValidateFields()
    {
        evaluationInterval = Mathf.Max(0.02f, evaluationInterval);
        maxRealtimeLights = Mathf.Max(0, maxRealtimeLights);
        maxShadowedLights = Mathf.Max(0, maxShadowedLights);
        lightCullDistance = Mathf.Max(0f, lightCullDistance);
        shadowCullDistance = Mathf.Max(0f, shadowCullDistance);
        inViewDistanceMultiplier = Mathf.Max(0f, inViewDistanceMultiplier);
        hdrpShadowResolutionCap = Mathf.Max(128, hdrpShadowResolutionCap);
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
