using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class FlameLightReceiver : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Light targetLight;
    [SerializeField] private bool searchInChildren = true;
    [SerializeField] private bool disableColorTemperatureWhenColored = true;

    [Header("Shadowing")]
    [SerializeField] private bool configureFlameShadowing = true;
    [SerializeField] private LightRenderMode renderMode = LightRenderMode.ForcePixel;
    [SerializeField] private LightShadows shadowMode = LightShadows.Soft;
    [SerializeField, Range(0f, 1f)] private float shadowStrength = 1f;
    [SerializeField, Range(0f, 0.2f)] private float shadowBias = 0.005f;
    [SerializeField, Range(0f, 0.5f)] private float shadowNormalBias = 0.03f;
    [SerializeField, Min(0.01f)] private float shadowNearPlane = 0.02f;
    [SerializeField, Min(128)] private int hdrpShadowResolution = 1024;
    [SerializeField, Range(0f, 1f)] private float hdrpNormalBias = 0.03f;
    [SerializeField, Range(0f, 1f)] private float hdrpSlopeBias = 0.1f;
    [SerializeField] private bool enableHdrpContactShadows = true;

    [Header("Owner")]
    [SerializeField] private SquadCharacterController owner;
    [SerializeField] private bool searchOwnerInParents = true;

    [Header("Dissolve Reveal Source")]
    [FormerlySerializedAs("registerAsWorldFlameSource")]
    [SerializeField] private bool registerAsWorldRevealSource;
    [FormerlySerializedAs("worldFlameCountsAsEquipped")]
    [SerializeField] private bool worldSourceCountsAsActive = true;
    [SerializeField] private Color revealColor = Color.white;

    private Color defaultColor;
    private bool defaultUseColorTemperature;
    private float defaultColorTemperature;
    private bool hasDefault;
    private bool worldRevealSuppressed;
    private HDAdditionalLightData targetHdLight;

    public SquadCharacterController Owner => owner;
    public bool ControlsShadowing => configureFlameShadowing;
    public Color CurrentFlameColor => targetLight != null ? targetLight.color : revealColor;
    public Vector3 FlameWorldPosition => targetLight != null ? targetLight.transform.position : transform.position;

    private void Awake()
    {
        CacheLight();
        CacheOwner();
        ApplyShadowing();
    }

    private void OnEnable()
    {
        CacheLight();
        CacheOwner();
        ApplyShadowing();
        ApplyRevealColor();
        DissolveRevealSystem.RegisterSource(this);
    }

    private void OnDisable()
    {
        DissolveRevealSystem.UnregisterSource(this);
    }

    private void OnValidate()
    {
        CacheLight();
        CacheOwner();
        if (shadowMode == LightShadows.None)
        {
            shadowMode = LightShadows.Soft;
        }

        shadowStrength = Mathf.Clamp01(shadowStrength);
        shadowBias = Mathf.Clamp(shadowBias, 0f, 0.2f);
        shadowNormalBias = Mathf.Clamp(shadowNormalBias, 0f, 0.5f);
        shadowNearPlane = Mathf.Max(0.01f, shadowNearPlane);
        hdrpShadowResolution = Mathf.Max(128, hdrpShadowResolution);
        hdrpNormalBias = Mathf.Clamp01(hdrpNormalBias);
        hdrpSlopeBias = Mathf.Clamp01(hdrpSlopeBias);
        ApplyShadowing();
        ApplyRevealColor();
    }

    public void ConfigureWorldRevealSource(bool countsAsActive = true, Color? color = null)
    {
        registerAsWorldRevealSource = true;
        worldSourceCountsAsActive = countsAsActive;
        if (color.HasValue)
        {
            revealColor = color.Value;
        }

        CacheLight();
        ApplyRevealColor();
    }

    public bool TryGetRevealSourceInfo(out DissolveRevealSourceInfo info)
    {
        CacheOwner();
        CacheLight();

        bool active = !worldRevealSuppressed && (owner != null
            ? owner.IsFlameEquipped
            : registerAsWorldRevealSource && worldSourceCountsAsActive);

        if (targetLight != null && !targetLight.enabled)
        {
            active = false;
        }

        info = new DissolveRevealSourceInfo(this, owner, FlameWorldPosition, CurrentFlameColor, active);
        return owner != null || registerAsWorldRevealSource || targetLight != null;
    }

    public void SetWorldRevealSuppressed(bool suppressed)
    {
        worldRevealSuppressed = suppressed;
    }

    private void CacheLight()
    {
        if (targetLight == null)
        {
            targetLight = searchInChildren ? GetComponentInChildren<Light>(true) : GetComponent<Light>();
        }

        targetHdLight = targetLight != null ? targetLight.GetComponent<HDAdditionalLightData>() : null;

        if (targetLight == null || hasDefault)
        {
            return;
        }

        defaultColor = targetLight.color;
        defaultUseColorTemperature = targetLight.useColorTemperature;
        defaultColorTemperature = targetLight.colorTemperature;
        if (revealColor == Color.white)
        {
            revealColor = defaultColor;
        }

        hasDefault = true;
    }

    private void ApplyShadowing()
    {
        if (targetLight == null || !configureFlameShadowing)
        {
            return;
        }

        targetLight.renderMode = renderMode;
        targetLight.shadows = shadowMode == LightShadows.None ? LightShadows.Soft : shadowMode;
        targetLight.shadowStrength = shadowStrength;
        targetLight.shadowBias = shadowBias;
        targetLight.shadowNormalBias = shadowNormalBias;
        targetLight.shadowNearPlane = shadowNearPlane;

        if (targetHdLight == null)
        {
            return;
        }

        targetHdLight.SetShadowResolution(hdrpShadowResolution);
        targetHdLight.shadowDimmer = 1f;
        targetHdLight.normalBias = hdrpNormalBias;
        targetHdLight.slopeBias = hdrpSlopeBias;
        targetHdLight.useContactShadow.useOverride = true;
        targetHdLight.useContactShadow.@override = enableHdrpContactShadows;
    }

    private void CacheOwner()
    {
        if (owner == null && searchOwnerInParents)
        {
            owner = GetComponentInParent<SquadCharacterController>(true);
        }
    }

    private void ApplyRevealColor()
    {
        if (targetLight == null)
        {
            return;
        }

        if (hasDefault && revealColor == Color.white)
        {
            targetLight.color = defaultColor;
            targetLight.useColorTemperature = defaultUseColorTemperature;
            targetLight.colorTemperature = defaultColorTemperature;
            return;
        }

        targetLight.color = revealColor;
        if (disableColorTemperatureWhenColored)
        {
            targetLight.useColorTemperature = false;
        }
    }
}
