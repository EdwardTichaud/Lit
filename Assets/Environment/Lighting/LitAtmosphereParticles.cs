using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Authoring controller for the castle's ambient particles and fixed-flame accents.
/// Particle systems are serialized children: this component never creates renderers
/// or materials at runtime, which keeps scene loading deterministic.
/// </summary>
[DisallowMultipleComponent]
public sealed class LitAtmosphereParticles : MonoBehaviour
{
    public enum Profile { CastleIceDust, FlameEmbers, AncientFlameMix }

    [SerializeField] private Profile profile = Profile.CastleIceDust;
    [SerializeField, Min(0.01f)] private float density = 1f;
    [SerializeField] private Vector3 castleDustVolume = new Vector3(58f, 15f, 58f);
    [SerializeField, Range(0.25f, 3f)] private float particleSize = 1f;
    [SerializeField, Range(0.01f, 1f)] private float opacity = 0.16f;
    [SerializeField, Range(0f, 1f)] private float alphaClipThreshold = 0.04f;
    [SerializeField] private bool previewInEditMode;
    [SerializeField, Tooltip("Explicit authored particle systems. Their count depends on the selected profile.")]
    private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();
    [SerializeField] private Material dustMaterial;
    [SerializeField] private Material emberMaterial;
    [SerializeField] private Material ancientMistMaterial;

    private Flame sourceFlame;
    private bool loggedInvalidConfiguration;

    public Profile CurrentProfile => profile;
    public int RequiredSystemCount => profile == Profile.AncientFlameMix ? 2 : 1;

    private void Awake() => ConfigureSystems();

    private void OnEnable()
    {
        BindFlame();
        RefreshEmissionState();
    }

    private void OnDisable()
    {
        UnbindFlame();
        StopAll();
    }

    private void OnValidate()
    {
        density = Mathf.Max(0.01f, density);
        castleDustVolume.x = Mathf.Max(0.1f, castleDustVolume.x);
        castleDustVolume.y = Mathf.Max(0.1f, castleDustVolume.y);
        castleDustVolume.z = Mathf.Max(0.1f, castleDustVolume.z);
        ConfigureSystems();
        // AddComponent invokes OnValidate before the editor setup can assign
        // its serialized particle references. Avoid a transient false error
        // during that single construction step; invalid saved setups still
        // report their configuration once they own a reference slot.
        if (isActiveAndEnabled && particleSystems != null && particleSystems.Length > 0)
            RefreshEmissionState();
    }

