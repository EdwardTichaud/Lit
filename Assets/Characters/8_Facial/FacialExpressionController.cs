using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime controller that owns facial expression BlendShapes independently from body animation.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class FacialExpressionController : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField, Tooltip("SkinnedMeshRenderer that owns the facial BlendShapes, usually CC_Base_Body.")]
    private SkinnedMeshRenderer faceRenderer;

    [SerializeField, Tooltip("If no renderer is assigned, searches children for this renderer name.")]
    private string preferredRendererName = "CC_Base_Body";

    [SerializeField, Tooltip("Automatically resolves the face renderer during Awake if missing.")]
    private bool autoResolveFaceRenderer = true;

    [Header("Jaw Neutralization")]
    [SerializeField, Tooltip("Lower jaw bone. For Character Creator this is usually CC_Base_JawRoot.")]
    private Transform jawRoot;

    [SerializeField, Tooltip("Bone name used when the jaw root is not assigned.")]
    private string jawRootName = "CC_Base_JawRoot";

    [SerializeField, Tooltip("Automatically resolves the jaw root during Awake if missing.")]
    private bool autoResolveJawRoot = true;

    [SerializeField, Tooltip("Reapplies the neutral jaw pose in LateUpdate so body animation clips cannot leave the mouth open.")]
    private bool enforceNeutralJawPoseInLateUpdate = true;

    [SerializeField, Tooltip("Stored closed-mouth local jaw position.")]
    private Vector3 neutralJawLocalPosition;

    [SerializeField, Tooltip("Stored closed-mouth local jaw rotation.")]
    private Quaternion neutralJawLocalRotation = Quaternion.identity;

    [SerializeField, Tooltip("True when a neutral jaw pose has been captured or authored.")]
    private bool neutralJawPoseCaptured;

    [Header("Presets")]
    [SerializeField, Tooltip("All expression presets available to this controller.")]
    private List<FacialExpressionPreset> presets = new List<FacialExpressionPreset>();

    [SerializeField, Tooltip("Passive expression applied on Start.")]
    private FacialEmotion initialPassiveEmotion = FacialEmotion.Idle;

    [SerializeField, Tooltip("Apply the initial passive expression on Start.")]
    private bool playInitialPassiveOnStart = true;

    [Header("Runtime")]
    [SerializeField, Tooltip("Use unscaled time for facial transitions.")]
    private bool useUnscaledTime;

    [SerializeField, Tooltip("Reapplies controlled BlendShapes in LateUpdate so body animation clips cannot overwrite the face.")]
    private bool enforceControlledWeightsInLateUpdate = true;

    [SerializeField, Tooltip("Logs important state changes and validation details.")]
    private bool verboseLogging = true;

    private readonly Dictionary<string, int> blendShapeCache = new Dictionary<string, int>();
    private readonly Dictionary<string, float> currentWeights = new Dictionary<string, float>();
    private readonly Dictionary<FacialEmotion, FacialExpressionPreset> presetCache = new Dictionary<FacialEmotion, FacialExpressionPreset>();
    private readonly HashSet<string> controlledBlendShapes = new HashSet<string>();
    private readonly List<string> availableBlendShapeNames = new List<string>();

    private FacialExpressionPreset currentPassivePreset;
    private FacialExpressionPreset currentOneShotPreset;
    private FacialEmotion currentPassiveEmotion = FacialEmotion.Idle;
    private Coroutine activeRoutine;
    private SkinnedMeshRenderer cachedRenderer;
    private Mesh cachedMesh;
    private bool blendShapeCacheBuilt;

    public FacialEmotion CurrentPassiveEmotion
    {
        get { return currentPassiveEmotion; }
    }

    public FacialEmotion? CurrentOneShotEmotion
    {
        get { return currentOneShotPreset != null ? currentOneShotPreset.emotion : (FacialEmotion?)null; }
    }

    public bool IsPlayingOneShot
    {
        get { return currentOneShotPreset != null; }
    }

    private void Awake()
    {
        Initialize();
        if (!neutralJawPoseCaptured)
        {
            CaptureNeutralJawPose();
        }
    }

    private void Start()
    {
        if (playInitialPassiveOnStart)
        {
            SetPassiveEmotion(initialPassiveEmotion);
        }
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        currentOneShotPreset = null;
    }

    private void LateUpdate()
    {
        if (enforceControlledWeightsInLateUpdate && blendShapeCacheBuilt && faceRenderer != null)
        {
            foreach (string blendShapeName in controlledBlendShapes)
            {
                float weight;
                if (!currentWeights.TryGetValue(blendShapeName, out weight))
                {
                    continue;
                }

                SetBlendShapeInternal(blendShapeName, weight);
            }
        }

        if (enforceNeutralJawPoseInLateUpdate)
        {
            ApplyNeutralJawPose();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && faceRenderer == null && autoResolveFaceRenderer)
        {
            faceRenderer = FindFaceRenderer();
        }

        if (!Application.isPlaying && jawRoot == null && autoResolveJawRoot)
        {
            jawRoot = FindTransformByName(jawRootName);
        }
    }

    /// <summary>
    /// Plays an expression by emotion. Passive presets become the held passive state.
    /// Active presets interrupt the current one-shot and play immediately.
    /// </summary>
    public void PlayEmotion(FacialEmotion emotion)
    {
        Initialize();

        FacialExpressionPreset preset;
        if (!TryGetPreset(emotion, out preset))
        {
            Debug.LogWarning("[Facial] No preset found for emotion: " + emotion, this);
            return;
        }

        if (preset.mode == FacialExpressionMode.PassiveLoop)
        {
            SetPassiveEmotion(emotion);
            return;
        }

        PlayOneShot(preset);
    }

    /// <summary>
    /// Sets the current passive expression. If a one-shot is playing, the passive state is remembered
    /// and will be used when the one-shot returns to the previous passive expression.
    /// </summary>
    public void SetPassiveEmotion(FacialEmotion emotion)
    {
        Initialize();

        FacialExpressionPreset preset;
        if (!TryGetPreset(emotion, out preset))
        {
            if (emotion != FacialEmotion.Idle)
            {
                Debug.LogWarning("[Facial] No passive preset found for emotion: " + emotion, this);
                return;
            }
        }
        else if (preset.mode != FacialExpressionMode.PassiveLoop)
        {
            Debug.LogWarning("[Facial] SetPassiveEmotion was called with an ActiveOneShot preset: " + emotion, this);
            return;
        }

        currentPassiveEmotion = emotion;
        currentPassivePreset = preset;

        if (currentOneShotPreset != null)
        {
            return;
        }

        StartTransitionToPassive(preset);
    }

    /// <summary>
    /// Stops any active expression and fades back to Idle.
    /// </summary>
    public void ReturnToIdle()
    {
        Initialize();

        StopActiveRoutine();
        currentOneShotPreset = null;
        currentPassiveEmotion = FacialEmotion.Idle;
        TryGetPreset(FacialEmotion.Idle, out currentPassivePreset);
        StartTransitionToPassive(currentPassivePreset);
    }

    /// <summary>
    /// Immediately gives movement/gameplay control back to a neutral face.
    /// Safe to call repeatedly: if the controller is already idle and no one-shot is playing, it does nothing.
    /// </summary>
    public void ForceIdleExpression(float fadeDuration = 0.1f)
    {
        Initialize();

        if (currentPassiveEmotion == FacialEmotion.Idle && currentOneShotPreset == null)
        {
            return;
        }

        StopActiveRoutine();
        currentOneShotPreset = null;
        currentPassiveEmotion = FacialEmotion.Idle;
        TryGetPreset(FacialEmotion.Idle, out currentPassivePreset);
        StartTransitionToPassive(currentPassivePreset, Mathf.Max(0f, fadeDuration));
    }

    /// <summary>
    /// Stops the current one-shot and returns according to that one-shot's return policy.
    /// </summary>
    public void StopCurrentOneShot()
    {
        Initialize();

        if (currentOneShotPreset == null)
        {
            return;
        }

        FacialExpressionPreset interruptedPreset = currentOneShotPreset;
        StopActiveRoutine();
        currentOneShotPreset = null;

        FacialExpressionPreset returnPreset = ResolveReturnPreset(interruptedPreset);
        StartTransitionToPassive(returnPreset, interruptedPreset.fadeOutDuration);
    }

    /// <summary>
    /// Directly sets one BlendShape weight using the cached BlendShape dictionary.
    /// </summary>
    public void SetBlendShape(string name, float value)
    {
        Initialize();

        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogWarning("[Facial] SetBlendShape called with an empty BlendShape name.", this);
            return;
        }

        int index;
        if (!blendShapeCache.TryGetValue(name, out index))
        {
            Debug.LogWarning("[Facial] Missing BlendShape: " + name, this);
            return;
        }

        float clampedValue = Mathf.Clamp(value, 0f, 100f);
        faceRenderer.SetBlendShapeWeight(index, clampedValue);
        currentWeights[name] = clampedValue;
    }

    [ContextMenu("Print Available BlendShapes")]
    public void PrintAvailableBlendShapes()
    {
        Initialize();

        if (availableBlendShapeNames.Count == 0)
        {
            Debug.LogWarning("[Facial] No BlendShapes found on the configured face renderer.", this);
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("[Facial] Available BlendShapes on " + faceRenderer.name + ":");
        for (int i = 0; i < availableBlendShapeNames.Count; i++)
        {
            builder.AppendLine(i.ToString("000") + " : " + availableBlendShapeNames[i]);
        }

        Debug.Log(builder.ToString(), this);
    }

    [ContextMenu("Validate Presets")]
    public void ValidatePresets()
    {
        Initialize();

        HashSet<FacialEmotion> seenEmotions = new HashSet<FacialEmotion>();
        bool hasWarnings = false;

        if (presets == null || presets.Count == 0)
        {
            Debug.LogWarning("[Facial] No facial presets assigned to " + name + ".", this);
            return;
        }

        for (int i = 0; i < presets.Count; i++)
        {
            FacialExpressionPreset preset = presets[i];
            if (preset == null)
            {
                Debug.LogWarning("[Facial] Null preset at index " + i + ".", this);
                hasWarnings = true;
                continue;
            }

            if (!seenEmotions.Add(preset.emotion))
            {
                Debug.LogWarning("[Facial] Duplicate preset for emotion: " + preset.emotion + "\nPreset: " + preset.name, this);
                hasWarnings = true;
            }

            if (preset.blendShapes == null)
            {
                continue;
            }

            HashSet<string> presetNames = new HashSet<string>();
            for (int blendShapeIndex = 0; blendShapeIndex < preset.blendShapes.Count; blendShapeIndex++)
            {
                FacialBlendShapeWeight entry = preset.blendShapes[blendShapeIndex];
                if (entry == null || string.IsNullOrWhiteSpace(entry.blendShapeName))
                {
                    Debug.LogWarning("[Facial] Empty BlendShape entry.\nPreset :\n" + preset.emotion, preset);
                    hasWarnings = true;
                    continue;
                }

                if (!presetNames.Add(entry.blendShapeName))
                {
                    Debug.LogWarning("[Facial] Duplicate BlendShape in preset:\n" + entry.blendShapeName + "\n\nPreset :\n" + preset.emotion, preset);
                    hasWarnings = true;
                }

                if (!blendShapeCache.ContainsKey(entry.blendShapeName))
                {
                    Debug.LogWarning("[Facial] Missing BlendShape :\n" + entry.blendShapeName + "\n\nPreset :\n" + preset.emotion, preset);
                    hasWarnings = true;
                }
            }
        }

        if (!hasWarnings)
        {
            Debug.Log("[Facial] Preset validation passed for " + presets.Count + " presets.", this);
        }
    }

    public IReadOnlyList<string> GetAvailableBlendShapeNames()
    {
        Initialize();
        return availableBlendShapeNames;
    }

    public SkinnedMeshRenderer GetFaceRenderer()
    {
        Initialize();
        return faceRenderer;
    }

    private void Initialize()
    {
        if (faceRenderer == null && autoResolveFaceRenderer)
        {
            faceRenderer = FindFaceRenderer();
        }

        if (jawRoot == null && autoResolveJawRoot)
        {
            jawRoot = FindTransformByName(jawRootName);
        }

        Mesh currentMesh = faceRenderer != null ? faceRenderer.sharedMesh : null;
        if (!blendShapeCacheBuilt || cachedRenderer != faceRenderer || cachedMesh != currentMesh)
        {
            BuildBlendShapeCache();
        }

        BuildPresetCache();
        RebuildControlledBlendShapes();
    }

    private SkinnedMeshRenderer FindFaceRenderer()
    {
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredRendererName))
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].name == preferredRendererName)
                {
                    return renderers[i];
                }
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer rendererCandidate = renderers[i];
            if (rendererCandidate != null && rendererCandidate.sharedMesh != null && rendererCandidate.sharedMesh.blendShapeCount > 0)
            {
                return rendererCandidate;
            }
        }

        return renderers[0];
    }

    [ContextMenu("Capture Neutral Jaw Pose")]
    private void CaptureNeutralJawPose()
    {
        if (jawRoot == null && autoResolveJawRoot)
        {
            jawRoot = FindTransformByName(jawRootName);
        }

        if (jawRoot == null)
        {
            neutralJawPoseCaptured = false;
            return;
        }

        neutralJawLocalPosition = jawRoot.localPosition;
        neutralJawLocalRotation = jawRoot.localRotation;
        neutralJawPoseCaptured = true;
    }

    [ContextMenu("Apply Neutral Jaw Pose")]
    private void ApplyNeutralJawPose()
    {
        if (jawRoot == null && autoResolveJawRoot)
        {
            jawRoot = FindTransformByName(jawRootName);
        }

        if (!neutralJawPoseCaptured)
        {
            CaptureNeutralJawPose();
        }

        if (!neutralJawPoseCaptured || jawRoot == null)
        {
            return;
        }

        jawRoot.localPosition = neutralJawLocalPosition;
        jawRoot.localRotation = neutralJawLocalRotation;
    }

    private Transform FindTransformByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == targetName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private void BuildBlendShapeCache()
    {
        blendShapeCache.Clear();
        currentWeights.Clear();
        availableBlendShapeNames.Clear();
        blendShapeCacheBuilt = true;
        cachedRenderer = faceRenderer;
        cachedMesh = faceRenderer != null ? faceRenderer.sharedMesh : null;

        if (faceRenderer == null || faceRenderer.sharedMesh == null)
        {
            Debug.LogWarning("[Facial] Face renderer or shared mesh is missing.", this);
            return;
        }

        Mesh mesh = faceRenderer.sharedMesh;
        int count = mesh.blendShapeCount;
        for (int i = 0; i < count; i++)
        {
            string blendShapeName = mesh.GetBlendShapeName(i);
            if (string.IsNullOrEmpty(blendShapeName))
            {
                continue;
            }

            if (blendShapeCache.ContainsKey(blendShapeName))
            {
                Debug.LogWarning("[Facial] Duplicate BlendShape name found on mesh: " + blendShapeName, this);
                continue;
            }

            blendShapeCache.Add(blendShapeName, i);
            availableBlendShapeNames.Add(blendShapeName);

            float currentValue = faceRenderer.GetBlendShapeWeight(i);
            currentWeights[blendShapeName] = currentValue;
        }
    }

    private void BuildPresetCache()
    {
        presetCache.Clear();

        if (presets == null)
        {
            return;
        }

        for (int i = 0; i < presets.Count; i++)
        {
            FacialExpressionPreset preset = presets[i];
            if (preset == null)
            {
                continue;
            }

            if (!presetCache.ContainsKey(preset.emotion))
            {
                presetCache.Add(preset.emotion, preset);
            }
        }
    }

    private void RebuildControlledBlendShapes()
    {
        controlledBlendShapes.Clear();

        if (presets == null)
        {
            return;
        }

        for (int i = 0; i < presets.Count; i++)
        {
            FacialExpressionPreset preset = presets[i];
            if (preset == null || preset.blendShapes == null)
            {
                continue;
            }

            for (int blendShapeIndex = 0; blendShapeIndex < preset.blendShapes.Count; blendShapeIndex++)
            {
                FacialBlendShapeWeight entry = preset.blendShapes[blendShapeIndex];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.blendShapeName))
                {
                    controlledBlendShapes.Add(entry.blendShapeName);
                }
            }
        }
    }

    private bool TryGetPreset(FacialEmotion emotion, out FacialExpressionPreset preset)
    {
        if (presetCache.TryGetValue(emotion, out preset))
        {
            return true;
        }

        preset = null;
        return false;
    }

    private void PlayOneShot(FacialExpressionPreset preset)
    {
        StopActiveRoutine();

        if (!Application.isPlaying)
        {
            ApplyWeights(BuildTargetWeights(preset));
            currentOneShotPreset = null;

            if (verboseLogging)
            {
                Debug.Log("[Facial] Previewing one-shot emotion in edit mode: " + preset.emotion, this);
            }

            return;
        }

        currentOneShotPreset = preset;
        activeRoutine = StartCoroutine(PlayOneShotRoutine(preset));

        if (verboseLogging)
        {
            Debug.Log("[Facial] Playing one-shot emotion: " + preset.emotion, this);
        }
    }

    private IEnumerator PlayOneShotRoutine(FacialExpressionPreset preset)
    {
        Dictionary<string, float> targetWeights = BuildTargetWeights(preset);

        yield return FadeToTarget(targetWeights, preset.fadeInDuration);
        yield return WaitDuration(preset.holdDuration);

        FacialExpressionPreset returnPreset = ResolveReturnPreset(preset);
        Dictionary<string, float> returnWeights = BuildTargetWeights(returnPreset);

        yield return FadeToTarget(returnWeights, preset.fadeOutDuration);

        currentOneShotPreset = null;
        activeRoutine = null;
    }

    private FacialExpressionPreset ResolveReturnPreset(FacialExpressionPreset oneShotPreset)
    {
        if (oneShotPreset != null && oneShotPreset.returnToPreviousPassiveExpression && currentPassivePreset != null)
        {
            return currentPassivePreset;
        }

        currentPassiveEmotion = FacialEmotion.Idle;
        TryGetPreset(FacialEmotion.Idle, out currentPassivePreset);
        return currentPassivePreset;
    }

    private void StartTransitionToPassive(FacialExpressionPreset preset)
    {
        float duration = preset != null ? preset.fadeInDuration : 0.15f;
        StartTransitionToPassive(preset, duration);
    }

    private void StartTransitionToPassive(FacialExpressionPreset preset, float duration)
    {
        StopActiveRoutine();

        if (!Application.isPlaying)
        {
            ApplyWeights(BuildTargetWeights(preset));

            if (verboseLogging)
            {
                Debug.Log("[Facial] Previewing passive emotion in edit mode: " + currentPassiveEmotion, this);
            }

            return;
        }

        activeRoutine = StartCoroutine(TransitionToPassiveRoutine(preset, duration));

        if (verboseLogging)
        {
            Debug.Log("[Facial] Setting passive emotion: " + currentPassiveEmotion, this);
        }
    }

    private IEnumerator TransitionToPassiveRoutine(FacialExpressionPreset preset, float duration)
    {
        yield return FadeToTarget(BuildTargetWeights(preset), duration);
        activeRoutine = null;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    private Dictionary<string, float> BuildTargetWeights(FacialExpressionPreset preset)
    {
        Dictionary<string, float> targetWeights = new Dictionary<string, float>();

        foreach (string blendShapeName in controlledBlendShapes)
        {
            if (string.IsNullOrWhiteSpace(blendShapeName) || !blendShapeCache.ContainsKey(blendShapeName))
            {
                continue;
            }

            targetWeights[blendShapeName] = 0f;
        }

        if (preset == null || preset.blendShapes == null)
        {
            return targetWeights;
        }

        for (int i = 0; i < preset.blendShapes.Count; i++)
        {
            FacialBlendShapeWeight entry = preset.blendShapes[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.blendShapeName) || !blendShapeCache.ContainsKey(entry.blendShapeName))
            {
                continue;
            }

            targetWeights[entry.blendShapeName] = Mathf.Clamp(entry.weight, 0f, 100f);
        }

        return targetWeights;
    }

    private IEnumerator FadeToTarget(Dictionary<string, float> targetWeights, float duration)
    {
        Dictionary<string, float> startWeights = CaptureCurrentWeights(targetWeights);

        if (duration <= 0f)
        {
            ApplyWeights(targetWeights);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            foreach (KeyValuePair<string, float> targetWeight in targetWeights)
            {
                float startValue;
                startWeights.TryGetValue(targetWeight.Key, out startValue);
                SetBlendShapeInternal(targetWeight.Key, Mathf.Lerp(startValue, targetWeight.Value, easedT));
            }

            yield return null;
        }

        ApplyWeights(targetWeights);
    }

    private Dictionary<string, float> CaptureCurrentWeights(Dictionary<string, float> targetWeights)
    {
        Dictionary<string, float> capturedWeights = new Dictionary<string, float>();

        foreach (KeyValuePair<string, float> targetWeight in targetWeights)
        {
            int index;
            if (!blendShapeCache.TryGetValue(targetWeight.Key, out index))
            {
                continue;
            }

            float currentValue = faceRenderer.GetBlendShapeWeight(index);
            capturedWeights[targetWeight.Key] = currentValue;
            currentWeights[targetWeight.Key] = currentValue;
        }

        return capturedWeights;
    }

    private void ApplyWeights(Dictionary<string, float> weights)
    {
        foreach (KeyValuePair<string, float> weight in weights)
        {
            SetBlendShapeInternal(weight.Key, weight.Value);
        }
    }

    private void SetBlendShapeInternal(string blendShapeName, float weight)
    {
        int index;
        if (!blendShapeCache.TryGetValue(blendShapeName, out index))
        {
            return;
        }

        float clampedWeight = Mathf.Clamp(weight, 0f, 100f);
        faceRenderer.SetBlendShapeWeight(index, clampedWeight);
        currentWeights[blendShapeName] = clampedWeight;
    }

    private IEnumerator WaitDuration(float duration)
    {
        if (duration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            yield return null;
        }
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
