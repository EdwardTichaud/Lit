using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class SceneLightOcclusionEnforcer : MonoBehaviour
{
    [Header("Runtime")]
    [SerializeField] private bool enforceOnEnable;
    [SerializeField, Tooltip("Laisse desactive pour eviter que l'inspecteur modifie les lights/renderers en edit mode.")]
    private bool enforceInEditMode = false;
    [SerializeField, Tooltip("Desactive par defaut: scanner toutes les lumieres et tous les renderers en continu est couteux. Active seulement pour du debug ou des scenes qui spawnent du decor/lumieres dynamiques.")]
    private bool enforceContinuously = false;
    [SerializeField, Min(0.1f)] private float refreshInterval = 1f;
    [SerializeField] private bool includeInactiveLights = true;

    [Header("Light Shadows")]
    [SerializeField] private bool configureLightShadows = true;
    [SerializeField, Tooltip("Si desactive, ce composant n'allume jamais les ombres d'une light qui les avait coupees. Il ajuste seulement les lights deja shadowed.")]
    private bool enableMissingLightShadows = false;
    [SerializeField, Min(0)] private int maxMissingLightShadowsEnabledPerPass = 12;
    [SerializeField] private LayerMask lightShadowLayers = ~0;
    [SerializeField] private bool includePointLights = true;
    [SerializeField] private bool includeSpotLights = true;
    [SerializeField] private bool includeDirectionalLights = false;
    [SerializeField] private bool includeAreaLights = false;
    [SerializeField] private bool skipBakedLights = true;
    [SerializeField] private LightShadows shadowModeWhenMissing = LightShadows.Soft;
    [SerializeField] private bool forcePixelRenderMode = true;
    [SerializeField, Range(0f, 1f)] private float shadowStrength = 1f;
    [SerializeField, Range(0f, 0.2f)] private float maxShadowBias = 0.005f;
    [SerializeField, Range(0f, 0.5f)] private float maxShadowNormalBias = 0.03f;
    [SerializeField, Min(0.01f)] private float shadowNearPlane = 0.02f;

    [Header("HDRP")]
    [SerializeField, Min(128)] private int hdrpShadowResolution = 1024;
    [SerializeField, Range(0f, 1f)] private float hdrpNormalBias = 0.03f;
    [SerializeField, Range(0f, 1f)] private float hdrpSlopeBias = 0.1f;
    [SerializeField] private bool enableHdrpContactShadows;

    [Header("Wall Shadow Casters")]
    [SerializeField, Tooltip("Force les gros renderers de decor a caster des ombres pour bloquer les lumieres a travers les murs.")]
    private bool enforceWallShadowCasters = true;
    [SerializeField] private LayerMask wallShadowCasterLayers = 1 | (1 << 3) | (1 << 7) | (1 << 9);
    [SerializeField] private bool includeInactiveRenderers = false;

    [Header("Debug")]
    [SerializeField] private bool logChanges = false;

    private float nextRefreshTime;

    private void OnEnable()
    {
        ValidateFields();
        if (enforceOnEnable)
        {
            EnforceNow();
        }

        nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !enforceContinuously)
        {
            return;
        }

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        EnforceNow();
        nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    private void OnValidate()
    {
        ValidateFields();
        if (!Application.isPlaying && enforceInEditMode && enforceOnEnable)
        {
            EnforceNow();
        }
    }

    public void EnforceNow()
    {
        ConfigureSceneLights();
        if (enforceWallShadowCasters)
        {
            ConfigureSceneShadowCasters();
        }
    }

    private void ConfigureSceneLights()
    {
        if (!configureLightShadows)
        {
            return;
        }

        FindObjectsInactive inactiveMode = includeInactiveLights ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        Light[] sceneLights = Object.FindObjectsByType<Light>(inactiveMode);
        int missingShadowsEnabled = 0;
        for (int i = 0; i < sceneLights.Length; i++)
        {
            ConfigureLight(sceneLights[i], ref missingShadowsEnabled);
        }
    }

    private void ConfigureLight(Light targetLight, ref int missingShadowsEnabled)
    {
        if (targetLight == null || !ShouldConfigureLight(targetLight))
        {
            return;
        }

        bool changed = false;
        LightShadows resolvedShadowMode = shadowModeWhenMissing == LightShadows.None
            ? LightShadows.Soft
            : shadowModeWhenMissing;

        if (targetLight.shadows == LightShadows.None)
        {
            if (!CanEnableMissingShadows(ref missingShadowsEnabled))
            {
                return;
            }

            targetLight.shadows = resolvedShadowMode;
            changed = true;
        }

        if (forcePixelRenderMode && targetLight.renderMode != LightRenderMode.ForcePixel)
        {
            targetLight.renderMode = LightRenderMode.ForcePixel;
            changed = true;
        }

        if (!Mathf.Approximately(targetLight.shadowStrength, shadowStrength))
        {
            targetLight.shadowStrength = shadowStrength;
            changed = true;
        }

        if (targetLight.shadowBias > maxShadowBias)
        {
            targetLight.shadowBias = maxShadowBias;
            changed = true;
        }

        if (targetLight.shadowNormalBias > maxShadowNormalBias)
        {
            targetLight.shadowNormalBias = maxShadowNormalBias;
            changed = true;
        }

        if (targetLight.shadowNearPlane > shadowNearPlane)
        {
            targetLight.shadowNearPlane = shadowNearPlane;
            changed = true;
        }

        HDAdditionalLightData hdLight = targetLight.GetComponent<HDAdditionalLightData>();
        if (hdLight != null)
        {
            hdLight.SetShadowResolution(hdrpShadowResolution);
            hdLight.shadowDimmer = 1f;
            hdLight.normalBias = hdrpNormalBias;
            hdLight.slopeBias = hdrpSlopeBias;
            hdLight.useContactShadow.useOverride = true;
            hdLight.useContactShadow.@override = enableHdrpContactShadows;
        }

        if (changed && logChanges)
        {
            Debug.Log($"[LightOcclusion] Shadows enforced on '{targetLight.name}'.", targetLight);
        }
    }

    private bool ShouldConfigureLight(Light targetLight)
    {
        if ((lightShadowLayers.value & (1 << targetLight.gameObject.layer)) == 0)
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

    private bool CanEnableMissingShadows(ref int missingShadowsEnabled)
    {
        if (!enableMissingLightShadows || maxMissingLightShadowsEnabledPerPass < 1)
        {
            return false;
        }

        if (missingShadowsEnabled >= maxMissingLightShadowsEnabledPerPass)
        {
            return false;
        }

        missingShadowsEnabled++;
        return true;
    }

    private void ConfigureSceneShadowCasters()
    {
        FindObjectsInactive inactiveMode = includeInactiveRenderers ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        Renderer[] sceneRenderers = Object.FindObjectsByType<Renderer>(inactiveMode);
        int layerMask = wallShadowCasterLayers.value;
        for (int i = 0; i < sceneRenderers.Length; i++)
        {
            Renderer targetRenderer = sceneRenderers[i];
            if (!ShouldEnforceRendererShadowCasting(targetRenderer, layerMask))
            {
                continue;
            }

            if (targetRenderer.shadowCastingMode == ShadowCastingMode.Off)
            {
                targetRenderer.shadowCastingMode = ShadowCastingMode.On;
            }

            if (!targetRenderer.receiveShadows)
            {
                targetRenderer.receiveShadows = true;
            }
        }
    }

    private static bool ShouldEnforceRendererShadowCasting(Renderer targetRenderer, int layerMask)
    {
        if (targetRenderer == null)
        {
            return false;
        }

        if ((layerMask & (1 << targetRenderer.gameObject.layer)) == 0)
        {
            return false;
        }

        return targetRenderer is MeshRenderer || targetRenderer is SkinnedMeshRenderer;
    }

    private void ValidateFields()
    {
        refreshInterval = Mathf.Max(0.1f, refreshInterval);
        maxMissingLightShadowsEnabledPerPass = Mathf.Max(0, maxMissingLightShadowsEnabledPerPass);
        if (shadowModeWhenMissing == LightShadows.None)
        {
            shadowModeWhenMissing = LightShadows.Soft;
        }

        shadowStrength = Mathf.Clamp01(shadowStrength);
        maxShadowBias = Mathf.Clamp(maxShadowBias, 0f, 0.2f);
        maxShadowNormalBias = Mathf.Clamp(maxShadowNormalBias, 0f, 0.5f);
        shadowNearPlane = Mathf.Max(0.01f, shadowNearPlane);
        hdrpShadowResolution = Mathf.Max(128, hdrpShadowResolution);
        hdrpNormalBias = Mathf.Clamp01(hdrpNormalBias);
        hdrpSlopeBias = Mathf.Clamp01(hdrpSlopeBias);
    }
}
