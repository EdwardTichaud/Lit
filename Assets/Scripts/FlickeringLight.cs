using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
[ExecuteAlways]
public class FlickeringLight : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Light targetLight;
    [SerializeField] private bool searchInChildren = true;
    [SerializeField] private bool forcePointLight = true;
    [SerializeField] private bool useCurrentLightAsBase = true;
    [SerializeField] private bool syncTorchReceiverColor = true;
    [SerializeField] private bool deferShadowingToTorchReceiver = true;

    [Header("Base Flame")]
    [SerializeField, Min(0.01f)] private float baseIntensity = 1.4f;
    [SerializeField, Min(0.01f)] private float baseRange = 4.5f;
    [SerializeField] private Color emberColor = new Color(1f, 0.48f, 0.18f, 1f);
    [SerializeField] private Color flameColor = new Color(1f, 0.78f, 0.48f, 1f);

    [Header("Flicker")]
    [SerializeField, Range(0f, 1f)] private float intensityVariation = 0.24f;
    [SerializeField, Range(0f, 1f)] private float rangeVariation = 0.08f;
    [SerializeField, Range(0f, 1f)] private float colorVariation = 0.45f;
    [SerializeField, Min(0.1f)] private float flickerSpeed = 7.5f;
    [SerializeField, Min(0f)] private float burstStrength = 0.12f;

    [Header("Motion")]
    [SerializeField] private bool animateLocalPosition = true;
    [SerializeField, Min(0f)] private float swayAmplitude = 0.015f;
    [SerializeField, Min(0.1f)] private float swaySpeed = 1.6f;

    [Header("Shadowing")]
    [SerializeField] private bool configureCandleShadowing = true;
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

    private float initialIntensity;
    private float initialRange;
    private Color initialColor;
    private Vector3 initialLocalPosition;
    private bool hasCachedState;
    private HDAdditionalLightData targetHdLight;
    private TorchLightReceiver torchLightReceiver;

    private float noiseSeedA;
    private float noiseSeedB;
    private float noiseSeedC;
    private float noiseSeedD;
    private float noiseSeedE;

    private void Reset()
    {
        CacheLight();
        CacheInitialState();
        ApplyLightSetup();
    }

    private void Awake()
    {
        CacheLight();
        CacheInitialState();
        InitializeNoiseSeeds();
        ApplyLightSetup();
    }

    private void OnEnable()
    {
        CacheLight();
        CacheInitialState();
        ApplyLightSetup();
        ApplyFlicker(Time.time);
    }

    private void OnDisable()
    {
        RestoreInitialState();
    }

    private void OnValidate()
    {
        CacheLight();

        baseIntensity = Mathf.Max(0.01f, baseIntensity);
        baseRange = Mathf.Max(0.01f, baseRange);
        flickerSpeed = Mathf.Max(0.1f, flickerSpeed);
        swaySpeed = Mathf.Max(0.1f, swaySpeed);
        swayAmplitude = Mathf.Max(0f, swayAmplitude);
        shadowStrength = Mathf.Clamp01(shadowStrength);
        shadowBias = Mathf.Clamp(shadowBias, 0f, 0.2f);
        shadowNormalBias = Mathf.Clamp(shadowNormalBias, 0f, 0.5f);
        shadowNearPlane = Mathf.Max(0.01f, shadowNearPlane);
        hdrpShadowResolution = Mathf.Max(128, hdrpShadowResolution);
        hdrpNormalBias = Mathf.Clamp01(hdrpNormalBias);
        hdrpSlopeBias = Mathf.Clamp01(hdrpSlopeBias);

        ApplyLightSetup();
    }

    private void LateUpdate()
    {
        if (targetLight == null)
        {
            return;
        }

        ApplyFlicker(Time.time);
    }

    private void CacheLight()
    {
        if (targetLight == null)
        {
            targetLight = searchInChildren ? GetComponentInChildren<Light>(true) : GetComponent<Light>();
        }

        targetHdLight = targetLight != null ? targetLight.GetComponent<HDAdditionalLightData>() : null;
        torchLightReceiver = GetComponent<TorchLightReceiver>();

        if (torchLightReceiver == null && targetLight != null)
        {
            torchLightReceiver = targetLight.GetComponent<TorchLightReceiver>();
        }

        if (torchLightReceiver == null)
        {
            torchLightReceiver = GetComponentInParent<TorchLightReceiver>(true);
        }
    }

    private void ApplyLightSetup()
    {
        ApplyLightMode();
        ApplyShadowing();
    }

    private void CacheInitialState()
    {
        if (targetLight == null || hasCachedState)
        {
            return;
        }

        initialIntensity = targetLight.intensity;
        initialRange = targetLight.range;
        initialColor = targetLight.color;
        initialLocalPosition = targetLight.transform.localPosition;
        hasCachedState = true;

        if (useCurrentLightAsBase)
        {
            baseIntensity = Mathf.Max(0.01f, initialIntensity);
            baseRange = Mathf.Max(0.01f, initialRange);
            flameColor = initialColor;
        }
    }

    private void InitializeNoiseSeeds()
    {
        int seed = Mathf.Abs(GetInstanceID()) + 1;
        noiseSeedA = (seed * 0.173f) + 3.1f;
        noiseSeedB = (seed * 0.317f) + 11.7f;
        noiseSeedC = (seed * 0.521f) + 19.4f;
        noiseSeedD = (seed * 0.719f) + 29.3f;
        noiseSeedE = (seed * 0.947f) + 37.9f;
    }

    private void ApplyLightMode()
    {
        if (targetLight == null)
        {
            return;
        }

        if (forcePointLight)
        {
            targetLight.type = LightType.Point;
        }
    }

    private void ApplyShadowing()
    {
        if (targetLight == null || !configureCandleShadowing)
        {
            return;
        }

        if (deferShadowingToTorchReceiver && torchLightReceiver != null && torchLightReceiver.ControlsShadowing)
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

    private void ApplyFlicker(float timeValue)
    {
        float primary = SampleSignedNoise(noiseSeedA, timeValue, flickerSpeed);
        float secondary = SampleSignedNoise(noiseSeedB, timeValue, flickerSpeed * 1.91f);
        float turbulence = SampleSignedNoise(noiseSeedC, timeValue, flickerSpeed * 3.67f);
        float burst = Mathf.Clamp01((turbulence * 0.5f) + 0.5f);
        burst *= burst;

        // Blend smooth noise with a sharper turbulence term so the light feels alive without strobing.
        float flicker = (primary * 0.65f) + (secondary * 0.35f) - (burst * burstStrength);
        float normalizedHeat = Mathf.Clamp01(0.5f + (flicker * 0.8f));
        Color drivenFlameColor = GetDrivenFlameColor();
        Color drivenEmberColor = Color.Lerp(emberColor, drivenFlameColor, 0.35f);

        targetLight.intensity = Mathf.Max(0.01f, baseIntensity * (1f + (flicker * intensityVariation)));
        targetLight.range = Mathf.Max(0.01f, baseRange * (1f + ((primary * 0.6f + secondary * 0.4f) * rangeVariation)));
        targetLight.color = Color.Lerp(drivenEmberColor, drivenFlameColor, Mathf.Lerp(0.5f, normalizedHeat, colorVariation));

        if (!animateLocalPosition)
        {
            return;
        }

        float swayX = SampleSignedNoise(noiseSeedD, timeValue, swaySpeed) * swayAmplitude;
        float swayZ = SampleSignedNoise(noiseSeedE, timeValue, swaySpeed * 1.17f) * swayAmplitude;
        targetLight.transform.localPosition = initialLocalPosition + new Vector3(swayX, 0f, swayZ);
    }

    private void RestoreInitialState()
    {
        if (targetLight == null || !hasCachedState)
        {
            return;
        }

        targetLight.intensity = initialIntensity;
        targetLight.range = initialRange;
        targetLight.color = GetRestoreColor();
        targetLight.transform.localPosition = initialLocalPosition;
    }

    private Color GetDrivenFlameColor()
    {
        if (syncTorchReceiverColor && torchLightReceiver != null)
        {
            return torchLightReceiver.CurrentTorchColor;
        }

        return flameColor;
    }

    private Color GetRestoreColor()
    {
        if (syncTorchReceiverColor && torchLightReceiver != null)
        {
            return torchLightReceiver.CurrentTorchColor;
        }

        return initialColor;
    }

    private static float SampleSignedNoise(float seed, float timeValue, float speed)
    {
        return (Mathf.PerlinNoise(seed, timeValue * speed) * 2f) - 1f;
    }
}
