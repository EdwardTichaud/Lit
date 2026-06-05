using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
public sealed class DecorCullable : MonoBehaviour
{
    [Header("Runtime")]
    [SerializeField, Tooltip("Ancien culling decoratif. Laisse desactive pour eviter que des renderers disparaissent/reapparaissent quand la camera bouge.")]
    private bool enableRuntimeCulling = false;

    [Header("Bounds")]
    [SerializeField] private Transform boundsRoot;
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField, Min(0f)] private float boundsPadding = 2f;
    [SerializeField, Min(0.05f)] private float minimumBoundsRadius = 0.5f;

    [Header("Culling Targets")]
    [SerializeField] private bool autoCollectTargets = true;
    [SerializeField] private bool disableRenderers = true;
    [SerializeField] private bool disableLights = true;
    [SerializeField] private bool disableLightShadows = false;
    [SerializeField] private bool disableHdrpContactShadows = false;
    [SerializeField] private bool pauseParticles = true;
    [SerializeField] private bool disableCollidersWhenCulled = false;
    [SerializeField] private Renderer[] targetRenderers = Array.Empty<Renderer>();
    [SerializeField] private Light[] targetLights = Array.Empty<Light>();
    [SerializeField] private ParticleSystem[] targetParticles = Array.Empty<ParticleSystem>();
    [SerializeField] private Collider[] targetColliders = Array.Empty<Collider>();

    private bool[] rendererEnabledStates = Array.Empty<bool>();
    private bool[] lightEnabledStates = Array.Empty<bool>();
    private bool[] particlePlayingStates = Array.Empty<bool>();
    private bool[] colliderEnabledStates = Array.Empty<bool>();
    private BoundingSphere boundingSphere;
    private bool boundsDirty = true;
    private bool isCulled;
    private bool isLightDistanceCulled;
    private bool isRegistered;

    public bool IsCulled => isCulled;
    public bool RuntimeCullingEnabled => enableRuntimeCulling;

    internal BoundingSphere CurrentBoundingSphere
    {
        get
        {
            if (boundsDirty)
            {
                RecalculateBounds();
            }

            return boundingSphere;
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
        if (autoCollectTargets && ShouldRefreshTargetsOnAwake())
        {
            RefreshCachedTargets();
        }
        else
        {
            ApplyLightPerformanceSettings();
            RecalculateBounds();
        }
    }

    private bool ShouldRefreshTargetsOnAwake()
    {
        if (!Application.isPlaying)
        {
            return true;
        }

        return !HasAnyCachedTargetReference();
    }

    private bool HasAnyCachedTargetReference()
    {
        return HasAnyReference(targetRenderers) ||
               HasAnyReference(targetLights) ||
               HasAnyReference(targetParticles) ||
               HasAnyReference(targetColliders);
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

    private void OnEnable()
    {
        boundsDirty = true;

        if (!Application.isPlaying)
        {
            return;
        }

        if (!enableRuntimeCulling)
        {
            RestoreRuntimeVisibilityState();
            return;
        }

        DecorCullingManager.Register(this);
        isRegistered = true;
    }

    private void OnDisable()
    {
        RestoreRuntimeVisibilityState();

        if (isRegistered)
        {
            DecorCullingManager.Unregister(this);
            isRegistered = false;
        }
    }

    private void OnValidate()
    {
        boundsPadding = Mathf.Max(0f, boundsPadding);
        minimumBoundsRadius = Mathf.Max(0.05f, minimumBoundsRadius);

        if (!Application.isPlaying && autoCollectTargets)
        {
            RefreshCachedTargets();
        }
        else
        {
            ApplyLightPerformanceSettings();
            boundsDirty = true;
        }

        if (Application.isPlaying && !enableRuntimeCulling)
        {
            if (isRegistered)
            {
                DecorCullingManager.Unregister(this);
                isRegistered = false;
            }

            RestoreRuntimeVisibilityState();
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (!Application.isPlaying && autoCollectTargets)
        {
            RefreshCachedTargets();
        }
        else
        {
            boundsDirty = true;
        }
    }

    public void RefreshCachedTargets()
    {
        Transform root = BoundsRoot;
        if (root == null)
        {
            targetRenderers = Array.Empty<Renderer>();
            targetLights = Array.Empty<Light>();
            targetParticles = Array.Empty<ParticleSystem>();
            targetColliders = Array.Empty<Collider>();
            boundsDirty = true;
            return;
        }

        targetRenderers = disableRenderers
            ? FilterOwnedTargets(root.GetComponentsInChildren<Renderer>(includeInactiveChildren))
            : Array.Empty<Renderer>();
        targetLights = disableLights
            ? FilterOwnedTargets(root.GetComponentsInChildren<Light>(includeInactiveChildren))
            : Array.Empty<Light>();
        targetParticles = pauseParticles
            ? FilterOwnedTargets(root.GetComponentsInChildren<ParticleSystem>(includeInactiveChildren))
            : Array.Empty<ParticleSystem>();
        targetColliders = disableCollidersWhenCulled
            ? FilterOwnedTargets(root.GetComponentsInChildren<Collider>(includeInactiveChildren))
            : Array.Empty<Collider>();

        ApplyLightPerformanceSettings();
        boundsDirty = true;
        RecalculateBounds();
    }

    private T[] FilterOwnedTargets<T>(T[] targets) where T : Component
    {
        if (targets == null || targets.Length == 0)
        {
            return targets ?? Array.Empty<T>();
        }

        int writeIndex = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            T target = targets[i];
            if (IsOwnedTarget(target))
            {
                targets[writeIndex] = target;
                writeIndex++;
            }
        }

        if (writeIndex == targets.Length)
        {
            return targets;
        }

        T[] filteredTargets = new T[writeIndex];
        Array.Copy(targets, filteredTargets, writeIndex);
        return filteredTargets;
    }

    private bool IsOwnedTarget(Component target)
    {
        if (target == null)
        {
            return false;
        }

        Transform current = target.transform;
        while (current != null)
        {
            DecorCullable owner = current.GetComponent<DecorCullable>();
            if (owner != null)
            {
                return owner == this;
            }

            current = current.parent;
        }

        return true;
    }

    internal void SetCulled(bool culled)
    {
        if (!enableRuntimeCulling)
        {
            RestoreRuntimeVisibilityState();
            return;
        }

        if (isCulled == culled)
        {
            return;
        }

        if (culled)
        {
            CaptureTargetStates(captureLights: !isLightDistanceCulled);
            ApplyCulledState();
        }
        else
        {
            ApplyVisibleState();
        }

        isCulled = culled;
    }

    internal void SetLightDistanceCulled(bool culled)
    {
        if (!enableRuntimeCulling)
        {
            RestoreRuntimeVisibilityState();
            return;
        }

        if (!disableLights || targetLights == null || targetLights.Length == 0)
        {
            isLightDistanceCulled = false;
            return;
        }

        if (isLightDistanceCulled == culled)
        {
            return;
        }

        if (culled)
        {
            if (!isCulled)
            {
                CaptureLightStates();
                ApplyLightCulledState();
            }

            isLightDistanceCulled = true;
        }
        else
        {
            isLightDistanceCulled = false;
            if (!isCulled)
            {
                ApplyLightVisibleState();
            }
        }
    }

    private void RestoreRuntimeVisibilityState()
    {
        if (!isCulled && !isLightDistanceCulled)
        {
            return;
        }

        isLightDistanceCulled = false;
        ApplyVisibleState();
        isCulled = false;
    }

    private void CaptureTargetStates(bool captureLights = true)
    {
        EnsureStateArray(ref rendererEnabledStates, targetRenderers != null ? targetRenderers.Length : 0);
        EnsureStateArray(ref particlePlayingStates, targetParticles != null ? targetParticles.Length : 0);
        EnsureStateArray(ref colliderEnabledStates, targetColliders != null ? targetColliders.Length : 0);

        for (int i = 0; targetRenderers != null && i < targetRenderers.Length; i++)
        {
            Renderer target = targetRenderers[i];
            rendererEnabledStates[i] = target != null && target.enabled;
        }

        if (captureLights)
        {
            CaptureLightStates();
        }

        for (int i = 0; targetParticles != null && i < targetParticles.Length; i++)
        {
            ParticleSystem target = targetParticles[i];
            particlePlayingStates[i] = target != null && (target.isPlaying || target.isEmitting);
        }

        for (int i = 0; targetColliders != null && i < targetColliders.Length; i++)
        {
            Collider target = targetColliders[i];
            colliderEnabledStates[i] = target != null && target.enabled;
        }
    }

    private void ApplyCulledState()
    {
        if (disableRenderers && targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer target = targetRenderers[i];
                if (target != null)
                {
                    target.enabled = false;
                }
            }
        }

        if (disableLights && targetLights != null)
        {
            ApplyLightCulledState();
        }

        if (pauseParticles && Application.isPlaying && targetParticles != null)
        {
            for (int i = 0; i < targetParticles.Length; i++)
            {
                ParticleSystem target = targetParticles[i];
                if (target != null && particlePlayingStates.Length > i && particlePlayingStates[i])
                {
                    target.Pause(withChildren: true);
                }
            }
        }

        if (disableCollidersWhenCulled && targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider target = targetColliders[i];
                if (target != null)
                {
                    target.enabled = false;
                }
            }
        }
    }

    private void ApplyVisibleState()
    {
        if (disableRenderers && targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer target = targetRenderers[i];
                if (target != null && rendererEnabledStates.Length > i)
                {
                    target.enabled = rendererEnabledStates[i];
                }
            }
        }

        if (disableLights && targetLights != null && !isLightDistanceCulled)
        {
            ApplyLightVisibleState();
        }

        if (pauseParticles && Application.isPlaying && targetParticles != null)
        {
            for (int i = 0; i < targetParticles.Length; i++)
            {
                ParticleSystem target = targetParticles[i];
                if (target != null && particlePlayingStates.Length > i && particlePlayingStates[i])
                {
                    target.Play(withChildren: true);
                }
            }
        }

        if (disableCollidersWhenCulled && targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider target = targetColliders[i];
                if (target != null && colliderEnabledStates.Length > i)
                {
                    target.enabled = colliderEnabledStates[i];
                }
            }
        }
    }

    private void CaptureLightStates()
    {
        EnsureStateArray(ref lightEnabledStates, targetLights != null ? targetLights.Length : 0);

        for (int i = 0; targetLights != null && i < targetLights.Length; i++)
        {
            Light target = targetLights[i];
            lightEnabledStates[i] = target != null && target.enabled;
        }
    }

    private void ApplyLightCulledState()
    {
        for (int i = 0; targetLights != null && i < targetLights.Length; i++)
        {
            Light target = targetLights[i];
            if (target != null)
            {
                target.enabled = false;
            }
        }
    }

    private void ApplyLightVisibleState()
    {
        for (int i = 0; targetLights != null && i < targetLights.Length; i++)
        {
            Light target = targetLights[i];
            if (target != null && lightEnabledStates.Length > i)
            {
                target.enabled = lightEnabledStates[i];
            }
        }
    }

    private void ApplyLightPerformanceSettings()
    {
        for (int i = 0; targetLights != null && i < targetLights.Length; i++)
        {
            Light target = targetLights[i];
            if (target == null)
            {
                continue;
            }

            // Shadows are required so walls can occlude point lights and torches.
            // Keep the serialized legacy flags for scene compatibility, but never
            // turn shadows off here; the light can still be distance-culled.
            if (disableLightShadows && target.shadows == LightShadows.None)
            {
                target.shadows = LightShadows.Soft;
            }
            else if (target.shadows == LightShadows.None)
            {
                target.shadows = LightShadows.Soft;
            }

            if (disableHdrpContactShadows)
            {
                continue;
            }

            HDAdditionalLightData hdLight = target.GetComponent<HDAdditionalLightData>();
            if (hdLight == null)
            {
                continue;
            }

            hdLight.useContactShadow.useOverride = true;
            hdLight.useContactShadow.@override = true;
        }
    }

    private void RecalculateBounds()
    {
        Bounds bounds = default;
        bool hasBounds = false;

        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer target = targetRenderers[i];
                if (target == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = target.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(target.bounds);
                }
            }
        }

        if (!hasBounds && targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider target = targetColliders[i];
                if (target == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = target.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(target.bounds);
                }
            }
        }

        if (hasBounds)
        {
            boundingSphere = new BoundingSphere(
                bounds.center,
                Mathf.Max(minimumBoundsRadius, bounds.extents.magnitude + boundsPadding));
        }
        else
        {
            boundingSphere = new BoundingSphere(
                transform.position,
                Mathf.Max(minimumBoundsRadius, boundsPadding));
        }

        boundsDirty = false;
    }

    private static void EnsureStateArray(ref bool[] states, int targetLength)
    {
        if (states == null || states.Length != targetLength)
        {
            states = new bool[targetLength];
        }
    }

    private void OnDrawGizmosSelected()
    {
        BoundingSphere sphere = CurrentBoundingSphere;
        Gizmos.color = isCulled
            ? new Color(1f, 0.35f, 0.15f, 0.35f)
            : new Color(0.2f, 0.85f, 1f, 0.35f);
        Gizmos.DrawWireSphere(sphere.position, sphere.radius);
    }
}
