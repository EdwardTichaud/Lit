using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterUnit))]
public class StunnedStatus : MonoBehaviour
{
    private const string DefaultStunMaterialPath = "Assets/Resources/Materials/Material_Symphonie_StunnedUnit.mat";
    private const string DefaultCombatEffectPath = "Assets/Resources/CombatEffects/CombatEffect_Stunned.asset";

    [Header("Visuels")] 
    [SerializeField] private Material stunnedMaterial;
    [SerializeField] private CombatEffectSO combatEffect;

    private sealed class RendererState
    {
        public Renderer Renderer;
        public Material[] OriginalMaterials;
    }

    private readonly List<RendererState> rendererStates = new();
    private GameObject spawnedEffectInstance;
    private bool isStunned;
    private int remainingTurns = -1;

    public bool IsStunned => isStunned;

    private void Awake()
    {
        EnsureConfiguration();
        CacheRendererStates();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (stunnedMaterial == null)
            stunnedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultStunMaterialPath);

        if (combatEffect == null)
            combatEffect = AssetDatabase.LoadAssetAtPath<CombatEffectSO>(DefaultCombatEffectPath);
#endif
    }

    public void Stun(int turns)
    {
        EnsureConfiguration();
        remainingTurns = Mathf.Max(1, turns);

        if (!isStunned)
        {
            EnterStunState();
        }
        else
        {
            EnsureEffectInstance();
        }

        isStunned = true;
    }

    public void TickTurn()
    {
        if (!isStunned || remainingTurns < 0)
            return;

        remainingTurns--;
        if (remainingTurns <= 0)
            Recover();
    }

    public void Recover()
    {
        if (!isStunned && spawnedEffectInstance == null)
            return;

        isStunned = false;
        remainingTurns = -1;
        RestoreMaterials();
        CleanupEffectInstance();
    }

    private void EnterStunState()
    {
        EnsureConfiguration();
        ApplyMaterialOverride();
        EnsureEffectInstance();
    }

    private void EnsureEffectInstance()
    {
        EnsureConfiguration();
        if (combatEffect == null || combatEffect.effectPrefab == null || spawnedEffectInstance != null)
            return;

        spawnedEffectInstance = Instantiate(combatEffect.effectPrefab, transform);
        spawnedEffectInstance.transform.localPosition = combatEffect.spawnOffset;

        if (combatEffect.lifetime > 0f)
            Destroy(spawnedEffectInstance, combatEffect.lifetime);
    }

    private void CleanupEffectInstance()
    {
        if (spawnedEffectInstance == null)
            return;

        if (Application.isPlaying)
            Destroy(spawnedEffectInstance);
        else
            DestroyImmediate(spawnedEffectInstance);

        spawnedEffectInstance = null;
    }

    private void ApplyMaterialOverride()
    {
        if (stunnedMaterial == null)
            return;

        CacheRendererStates();
        foreach (var state in rendererStates)
        {
            if (state.Renderer == null || state.OriginalMaterials == null || state.OriginalMaterials.Length == 0)
                continue;

            var overrideMaterials = new Material[state.OriginalMaterials.Length];
            for (int i = 0; i < overrideMaterials.Length; i++)
                overrideMaterials[i] = stunnedMaterial;

            state.Renderer.sharedMaterials = overrideMaterials;
        }
    }

    private void RestoreMaterials()
    {
        foreach (var state in rendererStates)
        {
            if (state.Renderer == null || state.OriginalMaterials == null)
                continue;

            state.Renderer.sharedMaterials = state.OriginalMaterials;
        }
    }

    private void CacheRendererStates()
    {
        EnsureConfiguration();
        if (rendererStates.Count > 0)
        {
            foreach (var state in rendererStates)
            {
                if (state.Renderer == null)
                    continue;

                state.OriginalMaterials = CloneMaterials(state.Renderer.sharedMaterials);
            }

            return;
        }

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            rendererStates.Add(new RendererState
            {
                Renderer = renderer,
                OriginalMaterials = CloneMaterials(renderer.sharedMaterials)
            });
        }
    }

    private static Material[] CloneMaterials(Material[] source)
    {
        if (source == null)
            return null;

        var clone = new Material[source.Length];
        for (int i = 0; i < source.Length; i++)
            clone[i] = source[i];

        return clone;
    }

    private void OnDisable()
    {
        Recover();
    }

    private void EnsureConfiguration()
    {
#if UNITY_EDITOR
        if (stunnedMaterial == null)
            stunnedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultStunMaterialPath);

        if (combatEffect == null)
            combatEffect = AssetDatabase.LoadAssetAtPath<CombatEffectSO>(DefaultCombatEffectPath);
#else
        if (stunnedMaterial == null)
            stunnedMaterial = Resources.Load<Material>("Materials/Material_Symphonie_StunnedUnit");

        if (combatEffect == null)
            combatEffect = Resources.Load<CombatEffectSO>("CombatEffects/CombatEffect_Stunned");
#endif
    }
}