    public bool ValidateConfiguration(out string reason)
    {
        if (particleSystems == null || particleSystems.Length != RequiredSystemCount)
        {
            reason = $"{profile} requires {RequiredSystemCount} explicitly assigned ParticleSystem reference(s).";
            return false;
        }
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
            {
                reason = $"Particle system {i} is missing.";
                return false;
            }
            if (particleSystems[i].GetComponent<ParticleSystemRenderer>() == null)
            {
                reason = $"Particle system '{particleSystems[i].name}' has no ParticleSystemRenderer.";
                return false;
            }
        }
        if (profile == Profile.CastleIceDust && dustMaterial == null)
        {
            reason = "Castle dust requires a lit transparent dust material.";
            return false;
        }
        if (profile == Profile.FlameEmbers && emberMaterial == null)
        {
            reason = "Flame embers require an emissive transparent material.";
            return false;
        }
        if (profile == Profile.AncientFlameMix && (emberMaterial == null || ancientMistMaterial == null))
        {
            reason = "Ancient flame requires both ember and cyan-mist materials.";
            return false;
        }
        reason = null;
        return true;
    }

    public void ConfigureAuthoring(Profile newProfile, ParticleSystem[] systems, Material dust, Material embers, Material mist)
    {
        profile = newProfile;
        particleSystems = systems ?? Array.Empty<ParticleSystem>();
        dustMaterial = dust;
        emberMaterial = embers;
        ancientMistMaterial = mist;
        ConfigureSystems();
    }

    /// <summary>
    /// Applies the initial cinematic calibration once when the authoring setup
    /// is rebuilt. All values stay serialized afterwards and remain editable
    /// in the Inspector.
    /// </summary>
    public void ApplyVisibilityDefaults()
    {
        switch (profile)
        {
            case Profile.CastleIceDust:
                density = 1.8f;
                particleSize = 1.65f;
                opacity = 0.28f;
                alphaClipThreshold = 0.01f;
                break;
            case Profile.FlameEmbers:
                density = 3.5f;
                particleSize = 1.7f;
                opacity = 0.85f;
                alphaClipThreshold = 0.01f;
                break;
            case Profile.AncientFlameMix:
                density = 2.5f;
                particleSize = 1.8f;
                opacity = 0.42f;
                alphaClipThreshold = 0.01f;
                break;
        }

        ConfigureSystems();
    }

    private void BindFlame()
    {
        Flame resolved = GetComponentInParent<Flame>();
        if (resolved == sourceFlame) return;
        UnbindFlame();
        sourceFlame = resolved;
        if (sourceFlame != null) sourceFlame.StateChanged += OnFlameStateChanged;
    }

    private void UnbindFlame()
    {
        if (sourceFlame != null) sourceFlame.StateChanged -= OnFlameStateChanged;
        sourceFlame = null;
    }

    private void OnFlameStateChanged(Flame _, bool isLit) => SetEmissionState(isLit);

    private void RefreshEmissionState()
    {
        if (!ValidateConfiguration(out string reason))
        {
            if (!loggedInvalidConfiguration)
            {
                loggedInvalidConfiguration = true;
                Debug.LogError($"[{nameof(LitAtmosphereParticles)}] '{name}' is not configured: {reason} It will not create runtime particle systems.", this);
            }
            return;
        }
        loggedInvalidConfiguration = false;
        bool canPreview = Application.isPlaying || previewInEditMode;
        SetEmissionState(canPreview && (sourceFlame == null || sourceFlame.IsEffectivelyLit));
    }

    private void SetEmissionState(bool active)
    {
        if (particleSystems == null) return;
        foreach (ParticleSystem system in particleSystems)
        {
            if (system == null) continue;
            if (active)
            {
                if (!system.isPlaying) system.Play(true);
            }
            else system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void StopAll()
    {
        if (particleSystems == null) return;
        foreach (ParticleSystem system in particleSystems)
            if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ConfigureSystems()
    {
        if (!ValidateConfiguration(out _)) return;
        if (profile == Profile.CastleIceDust)
            ConfigureDust(particleSystems[0], dustMaterial, new Color(0.62f, 0.8f, 1f, opacity), castleDustVolume, 28f * density, 360, particleSize, 1f);
        else if (profile == Profile.FlameEmbers)
            ConfigureEmbers(particleSystems[0], emberMaterial, new Color(1f, 0.22f, 0.035f, opacity), 2f * density, particleSize);
        else
        {
            ConfigureEmbers(particleSystems[0], emberMaterial, new Color(1f, 0.25f, 0.04f, opacity), 1.6f * density, particleSize);
            ConfigureDust(particleSystems[1], ancientMistMaterial, new Color(0.22f, 0.72f, 1f, opacity), new Vector3(1.8f, 2.4f, 1.8f), 5f * density, 56, particleSize, 1.2f);
        }
    }

    private void ConfigureDust(ParticleSystem system, Material material, Color color, Vector3 box, float rate, int maxParticles, float sizeMultiplier, float profileSize)
    {
        ParticleSystem.MainModule main = system.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(maxParticles * density), maxParticles, 900);
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f * profileSize * sizeMultiplier, 0.18f * profileSize * sizeMultiplier);
        main.startColor = color;
        ParticleSystem.EmissionModule emission = system.emission; emission.enabled = true; emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box;
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // Unity requires X/Y/Z curves in Velocity over Lifetime to share the same mode.
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.01f, 0.035f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystem.NoiseModule noise = system.noise; noise.enabled = true; noise.strength = 0.045f; noise.frequency = 0.16f; noise.scrollSpeed = 0.04f;
        ConfigureRenderer(system, material);
    }

    private void ConfigureEmbers(ParticleSystem system, Material material, Color color, float rate, float sizeMultiplier)
    {
        ParticleSystem.MainModule main = system.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 56; main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.14f, 0.38f); main.startSize = new ParticleSystem.MinMaxCurve(0.025f * sizeMultiplier, 0.07f * sizeMultiplier);
        main.startColor = color;
        ParticleSystem.EmissionModule emission = system.emission; emission.enabled = true; emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.16f;
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.3f, 0.75f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystem.NoiseModule noise = system.noise; noise.enabled = true; noise.strength = 0.1f; noise.frequency = 0.75f;
        ConfigureRenderer(system, material);
    }

    private void ConfigureRenderer(ParticleSystem system, Material material)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return;
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enableGPUInstancing = true;
        if (material != null && material.HasProperty("_AlphaCutoff"))
            material.SetFloat("_AlphaCutoff", alphaClipThreshold);
    }
}
