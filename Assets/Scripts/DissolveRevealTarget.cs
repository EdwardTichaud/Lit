using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[AddComponentMenu("Lit/Rendering/Dissolve Reveal Target")]
public class DissolveRevealTarget : MonoBehaviour
{
    private struct DissolveRendererData
    {
        public Renderer Renderer;
        public bool OriginalEnabled;
        public bool HasDissolveAmountProperty;
        public int DissolveAmountPropertyId;
        public bool HasDissolveEdgeColorProperty;
        public int DissolveEdgeColorPropertyId;
    }

    private const float ActiveRefreshInterval = 0.02f;
    private const float IdleRefreshInterval = 0.1f;
    private const float VisibilityApplyEpsilon = 0.001f;
    private static readonly string[] LegacyDissolveEdgeColorPropertyNames = { "_DissolveColor", "_EdgeColor" };

    [Header("Evaluation")]
    [SerializeField] private Transform distanceReference;
    [SerializeField] private bool evaluateDistanceFromBounds = true;
    [SerializeField] private bool useColliderBoundsWhenNoRendererBounds = true;

    [Header("Light Reveal")]
    [FormerlySerializedAs("requireTorchEquipped")]
    [SerializeField] private bool requireActiveRevealSource = true;
    [SerializeField, Min(0f)] private float hiddenDistance = 5f;
    [SerializeField, Min(0f)] private float fullyVisibleDistance = 2f;

    [Header("Visuals")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private string dissolveAmountPropertyName = "_DissolveAmount";
    [FormerlySerializedAs("dissolveColorPropertyName")]
    [SerializeField] private string dissolveEdgeColorPropertyName = "_DissolveEdgeColor";
    [SerializeField, Min(0f)] private float revealTransitionDuration = 0.5f;
    [SerializeField, Min(0f)] private float hiddenDissolveAmount = 1f;
    [SerializeField, Min(0f)] private float visibleDissolveAmount = 0f;
    [FormerlySerializedAs("fallbackDissolveColor")]
    [SerializeField] private Color fallbackDissolveEdgeColor = Color.white;

    [Header("Interaction")]
    [SerializeField] private bool affectColliders = true;
    [SerializeField] private Collider[] colliders;
    [SerializeField] private bool affectBehaviours = false;
    [SerializeField] private Behaviour[] behaviours;

    [Header("Debug")]
    [SerializeField] private bool showDistanceReferenceGizmo = true;
    [SerializeField, Min(0.01f)] private float distanceReferenceGizmoRadius = 0.2f;

    private readonly List<Renderer> rendererBuffer = new List<Renderer>();
    private readonly List<DissolveRevealSourceInfo> revealSources = new List<DissolveRevealSourceInfo>();
    private DissolveRendererData[] cachedRenderers = Array.Empty<DissolveRendererData>();
    private MaterialPropertyBlock propertyBlock;
    private float refreshTimer;
    private float currentVisibilityFactor = 1f;
    private float targetVisibilityFactor = 1f;
    private bool currentSourceInRange;
    private bool hasEvaluatedVisibility;
    private float lastAppliedVisibilityFactor = float.NaN;
    private Color targetDissolveEdgeColor = Color.white;
    private Color lastAppliedDissolveEdgeColor;
    private bool lastAppliedInteractionEnabled;
    private bool hasAppliedVisuals;
    private bool hasAppliedInteraction;

    public float CurrentVisibilityFactor => currentVisibilityFactor;
    public bool IsWorldUiVisible => hasEvaluatedVisibility && currentVisibilityFactor > 0.001f;
    public bool IsRevealSourceInRange => currentSourceInRange;

    private void Awake()
    {
        CacheTargets();
    }

    private void OnEnable()
    {
        CacheTargets();
        DissolveRevealSystem.SourcesChanged += OnRevealSourcesChanged;
        InvalidateAppliedState();
        RefreshState();
    }

    private void OnDisable()
    {
        DissolveRevealSystem.SourcesChanged -= OnRevealSourcesChanged;
        ClearPropertyBlocks();
        RestoreRendererState();
        InvalidateAppliedState();
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            RefreshState();
            return;
        }

        if (hasEvaluatedVisibility && AdvanceVisibility(Time.deltaTime))
        {
            ApplyCurrentState();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheTargets();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDistanceReferenceGizmo)
        {
            return;
        }

        Vector3 referencePosition = GetReferencePosition();
        Gizmos.color = distanceReference != null
            ? new Color(0.2f, 0.8f, 1f, 0.95f)
            : new Color(1f, 0.75f, 0.2f, 0.95f);
        Gizmos.DrawSphere(referencePosition, Mathf.Max(0.01f, distanceReferenceGizmoRadius));
    }

