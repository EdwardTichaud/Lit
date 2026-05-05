using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable]
public sealed class RunSpeedCameraEffect
{
    [SerializeField] private bool effectEnabled = true;
    [SerializeField, Tooltip("Vitesse horizontale a partir de laquelle l'effet commence.")]
    private float runStartSpeed = 5.2f;
    [SerializeField, Tooltip("Vitesse horizontale qui applique l'effet a pleine intensite.")]
    private float fullEffectSpeed = 6.5f;
    [SerializeField, Tooltip("Vitesse maximale prise en compte pour ignorer les pics de teleportation ou de repositionnement.")]
    private float maxTrackedSpeed = 12f;
    [SerializeField] private float appearSharpness = 8f;
    [SerializeField] private float disappearSharpness = 5f;

    [Header("Camera")]
    [SerializeField] private bool affectFieldOfView = true;
    [SerializeField] private float fieldOfViewBoost = 5.5f;

    [Header("Volume")]
    [SerializeField] private bool createRuntimeVolume = true;
    [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.18f;
    [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.52f;
    [SerializeField, Range(0f, 1f)] private float chromaticAberrationIntensity = 0.045f;

    [Header("Peripheral Blur")]
    [SerializeField, Range(0f, 1f)] private float peripheralBlurIntensity = 0.34f;
    [SerializeField, Range(0f, 1f)] private float peripheralBlurCenterRadius = 0.32f;
    [SerializeField, Range(0f, 1f)] private float peripheralBlurEdgeStart = 0.48f;
    [SerializeField, Range(0.001f, 0.03f)] private float peripheralBlurSampleStep = 0.006f;
    [SerializeField, Range(2, 12)] private int peripheralBlurSamples = 6;

    private Camera cachedCamera;
    private float baseFieldOfView;
    private bool hasBaseFieldOfView;
    private float currentWeight;
    private float currentRunWeight;
    private float currentFallWeight;
    private float currentFieldOfViewBoost;

    private Transform trackedTarget;
    private Vector3 lastTargetPosition;
    private bool hasLastTargetPosition;

    private GameObject runtimeVolumeObject;
    private Volume runtimeVolume;
    private VolumeProfile runtimeProfile;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private RunSpeedPeripheralBlur peripheralBlur;

    public void Initialize(Camera camera)
    {
        cachedCamera = camera;
        CaptureBaseFieldOfView(camera);

        if (!createRuntimeVolume || camera == null)
        {
            return;
        }

        EnsureRuntimeVolume();
        ApplyVolumeSettings(0f, 0f, null, createIfMissing: true);
    }

    public void UpdateEffect(Camera camera, Transform gameplayTarget, float deltaTime, FallSpeedCameraEffect fallSpeedEffect)
    {
        if (camera != null && camera != cachedCamera)
        {
            cachedCamera = camera;
            CaptureBaseFieldOfView(camera);
        }

        if (!effectEnabled && (fallSpeedEffect == null || !fallSpeedEffect.EffectEnabled))
        {
            ResetEffect(camera);
            return;
        }

        Initialize(camera);

        ResolveMovementSpeeds(gameplayTarget, deltaTime, out float runSpeed, out float fallSpeed);
        float targetRunWeight = effectEnabled ? Mathf.InverseLerp(runStartSpeed, fullEffectSpeed, runSpeed) : 0f;
        float targetFallWeight = fallSpeedEffect != null ? fallSpeedEffect.EvaluateWeight(fallSpeed) : 0f;
        currentRunWeight = SmoothWeight(currentRunWeight, targetRunWeight, deltaTime);
        currentFallWeight = SmoothWeight(currentFallWeight, targetFallWeight, deltaTime);
        currentWeight = Mathf.Max(currentRunWeight, currentFallWeight);

        float runFieldOfViewBoost = affectFieldOfView ? fieldOfViewBoost * currentRunWeight : 0f;
        float fallFieldOfViewBoost = fallSpeedEffect != null ? fallSpeedEffect.EvaluateFieldOfViewBoost(currentFallWeight) : 0f;
        currentFieldOfViewBoost = Mathf.Max(runFieldOfViewBoost, fallFieldOfViewBoost);

        ApplyCameraSettings(camera, currentFieldOfViewBoost);
        ApplyVolumeSettings(currentRunWeight, currentFallWeight, fallSpeedEffect, createIfMissing: true);
    }

    public void ResetEffect(Camera camera)
    {
        currentWeight = 0f;
        currentRunWeight = 0f;
        currentFallWeight = 0f;
        currentFieldOfViewBoost = 0f;
        trackedTarget = null;
        hasLastTargetPosition = false;

        if (camera != null && hasBaseFieldOfView)
        {
            camera.fieldOfView = baseFieldOfView;
        }

        ApplyVolumeSettings(0f, 0f, null, createIfMissing: false);
    }

    public void Cleanup(Camera camera)
    {
        ResetEffect(camera);

        DestroyObject(runtimeVolumeObject);
        DestroyObject(runtimeProfile);

        runtimeVolumeObject = null;
        runtimeVolume = null;
        runtimeProfile = null;
        vignette = null;
        chromaticAberration = null;
        peripheralBlur = null;
        cachedCamera = null;
        hasBaseFieldOfView = false;
    }

    public void Validate()
    {
        runStartSpeed = Mathf.Max(0f, runStartSpeed);
        fullEffectSpeed = Mathf.Max(runStartSpeed + 0.01f, fullEffectSpeed);
        maxTrackedSpeed = Mathf.Max(fullEffectSpeed, maxTrackedSpeed);
        appearSharpness = Mathf.Max(0f, appearSharpness);
        disappearSharpness = Mathf.Max(0f, disappearSharpness);
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

    private void CaptureBaseFieldOfView(Camera camera)
    {
        if (camera == null || hasBaseFieldOfView)
        {
            return;
        }

        baseFieldOfView = camera.fieldOfView;
        hasBaseFieldOfView = true;
    }

    private float SmoothWeight(float current, float target, float deltaTime)
    {
        float sharpness = target > current ? appearSharpness : disappearSharpness;
        float t = sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * deltaTime);
        return Mathf.Lerp(current, target, t);
    }

    private void ResolveMovementSpeeds(Transform target, float deltaTime, out float horizontalSpeed, out float fallSpeed)
    {
        horizontalSpeed = 0f;
        fallSpeed = 0f;

        if (target == null || deltaTime <= 0f)
        {
            trackedTarget = null;
            hasLastTargetPosition = false;
            return;
        }

        StarterInspiredThirdPersonMotor flightMotor = target.GetComponent<StarterInspiredThirdPersonMotor>();
        if (flightMotor != null && flightMotor.FlightActive)
        {
            horizontalSpeed = Mathf.Min(flightMotor.FlightSpeed, maxTrackedSpeed);
            trackedTarget = target;
            lastTargetPosition = target.position;
            hasLastTargetPosition = true;
            return;
        }

        if (target != trackedTarget)
        {
            trackedTarget = target;
            lastTargetPosition = target.position;
            hasLastTargetPosition = true;
            return;
        }

        if (!hasLastTargetPosition)
        {
            lastTargetPosition = target.position;
            hasLastTargetPosition = true;
            return;
        }

        Vector3 delta = target.position - lastTargetPosition;
        lastTargetPosition = target.position;

        Vector3 horizontalDelta = Vector3.ProjectOnPlane(delta, Vector3.up);
        float resolvedHorizontalSpeed = horizontalDelta.magnitude / deltaTime;
        horizontalSpeed = resolvedHorizontalSpeed > maxTrackedSpeed ? 0f : resolvedHorizontalSpeed;

        float verticalSpeed = Vector3.Dot(delta / deltaTime, Vector3.up);
        fallSpeed = Mathf.Max(0f, -verticalSpeed);
    }

    private void ApplyCameraSettings(Camera camera, float fieldOfViewBoostOffset)
    {
        if (camera == null || !hasBaseFieldOfView)
        {
            return;
        }

        camera.fieldOfView = baseFieldOfView + Mathf.Max(0f, fieldOfViewBoostOffset);
    }

    private void EnsureRuntimeVolume()
    {
        if (runtimeVolume != null && runtimeProfile != null)
        {
            return;
        }

        runtimeVolumeObject = new GameObject("Run Speed Camera Volume");
        runtimeVolumeObject.hideFlags = HideFlags.HideAndDontSave;
        runtimeVolume = runtimeVolumeObject.AddComponent<Volume>();
        runtimeVolume.isGlobal = true;
        runtimeVolume.priority = 900f;
        runtimeVolume.weight = 1f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
        runtimeVolume.sharedProfile = runtimeProfile;

        vignette = runtimeProfile.Add<Vignette>(true);
        vignette.mode.overrideState = true;
        vignette.mode.value = VignetteMode.Procedural;
        vignette.color.overrideState = true;
        vignette.color.value = Color.black;
        vignette.center.overrideState = true;
        vignette.center.value = new Vector2(0.5f, 0.5f);
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        vignette.roundness.overrideState = true;
        vignette.roundness.value = 1f;
        vignette.rounded.overrideState = true;
        vignette.rounded.value = false;

        chromaticAberration = runtimeProfile.Add<ChromaticAberration>(true);
        chromaticAberration.intensity.overrideState = true;
        chromaticAberration.maxSamples = 6;

        peripheralBlur = runtimeProfile.Add<RunSpeedPeripheralBlur>(true);
        peripheralBlur.intensity.overrideState = true;
        peripheralBlur.centerRadius.overrideState = true;
        peripheralBlur.edgeStart.overrideState = true;
        peripheralBlur.sampleStep.overrideState = true;
        peripheralBlur.samples.overrideState = true;
    }

    private void ApplyVolumeSettings(float runWeight, float fallWeight, FallSpeedCameraEffect fallSpeedEffect, bool createIfMissing)
    {
        if (!createRuntimeVolume)
        {
            return;
        }

        if (createIfMissing)
        {
            EnsureRuntimeVolume();
        }
        else if (runtimeVolume == null || runtimeProfile == null)
        {
            return;
        }

        if (runtimeVolume != null)
        {
            runtimeVolume.weight = 1f;
        }

        if (vignette != null)
        {
            float runVignetteIntensity = vignetteIntensity * runWeight;
            float fallVignetteIntensity = fallSpeedEffect != null ? fallSpeedEffect.EvaluateVignetteIntensity(fallWeight) : 0f;
            bool useFallVignetteSettings = fallVignetteIntensity > runVignetteIntensity;
            vignette.intensity.value = Mathf.Max(runVignetteIntensity, fallVignetteIntensity);
            vignette.smoothness.value = useFallVignetteSettings && fallSpeedEffect != null
                ? fallSpeedEffect.VignetteSmoothness
                : vignetteSmoothness;
        }

        if (chromaticAberration != null)
        {
            float runChromaticAberrationIntensity = chromaticAberrationIntensity * runWeight;
            float fallChromaticAberrationIntensity = fallSpeedEffect != null ? fallSpeedEffect.EvaluateChromaticAberrationIntensity(fallWeight) : 0f;
            chromaticAberration.intensity.value = Mathf.Max(runChromaticAberrationIntensity, fallChromaticAberrationIntensity);
        }

        if (peripheralBlur != null)
        {
            float runPeripheralBlurIntensity = peripheralBlurIntensity * runWeight;
            float fallPeripheralBlurIntensity = fallSpeedEffect != null ? fallSpeedEffect.EvaluatePeripheralBlurIntensity(fallWeight) : 0f;
            bool useFallPeripheralBlurSettings = fallPeripheralBlurIntensity > runPeripheralBlurIntensity && fallSpeedEffect != null;
            peripheralBlur.intensity.value = Mathf.Max(runPeripheralBlurIntensity, fallPeripheralBlurIntensity);
            peripheralBlur.centerRadius.value = useFallPeripheralBlurSettings ? fallSpeedEffect.PeripheralBlurCenterRadius : peripheralBlurCenterRadius;
            peripheralBlur.edgeStart.value = useFallPeripheralBlurSettings ? fallSpeedEffect.PeripheralBlurEdgeStart : peripheralBlurEdgeStart;
            peripheralBlur.sampleStep.value = useFallPeripheralBlurSettings ? fallSpeedEffect.PeripheralBlurSampleStep : peripheralBlurSampleStep;
            peripheralBlur.samples.value = useFallPeripheralBlurSettings ? fallSpeedEffect.PeripheralBlurSamples : peripheralBlurSamples;
        }
    }

    private static void DestroyObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
