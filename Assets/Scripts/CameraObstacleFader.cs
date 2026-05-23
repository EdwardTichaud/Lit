using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraObstacleFader : MonoBehaviour
{
    private sealed class RendererFadeState
    {
        public Renderer renderer;
        public MaterialPropertyBlock propertyBlock;
        public float currentAmount;
        public float targetAmount;
        public bool capturedRendererEnabled;
        public bool originalRendererEnabled;
        public bool originalForceRenderingOff;
        public bool hasBaseColor;
        public bool supportsShaderFade;
        public Color baseColor;
    }

    [Header("Fade")]
    [SerializeField, Tooltip("Applique les valeurs de fade aux renderers obstruants. Les shaders doivent lire les proprietes ci-dessous pour afficher une vraie transparence/dither.")]
    private bool fadeEnabled = true;
    [SerializeField, Range(0f, 1f), Tooltip("Valeur envoyee aux renderers obstruants.")]
    private float obstructedFadeAmount = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("Valeur restauree quand le renderer n'obstrue plus la vue.")]
    private float clearFadeAmount = 0f;
    [SerializeField, Min(0f), Tooltip("Vitesse de fade quand un obstacle apparait entre la camera et le joueur.")]
    private float fadeInSpeed = 8f;
    [SerializeField, Min(0f), Tooltip("Vitesse de restauration quand l'obstacle ne masque plus le joueur.")]
    private float fadeOutSpeed = 10f;

    [Header("Shader Properties")]
    [SerializeField, Tooltip("Propriete float reservee aux shaders d'obstruction. 0 = normal, 1 = obstruant.")]
    private string obstructionFadeProperty = "_CameraObstructionFade";
    [SerializeField, Tooltip("Propriete float optionnelle. 1 = opaque, 0 = invisible.")]
    private string obstructionAlphaProperty = "_CameraObstructionAlpha";
    [SerializeField, Tooltip("Option de secours pour les shaders transparents HDRP qui utilisent _BaseColor alpha. Laisser desactive si les murs restent opaques.")]
    private bool writeBaseColorAlpha = false;
    [SerializeField, Tooltip("Nom de la couleur principale HDRP/ShaderGraph si writeBaseColorAlpha est actif.")]
    private string baseColorProperty = "_BaseColor";

    [Header("Opaque Fallback")]
    [SerializeField, Tooltip("Secours radical : desactive temporairement le Renderer si aucun material ne semble supporter les proprietes d'obstruction.")]
    private bool hideRendererFallback = true;
    [SerializeField, Range(0f, 1f)]
    private float hideRendererThreshold = 0.65f;

    private readonly Dictionary<Renderer, RendererFadeState> states = new Dictionary<Renderer, RendererFadeState>();
    private readonly List<Renderer> stateKeys = new List<Renderer>(32);
    private int obstructionFadeId;
    private int obstructionAlphaId;
    private int baseColorId;

    private void Awake()
    {
        CacheShaderPropertyIds();
    }

    private void OnDisable()
    {
        RestoreAllImmediate();
    }

    private void OnDestroy()
    {
        RestoreAllImmediate();
    }

    private void OnValidate()
    {
        obstructedFadeAmount = Mathf.Clamp01(obstructedFadeAmount);
        clearFadeAmount = Mathf.Clamp01(clearFadeAmount);
        fadeInSpeed = Mathf.Max(0f, fadeInSpeed);
        fadeOutSpeed = Mathf.Max(0f, fadeOutSpeed);
        hideRendererThreshold = Mathf.Clamp01(hideRendererThreshold);
        CacheShaderPropertyIds();
    }

    public void ApplyObstructions(IReadOnlyList<Renderer> obstructingRenderers, float deltaTime)
    {
        if (!fadeEnabled)
        {
            RestoreAllImmediate();
            return;
        }

        CacheShaderPropertyIds();
        float safeDeltaTime = deltaTime > 0f ? deltaTime : Time.unscaledDeltaTime;
        if (safeDeltaTime <= 0f)
        {
            safeDeltaTime = 1f / 60f;
        }

        MarkAllStatesClear();
        MarkObstructingRenderers(obstructingRenderers);
        TickStates(safeDeltaTime);
    }

    public void RestoreAllImmediate()
    {
        stateKeys.Clear();
        foreach (KeyValuePair<Renderer, RendererFadeState> pair in states)
        {
            stateKeys.Add(pair.Key);
        }

        for (int i = 0; i < stateKeys.Count; i++)
        {
            if (states.TryGetValue(stateKeys[i], out RendererFadeState state))
            {
                RestoreStateImmediate(state);
            }
        }

        states.Clear();
        stateKeys.Clear();
    }

    private void MarkAllStatesClear()
    {
        foreach (KeyValuePair<Renderer, RendererFadeState> pair in states)
        {
            pair.Value.targetAmount = clearFadeAmount;
        }
    }

    private void MarkObstructingRenderers(IReadOnlyList<Renderer> obstructingRenderers)
    {
        if (obstructingRenderers == null)
        {
            return;
        }

        for (int i = 0; i < obstructingRenderers.Count; i++)
        {
            Renderer renderer = obstructingRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            RendererFadeState state = GetOrCreateState(renderer);
            state.targetAmount = obstructedFadeAmount;
        }
    }

    private RendererFadeState GetOrCreateState(Renderer renderer)
    {
        if (states.TryGetValue(renderer, out RendererFadeState state))
        {
            return state;
        }

        state = new RendererFadeState
        {
            renderer = renderer,
            propertyBlock = new MaterialPropertyBlock(),
            currentAmount = clearFadeAmount,
            targetAmount = clearFadeAmount,
            capturedRendererEnabled = true,
            originalRendererEnabled = renderer.enabled,
            originalForceRenderingOff = renderer.forceRenderingOff
        };

        Material sharedMaterial = renderer.sharedMaterial;
        if (sharedMaterial != null && sharedMaterial.HasProperty(baseColorId))
        {
            state.hasBaseColor = true;
            state.baseColor = sharedMaterial.GetColor(baseColorId);
        }

        Material[] sharedMaterials = renderer.sharedMaterials;
        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material material = sharedMaterials[i];
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty(obstructionFadeId) ||
                material.HasProperty(obstructionAlphaId) ||
                writeBaseColorAlpha && material.HasProperty(baseColorId))
            {
                state.supportsShaderFade = true;
                break;
            }
        }

        states.Add(renderer, state);
        ApplyFadeAmount(state, clearFadeAmount);
        return state;
    }

    private void TickStates(float deltaTime)
    {
        stateKeys.Clear();
        foreach (KeyValuePair<Renderer, RendererFadeState> pair in states)
        {
            stateKeys.Add(pair.Key);
        }

        for (int i = 0; i < stateKeys.Count; i++)
        {
            Renderer renderer = stateKeys[i];
            if (!states.TryGetValue(renderer, out RendererFadeState state))
            {
                continue;
            }

            if (renderer == null)
            {
                states.Remove(renderer);
                continue;
            }

            float speed = state.targetAmount > state.currentAmount ? fadeInSpeed : fadeOutSpeed;
            float t = speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * deltaTime);
            state.currentAmount = Mathf.Lerp(state.currentAmount, state.targetAmount, t);
            if (Mathf.Abs(state.currentAmount - state.targetAmount) <= 0.001f)
            {
                state.currentAmount = state.targetAmount;
            }

            ApplyFadeAmount(state, state.currentAmount);

            if (Mathf.Approximately(state.targetAmount, clearFadeAmount) &&
                Mathf.Abs(state.currentAmount - clearFadeAmount) <= 0.001f)
            {
                RestoreStateImmediate(state);
                states.Remove(renderer);
            }
        }

        stateKeys.Clear();
    }

    private void ApplyFadeAmount(RendererFadeState state, float amount)
    {
        Renderer renderer = state.renderer;
        if (renderer == null)
        {
            return;
        }

        renderer.GetPropertyBlock(state.propertyBlock);
        state.propertyBlock.SetFloat(obstructionFadeId, amount);
        state.propertyBlock.SetFloat(obstructionAlphaId, 1f - amount);

        if (writeBaseColorAlpha && state.hasBaseColor)
        {
            Color faded = state.baseColor;
            faded.a = state.baseColor.a * (1f - amount);
            state.propertyBlock.SetColor(baseColorId, faded);
        }

        renderer.SetPropertyBlock(state.propertyBlock);

        if (hideRendererFallback && !state.supportsShaderFade && state.capturedRendererEnabled)
        {
            // Keeps the player visible while old opaque materials are migrated to a proper obstruction shader.
            renderer.forceRenderingOff = state.originalForceRenderingOff || amount >= hideRendererThreshold;
        }
    }

    private void RestoreStateImmediate(RendererFadeState state)
    {
        if (state == null || state.renderer == null)
        {
            return;
        }

        ApplyFadeAmount(state, clearFadeAmount);
        if (hideRendererFallback && !state.supportsShaderFade && state.capturedRendererEnabled)
        {
            state.renderer.enabled = state.originalRendererEnabled;
            state.renderer.forceRenderingOff = state.originalForceRenderingOff;
        }
    }

    private void CacheShaderPropertyIds()
    {
        obstructionFadeId = Shader.PropertyToID(string.IsNullOrWhiteSpace(obstructionFadeProperty)
            ? "_CameraObstructionFade"
            : obstructionFadeProperty);
        obstructionAlphaId = Shader.PropertyToID(string.IsNullOrWhiteSpace(obstructionAlphaProperty)
            ? "_CameraObstructionAlpha"
            : obstructionAlphaProperty);
        baseColorId = Shader.PropertyToID(string.IsNullOrWhiteSpace(baseColorProperty)
            ? "_BaseColor"
            : baseColorProperty);
    }
}
