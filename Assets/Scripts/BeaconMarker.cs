using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
public class BeaconMarker : MonoBehaviour
{
    [Header("Color")]
    [SerializeField] private Color markerColor = new Color(0.98f, 0.48f, 0.14f, 1f);

    [Header("Shape")]
    [SerializeField] private float discRadius = 0.1f;
    [SerializeField] private float discThickness = 0.025f;
    [SerializeField] private float stemRadius = 0.018f;
    [SerializeField] private float stemLength = 0.12f;
    [SerializeField] private float gemRadius = 0.032f;
    [SerializeField] private float surfaceOffset = 0.015f;

    [Header("Support")]
    [SerializeField] private bool enableDynamicSupport = true;
    [SerializeField, Min(0.02f)] private float supportCheckInterval = 0.1f;
    [SerializeField, Min(0.005f)] private float supportProbeRadius = 0.03f;
    [SerializeField, Min(0.02f)] private float supportProbeDistance = 0.08f;
    [SerializeField, Min(0.02f)] private float fallProbePadding = 0.08f;
    [SerializeField, Min(0.1f)] private float fallAcceleration = 18f;
    [SerializeField, Min(0.1f)] private float maxFallSpeed = 8f;
    [SerializeField] private LayerMask supportMask = ~0;
    [SerializeField] private bool ignoreCharacterSupport = true;
    [SerializeField] private bool drawSupportDebug = false;

    [Header("Shadowing")]
    [SerializeField] private bool configureBeaconShadowing = true;
    [SerializeField] private LightRenderMode lightRenderMode = LightRenderMode.ForcePixel;
    [SerializeField] private LightShadows shadowMode = LightShadows.None;
    [SerializeField, Range(0f, 1f)] private float shadowStrength = 1f;
    [SerializeField, Range(0f, 0.2f)] private float shadowBias = 0.02f;
    [SerializeField, Range(0f, 0.5f)] private float shadowNormalBias = 0.08f;
    [SerializeField, Min(0.01f)] private float shadowNearPlane = 0.05f;
    [SerializeField, Min(128)] private int hdrpShadowResolution = 1024;
    [SerializeField, Range(0f, 1f)] private float hdrpNormalBias = 0.1f;
    [SerializeField, Range(0f, 1f)] private float hdrpSlopeBias = 0.2f;
    [SerializeField] private bool enableHdrpContactShadows = false;

    [Header("Light Placement")]
    [SerializeField] private bool autoPositionPointLights = true;
    [SerializeField, Range(0f, 1f)] private float pointLightStemRatio = 0.65f;
    [SerializeField, Min(0f)] private float pointLightAdditionalOffset = 0.015f;
    [SerializeField] private bool disableBeaconSelfShadows = true;

