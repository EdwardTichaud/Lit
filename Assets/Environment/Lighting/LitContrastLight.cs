using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Authoring component for local HDRP lights used to shape contrast without
/// changing the scene-wide exposure or ambient lighting.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light), typeof(HDAdditionalLightData))]
public sealed class LitContrastLight : MonoBehaviour
{
    public enum Role
    {
        MoonBeam,
        Flame,
        MagicOrIceCrystal
    }

    [Header("Intent")]
    [SerializeField] private Role role = Role.Flame;
    [SerializeField, TextArea(2, 3)]
    private string authoringNote = "Local practical light. Keep broad ambient fill in the HDRP Volume, not here.";

    [Header("Light")]
    [SerializeField] private LightType lightType = LightType.Point;
    [SerializeField, ColorUsage(true, true)] private Color lightColor = new(1f, 0.38f, 0.08f, 1f);
    [SerializeField] private LightUnit intensityUnit = LightUnit.Lumen;
    [SerializeField, Min(0f)] private float intensity = 380f;
    [SerializeField, Min(0.01f)] private float range = 5f;
    [SerializeField, Range(1f, 179f)] private float spotAngle = 55f;

    [Header("Contrast and shadows")]
    [SerializeField] private bool castSoftShadows = true;
    [SerializeField, Range(0f, 1f)] private float shadowDimmer = 0.78f;
    [SerializeField, Range(0f, 1f)] private float volumetricShadowDimmer = 0.7f;
    [SerializeField, Range(0f, 16f)] private float volumetricDimmer = 0.7f;

    [Header("Bloom intent")]
    [SerializeField, Tooltip("Documents whether this source is allowed to exceed the HDRP bloom threshold. It does not alter the shared Volume.")]
    private bool bloomEligible = true;
    [SerializeField, Tooltip("Keep this below the castle bloom threshold for non-emissive props such as the sword.")]
    private bool keepAssociatedEmissionBelowBloomThreshold;

    private Light cachedLight;
    private HDAdditionalLightData cachedHdLight;

    public Role LightRole => role;
    public bool BloomEligible => bloomEligible;

    private void Reset() => ApplyRolePreset();
    private void OnEnable() => ApplySettings();
    private void OnValidate() => ApplySettings();

    [ContextMenu("Apply selected role preset")]
    public void ApplyRolePreset()
    {
        switch (role)
        {
            case Role.MoonBeam:
                SetProfile(LightType.Directional, new Color(0.62f, 0.74f, 1f), LightUnit.Lux, 1.1f, 10f, 30f, true, 1f, 0.7f, 1.35f, false, true,
                    "Use only inside the matching local Volume. It sculpts moonlit surfaces; it is not blue ambient fill.");
                break;
            case Role.Flame:
                SetProfile(LightType.Point, new Color(1f, 0.31f, 0.08f), LightUnit.Lumen, 330f, 4.5f, 55f, true, 0.75f, 0.65f, 0.55f, true, false,
                    "Warm practical source. Keep its range short so the arena background stays dark.");
                break;
            case Role.MagicOrIceCrystal:
                SetProfile(LightType.Point, new Color(0.18f, 0.72f, 1f), LightUnit.Lumen, 240f, 4f, 55f, false, 0f, 0f, 0.45f, true, false,
                    "Reserve this cyan, bloom-capable source for magic and ice crystals, never general navigation light.");
                break;
        }

        ApplySettings();
    }

    public void Configure(
        LightType type, Color color, LightUnit unit, float lightIntensity, float lightRange, float angle,
        bool softShadows, float shadows, float volumetricShadows, float volumetric, bool allowBloom, string note)
    {
        SetProfile(type, color, unit, lightIntensity, lightRange, angle, softShadows, shadows, volumetricShadows, volumetric, allowBloom, !allowBloom, note);
        ApplySettings();
    }

    [ContextMenu("Apply current settings")]
    public void ApplySettings()
    {
        ResolveComponents();
        if (cachedLight == null || cachedHdLight == null)
            return;

        cachedLight.type = lightType;
        cachedLight.color = lightColor;
        cachedLight.range = range;
        cachedLight.spotAngle = spotAngle;
        cachedLight.shadows = castSoftShadows ? LightShadows.Soft : LightShadows.None;

        if (LightUnitUtils.IsLightUnitSupported(lightType, intensityUnit))
        {
            cachedLight.lightUnit = intensityUnit;
            cachedLight.intensity = LightUnitUtils.ConvertIntensity(cachedLight, intensity, intensityUnit, LightUnitUtils.GetNativeLightUnit(lightType));
        }

        cachedHdLight.SetLightDimmer(1f, volumetricDimmer);
        cachedHdLight.SetShadowDimmer(shadowDimmer, volumetricShadowDimmer);
    }

    private void SetProfile(LightType type, Color color, LightUnit unit, float lightIntensity, float lightRange, float angle,
        bool softShadows, float shadows, float volumetricShadows, float volumetric, bool allowBloom, bool keepBelowBloom, string note)
    {
        lightType = type;
        lightColor = color;
        intensityUnit = unit;
        intensity = lightIntensity;
        range = lightRange;
        spotAngle = angle;
        castSoftShadows = softShadows;
        shadowDimmer = shadows;
        volumetricShadowDimmer = volumetricShadows;
        volumetricDimmer = volumetric;
        bloomEligible = allowBloom;
        keepAssociatedEmissionBelowBloomThreshold = keepBelowBloom;
        authoringNote = note;
    }

    private void ResolveComponents()
    {
        if (cachedLight == null) cachedLight = GetComponent<Light>();
        if (cachedHdLight == null) cachedHdLight = GetComponent<HDAdditionalLightData>();
    }
}