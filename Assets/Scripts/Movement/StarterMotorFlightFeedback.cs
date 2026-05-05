using UnityEngine;

[DefaultExecutionOrder(150)]
[RequireComponent(typeof(StarterInspiredThirdPersonMotor))]
public sealed class StarterMotorFlightFeedback : MonoBehaviour
{
    private const string FlightRootName = "Flight";
    private const string FlightAudioName = "Flight Audio";
    private const string FlightTrailLeftName = "Flight Trail L";
    private const string FlightTrailRightName = "Flight Trail R";
    private const string FlightSpeedLinesName = "Flight Speed Lines";
    private const string FlightBoostBurstName = "Flight Boost Burst";
    private const float VfxMoveStartSpeed = 0.35f;
    private const float VfxMoveStopSpeed = 0.12f;

    [Header("References")]
    [SerializeField] private StarterInspiredThirdPersonMotor motor;
    [SerializeField] private AudioSource loopAudioSource;
    [SerializeField] private AudioSource burstAudioSource;
    [SerializeField] private ParticleSystem speedLineParticles;
    [SerializeField] private ParticleSystem boostBurstParticles;
    [SerializeField] private TrailRenderer leftTrail;
    [SerializeField] private TrailRenderer rightTrail;

    [Header("Audio")]
    [SerializeField] private AudioClip flightLoopClip;
    [SerializeField] private AudioClip flightBurstClip;
    [SerializeField, Range(0f, 1f)] private float loopMinVolume = 0.08f;
    [SerializeField, Range(0f, 1f)] private float loopMaxVolume = 0.34f;
    [SerializeField, Range(0.1f, 3f)] private float loopMinPitch = 0.8f;
    [SerializeField, Range(0.1f, 3f)] private float loopMaxPitch = 1.65f;
    [SerializeField, Range(0f, 1f)] private float burstVolume = 0.55f;

