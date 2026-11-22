using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightningPointLight : MonoBehaviour
{
    [Header("Lights (configure each Light component in Inspector)")]
    [SerializeField] private Light[] targetLights = new Light[0];
    [SerializeField] private bool forcePointLight = false;
    [SerializeField] private bool disableLightWhenIdle = true;
    [Tooltip("0 = off, 100 = each Light's configured intensity.")]
    [SerializeField, Min(0f)] private float idleIntensityPercent = 0f;
    [Tooltip("100 = each Light's configured range.")]
    [SerializeField, Min(0f)] private float idleRangePercent = 100f;

    [Header("Strike")]
    [SerializeField] private bool stormActive = true;
    [SerializeField] private bool playAutomatically = true;
    [SerializeField] private Vector2 strikeDelaySeconds = new Vector2(4f, 12f);
    [SerializeField] private Vector2Int pulseCount = new Vector2Int(2, 5);
    [SerializeField] private Vector2 pulseOnSeconds = new Vector2(0.035f, 0.11f);
    [SerializeField] private Vector2 pulseOffSeconds = new Vector2(0.025f, 0.16f);
    [Tooltip("Random peak intensity, as a percentage of each Light's configured intensity. Values above 100 are supported.")]
    [SerializeField] private Vector2 strikeIntensityPercent = new Vector2(100f, 150f);
    [Tooltip("Random peak range, as a percentage of each Light's configured range.")]
    [SerializeField] private Vector2 strikeRangePercent = new Vector2(100f, 100f);
    [SerializeField, Range(0f, 1f)] private float secondaryPulseStrength = 0.55f;
    [SerializeField, Min(0f)] private float afterglowSeconds = 0.18f;

    [Header("Visual Effect")]
    [SerializeField] private Transform particleRoot;
    [SerializeField] private bool autoCollectParticleSystems = true;
    [SerializeField] private bool includeInactiveParticleSystems = true;
    [SerializeField] private bool restartParticlesOnPulse = true;
    [SerializeField, Min(0f)] private float particleTriggerIntensityThreshold = 0.001f;
    [SerializeField] private ParticleSystem[] lightningParticles = new ParticleSystem[0];

    [Header("Position")]
    [SerializeField] private bool randomizeLocalPosition;
    [SerializeField] private Vector3 localPositionJitter = new Vector3(12f, 4f, 12f);
    [SerializeField] private bool restoreLocalPositionAfterStrike = true;

    [Header("Timing")]
    [SerializeField] private bool useUnscaledTime;

    private struct LightInitialState
    {
        public Light light;
        public bool enabled;
        public float intensity;
        public float range;
        public Color color;
        public Vector3 localPosition;
    }

    private readonly List<LightInitialState> initialStates = new List<LightInitialState>();
    private float nextStrikeTime;
    private Coroutine strikeRoutine;

    public bool IsStormActive => stormActive;
    public bool IsStriking => strikeRoutine != null;

    private void Reset()
    {
        CacheLights();
        CacheParticleSystems();
        ApplyLightMode();
        CacheInitialState();
        // Do not overwrite the Light settings in the editor.
    }

    private void Awake()
    {
        CacheLights();
        CacheParticleSystems();
        ApplyLightMode();
        CacheInitialState();
    }

    private void OnEnable()
    {
        CacheLights();
        CacheParticleSystems();
        ApplyLightMode();
        initialStates.Clear(); // Refresh each Light baseline when the component is enabled.
        CacheInitialState();
        ApplyIdleState();
        ScheduleNextStrike();
    }

    private void OnDisable()
    {
        if (strikeRoutine != null)
        {
            StopCoroutine(strikeRoutine);
            strikeRoutine = null;
        }

        RestoreInitialState();
    }

    private void OnValidate()
    {
        CacheLights();
        idleIntensityPercent = Mathf.Max(0f, idleIntensityPercent);
        idleRangePercent = Mathf.Max(0f, idleRangePercent);
        strikeDelaySeconds = ValidateRange(strikeDelaySeconds, 0f, 0.01f);
        pulseOnSeconds = ValidateRange(pulseOnSeconds, 0.001f, 0.001f);
        pulseOffSeconds = ValidateRange(pulseOffSeconds, 0f, 0.001f);
        strikeIntensityPercent = ValidateRange(strikeIntensityPercent, 0f, 0f);
        strikeRangePercent = ValidateRange(strikeRangePercent, 0f, 0f);
        pulseCount.x = Mathf.Max(1, pulseCount.x);
        pulseCount.y = Mathf.Max(pulseCount.x, pulseCount.y);
        secondaryPulseStrength = Mathf.Clamp01(secondaryPulseStrength);
        afterglowSeconds = Mathf.Max(0f, afterglowSeconds);
        particleTriggerIntensityThreshold = Mathf.Max(0f, particleTriggerIntensityThreshold);
        localPositionJitter = new Vector3(
            Mathf.Max(0f, localPositionJitter.x),
            Mathf.Max(0f, localPositionJitter.y),
            Mathf.Max(0f, localPositionJitter.z));
        ApplyLightMode();
    }

    private void Update()
    {
        if (!Application.isPlaying || !stormActive || !playAutomatically || strikeRoutine != null)
        {
            return;
        }

        if (GetTime() >= nextStrikeTime)
        {
            StartStrike();
        }
    }

    public void SetStormActive(bool active)
    {
        stormActive = active;

        if (stormActive)
        {
            ScheduleNextStrike();
            return;
        }

        if (strikeRoutine != null)
        {
            StopCoroutine(strikeRoutine);
            strikeRoutine = null;
        }

        ApplyIdleState();
    }

    public void TriggerStrike()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StartStrike();
    }

    private void StartStrike()
    {
        CacheInitialState();
        if (!HasValidLight())
        {
            return;
        }

        if (strikeRoutine != null)
        {
            StopCoroutine(strikeRoutine);
        }

        strikeRoutine = StartCoroutine(StrikeRoutine());
    }

    private IEnumerator StrikeRoutine()
    {
        if (randomizeLocalPosition)
        {
            foreach (LightInitialState state in initialStates)
            {
                if (state.light == null) continue;
                state.light.transform.localPosition = state.localPosition + new Vector3(
                    Random.Range(-localPositionJitter.x, localPositionJitter.x),
                    Random.Range(-localPositionJitter.y, localPositionJitter.y),
                    Random.Range(-localPositionJitter.z, localPositionJitter.z));
            }
        }

        int pulses = Random.Range(pulseCount.x, pulseCount.y + 1);
        float peakIntensityPercent = Random.Range(strikeIntensityPercent.x, strikeIntensityPercent.y);
        float peakRangePercent = Random.Range(strikeRangePercent.x, strikeRangePercent.y);

        for (int i = 0; i < pulses; i++)
        {
            float pulseStrength = i == 0
                ? 1f
                : Random.Range(secondaryPulseStrength * 0.45f, secondaryPulseStrength);

            ApplyFlash(
                peakIntensityPercent * pulseStrength,
                Mathf.Lerp(idleRangePercent, peakRangePercent, pulseStrength));

            yield return WaitSeconds(Random.Range(pulseOnSeconds.x, pulseOnSeconds.y));

            ApplyIdleState();

            if (i < pulses - 1)
            {
                yield return WaitSeconds(Random.Range(pulseOffSeconds.x, pulseOffSeconds.y));
            }
        }

        if (afterglowSeconds > 0f)
        {
            yield return FadeToIdle(peakIntensityPercent * 0.12f, peakRangePercent * 0.65f, afterglowSeconds);
        }

        if (restoreLocalPositionAfterStrike)
        {
            foreach (LightInitialState state in initialStates)
            {
                if (state.light != null)
                    state.light.transform.localPosition = state.localPosition;
            }
        }

        ApplyIdleState();
        strikeRoutine = null;
        ScheduleNextStrike();
    }

    private IEnumerator FadeToIdle(float startIntensityPercent, float startRangePercent, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);

            ApplyFlash(
                Mathf.Lerp(startIntensityPercent, idleIntensityPercent, eased),
                Mathf.Lerp(startRangePercent, idleRangePercent, eased),
                false);

            yield return null;
        }
    }

    private void CacheLights()
    {
        // Use the local Light only as a fallback when no list has been assigned.
        if (targetLights == null || targetLights.Length == 0)
        {
            Light localLight = GetComponent<Light>();
            if (localLight != null)
                targetLights = new Light[] { localLight };
        }
    }

    private bool HasValidLight()
    {
        if (targetLights == null) return false;
        foreach (Light light in targetLights)
            if (light != null) return true;
        return false;
    }

    private void CacheInitialState()
    {
        if (targetLights == null) return;
        foreach (Light light in targetLights)
        {
            if (light == null) continue;
            bool alreadyCached = false;
            foreach (LightInitialState state in initialStates)
            {
                if (state.light == light)
                {
                    alreadyCached = true;
                    break;
                }
            }
            if (alreadyCached) continue;

            initialStates.Add(new LightInitialState
            {
                light = light,
                enabled = light.enabled,
                intensity = light.intensity,
                range = light.range,
                color = light.color,
                localPosition = light.transform.localPosition
            });
        }
    }

    private void ApplyLightMode()
    {
        if (!forcePointLight || targetLights == null) return;
        foreach (Light light in targetLights)
            if (light != null) light.type = LightType.Point;
    }

    private void CacheParticleSystems()
    {
        if (!autoCollectParticleSystems)
        {
            return;
        }

        if (particleRoot == null)
        {
            particleRoot = FindDefaultParticleRoot();
        }

        if (particleRoot == null)
        {
            return;
        }

        ParticleSystem[] foundParticles = particleRoot.GetComponentsInChildren<ParticleSystem>(includeInactiveParticleSystems);
        if (foundParticles.Length == 0)
        {
            return;
        }

        lightningParticles = GetRootParticleSystems(foundParticles);
    }

    private Transform FindDefaultParticleRoot()
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            return null;
        }

        Transform root = FindChildByName(parent, "AngelOfJustice_Lightning");
        if (root != null)
        {
            return root;
        }

        root = FindChildByName(parent, "AngelOfJustice Lightning");
        if (root != null)
        {
            return root;
        }

        root = FindChildByName(parent, "Lightning");
        if (root != null)
        {
            return root;
        }

        return FindChildByName(parent, "Spawn_Lightning");
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform match = FindChildByName(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static ParticleSystem[] GetRootParticleSystems(ParticleSystem[] particles)
    {
        List<ParticleSystem> rootParticles = new List<ParticleSystem>(particles.Length);

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null || HasParticleSystemAncestor(particle.transform))
            {
                continue;
            }

            rootParticles.Add(particle);
        }

        return rootParticles.ToArray();
    }

    private static bool HasParticleSystemAncestor(Transform transformToCheck)
    {
        Transform current = transformToCheck.parent;
        while (current != null)
        {
            if (current.GetComponent<ParticleSystem>() != null)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    // Percentages are relative to each individual Light's Inspector values
    // captured before this component starts controlling them.
    private void ApplyFlash(float intensityPercent, float rangePercent, bool triggerParticlesOnIntensityIncrease = true)
    {
        bool shouldTriggerParticles = false;
        foreach (LightInitialState state in initialStates)
        {
            Light light = state.light;
            if (light == null) continue;

            float nextIntensity = Mathf.Max(0f, state.intensity * intensityPercent * 0.01f);
            if (triggerParticlesOnIntensityIncrease &&
                nextIntensity > light.intensity + particleTriggerIntensityThreshold)
                shouldTriggerParticles = true;

            light.intensity = nextIntensity;
            light.range = Mathf.Max(0.01f, state.range * rangePercent * 0.01f);
            light.color = state.color;
            light.enabled = nextIntensity > 0.001f;
        }

        if (shouldTriggerParticles)
            TriggerLightningParticles();
    }

    private void TriggerLightningParticles()
    {
        if (autoCollectParticleSystems && (lightningParticles == null || lightningParticles.Length == 0))
        {
            CacheParticleSystems();
        }

        if (lightningParticles == null)
        {
            return;
        }

        for (int i = 0; i < lightningParticles.Length; i++)
        {
            ParticleSystem particle = lightningParticles[i];
            if (particle == null)
            {
                continue;
            }

            if (restartParticlesOnPulse)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            particle.Play(true);
        }
    }

    private void ApplyIdleState()
    {
        foreach (LightInitialState state in initialStates)
        {
            Light light = state.light;
            if (light == null) continue;
            light.intensity = Mathf.Max(0f, state.intensity * idleIntensityPercent * 0.01f);
            light.range = Mathf.Max(0.01f, state.range * idleRangePercent * 0.01f);
            light.color = state.color;
            light.enabled = !disableLightWhenIdle || light.intensity > 0.001f;
        }
    }

    private void RestoreInitialState()
    {
        foreach (LightInitialState state in initialStates)
        {
            if (state.light == null) continue;
            state.light.enabled = state.enabled;
            state.light.intensity = state.intensity;
            state.light.range = state.range;
            state.light.color = state.color;
            state.light.transform.localPosition = state.localPosition;
        }
    }

    private void ScheduleNextStrike()
    {
        nextStrikeTime = GetTime() + Random.Range(strikeDelaySeconds.x, strikeDelaySeconds.y);
    }

    private float GetTime()
    {
        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        float endTime = GetTime() + Mathf.Max(0f, seconds);
        while (GetTime() < endTime)
        {
            yield return null;
        }
    }

    private static Vector2 ValidateRange(Vector2 range, float minValue, float fallbackSpan)
    {
        range.x = Mathf.Max(minValue, range.x);
        range.y = Mathf.Max(range.x + fallbackSpan, range.y);
        return range;
    }
}
