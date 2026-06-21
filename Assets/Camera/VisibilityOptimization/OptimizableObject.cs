using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class OptimizableObject : MonoBehaviour
{
    [Header("Classification")]
    [SerializeField] private VisibilityOptimizationCategory category = VisibilityOptimizationCategory.Decoration;
    [SerializeField] private bool optimizationEnabled = true;
    [SerializeField] private bool neverCull;
    [SerializeField] private bool preserveForCameraFade;

    [Header("Targets")]
    [SerializeField] private Transform boundsRoot;
    [SerializeField] private bool autoCollectTargets = true;
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private bool controlRenderers = true;
    [SerializeField] private bool controlSkinnedMeshRenderers = true;
    [SerializeField] private bool controlLights = true;
    [SerializeField, Tooltip("Ne pilote que les comportements qui implementent IPausableWhenInvisible ou IVisibilityUpdateRateTarget.")]
    private bool controlExplicitPausables;
    [SerializeField] private bool reduceUpdateRateWhenDistant = true;
    [SerializeField, Min(0f)] private float distantUpdateInterval = 0.35f;
    [SerializeField, Min(0f)] private float pausedUpdateInterval = 1f;
    [SerializeField] private Renderer[] targetRenderers = Array.Empty<Renderer>();
    [SerializeField] private Light[] targetLights = Array.Empty<Light>();
    [SerializeField] private MonoBehaviour[] explicitPausables = Array.Empty<MonoBehaviour>();

    [Header("Distances")]
    [SerializeField, Tooltip("Valeur < 0 = profil de categorie du manager.")]
    private float visibleDistanceOverride = -1f;
    [SerializeField, Tooltip("Valeur < 0 = profil de categorie du manager.")]
    private float lightDistanceOverride = -1f;
    [SerializeField, Tooltip("Valeur < 0 = profil de categorie du manager.")]
    private float pauseDistanceOverride = -1f;
    [SerializeField, Min(0.1f)] private float distanceMultiplier = 1f;
    [SerializeField, Min(0f)] private float boundsPadding = 1.5f;
    [SerializeField, Min(0.05f)] private float minimumBoundsRadius = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos;
    [SerializeField] private bool logStateChanges;

    private bool[] rendererEnabledStates = Array.Empty<bool>();
    private bool[] lightEnabledStates = Array.Empty<bool>();
    private bool capturedRendererState;
    private bool capturedLightState;
    private Bounds cachedBounds;
    private bool boundsDirty = true;
    private VisibilityOptimizationState currentState = VisibilityOptimizationState.Visible;
    private bool currentLightsVisible = true;
    private bool currentPaused;

    public VisibilityOptimizationCategory Category => category;
    public bool OptimizationEnabled => optimizationEnabled;
    public bool NeverCull => neverCull || category == VisibilityOptimizationCategory.Critical;
    public bool PreserveForCameraFade => preserveForCameraFade;
    public float DistanceMultiplier => distanceMultiplier;
    public float VisibleDistanceOverride => visibleDistanceOverride;
    public float LightDistanceOverride => lightDistanceOverride;
    public float PauseDistanceOverride => pauseDistanceOverride;
    public VisibilityOptimizationState CurrentState => currentState;
    public bool CurrentLightsVisible => currentLightsVisible;
    public bool CurrentPaused => currentPaused;

    public Bounds CurrentBounds
    {
        get
        {
            if (boundsDirty)
            {
                RecalculateBounds();
            }

            return cachedBounds;
        }
    }

    public BoundingSphere CurrentBoundingSphere
    {
        get
        {
            Bounds bounds = CurrentBounds;
            return new BoundingSphere(bounds.center, Mathf.Max(minimumBoundsRadius, bounds.extents.magnitude));
        }
    }

    private Transform BoundsRoot => boundsRoot != null ? boundsRoot : transform;

    private void Reset()
    {
        boundsRoot = transform;
        RefreshCachedTargets();
        InferCategoryFromContents();
    }

    private void Awake()
    {
        if (autoCollectTargets && !HasAnyCachedTargets())
        {
            RefreshCachedTargets();
        }
        else
        {
            RecalculateBounds();
        }
    }

    private void OnEnable()
    {
        boundsDirty = true;
        if (Application.isPlaying)
        {
            VisibilityOptimizationManager.Register(this);
        }
    }

    private void OnDisable()
    {
        RestoreAll();
        if (Application.isPlaying)
        {
            VisibilityOptimizationManager.Unregister(this);
        }
    }

    private void OnDestroy()
    {
        RestoreAll();
        if (Application.isPlaying)
        {
            VisibilityOptimizationManager.Unregister(this);
        }
    }

    private void OnValidate()
    {
        distanceMultiplier = Mathf.Max(0.1f, distanceMultiplier);
        boundsPadding = Mathf.Max(0f, boundsPadding);
        minimumBoundsRadius = Mathf.Max(0.05f, minimumBoundsRadius);
        if (!Application.isPlaying && autoCollectTargets)
        {
            RefreshCachedTargets();
        }
        else
        {
            boundsDirty = true;
        }
    }

    private void OnTransformChildrenChanged()
    {
        boundsDirty = true;
        if (!Application.isPlaying && autoCollectTargets)
        {
            RefreshCachedTargets();
        }
    }

    public void RefreshCachedTargets()
    {
        Transform root = BoundsRoot;
        if (root == null)
        {
            targetRenderers = Array.Empty<Renderer>();
            targetLights = Array.Empty<Light>();
            explicitPausables = Array.Empty<MonoBehaviour>();
            boundsDirty = true;
            return;
        }

        targetRenderers = controlRenderers
            ? FilterOwnedTargets(root.GetComponentsInChildren<Renderer>(includeInactiveChildren))
            : Array.Empty<Renderer>();
        if (!controlSkinnedMeshRenderers && targetRenderers.Length > 0)
        {
            targetRenderers = Array.FindAll(targetRenderers, renderer => renderer != null && !(renderer is SkinnedMeshRenderer));
        }

        targetLights = controlLights
            ? FilterOwnedTargets(root.GetComponentsInChildren<Light>(includeInactiveChildren))
            : Array.Empty<Light>();

        if (controlExplicitPausables)
        {
            explicitPausables = FilterPausables(root.GetComponentsInChildren<MonoBehaviour>(includeInactiveChildren));
        }

        capturedRendererState = false;
        capturedLightState = false;
        boundsDirty = true;
        RecalculateBounds();
    }

    public void ApplyEvaluation(
        VisibilityOptimizationState requestedState,
        bool lightsVisible,
        bool pause,
        VisibilityPauseContext context)
    {
        if (!optimizationEnabled || NeverCull)
        {
            RestoreAll();
            currentState = NeverCull ? VisibilityOptimizationState.Excluded : VisibilityOptimizationState.Visible;
            return;
        }

        bool renderersVisible = requestedState == VisibilityOptimizationState.Visible ||
                                requestedState == VisibilityOptimizationState.LightCulled ||
                                HasCameraProtectedRenderer();

        ApplyRendererVisibility(renderersVisible);
        ApplyLightVisibility(lightsVisible);
        ApplyPause(pause, context);

        VisibilityOptimizationState nextState = renderersVisible
            ? lightsVisible ? VisibilityOptimizationState.Visible : VisibilityOptimizationState.LightCulled
            : pause ? VisibilityOptimizationState.Paused : VisibilityOptimizationState.RendererCulled;

        if (currentState != nextState && logStateChanges)
        {
            Debug.Log($"[VisibilityOptimization] {name}: {currentState} -> {nextState} reason='{context.Reason}'", this);
        }

        currentState = nextState;
        currentLightsVisible = lightsVisible;
        currentPaused = pause;
    }

    public void RestoreAll()
    {
        ApplyRendererVisibility(true, forceRestore: true);
        ApplyLightVisibility(true, forceRestore: true);
        ApplyPause(false, new VisibilityPauseContext(
            VisibilityOptimizationState.Visible,
            category,
            0f,
            0f,
            true,
            "restore"));

        currentState = VisibilityOptimizationState.Visible;
        currentLightsVisible = true;
        currentPaused = false;
    }

    private void ApplyRendererVisibility(bool visible, bool forceRestore = false)
    {
        if (!controlRenderers || targetRenderers == null)
        {
            return;
        }

        if (!visible)
        {
            CaptureRendererStates();
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer target = targetRenderers[i];
            if (target == null)
            {
                continue;
            }

            if (!visible && IsRendererProtected(target))
            {
                continue;
            }

            if (visible)
            {
                if (forceRestore || capturedRendererState)
                {
                    target.enabled = rendererEnabledStates.Length > i && rendererEnabledStates[i];
                }
            }
            else
            {
                target.enabled = false;
            }
        }

        if (visible)
        {
            capturedRendererState = false;
        }
    }

    private void ApplyLightVisibility(bool visible, bool forceRestore = false)
    {
        if (!controlLights || targetLights == null)
        {
            return;
        }

        if (!visible)
        {
            CaptureLightStates();
        }

        for (int i = 0; i < targetLights.Length; i++)
        {
            Light target = targetLights[i];
            if (target == null)
            {
                continue;
            }

            if (visible)
            {
                if (forceRestore || capturedLightState)
                {
                    target.enabled = lightEnabledStates.Length > i && lightEnabledStates[i];
                }
            }
            else
            {
                target.enabled = false;
            }
        }

        if (visible)
        {
            capturedLightState = false;
        }
    }

    private void ApplyPause(bool pause, VisibilityPauseContext context)
    {
        if (!controlExplicitPausables || explicitPausables == null)
        {
            return;
        }

        bool shouldReduceUpdateRate = reduceUpdateRateWhenDistant &&
                                      (pause || context.State != VisibilityOptimizationState.Visible);
        float updateInterval = 0f;
        if (shouldReduceUpdateRate)
        {
            updateInterval = pause ? pausedUpdateInterval : distantUpdateInterval;
        }

        for (int i = 0; i < explicitPausables.Length; i++)
        {
            MonoBehaviour behaviour = explicitPausables[i];
            if (behaviour is IPausableWhenInvisible pausable)
            {
                pausable.SetPausedWhenInvisible(pause, context);
            }

            if (behaviour is IVisibilityUpdateRateTarget updateRateTarget)
            {
                updateRateTarget.SetVisibilityUpdateInterval(updateInterval, context);
            }
        }
    }

    private void CaptureRendererStates()
    {
        if (capturedRendererState)
        {
            return;
        }

        EnsureStateArray(ref rendererEnabledStates, targetRenderers != null ? targetRenderers.Length : 0);
        for (int i = 0; targetRenderers != null && i < targetRenderers.Length; i++)
        {
            Renderer target = targetRenderers[i];
            rendererEnabledStates[i] = target != null && target.enabled;
        }

        capturedRendererState = true;
    }

    private void CaptureLightStates()
    {
        if (capturedLightState)
        {
            return;
        }

        EnsureStateArray(ref lightEnabledStates, targetLights != null ? targetLights.Length : 0);
        for (int i = 0; targetLights != null && i < targetLights.Length; i++)
        {
            Light target = targetLights[i];
            lightEnabledStates[i] = target != null && target.enabled;
        }

        capturedLightState = true;
    }

    private bool HasCameraProtectedRenderer()
    {
        if (targetRenderers == null)
        {
            return false;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (IsRendererProtected(targetRenderers[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsRendererProtected(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        if (preserveForCameraFade || CameraVisibilityProtection.IsRendererProtected(renderer))
        {
            return true;
        }

        CameraVisibilityObstacle obstacle = renderer.GetComponentInParent<CameraVisibilityObstacle>();
        return obstacle != null && obstacle.PreserveForCameraFade;
    }

    private void RecalculateBounds()
    {
        Transform root = BoundsRoot;
        if (root == null)
        {
            cachedBounds = new Bounds(transform.position, Vector3.one * minimumBoundsRadius);
            boundsDirty = false;
            return;
        }

        bool hasBounds = false;
        Bounds merged = new Bounds(root.position, Vector3.one * minimumBoundsRadius);
        Renderer[] renderers = targetRenderers != null && targetRenderers.Length > 0
            ? targetRenderers
            : root.GetComponentsInChildren<Renderer>(includeInactiveChildren);
        for (int i = 0; renderers != null && i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                merged = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                merged.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Light[] lights = targetLights != null && targetLights.Length > 0
                ? targetLights
                : root.GetComponentsInChildren<Light>(includeInactiveChildren);
            for (int i = 0; lights != null && i < lights.Length; i++)
            {
                Light targetLight = lights[i];
                if (targetLight == null)
                {
                    continue;
                }

                Bounds lightBounds = new Bounds(targetLight.transform.position, Vector3.one * Mathf.Max(0.5f, targetLight.range));
                if (!hasBounds)
                {
                    merged = lightBounds;
                    hasBounds = true;
                }
                else
                {
                    merged.Encapsulate(lightBounds);
                }
            }
        }

        if (!hasBounds)
        {
            merged = new Bounds(root.position, Vector3.one * minimumBoundsRadius);
        }

        if (boundsPadding > 0f)
        {
            merged.Expand(boundsPadding * 2f);
        }

        if (merged.extents.magnitude < minimumBoundsRadius)
        {
            merged.Expand((minimumBoundsRadius - merged.extents.magnitude) * 2f);
        }

        cachedBounds = merged;
        boundsDirty = false;
    }

    private T[] FilterOwnedTargets<T>(T[] targets) where T : Component
    {
        if (targets == null || targets.Length == 0)
        {
            return Array.Empty<T>();
        }

        List<T> filtered = new List<T>(targets.Length);
        for (int i = 0; i < targets.Length; i++)
        {
            T target = targets[i];
            if (IsOwnedTarget(target))
            {
                filtered.Add(target);
            }
        }

        return filtered.ToArray();
    }

    private MonoBehaviour[] FilterPausables(MonoBehaviour[] behaviours)
    {
        if (behaviours == null || behaviours.Length == 0)
        {
            return Array.Empty<MonoBehaviour>();
        }

        List<MonoBehaviour> filtered = new List<MonoBehaviour>(behaviours.Length);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this || !IsOwnedTarget(behaviour))
            {
                continue;
            }

            if (behaviour is IPausableWhenInvisible || behaviour is IVisibilityUpdateRateTarget)
            {
                filtered.Add(behaviour);
            }
        }

        return filtered.ToArray();
    }

    private bool IsOwnedTarget(Component target)
    {
        if (target == null)
        {
            return false;
        }

        Transform current = target.transform;
        while (current != null && current != transform)
        {
            OptimizableObject owner = current.GetComponent<OptimizableObject>();
            if (owner != null)
            {
                return owner == this;
            }

            current = current.parent;
        }

        return true;
    }

    private bool HasAnyCachedTargets()
    {
        return HasAnyReference(targetRenderers) || HasAnyReference(targetLights) || HasAnyReference(explicitPausables);
    }

    private static bool HasAnyReference<T>(T[] targets) where T : UnityEngine.Object
    {
        if (targets == null)
        {
            return false;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureStateArray(ref bool[] states, int requiredLength)
    {
        if (states == null || states.Length != requiredLength)
        {
            states = new bool[requiredLength];
        }
    }

    private void InferCategoryFromContents()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICharacterDetectedInteractable)
            {
                category = VisibilityOptimizationCategory.Interactive;
                return;
            }
        }

        if (GetComponentInChildren<GhostController>(true) != null)
        {
            category = VisibilityOptimizationCategory.NPC;
            return;
        }

        if (GetComponentInChildren<Light>(true) != null && GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            category = VisibilityOptimizationCategory.Light;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Bounds bounds = CurrentBounds;
        switch (currentState)
        {
            case VisibilityOptimizationState.Visible:
                Gizmos.color = Color.green;
                break;
            case VisibilityOptimizationState.LightCulled:
                Gizmos.color = Color.yellow;
                break;
            case VisibilityOptimizationState.RendererCulled:
                Gizmos.color = Color.gray;
                break;
            case VisibilityOptimizationState.Paused:
                Gizmos.color = Color.cyan;
                break;
            case VisibilityOptimizationState.Excluded:
                Gizmos.color = Color.magenta;
                break;
            default:
                Gizmos.color = Color.white;
                break;
        }

        Gizmos.DrawWireSphere(bounds.center, Mathf.Max(minimumBoundsRadius, bounds.extents.magnitude));
    }
}
