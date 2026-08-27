using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class EnvironmentManager : MonoBehaviour
{
    // Client/local-only visual controller. In multiplayer, enable one instance only for the local player
    // or local camera; remote players must not drive this manager and no values should be networked.
    private enum TransitionMode
    {
        Speed,
        Duration
    }

    [Header("Local Target")]
    [SerializeField, Tooltip("Resolved automatically to the locally controlled character.")]
    private Transform target;
    [SerializeField, Tooltip("When enabled, this manager always follows LocalPlayerContext / LocalPlayerUtils.")]
    private bool useControlledCharacterAsTarget = true;
    [SerializeField, Tooltip("Editor fallback used only when controlled-character targeting is disabled.")]
    private bool fallbackToMainCamera;

    [Header("Global HDRP Volumes")]
    [SerializeField, Tooltip("Global Volumes driven by this local manager. They should be global, not local.")]
    private Volume[] globalVolumes = new Volume[0];
    [SerializeField, Tooltip("HDRP Volume Profile used when no local EnvironmentZone is active.")]
    private VolumeProfile defaultProfile;
    [SerializeField] private bool captureCurrentVolumeAsDefault = true;

    [Header("Transition")]
    [SerializeField] private TransitionMode transitionMode = TransitionMode.Speed;
    [SerializeField, Min(0f)] private float transitionSpeed = 3f;
    [SerializeField, Min(0.01f)] private float transitionDuration = 1f;

    [Header("Zone Blending")]
    [SerializeField, Tooltip("Smooths zone influence over time so priority/profile changes do not snap.")]
    private bool smoothZoneWeights = true;
    [SerializeField, Min(0f)] private float zoneWeightRiseSpeed = 6f;
    [SerializeField, Min(0f)] private float zoneWeightFallSpeed = 4f;
    [SerializeField, Range(0f, 0.1f)] private float zoneWeightEpsilon = 0.001f;

    [Header("Altitude")]
    [SerializeField] private bool applyAltitudeMultiplier;
    [SerializeField] private float altitudeReferenceY;
    [SerializeField, Min(0.01f)] private float altitudeScale = 100f;
    [SerializeField] private AnimationCurve altitudeIntensity = AnimationCurve.Linear(-1f, 1f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] private bool debugLogging;

    private readonly Dictionary<EnvironmentZone, long> activeZoneOrder = new Dictionary<EnvironmentZone, long>();
    private readonly Dictionary<EnvironmentZone, float> smoothedZoneWeights = new Dictionary<EnvironmentZone, float>();
    private readonly List<VolumeProfile> runtimeProfiles = new List<VolumeProfile>();
    private readonly List<WeightedZone> weightedZones = new List<WeightedZone>();
    private readonly List<EnvironmentZone> zonesToRemove = new List<EnvironmentZone>();
    private readonly List<BaseProfileOverride> baseProfileOverrides = new List<BaseProfileOverride>();

    private struct WeightedZone
    {
        public EnvironmentZone zone;
        public float weight;
        public long order;
    }

    private struct BaseProfileOverride
    {
        public int token;
        public VolumeProfile profile;
    }

    private VolumeProfile baselineProfile;
    private VolumeProfile targetProfile;
    private EnvironmentZone currentZone;
    private VolumeProfile currentProfile;
    private long zoneSequence;
    private bool initialized;

    private VolumeProfile forcedProfile;
    private float forcedProfileTimer;
    private float forcedProfileIntensity = 1f;
    private bool forcedProfileHasTimer;
    private int nextBaseProfileOverrideToken = 1;

    public Transform Target => target;
    public EnvironmentZone CurrentZone => currentZone;
    public VolumeProfile CurrentProfile => currentProfile;

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        InitializeIfNeeded();
        ResolveControlledTarget();
    }

    private void OnDisable()
    {
        LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;
    }

    private void Update()
    {
        InitializeIfNeeded();
        ResolveControlledTarget();

        if (!useControlledCharacterAsTarget)
        {
            ResolveFallbackTarget();
        }

        if (target == null || runtimeProfiles.Count == 0)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        UpdateForcedProfileTimer(deltaTime);
        ResolveTargetState(deltaTime);
        BlendCurrentState(deltaTime);
    }

    // Multiplayer integration point: call this only for the local player/camera.
    // Do not synchronize environment values over the network.
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        activeZoneOrder.Clear();
        smoothedZoneWeights.Clear();
        currentZone = null;
        currentProfile = null;
    }

    private void OnLocalCharacterChanged(Transform localCharacter)
    {
        if (!useControlledCharacterAsTarget)
        {
            return;
        }

        SetTarget(localCharacter);
    }

    public void ForceProfile(VolumeProfile profile, float duration = 0f, float intensity = 1f)
    {
        forcedProfile = profile;
        forcedProfileIntensity = Mathf.Clamp01(intensity);
        forcedProfileHasTimer = duration > 0f;
        forcedProfileTimer = Mathf.Max(0f, duration);

        if (debugLogging)
        {
            Debug.Log(
                $"EnvironmentManager forced profile '{(profile != null ? profile.name : "None")}' on '{name}'.",
                this);
        }
    }

    public void ClearForcedProfile()
    {
        forcedProfile = null;
        forcedProfileTimer = 0f;
        forcedProfileHasTimer = false;
    }

    /// <summary>Ajoute une base HDRP temporaire. Les EnvironmentZone locaux restent melanges au-dessus.</summary>
    public int PushBaseProfileOverride(VolumeProfile profile)
    {
        if (profile == null)
        {
            return 0;
        }

        int token = nextBaseProfileOverrideToken++;
        if (nextBaseProfileOverrideToken <= 0)
        {
            nextBaseProfileOverrideToken = 1;
        }

        baseProfileOverrides.Add(new BaseProfileOverride { token = token, profile = profile });
        RefreshBaseProfileImmediately();
        return token;
    }

    public void PopBaseProfileOverride(int token)
    {
        if (token == 0)
        {
            return;
        }

        for (int index = baseProfileOverrides.Count - 1; index >= 0; index--)
        {
            if (baseProfileOverrides[index].token == token)
            {
                baseProfileOverrides.RemoveAt(index);
                RefreshBaseProfileImmediately();
                return;
            }
        }
    }

    public void Reinitialize()
    {
        initialized = false;
        runtimeProfiles.Clear();
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        runtimeProfiles.Clear();
        for (int i = 0; i < globalVolumes.Length; i++)
        {
            Volume volume = globalVolumes[i];
            if (volume == null)
            {
                continue;
            }

            volume.isGlobal = true;
            if (volume.profile == null)
            {
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            }

            runtimeProfiles.Add(volume.profile);
        }

        if (baselineProfile == null)
        {
            baselineProfile = EnvironmentRuntimeState.CreateProfile($"{name}_EnvironmentBaseline");
        }

        if (targetProfile == null)
        {
            targetProfile = EnvironmentRuntimeState.CreateProfile($"{name}_EnvironmentTarget");
        }

        ResolveBaselineProfile();
        EnvironmentRuntimeState.CopyProfile(baselineProfile, targetProfile);
        ApplyProfileToVolumes(targetProfile);
        initialized = true;
    }

    private void ResolveBaselineProfile()
    {
        VolumeProfile effectiveDefaultProfile = GetEffectiveDefaultProfile();
        if (effectiveDefaultProfile != null)
        {
            EnvironmentRuntimeState.CopyProfile(effectiveDefaultProfile, baselineProfile);
            return;
        }

        if (captureCurrentVolumeAsDefault && runtimeProfiles.Count > 0)
        {
            EnvironmentRuntimeState.CopyProfile(runtimeProfiles[0], baselineProfile);
            return;
        }

        EnvironmentRuntimeState.CopyProfile(null, baselineProfile);
    }

    private void ResolveFallbackTarget()
    {
        if (target != null || !fallbackToMainCamera)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            SetTarget(mainCamera.transform);
        }
    }

    private void ResolveControlledTarget()
    {
        if (!useControlledCharacterAsTarget)
        {
            return;
        }

        Transform controlledCharacter = ResolveControlledCharacterTransform();
        if (controlledCharacter == target)
        {
            return;
        }

        SetTarget(controlledCharacter);

        if (debugLogging)
        {
            Debug.Log(
                $"EnvironmentManager target set to controlled character '{(controlledCharacter != null ? controlledCharacter.name : "None")}'.",
                this);
        }
    }

    private static Transform ResolveControlledCharacterTransform()
    {
        Transform localContextCharacter = LocalPlayerContext.LocalCharacterRoot;
        if (localContextCharacter != null)
        {
            return localContextCharacter;
        }

        GameObject controlledCharacter = LocalPlayerUtils.GetControlledCharacter();
        return controlledCharacter != null ? controlledCharacter.transform : null;
    }

    private void UpdateForcedProfileTimer(float deltaTime)
    {
        if (forcedProfile == null || !forcedProfileHasTimer)
        {
            return;
        }

        forcedProfileTimer -= deltaTime;
        if (forcedProfileTimer <= 0f)
        {
            ClearForcedProfile();
        }
    }

    private void ResolveTargetState(float deltaTime)
    {
        if (forcedProfile != null)
        {
            currentZone = null;
            currentProfile = forcedProfile;
            EnvironmentRuntimeState.BuildTargetProfile(
                baselineProfile,
                forcedProfile,
                targetProfile,
                forcedProfileIntensity);
            return;
        }

        UpdateZoneWeights(target.position, deltaTime);
        EnvironmentZone bestZone = FindBestZoneFromWeights();
        VolumeProfile nextProfile = bestZone != null ? bestZone.Profile : GetEffectiveDefaultProfile();
        float altitudeMultiplier = ResolveAltitudeMultiplier(target.position.y);

        if (debugLogging && (currentZone != bestZone || currentProfile != nextProfile))
        {
            string zoneName = bestZone != null ? bestZone.name : "Default";
            string profileName = nextProfile != null ? nextProfile.name : "Baseline";
            Debug.Log($"EnvironmentManager blending to '{profileName}' from zone '{zoneName}'.", this);
        }

        currentZone = bestZone;
        currentProfile = nextProfile;
        EnvironmentRuntimeState.CopyProfile(baselineProfile, targetProfile);

        for (int i = 0; i < weightedZones.Count; i++)
        {
            WeightedZone weightedZone = weightedZones[i];
            if (weightedZone.zone == null || weightedZone.zone.Profile == null)
            {
                continue;
            }

            EnvironmentRuntimeState.BlendSourceIntoProfile(
                targetProfile,
                weightedZone.zone.Profile,
                weightedZone.weight * altitudeMultiplier);
        }
    }

    private VolumeProfile GetEffectiveDefaultProfile()
    {
        for (int index = baseProfileOverrides.Count - 1; index >= 0; index--)
        {
            if (baseProfileOverrides[index].profile != null)
            {
                return baseProfileOverrides[index].profile;
            }
        }

        return defaultProfile;
    }

    private void RefreshBaseProfileImmediately()
    {
        InitializeIfNeeded();
        if (baselineProfile == null || targetProfile == null)
        {
            return;
        }

        ResolveBaselineProfile();
        EnvironmentRuntimeState.CopyProfile(baselineProfile, targetProfile);
        ApplyProfileToVolumes(targetProfile);
    }

    private void UpdateZoneWeights(Vector3 position, float deltaTime)
    {
        weightedZones.Clear();
        zonesToRemove.Clear();

        IReadOnlyList<EnvironmentZone> zones = EnvironmentZone.RegisteredZones;
        for (int i = 0; i < zones.Count; i++)
        {
            EnvironmentZone zone = zones[i];
            if (zone == null)
            {
                continue;
            }

            float rawWeight = zone.EvaluateWeight(position);
            float currentWeight = smoothedZoneWeights.TryGetValue(zone, out float existingWeight)
                ? existingWeight
                : 0f;
            float nextWeight = smoothZoneWeights
                ? SmoothWeight(currentWeight, rawWeight, deltaTime)
                : rawWeight;

            if (nextWeight <= zoneWeightEpsilon)
            {
                smoothedZoneWeights.Remove(zone);
                activeZoneOrder.Remove(zone);
                continue;
            }

            smoothedZoneWeights[zone] = nextWeight;
            EnsureZoneOrder(zone);
        }

        foreach (KeyValuePair<EnvironmentZone, float> entry in smoothedZoneWeights)
        {
            EnvironmentZone zone = entry.Key;
            if (zone == null || !ContainsRegisteredZone(zone))
            {
                zonesToRemove.Add(zone);
            }
        }

        for (int i = 0; i < zonesToRemove.Count; i++)
        {
            EnvironmentZone zone = zonesToRemove[i];
            smoothedZoneWeights.Remove(zone);
            activeZoneOrder.Remove(zone);
        }

        foreach (KeyValuePair<EnvironmentZone, float> entry in smoothedZoneWeights)
        {
            EnvironmentZone zone = entry.Key;
            if (zone == null || zone.Profile == null)
            {
                continue;
            }

            weightedZones.Add(new WeightedZone
            {
                zone = zone,
                weight = Mathf.Clamp01(entry.Value),
                order = EnsureZoneOrder(zone)
            });
        }

        weightedZones.Sort(CompareWeightedZonesForApplication);
    }

    private EnvironmentZone FindBestZoneFromWeights()
    {
        EnvironmentZone bestZone = null;
        int bestPriority = int.MinValue;
        float bestWeight = -1f;
        long bestOrder = long.MinValue;

        for (int i = 0; i < weightedZones.Count; i++)
        {
            WeightedZone weightedZone = weightedZones[i];
            EnvironmentZone zone = weightedZone.zone;
            if (zone == null)
            {
                continue;
            }

            if (zone.Priority > bestPriority ||
                (zone.Priority == bestPriority && weightedZone.weight > bestWeight) ||
                (zone.Priority == bestPriority && Mathf.Approximately(weightedZone.weight, bestWeight) && weightedZone.order > bestOrder))
            {
                bestZone = zone;
                bestPriority = zone.Priority;
                bestWeight = weightedZone.weight;
                bestOrder = weightedZone.order;
            }
        }

        return bestZone;
    }

    private float SmoothWeight(float currentWeight, float targetWeight, float deltaTime)
    {
        float speed = targetWeight > currentWeight ? zoneWeightRiseSpeed : zoneWeightFallSpeed;
        if (speed <= 0f || deltaTime <= 0f)
        {
            return targetWeight;
        }

        float t = 1f - Mathf.Exp(-speed * deltaTime);
        return Mathf.Lerp(currentWeight, targetWeight, t);
    }

    private long EnsureZoneOrder(EnvironmentZone zone)
    {
        if (!activeZoneOrder.TryGetValue(zone, out long order))
        {
            zoneSequence++;
            order = zoneSequence;
            activeZoneOrder[zone] = order;
        }

        return order;
    }

    private static int CompareWeightedZonesForApplication(WeightedZone a, WeightedZone b)
    {
        int priorityComparison = a.zone.Priority.CompareTo(b.zone.Priority);
        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        int weightComparison = a.weight.CompareTo(b.weight);
        if (weightComparison != 0)
        {
            return weightComparison;
        }

        return a.order.CompareTo(b.order);
    }

    private static bool ContainsRegisteredZone(EnvironmentZone zone)
    {
        IReadOnlyList<EnvironmentZone> zones = EnvironmentZone.RegisteredZones;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] == zone)
            {
                return true;
            }
        }

        return false;
    }

    private float ResolveAltitudeMultiplier(float y)
    {
        if (!applyAltitudeMultiplier || altitudeIntensity == null)
        {
            return 1f;
        }

        float normalizedAltitude = (y - altitudeReferenceY) / Mathf.Max(0.01f, altitudeScale);
        return Mathf.Max(0f, altitudeIntensity.Evaluate(normalizedAltitude));
    }

    private void BlendCurrentState(float deltaTime)
    {
        float blendFactor = ResolveBlendFactor(deltaTime);
        for (int i = 0; i < runtimeProfiles.Count; i++)
        {
            EnvironmentRuntimeState.BlendProfileTowards(runtimeProfiles[i], targetProfile, blendFactor);
        }
    }

    private float ResolveBlendFactor(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return 1f;
        }

        switch (transitionMode)
        {
            case TransitionMode.Duration:
                return transitionDuration > 0f
                    ? 1f - Mathf.Exp((-4f * deltaTime) / transitionDuration)
                    : 1f;
            default:
                return transitionSpeed > 0f
                    ? 1f - Mathf.Exp(-transitionSpeed * deltaTime)
                    : 1f;
        }
    }

    private void ApplyProfileToVolumes(VolumeProfile profile)
    {
        for (int i = 0; i < runtimeProfiles.Count; i++)
        {
            EnvironmentRuntimeState.CopyProfile(profile, runtimeProfiles[i]);
        }
    }

    private void OnValidate()
    {
        transitionSpeed = Mathf.Max(0f, transitionSpeed);
        transitionDuration = Mathf.Max(0.01f, transitionDuration);
        zoneWeightRiseSpeed = Mathf.Max(0f, zoneWeightRiseSpeed);
        zoneWeightFallSpeed = Mathf.Max(0f, zoneWeightFallSpeed);
        altitudeScale = Mathf.Max(0.01f, altitudeScale);

        if (globalVolumes == null)
        {
            globalVolumes = new Volume[0];
        }
    }
}
