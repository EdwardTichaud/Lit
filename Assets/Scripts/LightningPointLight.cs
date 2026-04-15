using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class LightningPointLight : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Light targetLight;
    [SerializeField] private bool forcePointLight = true;
    [SerializeField] private bool disableLightWhenIdle = true;
    [SerializeField, Min(0f)] private float idleIntensity;
    [SerializeField, Min(0.01f)] private float idleRange = 20f;
    [SerializeField] private Color idleColor = new Color(0.6f, 0.75f, 1f, 1f);

    [Header("Strike")]
    [SerializeField] private bool stormActive = true;
    [SerializeField] private bool playAutomatically = true;
    [SerializeField] private Vector2 strikeDelaySeconds = new Vector2(4f, 12f);
    [SerializeField] private Vector2Int pulseCount = new Vector2Int(2, 5);
    [SerializeField] private Vector2 pulseOnSeconds = new Vector2(0.035f, 0.11f);
    [SerializeField] private Vector2 pulseOffSeconds = new Vector2(0.025f, 0.16f);
    [SerializeField] private Vector2 strikeIntensity = new Vector2(6500f, 18000f);
    [SerializeField] private Vector2 strikeRange = new Vector2(45f, 95f);
    [SerializeField] private Color coldFlashColor = new Color(0.63f, 0.78f, 1f, 1f);
    [SerializeField] private Color hotFlashColor = new Color(1f, 0.96f, 0.82f, 1f);
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

    private float initialIntensity;
    private float initialRange;
    private Color initialColor;
    private Vector3 initialLocalPosition;
    private bool initialEnabled;
    private bool hasInitialState;
    private float nextStrikeTime;
    private Coroutine strikeRoutine;

    public bool IsStormActive => stormActive;
    public bool IsStriking => strikeRoutine != null;

    private void Reset()
    {
        CacheLight();
        CacheParticleSystems();
        ApplyLightMode();
        CacheInitialState();
        ApplyIdleState();
    }

    private void Awake()
    {
        CacheLight();
        CacheParticleSystems();
        ApplyLightMode();
        CacheInitialState();
    }

    private void OnEnable()
    {
        CacheLight();
        CacheParticleSystems();
        ApplyLightMode();
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
        CacheLight();
        idleIntensity = Mathf.Max(0f, idleIntensity);
        idleRange = Mathf.Max(0.01f, idleRange);
        strikeDelaySeconds = ValidateRange(strikeDelaySeconds, 0f, 0.01f);
        pulseOnSeconds = ValidateRange(pulseOnSeconds, 0.001f, 0.001f);
        pulseOffSeconds = ValidateRange(pulseOffSeconds, 0f, 0.001f);
        strikeIntensity = ValidateRange(strikeIntensity, 0f, 1f);
        strikeRange = ValidateRange(strikeRange, 0.01f, 1f);
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
        if (targetLight == null)
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
            targetLight.transform.localPosition = initialLocalPosition + new Vector3(
                Random.Range(-localPositionJitter.x, localPositionJitter.x),
                Random.Range(-localPositionJitter.y, localPositionJitter.y),
                Random.Range(-localPositionJitter.z, localPositionJitter.z));
        }

        int pulses = Random.Range(pulseCount.x, pulseCount.y + 1);
        float peakIntensity = Random.Range(strikeIntensity.x, strikeIntensity.y);
        float peakRange = Random.Range(strikeRange.x, strikeRange.y);

        for (int i = 0; i < pulses; i++)
        {
            float pulseStrength = i == 0
                ? 1f
                : Random.Range(secondaryPulseStrength * 0.45f, secondaryPulseStrength);

            ApplyFlash(
                peakIntensity * pulseStrength,
                Mathf.Lerp(idleRange, peakRange, pulseStrength),
                Color.Lerp(coldFlashColor, hotFlashColor, Random.value));

            yield return WaitSeconds(Random.Range(pulseOnSeconds.x, pulseOnSeconds.y));

            ApplyIdleState();

            if (i < pulses - 1)
            {
                yield return WaitSeconds(Random.Range(pulseOffSeconds.x, pulseOffSeconds.y));
            }
        }

        if (afterglowSeconds > 0f)
        {
            yield return FadeToIdle(peakIntensity * 0.12f, peakRange * 0.65f, afterglowSeconds);
        }

        if (restoreLocalPositionAfterStrike)
        {
            targetLight.transform.localPosition = initialLocalPosition;
        }

        ApplyIdleState();
        strikeRoutine = null;
        ScheduleNextStrike();
    }

    private IEnumerator FadeToIdle(float startIntensity, float startRange, float duration)
    {
        float elapsed = 0f;
        Color startColor = Color.Lerp(coldFlashColor, idleColor, 0.45f);

        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - ((1f - t) * (1f - t));

            ApplyFlash(
                Mathf.Lerp(startIntensity, idleIntensity, eased),
                Mathf.Lerp(startRange, idleRange, eased),
                Color.Lerp(startColor, idleColor, eased),
                false);

            yield return null;
        }
    }

    private void CacheLight()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
        }
    }

    private void CacheInitialState()
    {
        if (targetLight == null || hasInitialState)
        {
            return;
        }

        initialEnabled = targetLight.enabled;
        initialIntensity = targetLight.intensity;
        initialRange = targetLight.range;
        initialColor = targetLight.color;
        initialLocalPosition = targetLight.transform.localPosition;
        hasInitialState = true;
    }

    private void ApplyLightMode()
    {
        if (targetLight != null && forcePointLight)
        {
            targetLight.type = LightType.Point;
        }
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

    private void ApplyFlash(float intensity, float range, Color color, bool triggerParticlesOnIntensityIncrease = true)
    {
        float previousIntensity = targetLight.intensity;
        float nextIntensity = Mathf.Max(0f, intensity);

        targetLight.enabled = true;
        targetLight.intensity = nextIntensity;
        targetLight.range = Mathf.Max(0.01f, range);
        targetLight.color = color;

        if (triggerParticlesOnIntensityIncrease && nextIntensity > previousIntensity + particleTriggerIntensityThreshold)
        {
            TriggerLightningParticles();
        }
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
        if (targetLight == null)
        {
            return;
        }

        targetLight.intensity = idleIntensity;
        targetLight.range = idleRange;
        targetLight.color = idleColor;
        targetLight.enabled = !disableLightWhenIdle || idleIntensity > 0.001f;
    }

    private void RestoreInitialState()
    {
        if (targetLight == null || !hasInitialState)
        {
            return;
        }

        targetLight.enabled = initialEnabled;
        targetLight.intensity = initialIntensity;
        targetLight.range = initialRange;
        targetLight.color = initialColor;
        targetLight.transform.localPosition = initialLocalPosition;
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
