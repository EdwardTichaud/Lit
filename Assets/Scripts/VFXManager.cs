using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralise les VFX gameplay qui ne doivent pas etre portes par chaque objet.
/// Les objets bloques par une Flame gardent une seule instance de VFX boucle :
/// seule son emission varie quand la Flame s'allume ou s'eteint.
/// </summary>
[DisallowMultipleComponent]
public sealed class VFXManager : MonoBehaviour
{
    private sealed class InactiveFlameItemEffect
    {
        public Component Owner;
        public Transform Anchor;
        public GameObject Instance;
        public Vector3 InitialLocalScale;
        public readonly List<ParticleEmission> Emissions = new List<ParticleEmission>();
        public float EmissionFactor;
        public float TargetEmissionFactor;
    }

    private struct ParticleEmission
    {
        public ParticleSystem System;
        public float RateOverTimeMultiplier;
        public float RateOverDistanceMultiplier;
    }

    [Header("Inactive Flame-Gated Items")]
    [SerializeField, Tooltip("Prefab boucle commun aux objets interactifs bloques hors d'une Flame active.")]
    private GameObject inactiveFlameGatedItemVfxPrefab;
    [SerializeField, Min(0.01f), Tooltip("Duree du fondu de l'emission a l'allumage ou l'extinction de la Flame.")]
    private float inactiveFlameGatedItemEmissionFadeSeconds = 0.35f;
    [SerializeField, Min(0.01f), Tooltip("Multiplicateur applique a la plus grande dimension visuelle de l'objet bloque.")]
    private float inactiveFlameGatedItemScalePerWorldUnit = 0.35f;
    [SerializeField, Min(0.01f)] private float inactiveFlameGatedItemMinScale = 0.35f;
    [SerializeField, Min(0.01f)] private float inactiveFlameGatedItemMaxScale = 2f;

    private readonly Dictionary<Component, InactiveFlameItemEffect> inactiveFlameItemEffects =
        new Dictionary<Component, InactiveFlameItemEffect>();
    private readonly List<Component> inactiveFlameItemRemovalBuffer = new List<Component>();

    public static VFXManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        foreach (InactiveFlameItemEffect effect in inactiveFlameItemEffects.Values)
        {
            DestroyEffect(effect);
        }

