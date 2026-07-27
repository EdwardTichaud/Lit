using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class MuninOrbAlphaGuard : MonoBehaviour
{
    private static readonly int[] ColorPropertyIds =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("_Color"),
        Shader.PropertyToID("_TintColor"),
        Shader.PropertyToID("_UnlitColor"),
        Shader.PropertyToID("_EmissionColor"),
        Shader.PropertyToID("_EmissiveColor")
    };
    private static readonly int[] TexturePropertyIds =
    {
        Shader.PropertyToID("_BaseMap"),
        Shader.PropertyToID("_MainTex"),
        Shader.PropertyToID("_BaseColorMap"),
        Shader.PropertyToID("_UnlitColorMap")
    };
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int AlphaMultiplierId = Shader.PropertyToID("_AlphaMultiplier");
    private static readonly int BlackLuminanceThresholdId = Shader.PropertyToID("_BlackLuminanceThreshold");
    private static readonly int BlackFeatherId = Shader.PropertyToID("_BlackFeather");

    [Header("Alpha Safety")]
    [SerializeField, Range(0f, 1f), Tooltip("Alpha global conserve sur les couleurs non noires.")]
    private float alphaMultiplier = 1f;
    [SerializeField, Range(0f, 0.25f), Tooltip("Toute couleur sous ce seuil de luminance devient transparente.")]
    private float blackLuminanceThreshold = 0.025f;
    [SerializeField, Range(0.001f, 0.25f), Tooltip("Adoucit la transition entre noir transparent et couleur visible.")]
    private float blackFeather = 0.04f;
    [SerializeField, Tooltip("Force l'alpha a 0 sur les couleurs noires pour eviter les fonds noirs de textures VFX.")]
    private bool forceBlackAlphaToZero = true;

    [Header("Targets")]
    [SerializeField, Tooltip("Corrige les couleurs de depart et modules couleur des ParticleSystem enfants.")]
    private bool enforceParticleColors = true;
    [SerializeField, Tooltip("Applique des MaterialPropertyBlock aux renderers enfants sans modifier les assets de materiaux.")]
    private bool enforceMaterialPropertyBlocks = true;
    [SerializeField, Tooltip("Remplace les materiaux de particules par des clones runtime alpha-safe.")]
    private bool useAlphaSafeRuntimeParticleMaterials = true;
    [SerializeField, Tooltip("Shader utilise par les clones runtime pour rendre le noir transparent.")]
    private Shader alphaSafeShader;
    [SerializeField, Tooltip("Revalide chaque frame pour resister aux reimports, pooling et scripts qui remplacent des enfants.")]
    private bool enforceContinuously = true;

    private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
    private readonly Dictionary<Material, Material> alphaSafeMaterialCache = new Dictionary<Material, Material>();
    private ParticleSystem[] particleSystems;
    private Renderer[] renderers;
    private Transform cachedTransform;
    private int cachedChildCount = -1;

    private void Awake()
    {
        RefreshTargetsIfNeeded(true);
        ApplyAlphaSafety();
    }

    private void OnEnable()
    {
        RefreshTargetsIfNeeded(true);
        ApplyAlphaSafety();
    }

    private void LateUpdate()
    {
        if (!enforceContinuously)
        {
            return;
        }

        RefreshTargetsIfNeeded(false);
        ApplyAlphaSafety();
    }

    private void OnTransformChildrenChanged()
    {
        RefreshTargetsIfNeeded(true);
        ApplyAlphaSafety();
    }

    private void OnDestroy()
    {
        foreach (Material material in alphaSafeMaterialCache.Values)
        {
            if (material == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }

        alphaSafeMaterialCache.Clear();
    }

    private void OnValidate()
    {
        alphaMultiplier = Mathf.Clamp01(alphaMultiplier);
        blackLuminanceThreshold = Mathf.Clamp(blackLuminanceThreshold, 0f, 0.25f);
        blackFeather = Mathf.Clamp(blackFeather, 0.001f, 0.25f);
        RefreshTargetsIfNeeded(true);
        ApplyAlphaSafety();
    }

    private void RefreshTargetsIfNeeded(bool force)
    {
        if (cachedTransform == null)
        {
            cachedTransform = transform;
        }

        int childCount = cachedTransform.childCount;
        if (!force && childCount == cachedChildCount && particleSystems != null && renderers != null)
        {
            return;
        }

        cachedChildCount = childCount;
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void ApplyAlphaSafety()
    {
        if (enforceParticleColors && particleSystems != null)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ApplyParticleAlphaSafety(particleSystems[i]);
            }
        }

        if (enforceMaterialPropertyBlocks && renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                ApplyRendererAlphaSafety(renderers[i]);
            }
        }
    }

    private void ApplyParticleAlphaSafety(ParticleSystem system)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystem.MainModule main = system.main;
        main.startColor = SanitizeGradient(main.startColor);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        if (colorOverLifetime.enabled)
        {
            colorOverLifetime.color = SanitizeGradient(colorOverLifetime.color);
        }

        ParticleSystem.ColorBySpeedModule colorBySpeed = system.colorBySpeed;
        if (colorBySpeed.enabled)
        {
            colorBySpeed.color = SanitizeGradient(colorBySpeed.color);
        }
    }

    private void ApplyRendererAlphaSafety(Renderer targetRenderer)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Material[] materials = targetRenderer.sharedMaterials;
        if (useAlphaSafeRuntimeParticleMaterials && Application.isPlaying && targetRenderer is ParticleSystemRenderer)
        {
            materials = EnsureAlphaSafeRuntimeMaterials(targetRenderer, materials);
        }

        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
        {
            Material material = materials[materialIndex];
            if (material == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
            bool changed = false;
            for (int propertyIndex = 0; propertyIndex < ColorPropertyIds.Length; propertyIndex++)
            {
                int propertyId = ColorPropertyIds[propertyIndex];
                if (!material.HasProperty(propertyId))
                {
                    continue;
                }

                propertyBlock.SetColor(propertyId, SanitizeColor(material.GetColor(propertyId)));
                changed = true;
            }

            if (changed)
            {
                targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }
    }

    private Material[] EnsureAlphaSafeRuntimeMaterials(Renderer targetRenderer, Material[] sourceMaterials)
    {
        if (sourceMaterials == null || sourceMaterials.Length == 0)
        {
            return sourceMaterials;
        }

        Shader shader = ResolveAlphaSafeShader();
        if (shader == null)
        {
            return sourceMaterials;
        }

        bool changed = false;
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Material source = sourceMaterials[i];
            if (source == null || source.shader == shader)
            {
                continue;
            }

            Material safeMaterial = GetOrCreateAlphaSafeMaterial(source, shader);
            if (safeMaterial == null || safeMaterial == source)
            {
                continue;
            }

            sourceMaterials[i] = safeMaterial;
            changed = true;
        }

        if (changed)
        {
            targetRenderer.sharedMaterials = sourceMaterials;
        }

        return sourceMaterials;
    }

    private Shader ResolveAlphaSafeShader()
    {
        if (alphaSafeShader != null)
        {
            return alphaSafeShader;
        }

        alphaSafeShader = Shader.Find("Hidden/Lit/MuninOrbAlphaSafe");
        return alphaSafeShader;
    }

    private Material GetOrCreateAlphaSafeMaterial(Material source, Shader shader)
    {
        if (alphaSafeMaterialCache.TryGetValue(source, out Material cached) && cached != null)
        {
            UpdateAlphaSafeMaterial(cached, source);
            return cached;
        }

        Material material = new Material(shader)
        {
            name = source.name + " Alpha Safe Runtime",
            hideFlags = HideFlags.DontSave
        };

        Texture texture = ResolveMainTexture(source);
        if (texture != null)
        {
            material.SetTexture(MainTexId, texture);
        }

        UpdateAlphaSafeMaterial(material, source);
        alphaSafeMaterialCache[source] = material;
        return material;
    }

    private void UpdateAlphaSafeMaterial(Material target, Material source)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.SetColor(ColorPropertyIds[1], ResolveMaterialTint(source));
        target.SetFloat(AlphaMultiplierId, alphaMultiplier);
        target.SetFloat(BlackLuminanceThresholdId, blackLuminanceThreshold);
        target.SetFloat(BlackFeatherId, blackFeather);
        target.renderQueue = source.renderQueue > 0 ? source.renderQueue : 3000;
    }

    private static Texture ResolveMainTexture(Material source)
    {
        for (int i = 0; i < TexturePropertyIds.Length; i++)
        {
            int propertyId = TexturePropertyIds[i];
            if (!source.HasProperty(propertyId))
            {
                continue;
            }

            Texture texture = source.GetTexture(propertyId);
            if (texture != null)
            {
                return texture;
            }
        }

        return source.mainTexture;
    }

    private Color ResolveMaterialTint(Material source)
    {
        for (int i = 0; i < ColorPropertyIds.Length; i++)
        {
            int propertyId = ColorPropertyIds[i];
            if (!source.HasProperty(propertyId))
            {
                continue;
            }

            return SanitizeColor(source.GetColor(propertyId));
        }

        return Color.white;
    }

    private ParticleSystem.MinMaxGradient SanitizeGradient(ParticleSystem.MinMaxGradient source)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(SanitizeColor(source.color));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(SanitizeColor(source.colorMin), SanitizeColor(source.colorMax));
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(SanitizeGradient(source.gradient));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(SanitizeGradient(source.gradientMin), SanitizeGradient(source.gradientMax));
            case ParticleSystemGradientMode.RandomColor:
                return new ParticleSystem.MinMaxGradient(SanitizeGradient(source.gradient))
                {
                    mode = ParticleSystemGradientMode.RandomColor
                };
            default:
                return source;
        }
    }

    private Gradient SanitizeGradient(Gradient source)
    {
        if (source == null)
        {
            return null;
        }

        Gradient sanitized = new Gradient
        {
            mode = source.mode
        };

        GradientColorKey[] colorKeys = source.colorKeys;
        GradientAlphaKey[] alphaKeys = source.alphaKeys;
        for (int i = 0; i < alphaKeys.Length; i++)
        {
            Color evaluated = source.Evaluate(alphaKeys[i].time);
            alphaKeys[i].alpha = ResolveSafeAlpha(evaluated, alphaKeys[i].alpha);
        }

        sanitized.SetKeys(colorKeys, alphaKeys);
        return sanitized;
    }

    private Color SanitizeColor(Color source)
    {
        source.a = ResolveSafeAlpha(source, source.a);
        return source;
    }

    private float ResolveSafeAlpha(Color color, float alpha)
    {
        if (forceBlackAlphaToZero && GetLuminance(color) <= blackLuminanceThreshold)
        {
            return 0f;
        }

        return Mathf.Clamp01(alpha * alphaMultiplier);
    }

    private static float GetLuminance(Color color)
    {
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }
}
