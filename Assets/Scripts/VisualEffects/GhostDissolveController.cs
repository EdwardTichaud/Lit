using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.VFX;

[DisallowMultipleComponent]
public class GhostDissolveController : MonoBehaviour
{
    [Serializable]
    public sealed class DustVfxBinding
    {
        [Tooltip("Visual Effect Graph instance that emits ash/dust.")]
        public VisualEffect visualEffect;

        [Tooltip("Skinned mesh sampled by this VFX Graph. Leave empty to use the first SkinnedMeshRenderer found.")]
        public SkinnedMeshRenderer sourceRenderer;

        [Min(0f), Tooltip("Per-effect multiplier sent to the VFX Graph as SpawnRateMultiplier.")]
        public float spawnRateMultiplier = 1f;
    }

    private sealed class RendererBinding
    {
        public readonly Renderer renderer;
        public readonly MaterialPropertyBlock[] propertyBlocks;

        public RendererBinding(Renderer renderer, int materialCount)
        {
            this.renderer = renderer;
            propertyBlocks = new MaterialPropertyBlock[Mathf.Max(1, materialCount)];
            for (int i = 0; i < propertyBlocks.Length; i++)
            {
                propertyBlocks[i] = new MaterialPropertyBlock();
            }
        }
    }

    private sealed class RuntimeVfxBinding
    {
        public VisualEffect visualEffect;
        public SkinnedMeshRenderer sourceRenderer;
        public float spawnRateMultiplier;
    }

    [Header("Renderer Collection")]
    [SerializeField] private Transform rendererRoot;
    [SerializeField] private Renderer[] explicitRenderers = Array.Empty<Renderer>();
    [SerializeField] private bool autoCollectRenderersOnAwake = true;
    [SerializeField] private bool includeInactiveRenderers = true;
    [SerializeField] private bool includeSkinnedMeshRenderers = true;
    [SerializeField] private bool includeMeshRenderers = true;
    [SerializeField, Tooltip("Refresh combined bounds while dissolving. Keep enabled for root motion and large animation poses.")]
    private bool updateBoundsDuringDissolve = true;
    [SerializeField, Min(0f)] private float boundsHeightPadding = 0.05f;

