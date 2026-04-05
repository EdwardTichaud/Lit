using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TorchVisionSensitive : MonoBehaviour
{
    public enum VisibilityMode
    {
        AlwaysVisible,
        VisibleOnlyWhenVisionMatches,
        HiddenWhenVisionMatches
    }

    private struct DissolveRendererData
    {
        public Renderer Renderer;
        public bool OriginalEnabled;
        public bool HasDissolveAmountProperty;
        public int DissolveAmountPropertyId;
        public bool HasDissolveColorProperty;
        public int DissolveColorPropertyId;
        public Material[] OriginalSharedMaterials;
        public Material[] RuntimeMaterials;
    }

    private const float ActiveRefreshInterval = 0.02f;
    private const float IdleRefreshInterval = 0.1f;

    [Header("Evaluation")]
    [SerializeField] private Transform distanceReference;

    [Header("Vision")]
    [SerializeField] private VisibilityMode visibilityMode = VisibilityMode.VisibleOnlyWhenVisionMatches;
    [SerializeField] private TorchVisionDefinition vision;

    [Header("Torch")]
    [SerializeField] private bool requireTorchEquipped = true;
    [SerializeField, Min(0f)] private float hiddenDistance = 5f;
    [SerializeField, Min(0f)] private float fullyVisibleDistance = 2f;

    [Header("Visuals")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private string dissolveAmountPropertyName = "_DissolveAmount";
    [SerializeField] private string dissolveColorPropertyName = "_DissolveColor";

    [Header("Interaction")]
    [SerializeField] private bool affectColliders = true;
    [SerializeField] private Collider[] colliders;
    [SerializeField] private bool affectBehaviours = false;
    [SerializeField] private Behaviour[] behaviours;

    private readonly List<Renderer> rendererBuffer = new List<Renderer>();
    private DissolveRendererData[] cachedRenderers = Array.Empty<DissolveRendererData>();
    private MaterialPropertyBlock propertyBlock;
    private float refreshTimer;
    private float currentVisibilityFactor = 1f;
    private bool currentTorchInRange;
    private bool hasEvaluatedVisibility;

    public float CurrentVisibilityFactor => currentVisibilityFactor;
    public bool IsWorldUiVisible => visibilityMode == VisibilityMode.AlwaysVisible
        || (hasEvaluatedVisibility && currentVisibilityFactor > 0.001f);
    public bool IsTorchInRange => currentTorchInRange;
    public TorchVisionDefinition RequiredVision => vision;

    private void Awake()
    {
        CacheTargets();
    }

    private void OnEnable()
    {
        CacheTargets();
        TorchVisionSystem.GetOrCreate();
        TorchVisionSystem.VisionChanged += OnTorchDataChanged;
        TorchVisionSystem.TorchStateChanged += OnTorchStateChanged;
        TorchVisionSystem.TorchSourcesChanged += OnTorchSourcesChanged;
        RefreshState();
    }

    private void OnDisable()
    {
        TorchVisionSystem.VisionChanged -= OnTorchDataChanged;
        TorchVisionSystem.TorchStateChanged -= OnTorchStateChanged;
        TorchVisionSystem.TorchSourcesChanged -= OnTorchSourcesChanged;
        ClearPropertyBlocks();
        ReleaseRuntimeMaterials();
        RestoreRendererState();
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f)
        {
            return;
        }

        RefreshState();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheTargets();
        }
    }

    private void CacheTargets()
    {
        ReleaseRuntimeMaterials();

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
            DissolveRendererData data = new DissolveRendererData
            {
                Renderer = renderer
            };

            if (renderer == null)
            {
                cachedRenderers[i] = data;
                continue;
            }

            data.OriginalEnabled = renderer.enabled;
            data.OriginalSharedMaterials = renderer.sharedMaterials ?? Array.Empty<Material>();
            Material[] inspectionMaterials = data.OriginalSharedMaterials;

            if (inspectionMaterials != null)
            {
                for (int m = 0; m < inspectionMaterials.Length; m++)
                {
                    Material material = inspectionMaterials[m];
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

                    if (!data.HasDissolveColorProperty
                        && TryResolveColorProperty(material, dissolveColorPropertyName, out int dissolveColorPropertyId))
                    {
                        data.HasDissolveColorProperty = true;
                        data.DissolveColorPropertyId = dissolveColorPropertyId;
                    }
                }
            }

            cachedRenderers[i] = data;
        }
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

    private static bool TryResolveColorProperty(Material material, string propertyName, out int propertyId)
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

    private void RestoreRendererState()
    {
        if (cachedRenderers == null)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            DissolveRendererData data = cachedRenderers[i];
            if (data.Renderer == null)
            {
                continue;
            }

            data.Renderer.enabled = data.OriginalEnabled;
        }
    }

    private void ReleaseRuntimeMaterials()
    {
        if (cachedRenderers == null)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            DissolveRendererData data = cachedRenderers[i];
            if (data.Renderer == null || data.RuntimeMaterials == null || data.RuntimeMaterials.Length == 0)
            {
                continue;
            }

            if (data.OriginalSharedMaterials != null && data.OriginalSharedMaterials.Length > 0)
            {
                data.Renderer.SetPropertyBlock(null);
                data.Renderer.sharedMaterials = data.OriginalSharedMaterials;
            }

            for (int m = 0; m < data.RuntimeMaterials.Length; m++)
            {
                Material material = data.RuntimeMaterials[m];
                if (material == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(material);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            data.RuntimeMaterials = null;
            cachedRenderers[i] = data;
        }
    }

    private void EnsureRuntimeMaterials(ref DissolveRendererData data)
    {
        if (!Application.isPlaying
            || data.Renderer == null
            || data.RuntimeMaterials != null
            || (!data.HasDissolveAmountProperty && !data.HasDissolveColorProperty))
        {
            return;
        }

        if (data.OriginalSharedMaterials == null || data.OriginalSharedMaterials.Length == 0)
        {
            data.OriginalSharedMaterials = data.Renderer.sharedMaterials ?? Array.Empty<Material>();
        }

        if (data.OriginalSharedMaterials.Length == 0)
        {
            return;
        }

        // Some renderers/shaders ignore per-renderer blocks in play mode; use per-renderer material
        // instances as a reliable fallback for dissolve-driven visibility.
        data.RuntimeMaterials = data.Renderer.materials;
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
            }
        }
    }

    private void OnTorchDataChanged(SquadCharacterController controller, TorchVisionDefinition previous, TorchVisionDefinition current)
    {
        RefreshState();
    }

    private void OnTorchStateChanged(SquadCharacterController controller, bool equipped)
    {
        RefreshState();
    }

    private void OnTorchSourcesChanged()
    {
        RefreshState();
    }

    public void ConfigureRuntime(
        TorchVisionDefinition targetVision,
        VisibilityMode mode = VisibilityMode.VisibleOnlyWhenVisionMatches,
        bool requireEquippedTorch = true,
        Transform runtimeVisualRoot = null,
        bool includeChildRenderers = true,
        float maxRevealDistance = 5f,
        float fullRevealDistance = 2f,
        bool driveColliders = true,
        bool driveBehaviours = false)
    {
        visibilityMode = mode;
        vision = targetVision;
        requireTorchEquipped = requireEquippedTorch;
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

        if (Application.isPlaying && isActiveAndEnabled)
        {
            RefreshState();
        }
    }

    private void RefreshState()
    {
        float visibilityFactor = DetermineVisibilityFactor(out Color dissolveColor, out bool torchInRange);
        currentVisibilityFactor = Mathf.Clamp01(visibilityFactor);
        currentTorchInRange = torchInRange;
        hasEvaluatedVisibility = true;
        refreshTimer = torchInRange ? ActiveRefreshInterval : IdleRefreshInterval;
        ApplyVisuals(currentVisibilityFactor, dissolveColor);
        ApplyInteraction(currentVisibilityFactor >= 0.999f);
    }

    private float DetermineVisibilityFactor(out Color dissolveColor, out bool torchInRange)
    {
        switch (visibilityMode)
        {
            case VisibilityMode.AlwaysVisible:
                dissolveColor = GetFallbackTorchColor();
                torchInRange = false;
                return 1f;
            case VisibilityMode.HiddenWhenVisionMatches:
            {
                float revealFactor = GetRevealFactorFromNearestTorch(out dissolveColor, out torchInRange);
                return 1f - revealFactor;
            }
            default:
                return GetRevealFactorFromNearestTorch(out dissolveColor, out torchInRange);
        }
    }

    private float GetRevealFactorFromNearestTorch(out Color torchColor, out bool torchInRange)
    {
        torchColor = GetFallbackTorchColor();
        torchInRange = false;
        GetOrderedDistanceThresholds(out float maxRevealDistance, out float fullRevealDistance);

        if (!TorchVisionSystem.TryGetNearestMatchingTorch(
                vision,
                GetReferencePosition(),
                maxRevealDistance,
                requireTorchEquipped,
                out TorchVisionSystem.TorchSourceMatch match))
        {
            return 0f;
        }

        torchInRange = true;
        torchColor = GetTorchColor(match);
        if (match.Distance <= fullRevealDistance)
        {
            return 1f;
        }

        if (maxRevealDistance <= fullRevealDistance)
        {
            return 1f;
        }

        return Mathf.Clamp01(Mathf.InverseLerp(maxRevealDistance, fullRevealDistance, match.Distance));
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

    private Color GetFallbackTorchColor()
    {
        if (vision != null && !vision.useDefaultLightSettings)
        {
            return vision.lightColor;
        }

        return Color.white;
    }

    private Color GetTorchColor(TorchVisionSystem.TorchSourceMatch match)
    {
        if (match.Receiver != null)
        {
            return match.Receiver.CurrentTorchColor;
        }

        if (match.Vision != null && !match.Vision.useDefaultLightSettings)
        {
            return match.Vision.lightColor;
        }

        return GetFallbackTorchColor();
    }

    private void ApplyVisuals(float visibilityFactor, Color dissolveColor)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            return;
        }

        float clampedVisibility = Mathf.Clamp01(visibilityFactor);
        float dissolveAmount = 1f - clampedVisibility;

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

            EnsureRuntimeMaterials(ref data);

            data.Renderer.GetPropertyBlock(propertyBlock);

            if (data.HasDissolveAmountProperty)
            {
                propertyBlock.SetFloat(data.DissolveAmountPropertyId, dissolveAmount);
            }

            if (data.HasDissolveColorProperty)
            {
                propertyBlock.SetColor(data.DissolveColorPropertyId, dissolveColor);
            }

            data.Renderer.SetPropertyBlock(propertyBlock);

            if (data.RuntimeMaterials != null)
            {
                for (int m = 0; m < data.RuntimeMaterials.Length; m++)
                {
                    Material material = data.RuntimeMaterials[m];
                    if (material == null)
                    {
                        continue;
                    }

                    if (data.HasDissolveAmountProperty && material.HasProperty(data.DissolveAmountPropertyId))
                    {
                        material.SetFloat(data.DissolveAmountPropertyId, dissolveAmount);
                    }

                    if (data.HasDissolveColorProperty && material.HasProperty(data.DissolveColorPropertyId))
                    {
                        material.SetColor(data.DissolveColorPropertyId, dissolveColor);
                    }
                }
            }

            cachedRenderers[i] = data;
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
                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                behaviour.enabled = enabled;
            }
        }
    }
}