    [Header("Runtime")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer discRenderer;
    [SerializeField] private Renderer stemRenderer;
    [SerializeField] private Renderer gemRenderer;
    [SerializeField] private Light[] pointLights;

    private MaterialPropertyBlock propertyBlock;
    private readonly RaycastHit[] supportHits = new RaycastHit[8];
    private HDAdditionalLightData[] pointHdLights = System.Array.Empty<HDAdditionalLightData>();
    private InteractableItem cachedLootContainer;
    private Transform movementRoot;
    private Collider currentSupportCollider;
    private Vector3 currentSupportNormal = Vector3.up;
    private float supportCheckTimer;
    private float currentFallSpeed;
    private bool isFalling;
    private bool supportInitialized;
    private bool movementRootResolved;

    public Color MarkerColor => markerColor;
    public float SurfaceOffset => Mathf.Max(0f, surfaceOffset);

    private void Awake()
    {
        EnsureVisuals();
        EnsurePropertyBlock();
        ApplyLightSetup();
        ApplyColor();
    }

    private void OnEnable()
    {
        EnsureVisuals();
        EnsurePropertyBlock();
        ApplyLightSetup();
        ApplyColor();
        movementRootResolved = false;
        ResolveMovementRoot();
        ResetDynamicSupportState();
    }

    private void OnValidate()
    {
        shadowStrength = Mathf.Clamp01(shadowStrength);
        shadowBias = Mathf.Clamp(shadowBias, 0f, 0.2f);
        shadowNormalBias = Mathf.Clamp(shadowNormalBias, 0f, 0.5f);
        shadowNearPlane = Mathf.Max(0.01f, shadowNearPlane);
        hdrpShadowResolution = Mathf.Max(128, hdrpShadowResolution);
        hdrpNormalBias = Mathf.Clamp01(hdrpNormalBias);
        hdrpSlopeBias = Mathf.Clamp01(hdrpSlopeBias);
        pointLightStemRatio = Mathf.Clamp01(pointLightStemRatio);
        pointLightAdditionalOffset = Mathf.Max(0f, pointLightAdditionalOffset);

        EnsureVisuals();
        ApplyLightSetup();
    }

    private void Update()
    {
        UpdateDynamicSupport();
    }

    public void SetColor(Color color)
    {
        markerColor = color;
        EnsureVisuals();
        EnsurePropertyBlock();
        ApplyColor();
    }

    public static bool TrySetColor(GameObject target, Color color)
    {
        if (!TryFind(target, out BeaconMarker beacon))
        {
            return false;
        }

        beacon.SetColor(color);
        return true;
    }

    public void NotifyPlacedOnSurface(Collider supportCollider, Vector3 supportNormal)
    {
        movementRootResolved = false;
        ResolveMovementRoot();
        currentSupportCollider = supportCollider;
        currentSupportNormal = NormalizeSupportNormal(supportNormal);
        supportInitialized = true;
        isFalling = false;
        currentFallSpeed = 0f;
        supportCheckTimer = Mathf.Max(0.02f, supportCheckInterval);
    }

    public static bool TryFind(GameObject target, out BeaconMarker beacon)
    {
        beacon = null;
        if (target == null)
        {
            return false;
        }

        beacon = target.GetComponent<BeaconMarker>();
        if (beacon == null)
        {
            beacon = target.GetComponentInChildren<BeaconMarker>(true);
        }

        return beacon != null;
    }

    private void EnsureVisuals()
    {
        if (visualRoot == null)
        {
            Transform existing = transform.Find("BeaconVisual");
            if (existing != null)
            {
                visualRoot = existing;
            }
        }

        if (visualRoot == null)
        {
            GameObject root = new GameObject("BeaconVisual");
            root.transform.SetParent(transform, false);
            visualRoot = root.transform;
        }

        if (discRenderer == null)
        {
            discRenderer = EnsurePrimitiveRenderer("Disc", PrimitiveType.Cylinder);
        }

        if (stemRenderer == null)
        {
            stemRenderer = EnsurePrimitiveRenderer("Stem", PrimitiveType.Cylinder);
        }

        if (gemRenderer == null)
        {
            gemRenderer = EnsurePrimitiveRenderer("Gem", PrimitiveType.Sphere);
        }

        LayoutVisuals();
        EnsureLights();
    }

    private Renderer EnsurePrimitiveRenderer(string name, PrimitiveType primitiveType)
    {
        if (visualRoot == null)
        {
            return null;
        }

        Transform existing = visualRoot.Find(name);
        GameObject target;
        if (existing != null)
        {
            target = existing.gameObject;
        }
        else
        {
            target = GameObject.CreatePrimitive(primitiveType);
            target.name = name;
            target.transform.SetParent(visualRoot, false);
        }

        Collider col = target.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            Destroy(col);
        }

        return target.GetComponent<Renderer>();
    }

    private void LayoutVisuals()
    {
        if (visualRoot == null)
        {
            return;
        }

        float baseOffset = SurfaceOffset;
        float discHeight = Mathf.Max(0.002f, discThickness);
        float stemHeight = Mathf.Max(0.01f, stemLength);
        float discScaleXZ = Mathf.Max(0.01f, discRadius) * 2f;
        float discScaleY = discHeight * 0.5f;
        float stemScaleXZ = Mathf.Max(0.005f, stemRadius) * 2f;
        float stemScaleY = stemHeight * 0.5f;
        float gemScale = Mathf.Max(0.01f, gemRadius) * 2f;

        if (discRenderer != null)
        {
            Transform disc = discRenderer.transform;
            disc.localPosition = Vector3.up * (baseOffset + discHeight * 0.5f);
            disc.localRotation = Quaternion.identity;
            disc.localScale = new Vector3(discScaleXZ, discScaleY, discScaleXZ);
        }

        if (stemRenderer != null)
        {
            Transform stem = stemRenderer.transform;
            float stemCenter = baseOffset + discHeight + stemHeight * 0.5f;
            stem.localPosition = Vector3.up * stemCenter;
            stem.localRotation = Quaternion.identity;
            stem.localScale = new Vector3(stemScaleXZ, stemScaleY, stemScaleXZ);
        }

        if (gemRenderer != null)
        {
            Transform gem = gemRenderer.transform;
            float gemCenter = baseOffset + discHeight + stemHeight + Mathf.Max(0.01f, gemRadius);
            gem.localPosition = Vector3.up * gemCenter;
            gem.localRotation = Quaternion.identity;
            gem.localScale = Vector3.one * gemScale;
        }

        ApplyVisualShadowCasting();
        UpdatePointLightPlacement(baseOffset, discHeight, stemHeight);
    }

