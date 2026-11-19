using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueIntent { Automatic, Neutral, Explain, Question, Firm, Concerned, Amused, Surprised }
public enum DialogueRole { Speaking, Listening, Waiting }

[Serializable]
public sealed class DialogueDirection
{
    public DialogueIntent intent;
    [Range(0.25f, 1f)] public float intensity = 1f;
    [Tooltip("Optional stable gesture ID from the character's dialogue profile.")]
    public string gestureId;
}

[CreateAssetMenu(menuName = "Lit/Animation/Dialogue Presentation Profile")]
public sealed class DialoguePresentationProfile : ScriptableObject
{
    [Serializable]
    public sealed class Gesture
    {
        public string id;
        public AnimationClip source;
        public long sourceId;
        public AnimationClip body;
        public AnimationClip upperBody;
        public DialogueRole role;
        public DialogueIntent intent;
        public bool seated;
        public bool standing = true;
        [Min(0f)] public float weight = 1f;
        [Range(0f, 1f)] public float startNormalizedTime;
        public bool validated;
        [TextArea] public string validation;
        public FacialExpressionPreset expression;
    }

    [Serializable]
    public sealed class IntentExpression
    {
        public DialogueIntent intent;
        public FacialExpressionPreset preset;
    }
    public List<IntentExpression> expressions = new List<IntentExpression>();

    public FacialExpressionPreset ExpressionFor(DialogueIntent intent, Gesture gesture)
    {
        if (intent == DialogueIntent.Automatic) intent = defaultIntent;
        var configured = expressions.Find(entry => entry.intent == intent);
        return configured != null && configured.preset != null ? configured.preset : gesture.expression;
    }

    [Min(.05f)] public float blendSeconds = .25f;
    [Min(.1f)] public float textGestureSeconds = 2.5f;
    [Min(.1f)] public float speechVariationSeconds = 5f;
    [Min(1f)] public float facingDegreesPerSecond = 100f;
    [Range(0f, 1f)] public float headLookWeight = .65f;
    public DialogueIntent defaultIntent = DialogueIntent.Explain;
    public List<Gesture> gestures = new List<Gesture>();

    public int Choose(DialogueRole role, DialogueIntent intent, bool seated, string requestedId, int previous, uint seed)
    {
        if (intent == DialogueIntent.Automatic) intent = defaultIntent;
        var candidates = new List<int>();
        for (int i = 0; i < gestures.Count; i++)
        {
            var g = gestures[i];
            if (!g.validated || g.upperBody == null || g.role != role || (seated ? !g.seated : !g.standing))
                continue;
            if (!string.IsNullOrEmpty(requestedId) && g.id == requestedId) return i;
            candidates.Add(i);
        }
        var directed = candidates.FindAll(i => gestures[i].intent == intent);
        if (directed.Count > 0 && !(directed.Count == 1 && directed[0] == previous && candidates.Count > 1))
            candidates = directed;
        else if (role == DialogueRole.Speaking)
        {
            var general = candidates.FindAll(i => gestures[i].intent == DialogueIntent.Neutral ||
                gestures[i].intent == DialogueIntent.Explain || gestures[i].intent == DialogueIntent.Automatic);
            if (general.Count > 0) candidates = general;
        }
        if (candidates.Count > 1) candidates.Remove(previous);
        float total = 0;
        foreach (int i in candidates) total += Mathf.Max(0, gestures[i].weight);
        if (total <= 0) return -1;
        float selection = (seed % 100000u) / 100000f * total;
        foreach (int i in candidates)
        {
            selection -= Mathf.Max(0, gestures[i].weight);
            if (selection < 0) return i;
        }
        return candidates[candidates.Count - 1];
    }
}