    [Header("Shader Values")]
    [SerializeField, Min(0f)] private float noiseScale = 2.75f;
    [SerializeField, Min(0.001f)] private float edgeWidth = 0.055f;
    [SerializeField] private Color edgeColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField] private Vector3 dissolveDirection = Vector3.up;

    [Header("Shader Property Names")]
    [SerializeField] private string dissolveAmountProperty = "_DissolveAmount";
    [SerializeField] private string noiseScaleProperty = "_NoiseScale";
    [SerializeField] private string edgeWidthProperty = "_EdgeWidth";
    [SerializeField] private string edgeColorProperty = "_EdgeColor";
    [SerializeField] private string ghostAlphaProperty = "_GhostAlpha";
    [SerializeField] private string dissolveWorldMinYProperty = "_DissolveWorldMinY";
    [SerializeField] private string dissolveWorldHeightProperty = "_DissolveWorldHeight";
    [SerializeField] private string dissolveDirectionProperty = "_DissolveDirection";

    [Header("Dissolve Animation")]
    [SerializeField, Min(0.01f)] private float dissolveDuration = 2.8f;
    [SerializeField, Min(0f)] private float startDelay = 0f;
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private bool interruptAndRestart = true;
    [SerializeField] private float startDissolveAmount = -0.08f;
    [SerializeField] private float finalDissolveAmount = 1.12f;
    [SerializeField, Range(0f, 1f)] private float startGhostAlpha = 0.68f;
    [SerializeField, Range(0f, 1f)] private float finalGhostAlpha = 0f;
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve ghostFadeCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.35f, 0.18f),
        new Keyframe(0.8f, 0.7f),
        new Keyframe(1f, 1f));

    [Header("Lifecycle")]
    [SerializeField] private bool resetOnEnable = true;
    [SerializeField] private bool disableRenderersOnFinish = true;
    [SerializeField] private bool deactivateGameObjectOnFinish = false;
    [SerializeField] private bool destroyGameObjectOnFinish = false;
    [SerializeField, Min(0f), Tooltip("Delay after dissolve finish before deactivate/destroy. Useful when VFX children need time to finish.")]
    private float postDissolveObjectDelay = 0.75f;

    [Header("Collider Trigger")]
    [SerializeField, Tooltip("If enabled, a character entering this GameObject's trigger collider makes the ghost appear; leaving makes it dissolve.")]
    private bool triggerWithCollider = false;
    [SerializeField, Tooltip("Layers accepted by the trigger. Keep Everything unless characters have a dedicated layer.")]
    private LayerMask triggerCharacterLayers = ~0;
    [SerializeField, Tooltip("When enabled, only colliders belonging to a SquadCharacterController can trigger this ghost.")]
    private bool triggerRequiresSquadCharacter = true;
    [SerializeField, Tooltip("Optional root tag filter. Leave empty to accept any character tag.")]
    private string triggerCharacterTag = "";
    [SerializeField, Tooltip("When triggerWithCollider is enabled, start hidden until a character enters the trigger.")]
    private bool hideWhenColliderTriggerStartsEmpty = true;

    [Header("VFX Graph")]
    [SerializeField] private DustVfxBinding[] dustVfxBindings = Array.Empty<DustVfxBinding>();
    [SerializeField] private bool autoFindChildVisualEffects = true;
    [SerializeField] private bool autoBindFirstSkinnedMeshRenderer = true;
    [SerializeField] private string vfxStartEventName = "OnDissolveStart";
    [SerializeField] private string vfxFinishEventName = "OnDissolveFinish";
    [SerializeField] private string vfxAppearStartEventName = "OnAppearStart";
    [SerializeField] private string vfxAppearFinishEventName = "OnAppearFinish";
    [SerializeField] private string vfxSkinnedMeshRendererProperty = "SourceSkinnedMesh";
    [SerializeField] private string vfxDissolveAmountProperty = "DissolveAmount";
    [SerializeField] private string vfxGhostAlphaProperty = "GhostAlpha";
    [SerializeField] private string vfxDissolveMinYProperty = "DissolveWorldMinY";
    [SerializeField] private string vfxDissolveHeightProperty = "DissolveWorldHeight";
    [SerializeField] private string vfxDissolveDirectionProperty = "DissolveDirection";
    [SerializeField] private string vfxEdgeColorProperty = "EdgeColor";
    [SerializeField] private string vfxSpawnRateMultiplierProperty = "SpawnRateMultiplier";
    [SerializeField] private string vfxNormalizedTimeProperty = "DissolveNormalizedTime";

    [Header("Events")]
    public UnityEvent OnDissolveStarted = new UnityEvent();
    public UnityEvent OnDissolveFinished = new UnityEvent();
    public UnityEvent OnAppearStarted = new UnityEvent();
    public UnityEvent OnAppearFinished = new UnityEvent();

    private readonly List<Renderer> collectedRenderers = new List<Renderer>(8);
    private readonly List<RendererBinding> rendererBindings = new List<RendererBinding>(8);
    private readonly List<RuntimeVfxBinding> runtimeVfxBindings = new List<RuntimeVfxBinding>(4);
    private readonly HashSet<Renderer> rendererSet = new HashSet<Renderer>();
    private readonly Dictionary<Collider, GameObject> triggerColliderCharacters = new Dictionary<Collider, GameObject>();
    private readonly Dictionary<GameObject, int> triggerCharacterColliderCounts = new Dictionary<GameObject, int>();
    private readonly List<Collider> triggerColliderCleanupBuffer = new List<Collider>(8);
    private readonly List<GameObject> triggerCharacterCleanupBuffer = new List<GameObject>(4);

    private Coroutine dissolveRoutine;
    private bool isAppearing;
    private Bounds cachedDissolveBounds;
    private bool hasCachedDissolveBounds;
    private SkinnedMeshRenderer firstSkinnedRenderer;

    private int dissolveAmountId;
    private int noiseScaleId;
    private int edgeWidthId;
    private int edgeColorId;
    private int ghostAlphaId;
    private int dissolveWorldMinYId;
    private int dissolveWorldHeightId;
    private int dissolveDirectionId;

    private int vfxSkinnedMeshRendererId;
    private int vfxDissolveAmountId;
    private int vfxGhostAlphaId;
    private int vfxDissolveMinYId;
    private int vfxDissolveHeightId;
    private int vfxDissolveDirectionId;
    private int vfxEdgeColorId;
    private int vfxSpawnRateMultiplierId;
    private int vfxNormalizedTimeId;

    public bool IsDissolving => dissolveRoutine != null && !isAppearing;
    public bool IsAppearing => dissolveRoutine != null && isAppearing;
    public bool IsTransitioning => dissolveRoutine != null;
    public float CurrentDissolveAmount { get; private set; }
    public float CurrentGhostAlpha { get; private set; }
    public float NormalizedTime { get; private set; }

    private void Awake()
    {
        CachePropertyIds();
        RebuildRendererCache();
        RebuildVfxCache();
    }

    private void OnEnable()
    {
        if (resetOnEnable)
        {
            ResetDissolve();
        }

        if (triggerWithCollider && hideWhenColliderTriggerStartsEmpty && triggerCharacterColliderCounts.Count == 0)
        {
            HideInstant();
        }
    }

    private void OnDisable()
    {
        if (dissolveRoutine != null)
        {
            StopCoroutine(dissolveRoutine);
            dissolveRoutine = null;
        }

        isAppearing = false;
        triggerColliderCharacters.Clear();
        triggerCharacterColliderCounts.Clear();
    }

    private void Reset()
    {
        rendererRoot = transform;
        explicitRenderers = GetComponentsInChildren<Renderer>(true);
    }

    [ContextMenu("Collect Renderers")]
    public void CollectRenderers()
    {
        RebuildRendererCache();
        RebuildVfxCache();
        ApplyCurrentProperties();
    }

    [ContextMenu("Trigger Dissolve")]
    public void TriggerDissolve()
    {
        TriggerDissolve(dissolveDuration);
    }

    public void TriggerDissolve(float durationOverride)
    {
        if (!StopActiveRoutineIfAllowed())
        {
            return;
        }

        RebuildRendererCache();
        RebuildVfxCache();
        SetRenderersEnabled(true);
        isAppearing = false;
        dissolveRoutine = StartCoroutine(DissolveRoutine(Mathf.Max(0.01f, durationOverride)));
    }

    // AnimationEvent-friendly alias.
    public void StartGhostDissolve()
    {
        TriggerDissolve();
    }

    [ContextMenu("Trigger Appear")]
    public void TriggerAppear()
    {
        TriggerAppear(dissolveDuration);
    }

    public void TriggerAppear(float durationOverride)
    {
        if (!StopActiveRoutineIfAllowed())
        {
            return;
        }

        RebuildRendererCache();
        RebuildVfxCache();
        SetRenderersEnabled(true);
        isAppearing = true;
        dissolveRoutine = StartCoroutine(AppearRoutine(Mathf.Max(0.01f, durationOverride)));
    }

    // AnimationEvent-friendly alias.
    public void StartGhostAppear()
    {
        TriggerAppear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerWithCollider || !TryResolveTriggerCharacter(other, out GameObject character))
        {
            return;
        }

        bool wasEmpty = GetActiveTriggerCharacterCount() == 0;
        RegisterTriggerCharacterCollider(other, character);

        if (wasEmpty)
        {
            TriggerAppear();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!triggerWithCollider || !TryResolveTriggerCharacter(other, out GameObject character))
        {
            return;
        }

        if (triggerColliderCharacters.ContainsKey(other))
        {
            return;
        }

        bool wasEmpty = GetActiveTriggerCharacterCount() == 0;
        RegisterTriggerCharacterCollider(other, character);

        if (wasEmpty)
        {
            TriggerAppear();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!triggerWithCollider)
        {
            return;
        }

        if (!UnregisterTriggerCharacterCollider(other) || GetActiveTriggerCharacterCount() > 0)
        {
            return;
        }

        TriggerDissolve();
    }

    public void SetDissolveAmount(float amount)
    {
        NormalizedTime = Mathf.InverseLerp(startDissolveAmount, finalDissolveAmount, amount);
        CurrentDissolveAmount = amount;
        CurrentGhostAlpha = EvaluateGhostAlphaForAmount(amount);
        ApplyCurrentProperties();
    }

    public void LerpDissolveAmount(float targetAmount, float duration)
    {
        if (!StopActiveRoutineIfAllowed())
        {
            return;
        }

        RebuildRendererCache();
        RebuildVfxCache();
        SetRenderersEnabled(true);

        if (duration <= 0f)
        {
            SetDissolveAmount(targetAmount);
            return;
        }

        isAppearing = targetAmount < CurrentDissolveAmount;
        dissolveRoutine = StartCoroutine(DissolveAmountRoutine(CurrentDissolveAmount, targetAmount, Mathf.Max(0.01f, duration)));
    }

    public void ResetDissolve()
    {
        if (dissolveRoutine != null)
        {
            StopCoroutine(dissolveRoutine);
            dissolveRoutine = null;
        }

        isAppearing = false;
        RebuildRendererCache();
        RebuildVfxCache();
        SetRenderersEnabled(true);
        NormalizedTime = 0f;
        CurrentDissolveAmount = startDissolveAmount;
        CurrentGhostAlpha = startGhostAlpha;
        ApplyCurrentProperties();
    }

    public void HideInstant()
    {
        if (dissolveRoutine != null)
        {
            StopCoroutine(dissolveRoutine);
            dissolveRoutine = null;
        }

        isAppearing = false;
        RebuildRendererCache();
        RebuildVfxCache();
        SetRenderersEnabled(true);
        NormalizedTime = 1f;
        CurrentDissolveAmount = finalDissolveAmount;
        CurrentGhostAlpha = finalGhostAlpha;
        ApplyCurrentProperties();
    }

    private bool StopActiveRoutineIfAllowed()
    {
        if (dissolveRoutine == null)
        {
            return true;
        }

        if (!interruptAndRestart)
        {
            return false;
        }

        StopCoroutine(dissolveRoutine);
        dissolveRoutine = null;
        isAppearing = false;
        return true;
    }

    private bool TryResolveTriggerCharacter(Collider other, out GameObject character)
    {
        character = null;
        if (other == null)
        {
            return false;
        }

        SquadCharacterController controller = other.GetComponentInParent<SquadCharacterController>();
        if (controller == null && other.attachedRigidbody != null)
        {
            controller = other.attachedRigidbody.GetComponentInParent<SquadCharacterController>();
        }

        if (controller != null)
        {
            character = controller.gameObject;
        }
        else if (!triggerRequiresSquadCharacter)
        {
            Transform actorTransform = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
            character = actorTransform != null ? actorTransform.gameObject : other.gameObject;
        }

        if (character == null || !character.activeInHierarchy)
        {
            return false;
        }

        int characterLayerMask = 1 << character.layer;
        int colliderLayerMask = 1 << other.gameObject.layer;
        if ((triggerCharacterLayers.value & (characterLayerMask | colliderLayerMask)) == 0)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(triggerCharacterTag) || character.tag == triggerCharacterTag;
    }

    private bool RegisterTriggerCharacterCollider(Collider sourceCollider, GameObject character)
    {
        if (ReferenceEquals(sourceCollider, null) || character == null || triggerColliderCharacters.ContainsKey(sourceCollider))
        {
            return false;
        }

        triggerColliderCharacters[sourceCollider] = character;
        if (!triggerCharacterColliderCounts.TryGetValue(character, out int count))
        {
            triggerCharacterColliderCounts[character] = 1;
            return true;
        }

        triggerCharacterColliderCounts[character] = count + 1;
        return false;
    }

    private bool UnregisterTriggerCharacterCollider(Collider sourceCollider)
    {
        if (ReferenceEquals(sourceCollider, null) || !triggerColliderCharacters.TryGetValue(sourceCollider, out GameObject character))
        {
            return false;
        }

        triggerColliderCharacters.Remove(sourceCollider);
        return UnregisterTriggerCharacterCollider(character);
    }

    private bool UnregisterTriggerCharacterCollider(GameObject character)
    {
        if (character == null || !triggerCharacterColliderCounts.TryGetValue(character, out int count))
        {
            return false;
        }

        count -= 1;
        if (count > 0)
        {
            triggerCharacterColliderCounts[character] = count;
            return false;
        }

        triggerCharacterColliderCounts.Remove(character);
        return true;
    }

    private int GetActiveTriggerCharacterCount()
    {
        if (triggerCharacterColliderCounts.Count == 0)
        {
            return 0;
        }

        triggerColliderCleanupBuffer.Clear();
        foreach (KeyValuePair<Collider, GameObject> pair in triggerColliderCharacters)
        {
            Collider sourceCollider = pair.Key;
            GameObject character = pair.Value;
            if (sourceCollider == null || character == null || !character.activeInHierarchy)
            {
                triggerColliderCleanupBuffer.Add(sourceCollider);
            }
        }

        for (int i = 0; i < triggerColliderCleanupBuffer.Count; i++)
        {
            UnregisterTriggerCharacterCollider(triggerColliderCleanupBuffer[i]);
        }

        triggerColliderCleanupBuffer.Clear();
        triggerCharacterCleanupBuffer.Clear();
        foreach (KeyValuePair<GameObject, int> pair in triggerCharacterColliderCounts)
        {
            GameObject character = pair.Key;
            if (character == null || !character.activeInHierarchy)
            {
                triggerCharacterCleanupBuffer.Add(character);
            }
        }

        for (int i = 0; i < triggerCharacterCleanupBuffer.Count; i++)
        {
            triggerCharacterColliderCounts.Remove(triggerCharacterCleanupBuffer[i]);
        }

        triggerCharacterCleanupBuffer.Clear();
        return triggerCharacterColliderCounts.Count;
    }

    private IEnumerator DissolveRoutine(float duration)
    {
        if (startDelay > 0f)
        {
            yield return WaitForSecondsFlexible(startDelay);
        }

        isAppearing = false;
        NormalizedTime = 0f;
        CurrentDissolveAmount = startDissolveAmount;
        CurrentGhostAlpha = startGhostAlpha;
        ApplyCurrentProperties();
        SendVfxEvent(vfxStartEventName);
        OnDissolveStarted.Invoke();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            NormalizedTime = Mathf.Clamp01(elapsed / duration);

            float dissolveT = EvaluateCurve(dissolveCurve, NormalizedTime);
            float ghostT = EvaluateCurve(ghostFadeCurve, NormalizedTime);
            CurrentDissolveAmount = Mathf.LerpUnclamped(startDissolveAmount, finalDissolveAmount, dissolveT);
            CurrentGhostAlpha = Mathf.Lerp(startGhostAlpha, finalGhostAlpha, ghostT);
            ApplyCurrentProperties();

            yield return null;
        }

        NormalizedTime = 1f;
        CurrentDissolveAmount = finalDissolveAmount;
        CurrentGhostAlpha = finalGhostAlpha;
        ApplyCurrentProperties();
        SendVfxEvent(vfxFinishEventName);
        OnDissolveFinished.Invoke();

        if (disableRenderersOnFinish)
        {
            SetRenderersEnabled(false);
        }

        if (deactivateGameObjectOnFinish || destroyGameObjectOnFinish)
        {
            if (postDissolveObjectDelay > 0f)
            {
                yield return WaitForSecondsFlexible(postDissolveObjectDelay);
            }

            dissolveRoutine = null;
            isAppearing = false;
            if (destroyGameObjectOnFinish)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }

            yield break;
        }

        dissolveRoutine = null;
        isAppearing = false;
    }

    private IEnumerator AppearRoutine(float duration)
    {
        if (startDelay > 0f)
        {
            yield return WaitForSecondsFlexible(startDelay);
        }

        isAppearing = true;
        NormalizedTime = 0f;
        CurrentDissolveAmount = finalDissolveAmount;
        CurrentGhostAlpha = finalGhostAlpha;
        ApplyCurrentProperties();
        SendVfxEvent(vfxAppearStartEventName);
        OnAppearStarted.Invoke();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            NormalizedTime = Mathf.Clamp01(elapsed / duration);

            float dissolveT = EvaluateCurve(dissolveCurve, NormalizedTime);
            float ghostT = EvaluateCurve(ghostFadeCurve, NormalizedTime);
            CurrentDissolveAmount = Mathf.LerpUnclamped(finalDissolveAmount, startDissolveAmount, dissolveT);
            CurrentGhostAlpha = Mathf.Lerp(finalGhostAlpha, startGhostAlpha, ghostT);
            ApplyCurrentProperties();

            yield return null;
        }

        NormalizedTime = 1f;
        CurrentDissolveAmount = startDissolveAmount;
        CurrentGhostAlpha = startGhostAlpha;
        ApplyCurrentProperties();
        SendVfxEvent(vfxAppearFinishEventName);
        OnAppearFinished.Invoke();

        dissolveRoutine = null;
        isAppearing = false;
    }

    private IEnumerator DissolveAmountRoutine(float startAmount, float targetAmount, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            NormalizedTime = Mathf.Clamp01(elapsed / duration);
            float t = EvaluateCurve(dissolveCurve, NormalizedTime);
            CurrentDissolveAmount = Mathf.LerpUnclamped(startAmount, targetAmount, t);
            CurrentGhostAlpha = EvaluateGhostAlphaForAmount(CurrentDissolveAmount);
            ApplyCurrentProperties();
            yield return null;
        }

        SetDissolveAmount(targetAmount);
        dissolveRoutine = null;
        isAppearing = false;
    }

    private void CachePropertyIds()
    {
        dissolveAmountId = Shader.PropertyToID(dissolveAmountProperty);
        noiseScaleId = Shader.PropertyToID(noiseScaleProperty);
        edgeWidthId = Shader.PropertyToID(edgeWidthProperty);
        edgeColorId = Shader.PropertyToID(edgeColorProperty);
        ghostAlphaId = Shader.PropertyToID(ghostAlphaProperty);
        dissolveWorldMinYId = Shader.PropertyToID(dissolveWorldMinYProperty);
        dissolveWorldHeightId = Shader.PropertyToID(dissolveWorldHeightProperty);
        dissolveDirectionId = Shader.PropertyToID(dissolveDirectionProperty);

        vfxSkinnedMeshRendererId = Shader.PropertyToID(vfxSkinnedMeshRendererProperty);
        vfxDissolveAmountId = Shader.PropertyToID(vfxDissolveAmountProperty);
        vfxGhostAlphaId = Shader.PropertyToID(vfxGhostAlphaProperty);
        vfxDissolveMinYId = Shader.PropertyToID(vfxDissolveMinYProperty);
        vfxDissolveHeightId = Shader.PropertyToID(vfxDissolveHeightProperty);
        vfxDissolveDirectionId = Shader.PropertyToID(vfxDissolveDirectionProperty);
        vfxEdgeColorId = Shader.PropertyToID(vfxEdgeColorProperty);
        vfxSpawnRateMultiplierId = Shader.PropertyToID(vfxSpawnRateMultiplierProperty);
        vfxNormalizedTimeId = Shader.PropertyToID(vfxNormalizedTimeProperty);
    }

    private void RebuildRendererCache()
    {
        collectedRenderers.Clear();
        rendererBindings.Clear();
        rendererSet.Clear();
        firstSkinnedRenderer = null;

        for (int i = 0; i < explicitRenderers.Length; i++)
        {
            AddRenderer(explicitRenderers[i]);
        }

        if (autoCollectRenderersOnAwake)
        {
            Transform root = rendererRoot != null ? rendererRoot : transform;
            Renderer[] childRenderers = root.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
            for (int i = 0; i < childRenderers.Length; i++)
            {
                AddRenderer(childRenderers[i]);
            }
        }

        for (int i = 0; i < collectedRenderers.Count; i++)
        {
            Renderer renderer = collectedRenderers[i];
            int materialCount = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 1;
            rendererBindings.Add(new RendererBinding(renderer, materialCount));
        }

        hasCachedDissolveBounds = false;
        RefreshDissolveBounds();
    }

    private void AddRenderer(Renderer renderer)
    {
        if (renderer == null || rendererSet.Contains(renderer) || !ShouldUseRenderer(renderer))
        {
            return;
        }

        rendererSet.Add(renderer);
        collectedRenderers.Add(renderer);

        if (firstSkinnedRenderer == null && renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            firstSkinnedRenderer = skinnedMeshRenderer;
        }
    }

    private bool ShouldUseRenderer(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer)
        {
            return includeSkinnedMeshRenderers;
        }

        if (renderer is MeshRenderer)
        {
            return includeMeshRenderers;
        }

        return false;
    }

    private void RebuildVfxCache()
    {
        runtimeVfxBindings.Clear();

        if (dustVfxBindings != null)
        {
            for (int i = 0; i < dustVfxBindings.Length; i++)
            {
                DustVfxBinding binding = dustVfxBindings[i];
                if (binding == null || binding.visualEffect == null)
                {
                    continue;
                }

                AddRuntimeVfxBinding(binding.visualEffect, binding.sourceRenderer, binding.spawnRateMultiplier);
            }
        }

        if (runtimeVfxBindings.Count == 0 && autoFindChildVisualEffects)
        {
            VisualEffect[] childEffects = GetComponentsInChildren<VisualEffect>(true);
            for (int i = 0; i < childEffects.Length; i++)
            {
                AddRuntimeVfxBinding(childEffects[i], null, 1f);
            }
        }
    }

    private void AddRuntimeVfxBinding(VisualEffect visualEffect, SkinnedMeshRenderer sourceRenderer, float spawnRateMultiplier)
    {
        RuntimeVfxBinding binding = new RuntimeVfxBinding
        {
            visualEffect = visualEffect,
            sourceRenderer = sourceRenderer != null || !autoBindFirstSkinnedMeshRenderer ? sourceRenderer : firstSkinnedRenderer,
            spawnRateMultiplier = Mathf.Max(0f, spawnRateMultiplier)
        };

        runtimeVfxBindings.Add(binding);
    }

    private void ApplyCurrentProperties()
    {
        if (updateBoundsDuringDissolve || !hasCachedDissolveBounds)
        {
            RefreshDissolveBounds();
        }

        float minY = hasCachedDissolveBounds ? cachedDissolveBounds.min.y - boundsHeightPadding : transform.position.y;
        float height = hasCachedDissolveBounds ? cachedDissolveBounds.size.y + boundsHeightPadding * 2f : 1f;
        height = Mathf.Max(0.001f, height);
        Vector3 direction = dissolveDirection.sqrMagnitude > 0.0001f ? dissolveDirection.normalized : Vector3.up;

        for (int i = 0; i < rendererBindings.Count; i++)
        {
            ApplyRendererProperties(rendererBindings[i], minY, height, direction);
        }

        ApplyVfxProperties(minY, height, direction);
    }

    private void ApplyRendererProperties(RendererBinding binding, float minY, float height, Vector3 direction)
    {
        if (binding == null || binding.renderer == null)
        {
            return;
        }

        for (int i = 0; i < binding.propertyBlocks.Length; i++)
        {
            MaterialPropertyBlock block = binding.propertyBlocks[i];
            binding.renderer.GetPropertyBlock(block, i);
            block.SetFloat(dissolveAmountId, CurrentDissolveAmount);
            block.SetFloat(noiseScaleId, noiseScale);
            block.SetFloat(edgeWidthId, edgeWidth);
            block.SetColor(edgeColorId, edgeColor);
            block.SetFloat(ghostAlphaId, CurrentGhostAlpha);
            block.SetFloat(dissolveWorldMinYId, minY);
            block.SetFloat(dissolveWorldHeightId, height);
            block.SetVector(dissolveDirectionId, direction);
            binding.renderer.SetPropertyBlock(block, i);
        }
    }

    private void ApplyVfxProperties(float minY, float height, Vector3 direction)
    {
        Vector4 edgeColorVector = edgeColor;
        for (int i = 0; i < runtimeVfxBindings.Count; i++)
        {
            RuntimeVfxBinding binding = runtimeVfxBindings[i];
            VisualEffect effect = binding.visualEffect;
            if (effect == null)
            {
                continue;
            }

            if (binding.sourceRenderer != null && effect.HasSkinnedMeshRenderer(vfxSkinnedMeshRendererId))
            {
                effect.SetSkinnedMeshRenderer(vfxSkinnedMeshRendererId, binding.sourceRenderer);
            }

            if (effect.HasFloat(vfxDissolveAmountId))
            {
                effect.SetFloat(vfxDissolveAmountId, CurrentDissolveAmount);
            }

            if (effect.HasFloat(vfxGhostAlphaId))
            {
                effect.SetFloat(vfxGhostAlphaId, CurrentGhostAlpha);
            }

            if (effect.HasFloat(vfxDissolveMinYId))
            {
                effect.SetFloat(vfxDissolveMinYId, minY);
            }

            if (effect.HasFloat(vfxDissolveHeightId))
            {
                effect.SetFloat(vfxDissolveHeightId, height);
            }

            if (effect.HasFloat(vfxSpawnRateMultiplierId))
            {
                effect.SetFloat(vfxSpawnRateMultiplierId, binding.spawnRateMultiplier);
            }

            if (effect.HasFloat(vfxNormalizedTimeId))
            {
                effect.SetFloat(vfxNormalizedTimeId, NormalizedTime);
            }

            if (effect.HasVector3(vfxDissolveDirectionId))
            {
                effect.SetVector3(vfxDissolveDirectionId, direction);
            }

            if (effect.HasVector4(vfxEdgeColorId))
            {
                effect.SetVector4(vfxEdgeColorId, edgeColorVector);
            }
        }
    }

    private void SendVfxEvent(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        for (int i = 0; i < runtimeVfxBindings.Count; i++)
        {
            VisualEffect effect = runtimeVfxBindings[i].visualEffect;
            if (effect != null)
            {
                effect.SendEvent(eventName);
            }
        }
    }

    private void RefreshDissolveBounds()
    {
        hasCachedDissolveBounds = false;
        Bounds combinedBounds = new Bounds(transform.position, Vector3.zero);

        for (int i = 0; i < rendererBindings.Count; i++)
        {
            Renderer renderer = rendererBindings[i].renderer;
            if (renderer == null)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (!IsUsableBounds(bounds))
            {
                continue;
            }

            if (!hasCachedDissolveBounds)
            {
                combinedBounds = bounds;
                hasCachedDissolveBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(bounds);
            }
        }

        cachedDissolveBounds = combinedBounds;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        for (int i = 0; i < rendererBindings.Count; i++)
        {
            Renderer renderer = rendererBindings[i].renderer;
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }
    }

    private IEnumerator WaitForSecondsFlexible(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += GetDeltaTime();
            yield return null;
        }
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private static float EvaluateCurve(AnimationCurve curve, float time)
    {
        return curve != null ? curve.Evaluate(Mathf.Clamp01(time)) : Mathf.Clamp01(time);
    }

    private float EvaluateGhostAlphaForAmount(float amount)
    {
        float amount01 = Mathf.InverseLerp(startDissolveAmount, finalDissolveAmount, amount);
        return Mathf.Lerp(startGhostAlpha, finalGhostAlpha, EvaluateCurve(ghostFadeCurve, amount01));
    }

    private static bool IsUsableBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        Vector3 center = bounds.center;
        return IsFinite(size.x) &&
            IsFinite(size.y) &&
            IsFinite(size.z) &&
            IsFinite(center.x) &&
            IsFinite(center.y) &&
            IsFinite(center.z) &&
            size.sqrMagnitude > 0.000001f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void OnValidate()
    {
        dissolveDuration = Mathf.Max(0.01f, dissolveDuration);
        startDelay = Mathf.Max(0f, startDelay);
        noiseScale = Mathf.Max(0f, noiseScale);
        edgeWidth = Mathf.Max(0.001f, edgeWidth);
        startGhostAlpha = Mathf.Clamp01(startGhostAlpha);
        finalGhostAlpha = Mathf.Clamp01(finalGhostAlpha);
        boundsHeightPadding = Mathf.Max(0f, boundsHeightPadding);
        postDissolveObjectDelay = Mathf.Max(0f, postDissolveObjectDelay);

        if (destroyGameObjectOnFinish)
        {
            deactivateGameObjectOnFinish = false;
        }
    }
}