    [Header("VFX")]
    [SerializeField] private Color baseColor = new Color(0.45f, 0.9f, 1f, 0.65f);
    [SerializeField] private Color boostColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField, Min(0f)] private float minParticleRate = 10f;
    [SerializeField, Min(0f)] private float maxParticleRate = 115f;
    [SerializeField, Min(0f)] private float minTrailTime = 0.08f;
    [SerializeField, Min(0f)] private float maxTrailTime = 0.34f;
    [SerializeField, Min(0f)] private float minTrailWidth = 0.015f;
    [SerializeField, Min(0f)] private float maxTrailWidth = 0.115f;
    [SerializeField, Min(0)] private int boostBurstParticleCount = 42;

    private Material runtimeMaterial;
    private AudioClip generatedLoopClip;
    private AudioClip generatedBurstClip;
    private bool wasFlightActive;
    private bool vfxMoving;

    private void Reset()
    {
        motor = GetComponent<StarterInspiredThirdPersonMotor>();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureFeedbackObjects();
        SetFeedbackActive(false, clearTrails: true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureFeedbackObjects();
    }

    private void LateUpdate()
    {
        if (motor == null)
        {
            ResolveReferences();
            if (motor == null)
            {
                return;
            }
        }

        bool flightActive = motor.FlightActive;
        bool boosting = motor.FlightBoosting;
        bool boostStarted = motor.FlightBoostStarted;
        float speed01 = motor.FlightNormalizedSpeed;
        float boost01 = Mathf.Clamp01(motor.FlightBoostAmount);

        UpdateAudio(flightActive, boostStarted, speed01, boost01);
        UpdateVfx(flightActive, boosting, boostStarted, speed01, boost01);

        wasFlightActive = flightActive;
    }

    private void OnDisable()
    {
        SetFeedbackActive(false, clearTrails: true);
        wasFlightActive = false;
    }

    private void OnDestroy()
    {
        DestroyRuntimeObject(runtimeMaterial);
        DestroyRuntimeObject(generatedLoopClip);
        DestroyRuntimeObject(generatedBurstClip);
    }

    private void ResolveReferences()
    {
        if (motor == null)
        {
            motor = GetComponent<StarterInspiredThirdPersonMotor>();
        }

        Transform feedbackRoot = ResolveFeedbackRoot();

        if (loopAudioSource == null || burstAudioSource == null)
        {
            ResolveAudioSources(feedbackRoot);
        }

        if (leftTrail == null)
        {
            leftTrail = FindNamedComponent<TrailRenderer>(feedbackRoot, FlightTrailLeftName);
        }

        if (rightTrail == null)
        {
            rightTrail = FindNamedComponent<TrailRenderer>(feedbackRoot, FlightTrailRightName);
        }

        if (speedLineParticles == null)
        {
            speedLineParticles = FindNamedComponent<ParticleSystem>(feedbackRoot, FlightSpeedLinesName);
        }

        if (boostBurstParticles == null)
        {
            boostBurstParticles = FindNamedComponent<ParticleSystem>(feedbackRoot, FlightBoostBurstName);
        }
    }

    private void EnsureFeedbackObjects()
    {
        ResolveReferences();

        if (runtimeMaterial == null)
        {
            runtimeMaterial = CreateRuntimeMaterial();
        }

        ConfigureResolvedFeedbackObjects();

        ApplyRuntimeMaterialToRenderers();

        if (generatedLoopClip == null)
        {
            generatedLoopClip = CreateProceduralLoopClip();
        }

        if (generatedBurstClip == null)
        {
            generatedBurstClip = CreateProceduralBurstClip();
        }
    }

    private Transform ResolveFeedbackRoot()
    {
        Transform feedbackRoot = FindDescendant(transform, FlightRootName);
        return feedbackRoot != null ? feedbackRoot : transform;
    }

    private void ResolveAudioSources(Transform feedbackRoot)
    {
        Transform audioTransform = FindDescendant(feedbackRoot, FlightAudioName);
        if (audioTransform == null && feedbackRoot != transform)
        {
            audioTransform = FindDescendant(transform, FlightAudioName);
        }

        if (audioTransform == null)
        {
            return;
        }

        AudioSource[] audioSources = audioTransform.GetComponentsInChildren<AudioSource>(true);
        if (audioSources.Length == 0)
        {
            return;
        }

        if (loopAudioSource == null)
        {
            loopAudioSource = FindLoopAudioSource(audioSources);
        }

        if (burstAudioSource == null)
        {
            burstAudioSource = FindBurstAudioSource(audioSources, loopAudioSource);
        }
    }

    private void ConfigureResolvedFeedbackObjects()
    {
        ConfigureAudioSource(loopAudioSource, true, 28f);

        if (burstAudioSource != loopAudioSource)
        {
            ConfigureAudioSource(burstAudioSource, false, 34f);
        }

        ConfigureTrail(leftTrail);
        ConfigureTrail(rightTrail);
        ConfigureSpeedLineParticles();
        ConfigureBoostBurstParticles();
    }

    private void ConfigureAudioSource(AudioSource source, bool loop, float maxDistance)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = maxDistance;
    }

    private void ConfigureTrail(TrailRenderer trail)
    {
        if (trail == null)
        {
            return;
        }

        trail.emitting = false;
        trail.startColor = baseColor;
        trail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
    }

    private void ConfigureSpeedLineParticles()
    {
        if (speedLineParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = speedLineParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startColor = baseColor;

        ParticleSystem.EmissionModule emission = speedLineParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
    }

    private void ConfigureBoostBurstParticles()
    {
        if (boostBurstParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = boostBurstParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startColor = boostColor;
    }

    private T FindNamedComponent<T>(Transform feedbackRoot, string objectName) where T : Component
    {
        Transform target = FindDescendant(feedbackRoot, objectName);
        if (target == null && feedbackRoot != transform)
        {
            target = FindDescendant(transform, objectName);
        }

        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindDescendant(Transform searchRoot, string objectName)
    {
        if (searchRoot == null)
        {
            return null;
        }

        Transform[] descendants = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i].name == objectName)
            {
                return descendants[i];
            }
        }

        return null;
    }

    private static AudioSource FindLoopAudioSource(AudioSource[] audioSources)
    {
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null && audioSources[i].loop)
            {
                return audioSources[i];
            }
        }

        return audioSources[0];
    }

    private static AudioSource FindBurstAudioSource(AudioSource[] audioSources, AudioSource loopSource)
    {
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null && audioSources[i] != loopSource && !audioSources[i].loop)
            {
                return audioSources[i];
            }
        }

        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null && audioSources[i] != loopSource)
            {
                return audioSources[i];
            }
        }

        return null;
    }

    private void UpdateAudio(bool flightActive, bool boostStarted, float speed01, float boost01)
    {
        if (loopAudioSource == null && burstAudioSource == null)
        {
            return;
        }

        if (!flightActive)
        {
            if (loopAudioSource != null && loopAudioSource.isPlaying)
            {
                loopAudioSource.Stop();
            }

            return;
        }

        if (loopAudioSource != null)
        {
            AudioClip loopClip = flightLoopClip != null ? flightLoopClip : generatedLoopClip;
            if (loopAudioSource.clip != loopClip)
            {
                loopAudioSource.clip = loopClip;
            }

            loopAudioSource.volume = Mathf.Lerp(loopMinVolume, loopMaxVolume, Mathf.Max(speed01, boost01));
            loopAudioSource.pitch = Mathf.Lerp(loopMinPitch, loopMaxPitch, Mathf.Max(speed01, boost01));

            if (!loopAudioSource.isPlaying && loopClip != null)
            {
                loopAudioSource.Play();
            }
        }

        if (burstAudioSource != null && (boostStarted || (flightActive && !wasFlightActive)))
        {
            AudioClip burstClip = flightBurstClip != null ? flightBurstClip : generatedBurstClip;
            if (burstClip != null)
            {
                burstAudioSource.pitch = boostStarted ? 1.12f : 0.92f;
                burstAudioSource.PlayOneShot(burstClip, burstVolume);
            }
        }
    }

    private void UpdateVfx(bool flightActive, bool boosting, bool boostStarted, float speed01, float boost01)
    {
        float movementSpeed = motor != null ? motor.FlightVelocity.magnitude : 0f;
        if (!flightActive)
        {
            vfxMoving = false;
        }
        else if (movementSpeed >= VfxMoveStartSpeed)
        {
            vfxMoving = true;
        }
        else if (movementSpeed <= VfxMoveStopSpeed)
        {
            vfxMoving = false;
        }

        float movementIntensity = vfxMoving ? Mathf.Clamp01(speed01) : 0f;
        float trailIntensity = vfxMoving ? Mathf.Clamp01(Mathf.Max(movementIntensity, boost01)) : 0f;
        Color color = Color.Lerp(baseColor, boostColor, boosting && vfxMoving ? 1f : trailIntensity);

        UpdateTrail(leftTrail, vfxMoving, trailIntensity, color);
        UpdateTrail(rightTrail, vfxMoving, trailIntensity, color);
        UpdateParticles(vfxMoving, movementIntensity, color);
        UpdateBoostBurst(flightActive && vfxMoving && boostStarted, color);
    }

    private void UpdateTrail(TrailRenderer trail, bool moving, float intensity, Color color)
    {
        if (trail == null)
        {
            return;
        }

        bool shouldEmit = moving && intensity > 0.08f;
        if (shouldEmit)
        {
            SetFeedbackGameObjectActive(trail.gameObject, true);
        }

        trail.emitting = shouldEmit;
        trail.time = Mathf.Lerp(minTrailTime, maxTrailTime, intensity);
        trail.widthMultiplier = Mathf.Lerp(minTrailWidth, maxTrailWidth, intensity);
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);

        if (!shouldEmit)
        {
            trail.Clear();
            SetFeedbackGameObjectActive(trail.gameObject, false);
        }
    }

    private void UpdateParticles(bool moving, float intensity, Color color)
    {
        if (speedLineParticles == null)
        {
            return;
        }

        bool shouldPlay = moving && intensity > 0.005f;
        if (shouldPlay)
        {
            SetFeedbackGameObjectActive(speedLineParticles.gameObject, true);
        }

        ParticleSystem.EmissionModule emission = speedLineParticles.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(shouldPlay
            ? Mathf.Lerp(minParticleRate, maxParticleRate, intensity)
            : 0f);

        ParticleSystem.MainModule main = speedLineParticles.main;
        main.startColor = color;

        if (shouldPlay && !speedLineParticles.isPlaying)
        {
            speedLineParticles.Play();
        }
        else if (!shouldPlay && speedLineParticles.isPlaying)
        {
            speedLineParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (!shouldPlay)
        {
            SetFeedbackGameObjectActive(speedLineParticles.gameObject, false);
        }
    }

    private void SetFeedbackActive(bool active, bool clearTrails)
    {
        if (!active)
        {
            vfxMoving = false;
        }

        if (loopAudioSource != null && !active)
        {
            loopAudioSource.Stop();
        }

        if (speedLineParticles != null && !active)
        {
            speedLineParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetFeedbackGameObjectActive(speedLineParticles.gameObject, false);
        }

        if (boostBurstParticles != null && !active)
        {
            boostBurstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetFeedbackGameObjectActive(boostBurstParticles.gameObject, false);
        }

        if (leftTrail != null)
        {
            leftTrail.emitting = active;
            if (clearTrails)
            {
                leftTrail.Clear();
            }

            if (!active)
            {
                SetFeedbackGameObjectActive(leftTrail.gameObject, false);
            }
        }

        if (rightTrail != null)
        {
            rightTrail.emitting = active;
            if (clearTrails)
            {
                rightTrail.Clear();
            }

            if (!active)
            {
                SetFeedbackGameObjectActive(rightTrail.gameObject, false);
            }
        }
    }

    private void UpdateBoostBurst(bool boostStarted, Color color)
    {
        if (boostBurstParticles == null)
        {
            return;
        }

        if (boostStarted)
        {
            SetFeedbackGameObjectActive(boostBurstParticles.gameObject, true);
            ParticleSystem.MainModule main = boostBurstParticles.main;
            main.startColor = color;
            boostBurstParticles.Emit(boostBurstParticleCount);
        }
        else if (!boostBurstParticles.IsAlive(true))
        {
            SetFeedbackGameObjectActive(boostBurstParticles.gameObject, false);
        }
    }

    private static void SetFeedbackGameObjectActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private Material CreateRuntimeMaterial()
    {
        Shader shader = ResolveFlightFeedbackShader();
        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader)
        {
            name = "Runtime Flight Feedback",
            color = baseColor
        };
        ConfigureFlightFeedbackMaterial(material, baseColor);
        return material;
    }

    private void ApplyRuntimeMaterialToRenderers()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (leftTrail != null)
        {
            leftTrail.sharedMaterial = runtimeMaterial;
        }

        if (rightTrail != null)
        {
            rightTrail.sharedMaterial = runtimeMaterial;
        }

        AssignRuntimeMaterial(speedLineParticles);
        AssignRuntimeMaterial(boostBurstParticles);
    }

    private void AssignRuntimeMaterial(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = runtimeMaterial;
        }
    }

    private static Shader ResolveFlightFeedbackShader()
    {
        string[] shaderNames =
        {
            "HDRP/Particles/Unlit",
            "HDRP/Unlit",
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default"
        };

        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader != null)
            {
                return shader;
            }
        }

        return Shader.Find("Standard");
    }

    private static void ConfigureFlightFeedbackMaterial(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetShaderPassEnabled("MOTIONVECTORS", false);
        material.SetShaderPassEnabled("DistortionVectors", false);

        SetFloatIfPresent(material, "_SurfaceType", 1f);
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_BlendMode", 0f);
        SetFloatIfPresent(material, "_SrcBlend", 1f);
        SetFloatIfPresent(material, "_DstBlend", 10f);
        SetFloatIfPresent(material, "_AlphaSrcBlend", 1f);
        SetFloatIfPresent(material, "_AlphaDstBlend", 10f);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        SetFloatIfPresent(material, "_EnableFogOnTransparent", 1f);
        SetFloatIfPresent(material, "_EnableBlendModePreserveSpecularLighting", 1f);
        SetColorIfPresent(material, "_BaseColor", color);
        SetColorIfPresent(material, "_Color", color);
        SetColorIfPresent(material, "_UnlitColor", color);
        SetColorIfPresent(material, "_EmissiveColor", color);
        SetColorIfPresent(material, "_EmissiveColorLDR", color);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static AudioClip CreateProceduralLoopClip()
    {
        const int sampleRate = 44100;
        const float duration = 1.25f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float rumble = Mathf.Sin(Mathf.PI * 2f * 72f * t) * 0.16f;
            float air = Mathf.Sin(Mathf.PI * 2f * 311f * t + Mathf.Sin(t * 29f)) * 0.045f;
            float hiss = Mathf.Sin(t * 2197.17f) * Mathf.Sin(t * 467.31f) * 0.035f;
            samples[i] = (rumble + air + hiss) * 0.45f;
        }

        AudioClip clip = AudioClip.Create("Procedural Flight Wind Loop", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateProceduralBurstClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.42f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float normalizedTime = t / duration;
            float envelope = Mathf.Sin(normalizedTime * Mathf.PI) * Mathf.Exp(-normalizedTime * 1.8f);
            float sweep = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(120f, 760f, normalizedTime) * t);
            float air = Mathf.Sin(t * 3721.13f) * Mathf.Sin(t * 911.7f);
            samples[i] = (sweep * 0.24f + air * 0.08f) * envelope;
        }

        AudioClip clip = AudioClip.Create("Procedural Flight Burst", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void DestroyRuntimeObject(Object runtimeObject)
    {
        if (runtimeObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeObject);
            return;
        }

        DestroyImmediate(runtimeObject);
    }
}
