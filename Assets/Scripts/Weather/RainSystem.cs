using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public sealed class RainSystem : MonoBehaviour
{
    private static readonly List<RainZone> zones = new List<RainZone>();

    [Header("Target")]
    [SerializeField, Tooltip("Cible autour de laquelle la pluie est rendue. Si vide, Camera.main est utilisee.")]
    private Transform target;
    [SerializeField] private bool fallbackToMainCamera = true;
    [SerializeField, Tooltip("Offset applique au systeme de pluie visuelle par rapport a la cible.")]
    private Vector3 rainVolumeOffset = new Vector3(0f, 9f, 0f);

    [Header("Particles")]
    [SerializeField, Tooltip("Systeme de particules des gouttes. Il doit rester sans Collision Module.")]
    private ParticleSystem rainParticleSystem;
    [SerializeField, Tooltip("Systeme de particules des impacts au sol. Un seul systeme emet tous les impacts.")]
    private ParticleSystem splashParticleSystem;
    [SerializeField, Min(0f)] private float maxRainEmissionRate = 900f;
    [SerializeField, Min(0)] private int maxRainParticles = 1800;
    [SerializeField, Tooltip("Desactive les Collision/Trigger Modules des particules controlees au demarrage.")]
    private bool forceDisableParticleCollisionModules = true;

    [Header("Surface Impacts")]
    [SerializeField, Min(0f)] private float splashRadius = 12f;
    [SerializeField, Min(0f)] private float splashCastHeight = 8f;
    [SerializeField, Min(0f)] private float splashCastDistance = 18f;
    [SerializeField, Min(0f)] private float maxSplashRaycastsPerSecond = 55f;
    [SerializeField, Min(1)] private int maxSplashRaycastsPerFrame = 8;
    [SerializeField, Min(0f)] private float splashSurfaceOffset = 0.03f;
    [SerializeField] private LayerMask splashGroundMask = ~0;
    [SerializeField] private QueryTriggerInteraction splashTriggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField, Tooltip("Evite de generer des impacts sur les surfaces couvertes par un toit.")]
    private bool skipCoveredSurfaces = true;
    [SerializeField, Min(0f)] private float coverProbeHeight = 25f;
    [SerializeField] private LayerMask coverMask = ~0;

    [Header("Zones")]
    [SerializeField, Min(0.02f)] private float zonePollInterval = 0.2f;
    [SerializeField, Min(0f)] private float defaultFadeInSeconds = 1f;
    [SerializeField, Min(0f)] private float defaultFadeOutSeconds = 1.5f;

    private readonly RaycastHit[] raycastHits = new RaycastHit[4];
    private readonly List<ParticleSystem> controlledParticleSystems = new List<ParticleSystem>(32);
    private RainZone activeZone;
    private float targetIntensity;
    private float currentIntensity;
    private float targetSplashMultiplier = 1f;
    private float nextZonePollTime;
    private float splashRaycastAccumulator;

    public static void RegisterZone(RainZone zone)
    {
        if (zone != null && !zones.Contains(zone))
        {
            zones.Add(zone);
        }
    }

    public static void UnregisterZone(RainZone zone)
    {
        zones.Remove(zone);
    }

    private void Reset()
    {
        splashGroundMask = BuildDefaultEnvironmentMask();
        coverMask = BuildDefaultEnvironmentMask();
    }

    private void Awake()
    {
        if (forceDisableParticleCollisionModules)
        {
            DisableParticleCollisionModules(rainParticleSystem);
            DisableParticleCollisionModules(splashParticleSystem);
        }

        ApplyParticleBudgets();
    }

    private void OnValidate()
    {
        maxRainEmissionRate = Mathf.Max(0f, maxRainEmissionRate);
        maxRainParticles = Mathf.Max(0, maxRainParticles);
        splashRadius = Mathf.Max(0f, splashRadius);
        splashCastHeight = Mathf.Max(0f, splashCastHeight);
        splashCastDistance = Mathf.Max(0f, splashCastDistance);
        maxSplashRaycastsPerSecond = Mathf.Max(0f, maxSplashRaycastsPerSecond);
        maxSplashRaycastsPerFrame = Mathf.Max(1, maxSplashRaycastsPerFrame);
        splashSurfaceOffset = Mathf.Max(0f, splashSurfaceOffset);
        coverProbeHeight = Mathf.Max(0f, coverProbeHeight);
        zonePollInterval = Mathf.Max(0.02f, zonePollInterval);
        defaultFadeInSeconds = Mathf.Max(0f, defaultFadeInSeconds);
        defaultFadeOutSeconds = Mathf.Max(0f, defaultFadeOutSeconds);
    }

    private void Update()
    {
        Transform resolvedTarget = ResolveTarget();
        float deltaTime = Time.deltaTime;

        if (resolvedTarget != null && Time.time >= nextZonePollTime)
        {
            nextZonePollTime = Time.time + zonePollInterval;
            PollActiveZone(resolvedTarget.position);
        }

        UpdateIntensity(deltaTime);
        UpdateRainParticles(resolvedTarget);
        EmitSurfaceImpacts(resolvedTarget, deltaTime);
    }

    private Transform ResolveTarget()
    {
        if (target != null)
        {
            return target;
        }

        if (!fallbackToMainCamera || Camera.main == null)
        {
            return null;
        }

        target = Camera.main.transform;
        return target;
    }

    private void PollActiveZone(Vector3 worldPosition)
    {
        RainZone bestZone = null;
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            RainZone zone = zones[i];
            if (zone == null)
            {
                zones.RemoveAt(i);
                continue;
            }

            if (!zone.isActiveAndEnabled || !zone.Contains(worldPosition))
            {
                continue;
            }

            if (bestZone == null ||
                zone.Priority > bestZone.Priority ||
                zone.Priority == bestZone.Priority && zone.Intensity > bestZone.Intensity)
            {
                bestZone = zone;
            }
        }

        activeZone = bestZone;
        targetIntensity = activeZone != null ? activeZone.Intensity : 0f;
        targetSplashMultiplier = activeZone != null ? activeZone.SplashMultiplier : 1f;
    }

    private void UpdateIntensity(float deltaTime)
    {
        float fadeSeconds = targetIntensity > currentIntensity
            ? (activeZone != null ? activeZone.FadeInSeconds : defaultFadeInSeconds)
            : (activeZone != null ? activeZone.FadeOutSeconds : defaultFadeOutSeconds);

        if (fadeSeconds <= 0.0001f)
        {
            currentIntensity = targetIntensity;
            return;
        }

        currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, deltaTime / fadeSeconds);
    }

    private void UpdateRainParticles(Transform resolvedTarget)
    {
        if (rainParticleSystem == null)
        {
            return;
        }

        if (resolvedTarget != null)
        {
            rainParticleSystem.transform.position = resolvedTarget.position + rainVolumeOffset;
        }

        ParticleSystem.EmissionModule emission = rainParticleSystem.emission;
        emission.enabled = currentIntensity > 0.001f && maxRainEmissionRate > 0f;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(maxRainEmissionRate * currentIntensity);

        ParticleSystem.MainModule main = rainParticleSystem.main;
        if (maxRainParticles > 0)
        {
            main.maxParticles = maxRainParticles;
        }

        if (currentIntensity > 0.001f)
        {
            if (!rainParticleSystem.isPlaying)
            {
                rainParticleSystem.Play(true);
            }
        }
        else if (rainParticleSystem.isPlaying)
        {
            rainParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void EmitSurfaceImpacts(Transform resolvedTarget, float deltaTime)
    {
        if (resolvedTarget == null ||
            splashParticleSystem == null ||
            currentIntensity <= 0.001f ||
            maxSplashRaycastsPerSecond <= 0f ||
            splashRadius <= 0f)
        {
            return;
        }

        splashRaycastAccumulator += maxSplashRaycastsPerSecond * currentIntensity * targetSplashMultiplier * deltaTime;
        int budget = Mathf.Min(maxSplashRaycastsPerFrame, Mathf.FloorToInt(splashRaycastAccumulator));
        if (budget <= 0)
        {
            return;
        }

        splashRaycastAccumulator -= budget;

        Vector3 targetPosition = resolvedTarget.position;
        for (int i = 0; i < budget; i++)
        {
            Vector2 random = Random.insideUnitCircle * splashRadius;
            Vector3 samplePosition = new Vector3(
                targetPosition.x + random.x,
                targetPosition.y,
                targetPosition.z + random.y);

            if (activeZone != null && !activeZone.Contains(samplePosition))
            {
                continue;
            }

            Vector3 origin = samplePosition + Vector3.up * splashCastHeight;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                raycastHits,
                splashCastHeight + splashCastDistance,
                splashGroundMask,
                splashTriggerInteraction);

            if (!TryGetNearestHit(hitCount, out RaycastHit hit))
            {
                continue;
            }

            if (skipCoveredSurfaces && IsSurfaceCovered(hit.point, hit.collider))
            {
                continue;
            }

            EmitSplash(hit);
        }
    }

    private bool TryGetNearestHit(int hitCount, out RaycastHit nearestHit)
    {
        nearestHit = default;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (hit.collider == null || hit.distance >= nearestDistance)
            {
                continue;
            }

            nearestHit = hit;
            nearestDistance = hit.distance;
        }

        return nearestHit.collider != null;
    }

    private bool IsSurfaceCovered(Vector3 point, Collider surfaceCollider)
    {
        if (coverProbeHeight <= 0f)
        {
            return false;
        }

        Vector3 origin = point + Vector3.up * Mathf.Max(0.05f, splashSurfaceOffset + 0.02f);
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.up,
            raycastHits,
            coverProbeHeight,
            coverMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = raycastHits[i].collider;
            if (hitCollider != null && hitCollider != surfaceCollider)
            {
                return true;
            }
        }

        return false;
    }

    private void EmitSplash(RaycastHit hit)
    {
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = hit.point + hit.normal * splashSurfaceOffset
        };

        splashParticleSystem.Emit(emitParams, 1);
        if (!splashParticleSystem.isPlaying)
        {
            splashParticleSystem.Play(true);
        }
    }

    private void ApplyParticleBudgets()
    {
        if (rainParticleSystem != null && maxRainParticles > 0)
        {
            ParticleSystem.MainModule main = rainParticleSystem.main;
            main.maxParticles = maxRainParticles;
        }
    }

    private void DisableParticleCollisionModules(ParticleSystem root)
    {
        if (root == null)
        {
            return;
        }

        controlledParticleSystems.Clear();
        root.GetComponentsInChildren(true, controlledParticleSystems);
        for (int i = 0; i < controlledParticleSystems.Count; i++)
        {
            ParticleSystem system = controlledParticleSystems[i];
            if (system == null)
            {
                continue;
            }

            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = false;
            ParticleSystem.TriggerModule trigger = system.trigger;
            trigger.enabled = false;
        }

        controlledParticleSystems.Clear();
    }

    private static LayerMask BuildDefaultEnvironmentMask()
    {
        int mask = 0;
        AddLayerIfExists(ref mask, "Default");
        AddLayerIfExists(ref mask, "Ground");
        AddLayerIfExists(ref mask, "Stairs");
        return mask != 0 ? mask : ~0;
    }

    private static void AddLayerIfExists(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            mask |= 1 << layer;
        }
    }
}
