using System;
using UnityEngine;

[Serializable]
public sealed class FallSpeedCameraEffect
{
    [SerializeField] private bool effectEnabled = true;
    [SerializeField, Tooltip("Vitesse verticale descendante a partir de laquelle l'effet commence.")]
    private float fallStartSpeed = 4f;
    [SerializeField, Tooltip("Vitesse verticale descendante qui applique l'effet a pleine intensite.")]
    private float fullEffectSpeed = 12f;
    [SerializeField, Tooltip("Vitesse verticale maximale prise en compte pour ignorer les pics de teleportation ou de repositionnement.")]
    private float maxTrackedFallSpeed = 45f;
    [SerializeField, Range(0f, 2f), Tooltip("Multiplicateur d'intensite applique uniquement a la chute.")]
    private float intensityMultiplier = 1f;

    [Header("Camera")]
    [SerializeField] private bool affectFieldOfView = true;
    [SerializeField, Tooltip("Boost de FOV applique pendant la chute a pleine intensite.")]
    private float fieldOfViewBoost = 8f;

    [Header("Volume")]
    [SerializeField] private bool affectVolume = true;
    [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.22f;
    [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.58f;
    [SerializeField, Range(0f, 1f)] private float chromaticAberrationIntensity = 0.06f;

    [Header("Peripheral Blur")]
    [SerializeField] private bool affectPeripheralBlur = true;
    [SerializeField, Range(0f, 1f)] private float peripheralBlurIntensity = 0.42f;
    [SerializeField, Range(0f, 1f)] private float peripheralBlurCenterRadius = 0.28f;
    [SerializeField, Range(0f, 1f)] private float peripheralBlurEdgeStart = 0.44f;
    [SerializeField, Range(0.001f, 0.03f)] private float peripheralBlurSampleStep = 0.008f;
    [SerializeField, Range(2, 12)] private int peripheralBlurSamples = 7;

    public bool EffectEnabled => effectEnabled;
    public float VignetteSmoothness => vignetteSmoothness;
    public float PeripheralBlurCenterRadius => peripheralBlurCenterRadius;
    public float PeripheralBlurEdgeStart => peripheralBlurEdgeStart;
    public float PeripheralBlurSampleStep => peripheralBlurSampleStep;
    public int PeripheralBlurSamples => peripheralBlurSamples;

    public float EvaluateWeight(float fallSpeed)
    {
        if (!effectEnabled || fallSpeed <= 0f || fallSpeed > maxTrackedFallSpeed)
        {
            return 0f;
        }

        return Mathf.Clamp01(Mathf.InverseLerp(fallStartSpeed, fullEffectSpeed, fallSpeed) * intensityMultiplier);
    }

    public float EvaluateFieldOfViewBoost(float weight)
    {
        return affectFieldOfView ? fieldOfViewBoost * Mathf.Clamp01(weight) : 0f;
    }

    public float EvaluateVignetteIntensity(float weight)
    {
        return affectVolume ? vignetteIntensity * Mathf.Clamp01(weight) : 0f;
    }

    public float EvaluateChromaticAberrationIntensity(float weight)
    {
        return affectVolume ? chromaticAberrationIntensity * Mathf.Clamp01(weight) : 0f;
    }

    public float EvaluatePeripheralBlurIntensity(float weight)
    {
        return affectPeripheralBlur ? peripheralBlurIntensity * Mathf.Clamp01(weight) : 0f;
    }

    public void Validate()
    {
        fallStartSpeed = Mathf.Max(0f, fallStartSpeed);
        fullEffectSpeed = Mathf.Max(fallStartSpeed + 0.01f, fullEffectSpeed);
        maxTrackedFallSpeed = Mathf.Max(fullEffectSpeed, maxTrackedFallSpeed);
        intensityMultiplier = Mathf.Clamp(intensityMultiplier, 0f, 2f);
        fieldOfViewBoost = Mathf.Max(0f, fieldOfViewBoost);
        vignetteIntensity = Mathf.Clamp01(vignetteIntensity);
        vignetteSmoothness = Mathf.Clamp(vignetteSmoothness, 0.01f, 1f);
        chromaticAberrationIntensity = Mathf.Clamp01(chromaticAberrationIntensity);
        peripheralBlurIntensity = Mathf.Clamp01(peripheralBlurIntensity);
        peripheralBlurCenterRadius = Mathf.Clamp01(peripheralBlurCenterRadius);
        peripheralBlurEdgeStart = Mathf.Clamp(peripheralBlurEdgeStart, peripheralBlurCenterRadius + 0.01f, 1f);
        peripheralBlurSampleStep = Mathf.Clamp(peripheralBlurSampleStep, 0.001f, 0.03f);
        peripheralBlurSamples = Mathf.Clamp(peripheralBlurSamples, 2, 12);
    }
}
