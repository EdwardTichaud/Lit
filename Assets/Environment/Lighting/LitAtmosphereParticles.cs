using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Generates lightweight, soft-particle atmosphere for the castle and fixed flames.
/// The particle controls remain authorable on the prefab; no global Volume is changed.
/// </summary>
[DisallowMultipleComponent]
public sealed class LitAtmosphereParticles : MonoBehaviour
{
    public enum Profile { CastleIceDust, FlameEmbers, AncientFlameMix }

    [SerializeField] private Profile profile = Profile.CastleIceDust;
    [SerializeField, Min(0.01f)] private float density = 1f;
    [SerializeField] private Vector3 castleDustVolume = new Vector3(58f, 15f, 58f);
    [SerializeField, Range(0.25f, 3f)] private float particleSize = 1f;
    [SerializeField, Tooltip("Use an HDRP particle material with soft depth fading. A per-instance copy is used at runtime.")]
    private Material particleMaterial;
    [SerializeField] private Texture2D particleTexture;
    [SerializeField] private bool previewInEditMode;

    private ParticleSystem[] systems;
    private Material runtimeMaterial;

    private void OnEnable()
    {
        BuildIfNeeded();
        SetPreviewState(Application.isPlaying || previewInEditMode);
    }

    private void OnDisable()
    {
        if (systems == null) return;
        foreach (ParticleSystem system in systems)
            if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnValidate()
    {
        density = Mathf.Max(0.01f, density);
        castleDustVolume.x = Mathf.Max(0.1f, castleDustVolume.x);
        castleDustVolume.y = Mathf.Max(0.1f, castleDustVolume.y);
        castleDustVolume.z = Mathf.Max(0.1f, castleDustVolume.z);
        if (Application.isPlaying && isActiveAndEnabled)
        {
            BuildIfNeeded();
            Configure();
        }
    }

    private void BuildIfNeeded()
    {
        int count = profile == Profile.AncientFlameMix ? 2 : 1;
        if (HasValidSystems(count)) return;

        systems = new ParticleSystem[count];
        for (int i = 0; i < count; i++)
        {
            systems[i] = GetOrCreateSystem(i);
            ParticleSystemRenderer renderer = systems[i] != null ? systems[i].GetComponent<ParticleSystemRenderer>() : null;
            if (renderer == null)
            {
                continue;
            }

            renderer.sharedMaterial = ResolveMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enableGPUInstancing = true;
        }

        Configure();
    }

    private bool HasValidSystems(int count)
    {
        if (systems == null || systems.Length != count)
        {
            return false;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null || systems[i].GetComponent<ParticleSystemRenderer>() == null)
            {
                return false;
            }
        }

        return true;
    }

    private ParticleSystem GetOrCreateSystem(int index)
    {
        string childName = $"Particles_{index}";
        Transform child = transform.Find(childName);
        GameObject childObject = child != null ? child.gameObject : new GameObject(childName);
        if (child == null)
        {
            childObject.transform.SetParent(transform, false);
        }

        ParticleSystem system = childObject.GetComponent<ParticleSystem>();
        if (system == null)
        {
            return childObject.AddComponent<ParticleSystem>();
        }

        if (system.GetComponent<ParticleSystemRenderer>() != null)
        {
            return system;
        }

        // A ParticleSystemRenderer removed from an authored child cannot be
        // restored in place at runtime. Keep the broken child dormant and use
        // a clean sibling so enabling this component never throws repeatedly.
        childObject.SetActive(false);
        string replacementName = childName + "_Runtime";
        Transform replacement = transform.Find(replacementName);
        GameObject replacementObject = replacement != null ? replacement.gameObject : new GameObject(replacementName);
        if (replacement == null)
        {
            replacementObject.transform.SetParent(transform, false);
        }

        return replacementObject.GetComponent<ParticleSystem>() ?? replacementObject.AddComponent<ParticleSystem>();
    }

    private void Configure()
    {
        if (systems == null) return;
        if (profile == Profile.CastleIceDust)
            ConfigureDust(systems[0], new Color(0.62f, 0.8f, 1f, 0.16f), castleDustVolume, 28f * density, 220, particleSize, 1f);
        else if (profile == Profile.FlameEmbers)
            ConfigureEmbers(systems[0], new Color(1f, 0.22f, 0.035f, 0.28f), 2.2f * density, particleSize);
        else
        {
            ConfigureEmbers(systems[0], new Color(1f, 0.25f, 0.04f, 0.2f), 1.2f * density, particleSize);
            ConfigureDust(systems[1], new Color(0.22f, 0.72f, 1f, 0.2f), new Vector3(1.4f, 1f, 1.4f), 4.5f * density, 42, particleSize, 0.65f);
        }
    }

    private static void ConfigureDust(ParticleSystem system, Color color, Vector3 box, float rate, int maxParticles, float sizeMultiplier, float profileSize)
    {
        if (system == null) return;
        ParticleSystem.MainModule main = system.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles; main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.025f, 0.08f); main.startSize = new ParticleSystem.MinMaxCurve(0.06f * profileSize * sizeMultiplier, 0.14f * profileSize * sizeMultiplier);
        main.startColor = color;
        ParticleSystem.EmissionModule emission = system.emission; emission.enabled = true; emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box;
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
        ParticleSystem.NoiseModule noise = system.noise; noise.enabled = true; noise.strength = 0.06f; noise.frequency = 0.18f; noise.scrollSpeed = 0.05f;
    }

    private static void ConfigureEmbers(ParticleSystem system, Color color, float rate, float sizeMultiplier)
    {
        if (system == null) return;
        ParticleSystem.MainModule main = system.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 24; main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.36f); main.startSize = new ParticleSystem.MinMaxCurve(0.012f * sizeMultiplier, 0.035f * sizeMultiplier);
        main.startColor = color;
        ParticleSystem.EmissionModule emission = system.emission; emission.enabled = true; emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.18f;
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime; velocity.enabled = true; velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        ParticleSystem.NoiseModule noise = system.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.8f;
    }

    private Material ResolveMaterial()
    {
        if (runtimeMaterial != null) return runtimeMaterial;
        if (particleMaterial != null)
        {
            runtimeMaterial = new Material(particleMaterial) { name = "Runtime_LitAtmosphereSoftParticle", hideFlags = HideFlags.DontSave };
            if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", Color.white);
            return runtimeMaterial;
        }

        Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        if (shader == null) return null;
        runtimeMaterial = new Material(shader) { name = "Runtime_LitAtmosphereSoftParticle", hideFlags = HideFlags.DontSave };
        if (particleTexture != null)
        {
            if (runtimeMaterial.HasProperty("_BaseColorMap")) runtimeMaterial.SetTexture("_BaseColorMap", particleTexture);
            if (runtimeMaterial.HasProperty("_MainTex")) runtimeMaterial.SetTexture("_MainTex", particleTexture);
        }
        if (runtimeMaterial.HasProperty("_SurfaceType")) runtimeMaterial.SetFloat("_SurfaceType", 1f);
        if (runtimeMaterial.HasProperty("_BlendMode")) runtimeMaterial.SetFloat("_BlendMode", 0f);
        if (runtimeMaterial.HasProperty("_ZWrite")) runtimeMaterial.SetFloat("_ZWrite", 0f);
        if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", Color.white);
        runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return runtimeMaterial;
    }

    private void SetPreviewState(bool active)
    {
        if (systems == null) return;
        foreach (ParticleSystem system in systems)
        {
            if (system == null) continue;
            if (active && !system.isPlaying) system.Play(true);
            else if (!active) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }
}