    public void ConfigureRuntime(
        bool requireActiveSource = true,
        Transform runtimeVisualRoot = null,
        bool includeChildRenderers = true,
        float maxRevealDistance = 5f,
        float fullRevealDistance = 2f,
        bool driveColliders = true,
        bool driveBehaviours = false)
    {
        requireActiveRevealSource = requireActiveSource;
        visualRoot = runtimeVisualRoot;
        includeChildren = includeChildRenderers;
        hiddenDistance = Mathf.Max(0f, maxRevealDistance);
        fullyVisibleDistance = Mathf.Max(0f, fullRevealDistance);
        affectColliders = driveColliders;
        affectBehaviours = driveBehaviours;
        targetRenderers = Array.Empty<Renderer>();
        colliders = Array.Empty<Collider>();
        behaviours = Array.Empty<Behaviour>();
        CacheTargets();
        InvalidateAppliedState();

        if (Application.isPlaying && isActiveAndEnabled)
        {
            RefreshState();
        }
    }

    private void CacheTargets()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = CollectRenderers(visualRoot != null ? visualRoot : transform);
        }

        if (affectColliders && (colliders == null || colliders.Length == 0))
        {
            colliders = includeChildren ? GetComponentsInChildren<Collider>(true) : GetComponents<Collider>();
        }

        if (affectBehaviours && (behaviours == null || behaviours.Length == 0))
        {
            behaviours = includeChildren ? GetComponentsInChildren<Behaviour>(true) : GetComponents<Behaviour>();
        }

        CacheRendererData();
        InvalidateAppliedState();
    }

    private Renderer[] CollectRenderers(Transform root)
    {
        if (root == null)
        {
            return Array.Empty<Renderer>();
        }

        rendererBuffer.Clear();
        if (includeChildren)
        {
            root.GetComponentsInChildren(true, rendererBuffer);
        }
        else
        {
            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null)
            {
                rendererBuffer.Add(renderer);
            }
        }

        return rendererBuffer.ToArray();
    }

    private void CacheRendererData()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            cachedRenderers = Array.Empty<DissolveRendererData>();
            return;
        }

        cachedRenderers = new DissolveRendererData[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            DissolveRendererData data = new DissolveRendererData { Renderer = renderer };
            if (renderer == null)
            {
                cachedRenderers[i] = data;
                continue;
            }

            data.OriginalEnabled = renderer.enabled;
            Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material == null)
                {
                    continue;
                }

                if (!data.HasDissolveAmountProperty
                    && TryResolveFloatProperty(material, dissolveAmountPropertyName, out int dissolveAmountPropertyId))
                {
                    data.HasDissolveAmountProperty = true;
                    data.DissolveAmountPropertyId = dissolveAmountPropertyId;
                }

                if (!data.HasDissolveEdgeColorProperty
                    && TryResolveColorProperty(
                        material,
                        dissolveEdgeColorPropertyName,
                        LegacyDissolveEdgeColorPropertyNames,
                        out int dissolveEdgeColorPropertyId))
                {
                    data.HasDissolveEdgeColorProperty = true;
                    data.DissolveEdgeColorPropertyId = dissolveEdgeColorPropertyId;
                }
            }

            cachedRenderers[i] = data;
        }
    }

    private void RefreshState()
    {
        float visibilityFactor = GetRevealFactorFromNearestSource(out Color dissolveEdgeColor, out bool sourceInRange);
        targetVisibilityFactor = Mathf.Clamp01(visibilityFactor);
        targetDissolveEdgeColor = dissolveEdgeColor;
        currentSourceInRange = sourceInRange;
        hasEvaluatedVisibility = true;

        if (!hasAppliedVisuals)
        {
            currentVisibilityFactor = targetVisibilityFactor;
        }
        else
        {
            AdvanceVisibility(Time.deltaTime);
        }

        refreshTimer = sourceInRange || IsVisibilityTransitioning()
            ? ActiveRefreshInterval
            : IdleRefreshInterval;

        ApplyCurrentState();
    }

    private void ApplyCurrentState()
    {
        if (ShouldApplyVisuals(currentVisibilityFactor, targetDissolveEdgeColor))
        {
            ApplyVisuals(currentVisibilityFactor, targetDissolveEdgeColor);
            MarkVisualsApplied(currentVisibilityFactor, targetDissolveEdgeColor);
        }

        bool interactionEnabled = currentVisibilityFactor >= 0.999f;
        if (ShouldApplyInteraction(interactionEnabled))
        {
            ApplyInteraction(interactionEnabled);
            MarkInteractionApplied(interactionEnabled);
        }
    }

    private float GetRevealFactorFromNearestSource(out Color sourceColor, out bool sourceInRange)
    {
        sourceColor = fallbackDissolveEdgeColor;
        sourceInRange = false;
        GetOrderedDistanceThresholds(out float maxRevealDistance, out float fullRevealDistance);

        if (!TryGetNearestRevealSource(maxRevealDistance, out DissolveRevealSourceInfo source, out float distance))
        {
            return 0f;
        }

        sourceInRange = true;
        sourceColor = source.Color;
        if (distance <= fullRevealDistance || maxRevealDistance <= fullRevealDistance)
        {
            return 1f;
        }

        return Mathf.Clamp01(Mathf.InverseLerp(maxRevealDistance, fullRevealDistance, distance));
    }

    private bool TryGetNearestRevealSource(
        float maxRevealDistance,
        out DissolveRevealSourceInfo bestSource,
        out float bestDistance)
    {
        bestSource = default;
        bestDistance = 0f;
        DissolveRevealSystem.GetSources(revealSources, requireActiveRevealSource);
        if (revealSources.Count == 0)
        {
            return false;
        }

        Bounds bounds = default;
        bool useBounds = evaluateDistanceFromBounds && TryGetEvaluationBounds(out bounds);
        Vector3 referencePosition = GetReferencePosition();
        float bestDistanceSqr = maxRevealDistance > 0f
            ? maxRevealDistance * maxRevealDistance
            : float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < revealSources.Count; i++)
        {
            DissolveRevealSourceInfo source = revealSources[i];
            float distanceSqr = useBounds
                ? bounds.SqrDistance(source.Position)
                : (source.Position - referencePosition).sqrMagnitude;
            if (distanceSqr > bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestSource = source;
            found = true;
        }

        revealSources.Clear();
        if (!found)
        {
            return false;
        }

        bestDistance = Mathf.Sqrt(bestDistanceSqr);
        return true;
    }

    private void GetOrderedDistanceThresholds(out float maxRevealDistance, out float fullRevealDistance)
    {
        maxRevealDistance = Mathf.Max(hiddenDistance, fullyVisibleDistance);
        fullRevealDistance = Mathf.Min(hiddenDistance, fullyVisibleDistance);
    }

    private Vector3 GetReferencePosition()
    {
        Transform reference = distanceReference != null ? distanceReference : transform;
        return reference.position;
    }

    private bool TryGetEvaluationBounds(out Bounds bounds)
    {
        return TryGetRendererBounds(out bounds)
            || (useColliderBoundsWhenNoRendererBounds && TryGetColliderBounds(out bounds));
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer renderer = cachedRenderers[i].Renderer;
                if (renderer != null)
                {
                    EncapsulateBounds(renderer.bounds, ref bounds, ref hasBounds);
                }
            }
        }

        return hasBounds;
    }

    private bool TryGetColliderBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        if (colliders == null)
        {
            return false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null)
            {
                EncapsulateBounds(collider.bounds, ref bounds, ref hasBounds);
            }
        }

        return hasBounds;
    }

    private static void EncapsulateBounds(Bounds source, ref Bounds combined, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            combined = source;
            hasBounds = true;
            return;
        }

        combined.Encapsulate(source);
    }

    private void ApplyVisuals(float visibilityFactor, Color dissolveEdgeColor)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            return;
        }

        float clampedVisibility = Mathf.Clamp01(visibilityFactor);
        float dissolveAmount = Mathf.Lerp(hiddenDissolveAmount, visibleDissolveAmount, clampedVisibility);

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            DissolveRendererData data = cachedRenderers[i];
            if (data.Renderer == null)
            {
                continue;
            }

            bool canDriveDissolve = data.HasDissolveAmountProperty;
            data.Renderer.enabled = data.OriginalEnabled && (canDriveDissolve || clampedVisibility > 0f);
            data.Renderer.GetPropertyBlock(propertyBlock);

            if (data.HasDissolveAmountProperty)
            {
                propertyBlock.SetFloat(data.DissolveAmountPropertyId, dissolveAmount);
            }

            if (data.HasDissolveEdgeColorProperty)
            {
                propertyBlock.SetColor(data.DissolveEdgeColorPropertyId, dissolveEdgeColor);
            }

            data.Renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplyInteraction(bool enabled)
    {
        if (affectColliders && colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }
        }

        if (affectBehaviours && behaviours != null)
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour != this)
                {
                    behaviour.enabled = enabled;
                }
            }
        }
    }

    private void RestoreRendererState()
    {
        if (cachedRenderers == null)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i].Renderer;
            if (renderer != null)
            {
                renderer.enabled = cachedRenderers[i].OriginalEnabled;
            }
        }
    }

    private void ClearPropertyBlocks()
    {
        if (cachedRenderers == null)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i].Renderer;
            if (renderer != null)
            {
                renderer.SetPropertyBlock(null);
                AgeManager.ActiveInstance?.ApplyShaderAgeToRenderer(renderer);
            }
        }
    }

    private void OnRevealSourcesChanged()
    {
        RefreshState();
    }

    private bool ShouldApplyVisuals(float visibilityFactor, Color dissolveEdgeColor)
    {
        return !hasAppliedVisuals
            || Mathf.Abs(lastAppliedVisibilityFactor - visibilityFactor) > VisibilityApplyEpsilon
            || !ColorsApproximatelyEqual(lastAppliedDissolveEdgeColor, dissolveEdgeColor);
    }

    private void MarkVisualsApplied(float visibilityFactor, Color dissolveEdgeColor)
    {
        hasAppliedVisuals = true;
        lastAppliedVisibilityFactor = visibilityFactor;
        lastAppliedDissolveEdgeColor = dissolveEdgeColor;
    }

    private bool ShouldApplyInteraction(bool enabled)
    {
        return affectBehaviours || !hasAppliedInteraction || lastAppliedInteractionEnabled != enabled;
    }

    private void MarkInteractionApplied(bool enabled)
    {
        hasAppliedInteraction = true;
        lastAppliedInteractionEnabled = enabled;
    }

    private void InvalidateAppliedState()
    {
        hasAppliedVisuals = false;
        hasAppliedInteraction = false;
        lastAppliedVisibilityFactor = float.NaN;
        lastAppliedDissolveEdgeColor = default;
        lastAppliedInteractionEnabled = false;
    }

    private bool AdvanceVisibility(float deltaTime)
    {
        float previousVisibility = currentVisibilityFactor;
        if (revealTransitionDuration <= 0f)
        {
            currentVisibilityFactor = targetVisibilityFactor;
        }
        else
        {
            currentVisibilityFactor = Mathf.MoveTowards(
                currentVisibilityFactor,
                targetVisibilityFactor,
                Mathf.Max(0f, deltaTime) / revealTransitionDuration);
        }

        return Mathf.Abs(previousVisibility - currentVisibilityFactor) > VisibilityApplyEpsilon;
    }

    private bool IsVisibilityTransitioning()
    {
        return Mathf.Abs(currentVisibilityFactor - targetVisibilityFactor) > VisibilityApplyEpsilon;
    }

    private static bool TryResolveFloatProperty(Material material, string propertyName, out int propertyId)
    {
        propertyId = 0;
        if (material == null || string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        propertyId = Shader.PropertyToID(propertyName);
        if (!material.HasProperty(propertyId))
        {
            propertyId = 0;
            return false;
        }

        return true;
    }

    private static bool TryResolveColorProperty(
        Material material,
        string propertyName,
        IReadOnlyList<string> fallbackPropertyNames,
        out int propertyId)
    {
        if (TryResolveColorProperty(material, propertyName, out propertyId))
        {
            return true;
        }

        if (fallbackPropertyNames == null)
        {
            return false;
        }

        for (int i = 0; i < fallbackPropertyNames.Count; i++)
        {
            if (TryResolveColorProperty(material, fallbackPropertyNames[i], out propertyId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveColorProperty(Material material, string propertyName, out int propertyId)
    {
        propertyId = 0;
        if (material == null || string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        propertyId = Shader.PropertyToID(propertyName);
        if (material.HasProperty(propertyId))
        {
            return true;
        }

        propertyId = 0;
        return false;
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) <= VisibilityApplyEpsilon
            && Mathf.Abs(a.g - b.g) <= VisibilityApplyEpsilon
            && Mathf.Abs(a.b - b.b) <= VisibilityApplyEpsilon
            && Mathf.Abs(a.a - b.a) <= VisibilityApplyEpsilon;
    }
}