        inactiveFlameItemEffects.Clear();
        inactiveFlameItemRemovalBuffer.Clear();
    }

    /// <summary>
    /// Affiche ou masque progressivement le VFX boucle d'un objet bloque par une Flame.
    /// L'instance est conservee quand elle est masquee afin de revenir de zero a son
    /// emission d'origine si la Flame s'eteint de nouveau.
    /// </summary>
    public void SetInactiveFlameGatedItemVfx(Component owner, Transform anchor, bool shouldShow)
    {
        if (owner == null)
        {
            return;
        }

        if (!inactiveFlameItemEffects.TryGetValue(owner, out InactiveFlameItemEffect effect))
        {
            if (!shouldShow || inactiveFlameGatedItemVfxPrefab == null)
            {
                return;
            }

            effect = CreateInactiveFlameItemEffect(owner, anchor);
            if (effect == null)
            {
                return;
            }

            inactiveFlameItemEffects.Add(owner, effect);
        }

        Transform resolvedAnchor = anchor != null ? anchor : owner.transform;
        bool anchorChanged = effect.Anchor != resolvedAnchor;
        bool becomesVisible = shouldShow && effect.TargetEmissionFactor <= 0f;
        effect.Anchor = resolvedAnchor;
        EnsureEffectParent(effect);
        if (anchorChanged || becomesVisible)
        {
            UpdateEffectScale(effect);
        }
        effect.TargetEmissionFactor = shouldShow ? 1f : 0f;

        if (shouldShow)
        {
            PlayEffect(effect);
        }
    }

    /// <summary>
    /// Libere l'instance quand son proprietaire disparait definitivement (desactivation,
    /// destruction ou changement de scene). Ce n'est pas utilise lors d'un simple passage
    /// Flame allumee/eteinte.
    /// </summary>
    public void ClearInactiveFlameGatedItemVfx(Component owner)
    {
        if (owner == null || !inactiveFlameItemEffects.TryGetValue(owner, out InactiveFlameItemEffect effect))
        {
            return;
        }

        DestroyEffect(effect);
        inactiveFlameItemEffects.Remove(owner);
    }

    private void Update()
    {
        if (inactiveFlameItemEffects.Count == 0)
        {
            return;
        }

        inactiveFlameItemRemovalBuffer.Clear();
        float fadeSpeed = 1f / Mathf.Max(0.01f, inactiveFlameGatedItemEmissionFadeSeconds);
        float delta = Time.unscaledDeltaTime * fadeSpeed;

        foreach (KeyValuePair<Component, InactiveFlameItemEffect> pair in inactiveFlameItemEffects)
        {
            InactiveFlameItemEffect effect = pair.Value;
            if (effect == null || effect.Owner == null || effect.Anchor == null || effect.Instance == null ||
                effect.Owner is Behaviour behaviour && !behaviour.isActiveAndEnabled)
            {
                inactiveFlameItemRemovalBuffer.Add(pair.Key);
                continue;
            }

            float nextFactor = Mathf.MoveTowards(effect.EmissionFactor, effect.TargetEmissionFactor, delta);
            if (!Mathf.Approximately(nextFactor, effect.EmissionFactor))
            {
                effect.EmissionFactor = nextFactor;
                ApplyEmissionFactor(effect);
            }
        }

        for (int i = 0; i < inactiveFlameItemRemovalBuffer.Count; i++)
        {
            Component owner = inactiveFlameItemRemovalBuffer[i];
            if (inactiveFlameItemEffects.TryGetValue(owner, out InactiveFlameItemEffect effect))
            {
                DestroyEffect(effect);
                inactiveFlameItemEffects.Remove(owner);
            }
        }
    }

    private InactiveFlameItemEffect CreateInactiveFlameItemEffect(Component owner, Transform anchor)
    {
        Transform resolvedAnchor = anchor != null ? anchor : owner.transform;
        if (resolvedAnchor == null || inactiveFlameGatedItemVfxPrefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(inactiveFlameGatedItemVfxPrefab, resolvedAnchor.position,
            resolvedAnchor.rotation, resolvedAnchor);
        InactiveFlameItemEffect effect = new InactiveFlameItemEffect
        {
            Owner = owner,
            Anchor = resolvedAnchor,
            Instance = instance,
            InitialLocalScale = instance.transform.localScale,
            EmissionFactor = 0f,
            TargetEmissionFactor = 0f
        };

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            effect.Emissions.Add(new ParticleEmission
            {
                System = particleSystem,
                RateOverTimeMultiplier = emission.rateOverTimeMultiplier,
                RateOverDistanceMultiplier = emission.rateOverDistanceMultiplier
            });
        }

        UpdateEffectScale(effect);
        ApplyEmissionFactor(effect);
        PlayEffect(effect);
        return effect;
    }

    private static void EnsureEffectParent(InactiveFlameItemEffect effect)
    {
        if (effect.Instance != null && effect.Anchor != null && effect.Instance.transform.parent != effect.Anchor)
        {
            effect.Instance.transform.SetParent(effect.Anchor, true);
        }
    }

    private void UpdateEffectScale(InactiveFlameItemEffect effect)
    {
        if (effect.Instance == null || effect.Owner == null)
        {
            return;
        }

        float itemSize = ResolveVisualSize(effect.Owner.transform, effect.Instance.transform);
        float scaleMultiplier = Mathf.Clamp(itemSize * inactiveFlameGatedItemScalePerWorldUnit,
            inactiveFlameGatedItemMinScale, inactiveFlameGatedItemMaxScale);
        effect.Instance.transform.localScale = effect.InitialLocalScale * scaleMultiplier;
    }

    private static float ResolveVisualSize(Transform ownerRoot, Transform vfxRoot)
    {
        if (ownerRoot == null)
        {
            return 1f;
        }

        bool hasBounds = false;
        Bounds bounds = new Bounds(ownerRoot.position, Vector3.zero);
        Renderer[] renderers = ownerRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsPartOfVfx(renderer.transform, vfxRoot))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Collider[] colliders = ownerRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || IsPartOfVfx(collider.transform, vfxRoot))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        return hasBounds ? Mathf.Max(0.01f, bounds.size.x, bounds.size.y, bounds.size.z) : 1f;
    }

    private static bool IsPartOfVfx(Transform candidate, Transform vfxRoot)
    {
        return candidate != null && vfxRoot != null && candidate.IsChildOf(vfxRoot);
    }

    private static void PlayEffect(InactiveFlameItemEffect effect)
    {
        for (int i = 0; i < effect.Emissions.Count; i++)
        {
            ParticleSystem particleSystem = effect.Emissions[i].System;
            if (particleSystem != null && !particleSystem.isPlaying)
            {
                particleSystem.Play(true);
            }
        }
    }

    private static void ApplyEmissionFactor(InactiveFlameItemEffect effect)
    {
        for (int i = 0; i < effect.Emissions.Count; i++)
        {
            ParticleEmission source = effect.Emissions[i];
            if (source.System == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = source.System.emission;
            emission.rateOverTimeMultiplier = source.RateOverTimeMultiplier * effect.EmissionFactor;
            emission.rateOverDistanceMultiplier = source.RateOverDistanceMultiplier * effect.EmissionFactor;
        }
    }

    private static void DestroyEffect(InactiveFlameItemEffect effect)
    {
        if (effect != null && effect.Instance != null)
        {
            Destroy(effect.Instance);
        }
    }
}
