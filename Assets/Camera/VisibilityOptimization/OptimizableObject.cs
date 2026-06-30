using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class OptimizableObject : MonoBehaviour
{
    private struct ParticleState
    {
        public bool WasPlaying;
        public bool WasPaused;
        public bool EmissionEnabled;
    }

    private struct BehaviourState
    {
        public bool WasEnabled;
        public bool CanRestore;
    }

    [Header("Runtime")]
    [SerializeField] private bool optimizationEnabled = true;
    [SerializeField] private bool neverCull;
    [SerializeField, Tooltip("Garde les renderers visibles quand le fondu camera/XRay les protege.")]
    private bool preserveForCameraFade;

    [Header("Bounds")]
    [SerializeField] private Transform boundsRoot;
    [SerializeField] private bool autoCollectTargets = true;
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField, Min(0f)] private float boundsPadding = 1.5f;
    [SerializeField, Min(0.05f)] private float minimumBoundsRadius = 0.5f;
    [SerializeField, Min(0f), Tooltip("Distance joueur local sous laquelle l'objet reste actif meme hors frustum.")]
    private float localPlayerKeepVisibleDistance = 2f;

    [Header("Presentation Targets")]
    [SerializeField] private bool controlRenderers = true;
    [SerializeField] private bool controlSkinnedMeshRenderers = true;
    [SerializeField] private bool controlParticleSystems = true;
    [SerializeField, FormerlySerializedAs("controlExplicitPausables"), Tooltip("Opt-in uniquement: ne jamais ajouter de composant reseau ou de logique autoritaire.")]
    private bool controlExplicitBehaviours;
    [SerializeField] private VisibilityParticleOffscreenAction particleOffscreenAction = VisibilityParticleOffscreenAction.PauseAndResume;
    [SerializeField] private Renderer[] targetRenderers = Array.Empty<Renderer>();
    [SerializeField] private ParticleSystem[] targetParticleSystems = Array.Empty<ParticleSystem>();
    [SerializeField, FormerlySerializedAs("explicitPausables")]
    private Behaviour[] explicitBehaviours = Array.Empty<Behaviour>();

    [Header("Debug")]
    [SerializeField] private bool drawGizmos;
    [SerializeField] private bool logStateChanges;

    [SerializeField, HideInInspector] private VisibilityOptimizationCategory category = VisibilityOptimizationCategory.Decoration;
#pragma warning disable 0414
    [SerializeField, HideInInspector] private bool controlLights;
    [SerializeField, HideInInspector] private Light[] targetLights = Array.Empty<Light>();
    [SerializeField, HideInInspector] private float visibleDistanceOverride = -1f;
    [SerializeField, HideInInspector] private float lightDistanceOverride = -1f;
    [SerializeField, HideInInspector] private float pauseDistanceOverride = -1f;
    [SerializeField, HideInInspector] private bool reduceUpdateRateWhenDistant = true;
    [SerializeField, HideInInspector] private float distantUpdateInterval = 0.35f;
    [SerializeField, HideInInspector] private float pausedUpdateInterval = 1f;