    private void ApplyColor()
    {
        EnsurePropertyBlock();
        ApplyColorToRenderer(discRenderer, markerColor * 0.85f);
        ApplyColorToRenderer(stemRenderer, markerColor * 0.7f);
        ApplyColorToRenderer(gemRenderer, markerColor);
        ApplyColorToLights(markerColor);
    }

    private void ApplyColorToRenderer(Renderer renderer, Color color)
    {
        if (renderer == null || renderer.sharedMaterial == null)
        {
            return;
        }

        EnsurePropertyBlock();
        if (propertyBlock == null)
        {
            return;
        }

        propertyBlock.Clear();
        if (renderer.sharedMaterial.HasProperty("_BaseColor"))
        {
            propertyBlock.SetColor("_BaseColor", color);
        }
        else if (renderer.sharedMaterial.HasProperty("_Color"))
        {
            propertyBlock.SetColor("_Color", color);
        }
        else
        {
            return;
        }

        renderer.SetPropertyBlock(propertyBlock);
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void EnsureLights()
    {
        if (pointLights != null && pointLights.Length > 0)
        {
            return;
        }

        pointLights = GetComponentsInChildren<Light>(true);
    }

    private void ApplyLightSetup()
    {
        EnsureLights();
        CacheHdLights();

        if (!configureBeaconShadowing || pointLights == null || pointLights.Length == 0)
        {
            return;
        }

        for (int i = 0; i < pointLights.Length; i++)
        {
            Light target = pointLights[i];
            if (target == null || target.type != LightType.Point)
            {
                continue;
            }

            target.renderMode = lightRenderMode;
            target.shadows = shadowMode;
            target.shadowStrength = shadowStrength;
            target.shadowBias = shadowBias;
            target.shadowNormalBias = shadowNormalBias;
            target.shadowNearPlane = shadowNearPlane;

            HDAdditionalLightData hdLight = i < pointHdLights.Length ? pointHdLights[i] : null;
            if (hdLight == null)
            {
                continue;
            }

            hdLight.SetShadowResolution(hdrpShadowResolution);
            hdLight.shadowDimmer = 1f;
            hdLight.normalBias = hdrpNormalBias;
            hdLight.slopeBias = hdrpSlopeBias;
            hdLight.useContactShadow.useOverride = true;
            hdLight.useContactShadow.@override = enableHdrpContactShadows;
        }
    }

    private void CacheHdLights()
    {
        if (pointLights == null || pointLights.Length == 0)
        {
            pointHdLights = System.Array.Empty<HDAdditionalLightData>();
            return;
        }

        if (pointHdLights == null || pointHdLights.Length != pointLights.Length)
        {
            pointHdLights = new HDAdditionalLightData[pointLights.Length];
        }

        for (int i = 0; i < pointLights.Length; i++)
        {
            Light target = pointLights[i];
            if (target == null)
            {
                pointHdLights[i] = null;
                continue;
            }

            HDAdditionalLightData hdLight = target.GetComponent<HDAdditionalLightData>();
            if (hdLight == null && Application.isPlaying)
            {
                hdLight = target.gameObject.AddComponent<HDAdditionalLightData>();
            }

            pointHdLights[i] = hdLight;
        }
    }

    private void ApplyColorToLights(Color color)
    {
        EnsureLights();
        if (pointLights == null || pointLights.Length == 0)
        {
            return;
        }

        for (int i = 0; i < pointLights.Length; i++)
        {
            Light target = pointLights[i];
            if (target == null || target.type != LightType.Point)
            {
                continue;
            }

            target.color = color;
        }
    }

    private void ApplyVisualShadowCasting()
    {
        ApplyRendererShadowCasting(discRenderer);
        ApplyRendererShadowCasting(stemRenderer);
        ApplyRendererShadowCasting(gemRenderer);
    }

    private void ApplyRendererShadowCasting(Renderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.shadowCastingMode = disableBeaconSelfShadows
            ? ShadowCastingMode.Off
            : ShadowCastingMode.On;
        renderer.receiveShadows = !disableBeaconSelfShadows;
    }

    private void UpdatePointLightPlacement(float baseOffset, float discHeight, float stemHeight)
    {
        if (!autoPositionPointLights)
        {
            return;
        }

        EnsureLights();
        if (pointLights == null || pointLights.Length == 0)
        {
            return;
        }

        float anchorDistance = baseOffset
            + discHeight
            + (stemHeight * pointLightStemRatio)
            + pointLightAdditionalOffset;

        for (int i = 0; i < pointLights.Length; i++)
        {
            Light target = pointLights[i];
            if (target == null || target.type != LightType.Point)
            {
                continue;
            }

            Transform lightTransform = target.transform;
            if (lightTransform == null)
            {
                continue;
            }

            lightTransform.localPosition = Vector3.up * anchorDistance;
        }
    }

    private void UpdateDynamicSupport()
    {
        if (!ShouldEvaluateDynamicSupport())
        {
            return;
        }

        if (!supportInitialized)
        {
            BootstrapDynamicSupport();
        }

        if (movementRoot == null)
        {
            return;
        }

        if (isFalling)
        {
            SimulateFall(Time.deltaTime);
            return;
        }

        supportCheckTimer -= Time.deltaTime;
        if (supportCheckTimer > 0f)
        {
            return;
        }

        supportCheckTimer = Mathf.Max(0.02f, supportCheckInterval);
        if (TryProbeAttachedSupport(out RaycastHit supportHit))
        {
            currentSupportCollider = supportHit.collider;
            currentSupportNormal = NormalizeSupportNormal(supportHit.normal);
            return;
        }

        BeginFall();
    }

    private bool ShouldEvaluateDynamicSupport()
    {
        if (!enableDynamicSupport || !Application.isPlaying)
        {
            return false;
        }

        ResolveMovementRoot();
        return cachedLootContainer != null && movementRoot != null;
    }

    private void ResolveMovementRoot()
    {
        if (movementRootResolved)
        {
            movementRoot = cachedLootContainer != null ? cachedLootContainer.transform : null;
            return;
        }

        if (cachedLootContainer == null)
        {
            cachedLootContainer = GetComponent<InteractableItem>();
            if (cachedLootContainer == null)
            {
                cachedLootContainer = GetComponentInParent<InteractableItem>(true);
            }
        }

        movementRoot = cachedLootContainer != null ? cachedLootContainer.transform : null;
        movementRootResolved = true;
    }

    private void OnTransformParentChanged()
    {
        movementRootResolved = false;
    }

    private void ResetDynamicSupportState()
    {
        supportCheckTimer = 0f;
        currentFallSpeed = 0f;
        isFalling = false;
        supportInitialized = false;
        currentSupportCollider = null;
        currentSupportNormal = NormalizeSupportNormal(transform.up);
    }

    private void BootstrapDynamicSupport()
    {
        supportInitialized = true;
        currentSupportNormal = NormalizeSupportNormal(transform.up);
        if (TryProbeAttachedSupport(out RaycastHit supportHit))
        {
            currentSupportCollider = supportHit.collider;
            currentSupportNormal = NormalizeSupportNormal(supportHit.normal);
            supportCheckTimer = Mathf.Max(0.02f, supportCheckInterval);
            return;
        }

        BeginFall();
    }

    private bool TryProbeAttachedSupport(out RaycastHit bestHit)
    {
        bestHit = default;
        if (movementRoot == null)
        {
            return false;
        }

        Vector3 surfaceNormal = NormalizeSupportNormal(currentSupportNormal);
        float radius = Mathf.Max(0.005f, supportProbeRadius);
        float distance = Mathf.Max(radius + 0.01f, SurfaceOffset + supportProbeDistance);
        Vector3 origin = movementRoot.position + surfaceNormal * (radius + 0.01f);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            -surfaceNormal,
            supportHits,
            distance,
            supportMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return false;
        }

        Collider preferredCollider = IsSupportColliderValid(currentSupportCollider) ? currentSupportCollider : null;
        bool foundPreferred = false;
        float preferredDistance = float.MaxValue;
        bool foundAny = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = supportHits[i];
            Collider candidate = hit.collider;
            if (!IsSupportColliderValid(candidate))
            {
                continue;
            }

            if (preferredCollider != null && candidate == preferredCollider)
            {
                if (!foundPreferred || hit.distance < preferredDistance)
                {
                    bestHit = hit;
                    preferredDistance = hit.distance;
                    foundPreferred = true;
                }
                continue;
            }

            if (!foundAny || hit.distance < bestDistance)
            {
                bestHit = hit;
                bestDistance = hit.distance;
                foundAny = true;
            }
        }

