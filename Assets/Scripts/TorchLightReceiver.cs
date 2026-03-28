using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
public class TorchLightReceiver : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Light targetLight;
    [SerializeField] private bool searchInChildren = true;
    [SerializeField] private bool disableColorTemperatureWhenColored = true;

    [Header("Shadowing")]
    [SerializeField] private bool configureTorchShadowing = true;
    [SerializeField] private LightRenderMode renderMode = LightRenderMode.ForcePixel;
    [SerializeField] private LightShadows shadowMode = LightShadows.Soft;
    [SerializeField, Range(0f, 1f)] private float shadowStrength = 1f;
    [SerializeField, Range(0f, 0.2f)] private float shadowBias = 0.02f;
    [SerializeField, Range(0f, 0.5f)] private float shadowNormalBias = 0.08f;
    [SerializeField, Min(0.01f)] private float shadowNearPlane = 0.05f;
    [SerializeField, Min(128)] private int hdrpShadowResolution = 1024;
    [SerializeField, Range(0f, 1f)] private float hdrpNormalBias = 0.1f;
    [SerializeField, Range(0f, 1f)] private float hdrpSlopeBias = 0.2f;
    [SerializeField] private bool enableHdrpContactShadows = true;

    [Header("Owner")]
    [SerializeField] private SquadCharacterController owner;
    [SerializeField] private bool searchOwnerInParents = true;

    private Color defaultColor;
    private bool defaultUseColorTemperature;
    private float defaultColorTemperature;
    private bool hasDefault;
    private Color currentTorchColor = Color.white;
    private HDAdditionalLightData targetHdLight;

    public SquadCharacterController Owner => owner;
    public bool ControlsShadowing => configureTorchShadowing;

    public Color CurrentTorchColor
    {
        get
        {
            CacheLight();

            if (hasDefault)
            {
                return currentTorchColor;
            }

            TorchVisionDefinition vision = GetOwnerVision();
            if (vision != null && !vision.useDefaultLightSettings)
            {
                return vision.lightColor;
            }

            return hasDefault ? defaultColor : Color.white;
        }
    }

    public Vector3 TorchWorldPosition
    {
        get
        {
            if (targetLight != null)
            {
                return targetLight.transform.position;
            }

            return transform.position;
        }
    }

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
        TorchVisionSystem.GetOrCreate();
        TorchVisionSystem.RegisterTorchSource(this);
        TorchVisionSystem.VisionChanged += OnVisionChanged;
        ApplyVision(GetOwnerVision());
    }

    private void OnValidate()
    {
        CacheLight();
        shadowStrength = Mathf.Clamp01(shadowStrength);
        shadowBias = Mathf.Clamp(shadowBias, 0f, 0.2f);
        shadowNormalBias = Mathf.Clamp(shadowNormalBias, 0f, 0.5f);
        shadowNearPlane = Mathf.Max(0.01f, shadowNearPlane);
        hdrpShadowResolution = Mathf.Max(128, hdrpShadowResolution);
        hdrpNormalBias = Mathf.Clamp01(hdrpNormalBias);
        hdrpSlopeBias = Mathf.Clamp01(hdrpSlopeBias);
        ApplyShadowing();
    }

    private void OnDisable()
    {
        TorchVisionSystem.UnregisterTorchSource(this);
        TorchVisionSystem.VisionChanged -= OnVisionChanged;
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
        currentTorchColor = defaultColor;
        hasDefault = true;
    }

    private void ApplyShadowing()
    {
        if (targetLight == null || !configureTorchShadowing)
        {
            return;
        }

        targetLight.renderMode = renderMode;
        targetLight.shadows = shadowMode;
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
        if (owner != null)
        {
            return;
        }

        if (searchOwnerInParents)
        {
            owner = GetComponentInParent<SquadCharacterController>(true);
        }
    }

    private TorchVisionDefinition GetOwnerVision()
    {
        if (owner == null)
        {
            return null;
        }

        return TorchVisionSystem.GetVisionFor(owner);
    }

    public bool TryGetTorchSourceInfo(
        out SquadCharacterController controller,
        out TorchVisionDefinition vision,
        out bool torchEquipped,
        out Vector3 position)
    {
        CacheOwner();
        CacheLight();

        controller = owner;
        vision = GetOwnerVision();
        torchEquipped = owner != null && owner.IsTorchEquipped;
        position = TorchWorldPosition;

        return controller != null;
    }

    private void OnVisionChanged(SquadCharacterController controller, TorchVisionDefinition previous, TorchVisionDefinition current)
    {
        if (controller != owner)
        {
            return;
        }

        ApplyVision(current);
    }

    private void ApplyVision(TorchVisionDefinition vision)
    {
        if (targetLight == null)
        {
            return;
        }

        if (vision == null || vision.useDefaultLightSettings)
        {
            currentTorchColor = hasDefault ? defaultColor : Color.white;

            if (hasDefault)
            {
                targetLight.color = defaultColor;
                targetLight.useColorTemperature = defaultUseColorTemperature;
                targetLight.colorTemperature = defaultColorTemperature;
            }
            return;
        }

        currentTorchColor = vision.lightColor;
        targetLight.color = vision.lightColor;

        if (disableColorTemperatureWhenColored)
        {
            targetLight.useColorTemperature = false;
        }
    }
}
