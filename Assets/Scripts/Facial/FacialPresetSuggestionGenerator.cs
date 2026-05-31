using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Scans real BlendShape names and proposes starter facial presets with nomenclature-agnostic heuristics.
/// </summary>
[DisallowMultipleComponent]
public class FacialPresetSuggestionGenerator : MonoBehaviour
{
    private const float DefaultStrongWeight = 70f;
    private const float DefaultMediumWeight = 50f;
    private const float DefaultSubtleWeight = 30f;

    [Header("Source")]
    [SerializeField, Tooltip("Optional controller used to resolve the face renderer.")]
    private FacialExpressionController controller;

    [SerializeField, Tooltip("Renderer to scan when no controller is assigned.")]
    private SkinnedMeshRenderer faceRenderer;

    [Header("Suggestion Limits")]
    [SerializeField, Min(1), Tooltip("Maximum BlendShapes suggested per emotion.")]
    private int maxBlendShapesPerEmotion = 6;

    [SerializeField, Tooltip("Include lower-confidence candidates in logs and generated assets.")]
    private bool includeLowConfidenceCandidates;

#if UNITY_EDITOR
    [Header("Asset Generation")]
    [SerializeField, Tooltip("Folder where generated preset assets are created.")]
    private string outputFolder = "Assets/ScriptableObjects/Facial";
#endif

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        maxBlendShapesPerEmotion = Mathf.Max(1, maxBlendShapesPerEmotion);
        if (controller == null || faceRenderer == null)
        {
            ResolveReferences();
        }
    }

    [ContextMenu("Print Preset Suggestions")]
    public void PrintPresetSuggestions()
    {
        List<EmotionSuggestion> suggestions = BuildSuggestions();
        if (suggestions.Count == 0)
        {
            Debug.LogWarning("[Facial] No facial preset suggestions could be generated.", this);
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("[Facial] Suggested starter presets:");

        for (int i = 0; i < suggestions.Count; i++)
        {
            EmotionSuggestion suggestion = suggestions[i];
            builder.AppendLine(string.Empty);
            builder.AppendLine(suggestion.emotion + " :");

            if (suggestion.weights.Count == 0)
            {
                builder.AppendLine("No strong candidate found. Keep this preset empty or tune it manually.");
                continue;
            }

            for (int weightIndex = 0; weightIndex < suggestion.weights.Count; weightIndex++)
            {
                FacialBlendShapeWeight weight = suggestion.weights[weightIndex];
                builder.AppendLine(weight.blendShapeName);
                builder.AppendLine("Suggested Weight : " + weight.weight.ToString("0"));
            }
        }

        Debug.Log(builder.ToString(), this);
    }

#if UNITY_EDITOR
    [ContextMenu("Create Suggested Preset Assets")]
    public void CreateSuggestedPresetAssets()
    {
        List<EmotionSuggestion> suggestions = BuildSuggestions();
        if (suggestions.Count == 0)
        {
            Debug.LogWarning("[Facial] No facial preset assets were created because no suggestions were available.", this);
            return;
        }

        string resolvedOutputFolder = EnsureOutputFolder(outputFolder);

        for (int i = 0; i < suggestions.Count; i++)
        {
            EmotionSuggestion suggestion = suggestions[i];
            FacialExpressionPreset preset = ScriptableObject.CreateInstance<FacialExpressionPreset>();
            preset.emotion = suggestion.emotion;
            preset.mode = suggestion.mode;
            preset.fadeInDuration = suggestion.mode == FacialExpressionMode.ActiveOneShot ? 0.12f : 0.2f;
            preset.holdDuration = suggestion.mode == FacialExpressionMode.ActiveOneShot ? 0.45f : 0f;
            preset.fadeOutDuration = suggestion.mode == FacialExpressionMode.ActiveOneShot ? 0.18f : 0.2f;
            preset.returnToPreviousPassiveExpression = suggestion.mode == FacialExpressionMode.ActiveOneShot;
            preset.blendShapes = suggestion.weights;

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(resolvedOutputFolder + "/" + suggestion.emotion + "_FacialPreset.asset");
            AssetDatabase.CreateAsset(preset, assetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Facial] Created " + suggestions.Count + " suggested facial preset assets in " + resolvedOutputFolder + ".", this);
    }
#endif

    public List<EmotionSuggestion> BuildSuggestions()
    {
        ResolveReferences();

        SkinnedMeshRenderer rendererToScan = ResolveRenderer();
        if (rendererToScan == null || rendererToScan.sharedMesh == null)
        {
            Debug.LogWarning("[Facial] Cannot generate suggestions: face renderer or shared mesh is missing.", this);
            return new List<EmotionSuggestion>();
        }

        List<BlendShapeCandidate> candidates = ScanBlendShapes(rendererToScan.sharedMesh);
        List<EmotionSuggestion> suggestions = new List<EmotionSuggestion>();
        suggestions.Add(BuildSuggestion(FacialEmotion.Idle, FacialExpressionMode.PassiveLoop, candidates));
        suggestions.Add(BuildSuggestion(FacialEmotion.Fear, FacialExpressionMode.ActiveOneShot, candidates));
        suggestions.Add(BuildSuggestion(FacialEmotion.Anger, FacialExpressionMode.ActiveOneShot, candidates));
        suggestions.Add(BuildSuggestion(FacialEmotion.Laugh, FacialExpressionMode.ActiveOneShot, candidates));
        suggestions.Add(BuildSuggestion(FacialEmotion.Surprise, FacialExpressionMode.ActiveOneShot, candidates));
        suggestions.Add(BuildSuggestion(FacialEmotion.Smirk, FacialExpressionMode.PassiveLoop, candidates));
        suggestions.Add(BuildSuggestion(FacialEmotion.Suspicious, FacialExpressionMode.PassiveLoop, candidates));
        suggestions.Add(BuildSuggestion(FacialEmotion.HalfSmile, FacialExpressionMode.PassiveLoop, candidates));
        return suggestions;
    }

    private EmotionSuggestion BuildSuggestion(FacialEmotion emotion, FacialExpressionMode mode, List<BlendShapeCandidate> candidates)
    {
        EmotionSuggestion suggestion = new EmotionSuggestion(emotion, mode);
        if (emotion == FacialEmotion.Idle)
        {
            return suggestion;
        }

        List<ScoredBlendShape> scoredBlendShapes = new List<ScoredBlendShape>();
        for (int i = 0; i < candidates.Count; i++)
        {
            BlendShapeCandidate candidate = candidates[i];
            float score = ScoreCandidate(emotion, candidate);
            if (score >= 3f || includeLowConfidenceCandidates && score >= 2f)
            {
                scoredBlendShapes.Add(new ScoredBlendShape(candidate.name, score, SuggestedWeight(emotion, candidate, score)));
            }
        }

        scoredBlendShapes.Sort(CompareScoredBlendShapes);

        HashSet<string> usedNames = new HashSet<string>();
        int count = Mathf.Min(maxBlendShapesPerEmotion, scoredBlendShapes.Count);
        for (int i = 0; i < scoredBlendShapes.Count && suggestion.weights.Count < count; i++)
        {
            ScoredBlendShape scored = scoredBlendShapes[i];
            if (!usedNames.Add(scored.name))
            {
                continue;
            }

            suggestion.weights.Add(new FacialBlendShapeWeight
            {
                blendShapeName = scored.name,
                weight = scored.weight
            });
        }

        return suggestion;
    }

    private float ScoreCandidate(FacialEmotion emotion, BlendShapeCandidate candidate)
    {
        switch (emotion)
        {
            case FacialEmotion.Fear:
                return ScoreFear(candidate);
            case FacialEmotion.Anger:
                return ScoreAnger(candidate);
            case FacialEmotion.Laugh:
                return ScoreLaugh(candidate);
            case FacialEmotion.Surprise:
                return ScoreSurprise(candidate);
            case FacialEmotion.Smirk:
                return ScoreSmirk(candidate);
            case FacialEmotion.Suspicious:
                return ScoreSuspicious(candidate);
            case FacialEmotion.HalfSmile:
                return ScoreHalfSmile(candidate);
            default:
                return 0f;
        }
    }

    private float ScoreFear(BlendShapeCandidate candidate)
    {
        float score = 0f;
        score += ScoreAny(candidate, 2f, "eye", "eyes");
        score += ScoreAny(candidate, 3f, "widen", "wide", "open");
        score += ScoreAny(candidate, 2f, "brow", "eyebrow");
        score += ScoreAny(candidate, 2f, "raise", "up", "inner");
        score += ScoreAny(candidate, 1.5f, "jaw", "mouth");
        score += ScoreAny(candidate, 1.5f, "open", "drop");
        score -= ScoreAny(candidate, 2f, "blink", "close", "squint");
        return score;
    }

    private float ScoreAnger(BlendShapeCandidate candidate)
    {
        float score = 0f;
        score += ScoreAny(candidate, 2.5f, "brow", "eyebrow");
        score += ScoreAny(candidate, 3f, "down", "lower", "furrow", "frown", "angry");
        score += ScoreAny(candidate, 2f, "squint", "narrow");
        score += ScoreAny(candidate, 1.5f, "nose", "wrinkle");
        score += ScoreAny(candidate, 1.5f, "lip", "mouth", "jaw");
        score += ScoreAny(candidate, 1.5f, "press", "tight", "clench");
        score -= ScoreAny(candidate, 2f, "smile", "grin", "laugh");
        return score;
    }

    private float ScoreLaugh(BlendShapeCandidate candidate)
    {
        float score = 0f;
        score += ScoreAny(candidate, 3.5f, "smile", "grin", "laugh", "happy");
        score += ScoreAny(candidate, 2f, "cheek");
        score += ScoreAny(candidate, 2f, "raise", "up");
        score += ScoreAny(candidate, 1.5f, "eye", "eyes");
        score += ScoreAny(candidate, 2f, "squint", "narrow");
        score += ScoreAny(candidate, 2f, "mouth", "jaw");
        score += ScoreAny(candidate, 1.5f, "open");
        score -= ScoreAny(candidate, 2f, "frown", "sad", "down");
        return score;
    }

    private float ScoreSurprise(BlendShapeCandidate candidate)
    {
        float score = 0f;
        score += ScoreAny(candidate, 2f, "eye", "eyes");
        score += ScoreAny(candidate, 3f, "widen", "wide", "open");
        score += ScoreAny(candidate, 2f, "brow", "eyebrow");
        score += ScoreAny(candidate, 2.5f, "raise", "up");
        score += ScoreAny(candidate, 2f, "jaw", "mouth");
        score += ScoreAny(candidate, 2.5f, "open", "drop");
        score -= ScoreAny(candidate, 2f, "squint", "blink", "close");
        return score;
    }

    private float ScoreSmirk(BlendShapeCandidate candidate)
    {
        float score = 0f;
        score += ScoreAny(candidate, 3f, "smirk");
        score += ScoreAny(candidate, 2.5f, "smile", "corner");
        score += ScoreAny(candidate, 2f, "left", "right", "l", "r");
        score += ScoreAny(candidate, 1.5f, "mouth", "lip", "cheek");
        score += ScoreAny(candidate, 1.5f, "up", "raise");
        score -= ScoreAny(candidate, 2f, "both", "wide", "open", "jaw");
        return score;
    }

    private float ScoreSuspicious(BlendShapeCandidate candidate)
    {
        float score = 0f;
        score += ScoreAny(candidate, 2.5f, "brow", "eyebrow");
        score += ScoreAny(candidate, 2f, "raise", "up", "down", "lower", "inner", "outer");
        score += ScoreAny(candidate, 2f, "eye", "eyes");
        score += ScoreAny(candidate, 2f, "squint", "narrow");
        score += ScoreAny(candidate, 1.5f, "mouth", "lip", "corner");
        score += ScoreAny(candidate, 1f, "press", "tight");
        score -= ScoreAny(candidate, 2f, "smile", "laugh", "wide", "jaw");
        return score;
    }

    private float ScoreHalfSmile(BlendShapeCandidate candidate)
    {
        float score = 0f;
        score += ScoreAny(candidate, 2.5f, "smile");
        score += ScoreAny(candidate, 2f, "corner", "mouth", "lip", "cheek");
        score += ScoreAny(candidate, 2f, "left", "right", "l", "r");
        score += ScoreAny(candidate, 1.5f, "up", "raise");
        score -= ScoreAny(candidate, 1.5f, "wide", "open", "jaw", "grin");
        return score;
    }

    private float SuggestedWeight(FacialEmotion emotion, BlendShapeCandidate candidate, float score)
    {
        if (emotion == FacialEmotion.Smirk || emotion == FacialEmotion.Suspicious || emotion == FacialEmotion.HalfSmile)
        {
            return candidate.HasAny("squint", "brow", "mouth", "smile") ? DefaultSubtleWeight : 20f;
        }

        if (score >= 6f)
        {
            return DefaultStrongWeight;
        }

        return score >= 4f ? DefaultMediumWeight : DefaultSubtleWeight;
    }

    private static float ScoreAny(BlendShapeCandidate candidate, float score, params string[] tokens)
    {
        return candidate.HasAny(tokens) ? score : 0f;
    }

    private static int CompareScoredBlendShapes(ScoredBlendShape left, ScoredBlendShape right)
    {
        int scoreComparison = right.score.CompareTo(left.score);
        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        return string.Compare(left.name, right.name, StringComparison.Ordinal);
    }

    private List<BlendShapeCandidate> ScanBlendShapes(Mesh mesh)
    {
        List<BlendShapeCandidate> candidates = new List<BlendShapeCandidate>();
        int blendShapeCount = mesh.blendShapeCount;

        for (int i = 0; i < blendShapeCount; i++)
        {
            string blendShapeName = mesh.GetBlendShapeName(i);
            if (!string.IsNullOrWhiteSpace(blendShapeName))
            {
                candidates.Add(new BlendShapeCandidate(blendShapeName));
            }
        }

        return candidates;
    }

    private SkinnedMeshRenderer ResolveRenderer()
    {
        if (faceRenderer != null)
        {
            return faceRenderer;
        }

        if (controller != null)
        {
            faceRenderer = controller.GetFaceRenderer();
            if (faceRenderer != null)
            {
                return faceRenderer;
            }
        }

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sharedMesh != null && renderers[i].sharedMesh.blendShapeCount > 0)
            {
                faceRenderer = renderers[i];
                return faceRenderer;
            }
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = GetComponent<FacialExpressionController>();
            if (controller == null)
            {
                controller = GetComponentInParent<FacialExpressionController>();
            }
            if (controller == null)
            {
                controller = GetComponentInChildren<FacialExpressionController>();
            }
        }

        if (faceRenderer == null)
        {
            ResolveRenderer();
        }
    }