        return foundPreferred || foundAny;
    }

    private void BeginFall()
    {
        currentSupportCollider = null;
        isFalling = true;
        currentFallSpeed = 0f;
    }

    private void SimulateFall(float deltaTime)
    {
        if (movementRoot == null || deltaTime <= 0f)
        {
            return;
        }

        currentFallSpeed = Mathf.Min(Mathf.Max(0.1f, maxFallSpeed), currentFallSpeed + Mathf.Max(0.1f, fallAcceleration) * deltaTime);
        float travelDistance = currentFallSpeed * deltaTime;
        float probeDistance = travelDistance + Mathf.Max(0.02f, fallProbePadding);
        Vector3 origin = movementRoot.position + Vector3.up * 0.02f;

        if (TryProbeDownwardSupport(origin, probeDistance, out RaycastHit landingHit))
        {
            Vector3 landingNormal = NormalizeSupportNormal(landingHit.normal);
            movementRoot.position = landingHit.point + landingNormal * SurfaceOffset;
            currentSupportCollider = landingHit.collider;
            currentSupportNormal = landingNormal;
            isFalling = false;
            currentFallSpeed = 0f;
            supportCheckTimer = Mathf.Max(0.02f, supportCheckInterval);
            AlignToSupport(landingNormal);
            return;
        }

        movementRoot.position += Vector3.down * travelDistance;
    }

    private bool TryProbeDownwardSupport(Vector3 origin, float distance, out RaycastHit bestHit)
    {
        bestHit = default;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            supportHits,
            Mathf.Max(0.02f, distance),
            supportMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return false;
        }

        bool found = false;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = supportHits[i];
            if (!IsSupportColliderValid(hit.collider))
            {
                continue;
            }

            if (!found || hit.distance < bestDistance)
            {
                bestHit = hit;
                bestDistance = hit.distance;
                found = true;
            }
        }

        return found;
    }

    private bool IsSupportColliderValid(Collider collider)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (movementRoot != null && collider.transform.IsChildOf(movementRoot))
        {
            return false;
        }

        if (ignoreCharacterSupport && collider.GetComponentInParent<SquadCharacterController>() != null)
        {
            return false;
        }

        return true;
    }

    private void AlignToSupport(Vector3 supportNormal)
    {
        Vector3 up = NormalizeSupportNormal(supportNormal);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.right, up);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.right);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(forward.normalized, up);
    }

    private static Vector3 NormalizeSupportNormal(Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.0001f)
        {
            return Vector3.up;
        }

        return normal.normalized;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSupportDebug)
        {
            return;
        }

        Transform root = movementRoot != null ? movementRoot : transform;
        Vector3 anchor = root.position;
        Vector3 supportNormal = NormalizeSupportNormal(currentSupportNormal == Vector3.zero ? transform.up : currentSupportNormal);

        Gizmos.color = isFalling ? Color.yellow : Color.cyan;
        Gizmos.DrawLine(anchor, anchor - supportNormal * Mathf.Max(0.02f, SurfaceOffset + supportProbeDistance));
        Gizmos.DrawWireSphere(anchor + supportNormal * (Mathf.Max(0.005f, supportProbeRadius) + 0.01f), Mathf.Max(0.005f, supportProbeRadius));

        if (isFalling)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(anchor, anchor + Vector3.down * Mathf.Max(0.05f, fallProbePadding));
        }
    }
}