#pragma warning restore 0414
    [SerializeField, HideInInspector] private float distanceMultiplier = 1f;

    private bool[] rendererEnabledStates = Array.Empty<bool>();
    private ParticleState[] particleStates = Array.Empty<ParticleState>();
    private BehaviourState[] behaviourStates = Array.Empty<BehaviourState>();
    private bool capturedRendererState;
    private bool capturedParticleState;
    private bool capturedBehaviourState;
    private Bounds cachedBounds;
    private bool boundsDirty = true;
    private VisibilityOptimizationState currentState = VisibilityOptimizationState.Visible;

    public VisibilityOptimizationCategory Category => category;
    public bool OptimizationEnabled => optimizationEnabled;
    public bool NeverCull => neverCull || category == VisibilityOptimizationCategory.Critical;
    public bool PreserveForCameraFade => preserveForCameraFade;
    public float LocalPlayerKeepVisibleDistance => localPlayerKeepVisibleDistance;
    public VisibilityOptimizationState CurrentState => currentState;

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
    }

    private void Awake()
    {
        if (autoCollectTargets)
        {
            RefreshCachedTargets();
        }
        else
        {
            explicitBehaviours = FilterExplicitBehaviours(explicitBehaviours);
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
        boundsPadding = Mathf.Max(0f, boundsPadding);
        minimumBoundsRadius = Mathf.Max(0.05f, minimumBoundsRadius);
        localPlayerKeepVisibleDistance = Mathf.Max(0f, localPlayerKeepVisibleDistance);
        distanceMultiplier = Mathf.Max(0.1f, distanceMultiplier);
        if (!Application.isPlaying && autoCollectTargets)
        {
            RefreshCachedTargets();
        }
        else
        {
            explicitBehaviours = FilterExplicitBehaviours(explicitBehaviours);
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
            targetParticleSystems = Array.Empty<ParticleSystem>();
            explicitBehaviours = Array.Empty<Behaviour>();
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

        targetParticleSystems = controlParticleSystems
            ? FilterOwnedTargets(root.GetComponentsInChildren<ParticleSystem>(includeInactiveChildren))
            : Array.Empty<ParticleSystem>();

        explicitBehaviours = FilterExplicitBehaviours(explicitBehaviours);
        capturedRendererState = false;
        capturedParticleState = false;
        capturedBehaviourState = false;
        boundsDirty = true;
        RecalculateBounds();
    }

    public bool IsNearLocalPlayer(Transform localPlayer)
    {
        if (localPlayer == null || localPlayerKeepVisibleDistance <= 0f)
        {
            return false;
        }

        Bounds bounds = CurrentBounds;
        return bounds.SqrDistance(localPlayer.position) <= localPlayerKeepVisibleDistance * localPlayerKeepVisibleDistance;
    }

    public void ApplyVisibility(bool visible, string reason)
    {
        if (!optimizationEnabled || NeverCull)
        {
            RestorePresentation();
            SetCurrentState(NeverCull ? VisibilityOptimizationState.Excluded : VisibilityOptimizationState.Visible, reason);
            return;
        }

        bool protectedForCameraFade = HasCameraProtectedRenderer();
        bool presentationVisible = visible || protectedForCameraFade;

        ApplyRendererVisibility(presentationVisible);
        ApplyParticleVisibility(presentationVisible);
        ApplyBehaviourVisibility(visible);

        VisibilityOptimizationState nextState = ResolveState(presentationVisible);
        SetCurrentState(nextState, protectedForCameraFade && !visible ? "camera_fade_protected" : reason);
    }

    public void RestoreAll()
    {
        RestorePresentation();
        SetCurrentState(VisibilityOptimizationState.Visible, "restore");
    }

    private void RestorePresentation()
    {
        ApplyRendererVisibility(true, forceRestore: true);
        ApplyParticleVisibility(true, forceRestore: true);
        ApplyBehaviourVisibility(true, forceRestore: true);
    }

    private VisibilityOptimizationState ResolveState(bool presentationVisible)
    {
        if (presentationVisible)
        {
            return VisibilityOptimizationState.Visible;
        }

        if (HasAnyReference(targetParticleSystems) ||
            controlExplicitBehaviours && HasAnyReference(explicitBehaviours))
        {
            return VisibilityOptimizationState.Paused;
        }

        return VisibilityOptimizationState.RendererCulled;
    }

    private void SetCurrentState(VisibilityOptimizationState nextState, string reason)
    {
        if (currentState != nextState && logStateChanges)
        {
            Debug.Log($"[VisibilityOptimization] {name}: {currentState} -> {nextState} reason='{reason}'", this);
        }

        currentState = nextState;
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
                if (capturedRendererState && rendererEnabledStates.Length > i)
                {
                    target.enabled = rendererEnabledStates[i];
                }
            }
            else
            {
                target.enabled = false;
            }
        }

        if (visible && capturedRendererState)
        {
            capturedRendererState = false;
        }
    }

    private void ApplyParticleVisibility(bool visible, bool forceRestore = false)
    {
        if (!controlParticleSystems || targetParticleSystems == null)
        {
            return;
        }

        if (!visible)
        {
            CaptureParticleStates();
        }

        for (int i = 0; i < targetParticleSystems.Length; i++)
        {
            ParticleSystem target = targetParticleSystems[i];
            if (target == null)
            {
                continue;
            }

            if (visible)
            {
                if (capturedParticleState && particleStates.Length > i)
                {
                    RestoreParticle(target, particleStates[i]);
                }
            }
            else
            {
                SuspendParticle(target);
            }
        }

        if (visible && capturedParticleState)
        {
            capturedParticleState = false;
        }
    }

    private void ApplyBehaviourVisibility(bool visible, bool forceRestore = false)
    {
        if (!controlExplicitBehaviours || explicitBehaviours == null)
        {
            return;
        }

        if (!visible)
        {
            CaptureBehaviourStates();
        }

        for (int i = 0; i < explicitBehaviours.Length; i++)
        {
            Behaviour behaviour = explicitBehaviours[i];
            if (behaviour == null || !IsBehaviourSafeToDisable(behaviour))
            {
                continue;
            }

            if (visible)
            {
                if (capturedBehaviourState &&
                    behaviourStates.Length > i &&
                    behaviourStates[i].CanRestore)
                {
                    behaviour.enabled = behaviourStates[i].WasEnabled;
                }
            }
            else
            {
                behaviour.enabled = false;
            }
        }

        if (visible && capturedBehaviourState)
        {
            capturedBehaviourState = false;
        }
    }

    private void SuspendParticle(ParticleSystem target)
    {
        switch (particleOffscreenAction)
        {
            case VisibilityParticleOffscreenAction.StopEmitting:
                target.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                break;
            case VisibilityParticleOffscreenAction.DisableEmission:
                ParticleSystem.EmissionModule emission = target.emission;
                emission.enabled = false;
                break;
            default:
                if (target.isPlaying)
                {
                    target.Pause(true);
                }
                break;
        }
    }

    private void RestoreParticle(ParticleSystem target, ParticleState state)
    {
        if (particleOffscreenAction == VisibilityParticleOffscreenAction.DisableEmission)
        {
            ParticleSystem.EmissionModule emission = target.emission;
            emission.enabled = state.EmissionEnabled;
        }

        if (state.WasPlaying)
        {
            target.Play(true);
        }
        else if (state.WasPaused)
        {
            target.Pause(true);
        }
        else if (particleOffscreenAction == VisibilityParticleOffscreenAction.StopEmitting)
        {
            target.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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

    private void CaptureParticleStates()
    {
        if (capturedParticleState)
        {
            return;
        }

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

        capturedParticleState = true;
    }

    private void CaptureBehaviourStates()
    {
        if (capturedBehaviourState)
        {
            return;
        }

        EnsureStateArray(ref behaviourStates, explicitBehaviours != null ? explicitBehaviours.Length : 0);
        for (int i = 0; explicitBehaviours != null && i < explicitBehaviours.Length; i++)
        {
            Behaviour behaviour = explicitBehaviours[i];
            bool canRestore = behaviour != null && IsBehaviourSafeToDisable(behaviour);
            behaviourStates[i] = new BehaviourState
            {
                WasEnabled = canRestore && behaviour.enabled,
                CanRestore = canRestore
            };
        }

        capturedBehaviourState = true;
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
            for (int i = 0; targetParticleSystems != null && i < targetParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = targetParticleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                Bounds particleBounds = new Bounds(particleSystem.transform.position, Vector3.one * minimumBoundsRadius);
                if (!hasBounds)
                {
                    merged = particleBounds;
                    hasBounds = true;
                }
                else
                {
                    merged.Encapsulate(particleBounds);
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

    private Behaviour[] FilterExplicitBehaviours(Behaviour[] behaviours)
    {
        if (behaviours == null || behaviours.Length == 0)
        {
            return Array.Empty<Behaviour>();
        }

        List<Behaviour> filtered = new List<Behaviour>(behaviours.Length);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || !IsOwnedTarget(behaviour) || !IsBehaviourSafeToDisable(behaviour))
            {
                continue;
            }

            filtered.Add(behaviour);
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
        return HasAnyReference(targetRenderers) ||
               HasAnyReference(targetParticleSystems) ||
               HasAnyReference(explicitBehaviours);
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

    private static bool IsBehaviourSafeToDisable(Behaviour behaviour)
    {
        if (behaviour == null ||
            behaviour is OptimizableObject ||
            behaviour is VisibilityOptimizationManager ||
            behaviour is Camera ||
            behaviour is AudioListener ||
            behaviour is Animator ||
            behaviour is Light ||
            behaviour is NetworkBehaviour ||
            behaviour is NetworkObject)
        {
            return false;
        }

        string namespaceName = behaviour.GetType().Namespace ?? string.Empty;
        return !namespaceName.StartsWith("Unity.Netcode", StringComparison.Ordinal);
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

    private static void EnsureStateArray(ref BehaviourState[] states, int requiredLength)
    {
        if (states == null || states.Length != requiredLength)
        {
            states = new BehaviourState[requiredLength];
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Bounds bounds = CurrentBounds;
        Gizmos.color = currentState == VisibilityOptimizationState.Visible ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(bounds.center, Mathf.Max(minimumBoundsRadius, bounds.extents.magnitude));
    }
}