#if UNITY_EDITOR
    private static string EnsureOutputFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !folderPath.StartsWith("Assets", StringComparison.Ordinal))
        {
            Debug.LogWarning("[Facial] Invalid facial preset output folder. Falling back to Assets/ScriptableObjects/Facial.");
            folderPath = "Assets/ScriptableObjects/Facial";
        }

        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return folderPath;
        }

        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }
            currentPath = nextPath;
        }

        return folderPath;
    }
#endif

    [Serializable]
    public class EmotionSuggestion
    {
        public FacialEmotion emotion;
        public FacialExpressionMode mode;
        public List<FacialBlendShapeWeight> weights = new List<FacialBlendShapeWeight>();

        public EmotionSuggestion(FacialEmotion emotion, FacialExpressionMode mode)
        {
            this.emotion = emotion;
            this.mode = mode;
        }
    }

    private class ScoredBlendShape
    {
        public readonly string name;
        public readonly float score;
        public readonly float weight;

        public ScoredBlendShape(string name, float score, float weight)
        {
            this.name = name;
            this.score = score;
            this.weight = weight;
        }
    }

    private class BlendShapeCandidate
    {
        public readonly string name;
        private readonly string normalizedName;
        private readonly HashSet<string> tokens = new HashSet<string>();

        public BlendShapeCandidate(string name)
        {
            this.name = name;
            normalizedName = Normalize(name);
            Tokenize(normalizedName, tokens);
        }

        public bool HasAny(params string[] queryTokens)
        {
            for (int i = 0; i < queryTokens.Length; i++)
            {
                string query = queryTokens[i];
                if (tokens.Contains(query))
                {
                    return true;
                }

                if (query.Length > 1 && normalizedName.Contains(query))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.ToLowerInvariant()
                .Replace("blendshape.", string.Empty)
                .Replace("blendshape", string.Empty)
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace(".", " ")
                .Replace("/", " ")
                .Replace("\\", " ");
        }

        private static void Tokenize(string value, HashSet<string> output)
        {
            string[] splitTokens = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < splitTokens.Length; i++)
            {
                string token = splitTokens[i].Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                output.Add(token);
                if (token.EndsWith("left", StringComparison.Ordinal))
                {
                    output.Add("left");
                    output.Add("l");
                }
                else if (token.EndsWith("right", StringComparison.Ordinal))
                {
                    output.Add("right");
                    output.Add("r");
                }
            }
        }
    }
}
